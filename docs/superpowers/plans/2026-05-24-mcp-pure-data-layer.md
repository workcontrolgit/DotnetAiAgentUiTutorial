# MCP Pure Data Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all LLM code from the MCP server so it becomes a pure data layer — the agent is the only process that calls an LLM.

**Architecture:** Delete `JobDescriptionTools.cs` and strip all LLM packages, config, and wiring from the MCP server. Update the agent's system prompt to instruct the LLM to call `GetPositionById` and write the job description draft itself. No new files are created.

**Tech Stack:** .NET 10, ModelContextProtocol, Microsoft.Extensions.AI (agent only), OllamaSharp (agent only).

---

## File Map

| File | Change |
|---|---|
| `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/JobDescriptionTools.cs` | **Delete** |
| `DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj` | Remove 4 LLM NuGet packages |
| `DotnetAiAgentMcp/src/HrMcp.McpServer/appsettings.json` | Remove `AI` section |
| `DotnetAiAgentMcp/src/HrMcp.McpServer/appsettings.Development.json` | Remove `AI` section |
| `DotnetAiAgentMcp/src/HrMcp.McpServer/Program.cs` | Remove LLM usings, configOverrides, CreateChatClient, IChatClient DI, banner AI lines, WithTools<JobDescriptionTools> |
| `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs` | Update system prompt: replace WriteJobDescription guidance with self-write instruction |

---

## Task 1: Delete JobDescriptionTools.cs

**Files:**
- Delete: `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/JobDescriptionTools.cs`

- [ ] **Step 1: Delete the file**

```bash
cd c:/apps/DotnetMcpTutorial
rm DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/JobDescriptionTools.cs
```

- [ ] **Step 2: Verify build fails with a useful error (tool no longer registered)**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj 2>&1 | grep -E "error|Error"
```

Expected: error about `JobDescriptionTools` not found (referenced in `Program.cs`). This is correct — Program.cs still references it. That gets fixed in Task 3.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "Delete JobDescriptionTools — LLM draft generation moves to agent"
```

---

## Task 2: Remove LLM NuGet Packages from McpServer

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj`

- [ ] **Step 1: Remove the four LLM packages**

Replace the `<ItemGroup>` package block with:

```xml
<ItemGroup>
  <PackageReference Include="DocumentFormat.OpenXml" Version="3.*" />
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.*" />
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.6" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.*">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.*" />
  <PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.*" />
  <PackageReference Include="Serilog.AspNetCore" Version="9.*" />
  <PackageReference Include="Serilog.Sinks.File" Version="6.*" />
