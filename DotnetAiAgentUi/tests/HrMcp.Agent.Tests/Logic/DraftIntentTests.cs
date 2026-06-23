using HrMcp.Agent.Components.Pages;
using Xunit;

namespace HrMcp.Agent.Tests.Logic;

public sealed class DraftIntentTests
{
    [Fact]
    public void IsDraftIntentPrompt_Draft_ReturnsTrue() =>
        Assert.True(DraftWorkspace.IsDraftIntentPrompt("draft a PD for a software engineer"));

    [Fact]
    public void IsDraftIntentPrompt_Revise_ReturnsTrue() =>
        Assert.True(DraftWorkspace.IsDraftIntentPrompt("revise the qualifications section"));

    [Fact]
    public void IsDraftIntentPrompt_OpenPositions_ReturnsFalse() =>
        Assert.False(DraftWorkspace.IsDraftIntentPrompt("list open positions"));

    [Fact]
    public void IsDraftIntentPrompt_Empty_ReturnsFalse() =>
        Assert.False(DraftWorkspace.IsDraftIntentPrompt(""));

    [Fact]
    public void ExtractDraftMarkdown_WithHeading_ReturnsBody()
    {
        const string response = "Sure! Here is your draft.\n\n## Job Title\n\nThis is the body.\n";
        var result = DraftWorkspace.ExtractDraftMarkdown(response);
        Assert.NotNull(result);
        Assert.StartsWith("## Job Title", result);
        Assert.DoesNotContain("Sure!", result);
    }

    [Fact]
    public void ExtractDraftMarkdown_NoHeading_ReturnsNull()
    {
        var result = DraftWorkspace.ExtractDraftMarkdown("Sure, I can help with that.");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractDraftMarkdown_StripsClosingLines()
    {
        const string response = "## Summary\n\nThis is the body.\n\nLet me know if you'd like any changes.";
        var result = DraftWorkspace.ExtractDraftMarkdown(response);
        Assert.NotNull(result);
        Assert.DoesNotContain("Let me know", result);
        Assert.Contains("This is the body.", result);
    }
}
