# Draft Self-Review Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After the first PD draft is generated, fire a non-blocking AI self-review call and post findings (OPM compliance, completeness, candidate appeal) as a new assistant chat turn ending with "What would you like to address first?"

**Architecture:** Two changes only. (1) `AgentDraftService` gains a new `GetDraftSelfReviewAsync` method that makes a focused, standalone AI call (no session history) and returns a structured findings string. (2) `DraftWorkspace.razor` fires this call non-blocking after the first draft route, shows a loading indicator, then adds the review as a chat turn.

**Tech Stack:** Blazor Server (.NET 10), bUnit (component tests), xUnit (unit tests), `Microsoft.Extensions.AI` (`IChatClient.CompleteAsync`)

## Global Constraints

- Never include `Co-Authored-By:` in commit messages
- Test command: `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests` from the project root (76 tests currently passing — must stay passing)
- Self-review fires on first draft only — guarded by `_selfReviewDone` field
- Self-review failures must be silent — never disrupt the draft workflow
- `GetDraftSelfReviewAsync` must NOT add to the agent's conversation history — use a fresh `IChatClient` call, not `_agent.AskAsync`
- Changes confined to the four files listed in the File Map below

---

## File Map

| File | Change |
|------|--------|
| `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` | Add `GetDraftSelfReviewAsync` to `IAgentDraftService`; add `BuildSelfReviewPrompt` static helper; implement the method |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add `_selfReviewLoading`, `_selfReviewDone` fields; update `SendPromptAsync`; add loading indicator |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/AgentDraftServiceSelfReviewTests.cs` | New file — unit tests for `BuildSelfReviewPrompt` |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs` | Extend `FakeAgentDraftService`; add 4 component tests |

---

## Task 1: Service method + prompt helper + unit tests

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs`
- Create: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/AgentDraftServiceSelfReviewTests.cs`
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs` (stub only — add method to `FakeAgentDraftService` so the project compiles)

**Interfaces:**
- Produces: `Task<string> GetDraftSelfReviewAsync(string draftMarkdown, CancellationToken ct = default)` on `IAgentDraftService` and `AgentDraftService`
- Produces: `internal static string BuildSelfReviewPrompt(string draftMarkdown)` on `AgentDraftService`

- [ ] **Step 1: Write the failing unit tests**

Create `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/AgentDraftServiceSelfReviewTests.cs`:

```csharp
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
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "AgentDraftServiceSelfReviewTests"
```

Expected: FAIL — `AgentDraftService.BuildSelfReviewPrompt` does not exist yet.

- [ ] **Step 3: Add `GetDraftSelfReviewAsync` to the interface**

In `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs`, update `IAgentDraftService`:

```csharp
public interface IAgentDraftService
{
    Task<string> SendPromptAsync(string prompt, Guid? sessionId = null, CancellationToken ct = default);
    Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(string draftText, CancellationToken ct = default);
    Task<string> GetDraftSelfReviewAsync(string draftMarkdown, CancellationToken ct = default);
}
```

- [ ] **Step 4: Add stub to `FakeAgentDraftService` so the project compiles**

In `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`, add to `FakeAgentDraftService`:

```csharp
public string SelfReviewResponse { get; set; } = "";
public Task<string> GetDraftSelfReviewAsync(string draftMarkdown, CancellationToken ct = default) =>
    Task.FromResult(SelfReviewResponse);
```

The full `FakeAgentDraftService` now looks like:

```csharp
private sealed class FakeAgentDraftService : IAgentDraftService
{
    public string NextResponse { get; set; } = "Hello from assistant";
    public string SelfReviewResponse { get; set; } = "";

    public Task<string> SendPromptAsync(string prompt, Guid? sessionId = null, CancellationToken ct = default) =>
        Task.FromResult(NextResponse);

    public Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(
        string draftText, CancellationToken ct = default) =>
        Task.FromResult(("ok", (string?)null, (byte[]?)null));

