# Part 6: Securing the MCP Server with OIDC

**Series:** [AI Agents & MCP with .NET 10](preface.md) | **Part 6 of 6**  
**GitHub:** [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp)
![AI Agents & MCP with .NET 10 blog cover](screenshots/blog-cover.png)

---

## Introduction

By this point in the series, we have two very different MCP deployment shapes:

- local `stdio` clients such as Claude Desktop
- HTTP-hosted MCP endpoints for remote or programmatic access

The local `stdio` path is inherently narrower in exposure because the host launches the process directly on the same machine.

The HTTP path is different. Once the server is listening on an address like `http://localhost:5100/mcp` or a deployed HTTPS endpoint, authentication becomes part of the design.

This part explains how the current repo secures that HTTP-hosted path with **OIDC-backed JWT bearer authentication**.

Important current-code detail:

- OIDC is **feature-flagged**
- when enabled, it protects the HTTP MCP route
- the `stdio` path remains unaffected

---

## The Security Model

The current implementation follows a standard split:

- `HrMcp.McpServer` acts as the **resource server**
- `HrMcp.Agent` acts as the **OAuth2 client**
- an external identity provider issues access tokens

The server validates incoming bearer tokens.
The agent acquires a token using the client credentials flow and sends it on HTTP MCP requests.

That means:

- local `stdio` use can stay simple
- hosted HTTP access can require auth
- the same business tools remain unchanged

![OIDC integration between HrMcp.Agent, identity provider, and HrMcp.McpServer](diagrams/part-6-diagram-1-identityserver-oidc-integration.png)

---

## Current Server Behavior

The current `HrMcp.McpServer` code does **not** hard-code OIDC on all paths.

Instead, it reads:

```json
{
  "Features": {
    "EnableOidc": false
  }
}
```

from `src/HrMcp.McpServer/appsettings.json`.

When OIDC is enabled:

- the server adds JWT bearer authentication
- the server adds authorization
- the HTTP MCP route requires authorization

When it is disabled:

- the HTTP MCP route runs without bearer auth

Crucially, in `stdio` mode the server uses `.WithStdioServerTransport()` and does not expose the HTTP route at all.

---

## Step 1 - Server Configuration

The current server config includes:

```json
{
  "Features": {
    "EnableOidc": false,
    "EnableDebug": false
  },
  "Oidc": {
    "Authority": "https://YOUR-DOMAIN.okta.com/oauth2/default",
    "Audience": "api://hr-mcp"
  }
}
```

Meaning of the OIDC settings:

- `Authority` points to the identity provider
- `Audience` is the audience the server expects in access tokens

When you move from placeholders to a real provider, those values are what change first.

---

## Step 2 - What `Program.cs` Does

The current server setup looks like this conceptually:

```csharp
var enableOidc = builder.Configuration.GetValue<bool>("Features:EnableOidc");

if (enableOidc)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Oidc:Authority"];
            options.Audience = builder.Configuration["Oidc:Audience"];

            if (builder.Environment.IsDevelopment())
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
        });

    builder.Services.AddAuthorization();
}

builder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<ExportTools>()
    .WithHttpTransport();

if (enableOidc)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

var route = app.MapMcp(mcpPath);
if (enableOidc)
    route.RequireAuthorization();
```

What matters here:

- auth is added only when the feature flag is on
- the tools themselves do not change
- the MCP route becomes protected only on the HTTP path

This is a better match for the current repo than the older blog version, which described a different tool set and a different `Program.cs` shape.

---

## Step 3 - Agent Token Acquisition

The current `HrMcp.Agent` also treats OIDC as optional.

In `src/HrMcp.Agent/appsettings.json`, the relevant settings are:

```json
{
  "Features": {
    "EnableOidc": false
  },
  "Oidc": {
    "Authority": "https://localhost:44310",
    "ClientId": "hr-mcp-agent",
    "ClientSecret": "hr-mcp-agent-secret",
    "Scope": "hr-mcp-api"
  }
}
```

When `Features:EnableOidc` is true, the agent:

1. posts to `{Authority}/connect/token`
2. uses the client credentials grant
3. reads the returned `access_token`
4. adds `Authorization: Bearer <token>` to the MCP HTTP transport headers

Conceptually:

```csharp
if (enableOidc)
{
    var tokenResponse = await tokenClient.PostAsync(
        $"{authority.TrimEnd('/')}/connect/token",
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = scope,
        }));

    additionalHeaders["Authorization"] = $"Bearer {accessToken}";
}
```

That means the agent only needs special auth logic for the HTTP path. Its `stdio` path does not need bearer tokens because it is not crossing the HTTP boundary.

---

## Step 4 - Why `stdio` and OIDC Are Different Concerns

This is the conceptual split worth emphasizing:

**`stdio`**

- local process-to-process communication
- no exposed HTTP endpoint
- best fit for local desktop hosts
- bearer-token auth usually not the first concern

**HTTP MCP**

- network-addressable endpoint
- suitable for remote, shared, or hosted access
- right place for JWT bearer authentication
- where OIDC matters

So when you read “secure the MCP server with OIDC,” in the current repo that should be understood as:

- **secure the HTTP-hosted MCP route**
- not “add tokens to every local `stdio` scenario”

---

## Step 5 - What Changes Operationally

Once OIDC is enabled for HTTP mode:

- unauthenticated HTTP access to the MCP route should fail
- authenticated agent access over HTTP should succeed
- local `stdio` scenarios can continue to work independently

That gives you a clean development story:

- Claude Desktop or other local hosts: use `stdio`
- hosted agent/server scenarios: use HTTP + OIDC

That separation keeps local iteration easy while still giving you a path to production-grade security.

---

## Provider Notes

The current repo uses generic OIDC settings rather than tying the code to one provider.

That means you can point it at:

- Okta
- Azure Entra ID
- Duende IdentityServer
- any compatible OIDC provider

The server and agent code remain the same. What changes is configuration:

- authority URL
- audience
- client ID / client secret / scope on the agent side

---

## What This Part Proves

The important architectural outcome is that security is layered at the hosting boundary, not shoved into the domain model.

The same 8-tool MCP surface can now be:

- consumed locally over `stdio`
- hosted over HTTP
- protected with bearer authentication when needed

That is exactly how Clean Architecture should feel: the outer layer changes, the core use cases do not.

---

## Final Stack Recap

Across the full series, the current repo now has:

- a .NET 10 Clean Architecture solution
- an MCP server exposing 8 tools
- dual transport support: `stdio` and Streamable HTTP
- a .NET MCP client/agent
- export workflows
- optional OIDC protection on the HTTP route

That is a much more production-shaped stack than the original early draft.

---

## Sources

- [OpenID Connect](https://openid.net/developers/how-connect-works/)
- [JWT Bearer Authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn)
- [OAuth 2.0 Client Credentials Flow](https://datatracker.ietf.org/doc/html/rfc6749#section-4.4)
- [ModelContextProtocol C# SDK - GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
