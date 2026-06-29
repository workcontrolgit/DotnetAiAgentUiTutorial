# Part 4: Real-Time UX & Session Persistence

Series: AI Agent UI with Blazor United & .NET 10 | Part 4 of 8
GitHub: workcontrolgit/DotnetAiAgentUiTutorial
![Series 2 cover](screenshots/blog_cover.png)

---

## Introduction

Part 3 gave us a working chat panel: the user types a prompt, presses Enter, and sees a response. But three things are missing that real users expect: visible feedback while the model is thinking, conversation history that survives a page refresh, and a draft panel that appears automatically when the agent produces a document.

This part adds all three. We examine the `_busy` flag lifecycle that drives the loading indicator, the `IConversationService` integration that persists sessions to the database, and the draft intelligence logic that decides when to push a response into the WYSIWYG editor on the right.

---

## The UX Model

The agent uses `HrAgent.AskAsync`, which calls `chatClient.GetResponseAsync` — a single blocking call that returns the complete response once the model finishes. This is the simplest possible model: send a prompt, wait, get a string.

The UI challenge is making that wait feel responsive. The user should see:

1. Their message appear instantly in the thread
2. A loading indicator while the model works
3. The response appear as formatted markdown once complete

All three happen within `SendPromptAsync` in `DraftWorkspace.razor`, orchestrated by the `_busy` flag.

---

## Step 1 — The `_busy` Lifecycle

`_busy` is a single `bool` field that drives three things at once:

```csharp
// DraftWorkspace.razor @code section
private bool _busy;
```

When `_busy` is `true`:
- The Send button is disabled
- The loading bubble appears in the chat thread
- The Export dropdown is disabled

The complete lifecycle in `SendPromptAsync`:

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
private async Task SendPromptAsync()
{
    if (_busy || string.IsNullOrWhiteSpace(Prompt))
        return;

    _busy = true;
    _status = string.Empty;
    var input = Prompt.Trim();
    Prompt = string.Empty;

    try
    {
        // Create a new session on the first turn
        if (SessionId is null)
        {
            var newSession = await ConversationService.CreateSessionAsync(_userId, input);
            SessionId = newSession.Id;
            Nav.NavigateTo($"/workspace/{SessionId}", forceLoad: false);
        }

        _turns.Add(new ChatTurn("You", input, DateTimeOffset.UtcNow));
        await ConversationService.AddTurnAsync(SessionId.Value, _userId, "user", input);

        var response = await AgentDraftService.SendPromptAsync(input, SessionId);
        _turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
        await ConversationService.AddTurnAsync(SessionId.Value, _userId, "assistant", response);

        if (ShouldSyncDraft(input, response))
        {
            // ... draft sync logic covered in Step 4 ...
        }
    }
    catch (Exception ex)
    {
        _status = $"Error: {ex.Message}";
    }
    finally
    {
        _busy = false;
    }
}
```

The guard at the top — `if (_busy || string.IsNullOrWhiteSpace(Prompt)) return;` — is the re-entrancy protection. Even if the user triggers `SendPromptAsync` twice simultaneously (keyboard shortcut and button click), the second invocation bails immediately.

The `finally` block is load-bearing. It guarantees `_busy = false` regardless of whether the operation succeeded, failed, or threw. Without it, any exception would leave `_busy = true` permanently and lock the user out of the input until they refresh the page.

The Send button's `disabled` attribute has two conditions:

```razor
<button class="primary-btn primary-btn--compact"
        @onclick="SendPromptAsync"
        disabled="@(_busy || string.IsNullOrEmpty(_userId))">Send</button>
