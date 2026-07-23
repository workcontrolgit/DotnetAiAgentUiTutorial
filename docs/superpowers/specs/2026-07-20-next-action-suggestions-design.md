# Next-Action Suggestions — Design Spec

**Date:** 2026-07-20
**Status:** Approved
**Scope:** `AgentDraftService.cs` (interface + implementation), `DraftWorkspace.razor`, `app.css`, test files

---

## Problem

After a PD draft is created or updated, users see a summary in the chat panel but have no guidance on what to do next. They must independently decide whether to improve HR compliance, strengthen duties, or refine qualifications — a blank input box with no direction.

---

## Goal

After every draft response (creation and revision), display 3 AI-generated suggested next actions inline in the chat thread as clickable chips, plus a "None" dismiss option. Clicking a chip populates the chat textarea with the suggestion text for the user to edit before sending.

---

## User Stories

- **HR specialist:** After the draft appears, I want to see specific compliance gaps I should address next, without having to remember OPM requirements.
- **Recruiter:** After the draft appears, I want suggestions that make the posting more appealing to candidates.
- **Power user:** I want to be able to dismiss suggestions and type my own prompt.

---

## Architecture

### Flow

```
Draft routed to Draft panel + summary added to _turns
        │
        ▼
_suggestionsLoading = true  →  StateHasChanged (loading indicator appears)
        │
        ▼  (non-blocking: Task.Run)
AgentDraftService.GetNextActionSuggestionsAsync(draftMarkdown)
        │
        ├── success → _suggestions = string[3], _suggestionsLoading = false → StateHasChanged
        └── failure → _suggestions = [],        _suggestionsLoading = false → StateHasChanged (silent)
```

Suggestions are **ephemeral UI state** — never stored in `_turns`, never persisted to the DB or conversation history.

### Suggestion Lifecycle

| Event | Effect on `_suggestions` |
|-------|--------------------------|
| Draft routed (creation or revision) | Cleared, then repopulated when AI responds |
| User clicks a suggestion chip | `Prompt` = suggestion text; `_suggestions = []` |
| User clicks "None" | `_suggestions = []` |
| User sends any message | `_suggestions = []` at top of `SendPromptAsync` |

---

## Service Layer

### Interface change — `IAgentDraftService` (in `AgentDraftService.cs`)

Add one method:

```csharp
Task<string[]> GetNextActionSuggestionsAsync(string draftMarkdown, CancellationToken ct = default);
```

### Implementation — `AgentDraftService`

Calls the AI with a focused prompt. **No session ID, not stored in conversation history.**

**System prompt injected into call:**
> You are an HR compliance and recruitment expert for U.S. federal government position descriptions.

**User prompt template:**
```
Given this GS position description draft, suggest exactly 3 concise next actions
(each under 12 words) to improve HR compliance or attract more candidates.
Return ONLY a valid JSON array of 3 strings. No explanation. No markdown fences.

Example: ["Review qualifications against GS-14 OPM standards", "Add telework eligibility statement", "Strengthen duties with measurable outcomes"]

Draft:
{draftMarkdown}
```

**Response parsing:**
```csharp
var suggestions = JsonSerializer.Deserialize<string[]>(response.Trim());
return suggestions is { Length: >= 1 } ? suggestions.Take(3).ToArray() : [];
```

If `JsonSerializer.Deserialize` throws, catch and return `[]`. Suggestions are an enhancement — failure must never disrupt the draft workflow.

---

## Component Changes — `DraftWorkspace.razor`

### New fields

```csharp
private string[] _suggestions = [];
private bool _suggestionsLoading;
```

### `SendPromptAsync` — clear suggestions at start, fire generation after draft route

At the top of `SendPromptAsync` (before `_busy = true`):
```csharp
_suggestions = [];
_suggestionsLoading = false;
```

After draft is routed to Quill (inside `if (routedDraftMarkdown is not null)`), fire non-blocking:
```csharp
_suggestionsLoading = true;
await InvokeAsync(StateHasChanged);

_ = Task.Run(async () =>
{
    var suggestions = await AgentDraftService.GetNextActionSuggestionsAsync(routedDraftMarkdown);
    _suggestions = suggestions;
    _suggestionsLoading = false;
    await InvokeAsync(StateHasChanged);
});
```

