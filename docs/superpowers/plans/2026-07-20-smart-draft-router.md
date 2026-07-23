# Smart Draft Router Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route PD draft responses to the Draft panel and show a concise summary in the Chat panel, instead of rendering the full draft markdown in both places.

**Architecture:** Three new `internal static` helpers are added to `DraftWorkspace` following its existing pattern. `SendPromptAsync` is refactored to classify each response before deciding what to add to `_turns`. `OnParametersSetAsync` gets a post-load pass that replaces draft-containing historical turns with summaries.

**Tech Stack:** Blazor Server (.NET 10), bUnit (component tests), xUnit (unit tests), Markdig

## Global Constraints

- All changes confined to `DraftWorkspace.razor` and the two test files listed below
- No new files, services, or interfaces
- `internal static` visibility on all new helpers (matches existing pattern)
- `ChatTurn` is `sealed record ChatTurn(string Role, string Text, DateTimeOffset Timestamp)` — use `with` expression for mutation
- Test command: `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests` (58 tests must stay passing)
- Never include `Co-Authored-By:` in commit messages

---

## File Map

| File | Change |
|------|--------|
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add 3 static helpers; refactor `SendPromptAsync`; add post-load pass in `OnParametersSetAsync` |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs` | Add unit tests for 3 new helpers |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs` | Add component tests for routing behavior and session restore |

---

## Task 1: Add `GetDraftSectionNames` helper + tests

**Files:**
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Produces: `internal static List<string> GetDraftSectionNames(string draftMarkdown)` — parses `##` headings; later tasks use this in `BuildChatSummary`

- [ ] **Step 1: Write the failing tests**

Add to the bottom of `DraftIntentTests.cs` (before the closing `}`):

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "GetDraftSectionNames"
```

Expected: FAIL — `GetDraftSectionNames` does not exist yet.

- [ ] **Step 3: Add the helper to `DraftWorkspace.razor`**

In the `@code` block, after the `IsClosingLine` method (around line 607), add:

```csharp
internal static List<string> GetDraftSectionNames(string draftMarkdown) =>
    draftMarkdown
        .Split(["\r\n", "\n"], StringSplitOptions.None)
        .Where(line => line.StartsWith("## ", StringComparison.Ordinal))
        .Select(line => line[3..].Trim())
        .ToList();
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "GetDraftSectionNames"
```

Expected: PASS (4 tests)

- [ ] **Step 5: Run full suite to check no regressions**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests
```

Expected: all 62 tests pass.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs
git commit -m "feat: add GetDraftSectionNames helper to DraftWorkspace"
```

---

## Task 2: Add `ExtractPositionTitle` helper + tests

**Files:**
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: nothing
- Produces: `internal static string? ExtractPositionTitle(string draftMarkdown)` — returns text of first `# ` (H1) line, or `null`

- [ ] **Step 1: Write the failing tests**

Add to the bottom of `DraftIntentTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "ExtractPositionTitle"
```

Expected: FAIL — method does not exist.

- [ ] **Step 3: Add the helper to `DraftWorkspace.razor`**

After `GetDraftSectionNames`, add:

```csharp
internal static string? ExtractPositionTitle(string draftMarkdown)
{
    var line = draftMarkdown
        .Split(["\r\n", "\n"], StringSplitOptions.None)
        .FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal)
                          && !l.StartsWith("## ", StringComparison.Ordinal));
    return line?[2..].Trim();
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "ExtractPositionTitle"
```

Expected: PASS (3 tests)

- [ ] **Step 5: Run full suite**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests
```

Expected: all 65 tests pass.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs
git commit -m "feat: add ExtractPositionTitle helper to DraftWorkspace"
```

---

## Task 3: Add `BuildChatSummary` helper + tests

**Files:**
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: `GetDraftSectionNames(string)`, `ExtractPositionTitle(string)`
- Produces: `internal static string BuildChatSummary(string? previousDraftMarkdown, string newDraftMarkdown)` — returns markdown summary string for the chat turn

- [ ] **Step 1: Write the failing tests**

Add to the bottom of `DraftIntentTests.cs`:

```csharp
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
    Assert.DoesNotContain(" — ", result);
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "BuildChatSummary"
```

