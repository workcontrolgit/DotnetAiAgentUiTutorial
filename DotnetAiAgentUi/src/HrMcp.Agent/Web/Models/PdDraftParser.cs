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
