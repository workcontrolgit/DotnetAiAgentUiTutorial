# Add Serilog to HrMcp.Agent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `Console.WriteLine` in `HrMcp.Agent` with Serilog using a console sink and two rolling file sinks (info/error), mirroring the McpServer pattern.

**Architecture:** Bootstrap `Log.Logger` at the top of `Program.cs` using explicit Serilog packages (no AspNetCore meta-package). The existing `ConfigurationBuilder` block is extended to feed `ReadFrom.Configuration`. The full program body is wrapped in `try/catch/finally` with `Log.CloseAndFlushAsync()`.

**Tech Stack:** `Serilog 4.*`, `Serilog.Settings.Configuration 9.*`, `Serilog.Sinks.Console 6.*`, `Serilog.Sinks.File 6.*`, .NET 10 console app.

---

## Files

- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs`

---

### Task 1: Add Serilog NuGet packages

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj`

- [ ] **Step 1: Add the four packages**

```bash
cd DotnetAiAgentMcp/src/HrMcp.Agent
dotnet add package Serilog --version 4.*
dotnet add package Serilog.Settings.Configuration --version 9.*
dotnet add package Serilog.Sinks.Console --version 6.*
dotnet add package Serilog.Sinks.File --version 6.*
```

- [ ] **Step 2: Verify the csproj has all four references**

Open `HrMcp.Agent.csproj` and confirm these lines appear in the `<ItemGroup>`:

```xml
<PackageReference Include="Serilog" Version="4.*" />
<PackageReference Include="Serilog.Settings.Configuration" Version="9.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
<PackageReference Include="Serilog.Sinks.File" Version="6.*" />
```

- [ ] **Step 3: Build to confirm no package resolution errors**

```bash
cd DotnetAiAgentMcp
dotnet build src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj
git commit -m "feat: add Serilog packages to HrMcp.Agent"
```

---

### Task 2: Add Serilog configuration to appsettings.json

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json`

- [ ] **Step 1: Add the Serilog section**

Open `DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json`. Add the `Serilog` key so the file becomes:

```json
{
  "McpServer": {
    "Url": "http://localhost:5100/mcp"
  },
  "Features": {
    "EnableOidc": false
  },
  "Oidc": {
    "Authority": "https://localhost:44310",
    "ClientId": "hr-mcp-agent",
    "ClientSecret": "hr-mcp-agent-secret",
    "Scope": "hr-mcp-api"
  },
  "AI": {
    "Provider": "AzureOpenAI",
    "AzureOpenAI": {
      "Endpoint": "https://YOUR-RESOURCE-NAME.openai.azure.com/",
      "Deployment": "gpt-4.1-mini",
      "ApiKey": "YOUR_AZURE_OPENAI_KEY"
    },
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.2"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json
git commit -m "feat: add Serilog config section to HrMcp.Agent appsettings"
```

---

### Task 3: Bootstrap Serilog and replace Console.WriteLine in Program.cs

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs`

- [ ] **Step 1: Add using directives at the top**

After the existing `using` block, add:

```csharp
using Serilog;
using Serilog.Events;
```

- [ ] **Step 2: Extend the ConfigurationBuilder to include user secrets and build tempConfig**

The existing `ConfigurationBuilder` block reads `appsettings.json`. Rename the result to `tempConfig` (it may already be named `configuration` — create a `tempConfig` alias before it, or rename the variable). The bootstrap logger must read config before the main configuration object is used for MCP/AI settings.

Replace the existing `ConfigurationBuilder` block:

```csharp
var tempConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
```

> Note: This replaces the existing `configuration` variable. Rename all subsequent references from `configuration` to `tempConfig`, OR keep the variable named `configuration` — pick one name and use it consistently throughout.

- [ ] **Step 3: Initialize Log.Logger immediately after tempConfig is built**

Add these lines directly after the `tempConfig` block:

```csharp
var logBase = Path.Combine(AppContext.BaseDirectory, "logs");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(tempConfig)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logBase, "info", "info-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        restrictedToMinimumLevel: LogEventLevel.Information,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(logBase, "error", "error-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        restrictedToMinimumLevel: LogEventLevel.Error,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

- [ ] **Step 4: Wrap the program body in try/catch/finally**

Wrap everything after the logger initialization in:

```csharp
try
{
    // ... all existing program logic (mcpServerUrl, enableOidc, mcpClient, agent.RunAsync etc.) ...
}
catch (Exception ex)
{
    Log.Fatal(ex, "HrMcp.Agent terminated unexpectedly.");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

- [ ] **Step 5: Replace Console.WriteLine calls**

Replace:
```csharp
Console.WriteLine("Token acquired.\n");
```
With:
```csharp
Log.Information("Token acquired.");
```

Replace:
```csharp
Console.WriteLine($"Connected. Tools: {string.Join(", ", mcpTools.Select(t => t.Name))}\n");
```
With:
```csharp
Log.Information("Connected. Tools: {Tools}", string.Join(", ", mcpTools.Select(t => t.Name)));
```

- [ ] **Step 6: Build to confirm no compile errors**

```bash
cd DotnetAiAgentMcp
dotnet build src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Run the agent and verify log output**

Start `HrMcp.McpServer` first (in a separate terminal):
```bash
dotnet run --project DotnetAiAgentMcp/src/HrMcp.McpServer
```

Then run the agent:
```bash
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent
```

Expected console output (Serilog format, not raw Console.WriteLine):
```
2026-05-21 HH:mm:ss [INF] Connected. Tools: GetOpenPositions, GetHiringOrganizations, ...
```

Expected log files created under the Agent binary output directory:
```
DotnetAiAgentMcp/src/HrMcp.Agent/bin/Debug/net10.0/logs/info/info-YYYYMMDD.log
DotnetAiAgentMcp/src/HrMcp.Agent/bin/Debug/net10.0/logs/error/error-YYYYMMDD.log
```

- [ ] **Step 8: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs
git commit -m "feat: add Serilog logging to HrMcp.Agent with console and file sinks"
```