```

`_userId` is empty until `OnParametersSetAsync` resolves the authenticated user from `UserContext`. This prevents submitting a prompt before the user identity is loaded — which would fail at `ConversationService.CreateSessionAsync`.

---

## Step 2 — Session Persistence

Each conversation is a session stored in the database. The session ID is part of the URL — `/workspace/{SessionId:guid}`. This means:

- Navigating to `/workspace/` always shows a fresh chat
- Bookmarking or refreshing `/workspace/7cfc32b1-...` restores the full conversation

**First turn — creating a session:**

```csharp
if (SessionId is null)
{
    var newSession = await ConversationService.CreateSessionAsync(_userId, input);
    SessionId = newSession.Id;
    Nav.NavigateTo($"/workspace/{SessionId}", forceLoad: false);
}
```

`CreateSessionAsync` creates a new session row named after the first prompt. `Nav.NavigateTo` with `forceLoad: false` updates the address bar without a page reload — the component stays alive, the GUID appears in the URL.

**Every turn — persisting to the database:**

```csharp
await ConversationService.AddTurnAsync(SessionId.Value, _userId, "user", input);
// ... get response ...
await ConversationService.AddTurnAsync(SessionId.Value, _userId, "assistant", response);
```

Both turns are persisted. The component's `_turns` list is the in-memory display state; the database is the persistent truth.

**Restoring a session on load:**

When the component loads with a `SessionId` route parameter, `OnParametersSetAsync` rehydrates the conversation:

```csharp
protected override async Task OnParametersSetAsync()
{
    _userId = await UserContext.GetUserIdAsync() ?? string.Empty;

    if (SessionId is null)
    {
        _turns.Clear();
        return;
    }

    var session = await ConversationService.GetSessionAsync(SessionId.Value, _userId);
    if (session is null)
    {
        Nav.NavigateTo("/");
        return;
    }

    _turns.Clear();
    foreach (var turn in session.Turns.OrderBy(t => t.Timestamp))
    {
        var role = string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase) ? "You" : "Assistant";
        _turns.Add(new ChatTurn(role, turn.Text, turn.Timestamp));
    }
}
```

If the session exists and belongs to the current user, turns are loaded and rendered in order. If it does not exist — wrong user, deleted session — the component redirects to `/`.

`AgentDraftService.SendPromptAsync` also receives the `SessionId`. On each call it loads the session history from the database and calls `_agent.ResetHistory(priorMessages)` before forwarding the new prompt to the LLM. This gives the model full conversation context even after a page refresh, at the cost of one extra database read per turn.

---

## Step 3 — The Loading Indicator

The loading bubble appears the moment `_busy` becomes `true` — before any await:

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

This block sits inside the `chat-thread` div, after the committed turns loop. Setting `_busy = true` triggers a Blazor re-render (because it happens before the first `await`), which inserts the loading bubble immediately. The `finally` block sets `_busy = false`, which triggers another re-render that removes the bubble and shows the completed turn.

The CSS spinner requires no JavaScript:

```css
/* wwwroot/css/app.css */
.chat-spinner {
    width: 14px;
    height: 14px;
    border: 2px solid var(--surface1);
    border-top-color: var(--blue);
    border-radius: 50%;
    display: inline-block;
    animation: spin 0.7s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }
```

The `aria-live="polite"` on the loading bubble announces the assistant activity to screen readers. The `role="status"` communicates that this is a status update, not interactive content.

![Chat panel with AI response](screenshots/chat-with-response.png)

---

## Step 4 — Keyboard UX

Enter sends the prompt. Shift+Enter adds a newline. This is the expected behavior for anyone who has used a chat application.

```csharp
private async Task HandlePromptKeyDown(KeyboardEventArgs args)
{
    if (!string.Equals(args.Key, "Enter", StringComparison.Ordinal) || args.ShiftKey)
        return;

    Prompt = Prompt.TrimEnd('\r', '\n');
    await SendPromptAsync();
}
```

The condition reads: only act when `Enter` is pressed *without* Shift. `TrimEnd('\r', '\n')` strips the newline the browser inserts into the textarea value before the handler fires — so the text sent to the agent is clean.

`HandlePromptKeyDown` delegates to `SendPromptAsync`, which means the `_busy` guard prevents submitting while a response is in flight without any additional logic in the key handler.

The textarea uses `@bind:event="oninput"` so `Prompt` updates on every keystroke:

```razor
<textarea class="chat-input"
          @bind="Prompt"
          @bind:event="oninput"
          @onkeydown="HandlePromptKeyDown"
          placeholder="Ask for help drafting or improving your position description..."></textarea>
