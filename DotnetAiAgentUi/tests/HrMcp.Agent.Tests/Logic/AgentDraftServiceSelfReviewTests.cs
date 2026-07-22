using HrMcp.Agent.Web.Services;
using Xunit;

namespace HrMcp.Agent.Tests.Logic;

public sealed class AgentDraftServiceSelfReviewTests
{
    [Fact]
    public void BuildSelfReviewPrompt_ContainsDraftMarkdown()
    {
        var result = AgentDraftService.BuildSelfReviewPrompt("## Job Title\n\nSome duties.");
        Assert.Contains("## Job Title", result);
    }

    [Fact]
    public void BuildSelfReviewPrompt_ContainsOpmComplianceLens()
    {
        var result = AgentDraftService.BuildSelfReviewPrompt("some draft");
        Assert.Contains("OPM Compliance", result);
    }

    [Fact]
    public void BuildSelfReviewPrompt_ContainsCompletenessLens()
    {
        var result = AgentDraftService.BuildSelfReviewPrompt("some draft");
        Assert.Contains("Completeness", result);
    }

    [Fact]
    public void BuildSelfReviewPrompt_ContainsCandidateAppealLens()
    {
        var result = AgentDraftService.BuildSelfReviewPrompt("some draft");
        Assert.Contains("Candidate Appeal", result);
    }

    [Fact]
    public void BuildSelfReviewPrompt_ContainsSeverityMarkers()
    {
        var result = AgentDraftService.BuildSelfReviewPrompt("some draft");
        Assert.Contains("🔴", result);
        Assert.Contains("🟡", result);
        Assert.Contains("🟢", result);
    }

    [Fact]
    public void BuildSelfReviewPrompt_ContainsCallToAction()
    {
        var result = AgentDraftService.BuildSelfReviewPrompt("some draft");
        Assert.Contains("What would you like to address first?", result);
    }

    [Fact]
    public void BuildSelfReviewPrompt_ContainsProhibitedLanguageLens()
    {
        var result = AgentDraftService.BuildSelfReviewPrompt("some draft");
        Assert.Contains("Prohibited Language", result);
    }
}
