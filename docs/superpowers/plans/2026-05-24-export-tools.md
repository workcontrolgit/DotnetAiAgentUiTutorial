# Export Tools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add HTML, Word (.docx), and Excel (.xlsx) export MCP tools that stream base64 file content to the calling client, with agent-side file saving to `usajobs/output/`.

**Architecture:** All four export tools live on the MCP server and return `{ "fileName": "...", "content": "<base64>" }`. The MCP server never writes files to disk. The agent intercepts export tool results, decodes base64, and saves to `usajobs/output/`. A future SPA client uses the same base64 payload to trigger a browser download.

**Tech Stack:** .NET 10, DocumentFormat.OpenXml 3.x (Word + Excel), ModelContextProtocol, Microsoft.Extensions.AI, Spectre.Console.

---

## File Map

| File | Change |
|---|---|
| `DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj` | Add `DocumentFormat.OpenXml` package |
| `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/PositionTools.cs` | Rename `RenderPositionAsUsaJobsHtml` → `ExportPositionToHtml`; return base64 JSON instead of saving to disk |
| `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs` | New — `ExportPositionToWord`, `ExportDraftToWord`, `ExportPositionsToExcel` |
| `DotnetAiAgentMcp/src/HrMcp.McpServer/Program.cs` | Register `ExportTools` |
| `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs` | Add `ExportToolNames`, `TrySaveExportResult`, `_outputFolder`; intercept export results in tool loop |
| `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs` | Pass `outputFolder` to `HrAgent`; update system prompt guidance |

---

## Task 1: Add DocumentFormat.OpenXml NuGet Package

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj`

- [ ] **Step 1: Add the package**

```bash
cd c:/apps/DotnetMcpTutorial
dotnet add DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj package DocumentFormat.OpenXml --version "3.*"
```

- [ ] **Step 2: Verify build**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
git commit -m "Add DocumentFormat.OpenXml package to McpServer"
```

---

## Task 2: Refactor ExportPositionToHtml (stream base64)

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/PositionTools.cs`

- [ ] **Step 1: Add using directive at top of PositionTools.cs**

Add to the existing `using` block:
```csharp
using System.Text.Json;
```

- [ ] **Step 2: Rename the tool and change return to base64 JSON**

Replace the `[McpServerTool(Name = "RenderPositionAsUsaJobsHtml")]` method with:

```csharp
[McpServerTool(Name = "ExportPositionToHtml"),
 Description("Exports a position as a USAJobs-style HTML page. Returns a JSON payload with fileName and base64-encoded content for the agent to save locally.")]
