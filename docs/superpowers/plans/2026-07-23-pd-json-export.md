# PD JSON Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current `ExportJsonAsync` (which exports raw chat history + raw markdown) with a structured JSON export that parses the draft panel markdown into 12 canonical PD sections — no chat history included.

**Architecture:** A new static `PdDraftParser` class parses `_currentDraftMarkdown` by splitting on `## ` headings. `PdDraftExport` is a typed C# model hierarchy serialized via `System.Text.Json` polymorphic support. `ExportJsonAsync` in `DraftWorkspace.razor` is replaced to call the parser and download the result as `pd-draft-<slug>.json`.

**Tech Stack:** .NET 10, Blazor Server, xUnit, System.Text.Json (already in use)

## Global Constraints

- .NET 10, Blazor Server, xUnit
- No new NuGet packages
- `nullable enable` and top-level namespace declarations on all new C# files
- `System.Text.Json` for serialization (already used — no Newtonsoft)
- `[JsonPolymorphic]` / `[JsonDerivedType]` attributes require .NET 7+ — available in .NET 10 ✅
- **`PdSection` is already defined in `HrMcp.Agent.Web.Models` (in `PdChecklistState.cs`) as a record for checklist sections** — the export model's abstract base class MUST be named `PdExportSection` (not `PdSection`) to avoid a compile-time conflict
- All existing 141 tests must continue to pass
- Commit prefix: `feat:`
- PR target branch: `develop` (never `master` directly from a feature branch)

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftExport.cs` | C# model classes for the structured export |
| Create | `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftParser.cs` | Static parser: markdown → `PdDraftExport` |
| Modify | `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Replace `ExportJsonAsync` body; add `SlugifyTitle` helper |
| Create | `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdDraftParserTests.cs` | 16 xUnit tests for `PdDraftParser` |

---

### Task 1: Model Classes, Parser, and Tests (TDD)

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftExport.cs`
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftParser.cs`
- Create: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdDraftParserTests.cs`

**Interfaces:**
- Produces: `PdDraftParser.Parse(string markdown, List<string> acknowledgments, DateTimeOffset exportedAt) → PdDraftExport`
- Produces: `DraftWorkspace.SlugifyTitle(string title) → string` (static helper — Task 2 uses it)
- Consumed by: Task 2 (`DraftWorkspace.razor` calls `PdDraftParser.Parse`)

---

- [ ] **Step 1: Write the failing tests**

Create `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdDraftParserTests.cs`:

```csharp
using HrMcp.Agent.Web.Models;
using System.Text.Json;

namespace HrMcp.Agent.Tests.Logic;

public class PdDraftParserTests
{
    // ── Shared sample markdown ────────────────────────────────────────────────

    private const string FullDraft = """
        # IT Specialist

        ## Pay Plan / Series / Grade
        Pay Plan: GS
        Series: 2210 – IT Management
        Grade: GS-12–GS-13

        ## Supervisory Status
        Non-Supervisory

        ## Position Summary
        Serves as an IT Specialist responsible for planning and coordinating agency IT systems.

        ## Major Duties
        - Plans and coordinates IT infrastructure improvements across the agency.
        - Serves as technical expert on cloud migration strategy.
        - Develops and maintains system security documentation.
        - Analyzes user requirements and translates them into technical specifications.
        - Provides authoritative guidance on IT acquisition standards.

        ## Qualifications Required
        Specialized Experience: One year of specialized experience equivalent to GS-11 performing IT systems administration.

        Time-in-Grade: Must have served 52 weeks at the GS-11 level or equivalent.

        Knowledge, Skills, and Abilities:
        - Knowledge of federal IT security frameworks (FISMA, NIST).
        - Ability to communicate complex technical concepts to non-technical audiences.
        - Skill in cloud architecture and migration planning.

        ## Preferred Qualifications
        Experience with AWS GovCloud or Azure Government environments.

        ## Education Requirements
        Bachelor's degree in Computer Science or related field.

        ## Security Clearance
        Public Trust — Moderate Risk (MBI)

        ## Remote Work Eligibility
        Remote work eligible per agency policy.

        ## Travel Requirements
        Occasional travel — up to 10% of the time.

        ## EEO Statement
        This agency is an Equal Opportunity Employer. All qualified applicants will receive consideration without regard to race, color, religion, sex, national origin, disability, or veteran status.

        ## Reasonable Accommodation
        Persons with disabilities who require alternative means for communication of program information (Braille, large print, audiotape, etc.) should contact this agency.
        """;

    private static readonly List<string> SampleAcknowledgments = ["Major Duties", "Qualifications Required"];
    private static readonly DateTimeOffset SampleDate = new(2026, 7, 23, 14, 32, 0, TimeSpan.Zero);

