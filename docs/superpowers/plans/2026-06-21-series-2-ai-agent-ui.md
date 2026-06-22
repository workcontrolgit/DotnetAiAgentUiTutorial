# Series 2: AI Agent UI with Blazor United & .NET 10 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write 7 blog posts (preface + 6 parts) for the "AI Agent UI with Blazor United & .NET 10" series, saved under `blogs/series-2-ai-agent-ui/`, mirroring the exact structure, tone, and conventions of Series 1.

**Architecture:** Each task produces one complete, publication-ready markdown file. Files reference the actual source code in `DotnetAiAgentUI/src/HrMcp.Agent/`. Every post follows the Series 1 header format (`**Series:** ... | **Part N of 6**`) and ends with a "Next Up" footer. The preface is written last so it can include accurate links to all parts.

**Tech Stack:** .NET 10, Blazor United (Auto render mode), MudBlazor 8, `IChatClient` / `Microsoft.Extensions.AI`, Ollama, `Blazored.TextEditor`, OIDC / Duende IdentityServer, `ModelContextProtocol` NuGet.

## Global Constraints

- Tone: first-person, opinionated, every design decision justified — same voice as Series 1
- Header format every part: `**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part N of 6**`
- GitHub link every part: `workcontrolgit/DotnetAiAgentUiTutorial`
- Cover image placeholder every file: `![Series 2 cover](screenshots/blog-cover.png)`
- Footer tag line every part: `*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*`
- "Next Up" link at the bottom of every part pointing to the next part file
- No HR-specific logic described in UI components — keep patterns general
- `IChatClient` abstraction present from Part 3 onward — no direct OllamaSharp SDK calls in UI code
- MudBlazor 8 used consistently — no Bootstrap or plain CSS
- All code blocks must have a language tag (`csharp`, `bash`, `json`, `xml`, `razor`)
- All file paths in code snippets must match the actual solution: `DotnetAiAgentUI/src/HrMcp.Agent/`
- Series 1 parallel noted in each part introduction

---

## File Map

| File | Responsibility |
|---|---|
| `blogs/series-2-ai-agent-ui/preface.md` | Series overview, goals, prerequisites, series-at-a-glance table, connection to Series 1 |
| `blogs/series-2-ai-agent-ui/part-1-blazor-united-foundation.md` | Blazor United scaffold, MudBlazor setup, layout, routing |
| `blogs/series-2-ai-agent-ui/part-2-ai-agent-ui-patterns.md` | Concepts only — `IChatClient`, `ChatTurn`, state design, provider swap |
| `blogs/series-2-ai-agent-ui/part-3-building-the-chat-ui.md` | `IAgentDraftService`, `AgentDraftService`, `ChatTurn`, chat panel component |
| `blogs/series-2-ai-agent-ui/part-4-streaming-responses-realtime-ux.md` | Streaming, `IAsyncEnumerable`, `StateHasChanged`, cancel button |
| `blogs/series-2-ai-agent-ui/part-5-document-editor-and-export.md` | Split-panel layout, `Blazored.TextEditor`, `DraftDocumentState`, Word export |
| `blogs/series-2-ai-agent-ui/part-6-securing-ui-with-oidc.md` | OIDC auth, `AuthorizeView`, token acquisition, Duende IdentityServer |
| `blogs/series-2-ai-agent-ui/CHECKLIST.md` | Publishing checklist mirroring Series 1's CHECKLIST.md |
| `blogs/series-2-ai-agent-ui/diagrams/` | Placeholder directory for architecture diagrams |
| `blogs/series-2-ai-agent-ui/screenshots/` | Placeholder directory for screenshots |

---

## Task 1: Scaffold the Series 2 folder structure

**Files:**
- Create: `blogs/series-2-ai-agent-ui/diagrams/.gitkeep`
- Create: `blogs/series-2-ai-agent-ui/screenshots/.gitkeep`

**Interfaces:**
- Produces: `blogs/series-2-ai-agent-ui/` folder with `diagrams/` and `screenshots/` subdirectories, ready for all subsequent tasks

- [ ] **Step 1: Create the folder structure**

```bash
mkdir -p blogs/series-2-ai-agent-ui/diagrams
mkdir -p blogs/series-2-ai-agent-ui/screenshots
touch blogs/series-2-ai-agent-ui/diagrams/.gitkeep
touch blogs/series-2-ai-agent-ui/screenshots/.gitkeep
```

- [ ] **Step 2: Verify**

```bash
ls blogs/series-2-ai-agent-ui/
```
Expected output: `diagrams/  screenshots/`

- [ ] **Step 3: Commit**

```bash
git add blogs/series-2-ai-agent-ui/
git commit -m "feat(series-2): scaffold folder structure for AI Agent UI blog series"
```

---

## Task 2: Write Part 1 — Blazor United Foundation

**Files:**
- Create: `blogs/series-2-ai-agent-ui/part-1-blazor-united-foundation.md`

