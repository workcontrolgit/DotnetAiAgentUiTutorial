# Intake Brainstorming Flow — Design Spec

**Date:** 2026-07-20
**Status:** Approved
**Scope:** `AgentDraftService.cs` (system prompt), `DraftWorkspace.razor` (welcome injection), `DraftWorkspaceTests.cs` (2 new tests)

---

## Problem

When a user opens a new chat, they see a blank textarea with no guidance. They must independently decide what to type and how much context to provide. Underspecified prompts produce shallow drafts that need many correction rounds. There is no structured intake process to gather the minimum required PD fields before drafting.

---

## Goal

When a new session opens, the AI automatically greets the user and guides them through a conversational intake — one question at a time — gathering position title, grade/series, and duties before drafting. The AI confirms a summary with the user before generating the draft. If the user bypasses the flow with a free-form prompt, the AI acknowledges, fills what it can, and confirms before drafting.

---

## Design Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Welcome trigger | Static frontend injection | Zero latency, zero API cost; conversational intelligence lives in system prompt |
| Intake questions | Hybrid: fixed minimum + adaptive follow-ups | Minimum guarantees a complete draft; adaptive avoids interrogating users who already provided details |
| Transition to draft | AI summarizes → user confirms | Prevents premature drafts; user sees what AI understood before committing |
| Free-form bypass | AI acknowledges, fills gaps, confirms | Respects user intent without silently producing a poor draft |

---

## Architecture

### Welcome Message Injection — `DraftWorkspace.razor`

A static `ChatTurn` is injected into `_turns` when `SessionId == null` (new session, no turns). No API call is made. The session is not created until the user sends their first message.

```csharp
private static readonly ChatTurn WelcomeTurn = new(
    "Assistant",
    "Hi! I'm your Position Description Writing Assistant. Let's build your PD together — " +
    "I'll ask a few quick questions first so the draft fits your position accurately.\n\n" +
    "**What position are you hiring for?** *(e.g., IT Specialist, Program Analyst, Contracting Officer)*",
    DateTimeOffset.MinValue
);
```

**Injection point** — in `OnParametersSetAsync`, at the existing `if (SessionId is null)` block:

```csharp
if (SessionId is null)
{
    _turns.Clear();
    _turns.Add(WelcomeTurn);   // static, not stored in DB
    return;
}
```

`DateTimeOffset.MinValue` is used as a sentinel timestamp. `WelcomeTurn` is never persisted to the DB and never restored from session history, so the timestamp never participates in any sort.

### Session Lifecycle

```
User opens /  (SessionId == null)
        │
        ▼
OnParametersSetAsync: _turns.Clear(); _turns.Add(WelcomeTurn)
        │
        ▼
User sends first message
        │
        ▼
SendPromptAsync: ConversationService.CreateSessionAsync → SessionId assigned
        │
        ▼
Nav.NavigateTo(/workspace/{SessionId})
        │
        ▼
OnParametersSetAsync: SessionId is set → normal restore path → WelcomeTurn NOT re-injected
```

### System Prompt — Intake Preamble — `AgentDraftService.cs`

Prepended before the existing PD drafting instructions:

```
## Intake Mode (before a draft exists)

You are also a collaborative intake specialist. When the conversation has no draft yet:

1. Ask ONE question at a time. Never ask multiple questions in one message.
2. Gather these minimum required fields before drafting:
   - Position title
   - Pay plan / series / grade (e.g., GS-2210-14)
   - Summary of major duties (even a few sentences)
3. After minimum fields are collected, ask adaptive follow-up questions for any
   of these that are still unknown:
   - Supervisory status (supervisory / non-supervisory)
   - Remote / telework eligibility
   - Security clearance requirement
   - Education or specialized experience requirements
4. When you have enough to draft, summarize what you've collected:
   "Here's what I have so far: [bullet summary]. Ready to generate your draft,
   or is there anything you'd like to add or change first?"
   Wait for the user to confirm before generating the draft.
5. If the user provides a free-form description or pastes content, extract what
   you can, summarize it back, fill obvious gaps with [TBD], and ask:
   "I've captured the following — shall I proceed with the draft?"
6. If the user says "just draft it" or similar, acknowledge and confirm once:
   "Got it — I'll draft with what I have. Here's my understanding: [summary].
   Proceed?" Then draft on confirmation.
```

The existing PD drafting instructions (OPM compliance, section format, series guidance) follow unchanged.

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| User reloads `/` | `_turns.Clear()` → `WelcomeTurn` injected again — correct for a fresh chat |
| User navigates away mid-intake, returns via `/` | Session was never created → URL is `/` → welcome shown again |
| User sent first message → nav to `/workspace/{id}` → reloads | `SessionId` set → normal restore path → welcome NOT injected |
| User pastes a full job description as first message | AI intake preamble: extract, summarize, confirm before drafting |
| User types "just draft it" mid-intake | AI acknowledges, summarizes understanding, confirms once, then drafts |
| Existing session opened from sidebar | `SessionId` always set → `OnParametersSetAsync` skips welcome injection entirely |
| `WelcomeTurn` timestamp (`DateTimeOffset.MinValue`) in session restore | `WelcomeTurn` is never added when `SessionId` is set — no timestamp conflict |

---

## Testing

**File:** `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`

```csharp
[Fact]
public void NewSession_ShowsWelcomeBubble()
{
    var cut = RenderComponent<DraftWorkspace>();
    // No SessionId → WelcomeTurn injected
    var bubbles = cut.FindAll(".chat-bubble-row--assistant");
    Assert.Single(bubbles);
    Assert.Contains("Position Description Writing Assistant", bubbles[0].TextContent);
}

[Fact]
public void ExistingSession_DoesNotShowWelcomeBubble()
{
    var sessionId = Guid.NewGuid();
    var session = new ConversationSession
    {
        Id = sessionId,
        UserId = "testuser",
        Name = "Test",
        Turns =
        [
            new ConversationTurn
            {
                Role = "user",
                Text = "draft a pd",
                Timestamp = DateTimeOffset.UtcNow
            }
        ]
    };
    Services.AddScoped<IConversationService>(_ => new FakeConversationServiceWithSession(session));
    var cut = RenderComponent<DraftWorkspace>(p => p.Add(w => w.SessionId, sessionId));
    cut.WaitForAssertion(() =>
        Assert.DoesNotContain(
            "Position Description Writing Assistant",
            cut.Find(".chat-thread").TextContent));
}
```

`FakeConversationServiceWithSession` already exists from Task 5 of the Smart Draft Router implementation.

---

## Files Changed

| File | Change |
|------|--------|
| `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` | Prepend intake preamble to system prompt string |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add `WelcomeTurn` static field; inject in `OnParametersSetAsync` when `SessionId is null` |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs` | Add 2 component tests |

---

## Out of Scope

- Visual starter prompt cards / chips on the empty state
- Tracking intake "progress" with a progress bar or checklist
- Saving intake answers separately from conversation turns
- Different intake flows per role (HR specialist vs. manager)
- Next-action suggestions after draft (separate parked spec: `2026-07-20-next-action-suggestions-design.md`)