    // ── Parse_FullDraft_ReturnsAllSections ───────────────────────────────────

    [Fact]
    public void Parse_FullDraft_ReturnsAllSections()
    {
        var result = PdDraftParser.Parse(FullDraft, SampleAcknowledgments, SampleDate);

        Assert.Equal(10, result.Sections.Count);
    }

    // ── Parse_FullDraft_PositionInfoHasCorrectFields ─────────────────────────

    [Fact]
    public void Parse_FullDraft_PositionInfoHasCorrectFields()
    {
        var result = PdDraftParser.Parse(FullDraft, SampleAcknowledgments, SampleDate);

        Assert.Equal("IT Specialist", result.PositionInfo.Title);
        Assert.Equal("GS", result.PositionInfo.PayPlan);
        Assert.Equal("2210", result.PositionInfo.Series);
        Assert.Equal("IT Management", result.PositionInfo.SeriesTitle);
        Assert.Equal("GS-12", result.PositionInfo.GradeMin);
        Assert.Equal("GS-13", result.PositionInfo.GradeMax);
        Assert.Equal("Non-Supervisory", result.PositionInfo.SupervisoryStatus);
    }

    // ── Parse_FullDraft_MajorDutiesIsArray ───────────────────────────────────

    [Fact]
    public void Parse_FullDraft_MajorDutiesIsArray()
    {
        var result = PdDraftParser.Parse(FullDraft, SampleAcknowledgments, SampleDate);

        var duties = result.Sections.OfType<PdListSection>()
            .FirstOrDefault(s => s.SectionName == "Major Duties");
        Assert.NotNull(duties);
        Assert.Equal(5, duties.Items.Count);
        Assert.Contains("Plans and coordinates IT infrastructure improvements across the agency.", duties.Items);
    }

    // ── Parse_FullDraft_QualificationsSubFieldsExtracted ────────────────────

    [Fact]
    public void Parse_FullDraft_QualificationsSubFieldsExtracted()
    {
        var result = PdDraftParser.Parse(FullDraft, SampleAcknowledgments, SampleDate);

        var quals = result.Sections.OfType<PdQualificationsSection>()
            .FirstOrDefault(s => s.SectionName == "Qualifications Required");
        Assert.NotNull(quals);
        Assert.Contains("One year of specialized experience", quals.SpecializedExperience);
        Assert.Contains("52 weeks", quals.TimeInGrade);
        Assert.Equal(3, quals.KnowledgeSkillsAbilities.Count);
        Assert.Contains("Knowledge of federal IT security frameworks (FISMA, NIST).", quals.KnowledgeSkillsAbilities);
    }

    // ── Parse_MajorDuties_NumberedList_ExtractsItems ─────────────────────────

    [Fact]
    public void Parse_MajorDuties_NumberedList_ExtractsItems()
    {
        const string md = """
            # Analyst

            ## Major Duties
            1. Duty one with enough content to pass.
            2. Duty two with enough content to pass.
            3. Duty three with enough content to pass.
            """;

        var result = PdDraftParser.Parse(md, [], SampleDate);

        var duties = result.Sections.OfType<PdListSection>()
            .FirstOrDefault(s => s.SectionName == "Major Duties");
        Assert.NotNull(duties);
        Assert.Equal(3, duties.Items.Count);
        Assert.Equal("Duty one with enough content to pass.", duties.Items[0]);
    }

    // ── Parse_MajorDuties_BulletedList_ExtractsItems ─────────────────────────

    [Fact]
    public void Parse_MajorDuties_BulletedList_ExtractsItems()
    {
        const string md = """
            # Analyst

            ## Major Duties
            - First duty item with enough content.
            - Second duty item with enough content.
            """;

        var result = PdDraftParser.Parse(md, [], SampleDate);

        var duties = result.Sections.OfType<PdListSection>()
            .FirstOrDefault(s => s.SectionName == "Major Duties");
        Assert.NotNull(duties);
        Assert.Equal(2, duties.Items.Count);
        Assert.Equal("First duty item with enough content.", duties.Items[0]);
    }

    // ── Parse_Qualifications_NoSubHeaders_PutsAllInOther ────────────────────

    [Fact]
    public void Parse_Qualifications_NoSubHeaders_PutsAllInOther()
    {
        const string md = """
            # Analyst

            ## Qualifications Required
            Must have a college degree and general work experience.
            """;

        var result = PdDraftParser.Parse(md, [], SampleDate);

        var quals = result.Sections.OfType<PdQualificationsSection>()
            .FirstOrDefault(s => s.SectionName == "Qualifications Required");
        Assert.NotNull(quals);
        Assert.Equal(string.Empty, quals.SpecializedExperience);
        Assert.Equal(string.Empty, quals.TimeInGrade);
        Assert.Empty(quals.KnowledgeSkillsAbilities);
        Assert.Contains("Must have a college degree", quals.Other);
    }

