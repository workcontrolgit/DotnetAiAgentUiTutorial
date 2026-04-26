# Part 6: Securing the MCP Server with OIDC

**Series:** AI Agents & MCP with .NET 10 | **Part 6 of 6**  
**GitHub:** [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp)

---

## Introduction

The MCP server we built in Parts 3–5 listens on `http://localhost:5100/mcp` with no authentication. On a developer laptop that is fine — nothing reaches the port except local processes. But the moment you deploy the server to a shared environment, a container, or a cloud VM, any process that can reach the network can call your HR tools, read position data, and trigger AI generation at your cost.

This part adds JWT Bearer authentication using OpenID Connect (OIDC). The pattern is standard ASP.NET Core:

- The MCP server becomes a **resource server** — it validates JWT tokens on every request.
- The agent (or any client) becomes an **OAuth2 client** — it acquires a token via the client credentials flow before connecting.
- An external **OIDC provider** issues and validates tokens (Okta, Azure Entra, Duende IdentityServer, or any standards-compliant provider).

The surface area of change is small: four lines in `Program.cs`, two lines in `appsettings.json`, and one line in the agent.

---

## Architecture

```
┌─────────────────────────┐
│     OIDC Provider        │
│  (Okta / Entra / Duende) │
│                         │
│  Token endpoint          │
│  JWKS endpoint           │
└────────┬────────────────┘
         │  issues JWT (client credentials)
         ▼
┌─────────────────────────┐         ┌─────────────────────────┐
│      HrMcp.Agent         │         │    HrMcp.McpServer       │
│                         │         │                         │
│  1. POST /token          │  Bearer │  2. validate JWT        │
│     client_id + secret  │────────►│     (Authority / JWKS)  │
│                         │         │  3. authorize endpoint  │
│  4. call MCP tools       │◄────────│  4. return tools/data   │
└─────────────────────────┘         └─────────────────────────┘
```

The OIDC provider is the only component that changes between deployments. The server and agent code are provider-agnostic — they speak standard JWT Bearer and OAuth2 client credentials.

---

## Provider Options

Any standards-compliant OIDC provider works. Common choices:

- **Okta** — free developer account supports up to 1,000 monthly active users; well-documented client credentials setup; hosted, no infrastructure required.
- **Azure Entra ID (formerly Azure AD)** — use App Registrations with a client secret; integrates with MSAL and Azure RBAC; free tier available in Azure portal.
- **Duende IdentityServer** — open-source .NET library; run your own identity server locally or in a container; free community license for development; production license required for revenue-generating use.
- **DotnetFastMCP** — community MCP package (`tekspry/DotnetFastMCP`) with OAuth support built in as an attribute. If you are starting a new project and want OAuth handled at the framework level rather than as ASP.NET Core middleware, this is worth evaluating.

The steps below use Okta as the example. The `Authority` and `Audience` values are the only provider-specific configuration.

---

## Step 1 — Add JWT Bearer to `HrMcp.McpServer`

```bash
dotnet add src/HrMcp.McpServer package Microsoft.AspNetCore.Authentication.JwtBearer --version 9.*
```

> **Note:** `Microsoft.AspNetCore.Authentication.JwtBearer` ships as a standalone NuGet package. For .NET 10 projects targeting ASP.NET Core, version `9.*` is the current stable release that works on net10.0.

---

## Step 2 — Update `appsettings.json`

