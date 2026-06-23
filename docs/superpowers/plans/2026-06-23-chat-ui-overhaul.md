# Chat UI Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the Blazor UI into a ChatGPT-style app shell with auto-scrolling chat, collapsible history sidebar, user info placeholder, and a display-only settings modal.

**Architecture:** `MainLayout.razor` is rewritten as the full app shell (sidebar + topbar + settings modal). `DraftWorkspace.razor` is stripped of its topbar and renders only the workspace grid. Auto-scroll is implemented via a small JS function called from `OnAfterRenderAsync` whenever chat state changes.

**Tech Stack:** Blazor Server (.NET 10), C#, vanilla CSS, JS interop via `IJSRuntime`, `IConfiguration` for reading AI settings.

## Global Constraints

- Project: `DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj`
- Run command: `dotnet run --project DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj -- --web` from repo root
- Font: `"IBM Plex Sans", "Segoe UI", Arial, sans-serif` — do not change
- Color palette: blue/slate (`#2f62d8`, `#1d2433`, `#f0f4fc`) — stay consistent
- No new NuGet packages
- No changes to `AgentDraftService.cs`, `HrAgent.cs`, or any backend service
- No changes to the right editor panel markup or Quill logic

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor` | Modify | Add `scrollChatToBottom` JS function to inline script |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/MainLayout.razor` | Rewrite | App shell: sidebar + topbar + settings modal |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Modify | Remove topbar block, add scroll tracking, clean up chat bubbles, new placeholder |
| `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css` | Modify | Add app-shell/sidebar/topbar/modal CSS, remove old `.main-layout` and `.workspace-topbar` rules, clean up chat-thread border |

---

### Task 1: Auto-scroll — JS function + scroll tracking

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Produces: `window.scrollChatToBottom(id)` — scrolls element matching `id` to its bottom
- Produces: `id="chat-thread"` on the `.chat-thread` div for JS targeting

- [ ] **Step 1: Add `scrollChatToBottom` to `App.razor`**

Open `DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor`. Inside the `<script>` block, add after the `downloadFile` function (before `</script>`):

```js
window.scrollChatToBottom = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollTop = el.scrollHeight;
};
```

- [ ] **Step 2: Add `id` to the chat-thread div and scroll tracking fields in `DraftWorkspace.razor`**

In `DraftWorkspace.razor`, find the `.chat-thread` div (line 23) and add `id="chat-thread"`:

```razor
<div class="chat-thread" id="chat-thread">
```

In the `@code` block, add two tracking fields after `private bool _draftVisible;`:

```csharp
private int _lastScrollTurnCount;
private bool _lastScrollBusy;
```

- [ ] **Step 3: Add scroll call to `OnAfterRenderAsync`**

The existing `OnAfterRenderAsync` handles pending Quill HTML. Extend it to also scroll the chat. Replace the entire `OnAfterRenderAsync` method with:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (_pendingHtml is not null)
    {
        var html = _pendingHtml;
        _pendingHtml = null;
        await JS.InvokeVoidAsync("loadQuillWhenReady", "quill-editor-wrapper", html);
    }

    var shouldScroll = _turns.Count != _lastScrollTurnCount || _busy != _lastScrollBusy;
    if (shouldScroll)
    {
        _lastScrollTurnCount = _turns.Count;
        _lastScrollBusy = _busy;
        await JS.InvokeVoidAsync("scrollChatToBottom", "chat-thread");
    }
}
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: Build succeeded, 0 errors.

Then run (`dotnet run --project DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj -- --web`) and send a prompt that produces a long response. Confirm the chat automatically scrolls to show the new message without manual scrolling.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
git commit -m "feat(chat): auto-scroll to bottom after each response"
```

---

### Task 2: Chat panel visual cleanup

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css`

**Interfaces:**
- Consumes: `id="chat-thread"` on `.chat-thread` div (Task 1)
- Produces: borderless chat thread, label-free bubbles, improved placeholder

- [ ] **Step 1: Remove the "Writing Assistant" header**

In `DraftWorkspace.razor`, find and delete this line (currently line 22):

```razor
<h3>Writing Assistant</h3>
```