    // ── Parse_MissingSection_OmitsFromSectionsArray ──────────────────────────

    [Fact]
    public void Parse_MissingSection_OmitsFromSectionsArray()
    {
        const string md = """
            # Analyst

            ## Position Summary
            Summary text here.

            ## Major Duties
            - Does something meaningful with enough content.
            """;

        var result = PdDraftParser.Parse(md, [], SampleDate);

        Assert.DoesNotContain(result.Sections, s => s.SectionName == "Preferred Qualifications");
        Assert.DoesNotContain(result.Sections, s => s.SectionName == "Qualifications Required");
    }

    // ── Parse_EmptyDraft_ReturnsEmptyExport ──────────────────────────────────

    [Fact]
    public void Parse_EmptyDraft_ReturnsEmptyExport()
    {
        var result = PdDraftParser.Parse(string.Empty, [], SampleDate);

        Assert.Equal(string.Empty, result.PositionInfo.Title);
        Assert.Empty(result.Sections);
        Assert.Empty(result.ChecklistAcknowledgments);
    }

    // ── Parse_GradeRange_SingleGrade_SetsMinEqualsMax ────────────────────────

    [Fact]
    public void Parse_GradeRange_SingleGrade_SetsMinEqualsMax()
    {
        const string md = """
            # Analyst

            ## Pay Plan / Series / Grade
            Grade: GS-13
            """;

        var result = PdDraftParser.Parse(md, [], SampleDate);

        Assert.Equal("GS-13", result.PositionInfo.GradeMin);
        Assert.Equal("GS-13", result.PositionInfo.GradeMax);
    }

    // ── Parse_SupervisoryStatus_Supervisory_DetectedCorrectly ────────────────

    [Fact]
    public void Parse_SupervisoryStatus_Supervisory_DetectedCorrectly()
    {
        const string md = """
            # Manager

            ## Supervisory Status
            Supervisory — First-Line Supervisor
            """;

        var result = PdDraftParser.Parse(md, [], SampleDate);

        Assert.Equal("Supervisory", result.PositionInfo.SupervisoryStatus);
    }

    // ── Parse_SupervisoryStatus_NonSupervisory_DetectedCorrectly ────────────

    [Fact]
    public void Parse_SupervisoryStatus_NonSupervisory_DetectedCorrectly()
    {
        const string md = """
            # Analyst

            ## Supervisory Status
            Non-Supervisory
            """;

        var result = PdDraftParser.Parse(md, [], SampleDate);

        Assert.Equal("Non-Supervisory", result.PositionInfo.SupervisoryStatus);
    }

    // ── Parse_ExportedAt_IsUtcIsoString ──────────────────────────────────────

    [Fact]
    public void Parse_ExportedAt_IsUtcIsoString()
    {
        var result = PdDraftParser.Parse(string.Empty, [], SampleDate);

        Assert.Equal("2026-07-23T14:32:00Z", result.ExportedAt);
    }

    // ── Parse_SchemaVersion_Is_1_0 ───────────────────────────────────────────

    [Fact]
    public void Parse_SchemaVersion_Is_1_0()
    {
        var result = PdDraftParser.Parse(string.Empty, [], SampleDate);

        Assert.Equal("1.0", result.SchemaVersion);
    }

    // ── Parse_ChecklistAcknowledgments_Roundtrips ────────────────────────────

    [Fact]
    public void Parse_ChecklistAcknowledgments_Roundtrips()
    {
        var acks = new List<string> { "Major Duties", "Qualifications Required" };
        var result = PdDraftParser.Parse(string.Empty, acks, SampleDate);

        Assert.Equal(acks, result.ChecklistAcknowledgments);
    }

    // ── SlugifyTitle_NormalTitle_ProducesSlug ────────────────────────────────

    [Fact]
    public void SlugifyTitle_NormalTitle_ProducesSlug()
    {
        Assert.Equal("it-specialist", DraftWorkspaceHelper.SlugifyTitle("IT Specialist"));
    }

    // ── SlugifyTitle_EmptyTitle_ReturnsDraft ─────────────────────────────────

    [Fact]
    public void SlugifyTitle_EmptyTitle_ReturnsDraft()
    {
        Assert.Equal("draft", DraftWorkspaceHelper.SlugifyTitle(""));
    }

