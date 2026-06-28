using Azure.AI.OpenAI;
using Azure.Identity;
using HrMcp.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OllamaSharp;

namespace HrMcp.Agent.Web.Services;

public interface IAgentDraftService
{
    Task<string> SendPromptAsync(string prompt, Guid? sessionId = null, CancellationToken ct = default);
    Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(string draftText, CancellationToken ct = default);
}

public sealed class AgentDraftService : IAgentDraftService, IAsyncDisposable
{
    private const int DefaultExportContextPositionId = 1;

    private readonly IConversationService _conversationService;
    private readonly UserContext _userContext;
    private HrAgent? _agent;
    private McpClient? _mcpClient;
    private IConfiguration? _configuration;

    public AgentDraftService(IConversationService conversationService, UserContext userContext)
    {
        _conversationService = conversationService;
        _userContext = userContext;
    }

    public async Task<string> SendPromptAsync(string prompt, Guid? sessionId = null, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        if (sessionId.HasValue)
        {
            var userId = await _userContext.GetUserIdAsync() ?? "dev-user";
            var session = await _conversationService.GetSessionAsync(sessionId.Value, userId, ct);
            if (session is not null && session.Turns.Count > 0)
            {
                var priorMessages = session.Turns
                    .OrderBy(t => t.Timestamp)
                    .Select(t => new ChatMessage(
                        string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase)
                            ? ChatRole.User
                            : ChatRole.Assistant,
                        t.Text))
                    .ToList();
                _agent!.ResetHistory(priorMessages);
            }
        }

