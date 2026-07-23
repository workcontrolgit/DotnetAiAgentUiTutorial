# Intake Brainstorming Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a user opens a new chat, the AI greets them and guides them through a structured one-question-at-a-time intake before drafting, with a confirmation step before generating the PD.

**Architecture:** Two changes only. (1) The system prompt in `HrAgent.cs` gains an intake preamble that replaces the old "Input modes" section — governing AI behavior before a draft exists. (2) `DraftWorkspace.razor` injects a static welcome `ChatTurn` when `SessionId == null`, giving users an immediate first question to respond to with zero API latency.

**Tech Stack:** Blazor Server (.NET 10), bUnit (component tests), xUnit

## Global Constraints

- Never include `Co-Authored-By:` in commit messages
- Test command: `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests` from the project root (74 tests currently passing — must stay passing)
- `WelcomeTurn` timestamp must be `DateTimeOffset.MinValue` (sentinel — never persisted, never sorted with real turns)
- `FakeConversationServiceWithSession` already exists in `DraftWorkspaceTests.cs` — reuse it, do not duplicate
- All changes confined to the three files listed in the File Map below

---

## File Map

| File | Change |
|------|--------|
| `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs` | Replace "Input modes" section in `SystemPrompt` with intake preamble |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add `WelcomeTurn` static field; inject in `OnParametersSetAsync` |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs` | Add 2 component tests |

---

## Task 1: Update system prompt with intake preamble

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs` (lines 22–25)

**Interfaces:**
- Produces: Updated `SystemPrompt` const — AI now runs intake before drafting

No automated test for this task — `SystemPrompt` is a `private const string`. Correctness is verified by the welcome message integration in Task 2 and manual QA.

- [ ] **Step 1: Locate the "Input modes" section**

Open `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs`. Find lines 22–25 (inside the `SystemPrompt` raw string literal):

```
        Input modes — detect automatically from the first message:
        - Freeform description ("I need a GS-13 cloud architect"): extract intent, draft immediately, flag gaps.
        - Pasted notes or old PD: clean up language, apply agency template, flag non-compliant sections.
        - Incomplete context: ask one targeted question ("What grade level are you targeting?"), draft when ready.
```

- [ ] **Step 2: Replace with intake preamble**

Replace those 4 lines with the following (preserve the 8-space indent of the surrounding raw string content):

```
        ## Intake Mode (before a draft exists)

        When the conversation has no draft yet, you are a collaborative intake specialist:

        1. Ask ONE question at a time. Never ask multiple questions in one message.
        2. Gather these minimum required fields before drafting:
           - Position title
           - Pay plan / series / grade (e.g., GS-2210-14)
           - Summary of major duties (even a few sentences)
        3. After minimum fields are collected, ask adaptive follow-up questions for any
           of these still unknown:
           - Supervisory status (supervisory / non-supervisory)
           - Remote / telework eligibility
           - Security clearance requirement
           - Education or specialized experience requirements
        4. When you have enough to draft, summarize what you have collected:
           "Here's what I have so far:
           - Title: [title]
           - Grade: [grade]
           - Duties: [summary]
           [other fields if known]
           Ready to generate your draft, or is there anything you'd like to add or change first?"
           Wait for the user to confirm before generating the draft.
        5. If the user provides a free-form description or pastes content, extract what
           you can, summarize it back, and confirm:
           "I've captured the following — shall I proceed with the draft?"
        6. If the user says "just draft it" or similar, acknowledge and confirm once:
           "Got it — I'll draft with what I have. Here's my understanding: [summary]. Proceed?"
           Then draft on confirmation.

        Input modes (after intake is complete or user bypasses intake):
        - Pasted notes or old PD: clean up language, apply agency template, flag non-compliant sections.
```

- [ ] **Step 3: Build the project to verify no compile errors**

```
dotnet build DotnetAiAgentUi/src/HrMcp.Agent
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run the full test suite**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests
```

Expected: Passed — 74 tests, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs
git commit -m "feat: add intake brainstorming preamble to system prompt"
```

---

## Task 2: Inject WelcomeTurn on new session + component tests

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`