    // ── Parse_SectionsInCanonicalOrder ───────────────────────────────────────

    [Fact]
    public void Parse_SectionsInCanonicalOrder()
    {
        // Draft deliberately out of canonical order
        const string md = """
            # Analyst

            ## EEO Statement
            This agency is an Equal Opportunity Employer.

            ## Position Summary
            Summary text.

            ## Major Duties
            - Does something meaningful enough.
            """;

        var result = PdDraftParser.Parse(md, [], SampleDate);

        var names = result.Sections.Select(s => s.SectionName).ToList();
        var positionSummaryIdx = names.IndexOf("Position Summary");
        var majorDutiesIdx = names.IndexOf("Major Duties");
        var eeoIdx = names.IndexOf("EEO Statement");

        Assert.True(positionSummaryIdx < majorDutiesIdx, "Position Summary must come before Major Duties");
        Assert.True(majorDutiesIdx < eeoIdx, "Major Duties must come before EEO Statement");
    }

    // ── Parse_TypeDiscriminator_PresentInJson ────────────────────────────────

    [Fact]
    public void Parse_TypeDiscriminator_PresentInJson()
    {
        const string md = """
            # Analyst

            ## Position Summary
            Summary text here for the analyst position.

            ## Major Duties
            - Does something meaningful with enough content.
            """;

        var result = PdDraftParser.Parse(md, [], SampleDate);
        var options = new JsonSerializerOptions { WriteIndented = false };
        var json = JsonSerializer.Serialize(result, options);

        Assert.Contains("\"type\":\"text\"", json);
        Assert.Contains("\"type\":\"list\"", json);
    }
}
```

> **Note on `SlugifyTitle`:** The spec defines this as a private static method on `DraftWorkspace`. Tests need to call it — extract it as `internal static` in a new `DraftWorkspaceHelper` static class in `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspaceHelper.cs` so it can be unit-tested without Blazor dependencies.  
> `DraftWorkspace.razor` delegates to `DraftWorkspaceHelper.SlugifyTitle(...)` internally.

- [ ] **Step 2: Run tests — expect compile failures (types don't exist yet)**

```bash
cd c:/apps/DotnetAiAgentUiTutorial
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "PdDraftParser" 2>&1 | tail -20
```

Expected: Build error — `PdDraftParser`, `PdDraftExport`, `PdListSection`, etc. not found.

- [ ] **Step 3: Create `DraftWorkspaceHelper.cs`**

Create `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspaceHelper.cs`:

```csharp
namespace HrMcp.Agent.Components.Pages;

internal static class DraftWorkspaceHelper
{
    internal static string SlugifyTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "draft";
        var slug = new string(title.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray()).Trim('-');
        return slug[..Math.Min(40, slug.Length)];
    }
}
```

Add `using HrMcp.Agent.Components.Pages;` to the test file (or fully qualify as `HrMcp.Agent.Components.Pages.DraftWorkspaceHelper`).

- [ ] **Step 4: Create `PdDraftExport.cs`**

Create `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftExport.cs`:

```csharp
using System.Text.Json.Serialization;

namespace HrMcp.Agent.Web.Models;

public sealed class PdDraftExport
{
    public string SchemaVersion { get; init; } = "1.0";
    public string ExportedAt { get; init; } = string.Empty;
    public PdPositionInfo PositionInfo { get; init; } = new();
    public List<PdExportSection> Sections { get; init; } = [];
    public List<string> ChecklistAcknowledgments { get; init; } = [];
}

public sealed class PdPositionInfo
{
    public string Title { get; init; } = string.Empty;
    public string PayPlan { get; init; } = string.Empty;
    public string Series { get; init; } = string.Empty;
    public string SeriesTitle { get; init; } = string.Empty;
    public string GradeMin { get; init; } = string.Empty;
    public string GradeMax { get; init; } = string.Empty;
    public string SupervisoryStatus { get; init; } = string.Empty;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PdTextSection), "text")]
[JsonDerivedType(typeof(PdListSection), "list")]
[JsonDerivedType(typeof(PdQualificationsSection), "qualifications")]
public abstract class PdExportSection
{
    public string SectionName { get; init; } = string.Empty;
}

public sealed class PdTextSection : PdExportSection
{
    public string Content { get; init; } = string.Empty;
}

public sealed class PdListSection : PdExportSection
{
    public List<string> Items { get; init; } = [];
}