public async Task<string> ExportPositionToHtml(
    [Description("The numeric ID of the position to export")] int positionId,
    CancellationToken ct = default)
{
    logger.LogInformation("[Request ] ExportPositionToHtml positionId={PositionId}", positionId);
    var p = await positions.GetPositionByIdAsync(positionId, ct);
    if (p is null)
    {
        logger.LogWarning("[Response] ExportPositionToHtml positionId={PositionId} => not found", positionId);
        return $"Position {positionId} not found.";
    }

    var templatePath = FindLayoutFile("usajobs/layout/usajobs-template.html");
    if (templatePath is null) return "Template file not found. Expected at usajobs/layout/usajobs-template.html.";

    var template = await File.ReadAllTextAsync(templatePath, ct);

    var salary = p.PositionRemuneration;
    var minSalary = salary?.MinimumRange is > 0 ? $"${salary.MinimumRange:N0}" : "N/A";
    var maxSalary = salary?.MaximumRange is > 0 ? $"${salary.MaximumRange:N0}" : "N/A";
    var payGrade = p.PayGradeMin == p.PayGradeMax ? p.PayGradeMin : $"{p.PayGradeMin}–{p.PayGradeMax}";

    var adjudicationHtml = string.IsNullOrWhiteSpace(p.AdjudicationType)
        ? "N/A"
        : string.Join("<br/>", p.AdjudicationType.Split(',', StringSplitOptions.TrimEntries));

    var hiringPathsHtml = BuildHiringPathsHtml(p.HiringPath);

    var html = template
        .Replace("{{TITLE}}",                    Encode(p.Title))
        .Replace("{{DEPARTMENT_NAME}}",           Encode(p.HiringOrganization?.DepartmentName ?? ""))
        .Replace("{{ORGANIZATION_NAME}}",         Encode(p.HiringOrganization?.OrganizationName ?? ""))
        .Replace("{{STATUS_CLASS}}",              p.IsOpen ? "open" : "closed")
        .Replace("{{STATUS_TEXT}}",               p.IsOpen ? "Accepting applications" : "Closed")
        .Replace("{{OPEN_DATE}}",                 p.OpenDate.ToString("MM/dd/yyyy"))
        .Replace("{{CLOSE_DATE}}",                p.CloseDate?.ToString("MM/dd/yyyy") ?? "Open until filled")
        .Replace("{{SALARY_MIN}}",                minSalary)
        .Replace("{{SALARY_MAX}}",                maxSalary)
        .Replace("{{PAY_GRADE}}",                 Encode(payGrade))
        .Replace("{{DUTY_LOCATION}}",             Encode(p.DutyLocation))
        .Replace("{{TOTAL_OPENINGS}}",            string.IsNullOrWhiteSpace(p.TotalOpenings) ? "1 vacancy" : $"{p.TotalOpenings} vacancies")
        .Replace("{{REMOTE_ELIGIBLE}}",           p.RemoteEligible ? "Yes" : "No")
        .Replace("{{TELEWORK_ELIGIBLE}}",         p.TeleworkEligible ? "Yes — Per Agency Policy" : "No")
        .Replace("{{TRAVEL_REQUIRED}}",           Encode(p.TravelRequired.ToString()))
        .Replace("{{RELOCATION_AUTHORIZED}}",     p.RelocationAuthorized ? "Yes" : "No")
        .Replace("{{APPOINTMENT_TYPE}}",          Encode(p.AppointmentType.ToString()))
        .Replace("{{WORK_SCHEDULE}}",             Encode(p.WorkSchedule.ToString()))
        .Replace("{{SERVICE_TYPE}}",              Encode(p.ServiceType))
        .Replace("{{PROMOTION_POTENTIAL}}",       Encode(p.PromotionPotential))
        .Replace("{{OCCUPATIONAL_SERIES}}",       Encode(p.OccupationalSeries))
        .Replace("{{OCCUPATIONAL_SERIES_TITLE}}", Encode(p.OccupationalSeriesTitle))
        .Replace("{{SUPERVISORY_STATUS}}",        p.SupervisoryStatus ? "Yes" : "No")
        .Replace("{{SECURITY_CLEARANCE}}",        Encode(p.SecurityClearance.ToString()))
        .Replace("{{DRUG_TEST_REQUIRED}}",        p.DrugTestRequired ? "Yes" : "No")
        .Replace("{{ADJUDICATION_TYPE}}",         adjudicationHtml)
        .Replace("{{ANNOUNCEMENT_NUMBER}}",       Encode(p.AnnouncementNumber))
        .Replace("{{USAJOBS_ID}}",                Encode(p.UsaJobsId))
        .Replace("{{CONTACT_NAME}}",              Encode(p.ContactName))
        .Replace("{{CONTACT_PHONE}}",             Encode(p.ContactPhone.Trim()))
        .Replace("{{CONTACT_EMAIL}}",             Encode(p.ContactEmail))
        .Replace("{{CONTACT_ADDRESS}}",           Encode(p.ContactAddress).Replace("\n", "<br/>"))
        .Replace("{{HIRING_PATHS_HTML}}",         hiringPathsHtml)
        .Replace("{{DESCRIPTION_HTML}}",          TextToHtml(p.Description))
        .Replace("{{DUTIES_HTML}}",               TextToHtml(p.Duties))
        .Replace("{{CONDITIONS_HTML}}",           TextToHtml(p.ConditionsOfEmployment))
        .Replace("{{QUALIFICATIONS_HTML}}",       TextToHtml(p.Qualifications))
        .Replace("{{EDUCATION_HTML}}",            TextToHtml(p.Education))
        .Replace("{{ADDITIONAL_INFO_HTML}}",      TextToHtml(p.AdditionalInformation))
        .Replace("{{EVALUATIONS_HTML}}",          TextToHtml(p.Evaluations))
        .Replace("{{REQUIRED_DOCUMENTS_HTML}}",   TextToHtml(p.RequiredDocuments))
        .Replace("{{HOW_TO_APPLY_HTML}}",         TextToHtml(p.HowToApply))
        .Replace("{{NEXT_STEPS_HTML}}",           TextToHtml(p.NextSteps));

    var bytes = Encoding.UTF8.GetBytes(html);
    var base64 = Convert.ToBase64String(bytes);
    var result = JsonSerializer.Serialize(new { fileName = $"position-{positionId}.html", content = base64 });
    logger.LogInformation("[Response] ExportPositionToHtml positionId={PositionId} => {Bytes} bytes", positionId, bytes.Length);
    return result;
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/PositionTools.cs
git commit -m "Refactor ExportPositionToHtml to stream base64 instead of saving server-side"
```

---

## Task 3: Create ExportTools.cs with ExportPositionToWord

**Files:**
- Create: `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs`

- [ ] **Step 1: Create ExportTools.cs**

```csharp
// src/HrMcp.McpServer/Tools/ExportTools.cs
using System.ComponentModel;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HrMcp.Application.Services;
using HrMcp.Core.Entities;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public sealed class ExportTools(PositionService positions, ILogger<ExportTools> logger)
{
    [McpServerTool(Name = "ExportPositionToWord"),
     Description("Exports a position's full structured data (all sections matching the USAJobs HTML layout) to a Word (.docx) file. Returns a JSON payload with fileName and base64-encoded content for the agent to save locally. Use when the user wants to export or download a position as a Word document.")]
    public async Task<string> ExportPositionToWord(
        [Description("The numeric ID of the position to export")] int positionId,
        CancellationToken ct = default)
    {
        logger.LogInformation("[Request ] ExportPositionToWord positionId={PositionId}", positionId);
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null)
        {
            logger.LogWarning("[Response] ExportPositionToWord positionId={PositionId} => not found", positionId);
            return $"Position {positionId} not found.";
        }

        var bytes = BuildPositionDocx(p);
        var result = ToBase64Json($"position-{positionId}.docx", bytes);
        logger.LogInformation("[Response] ExportPositionToWord positionId={PositionId} => {Bytes} bytes", positionId, bytes.Length);
        return result;
    }

    // ── Word document builder ────────────────────────────────────────────────

    private static byte[] BuildPositionDocx(Position p)
    {
        using var ms = new MemoryStream();
        using var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true);

        var mainPart = doc.AddMainDocumentPart();
        AddStylesPart(mainPart);
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.AppendChild(body);

        var rem = p.PositionRemuneration;
        var grade = p.PayGradeMin == p.PayGradeMax ? p.PayGradeMin : $"{p.PayGradeMin}–{p.PayGradeMax}";
        var minSal = rem?.MinimumRange is > 0 ? $"${rem.MinimumRange:N0}" : "N/A";
        var maxSal = rem?.MaximumRange is > 0 ? $"${rem.MaximumRange:N0}" : "N/A";

        // Header
        body.AppendChild(StyledParagraph("Heading1", p.Title ?? ""));
        body.AppendChild(StyledParagraph("Normal", $"{p.HiringOrganization?.DepartmentName} | {p.HiringOrganization?.OrganizationName}"));
        body.AppendChild(new Paragraph());

        // Overview table (maps to HTML sidebar)
        body.AppendChild(StyledParagraph("Heading2", "Overview"));
        body.AppendChild(BuildOverviewTable(p, rem, grade, minSal, maxSal));
        body.AppendChild(new Paragraph());

        // Main content sections
        AppendSection(body, "Summary", p.Description);
        AppendSection(body, "Duties", p.Duties);
        body.AppendChild(StyledParagraph("Heading2", "Requirements"));
        AppendSection(body, "Conditions of Employment", p.ConditionsOfEmployment, "Heading3");
        AppendSection(body, "Qualifications", p.Qualifications, "Heading3");
        AppendSection(body, "Education", p.Education, "Heading3");
        AppendSection(body, "Additional Information", p.AdditionalInformation, "Heading3");
        AppendSection(body, "How You Will Be Evaluated", p.Evaluations);
        AppendSection(body, "Required Documents", p.RequiredDocuments);
        AppendSection(body, "How to Apply", p.HowToApply);

        // Contact
        body.AppendChild(StyledParagraph("Heading3", "Agency Contact Information"));
        body.AppendChild(NormalParagraph($"Name: {p.ContactName}"));
        body.AppendChild(NormalParagraph($"Phone: {p.ContactPhone}"));
        body.AppendChild(NormalParagraph($"Email: {p.ContactEmail}"));
        body.AppendChild(NormalParagraph($"Address: {p.ContactAddress}"));

        AppendSection(body, "Next Steps", p.NextSteps, "Heading3");

        doc.Save();
        return ms.ToArray();
    }

    private static Table BuildOverviewTable(Position p, PositionRemuneration? rem, string grade, string minSal, string maxSal)
    {
        var table = new Table();
        var tblProps = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder    { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new LeftBorder   { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new RightBorder  { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new InsideVerticalBorder   { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" }
            )
        );
        table.AppendChild(tblProps);

        void Row(string label, string value) => table.AppendChild(OverviewRow(label, value));

        Row("Status",           p.IsOpen ? "Accepting applications" : "Closed");
        Row("Open / Closing",   $"{p.OpenDate:MM/dd/yyyy} – {p.CloseDate?.ToString("MM/dd/yyyy") ?? "Open until filled"}");
        Row("Salary",           $"{minSal} – {maxSal} per year");
        Row("Pay grade",        grade);
        Row("Location",         p.DutyLocation ?? "");
        Row("Openings",         string.IsNullOrWhiteSpace(p.TotalOpenings) ? "1 vacancy" : $"{p.TotalOpenings} vacancies");
        Row("Remote",           p.RemoteEligible ? "Yes" : "No");
        Row("Telework",         p.TeleworkEligible ? "Yes – Per Agency Policy" : "No");
        Row("Travel required",  p.TravelRequired.ToString());
        Row("Relocation",       p.RelocationAuthorized ? "Yes" : "No");
        Row("Appointment type", p.AppointmentType.ToString());
        Row("Work schedule",    p.WorkSchedule.ToString());
        Row("Service",          p.ServiceType ?? "");
        Row("Promotion potential", p.PromotionPotential ?? "");
        Row("Job series",       $"{p.OccupationalSeries} {p.OccupationalSeriesTitle}");
        Row("Supervisory",      p.SupervisoryStatus ? "Yes" : "No");
        Row("Security clearance", p.SecurityClearance.ToString());
        Row("Drug test",        p.DrugTestRequired ? "Yes" : "No");
        Row("Announcement #",   p.AnnouncementNumber ?? "");
        Row("Control #",        p.UsaJobsId ?? "");

        return table;
    }

    private static TableRow OverviewRow(string label, string value)
    {
        var labelCell = new TableCell(
            new TableCellProperties(new TableCellWidth { Width = "1800", Type = TableWidthUnitValues.Dxa }),
            new Paragraph(new Run(new RunProperties(new Bold()), new Text(label)))
        );
        var valueCell = new TableCell(
            new Paragraph(new Run(new Text(value) { Space = SpaceProcessingModeValues.Preserve }))
        );
        return new TableRow(labelCell, valueCell);
    }

    private static void AppendSection(Body body, string heading, string? text, string styleId = "Heading2")
    {
        body.AppendChild(StyledParagraph(styleId, heading));
        if (string.IsNullOrWhiteSpace(text))
        {
            body.AppendChild(NormalParagraph("N/A"));
            return;
        }
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            if (!string.IsNullOrWhiteSpace(t))
                body.AppendChild(NormalParagraph(t));
        }
    }

    // ── OpenXML helpers ──────────────────────────────────────────────────────

    private static Paragraph StyledParagraph(string styleId, string text)
    {
        return new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })
        );
    }

    private static Paragraph NormalParagraph(string text)
    {
        return new Paragraph(
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })
        );
    }

    private static void AddStylesPart(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        // Normal
        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.AppendChild(new StyleName { Val = "Normal" });
        var normalRp = new StyleRunProperties();
        normalRp.AppendChild(new FontSize { Val = "22" });
        normal.AppendChild(normalRp);
        styles.AppendChild(normal);

        // Heading 1 — 28 half-points = 14pt bold
        styles.AppendChild(HeadingStyle("Heading1", "heading 1", "28"));
        // Heading 2 — 26 half-points = 13pt bold
        styles.AppendChild(HeadingStyle("Heading2", "heading 2", "26"));
        // Heading 3 — 24 half-points = 12pt bold
        styles.AppendChild(HeadingStyle("Heading3", "heading 3", "24"));

        stylesPart.Styles = styles;
    }

    private static Style HeadingStyle(string styleId, string styleName, string halfPoints)
    {
        var style = new Style { Type = StyleValues.Paragraph, StyleId = styleId };
        style.AppendChild(new StyleName { Val = styleName });
        style.AppendChild(new BasedOn { Val = "Normal" });
        var spp = new StyleParagraphProperties();
        spp.AppendChild(new SpacingBetweenLines { Before = "240", After = "80" });
        style.AppendChild(spp);
        var srp = new StyleRunProperties();
        srp.AppendChild(new Bold());
        srp.AppendChild(new FontSize { Val = halfPoints });
        srp.AppendChild(new FontSizeComplexScript { Val = halfPoints });
        style.AppendChild(srp);
        return style;
    }

    private static string ToBase64Json(string fileName, byte[] bytes)
    {
        return JsonSerializer.Serialize(new { fileName, content = Convert.ToBase64String(bytes) });
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs
git commit -m "Add ExportPositionToWord MCP tool with OpenXML Word doc generation"
```

---

## Task 4: Add ExportDraftToWord to ExportTools.cs

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs`

- [ ] **Step 1: Add ExportDraftToWord tool method**

Insert after the closing brace of `ExportPositionToWord` and before the `BuildPositionDocx` method:

```csharp
[McpServerTool(Name = "ExportDraftToWord"),
 Description("Exports an AI-generated job description draft to an editable Word (.docx) file. Pass the full current draft text (which may have been updated by the user). Returns a JSON payload with fileName and base64-encoded content for the agent to save locally.")]
public async Task<string> ExportDraftToWord(
    [Description("The numeric ID of the position the draft is for (used for the document title)")] int positionId,
    [Description("The full job description draft text, including any user edits. Markdown headings (##) become Word headings.")] string draftContent,
    CancellationToken ct = default)
{
    logger.LogInformation("[Request ] ExportDraftToWord positionId={PositionId}", positionId);

    if (string.IsNullOrWhiteSpace(draftContent))
        return "Draft content is empty — nothing to export.";

    var p = await positions.GetPositionByIdAsync(positionId, ct);
    var title = p?.Title ?? $"Position {positionId}";
    var org = p is null ? "" : $"{p.HiringOrganization?.DepartmentName} | {p.HiringOrganization?.OrganizationName}";

    var bytes = BuildDraftDocx(title, org, draftContent);
    var result = ToBase64Json($"position-{positionId}-draft.docx", bytes);
    logger.LogInformation("[Response] ExportDraftToWord positionId={PositionId} => {Bytes} bytes", positionId, bytes.Length);
    return result;
}
```

- [ ] **Step 2: Add BuildDraftDocx private method**

Insert after `BuildPositionDocx` and before `BuildOverviewTable`:

```csharp
private static byte[] BuildDraftDocx(string title, string org, string markdownDraft)
{
    using var ms = new MemoryStream();
    using var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true);

    var mainPart = doc.AddMainDocumentPart();
    AddStylesPart(mainPart);
    mainPart.Document = new Document();
    var body = new Body();
    mainPart.Document.AppendChild(body);

    // Header
    body.AppendChild(StyledParagraph("Heading1", title));
    if (!string.IsNullOrWhiteSpace(org))
        body.AppendChild(StyledParagraph("Normal", org));

    // Draft notice
    var notice = new Paragraph(new Run(
        new RunProperties(new Italic(), new Color { Val = "888888" }),
        new Text("AI Draft — For Review and Editing")
    ));
    body.AppendChild(notice);
    body.AppendChild(new Paragraph());

    // Parse markdown into Word paragraphs
    AppendMarkdownContent(body, markdownDraft);

    doc.Save();
    return ms.ToArray();
}

private static void AppendMarkdownContent(Body body, string markdown)
{
    foreach (var rawLine in markdown.Split('\n'))
    {
        var line = rawLine.TrimEnd();

        if (line.StartsWith("## "))
            body.AppendChild(StyledParagraph("Heading2", line[3..].Trim()));
        else if (line.StartsWith("### "))
            body.AppendChild(StyledParagraph("Heading3", line[4..].Trim()));
        else if (line.Length >= 2 && line.TrimStart()[0] == '*' && char.IsWhiteSpace(line.TrimStart()[1]))
            body.AppendChild(NormalParagraph("• " + line.TrimStart().Substring(1).TrimStart()));
        else if (string.IsNullOrWhiteSpace(line))
            body.AppendChild(new Paragraph());
        else
            body.AppendChild(NormalParagraph(line));
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs
git commit -m "Add ExportDraftToWord tool — markdown draft to editable Word doc"
```

---

## Task 5: Add ExportPositionsToExcel to ExportTools.cs

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs`

- [ ] **Step 1: Add Excel using directives**

Add to the top of `ExportTools.cs`:
```csharp
using DocumentFormat.OpenXml.Spreadsheet;
```

- [ ] **Step 2: Add ExportPositionsToExcel tool method**

Insert after `ExportDraftToWord` method:

```csharp
[McpServerTool(Name = "ExportPositionsToExcel"),
 Description("Exports all open positions to an Excel (.xlsx) spreadsheet. Returns a JSON payload with fileName and base64-encoded content for the agent to save locally.")]
public async Task<string> ExportPositionsToExcel(CancellationToken ct = default)
{
    logger.LogInformation("[Request ] ExportPositionsToExcel");
    var list = await positions.GetOpenPositionsAsync(ct);
    var positionList = list.ToList();

    var bytes = BuildPositionsExcel(positionList);
    var result = ToBase64Json("positions.xlsx", bytes);
    logger.LogInformation("[Response] ExportPositionsToExcel => {Count} rows, {Bytes} bytes", positionList.Count, bytes.Length);
    return result;
}
```

- [ ] **Step 3: Add BuildPositionsExcel private method**

Insert after `AppendMarkdownContent`:

```csharp
private static byte[] BuildPositionsExcel(IEnumerable<HrMcp.Core.Entities.Position> positionList)
{
    using var ms = new MemoryStream();
    using var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook, true);

    var workbookPart = doc.AddWorkbookPart();
    workbookPart.Workbook = new Workbook();

    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
    var sheetData = new SheetData();
    worksheetPart.Worksheet = new Worksheet(sheetData);

    var sheets = workbookPart.Workbook.AppendChild(new Sheets());
    sheets.AppendChild(new Sheet
    {
        Id = workbookPart.GetIdOfPart(worksheetPart),
        SheetId = 1,
        Name = "Open Positions"
    });

    string[] headers =
    [
        "ID", "Title", "Series", "Pay Grade",
        "Salary Min", "Salary Max", "Location",
        "Telework", "Security Clearance", "Who May Apply",
        "Department", "Organization", "Open Date", "Close Date"
    ];

    sheetData.AppendChild(ExcelRow(1, headers));

    uint rowIndex = 2;
    foreach (var p in positionList)
    {
        var rem = p.PositionRemuneration;
        var grade = p.PayGradeMin == p.PayGradeMax ? p.PayGradeMin : $"{p.PayGradeMin}-{p.PayGradeMax}";
        sheetData.AppendChild(ExcelRow(rowIndex++,
        [
            p.Id.ToString(),
            p.Title ?? "",
            p.OccupationalSeries ?? "",
            grade,
            rem?.MinimumRange.ToString("N0") ?? "",
            rem?.MaximumRange.ToString("N0") ?? "",
            p.DutyLocation ?? "",
            p.TeleworkEligible ? "Yes" : "No",
            p.SecurityClearance.ToString(),
            p.WhoMayApply ?? "",
            p.HiringOrganization?.DepartmentName ?? "",
            p.HiringOrganization?.OrganizationName ?? "",
            p.OpenDate.ToString("yyyy-MM-dd"),
            p.CloseDate?.ToString("yyyy-MM-dd") ?? ""
        ]));
    }

    workbookPart.Workbook.Save();
    return ms.ToArray();
}