    public Task<string> GetDraftSelfReviewAsync(string draftMarkdown, CancellationToken ct = default) =>
        Task.FromResult(SelfReviewResponse);
}
```

- [ ] **Step 5: Add `BuildSelfReviewPrompt` and implement `GetDraftSelfReviewAsync`**

In `AgentDraftService`, add after `ExportDraftToWordAsync`:

```csharp
public async Task<string> GetDraftSelfReviewAsync(string draftMarkdown, CancellationToken ct = default)
{
    await EnsureInitializedAsync(ct);
    try
    {
        var chatClient = CreateChatClient(_configuration!);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "You are an expert HR specialist and federal hiring specialist reviewing a U.S. federal government position description draft."),
            new(ChatRole.User, BuildSelfReviewPrompt(draftMarkdown))
        };
        var result = await chatClient.CompleteAsync(messages, cancellationToken: ct);
        return result.Message.Text ?? "";
    }
    catch
    {
        return "";
    }
}

internal static string BuildSelfReviewPrompt(string draftMarkdown) =>
    $"""
    Review this GS position description draft across three lenses and list findings by severity.

    Lenses:
    1. OPM Compliance — required sections present, qualifications cite OPM minimum standards,
       duties start with grade-calibrated action verbs, no prohibited language
    2. Completeness — supervisory status, remote/telework eligibility, security clearance,
       education/experience requirements present or explicitly marked N/A
    3. Candidate Appeal — duties and qualifications attract qualified candidates;
       language is clear, specific, and avoids jargon

    Format your response as:
    **Draft Self-Review**

    🔴 Critical (must fix before posting):
    - [issue] — [brief explanation]

    🟡 Important (strongly recommended):
    - [issue] — [brief explanation]

    🟢 Minor (nice to have):
    - [issue] — [brief explanation]

    If a category has no findings, write "None."

    End with: "What would you like to address first?"

    Draft:
    {draftMarkdown}
    """;
```

- [ ] **Step 6: Run the unit tests to verify they pass**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "AgentDraftServiceSelfReviewTests"
```

Expected: PASS (6 tests)

- [ ] **Step 7: Run the full test suite**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests
```

Expected: Passed — 82 tests, 0 failed (76 existing + 6 new).

- [ ] **Step 8: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/AgentDraftServiceSelfReviewTests.cs \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs
git commit -m "feat: add GetDraftSelfReviewAsync and BuildSelfReviewPrompt to AgentDraftService"
```

---

## Task 2: Component changes + component tests

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`

**Interfaces:**
- Consumes: `Task<string> GetDraftSelfReviewAsync(string draftMarkdown, CancellationToken ct = default)` — from Task 1
- Consumes: `FakeAgentDraftService.SelfReviewResponse` — stub added in Task 1

- [ ] **Step 1: Write the failing component tests**

In `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`, add these four tests before the closing `}` of `DraftWorkspaceTests`:

```csharp
[Fact]
public void FirstDraft_AddsSelfReviewTurnToChat()
{
    _fake.NextResponse = "## Position Title\n\n## Major Duties\n\nDevelops software applications.";
    _fake.SelfReviewResponse = "**Draft Self-Review**\n\n🔴 Critical:\n- None.\n\nWhat would you like to address first?";
    var cut = RenderComponent<DraftWorkspace>();
    cut.Find("textarea").Input("draft a pd");
    cut.Find("button.primary-btn").Click();
    cut.WaitForAssertion(() =>
        Assert.Contains("Draft Self-Review", cut.Find(".chat-thread").TextContent));
}

[Fact]
public void SecondDraft_DoesNotAddSelfReviewTurnAgain()
{
    _fake.NextResponse = "## Position Title\n\n## Major Duties\n\nDevelops software applications.";
    _fake.SelfReviewResponse = "**Draft Self-Review**\n\nWhat would you like to address first?";
    var cut = RenderComponent<DraftWorkspace>();

    // First draft
    cut.Find("textarea").Input("draft a pd");
    cut.Find("button.primary-btn").Click();
    cut.WaitForAssertion(() =>
        Assert.Contains("Draft Self-Review", cut.Find(".chat-thread").TextContent));

    // Second draft (revision)
    cut.Find("textarea").Input("revise the duties section");
    cut.Find("button.primary-btn").Click();
    cut.WaitForAssertion(() =>
    {
        var reviewCount = cut.FindAll(".chat-bubble-row--assistant")
            .Count(b => b.TextContent.Contains("Draft Self-Review"));
        Assert.Equal(1, reviewCount);
    });
}

