# PD Draft Guard Rails Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three guard rails to the PD Draft feature: a prohibited-language self-review lens, content-depth threshold validation in the checklist, and UX safety additions (navigation warning, re-review button, acknowledge confirmation).

**Architecture:** GR1 extends the existing `BuildSelfReviewPrompt` string in `AgentDraftService.cs`. GR2 extends `PdChecklistState.UpdateFromDraft` with a body-map pass and a `MeetsDepthThreshold` helper. GR3 adds JS to the inline script block in `App.razor` and Razor changes to `DraftWorkspace.razor` and `PdSectionChecklist.razor`.

**Tech Stack:** .NET 10, Blazor Server (InteractiveServer), xUnit + bUnit, vanilla JS (inline in App.razor)

## Global Constraints

- All C# files use `nullable enable` and top-level namespace declarations
- Test project: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/` — run with `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/`
- No new NuGet packages
- JS lives in the inline `<script>` block in `DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor` — there is no separate `app.js`
- Follow existing commit style: `feat:`, `fix:`, `test:`, `docs:` prefixes

---

## File Map

| File | Change |
|------|--------|
| `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs` | GR1: add 4th lens to `BuildSelfReviewPrompt` |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/AgentDraftServiceSelfReviewTests.cs` | GR1: add 1 test |
| `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdChecklistState.cs` | GR2: body map + `MeetsDepthThreshold` + `UpdateFromDraft` update |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdChecklistStateTests.cs` | GR2: fix 2 existing tests + add 5 new tests |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor` | GR3a: add `registerDraftUnloadGuard` / `unregisterDraftUnloadGuard` JS |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | GR3a: implement `IAsyncDisposable`, register/unregister guard; GR3b: Re-review button + `ReReviewDraftAsync` |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdSectionChecklist.razor` | GR3c: inline two-step Acknowledge confirm |

---

## Task 1: GR1 — Prohibited Language Lens

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs`
- Test: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/AgentDraftServiceSelfReviewTests.cs`

**Interfaces:**
- Produces: `BuildSelfReviewPrompt(string draftMarkdown)` now includes a 4th lens block containing the text "Prohibited Language"

- [ ] **Step 1: Write the failing test**

  Add to `AgentDraftServiceSelfReviewTests.cs`:

  ```csharp
  [Fact]
  public void BuildSelfReviewPrompt_ContainsProhibitedLanguageLens()
  {
      var result = AgentDraftService.BuildSelfReviewPrompt("some draft");
      Assert.Contains("Prohibited Language", result);
  }
  ```

- [ ] **Step 2: Run to confirm it fails**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "BuildSelfReviewPrompt_ContainsProhibitedLanguageLens"
  ```

  Expected: FAIL — "Prohibited Language" not in prompt string.

- [ ] **Step 3: Add the 4th lens to `BuildSelfReviewPrompt`**

  In `AgentDraftService.cs`, find `BuildSelfReviewPrompt`. The current prompt ends with `3. Candidate Appeal — ...`. Add a 4th lens immediately after it, before the `Format your response as:` line:

  ```csharp
  internal static string BuildSelfReviewPrompt(string draftMarkdown) =>
      $"""
      Review this GS position description draft across four lenses and list findings by severity.

      Lenses:
      1. OPM Compliance — required sections present, qualifications cite OPM minimum standards,
         duties start with grade-calibrated action verbs, no prohibited language
      2. Completeness — supervisory status, remote/telework eligibility, security clearance,
         education/experience requirements present or explicitly marked N/A
      3. Candidate Appeal — duties and qualifications attract qualified candidates;
         language is clear, specific, and avoids jargon
      4. Prohibited Language — flag any of the following:
         - Age indicators (e.g., "young professional", "recent graduate", "under 40")
         - Gender-specific role titles (e.g., "manpower", "chairman", "journeyman")
         - Disability restrictions not tied to a documented bona fide occupational requirement
         - Citizenship/nationality phrasing beyond the standard "U.S. Citizen" or "U.S. National" statements
         - Any pre-employment inquiry language prohibited by EEO law or OPM policy

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

- [ ] **Step 4: Run all self-review tests**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "AgentDraftServiceSelfReviewTests"
  ```

  Expected: all 7 tests PASS (6 existing + 1 new).

