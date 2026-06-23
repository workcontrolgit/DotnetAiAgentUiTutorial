# Chat UI Overhaul — Design Spec
**Date:** 2026-06-23  
**Status:** Approved

## Problem Statement

The left chat panel has three UX problems:
1. When a response arrives, the chat does not scroll to the bottom — the user must scroll manually.
2. The "You:" / "Assistant:" labels, inner header, and bordered box feel like a basic form, not a modern chat interface.
3. There is no settings panel and no structural room for chat history or user auth.

## Goals

- Fix auto-scroll so new messages are always visible without manual scrolling (ChatGPT parity)
- Redesign the chat panel to feel natural and modern (borderless messages, aligned bubbles, no role labels)
- Add a ChatGPT-style app shell: collapsible history sidebar + user info slot + topbar
- Add a display-only Settings modal (AI provider, model, transport, app version)
- Design all placeholders (history, user info) so wiring them up later requires no layout surgery

## Out of Scope

- Persisting chat history (placeholder only)
- OIDC login/logout wiring (placeholder only)
- Changing the right editor panel
- Changing the AI service or backend

---

## Architecture

### App Shell (`MainLayout.razor`)

`MainLayout.razor` becomes the full app shell. It owns:
- The collapsible history sidebar
- The topbar (hamburger + title + settings button)
- `@Body` renders inside `main-area`

`DraftWorkspace.razor` drops its `workspace-topbar` block and renders only the workspace grid (left chat + splitter + right editor).

```
app-shell
├── history-sidebar (collapsible, ~240px)
│   ├── sidebar-top       → + New Chat button (no-op placeholder)
│   ├── sidebar-history   → empty state "No history yet" (placeholder)
│   └── sidebar-footer    → 👤 Sign in button (no-op placeholder)
└── main-area
    ├── main-topbar
    │   ├── left: hamburger (≡) + app title
    │   └── right: topbar-actions div → [⚙ Settings]  (room for future [👤 User])
    └── @Body → DraftWorkspace workspace grid
```

### Settings Modal (`MainLayout.razor`)

A `bool _settingsOpen` field drives a modal overlay. It lives in `MainLayout.razor` alongside the ⚙ button that triggers it. Values are read from `IConfiguration` injected into `MainLayout.razor`.

Fields displayed:
| Label | Config key |
|---|---|
| AI Provider | `AI:Provider` |
| Model | `AI:AzureOpenAI:Deployment` or `AI:Ollama:Model` depending on provider |
| Transport | `McpServer:Transport:Type` |
| App Version | Hardcoded `"1.0"` |

The modal opens via the ⚙ button in the topbar. Clicking the backdrop or × closes it.

---

## Detailed Changes

### 1. Auto-scroll

**`App.razor`** — add one JS function to the existing inline script:

```js
window.scrollChatToBottom = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollTop = el.scrollHeight;
};
```

**`DraftWorkspace.razor`** — add `id="chat-thread"` to the `.chat-thread` div.  
Track `_lastScrollTurnCount` (int) and `_lastScrollBusy` (bool). In `OnAfterRenderAsync`:

```csharp
var shouldScroll = _turns.Count != _lastScrollTurnCount || _busy != _lastScrollBusy;
if (shouldScroll) {
    _lastScrollTurnCount = _turns.Count;
    _lastScrollBusy = _busy;
    await JS.InvokeVoidAsync("scrollChatToBottom", "chat-thread");
}
```

### 2. Chat Panel Visual Redesign

Remove from `DraftWorkspace.razor`:
- `<h3>Writing Assistant</h3>` (topbar title covers this)
- `<strong>@turn.Role:</strong>` labels inside each bubble

Change placeholder text from:
> "Ask for help drafting or improving your position description..."

To:
> `Message the assistant — e.g. "Draft a mid-level software engineer PD"`

CSS changes to `app.css`:
- Remove `border` and `background` from `.chat-thread` — messages flow on the plain panel background
- Tighten `.chat-bubble` padding slightly for denser feel

### 3. App Shell Layout

**`MainLayout.razor`** — rewrite to full app shell:

```razor
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
                <span>👤</span>
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
                <!-- ⚙ button injected by DraftWorkspace via CascadingValue or kept here -->
                <button class="ghost-btn" @onclick="() => _settingsOpen = true">⚙ Settings</button>
            </div>
        </header>
        <div class="main-body">
            @Body
        </div>
    </div>
</div>
```

State in `MainLayout.razor`:
```csharp
private bool _sidebarOpen = true;
private void ToggleSidebar() => _sidebarOpen = !_sidebarOpen;
```

> **Note on Settings modal:** Since the ⚙ button lives in `MainLayout` but reads AI config, inject `IConfiguration` into `MainLayout.razor` directly. `_settingsOpen` state lives in `MainLayout.razor` too.

**`DraftWorkspace.razor`** — remove its `workspace-topbar` block entirely. The workspace `div` becomes `@Body` output directly.

### 4. CSS — New Rules (`app.css`)

```
.app-shell              → display: flex; height: 100vh; overflow: hidden
.history-sidebar        → width: 240px; flex-shrink: 0; display: flex; flex-direction: column; transition: width 0.2s ease; background: #f0f4fc; border-right: 1px solid #dde3ef
.history-sidebar--collapsed → width: 0; overflow: hidden
.main-area              → flex: 1; display: flex; flex-direction: column; min-width: 0
.main-topbar            → display: flex; justify-content: space-between; align-items: center; padding: 10px 14px; border-bottom: 1px solid #e4e8f0
.topbar-left            → display: flex; align-items: center; gap: 10px
.topbar-actions         → display: flex; align-items: center; gap: 8px
.app-title              → font-size: 1rem; font-weight: 600; color: #1d2433
.main-body              → flex: 1; overflow: hidden; padding: 12px
.icon-btn               → borderless icon button, 32x32, hover background
.new-chat-btn           → full-width button, subtle style
.sidebar-history        → flex: 1; overflow-y: auto; padding: 8px
.sidebar-empty          → centered muted text
.sidebar-footer         → border-top; padding: 10px
.sidebar-user-btn       → full-width, flex row, icon + label
.modal-overlay          → fixed inset-0, rgba backdrop
.modal-card             → centered white card, 360px wide, border-radius: 12px, padding: 24px
```

Remove from existing rules:
- `.main-layout { padding: 12px }` — replaced by `.main-body`
- `.workspace-topbar` — moved into `MainLayout`

---

## File Change Summary

| File | Change |
|---|---|
| `App.razor` | Add `scrollChatToBottom` JS function |
| `MainLayout.razor` | Rewrite to app shell (sidebar + topbar + settings modal) |
| `DraftWorkspace.razor` | Remove topbar, add scroll tracking, remove role labels, new placeholder, add `id="chat-thread"` |
| `app.css` | Add app-shell/sidebar/topbar/modal styles, remove border from chat-thread, remove old topbar/main-layout rules |

## Future Integration Points

- **Chat history:** Replace `sidebar-history` empty state with a list of `ChatSession` objects. `+ New Chat` clears `_turns` and starts a fresh session.
- **User auth:** Replace `sidebar-user-btn` with user avatar + name when OIDC token is present. The `topbar-actions` div has room for a user chip if preferred there instead.
- **Model selector:** Settings modal can gain an editable dropdown when runtime model switching is needed.
