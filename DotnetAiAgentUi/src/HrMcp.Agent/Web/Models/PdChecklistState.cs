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

    // Parses the markdown draft headings and marks detected sections as Complete or Warning
    // based on whether the body content meets the depth threshold for that section.
    // Locked sections always stay AutoFilled regardless of draft content.
    public void UpdateFromDraft(string draftMarkdown)
    {
        if (string.IsNullOrWhiteSpace(draftMarkdown))
            return;

        var lines = draftMarkdown
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Build a map of normalized heading text → body lines beneath it.
        // Body lines are everything between this heading and the next ## heading.
        var bodyMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentHeading = null;
        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
            {
                currentHeading = line.TrimStart('#').Trim().ToLowerInvariant();
                bodyMap[currentHeading] = [];
            }
            else if (currentHeading is not null)
            {
                bodyMap[currentHeading].Add(line);
            }
        }

        var headings = bodyMap.Keys.ToList();

        foreach (var section in _sections)
        {
            if (section.IsLocked) continue;

            var keywords = SectionKeywords(section.Name);
            var matchedHeading = headings.FirstOrDefault(h =>
                keywords.Any(k => h.Contains(k, StringComparison.Ordinal)));

            if (matchedHeading is null) continue;

            var body = bodyMap[matchedHeading];
            section.Status = MeetsDepthThreshold(section.Name, body)
                ? PdSectionStatus.Complete
                : PdSectionStatus.Warning;
        }
    }

    private static bool MeetsDepthThreshold(string sectionName, List<string> bodyLines)
    {
        return sectionName switch
        {
            "Major Duties" =>
                bodyLines.Count(l => l.TrimStart().StartsWith("- ", StringComparison.Ordinal)
                                  || l.TrimStart().StartsWith("* ", StringComparison.Ordinal)) >= 5,

            "Position Summary" =>
                bodyLines.Count(l =>
                {
                    var t = l.TrimEnd();
                    return t.EndsWith('.') || t.EndsWith('!') || t.EndsWith('?');
                }) >= 3,

            _ => bodyLines.Any(l => !string.IsNullOrWhiteSpace(l))
        };
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
        "Position Title"            => ["position title", "job title"],
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
