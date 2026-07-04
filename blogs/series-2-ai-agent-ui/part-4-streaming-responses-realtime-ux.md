<<<<<<< HEAD
# Part 4: Streaming Responses & Real-Time UX

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 4 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)
=======
# Part 4: Real-Time UX & Session Persistence

Series: AI Agent UI with Blazor United & .NET 10 | Part 4 of 8
GitHub: workcontrolgit/DotnetAiAgentUiTutorial
![Series 2 cover](screenshots/blog_cover.png)
>>>>>>> release/v1

---

## Introduction

<<<<<<< HEAD
Part 3 gave us a fully wired chat panel: type a prompt, press Enter, wait, read the response. It works. But "wait" is the problem. A well-structured job description draft can take 8–12 seconds to generate. During that time, the UI is frozen and silent. To a user who has never seen an AI at work, that looks like a crash.

This part fixes that. We add three things: a loading spinner that appears the moment the user sends a prompt, a cancel button so they can bail if the response goes off-track, and — the centerpiece — token-by-token streaming so text appears word by word as the model generates it.

The parallel in Series 1 is [Part 4: Multi-Model AI Agent with Microsoft.Extensions.AI](../series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md), which wired up `IChatClient` and the manual tool loop. We are building on that same abstraction — `IChatClient` is the surface we call, and `CompleteStreamingAsync` is the streaming entry point it exposes.

---

## Why Streaming Matters

The UX problem is straightforward: a model generating 500 tokens at 30 tokens per second takes about 16 seconds. If you wait for all 500 tokens before rendering anything, the user stares at a blank space for 16 seconds. That feels broken. It is broken — from a perceived performance standpoint.

Streaming solves this by returning tokens as they are generated. The model produces token one, it arrives in your application, you render it. Token two arrives, you append it. By the time the model finishes, the user has been reading for 15 seconds and the final render is simply the last word appearing.

There is a second benefit: early exit. If the model starts generating a response that is clearly wrong — wrong position, wrong format, wrong tone — the user can cancel after two sentences instead of waiting 16 seconds to see a response they will immediately discard.

`Microsoft.Extensions.AI` exposes this through `IChatClient.CompleteStreamingAsync()`, which returns `IAsyncEnumerable<StreamingChatCompletionUpdate>`. Each update carries a `Text` fragment — typically one or a few tokens. You iterate the async enumerable, accumulate the fragments, and update the UI after each one.

---

## Step 1 — The Streaming Call

The non-streaming call in the current codebase looks like this:

```csharp
// Current: waits for the full response before returning
var response = await _agent!.AskAsync(prompt, ct);
```

The streaming equivalent uses `CompleteStreamingAsync` directly on `IChatClient`:

```csharp
// Streaming pattern — tokens arrive as they are generated
var sb = new StringBuilder();
await foreach (var update in chatClient.CompleteStreamingAsync(messages, cancellationToken: ct))
{
    sb.Append(update.Text);
    // update the UI here after each token
}
_turns.Add(new ChatTurn("Assistant", sb.ToString(), DateTimeOffset.UtcNow));
```

A few things to note about this pattern:

`await foreach` is the C# async iteration syntax for `IAsyncEnumerable<T>`. It awaits each `MoveNextAsync()` call, so it does not block the thread between tokens — it yields control while waiting for the next fragment.

`update.Text` is the incremental fragment. It is typically one or a few tokens, not a full sentence. You accumulate these into a `StringBuilder` rather than concatenating strings in a loop.

The final `_turns.Add` happens after the loop completes, which means the turn is only committed to the permanent history once the full response is done. While streaming is in progress, you surface the partial text through a separate in-flight state variable rather than a committed `ChatTurn`.

To surface the in-flight text during streaming, introduce a separate field:

```csharp
// In DraftWorkspace.razor @code section
private string _streamingText = string.Empty;
```

Then render it in the chat thread alongside the committed turns:

```razor
@if (_busy && !string.IsNullOrEmpty(_streamingText))
{
    <div class="chat-bubble-row chat-bubble-row--assistant">
        <div class="chat-bubble">
            <strong>Assistant:</strong>
            <div class="chat-bubble-content">@((MarkupString)_streamingText)</div>
        </div>
    </div>
}
```

---

## Step 2 — StateHasChanged and Blazor's Rendering Loop

This is the part that trips up most developers the first time.

Blazor does not automatically re-render when a background operation updates component state. The rendering loop is tied to the Blazor synchronization context. When you are inside an `await foreach` loop receiving tokens from the model, you are effectively running on a background continuation — Blazor has no automatic hook to know that `_streamingText` changed.

You have to tell it explicitly:

```csharp
await foreach (var update in chatClient.CompleteStreamingAsync(messages, cancellationToken: ct))
{
    sb.Append(update.Text);
    _streamingText = sb.ToString();
    await InvokeAsync(StateHasChanged);
}
```

