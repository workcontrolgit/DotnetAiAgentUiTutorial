# Dotnet AI Agent and Mcp

A tutorial repository demonstrating **AI Agents** and the **Model Context Protocol (MCP)** using .NET 10 and Clean Architecture.

This series builds a federal HR MCP server — a real, runnable .NET 10 application that exposes job position data (modeled after USAJobs.gov) as MCP tools any AI client can call, including Claude Desktop, VS Code Copilot, and your own .NET agents.

---

## Architecture

The solution follows Clean Architecture with five projects:

![HR MCP Server — Full System Architecture](blogs/series-1-ai-agent-mcp/diagrams/preface-diagram-2-system-architecture.png)

| Project | Role |
|---|---|
| **HrMcp.Core** | Domain entities and repository interfaces. No dependencies. |
| **HrMcp.Application** | Application services. Depends only on Core. |
| **HrMcp.Infrastructure.Persistence** | EF Core + SQL Server. Implements Core interfaces. |
| **HrMcp.McpServer** | ASP.NET Core MCP server. Exposes tools to AI clients. |
| **HrMcp.Agent** | AI agent host with web mode (`--web`) and console fallback. Connects to the MCP server over configurable `stdio` or stream HTTP transport. |

**Why MCP?** Traditional AI integration requires a custom connector for every AI tool × every data source (N×M). MCP replaces that with a shared protocol — N+M integrations instead.

![Before and After MCP — N×M vs N+M integrations](blogs/series-1-ai-agent-mcp/diagrams/preface-diagram-1-nm-problem.png)

---

