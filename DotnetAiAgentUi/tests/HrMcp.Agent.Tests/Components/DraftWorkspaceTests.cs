using Bunit;
using HrMcp.Agent.Components.Pages;
using HrMcp.Agent.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrMcp.Agent.Tests.Components;

public sealed class DraftWorkspaceTests : TestContext
{
    private sealed class FakeAgentDraftService : IAgentDraftService
    {
        public string NextResponse { get; set; } = "Hello from assistant";

        public Task<string> SendPromptAsync(string prompt, CancellationToken ct = default) =>
            Task.FromResult(NextResponse);

        public Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(
            string draftText, CancellationToken ct = default) =>
            Task.FromResult(("ok", (string?)null, (byte[]?)null));
    }

    private readonly FakeAgentDraftService _fake = new();

    public DraftWorkspaceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<IAgentDraftService>(_ => _fake);
    }

    [Fact]
    public void Renders_EmptyState_Placeholder()
    {
        var cut = RenderComponent<DraftWorkspace>();
        var placeholder = cut.Find("textarea").GetAttribute("placeholder") ?? string.Empty;
        Assert.Contains("Message the assistant", placeholder);
    }

    [Fact]
    public void Renders_EmptyState_NoBubbles()
    {
        var cut = RenderComponent<DraftWorkspace>();
        Assert.Empty(cut.FindAll(".chat-bubble"));
    }

    [Fact]
    public void ChatThread_HasCorrectId()
    {
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("#chat-thread");
    }

    [Fact]
    public void UserBubble_AlignsRight()
    {
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("textarea").Input("hello");
        cut.Find("button.primary-btn").Click();
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".chat-bubble-row--user")));
    }

    [Fact]
    public void AssistantBubble_AlignsLeft()
    {
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("textarea").Input("hello");
        cut.Find("button.primary-btn").Click();
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".chat-bubble-row--assistant")));
    }

    [Fact]
    public void NoRoleLabels_InBubbles()
    {
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("textarea").Input("hello");
        cut.Find("button.primary-btn").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll(".chat-bubble"));
            Assert.Empty(cut.FindAll(".chat-bubble strong"));
        });
    }
}
