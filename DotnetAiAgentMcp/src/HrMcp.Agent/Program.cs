// src/HrMcp.Agent/Program.cs
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using Azure.AI.OpenAI;
using Azure.Identity;
using OllamaSharp;
using HrMcp.Agent;
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

var mcpServerUrl =
    configuration["McpServer:Url"] ??
    Environment.GetEnvironmentVariable("HR_MCP_SERVER_URL") ??
    "http://localhost:5100/mcp";

// ── OIDC feature flag ────────────────────────────────────────────────────────
// Set Features:EnableOidc = false in appsettings.json (or appsettings.Development.json)
// to connect without authentication — useful when running against MCP Inspector
// or a server instance started without OIDC enforcement.
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

    // Trust self-signed cert used by the local Duende IdentityServer container
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
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = clientId,
            ["client_secret"] = clientSecret,
            ["scope"]         = scope,
        }));
    tokenResponse.EnsureSuccessStatusCode();

    var tokenDoc    = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
    var accessToken = tokenDoc.GetProperty("access_token").GetString()!;
    Console.WriteLine("Token acquired.\n");

    additionalHeaders["Authorization"] = $"Bearer {accessToken}";
}

// --- Connect to MCP server ---
await using var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint          = new Uri(mcpServerUrl),
        AdditionalHeaders = additionalHeaders
    }));

var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Connected. Tools: {string.Join(", ", mcpTools.Select(t => t.Name))}\n");

IChatClient chatClient = CreateChatClient(configuration)
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList());
await agent.RunAsync();

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
