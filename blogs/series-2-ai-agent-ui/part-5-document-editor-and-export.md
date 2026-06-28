# Part 5: Document Editor & Word Export

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 5 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog_cover.png)

---

## Introduction

Parts 1–4 built the chat foundation: the Blazor United shell, agent wiring, styled chat bubbles, and Markdig-powered markdown rendering. The left panel is in good shape. Now it's time to earn the second half of the screen.

In Series 1 Part 5, we wired Claude Desktop directly to the MCP server and ran an end-to-end demo — proving the server was host-independent. This part is the Series 2 equivalent payoff moment: we add a WYSIWYG document editor to the right panel and a Word export button that reaches back through the agent to call the `ExportDraftToWord` MCP tool. By the end, the user can ask the Writing Assistant to draft a position description, see it appear in the editor, refine it inline, and download a `.docx` file — all from a single Blazor page.

The three pieces that make this work:

1. **Draft state fields** — `_draftVisible` (bool) controls whether the right panel renders, and `_currentDraftMarkdown` (string) holds the latest draft so Markdown export works without re-reading from Quill.
2. **`Blazored.TextEditor`** — a Blazor wrapper around the Quill rich-text editor, giving us a browser-native WYSIWYG surface without writing JavaScript widget code ourselves.
3. **A resizable split-panel layout** — CSS Grid with a draggable splitter, because `MudGrid` does not give us pixel-level column control at drag time.

---

## Step 1 — Install Blazored.TextEditor

Add the package to the agent web project:

```bash
dotnet add DotnetAiAgentUI/src/HrMcp.Agent package Blazored.TextEditor --version 1.*
```

After the install, the `.csproj` should contain:

```xml
<!-- DotnetAiAgentUI/src/HrMcp.Agent/HrMcp.Agent.csproj -->
<PackageReference Include="Blazored.TextEditor" Version="1.*" />
```

Blazored.TextEditor ships its own JS and CSS. You need to load them in `App.razor` **before** `blazor.web.js`, alongside the Quill CDN files it depends on:

```html
<!-- DotnetAiAgentUI/src/HrMcp.Agent/Components/App.razor — head section -->
<link href="https://cdn.quilljs.com/1.3.6/quill.snow.css" rel="stylesheet" />
```

```html
<!-- DotnetAiAgentUI/src/HrMcp.Agent/Components/App.razor — body scripts -->
<script src="https://cdn.quilljs.com/1.3.6/quill.min.js"></script>
<script src="_content/Blazored.TextEditor/quill-blot-formatter.min.js"></script>
<script src="_content/Blazored.TextEditor/Blazored-BlazorQuill.js"></script>
<script src="_framework/blazor.web.js"></script>
```

Finally, add the namespace to `_Imports.razor` so every component can use `<BlazoredTextEditor>` without a per-file `@using`:

```razor
@* DotnetAiAgentUI/src/HrMcp.Agent/Components/_Imports.razor *@
@using Blazored.TextEditor
```

---

## Step 2 — The DraftDocumentState Model

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/DraftDocumentState.cs
namespace HrMcp.Agent.Web.Models;

public sealed class DraftDocumentState
{
    public string DraftText { get; set; } = string.Empty;
    public int Revision { get; set; }

    public DraftDocumentState() { }

    public DraftDocumentState(string draftText, int revision)
    {
        DraftText = draftText;
        Revision = revision;
    }
}
```

Two design decisions worth calling out.

**Class, not record.** `DraftDocumentState` is a mutable class. A `record` would be immutable by default and would require replacing the entire object on each update. Here the component holds a single instance and mutates its properties, then signals Blazor to re-render.

**The `Revision` counter.** Quill does not expose a simple "set content" binding. You cannot just drop a new string into a Blazor parameter and expect the editor to refresh. `Revision` is the escape hatch: when a new draft arrives from the agent, `Revision` increments. The component watches for that change in `OnAfterRenderAsync` and calls back into JavaScript to reload the editor content. Without something like this you would need to track state in JS side-by-side with Blazor state — a recipe for drift.

---

## Step 3 — The Split-Panel Layout

The workspace is a CSS Grid container with three columns: the chat panel, an 8 px splitter track, and the editor panel.

```css
/* DotnetAiAgentUI/src/HrMcp.Agent/wwwroot/css/app.css */
.workspace {
    display: grid;
    grid-template-columns: minmax(320px, 420px) 8px minmax(0, 1fr);
    gap: 0;
    height: calc(100vh - 110px);
    margin-top: 10px;
    border: 1px solid #d7deea;
    border-radius: 12px;
    overflow: hidden;
    background: #ffffff;
}

.splitter {
    background: #edf1f7;
    cursor: col-resize;
    transition: background 0.2s ease;
}