public sealed class PdQualificationsSection : PdExportSection
{
    public string SpecializedExperience { get; init; } = string.Empty;
    public string TimeInGrade { get; init; } = string.Empty;
    public List<string> KnowledgeSkillsAbilities { get; init; } = [];
    public string Other { get; init; } = string.Empty;
}
```

> **Important:** The abstract class is `PdExportSection`, NOT `PdSection`. `PdSection` is already declared in `PdChecklistState.cs` in the same namespace and would cause a compile conflict.

- [ ] **Step 5: Create `PdDraftParser.cs`**

Create `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftParser.cs`:

```csharp
using System.Text.RegularExpressions;

namespace HrMcp.Agent.Web.Models;

public static class PdDraftParser
{
    // Canonical order for output sections (positionInfo fields excluded)
    private static readonly string[] CanonicalOrder =
    [
        "Position Summary",
        "Major Duties",
        "Qualifications Required",
        "Preferred Qualifications",
        "Education Requirements",
        "Security Clearance",
        "Remote Work Eligibility",
        "Travel Requirements",
        "EEO Statement",
        "Reasonable Accommodation",
    ];

    // Sections folded into positionInfo — excluded from sections array
    private static readonly HashSet<string> PositionInfoSections =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Position Title",
            "Pay Plan / Series / Grade",
            "Supervisory Status",
        };

    public static PdDraftExport Parse(
        string markdown,
        List<string> acknowledgments,
        DateTimeOffset exportedAt)
    {
        try
        {
            return ParseInternal(markdown, acknowledgments, exportedAt);
        }
        catch
        {
            return new PdDraftExport
            {
                ExportedAt = ToIso8601(exportedAt),
                ChecklistAcknowledgments = acknowledgments,
            };
        }
    }

    private static PdDraftExport ParseInternal(
        string markdown,
        List<string> acknowledgments,
        DateTimeOffset exportedAt)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new PdDraftExport
            {
                ExportedAt = ToIso8601(exportedAt),
                ChecklistAcknowledgments = acknowledgments,
            };
        }

        // Step 1 — Split into sections
        var (title, sectionBodies) = SplitSections(markdown);

        // Step 2 — positionInfo
        var positionInfo = BuildPositionInfo(title, sectionBodies);

        // Step 3-5 — Build typed sections
        var built = new Dictionary<string, PdExportSection>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, body) in sectionBodies)
        {
            if (PositionInfoSections.Contains(name)) continue;
            built[name] = BuildSection(name, body);
        }

        // Step 6 — Order: canonical first, then remaining in document order
        var sections = new List<PdExportSection>();
        foreach (var name in CanonicalOrder)
        {
            if (built.TryGetValue(name, out var section))
                sections.Add(section);
        }
        // Append any non-canonical sections in document order
        foreach (var name in sectionBodies.Keys)
        {
            if (!CanonicalOrder.Contains(name, StringComparer.OrdinalIgnoreCase) &&
                !PositionInfoSections.Contains(name) &&
                built.TryGetValue(name, out var extra))
            {
                sections.Add(extra);
            }
        }

        return new PdDraftExport
        {
            ExportedAt = ToIso8601(exportedAt),
            PositionInfo = positionInfo,
            Sections = sections,
            ChecklistAcknowledgments = acknowledgments,
        };
    }

    // ── Section splitting ────────────────────────────────────────────────────

    private static (string Title, Dictionary<string, string> Sections) SplitSections(string markdown)
    {
        var title = string.Empty;
        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = markdown.Split('\n');

        string? currentSection = null;
        var bodyLines = new List<string>();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                title = line[2..].Trim();
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (currentSection is not null)
                    sections[currentSection] = string.Join('\n', bodyLines).Trim();

                currentSection = line[3..].Trim();
                bodyLines = [];
                continue;
            }

            bodyLines.Add(line);
        }

        if (currentSection is not null)
            sections[currentSection] = string.Join('\n', bodyLines).Trim();

        return (title, sections);
    }

    // ── positionInfo ─────────────────────────────────────────────────────────

    private static PdPositionInfo BuildPositionInfo(
        string title,
        Dictionary<string, string> sections)
    {
        var payPlan = string.Empty;
        var series = string.Empty;
        var seriesTitle = string.Empty;
        var gradeMin = string.Empty;
        var gradeMax = string.Empty;
        var supervisoryStatus = string.Empty;

        if (sections.TryGetValue("Pay Plan / Series / Grade", out var ppBody))
        {
            // series number: first 4-digit word
            var seriesMatch = Regex.Match(ppBody, @"\b(\d{4})\b");
            if (seriesMatch.Success)
            {
                series = seriesMatch.Groups[1].Value;
                payPlan = "GS";

                // series title: text after – or - on the same line as the series number
                var seriesLine = ppBody.Split('\n')
                    .FirstOrDefault(l => l.Contains(series)) ?? string.Empty;
                var dashIdx = seriesLine.IndexOfAny(['\u2013', '-'], seriesLine.IndexOf(series, StringComparison.Ordinal) + series.Length);
                if (dashIdx >= 0)
                    seriesTitle = seriesLine[(dashIdx + 1)..].Trim();
            }

            // grades: all GS-\d+ matches
            var gradeMatches = Regex.Matches(ppBody, @"GS-(\d+)");
            if (gradeMatches.Count >= 2)
            {
                gradeMin = gradeMatches[0].Value;
                gradeMax = gradeMatches[^1].Value;
            }
            else if (gradeMatches.Count == 1)
            {
                gradeMin = gradeMax = gradeMatches[0].Value;
            }
        }

        if (sections.TryGetValue("Supervisory Status", out var supBody))
        {
            // "Non-Supervisory" must be checked first (contains "Supervisory")
            if (supBody.Contains("Non-Supervisory", StringComparison.OrdinalIgnoreCase))
                supervisoryStatus = "Non-Supervisory";
            else if (supBody.Contains("Supervisory", StringComparison.OrdinalIgnoreCase))
                supervisoryStatus = "Supervisory";
        }

        return new PdPositionInfo
        {
            Title = title,
            PayPlan = payPlan,
            Series = series,
            SeriesTitle = seriesTitle,
            GradeMin = gradeMin,
            GradeMax = gradeMax,
            SupervisoryStatus = supervisoryStatus,
        };
    }

    // ── Section builders ─────────────────────────────────────────────────────

    private static PdExportSection BuildSection(string name, string body)
    {
        try
        {
            return name switch
            {
                "Major Duties" => BuildListSection(name, body),
                "Qualifications Required" => BuildQualificationsSection(name, body),
                _ => new PdTextSection { SectionName = name, Content = body.Trim() },
            };
        }
        catch
        {
            return new PdTextSection { SectionName = name, Content = body };
        }
    }

    private static PdListSection BuildListSection(string name, string body)
    {
        var items = body.Split('\n')
            .Select(l => l.TrimEnd())
            .Select(l =>
            {
                // Remove bullet prefix: "- ", "* ", "• "
                if (l.StartsWith("- ", StringComparison.Ordinal)) return l[2..].Trim();
                if (l.StartsWith("* ", StringComparison.Ordinal)) return l[2..].Trim();
                if (l.StartsWith("• ", StringComparison.Ordinal)) return l[2..].Trim();
                // Remove numbered prefix: "1. ", "12. ", etc.
                var numbered = Regex.Match(l, @"^\d+\.\s+(.+)$");
                if (numbered.Success) return numbered.Groups[1].Value.Trim();
                return string.Empty;
            })
            .Where(s => s.Length > 5)
            .ToList();

        return new PdListSection { SectionName = name, Items = items };
    }

    private static PdQualificationsSection BuildQualificationsSection(string name, string body)
    {
        var specializedExperience = string.Empty;
        var timeInGrade = string.Empty;
        var ksas = new List<string>();
        var otherLines = new List<string>();

        var lines = body.Split('\n');
        var state = "other"; // current parsing context

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            // Detect sub-block headers (case-insensitive)
            if (trimmed.StartsWith("Specialized Experience", StringComparison.OrdinalIgnoreCase))
            {
                state = "specialized";
                // Inline content after the label (e.g., "Specialized Experience: ...")
                var colonIdx = trimmed.IndexOf(':', StringComparison.Ordinal);
                if (colonIdx >= 0 && colonIdx < trimmed.Length - 1)
                    specializedExperience = trimmed[(colonIdx + 1)..].Trim();
                else if (colonIdx < 0)
                    specializedExperience = trimmed["Specialized Experience".Length..].Trim();
                continue;
            }

            if (trimmed.StartsWith("Time-in-Grade", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Time in Grade", StringComparison.OrdinalIgnoreCase))
            {
                state = "timeingrade";
                var colonIdx = trimmed.IndexOf(':', StringComparison.Ordinal);
                if (colonIdx >= 0 && colonIdx < trimmed.Length - 1)
                    timeInGrade = trimmed[(colonIdx + 1)..].Trim();
                else
                    timeInGrade = trimmed;
                continue;
            }

            if (Regex.IsMatch(trimmed, @"^(Knowledge|Skills|Abilities|KSA)", RegexOptions.IgnoreCase))
            {
                state = "ksa";
                continue;
            }

            // Accumulate content for current state
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                // Blank line resets inline accumulation but doesn't switch state
                continue;
            }

            switch (state)
            {
                case "specialized":
                    if (!string.IsNullOrEmpty(specializedExperience))
                        specializedExperience += " " + trimmed;
                    else
                        specializedExperience = trimmed;
                    break;

                case "timeingrade":
                    if (!string.IsNullOrEmpty(timeInGrade))
                        timeInGrade += " " + trimmed;
                    else
                        timeInGrade = trimmed;
                    break;

                case "ksa":
                    // KSA items are bullet lines
                    var ksaItem = string.Empty;
                    if (trimmed.StartsWith("- ", StringComparison.Ordinal)) ksaItem = trimmed[2..].Trim();
                    else if (trimmed.StartsWith("* ", StringComparison.Ordinal)) ksaItem = trimmed[2..].Trim();
                    else if (trimmed.StartsWith("• ", StringComparison.Ordinal)) ksaItem = trimmed[2..].Trim();
                    else ksaItem = trimmed;

                    if (!string.IsNullOrWhiteSpace(ksaItem))
                        ksas.Add(ksaItem);
                    break;

                default: // "other"
                    otherLines.Add(trimmed);
                    break;
            }
        }

        // If nothing was matched into sub-fields, entire body goes to Other
        var hasAnySubField = !string.IsNullOrEmpty(specializedExperience)
            || !string.IsNullOrEmpty(timeInGrade)
            || ksas.Count > 0;

        var other = hasAnySubField
            ? string.Join('\n', otherLines).Trim()
            : body.Trim();

        if (!hasAnySubField)
        {
            specializedExperience = string.Empty;
            timeInGrade = string.Empty;
            ksas.Clear();
        }

        return new PdQualificationsSection
        {
            SectionName = name,
            SpecializedExperience = specializedExperience.Trim(),
            TimeInGrade = timeInGrade.Trim(),
            KnowledgeSkillsAbilities = ksas,
            Other = other,
        };
    }

    // ── Utilities ────────────────────────────────────────────────────────────

    private static string ToIso8601(DateTimeOffset dt) =>
        dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
}
```

- [ ] **Step 6: Run tests — expect failures (parse logic not yet validated)**

```bash
cd c:/apps/DotnetAiAgentUiTutorial
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "PdDraftParser" -v normal 2>&1 | tail -40
```

Expected: Build succeeds; tests run; fix any failures before proceeding.

- [ ] **Step 7: Fix any failing tests**

Common failure causes:
- `SupervisoryStatus`: "Non-Supervisory" must be checked before "Supervisory" (substring match) — already handled in the parser above
- KSA extraction: The `state` variable may not reset between sub-blocks; ensure blank lines between blocks don't cause cross-contamination
- `SlugifyTitle` with special chars: verify `Trim('-')` handles leading/trailing dashes from non-alphanumeric title chars

Fix inline until all 16 parser tests pass.

- [ ] **Step 8: Run all tests**

```bash
cd c:/apps/DotnetAiAgentUiTutorial
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ 2>&1 | tail -20
```

Expected: All 141 existing + 16 new = 157 tests pass.

- [ ] **Step 9: Commit**

```bash
cd c:/apps/DotnetAiAgentUiTutorial
git add DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftExport.cs
git add DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftParser.cs
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspaceHelper.cs
git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdDraftParserTests.cs
git commit -m "feat: add PdDraftExport model, PdDraftParser, and unit tests"
```

---

### Task 2: Wire Parser into DraftWorkspace.razor

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` (lines 722–744, `ExportJsonAsync`)