- [ ] **Step 2: Remove role labels from chat bubbles**

Find the user bubble block. Replace:

```razor
<div class="chat-bubble">
    <strong>@turn.Role:</strong>
    @if (isUser)
    {
        <span> @turn.Text</span>
    }
    else
    {
        <div class="chat-bubble-content">@RenderAssistantMarkdown(turn.Text)</div>
    }
</div>
```

With:

```razor
<div class="chat-bubble">
    @if (isUser)
    {
        <span>@turn.Text</span>
    }
    else
    {
        <div class="chat-bubble-content">@RenderAssistantMarkdown(turn.Text)</div>
    }
</div>
```

- [ ] **Step 3: Update the textarea placeholder**

Find the `<textarea>` element and change the `placeholder` attribute:

```razor
<textarea class="chat-input" @bind="Prompt" @bind:event="oninput" @onkeydown="HandlePromptKeyDown" placeholder="Message the assistant — e.g. &quot;Draft a mid-level software engineer PD&quot;"></textarea>
```

- [ ] **Step 4: Remove border and background from `.chat-thread` in CSS**

In `app.css`, find the `.chat-thread` rule and replace it with:

```css
.chat-thread {
    flex: 1;
    overflow: auto;
    padding: 10px;
    margin-bottom: 8px;
}
```

(Removes `border: 1px solid #e4e8f0`, `border-radius: 6px`, and `background: linear-gradient(...)`)

- [ ] **Step 5: Build and verify**

```bash
dotnet build DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: Build succeeded, 0 errors.

Run the app and confirm: no "Writing Assistant" heading in the chat panel, bubbles show no "You:" / "Assistant:" prefix, placeholder text updated, chat thread has no border box.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
git add DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css
git commit -m "feat(chat): remove role labels, header, and thread border for ChatGPT-like feel"
```

---

### Task 3: App shell CSS

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css`

**Interfaces:**
- Produces: CSS classes consumed by Task 4's markup — `.app-shell`, `.history-sidebar`, `.history-sidebar--collapsed`, `.main-area`, `.main-topbar`, `.topbar-left`, `.topbar-actions`, `.app-title`, `.main-body`, `.icon-btn`, `.new-chat-btn`, `.sidebar-history`, `.sidebar-empty`, `.sidebar-footer`, `.sidebar-user-btn`, `.modal-overlay`, `.modal-card`, `.modal-header`, `.modal-close-btn`, `.settings-table`

- [ ] **Step 1: Replace `.main-layout` rule**

In `app.css`, find:

```css
.main-layout {
    padding: 12px;
}
```

Replace with:

```css
.app-shell {
    display: flex;
    height: 100vh;
    overflow: hidden;
}
```

- [ ] **Step 2: Add sidebar CSS**

After the `.app-shell` rule, add:

```css
.history-sidebar {
    width: 240px;
    flex-shrink: 0;
    display: flex;
    flex-direction: column;
    background: #f0f4fc;
    border-right: 1px solid #dde3ef;
    transition: width 0.2s ease;
    overflow: hidden;
}

.history-sidebar--collapsed {
    width: 0;
}

.sidebar-top {
    padding: 12px 10px 8px;
}

.new-chat-btn {
    width: 100%;
    border: 1px solid #ccd4e4;
    background: #ffffff;
    color: #2b3a57;
    border-radius: 8px;
    padding: 8px 12px;
    cursor: pointer;
    font-size: 0.9rem;
    text-align: left;
}

.new-chat-btn:hover {
    background: #edf2fb;
}

.new-chat-btn:disabled {
    opacity: 0.5;
    cursor: default;
}

.sidebar-history {
    flex: 1;
    overflow-y: auto;
    padding: 8px 10px;
}

.sidebar-empty {
    display: block;
    color: #8a97b0;
    font-size: 0.85rem;
    padding: 8px 4px;
}

.sidebar-footer {
    border-top: 1px solid #dde3ef;
    padding: 10px;
}

.sidebar-user-btn {
    width: 100%;
    display: flex;
    align-items: center;
    gap: 8px;
    border: none;
    background: transparent;
    color: #2b3a57;
    border-radius: 8px;
    padding: 8px 10px;
    cursor: pointer;
    font-size: 0.9rem;
}