## How to Get Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — `dotnet --version` should show `10.x`
- **SQL Server LocalDB** — ships with Visual Studio 2022/2026; or install standalone from Microsoft
- [Ollama](https://ollama.com) with `llama3.2` pulled (`ollama pull llama3.2`)
- [Claude Desktop](https://claude.ai/download) *(optional — only needed for Part 5)*

### Setup

```bash
# Clone
git clone https://github.com/workcontrolgit/DotnetAiAgentMcp.git
cd DotnetAiAgentMcp

# Restore and build
dotnet build DotnetAiAgentMcp/DotnetAiAgentMcp.slnx

# Run database migrations
dotnet ef database update \
  --project DotnetAiAgentMcp/src/HrMcp.Infrastructure.Persistence \
  --startup-project DotnetAiAgentMcp/src/HrMcp.McpServer

# Start the MCP server with the default transport (stdio)
dotnet run --project DotnetAiAgentMcp/src/HrMcp.McpServer -- --stdio

# Or start the MCP server with stream HTTP on port 5100
dotnet run --project DotnetAiAgentMcp/src/HrMcp.McpServer -- --stream-http
```

### Transport Modes

Both the server and agent support two transports, selectable via command-line flag or `appsettings.json`.

| Flag | Transport | When to use |
|---|---|---|
| `--stdio` *(default)* | stdio | Claude Desktop, VS Code Copilot, single-terminal dev |
| `--stream-http` | Stream HTTP (port 5100) | Two-terminal dev, visible server logs, MCP Inspector |

**Web mode (MVP default)** — start the agent UI in Blazor Server:

```bash
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --web
```

Use `--web --stream-http` when running the MCP server in a separate terminal.

**stdio mode** — agent auto-spawns the server as a subprocess (one terminal):

```bash
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent
```

**Stream HTTP mode** — server and agent run as separate processes (two terminals):

```bash
# Terminal 1 — start the server
dotnet run --project DotnetAiAgentMcp/src/HrMcp.McpServer -- --stream-http

# Terminal 2 — start the agent
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --stream-http
```

The `--stream-http` flag overrides `appsettings.json` at runtime. No config file changes needed.

To persist the default, set `McpServer:Transport:Type` to `stdio` or `streamHttp` in `appsettings.json`.

### Debug Mode

Both the server and agent support a `--debug` flag that enables verbose logging at startup.

```bash
# Start server with debug logging
dotnet run --project DotnetAiAgentMcp/src/HrMcp.McpServer -- --debug

# Start agent with debug logging
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --debug

# Combine flags (server)
dotnet run --project DotnetAiAgentMcp/src/HrMcp.McpServer -- --stream-http --debug

# Combine flags (agent)
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --stream-http --debug
```

To enable debug mode permanently, set `Features:EnableDebug` to `true` in `appsettings.json` for either project.

### Ollama Context Window (NumCtx)

The agent sends `num_ctx` to Ollama on every request. Configure it in `DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json`:

```json
"Ollama": {
  "Endpoint": "http://localhost:11434",
  "Model": "gemma4:latest",
  "NumCtx": 32768
}
```

Omit `NumCtx` entirely to let Ollama use its own default (typically 2048, which truncates multi-turn sessions).

Sizing guidelines for `gemma4:latest`:

- **4096** — minimal VRAM impact; short Q&A sessions
- **8192** — moderate; typical multi-turn chat
- **16384** — significant; long tool result payloads
- **32768** — heavy (`-32k` territory); slower but handles large contexts

The startup banner prints the active `NumCtx` value so you can confirm it at a glance.

---

### User Secrets

Sensitive configuration (API keys, endpoints) is stored in .NET user secrets and never committed to source control.

```bash
# List secrets for the MCP server
dotnet user-secrets list --project DotnetAiAgentMcp/src/HrMcp.McpServer

# List secrets for the AI agent
dotnet user-secrets list --project DotnetAiAgentMcp/src/HrMcp.Agent
```

To set a secret:

```bash
dotnet user-secrets set "AI:AzureOpenAI:ApiKey" "<your-key>" \
  --project DotnetAiAgentMcp/src/HrMcp.Agent
```

The agent connects to the MCP server, discovers the available tools, and answers HR questions in natural language using Ollama locally.

---

## Blog Series

### AI Agents & MCP with .NET 10

Start with the [Preface](blogs/series-1-ai-agent-mcp/preface.md) for the full overview, then follow the parts in order.

| # | Title | What You Build |
|---|---|---|
| Preface | [Series Overview](blogs/series-1-ai-agent-mcp/preface.md) | Context, goals, prerequisites |
| 1 | [Clean Architecture Foundation with HR Domain](blogs/series-1-ai-agent-mcp/part-1-clean-architecture-hr-domain.md) | Solution skeleton, HR entities, EF Core migrations, seed data |
| 2 | [Introduction to Model Context Protocol](blogs/series-1-ai-agent-mcp/part-2-intro-to-mcp.md) | MCP concepts — Tools, Resources, Prompts, transports |
| 3 | [Building an MCP Server in .NET 10](blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md) | MCP tools with `[McpServerTool]`, both transports, MCP Inspector |
| 4 | [AI Agent with Microsoft.Extensions.AI + Ollama](blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md) | `IChatClient`, tool-call loop, local LLM |
| 5 | [Claude Desktop Integration & End-to-End Demo](blogs/series-1-ai-agent-mcp/part-5-claude-desktop-integration.md) | Claude Desktop + VS Code Copilot connecting to the server |
| 6 | [Securing the MCP Server with OIDC](blogs/series-1-ai-agent-mcp/part-6-mcp-security-oidc.md) | JWT Bearer auth, Duende IdentityServer, client credentials flow |

### Standalone Articles

| Title | Topic |
|---|---|
| [Magnificent 7 Tools Every .NET MCP Developer Should Know](blogs/standalone/7-mcp-tools-dotnet.md) | Tooling roundup from building the series |

---

## Related Series

- [AngularNetTutorial](https://github.com/workcontrolgit/AngularNetTutorial) — Full-stack Angular 20 / .NET 10 / Duende IdentityServer tutorial

---

## License

MIT
