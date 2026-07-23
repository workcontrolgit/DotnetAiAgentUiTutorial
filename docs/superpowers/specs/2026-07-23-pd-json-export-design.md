# PD JSON Export — Design Spec

**Date:** 2026-07-23  
**Feature:** Structured JSON export of the draft panel content aligned to the 12 PD sections  
**Status:** Approved

---

## Goal

Replace the current `ExportJsonAsync` implementation (which exports raw chat history + raw markdown) with a structured JSON export that reflects the **content of the draft panel**, parsed into the 12 canonical PD sections. No chat history is included.

---

## Problem Statement

Current `ExportJsonAsync` (in `DraftWorkspace.razor:722`) exports:
```json
{
  "exportedAt": "...",
  "draft": "<raw markdown string>",
  "acknowledgments": [...],
  "turns": [{ "Role": "...", "Text": "...", "Timestamp": "..." }]
}
```

Issues:
1. `turns` contains full chat history — not useful for PD consumers
2. `draft` is a raw markdown blob — not machine-readable by downstream systems
3. No structured breakdown of duties, qualifications sub-fields, or metadata

---

## Architecture

**Approach:** Markdown section parser (Option A — no AI call, deterministic, instant).

Parse `_currentDraftMarkdown` at export time using a new static `PdDraftParser` class. The AI writes consistent `## Section Name` headings and bullet lists — this is reliably parseable without AI assistance.

### Files

| File | Role |
|------|------|
| `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftExport.cs` | New: C# model classes |
| `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdDraftParser.cs` | New: `PdDraftParser.Parse(string) → PdDraftExport` |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Modified: `ExportJsonAsync` body only |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdDraftParserTests.cs` | New: xUnit tests |

---

## C# Model

```csharp
// PdDraftExport.cs
namespace HrMcp.Agent.Web.Models;

public sealed class PdDraftExport
{
    public string SchemaVersion { get; init; } = "1.0";
    public string ExportedAt { get; init; } = string.Empty;     // ISO 8601 UTC
    public PdPositionInfo PositionInfo { get; init; } = new();
    public List<PdSection> Sections { get; init; } = [];
    public List<string> ChecklistAcknowledgments { get; init; } = [];
}

public sealed class PdPositionInfo
{
    public string Title { get; init; } = string.Empty;
    public string PayPlan { get; init; } = string.Empty;        // e.g. "GS"
    public string Series { get; init; } = string.Empty;         // e.g. "2210"
    public string SeriesTitle { get; init; } = string.Empty;    // e.g. "IT Management"
    public string GradeMin { get; init; } = string.Empty;       // e.g. "GS-12"
    public string GradeMax { get; init; } = string.Empty;       // e.g. "GS-13"
    public string SupervisoryStatus { get; init; } = string.Empty; // "Supervisory" | "Non-Supervisory"
}

// Section polymorphism via sealed subclasses
// JSON serialization uses [JsonDerivedType] attributes for System.Text.Json discriminated union

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PdTextSection), "text")]
[JsonDerivedType(typeof(PdListSection), "list")]
[JsonDerivedType(typeof(PdQualificationsSection), "qualifications")]
public abstract class PdSection
{
    public string SectionName { get; init; } = string.Empty;
}

public sealed class PdTextSection : PdSection
{
    public string Content { get; init; } = string.Empty;
}

public sealed class PdListSection : PdSection
{
    public List<string> Items { get; init; } = [];
}

