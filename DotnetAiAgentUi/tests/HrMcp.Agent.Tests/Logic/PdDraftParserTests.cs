using HrMcp.Agent.Web.Models;
using HrMcp.Agent.Components.Pages;
using System.Text.Json;
using Xunit;

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