- [ ] **Step 5: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs
  git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/AgentDraftServiceSelfReviewTests.cs
  git commit -m "feat: add prohibited language as 4th self-review lens"
  ```

---

## Task 2: GR2 — Content-Depth Threshold Validation

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdChecklistState.cs`
- Test: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdChecklistStateTests.cs`

**Interfaces:**
- Consumes: `PdChecklistState.UpdateFromDraft(string draftMarkdown)` — existing method
- Produces: same method signature; now sets `PdSectionStatus.Warning` instead of `Complete` when body fails depth threshold
- New private helper: `static bool MeetsDepthThreshold(string sectionName, List<string> bodyLines)`

**Important:** Two existing tests will fail after this change because they use thin content (1 bullet / 1 sentence) and assert `Complete`. Fix them as part of this task.

- [ ] **Step 1: Write the 5 new failing tests**

  Add to `PdChecklistStateTests.cs` under `// ── UpdateFromDraft ───` region:

  ```csharp
  [Fact]
  public void UpdateFromDraft_MajorDutiesWithThreeBullets_MarksWarning()
  {
      var state = new PdChecklistState();
      state.UpdateFromDraft("""
          ## Major Duties
          - Leads cloud projects.
          - Manages stakeholders.
          - Develops architecture documents.
          """);
      var section = state.Sections.Single(s => s.Name == "Major Duties");
      Assert.Equal(PdSectionStatus.Warning, section.Status);
  }

  [Fact]
  public void UpdateFromDraft_MajorDutiesWithFiveBullets_MarksComplete()
  {
      var state = new PdChecklistState();
      state.UpdateFromDraft("""
          ## Major Duties
          - Independently leads cloud infrastructure projects.
          - Designs and implements AWS migration strategy.
          - Serves as technical expert for cloud security.
          - Provides authoritative guidance on IaC tooling.
          - Establishes policy for cloud cost governance.
          """);
      var section = state.Sections.Single(s => s.Name == "Major Duties");
      Assert.Equal(PdSectionStatus.Complete, section.Status);
  }

  [Fact]
  public void UpdateFromDraft_PositionSummaryWithTwoSentences_MarksWarning()
  {
      var state = new PdChecklistState();
      state.UpdateFromDraft("""
          ## Position Summary
          This position leads cloud migration. It reports to the CTO.
          """);
      var section = state.Sections.Single(s => s.Name == "Position Summary");
      Assert.Equal(PdSectionStatus.Warning, section.Status);
  }

  [Fact]
  public void UpdateFromDraft_PositionSummaryWithThreeSentences_MarksComplete()
  {
      var state = new PdChecklistState();
      state.UpdateFromDraft("""
          ## Position Summary
          This position serves as the lead cloud architect for the agency's digital modernization initiative.
          The incumbent designs and implements AWS-based infrastructure supporting mission-critical applications.
          This role reports to the Deputy CIO and collaborates with enterprise architecture teams.
          """);
      var section = state.Sections.Single(s => s.Name == "Position Summary");
      Assert.Equal(PdSectionStatus.Complete, section.Status);
  }

  [Fact]
  public void UpdateFromDraft_RequiredSectionWithEmptyBody_MarksWarning()
  {
      var state = new PdChecklistState();
      state.UpdateFromDraft("""
          ## Security Clearance

          ## Remote Work Eligibility
          Hybrid — duty station Washington, DC.
          """);
      var clearance = state.Sections.Single(s => s.Name == "Security Clearance");
      Assert.Equal(PdSectionStatus.Warning, clearance.Status);
  }
  ```

