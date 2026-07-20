using Bunit;
using HrMcp.Agent.Components.Pages;
using HrMcp.Agent.Web.Services;
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

namespace HrMcp.Agent.Tests.Components;

public sealed class DraftWorkspaceTests : TestContext
{
    private sealed class FakeAgentDraftService : IAgentDraftService
    {
        public string NextResponse { get; set; } = "Hello from assistant";

        public Task<string> SendPromptAsync(string prompt, Guid? sessionId = null, CancellationToken ct = default) =>
            Task.FromResult(NextResponse);

        public Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(
            string draftText, CancellationToken ct = default) =>
            Task.FromResult(("ok", (string?)null, (byte[]?)null));
    }

    private sealed class FakeConversationService : IConversationService
    {
        public Task<IReadOnlyList<ConversationSession>> GetSessionsAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ConversationSession>>([]);

        public Task<ConversationSession> CreateSessionAsync(string userId, string firstPrompt, CancellationToken ct = default) =>
            Task.FromResult(new ConversationSession { Id = Guid.NewGuid(), UserId = userId, Name = firstPrompt });

        public Task<ConversationSession?> GetSessionAsync(Guid sessionId, string userId, CancellationToken ct = default) =>
            Task.FromResult<ConversationSession?>(null);

        public Task AddTurnAsync(Guid sessionId, string userId, string role, string text, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RenameSessionAsync(Guid sessionId, string userId, string newName, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DeleteSessionAsync(Guid sessionId, string userId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeConversationServiceWithSession : IConversationService
    {
        private readonly ConversationSession _session;

        public FakeConversationServiceWithSession(ConversationSession session) =>
            _session = session;

        public Task<IReadOnlyList<ConversationSession>> GetSessionsAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ConversationSession>>([_session]);

        public Task<ConversationSession> CreateSessionAsync(string userId, string firstPrompt, CancellationToken ct = default) =>
            Task.FromResult(_session);

        public Task<ConversationSession?> GetSessionAsync(Guid sessionId, string userId, CancellationToken ct = default) =>
            Task.FromResult<ConversationSession?>(_session);

        public Task AddTurnAsync(Guid sessionId, string userId, string role, string text, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RenameSessionAsync(Guid sessionId, string userId, string newName, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DeleteSessionAsync(Guid sessionId, string userId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "testuser")], "test"))));
    }

    private readonly FakeAgentDraftService _fake = new();

    public DraftWorkspaceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());
        Services.AddScoped<IAgentDraftService>(_ => _fake);
        Services.AddScoped<IConversationService>(_ => new FakeConversationService());
        Services.AddScoped<UserContext>();
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

    [Fact]
    public void DraftResponse_ShowsSummaryInChat_NotFullDraftMarkdown()
    {
        _fake.NextResponse =
            "# IT Specialist\n\n## Position Info\n\nThis is a test.\n\n## Major Duties\n\nDuties here.";
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("textarea").Input("draft a pd for IT specialist");
        cut.Find("button.primary-btn").Click();
        cut.WaitForAssertion(() =>
        {
            var assistantBubbles = cut.FindAll(".chat-bubble-row--assistant");
            Assert.NotEmpty(assistantBubbles);
            var lastText = assistantBubbles[^1].TextContent;
            Assert.Contains("Draft created", lastText);
            Assert.DoesNotContain("## Position Info", lastText);
            Assert.DoesNotContain("## Major Duties", lastText);
        });
    }

    [Fact]
    public void DraftResponse_MakesDraftPanelVisible()
    {
        _fake.NextResponse =
            "# IT Specialist\n\n## Position Info\n\nTest.\n\n## Major Duties\n\nDuties.";
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("textarea").Input("draft a pd");
        cut.Find("button.primary-btn").Click();
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".right-editor")));
    }

    [Fact]
    public void ConversationalResponse_ShowsFullTextInChat()
    {
        _fake.NextResponse = "Here are some open positions: Software Engineer, Data Analyst.";
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("textarea").Input("list open positions");
        cut.Find("button.primary-btn").Click();
        cut.WaitForAssertion(() =>
        {
            var assistantBubbles = cut.FindAll(".chat-bubble-row--assistant");
            Assert.NotEmpty(assistantBubbles);
            var lastText = assistantBubbles[^1].TextContent;
            Assert.Contains("open positions", lastText);
            Assert.DoesNotContain("Draft created", lastText);
        });
    }

    [Fact]
    public void SessionRestore_DraftTurns_ShowSummaryNotRawMarkdown()
    {
        const string draftText =
            "# IT Specialist\n\n## Position Info\n\nTest.\n\n## Major Duties\n\nDuties.";
        var sessionId = Guid.NewGuid();
        var session = new ConversationSession
        {
            Id = sessionId,
            UserId = "testuser",
            Name = "Test Session",
            Turns =
            [
                new ConversationTurn
                {
                    Role = "user",
                    Text = "draft a pd",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2)
                },
                new ConversationTurn
                {
                    Role = "assistant",
                    Text = draftText,
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
                }
            ]
        };

        Services.AddScoped<IConversationService>(_ => new FakeConversationServiceWithSession(session));

        var cut = RenderComponent<DraftWorkspace>(p =>
            p.Add(w => w.SessionId, sessionId));

        cut.WaitForAssertion(() =>
        {
            var assistantBubbles = cut.FindAll(".chat-bubble-row--assistant");
            Assert.NotEmpty(assistantBubbles);
            var text = assistantBubbles[0].TextContent;
            Assert.Contains("Draft created", text);
            Assert.DoesNotContain("## Position Info", text);
            Assert.DoesNotContain("## Major Duties", text);
        });
    }

    [Fact]
    public void SessionRestore_DraftTurns_MakesDraftPanelVisible()
    {
        const string draftText =
            "# IT Specialist\n\n## Position Info\n\nTest.\n\n## Major Duties\n\nDuties.";
        var sessionId = Guid.NewGuid();
        var session = new ConversationSession
        {
            Id = sessionId,
            UserId = "testuser",
            Name = "Test Session",
            Turns =
            [
                new ConversationTurn
                {
                    Role = "user",
                    Text = "draft a pd",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2)
                },
                new ConversationTurn
                {
                    Role = "assistant",
                    Text = draftText,
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
                }
            ]
        };

        Services.AddScoped<IConversationService>(_ => new FakeConversationServiceWithSession(session));

        var cut = RenderComponent<DraftWorkspace>(p =>
            p.Add(w => w.SessionId, sessionId));

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".right-editor")));
    }
}
