# Part 4: Multi-Model AI Agent with Microsoft.Extensions.AI

**Series:** [AI Agents & MCP with .NET 10](preface.md) | **Part 4 of 6**  
**GitHub:** [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp)
![AI Agents & MCP with .NET 10 blog cover](screenshots/blog-cover.png)

---

## Introduction

In Part 3 we built an MCP server with eight tools and verified them with MCP Inspector. The tools work, but no AI is involved yet. That changes now.

In this part we wire up the `HrMcp.Agent` console app. It connects to the MCP server over `stdio` by default or Streamable HTTP when requested, hands the MCP tools to an `IChatClient`, and lets the model decide which tools to call and when. The agent supports multiple providers through configuration: Ollama for local runs and Azure OpenAI for cloud-backed runs.

One important implementation detail in the real project: the agent does **not** use `UseFunctionInvocation()` middleware. `HrAgent.cs` runs the tool loop manually by reading `FunctionCallContent`, invoking the matching MCP tool, appending `FunctionResultContent`, and asking the model for the next step.

By the end you will have a running AI agent that answers HR questions, writes job descriptions from tool-returned data, and exports files to HTML, Word, and Excel.

---

## The Architecture

![MCP Tool Architecture - Agent, IChatClient, McpClient, and MCP Server](diagrams/part-4-diagram-1-mcp-tool-architecture.png)

`Microsoft.Extensions.AI` is the abstraction layer. `HrAgent.cs` depends only on `IChatClient`, so the conversation code does not care whether the backing model is Ollama or Azure OpenAI.

`McpClientTool` is the bridge. `mcpClient.ListToolsAsync()` returns MCP tools that also behave as `AITool`/`AIFunction` instances, so the agent can expose them to the model and invoke them when the model requests a function call.

---

## Prerequisites

**Option A - Ollama**