- [ ] **Step 2: Run new tests to confirm they fail**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "UpdateFromDraft_MajorDuties|UpdateFromDraft_PositionSummary|UpdateFromDraft_RequiredSectionWithEmptyBody"
  ```

  Expected: all 5 FAIL.

- [ ] **Step 3: Implement body map + MeetsDepthThreshold in `PdChecklistState.cs`**

  Replace `UpdateFromDraft` and add the helper. The full updated implementation:

  ```csharp
  public void UpdateFromDraft(string draftMarkdown)
  {
      if (string.IsNullOrWhiteSpace(draftMarkdown))
          return;

      var lines = draftMarkdown
          .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
          .ToList();

      // Build a map of normalized heading text → body lines beneath it.
      // Body lines are everything between this heading and the next ## heading.
      var bodyMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
      string? currentHeading = null;
      foreach (var line in lines)
      {
          if (line.StartsWith('#'))
          {
              currentHeading = line.TrimStart('#').Trim().ToLowerInvariant();
              bodyMap[currentHeading] = [];
          }
          else if (currentHeading is not null)
          {
              bodyMap[currentHeading].Add(line);
          }
      }

      var headings = bodyMap.Keys.ToList();

      foreach (var section in _sections)
      {
          if (section.IsLocked) continue;

          var keywords = SectionKeywords(section.Name);
          var matchedHeading = headings.FirstOrDefault(h =>
              keywords.Any(k => h.Contains(k, StringComparison.Ordinal)));

          if (matchedHeading is null) continue;

          var body = bodyMap[matchedHeading];
          section.Status = MeetsDepthThreshold(section.Name, body)
              ? PdSectionStatus.Complete
              : PdSectionStatus.Warning;
      }
  }

  private static bool MeetsDepthThreshold(string sectionName, List<string> bodyLines)
  {
      return sectionName switch
      {
          "Major Duties" =>
              bodyLines.Count(l => l.TrimStart().StartsWith("- ", StringComparison.Ordinal)
                                || l.TrimStart().StartsWith("* ", StringComparison.Ordinal)) >= 5,

          "Position Summary" =>
              bodyLines.Count(l =>
              {
                  var t = l.TrimEnd();
                  return t.EndsWith('.') || t.EndsWith('!') || t.EndsWith('?');
              }) >= 3,

          _ => bodyLines.Any(l => !string.IsNullOrWhiteSpace(l))
      };
  }
  ```

- [ ] **Step 4: Fix the two broken existing tests**

  The existing tests `UpdateFromDraft_WithDutiesHeading_MarksComplete` and `UpdateFromDraft_WithPositionSummaryHeading_MarksComplete` now assert `Complete` on thin content — they will fail. Update them:

  ```csharp
  // BEFORE:
  [Fact]
  public void UpdateFromDraft_WithDutiesHeading_MarksComplete()
  {
      var state = new PdChecklistState();
      state.UpdateFromDraft("## Major Duties\n\n- Independently leads cloud infrastructure projects.");
      var section = state.Sections.Single(s => s.Name == "Major Duties");
      Assert.Equal(PdSectionStatus.Complete, section.Status);
  }

  // AFTER:
  [Fact]
  public void UpdateFromDraft_WithDutiesHeading_MarksComplete()
  {
      var state = new PdChecklistState();
      state.UpdateFromDraft("""
          ## Major Duties
          - Independently leads cloud infrastructure projects.
          - Designs and implements AWS migration strategy.
          - Serves as technical expert for cloud security.
          - Provides authoritative guidance on IaC tooling.
          - Establishes policy for cloud cost governance.
          """);
      var section = state.Sections.Single(s => s.Name == "Major Duties");
      Assert.Equal(PdSectionStatus.Complete, section.Status);
  }
  ```

  ```csharp
  // BEFORE:
  [Fact]
  public void UpdateFromDraft_WithPositionSummaryHeading_MarksComplete()
  {
      var state = new PdChecklistState();
      state.UpdateFromDraft("## Position Summary\n\nThis position serves as...");
      var section = state.Sections.Single(s => s.Name == "Position Summary");
      Assert.Equal(PdSectionStatus.Complete, section.Status);
  }

  // AFTER:
  [Fact]
  public void UpdateFromDraft_WithPositionSummaryHeading_MarksComplete()
  {
      var state = new PdChecklistState();
      state.UpdateFromDraft("""
          ## Position Summary
          This position serves as the lead cloud architect for the agency.
          The incumbent designs and implements AWS-based infrastructure supporting mission-critical applications.
          This role reports to the Deputy CIO and collaborates with enterprise architecture teams.
          """);
      var section = state.Sections.Single(s => s.Name == "Position Summary");
      Assert.Equal(PdSectionStatus.Complete, section.Status);
  }
  ```

- [ ] **Step 5: Run all checklist tests**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "PdChecklistStateTests"
  ```

  Expected: all tests PASS (existing + 5 new). Count: 16 total.