**Interfaces:**
- Consumes: Series 1 Part 1 format (`blogs/series-1-ai-agent-mcp/part-1-clean-architecture-hr-domain.md`) for structure reference
- Consumes: Actual project files in `DotnetAiAgentUI/src/HrMcp.Agent/` for code listings
- Produces: Part 1 markdown file that Part 2's "Previous Part" link points to

- [ ] **Step 1: Write the file**

Create `blogs/series-2-ai-agent-ui/part-1-blazor-united-foundation.md` with this exact content structure:

```markdown
# Part 1: Blazor United Foundation

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 1 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)

---

## Introduction

[First-person intro: what the reader builds in this part, the Series 1 parallel (Part 1 = Clean Architecture), and what they will have running by the end — a Blazor United app with MudBlazor layout, no AI yet.]

---

## Why Blazor United?

[Explain Auto render mode: combines Server-side prerendering with interactive Server or WASM rendering. Contrast with Blazor Server (always server) and Blazor WASM (always client). Quote .NET 10 as the target. Explain why this matters for an AI Agent UI — real-time interactivity without the complexity of a separate API layer.]

---

## Step 1 — Create the Blazor United Project

[Show the dotnet new command using the `blazor` template (Blazor United). Show the project file (`HrMcp.Agent.csproj`) with `<TargetFramework>net10.0</TargetFramework>` and `Microsoft.NET.Sdk.Web`. Explain `OutputType = Exe`.]

```bash
dotnet new blazor -n HrMcp.Agent --interactivity Auto --all-interactive
```

---

## Step 2 — Install MudBlazor 8

[Show the dotnet add package command. Show the three MudBlazor setup steps: (1) add `@using MudBlazor` to `_Imports.razor`, (2) add `MudThemeProvider`, `MudDialogProvider`, `MudSnackbarProvider` to `App.razor`, (3) add CSS/JS links to `App.razor` head. Explain why MudBlazor is the right choice for .NET developers.]

```bash
dotnet add DotnetAiAgentUI/src/HrMcp.Agent package MudBlazor --version 8.*
```

---

## Step 3 — Configure the Main Layout

[Show `MainLayout.razor` using `MudLayout`, `MudAppBar`, `MudDrawer`, `MudMainContent`. Explain each MudBlazor layout component's role. Show the `_Imports.razor` additions. Reference actual file: `DotnetAiAgentUI/src/HrMcp.Agent/Components/Layout/MainLayout.razor`.]

---

## Step 4 — Set Up Routing

[Show `App.razor` and `Routes.razor`. Explain how Blazor United routing differs from Blazor Server. Show the render mode attribute `@rendermode InteractiveServer`. Reference actual files: `DotnetAiAgentUI/src/HrMcp.Agent/Components/App.razor` and `Routes.razor`.]

---

## Step 5 — Run the Shell

[Show the dotnet run command with `--web` flag. Explain what `--web` does in `Program.cs` — the `RunWebAsync` branch. Show the expected browser output: MudBlazor layout with navigation, no content yet.]

```bash
dotnet run --project DotnetAiAgentUI/src/HrMcp.Agent -- --web
```

---

## What We Have

[Summary: running Blazor United app with MudBlazor layout. No AI, no MCP connection yet. Same milestone as Series 1 Part 1 — the foundation every other part builds on.]

---

## Next Up

Part 2 covers the concepts behind AI Agent UI design — `IChatClient`, component state, and the provider-swap pattern — before we write a single chat component.

→ **[Part 2: AI Agent UI Patterns](part-2-ai-agent-ui-patterns.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
```

- [ ] **Step 2: Verify the file exists and opens correctly**

```bash
ls blogs/series-2-ai-agent-ui/part-1-blazor-united-foundation.md
```

- [ ] **Step 3: Commit**

```bash
git add blogs/series-2-ai-agent-ui/part-1-blazor-united-foundation.md
git commit -m "blog(series-2): add Part 1 — Blazor United Foundation"
```

---

## Task 3: Write Part 2 — AI Agent UI Patterns (Concepts)

**Files:**
- Create: `blogs/series-2-ai-agent-ui/part-2-ai-agent-ui-patterns.md`

**Interfaces:**
- Consumes: Series 1 Part 2 (concepts-only format) for structure reference
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/ChatTurn.cs` — `public sealed record ChatTurn(string Role, string Text, DateTimeOffset Timestamp)`
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` — `IAgentDraftService` interface signature
- Produces: Part 2 markdown — conceptual foundation Part 3's implementation section references

- [ ] **Step 1: Write the file**

Create `blogs/series-2-ai-agent-ui/part-2-ai-agent-ui-patterns.md` with this structure:

