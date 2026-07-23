# PD Browser — Design Spec

**Date:** 2026-07-23  
**Feature:** Browse & reuse existing Position Descriptions as a draft starting point  
**Status:** Approved

---

## Goal

Let hiring managers find and reuse an existing PD from the database as a starting point for drafting, rather than always starting from scratch. Browsing is triggered from the welcome screen, from natural language in the chat, or discovered via the help system. The AI takes over after selection to guide updates.

---

## Architecture

Three entry points all converge on a single inline Blazor panel rendered inside the chat thread:

```
Welcome screen button  ──┐
Chat phrase             ──┼──► _browsing = true ──► PdBrowserPanel renders in chat thread
Help hint (user types)  ──┘
```

### Files

| File | Change |
|------|--------|
| `DraftWorkspace.razor` | `_browsing` flag; `IsBrowseIntent()` routing; welcome shortcuts; `NonDraftIntentTerms` additions; `OnPositionSelected` handler |
| `PdBrowserPanel.razor` (new) | Search/filter UI, position cards, preview card, confirmation |
| `HrAgent.cs` | Browse hint added to `## Help Mode` block in `SystemPrompt` |
| `DraftIntentTests.cs` | `BrowsePhrases_AreBrowseIntentPrompts` theory (covers `IsBrowseIntent`) |

No new NuGet packages. No new services. `PositionService` (already in `HrMcp.Application`) is injected directly into the panel.

---

## Component: PdBrowserPanel.razor

A shared Blazor component that renders as an assistant-style chat bubble inside the chat thread. Three internal states:

### State 1 — Browse/Filter

Displayed when the panel first opens.

```
┌──────────────────────────────────────────────────┐
│ 📂 Browse Existing Position Descriptions         │
│                                                  │
│ [🔍 Search by title, keyword...              ]  │
│ [Filter by Organization              ▼       ]  │
│                                                  │
│ ┌──────────────────────────────────────────┐    │
│ │ IT Specialist                   GS-9/11  │    │
│ │ Office of Information Technology 🟢 Open │    │
│ │ Series 2210 · Washington DC              │    │
│ └──────────────────────────────────────────┘    │
│ ┌──────────────────────────────────────────┐    │
│ │ Program Analyst                 GS-12    │    │
│ │ Budget Office                   🔴 Closed│    │
│ │ Series 0343 · Remote                     │    │
│ └──────────────────────────────────────────┘    │
│                               [Cancel]           │
└──────────────────────────────────────────────────┘
```

**Filtering behaviour:**
- All positions loaded once on `OnInitializedAsync` via `PositionService` (no further DB calls)
- Search input filters `Title` + `Description` + `Duties` (case-insensitive substring, client-side)
- Org dropdown populated from distinct `HiringOrganization.OrganizationName` values in the loaded list; default = "All organizations"
- Cards display: Title, OrganizationName, OccupationalSeries, PayGradeMin/Max, DutyLocation, Open 🟢 / Closed 🔴 badge
- If no results match filter → show "No positions match your search."
- If list is empty on load → show "No existing PDs found. Start a new one by describing your position above."

### State 2 — Preview Card

Shown after the user clicks a position card.

```
┌──────────────────────────────────────────────────┐
│ 📋 IT Specialist (GS-9/11) · 🟢 Open            │
│ Office of Information Technology                 │
│ Series 2210 · Washington DC                      │
│ ──────────────────────────────────────────────── │
│ Performs a variety of IT management duties       │
│ including planning, policy development...        │
│ (Description, truncated to ~300 chars)           │
│                                                  │
│          [← Back]    [Use this PD →]            │
└──────────────────────────────────────────────────┘
```

- "← Back" returns to State 1, preserving current search/filter values
- "Use this PD →" triggers the confirmation flow (State 3)

### State 3 — Confirmation (panel exits)

On "Use this PD →":
1. Panel disappears from the chat thread (`_browsing = false`)
2. Draft panel populates from the selected position's fields (see Data Flow)
3. A user-visible chat turn appears: *"Using [Title] as starting point"* (role: You)
4. A hidden seeded prompt is sent to the AI via `AgentDraftService.SendAsync` with the prompt text: *"The manager has loaded the existing [Title] PD (Series [OccupationalSeries], [PayGradeMin]–[PayGradeMax]). Ask what they would like to update."* — this call goes through the normal streaming path so the AI response appears as an assistant chat bubble.
5. AI responds in the chat panel and leads the conversation forward

**Error:** If `GetPositionByIdAsync` returns null on confirmation, show an inline error inside the preview card ("Could not load this PD. Please try another.") and keep the panel open.

### Component Parameters