</ItemGroup>
```

Removed: `Microsoft.Extensions.AI.OpenAI`, `Azure.AI.OpenAI`, `Azure.Identity`, `OllamaSharp`.

- [ ] **Step 2: Restore packages**

```bash
cd c:/apps/DotnetMcpTutorial
dotnet restore DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: restore succeeds, no errors about missing packages.

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
git commit -m "Remove LLM NuGet packages from McpServer (Ollama, Azure OpenAI, AI extensions)"
```

---

## Task 3: Clean Up appsettings Files

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/appsettings.json`
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/appsettings.Development.json`

- [ ] **Step 1: Update appsettings.json — remove the AI section**

Replace the entire file with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=HrMcpDb;Trusted_Connection=True;"
  },
  "Urls": "http://localhost:5100",
  "McpServer": {
    "Transport": {
      "Type": "stdio",
      "StreamHttp": {
        "Path": "/mcp",
        "Url": "http://localhost:5100"
      }
    }
  },
  "Features": {
    "EnableOidc": false,
    "EnableDebug": false
  },
  "Oidc": {
    "Authority": "https://YOUR-DOMAIN.okta.com/oauth2/default",
    "Audience": "api://hr-mcp"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "ModelContextProtocol": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/info/info-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/error/error-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "restrictedToMinimumLevel": "Error",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 2: Update appsettings.Development.json — remove the AI section**

Replace the entire file with:

```json
{
  "Features": {
    "EnableOidc": false
  },
  "Oidc": {
    "Authority": "https://localhost:44310",
    "Audience": "hr-mcp"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.AspNetCore": "Information",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/appsettings.json
git add DotnetAiAgentMcp/src/HrMcp.McpServer/appsettings.Development.json
git commit -m "Remove AI config section from McpServer appsettings"
```

---

## Task 4: Refactor Program.cs — Remove All LLM Wiring

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/Program.cs`

This task removes: LLM usings, `--num-ctx` arg parsing, `configOverrides` dictionary, `AddInMemoryCollection` calls, `IChatClient` DI, `CreateChatClient` function, AI info from startup banners, and both `.WithTools<JobDescriptionTools>()` registrations.

- [ ] **Step 1: Replace Program.cs with the cleaned version**

```csharp
// src/HrMcp.McpServer/Program.cs
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using HrMcp.McpServer.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Net;
using System.Net.NetworkInformation;

var explicitStdio = args.Contains("--stdio");
var explicitStreamHttp = args.Contains("--stream-http");

var tempConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddEnvironmentVariables()
    .Build();

var configuredTransport = tempConfig["McpServer:Transport:Type"] ?? "stdio";
var useStdio = explicitStdio || (!explicitStreamHttp &&
    string.Equals(configuredTransport, "stdio", StringComparison.OrdinalIgnoreCase));

var enableDebug = args.Contains("--debug") ||
    tempConfig.GetValue<bool>("Features:EnableDebug");

var logConfig = new LoggerConfiguration().ReadFrom.Configuration(tempConfig);
if (enableDebug)
    logConfig = logConfig.MinimumLevel.Debug();

Log.Logger = logConfig
    .WriteTo.Conditional(_ => !useStdio, wt => wt.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .WriteTo.Conditional(_ => useStdio, wt => wt.Console(
        standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose,
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .CreateLogger();

try
{
    if (useStdio)
    {
        var hostBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        hostBuilder.Services.AddSerilog();

        ConfigureCommonServices(hostBuilder.Services, hostBuilder.Configuration);

        hostBuilder.Services
            .AddMcpServer()
            .WithTools<PositionTools>()
            .WithTools<HiringOrganizationTools>()
            .WithTools<ExportTools>()
            .WithStdioServerTransport();

        using var host = hostBuilder.Build();
        await InitializeDatabaseAsync(host.Services, args.Contains("--reseed"));

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() =>
        {
            Console.Error.WriteLine("┌─────────────────────────────────────┐");
            Console.Error.WriteLine("│  HrMcp.McpServer                    │");
            Console.Error.WriteLine("│  Transport : stdio                  │");
            Console.Error.WriteLine("│  Status    : READY                  │");
            Console.Error.WriteLine("└─────────────────────────────────────┘");
        });

        await host.RunAsync();
        return;
    }

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Host.UseSerilog();

    ConfigureCommonServices(builder.Services, builder.Configuration);

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

    var app = builder.Build();
    await InitializeDatabaseAsync(app.Services, args.Contains("--reseed"));

    var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    appLifetime.ApplicationStarted.Register(() =>
    {
        var url = builder.Configuration["McpServer:Transport:StreamHttp:Url"] ?? "http://localhost:5100";
        Console.WriteLine("┌─────────────────────────────────────┐");
        Console.WriteLine("│  HrMcp.McpServer                    │");
        Console.WriteLine("│  Transport : stream-http             │");
        Console.WriteLine($"│  URL       : {url,-23}│");
        Console.WriteLine("│  Status    : READY                  │");
        Console.WriteLine("└─────────────────────────────────────┘");
    });

    if (enableOidc)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    var mcpPath = builder.Configuration["McpServer:Transport:StreamHttp:Path"] ?? "/mcp";
    var route = app.MapMcp(mcpPath);
    if (enableOidc)
        route.RequireAuthorization();

    await app.RunAsync();
}
catch (IOException ex) when (!useStdio && TryDescribePortConflict(ex, tempConfig, out var conflictMessage))
{
    Log.Fatal(ex, "{ConflictMessage}", conflictMessage);
    Console.Error.WriteLine(conflictMessage);
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static void ConfigureCommonServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddPersistence(
        configuration.GetConnectionString("DefaultConnection")!);
    services.AddScoped<PositionService>();
    services.AddScoped<HiringOrganizationService>();
}

static async Task InitializeDatabaseAsync(IServiceProvider services, bool forceReseed = false)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();

    await db.Database.MigrateAsync();

    var seedPath = FindSeedFile("data/usajobs-seed.json");
    DbSeeder.Seed(db, seedPath, force: forceReseed);
}

static string? FindSeedFile(string relativePath)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, relativePath);
        if (File.Exists(candidate))
            return candidate;
    }

    return null;
}

static bool TryDescribePortConflict(IOException ex, IConfiguration configuration, out string message)
{
    var mcpUrl =
        configuration["McpServer:Transport:StreamHttp:Url"] ??
        configuration["Urls"] ??
        "http://localhost:5100";

    if (!Uri.TryCreate(mcpUrl.Split(';', StringSplitOptions.RemoveEmptyEntries)[0], UriKind.Absolute, out var uri))
    {
        message = "The MCP stream HTTP URL is invalid. Check McpServer:Transport:StreamHttp:Url or Urls.";
        return true;
    }

    if (!ex.Message.Contains(uri.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
    {
        message = string.Empty;
        return false;
    }

    var conflict = IPGlobalProperties.GetIPGlobalProperties()
        .GetActiveTcpListeners()
        .FirstOrDefault(endpoint =>
            endpoint.Port == uri.Port &&
            (IPAddress.IsLoopback(endpoint.Address) || endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.IPv6Any)));

    if (conflict is null)
    {
        message =
            $"Port {uri.Port} is already in use. Stop the existing listener or change the configured MCP server URL.";
        return true;
    }

    message =
        $"Port {uri.Port} is already in use. Stop the running server or change the configured URL before starting another instance.";
    return true;
}
```

- [ ] **Step 2: Build to verify**

```bash
cd c:/apps/DotnetMcpTutorial
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/Program.cs
git commit -m "Remove LLM wiring from McpServer Program.cs — pure data layer"
```

---

## Task 5: Update Agent System Prompt

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs` (lines 14–32, the `SystemPrompt` constant)

- [ ] **Step 1: Replace the system prompt constant**

In `HrAgent.cs`, replace the `SystemPrompt` constant:

```csharp
private const string SystemPrompt = """
    You are an HR assistant for a U.S. federal agency. Help users explore open job
    positions, hiring organizations, and generate job announcements.

    Guidelines:
    - Always call GetHiringOrganizations before GetPositionsByOrganization.
    - Use GetOpenPositions for an overview; GetPositionById for full detail.
    - When asked to write a job description, call GetPositionById to get the full position
      data, then write a compelling USAJobs-style job announcement yourself with these sections:
      ## Summary, ## Duties, ## Qualifications Required, ## How to Apply.
      Use professional federal HR writing style. Be specific and engaging.
    - To export a position's full structured data, call ExportPositionToHtml(positionId) or ExportPositionToWord(positionId).
    - To export an AI-generated job description draft to Word, call ExportDraftToWord(positionId, draftContent)
      passing the full current draft text including any edits the user has made.
    - To export all open positions to Excel, call ExportPositionsToExcel().
    - Format pay ranges as "$85,000 – $110,000 per year".
    - When you receive position data, format it as a markdown table with columns:
      ID, Title, Grade, Salary, Location.
    - Keep answers concise; offer to go deeper when asked.
    - Never present a numbered menu of options or ask the user what they want to do.
      Respond directly to what the user said, or call a tool immediately.
    """;
```

- [ ] **Step 2: Build the full solution**

```bash
cd c:/apps/DotnetMcpTutorial
dotnet build DotnetAiAgentMcp/DotnetAiAgentMcp.slnx 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs
git commit -m "Update agent system prompt — LLM writes job descriptions itself, no WriteJobDescription tool"
```

---

## Task 6: Integration Test

No automated tests exist for this project — verify manually.

- [ ] **Step 1: Start the agent**

```bash
cd c:/apps/DotnetMcpTutorial
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent
```

Expected: startup banner shows, no errors. The MCP server starts as a subprocess via stdio.

- [ ] **Step 2: Verify the MCP server has no LLM config**

In a second terminal while the agent is running:

```bash
cat DotnetAiAgentMcp/src/HrMcp.McpServer/appsettings.json | grep -c '"AI"'
```

Expected: `0`

- [ ] **Step 3: Test job description generation**

Type in the agent: `write a job description for position 41`

Expected:
- Agent calls `GetPositionById(41)` (visible in MCP server logs if debug enabled)
- Agent LLM writes the draft itself in its response with `## Summary`, `## Duties`, `## Qualifications Required`, `## How to Apply` sections
- No `WriteJobDescription` tool call in logs

- [ ] **Step 4: Test draft export**

After the draft is displayed, type: `export the draft to word`

Expected:
- Agent calls `ExportDraftToWord(positionId=41, draftContent="<full draft text>")`
- File saved to `DotnetAiAgentMcp/usajobs/output/position-41-draft.docx`
- Agent reports: `Saved to: ...\usajobs\output\position-41-draft.docx`

- [ ] **Step 5: Test position export (confirm MCP exports still work)**

Type: `export position 41 to word`

Expected: `Saved to: ...\usajobs\output\position-41.docx`

- [ ] **Step 6: Test Excel export**

Type: `export all positions to excel`

Expected: `Saved to: ...\usajobs\output\positions.xlsx`
