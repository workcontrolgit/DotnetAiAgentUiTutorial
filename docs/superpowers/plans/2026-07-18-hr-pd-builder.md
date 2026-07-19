# HR Position Description Builder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the Position Description Builder with a Section Completion Checklist, dual-role AI system prompt, extended draft-intent detection, series recommendation banner, export gate, and acknowledge override.

**Architecture:** `PdChecklistState` is a pure C# class that parses the markdown draft to track the status of 13 agency template sections. `DraftWorkspace.razor` owns the checklist state and passes it as a parameter to the new `PdSectionChecklist.razor` and `SeriesRecommendationBanner.razor` display components. The AI system prompt in `HrAgent.cs` is replaced to give the LLM dual HR Specialist + Writer roles.

**Tech Stack:** .NET 10, Blazor Server (InteractiveServer), xUnit 2.9, bUnit 1.x, BlazoredTextEditor (Quill), Markdig

## Global Constraints

- All C# files target `net10.0`; use C# 13 features (primary constructors, collection expressions) where the existing code already uses them
- Follow existing namespace conventions: `HrMcp.Agent` (src), `HrMcp.Agent.Tests` (tests)
- No new NuGet packages — all required packages are already referenced
- Blazor components go in `DotnetAiAgentUi/src/HrMcp.Agent/Components/` — new shared components in `Components/Shared/`
- Tests go in `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/` — follow existing folder conventions (`Logic/`, `Components/`)
- `DraftWorkspace.razor` internal static methods (`IsDraftIntentPrompt`, `ExtractDraftMarkdown`, `IsClosingLine`) remain `internal static` so tests can call them directly
- Run all tests with: `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ -v minimal`
- CSS goes in `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css`

---

## File Map

| Action | File | Responsibility |
|---|---|---|
| Create | `src/HrMcp.Agent/Web/Models/PdChecklistState.cs` | 13-section checklist state, section detection from markdown, acknowledge override |
| Modify | `src/HrMcp.Agent/HrAgent.cs` | Replace `SystemPrompt` const with dual HR Specialist + Writer role |
| Modify | `src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add draft-intent terms, checklist state, export gate, series mismatch detection, JSON log |
| Create | `src/HrMcp.Agent/Components/Shared/PdSectionChecklist.razor` | Display-only checklist component |
| Create | `src/HrMcp.Agent/Components/Shared/SeriesRecommendationBanner.razor` | Display-only series mismatch banner |
| Modify | `src/HrMcp.Agent/wwwroot/css/app.css` | Styles for checklist and series banner |
| Create | `tests/HrMcp.Agent.Tests/Logic/PdChecklistStateTests.cs` | Unit tests for checklist state logic |
| Modify | `tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs` | Tests for extended draft-intent terms |

---

### Task 1: PdChecklistState Model

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdChecklistState.cs`
- Test: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdChecklistStateTests.cs`

**Interfaces:**
- Produces: `PdSectionStatus` enum, `PdSection` record, `PdChecklistState` class with:
  - `IReadOnlyList<PdSection> Sections`
  - `bool HasBlockingItems` — true when any section has `Status == Missing`
  - `void UpdateFromDraft(string draftMarkdown)` — parses headings to update section status
  - `bool Acknowledge(string sectionName)` — converts Warning → Complete, records timestamp
  - `IReadOnlyList<AcknowledgedSection> Acknowledgments` — for JSON export log

- [ ] **Step 1: Write failing tests**

Create `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdChecklistStateTests.cs`:

```csharp
using HrMcp.Agent.Web.Models;
using Xunit;

namespace HrMcp.Agent.Tests.Logic;

public sealed class PdChecklistStateTests
{
    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_AllRequiredSections_AreMissing()
    {
        var state = new PdChecklistState();
        var missing = state.Sections
            .Where(s => s.IsRequired && s.Status != PdSectionStatus.AutoFilled)
            .ToList();
        Assert.All(missing, s => Assert.Equal(PdSectionStatus.Missing, s.Status));
    }

    [Fact]
    public void InitialState_EeoAndReasonableAccommodation_AreAutoFilled()
    {
        var state = new PdChecklistState();
        var autoFilled = state.Sections.Where(s => s.IsLocked).ToList();
        Assert.Equal(2, autoFilled.Count);
        Assert.Contains(autoFilled, s => s.Name == "EEO Statement");
        Assert.Contains(autoFilled, s => s.Name == "Reasonable Accommodation");
    }

    [Fact]
    public void InitialState_HasBlockingItems_IsTrue()
    {
        var state = new PdChecklistState();
        Assert.True(state.HasBlockingItems);
    }

    // ── UpdateFromDraft ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateFromDraft_WithPositionSummaryHeading_MarksComplete()
    {
        var state = new PdChecklistState();
        state.UpdateFromDraft("## Position Summary\n\nThis position serves as...");
        var section = state.Sections.Single(s => s.Name == "Position Summary");
        Assert.Equal(PdSectionStatus.Complete, section.Status);
    }