private static Row ExcelRow(uint rowIndex, string[] values)
{
    var row = new Row { RowIndex = rowIndex };
    for (var i = 0; i < values.Length; i++)
    {
        var colLetter = ((char)('A' + i)).ToString();
        var cell = new Cell
        {
            CellReference = $"{colLetter}{rowIndex}",
            DataType = CellValues.String,
            CellValue = new CellValue(values[i])
        };
        row.AppendChild(cell);
    }
    return row;
}
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs
git commit -m "Add ExportPositionsToExcel tool — open positions to .xlsx"
```

---

## Task 6: Register ExportTools in McpServer Program.cs

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/Program.cs`

- [ ] **Step 1: Add .WithTools<ExportTools>() to both host registrations**

Find both occurrences of `.WithTools<JobDescriptionTools>()` (one in the stdio host builder, one in the web app builder) and add `.WithTools<ExportTools>()` after each:

```csharp
// Both occurrences — stdio and stream-http
hostBuilder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<JobDescriptionTools>()
    .WithTools<ExportTools>()          // ← add this line
    .WithStdioServerTransport();

// and in the web app:
builder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<JobDescriptionTools>()
    .WithTools<ExportTools>()          // ← add this line
    .WithHttpTransport();
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/Program.cs
git commit -m "Register ExportTools in McpServer"
```

