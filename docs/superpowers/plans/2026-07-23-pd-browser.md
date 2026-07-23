# PD Browser Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let hiring managers browse, filter, and select existing PDs from the database to use as a draft starting point, accessible from the welcome screen, natural-language chat phrases, and the help system.

**Architecture:** A new `PdBrowserPanel.razor` shared component renders inline in the chat thread when `_browsing = true`. Three entry points — welcome screen button, `IsBrowseIntent` routing in `SendPromptAsync`, and a help system hint — all converge on this single panel. On selection, `OnPositionSelectedAsync` builds a draft skeleton, loads the draft panel, and seeds the AI to guide updates.

**Tech Stack:** .NET 10, Blazor Server, xUnit, existing `PositionService` (HrMcp.Application)

## Global Constraints

- All C# files use `nullable enable` and top-level namespace declarations
- Test project: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/` — run with `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/`
- No new NuGet packages
- Commit message prefix: `feat:` or `fix:`
- Follow existing Blazor component patterns (see `PdSectionChecklist.razor` for reference)

---

## File Map

| File | Role |
|------|------|
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdBrowserPanel.razor` | New: browse/filter UI + preview card + callbacks |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Modified: `BrowseIntentTerms`, `IsBrowseIntent`, `NonDraftIntentTerms`, `_browsing` flag, welcome shortcuts, panel rendering, `OnPositionSelectedAsync`, `BuildDraftSkeleton` |
| `DotnetAiAgentUi/src/HrMcp.Agent/Program.cs` | Modified: register `PositionService` in DI |
| `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs` | Modified: browse hint in `## Help Mode` system prompt block |
| `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css` | Modified: styles for `PdBrowserPanel` |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs` | Modified: two new `[Theory]` tests for `IsBrowseIntent` |

---

## Task 1: Browse Intent Routing + Unit Tests (TDD)

Adds `BrowseIntentTerms`, `IsBrowseIntent`, and browse-phrase entries to `NonDraftIntentTerms` in `DraftWorkspace.razor`. Covered by two new `[Theory]` tests.

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`

**Interfaces:**
- Produces: `public static bool IsBrowseIntent(string prompt)` on `DraftWorkspace` — returns `true` when the prompt contains any entry from `BrowseIntentTerms` (case-insensitive)

- [ ] **Step 1: Write the two failing tests**

  Add to the end of `DraftIntentTests.cs` (before the closing `}`):

  ```csharp
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
  ```

- [ ] **Step 2: Run to confirm both theories fail**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "BrowsePhrases"
  ```

  Expected: all 42 cases fail (`IsBrowseIntent` does not exist yet).

- [ ] **Step 3: Add `BrowseIntentTerms` and `IsBrowseIntent` to `DraftWorkspace.razor`**

  Find the `NonDraftIntentTerms` array (line ~251). Replace the entire `NonDraftIntentTerms` array and add `BrowseIntentTerms` + `IsBrowseIntent` immediately after it:

  ```csharp
  private static readonly string[] BrowseIntentTerms =
  [
      "browse pd",
      "browse pds",
      "browse position",
      "browse positions",
      "list pd",
      "list pds",
      "list position",
      "list positions",
      "show pd",
      "show pds",
      "show positions",
      "find pd",
      "find pds",
      "search pd",
      "search pds",
      "search positions",
      "use existing",
      "start from existing",
      "copy existing",
      "existing pd",
      "existing pds",
  ];
  private static readonly string[] NonDraftIntentTerms =
  [
      "list open position",
      "list open positions",
      "open position",
      "open positions",
      "show open position",
      "show open positions",
      "what is a gs",
      // Browse intent terms — IsBrowseIntent checked first in SendPromptAsync;
      // these entries are a safety net so IsDraftIntentPrompt never fires for them.
      "browse pd",
      "browse pds",
      "browse position",
      "browse positions",
      "list pd",
      "list pds",
      "list position",
      "list positions",
      "show pd",
      "show pds",
      "show positions",
      "find pd",
      "find pds",
      "search pd",
      "search pds",
      "search positions",
      "use existing",
      "start from existing",
      "copy existing",
      "existing pd",
      "existing pds",
  ];
  ```

  Then, immediately after the `IsDraftIntentPrompt` method (search for `public static bool IsDraftIntentPrompt`), add:

  ```csharp
  public static bool IsBrowseIntent(string prompt) =>
      BrowseIntentTerms.Any(t => prompt.ToLowerInvariant().Contains(t));
  ```

- [ ] **Step 4: Run to confirm all 42 cases pass**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "BrowsePhrases"
  ```

  Expected: 42/42 pass.

