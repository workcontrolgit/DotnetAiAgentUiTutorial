# Blog Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update Part 3 and Part 4 of the blog series to accurately reflect the codebase — remove deleted `JobDescriptionTools`/`WriteJobDescription` content, update the agent to show multi-model support with gemma4 as default, and add Export Tools + agent-side interception sections to Part 4.

**Architecture:** Surgical edits to two markdown files only. No code changes. Part 3 loses one tool class section. Part 4 loses two steps (McpServer LLM wiring + WriteJobDescription upgrade) and gains three new sections (JD-by-LLM explanation, Export Tools, Agent-Side Interception).

**Tech Stack:** Markdown editing only. All code shown in the blog is pulled verbatim from the actual codebase files.

---

## File Map

| File | Change |
|---|---|
| `blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md` | Remove `JobDescriptionTools` content; update tool counts |
| `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md` | Remove McpServer LLM steps; update packages + Program.cs; add 3 new sections; update summary |

---

## Task 1: Part 3 — Remove JobDescriptionTools from folder listing and series header

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md`

- [ ] **Step 1: Fix series count in header (line 3)**

Find:
```
**Series:** AI Agents & MCP with .NET 10 | **Part 3 of 5**  
```

Replace with:
```
**Series:** AI Agents & MCP with .NET 10 | **Part 3 of 6**  
```

- [ ] **Step 2: Fix "three tool classes" in intro (line 14)**

Find:
```
The server lives in `HrMcp.McpServer`, the project we scaffolded in Part 1. All we are adding is the MCP SDK and three tool classes.
```

Replace with:
```
The server lives in `HrMcp.McpServer`, the project we scaffolded in Part 1. All we are adding is the MCP SDK and two tool classes.
```

- [ ] **Step 3: Remove JobDescriptionTools.cs from folder listing**

Find:
```
```text
src/HrMcp.McpServer/
  Tools/
    PositionTools.cs
    HiringOrganizationTools.cs
    JobDescriptionTools.cs
```
```

Replace with:
```
```text
src/HrMcp.McpServer/
  Tools/
    PositionTools.cs
    HiringOrganizationTools.cs
```
```

- [ ] **Step 4: Remove the entire JobDescriptionTools.cs section**

Find and delete everything from:
```
### `JobDescriptionTools.cs`

One tool: generate a USAJobs-format job description. In this part it returns a structured template. In Part 4 we replace the template with real LLM output.
```

...through to (and including) the closing fence of the code block ending with:
```
}
```

followed by the `---` separator before `## Step 3`.

The deleted block runs from the `### \`JobDescriptionTools.cs\`` heading through the end of the fenced code block (inclusive). Stop before `---` and `## Step 3`.

- [ ] **Step 5: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md
git commit -m "blog(part-3): remove JobDescriptionTools section and fix series header"
```

---

## Task 2: Part 3 — Remove JobDescriptionTools from Program.cs snippet and Inspector walkthrough

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md`

- [ ] **Step 1: Remove .WithTools<JobDescriptionTools>() from Program.cs snippet**

Find:
```csharp
var mcp = builder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<JobDescriptionTools>();
```

Replace with:
```csharp
var mcp = builder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>();
```

- [ ] **Step 2: Fix "five tools" to "four tools" in Inspector intro**

Find:
```
The **Tools** tab lists all five tools auto-discovered from the server. Click any tool to see its description and a **Run Tool** button. The right panel shows the live JSON-RPC response.
```

Replace with:
```
The **Tools** tab lists all four tools auto-discovered from the server. Click any tool to see its description and a **Run Tool** button. The right panel shows the live JSON-RPC response.
```

- [ ] **Step 3: Remove the "Calling WriteJobDescription" walkthrough**

Find and delete from:
```
### Calling `WriteJobDescription`

Click `WriteJobDescription`, enter `positionId: 1` → **Run Tool**. Returns the structured Markdown template:
```

...through to (and including):
```
All four tools respond correctly. The server is working.
```

Replace the deleted block with just:
```
All four tools respond correctly. The server is working.
```

(Retain the tip block that follows unchanged.)