---

## Task 7: Add Agent-Side Export Interception to HrAgent.cs

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs`

- [ ] **Step 1: Add using directives**

Add to the top of `HrAgent.cs`:
```csharp
using System.Text.Json;
```

- [ ] **Step 2: Add ExportToolNames set and _outputFolder field**

After the `_history` field declaration, add:

```csharp
// Tools that return { "fileName": "...", "content": "<base64>" } — agent saves the file
private static readonly HashSet<string> ExportToolNames =
    new(StringComparer.OrdinalIgnoreCase)
    {
        "ExportPositionToHtml",
        "ExportPositionToWord",
        "ExportDraftToWord",
        "ExportPositionsToExcel"
    };

private readonly string _outputFolder;
```

- [ ] **Step 3: Update constructor signature to accept outputFolder**

Change:
```csharp
public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools, UiStyle style = UiStyle.Structured, int? numCtx = null)
```

To:
```csharp
public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools, UiStyle style = UiStyle.Structured, int? numCtx = null, string outputFolder = "usajobs/output")
```

And initialize the field by adding this line at the start of any instance method, or better: add a primary constructor body initializer. Since C# primary constructors don't have bodies, assign it in the field declaration:

Replace the field declaration with:
```csharp
private readonly string _outputFolder = outputFolder;
```

- [ ] **Step 4: Add interception logic in RunToolLoopAsync**

In `RunToolLoopAsync`, after `rawResult` is set (after the `try/catch` block that invokes the tool), add the interception before the `_history.Add(...)` call:

```csharp
// Intercept export tool results — decode base64 and save to output folder
if (ExportToolNames.Contains(call.Name ?? string.Empty))
{
    var json = rawResult switch
    {
        string s => s,
        JsonElement je => je.GetRawText(),
        _ => JsonSerializer.Serialize(rawResult)
    };
    var saved = TrySaveExportFile(json, _outputFolder);
    if (saved is not null) rawResult = saved;
}

