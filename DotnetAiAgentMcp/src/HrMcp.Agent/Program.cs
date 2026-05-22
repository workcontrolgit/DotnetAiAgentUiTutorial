// src/HrMcp.Agent/Program.cs
using Azure.AI.OpenAI;
using Azure.Identity;
using HrMcp.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;
using Spectre.Console;
using System.Net.Http.Json;
using System.Text.Json;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var transportType = configuration["McpServer:Transport:Type"] ?? "stdio";

// OIDC feature flag
// Set Features:EnableOidc = false in appsettings.json (or appsettings.Development.json)
// to connect without authentication.
// Set to true to enable the client-credentials token flow.
var enableOidc = bool.TryParse(configuration["Features:EnableOidc"], out var oidcFlag) && oidcFlag;

Dictionary<string, string> additionalHeaders = [];

if (enableOidc)
{
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
        }));
    tokenResponse.EnsureSuccessStatusCode();

    var tokenDoc = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
    var accessToken = tokenDoc.GetProperty("access_token").GetString()!;
    Console.WriteLine("Token acquired.\n");

    additionalHeaders["Authorization"] = $"Bearer {accessToken}";
}

var clientTransport = await CreateClientTransportAsync(configuration, transportType, additionalHeaders);
await using var mcpClient = await McpClient.CreateAsync(clientTransport);

var mcpTools = await mcpClient.ListToolsAsync();
AnsiConsole.MarkupLine($"[green]OK[/] Connected. Tools: [grey]{string.Join(", ", mcpTools.Select(t => t.Name))}[/]\n");

var style = UiStyle.Structured;

AnsiConsole.MarkupLine("[bold]Select UI style:[/]");
AnsiConsole.MarkupLine("  [cyan][[1]][/] Structured - tables, panels, spinners [grey](default)[/]");
AnsiConsole.MarkupLine("  [cyan][[2]][/] Minimal    - rule-separated turns");
AnsiConsole.MarkupLine("  [cyan][[3]][/] Panels     - bordered panel per message");
AnsiConsole.Markup("[grey]Choice [[1]]:[/] ");

try
{
    if (!Console.IsInputRedirected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && !Console.KeyAvailable)
            await Task.Delay(100);

        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            style = key.KeyChar switch
            {
                '2' => UiStyle.Minimal,
                '3' => UiStyle.Panels,
                _ => UiStyle.Structured
            };
        }
    }
}
catch (OperationCanceledException)
{
}

AnsiConsole.MarkupLine($"[green]{style}[/]\n");

IChatClient chatClient = CreateChatClient(configuration)
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style);
await agent.RunAsync();

static async Task<IClientTransport> CreateClientTransportAsync(
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
        var arguments = GetStdioArguments(configuration, workingDirectory);

        AnsiConsole.MarkupLine($"[green]OK[/] MCP transport: [grey]stdio[/]");

        return new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = command,
            Arguments = arguments,
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
    AnsiConsole.MarkupLine($"[green]OK[/] MCP transport: [grey]streamHttp[/]");

    return new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(mcpServerUrl),
        AdditionalHeaders = additionalHeaders,
        TransportMode = HttpTransportMode.StreamableHttp,
        Name = "hr-mcp-stream-http"
    });
}

static async Task WaitForHttpServerAsync(string mcpServerUrl)
{
    var baseUrl = mcpServerUrl.Replace("/mcp", "").TrimEnd('/') + "/";
    using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    const int maxAttempts = 30;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            var resp = await probe.GetAsync(baseUrl);
            AnsiConsole.MarkupLine($"[green]OK[/] MCP server is available (HTTP {(int)resp.StatusCode}).\n");
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
        }

        if (attempt == maxAttempts)
            break;

        AnsiConsole.MarkupLine($"[yellow]Waiting for MCP server... (attempt {attempt}/{maxAttempts})[/]");
        await Task.Delay(2000);
    }

    throw new TimeoutException($"MCP server at {mcpServerUrl} did not become available after {maxAttempts} attempts.");
}

static IList<string> GetStdioArguments(IConfiguration configuration, string workingDirectory)
{
    var configuredArgs = configuration
        .GetSection("McpServer:Transport:Stdio:Arguments")
        .GetChildren()
        .Select(section => section.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Cast<string>()
        .ToList();

    if (configuredArgs.Count > 0)
        return configuredArgs;

    var projectPath = configuration["McpServer:Transport:Stdio:ProjectPath"];
    if (string.IsNullOrWhiteSpace(projectPath))
    {
        projectPath = Path.Combine(workingDirectory, "DotnetAiAgentMcp", "src", "HrMcp.McpServer", "HrMcp.McpServer.csproj");
    }

    return new List<string>
    {
        "run",
        "--project",
        projectPath,
        "--",
        "--stdio"
    };
}

static string FindWorkspaceRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "DotnetAiAgentMcp")))
            return dir.FullName;
    }

    return AppContext.BaseDirectory;
}

static IChatClient CreateChatClient(IConfiguration configuration)
{
    var provider = configuration["AI:Provider"] ?? "Ollama";

    if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
    {
        var endpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["AI:Ollama:Model"] ?? "llama3.2";

        return (IChatClient)new OllamaApiClient(new Uri(endpoint), model);
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