Expected: FAIL — method does not exist.

- [ ] **Step 3: Add the helper to `DraftWorkspace.razor`**

After `ExtractPositionTitle`, add:

```csharp
internal static string BuildChatSummary(string? previousDraftMarkdown, string newDraftMarkdown)
{
    var newSections = GetDraftSectionNames(newDraftMarkdown);
    var title = ExtractPositionTitle(newDraftMarkdown);
    var titleLine = title is not null ? $" \u2014 {title}" : string.Empty;

    if (previousDraftMarkdown is null)
    {
        var sectionList = newSections.Count > 0
            ? $"\nSections: {string.Join(", ", newSections)}"
            : string.Empty;
        return $"\u2705 **Draft created**{titleLine}{sectionList}";
    }

    var oldSections = GetDraftSectionNames(previousDraftMarkdown);
    var added = newSections.Except(oldSections).ToList();
    var revised = newSections.Intersect(oldSections).ToList();

    var sb = new StringBuilder();
    sb.Append($"\u2705 **Draft updated**{titleLine}");
    if (added.Count > 0)
        sb.Append($"\nAdded: {string.Join(", ", added)}");
    if (revised.Count > 0)
        sb.Append($"\nRevised: {string.Join(", ", revised)}");

    return sb.ToString();
}
```

Note: `StringBuilder` is already imported via `using System.Text` at the top of the file.

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "BuildChatSummary"
```

Expected: PASS (4 tests)

- [ ] **Step 5: Run full suite**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests
```

Expected: all 69 tests pass.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs
git commit -m "feat: add BuildChatSummary helper to DraftWorkspace"
```

---

## Task 4: Refactor `SendPromptAsync` routing + component tests

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` (lines 403–431)
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`

**Interfaces:**
- Consumes: `BuildChatSummary(string?, string)`, `ShouldSyncDraft(string, string)`, `ExtractDraftMarkdown(string)`
- Produces: updated `SendPromptAsync` behavior — draft responses show summary in chat; conversational responses show full text

- [ ] **Step 1: Write the failing component tests**

Add to `DraftWorkspaceTests.cs` (before the closing `}`):

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "DraftResponse|ConversationalResponse"
```

Expected: `DraftResponse_ShowsSummaryInChat_NotFullDraftMarkdown` FAIL (currently shows full markdown); others may pass or fail.

- [ ] **Step 3: Refactor `SendPromptAsync` in `DraftWorkspace.razor`**

Locate the block starting at line 403 (`var response = await AgentDraftService...`). Replace from that line through the end of the `ShouldSyncDraft` block and the checklist update (up to and including `_seriesSuggestion = TryExtractSeriesSuggestion(response);` is NOT changed — leave it as-is after your new block).

Replace this existing block:

```csharp
var response = await AgentDraftService.SendPromptAsync(input, SessionId);
_turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
await ConversationService.AddTurnAsync(SessionId.Value, _userId, "assistant", response);
await InvokeAsync(StateHasChanged);
await JS.InvokeVoidAsync("scrollToBottom", "chat-thread");

if (ShouldSyncDraft(input, response))
{
    var draftMarkdown = ExtractDraftMarkdown(response);
    if (draftMarkdown is not null)
    {
        _currentDraftMarkdown = draftMarkdown;
        var html = NormalizeHtmlForQuill(Markdown.ToHtml(draftMarkdown, ChatMarkdownPipeline));
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
}
```

With:

```csharp
var response = await AgentDraftService.SendPromptAsync(input, SessionId);

string? routedDraftMarkdown = null;
if (ShouldSyncDraft(input, response))
    routedDraftMarkdown = ExtractDraftMarkdown(response);

if (routedDraftMarkdown is not null)
{
    var summary = BuildChatSummary(_currentDraftMarkdown, routedDraftMarkdown);
    _turns.Add(new ChatTurn("Assistant", summary, DateTimeOffset.UtcNow));
    await ConversationService.AddTurnAsync(SessionId.Value, _userId, "assistant", response);
    await InvokeAsync(StateHasChanged);
    await JS.InvokeVoidAsync("scrollToBottom", "chat-thread");

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
else
{
    _turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
    await ConversationService.AddTurnAsync(SessionId.Value, _userId, "assistant", response);
    await InvokeAsync(StateHasChanged);
    await JS.InvokeVoidAsync("scrollToBottom", "chat-thread");
}
```