- [ ] **Step 5: Run full suite**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/
  ```

  Expected: all 141 tests pass (99 existing + 42 new).

- [ ] **Step 6: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
  git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs
  git commit -m "feat: add browse intent routing and unit tests"
  ```

---

## Task 2: PdBrowserPanel Component + DI Registration + CSS

Creates the inline Blazor browse panel and registers `PositionService` in DI. No automated tests for the component itself — data loading and filtering are exercised by running the app.

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdBrowserPanel.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Program.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css`

**Interfaces:**
- Consumes: `PositionService.GetAllPositionsAsync()` → `IEnumerable<Position>` (all positions including closed)
- Consumes: `PositionService.GetPositionByIdAsync(int id)` → `Position?`
- Produces: `[Parameter] EventCallback<int> OnPositionSelected` — fires with `positionId` after user confirms
- Produces: `[Parameter] EventCallback OnCancel` — fires when user clicks Cancel

- [ ] **Step 1: Register `PositionService` in `Program.cs`**

  In `Program.cs`, find the line:
  ```csharp
  builder.Services.AddScoped<UserContext>();
  ```
  Add immediately after it:
  ```csharp
  builder.Services.AddScoped<HrMcp.Application.Services.PositionService>();
  ```

- [ ] **Step 2: Create `PdBrowserPanel.razor`**

  Create `DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdBrowserPanel.razor`:

  ```razor
  @using HrMcp.Core.Entities
  @using HrMcp.Application.Services
  @inject PositionService PositionService

  @if (_loadError)
  {
      <div class="chat-bubble-row chat-bubble-row--assistant">
          <div class="chat-bubble pd-browser">
              <p class="pd-browser-error">Unable to load position descriptions. Please try again later.</p>
              <button class="secondary-btn" @onclick="() => OnCancel.InvokeAsync()">Close</button>
          </div>
      </div>
  }
  else if (_preview is not null)
  {
      <div class="chat-bubble-row chat-bubble-row--assistant">
          <div class="chat-bubble pd-browser pd-browser--preview">
              <div class="pd-browser-preview-header">
                  <span class="pd-browser-preview-title">📋 @_preview.Title (@GradeRange(_preview))</span>
                  <span class="pd-browser-badge @BadgeClass(_preview)">@BadgeText(_preview)</span>
              </div>
              <div class="pd-browser-preview-org">@(_preview.HiringOrganization?.OrganizationName ?? string.Empty)</div>
              <div class="pd-browser-preview-meta">Series @_preview.OccupationalSeries · @_preview.DutyLocation</div>
              <hr class="pd-browser-divider" />
              <p class="pd-browser-preview-desc">@TruncateDescription(_preview.Description)</p>
              @if (_confirmError)
              {
                  <p class="pd-browser-error">Could not load this PD. Please try another.</p>
              }
              <div class="pd-browser-actions">
                  <button class="secondary-btn" @onclick="BackToList" disabled="@_confirming">← Back</button>
                  <button class="primary-btn" @onclick="ConfirmSelectionAsync" disabled="@_confirming">
                      @(_confirming ? "Loading…" : "Use this PD →")
                  </button>
              </div>
          </div>
      </div>
  }
  else
  {
      <div class="chat-bubble-row chat-bubble-row--assistant">
          <div class="chat-bubble pd-browser">
              <div class="pd-browser-header">📂 Browse Existing Position Descriptions</div>
              <input class="pd-browser-search"
                     type="text"
                     placeholder="Search by title, keyword…"
                     @bind="_searchText"
                     @bind:event="oninput" />
              <select class="pd-browser-org-filter" @bind="_selectedOrg">
                  <option value="">All organizations</option>
                  @foreach (var org in _orgOptions)
                  {
                      <option value="@org">@org</option>
                  }
              </select>
              @if (!Filtered.Any())
              {
                  <p class="pd-browser-empty">
                      @(_allPositions.Count == 0
                          ? "No existing PDs found. Start a new one by describing your position above."
                          : "No positions match your search.")
                  </p>
              }
              else
              {
                  <div class="pd-browser-list">
                      @foreach (var pos in Filtered)
                      {
                          var capturedPos = pos;
                          <button class="pd-browser-card" @onclick="() => SelectPreview(capturedPos)">
                              <div class="pd-browser-card-title">@pos.Title</div>
                              <div class="pd-browser-card-meta">
                                  <span>@(pos.HiringOrganization?.OrganizationName ?? string.Empty)</span>
                                  <span class="pd-browser-badge @BadgeClass(pos)">@BadgeText(pos)</span>
                              </div>
                              <div class="pd-browser-card-detail">
                                  Series @pos.OccupationalSeries · @GradeRange(pos) · @pos.DutyLocation
                              </div>
                          </button>
                      }
                  </div>
              }
              <div class="pd-browser-footer">
                  <button class="secondary-btn" @onclick="() => OnCancel.InvokeAsync()">Cancel</button>
              </div>
          </div>
      </div>
  }

  @code {
      [Parameter] public EventCallback<int> OnPositionSelected { get; set; }
      [Parameter] public EventCallback OnCancel { get; set; }

      private List<Position> _allPositions = [];
      private List<string> _orgOptions = [];
      private Position? _preview;
      private string _searchText = string.Empty;
      private string _selectedOrg = string.Empty;
      private bool _confirming;
      private bool _confirmError;
      private bool _loadError;

      private IEnumerable<Position> Filtered => _allPositions
          .Where(p =>
              (string.IsNullOrWhiteSpace(_selectedOrg) ||
               string.Equals(p.HiringOrganization?.OrganizationName, _selectedOrg, StringComparison.OrdinalIgnoreCase)) &&
              (string.IsNullOrWhiteSpace(_searchText) ||
               p.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               p.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               p.Duties.Contains(_searchText, StringComparison.OrdinalIgnoreCase)))
          .OrderBy(p => p.Title);

      protected override async Task OnInitializedAsync()
      {
          try
          {
              var all = await PositionService.GetAllPositionsAsync();
              _allPositions = all.ToList();
              _orgOptions = _allPositions
                  .Select(p => p.HiringOrganization?.OrganizationName ?? string.Empty)
                  .Where(n => !string.IsNullOrEmpty(n))
                  .Distinct()
                  .OrderBy(n => n)
                  .ToList();
          }
          catch
          {
              _loadError = true;
          }
      }

      private void SelectPreview(Position pos)
      {
          _preview = pos;
          _confirmError = false;
      }

      private void BackToList()
      {
          _preview = null;
          _confirmError = false;
      }

      private async Task ConfirmSelectionAsync()
      {
          if (_preview is null) return;
          _confirming = true;
          _confirmError = false;
          var id = _preview.Id;
          try
          {
              var pos = await PositionService.GetPositionByIdAsync(id);
              if (pos is null)
              {
                  _confirmError = true;
                  _confirming = false;
                  return;
              }
          }
          catch
          {
              _confirmError = true;
              _confirming = false;
              return;
          }
          await OnPositionSelected.InvokeAsync(id);
      }

      private static string GradeRange(Position p) =>
          string.IsNullOrWhiteSpace(p.PayGradeMax) || p.PayGradeMax == p.PayGradeMin
              ? p.PayGradeMin
              : $"{p.PayGradeMin}–{p.PayGradeMax}";

      private static string BadgeClass(Position p) =>
          p.IsOpen ? "pd-browser-badge--open" : "pd-browser-badge--closed";

      private static string BadgeText(Position p) =>
          p.IsOpen ? "🟢 Open" : "🔴 Closed";

      private static string TruncateDescription(string desc) =>
          desc.Length > 300 ? string.Concat(desc.AsSpan(0, 297), "…") : desc;
  }
  ```

- [ ] **Step 3: Add CSS for the browser panel**

  Append to the end of `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css`:

  ```css
  /* ── PD Browser Panel ──────────────────────────────────────── */
  .pd-browser {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      max-width: 540px;
      width: 100%;
  }

  .pd-browser-header {
      font-weight: 600;
      font-size: 1rem;
      margin-bottom: 0.25rem;
  }

  .pd-browser-search,
  .pd-browser-org-filter {
      width: 100%;
      padding: 0.4rem 0.6rem;
      border: 1px solid var(--color-border, #ccc);
      border-radius: 6px;
      font-size: 0.9rem;
      background: var(--color-input-bg, #fff);
      color: var(--color-text, inherit);
  }

  .pd-browser-list {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
      max-height: 320px;
      overflow-y: auto;
  }

  .pd-browser-card {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      padding: 0.6rem 0.75rem;
      background: var(--color-card-bg, #f8f8f8);
      border: 1px solid var(--color-border, #ddd);
      border-radius: 8px;
      text-align: left;
      cursor: pointer;
      transition: background 0.15s, border-color 0.15s;
  }

  .pd-browser-card:hover {
      background: var(--color-card-hover, #eef4ff);
      border-color: var(--color-primary, #3b82f6);
  }

  .pd-browser-card-title {
      font-weight: 600;
      font-size: 0.95rem;
  }

  .pd-browser-card-meta {
      display: flex;
      justify-content: space-between;
      font-size: 0.82rem;
      color: var(--color-muted, #666);
  }

  .pd-browser-card-detail {
      font-size: 0.8rem;
      color: var(--color-muted, #888);
  }

  .pd-browser-badge {
      font-size: 0.78rem;
      padding: 0.1rem 0.4rem;
      border-radius: 999px;
      font-weight: 500;
  }

  .pd-browser-badge--open {
      background: #dcfce7;
      color: #15803d;
  }

  .pd-browser-badge--closed {
      background: #fee2e2;
      color: #b91c1c;
  }

  .pd-browser-empty {
      color: var(--color-muted, #888);
      font-size: 0.9rem;
      text-align: center;
      padding: 1rem 0;
  }

  .pd-browser-error {
      color: #b91c1c;
      font-size: 0.9rem;
  }

  .pd-browser-footer {
      display: flex;
      justify-content: flex-end;
      padding-top: 0.25rem;
  }

  .pd-browser-actions {
      display: flex;
      gap: 0.5rem;
      justify-content: flex-end;
      padding-top: 0.5rem;
  }

  /* Preview state */
  .pd-browser--preview {
      gap: 0.4rem;
  }

  .pd-browser-preview-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.3rem;
  }

  .pd-browser-preview-title {
      font-weight: 700;
      font-size: 1rem;
  }

  .pd-browser-preview-org {
      font-size: 0.88rem;
      color: var(--color-muted, #555);
  }

  .pd-browser-preview-meta {
      font-size: 0.82rem;
      color: var(--color-muted, #888);
  }

  .pd-browser-divider {
      border: none;
      border-top: 1px solid var(--color-border, #ddd);
      margin: 0.25rem 0;
  }

  .pd-browser-preview-desc {
      font-size: 0.88rem;
      line-height: 1.5;
      color: var(--color-text, inherit);
      white-space: pre-wrap;
  }

  /* Welcome shortcuts */
  .welcome-shortcuts {
      display: flex;
      gap: 0.5rem;
      margin-top: 0.75rem;
      flex-wrap: wrap;
  }
  ```

- [ ] **Step 4: Run full test suite to confirm no regressions**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/
  ```

  Expected: 141/141 pass (component has no unit tests; any build errors will surface here).

- [ ] **Step 5: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdBrowserPanel.razor
  git add DotnetAiAgentUi/src/HrMcp.Agent/Program.cs
  git add DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css
  git commit -m "feat: add PdBrowserPanel component, DI registration, and CSS"
  ```

---

## Task 3: DraftWorkspace Integration

Wires the browse panel into the chat thread, adds welcome shortcuts, intercepts browse phrases in `SendPromptAsync`, and handles position selection to load the draft panel and seed the AI.

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: `PdBrowserPanel` — `OnPositionSelected: EventCallback<int>`, `OnCancel: EventCallback`
- Consumes: `IsBrowseIntent(string prompt)` — already added in Task 1
- Consumes: `PositionService.GetPositionByIdAsync(int)` — already registered in Task 2
- Consumes: `AgentDraftService.SendPromptAsync(string prompt, Guid? sessionId)` — existing
- Consumes: `ConversationService.CreateSessionAsync(string userId, string title)` — existing
- Consumes: `ConversationService.AddTurnAsync(Guid sessionId, string userId, string role, string text)` — existing

- [ ] **Step 1: Add `@using` and `@inject` for `PositionService`**

  Find the existing `@inject` block at the top of `DraftWorkspace.razor`. Add:

  ```razor
  @using HrMcp.Application.Services
  @inject PositionService PositionService
  ```

  Place `@using HrMcp.Application.Services` with the other `@using` directives and `@inject PositionService PositionService` with the other `@inject` lines.

- [ ] **Step 2: Add `_browsing` field**

  In the `@code` block, find the existing private fields (near `_busy`, `_hasDraft`, etc.). Add:

  ```csharp
  private bool _browsing;
  ```

- [ ] **Step 3: Add `BuildDraftSkeleton` static method**

  Add this static method to the `@code` block (place it near `ExtractDraftMarkdown`):

  ```csharp
  public static string BuildDraftSkeleton(HrMcp.Core.Entities.Position pos)
  {
      var sb = new StringBuilder();
      sb.AppendLine($"# {pos.Title}");
      sb.AppendLine();
      sb.AppendLine("## Pay Plan / Series / Grade");
      var seriesLine = !string.IsNullOrWhiteSpace(pos.OccupationalSeriesTitle)
          ? $"Series: {pos.OccupationalSeries} – {pos.OccupationalSeriesTitle}"
          : $"Series: {pos.OccupationalSeries}";
      var gradeLine = !string.IsNullOrWhiteSpace(pos.PayGradeMax) && pos.PayGradeMax != pos.PayGradeMin
          ? $"Grade: {pos.PayGradeMin}–{pos.PayGradeMax}"
          : $"Grade: {pos.PayGradeMin}";
      sb.AppendLine(seriesLine);
      sb.AppendLine(gradeLine);
      if (!string.IsNullOrWhiteSpace(pos.Description))
      {
          sb.AppendLine();
          sb.AppendLine("## Position Summary");
          sb.AppendLine(pos.Description);
      }
      if (!string.IsNullOrWhiteSpace(pos.Duties))
      {
          sb.AppendLine();
          sb.AppendLine("## Major Duties");
          sb.AppendLine(pos.Duties);
      }
      if (!string.IsNullOrWhiteSpace(pos.Qualifications))
      {
          sb.AppendLine();
          sb.AppendLine("## Qualifications Required");
          sb.AppendLine(pos.Qualifications);
      }
      if (!string.IsNullOrWhiteSpace(pos.Education))
      {
          sb.AppendLine();
          sb.AppendLine("## Education Requirements");
          sb.AppendLine(pos.Education);
      }
      return sb.ToString().TrimEnd();
  }
  ```

- [ ] **Step 4: Add `OnPositionSelectedAsync` and `OnBrowseCancel` handlers**

  Add these two methods to the `@code` block:

  ```csharp
  private async Task OnPositionSelectedAsync(int positionId)
  {
      _browsing = false;
      _busy = true;
      await InvokeAsync(StateHasChanged);

      try
      {
          var pos = await PositionService.GetPositionByIdAsync(positionId);
          if (pos is null)
          {
              _turns.Add(new ChatTurn("Assistant", "⚠ Could not load the selected position. Please try again.", DateTimeOffset.UtcNow));
              await InvokeAsync(StateHasChanged);
              return;
          }

          var visibleLabel = $"Using **{pos.Title}** as starting point";
          var seededPrompt = $"The manager has loaded an existing Position Description as their starting point: " +
              $"Title = \"{pos.Title}\", Series = {pos.OccupationalSeries}, " +
              $"Grade = {pos.PayGradeMin}–{pos.PayGradeMax}. " +
              $"The draft has been pre-loaded. Ask what they would like to update — " +
              $"duties, qualifications, grade level, or something else.";

          var draftSkeleton = BuildDraftSkeleton(pos);
          _currentDraftMarkdown = draftSkeleton;
          var html = NormalizeHtmlForQuill(Markdown.ToHtml(draftSkeleton, ChatMarkdownPipeline));
          _hasDraft = true;
          _selfReviewDone = true;
          if (!_draftVisible)
          {
              _pendingHtml = html;
              _draftVisible = true;
              if (!_unloadGuardRegistered)
              {
                  _unloadGuardRegistered = true;
                  _pendingUnloadGuardRegistration = true;
              }
          }
          else
          {
              await JS.InvokeVoidAsync("setQuillContent", "quill-editor-wrapper", html);
          }

          _turns.Add(new ChatTurn("You", visibleLabel, DateTimeOffset.UtcNow));
          _checklistState.UpdateFromDraft(draftSkeleton);

          var isFirstMessage = SessionId is null;
          if (isFirstMessage)
          {
              var newSession = await ConversationService.CreateSessionAsync(_userId, seededPrompt);
              SessionId = newSession.Id;
          }

          await ConversationService.AddTurnAsync(SessionId!.Value, _userId, "user", seededPrompt);
          await InvokeAsync(StateHasChanged);
          await JS.InvokeVoidAsync("scrollToBottom", "chat-thread");

          var response = await AgentDraftService.SendPromptAsync(seededPrompt, SessionId);
          _turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
          await ConversationService.AddTurnAsync(SessionId.Value, _userId, "assistant", response);
          await InvokeAsync(StateHasChanged);
          await JS.InvokeVoidAsync("scrollToBottom", "chat-thread");

          if (isFirstMessage && SessionId.HasValue)
              Nav.NavigateTo($"/workspace/{SessionId}", forceLoad: false);
      }
      catch (Exception ex)
      {
          _turns.Add(new ChatTurn("Assistant", $"⚠ Error: {ex.Message}", DateTimeOffset.UtcNow));
          await InvokeAsync(StateHasChanged);
      }
      finally
      {
          _busy = false;
      }
  }

  private void OnBrowseCancel()
  {
      _browsing = false;
  }
  ```

- [ ] **Step 5: Add browse intent check to `SendPromptAsync`**

  In `SendPromptAsync`, find this block (immediately after `var input = Prompt.Trim(); Prompt = string.Empty;`):

  ```csharp
  var isFirstMessage = SessionId is null;
  ```

  Insert the browse-intent early-return BEFORE that line:

  ```csharp
  // Browse intent: show PD browser panel without calling AI
  if (IsBrowseIntent(input))
  {
      _turns.Add(new ChatTurn("You", input, DateTimeOffset.UtcNow));
      _browsing = true;
      _busy = false;
      await InvokeAsync(StateHasChanged);
      await JS.InvokeVoidAsync("scrollToBottom", "chat-thread");
      return;
  }

  var isFirstMessage = SessionId is null;
  ```

- [ ] **Step 6: Render `PdBrowserPanel` and welcome shortcuts in the chat thread**

  In the razor markup, find this block inside the `else` branch of the turns loop (after the `@foreach` closing `}` and before `@if (_busy)`):

  ```razor
  @if (_busy)
  {
  ```

  Insert the welcome shortcuts and browse panel immediately before it:

  ```razor
  @if (_turns.Count == 1 && _turns[0] == WelcomeTurn && !_browsing)
  {
      <div class="welcome-shortcuts">
          <button class="secondary-btn" @onclick="() => { }" disabled="@_busy">✏️ Start from scratch</button>
          <button class="secondary-btn" @onclick="() => { _browsing = true; }" disabled="@_busy">📂 Browse existing PDs</button>
      </div>
  }

  @if (_browsing)
  {
      <PdBrowserPanel
          OnPositionSelected="OnPositionSelectedAsync"
          OnCancel="OnBrowseCancel" />
  }

  @if (_busy)
  {
  ```

  Note: "Start from scratch" button has an empty `@onclick` handler because its only purpose is to visually indicate the option — the user already types in the chat input to start from scratch. The button focuses on discovery, not action.

- [ ] **Step 7: Run full test suite**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/
  ```

  Expected: 141/141 pass.

- [ ] **Step 8: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
  git commit -m "feat: integrate PD browser panel into DraftWorkspace"
  ```

---

## Task 4: Help & Welcome Message Additions

Adds browse guidance to the AI's `## Help Mode` system prompt and a third hint line to the welcome message.

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: `SystemPrompt` constant — raw string literal (`"""..."""`) in `HrAgent.cs`
- Consumes: `WelcomeTurn` static field — concatenated string in `DraftWorkspace.razor`

- [ ] **Step 1: Add browse guidance to `## Help Mode` in `HrAgent.cs`**

  Find the line:

  ```
          After a help response, ask: "Is there anything else I can help you with,
          or are you ready to continue with the draft?"
  ```

  Insert the following block immediately after it (before the blank line and `When drafting or updating a PD`):

  ```
          Browsing existing PDs:
          - Questions about reusing, copying, or starting from an existing PD,
            how to find a PD, or what PDs are in the system
          → Tell the manager to type "browse PDs" in the chat to open the
            PD browser, where they can search by title, keyword, or organization
            and select a PD to use as their starting point. Do not draft.
  ```

  The indentation must be 8 spaces to match the surrounding raw string literal.

- [ ] **Step 2: Add browse hint to `WelcomeTurn` in `DraftWorkspace.razor`**

  Find the `WelcomeTurn` static field. It currently ends with:

  ```csharp
          "*Type **help** at any time if you need guidance on how to use this tool.*",
  ```

  Change it to:

  ```csharp
          "*Type **help** at any time if you need guidance on how to use this tool.*\n\n" +
          "*Type **browse PDs** to find and reuse an existing position description.*",
  ```

- [ ] **Step 3: Run full test suite**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/
  ```

  Expected: 141/141 pass.

- [ ] **Step 4: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
  git commit -m "feat: add browse PDs hint to help mode and welcome message"
  ```
