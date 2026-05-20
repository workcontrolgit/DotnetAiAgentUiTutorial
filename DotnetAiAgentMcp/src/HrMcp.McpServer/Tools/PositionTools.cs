// src/HrMcp.McpServer/Tools/PositionTools.cs
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HrMcp.Application.Services;
using HrMcp.Core.Entities;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public sealed class PositionTools(PositionService positions)
{
    [McpServerTool(Name = "GetOpenPositions"),
     Description("Returns all currently open federal job positions including title, pay grade, duty location, and security clearance requirements.")]
    public async Task<IEnumerable<object>> GetOpenPositions(CancellationToken ct = default)
    {
        var list = await positions.GetOpenPositionsAsync(ct);
        return list.Select(p => (object)new
        {
            p.Id,
            p.Title,
            p.Description,
            p.OccupationalSeries,
            p.PayGradeMin,
            p.PayGradeMax,
            MinimumRange     = p.PositionRemuneration?.MinimumRange,
            MaximumRange     = p.PositionRemuneration?.MaximumRange,
            RateIntervalCode = p.PositionRemuneration?.RateIntervalCode,
            p.DutyLocation,
            p.TeleworkEligible,
            SecurityClearance  = p.SecurityClearance.ToString(),
            p.WhoMayApply,
            OrganizationName   = p.HiringOrganization?.OrganizationName,
            DepartmentName     = p.HiringOrganization?.DepartmentName
        });
    }

    [McpServerTool(Name = "GetPositionById"),
     Description("Returns full details for a specific position by its ID, including duties, qualifications, and pay information.")]
    public async Task<object?> GetPositionById(
        [Description("The numeric ID of the position to retrieve")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return null;

        return new
        {
            p.Id,
            p.AnnouncementNumber,
            p.UsaJobsId,
            p.PositionUri,
            p.ApplyUri,
            p.Title,
            p.Description,
            p.Duties,
            p.Qualifications,
            p.Education,
            p.Evaluations,
            p.KeyRequirements,
            p.PromotionPotential,
            p.OccupationalSeries,
            p.OccupationalSeriesTitle,
            p.PayGradeMin,
            p.PayGradeMax,
            MinimumRange            = p.PositionRemuneration?.MinimumRange,
            MaximumRange            = p.PositionRemuneration?.MaximumRange,
            RateIntervalCode        = p.PositionRemuneration?.RateIntervalCode,
            p.IsOpen,
            p.DutyLocation,
            p.DutyLocationState,
            p.TeleworkEligible,
            SecurityClearance       = p.SecurityClearance.ToString(),
            TravelRequired          = p.TravelRequired.ToString(),
            AppointmentType         = p.AppointmentType.ToString(),
            p.PositionOfferingType,
            WorkSchedule            = p.WorkSchedule.ToString(),
            p.WhoMayApply,
            p.HiringPath,
            p.ServiceType,
            p.SubAgencyName,
            p.TotalOpenings,
            p.AdjudicationType,
            p.RemoteEligible,
            p.FinancialDisclosure,
            p.SupervisoryStatus,
            p.RelocationAuthorized,
            p.DrugTestRequired,
            OpenDate                = p.OpenDate.ToString("yyyy-MM-dd"),
            CloseDate               = p.CloseDate?.ToString("yyyy-MM-dd"),
            OrganizationName        = p.HiringOrganization?.OrganizationName,
            DepartmentName          = p.HiringOrganization?.DepartmentName,
            p.PositionSensitivityAndRisk,
            p.ContactName,
            p.ContactPhone,
            p.ContactEmail,
            p.ContactAddress,
            p.ConditionsOfEmployment,
            p.RequiredDocuments,
            p.HowToApply,
            p.NextSteps,
            p.AdditionalInformation
        };
    }

    [McpServerTool(Name = "RenderPositionAsUsaJobsHtml"),
     Description("Renders a position as a USAJobs-style HTML page and saves it to disk. Returns the output file path. Use when the user asks to display a position in USAJobs format.")]
    public async Task<string> RenderPositionAsUsaJobsHtml(
        [Description("The numeric ID of the position to render")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return $"Position {positionId} not found.";

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
            .Replace("{{TITLE}}",                  Encode(p.Title))
            .Replace("{{DEPARTMENT_NAME}}",         Encode(p.HiringOrganization?.DepartmentName ?? ""))
            .Replace("{{ORGANIZATION_NAME}}",       Encode(p.HiringOrganization?.OrganizationName ?? ""))
            .Replace("{{STATUS_CLASS}}",            p.IsOpen ? "open" : "closed")
            .Replace("{{STATUS_TEXT}}",             p.IsOpen ? "Accepting applications" : "Closed")
            .Replace("{{OPEN_DATE}}",               p.OpenDate.ToString("MM/dd/yyyy"))
            .Replace("{{CLOSE_DATE}}",              p.CloseDate?.ToString("MM/dd/yyyy") ?? "Open until filled")
            .Replace("{{SALARY_MIN}}",              minSalary)
            .Replace("{{SALARY_MAX}}",              maxSalary)
            .Replace("{{PAY_GRADE}}",               Encode(payGrade))
            .Replace("{{DUTY_LOCATION}}",           Encode(p.DutyLocation))
            .Replace("{{TOTAL_OPENINGS}}",          string.IsNullOrWhiteSpace(p.TotalOpenings) ? "1 vacancy" : $"{p.TotalOpenings} vacancies")
            .Replace("{{REMOTE_ELIGIBLE}}",         p.RemoteEligible ? "Yes" : "No")
            .Replace("{{TELEWORK_ELIGIBLE}}",       p.TeleworkEligible ? "Yes — Per Agency Policy" : "No")
            .Replace("{{TRAVEL_REQUIRED}}",         Encode(p.TravelRequired.ToString()))
            .Replace("{{RELOCATION_AUTHORIZED}}",   p.RelocationAuthorized ? "Yes" : "No")
            .Replace("{{APPOINTMENT_TYPE}}",        Encode(p.AppointmentType.ToString()))
            .Replace("{{WORK_SCHEDULE}}",           Encode(p.WorkSchedule.ToString()))
            .Replace("{{SERVICE_TYPE}}",            Encode(p.ServiceType))
            .Replace("{{PROMOTION_POTENTIAL}}",     Encode(p.PromotionPotential))
            .Replace("{{OCCUPATIONAL_SERIES}}",     Encode(p.OccupationalSeries))
            .Replace("{{OCCUPATIONAL_SERIES_TITLE}}", Encode(p.OccupationalSeriesTitle))
            .Replace("{{SUPERVISORY_STATUS}}",      p.SupervisoryStatus ? "Yes" : "No")
            .Replace("{{SECURITY_CLEARANCE}}",      Encode(p.SecurityClearance.ToString()))
            .Replace("{{DRUG_TEST_REQUIRED}}",      p.DrugTestRequired ? "Yes" : "No")
            .Replace("{{ADJUDICATION_TYPE}}",       adjudicationHtml)
            .Replace("{{ANNOUNCEMENT_NUMBER}}",     Encode(p.AnnouncementNumber))
            .Replace("{{USAJOBS_ID}}",              Encode(p.UsaJobsId))
            .Replace("{{CONTACT_NAME}}",            Encode(p.ContactName))
            .Replace("{{CONTACT_PHONE}}",           Encode(p.ContactPhone.Trim()))
            .Replace("{{CONTACT_EMAIL}}",           Encode(p.ContactEmail))
            .Replace("{{CONTACT_ADDRESS}}",         Encode(p.ContactAddress).Replace("\n", "<br/>"))
            .Replace("{{HIRING_PATHS_HTML}}",       hiringPathsHtml)
            .Replace("{{DESCRIPTION_HTML}}",        TextToHtml(p.Description))
            .Replace("{{DUTIES_HTML}}",             TextToHtml(p.Duties))
            .Replace("{{CONDITIONS_HTML}}",         TextToHtml(p.ConditionsOfEmployment))
            .Replace("{{QUALIFICATIONS_HTML}}",     TextToHtml(p.Qualifications))
            .Replace("{{EDUCATION_HTML}}",          TextToHtml(p.Education))
            .Replace("{{ADDITIONAL_INFO_HTML}}",    TextToHtml(p.AdditionalInformation))
            .Replace("{{EVALUATIONS_HTML}}",        TextToHtml(p.Evaluations))
            .Replace("{{REQUIRED_DOCUMENTS_HTML}}", TextToHtml(p.RequiredDocuments))
            .Replace("{{HOW_TO_APPLY_HTML}}",       TextToHtml(p.HowToApply))
            .Replace("{{NEXT_STEPS_HTML}}",         TextToHtml(p.NextSteps));

        var outputDir = Path.Combine(Path.GetDirectoryName(templatePath)!, "..", "output");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.GetFullPath(Path.Combine(outputDir, $"position-{positionId}.html"));
        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8, ct);

        return $"Rendered to: {outputPath}";
    }

    private static readonly Regex SectionHeaderRegex = new(
        @"(?:^|(?<=\.\s+))([A-Z][a-zA-Z]+(?: [A-Z][a-zA-Z]+){0,3}): ",
        RegexOptions.Compiled);

    private static readonly Regex SentenceSplitRegex = new(
        @"(?<=[.!?])\s+",
        RegexOptions.Compiled);

    private static readonly Regex BulletRegex = new(
        @"^\d+\.\s",
        RegexOptions.Compiled);

    // Converts plain text to HTML paragraphs, bullet lists, and section headers.
    private static string TextToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "<p>N/A</p>";

        var normalized = text.Replace("\r\n", "\n").Trim();

        if (normalized.Contains('\n'))
        {
            var lines = normalized.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();
            return ProcessLines(lines);
        }

        return ParseSingleBlock(normalized);
    }

    private static string ParseSingleBlock(string text)
    {
        var sb = new StringBuilder();
        int lastIndex = 0;
        string? pendingHeader = null;

        foreach (Match m in SectionHeaderRegex.Matches(text))
        {
            var before = text[lastIndex..m.Index].Trim();
            if (!string.IsNullOrWhiteSpace(before))
            {
                if (pendingHeader != null) { sb.Append($"<h3>{Encode(pendingHeader)}</h3>"); pendingHeader = null; }
                sb.Append(SentencesToBullets(before));
            }
            pendingHeader = m.Groups[1].Value;
            lastIndex = m.Index + m.Length;
        }

        var remaining = text[lastIndex..].Trim();
        if (pendingHeader != null) sb.Append($"<h3>{Encode(pendingHeader)}</h3>");

        if (!string.IsNullOrWhiteSpace(remaining))
            sb.Append(SentencesToBullets(remaining));
        else if (lastIndex == 0)
            sb.Append(SentencesToBullets(text));

        return sb.ToString();
    }

    private static string SentencesToBullets(string text)
    {
        var sentences = SentenceSplitRegex.Split(text.Trim())
            .Select(s => s.Trim())
            .Where(s => s.Length > 10)
            .ToList();

        if (sentences.Count == 0) return "";
        if (sentences.Count == 1) return $"<p>{Encode(sentences[0])}</p>";

        var sb = new StringBuilder("<ul>");
        foreach (var s in sentences)
            sb.Append($"<li>{Encode(s)}</li>");
        sb.Append("</ul>");
        return sb.ToString();
    }

    private static string ProcessLines(List<string> lines)
    {
        var sb = new StringBuilder();
        bool inList = false;

        foreach (var line in lines)
        {
            bool isBullet = line.StartsWith("•") || line.StartsWith("- ") ||
                            line.StartsWith("* ") || BulletRegex.IsMatch(line);
            bool isHeader = !isBullet && line.EndsWith(':') && line.Split(' ').Length <= 6;

            if (isHeader)
            {
                if (inList) { sb.Append("</ul>"); inList = false; }
                sb.Append($"<h3>{Encode(line.TrimEnd(':'))}</h3>");
            }
            else if (isBullet)
            {
                if (!inList) { sb.Append("<ul>"); inList = true; }
                var item = BulletRegex.Replace(line.TrimStart('•', '-', '*', ' '), "");
                sb.Append($"<li>{Encode(item.Trim())}</li>");
            }
            else
            {
                if (inList) { sb.Append("</ul>"); inList = false; }
                sb.Append($"<p>{Encode(line)}</p>");
            }
        }

        if (inList) sb.Append("</ul>");
        return sb.ToString();
    }

    private static string BuildHiringPathsHtml(string hiringPath)
    {
        var paths = hiringPath.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var path in paths)
        {
            var (icon, label, desc) = path.ToLower() switch
            {
                var s when s.Contains("public") =>
                    ("🌐", "The public", "U.S. Citizens, Nationals or those who owe allegiance to the U.S."),
                var s when s.Contains("fed-excepted") || s.Contains("excepted") =>
                    ("🏛", "Federal employees – Excepted service", "Current federal employees whose agencies have their own hiring rules, pay scales and evaluation criteria."),
                var s when s.Contains("fed-competitive") || s.Contains("competitive") =>
                    ("🏛", "Federal employees – Competitive service", "Current or former competitive service federal employees."),
                var s when s.Contains("veteran") =>
                    ("🎖", "Veterans", "Disabled veterans, veterans who served on active duty in the Armed Forces during a war, or in a campaign or expedition for which a campaign badge has been authorized."),
                var s when s.Contains("senior") || s.Contains("ses") =>
                    ("⭐", "Senior executives", "Those who meet the five Executive Core Qualifications (ECQs)."),
                _ => ("📋", path, "")
            };
            sb.Append($"""
                <div class="hiring-path">
                  <div style="font-size:22px">{icon}</div>
                  <div><h3>{Encode(label)}</h3><p>{Encode(desc)}</p></div>
                </div>
                """);
        }
        return sb.ToString();
    }

    private static string? FindLayoutFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string Encode(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);

    [McpServerTool(Name = "GetPositionsByOrganization"),
     Description("Returns all positions for a specific federal hiring organization. Use GetHiringOrganizations first to get valid organization IDs.")]
    public async Task<IEnumerable<object>> GetPositionsByOrganization(
        [Description("The numeric ID of the hiring organization")] int organizationId,
        CancellationToken ct = default)
    {
        var list = await positions.GetPositionsByOrganizationAsync(organizationId, ct);
        return list.Select(p => (object)new
        {
            p.Id,
            p.Title,
            p.Description,
            p.OccupationalSeries,
            p.PayGradeMin,
            p.PayGradeMax,
            p.IsOpen,
            p.DutyLocation,
            p.TeleworkEligible,
            SecurityClearance = p.SecurityClearance.ToString(),
            p.WhoMayApply
        });
    }
}