_history.Add(new ChatMessage(ChatRole.Tool,
    [new FunctionResultContent(call.CallId ?? string.Empty, rawResult)]));
```

- [ ] **Step 5: Add TrySaveExportFile helper method**

Add in the `// ── Helpers ──` section:

```csharp
// Decodes a base64 export payload and saves the file to outputFolder.
// Returns "Saved to: <path>" on success, null if the payload is not an export result.
private static string? TrySaveExportFile(string json, string outputFolder)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("fileName", out var fileNameEl) ||
            !root.TryGetProperty("content", out var contentEl))
            return null;

        var fileName = fileNameEl.GetString();
        var base64   = contentEl.GetString();
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(base64))
            return null;

        var bytes = Convert.FromBase64String(base64);
        Directory.CreateDirectory(outputFolder);
        var fullPath = Path.GetFullPath(Path.Combine(outputFolder, fileName));
        File.WriteAllBytes(fullPath, bytes);
        return $"Saved to: {fullPath}";
    }
    catch
    {
        return null;
    }
}
```

- [ ] **Step 6: Build to verify**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs
git commit -m "Add agent-side export interception — decode base64 and save to usajobs/output"
```

---

## Task 8: Wire Output Folder and Update System Prompt

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs`

- [ ] **Step 1: Pass outputFolder from Program.cs**