- [ ] **Step 4: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md
git commit -m "blog(part-3): remove WriteJobDescription from Inspector walkthrough and Program.cs"
```

---

## Task 3: Part 3 — Update "What We Built" summary and Next Up preview

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md`

- [ ] **Step 1: Update "What We Built" bullet list**

Find:
```
- **`ModelContextProtocol.AspNetCore` 1.2.0** installed in `HrMcp.McpServer`
- **3 tool classes** — `PositionTools` (3 tools), `HiringOrganizationTools` (1 tool), `JobDescriptionTools` (1 tool)
- **`--stdio` flag** — single binary, two transports, no code duplication
- **Verified with MCP Inspector** — all 4 tools discovered and callable against real DHS data
- **`WriteJobDescription`** returns a structured stub, ready for LLM upgrade in Part 4
```

Replace with:
```
- **`ModelContextProtocol.AspNetCore` 1.x** installed in `HrMcp.McpServer`
- **2 tool classes** — `PositionTools` (3 tools), `HiringOrganizationTools` (1 tool)
- **`--stdio` flag** — single binary, two transports, no code duplication
- **Verified with MCP Inspector** — all 4 tools discovered and callable against real DHS data
```

- [ ] **Step 2: Update the closing "The AI still knows nothing" paragraph and Next Up preview**

Find:
```
The AI still knows nothing about any of this. In Part 4, we wire in an LLM via `Microsoft.Extensions.AI` and Ollama, let it call these tools, and replace the `WriteJobDescription` stub with a real generated narrative.
```

Replace with:
```
The AI still knows nothing about any of this. In Part 4, we wire in an LLM via `Microsoft.Extensions.AI` and Ollama, connect it to these tools, and add export tools that let the agent save positions and job description drafts as Word and Excel files.
```

- [ ] **Step 3: Update Next Up paragraph**

Find:
```
We build the `HrMcp.Agent` console app: connect it to the MCP server, register Ollama as the chat client, and let the AI call `GetOpenPositions`, `GetHiringOrganizations`, and `WriteJobDescription` in a live conversation.
```

Replace with:
```
We build the `HrMcp.Agent` console app: connect it to the MCP server, configure multi-model support (Ollama with gemma4 as default, Azure OpenAI as a config swap), and let the AI answer HR questions and export positions and drafts as Word and Excel files.
```

- [ ] **Step 4: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md
git commit -m "blog(part-3): update What We Built summary and Next Up preview"
```

---

## Task 4: Part 4 — Update title, introduction, prerequisites, and Step 1 packages

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

- [ ] **Step 1: Update title and subtitle**

Find:
```
# Part 4: AI Agent with Microsoft.Extensions.AI + Ollama

**Series:** AI Agents & MCP with .NET 10 | **Part 4 of 6**  
```

Replace with:
```
# Part 4: Multi-Model AI Agent with Microsoft.Extensions.AI

**Series:** AI Agents & MCP with .NET 10 | **Part 4 of 6**  
```

- [ ] **Step 2: Update introduction paragraph**

Find:
```
In Part 3 we built an MCP server with five tools and verified them with MCP Inspector. The tools work — but no AI is involved yet. That changes now.

In this part we wire up the `HrMcp.Agent` console app: it connects to the MCP server over HTTP, hands the tools to an Ollama-backed `IChatClient`, and holds a live conversation where the AI decides which tools to call and when. We also upgrade `WriteJobDescription` from a static template to a real LLM-generated narrative.

By the end you will have a running AI agent that answers HR questions by calling your MCP tools — no hard-coded logic, no manual tool routing.
```

Replace with:
```
In Part 3 we built an MCP server with four tools and verified them with MCP Inspector. The tools work — but no AI is involved yet. That changes now.

In this part we wire up the `HrMcp.Agent` console app: it connects to the MCP server over HTTP, hands the tools to an `IChatClient`, and holds a live conversation where the AI decides which tools to call and when. The agent supports multiple LLM providers — Ollama with gemma4 as the local default, and Azure OpenAI as a production swap-in — all controlled by a single config key. We also add Export Tools that let the AI write positions and job description drafts to Word and Excel files, saved directly to your machine.