**Interfaces:**
- Consumes: `PdDraftParser.Parse(string, List<string>, DateTimeOffset)` from Task 1
- Consumes: `DraftWorkspaceHelper.SlugifyTitle(string)` from Task 1
- Consumes: `_currentDraftMarkdown` (existing field, line 219)
- Consumes: `_checklistState.Acknowledgments` (existing property — `IReadOnlyList<AcknowledgedSection>` with `.SectionName`)
- Consumes: `JS.InvokeVoidAsync("downloadFile", filename, base64)` (existing JS interop)

---

- [ ] **Step 1: Add `using` directive for new namespace**

At the top of `DraftWorkspace.razor` (in the `@using` block), add:

```razor
@using System.Text.Json
@using System.Text
```

> These may already be present — check the top of the file before adding. If already there as `@using`, skip adding duplicates.

- [ ] **Step 2: Replace `ExportJsonAsync` body (lines 722–744)**

Find the existing method:
```csharp
private async Task ExportJsonAsync()
{
    _exportMenuOpen = false;
    if (_turns.Count == 0)
    {
        _status = "No conversation to export.";
        return;
    }

    var export = new
    {
        exportedAt = DateTimeOffset.UtcNow,
        draft = _currentDraftMarkdown,
        acknowledgments = _checklistState.Acknowledgments
            .Select(a => new { a.SectionName, a.AcknowledgedAt }),
        turns = _turns.Select(t => new { t.Role, t.Text, t.Timestamp })
    };

    var json = System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
    await JS.InvokeVoidAsync("downloadFile", "position-description.json", Convert.ToBase64String(bytes));
    _status = "✅ JSON file ready.";
}
```

