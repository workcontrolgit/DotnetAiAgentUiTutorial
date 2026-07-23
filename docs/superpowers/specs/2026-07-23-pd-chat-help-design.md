# PD Chat Help — Design Spec

**Date:** 2026-07-23
**Status:** Approved
**Scope:** `HrAgent.cs`, `DraftWorkspace.razor`, `DraftIntentTests.cs`

---

## Problem

Hiring managers using the PD Draft Builder have no way to get guidance on:

1. **Getting started** — "What do I type first?"
2. **Feature guidance** — "What does the ⚠️ checklist item mean?"
3. **Federal HR concepts** — "What is a GS series code?"

There is no help surface in the app. A manager who types "help" currently receives an intake question or a draft — not guidance.

---

## Design Decisions

| # | Decision | Choice | Reason |
|---|---|---|---|
| D1 | Help delivery surface | In-chat AI response | Chat is the primary UI; no new surface needed |
| D2 | Help response visual signal | ℹ️ prefix on every help response | One character distinguishes guidance from draft actions; zero new UI components |
| D3 | Help implementation mechanism | System prompt extension | The AI already plays HR Specialist; extending the prompt adds help-desk role without new service methods or components |
| D4 | Draft routing impact | None — help responses use existing non-draft chat path | Help responses contain no `##` section headings; `IsDraftIntentPrompt` + `ExtractDraftMarkdown` already suppress draft routing for non-draft responses |

---

## Solution

Extend the `SystemPrompt` in `HrAgent.cs` with a `## Help Mode` block. Add a one-line hint to the welcome message. Add unit tests verifying help phrases do not trigger draft routing.

---

## Section 1 — System Prompt Extension (`HrAgent.cs`)

### New block added to `SystemPrompt`

Insert after the `## Intake Mode` block (ending with `- Pasted notes or old PD: clean up language, apply agency template, flag non-compliant sections.`), before the `When drafting or updating a PD, always output the draft using these section headings in order:` line:

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

### Behavior

| Manager types | AI does |
|---|---|
| "help" | Explains three input modes, ends with "Which works best for you?" |
| "how do I use this" | Same as above |
| "what does ⚠️ mean" | Explains checklist warning state in 2-3 sentences |
| "what is a GS series code" | Explains OPM series codes briefly in plain English |
| "what is a GS-13" | Explains GS-13 grade level briefly |
| Any drafting request after a help response | Resumes normal intake/drafting flow |

### Draft routing impact

None. Help responses contain no `## ` markdown section headings matching PD template sections. The existing `ExtractDraftMarkdown` returns `null` for help responses, and `IsDraftIntentPrompt` returns `false` for help phrases. The draft panel does not open.

---

## Section 2 — Welcome Message Update (`DraftWorkspace.razor`)

Add one italic hint line to `WelcomeTurn`:

**Before:**
```
Hi! I'm your Position Description Writing Assistant. Let's build your PD together — 
I'll ask a few quick questions first so the draft fits your position accurately.

**What position are you hiring for?** *(e.g., IT Specialist, Program Analyst, Contracting Officer)*
```

**After:**
```
Hi! I'm your Position Description Writing Assistant. Let's build your PD together — 
I'll ask a few quick questions first so the draft fits your position accurately.

**What position are you hiring for?** *(e.g., IT Specialist, Program Analyst, Contracting Officer)*

*Type **help** at any time if you need guidance on how to use this tool.*
```

No new fields, no new state, no new components.

---

## Section 3 — Tests

### New theory test (add to `DraftIntentTests.cs`)

Verifies that common help phrases return `false` from `IsDraftIntentPrompt` — i.e., they do not trigger draft routing:

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

### Manual smoke test

1. Open the app — confirm welcome message ends with help hint
2. Type "help" — response starts with ℹ️, no draft panel opens, no checklist activates
3. Type "what does ⚠️ mean" — response starts with ℹ️, explains warning state
4. Type "what is a GS-13" — response starts with ℹ️, explains grade level
5. After any help response, send a drafting message — normal intake/draft flow resumes

---

## Files Changed

| File | Change |
|------|--------|
| `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs` | Add `## Help Mode` block to `SystemPrompt` constant |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add help hint line to `WelcomeTurn` |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs` | Add `HelpPhrases_AreNotDraftIntentPrompts` theory |

---

## Out of Scope

- A dedicated help panel, modal, or sidebar
- Hardcoded FAQ lookup table
- Help history persistence (separate from normal conversation turns)
- A "?" button or help icon in the UI
- Structured onboarding tour or wizard