.sidebar-user-btn:hover {
    background: #e6eaf5;
}

.sidebar-user-btn:disabled {
    opacity: 0.5;
    cursor: default;
}

.sidebar-user-label {
    font-size: 0.9rem;
}
```

- [ ] **Step 3: Add main-area and topbar CSS**

After the sidebar rules, add:

```css
.main-area {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-width: 0;
}

.main-topbar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 10px 14px;
    border-bottom: 1px solid #e4e8f0;
    background: #ffffff;
    flex-shrink: 0;
}

.topbar-left {
    display: flex;
    align-items: center;
    gap: 10px;
}

.topbar-actions {
    display: flex;
    align-items: center;
    gap: 8px;
}

.app-title {
    font-size: 1rem;
    font-weight: 600;
    color: #1d2433;
}

.icon-btn {
    width: 32px;
    height: 32px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border: none;
    background: transparent;
    border-radius: 6px;
    cursor: pointer;
    font-size: 1.1rem;
    color: #2b3a57;
}

.icon-btn:hover {
    background: #edf2fb;
}

.main-body {
    flex: 1;
    overflow: hidden;
    padding: 12px;
    display: flex;
    flex-direction: column;
}
```

- [ ] **Step 4: Add settings modal CSS**

After the `.main-body` rule, add:

```css
.modal-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.35);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
}

.modal-card {
    background: #ffffff;
    border-radius: 12px;
    padding: 24px;
    width: 360px;
    max-width: calc(100vw - 32px);
    box-shadow: 0 8px 32px rgba(30, 50, 100, 0.18);
}

.modal-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 18px;
}

.modal-header h2 {
    margin: 0;
    font-size: 1.1rem;
    color: #1d2433;
}

.modal-close-btn {
    border: none;
    background: transparent;
    font-size: 1.2rem;
    cursor: pointer;
    color: #4e5a74;
    padding: 2px 6px;
    border-radius: 4px;
}

.modal-close-btn:hover {
    background: #f0f4fc;
}

.settings-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.9rem;
}

.settings-table tr {
    border-bottom: 1px solid #eef2f8;
}

.settings-table tr:last-child {
    border-bottom: none;
}

.settings-table td {
    padding: 10px 4px;
    vertical-align: top;
}

.settings-table td:first-child {
    color: #4e5a74;
    width: 40%;
    font-weight: 500;
}

.settings-table td:last-child {
    color: #1d2433;
    font-weight: 400;
}
```

- [ ] **Step 5: Remove old topbar CSS**

Find and delete the entire `.workspace-topbar` and `.workspace-topbar h1` rules:

```css
.workspace-topbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
}

.workspace-topbar h1 {
    margin: 0;
}
```

Also update the `.workspace` rule — remove `margin-top: 10px` and change `height` since it now lives inside `.main-body`:

Find:

```css
.workspace {
    display: grid;
    grid-template-columns: minmax(320px, 420px) 8px minmax(0, 1fr);
    gap: 0;
    height: calc(100vh - 110px);
    margin-top: 10px;
    border: 1px solid #d7deea;
    border-radius: 12px;
    overflow: hidden;
    background: #ffffff;
}
```

Replace with:

```css
.workspace {
    display: grid;
    grid-template-columns: minmax(320px, 420px) 8px minmax(0, 1fr);
    gap: 0;
    flex: 1;
    border: 1px solid #d7deea;
    border-radius: 12px;
    overflow: hidden;
    background: #ffffff;
}
```

(Uses `flex: 1` instead of a fixed `calc(100vh - 110px)` height — `.main-body` is now the flex container that controls available height.)

- [ ] **Step 6: Build check (CSS only — no markup yet)**

```bash
dotnet build DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: Build succeeded, 0 errors. The app still runs and looks the same (new CSS classes are not yet referenced by markup).