Replace with:
```csharp
private async Task ExportJsonAsync()
{
    _exportMenuOpen = false;
    if (string.IsNullOrWhiteSpace(_currentDraftMarkdown))
    {
        _status = "No draft to export.";
        return;
    }

    var export = PdDraftParser.Parse(
        _currentDraftMarkdown,
        _checklistState.Acknowledgments.Select(a => a.SectionName).ToList(),
        DateTimeOffset.UtcNow);

    var options = new JsonSerializerOptions { WriteIndented = true };
    var json = JsonSerializer.Serialize(export, options);
    var bytes = Encoding.UTF8.GetBytes(json);
    var slug = DraftWorkspaceHelper.SlugifyTitle(export.PositionInfo.Title);
    await JS.InvokeVoidAsync("downloadFile", $"pd-draft-{slug}.json", Convert.ToBase64String(bytes));
    _status = "✅ JSON file ready.";
}
```

- [ ] **Step 3: Build to verify no compile errors**

```bash
cd c:/apps/DotnetAiAgentUiTutorial
dotnet build DotnetAiAgentUi/src/HrMcp.Agent/ 2>&1 | tail -20
```

Expected: Build succeeded, 0 errors.

If `JsonSerializerOptions` or `Encoding` is ambiguous, fully qualify: `System.Text.Json.JsonSerializerOptions`, `System.Text.Encoding.UTF8`.

