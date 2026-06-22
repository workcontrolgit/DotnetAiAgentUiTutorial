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

## Part 1 — Blazor United Foundation

**File:** `blogs/series-2-ai-agent-ui/part-1-blazor-united-foundation.md`  
**Code:** `DotnetAiAgentUI/src/HrMcp.Agent/` (Blazor United project, MudBlazor layout, dual-mode startup)

### Blog Content
- [ ] Intro states what the reader will have by the end (Blazor United shell, MudBlazor layout, working navigation)
- [ ] Series 1 parallel noted in the introduction
- [ ] All prerequisites listed with version check commands
- [ ] Every step is numbered and sequential
- [ ] All code blocks have a language tag (`csharp`, `bash`, `xml`, `razor`)
- [ ] File paths in code snippets match the actual solution structure (`DotnetAiAgentUI/src/HrMcp.Agent/`)
- [ ] Why Blazor United section explains SSR + interactivity trade-off
- [ ] Project file `.csproj` snippet shown with all package references
- [ ] MudBlazor 8 installation command includes version constraint
- [ ] `Program.cs` dual-mode startup logic shown (--web flag)
- [ ] Layout component shown with MudAppBar, MudDrawer, MudMainContent
- [ ] "Next Up" footer links to Part 2
- [ ] Tags line present at the bottom

### Code
- [ ] `dotnet build DotnetAiAgentUI/DotnetAiAgentUI.slnx` → 0 errors, 0 warnings
- [ ] `dotnet run --project DotnetAiAgentUI/src/HrMcp.Agent --args "--web"` starts web UI on `http://localhost:5000` (or configured port)
- [ ] Browser loads MudBlazor layout with AppBar, drawer navigation, and main content area
- [ ] MudBlazor version is 8.* in `.csproj`
- [ ] All `dotnet add package` commands in blog include version constraints
- [ ] Blazor United project uses `OutputType = Exe` and `Microsoft.NET.Sdk.Web`
- [ ] Static assets handled via `MapStaticAssets` (not legacy asset serving)
- [ ] Auto render mode applied globally via `--all-interactive` flag at setup
- [ ] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] GitHub commit tagged: `part-1-series-2`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 2 — AI Agent UI Patterns

**File:** `blogs/series-2-ai-agent-ui/part-2-ai-agent-ui-patterns.md`  
**Code:** None (concepts-only post)

### Blog Content
- [ ] Three-state UI model explained: idle, loading, error (with diagram if applicable)
- [ ] `IChatClient` abstraction introduced as service seam between UI and AI
- [ ] Series 1 parallel noted (Series 1 Part 4 built the agent; Part 2 here shows how UI consumes it)
- [ ] Request round-trip workflow described (user input → service call → response → render)
- [ ] Why abstraction matters: testability, swappability, isolation from OllamaSharp details
- [ ] `IAgentDraftService` interface preview (signature of `SendPromptAsync`)
- [ ] Markdown rendering strategy introduced (Markdig, not raw HTML)
- [ ] MudBlazor component patterns noted (buttons, panels, loading skeletons)
- [ ] Preview of Part 3 components listed: `ChatPanel`, `ChatTurn` model, `AgentDraftService`
- [ ] "Next Up" footer links to Part 3
- [ ] Tags line present at the bottom
- [ ] No executable code blocks (verified — 0 code fences)

### Code
- [ ] N/A — no code deliverable for this part

### Publish Gate
- [ ] All blog content items above are checked
- [ ] GitHub commit tagged: `part-2-series-2`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 3 — Building the Chat UI

**File:** `blogs/series-2-ai-agent-ui/part-3-building-the-chat-ui.md`  
**Code:** `DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/ChatTurn.cs`, `DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs`, `DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/Chat.razor`