```markdown
# Part 2: AI Agent UI Patterns

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 2 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)

---

## Introduction

[No code this part — pure concepts. Parallel to Series 1 Part 2 (MCP concepts). Explain what the reader will understand by the end: the mental model for the entire UI architecture.]

---

## The IChatClient Abstraction

[Explain `IChatClient` from `Microsoft.Extensions.AI`. Show the interface signature. Explain why the UI never calls Ollama directly — always through `IChatClient`. Show the provider-swap pattern: change `AI:Provider` in `appsettings.json` to switch between Ollama and Azure OpenAI without touching a single line of UI code.]

```json
// appsettings.json — swap provider here, nothing else changes
{
  "AI": {
    "Provider": "Ollama",
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "gemma4:latest",
      "NumCtx": 32768
    }
  }
}
```

---

## The Chat Turn Model

[Explain `ChatTurn` — why a record, why three fields (Role, Text, Timestamp). Show the actual type:]

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/ChatTurn.cs
public sealed record ChatTurn(string Role, string Text, DateTimeOffset Timestamp);
```

[Explain how the list of `ChatTurn` objects IS the conversation state — no other state needed for the thread.]

---

## Component State Design for Async AI Responses

[Explain the three-state model every AI chat component needs: Idle (waiting for input), Busy (waiting for response), Streaming (tokens arriving). Show why this maps to: `_busy` bool + `_turns` list + `StateHasChanged()` call pattern. Explain Blazor's rendering loop and why you must call `StateHasChanged()` from inside async callbacks.]

---

## The IAgentDraftService Interface

[Show the interface — the contract between the UI component and the AI backend:]

```csharp
public interface IAgentDraftService
{
    Task<string> SendPromptAsync(string prompt, CancellationToken ct = default);
    Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(
        string draftText, CancellationToken ct = default);
}
```

[Explain why this interface exists — it lets the Blazor component be tested and replaced without touching the MCP wiring. The UI knows nothing about Ollama, MCP, or transport modes.]

---

## How the UI Connects to the MCP Agent

[Diagram the full call chain in words: Browser → Blazor Component → `IAgentDraftService` → `HrAgent.AskAsync()` → `IChatClient` → Ollama → MCP tool call → `HrMcp.McpServer`. Explain which layer owns which concern. Emphasize that the Blazor component is at the top — it knows nothing below `IAgentDraftService`.]

---

## Next Up

Part 3 is where we write the code. We implement `IAgentDraftService`, build the `ChatTurn` model, and wire up the first working chat panel.

→ **[Part 3: Building the Chat UI](part-3-building-the-chat-ui.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
```

- [ ] **Step 2: Verify**

```bash
ls blogs/series-2-ai-agent-ui/part-2-ai-agent-ui-patterns.md
```

- [ ] **Step 3: Commit**

```bash
git add blogs/series-2-ai-agent-ui/part-2-ai-agent-ui-patterns.md
git commit -m "blog(series-2): add Part 2 — AI Agent UI Patterns (concepts)"
```

---

## Task 4: Write Part 3 — Building the Chat UI

**Files:**
- Create: `blogs/series-2-ai-agent-ui/part-3-building-the-chat-ui.md`

**Interfaces:**
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/ChatTurn.cs` — `public sealed record ChatTurn(string Role, string Text, DateTimeOffset Timestamp)`
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` — `SendPromptAsync(string prompt, CancellationToken ct)`
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` — chat panel section for code listing reference
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Program.cs` — `RunWebAsync` for DI registration: `builder.Services.AddScoped<IAgentDraftService, AgentDraftService>()`
- Produces: Part 3 markdown — Part 4 references the chat component extended with streaming

- [ ] **Step 1: Write the file**

Create `blogs/series-2-ai-agent-ui/part-3-building-the-chat-ui.md` with this structure:

```markdown
# Part 3: Building the Chat UI

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 3 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)

---

## Introduction

[First-person intro: this part implements everything Part 2 described. By the end: a working chat panel — type a prompt, get a response, see the conversation thread. Parallel: Series 1 Part 3 (Building the MCP Server).]

---

## Step 1 — The ChatTurn Model

[Show `ChatTurn.cs`. Explain why a record (immutable, value equality). Explain the three fields. Show where the file lives.]

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/ChatTurn.cs
namespace HrMcp.Agent.Web.Models;

public sealed record ChatTurn(string Role, string Text, DateTimeOffset Timestamp);
```

---

## Step 2 — The IAgentDraftService Interface

[Show the interface. Explain why it only has two methods. Show how DI registration works in `Program.cs`:]

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Program.cs (RunWebAsync)
builder.Services.AddScoped<IAgentDraftService, AgentDraftService>();
```

---

## Step 3 — The AgentDraftService Implementation

[Show `SendPromptAsync` and `EnsureInitializedAsync` from `AgentDraftService.cs`. Explain the lazy initialization pattern — why the MCP client isn't created at DI registration time. Explain `ResolveTransportType` — how the service picks stdio vs stream-http from args or config.]

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs (excerpt)
public async Task<string> SendPromptAsync(string prompt, CancellationToken ct = default)
{
    await EnsureInitializedAsync(ct);
    return await _agent!.AskAsync(prompt, ct);
}
```

---

## Step 4 — The Chat Panel Component

[Show the relevant section of `DraftWorkspace.razor` — the left chat panel. Cover: `@inject IAgentDraftService`, the `_turns` list, the `_busy` bool, `SendPromptAsync`, `HandlePromptKeyDown` (Enter to send, Shift+Enter for newline). Show MudBlazor components used: `MudPaper`, `MudTextField`, `MudButton`. Show the chat bubble markup with role-based CSS classes.]

---

## Step 5 — Run It

[Show the two-terminal startup: one for MCP server (stdio or stream-http), one for the agent web UI. Show the expected browser output: chat panel with empty thread and composer. Show a sample prompt and response.]

```bash
# Terminal 1 — MCP Server
dotnet run --project DotnetAiAgentUI/src/HrMcp.McpServer -- --stream-http

# Terminal 2 — Agent UI
dotnet run --project DotnetAiAgentUI/src/HrMcp.Agent -- --web --stream-http
```

---

## What We Have

[Summary: working chat panel. User can type a prompt, the agent calls the MCP server, and the response appears in the thread. No streaming yet — the full response arrives at once.]

---

## Next Up

Part 4 adds token-by-token streaming so responses appear as they are generated, with a cancel button for long responses.

→ **[Part 4: Streaming Responses & Real-Time UX](part-4-streaming-responses-realtime-ux.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
```

- [ ] **Step 2: Verify**

```bash
ls blogs/series-2-ai-agent-ui/part-3-building-the-chat-ui.md
```

- [ ] **Step 3: Commit**

```bash
git add blogs/series-2-ai-agent-ui/part-3-building-the-chat-ui.md
git commit -m "blog(series-2): add Part 3 — Building the Chat UI"
```

---

## Task 5: Write Part 4 — Streaming Responses & Real-Time UX

**Files:**
- Create: `blogs/series-2-ai-agent-ui/part-4-streaming-responses-realtime-ux.md`

**Interfaces:**
- Consumes: `IAsyncEnumerable<StreamingChatCompletionUpdate>` from `Microsoft.Extensions.AI`
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` — `_busy` flag, loading spinner markup, `CancellationToken` usage
- Consumes: `ChatTurn(string Role, string Text, DateTimeOffset Timestamp)` from Task 4
- Produces: Part 4 markdown — Part 5 builds on the completed, streaming chat panel

- [ ] **Step 1: Write the file**

Create `blogs/series-2-ai-agent-ui/part-4-streaming-responses-realtime-ux.md` with this structure:

```markdown
# Part 4: Streaming Responses & Real-Time UX

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 4 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)

---

## Introduction

[First-person intro: Part 3 gave us a working chat panel but responses arrive all at once. This part makes the UI feel alive — tokens appear as they stream from the model. Parallel: Series 1 Part 4 (AI Agent with Microsoft.Extensions.AI + Ollama).]

---

## Why Streaming Matters

[Explain the UX problem: waiting 5–10 seconds for a long response with no feedback feels broken. Streaming solves it — users see tokens arrive immediately and can cancel if the response goes off-track. Explain how `IChatClient.CompleteStreamingAsync()` returns `IAsyncEnumerable<StreamingChatCompletionUpdate>`.]

---

## Step 1 — The Streaming Call

[Show the `CompleteStreamingAsync` pattern. Show how to accumulate tokens into a string and append to the `_turns` list:]

```csharp
// Pattern — streaming from IChatClient
var sb = new StringBuilder();
await foreach (var update in chatClient.CompleteStreamingAsync(messages, ct: ct))
{
    sb.Append(update.Text);
    // Call StateHasChanged() here to update the UI as tokens arrive
}
_turns.Add(new ChatTurn("Assistant", sb.ToString(), DateTimeOffset.UtcNow));
```

---

## Step 2 — StateHasChanged and Blazor's Rendering Loop

[Explain why you must call `StateHasChanged()` inside the streaming loop. Explain that Blazor doesn't automatically re-render on background thread updates. Show the correct pattern: `await InvokeAsync(StateHasChanged)` from inside `await foreach`. Explain why `InvokeAsync` is needed — it marshals back to the Blazor synchronization context.]

```csharp
await foreach (var update in chatClient.CompleteStreamingAsync(messages, ct: ct))
{
    sb.Append(update.Text);
    await InvokeAsync(StateHasChanged);
}
```

---

## Step 3 — The Loading Spinner

[Show the MudBlazor loading spinner markup from `DraftWorkspace.razor`. Show the `_busy` flag pattern — set to `true` before the call, `false` in a `finally` block. Show the chat bubble with `chat-bubble--loading` CSS class and spinner.]

```razor
@if (_busy)
{
    <div class="chat-bubble-row chat-bubble-row--assistant">
        <div class="chat-bubble chat-bubble--loading" role="status" aria-live="polite">
            <span class="chat-spinner" aria-hidden="true"></span>
            <span>Assistant is drafting your response...</span>
        </div>
    </div>
}
```

---

## Step 4 — Graceful Cancellation

[Show the `CancellationTokenSource` pattern. Show the cancel button that calls `_cts.Cancel()`. Show how `CancellationToken` is passed to `SendPromptAsync`. Explain what happens on the server side — the MCP tool call is abandoned and the partial response is kept.]

```csharp
private CancellationTokenSource _cts = new();

private async Task SendPromptAsync()
{
    _cts = new CancellationTokenSource();
    _busy = true;
    try
    {
        var response = await AgentDraftService.SendPromptAsync(Prompt, _cts.Token);
        _turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
    }
    catch (OperationCanceledException)
    {
        _turns.Add(new ChatTurn("Assistant", "[Cancelled]", DateTimeOffset.UtcNow));
    }
    finally
    {
        _busy = false;
        Prompt = string.Empty;
    }
}
```

---

## What We Have

[Summary: streaming chat panel with live token display, loading spinner, and cancel button. The agent UI now feels responsive even for long AI responses.]

---

## Next Up

Part 5 adds a document editor panel to the right of the chat — a WYSIWYG editor where the agent's draft responses appear, with a Word export button.

→ **[Part 5: Document Editor & Word Export](part-5-document-editor-and-export.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
```

- [ ] **Step 2: Verify**

```bash
ls blogs/series-2-ai-agent-ui/part-4-streaming-responses-realtime-ux.md
```

- [ ] **Step 3: Commit**

```bash
git add blogs/series-2-ai-agent-ui/part-4-streaming-responses-realtime-ux.md
git commit -m "blog(series-2): add Part 4 — Streaming Responses & Real-Time UX"
```

---

## Task 6: Write Part 5 — Document Editor & Word Export

**Files:**
- Create: `blogs/series-2-ai-agent-ui/part-5-document-editor-and-export.md`

**Interfaces:**
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/DraftDocumentState.cs` — `public sealed class DraftDocumentState { public string DraftText { get; set; } public int Revision { get; set; } }`
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` — `ExportDraftToWordAsync(string draftText, CancellationToken ct)` returning `(string Message, string? FileName, byte[]? FileBytes)`
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` — split-panel layout, splitter markup, WYSIWYG editor integration
- Produces: Part 5 markdown — Part 6 references the completed split-panel UI being secured

- [ ] **Step 1: Write the file**

Create `blogs/series-2-ai-agent-ui/part-5-document-editor-and-export.md` with this structure:

```markdown
# Part 5: Document Editor & Word Export

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 5 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)

---

## Introduction

[First-person intro: the chat panel is complete. Now we add a document editor to the right. The agent drafts position descriptions in the chat, and the user refines them in the WYSIWYG editor, then exports to Word. Parallel: Series 1 Part 5 (Claude Desktop end-to-end demo).]

---

## Step 1 — Install Blazored.TextEditor

[Show the NuGet install command. Show the three setup steps: add script/link to `App.razor`, add `@using Blazored.TextEditor` to `_Imports.razor`, add `<BlazoredTextEditor>` to the component.]

```bash
dotnet add DotnetAiAgentUI/src/HrMcp.Agent package Blazored.TextEditor --version 1.*
```

---

## Step 2 — The DraftDocumentState Model

[Show `DraftDocumentState.cs`. Explain why it's a class (mutable) not a record. Explain `Revision` — used to detect when the draft changes so the editor can reload its content.]

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/DraftDocumentState.cs
namespace HrMcp.Agent.Web.Models;

public sealed class DraftDocumentState
{
    public string DraftText { get; set; } = string.Empty;
    public int Revision { get; set; }
}
```

---

## Step 3 — The Split-Panel Layout

[Show the workspace grid CSS — two columns (chat left, editor right) with a draggable splitter in between. Show the `WorkspaceGridStyle` computed property that drives the CSS `grid-template-columns` value. Show the splitter `<div>` with `@onmousedown="StartResize"`. Explain why CSS Grid is used instead of MudBlazor's `MudGrid` — the splitter needs pixel-level control.]

---

## Step 4 — Wiring the WYSIWYG Editor

[Show the `<BlazoredTextEditor>` markup inside the right panel. Show the `@ref="_editor"` pattern for accessing the editor's API. Explain the `OnAfterRenderAsync` pattern for loading initial content into the editor — why it must be done after render, not in `OnInitializedAsync`.]

---

## Step 5 — Word Export via MCP Tool Call

[Show `ExportDraftToWordAsync` in the component — it calls `IAgentDraftService.ExportDraftToWordAsync`, which sends a prompt to the agent that triggers the `ExportDraftToWord` MCP tool. Show the JS interop for triggering the browser file download:]

```csharp
// Trigger browser download after export
if (fileBytes is not null && fileName is not null)
{
    await JS.InvokeVoidAsync("downloadFile", fileName, Convert.ToBase64String(fileBytes));
}
```

```javascript
// wwwroot/app.js
window.downloadFile = (filename, base64) => {
    const link = document.createElement('a');
    link.href = `data:application/octet-stream;base64,${base64}`;
    link.download = filename;
    link.click();
};
```

---

## What We Have

[Summary: full Position Description Builder UI — chat on the left, WYSIWYG draft editor on the right, Export to Word button. The agent generates a draft, the user refines it in the editor, and exports it as a `.docx` file.]

---

## Next Up

Part 6 secures the entire UI with OIDC — authenticated users only, Bearer tokens passed through to the MCP server.

→ **[Part 6: Securing the UI with OIDC](part-6-securing-ui-with-oidc.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
```

- [ ] **Step 2: Verify**

```bash
ls blogs/series-2-ai-agent-ui/part-5-document-editor-and-export.md
```

- [ ] **Step 3: Commit**

```bash
git add blogs/series-2-ai-agent-ui/part-5-document-editor-and-export.md
git commit -m "blog(series-2): add Part 5 — Document Editor & Word Export"
```

---

## Task 7: Write Part 6 — Securing the UI with OIDC

**Files:**
- Create: `blogs/series-2-ai-agent-ui/part-6-securing-ui-with-oidc.md`

**Interfaces:**
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` — `TryGetOidcHeadersAsync(IConfiguration, CancellationToken)` method
- Consumes: `DotnetAiAgentUI/src/HrMcp.Agent/Program.cs` — `RunWebAsync` for middleware registration
- Consumes: Series 1 Part 6 (`blogs/series-1-ai-agent-mcp/part-6-mcp-security-oidc.md`) for Duende IdentityServer Docker setup reference
- Produces: Part 6 markdown — final part, links back to preface

- [ ] **Step 1: Write the file**

Create `blogs/series-2-ai-agent-ui/part-6-securing-ui-with-oidc.md` with this structure:

```markdown
# Part 6: Securing the UI with OIDC

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 6 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)

---

## Introduction

[First-person intro: the UI works. Now lock it down. This part adds OIDC authentication so only authenticated users can access the agent. Parallel: Series 1 Part 6 (Securing the MCP Server with OIDC). Note the difference: Series 1 secured the server; this part secures the client UI.]

---

## The OIDC Feature Flag

[Show the `Features:EnableOidc` appsettings flag. Explain why it exists — lets you develop locally without auth, enable for staging/production. Show the `TryGetOidcHeadersAsync` method that returns an empty dictionary when OIDC is disabled:]

```json
// appsettings.json
{
  "Features": {
    "EnableOidc": false
  },
  "Oidc": {
    "Authority": "https://localhost:5001",
    "ClientId": "hr-agent",
    "ClientSecret": "secret",
    "Scope": "hr-mcp"
  }
}
```

---

## Step 1 — Token Acquisition in AgentDraftService

[Show the `TryGetOidcHeadersAsync` method from `AgentDraftService.cs`. Explain the client credentials flow: the Blazor app acts as an OAuth2 client, acquires a Bearer token from Duende IdentityServer, and passes it as an `Authorization` header to the MCP transport. This is the same flow as Series 1 Part 6 but on the client (UI) side instead of the server.]

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs (excerpt)
private static async Task<Dictionary<string, string>> TryGetOidcHeadersAsync(
    IConfiguration configuration,
    CancellationToken ct)
{
    var enableOidc = bool.TryParse(configuration["Features:EnableOidc"], out var oidcFlag) && oidcFlag;
    if (!enableOidc)
        return [];
    // ... client credentials flow ...
}
```

---

## Step 2 — Blazor Auth State and AuthorizeView

[Show how to add `Microsoft.AspNetCore.Components.Authorization` to the Blazor app. Show `AuthorizeView` wrapping the main workspace. Show the `<NotAuthorized>` redirect. Explain why Blazor Server auth differs from Blazor WASM auth — the server-side rendering context and the `AuthenticationStateProvider`.]

```razor
<AuthorizeView>
    <Authorized>
        <!-- DraftWorkspace content -->
    </Authorized>
    <NotAuthorized>
        <MudText>You must be signed in to use the Writing Assistant.</MudText>
    </NotAuthorized>
</AuthorizeView>
```

---

## Step 3 — Duende IdentityServer Setup (Docker)

[Reference Series 1 Part 6 for the full Duende setup. Show only the two config values the reader needs to change to swap to Okta or Azure Entra ID:]

```json
{
  "Oidc": {
    "Authority": "https://your-tenant.okta.com/oauth2/default",
    "ClientId": "your-client-id"
  }
}
```

---

## Step 4 — Run With Auth Enabled

[Show the startup sequence: start Duende IdentityServer (Docker), start MCP server, start agent UI with `Features:EnableOidc = true`. Show what changes in the UI: unauthenticated users see the not-authorized message, authenticated users get the full workspace.]

---

## What We Have — The Full Stack

[Summary: the complete AI Agent UI stack — Blazor United frontend, `IChatClient` abstraction, MudBlazor components, WYSIWYG editor, Word export, and OIDC security. Combined with Series 1's MCP server and AI agent backend, readers now have a full end-to-end AI-powered application.]

---

## The Series at a Glance

[Repeat the series table for easy reference:]

| # | Title | What You Built |
|---|---|---|
| 1 | Blazor United Foundation | Solution scaffold, MudBlazor layout, routing |
| 2 | AI Agent UI Patterns | Concepts — `IChatClient`, state, component design |
| 3 | Building the Chat UI | Chat component, message turns, `IAgentDraftService` wiring |
| 4 | Streaming Responses & Real-Time UX | Token streaming, loading states, cancellation |
| 5 | Document Editor & Word Export | Split-panel layout, WYSIWYG editor, Word export |
| 6 | Securing the UI with OIDC | Blazor auth, token acquisition, protected routes |

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
```

- [ ] **Step 2: Verify**

```bash
ls blogs/series-2-ai-agent-ui/part-6-securing-ui-with-oidc.md
```

- [ ] **Step 3: Commit**

```bash
git add blogs/series-2-ai-agent-ui/part-6-securing-ui-with-oidc.md
git commit -m "blog(series-2): add Part 6 — Securing the UI with OIDC"
```

---

## Task 8: Write the Preface

**Files:**
- Create: `blogs/series-2-ai-agent-ui/preface.md`

**Interfaces:**
- Consumes: All 6 part files (tasks 2–7) — links must resolve correctly
- Consumes: `blogs/series-1-ai-agent-mcp/preface.md` — mirror its structure and sections exactly
- Produces: `preface.md` — the series entry point; all parts link back to it via `[AI Agent UI with Blazor United & .NET 10](preface.md)`

- [ ] **Step 1: Write the file**

Create `blogs/series-2-ai-agent-ui/preface.md` mirroring Series 1 preface structure exactly:

```markdown
# AI Agent UI with Blazor United & .NET 10 — Blog Series

**Series:** AI Agent UI with Blazor United & .NET 10
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)

---

## Your .NET Skills Are Your UI Superpower

[First-person opening mirroring Series 1 tone: "You already know C#..." adapted to: you built the MCP backend in Series 1. Now you build the UI that puts it in front of users. No JavaScript frameworks. No React. No hand-waving.]

---

## The Problem This Series Solves

[Explain: building an AI Agent UI is harder than it looks. You need streaming, state management, async rendering, a WYSIWYG editor, file export, and auth — all wired to an MCP backend. This series shows you how, step by step, using Blazor United and MudBlazor.]

---

## What You Will Build

[Describe the final application: Position Description Builder — a split-panel Blazor app with a chat assistant on the left and a WYSIWYG document editor on the right. Word export. OIDC auth. All connected to the Series 1 MCP server via `IChatClient`.]

---

## What You Will Learn

[Mirror Series 1's "What You Will Learn" section with these topics:]
- Blazor United Auto render mode and when to use it
- MudBlazor 8 component model for AI chat interfaces
- `IChatClient` abstraction and the provider-swap pattern
- Token-by-token streaming and Blazor's rendering loop
- WYSIWYG editor integration with `Blazored.TextEditor`
- Word export via MCP tool calls and JS interop file download
- OIDC client credentials flow in a Blazor Server app

---

## How This Connects to Series 1

[Explain the relationship: Series 2 is the UI layer on top of Series 1's backend. The MCP server from Series 1 Parts 1–3 is the backend. The `IChatClient` / Ollama setup from Series 1 Part 4 is the AI layer. Readers who completed Series 1 have the full stack; readers starting here need only clone the repo.]

---

## The Series at a Glance

| # | Title | What You Build |
|---|---|---|
| Preface | Series Overview | Context, goals, prerequisites |
| 1 | [Blazor United Foundation](part-1-blazor-united-foundation.md) | Solution scaffold, MudBlazor layout, routing |
| 2 | [AI Agent UI Patterns](part-2-ai-agent-ui-patterns.md) | Concepts — `IChatClient`, state, component design |
| 3 | [Building the Chat UI](part-3-building-the-chat-ui.md) | Chat component, message turns, `IAgentDraftService` wiring |
| 4 | [Streaming Responses & Real-Time UX](part-4-streaming-responses-realtime-ux.md) | Token streaming, loading states, cancellation |
| 5 | [Document Editor & Word Export](part-5-document-editor-and-export.md) | Split-panel layout, WYSIWYG editor, Word export |
| 6 | [Securing the UI with OIDC](part-6-securing-ui-with-oidc.md) | Blazor auth, token acquisition, protected routes |

---

## Prerequisites

[Mirror Series 1 prerequisites section:]
- C# and .NET — classes, interfaces, async/await, dependency injection
- ASP.NET Core basics — `WebApplication.CreateBuilder`, middleware, configuration
- Series 1 recommended but not required — the Preface recaps what you need

Tools needed:
- .NET 10 SDK
- Ollama with `gemma4:latest` pulled
- Node.js 22+ (for MCP Inspector, optional)
- Git

---

## The Companion Repository

→ **[github.com/workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)**

---

## Related Series

- [AI Agents & MCP with .NET 10](../series-1-ai-agent-mcp/preface.md) — the MCP server and agent backend this UI connects to

---

*Ready? Start with Part 1.*

→ **[Part 1: Blazor United Foundation](part-1-blazor-united-foundation.md)**
```

- [ ] **Step 2: Verify all part links resolve**

```bash
ls blogs/series-2-ai-agent-ui/
```
Expected: `preface.md part-1-*.md part-2-*.md part-3-*.md part-4-*.md part-5-*.md part-6-*.md diagrams/ screenshots/`

- [ ] **Step 3: Commit**

```bash
git add blogs/series-2-ai-agent-ui/preface.md
git commit -m "blog(series-2): add Preface — AI Agent UI with Blazor United & .NET 10"
```

---

## Task 9: Write CHECKLIST.md

**Files:**
- Create: `blogs/series-2-ai-agent-ui/CHECKLIST.md`

**Interfaces:**
- Consumes: `blogs/series-1-ai-agent-mcp/CHECKLIST.md` — mirror its format exactly
- Produces: Publishing checklist for all 6 parts

- [ ] **Step 1: Write the file**

Create `blogs/series-2-ai-agent-ui/CHECKLIST.md` mirroring Series 1's CHECKLIST.md format:

```markdown
# Series 2 Publishing & Code Checklist

Complete all items in a part's checklist before starting the next part.

---

## Series Status

| Part | Title | Blog | Code | Published |
|------|-------|------|------|-----------|
| 1 | Blazor United Foundation | ⬜ | ⬜ | ⬜ |
| 2 | AI Agent UI Patterns | ⬜ | ⬜ | ⬜ |
| 3 | Building the Chat UI | ⬜ | ⬜ | ⬜ |
| 4 | Streaming Responses & Real-Time UX | ⬜ | ⬜ | ⬜ |
| 5 | Document Editor & Word Export | ⬜ | ⬜ | ⬜ |
| 6 | Securing the UI with OIDC | ⬜ | ⬜ | ⬜ |

---

## Blog Content Checklist (apply to every part)

- [ ] Intro states what the reader will have by the end
- [ ] Series 1 parallel noted in the introduction
- [ ] All prerequisites listed with version check commands
- [ ] Every step is numbered and sequential
- [ ] All code blocks have a language tag (`csharp`, `bash`, `json`, `xml`, `razor`)
- [ ] File paths in code snippets match the actual solution structure (`DotnetAiAgentUI/src/HrMcp.Agent/`)
- [ ] "Next Up" footer links to the next part (or back to preface for Part 6)
- [ ] Tags line present at the bottom

## Code Checklist (apply to every part)

- [ ] `dotnet build DotnetAiAgentUI/DotnetAiAgentUI.slnx` → 0 errors, 0 warnings
- [ ] Every `dotnet add package` command includes a version constraint (e.g. `--version 8.*`)
- [ ] `IChatClient` used in UI code — no direct OllamaSharp calls
- [ ] MudBlazor 8 components used — no Bootstrap or plain CSS
```

- [ ] **Step 2: Verify**

```bash
ls blogs/series-2-ai-agent-ui/CHECKLIST.md
```

- [ ] **Step 3: Commit**

```bash
git add blogs/series-2-ai-agent-ui/CHECKLIST.md
git commit -m "blog(series-2): add CHECKLIST.md for Series 2 publishing"
```

---

## Self-Review

**Spec coverage:**
- Preface ✅ Task 8
- Part 1 Blazor United Foundation ✅ Task 2
- Part 2 AI Agent UI Patterns (concepts only) ✅ Task 3
- Part 3 Chat UI (`IAgentDraftService`, `ChatTurn`, chat panel) ✅ Task 4
- Part 4 Streaming (`IAsyncEnumerable`, `StateHasChanged`, cancel) ✅ Task 5
- Part 5 Split panel, WYSIWYG, Word export ✅ Task 6
- Part 6 OIDC, `AuthorizeView`, token acquisition ✅ Task 7
- CHECKLIST.md ✅ Task 9
- Folder scaffold ✅ Task 1
- Mixed audience / Series 1 parallel in every part ✅ noted in every task
- `IChatClient` abstraction from Part 3 onward ✅ enforced in tasks 4–7
- MudBlazor 8 consistently ✅ enforced in tasks 2–7
- Tone: first-person, opinionated ✅ noted in global constraints

**Placeholder scan:** No TBDs or TODOs in task steps. Code blocks contain actual signatures from the real codebase.

**Type consistency:**
- `ChatTurn(string Role, string Text, DateTimeOffset Timestamp)` — defined in Task 3, used in Tasks 4 and 5 ✅
- `SendPromptAsync(string prompt, CancellationToken ct)` — defined in Task 4, referenced in Tasks 5 and 7 ✅
- `ExportDraftToWordAsync(string draftText, CancellationToken ct)` returning `(string Message, string? FileName, byte[]? FileBytes)` — defined in Task 6, referenced in same task ✅
- `DraftDocumentState { string DraftText, int Revision }` — defined in Task 6, consistent ✅