public sealed class PdQualificationsSection : PdSection
{
    public string SpecializedExperience { get; init; } = string.Empty;
    public string TimeInGrade { get; init; } = string.Empty;
    public List<string> KnowledgeSkillsAbilities { get; init; } = [];
    public string Other { get; init; } = string.Empty;
}
```

---

## JSON Output Schema

```json
{
  "schemaVersion": "1.0",
  "exportedAt": "2026-07-23T14:32:00Z",
  "positionInfo": {
    "title": "IT Specialist",
    "payPlan": "GS",
    "series": "2210",
    "seriesTitle": "IT Management",
    "gradeMin": "GS-12",
    "gradeMax": "GS-13",
    "supervisoryStatus": "Non-Supervisory"
  },
  "sections": [
    {
      "type": "text",
      "sectionName": "Position Summary",
      "content": "Serves as an IT Specialist responsible for..."
    },
    {
      "type": "list",
      "sectionName": "Major Duties",
      "items": [
        "Plans and coordinates IT infrastructure improvements across the agency.",
        "Serves as technical expert on cloud migration strategy.",
        "Develops and maintains system security documentation.",
        "Analyzes user requirements and translates them into technical specifications.",
        "Provides authoritative guidance on IT acquisition standards."
      ]
    },
    {
      "type": "qualifications",
      "sectionName": "Qualifications Required",
      "specializedExperience": "One year of specialized experience equivalent to GS-11...",
      "timeInGrade": "Must have served 52 weeks at the GS-11 level...",
      "knowledgeSkillsAbilities": [
        "Knowledge of federal IT security frameworks (FISMA, NIST).",
        "Ability to communicate complex technical concepts to non-technical audiences.",
        "Skill in cloud architecture and migration planning."
      ],
      "other": ""
    },
    {
      "type": "text",
      "sectionName": "Preferred Qualifications",
      "content": "Experience with AWS GovCloud or Azure Government environments..."
    },
    {
      "type": "text",
      "sectionName": "Education Requirements",
      "content": "Bachelor's degree in Computer Science or related field..."
    },
    {
      "type": "text",
      "sectionName": "Security Clearance",
      "content": "Public Trust — Moderate Risk (MBI)"
    },
    {
      "type": "text",
      "sectionName": "Remote Work Eligibility",
      "content": "Remote work eligible per agency policy."
    },
    {
      "type": "text",
      "sectionName": "Travel Requirements",
      "content": "Occasional travel — up to 10% of the time."
    },
    {
      "type": "text",
      "sectionName": "EEO Statement",
      "content": "This agency is an Equal Opportunity Employer..."
    },
    {
      "type": "text",
      "sectionName": "Reasonable Accommodation",
      "content": "Persons with disabilities who require alternative means..."
    }
  ],
  "checklistAcknowledgments": [
    "Major Duties",
    "Qualifications Required"
  ]
}
```

**Design decisions:**
- `positionInfo` holds the 3 structured header sections (Title, Pay Plan/Series/Grade, Supervisory Status) as flat fields
- `sections` holds the remaining 10 sections in document order
- `type` discriminator on each section enables typed deserialization by consumers
- `checklistAcknowledgments` is a flat list of acknowledged section names
- Chat history (`turns`) is **not** exported
- File name: `pd-draft-<title-slug>.json` (title lowercased, spaces → hyphens, max 40 chars)

---

## Parser: `PdDraftParser`

```
Parse(string markdown, List<string> acknowledgments, DateTimeOffset exportedAt) → PdDraftExport
```

**Step 1 — Split into sections:**
Split markdown on lines that start with `## ` to produce a `Dictionary<string, string>` (section name → raw body text). H1 (`# `) line is extracted separately as the title.

**Step 2 — positionInfo:**
- `title`: first line starting with `# ` (strip `# ` prefix)
- `payPlan`: always `"GS"` if series/grade fields are present; empty otherwise
- `series`: regex `\b(\d{4})\b` in the `## Pay Plan / Series / Grade` body
- `seriesTitle`: text after `–` or `-` on the series line (e.g., `"Series: 2210 – IT Management"`)
- `gradeMin`/`gradeMax`: regex `GS-\d+` matches — first match = min, second = max; if only one, both equal it
- `supervisoryStatus`: `## Supervisory Status` body — contains "Supervisory" (case-insensitive) → `"Supervisory"`, else `"Non-Supervisory"`; if section missing → `""`

**Step 3 — Major Duties → `PdListSection`:**
Split body on bullet lines: lines starting with `- `, `* `, `• `, or matching `^\d+\.\s`. Trim each item. Items with length > 5 included; blank/short lines discarded.