By the end you will have a running AI agent that answers HR questions, writes job descriptions, and exports files — no hard-coded logic, no manual tool routing.
```

- [ ] **Step 3: Update architecture blurb**

Find:
```
`Microsoft.Extensions.AI` is the abstraction layer. Ollama is the provider — swap it for Azure OpenAI or any other provider without touching `HrAgent.cs`.
```

Replace with:
```
`Microsoft.Extensions.AI` is the abstraction layer. `HrAgent.cs` depends only on `IChatClient` — it has no knowledge of whether Ollama or Azure OpenAI is underneath. Provider selection happens in `Program.cs` via a `CreateChatClient()` helper that reads `AI:Provider` from config.
```

- [ ] **Step 4: Update Prerequisites section**

Find:
```
Before running `HrMcp.Agent`, you need Ollama running locally with the `llama3.2` model pulled:

- **Install Ollama** — download from [ollama.com](https://ollama.com) and install
- **Pull the model** — `ollama pull llama3.2`
- **Verify** — `ollama run llama3.2 "Say hello"` (should print a greeting)
- **Confirm the API is up** — `curl http://localhost:11434/api/tags` (lists pulled models)

Ollama runs a local HTTP server on port `11434`. The `OllamaChatClient` points at this address. Nothing leaves your machine.
```

Replace with:
```
**Option A — Ollama (local, default)**

Run the agent locally for free with no cloud account required:

- **Install Ollama** — download from [ollama.com](https://ollama.com) and install
- **Pull the model** — `ollama pull gemma4`
- **Verify** — `ollama run gemma4 "Say hello"` (should print a greeting)
- **Confirm the API is up** — `curl http://localhost:11434/api/tags` (lists pulled models)

Ollama runs a local HTTP server on port `11434`. Nothing leaves your machine.

**Option B — Azure OpenAI**

Set `AI:Provider` to `AzureOpenAI` in `appsettings.json` and fill in your endpoint, deployment name, and API key. See the [Multi-Model Configuration](#multi-model-configuration) section below for details. `HrAgent.cs` is unchanged — only `Program.cs` and config differ.
```

- [ ] **Step 5: Update Step 1 packages — remove McpServer block, add Azure.AI.OpenAI**

Find:
```
### `HrMcp.Agent`

```bash
dotnet add src/HrMcp.Agent package Microsoft.Extensions.AI --version 9.*
dotnet add src/HrMcp.Agent package OllamaSharp --version 5.*
dotnet add src/HrMcp.Agent package ModelContextProtocol --version 1.*
```

The agent is a pure MCP client — it has no direct database access. The two project references to `HrMcp.Application` and `HrMcp.Infrastructure.Persistence` that were scaffolded in Part 1 are removed.

Why three packages:

- **`Microsoft.Extensions.AI`** — `IChatClient`, `ChatMessage`, `ChatOptions`, `AITool` abstractions
- **`OllamaSharp`** — `OllamaApiClient`, the GA-recommended Ollama provider; implements `IChatClient` natively
- **`ModelContextProtocol`** — `McpClient`, `HttpClientTransport`, `McpClientTool`; the client half of the MCP SDK

> **Note:** `Microsoft.Extensions.AI.Ollama` was the early preview provider and is now deprecated in the GA release. The official Microsoft.Extensions.AI GA guidance recommends `OllamaSharp` instead. `OllamaApiClient` (from `OllamaSharp`) implements `IChatClient` directly — no wrapper needed.

### `HrMcp.McpServer`

```bash
dotnet add src/HrMcp.McpServer package OllamaSharp --version 5.*
```

The server needs `OllamaApiClient` to power the `WriteJobDescription` tool upgrade.
```

Replace with:
```
### `HrMcp.Agent`

```bash
dotnet add src/HrMcp.Agent package Microsoft.Extensions.AI --version 9.*
dotnet add src/HrMcp.Agent package OllamaSharp --version 5.*
dotnet add src/HrMcp.Agent package Azure.AI.OpenAI --version 2.*
dotnet add src/HrMcp.Agent package ModelContextProtocol --version 1.*
```

The agent is a pure MCP client — it has no direct database access.

Why four packages:

- **`Microsoft.Extensions.AI`** — `IChatClient`, `ChatMessage`, `ChatOptions`, `AITool` abstractions
- **`OllamaSharp`** — `OllamaApiClient`, the Ollama provider; implements `IChatClient` natively. Use for local development with gemma4.
- **`Azure.AI.OpenAI`** — `AzureOpenAIClient`, for production deployment via Azure. Only active when `AI:Provider = AzureOpenAI` in config.
- **`ModelContextProtocol`** — `McpClient`, `HttpClientTransport`, `McpClientTool`; the client half of the MCP SDK

> **Note:** `Microsoft.Extensions.AI.Ollama` was the early preview provider and is now deprecated in the GA release. The official Microsoft.Extensions.AI GA guidance recommends `OllamaSharp` instead. `OllamaApiClient` (from `OllamaSharp`) implements `IChatClient` directly — no wrapper needed.

The MCP server has no AI dependency. It is a pure data layer — no LLM packages required.
```

- [ ] **Step 6: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md
git commit -m "blog(part-4): update intro, prerequisites, and Step 1 packages for multi-model support"
```

---

## Task 5: Part 4 — Update Step 3 Program.cs to show multi-model CreateChatClient

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

- [ ] **Step 1: Replace the Program.cs code block and explanation in Step 3**

Find the entire Step 3 section starting at:
```
## Step 3 — `Program.cs` for `HrMcp.Agent`

```csharp
// src/HrMcp.Agent/Program.cs
using Microsoft.Extensions.AI;
```

...through to the end of the "What each piece does" bullet list (ending before `---` and `## Step 4`).

Replace with:

````markdown
## Step 3 — `Program.cs` for `HrMcp.Agent`

`Program.cs` wires together three concerns: transport (how to reach the MCP server), LLM provider (which model to use), and the agent itself.

```csharp
// src/HrMcp.Agent/Program.cs (simplified — see GitHub for full version with OIDC + stdio transport)
using Azure.AI.OpenAI;
using Azure.Identity;
using HrMcp.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;
using Spectre.Console;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// Connect to the MCP server over HTTP (must be running on http://localhost:5100)
await using var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(configuration["McpServer:Transport:StreamHttp:Url"] ?? "http://localhost:5100/mcp"),
        TransportMode = HttpTransportMode.StreamableHttp,
        Name = "hr-mcp-stream-http"
    }));

var mcpTools = await mcpClient.ListToolsAsync();

// Style picker — 2-second auto-select, defaults to Structured
var style = UiStyle.Structured;
// ... (see GitHub for full picker code)

IChatClient chatClient = CreateChatClient(configuration);

var numCtx = configuration.GetValue<int?>("AI:Ollama:NumCtx");
var outputFolder = FindOutputFolder();
var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style, numCtx, outputFolder);
await agent.RunAsync();

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

    // Azure OpenAI
    var azureEndpoint = configuration["AI:AzureOpenAI:Endpoint"]!;
    var azureDeployment = configuration["AI:AzureOpenAI:Deployment"]!;
    var apiKey = configuration["AI:AzureOpenAI:ApiKey"];

    var client = string.IsNullOrWhiteSpace(apiKey)
        ? new AzureOpenAIClient(new Uri(azureEndpoint), new DefaultAzureCredential())
        : new AzureOpenAIClient(new Uri(azureEndpoint), new System.ClientModel.ApiKeyCredential(apiKey));

    return client.GetChatClient(azureDeployment).AsIChatClient();
}

static string FindOutputFolder()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "usajobs")))
            return Path.Combine(dir.FullName, "usajobs", "output");
    }
    return Path.GetFullPath("usajobs/output");
}
```

### Multi-Model Configuration

Provider selection is driven by `appsettings.json` — no code changes required to switch models:

```json
{
  "AI": {
    "Provider": "Ollama",
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "gemma4:latest",
      "NumCtx": 32768
    },
    "AzureOpenAI": {
      "Endpoint": "https://YOUR-RESOURCE-NAME.openai.azure.com/",
      "Deployment": "gpt-4.1-mini",
      "ApiKey": "YOUR_AZURE_OPENAI_KEY"
    }
  }
}
```

Set `AI:Provider` to `"AzureOpenAI"` to switch providers. For production, store `ApiKey` in user secrets or an environment variable — never in committed config.

### What each piece does

- **`McpClient.CreateAsync`** — creates an MCP client connected to the running server. `StreamableHttp` is the modern transport mode (replaces SSE from earlier SDK versions).
- **`mcpClient.ListToolsAsync()`** — fetches the tool list from the server. Returns `IList<McpClientTool>`. Each `McpClientTool` is an `AIFunction` (which is an `AITool`) — the bridge between the MCP protocol and `Microsoft.Extensions.AI`.
- **`CreateChatClient()`** — branches on `AI:Provider`. Returns `OllamaApiClient` for local dev or `AzureOpenAIClient` for cloud. Either way the return type is `IChatClient` — `HrAgent` never sees the difference.
- **`FindOutputFolder()`** — walks up from the binary's directory to find the `usajobs/` folder, then returns `usajobs/output/` as the save target for exported files.
- **`numCtx`** — Ollama context window size, passed to `HrAgent` as an `AdditionalProperties` hint. Ignored when using Azure OpenAI.
````

- [ ] **Step 2: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md
git commit -m "blog(part-4): replace Step 3 Program.cs with multi-model CreateChatClient pattern"
```

---

## Task 6: Part 4 — Remove Steps 4 and 5, renumber remaining steps

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

- [ ] **Step 1: Delete Step 4 — Upgrade WriteJobDescription to LLM Output**

Find and delete the entire section from:
```
## Step 4 — Upgrade `WriteJobDescription` to LLM Output
```
through to (and including) the closing `---` before `## Step 5`.

- [ ] **Step 2: Delete Step 5 — Register IChatClient in McpServer/Program.cs**

Find and delete the entire section from:
```
## Step 5 — Register `IChatClient` in `McpServer/Program.cs`
```
through to (and including) the closing `---` before `## Step 6`.

- [ ] **Step 3: Renumber Step 6 → Step 4**

Find:
```
## Step 6 — Build
```
Replace with:
```
## Step 4 — Build
```

- [ ] **Step 4: Renumber Step 7 → Step 5**

Find:
```
## Step 7 — Run a Conversation
```
Replace with:
```
## Step 5 — Run a Conversation
```

- [ ] **Step 5: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md
git commit -m "blog(part-4): remove WriteJobDescription steps, renumber remaining steps"
```

---

## Task 7: Part 4 — Add "Job Descriptions — the LLM Writes Them" section

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

- [ ] **Step 1: Insert new section after Step 5 (Run a Conversation) and before "What Happened Under the Hood"**

Find:
```
## What Happened Under the Hood
```

Insert before it:

````markdown
---

## Job Descriptions — the LLM Writes Them

Earlier versions of this series included a `WriteJobDescription` MCP tool that called Ollama server-side to generate a narrative. That tool has been removed. Here is why — and what replaced it.

**The old approach:** `JobDescriptionTools.cs` on the McpServer injected `IChatClient` and called the LLM inside the tool. The agent saw it as a black box: call tool, get text back.

**The problem:** It coupled the MCP server to a specific LLM provider and required Ollama to be reachable from the server process — even when running in Claude Desktop or a cloud deployment where only the agent has LLM access. It also meant the job description was written without conversational context (no history, no user feedback loop).

**The new approach:** The agent's system prompt instructs the model to call `GetPositionById` first, then write the job announcement itself:

```
- When asked to write a job description, call GetPositionById to get the full position
  data, then write a compelling USAJobs-style job announcement yourself with these sections:
  ## Summary, ## Duties, ## Qualifications Required, ## How to Apply.
  Use professional federal HR writing style. Be specific and engaging.
```

No extra code. No extra tool. The LLM already has the full position data from the tool call and writes the narrative in the same turn. The user can then ask for edits ("make the qualifications section stronger"), and the model refines it — something the server-side tool could never do.

The MCP server is now a **pure data layer**: it exposes data and export tools, and has no LLM dependency at all.

````

- [ ] **Step 2: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md
git commit -m "blog(part-4): add Job Descriptions — the LLM Writes Them section"
```

---

## Task 8: Part 4 — Add "Export Tools" section

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

- [ ] **Step 1: Insert Export Tools section after "Job Descriptions" and before "What Happened Under the Hood"**

Find:
```
## What Happened Under the Hood
```

Insert before it:

````markdown
---

## Export Tools

The MCP server exposes four export tools — two for Word documents, one for HTML, one for Excel. All four live in `ExportTools.cs` (and `PositionTools.cs` for HTML), and all four follow the same contract: return a JSON payload with a `fileName` and base64-encoded file `content`.

### NuGet Package

```bash
dotnet add src/HrMcp.McpServer package DocumentFormat.OpenXml --version "3.*"
```

`DocumentFormat.OpenXml` from the Open XML SDK handles both `.docx` (Word) and `.xlsx` (Excel) generation without requiring Microsoft Office to be installed.

### The Four Export Tools

| Tool | Output | File |
|---|---|---|
| `ExportPositionToHtml` | USAJobs-style HTML page | `position-{id}.html` |
| `ExportPositionToWord` | Full position data as `.docx` | `position-{id}.docx` |
| `ExportDraftToWord` | LLM-generated draft as `.docx` | `position-{id}-draft.docx` |
| `ExportPositionsToExcel` | All open positions as `.xlsx` | `positions.xlsx` |

### ExportTools.cs (key structure)

```csharp
// src/HrMcp.McpServer/Tools/ExportTools.cs
[McpServerToolType]
public sealed class ExportTools(PositionService positions, ILogger<ExportTools> logger)
{
    [McpServerTool(Name = "ExportPositionToWord"),
     Description("Exports a position's full structured data to a Word (.docx) file. Returns a JSON payload with fileName and base64-encoded content for the agent to save locally.")]
    public async Task<string> ExportPositionToWord(
        [Description("The numeric ID of the position to export")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return $"Position {positionId} not found.";
        var bytes = BuildPositionDocx(p);
        return ToBase64Json($"position-{positionId}.docx", bytes);
    }

    [McpServerTool(Name = "ExportDraftToWord"),
     Description("Exports an AI-generated job description draft to an editable Word (.docx) file. Markdown headings (##) become Word headings; **bold** spans become bold runs; * bullets become indented bullet paragraphs.")]
    public async Task<string> ExportDraftToWord(
        [Description("The numeric ID of the position the draft is for")] int positionId,
        [Description("The full job description draft text, including any user edits.")] string draftContent,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        var title = p?.Title ?? $"Position {positionId}";
        var org = p is null ? "" : $"{p.HiringOrganization?.DepartmentName} | {p.HiringOrganization?.OrganizationName}";
        var bytes = BuildDraftDocx(title, org, draftContent);
        return ToBase64Json($"position-{positionId}-draft.docx", bytes);
    }

    [McpServerTool(Name = "ExportPositionsToExcel"),
     Description("Exports all open positions to an Excel (.xlsx) spreadsheet.")]
    public async Task<string> ExportPositionsToExcel(CancellationToken ct = default)
    {
        var list = (await positions.GetOpenPositionsAsync(ct)).ToList();
        var bytes = BuildPositionsExcel(list);
        return ToBase64Json("positions.xlsx", bytes);
    }

    private static string ToBase64Json(string fileName, byte[] bytes) =>
        JsonSerializer.Serialize(new { fileName, content = Convert.ToBase64String(bytes) });
}
```

### Markdown-to-Word Rendering

`ExportDraftToWord` passes the LLM-generated draft through `AppendMarkdownContent`, which converts markdown to OpenXML:

- `## Heading` → Word Heading2 style
- `### Heading` → Word Heading3 style
- `* bullet` / `- bullet` → indented paragraph with `•` prefix
- `**bold text**` → `<Run>` with `<Bold/>` property (inline within any paragraph)

This means a draft like:

```
## Qualifications Required

* **Subject Matter Expertise:** Proven knowledge of OECD processes.
* **Analytical Acuity:** Exceptional ability to analyze complex information.
```

Opens in Word with proper heading styles, indented bullets, and bold labels — not raw markdown text.

### Registering ExportTools

```csharp
// src/HrMcp.McpServer/Program.cs
builder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<ExportTools>();
```

````

- [ ] **Step 2: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md
git commit -m "blog(part-4): add Export Tools section with Word/Excel/HTML tool descriptions"
```

---

## Task 9: Part 4 — Add "Agent-Side File Interception" section

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

- [ ] **Step 1: Insert Agent-Side File Interception section after Export Tools and before "What Happened Under the Hood"**

Find:
```
## What Happened Under the Hood
```

Insert before it:

````markdown
---

## Agent-Side File Interception

The export tools return `{ "fileName": "...", "content": "<base64>" }` — they never write files to disk themselves. The agent intercepts these results and saves the files. This is a deliberate architectural choice.

### Why base64 over the wire?

The MCP server has no knowledge of where the client is running. Consider who can call these tools:

- **This console agent** — running on your developer machine with a local filesystem
- **Claude Desktop** — running on a different machine or sandbox; the server cannot reach its filesystem
- **A future SPA client** — running in a browser, where the server has no filesystem access at all

By returning base64, the same server tool works for all three clients. Each client decodes and saves (or downloads) the file in whatever way makes sense for its environment. The server stays stateless and transport-agnostic.

### How the agent intercepts

```csharp
// src/HrMcp.Agent/HrAgent.cs

// Tools that return { "fileName": "...", "content": "<base64>" }
private static readonly HashSet<string> ExportToolNames =
    new(StringComparer.OrdinalIgnoreCase)
    {
        "ExportPositionToHtml",
        "ExportPositionToWord",
        "ExportDraftToWord",
        "ExportPositionsToExcel"
    };

// In RunToolLoopAsync, after fn.InvokeAsync():
if (ExportToolNames.Contains(call.Name ?? string.Empty))
{
    // The MCP SDK returns tool results as TextContent objects, not plain strings.
    // TextContent.Text holds the JSON payload.
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
```

The `TextContent` unwrapping (`TextContent tc => tc.Text`) is critical. The MCP SDK wraps tool return values in a `TextContent` object — calling `JsonSerializer.Serialize(rawResult)` on it produces a serialized wrapper object, not the JSON string inside it.

### TrySaveExportFile

```csharp
private static string? TrySaveExportFile(string json, string outputFolder)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("fileName", out var fileNameEl) ||
            !root.TryGetProperty("content", out var contentEl))
            return null;

        var fileName = fileNameEl.GetString();
        var base64   = contentEl.GetString();
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(base64))
            return null;

        var bytes = Convert.FromBase64String(base64);
        Directory.CreateDirectory(outputFolder);
        var fullPath = Path.GetFullPath(Path.Combine(outputFolder, fileName));
        File.WriteAllBytes(fullPath, bytes);
        return $"Saved to: {fullPath}";
    }
    catch (Exception ex)
    {
        return $"Export save failed: {ex.Message}";
    }
}
```

The return value replaces the raw tool result in `_history` — the LLM sees `"Saved to: C:\...\usajobs\output\positions.xlsx"` and can report that back to the user.

### Output folder

`FindOutputFolder()` in `Program.cs` walks up from `AppContext.BaseDirectory` looking for a `usajobs/` directory. For a standard `dotnet run` from the solution root, it resolves to `DotnetAiAgentMcp/usajobs/output/`. The folder is created if it doesn't exist.

### OpenXML flush gotcha

One non-obvious requirement: `ms.ToArray()` must be called **after** the `WordprocessingDocument` or `SpreadsheetDocument` is disposed. OpenXML writes the ZIP container to the stream on `Dispose()` — calling `ms.ToArray()` while the document is still open returns an empty or incomplete byte array. The builders use a nested `using` block to ensure disposal happens before reading the stream:

```csharp
var ms = new MemoryStream();
using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
{
    // ... build content ...
    doc.Save();
} // Dispose flushes ZIP to ms
return ms.ToArray();
```

````

- [ ] **Step 2: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md
git commit -m "blog(part-4): add Agent-Side File Interception section with architecture explanation"
```