```

Without `@bind:event="oninput"`, the default `onchange` binding only fires on blur — meaning `HandlePromptKeyDown` would see a stale `Prompt` value when Enter is pressed.

---

## Step 5 — Draft Intelligence

Not every response belongs in the WYSIWYG editor. "List open positions" returns a formatted table — that goes in the chat thread. "Draft a position description for a software engineer" returns a multi-section markdown document — that belongs in the editor.

`ShouldSyncDraft` makes the call:

```csharp
private static bool ShouldSyncDraft(string prompt, string response)
{
    if (string.IsNullOrWhiteSpace(response) || string.IsNullOrWhiteSpace(prompt))
        return false;

    return IsDraftIntentPrompt(prompt);
}

private static bool IsDraftIntentPrompt(string prompt)
{
    var normalized = prompt.ToLowerInvariant();

    if (NonDraftIntentTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
        return false;

    return DraftIntentTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
}
```

Draft-intent keywords include: `draft`, `job description`, `jd`, `pd`, `position description`, `rewrite`, `revise`, `refine`, `improve`, `edit`, `add`, `include`, `qualification`, `requirement`. Non-draft overrides include: `list open positions`, `show open positions`.

When `ShouldSyncDraft` returns `true`, `ExtractDraftMarkdown` strips the conversational preamble (anything before the first `#` heading) and trailing closing questions:

```csharp
private static string? ExtractDraftMarkdown(string response)
{
    var lines = response.Split(["\r\n", "\n"], StringSplitOptions.None);

    var start = -1;
    for (var i = 0; i < lines.Length; i++)
    {
        if (lines[i].StartsWith('#')) { start = i; break; }
    }

    // No heading found — conversational reply, skip draft update
    if (start < 0) return null;

    var end = lines.Length - 1;
    while (end > start && IsClosingLine(lines[end])) end--;

    return string.Join("\n", lines[start..(end + 1)]).Trim();
}
```

`IsClosingLine` strips lines starting with "Do you", "Would you", "Let me know", "Feel free", "If you" — the conversational offers that models append after a draft.

The extracted markdown is converted to HTML via Markdig and loaded into Quill:

```csharp
var html = NormalizeHtmlForQuill(Markdown.ToHtml(draftMarkdown, ChatMarkdownPipeline));
if (!_draftVisible)
{
    _pendingHtml = html;
    _draftVisible = true;   // triggers render that creates the Quill container
}
else
{
    await JS.InvokeVoidAsync("setQuillContent", "quill-editor-wrapper", html);
}
```

The `_pendingHtml` / `_draftVisible` pattern handles a Quill constraint: you cannot populate the editor before its container element is in the DOM. When `_draftVisible` flips to `true`, Blazor adds the right panel on the next render. `OnAfterRenderAsync` picks up `_pendingHtml` and calls `loadQuillWhenReady` — a JS function that polls until Quill is initialized before injecting the content.

![Chat with draft synced to editor panel](screenshots/chat-with-draft.png)

---

## What We Have

The chat panel now does four things that Part 3 laid the groundwork for but did not fully explain:

1. **Visible feedback** — the loading bubble appears the moment `_busy = true`, before any await, giving the user immediate confirmation that their prompt was received
2. **Session persistence** — conversations survive page refreshes; the URL encodes the session ID and `OnParametersSetAsync` reloads the history
3. **Context replay** — navigating to a prior session rebuilds the agent's in-memory history from the database so the LLM sees the full conversation on each call
4. **Draft intelligence** — prompts containing draft-intent keywords trigger `ExtractDraftMarkdown`, which isolates the document body and pushes it into the Quill editor automatically

The response model is single-call throughout: `GetResponseAsync` blocks until the model finishes, then the full response renders at once. That simplicity is what makes the session replay clean — there are no partial states or streaming cursors to manage.

---

## Next Up

Part 5 covers the right panel in detail: the Quill WYSIWYG editor setup, the drag-to-resize splitter, and the three-format export dropdown (Word, Markdown, JSON).

→ **[Part 5: Document Editor & Word Export](part-5-document-editor-and-export.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