.splitter:hover  { background: #d6deeb; }
.splitter:active { background: #c4cfe2; }
```

The reason for CSS Grid over `MudBlazor`'s `MudGrid` is control: MudGrid works in percentage-based fractions, which is fine for static layouts. A draggable splitter needs to write a pixel value back into the column definition on every `mousemove` event. CSS Grid handles that cleanly with a `style` attribute override.

The `WorkspaceGridStyle` computed property does the work:

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor — @code block
private string WorkspaceGridStyle => _leftPanelHidden || !_draftVisible
    ? "grid-template-columns: minmax(0, 1fr);"
    : $"grid-template-columns: {_leftPanelWidth}px 8px minmax(0, 1fr);";
```

When the chat panel is hidden or the editor has not appeared yet, the workspace collapses to a single column. Once both panels are visible, the left column width is driven by `_leftPanelWidth`, which starts at 420 px and is clamped between 320 px and 760 px during drag.

The splitter `<div>` in the markup sets `StartResize` on `mousedown`:

```razor
@* DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor *@
<div class="workspace" style="@WorkspaceGridStyle"
     @onmousemove="HandleResize"
     @onmouseup="EndResize"
     @onmouseleave="EndResize">

    @if (!_leftPanelHidden)
    {
        <section class="left-chat"> ... </section>

        @if (_draftVisible)
        {
            <div class="splitter"
                 role="separator"
                 aria-label="Resize chat and draft panels"
                 @onmousedown="StartResize"></div>
        }
    }

    @if (_draftVisible)
    {
        <section class="right-editor"> ... </section>
    }
</div>
```

The three resize handlers:

```csharp
private void StartResize()
{
    if (_leftPanelHidden) return;
    _resizing = true;
}

private void EndResize()
{
    _resizing = false;
}

private void HandleResize(MouseEventArgs args)
{
    if (!_resizing || _leftPanelHidden) return;

    var nextWidth = (int)args.ClientX - 24;
    _leftPanelWidth = Math.Clamp(nextWidth, MinLeftPanelWidth, MaxLeftPanelWidth);
}
```

`args.ClientX` is the cursor's X position relative to the viewport. Subtracting 24 px accounts for the outer container's left padding. `Math.Clamp` enforces the min/max bounds so the user cannot drag the chat panel off screen or into the editor.

---

## Step 4 — Wiring the WYSIWYG Editor

The right panel hosts the `<BlazoredTextEditor>` component. The `@ref` attribute captures a reference to the component instance so we can call its API:

```razor
@* DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor — right panel *@
<section class="right-editor">
    <div class="right-header">
        <h3>Your Position Description Draft</h3>
    </div>

    <div id="quill-editor-wrapper">
        <BlazoredTextEditor @ref="_richEditor"
                            Placeholder="Your draft will appear here. You can edit it directly.">
            <ToolbarContent>
                <select class="ql-header">
                    <option selected=""></option>
                    <option value="1"></option>
                    <option value="2"></option>
                    <option value="3"></option>
                </select>
                <button class="ql-bold"></button>
                <button class="ql-italic"></button>
                <button class="ql-underline"></button>
                <button class="ql-strike"></button>
                <span class="ql-formats">
                    <button class="ql-list" value="ordered"></button>
                    <button class="ql-list" value="bullet"></button>
                    <button class="ql-indent" value="-1"></button>
                    <button class="ql-indent" value="+1"></button>
                </span>
                <button class="ql-background"></button>
                <button class="ql-clean"></button>
            </ToolbarContent>
        </BlazoredTextEditor>
    </div>

    <div class="panel-actions panel-actions--editor">
        <div class="panel-actions-row panel-actions-row--end">
            <button class="primary-btn primary-btn--compact"
                    @onclick="ExportWordAsync"
                    disabled="@_busy">Export Word</button>
        </div>
    </div>
</section>
```

The tricky part is loading content into Quill after the agent returns a draft. `OnInitializedAsync` is too early — the Quill editor JS has not run yet. `OnAfterRenderAsync` is the correct hook, but even that fires before Quill's own `createQuill` callback completes, because the parent renders before the child component finishes its JS initialization.

The solution is a polling JS function. When the first draft arrives, the component sets `_pendingHtml` and shows the editor panel. On the next `OnAfterRenderAsync`, it calls `loadQuillWhenReady`, which polls at 50 ms intervals until it finds the Quill instance on the DOM:

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor — @code block
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (_pendingHtml is not null)
    {
        var html = _pendingHtml;
        _pendingHtml = null;
        // Quill may not be initialized yet — JS function polls until ready
        await JS.InvokeVoidAsync("loadQuillWhenReady", "quill-editor-wrapper", html);
    }
}
```

For subsequent draft updates (the editor is already open), the component calls `setQuillContent` directly, bypassing the poll:

```csharp
await JS.InvokeVoidAsync("setQuillContent", "quill-editor-wrapper", html);
```

There is one more wrinkle. Markdig's `UseAdvancedExtensions` produces "loose" list items: `<li><p>text</p></li>`. Quill's MutationObserver sees the `<p>` inside the `<li>` and normalizes it, truncating content in the process. The fix is to strip the inner `<p>` wrapper before handing HTML to Quill:

```csharp
private static string NormalizeHtmlForQuill(string html) =>
    Regex.Replace(html, @"<li>\s*<p>(.*?)</p>\s*</li>", "<li>$1</li>", RegexOptions.Singleline);
```

---

## Step 5 — Word Export via MCP Tool Call

### The service method

`ExportDraftToWordAsync` in `AgentDraftService` constructs a prompt that instructs the agent to call the `ExportDraftToWord` MCP tool, passing the current draft content unchanged:

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs
public async Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(
    string draftText, CancellationToken ct = default)
{
    await EnsureInitializedAsync(ct);
    var request =
        $"Export this draft to Word by calling ExportDraftToWord with positionId={DefaultExportContextPositionId}. Keep content unchanged.\n" +
        draftText;
    var message = await _agent!.AskAsync(request, ct);
    return (message, _agent.LastExportedFileName, _agent.LastExportedFileBytes);
}
```

The return tuple carries three values: a human-readable message from the agent, the file name, and the raw bytes of the `.docx` file. The agent exposes `LastExportedFileName` and `LastExportedFileBytes` as properties that the MCP tool populates when `ExportDraftToWord` runs successfully.

The interface contract for this method:

```csharp
Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(
    string draftText, CancellationToken ct = default);
```

### The component handler

`ExportWordAsync` in the component reads the current HTML from Quill via JS interop, hands it to the service, and if it gets bytes back, triggers a browser download:

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor — @code block
private async Task ExportWordAsync()
{
    var currentHtml = await JS.InvokeAsync<string>("getQuillContent", "quill-editor-wrapper");
    if (_busy || string.IsNullOrWhiteSpace(currentHtml) || currentHtml == "<p><br></p>")
    {
        _status = "Draft is empty. Add content before exporting.";
        return;
    }

    _busy = true;
    _exportBusy = true;
    _status = string.Empty;

    try
    {
        var (message, fileName, fileBytes) = await AgentDraftService.ExportDraftToWordAsync(currentHtml);

        if (fileName is not null && fileBytes is not null)
        {
            _status = $"\u2705 Word document ready: {fileName}";
            await JS.InvokeVoidAsync("downloadFile", fileName, Convert.ToBase64String(fileBytes));
        }
        else
        {
            _status = message;
        }
    }
    catch (Exception ex)
    {
        _status = $"Export failed: {ex.Message}";
    }
    finally
    {
        _busy = false;
        _exportBusy = false;
    }
}
```

Note that `getQuillContent` reads from `quill-editor-wrapper` using the same DOM traversal pattern as the set functions. This is necessary because `BlazoredTextEditor` does not expose an `@bind` path for reading HTML content out of the component.

### The JS download helper

The `downloadFile` function lives inline in `App.razor`. It decodes the base64 bytes, creates a `Blob`, and triggers a synthetic anchor click. It uses `URL.createObjectURL` rather than a `data:` URI to avoid the 2 MB size limit that some browsers impose on data URLs:

```javascript
// DotnetAiAgentUI/src/HrMcp.Agent/Components/App.razor — inline script
window.downloadFile = function (fileName, base64Content) {
    const byteChars = atob(base64Content);
    const byteNums = new Array(byteChars.length);
    for (let i = 0; i < byteChars.length; i++) byteNums[i] = byteChars.charCodeAt(i);
    const blob = new Blob([new Uint8Array(byteNums)], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
```

The full JS interop surface for the editor — read, write, poll, and download — is defined in one place (the `<script>` block in `App.razor`) and called via `IJSRuntime`. There is no separate `.js` file to manage.

---

## What We Have

The Position Description Builder is complete. The left panel is the Writing Assistant chat. The right panel is a full WYSIWYG editor with heading levels, bold, italic, lists, and indentation controls. Between them sits an 8 px draggable splitter that lets the user balance screen real estate to their preference.

The end-to-end flow:

1. The user types a prompt like "Draft a position description for a Software Engineer at GS-13 level."
2. The agent calls `GetPositionById` via MCP to retrieve reference data, then writes a structured draft.
3. The Blazor component detects draft intent in the response, converts the Markdown to HTML, and loads it into Quill.
4. The user refines the draft directly in the editor — formatting, reordering, adding qualifications.
5. The user clicks **Export ▾** to open the export dropdown and selects Word (.docx), Markdown (.md), or JSON (.json). For Word, the component reads the current HTML from Quill, passes it to `ExportDraftToWordAsync`, the agent calls the `ExportDraftToWord` MCP tool, and the browser downloads the `.docx` file. Markdown and JSON export directly from component state without a round-trip to the agent.

The only piece missing from a production-ready deployment is authentication. Any user who can reach the URL can use the agent and export documents.

---

## Next Up

Part 6 secures the entire UI with OIDC — authenticated users only, Bearer tokens forwarded through to the MCP server.

→ **[Part 6: Securing the UI with OIDC](part-6-securing-ui-with-oidc.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
