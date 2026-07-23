# Smart Draft Router — Design Spec

**Date:** 2026-07-20
**Status:** Approved
**Scope:** `DraftWorkspace.razor` only

---

## Problem

When the AI responds with a PD draft, the full draft markdown is added to the chat turn list AND rendered in the Draft panel (Quill editor). The user sees the same content twice — once as a chat bubble (raw markdown) and once as a formatted document in the Draft panel.

Additionally, when a draft is first created, the layout does not automatically reveal the Draft panel side-by-side with Chat.

---

## Goal

- Draft content routes to the Draft panel only; a concise summary notification appears in chat.
- The Chat panel remains the space for conversational turns, follow-up questions, and status messages.
- On the first draft creation, the layout auto-switches to "Both" (chat + draft side-by-side).
- Historical chat turns (session restore) show summaries, not raw draft markdown.

---

## Approach

All changes are confined to `DraftWorkspace.razor`. No new files, interfaces, or services.

---

## Routing Logic

```
AI response received
        │
        ▼
ShouldSyncDraft(prompt, response)?
   ├── YES → ExtractDraftMarkdown(response)
   │        ├── Draft found ──► push to Quill
   │        │                   add BuildChatSummary() to _turns
   │        │                   store full response in DB
   │        └── No draft (edge case) → add full response to _turns (fallback)
   └── NO  → add full response to _turns (conversational turn, no change)
```

**Key rule:** Full AI response is always persisted to the DB via `ConversationService.AddTurnAsync` (required for session restoration and draft extraction on reload). Only what appears in the `_turns` UI list changes.

**Layout auto-switch:** No new code needed. Setting `_draftVisible = true` with `_leftPanelHidden = false` (its default value) already produces the "both" layout per the existing `LayoutMode` computed property.

---

## New Static Helpers

Three `internal static` methods added to `DraftWorkspace`, following the existing pattern of `ExtractDraftMarkdown`, `IsClosingLine`, `IsDraftIntentPrompt`, etc.

### `GetDraftSectionNames(string draftMarkdown) → List<string>`

Parses all `##` headings from the draft markdown.

```
Input:  "# IT Specialist\n## Position Info\n...\n## Major Duties\n..."
Output: ["Position Info", "Major Duties", ...]
```

### `ExtractPositionTitle(string draftMarkdown) → string?`

Returns the text of the first `# ` (H1) heading, or `null` if not found.

```
Input:  "# IT Specialist, GS-2210-14\n## Position Info\n..."
Output: "IT Specialist, GS-2210-14"
```

### `BuildChatSummary(string? previousDraftMarkdown, string newDraftMarkdown) → string`

Returns a markdown string for the chat turn.

**First draft** (`previousDraftMarkdown` is null):

```
✅ **Draft created** — IT Specialist, GS-2210-14
Sections: Position Info, Pay Plan/Series/Grade, Major Duties, Qualifications, Security Clearance, Remote Work
```

**Updated draft** (previous exists):

```
✅ **Draft updated** — IT Specialist, GS-2210-14
Added: Qualifications
Revised: Major Duties, Remote Work
```

Change detection:
- Section in new but not old → **Added**
- Section in both old and new → **Revised** (always listed; we cannot cheaply diff paragraph content)
- Section in old but not new → not mentioned (removal is surfaced by the checklist component)
- If no title found, title line is omitted

---

## Changes to `SendPromptAsync`

Replace the current `ShouldSyncDraft` block (lines 409–431) with:

```
1. if ShouldSyncDraft(input, response):
     draftMarkdown = ExtractDraftMarkdown(response)
     if draftMarkdown is not null:
       summary = BuildChatSummary(_currentDraftMarkdown, draftMarkdown)
       _turns.Add(ChatTurn("Assistant", summary, now))          ← summary in chat
       ConversationService.AddTurnAsync(..., response)           ← full text in DB
       push draftMarkdown to Quill (existing Quill logic)
       update _currentDraftMarkdown, _hasDraft, _checklistState
     else:
       _turns.Add(ChatTurn("Assistant", response, now))          ← fallback: full text
       ConversationService.AddTurnAsync(..., response)
   else:
     _turns.Add(ChatTurn("Assistant", response, now))            ← conversational
     ConversationService.AddTurnAsync(..., response)
```

Note: `ConversationService.AddTurnAsync` is called in all branches with the full response to preserve session restoration fidelity.

---

## Changes to `OnParametersSetAsync` (Session Restore)

After the `foreach` that rebuilds `_turns` from stored session history, add a second pass:

```
for each turn in _turns where Role == "Assistant":
    if ExtractDraftMarkdown(turn.Text) is string md:
        turn = new ChatTurn("Assistant", BuildChatSummary(null, md), turn.Timestamp)
```

This replaces draft-containing historical turns with summaries in the UI. The full text remains in the DB.

Since `ChatTurn` is a record, the list is rebuilt with replaced entries for matching positions.

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `ShouldSyncDraft = true` but no `##` headings found | Full response shown in chat (no regression) |
| Draft panel already open when update arrives | Quill content replaced; summary shown in chat; layout unchanged |
| Export Word result (short ✅ message in chat) | Not a draft → `ShouldSyncDraft = false` → shown in chat as-is |
| Follow-up question response (no draft) | `ShouldSyncDraft = false` → shown in chat, draft panel unchanged |
| Series suggestion banner | Extracted from response text independently; unaffected |

---

## Files Changed

| File | Change |
|------|--------|
| `DraftWorkspace.razor` | Modify `SendPromptAsync`, modify `OnParametersSetAsync`, add 3 static helpers |

No other files modified. No new files created.

---

## Out of Scope

- Routing for non-PD structured documents
- AI-generated summaries (second LLM call)
- New services or interfaces
- Changes to `ShouldSyncDraft`, `ResponseContainsDraft`, `IsDraftIntentPrompt`, `ExtractDraftMarkdown`, layout switcher, Quill JS interop, export, checklist, or series banner
