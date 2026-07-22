# PD Draft Guard Rails — Design Spec

**Date:** 2026-07-22
**Status:** Approved
**Scope:** `AgentDraftService.cs`, `PdChecklistState.cs`, `DraftWorkspace.razor`, `PdSectionChecklist.razor`, `app.js`, test files

---

## Problem

Three gaps in the existing PD Draft feature expose compliance, quality, and UX risks:

1. **Compliance risk** — the self-review has no lens for prohibited language (age indicators, gendered titles, EEO-violating phrasing). A manager can export a PD with discriminatory language without any warning.
2. **Draft quality risk** — the Section Completion Checklist marks a section ✅ as soon as its heading appears in the draft, regardless of body content. Major Duties with 1 bullet shows ✅.
3. **UX risk** — no protection against accidental draft loss (tab close), no way to re-trigger the self-review after manual edits, and the Acknowledge override has no confirmation friction.

---

## Design Decisions

| # | Decision | Choice | Reason |
|---|---|---|---|
| D1 | Where does prohibited language detection fire? | 4th lens in existing self-review | No new triggers, no new UI; self-review already posts findings by severity |
| D2 | How are content-depth failures surfaced? | Downgrade ✅ → ⚠️ in checklist | Doesn't block export; manager can acknowledge; consistent with existing Warning behavior |
| D3 | Threshold determination approach | Minimum structural thresholds (bullet count, sentence count) | Deterministic, no extra AI calls, fast |
| D4 | Acknowledge override friction | Inline two-step confirm in checklist | No modal/JS dialog; pure Razor state; low friction but deliberate |
| D5 | Re-review trigger | On-demand button in right panel header | Manager-initiated; avoids noise on every revision |

---

## Guard Rail 1 — Prohibited Language (4th Self-Review Lens)

### What changes

`AgentDraftService.BuildSelfReviewPrompt` gains a 4th lens block:

```
4. Prohibited Language — flag any of the following:
   - Age indicators (e.g., "young professional", "recent graduate", "under 40")
   - Gender-specific role titles (e.g., "manpower", "chairman", "journeyman")
   - Disability restrictions not tied to a documented bona fide occupational requirement
   - Citizenship/nationality phrasing beyond the standard "U.S. Citizen" or "U.S. National" statements
   - Any pre-employment inquiry language prohibited by EEO law or OPM policy
```

Findings feed into the existing 🔴 / 🟡 / 🟢 severity format. Prohibited language findings are typically 🔴 Critical.

### Behavior

- Fires on first draft only (same trigger as existing self-review — `_selfReviewDone is false`)
- No new AI calls, no new service methods, no new UI
- No change to error handling (silent failure if AI call throws)

### Files changed

| File | Change |
|------|--------|
| `AgentDraftService.cs` | Add 4th lens block to `BuildSelfReviewPrompt` |
| `AgentDraftServiceSelfReviewTests.cs` | Add 1 test: prompt contains "Prohibited Language" |

---

## Guard Rail 2 — Content-Depth Threshold Validation

### Thresholds

| Section | Threshold | Failure → |
|---|---|---|
| Major Duties | ≥ 5 bullet lines (`- ` or `* ` prefix) | ⚠️ Warning |
| Position Summary | ≥ 3 sentences (lines or fragments ending in `.` `!` `?`) | ⚠️ Warning |
| All other required sections | ≥ 1 non-empty, non-heading body line | ⚠️ Warning |

A ⚠️ Warning does not block export. The manager can click Acknowledge to promote to ✅; the acknowledgment is recorded in the JSON export log (existing behavior).

### Implementation — `PdChecklistState.UpdateFromDraft`

`UpdateFromDraft` is extended in two parts:

**Part A — Body map.** Before the section loop, build a `Dictionary<string, List<string>>` mapping each detected heading (normalized) to the body lines beneath it (all lines between that heading and the next `##` heading).

**Part B — Threshold check.** After detecting a heading match and setting `Complete`, call a new `private static bool MeetsDepthThreshold(string sectionName, List<string> bodyLines)` helper. If it returns false, downgrade to `Warning`.

`MeetsDepthThreshold` logic:
```
"Major Duties"       → bodyLines.Count(l => l.TrimStart().StartsWith("- ") || l.TrimStart().StartsWith("* ")) >= 5
"Position Summary"   → bodyLines.Count(l => l.TrimEnd().EndsWith('.') || l.TrimEnd().EndsWith('!') || l.TrimEnd().EndsWith('?')) >= 3
all others (required)→ bodyLines.Any(l => !string.IsNullOrWhiteSpace(l))
```

Locked sections (EEO, Reasonable Accommodation) are skipped as before.

### Files changed

| File | Change |
|------|--------|
| `PdChecklistState.cs` | Add body map build pass + `MeetsDepthThreshold` helper; update `UpdateFromDraft` to downgrade to `Warning` on threshold failure |
| `PdChecklistStateTests.cs` | Add 5 tests (see below) |