In `Program.cs`, replace:
```csharp
var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style, numCtx);
```

With:
```csharp
var outputFolder = Path.Combine(FindWorkspaceRoot(), "DotnetAiAgentMcp", "usajobs", "output");
var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style, numCtx, outputFolder);
```

- [ ] **Step 2: Update system prompt in HrAgent.cs**

In the `SystemPrompt` constant, replace:
```
- Keep answers concise; offer to go deeper when asked.
- Never present a numbered menu of options or ask the user what they want to do.
  Respond directly to what the user said, or call a tool immediately.
```

With:
```
- Keep answers concise; offer to go deeper when asked.
- Never present a numbered menu of options or ask the user what they want to do.
  Respond directly to what the user said, or call a tool immediately.
- To export a position's full structured data, call ExportPositionToHtml(positionId) or ExportPositionToWord(positionId).
- To export an AI-generated job description draft to Word, call ExportDraftToWord(positionId, draftContent)
  passing the full current draft text including any edits the user has made.
- To export all open positions to Excel, call ExportPositionsToExcel().
```

- [ ] **Step 3: Build full solution**

```bash
dotnet build DotnetAiAgentMcp/DotnetAiAgentMcp.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs
git commit -m "Wire output folder and update system prompt with export tool guidance"
```

---

## Task 9: Integration Test

