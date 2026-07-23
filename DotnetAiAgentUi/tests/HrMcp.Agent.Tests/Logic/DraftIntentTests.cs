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

    [Theory]
    [InlineData("Do you want me to revise this?")]
    [InlineData("Would you like any changes?")]
    [InlineData("Let me know if this works.")]
    [InlineData("Feel free to ask for more.")]
    [InlineData("Please let me know your thoughts.")]
    [InlineData("If you need anything, just ask.")]
    [InlineData("")]
    [InlineData("   ")]
    // Mid-sentence question mark (e.g. "Next follow-up: Is this remote? If so, specify.")
    [InlineData("Next follow-up: Is this position eligible for remote work? If so, please specify.")]
    [InlineData("Next question: Is this a supervisory role? Please clarify.")]
    [InlineData("Follow-up: What clearance level is required? Provide details.")]
    public void IsClosingLine_ReturnsTrue(string line) =>
        Assert.True(DraftWorkspace.IsClosingLine(line));

    [Fact]
    public void IsClosingLine_ContentLine_ReturnsFalse() =>
        Assert.False(DraftWorkspace.IsClosingLine("## Summary"));

    [Theory]
    [InlineData("help")]
    [InlineData("how do I use this")]
    [InlineData("what do I do")]
    [InlineData("how do I start")]
    [InlineData("what is a GS series code")]
    [InlineData("what does the checklist mean")]
    [InlineData("what is OPM")]
    public void HelpPhrases_AreNotDraftIntentPrompts(string input)
    {
        Assert.False(DraftWorkspace.IsDraftIntentPrompt(input));
    }

    // New terms for extended draft-intent detection
    [Theory]
    [InlineData("I need a GS-13 cloud architect")]
    [InlineData("create a position for a software engineer")]
    [InlineData("we're hiring a data scientist at grade 12")]
    [InlineData("the title should be IT Specialist")]
    [InlineData("the series is 2210")]
    [InlineData("duties include leading the cloud migration")]
    [InlineData("clearance is required for this role")]
    [InlineData("the position is remote eligible")]
    [InlineData("education requirement is a bachelor's degree")]
    [InlineData("this will be a supervisory position")]
    public void IsDraftIntentPrompt_ExtendedTerms_ReturnsTrue(string prompt) =>
        Assert.True(DraftWorkspace.IsDraftIntentPrompt(prompt));

    [Fact]
    public void TryExtractSeriesSuggestion_WithSeriesSuggestionPhrase_ReturnsParsedValues()
    {
        const string response = "Series Suggestion: The duties align more closely with GS-0343 (Management & Program Analysis) than GS-2210.";
        var result = DraftWorkspace.TryExtractSeriesSuggestion(response);
        Assert.NotNull(result);
        Assert.Equal("GS-0343", result!.Value.Suggested);
        Assert.Equal("Management & Program Analysis", result.Value.SuggestedName);
        Assert.Equal("GS-2210", result.Value.Current);
    }

    [Fact]
    public void TryExtractSeriesSuggestion_WithoutPhrase_ReturnsNull()
    {
        const string response = "Here is your draft. ## Position Summary\n\nThis position...";
        var result = DraftWorkspace.TryExtractSeriesSuggestion(response);
        Assert.Null(result);
    }

    [Fact]
    public void GetDraftSectionNames_ReturnsSectionHeadings()
    {
        const string draft = "# IT Specialist\n\n## Position Info\n\nText.\n\n## Major Duties\n\nMore text.";
        var result = DraftWorkspace.GetDraftSectionNames(draft);
        Assert.Equal(["Position Info", "Major Duties"], result);
    }

    [Fact]
    public void GetDraftSectionNames_IgnoresH1AndH3()
    {
        const string draft = "# Title\n\n## Section A\n\n### Sub\n\n## Section B";
        var result = DraftWorkspace.GetDraftSectionNames(draft);
        Assert.Equal(["Section A", "Section B"], result);
    }

    [Fact]
    public void GetDraftSectionNames_EmptyDraft_ReturnsEmpty()
    {
        var result = DraftWorkspace.GetDraftSectionNames("No headings here.");
        Assert.Empty(result);
    }

    [Fact]
    public void GetDraftSectionNames_HandlesCrLf()
    {
        const string draft = "# Title\r\n\r\n## Section A\r\n\r\nText.";
        var result = DraftWorkspace.GetDraftSectionNames(draft);
        Assert.Equal(["Section A"], result);
    }

    [Fact]
    public void ExtractPositionTitle_ReturnsH1Text()
    {
        const string draft = "# IT Specialist, GS-2210-14\n\n## Position Info\n\nText.";
        var result = DraftWorkspace.ExtractPositionTitle(draft);
        Assert.Equal("IT Specialist, GS-2210-14", result);
    }

    [Fact]
    public void ExtractPositionTitle_NoH1_ReturnsNull()
    {
        const string draft = "## Position Info\n\nText.";
        var result = DraftWorkspace.ExtractPositionTitle(draft);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractPositionTitle_DoesNotReturnH2()
    {
        const string draft = "## Major Duties\n\nText.";
        var result = DraftWorkspace.ExtractPositionTitle(draft);
        Assert.Null(result);
    }

    [Fact]
    public void BuildChatSummary_FirstDraft_ContainsCreatedAndTitle()
    {
        const string draft = "# IT Specialist\n\n## Position Info\n\nText.\n\n## Major Duties\n\nDuties.";
        var result = DraftWorkspace.BuildChatSummary(null, draft);
        Assert.Contains("Draft created", result);
        Assert.Contains("IT Specialist", result);
        Assert.Contains("Position Info", result);
        Assert.Contains("Major Duties", result);
    }

    [Fact]
    public void BuildChatSummary_FirstDraft_NoTitle_OmitsTitleLine()
    {
        const string draft = "## Position Info\n\nText.";
        var result = DraftWorkspace.BuildChatSummary(null, draft);
        Assert.Contains("Draft created", result);
        Assert.DoesNotContain(" \u2014 ", result);
    }

    [Fact]
    public void BuildChatSummary_UpdatedDraft_ContainsUpdatedAndChanges()
    {
        const string previous = "# IT Specialist\n\n## Position Info\n\nOld text.";
        const string updated = "# IT Specialist\n\n## Position Info\n\nNew text.\n\n## Major Duties\n\nDuties.";
        var result = DraftWorkspace.BuildChatSummary(previous, updated);
        Assert.Contains("Draft updated", result);
        Assert.Contains("Added", result);
        Assert.Contains("Major Duties", result);
        Assert.Contains("Revised", result);
        Assert.Contains("Position Info", result);
    }

    [Fact]
    public void BuildChatSummary_UpdatedDraft_AllNewSections_NoRevisedLine()
    {
        const string previous = "## Section A\n\nText.";
        const string updated = "## Section B\n\nText.\n\n## Section C\n\nMore.";
        var result = DraftWorkspace.BuildChatSummary(previous, updated);
        Assert.Contains("Added", result);
        Assert.Contains("Section B", result);
        Assert.DoesNotContain("Revised", result);
    }

    [Theory]
    [InlineData("browse pd")]
    [InlineData("browse pds")]
    [InlineData("browse position")]
    [InlineData("browse positions")]
    [InlineData("list pd")]
    [InlineData("list pds")]
    [InlineData("list position")]
    [InlineData("list positions")]
    [InlineData("show pd")]
    [InlineData("show pds")]
    [InlineData("show positions")]
    [InlineData("find pd")]
    [InlineData("find pds")]
    [InlineData("search pd")]
    [InlineData("search pds")]
    [InlineData("search positions")]
    [InlineData("use existing")]
    [InlineData("start from existing")]
    [InlineData("copy existing")]
    [InlineData("existing pd")]
    [InlineData("existing pds")]
    public void BrowsePhrases_AreBrowseIntentPrompts(string input)
    {
        Assert.True(DraftWorkspace.IsBrowseIntent(input));
    }

    [Theory]
    [InlineData("browse pd")]
    [InlineData("browse pds")]
    [InlineData("browse position")]
    [InlineData("browse positions")]
    [InlineData("list pd")]
    [InlineData("list pds")]
    [InlineData("list position")]
    [InlineData("list positions")]
    [InlineData("show pd")]
    [InlineData("show pds")]
    [InlineData("show positions")]
    [InlineData("find pd")]
    [InlineData("find pds")]
    [InlineData("search pd")]
    [InlineData("search pds")]
    [InlineData("search positions")]
    [InlineData("use existing")]
    [InlineData("start from existing")]
    [InlineData("copy existing")]
    [InlineData("existing pd")]
    [InlineData("existing pds")]
    public void BrowsePhrases_AreNotDraftIntentPrompts(string input)
    {
        Assert.False(DraftWorkspace.IsDraftIntentPrompt(input));
    }
}