**Step 4 — Qualifications Required → `PdQualificationsSection`:**
Look for labeled sub-blocks within the body:
- Lines/paragraphs starting with `Specialized Experience` (case-insensitive) → `specializedExperience`
- Lines/paragraphs starting with `Time-in-Grade` or `Time in Grade` → `timeInGrade`
- Lines starting with bullets under a `Knowledge`, `Skills`, `Abilities`, or `KSA` heading → `knowledgeSkillsAbilities` array
- Anything not matched → `other` (trimmed, newline-joined)
- If none of the above match at all → entire body → `other`

**Step 5 — All other sections → `PdTextSection`:**
Trimmed body text as `content`. Section present but blank → `content: ""`.

**Step 6 — Section ordering:**
Output `sections` in canonical order:
1. Position Summary
2. Major Duties
3. Qualifications Required
4. Preferred Qualifications
5. Education Requirements
6. Security Clearance
7. Remote Work Eligibility
8. Travel Requirements
9. EEO Statement
10. Reasonable Accommodation

Sections present in the draft but not in the canonical list are appended at the end in document order.

**Resilience:**
- Missing section → omit from `sections` array
- Parser never throws — returns best-effort result; catch-all returns `PdTextSection` with raw body as `content`

---

## `ExportJsonAsync` Change (DraftWorkspace.razor)

Replace the current `ExportJsonAsync` body with:

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
    var slug = SlugifyTitle(export.PositionInfo.Title);
    await JS.InvokeVoidAsync("downloadFile", $"pd-draft-{slug}.json", Convert.ToBase64String(bytes));
    _status = "✅ JSON file ready.";
}

private static string SlugifyTitle(string title)
{
    if (string.IsNullOrWhiteSpace(title)) return "draft";
    var slug = new string(title.ToLowerInvariant()
        .Select(c => char.IsLetterOrDigit(c) ? c : '-')
        .ToArray()).Trim('-');
    return slug[..Math.Min(40, slug.Length)];
}
```

---

## Tests

New file: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdDraftParserTests.cs`

| Test | What it verifies |
|------|-----------------|
| `Parse_FullDraft_ReturnsAllSections` | All 10 sections present in output |
| `Parse_FullDraft_PositionInfoHasCorrectFields` | Title, series, gradeMin, gradeMax parsed correctly |
| `Parse_FullDraft_MajorDutiesIsArray` | 5 duties → `PdListSection` with 5 items |
| `Parse_FullDraft_QualificationsSubFieldsExtracted` | specializedExperience, timeInGrade, KSAs populated |
| `Parse_MajorDuties_NumberedList_ExtractsItems` | `"1. Duty"` format → array |
| `Parse_MajorDuties_BulletedList_ExtractsItems` | `"- Duty"` format → array |
| `Parse_Qualifications_NoSubHeaders_PutsAllInOther` | Unstructured quals body → `other` field |
| `Parse_MissingSection_OmitsFromSectionsArray` | Draft without Preferred Qualifications → not in sections |
| `Parse_EmptyDraft_ReturnsEmptyExport` | Empty string → empty positionInfo, empty sections |
| `Parse_GradeRange_SingleGrade_SetsMinEqualsMax` | `"GS-13"` only → both gradeMin and gradeMax = `"GS-13"` |
| `Parse_SupervisoryStatus_Supervisory_DetectedCorrectly` | Body containing "Supervisory" → `"Supervisory"` |
| `Parse_SupervisoryStatus_NonSupervisory_DetectedCorrectly` | Body containing "Non-Supervisory" → `"Non-Supervisory"` |
| `Parse_ExportedAt_IsUtcIsoString` | `exportedAt` round-trips as ISO 8601 |
| `Parse_SchemaVersion_Is_1_0` | `schemaVersion == "1.0"` |
| `SlugifyTitle_NormalTitle_ProducesSlug` | `"IT Specialist"` → `"it-specialist"` |
| `SlugifyTitle_EmptyTitle_ReturnsDraft` | `""` → `"draft"` |

Run: `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "PdDraftParser"`

---

## Global Constraints

- .NET 10, Blazor Server, xUnit
- No new NuGet packages
- `nullable enable` and top-level namespace declarations on all C# files
- `System.Text.Json` for serialization (already used in the codebase)
- `[JsonPolymorphic]` / `[JsonDerivedType]` require .NET 7+ — available in .NET 10 ✅
- Commit prefix: `feat:`