`StateHasChanged()` is the Blazor method that queues a re-render. But calling it directly from a background thread is not safe — it needs to run on the Blazor dispatcher.

`InvokeAsync()` is `ComponentBase`'s method that marshals a delegate onto the Blazor synchronization context. Wrapping `StateHasChanged` in `InvokeAsync` is the correct and thread-safe pattern for triggering re-renders from async operations that run off the Blazor dispatcher.

Without `InvokeAsync`, you will either see no UI updates during streaming (the renders are dropped), or you will hit intermittent threading exceptions. With it, each token arrival triggers a re-render on the right thread.

One performance note: calling `await InvokeAsync(StateHasChanged)` on every single token is fine for models producing tokens at 20–50 tokens per second. If you ever need to handle faster producers, you can batch — accumulate tokens for 50ms and then render — but for standard local Ollama or Azure OpenAI rates, per-token rendering is smooth.

---

## Step 3 — The Loading Spinner

Streaming or not, there is always a window between the user pressing Send and the first token arriving. During that window — which can be 1–3 seconds for local models — the UI needs to communicate that something is happening.

The current `DraftWorkspace.razor` already has this handled with the `_busy` flag and a loading bubble. Here is the exact markup from the file:
=======
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
>>>>>>> release/v1

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

<<<<<<< HEAD
This block sits inside the `chat-thread` div, after the committed turns loop. The moment `_busy` becomes `true`, Blazor renders the loading bubble. Once tokens start arriving, you can swap this bubble out for the in-flight streaming bubble shown in Step 1.

The `_busy` flag drives three things in parallel:

1. The loading spinner visible in the chat thread.
2. The Send button's `disabled` attribute — `disabled="@_busy"` prevents double-sends while a response is in flight.
3. The Export Word button's `disabled` attribute — you do not want an export triggered while a response is still generating.