    [Fact]
    public void UpdateFromDraft_WithDutiesHeading_MarksComplete()
    {
        var state = new PdChecklistState();
        state.UpdateFromDraft("## Major Duties\n\n- Independently leads cloud infrastructure projects.");
        var section = state.Sections.Single(s => s.Name == "Major Duties");
        Assert.Equal(PdSectionStatus.Complete, section.Status);
    }

    [Fact]
    public void UpdateFromDraft_WithSecurityClearanceHeading_MarksComplete()
    {
        var state = new PdChecklistState();
        state.UpdateFromDraft("## Security Clearance\n\nSecret clearance required.");
        var section = state.Sections.Single(s => s.Name == "Security Clearance");
        Assert.Equal(PdSectionStatus.Complete, section.Status);
    }

    [Fact]
    public void UpdateFromDraft_LockedSections_RemainAutoFilled()
    {
        var state = new PdChecklistState();
        state.UpdateFromDraft("## EEO Statement\n\nContent here.");
        var eeo = state.Sections.Single(s => s.Name == "EEO Statement");
        Assert.Equal(PdSectionStatus.AutoFilled, eeo.Status);
    }

    [Fact]
    public void UpdateFromDraft_AllSectionsPresent_HasBlockingItems_IsFalse()
    {
        const string fullDraft = """
            ## Position Title
            Cloud Architect

            ## Pay Plan / Series / Grade
            GS-2210-13

            ## Supervisory Status
            Non-Supervisory

            ## Position Summary
            This position leads cloud migration efforts.

            ## Major Duties
            - Independently leads AWS migration projects.

            ## Qualifications Required
            Expert knowledge of cloud infrastructure.

            ## Preferred Qualifications
            AWS certification preferred.

            ## Education Requirements
            Bachelor's degree required.

            ## Security Clearance
            Secret clearance required.

            ## Remote Work Eligibility
            Hybrid — duty station Washington, DC.

            ## Travel Requirements
            Occasional (up to 25%).
            """;

        var state = new PdChecklistState();
        state.UpdateFromDraft(fullDraft);
        Assert.False(state.HasBlockingItems);
    }

    // ── Acknowledge ───────────────────────────────────────────────────────────

    [Fact]
    public void Acknowledge_WarningSectionName_ReturnsTrueAndRecords()
    {
        var state = new PdChecklistState();
        // Manually force a section to Warning status for testing
        state.SetStatusForTest("Pay Plan / Series / Grade", PdSectionStatus.Warning);

        var result = state.Acknowledge("Pay Plan / Series / Grade");

        Assert.True(result);
        Assert.Equal(PdSectionStatus.Complete, state.Sections
            .Single(s => s.Name == "Pay Plan / Series / Grade").Status);
        Assert.Single(state.Acknowledgments);
        Assert.Equal("Pay Plan / Series / Grade", state.Acknowledgments[0].SectionName);
        Assert.NotNull(state.Acknowledgments[0].AcknowledgedAt);
    }

    [Fact]
    public void Acknowledge_MissingSectionName_ReturnsFalse()
    {
        var state = new PdChecklistState();
        var result = state.Acknowledge("Position Summary");
        Assert.False(result); // Can only acknowledge Warning, not Missing
    }