- [ ] **Step 6: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/Web/Models/PdChecklistState.cs
  git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/PdChecklistStateTests.cs
  git commit -m "feat: add content-depth threshold validation to checklist"
  ```

---

## Task 3: GR3a — Dirty-Draft Navigation Warning

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: `JS.InvokeVoidAsync("registerDraftUnloadGuard")` and `JS.InvokeVoidAsync("unregisterDraftUnloadGuard")`
- Produces: browser `beforeunload` dialog shown when draft is active and user tries to close/navigate away

- [ ] **Step 1: Add the two JS functions to `App.razor`**

  In `App.razor`, find the inline `<script>` block. After the `window.downloadFile` function (the last function), add these two before the closing `</script>`:

  ```js
  window.registerDraftUnloadGuard = function () {
      window._draftUnloadHandler = function (e) {
          e.preventDefault();
          e.returnValue = '';
      };
      window.addEventListener('beforeunload', window._draftUnloadHandler);
  };

  window.unregisterDraftUnloadGuard = function () {
      if (window._draftUnloadHandler) {
          window.removeEventListener('beforeunload', window._draftUnloadHandler);
          window._draftUnloadHandler = null;
      }
  };
  ```

- [ ] **Step 2: Add `IAsyncDisposable` and guard registration to `DraftWorkspace.razor`**

  **2a.** In `DraftWorkspace.razor`, find the `@code {` block opening and the class-level field declarations. Add `IAsyncDisposable` to the component by adding `@implements IAsyncDisposable` at the top of the file (after the `@using` directives, before the HTML):

  ```razor
  @implements IAsyncDisposable
  ```

  **2b.** In the `@code` block, add the dispose method and a flag to track whether the guard is registered:

  ```csharp
  private bool _unloadGuardRegistered;

  public async ValueTask DisposeAsync()
  {
      if (_unloadGuardRegistered)
      {
          try { await JS.InvokeVoidAsync("unregisterDraftUnloadGuard"); }
          catch { /* component may be disposed before JS is available */ }
      }
  }
  ```

  **2c.** In `SendPromptAsync`, find the block where `_draftVisible = true` is first set (inside `if (!_draftVisible)`). After `_draftVisible = true;`, register the guard:

  ```csharp
  if (!_draftVisible)
  {
      _pendingHtml = html;
      _draftVisible = true;
      _hasDraft = true;
      if (!_unloadGuardRegistered)
      {
          _unloadGuardRegistered = true;
          await JS.InvokeVoidAsync("registerDraftUnloadGuard");
      }
  }
  ```

  **2d.** Also register the guard in `OnParametersSetAsync` when restoring a draft from session (the `if (restoredMarkdown is not null)` block). After `_draftVisible = true;`:

  ```csharp
  if (!_unloadGuardRegistered)
  {
      _unloadGuardRegistered = true;
      // Guard registered after render in OnAfterRenderAsync to ensure JS is ready
      _pendingUnloadGuardRegistration = true;
  }
  ```

  Add `private bool _pendingUnloadGuardRegistration;` to the fields, and in `OnAfterRenderAsync`:

  ```csharp
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
      if (_pendingHtml is not null)
      {
          var html = _pendingHtml;
          _pendingHtml = null;
          await JS.InvokeVoidAsync("loadQuillWhenReady", "quill-editor-wrapper", html);
      }

      if (_pendingUnloadGuardRegistration)
      {
          _pendingUnloadGuardRegistration = false;
          try { await JS.InvokeVoidAsync("registerDraftUnloadGuard"); }
          catch { /* ignore if JS not ready */ }
      }
  }
  ```

- [ ] **Step 3: Manual smoke test**

  Run the app, create a draft, then try to close the browser tab or navigate to a different URL. The browser should show a "Leave site?" confirmation dialog. Closing the tab without a draft should NOT show the dialog.

  ```
  cd DotnetAiAgentUi/src/HrMcp.Agent && dotnet run
  ```

- [ ] **Step 4: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
  git commit -m "feat: add beforeunload guard to prevent accidental draft loss"
  ```

