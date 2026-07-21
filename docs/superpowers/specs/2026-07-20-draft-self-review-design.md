# Draft Self-Review — Design Spec

**Date:** 2026-07-20
**Status:** Approved
**Scope:** `AgentDraftService.cs` (interface + implementation), `DraftWorkspace.razor`, test files

---

## Problem

After a PD draft is generated and routed to the Draft panel, users see only a chat summary with no quality feedback. They must independently decide whether the draft is OPM-compliant, complete, and appealing to candidates — with no guidance on what to fix first.

---

## Goal

After the first draft is created, the AI automatically self-reviews it across three lenses — OPM compliance, completeness, and candidate appeal — and posts findings by severity in the chat thread, ending with "What would you like to address first?" The user can then type freely or address a specific finding.

---

## Design Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Trigger | First draft only | Revisions are user-directed; re-reviewing every revision would be noisy |
| Delivery | Dedicated second AI call | Candidate appeal requires AI judgment, not rules; keeps draft generation and review decoupled |
| Location | New assistant chat turn | No new UI surface needed; fits the existing chat thread pattern |
| Failure behavior | Silent (swallow exception, skip turn) | Self-review is an enhancement — must never disrupt the draft workflow |
| Persistence | Saved as normal assistant turn | Review is part of the conversation history; no special storage needed |
| Session restore | Not re-triggered | Restored turns already contain the review from the original session |

---

## Architecture

### Flow

```
User message → AI response → draft routed to panel + summary added to _turns
        │
        └── (first draft only: _selfReviewDone == false)
                │
                ▼
        _selfReviewDone = true
        _selfReviewLoading = true → StateHasChanged (loading indicator appears)
                │
                ▼  (non-blocking: Task.Run)
        AgentDraftService.GetDraftSelfReviewAsync(draftMarkdown)
                │
                ├── success, non-empty → new ChatTurn("Assistant", review) added → StateHasChanged + scrollToBottom
                └── failure or empty  → _selfReviewLoading = false → StateHasChanged (silent)
```

### First-Draft Detection

In `SendPromptAsync`, the check is:

```csharp
bool isFirstDraft = _selfReviewDone is false;
_currentDraftMarkdown = routedDraftMarkdown;

if (isFirstDraft)
{
    _selfReviewDone = true;
    // fire self-review...
}
```

`_selfReviewDone` is never persisted — component state only. On session restore, `_currentDraftMarkdown` is non-null (restored from session turns), so the `routedDraftMarkdown is not null` path is never entered on restore; the self-review is never re-triggered.

---

## Service Layer

### Interface change — `IAgentDraftService` (in `AgentDraftService.cs`)

Add one method:

```csharp
Task<string> GetDraftSelfReviewAsync(string draftMarkdown, CancellationToken ct = default);
```

### Implementation — `AgentDraftService`

No session ID — not stored in conversation history.

**System prompt injected into call:**
> You are an expert HR specialist and federal hiring specialist reviewing a U.S. federal government position description draft.

**User prompt — built by `internal static string BuildSelfReviewPrompt(string draftMarkdown)`:**

```
Review this GS position description draft across three lenses and list findings by severity.

Lenses:
1. OPM Compliance — required sections present, qualifications cite OPM minimum standards,
   duties start with grade-calibrated action verbs, no prohibited language
2. Completeness — supervisory status, remote/telework eligibility, security clearance,
   education/experience requirements present or explicitly marked N/A
3. Candidate Appeal — duties and qualifications attract qualified candidates;
   language is clear, specific, and avoids jargon

Format your response as:
**Draft Self-Review**

🔴 Critical (must fix before posting):
- [issue] — [brief explanation]

🟡 Important (strongly recommended):
- [issue] — [brief explanation]

🟢 Minor (nice to have):
- [issue] — [brief explanation]

If a category has no findings, write "None."

End with: "What would you like to address first?"

Draft:
{draftMarkdown}
```

