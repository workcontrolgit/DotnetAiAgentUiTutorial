# In-Place Console-To-Blazor MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the console-first `HrMcp.Agent` experience with a Blazor Server split-view UI (left chat, right JD/PD editor) while keeping changes minimal and preserving the existing console path during migration.

**Architecture:** Use an in-place upgrade in the existing `HrMcp.Agent` project. Keep orchestration logic in the same project for MVP to avoid broad refactoring and blog rewrite churn. Add a web host mode and thin UI services that reuse existing MCP + AI flow.

**Tech Stack:** .NET 10, Blazor Server, MudBlazor, existing MCP client (`ModelContextProtocol.Client`), existing AI providers (`IChatClient`), Serilog.

---

## File Structure And Responsibilities

- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj`
  - Switch to web-capable project setup and add MudBlazor dependencies.
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs`
  - Add runtime mode switch: `--web` for Blazor host, default console path unchanged.
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Web/Services/AgentDraftService.cs`
  - MVP thin service to reuse existing AI + MCP draft generation in web mode.
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Web/Models/ChatTurn.cs`
  - UI chat message model.
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Web/Models/DraftDocumentState.cs`
  - Right-panel draft editor state.
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/App.razor`
  - Root app with routes.
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/Routes.razor`
  - Route mapping.
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/Layout/MainLayout.razor`
  - Host shell for split-view page.
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`
  - Main split-view page (left chat, right editor, export).
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/wwwroot/css/app.css`
  - Minimal layout styling.
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json`
  - Add web host URL and mode defaults.
- Modify: `README.md`
  - Add run instructions for `HrMcp.Agent --web` and expected MVP behavior.

Notes:
- No new solution projects.
- No migration of orchestration to `HrMcp.Application` in this MVP.
- Console mode remains working for tutorial continuity.

---

### Task 1: Make HrMcp.Agent Web-Capable Without Breaking Console

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs`

- [ ] **Step 1: Add a failing startup check for web mode (manual fail-first)**

Run:

```powershell
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --web
```

Expected before implementation: app does not start a web server and exits/continues console path.

- [ ] **Step 2: Update project file for Blazor Server + MudBlazor**

Apply minimal csproj changes:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UserSecretsId>DotnetAiAgentMcp-HrMcp-Agent</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MudBlazor" Version="8.*" />
    <PackageReference Include="MudBlazor.Services" Version="8.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add mode switch in Program.cs**

Minimal structure:

```csharp
var runWeb = args.Contains("--web", StringComparer.OrdinalIgnoreCase);
if (runWeb)
{
    await RunWebAsync(args);
    return;
}

await RunConsoleAsync(args);
```

- [ ] **Step 4: Verify web and console both run**

Run:

```powershell
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --web
```

Expected: Kestrel starts and serves a page.

Run:

```powershell
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent
```

Expected: existing console banner and chat flow still work.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs
git commit -m "feat(agent): add web host mode while preserving console mode"
```

---

### Task 2: Add Split-View Blazor Shell (Left Chat, Right Draft Editor)

**Files:**
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/App.razor`
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/Routes.razor`
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/Layout/MainLayout.razor`
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/wwwroot/css/app.css`

- [ ] **Step 1: Add a failing page smoke check (manual)**

Run web mode and open root URL.
Expected before page implementation: no split-view workspace.

- [ ] **Step 2: Create split-view page with placeholder actions**

`DraftWorkspace.razor` skeleton:

```razor
<div class="workspace">
  <section class="left-chat">
    <h3>AI Assistant</h3>
    <div class="chat-thread">...</div>
    <textarea @bind="Prompt"></textarea>
    <button @onclick="SendPromptAsync">Send</button>
  </section>

  <section class="right-editor">
    <h3>JD/PD Draft</h3>
    <textarea @bind="DraftText"></textarea>
    <button @onclick="ExportWordAsync">Export Word</button>
  </section>
