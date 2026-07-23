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