```csharp
[Parameter] public EventCallback<int> OnPositionSelected { get; set; }
[Parameter] public EventCallback OnCancel { get; set; }
```

---

## Entry Points

### 1. Welcome Screen Shortcuts

Two shortcut buttons appear below the existing welcome message text:

```
What position are you hiring for? (e.g., IT Specialist, Program Analyst...)
Type help at any time if you need guidance.
Type browse PDs to find and reuse an existing position description.

──────────────────────────────────────
  [✏️ Start from scratch]  [📂 Browse existing PDs]
──────────────────────────────────────
```

- "Start from scratch" button: focuses the chat input (no state change)
- "Browse existing PDs" button: sets `_browsing = true`, panel appears in chat thread. No chat message is sent.

The shortcut buttons are only shown when `_turns.Count == 0` (same condition as the welcome message display).

### 2. Chat Natural Language

A new static method `IsBrowseIntent(string prompt)` in `DraftWorkspace.razor`:

```csharp
private static readonly string[] BrowseIntentTerms =
[
    "browse pd",
    "browse pds",
    "browse position",
    "browse positions",
    "list pd",
    "list pds",
    "list position",
    "list positions",
    "show pd",
    "show pds",
    "show positions",
    "find pd",
    "find pds",
    "search pd",
    "search pds",
    "search positions",
    "use existing",
    "start from existing",
    "copy existing",
    "existing pd",
    "existing pds",
];

public static bool IsBrowseIntent(string prompt) =>
    BrowseIntentTerms.Any(t => prompt.ToLowerInvariant().Contains(t));
```

In `SendPromptAsync`, check `IsBrowseIntent` before `IsDraftIntentPrompt`:

```
if IsBrowseIntent(prompt) → set _browsing = true, add user turn "Browse existing PDs", do NOT call AI
else if IsDraftIntentPrompt(prompt) → draft flow
else → general chat flow
```

All `BrowseIntentTerms` entries are also added to `NonDraftIntentTerms` so `IsDraftIntentPrompt` never fires for them if the order check is missed.

### 3. Help Integration

**SystemPrompt addition** (appended to the `## Help Mode` block in `HrAgent.cs`):

```
          Browsing existing PDs:
          - Questions about reusing, copying, or starting from an existing PD,
            or browsing the PD library
          → Tell the manager to type "browse PDs" in the chat to open the
            PD browser, where they can search by title, keyword, or organization
            and select a PD to use as their starting point.
```

**WelcomeTurn addition** (third hint line, after the existing two):

```
"*Type **browse PDs** to find and reuse an existing position description.*"
```

---

## Data Flow: Draft Loading

On `OnPositionSelected(int positionId)` in `DraftWorkspace`:

1. Call `PositionService.GetPositionByIdAsync(positionId)`
2. Build draft skeleton:

```
## Summary
{Description}

## Duties
{Duties}

## Qualifications
{Qualifications}

## Education
{Education}

## Series & Grade
Series: {OccupationalSeries} – {OccupationalSeriesTitle}
Grade: {PayGradeMin}–{PayGradeMax}
```

3. Assign to `_draft` (existing draft field) — triggers draft panel to appear
4. Add user chat turn: `"Using [Title] as starting point"`
5. Call `AgentDraftService.SendAsync` with prompt: `"The manager has loaded the existing [Title] PD (Series [OccupationalSeries], [PayGradeMin]–[PayGradeMax]). Ask what they would like to update."` — uses the normal streaming path so the AI response appears as an assistant chat bubble

---

## Error Handling

| Scenario | Behaviour |
|----------|-----------|
| Position list empty on load | Panel shows "No existing PDs found. Start a new one by describing your position above." Cancel button still works |
| Search/filter returns no results | Panel shows "No positions match your search." |
| `GetPositionByIdAsync` returns null on confirm | Inline error in preview card: "Could not load this PD. Please try another." Panel stays open |

---

## Testing

### New unit tests in `DraftIntentTests.cs`

**`BrowsePhrases_AreBrowseIntentPrompts`** — `[Theory]` covering all `BrowseIntentTerms` entries returns `true` from `IsBrowseIntent`.

**`BrowsePhrases_AreNotDraftIntentPrompts`** — same phrase set returns `false` from `IsDraftIntentPrompt` (guards the `NonDraftIntentTerms` additions).

Run with:
```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/
```

No Blazor component tests — filtering logic is pure C# and covered via the static method tests.

---

## Constraints

- .NET 10, Blazor Server, xUnit
- No new NuGet packages
- One new file: `PdBrowserPanel.razor` (shared component)
- All other changes to existing files only
- `nullable enable` and top-level namespace declarations on all C# blocks
- Commit prefix: `feat:`