[Fact]
public void SelfReview_EmptyResponse_DoesNotAddChatTurn()
{
    _fake.NextResponse = "## Position Title\n\n## Major Duties\n\nDevelops software applications.";
    _fake.SelfReviewResponse = "";
    var cut = RenderComponent<DraftWorkspace>();
    cut.Find("textarea").Input("draft a pd");
    cut.Find("button.primary-btn").Click();
    // Wait for the draft summary to appear, then verify no review turn
    cut.WaitForAssertion(() =>
        Assert.Contains("Position Title", cut.Find(".chat-thread").TextContent));
    Assert.DoesNotContain("Draft Self-Review", cut.Find(".chat-thread").TextContent);
}

[Fact]
public void ExistingSession_DoesNotTriggerSelfReview()
{
    var sessionId = Guid.NewGuid();
    var session = new ConversationSession
    {
        Id = sessionId,
        UserId = "testuser",
        Name = "Test",
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
                Text = "## Job Title\n\n## Major Duties\n\nDuties here.",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1)
            }
        ]
    };
    Services.AddScoped<IConversationService>(_ => new FakeConversationServiceWithSession(session));
    _fake.SelfReviewResponse = "**Draft Self-Review** — must not appear";
    var cut = RenderComponent<DraftWorkspace>(p => p.Add(w => w.SessionId, sessionId));
    cut.WaitForAssertion(() =>
        Assert.DoesNotContain("Draft Self-Review", cut.Find(".chat-thread").TextContent));
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "FirstDraft_AddsSelfReviewTurnToChat|SecondDraft_DoesNotAddSelfReviewTurnAgain|SelfReview_EmptyResponse_DoesNotAddChatTurn|ExistingSession_DoesNotTriggerSelfReview"
```

Expected: FAIL — `FirstDraft_AddsSelfReviewTurnToChat` fails because no self-review turn exists yet.

- [ ] **Step 3: Add new fields to `DraftWorkspace.razor`**

In the `@code` block, locate the line (around line 192):
```csharp
private (string Suggested, string SuggestedName, string Current)? _seriesSuggestion;
```

Add the two new fields immediately after it:

```csharp
private bool _selfReviewLoading;
private bool _selfReviewDone;
```

- [ ] **Step 4: Update `SendPromptAsync` to fire self-review after first draft**

Locate the `if (routedDraftMarkdown is not null)` block (around line 430). It currently ends with:

```csharp
            _currentDraftMarkdown = routedDraftMarkdown;
            var html = NormalizeHtmlForQuill(Markdown.ToHtml(routedDraftMarkdown, ChatMarkdownPipeline));
            if (!_draftVisible)
            {
                _pendingHtml = html;
                _draftVisible = true;
                _hasDraft = true;
            }
            else
            {
                await JS.InvokeVoidAsync("setQuillContent", "quill-editor-wrapper", html);
            }
        }
```

Replace the entire `if (routedDraftMarkdown is not null)` block with:

```csharp
            if (routedDraftMarkdown is not null)
            {
                var summary = BuildChatSummary(_currentDraftMarkdown, routedDraftMarkdown);
                _turns.Add(new ChatTurn("Assistant", summary, DateTimeOffset.UtcNow));
                await ConversationService.AddTurnAsync(SessionId.Value, _userId, "assistant", response);
                await InvokeAsync(StateHasChanged);
                await JS.InvokeVoidAsync("scrollToBottom", "chat-thread");

                bool isFirstDraft = _selfReviewDone is false;
                _currentDraftMarkdown = routedDraftMarkdown;
                var html = NormalizeHtmlForQuill(Markdown.ToHtml(routedDraftMarkdown, ChatMarkdownPipeline));
                if (!_draftVisible)
                {
                    _pendingHtml = html;
                    _draftVisible = true;
                    _hasDraft = true;
                }
                else
                {
                    await JS.InvokeVoidAsync("setQuillContent", "quill-editor-wrapper", html);
                }

                if (isFirstDraft)
                {
                    _selfReviewDone = true;
                    _selfReviewLoading = true;
                    await InvokeAsync(StateHasChanged);

                    _ = Task.Run(async () =>
                    {
                        var review = await AgentDraftService.GetDraftSelfReviewAsync(routedDraftMarkdown);
                        if (!string.IsNullOrWhiteSpace(review))
                            _turns.Add(new ChatTurn("Assistant", review, DateTimeOffset.UtcNow));
                        _selfReviewLoading = false;
                        await InvokeAsync(StateHasChanged);
                        await InvokeAsync(() => JS.InvokeVoidAsync("scrollToBottom", "chat-thread"));
                    });
                }
            }