- Install Ollama from [ollama.com](https://ollama.com)
- Pull the model: `ollama pull gemma4`
- Verify it works: `ollama run gemma4 "Say hello"`
- Confirm the API is up: `curl http://localhost:11434/api/tags`

**Option B - Azure OpenAI**

Set `AI:Provider` to `AzureOpenAI` in `src/HrMcp.Agent/appsettings.json` and provide endpoint, deployment, and API key values.

---

## Step 1 - Packages

### `HrMcp.Agent`

```bash
dotnet add src/HrMcp.Agent package Microsoft.Extensions.AI --version 9.*
dotnet add src/HrMcp.Agent package OllamaSharp --version 5.*
dotnet add src/HrMcp.Agent package Azure.AI.OpenAI --version 2.*
dotnet add src/HrMcp.Agent package ModelContextProtocol --version 1.*
dotnet add src/HrMcp.Agent package Spectre.Console --version 0.49.*
```

Why these packages:

- `Microsoft.Extensions.AI` provides `IChatClient`, `ChatMessage`, `ChatOptions`, and tool abstractions.
- `OllamaSharp` provides `OllamaApiClient`, which implements `IChatClient`.
- `Azure.AI.OpenAI` provides `AzureOpenAIClient` for the Azure path.
- `ModelContextProtocol` provides `McpClient`, `StdioClientTransport`, `HttpClientTransport`, and MCP client primitives.
- `Spectre.Console` provides the terminal UI.

---

## Step 2 - `HrAgent.cs`

Create `src/HrMcp.Agent/HrAgent.cs`. The real project version does four jobs:

- owns the conversation history
- runs the model/tool/model loop manually
- intercepts export payloads and saves files locally
- renders the terminal UI

Key shape from the actual project:

```csharp
// src/HrMcp.Agent/HrAgent.cs
using Microsoft.Extensions.AI;
using Spectre.Console;
using System.Text.Json;

namespace HrMcp.Agent;

public enum UiStyle { Structured, Minimal, Panels }

public sealed class HrAgent(
    IChatClient chatClient,
    IList<AITool> tools,
    UiStyle style = UiStyle.Structured,
    int? numCtx = null,
    string outputFolder = "usajobs/output")
{
    private const string SystemPrompt = """
        You are an HR assistant for a U.S. federal agency. Help users explore open job
        positions, hiring organizations, and generate job announcements.

        Guidelines:
        - Always call GetHiringOrganizations before GetPositionsByOrganization.
        - Use GetOpenPositions for an overview; GetPositionById for full detail.
        - When asked to write a job description, call GetPositionById to get the full position
          data, then write a compelling USAJobs-style job announcement yourself with these sections:
          ## Summary, ## Duties, ## Qualifications Required, ## How to Apply.
        - To export a position's full structured data, call ExportPositionToHtml(positionId)
          or ExportPositionToWord(positionId).
        - To export an AI-generated job description draft to Word, call
          ExportDraftToWord(positionId, draftContent).
        - To export all open positions to Excel, call ExportPositionsToExcel().
        """;

    private static readonly HashSet<string> ExportToolNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ExportPositionToHtml",
            "ExportPositionToWord",
            "ExportDraftToWord",
            "ExportPositionsToExcel"
        };

    private readonly string _outputFolder = outputFolder;
    private readonly List<ChatMessage> _history = [ new(ChatRole.System, SystemPrompt) ];

    public async Task RunAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var input = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            _history.Add(new ChatMessage(ChatRole.User, input));
            var text = await RunToolLoopAsync(ct);
            Console.WriteLine(text);
        }
    }

    private async Task<string> RunToolLoopAsync(CancellationToken ct)
    {
        var additional = new AdditionalPropertiesDictionary();
        if (numCtx.HasValue) additional["num_ctx"] = numCtx.Value;

        var options = new ChatOptions { Tools = tools, AdditionalProperties = additional };
        var response = await chatClient.GetResponseAsync(_history, options, ct);

        while (true)
        {
            var toolCalls = response.Messages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .ToList();

            if (toolCalls.Count == 0)
            {
                _history.AddMessages(response);
                return response.Text ?? string.Empty;
            }

            foreach (var msg in response.Messages)
                _history.Add(msg);

            foreach (var call in toolCalls)
            {
                var fn = tools.FirstOrDefault(t => t.Name == call.Name) as AIFunction;
                object? rawResult;

                if (fn is null)
                {
                    rawResult = $"Tool '{call.Name}' not found.";
                }
                else
                {
                    var fnArgs = call.Arguments is null ? null : new AIFunctionArguments(call.Arguments);
                    rawResult = await fn.InvokeAsync(fnArgs, ct);
                }

                if (ExportToolNames.Contains(call.Name ?? string.Empty))
                {
                    var json = rawResult switch
                    {
                        string s => s,
                        TextContent tc => tc.Text ?? string.Empty,
                        JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
                        JsonElement je => je.GetRawText(),
                        _ => JsonSerializer.Serialize(rawResult)
                    };

                    var saved = TrySaveExportFile(json, _outputFolder);
                    if (saved is not null) rawResult = saved;
                }

                _history.Add(new ChatMessage(ChatRole.Tool,
                    [new FunctionResultContent(call.CallId ?? string.Empty, rawResult)]));
            }

            response = await chatClient.GetResponseAsync(_history, options, ct);
        }
    }
}
```

### Why this matters

- The constructor really takes `numCtx` and `outputFolder`.
- The project currently uses a **manual tool loop**, not middleware-driven automatic invocation.
- Export tool results are intercepted in the agent and written to disk on the client machine.

---

## Step 3 - `Program.cs`

`Program.cs` wires together transport, optional auth, provider selection, and the agent.

The real project defaults to `stdio`, not HTTP:

```csharp
// src/HrMcp.Agent/Program.cs (trimmed to the same structure used in the current codebase)
using Azure.AI.OpenAI;
using Azure.Identity;
using HrMcp.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;

var transportType = configuration["McpServer:Transport:Type"] ?? "stdio";
var clientTransport = await CreateClientTransportAsync(configuration, transportType, additionalHeaders);
await using var mcpClient = await McpClient.CreateAsync(clientTransport);

var mcpTools = await mcpClient.ListToolsAsync();
var style = UiStyle.Structured;
IChatClient chatClient = CreateChatClient(configuration);

var numCtx = configuration.GetValue<int?>("AI:Ollama:NumCtx");
var outputFolder = FindOutputFolder();
var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style, numCtx, outputFolder);
await agent.RunAsync();

static async Task<IClientTransport> CreateClientTransportAsync(
    IConfiguration configuration,
    string transportType,
    Dictionary<string, string> additionalHeaders)
{
    if (string.Equals(transportType, "stdio", StringComparison.OrdinalIgnoreCase))
    {
        var workingDirectory = FindWorkspaceRoot();
        return new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments =
            [
                "run",
                "--project",
                Path.Combine(workingDirectory, "DotnetAiAgentMcp", "src", "HrMcp.McpServer", "HrMcp.McpServer.csproj"),
                "--",
                "--stdio"
            ],
            WorkingDirectory = workingDirectory,
            Name = "hr-mcp-stdio"
        });
    }

    var mcpServerUrl = configuration["McpServer:Transport:StreamHttp:Url"] ?? "http://localhost:5100/mcp";
    var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    return new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(mcpServerUrl),
        AdditionalHeaders = additionalHeaders,
        TransportMode = HttpTransportMode.StreamableHttp,
        Name = "hr-mcp-stream-http"
    }, httpClient, null, ownsHttpClient: true);
}

static IChatClient CreateChatClient(IConfiguration configuration)
{
    var provider = configuration["AI:Provider"] ?? "Ollama";

    if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
    {
        var endpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["AI:Ollama:Model"] ?? "gemma4:latest";
        var httpClient = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = Timeout.InfiniteTimeSpan };
        return (IChatClient)new OllamaApiClient(httpClient, model, null!);
    }

    var azureEndpoint = configuration["AI:AzureOpenAI:Endpoint"]!;
    var azureDeployment = configuration["AI:AzureOpenAI:Deployment"]!;
    var apiKey = configuration["AI:AzureOpenAI:ApiKey"];

    var client = string.IsNullOrWhiteSpace(apiKey)
        ? new AzureOpenAIClient(new Uri(azureEndpoint), new DefaultAzureCredential())
        : new AzureOpenAIClient(new Uri(azureEndpoint), new System.ClientModel.ApiKeyCredential(apiKey));

    return client.GetChatClient(azureDeployment).AsIChatClient();
}
```

### Multi-Model Configuration

The checked-in project file currently looks like this:

```json
{
  "McpServer": {
    "Transport": {
      "Type": "stdio",
      "Stdio": {
        "Command": "dotnet",
        "ProjectPath": "DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj",
        "WorkingDirectory": ""
      },
      "StreamHttp": {
        "Url": "http://localhost:5100/mcp"
      }
    }
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
      "Model": "gemma4:latest",
      "NumCtx": 32768
    }
  }
}
```

Important detail: the committed `appsettings.json` currently defaults `AI:Provider` to `AzureOpenAI`. If you want to run locally against Ollama, change it to `Ollama`.

### `stdio` vs Streamable HTTP

The project supports two MCP transports, and they serve different deployment shapes.

**`stdio`**

Use `stdio` when the MCP client and the MCP server live on the same machine and the client can launch a local process. That is the right fit for local desktop clients such as **Claude Desktop**, where the client starts the MCP server executable and talks to it over standard input/output. This is why the agent defaults to `stdio` in `appsettings.json`.

Benefits of `stdio`:

- best fit for local desktop MCP clients
- no port management
- no separate web host exposure
- simple local development flow

Tradeoff:

- it assumes the client can start a local server process
- it is not the right choice when the MCP server must be reached over the network

**Streamable HTTP**

Use Streamable HTTP when the MCP server is hosted as a network service and the client connects to it over HTTP. That is the right fit when the server runs remotely, inside a container, behind a reverse proxy, or anywhere you want a stable URL such as `https://your-host/mcp`.

Benefits of Streamable HTTP:

- works across machines and network boundaries
- fits hosted/server deployments
- works naturally with reverse proxies and ingress
- gives you a clean place to apply authentication and authorization

Tradeoff:

- you now need to manage server hosting, ports, certificates, and connectivity

**Security note**

If the MCP server is exposed over HTTP, you should think about authentication early. In this series, Part 6 adds **OIDC** so the agent can acquire a bearer token and send it to the MCP server. That security story matters much more for Streamable HTTP than for a local `stdio` setup, because HTTP hosting is the path most likely to cross machine or trust boundaries.

So the rule of thumb is:

- use `stdio` for local clients like Claude Desktop
- use Streamable HTTP for hosted MCP servers
- consider OIDC when exposing the MCP server over HTTP

### Runtime Flags

`HrMcp.Agent` supports several command-line flags that map directly to the real `Program.cs` behavior:

- `--stdio` forces `stdio` transport.
- `--stream-http` forces Streamable HTTP transport.
- `--debug` enables verbose MCP transport logging.
- `--num-ctx <value>` overrides `AI:Ollama:NumCtx` from configuration.

#### `--debug`

The `--debug` flag raises the logging level used for MCP categories. In the current code, it switches MCP logging from warning-level output to debug-level output, which is useful when you need to inspect transport behavior, tool discovery, or request/response flow between the client and the MCP server.

This is especially useful when:

- a tool is not appearing in the client
- a transport connection succeeds but tool calls fail
- you need to distinguish agent-side issues from MCP transport issues

The same behavior can also be enabled through config with:

```json
{
  "Features": {
    "EnableDebug": true
  }
}
```

CLI flags still take precedence when you pass them explicitly.

---

## Step 4 - Build

```bash
dotnet build DotnetAiAgentMcp.slnx
```

---

## Step 5 - Run a Conversation

Because the project defaults to `stdio`, the simplest run is:

```bash
dotnet run --project src/HrMcp.Agent
```

If you want to run the server separately over Streamable HTTP:

```bash
dotnet run --project src/HrMcp.McpServer
dotnet run --project src/HrMcp.Agent -- --stream-http
```

You should see the MCP server log in the first terminal:

![MCP server listening on http://localhost:5100](screenshots/part-4-screenshot-1-mcp-server.png)

And in the agent terminal, the style picker:

![Agent startup showing style picker](screenshots/part-4-screenshot-2-agent-startup.png)

### Sample conversation

![Sample conversation in Structured style](screenshots/part-4-screenshot-3-conversation.png)

---

## Job Descriptions - the LLM Writes Them

There is no `WriteJobDescription` MCP tool in the current project. The MCP server is a pure data layer. The agent gets position data with `GetPositionById`, then the model writes the narrative directly in the conversation.

This is the actual prompt guidance:

```text
- When asked to write a job description, call GetPositionById to get the full position
  data, then write a compelling USAJobs-style job announcement yourself with these sections:
  ## Summary, ## Duties, ## Qualifications Required, ## How to Apply.
```

That keeps LLM dependencies out of the server and keeps revisions inside the same chat history.

---

## Export Tools

The MCP server exposes four export tools:

| Tool | Output | File |
|---|---|---|
| `ExportPositionToHtml` | USAJobs-style HTML page | `position-{id}.html` |
| `ExportPositionToWord` | Full position data as `.docx` | `position-{id}.docx` |
| `ExportDraftToWord` | LLM-generated draft as `.docx` | `position-{id}-draft.docx` |
| `ExportPositionsToExcel` | All open positions as `.xlsx` | `positions.xlsx` |

Key structure from the real server code:

```csharp
// src/HrMcp.McpServer/Tools/ExportTools.cs
[McpServerToolType]
public sealed class ExportTools(PositionService positions, ILogger<ExportTools> logger)
{
    [McpServerTool(Name = "ExportPositionToWord")]
    public async Task<string> ExportPositionToWord(int positionId, CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return $"Position {positionId} not found.";

        var bytes = BuildPositionDocx(p);
        return ToBase64Json($"position-{positionId}.docx", bytes);
    }

    [McpServerTool(Name = "ExportDraftToWord")]
    public async Task<string> ExportDraftToWord(int positionId, string draftContent, CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        var title = p?.Title ?? $"Position {positionId}";
        var org = p is null ? "" : $"{p.HiringOrganization?.DepartmentName} | {p.HiringOrganization?.OrganizationName}";

        var bytes = BuildDraftDocx(title, org, draftContent);
        return ToBase64Json($"position-{positionId}-draft.docx", bytes);
    }

    [McpServerTool(Name = "ExportPositionsToExcel")]
    public async Task<string> ExportPositionsToExcel(CancellationToken ct = default)
    {
        var list = await positions.GetOpenPositionsAsync(ct);
        var bytes = BuildPositionsExcel(list.ToList());
        return ToBase64Json("positions.xlsx", bytes);
    }
}
```

### Agent-Side File Interception

The export tools return JSON payloads like:

```json
{ "fileName": "positions.xlsx", "content": "<base64>" }
```

The server never writes files to the client machine. `HrAgent.cs` intercepts these payloads, decodes the base64 content, writes the file under `usajobs/output/`, and replaces the raw tool result with a user-friendly `Saved to: ...` message.

The important detail is that MCP tool results may arrive as `TextContent`, so the agent unwraps them before parsing JSON.

---

## What Happened Under the Hood

For a question like "Show me IT positions at USCIS", the loop is:

1. The model requests `GetHiringOrganizations`.
2. The agent invokes the MCP tool and appends the result as `FunctionResultContent`.
3. The model requests `GetPositionsByOrganization`.
4. The agent invokes that tool, appends the result, and asks the model again.
5. The model returns final text.

That is why the agent only depends on `IChatClient` and `AITool`, even though the tools themselves live on the MCP server.

---

## Swapping Providers

`HrAgent.cs` stays the same when you change providers. Only `CreateChatClient()` and config change.

For example:

```csharp
IChatClient chatClient = new AzureOpenAIClient(
        new Uri("https://YOUR-RESOURCE.openai.azure.com"),
        new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")!))
    .GetChatClient("gpt-4.1-mini")
    .AsIChatClient();
```

The project's current Ollama path uses `OllamaApiClient`, and the Azure path uses `AzureOpenAIClient`.

---

## What We Built

- `HrMcp.Agent` console agent using `IChatClient` plus MCP tools
- manual tool dispatch in `HrAgent.cs`
- `stdio` transport by default, Streamable HTTP as an option
- multi-provider model selection through config
- export interception that saves files locally
- a pure-data MCP server with no embedded LLM dependency

---

## Next Up

**[Part 5: Claude Desktop Integration & End-to-End Demo ->](part-5-claude-desktop-integration.md)**

In Part 5 we connect the same MCP server to Claude Desktop and verify that the same tools work in a full AI host without any custom agent code.

---

## Sources

- [Microsoft.Extensions.AI - NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- [OllamaSharp - NuGet](https://www.nuget.org/packages/OllamaSharp)
- [ModelContextProtocol - NuGet](https://www.nuget.org/packages/ModelContextProtocol)
- [ModelContextProtocol C# SDK - GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [Azure.AI.OpenAI - NuGet](https://www.nuget.org/packages/Azure.AI.OpenAI)
- [DocumentFormat.OpenXml - NuGet](https://www.nuget.org/packages/DocumentFormat.OpenXml)
