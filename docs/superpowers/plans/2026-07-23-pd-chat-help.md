# PD Chat Help Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let hiring managers ask for help in the chat ("help", "what is a GS series code?", "what does ⚠️ mean?") and receive ℹ️-prefixed plain-language guidance from the AI without triggering a draft.

**Architecture:** Extend the `SystemPrompt` constant in `HrAgent.cs` with a `## Help Mode` block; add a one-line hint to `WelcomeTurn` in `DraftWorkspace.razor`; add a `NonDraftIntentTerms` entry to prevent `"what is a gs …"` phrases from being routed to the draft panel; add a unit test to lock in all 7 help phrases.

**Tech Stack:** .NET 10, Blazor Server, xUnit

## Global Constraints

- All C# files use `nullable enable` and top-level namespace declarations
- Test project: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/` — run with `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/`
- No new NuGet packages
- No new components, services, or files — three existing files only
- Commit message prefix: `feat:` or `fix:`

---

## File Map

| File | Change |
|------|--------|
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add `"what is a gs"` to `NonDraftIntentTerms`; add hint line to `WelcomeTurn` |
| `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs` | Add `## Help Mode` block to `SystemPrompt` constant |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs` | Add `HelpPhrases_AreNotDraftIntentPrompts` theory test |

---

## Task 1: Help Phrase Routing Fix + Test

Ensures the 7 common help phrases are not routed to the draft panel. "what is a GS series code" currently returns `true` from `IsDraftIntentPrompt` because it contains the substring `"series"` (which is in `DraftIntentTerms`). Adding `"what is a gs"` to `NonDraftIntentTerms` fixes this. All other 6 phrases already return `false` with no change needed.

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`
- Test: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`

**Interfaces:**
- Consumes: `DraftWorkspace.IsDraftIntentPrompt(string prompt)` — existing static method
- Produces: `HelpPhrases_AreNotDraftIntentPrompts` theory test (7 `[InlineData]` cases)

- [ ] **Step 1: Write the failing test**

  Add to `DraftIntentTests.cs` at the end of the class (before the closing `}`):

  ```csharp
  [Theory]
  [InlineData("help")]
  [InlineData("how do I use this")]
  [InlineData("what do I do")]
  [InlineData("how do I start")]
  [InlineData("what is a GS series code")]
  [InlineData("what does the checklist mean")]
  [InlineData("what is OPM")]
  public void HelpPhrases_AreNotDraftIntentPrompts(string input)
  {
      Assert.False(DraftWorkspace.IsDraftIntentPrompt(input));
  }
  ```

- [ ] **Step 2: Run to confirm exactly 1 failure**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "HelpPhrases_AreNotDraftIntentPrompts"
  ```

  Expected: 6 pass, 1 fail. The failure will be for `"what is a GS series code"` — it currently returns `true` because it contains the substring `"series"`.

- [ ] **Step 3: Add `"what is a gs"` to `NonDraftIntentTerms` in `DraftWorkspace.razor`**

  Find `NonDraftIntentTerms` in `DraftWorkspace.razor` (currently 3 entries). Add one entry:

  ```csharp
  private static readonly string[] NonDraftIntentTerms =
  [
      "list open position",
      "list open positions",
      "open position",
      "open positions",
      "show open position",
      "show open positions",
      "what is a gs",
  ];
  ```

  **Why this works:** `IsDraftIntentPrompt` checks `NonDraftIntentTerms` first (early return `false`). `"what is a GS series code".ToLowerInvariant()` = `"what is a gs series code"` which contains `"what is a gs"` → returns `false` before the `DraftIntentTerms` check runs.

- [ ] **Step 4: Run all 7 to confirm they pass**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "HelpPhrases_AreNotDraftIntentPrompts"
  ```

  Expected: 7/7 pass.

- [ ] **Step 5: Run full suite**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/
  ```

  Expected: all 99 tests pass (92 existing + 7 new).

- [ ] **Step 6: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
  git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs
  git commit -m "feat: add help phrase routing fix and unit tests"
  ```

---

## Task 2: System Prompt + Welcome Message

Adds the `## Help Mode` block to `HrAgent.cs` and the one-line hint to `WelcomeTurn`. No automated tests for these changes — the routing behaviour is already covered by Task 1. Run the full suite to confirm nothing broke.

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: `SystemPrompt` constant in `HrAgent.cs` — raw string literal (`"""..."""`)
- Consumes: `WelcomeTurn` static field in `DraftWorkspace.razor`
- Produces: no new methods or properties

- [ ] **Step 1: Add `## Help Mode` block to `SystemPrompt` in `HrAgent.cs`**

  Find the line:
  ```
          - Pasted notes or old PD: clean up language, apply agency template, flag non-compliant sections.
  ```

  Insert the following block immediately after it (before the blank line and then `When drafting or updating a PD, always output the draft using these section headings in order:`):

  ```
          ## Help Mode

          When the manager's message is a help question rather than a drafting request,
          respond with a ℹ️-prefixed answer. Do NOT ask an intake question or generate
          a draft. Help questions include any of these patterns:

          Getting started:
          - "help", "how do I use this", "what do I do", "how do I start", "what do I type"
          → Explain the three ways to start: (1) describe the position in plain English,
            (2) paste notes or an old PD, (3) answer my questions one at a time.
            End with: "Which works best for you?"

          Feature guidance:
          - Questions about the checklist, ⚠️, ❌, ✅, 🔒, the export button, the Re-review button,
            the draft panel, or how the app works
          → Explain the UI element in 2-3 plain-English sentences. Do not draft.

          Federal HR concepts:
          - Questions about OPM, GS grades, series codes, qualifications, PD sections,
            EEO, clearance levels, or other federal HR terminology
          → Explain the concept briefly and plainly. If relevant, note how it applies
            to the current draft. Do not draft unless the manager explicitly asks you to.

          Always start help responses with: ℹ️
          Never present a numbered list of options in a help response.
          After a help response, ask: "Is there anything else I can help you with,
          or are you ready to continue with the draft?"
  ```

  The indentation must match the surrounding raw string literal content (8 spaces).

- [ ] **Step 2: Add hint line to `WelcomeTurn` in `DraftWorkspace.razor`**

  Find `WelcomeTurn` — it is a `static readonly ChatTurn` field initialized with a multi-line string. The string currently ends with:

  ```
          "I'll ask a few quick questions first so the draft fits your position accurately.\n\n" +
          "**What position are you hiring for?** *(e.g., IT Specialist, Program Analyst, Contracting Officer)*",
  ```

  Change it to:

  ```
          "I'll ask a few quick questions first so the draft fits your position accurately.\n\n" +
          "**What position are you hiring for?** *(e.g., IT Specialist, Program Analyst, Contracting Officer)*\n\n" +
          "*Type **help** at any time if you need guidance on how to use this tool.*",
  ```

- [ ] **Step 3: Run the full test suite**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/
  ```

  Expected: 99/99 pass.

- [ ] **Step 4: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
  git commit -m "feat: add help mode to system prompt and welcome message hint"
  ```