---

## Task 4: GR3b — Re-Review Button

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: `AgentDraftService.GetDraftSelfReviewAsync(string draftMarkdown)` — already on `IAgentDraftService`
- Consumes: `_currentDraftMarkdown` — component field, already exists
- Produces: new button "Re-review draft" in right panel header; new `ReReviewDraftAsync` method

- [ ] **Step 1: Add the button to the right panel header**

  In `DraftWorkspace.razor`, find the `<div class="right-header">` section. Insert the `@if` block between the `<h3>` and the `<div class="export-menu-wrap">`:

  ```razor
  @if (_hasDraft && !_selfReviewLoading && !_busy)
  {
      <button class="ghost-btn" @onclick="ReReviewDraftAsync">Re-review draft</button>
  }
  ```

  Do not change any existing markup in `right-header` — only insert this block.

- [ ] **Step 2: Add `ReReviewDraftAsync` to the `@code` block**

  ```csharp
  private async Task ReReviewDraftAsync()
  {
      if (_currentDraftMarkdown is null || _selfReviewLoading || _busy)
          return;

      _selfReviewLoading = true;
      await InvokeAsync(StateHasChanged);

      _ = Task.Run(async () =>
      {
          var review = await AgentDraftService.GetDraftSelfReviewAsync(_currentDraftMarkdown);
          if (!string.IsNullOrWhiteSpace(review))
              _turns.Add(new ChatTurn("Assistant", review, DateTimeOffset.UtcNow));
          _selfReviewLoading = false;
          await InvokeAsync(StateHasChanged);
          await InvokeAsync(() => JS.InvokeVoidAsync("scrollToBottom", "chat-thread"));
      });
  }
  ```

  Note: `ReReviewDraftAsync` reads `_currentDraftMarkdown` — the last AI-generated draft. Manual Quill edits made since the last AI response are not included. This is intentional.

- [ ] **Step 3: Run existing component tests to confirm nothing broke**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "DraftWorkspaceTests"
  ```

  Expected: all PASS.

- [ ] **Step 4: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
  git commit -m "feat: add on-demand re-review button to draft panel"
  ```

---