### Blog Content
- [ ] Intro states what the reader will have by the end (working chat panel, full pipeline wired, responses render as markdown)
- [ ] Series 1 parallel noted (Series 1 Part 3 built MCP tools; Part 3 here builds UI layer)
- [ ] All prerequisites listed with version check commands
- [ ] Every step is numbered and sequential
- [ ] All code blocks have a language tag (`csharp`, `bash`, `xml`, `razor`)
- [ ] File paths in code snippets match actual structure (`DotnetAiAgentUI/src/HrMcp.Agent/Web/...`)
- [ ] `ChatTurn` record shown with explanation: `sealed`, `record` (value equality), `DateTimeOffset` (timezone-safe)
- [ ] `IAgentDraftService` interface shown with `SendPromptAsync(string prompt, CancellationToken ct)` method
- [ ] Service implementation shown: calls `IChatClient`, returns response text (no streaming yet)
- [ ] `IChatClient` abstraction emphasized — no direct OllamaSharp in UI code
- [ ] `ChatPanel` component shown: accepts chat turns, displays them, provides input form
- [ ] MudBlazor components used: `MudCard`, `MudButton`, `MudTextField` (no Bootstrap or plain CSS)
- [ ] Markdown rendering integrated (Markdig) for assistant messages
- [ ] Loading state UI shown (MudSkeleton or spinner)
- [ ] Error handling shown (error state in UI)
- [ ] "Next Up" footer links to Part 4
- [ ] Tags line present at the bottom

### Code
- [ ] `dotnet build DotnetAiAgentUI/DotnetAiAgentUI.slnx` → 0 errors, 0 warnings
- [ ] `IChatClient` injected into service, not direct OllamaSharp dependency in UI layer
- [ ] `ChatTurn` record defined with correct fields
- [ ] `IAgentDraftService` injected into `ChatPanel` component
- [ ] Chat panel accepts user input and calls `SendPromptAsync`
- [ ] Response displays in chat thread without streaming (single full message)
- [ ] Markdown rendered for assistant turns (Markdig used)
- [ ] Loading indicator shows while request is in flight
- [ ] MudBlazor 8 components used throughout (no Bootstrap)
- [ ] All `dotnet add package` commands include version constraints
- [ ] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] GitHub commit tagged: `part-3-series-2`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 4 — Streaming Responses & Real-Time UX

**File:** `blogs/series-2-ai-agent-ui/part-4-streaming-responses-realtime-ux.md`  
**Code:** `DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` (streaming upgrade), `DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/Chat.razor` (real-time update)

### Blog Content
- [ ] Intro states what the reader will have by the end (tokens stream one at a time, UI updates in real-time, cancel button functional)
- [ ] Series 1 parallel noted (Series 1 Part 4 integrated Ollama; Part 4 here adds streaming to UI)
- [ ] All prerequisites listed with version check commands
- [ ] Every step is numbered and sequential
- [ ] All code blocks have a language tag (`csharp`, `bash`, `razor`)
- [ ] File paths in code snippets match actual structure
- [ ] `IAsyncEnumerable<string>` explained (token stream, not full string at once)
- [ ] Service method signature shown: `IAsyncEnumerable<string> SendPromptStreamAsync(string prompt, CancellationToken ct)`
- [ ] `IChatClient.CompleteStreamingAsync` usage shown with example
- [ ] UI loop shown: iterate tokens, append to growing text, call `StateHasChanged()` per token
- [ ] `CancellationToken` plumbed through (user can abort mid-stream)
- [ ] Cancel button UI shown (MudButton with click handler that triggers `CancellationTokenSource.Cancel()`)
- [ ] Performance notes: why not await tokens individually (overhead), why `StateHasChanged()` per token is acceptable for chat
- [ ] Error handling in streaming context (exception during token stream)
- [ ] "Next Up" footer links to Part 5
- [ ] Tags line present at the bottom