        return await _agent!.AskAsync(prompt, ct);
    }

    public async Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(string draftText, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var request =
            $"Export this draft to Word by calling ExportDraftToWord with positionId={DefaultExportContextPositionId}. Keep content unchanged.\n" +
            draftText;
        var message = await _agent!.AskAsync(request, ct);
        return (message, _agent.LastExportedFileName, _agent.LastExportedFileBytes);
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_agent is not null)
            return;

        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
                optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var transportType = ResolveTransportType(_configuration);

        var additionalHeaders = await TryGetOidcHeadersAsync(_configuration, ct);
        var clientTransport = await CreateClientTransportAsync(_configuration, transportType, additionalHeaders);

        using var mcpLoggerFactory = LoggerFactory.Create(b => b
            .AddFilter((category, level) =>
                category?.StartsWith("ModelContextProtocol", StringComparison.Ordinal) == true && level >= LogLevel.Warning)
            .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));

        _mcpClient = await McpClient.CreateAsync(clientTransport, loggerFactory: mcpLoggerFactory);
        var mcpTools = await _mcpClient.ListToolsAsync();

        var chatClient = CreateChatClient(_configuration);
        var numCtx = _configuration.GetValue<int?>("AI:Ollama:NumCtx");

        _agent = new HrAgent(
            chatClient,
            mcpTools.Cast<AITool>().ToList(),
            UiStyle.Minimal,
            numCtx,
            FindOutputFolder());
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcpClient is not null)
        {
            await _mcpClient.DisposeAsync();
        }
    }

    private static string ResolveTransportType(IConfiguration configuration)
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => string.Equals(a, "--stream-http", StringComparison.OrdinalIgnoreCase)))
            return "streamHttp";

        if (args.Any(a => string.Equals(a, "--stdio", StringComparison.OrdinalIgnoreCase)))
            return "stdio";

        return configuration["McpServer:Transport:Type"] ?? "stdio";
    }

    private static async Task<Dictionary<string, string>> TryGetOidcHeadersAsync(
        IConfiguration configuration,
        CancellationToken ct)
    {
        var enableOidc = bool.TryParse(configuration["Features:EnableOidc"], out var oidcFlag) && oidcFlag;
        if (!enableOidc)
            return [];

        var authority = configuration["Oidc:Authority"]
            ?? throw new InvalidOperationException("Missing configuration: Oidc:Authority");
        var clientId = configuration["Oidc:ClientId"]
            ?? throw new InvalidOperationException("Missing configuration: Oidc:ClientId");
        var clientSecret = configuration["Oidc:ClientSecret"]
            ?? throw new InvalidOperationException("Missing configuration: Oidc:ClientSecret");
        var scope = configuration["Oidc:Scope"]
            ?? throw new InvalidOperationException("Missing configuration: Oidc:Scope");

        using var tokenHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var tokenClient = new HttpClient(tokenHandler);

        var tokenResponse = await tokenClient.PostAsync(
            $"{authority.TrimEnd('/')}/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = scope,
            }),
            ct);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenDoc = await tokenResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ct);
        var accessToken = tokenDoc.GetProperty("access_token").GetString()!;

        return new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {accessToken}"
        };
    }

    private static async Task<IClientTransport> CreateClientTransportAsync(
        IConfiguration configuration,
        string transportType,
        Dictionary<string, string> additionalHeaders)
    {
        if (string.Equals(transportType, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            var command = configuration["McpServer:Transport:Stdio:Command"] ?? "dotnet";
            var workingDirectory = configuration["McpServer:Transport:Stdio:WorkingDirectory"];
            if (string.IsNullOrWhiteSpace(workingDirectory))
                workingDirectory = FindWorkspaceRoot();

            var projectPath = configuration["McpServer:Transport:Stdio:ProjectPath"];
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                projectPath = Path.Combine(workingDirectory, "DotnetAiAgentUi", "src", "HrMcp.McpServer", "HrMcp.McpServer.csproj");
            }

            return new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = command,
                Arguments = ["run", "--project", projectPath, "--", "--stdio"],
                WorkingDirectory = workingDirectory,
                Name = "hr-mcp-stdio"
            });
        }

        var mcpServerUrl =
            configuration["McpServer:Transport:StreamHttp:Url"] ??
            configuration["McpServer:Url"] ??
            Environment.GetEnvironmentVariable("HR_MCP_SERVER_URL") ??
            "http://localhost:5100/mcp";

        await WaitForHttpServerAsync(mcpServerUrl);

        var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        return new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(mcpServerUrl),
            AdditionalHeaders = additionalHeaders,
            TransportMode = HttpTransportMode.StreamableHttp,
            Name = "hr-mcp-stream-http"
        }, httpClient, null, ownsHttpClient: true);
    }

    private static async Task WaitForHttpServerAsync(string mcpServerUrl)
    {
        var baseUrl = mcpServerUrl.Replace("/mcp", "").TrimEnd('/') + "/";
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        for (var attempt = 1; attempt <= 15; attempt++)
        {
            try
            {
                _ = await probe.GetAsync(baseUrl);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"MCP server at {mcpServerUrl} did not become available.");
    }

    private static string FindWorkspaceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "DotnetAiAgentMcp")))
                return dir.FullName;
        }

        return AppContext.BaseDirectory;
    }

    private static string FindOutputFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "usajobs", "output");
            if (Directory.Exists(Path.Combine(dir.FullName, "usajobs")))
                return candidate;
        }

        return Path.GetFullPath("usajobs/output");
    }

    private static IChatClient CreateChatClient(IConfiguration configuration)
    {
        var provider = configuration["AI:Provider"] ?? "Ollama";

        if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
            var model = configuration["AI:Ollama:Model"] ?? "llama3.2";

            var httpClient = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = Timeout.InfiniteTimeSpan };
            return (IChatClient)new OllamaApiClient(httpClient, model, null!);
        }

        var azureEndpoint = configuration["AI:AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Missing configuration: AI:AzureOpenAI:Endpoint");
        var azureDeployment = configuration["AI:AzureOpenAI:Deployment"]
            ?? throw new InvalidOperationException("Missing configuration: AI:AzureOpenAI:Deployment");
        var apiKey = configuration["AI:AzureOpenAI:ApiKey"];

        var client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(azureEndpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(azureEndpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        return client.GetChatClient(azureDeployment).AsIChatClient();
    }
}