### Render — chips after last assistant bubble

In the chat thread `foreach` loop, immediately after the `SeriesRecommendationBanner` block:

```razor
@if (!isUser && turn == _turns[^1])
{
    @if (_suggestionsLoading)
    {
        <div class="suggestion-chips suggestion-chips--loading">
            <span class="chat-spinner" aria-hidden="true"></span>
            <span>Suggesting next steps…</span>
        </div>
    }
    else if (_suggestions.Length > 0)
    {
        <div class="suggestion-chips">
            @foreach (var s in _suggestions)
            {
                var suggestion = s;
                <button class="suggestion-chip" @onclick="() => AcceptSuggestion(suggestion)">@suggestion</button>
            }
            <button class="suggestion-chip suggestion-chip--none" @onclick="DismissSuggestions">None</button>
        </div>
    }
}
```

Note: `var suggestion = s` captures the loop variable for the lambda closure.

### New handlers

```csharp
private void AcceptSuggestion(string text)
{
    Prompt = text;
    _suggestions = [];
}

private void DismissSuggestions() => _suggestions = [];
```

---

## CSS — `app.css`

Add after the `.chat-bubble-content` block:

```css
.suggestion-chips {
    display: flex;
    flex-wrap: wrap;
    gap: 0.4rem;
    margin: 0.4rem 0 0.6rem 0.5rem;
}

.suggestion-chips--loading {
    align-items: center;
    color: var(--text-muted, #888);
    font-size: 0.85rem;
    gap: 0.5rem;
}

.suggestion-chip {
    background: transparent;
    border: 1px solid var(--border-color, #d0d5dd);
    border-radius: 1rem;
    color: var(--text-primary, #344054);
    cursor: pointer;
    font-size: 0.8rem;
    padding: 0.25rem 0.75rem;
    transition: background 0.15s, border-color 0.15s;
}

.suggestion-chip:hover {
    background: var(--surface-hover, #f0f4ff);
    border-color: var(--accent, #4f7af8);
}

.suggestion-chip--none {
    border-color: transparent;
    color: var(--text-muted, #888);
}

.suggestion-chip--none:hover {
    background: var(--surface-hover, #f0f4ff);
    border-color: transparent;
}
```

---

## Testing

### Unit tests — `AgentDraftService` parsing (`AgentDraftServiceSuggestionTests.cs`)

Since `GetNextActionSuggestionsAsync` parsing is `internal`/private to the service, test via a lightweight helper or test the public method with a mock HTTP response. At minimum, test the parsing logic as an `internal static` helper:

`internal static string[] ParseSuggestions(string response)` — extracts the JSON-parse-and-fallback logic for testability.

Tests:
- Valid JSON array of 3 → returns 3 strings
- Valid JSON array of 5 → returns first 3 only
- Invalid JSON → returns `[]`
- Empty string → returns `[]`
- JSON array of 1 → returns that 1 string (minimum viable)

### Component tests — `DraftWorkspaceTests.cs`

Extend `FakeAgentDraftService` with:
```csharp
public string[] NextSuggestions { get; set; } = [];
public Task<string[]> GetNextActionSuggestionsAsync(string draftMarkdown, CancellationToken ct = default) =>
    Task.FromResult(NextSuggestions);
```

Tests:
- Draft response → suggestion chips render in chat after suggestions arrive
- Clicking a chip → textarea is populated with chip text
- Clicking "None" → chips disappear
- Sending a new message → chips cleared before response arrives
- Conversational response (no draft) → no chips rendered

---

## Files Changed

| File | Change |
|------|--------|
| `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` | Add `GetNextActionSuggestionsAsync` to interface + implementation; add `ParseSuggestions` static helper |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add `_suggestions`, `_suggestionsLoading` fields; update `SendPromptAsync`; add chip render block; add `AcceptSuggestion`, `DismissSuggestions` handlers |
| `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css` | Add suggestion chip styles |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Services/AgentDraftServiceSuggestionTests.cs` | New file — unit tests for `ParseSuggestions` |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs` | Extend `FakeAgentDraftService`; add 5 component tests |

---

## Out of Scope

- Persisting suggestions to conversation history
- Showing suggestions on session restore
- More than 3 suggestions
- User-configurable suggestion categories
- Suggestions for conversational (non-draft) responses