**Error handling:** If the AI call throws or returns empty, catch and return `""`. The component checks for empty and skips adding a chat turn.

---

## Component Changes — `DraftWorkspace.razor`

### New fields

```csharp
private bool _selfReviewLoading;
private bool _selfReviewDone;
```

### `SendPromptAsync` — fire self-review after first draft route

Inside `if (routedDraftMarkdown is not null)`, after summary turn is added and scroll completes:

```csharp
bool isFirstDraft = _selfReviewDone is false;
_currentDraftMarkdown = routedDraftMarkdown;

if (isFirstDraft)
{
    _selfReviewDone = true;
    _selfReviewLoading = true;
    await InvokeAsync(StateHasChanged);

    _ = Task.Run(async () =>
    {
        var review = await AgentDraftService.GetDraftSelfReviewAsync(routedDraftMarkdown);
        if (!string.IsNullOrWhiteSpace(review))
            _turns.Add(new ChatTurn("Assistant", review, DateTimeOffset.UtcNow));
        _selfReviewLoading = false;
        await InvokeAsync(StateHasChanged);
        await InvokeAsync(() => JS.InvokeVoidAsync("scrollToBottom", "chat-thread"));
    });
}
```

### Loading indicator — in chat thread, below `_turns` foreach loop

```razor
@if (_selfReviewLoading)
{
    <div class="chat-bubble-row chat-bubble-row--assistant">
        <div class="chat-bubble chat-bubble--assistant">
            <span class="chat-spinner" aria-hidden="true"></span>
            <span>Reviewing your draft…</span>
        </div>
    </div>
}
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| First draft — self-review AI call fails | Exception caught, `_selfReviewLoading = false`, no chat turn added, draft unaffected |
| First draft — self-review returns empty string | No chat turn added (guard on `IsNullOrWhiteSpace`) |
| User sends second draft (revision) | `_selfReviewDone == true` → self-review skipped |
| User opens existing session from sidebar | Session restore never enters the `routedDraftMarkdown is not null` path → `_selfReviewDone` stays false but is irrelevant |
| Self-review turn is persisted | Saved to DB as a normal assistant turn; restored with session history on next open |

---

## Testing

### Unit tests — `AgentDraftServiceSelfReviewTests.cs` (new file)

Tests for `BuildSelfReviewPrompt(string draftMarkdown)`:

- Returns string containing the draft markdown
- Returns string containing "OPM Compliance"
- Returns string containing "Completeness"
- Returns string containing "Candidate Appeal"
- Returns string containing "🔴", "🟡", "🟢"
- Returns string ending with "What would you like to address first?"

### Component tests — `DraftWorkspaceTests.cs` (additions)

Extend `FakeAgentDraftService` with:

```csharp
public string SelfReviewResponse { get; set; } = "";
public Task<string> GetDraftSelfReviewAsync(string draftMarkdown, CancellationToken ct = default) =>
    Task.FromResult(SelfReviewResponse);
```

Tests:
- First draft response → loading indicator appears, then self-review turn added to chat
- Second draft response (revision) → no additional self-review turn in chat
- `GetDraftSelfReviewAsync` returns `""` → no extra chat turn added
- Session restore (existing session with prior draft) → self-review not re-triggered

---

## Files Changed

| File | Change |
|------|--------|
| `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` | Add `GetDraftSelfReviewAsync` to interface + implementation; add `BuildSelfReviewPrompt` static helper |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add `_selfReviewLoading`, `_selfReviewDone` fields; update `SendPromptAsync`; add loading indicator render block |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Services/AgentDraftServiceSelfReviewTests.cs` | New file — unit tests for `BuildSelfReviewPrompt` |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs` | Extend `FakeAgentDraftService`; add 4 component tests |

---

## Out of Scope

- Re-reviewing on every revision
- A dedicated review panel or sidebar
- Scoring/rating the draft numerically
- Auto-fixing findings without user direction
- Persisting self-review findings separately from conversation turns