### Tests

- Major Duties with 3 bullets → `PdSectionStatus.Warning`
- Major Duties with 5 bullets → `PdSectionStatus.Complete`
- Position Summary with 2 sentences → `PdSectionStatus.Warning`
- Position Summary with 3 sentences → `PdSectionStatus.Complete`
- Required section with heading present but empty body → `PdSectionStatus.Warning`

---

## Guard Rail 3 — UX Safety

### 3a — Dirty-Draft Navigation Warning

**What:** Browser `beforeunload` dialog when the manager tries to close/navigate away with an active draft.

**Implementation:**
- In `DraftWorkspace.razor`, after `_draftVisible = true` is first set, call `JS.InvokeVoidAsync("registerDraftUnloadGuard")`
- In `IAsyncDisposable.DisposeAsync` (add interface to component), call `JS.InvokeVoidAsync("unregisterDraftUnloadGuard")`
- Add two JS functions to `app.js`:

```js
window.registerDraftUnloadGuard = () => {
    window._draftUnloadHandler = e => { e.preventDefault(); e.returnValue = ''; };
    window.addEventListener('beforeunload', window._draftUnloadHandler);
};
window.unregisterDraftUnloadGuard = () => {
    if (window._draftUnloadHandler)
        window.removeEventListener('beforeunload', window._draftUnloadHandler);
};
```

**Files changed:** `DraftWorkspace.razor`, `app.js`

---

### 3b — Re-Review Button

**What:** A "Re-review draft" ghost button in the right panel header, next to Export. Visible only when `_hasDraft && !_selfReviewLoading && !_busy`. Calls `GetDraftSelfReviewAsync` with the current draft markdown and appends the result as a new assistant chat turn.

**Note:** Re-review uses `_currentDraftMarkdown` — the last AI-generated draft. If the manager has manually edited the Quill editor since the last AI response, those edits are not included in the re-review. This is intentional: manual edits are outside the AI's draft state. If the manager wants the AI to review their manual edits, they should send a "revise" prompt first to sync the draft.

**Implementation in `DraftWorkspace.razor`:**

```razor
@if (_hasDraft && !_selfReviewLoading && !_busy)
{
    <button class="ghost-btn" @onclick="ReReviewDraftAsync">Re-review draft</button>
}
```

```csharp
private async Task ReReviewDraftAsync()
{
    if (_currentDraftMarkdown is null || _selfReviewLoading || _busy) return;
    _selfReviewLoading = true;
    await InvokeAsync(StateHasChanged);
    _ = Task.Run(async () =>
    {
        var review = await AgentDraftService.GetDraftSelfReviewAsync(_currentDraftMarkdown);
        if (!string.IsNullOrWhiteSpace(review))
            _turns.Add(new ChatTurn("Assistant", review, DateTimeOffset.UtcNow));
        _selfReviewLoading = false;
        await InvokeAsync(StateHasChanged);
        await InvokeAsync(() => JS.InvokeVoidAsync("scrollToBottom", "chat-thread"));
    });
}
```

**Files changed:** `DraftWorkspace.razor`

---

### 3c — Acknowledge Confirmation (Inline Two-Step)

**What:** Replace the immediate Acknowledge action with a two-step inline confirm: first click shows "[Confirm] [Cancel]" inline in the checklist item; Confirm fires the callback; Cancel dismisses.

**Implementation in `PdSectionChecklist.razor`:**

Add `private string? _pendingAcknowledgeSectionName` field. When "Acknowledge" is clicked, set `_pendingAcknowledgeSectionName = sectionName` instead of immediately invoking the callback. Render the confirm/cancel inline only for the pending item. On Confirm, invoke `OnAcknowledge` and clear the field. On Cancel, clear the field.

No modal, no JS — pure Razor component state.

**Files changed:** `PdSectionChecklist.razor`

---

## Files Changed Summary

| File | Guard Rail |
|------|-----------|
| `AgentDraftService.cs` | GR1 — 4th lens in `BuildSelfReviewPrompt` |
| `AgentDraftServiceSelfReviewTests.cs` | GR1 — 1 new test |
| `PdChecklistState.cs` | GR2 — body map + `MeetsDepthThreshold` + `UpdateFromDraft` update |
| `PdChecklistStateTests.cs` | GR2 — 5 new tests |
| `DraftWorkspace.razor` | GR3a — unload guard registration; GR3b — Re-review button + `ReReviewDraftAsync` |
| `app.js` | GR3a — `registerDraftUnloadGuard` / `unregisterDraftUnloadGuard` |
| `PdSectionChecklist.razor` | GR3c — inline two-step Acknowledge confirm |

---

## Out of Scope

- Re-triggering prohibited language detection on every draft revision
- AI-scored content depth (sentence quality, duty specificity)
- GS grade / series code format validation
- Rate limiting on prompt sends
- Draft version history / restore AI version