## Task 5: GR3c — Inline Acknowledge Confirmation

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdSectionChecklist.razor`

**Interfaces:**
- Consumes: `OnAcknowledge` `EventCallback<string>` — already exists; now fires on Confirm instead of immediately
- Produces: inline two-step confirm UI (`_pendingAcknowledgeSectionName` state)

- [ ] **Step 1: Add `_pendingAcknowledgeSectionName` field and update the Acknowledge button**

  Replace the entire `PdSectionChecklist.razor` content with:

  ```razor
  @using HrMcp.Agent.Web.Models

  <div class="pd-checklist">
      <button class="pd-checklist-summary" @onclick="ToggleExpanded" aria-expanded="@_expanded">
          <span class="pd-checklist-header-label">Section Checklist</span>
          <span class="pd-checklist-counts">
              @if (CompleteCount > 0) { <span title="Complete">✅ @CompleteCount</span> }
              @if (WarningCount > 0)  { <span title="Needs review">⚠️ @WarningCount</span> }
              @if (MissingCount > 0)  { <span title="Missing">❌ @MissingCount</span> }
              @if (LockedCount > 0)   { <span title="Auto-filled">🔒 @LockedCount</span> }
          </span>
          <span class="pd-checklist-toggle" aria-hidden="true">@(_expanded ? "▲" : "▼")</span>
      </button>
      @if (_expanded)
      {
          <ul class="pd-checklist-list">
              @foreach (var section in State.Sections)
              {
                  var icon = section.Status switch
                  {
                      PdSectionStatus.Complete   => "✅",
                      PdSectionStatus.Warning    => "⚠️",
                      PdSectionStatus.AutoFilled => "🔒",
                      _                          => "❌"
                  };
                  var cssClass = section.Status switch
                  {
                      PdSectionStatus.Complete   => "pd-checklist-item--complete",
                      PdSectionStatus.Warning    => "pd-checklist-item--warning",
                      PdSectionStatus.AutoFilled => "pd-checklist-item--locked",
                      _                          => "pd-checklist-item--missing"
                  };
                  var isClickable = section.Status is PdSectionStatus.Missing or PdSectionStatus.Warning;
                  var isPendingAck = _pendingAcknowledgeSectionName == section.Name;

                  <li class="pd-checklist-item @cssClass">
                      <span class="pd-checklist-icon">@icon</span>
                      @if (isClickable)
                      {
                          <button class="pd-checklist-name pd-checklist-name--link"
                                  @onclick="() => OnSectionClicked.InvokeAsync(section.Name)">
                              @section.Name
                          </button>
                      }
                      else
                      {
                          <span class="pd-checklist-name">@section.Name</span>
                      }
                      @if (section.Status == PdSectionStatus.Warning && !section.IsLocked)
                      {
                          @if (isPendingAck)
                          {
                              <span class="pd-checklist-ack-confirm">
                                  Override this warning?
                                  <button class="ghost-btn" @onclick="() => ConfirmAcknowledge(section.Name)">Confirm</button>
                                  <button class="ghost-btn" @onclick="CancelAcknowledge">Cancel</button>
                              </span>
                          }
                          else
                          {
                              <button class="pd-checklist-ack ghost-btn"
                                      @onclick="() => BeginAcknowledge(section.Name)">
                                  Ack
                              </button>
                          }
                      }
                  </li>
              }
          </ul>
      }
  </div>

  @code {
      [Parameter, EditorRequired] public PdChecklistState State { get; set; } = default!;
      [Parameter] public EventCallback<string> OnSectionClicked { get; set; }
      [Parameter] public EventCallback<string> OnAcknowledge { get; set; }

      private bool _expanded;
      private string? _pendingAcknowledgeSectionName;

      private void ToggleExpanded() => _expanded = !_expanded;

      private void BeginAcknowledge(string sectionName) =>
          _pendingAcknowledgeSectionName = sectionName;

      private async Task ConfirmAcknowledge(string sectionName)
      {
          _pendingAcknowledgeSectionName = null;
          await OnAcknowledge.InvokeAsync(sectionName);
      }

      private void CancelAcknowledge() =>
          _pendingAcknowledgeSectionName = null;

      private int CompleteCount => State.Sections.Count(s => s.Status == PdSectionStatus.Complete);
      private int WarningCount  => State.Sections.Count(s => s.Status == PdSectionStatus.Warning);
      private int MissingCount  => State.Sections.Count(s => s.Status == PdSectionStatus.Missing);
      private int LockedCount   => State.Sections.Count(s => s.Status == PdSectionStatus.AutoFilled);
  }
  ```

- [ ] **Step 2: Run all tests**

  ```
  dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/
  ```

  Expected: all PASS.

- [ ] **Step 3: Commit**

  ```
  git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Shared/PdSectionChecklist.razor
  git commit -m "feat: add inline two-step confirmation to checklist acknowledge"
  ```
