// src/HrMcp.Agent/Program.cs
using Azure.AI.OpenAI;
using Azure.Identity;
using HrMcp.Agent.Components;
using HrMcp.Agent;
using HrMcp.Agent.Web.Services;
using HrMcp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OllamaSharp;
using Serilog;
using Spectre.Console;
using System.Net.Http.Json;
using System.Text.Json;

var runConsole = args.Contains("--console", StringComparer.OrdinalIgnoreCase);
if (!runConsole)
{
    await RunWebAsync(args);
    return;
}

// --num-ctx <value> overrides AI:Ollama:NumCtx from appsettings.json
var numCtxArg = ParseIntArg(args, "--num-ctx");
var configOverrides = new Dictionary<string, string?>();
if (numCtxArg.HasValue)
    configOverrides["AI:Ollama:NumCtx"] = numCtxArg.Value.ToString();

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .AddInMemoryCollection(configOverrides)   // CLI wins over all file sources
    .Build();

var transportType = args.Contains("--stream-http") ? "streamHttp"
    : args.Contains("--stdio") ? "stdio"
    : configuration["McpServer:Transport:Type"] ?? "stdio";

var enableDebug = args.Contains("--debug") ||
    configuration.GetValue<bool>("Features:EnableDebug");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(enableDebug ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information)
    .WriteTo.File(
        path: Path.Combine("logs", "error-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{

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

var aiProvider = configuration["AI:Provider"] ?? "Ollama";
var aiModel = string.Equals(aiProvider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
    ? configuration["AI:AzureOpenAI:Deployment"] ?? "unknown"
    : configuration["AI:Ollama:Model"] ?? "unknown";

var clientTransport = await CreateClientTransportAsync(configuration, transportType, additionalHeaders);

// Pass a logger factory so StdioClientTransport forwards captured server stderr to the console.
// Without this, the transport redirects stderr internally and logs are silently discarded.
var mcpMinLevel = enableDebug ? LogLevel.Debug : LogLevel.Warning;
using var mcpLoggerFactory = LoggerFactory.Create(b => b
    .AddFilter((category, level) =>
        category?.StartsWith("ModelContextProtocol", StringComparison.Ordinal) == true && level >= mcpMinLevel)
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));

await using var mcpClient = await McpClient.CreateAsync(clientTransport, loggerFactory: mcpLoggerFactory);

var mcpTools = await mcpClient.ListToolsAsync();
var toolNames = mcpTools.Select(t => t.Name).ToList();

const int W = 43;
string L(string s) => $"│  {s.PadRight(W)}│";
string T(string s) => $"│    - {s.PadRight(W - 4)}│";
Console.WriteLine($"┌{new string('─', W + 2)}┐");
Console.WriteLine(L("HrMcp.Agent"));
Console.WriteLine(L($"Transport : {transportType}"));
Console.WriteLine(L($"Provider  : {aiProvider}"));
Console.WriteLine(L($"Model     : {aiModel}"));
var numCtxDisplay = configuration.GetValue<int?>("AI:Ollama:NumCtx");
if (numCtxDisplay.HasValue)
    Console.WriteLine(L($"NumCtx    : {numCtxDisplay.Value:N0}"));
Console.WriteLine(L($"Tools ({toolNames.Count})  :"));
foreach (var name in toolNames)
    Console.WriteLine(T(name));
Console.WriteLine(L("Status    : READY"));
Console.WriteLine($"└{new string('─', W + 2)}┘");
Console.WriteLine();

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

IChatClient chatClient = CreateChatClient(configuration);

var numCtx = configuration.GetValue<int?>("AI:Ollama:NumCtx");

var outputFolder = FindOutputFolder();
var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style, numCtx, outputFolder);
await agent.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception in HrMcp.Agent");
    Console.Error.WriteLine("A fatal error occurred. Check logs/error-*.log for details.");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task RunWebAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseStaticWebAssets();
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    var connectionString = builder.Configuration.GetConnectionString("HrDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:HrDb");
    builder.Services.AddPersistence(connectionString);
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });
    builder.Services.AddScoped<IAgentDraftService, AgentDraftService>();
    // TODO: UserContext full implementation is Task B5; stub registered here so DI compiles.
    builder.Services.AddScoped<UserContext>();

    // Cookie auth (set by Identity above) + optional OIDC federation
    var enableOidc = builder.Configuration.GetValue<bool>("Features:EnableOidc");
    if (enableOidc)
    {
        builder.Services.AddAuthentication()
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = builder.Configuration["Oidc:UserLogin:Authority"]
                    ?? throw new InvalidOperationException("Missing Oidc:UserLogin:Authority");
                options.ClientId = builder.Configuration["Oidc:UserLogin:ClientId"]
                    ?? throw new InvalidOperationException("Missing Oidc:UserLogin:ClientId");
                options.ClientSecret = builder.Configuration["Oidc:UserLogin:ClientSecret"];
                options.ResponseType = "code";
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
            });
    }

    builder.Services.AddAuthorization();

    var app = builder.Build();
    app.UseStaticFiles();
    app.MapStaticAssets();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    if (enableOidc)
    {
        app.MapGet("/challenge/oidc", async (HttpContext ctx, string? returnUrl) =>
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl
            };
            await ctx.ChallengeAsync("oidc", props);
        });
    }

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Console.WriteLine("HrMcp.Agent starting in web mode. Pass --console to run the console agent instead.");
    await app.RunAsync();
}

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

    var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    return new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(mcpServerUrl),
        AdditionalHeaders = additionalHeaders,
        TransportMode = HttpTransportMode.StreamableHttp,
        Name = "hr-mcp-stream-http"
    }, httpClient, null, ownsHttpClient: true);
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
        projectPath = Path.Combine(workingDirectory, "DotnetAiAgentUi", "src", "HrMcp.McpServer", "HrMcp.McpServer.csproj");
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

static string FindOutputFolder()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, "usajobs", "output");
        if (Directory.Exists(Path.Combine(dir.FullName, "usajobs")))
            return candidate;
    }
    // Fallback: relative to current working directory
    return Path.GetFullPath("usajobs/output");
}

static int? ParseIntArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var v) ? v : null;
}

static IChatClient CreateChatClient(IConfiguration configuration)
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