### Code
- [ ] `dotnet build DotnetAiAgentUI/DotnetAiAgentUI.slnx` → 0 errors, 0 warnings
- [ ] `SendPromptStreamAsync` returns `IAsyncEnumerable<string>` (not `Task<string>`)
- [ ] Streaming implemented via `IChatClient.CompleteStreamingAsync` (not direct OllamaSharp)
- [ ] `CancellationToken` parameter accepted and passed to streaming call
- [ ] UI loop iterates tokens: `await foreach (var token in stream)` pattern
- [ ] `StateHasChanged()` called per token for real-time rendering
- [ ] Cancel button toggles `CancellationTokenSource.Cancel()`
- [ ] Chat turn added after streaming completes (accumulates full text)
- [ ] Loading indicator removed when stream ends or errors
- [ ] MudBlazor components used (no Bootstrap, no plain CSS)
- [ ] All `dotnet add package` commands include version constraints
- [ ] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] GitHub commit tagged: `part-4-series-2`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 5 — Document Editor & Word Export

**File:** `blogs/series-2-ai-agent-ui/part-5-document-editor-and-export.md`  
**Code:** `DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/DraftDocumentState.cs`, `DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` (export method), `DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftEditor.razor` or split-panel component

### Blog Content
- [ ] Intro states what the reader will have by the end (split-panel layout, WYSIWYG editor, Word export button working)
- [ ] Series 1 parallel noted (Series 1 Part 5 was Claude Desktop demo; Part 5 here is the payoff: document editor + export)
- [ ] All prerequisites listed with version check commands (including Blazored.TextEditor)
- [ ] Every step is numbered and sequential
- [ ] All code blocks have a language tag (`csharp`, `bash`, `xml`, `razor`, `css`)
- [ ] File paths in code snippets match actual structure (`DotnetAiAgentUI/src/HrMcp.Agent/...`)
- [ ] Blazored.TextEditor installation command includes version constraint (1.*)
- [ ] Quill CDN dependencies shown in `App.razor` (head and body script tags)
- [ ] `DraftDocumentState` model shown: `DraftText` and `Revision` counter
- [ ] Revision counter explained: why it matters for UI reload (component key re-initialization)
- [ ] Split-panel layout shown: CSS Grid with draggable splitter (not MudGrid, because pixel-level drag control)
- [ ] Splitter resize JavaScript/C# interop shown (drag handler updates column widths)
- [ ] Editor component usage shown: bound to draft state
- [ ] Export method signature shown: `ExportDraftToWordAsync(string draftText, CancellationToken ct)` returning `(string Message, string? FileName, byte[]? FileBytes)`
- [ ] Export button click handler shown: calls service, triggers browser download via `BlazorDownloadFile` or equivalent
- [ ] MudBlazor components used: `MudButton`, `MudCard` (no Bootstrap, no plain CSS)
- [ ] Integration with MCP server explained: export calls back to `ExportDraftToWord` tool
- [ ] Error handling shown (export failure message)
- [ ] "Next Up" footer links to Part 6
- [ ] Tags line present at the bottom

### Code
- [ ] `dotnet build DotnetAiAgentUI/DotnetAiAgentUI.slnx` → 0 errors, 0 warnings
- [ ] Blazored.TextEditor version is 1.* in `.csproj`
- [ ] Quill CDN scripts loaded before `blazor.web.js` in `App.razor`
- [ ] `DraftDocumentState` model includes `DraftText` and `Revision` fields
- [ ] Editor component renders without JavaScript errors
- [ ] Draft state updates when chat adds a new assistant message
- [ ] Revision counter increments to trigger component re-initialization
- [ ] Split-panel layout resizable via drag (CSS Grid with JavaScript interop)
- [ ] Export button calls `ExportDraftToWordAsync` with cancellation support
- [ ] Service calls MCP server `ExportDraftToWord` tool (via `IChatClient` or MCP call)
- [ ] Downloaded file is a valid `.docx` (Word format)
- [ ] File download triggered in browser (no direct file system write)
- [ ] Error during export caught and displayed to user
- [ ] MudBlazor 8 components used (no Bootstrap)
- [ ] All `dotnet add package` commands include version constraints
- [ ] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] GitHub commit tagged: `part-5-series-2`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 6 — Securing the UI with OIDC