- [ ] **Step 4: Run full test suite**

```bash
cd c:/apps/DotnetAiAgentUiTutorial
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ 2>&1 | tail -20
```

Expected: All tests pass (157+).

- [ ] **Step 5: Commit**

```bash
cd c:/apps/DotnetAiAgentUiTutorial
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
git commit -m "feat: replace ExportJsonAsync with structured PD JSON export"
```

---

## Self-Review

**Spec coverage check:**

| Spec Requirement | Covered by |
|-----------------|-----------|
| `PdDraftExport` model with `schemaVersion`, `exportedAt`, `positionInfo`, `sections`, `checklistAcknowledgments` | Task 1, Step 4 (`PdDraftExport.cs`) |
| `PdPositionInfo` with 7 fields | Task 1, Step 4 |
| Polymorphic `PdExportSection` with `[JsonPolymorphic]` / `[JsonDerivedType]` | Task 1, Step 4 |
| `PdDraftParser.Parse(markdown, acks, exportedAt) → PdDraftExport` | Task 1, Step 5 |
| Section splitting on `## ` headings | Task 1, Step 5 (`SplitSections`) |
| `positionInfo`: title from `# `, payPlan/series/seriesTitle from `## Pay Plan / Series / Grade`, grades, supervisoryStatus | Task 1, Step 5 (`BuildPositionInfo`) |
| `Major Duties` → `PdListSection` with bullet/numbered support | Task 1, Step 5 (`BuildListSection`) |
| `Qualifications Required` → `PdQualificationsSection` sub-fields | Task 1, Step 5 (`BuildQualificationsSection`) |
| All other sections → `PdTextSection` | Task 1, Step 5 (`BuildSection`) |
| Canonical output order (10 sections) | Task 1, Step 5 (`CanonicalOrder`) |
| Missing sections omitted | Task 1, Step 5 (only adds `built[name]` if key exists) |
| Parser never throws (catch-all) | Task 1, Step 5 (`try/catch` in `Parse`) |
| `ExportJsonAsync` replacement — checks `_currentDraftMarkdown` not `_turns` | Task 2, Step 2 |
| File named `pd-draft-<slug>.json` | Task 2, Step 2 |
| `SlugifyTitle` helper, unit-tested | Task 1, Steps 3 + 1 |
| 16 specified tests | Task 1, Step 1 (16 test methods) |
| No new NuGet packages | ✅ only BCL APIs used |
| `nullable enable`, top-level namespace | ✅ all new files use top-level namespace declarations |

**Placeholder scan:** None found. All test methods contain complete code. All implementation steps contain the actual code to write.

**Type consistency check:** `PdExportSection` used consistently as the abstract base throughout `PdDraftExport.cs`, `PdDraftParser.cs`, and referenced in tests via `OfType<PdListSection>()` / `OfType<PdQualificationsSection>()`. `PdSection` (the checklist record) is never touched.