Add the `Oidc` section with placeholder values. Real values are stored in `appsettings.Development.json` (gitignored) or environment variables — never committed:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HrMcpDb;Trusted_Connection=True;"
  },
  "Urls": "http://localhost:5100",
  "Oidc": {
    "Authority": "https://YOUR-DOMAIN.okta.com/oauth2/default",
    "Audience": "api://hr-mcp"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

For local development, override in `appsettings.Development.json` (already gitignored):

```json
{
  "Oidc": {
    "Authority": "https://dev-abc123.okta.com/oauth2/default",
    "Audience": "api://hr-mcp"
  }
}
```

---

## Step 3 — Add Authentication Middleware to `Program.cs`

The full updated `Program.cs` with the four additions highlighted in comments:

```csharp
// src/HrMcp.McpServer/Program.cs
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using HrMcp.McpServer.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;   // NEW
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OllamaSharp;

var isStdio = args.Contains("--stdio");

var builder = WebApplication.CreateBuilder(args);

if (isStdio)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Trace);
    builder.WebHost.UseUrls();
}

builder.Services.AddPersistence(
    builder.Configuration.GetConnectionString("DefaultConnection")!);
builder.Services.AddScoped<PositionService>();
builder.Services.AddScoped<HiringOrganizationService>();

builder.Services.AddSingleton<IChatClient>(
    new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2"));

// NEW — JWT Bearer authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"];
        options.Audience  = builder.Configuration["Oidc:Audience"];
    });
builder.Services.AddAuthorization();  // NEW

var mcp = builder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<JobDescriptionTools>();

if (isStdio)
    mcp.WithStdioServerTransport();
else
    mcp.WithHttpTransport();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
    db.Database.Migrate();
    var seedPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
    DbSeeder.Seed(db, seedPath);
}

app.UseAuthentication();  // NEW
app.UseAuthorization();   // NEW

if (!isStdio)
    app.MapMcp("/mcp").RequireAuthorization();  // CHANGED

await app.RunAsync();
```

With this in place, any HTTP request to `/mcp` without a valid JWT returns `401 Unauthorized`. Stdio transport is unaffected — `MapMcp` is not called in stdio mode.

---

## Step 4 — Agent Token Acquisition

The agent acquires a token via the OAuth2 client credentials flow and passes it as a bearer header using `HttpClientTransportOptions.AdditionalHeaders`:

```csharp
// src/HrMcp.Agent/Program.cs
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;
using HrMcp.Agent;
using System.Net.Http.Json;

// --- Token acquisition (client credentials flow) ---
var tokenEndpoint = "https://YOUR-DOMAIN.okta.com/oauth2/default/v1/token";
var clientId     = Environment.GetEnvironmentVariable("MCP_CLIENT_ID")!;
var clientSecret = Environment.GetEnvironmentVariable("MCP_CLIENT_SECRET")!;

using var tokenClient = new HttpClient();
var tokenResponse = await tokenClient.PostAsync(tokenEndpoint,
    new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"]    = "client_credentials",
        ["client_id"]     = clientId,
        ["client_secret"] = clientSecret,
        ["scope"]         = "hr-mcp-api",
    }));
tokenResponse.EnsureSuccessStatusCode();
var tokenDoc  = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
var accessToken = tokenDoc.GetProperty("access_token").GetString()!;

// --- Connect to MCP server with bearer token ---
await using var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri("http://localhost:5100/mcp"),
        AdditionalHeaders = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {accessToken}"
        }
    }));

var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Connected. Tools: {string.Join(", ", mcpTools.Select(t => t.Name))}\n");

IChatClient chatClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList());
await agent.RunAsync();
```

Key points:

- **`AdditionalHeaders`** on `HttpClientTransportOptions` — sends the `Authorization` header with every request to the MCP server. This is the same property the checklist referred to as `SseClientTransportOptions.AdditionalHeaders`; in `ModelContextProtocol` 1.x it lives on `HttpClientTransportOptions`.
- **Environment variables** — client ID and secret come from the environment, not from code or config files.
- **`scope`** — the scope name must match what you configure in your OIDC provider and what the server expects. Omit if your provider does not require scope for client credentials.

---

## Optional — Tool-Level Role Check

JWT Bearer middleware secures the entire `/mcp` endpoint. If you need finer control — for example, only users with an `hr-admin` role can call `WriteJobDescription` — inject `IHttpContextAccessor` into the tool:

```csharp
// src/HrMcp.McpServer/Tools/JobDescriptionTools.cs
using Microsoft.AspNetCore.Http;

[McpServerToolType]
public sealed class JobDescriptionTools(
    PositionService positions,
    IChatClient chatClient,
    IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(Name = "WriteJobDescription"),
     Description("Generates a USAJobs-style job announcement using AI.")]
    public async Task<string> WriteJobDescription(int positionId, CancellationToken ct = default)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.IsInRole("hr-admin") != true)
            return "Unauthorized: WriteJobDescription requires the hr-admin role.";

        // ... rest of implementation
    }
}
```

Register `IHttpContextAccessor` in `Program.cs`:

```csharp
builder.Services.AddHttpContextAccessor();
```

---

## DotnetFastMCP — Built-In OAuth Alternative

[DotnetFastMCP](https://github.com/tekspry/DotnetFastMCP) is a community MCP package that wraps the Microsoft MCP SDK with attribute-based tooling and has OAuth support as a first-class feature. If you are starting a new MCP server project and want OAuth handled at the framework level without writing middleware directly, it is worth evaluating.

The trade-off: DotnetFastMCP uses static methods and targets `net8.0`, which is less idiomatic for Clean Architecture projects (DI, scoped services, etc.) than the approach used in this series. For those requirements, the middleware approach in Step 3 remains the better fit.

---

## Okta Free-Tier Setup

Okta's free developer account supports up to 1,000 monthly active users and provides the token infrastructure needed for client credentials.

**1. Create an Okta account**  
Sign up at [developer.okta.com](https://developer.okta.com). Note your **Okta domain** (e.g., `dev-abc123.okta.com`).

**2. Create an API Service application**  
In the Okta admin console: Applications → Create App Integration → API Services. This creates a machine-to-machine client that uses the client credentials flow. Note the **Client ID** and **Client Secret**.

**3. Create a custom scope**  
Security → API → Authorization Servers → default → Scopes → Add Scope. Name it `hr-mcp-api`. This is the scope value in the agent's token request.

**4. Configure the server values**  
Your `appsettings.Development.json`:

```json
{
  "Oidc": {
    "Authority": "https://dev-abc123.okta.com/oauth2/default",
    "Audience":  "api://default"
  }
}
```

> **Note:** The default Okta authorization server uses `api://default` as the audience. If you create a custom authorization server, set the Audience to the identifier you configure there.

**5. Set agent environment variables**  

```bash
set MCP_CLIENT_ID=0oaXXXXXXXXXXXXXX
set MCP_CLIENT_SECRET=XXXXXXXXXXXXXXXXXXXX
```

**6. Test**  
Start the MCP server, then run the agent. The agent calls the token endpoint, receives a JWT, and passes it in the `Authorization` header. The server validates the JWT against Okta's JWKS endpoint (discovered automatically from `Authority`).

---

## Step 5 — Build

```bash
dotnet build DotnetAiAgentMcp.slnx   # 0 errors, 0 warnings
```

The JWT Bearer middleware compiles without a live provider — the `Authority` and `Audience` values are read at runtime. A missing or incorrect `Authority` produces a runtime warning on first request, not a build error.

---

## What We Built

Across the six parts of this series you have built a complete, production-shaped AI agent stack:

**Infrastructure**
- Clean Architecture .NET 10 solution: Core, Application, Infrastructure.Persistence, McpServer, Agent
- EF Core migrations, SQL Server, `DbSeeder` with USAJobs data
- Self-contained `HrMcp.McpServer.exe` for desktop and server deployment

**MCP Server (`HrMcp.McpServer`)**
- Five typed tools: `GetOpenPositions`, `GetPositionById`, `GetPositionsByOrganization`, `GetHiringOrganizations`, `WriteJobDescription`
- Dual transport: stdio for Claude Desktop, HTTP/SSE for programmatic clients
- JWT Bearer authentication — any OIDC provider, configured via `appsettings.json`

**AI Agent (`HrMcp.Agent`)**
- `IChatClient` abstraction (Microsoft.Extensions.AI) with `OllamaApiClient` (OllamaSharp)
- `UseFunctionInvocation` middleware — automatic tool dispatch, no manual routing
- System prompt with HR-domain guidelines
- Full conversation history across turns

**Integration**
- Claude Desktop — stdio transport, `claude_desktop_config.json`, hammer icon
- VS Code Copilot Chat — `.vscode/mcp.json`, HTTP transport, `@hr-mcp`
- MCP Inspector — HTTP mode testing and debugging

---

## Sources

- [OpenID Connect — Introduction](https://openid.net/developers/how-connect-works/)
- [JWT Bearer — Microsoft Docs](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn)
- [OAuth2 Client Credentials Flow — RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749#section-4.4)
- [ModelContextProtocol C# SDK — GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [Okta Developer — Client Credentials](https://developer.okta.com/docs/guides/implement-grant-type/clientcreds/main/)
- [Duende IdentityServer — Overview](https://duendesoftware.com/products/identityserver)
- [DotnetFastMCP — GitHub](https://github.com/tekspry/DotnetFastMCP)
- [Microsoft.AspNetCore.Authentication.JwtBearer — NuGet](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer)