</div>
```

- [ ] **Step 3: Add minimal responsive styles**

`app.css` layout basics:

```css
.workspace { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; height: calc(100vh - 100px); }
.left-chat, .right-editor { border: 1px solid #ddd; border-radius: 8px; padding: 12px; }
@media (max-width: 960px) { .workspace { grid-template-columns: 1fr; } }
```

- [ ] **Step 4: Verify split-view renders**

Run:

```powershell
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --web
```

Expected: left chat and right editor panels visible on desktop, stacked on mobile width.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/Components DotnetAiAgentMcp/src/HrMcp.Agent/wwwroot/css/app.css
git commit -m "feat(agent-web): add split-view drafting workspace shell"
```

---

### Task 3: Reuse Existing Agent Flow Through A Thin Web Service

**Files:**
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Web/Services/AgentDraftService.cs`
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Web/Models/ChatTurn.cs`
- Create: `DotnetAiAgentMcp/src/HrMcp.Agent/Web/Models/DraftDocumentState.cs`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

- [ ] **Step 1: Add a failing behavior check**

In web mode, click Send with a prompt.
Expected before implementation: no AI response and no draft changes.

- [ ] **Step 2: Implement thin service contract and model types**

Model examples:

```csharp
public sealed record ChatTurn(string Role, string Text, DateTimeOffset Timestamp);
public sealed record DraftDocumentState(string DraftText, int Revision);
```

Service shape:

```csharp
public interface IAgentDraftService
{
    Task<string> SendPromptAsync(string prompt, CancellationToken ct);
    Task<string> ExportDraftToWordAsync(string draftText, CancellationToken ct);
}
```

- [ ] **Step 3: Implement service by reusing existing MCP/AI setup paths**

Service implementation should call existing transport + chat flow helpers from `Program.cs`/`HrAgent` with minimal duplication.

- [ ] **Step 4: Wire service into DraftWorkspace page**

```razor
@inject IAgentDraftService AgentDraftService
```

Send action updates left thread and optionally applies AI proposal to right editor.

- [ ] **Step 5: Verify prompt -> response -> draft update**

Run web mode and test:
- send prompt for JD draft
- receive assistant response
- apply generated text into right editor

Expected: working demo flow with no console involvement.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/Web DotnetAiAgentMcp/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs
git commit -m "feat(agent-web): reuse existing draft orchestration via thin web service"
```

---

### Task 4: Word Export From Right Panel

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Web/Services/AgentDraftService.cs`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

- [ ] **Step 1: Add failing export check**

In web mode, click Export Word.
Expected before implementation: no file path result or save behavior feedback.

- [ ] **Step 2: Reuse existing export tool behavior**

Service should invoke existing export path and return saved file message similar to console flow.

```csharp
var result = await ExportDraftToWordAsync(positionId, draftText, ct);
StatusMessage = result; // e.g. Saved to: C:\...\draft.docx
```

- [ ] **Step 3: Add user feedback and error state**

- success snackbar/text for save path
- error message if export fails

- [ ] **Step 4: Verify end-to-end export**

Run web mode and execute:
- generate or edit draft
- click export

Expected: `.docx` saved to output folder and success message shown.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/Web/Services/AgentDraftService.cs DotnetAiAgentMcp/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
git commit -m "feat(agent-web): enable word export from right-panel draft editor"
```

---

### Task 5: Keep Tutorial Docs In Sync With Minimal-Change Narrative

**Files:**
- Modify: `README.md`
- Modify: `blogs/series-1-ai-agent-mcp/preface.md`
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

- [ ] **Step 1: Add failing doc check**

Search for text implying console-only client:

```powershell
rg "console AI agent|console app|terminal" README.md blogs/series-1-ai-agent-mcp
```

Expected before update: references still imply console as primary UI.

- [ ] **Step 2: Update run instructions and narrative**

Add web-first run path:

```bash
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --web
```

Keep console path as fallback.

- [ ] **Step 3: Verify docs match MVP scope**

- manager-only drafting for MVP
- split-view left chat/right editor
- Word export demo
- HR review deferred

- [ ] **Step 4: Commit**

```bash
git add README.md blogs/series-1-ai-agent-mcp/preface.md blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md
git commit -m "docs: update tutorial for in-place console-to-blazor MVP"
```

---

### Task 6: Final Verification And Demo Script

**Files:**
- Modify: `README.md` (verification section)

- [ ] **Step 1: Build verification**

Run:

```powershell
dotnet build DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: both succeed.

- [ ] **Step 2: Runtime verification (web mode)**

Run:

```powershell
# Terminal 1
dotnet run --project DotnetAiAgentMcp/src/HrMcp.McpServer -- --stream-http

# Terminal 2
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --web --stream-http
```

Expected:
- split-view loads
- prompt returns AI output
- right-panel draft is editable
- export creates `.docx`

- [ ] **Step 3: Runtime verification (console fallback)**

Run:

```powershell
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent
```

Expected: existing console flow remains functional.

- [ ] **Step 4: Final commit**

```bash
git add README.md
git commit -m "chore: document validation steps for web-first agent MVP"
```

---

## Plan Self-Review

- Spec coverage: includes in-place upgrade, no new projects, minimal-change tutorial mode, split-view UI, manager-only MVP, and Word export.
- Placeholder scan: no `TODO`/`TBD`; each task has concrete files and commands.
- Type consistency: `IAgentDraftService`, `ChatTurn`, and `DraftDocumentState` are used consistently across tasks.

## Out-Of-Scope For This Plan

- HR specialist review/approval workflow.
- Hangfire pipelines.
- SignalR collaboration workflow.
- Full orchestration extraction into `HrMcp.Application`.