---

## Task 10: Part 4 — Update "What Happened Under the Hood", "What We Built", and Sources

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

- [ ] **Step 1: Update "What Happened Under the Hood" to remove WriteJobDescription reference**

Find:
```
For `WriteJobDescription`, the tool itself calls Ollama internally (server-side), so from the
agent's perspective it is just another tool call that returns a string.
```

Replace with:
```
For job description requests, the agent's system prompt instructs the model to call `GetPositionById` first to fetch the full position data, then write the USAJobs-style narrative itself — no special tool needed.
```

- [ ] **Step 2: Update "Swapping Providers" section**

Find:
```
## Swapping Providers

`HrAgent.cs` depends only on `IChatClient`. To switch from Ollama to another provider, change
three lines in `Program.cs` — nothing else:
```

Replace with:
```
## Swapping Providers

`HrAgent.cs` depends only on `IChatClient`. To switch providers, change `AI:Provider` in `appsettings.json` — nothing in `HrAgent.cs` changes. For reference, here are the `CreateChatClient()` patterns for other providers:
```

- [ ] **Step 3: Update "What We Built" summary**

Find:
```
- **`HrMcp.Agent`** — console AI agent using `IChatClient` + MCP tools
- **`HrAgent.cs`** — conversation loop with system prompt and full history management
- **`McpClient` + `HttpClientTransport`** — MCP client connected to the running server
- **`UseFunctionInvocation` middleware** — automatic tool dispatch, no manual routing
- **`WriteJobDescription` upgraded** — from static stub to LLM-generated USAJobs narrative
- **`IChatClient` in McpServer** — Ollama registered as singleton, injected into the tool
- **Build** — 0 errors, 0 warnings
```