**Interfaces:**
- Consumes: `ChatTurn(string Role, string Text, DateTimeOffset Timestamp)` — existing sealed record in `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/ChatTurn.cs`
- Consumes: `FakeConversationServiceWithSession` — already defined as a private nested class in `DraftWorkspaceTests.cs` (added in Smart Draft Router Task 5); do NOT re-define it
- Produces: `WelcomeTurn` static field visible to tests via the component's rendered output

- [ ] **Step 1: Write the failing tests**

Open `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`.

Add these two tests before the closing `}` of the `DraftWorkspaceTests` class:

```csharp
[Fact]
public void NewSession_ShowsWelcomeBubble()
{
    // No SessionId parameter — WelcomeTurn should be injected
    var cut = RenderComponent<DraftWorkspace>();
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

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "NewSession_ShowsWelcomeBubble|ExistingSession_DoesNotShowWelcomeBubble"
```

Expected: FAIL — `NewSession_ShowsWelcomeBubble` fails because no welcome bubble exists yet; `ExistingSession_DoesNotShowWelcomeBubble` may pass or fail.

- [ ] **Step 3: Add WelcomeTurn static field to DraftWorkspace.razor**

In the `@code` block, add this field near the top — after `private static readonly string[] DraftIntentTerms` and before the `private static readonly MarkdownPipeline` field (around line 242):

```csharp
private static readonly ChatTurn WelcomeTurn = new(
    "Assistant",
    "Hi! I'm your Position Description Writing Assistant. Let's build your PD together \u2014 " +
    "I'll ask a few quick questions first so the draft fits your position accurately.\n\n" +
    "**What position are you hiring for?** *(e.g., IT Specialist, Program Analyst, Contracting Officer)*",
    DateTimeOffset.MinValue
);
```

- [ ] **Step 4: Inject WelcomeTurn in OnParametersSetAsync**

Locate the `if (SessionId is null)` block in `OnParametersSetAsync` (around line 303):

```csharp
if (SessionId is null)
{
    _turns.Clear();
    return;
}
```

Replace it with:

```csharp
if (SessionId is null)
{
    _turns.Clear();
    _turns.Add(WelcomeTurn);
    return;
}
```

- [ ] **Step 5: Run the new tests to verify they pass**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "NewSession_ShowsWelcomeBubble|ExistingSession_DoesNotShowWelcomeBubble"
```

Expected: PASS (2 tests)

- [ ] **Step 6: Run the full suite**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests
```

Expected: Passed — 76 tests, 0 failed.

- [ ] **Step 7: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs
git commit -m "feat: inject welcome turn on new session to start intake brainstorming"
```

---

## Self-Review

**Spec coverage:**
- ✅ AI greets user automatically on new chat → Task 2 (`WelcomeTurn` injected when `SessionId == null`)
- ✅ One question at a time, minimum fields, adaptive follow-ups → Task 1 (system prompt intake preamble)
- ✅ Confirm before drafting → Task 1 (preamble rule 4)
- ✅ Free-form bypass: acknowledge, fill, confirm → Task 1 (preamble rules 5 & 6)
- ✅ Welcome not shown on session reload → Task 2 (`ExistingSession_DoesNotShowWelcomeBubble` test)
- ✅ `WelcomeTurn.Timestamp == DateTimeOffset.MinValue` → Task 2 step 3
- ✅ `FakeConversationServiceWithSession` reused, not duplicated → Task 2 step 1 (explicit note)

**Placeholder scan:** No TBDs. All code is complete. System prompt preamble is verbatim. Test code is complete.

**Type consistency:**
- `ChatTurn` constructor: `(string Role, string Text, DateTimeOffset Timestamp)` — matches sealed record definition in `ChatTurn.cs` ✅
- `_turns.Add(WelcomeTurn)` — `_turns` is `List<ChatTurn>`, `WelcomeTurn` is `ChatTurn` ✅
- `ConversationSession`, `ConversationTurn` — same types used in existing Task 5 tests ✅