- [ ] **Step 7: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css
git commit -m "feat(css): add app-shell, sidebar, topbar, and modal styles"
```

---

### Task 4: App shell markup — MainLayout + DraftWorkspace

**Files:**
- Rewrite: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/MainLayout.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: all CSS classes from Task 3
- Consumes: `IConfiguration` (`AI:Provider`, `AI:AzureOpenAI:Deployment`, `AI:Ollama:Model`, `McpServer:Transport:Type`) — injected into `MainLayout.razor`
- Produces: full working app shell with sidebar toggle, settings modal, and `@Body` rendered in `.main-body`

- [ ] **Step 1: Rewrite `MainLayout.razor`**

Replace the entire file content with:

```razor
@inherits LayoutComponentBase
@inject IConfiguration Config

<div class="app-shell">
    <aside class="history-sidebar @(_sidebarOpen ? "" : "history-sidebar--collapsed")">
        <div class="sidebar-top">
            <button class="new-chat-btn" disabled title="Coming soon">+ New Chat</button>
        </div>
        <nav class="sidebar-history">
            <span class="sidebar-empty">No history yet</span>
        </nav>
        <div class="sidebar-footer">
            <button class="sidebar-user-btn" disabled title="Login coming soon">
                <span>&#128100;</span>
                <span class="sidebar-user-label">Sign in</span>
            </button>
        </div>
    </aside>

    <div class="main-area">
        <header class="main-topbar">
            <div class="topbar-left">
                <button class="icon-btn" @onclick="ToggleSidebar" title="Toggle sidebar">&#9776;</button>
                <span class="app-title">Position Description Builder</span>
            </div>
            <div class="topbar-actions">
                <button class="ghost-btn" @onclick="OpenSettings">&#9881; Settings</button>
            </div>
        </header>

        <div class="main-body">
            @Body
        </div>
    </div>
</div>

@if (_settingsOpen)
{
    <div class="modal-overlay" @onclick="CloseSettings">
        <div class="modal-card" @onclick:stopPropagation="true">
            <div class="modal-header">
                <h2>Settings</h2>
                <button class="modal-close-btn" @onclick="CloseSettings" title="Close">&times;</button>
            </div>
            <table class="settings-table">
                <tr>
                    <td>AI Provider</td>
                    <td>@(Config["AI:Provider"] ?? "—")</td>
                </tr>
                <tr>
                    <td>Model</td>
                    <td>@ResolveModel()</td>
                </tr>
                <tr>
                    <td>Transport</td>
                    <td>@(Config["McpServer:Transport:Type"] ?? "—")</td>
                </tr>
                <tr>
                    <td>App Version</td>
                    <td>1.0</td>
                </tr>
            </table>
        </div>
    </div>
}

@code {
    private bool _sidebarOpen = true;
    private bool _settingsOpen;

    private void ToggleSidebar() => _sidebarOpen = !_sidebarOpen;
    private void OpenSettings() => _settingsOpen = true;
    private void CloseSettings() => _settingsOpen = false;

    private string ResolveModel()
    {
        var provider = Config["AI:Provider"] ?? "Ollama";
        return string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
            ? Config["AI:AzureOpenAI:Deployment"] ?? "—"
            : Config["AI:Ollama:Model"] ?? "—";
    }
}
```

- [ ] **Step 2: Remove `workspace-topbar` from `DraftWorkspace.razor`**

Find and delete the entire topbar block at the top of the component markup (lines 11–16 in the original):

```razor
<div class="workspace-topbar">
    <h1>Position Description Builder</h1>
    <button class="ghost-btn" @onclick="ToggleLeftPanel">
        @(_leftPanelHidden ? "Show Chat" : "Hide Chat")
    </button>
</div>
```

Also remove the `ToggleLeftPanel` button from the topbar — the Show/Hide Chat control no longer lives there. Since this was the only remaining way to show/hide the left chat panel, we need to keep that button, but move it into the **chat panel itself** or remove the feature for now since the sidebar hamburger partially replaces navigation. 

The simplest fix: remove the `ToggleLeftPanel` button entirely (the sidebar toggle in the main topbar handles overall navigation; individual panel hide/show adds complexity that the new layout doesn't need). Also remove the `ToggleLeftPanel` method and `_leftPanelHidden` field from `@code`.

Remove from `@code`:
```csharp
private bool _leftPanelHidden;
```

Remove the method:
```csharp
private void ToggleLeftPanel()
{
    _leftPanelHidden = !_leftPanelHidden;
    _resizing = false;
}
```

Remove the condition wrapping the left-chat section — the chat panel is always visible:

Replace:
```razor
@if (!_leftPanelHidden)
{
    <section class="left-chat">
        ...
    </section>

    @if (_draftVisible)
    {
        <div class="splitter" ...></div>
    }
}
```

With:
```razor
<section class="left-chat">
    ...
