# Design: Add Serilog to HrMcp.Agent

**Date:** 2026-05-21  
**Project:** DotnetAiAgentMcp  
**Scope:** `src/HrMcp.Agent`

---

## Problem

`HrMcp.Agent` uses raw `Console.WriteLine` for all output. There is no structured logging, no file output, and no log level control. `HrMcp.McpServer` already uses Serilog with file sinks. The Agent should match that capability.

---

## Approach

Option B — explicit Serilog packages without `Serilog.AspNetCore`. The Agent uses `Microsoft.NET.Sdk` (not Web), so the AspNetCore meta-package is unnecessary. Core Serilog packages are referenced directly.

---

## Packages

Add to `HrMcp.Agent.csproj`:

| Package | Version | Purpose |
|---|---|---|
| `Serilog` | `4.*` | Core logger |
| `Serilog.Settings.Configuration` | `9.*` | Read config from appsettings |
| `Serilog.Sinks.Console` | `6.*` | Console output |
| `Serilog.Sinks.File` | `6.*` | Rolling file output |

---

## Bootstrap Pattern

Mirrors `HrMcp.McpServer/Program.cs`:

1. Build `tempConfig` from `appsettings.json` + environment override file + user secrets + environment variables (the `ConfigurationBuilder` block already exists — extend it, do not duplicate).
2. Resolve `logBase = Path.Combine(AppContext.BaseDirectory, "logs")`.
3. Initialize `Log.Logger`:
   ```csharp
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
4. Wrap the entire program body in `try { ... } catch { Log.Fatal(...) } finally { await Log.CloseAndFlushAsync(); }`.

---

## Console.WriteLine Replacements

| Original | Replacement |
|---|---|
| `Console.WriteLine("Token acquired.\n")` | `Log.Information("Token acquired.")` |
| `Console.WriteLine($"Connected. Tools: ...")` | `Log.Information("Connected. Tools: {Tools}", string.Join(", ", ...))` |

Any future error paths use `Log.Error(ex, "...")`.

---

## appsettings.json Changes

Add `Serilog` section to `src/HrMcp.Agent/appsettings.json`:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "System": "Warning"
    }
  }
}
```

File sinks are configured in code (not in appsettings) so log paths are anchored to `AppContext.BaseDirectory` at runtime — same pattern as McpServer.

---

## Log File Layout

```
logs/
  info/
    info-YYYYMMDD.log    — Information and above, 7-day retention
  error/
    error-YYYYMMDD.log   — Error and Fatal, 30-day retention
```

Paths are relative to `AppContext.BaseDirectory`, consistent with McpServer.

---

## Files Changed

| File | Change |
|---|---|
| `src/HrMcp.Agent/HrMcp.Agent.csproj` | Add 4 Serilog package references |
| `src/HrMcp.Agent/Program.cs` | Bootstrap logger, wrap in try/finally, replace Console.WriteLine |
| `src/HrMcp.Agent/appsettings.json` | Add Serilog section |

---

## Out of Scope

- `HrAgent.cs` — no logging changes; the agent class does not need a logger injected
- McpServer — already has Serilog, no changes
- Log formatting changes — use identical output template to McpServer for consistency