- [ ] **Step 1: Start the agent in stdio mode**

```bash
cd c:/apps/DotnetMcpTutorial
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent
```

- [ ] **Step 2: Test ExportPositionToHtml**

Type: `export position 41 as html`

Expected: Agent reports `Saved to: ...\usajobs\output\position-41.html`

Verify: Open the file in a browser — should display the USAJobs-style page.

- [ ] **Step 3: Test ExportPositionToWord**

Type: `export position 41 to word`

Expected: Agent reports `Saved to: ...\usajobs\output\position-41.docx`

Verify: Open in Microsoft Word — should show title, overview table, and all sections.

- [ ] **Step 4: Test ExportDraftToWord**

Type: `write a job description for position 41`

Wait for the draft to appear, then type: `export the draft to word`

Expected: Agent reports `Saved to: ...\usajobs\output\position-41-draft.docx`

Verify: Open in Microsoft Word — should show title, "AI Draft — For Review and Editing" notice, and draft sections as Word headings.

- [ ] **Step 5: Test user edits then export**

After the draft is shown, type: `update the qualifications to also require a PMP certification`

After the LLM updates and shows the revised draft, type: `export the draft to word`

Expected: New `position-41-draft.docx` reflects the PMP requirement.

- [ ] **Step 6: Test ExportPositionsToExcel**

Type: `export all open positions to excel`

Expected: Agent reports `Saved to: ...\usajobs\output\positions.xlsx`

Verify: Open in Excel — should show header row (ID, Title, Series, etc.) and one data row per open position.

- [ ] **Step 7: Final commit**

```bash
git add -A
git commit -m "Export tools integration verified — HTML, Word, draft Word, Excel all working"
```