**File:** `blogs/series-2-ai-agent-ui/part-6-securing-ui-with-oidc.md`  
**Code:** `DotnetAiAgentUI/src/HrMcp.Agent/Program.cs` (auth middleware, cascading auth state), `DotnetAiAgentUI/src/HrMcp.Agent/Components/App.razor` (`CascadingAuthenticationState`)

### Blog Content
- [ ] Intro states what the reader will have by the end (OIDC login required, user authenticated, chat access gated)
- [ ] Series 1 parallel noted (Series 1 Part 6 secured the MCP server; Part 6 here secures the Blazor UI)
- [ ] All prerequisites listed with version check commands
- [ ] Every step is numbered and sequential
- [ ] All code blocks have a language tag (`csharp`, `bash`, `xml`, `razor`)
- [ ] File paths in code snippets match actual structure
- [ ] Problem framing: why unauthenticated UIs are a risk (identity, audit, data protection)
- [ ] Architecture diagram present: OIDC provider → Blazor app (client) → MCP server (protected)
- [ ] Provider options compared (Okta free tier, Duende IdentityServer, Azure AD/Entra, Google Cloud Identity Platform) — as bullet list, not table
- [ ] `Microsoft.AspNetCore.Components.Authorization` package included
- [ ] OIDC configuration in `appsettings.json` shown: `Authority`, `ClientId`, `ClientSecret`, `RedirectUris`
- [ ] `Program.cs` middleware added: `AddAuthentication`, `AddOpenIdConnect`, `CascadingAuthenticationState`
- [ ] `CascadingAuthenticationState` component shown in `App.razor` (wraps route content)
- [ ] `AuthorizeView` component shown: displays login button when unauthenticated, chat UI when authenticated
- [ ] Login/logout navigation items shown in layout (MudBlazor NavMenu)
- [ ] `@attribute [Authorize]` directive shown on protected pages (`Chat.razor`, `DraftEditor.razor`)
- [ ] Optional: example of token acquisition in service (if service calls MCP server with Bearer token)
- [ ] `appsettings.Development.json` shown with placeholder values only — no real secrets
- [ ] "What We Built" summary present (6-part series recap)
- [ ] No "Next Up" footer (this is the final part)
- [ ] Sources section complete
- [ ] Tags line present at the bottom

### Code
- [ ] `dotnet build DotnetAiAgentUI/DotnetAiAgentUI.slnx` → 0 errors, 0 warnings
- [ ] Authentication middleware configured in `Program.cs` (OIDC)
- [ ] `CascadingAuthenticationState` component in `App.razor`
- [ ] Unauthenticated request to web UI redirects to login (OIDC provider)
- [ ] After login, user returned to original page (redirect URI)
- [ ] Chat page protected with `@attribute [Authorize]`
- [ ] Draft editor page protected with `@attribute [Authorize]`
- [ ] `AuthorizeView` conditional renders: different UI for authenticated vs anonymous
- [ ] Login button visible in navigation when not authenticated
- [ ] Logout button visible in navigation when authenticated
- [ ] User claims accessible via `AuthenticationState` (name, email, roles if applicable)
- [ ] Bearer token available for calls to protected MCP server (if service uses it)
- [ ] `appsettings.Development.json` contains only placeholders — no real OIDC credentials committed
- [ ] `appsettings.json` has configuration keys (Authority, ClientId, etc.) but no secrets
- [ ] MudBlazor components used in auth UI (no Bootstrap, no plain CSS)
- [ ] All `dotnet add package` commands include version constraints
- [ ] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] GitHub commit tagged: `part-6-series-2`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live
- [ ] Series Status table at top of this file fully complete

**Update Series Status table ↑ when this gate is cleared.**

---

## How to Use This Checklist

1. Work through the items for the **current part** top to bottom
2. Check each item only when it is **fully done** — not "good enough"
3. The **Publish Gate** is the release condition: do not start the next part until the gate is cleared
4. Update the **Series Status** table at the top when a gate clears — one glance shows where the series stands