    [Fact]
    public void Acknowledge_LockedSection_ReturnsFalse()
    {
        var state = new PdChecklistState();
        var result = state.Acknowledge("EEO Statement");
        Assert.False(result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ -v minimal --filter "PdChecklistStateTests"
```

Expected: FAIL — `PdChecklistState` type not found.

- [ ] **Step 3: Create the model**

Create `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdChecklistState.cs`:

```csharp
namespace HrMcp.Agent.Web.Models;

public enum PdSectionStatus { Missing, Warning, Complete, AutoFilled }

public sealed record PdSection(string Name, bool IsRequired, bool IsLocked)
{
    public PdSectionStatus Status { get; internal set; } =
        IsLocked ? PdSectionStatus.AutoFilled : PdSectionStatus.Missing;
}

public sealed record AcknowledgedSection(string SectionName, DateTimeOffset AcknowledgedAt);

public sealed class PdChecklistState
{
    // The 13 required agency template sections in display order.
    private readonly List<PdSection> _sections =
    [
        new("Position Title",              IsRequired: true,  IsLocked: false),
        new("Pay Plan / Series / Grade",   IsRequired: true,  IsLocked: false),
        new("Supervisory Status",          IsRequired: true,  IsLocked: false),
        new("Position Summary",            IsRequired: true,  IsLocked: false),
        new("Major Duties",                IsRequired: true,  IsLocked: false),
        new("Qualifications Required",     IsRequired: true,  IsLocked: false),
        new("Preferred Qualifications",    IsRequired: false, IsLocked: false),
        new("Education Requirements",      IsRequired: true,  IsLocked: false),
        new("Security Clearance",          IsRequired: true,  IsLocked: false),
        new("Remote Work Eligibility",     IsRequired: true,  IsLocked: false),
        new("Travel Requirements",         IsRequired: true,  IsLocked: false),
        new("EEO Statement",               IsRequired: true,  IsLocked: true),
        new("Reasonable Accommodation",    IsRequired: true,  IsLocked: true),
    ];

    private readonly List<AcknowledgedSection> _acknowledgments = [];

    public IReadOnlyList<PdSection> Sections => _sections;
    public IReadOnlyList<AcknowledgedSection> Acknowledgments => _acknowledgments;

    // Export is blocked when any required, non-locked section is still Missing.
    public bool HasBlockingItems =>
        _sections.Any(s => s.IsRequired && !s.IsLocked && s.Status == PdSectionStatus.Missing);

    // Parses the markdown draft headings and marks detected sections as Complete.
    // Locked sections always stay AutoFilled regardless of draft content.
    public void UpdateFromDraft(string draftMarkdown)
    {
        if (string.IsNullOrWhiteSpace(draftMarkdown))
            return;

        var headings = draftMarkdown
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith('#'))
            .Select(l => l.TrimStart('#').Trim().ToLowerInvariant())
            .ToList();

        foreach (var section in _sections)
        {
            if (section.IsLocked) continue;

            var keywords = SectionKeywords(section.Name);
            if (headings.Any(h => keywords.Any(k => h.Contains(k, StringComparison.Ordinal))))
                section.Status = PdSectionStatus.Complete;
        }
    }

    // Converts a Warning section to Complete and records the acknowledgment timestamp.
    // Returns false if the section does not exist, is not in Warning status, or is locked.
    public bool Acknowledge(string sectionName)
    {
        var section = _sections.FirstOrDefault(s => s.Name == sectionName);
        if (section is null || section.IsLocked || section.Status != PdSectionStatus.Warning)
            return false;

        section.Status = PdSectionStatus.Complete;
        _acknowledgments.Add(new AcknowledgedSection(sectionName, DateTimeOffset.UtcNow));
        return true;
    }

    // Test-only helper — allows tests to set a specific status without going through UpdateFromDraft.
    internal void SetStatusForTest(string sectionName, PdSectionStatus status)
    {
        var section = _sections.FirstOrDefault(s => s.Name == sectionName);
        if (section is not null && !section.IsLocked)
            section.Status = status;
    }

    private static string[] SectionKeywords(string sectionName) => sectionName switch
    {
        "Position Title"            => ["position title", "job title", "title"],
        "Pay Plan / Series / Grade" => ["pay plan", "series", "grade", "classification"],
        "Supervisory Status"        => ["supervisory", "supervision"],
        "Position Summary"          => ["summary", "position summary", "overview"],
        "Major Duties"              => ["duties", "responsibilities", "major duties"],
        "Qualifications Required"   => ["qualifications required", "required qualifications", "minimum qualifications"],
        "Preferred Qualifications"  => ["preferred qualifications", "desired qualifications", "preferred"],
        "Education Requirements"    => ["education", "degree", "academic"],
        "Security Clearance"        => ["security clearance", "clearance", "secret", "top secret"],
        "Remote Work Eligibility"   => ["remote", "telework", "hybrid", "on-site", "duty station"],
        "Travel Requirements"       => ["travel"],
        _                           => [sectionName.ToLowerInvariant()]
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ -v minimal --filter "PdChecklistStateTests"
```

Expected: All 12 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdChecklistState.cs \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdChecklistStateTests.cs
git commit -m "feat: add PdChecklistState model for 13-section agency template tracking"
```

---

### Task 2: Update AI System Prompt

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs` (lines 12–33, the `SystemPrompt` const)

**Interfaces:**
- Consumes: nothing new
- Produces: updated `SystemPrompt` const used by `ResetHistory` and `_history` initialization

- [ ] **Step 1: Replace the SystemPrompt constant**

In `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs`, replace the `SystemPrompt` const (lines 12–33) with:

```csharp
    private const string SystemPrompt = """
        You are an expert federal HR Specialist and Position Description writer for a U.S. federal agency.
        You serve two simultaneous roles:
        1. HR Specialist — you know OPM occupational series and classification standards, GS grade-level
           descriptors, agency-required PD template sections, and federal HR compliance rules.
        2. Writer — you translate the hiring manager's plain-language technical knowledge into compliant
           federal HR prose using active voice, measurable duties, and OPM qualification standards.

        Your job is to help the hiring manager draft a fully compliant Position Description (PD).

        Input modes — detect automatically from the first message:
        - Freeform description ("I need a GS-13 cloud architect"): extract intent, draft immediately, flag gaps.
        - Pasted notes or old PD: clean up language, apply agency template, flag non-compliant sections.
        - Incomplete context: ask one targeted question ("What grade level are you targeting?"), draft when ready.

        When drafting or updating a PD, always output the draft using these section headings in order:
        ## Position Title
        ## Pay Plan / Series / Grade
        ## Supervisory Status
        ## Position Summary
        ## Major Duties
        ## Qualifications Required
        ## Preferred Qualifications
        ## Education Requirements
        ## Security Clearance
        ## Remote Work Eligibility
        ## Travel Requirements
        ## EEO Statement
        ## Reasonable Accommodation

        Grade-level duty language calibration:
        - GS-05 to GS-07: "Assists with...", "Performs routine...", "Under supervision..."
        - GS-09 to GS-11: "Applies knowledge of...", "Analyzes...", "Develops..."
        - GS-12 to GS-13: "Independently leads...", "Serves as technical expert...", "Designs and implements..."
        - GS-14 to GS-15: "Provides authoritative guidance...", "Establishes policy...", "Represents the agency..."

        Always include at least 5 Major Duties. Each duty must start with an action verb calibrated to the GS grade.
        Qualifications Required must cite OPM minimum qualifications for the series.
        EEO Statement: "This agency is an Equal Opportunity Employer. All qualified applicants will receive
        consideration without regard to race, color, religion, sex, national origin, disability, or veteran status."
        Reasonable Accommodation: "Persons with disabilities who require alternative means for communication of
        program information (Braille, large print, audiotape, etc.) should contact this agency."

        If the described duties suggest a different OPM series than requested, flag it explicitly:
        "Series Suggestion: The duties you described align more closely with GS-XXXX (Series Name) than GS-YYYY."

        After drafting, ask one follow-up question about the highest-priority missing or unclear section.
        Never present a numbered menu of options or ask what the manager wants to do next.

        Tool guidance:
        - Always call GetHiringOrganizations before GetPositionsByOrganization.
        - Use GetOpenPositions for overview; GetPositionById for full detail.
        - To export to Word, call ExportDraftToWord(positionId, draftContent).
        - To export all positions to Excel, call ExportPositionsToExcel().
        - Format pay ranges as "$85,000 – $110,000 per year".
        """;
```

- [ ] **Step 2: Run the full test suite to confirm nothing regressed**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ -v minimal
```

Expected: All existing tests PASS. (The system prompt is a string constant — no unit tests cover it directly.)

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs
git commit -m "feat: replace system prompt with dual HR Specialist + Writer role for PD builder"
```

---

### Task 3: Extend Draft-Intent Detection

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` (`DraftIntentTerms` array, lines 183–200)
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`

**Interfaces:**
- Consumes: existing `IsDraftIntentPrompt(string prompt)` internal static method
- Produces: extended `DraftIntentTerms` array that triggers draft panel for position/role/series/grade inputs

- [ ] **Step 1: Add tests for the new terms**

Append to `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`:

```csharp
    // New terms for extended draft-intent detection
    [Theory]
    [InlineData("I need a GS-13 cloud architect")]
    [InlineData("create a position for a software engineer")]
    [InlineData("we're hiring a data scientist at grade 12")]
    [InlineData("the title should be IT Specialist")]
    [InlineData("the series is 2210")]
    [InlineData("duties include leading the cloud migration")]
    [InlineData("clearance is required for this role")]
    [InlineData("the position is remote eligible")]
    [InlineData("education requirement is a bachelor's degree")]
    [InlineData("this will be a supervisory position")]
    public void IsDraftIntentPrompt_ExtendedTerms_ReturnsTrue(string prompt) =>
        Assert.True(DraftWorkspace.IsDraftIntentPrompt(prompt));
```

- [ ] **Step 2: Run to verify new tests fail**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ -v minimal --filter "IsDraftIntentPrompt_ExtendedTerms"
```

Expected: FAIL for several inputs (new terms not in `DraftIntentTerms` yet).

- [ ] **Step 3: Extend DraftIntentTerms in DraftWorkspace.razor**

In `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`, replace the `DraftIntentTerms` array (currently lines 183–200) with:

```csharp
    private static readonly string[] DraftIntentTerms =
    [
        "draft",
        "job description",
        "jd",
        "pd",
        "position description",
        "rewrite",
        "revise",
        "refine",
        "improve",
        "edit",
        "add",
        "include",
        "qualification",
        "qualifications",
        "requirement",
        "requirements",
        // Extended terms for freeform / paste / Q&A input modes
        "position",
        "create a",
        "we're hiring",
        "we are hiring",
        "need a",
        "i need a",
        "title",
        "series",
        "grade",
        "duties",
        "clearance",
        "remote",
        "telework",
        "education",
        "supervisory",
        "gs-",
    ];
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ -v minimal --filter "DraftIntentTests"
```

Expected: All tests in `DraftIntentTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs
git commit -m "feat: extend draft-intent detection for freeform, Q&A, and paste input modes"
```

---

### Task 4: PdSectionChecklist.razor Component

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdSectionChecklist.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css` (append styles)

**Interfaces:**
- Consumes: `PdChecklistState` (Task 1), `PdSection`, `PdSectionStatus`, `AcknowledgedSection`
- Produces: `<PdSectionChecklist>` component with parameters:
  - `[Parameter] PdChecklistState State`
  - `[Parameter] EventCallback<string> OnSectionClicked` — fires section name when ❌/⚠️ clicked
  - `[Parameter] EventCallback<string> OnAcknowledge` — fires section name when "Acknowledge" clicked

- [ ] **Step 1: Create the component**

Create `DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdSectionChecklist.razor`:

```razor
@using HrMcp.Agent.Web.Models

<div class="pd-checklist">
    <div class="pd-checklist-header">Section Checklist</div>
    <ul class="pd-checklist-list">
        @foreach (var section in State.Sections)
        {
            var icon = section.Status switch
            {
                PdSectionStatus.Complete   => "✅",
                PdSectionStatus.Warning    => "⚠️",
                PdSectionStatus.AutoFilled => "🔒",
                _                          => "❌"
            };
            var cssClass = section.Status switch
            {
                PdSectionStatus.Complete   => "pd-checklist-item--complete",
                PdSectionStatus.Warning    => "pd-checklist-item--warning",
                PdSectionStatus.AutoFilled => "pd-checklist-item--locked",
                _                          => "pd-checklist-item--missing"
            };
            var isClickable = section.Status is PdSectionStatus.Missing or PdSectionStatus.Warning;

            <li class="pd-checklist-item @cssClass">
                <span class="pd-checklist-icon">@icon</span>
                @if (isClickable)
                {
                    <button class="pd-checklist-name pd-checklist-name--link"
                            @onclick="() => OnSectionClicked.InvokeAsync(section.Name)">
                        @section.Name
                    </button>
                }
                else
                {
                    <span class="pd-checklist-name">@section.Name</span>
                }
                @if (section.Status == PdSectionStatus.Warning && !section.IsLocked)
                {
                    <button class="pd-checklist-ack ghost-btn"
                            @onclick="() => OnAcknowledge.InvokeAsync(section.Name)">
                        Acknowledge
                    </button>
                }
            </li>
        }
    </ul>
</div>

@code {
    [Parameter, EditorRequired] public PdChecklistState State { get; set; } = default!;
    [Parameter] public EventCallback<string> OnSectionClicked { get; set; }
    [Parameter] public EventCallback<string> OnAcknowledge { get; set; }
}
```

- [ ] **Step 2: Add CSS styles**

Append to `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css`:

```css
/* ── PD Section Checklist ───────────────────────────────────────────────────── */
.pd-checklist {
    border: 1px solid var(--border-color, #e0e0e0);
    border-radius: 6px;
    margin-bottom: 12px;
    font-size: 0.82rem;
}

.pd-checklist-header {
    font-weight: 600;
    padding: 6px 10px;
    border-bottom: 1px solid var(--border-color, #e0e0e0);
    background: var(--surface-alt, #f8f8f8);
    border-radius: 6px 6px 0 0;
}

.pd-checklist-list {
    list-style: none;
    margin: 0;
    padding: 4px 0;
}

.pd-checklist-item {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 3px 10px;
}

.pd-checklist-icon {
    flex-shrink: 0;
    width: 18px;
    text-align: center;
}

.pd-checklist-name {
    flex: 1;
}

.pd-checklist-name--link {
    background: none;
    border: none;
    padding: 0;
    color: var(--accent, #2563eb);
    cursor: pointer;
    text-align: left;
    font-size: inherit;
    text-decoration: underline;
}

.pd-checklist-name--link:hover {
    opacity: 0.75;
}

.pd-checklist-ack {
    font-size: 0.75rem;
    padding: 1px 6px;
}

.pd-checklist-item--missing .pd-checklist-name { color: var(--text-muted, #6b7280); }
.pd-checklist-item--warning .pd-checklist-name { color: var(--warning, #d97706); }
.pd-checklist-item--complete .pd-checklist-name { color: var(--text, inherit); }
.pd-checklist-item--locked .pd-checklist-name { color: var(--text-muted, #6b7280); font-style: italic; }

/* ── Series Recommendation Banner ──────────────────────────────────────────── */
.series-banner {
    border: 1px solid var(--info-border, #93c5fd);
    background: var(--info-bg, #eff6ff);
    border-radius: 6px;
    padding: 10px 14px;
    margin: 8px 0;
    font-size: 0.85rem;
}

.series-banner-title {
    font-weight: 600;
    margin-bottom: 4px;
}

.series-banner-body {
    margin-bottom: 8px;
    color: var(--text, inherit);
}

.series-banner-actions {
    display: flex;
    gap: 8px;
}
```

- [ ] **Step 3: Run existing tests to confirm nothing broken**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ -v minimal
```

Expected: All tests PASS (new component has no logic to break existing tests).

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdSectionChecklist.razor \
        DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css
git commit -m "feat: add PdSectionChecklist.razor component and CSS styles"
```

---

### Task 5: SeriesRecommendationBanner.razor Component

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/SeriesRecommendationBanner.razor`

**Interfaces:**
- Produces: `<SeriesRecommendationBanner>` with parameters:
  - `[Parameter] string? SuggestedSeries` — e.g. `"GS-0343"`
  - `[Parameter] string? SuggestedSeriesName` — e.g. `"Management & Program Analysis"`
  - `[Parameter] string? CurrentSeries` — e.g. `"GS-2210"`
  - `[Parameter] EventCallback OnKeep` — user keeps current series
  - `[Parameter] EventCallback OnSwitch` — user accepts suggested series

- [ ] **Step 1: Create the component**

Create `DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/SeriesRecommendationBanner.razor`:

```razor
@if (!string.IsNullOrWhiteSpace(SuggestedSeries))
{
    <div class="series-banner" role="alert">
        <div class="series-banner-title">ℹ️ Series Suggestion</div>
        <div class="series-banner-body">
            The duties described align more closely with
            <strong>@SuggestedSeries (@SuggestedSeriesName)</strong>
            than <strong>@CurrentSeries</strong>.
        </div>
        <div class="series-banner-actions">
            <button class="ghost-btn" @onclick="OnKeep">Keep @CurrentSeries</button>
            <button class="primary-btn primary-btn--compact" @onclick="OnSwitch">
                Switch to @SuggestedSeries
            </button>
        </div>
    </div>
}

@code {
    [Parameter] public string? SuggestedSeries { get; set; }
    [Parameter] public string? SuggestedSeriesName { get; set; }
    [Parameter] public string? CurrentSeries { get; set; }
    [Parameter] public EventCallback OnKeep { get; set; }
    [Parameter] public EventCallback OnSwitch { get; set; }
}
```

- [ ] **Step 2: Add a static series-mismatch parser to DraftWorkspace.razor**

The parser detects the AI's series suggestion phrase in a response. Add this `internal static` method to the `@code` block in `DraftWorkspace.razor` (before the closing `}`):

```csharp
    // Detects series suggestion phrases produced by the updated system prompt.
    // Expected AI format: "Series Suggestion: ... GS-XXXX (Name) ... GS-YYYY"
    // Returns (suggestedSeries, suggestedName, currentSeries) or null if not detected.
    internal static (string Suggested, string SuggestedName, string Current)? TryExtractSeriesSuggestion(string aiResponse)
    {
        if (string.IsNullOrWhiteSpace(aiResponse)) return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            aiResponse,
            @"Series Suggestion.*?GS-(\d{4})\s*\(([^)]+)\).*?GS-(\d{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (!match.Success) return null;

        return ($"GS-{match.Groups[1].Value}", match.Groups[2].Value.Trim(), $"GS-{match.Groups[3].Value}");
    }
```

- [ ] **Step 3: Add unit tests for TryExtractSeriesSuggestion**

Append to `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`:

```csharp
    [Fact]
    public void TryExtractSeriesSuggestion_WithSeriesSuggestionPhrase_ReturnsParsedValues()
    {
        const string response = "Series Suggestion: The duties align more closely with GS-0343 (Management & Program Analysis) than GS-2210.";
        var result = DraftWorkspace.TryExtractSeriesSuggestion(response);
        Assert.NotNull(result);
        Assert.Equal("GS-0343", result!.Value.Suggested);
        Assert.Equal("Management & Program Analysis", result.Value.SuggestedName);
        Assert.Equal("GS-2210", result.Value.Current);
    }

    [Fact]
    public void TryExtractSeriesSuggestion_WithoutPhrase_ReturnsNull()
    {
        const string response = "Here is your draft. ## Position Summary\n\nThis position...";
        var result = DraftWorkspace.TryExtractSeriesSuggestion(response);
        Assert.Null(result);
    }
```

- [ ] **Step 4: Run tests**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ -v minimal
```

Expected: All tests PASS.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/SeriesRecommendationBanner.razor \
        DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs
git commit -m "feat: add SeriesRecommendationBanner component and series mismatch detection"
```

---

### Task 6: Wire Checklist and Export Gate into DraftWorkspace.razor

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: `PdChecklistState` (Task 1), `PdSectionChecklist` (Task 4), `SeriesRecommendationBanner` (Task 5), `TryExtractSeriesSuggestion` (Task 5)
- Produces: fully wired workspace with live checklist, export gate, acknowledge override, JSON log

This task modifies `DraftWorkspace.razor` in several coordinated steps. Read the full file before starting.

- [ ] **Step 1: Add @using and field declarations**

In `DraftWorkspace.razor`, add the `@using` directive for the new Shared components at the top of the `@using` block:

```razor
@using HrMcp.Agent.Components.Shared
```

In the `@code` block, add these new fields after the existing `_hasDraft` field (around line 177):

```csharp
    private PdChecklistState _checklistState = new();
    private (string Suggested, string SuggestedName, string Current)? _seriesSuggestion;
```

- [ ] **Step 2: Update SendPromptAsync to populate checklist and series suggestion**

Inside `SendPromptAsync`, after the existing `if (ShouldSyncDraft(input, response))` block (around line 362), add:

```csharp
            // Update checklist from the new draft markdown
            if (_currentDraftMarkdown is not null)
                _checklistState.UpdateFromDraft(_currentDraftMarkdown);

            // Detect series suggestion from AI response
            _seriesSuggestion = TryExtractSeriesSuggestion(response);
```

- [ ] **Step 3: Add checklist to the right panel markup**

In the `<section class="right-editor">` block, insert `<PdSectionChecklist>` immediately after the `<div class="right-header">` closing tag (before the `@if (_exportBusy)` block):

```razor
            <PdSectionChecklist State="_checklistState"
                                OnSectionClicked="HandleChecklistSectionClicked"
                                OnAcknowledge="HandleChecklistAcknowledge" />
```

- [ ] **Step 4: Add the series banner to the chat thread**

In the chat thread `@foreach (var turn in _turns)` loop, after the closing `</div>` of each assistant bubble, add a conditional banner render. Replace the `@foreach` block in the chat thread with:

```razor
                    @foreach (var turn in _turns)
                    {
                        var isUser = string.Equals(turn.Role, "You", StringComparison.OrdinalIgnoreCase);
                        <div class="chat-bubble-row @(isUser ? "chat-bubble-row--user" : "chat-bubble-row--assistant")">
                            <div class="chat-bubble">
                                @if (isUser)
                                {
                                    <span>@turn.Text</span>
                                }
                                else
                                {
                                    <div class="chat-bubble-content">@RenderAssistantMarkdown(turn.Text)</div>
                                }
                            </div>
                        </div>
                        @if (!isUser && turn == _turns[^1] && _seriesSuggestion.HasValue)
                        {
                            <SeriesRecommendationBanner
                                SuggestedSeries="@_seriesSuggestion.Value.Suggested"
                                SuggestedSeriesName="@_seriesSuggestion.Value.SuggestedName"
                                CurrentSeries="@_seriesSuggestion.Value.Current"
                                OnKeep="DismissSeriesSuggestion"
                                OnSwitch="AcceptSeriesSuggestion" />
                        }
                    }
```

- [ ] **Step 5: Gate Word export on checklist blocking items**

In `ExportWordAsync`, add a checklist check after the empty-content check (after line 408):

```csharp
        if (_checklistState.HasBlockingItems)
        {
            var missing = _checklistState.Sections
                .Where(s => s.IsRequired && !s.IsLocked && s.Status == PdSectionStatus.Missing)
                .Select(s => s.Name);
            _status = $"Complete all required sections before exporting: {string.Join(", ", missing)}.";
            return;
        }
```

- [ ] **Step 6: Add acknowledgment to JSON export**

In `ExportJsonAsync`, update the anonymous export object to include acknowledgments:

```csharp
        var export = new
        {
            exportedAt = DateTimeOffset.UtcNow,
            draft = _currentDraftMarkdown,
            acknowledgments = _checklistState.Acknowledgments
                .Select(a => new { a.SectionName, a.AcknowledgedAt }),
            turns = _turns.Select(t => new { t.Role, t.Text, t.Timestamp })
        };
```

- [ ] **Step 7: Add the event handlers to the @code block**

Append these methods to the `@code` block in `DraftWorkspace.razor`:

```csharp
    private async Task HandleChecklistSectionClicked(string sectionName)
    {
        // Scroll Quill editor to the section heading via JS interop
        await JS.InvokeVoidAsync("scrollQuillToHeading", "quill-editor-wrapper", sectionName);
    }

    private void HandleChecklistAcknowledge(string sectionName)
    {
        _checklistState.Acknowledge(sectionName);
    }

    private void DismissSeriesSuggestion()
    {
        _seriesSuggestion = null;
    }

    private void AcceptSeriesSuggestion()
    {
        // Dismiss the banner; the manager's next prompt will reference the new series.
        // The AI will update the draft on the next exchange.
        _seriesSuggestion = null;
    }
```

- [ ] **Step 8: Add scrollQuillToHeading JS function**

Find the existing JS file that contains `loadQuillWhenReady` and `downloadFile` (likely `wwwroot/js/app.js` or inline in `App.razor`). Add:

```javascript
window.scrollQuillToHeading = function (wrapperId, headingText) {
    const wrapper = document.getElementById(wrapperId);
    if (!wrapper) return;
    const editor = wrapper.querySelector('.ql-editor');
    if (!editor) return;
    const headings = editor.querySelectorAll('h1, h2, h3, h4');
    for (const h of headings) {
        if (h.textContent.trim().toLowerCase().includes(headingText.toLowerCase())) {
            h.scrollIntoView({ behavior: 'smooth', block: 'start' });
            return;
        }
    }
};
```

- [ ] **Step 9: Run the full test suite**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ -v minimal
```

Expected: All tests PASS.

- [ ] **Step 10: Build to verify no compile errors**

```
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 11: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
git commit -m "feat: wire PdSectionChecklist, export gate, acknowledge override, and series banner into DraftWorkspace"
```

---

### Task 7: Find and Update JS File for scrollQuillToHeading

> **Note:** Task 6 Step 8 references the JS file. This task locates it first.

**Files:**
- Modify: whichever file contains `loadQuillWhenReady` — find with:
  `grep -r "loadQuillWhenReady" DotnetAiAgentUi/src/HrMcp.Agent/`

**Interfaces:**
- Produces: `window.scrollQuillToHeading` available to `IJSRuntime` in `DraftWorkspace.razor`

- [ ] **Step 1: Locate the JS file**

```
grep -r "loadQuillWhenReady" DotnetAiAgentUi/src/HrMcp.Agent/
```

This will print the file path. Open that file.

- [ ] **Step 2: Add scrollQuillToHeading after the existing functions**

Append to the JS file found in Step 1 (do not modify existing functions):

```javascript
window.scrollQuillToHeading = function (wrapperId, headingText) {
    const wrapper = document.getElementById(wrapperId);
    if (!wrapper) return;
    const editor = wrapper.querySelector('.ql-editor');
    if (!editor) return;
    const headings = editor.querySelectorAll('h1, h2, h3, h4');
    for (const h of headings) {
        if (h.textContent.trim().toLowerCase().includes(headingText.toLowerCase())) {
            h.scrollIntoView({ behavior: 'smooth', block: 'start' });
            return;
        }
    }
};
```

- [ ] **Step 3: Build and run the app to smoke-test**

```
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
dotnet run --project DotnetAiAgentUi/src/HrMcp.Agent -- --web
```

Navigate to `http://localhost:5000`. Log in, type "I need a GS-13 cloud architect to lead our AWS migration", send. Verify:
- Draft panel opens
- Section checklist appears above the Quill editor
- ❌ sections are red/grey; ✅ EEO and Reasonable Accommodation show 🔒
- Export button does NOT trigger download (checklist has ❌ items → status message appears)

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/  # the JS file
git commit -m "feat: add scrollQuillToHeading JS helper for checklist section navigation"
```

---

## Self-Review

### Spec Coverage

| Spec Requirement | Task |
|---|---|
| Section Completion Checklist (13 sections, ✅/⚠️/❌/🔒) | Task 1, Task 4 |
| Dual-role AI system prompt (HR Specialist + Writer) | Task 2 |
| Three input modes auto-detected | Task 2 (system prompt), Task 3 (draft-intent terms) |
| GS grade-level language calibration | Task 2 (system prompt) |
| Series Recommendation Banner | Task 5 |
| Checklist updates in real time after AI response | Task 6 Step 2 |
| Clicking ❌/⚠️ scrolls Quill to section | Task 6 Step 7, Task 7 |
| Export gate blocks Word export on ❌ items | Task 6 Step 5 |
| ⚠️ Acknowledge override converts to ✅ | Task 1 (model), Task 6 Steps 3, 7 |
| Acknowledge logged to JSON export | Task 6 Step 6 |
| EEO + Reasonable Accommodation auto-filled and locked | Task 1 |
| Series suggestion parsed from AI response | Task 5 |

All spec requirements covered. No gaps found.

### Placeholder Scan

No TBDs, TODOs, or incomplete steps. Every step contains actual code or commands.

### Type Consistency

- `PdSectionStatus` — defined in Task 1, used in Task 4, Task 6 ✅
- `PdChecklistState` — defined in Task 1, used in Task 4 (parameter type), Task 6 (field type) ✅
- `TryExtractSeriesSuggestion` — defined as `internal static` in Task 5, called in Task 6 Step 2 ✅
- `PdSection.IsLocked` — defined in Task 1 record, referenced in Task 4 Acknowledge guard ✅
- `AcknowledgedSection` — defined in Task 1, projected in Task 6 Step 6 JSON export ✅