Leave the lines after this block unchanged:

```csharp
// Update checklist from the new draft markdown
if (_currentDraftMarkdown is not null)
    _checklistState.UpdateFromDraft(_currentDraftMarkdown);

// Detect series suggestion from AI response
_seriesSuggestion = TryExtractSeriesSuggestion(response);
```

- [ ] **Step 4: Run the new tests to verify they pass**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "DraftResponse|ConversationalResponse"
```

Expected: PASS (3 tests)

- [ ] **Step 5: Run full suite**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests
```

Expected: all 72 tests pass.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs
git commit -m "feat: route draft responses to Draft panel; show summary in chat"
```

---

## Task 5: Fix session restore — replace draft turns with summaries

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` (`OnParametersSetAsync`)
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`

**Interfaces:**
- Consumes: `ExtractDraftMarkdown(string)`, `BuildChatSummary(string?, string)`
- Produces: historical draft-containing turns shown as summaries in the restored chat UI

- [ ] **Step 1: Write the failing component test**

In `DraftWorkspaceTests.cs`, add a second fake conversation service that can return a session with pre-loaded turns. Add this private class inside `DraftWorkspaceTests`:

```csharp
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
```

Then add the test:

```csharp
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
```

Note: this test registers `IConversationService` *after* the existing `Services.AddScoped` call in the constructor. In bUnit's `TestContext`, the last registration wins for `AddScoped`.

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "SessionRestore"
```

Expected: FAIL — restored turn shows raw markdown.

- [ ] **Step 3: Add post-load pass to `OnParametersSetAsync` in `DraftWorkspace.razor`**

Locate the end of the `foreach` loop that rebuilds `_turns` (around line 318–323):

```csharp
foreach (var turn in session.Turns.OrderBy(t => t.Timestamp))
{
    var role = string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase) ? "You" : "Assistant";
    _turns.Add(new ChatTurn(role, turn.Text, turn.Timestamp));
}
```

Immediately after this `foreach` block (before the `// Restore draft state` comment), add:

```csharp
// Replace draft-containing assistant turns with chat summaries
for (var i = 0; i < _turns.Count; i++)
{
    if (!string.Equals(_turns[i].Role, "Assistant", StringComparison.OrdinalIgnoreCase))
        continue;
    var md = ExtractDraftMarkdown(_turns[i].Text);
    if (md is null) continue;
    _turns[i] = _turns[i] with { Text = BuildChatSummary(null, md) };
}
```

- [ ] **Step 4: Run the test to verify it passes**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests --filter "SessionRestore"
```

Expected: PASS

- [ ] **Step 5: Run full suite**

```
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests
```

Expected: all 73 tests pass.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs
git commit -m "feat: replace draft turns with summaries on session restore"
```

---

## Self-Review

**Spec coverage check:**
- ✅ Draft routed to Draft panel, summary in chat → Task 4
- ✅ Summary = confirmation + change summary (Added/Revised) → Task 3
- ✅ Auto-switch to "Both" layout on first draft → already works via `_draftVisible = true` + `_leftPanelHidden = false`; no new code needed; confirmed in `DraftResponse_MakesDraftPanelVisible` test (Task 4)
- ✅ PD drafts only (existing `ShouldSyncDraft` + `ResponseContainsDraft` unchanged) → Task 4
- ✅ Session restore shows summaries → Task 5

**Placeholder scan:** No TBDs. All steps have code.

**Type consistency:**
- `GetDraftSectionNames` → `List<string>` — used in `BuildChatSummary` as `List<string>` ✅
- `ExtractPositionTitle` → `string?` — used in `BuildChatSummary` with null-conditional ✅
- `BuildChatSummary(string?, string)` → `string` — used in Task 4 `SendPromptAsync` and Task 5 session restore ✅
- `ChatTurn with { Text = ... }` — `ChatTurn` is `sealed record(string Role, string Text, DateTimeOffset Timestamp)` ✅