</section>

@if (_draftVisible)
{
    <div class="splitter" ...></div>
}
```

Also update `WorkspaceGridStyle` to remove the `_leftPanelHidden` reference:

Replace:
```csharp
private string WorkspaceGridStyle => _leftPanelHidden || !_draftVisible
    ? "grid-template-columns: minmax(0, 1fr);"
    : $"grid-template-columns: {_leftPanelWidth}px 8px minmax(0, 1fr);";
```

With:
```csharp
private string WorkspaceGridStyle => !_draftVisible
    ? "grid-template-columns: minmax(0, 1fr);"
    : $"grid-template-columns: {_leftPanelWidth}px 8px minmax(0, 1fr);";
```

Also update `StartResize` to remove the `_leftPanelHidden` guard:

Replace:
```csharp
private void StartResize()
{
    if (_leftPanelHidden)
        return;

    _resizing = true;
}
```

With:
```csharp
private void StartResize()
{
    _resizing = true;
}
```

- [ ] **Step 3: Build**

```bash
dotnet build DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Manual verification**

Run:
```bash
dotnet run --project DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj -- --web
```

Check each item:

| # | Check | Expected |
|---|---|---|
| 1 | Page loads | Sidebar visible on left, topbar with ≡ and ⚙, workspace below |
| 2 | Click ≡ | Sidebar collapses with smooth transition; click again to expand |
| 3 | Click ⚙ Settings | Modal opens showing AI Provider, Model, Transport, App Version |
| 4 | Click backdrop | Modal closes |
| 5 | Click × | Modal closes |
| 6 | Sidebar footer | "👤 Sign in" button visible, disabled (no click action) |
| 7 | Sidebar history | "No history yet" text visible |
| 8 | + New Chat | Button visible, disabled |
| 9 | Send a prompt | Response appears, chat auto-scrolls to bottom |
| 10 | Draft loads | Right panel still works, Export Word still works |

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/MainLayout.razor
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
git commit -m "feat(shell): ChatGPT-style app shell with sidebar, topbar, and settings modal"
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement | Task |
|---|---|
| Auto-scroll to bottom after response | Task 1 |
| Remove bordered chat-thread box | Task 2 (CSS), Task 3 |
| Remove "You:" / "Assistant:" role labels | Task 2 |
| Remove "Writing Assistant" h3 header | Task 2 |
| Improved placeholder text | Task 2 |
| Collapsible history sidebar with ≡ toggle | Tasks 3 + 4 |
| "No history yet" empty state placeholder | Task 4 |
| "+ New Chat" placeholder button | Task 4 |
| "👤 Sign in" user info placeholder | Task 4 |
| Topbar with left (≡ + title) and right (topbar-actions) | Tasks 3 + 4 |
| ⚙ Settings button in topbar | Task 4 |
| Settings modal (provider, model, transport, version) | Task 4 |
| Modal closes on backdrop click | Task 4 |
| `MainLayout.razor` owns the shell | Task 4 |
| `DraftWorkspace.razor` renders only workspace grid | Task 4 |
| `.workspace` uses `flex: 1` instead of fixed height | Task 3 |

All spec requirements covered. No gaps found.

**Placeholder scan:** No TBD/TODO/placeholder steps — all steps contain actual code.

**Type consistency:** `scrollChatToBottom` used consistently in Task 1. CSS class names defined in Task 3 match markup in Task 4. `Config["AI:Provider"]`, `Config["AI:AzureOpenAI:Deployment"]`, `Config["AI:Ollama:Model"]`, `Config["McpServer:Transport:Type"]` keys match `appsettings.json` exactly.
