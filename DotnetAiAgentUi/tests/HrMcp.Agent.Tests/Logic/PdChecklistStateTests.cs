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
        Assert.True(state.Acknowledgments[0].AcknowledgedAt != default);
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