Here is the `_busy` lifecycle in `SendPromptAsync`:

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
    _turns.Add(new ChatTurn("You", input, DateTimeOffset.UtcNow));

    try
    {
        var response = await AgentDraftService.SendPromptAsync(input);
        _turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
        // ... draft sync logic ...
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

The `finally` block is load-bearing. It guarantees `_busy = false` regardless of whether the operation succeeded, failed, or was cancelled. Without it, a network error or `OperationCanceledException` would leave `_busy = true` permanently, locking the user out of the input until they refresh the page.

The guard at the top — `if (_busy || string.IsNullOrWhiteSpace(Prompt)) return;` — is the re-entrancy protection. Even if the user somehow triggers `SendPromptAsync` twice (keyboard shortcut plus button click simultaneously), the second invocation bails out immediately when it sees `_busy` is already `true`.

---

## Step 4 — Graceful Cancellation

The service interface already carries `CancellationToken` through the stack:

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs
public interface IAgentDraftService
{
    Task<string> SendPromptAsync(string prompt, CancellationToken ct = default);
    Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(string draftText, CancellationToken ct = default);
}
```

And the implementation passes it straight through to the agent:

```csharp
public async Task<string> SendPromptAsync(string prompt, CancellationToken ct = default)
{
    await EnsureInitializedAsync(ct);
    return await _agent!.AskAsync(prompt, ct);
}
```

On the component side, the cancellation token comes from a `CancellationTokenSource` that the component owns:

```csharp
private CancellationTokenSource _cts = new();
```

When the user sends a prompt, create a fresh source and pass its token to the service:

```csharp
private async Task SendPromptAsync()
{
    if (_busy || string.IsNullOrWhiteSpace(Prompt))
        return;

    _cts = new CancellationTokenSource();
    _busy = true;
    _status = string.Empty;
    var input = Prompt.Trim();
    Prompt = string.Empty;
    _turns.Add(new ChatTurn("You", input, DateTimeOffset.UtcNow));

    try
    {
        var response = await AgentDraftService.SendPromptAsync(input, _cts.Token);
        _turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
    }
    catch (OperationCanceledException)
    {
        _turns.Add(new ChatTurn("Assistant", "[Cancelled]", DateTimeOffset.UtcNow));
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

Notice the separate `catch (OperationCanceledException)`. When the user cancels, the `await foreach` inside the streaming loop throws `OperationCanceledException` — that is the standard .NET cooperative cancellation contract. We catch it specifically to add a `[Cancelled]` turn to the thread rather than treating it as an error. The user sees the partial response they already observed during streaming, followed by a `[Cancelled]` marker.

Add a cancel button next to the Send button in the composer:

```razor
<div class="panel-actions-row panel-actions-row--end">
    <span
        class="help-icon"
        title="Enter to send, Shift+Enter for a new line."
        aria-label="Help: Enter to send, Shift+Enter for a new line.">?</span>
    @if (_busy)
    {
        <button class="ghost-btn ghost-btn--compact" @onclick="() => _cts.Cancel()">Cancel</button>
    }
    <button class="primary-btn primary-btn--compact" @onclick="SendPromptAsync" disabled="@_busy">Send</button>
</div>
```

The cancel button only appears when `_busy` is true — there is nothing to cancel when the panel is idle. Clicking it calls `_cts.Cancel()`, which signals the token, which causes the `await foreach` in the streaming loop (or the `await` on `AskAsync` in the current non-streaming implementation) to throw `OperationCanceledException` at the next await point.

What happens on the server side? The `CancellationToken` flows into `HrAgent.AskAsync`, into the `chatClient.GetResponseAsync` or `CompleteStreamingAsync` call, and into any MCP tool invocations in flight. When the token fires, the HTTP call to the model provider is aborted. If a tool call was in progress, it is abandoned. The MCP server itself is unaffected — it may finish processing, but the client discards the result.

The `HandlePromptKeyDown` handler also respects the pattern cleanly. Enter submits, Shift+Enter adds a newline — and since it delegates to `SendPromptAsync`, the `_busy` guard prevents submitting while a response is in progress:
=======
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
>>>>>>> release/v1

```csharp
private async Task HandlePromptKeyDown(KeyboardEventArgs args)
{
    if (!string.Equals(args.Key, "Enter", StringComparison.Ordinal) || args.ShiftKey)
        return;

    Prompt = Prompt.TrimEnd('\r', '\n');
    await SendPromptAsync();
}
```

<<<<<<< HEAD
---

## Putting It Together

Here is the complete picture of what happens when the user types a prompt and presses Enter:

1. `HandlePromptKeyDown` fires, calls `SendPromptAsync`.
2. `_busy = true` — the loading spinner appears, both buttons disable.
3. A fresh `CancellationTokenSource` is created.
4. The user's turn is added to `_turns` and rendered immediately.
5. `AgentDraftService.SendPromptAsync(input, _cts.Token)` is called — control yields to the async machinery.
6. (Streaming path) `CompleteStreamingAsync` returns an `IAsyncEnumerable`. Tokens arrive one by one. Each token appends to `_streamingText`. `InvokeAsync(StateHasChanged)` re-renders the bubble.
7. The `await foreach` completes. The full text is committed to `_turns` as an `Assistant` turn.
8. The `finally` block runs: `_busy = false`, `_streamingText = string.Empty`.
9. Blazor re-renders the thread — the streaming bubble disappears, the committed turn appears, buttons re-enable.

If the user clicks Cancel at step 6, the token fires, the `await foreach` throws, the `catch (OperationCanceledException)` block adds a `[Cancelled]` turn, and the `finally` block cleans up. The partial text the user already read in the streaming bubble is gone from the live display (since `_streamingText` is cleared in `finally`), but the `[Cancelled]` marker in `_turns` shows that an exchange happened.
=======
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
>>>>>>> release/v1

---

## What We Have

<<<<<<< HEAD
The chat panel is now fully alive. Responses appear token by token as the model generates them. The loading spinner covers the window before the first token arrives. The cancel button gives users agency over long or misdirected responses. The `CancellationToken` flows all the way from the UI gesture to the underlying HTTP call, so cancellation is real — not just a UI-level dismiss.

The architecture did not change. `IAgentDraftService` is still the seam. `IChatClient` is still the model abstraction. The only additions are the streaming iteration, the `CancellationTokenSource`, and `InvokeAsync(StateHasChanged)` inside the loop.
=======
The chat panel now does four things that Part 3 laid the groundwork for but did not fully explain:

1. **Visible feedback** — the loading bubble appears the moment `_busy = true`, before any await, giving the user immediate confirmation that their prompt was received
2. **Session persistence** — conversations survive page refreshes; the URL encodes the session ID and `OnParametersSetAsync` reloads the history
3. **Context replay** — navigating to a prior session rebuilds the agent's in-memory history from the database so the LLM sees the full conversation on each call
4. **Draft intelligence** — prompts containing draft-intent keywords trigger `ExtractDraftMarkdown`, which isolates the document body and pushes it into the Quill editor automatically

The response model is single-call throughout: `GetResponseAsync` blocks until the model finishes, then the full response renders at once. That simplicity is what makes the session replay clean — there are no partial states or streaming cursors to manage.
>>>>>>> release/v1

---

## Next Up

<<<<<<< HEAD
Part 5 adds a document editor panel to the right of the chat — a WYSIWYG editor where the agent's draft responses appear, with a Word export button.
=======
Part 5 covers the right panel in detail: the Quill WYSIWYG editor setup, the drag-to-resize splitter, and the three-format export dropdown (Word, Markdown, JSON).
>>>>>>> release/v1

→ **[Part 5: Document Editor & Word Export](part-5-document-editor-and-export.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