```

- [ ] **Step 5: Add loading indicator to the chat thread template**

In the template section, locate the `@if (_busy)` block (around line 85). It is inside the `else` branch of `@if (_turns.Count == 0)` and looks like:

```razor
                    @if (_busy)
                    {
                        <div class="chat-bubble-row chat-bubble-row--assistant">
                            <div class="chat-bubble chat-bubble--loading" role="status" aria-live="polite">
                                <span class="chat-spinner" aria-hidden="true"></span>
                                <span>Assistant is drafting your response...</span>
                            </div>
                        </div>
                    }
                }
```

Add the self-review loading indicator immediately after the `@if (_busy)` closing `}`, before the `else` closing `}`:

```razor
                    @if (_busy)
                    {
                        <div class="chat-bubble-row chat-bubble-row--assistant">
                            <div class="chat-bubble chat-bubble--loading" role="status" aria-live="polite">
                                <span class="chat-spinner" aria-hidden="true"></span>
                                <span>Assistant is drafting your response...</span>
                            </div>
                        </div>
                    }
                    @if (_selfReviewLoading)
                    {
                        <div class="chat-bubble-row chat-bubble-row--assistant">
                            <div class="chat-bubble chat-bubble--loading" role="status" aria-live="polite">
                                <span class="chat-spinner" aria-hidden="true"></span>
                                <span>Reviewing your draft…</span>
                            </div>
                        </div>
                    }
                }
```

- [ ] **Step 6: Run the new component tests to verify they pass**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "FirstDraft_AddsSelfReviewTurnToChat|SecondDraft_DoesNotAddSelfReviewTurnAgain|SelfReview_EmptyResponse_DoesNotAddChatTurn|ExistingSession_DoesNotTriggerSelfReview"
```

Expected: PASS (4 tests)

- [ ] **Step 7: Run the full test suite**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests
```

Expected: Passed — 86 tests, 0 failed.

- [ ] **Step 8: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs
git commit -m "feat: inject self-review turn into chat after first draft is generated"
```

---

## Self-Review

**Spec coverage:**
- ✅ Fires on first draft only → `_selfReviewDone` guard in Task 2 Step 4
- ✅ Non-blocking → `Task.Run` wrapper in Task 2 Step 4
- ✅ Loading indicator → Task 2 Step 5
- ✅ Three lenses (OPM compliance, completeness, candidate appeal) → `BuildSelfReviewPrompt` in Task 1 Step 5
- ✅ Severity markers (🔴 🟡 🟢) → `BuildSelfReviewPrompt` in Task 1 Step 5
- ✅ Ends with "What would you like to address first?" → `BuildSelfReviewPrompt` in Task 1 Step 5
- ✅ Failure is silent → `catch { return ""; }` in Task 1 Step 5
- ✅ Empty response → no chat turn → `IsNullOrWhiteSpace` guard in Task 2 Step 4
- ✅ Does not pollute agent history → fresh `IChatClient` call in Task 1 Step 5
- ✅ Unit tests for `BuildSelfReviewPrompt` → Task 1 Step 1 (6 tests)
- ✅ Component tests → Task 2 Step 1 (4 tests: first draft, second draft, empty response, session restore)
- ✅ `FakeAgentDraftService` extended → Task 1 Step 4 (stub) + Task 2 Step 1 (usage)

**Placeholder scan:** None. All code blocks are complete and verbatim.

**Type consistency:**
- `GetDraftSelfReviewAsync(string draftMarkdown, CancellationToken ct = default)` — consistent across interface (Task 1 Step 3), implementation (Task 1 Step 5), and fake (Task 1 Step 4) ✅
- `BuildSelfReviewPrompt(string draftMarkdown)` — referenced in unit tests (Task 1 Step 1) and defined in implementation (Task 1 Step 5) ✅
- `_selfReviewDone` and `_selfReviewLoading` — defined in Task 2 Step 3, used in Task 2 Steps 4 and 5 ✅
- `ChatTurn("Assistant", review, DateTimeOffset.UtcNow)` — matches existing `ChatTurn` sealed record constructor ✅