Replace with:
```
- **`HrMcp.Agent`** — console AI agent using `IChatClient` + MCP tools
- **`HrAgent.cs`** — conversation loop with system prompt, full history management, and export interception
- **`McpClient` + `HttpClientTransport`** — MCP client connected to the running server over Streamable HTTP
- **Multi-model support** — Ollama (gemma4, default) or Azure OpenAI, switched via `AI:Provider` config key
- **Job descriptions** — LLM writes them from position data; no server-side tool needed
- **`ExportTools`** — 4 export tools (HTML, Word, draft Word, Excel) returning base64 payloads
- **Agent-side interception** — `TrySaveExportFile` decodes base64 and saves to `usajobs/output/`
- **McpServer is a pure data layer** — no LLM dependency
- **Build** — 0 errors, 0 warnings
```

- [ ] **Step 4: Add Azure.AI.OpenAI to Sources**

Find:
```
- [Ollama — Download](https://ollama.com)
- [Microsoft.Extensions.AI — Announcement Blog](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/)
```

Replace with:
```
- [Ollama — Download](https://ollama.com)
- [Azure.AI.OpenAI — NuGet](https://www.nuget.org/packages/Azure.AI.OpenAI)
- [DocumentFormat.OpenXml — NuGet](https://www.nuget.org/packages/DocumentFormat.OpenXml)
- [Microsoft.Extensions.AI — Announcement Blog](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/)
```

- [ ] **Step 5: Commit**

```bash
git add blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md
git commit -m "blog(part-4): update What We Built summary, Swapping Providers, and Sources"
```

---

## Self-Review Checklist

- [ ] Part 3: No remaining references to `JobDescriptionTools`, `WriteJobDescription`, or "5 tools"
- [ ] Part 4: No remaining references to `llama3.2`, McpServer OllamaSharp, or "Upgrade WriteJobDescription"
- [ ] Part 4: `CreateChatClient()` code matches `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs` lines 290–314
- [ ] Part 4: `TrySaveExportFile` code matches `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs`
- [ ] Part 4: `ExportTools` tool descriptions match `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs`
- [ ] Part 4: `appsettings.json` JSON block matches `DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json`
