# WYSIWYG Draft Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the plain markdown textarea in the Position Description draft panel with a Blazored.TextEditor (Quill) WYSIWYG editor, add chat-first startup layout, and update the Word export to accept Quill HTML.

**Architecture:** Three independent tasks progressing from back-end (McpServer Word export) to front-end wiring (Agent NuGet/scripts) to UI (DraftWorkspace). The McpServer `ExportDraftToWord` tool auto-detects HTML vs markdown content so both `--web` mode (HTML) and console mode (markdown) continue to work. The Blazored.TextEditor component replaces the `<textarea>` and Edit/Preview tabs entirely.

**Tech Stack:** .NET 10, Blazor Server Interactive, Blazored.TextEditor (Quill), AngleSharp (HTML parser), DocumentFormat.OpenXml, Markdig

## Global Constraints

- Target framework: `net10.0` — no downgrade
- All NuGet versions use wildcard major pins matching the project style (e.g. `1.*`, `0.*`)
- `AppendMarkdownContent` in `ExportTools.cs` must NOT be removed — console mode still passes markdown
- Quill version: `1.3.6` (CDN) — matches Blazored.TextEditor's tested version
- No new Blazor service registrations needed for Blazored.TextEditor — it is script-only
- Run all `dotnet` commands from repo root: `c:\apps\DotnetMcpTutorial`

---

### Task 1: McpServer — HTML-aware Word export

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj`
- Modify: `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs`

**Interfaces:**
- Consumes: existing `BuildDraftDocx(string title, string org, string draftContent)` — signature unchanged
- Produces: `AppendHtmlContent(Body body, string html)` — called by `BuildDraftDocx` when content starts with `<`

---

- [ ] **Step 1: Add AngleSharp NuGet to McpServer**

Edit `DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj`. Add inside the existing `<ItemGroup>` that has `<PackageReference>` entries:

```xml
<PackageReference Include="AngleSharp" Version="1.*" />
```

- [ ] **Step 2: Restore and verify NuGet resolves**

```bash
dotnet restore DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: `Restore completed` with no errors.

- [ ] **Step 3: Add using directives for AngleSharp to ExportTools.cs**

At the top of `DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs`, add after the existing `using` block:

```csharp
using AngleSharp.Html.Parser;
using AngleSharp.Dom;
```

- [ ] **Step 4: Add `AppendHtmlContent` and `BuildHtmlParagraph` to ExportTools.cs**

Add these two private static methods to the `ExportTools` class, directly after the closing brace of `AppendMarkdownContent`:

```csharp
private static void AppendHtmlContent(Body body, string html)
{
    var parser = new HtmlParser();
    using var document = parser.ParseDocument(html);

    foreach (var node in document.Body!.ChildNodes)
    {
        if (node is not IElement element)
            continue;

        switch (element.TagName.ToLowerInvariant())
        {
            case "h1":
                body.AppendChild(StyledParagraph("Heading1", element.TextContent.Trim()));
                break;
            case "h2":
                body.AppendChild(StyledParagraph("Heading2", element.TextContent.Trim()));
                break;
            case "h3":
                body.AppendChild(StyledParagraph("Heading3", element.TextContent.Trim()));
                break;
            case "p":
                if (!string.IsNullOrWhiteSpace(element.TextContent))
                    body.AppendChild(BuildHtmlParagraph(element));
                else
                    body.AppendChild(new Paragraph());
                break;
            case "ul":
            case "ol":
                foreach (var li in element.QuerySelectorAll("li"))
                    body.AppendChild(BulletParagraph(li.TextContent.Trim()));
                break;
        }
    }
}

private static Paragraph BuildHtmlParagraph(IElement element)
{
    var para = new Paragraph();

    foreach (var child in element.ChildNodes)
    {
        if (child is IText textNode)
        {
            if (!string.IsNullOrEmpty(textNode.TextContent))
                para.AppendChild(new Run(
                    new Text(textNode.TextContent) { Space = SpaceProcessingModeValues.Preserve }));
        }
        else if (child is IElement inline)
        {
            var run = new Run(
                new Text(inline.TextContent) { Space = SpaceProcessingModeValues.Preserve });

            var rp = new RunProperties();
            switch (inline.TagName.ToLowerInvariant())
            {
                case "strong": rp.AppendChild(new Bold()); break;
                case "em":     rp.AppendChild(new Italic()); break;
                case "u":      rp.AppendChild(new Underline { Val = UnderlineValues.Single }); break;
                case "s":      rp.AppendChild(new Strike()); break;
                case "mark":   rp.AppendChild(new Highlight { Val = HighlightColorValues.Yellow }); break;
            }

            if (rp.HasChildren)
                run.PrependChild(rp);

            para.AppendChild(run);
        }
    }

    return para;
}
```

- [ ] **Step 5: Update `BuildDraftDocx` to route HTML vs markdown**

In `ExportTools.cs`, find `BuildDraftDocx`. Replace the final `AppendMarkdownContent(body, markdownDraft);` call with:

```csharp
// Auto-detect format: Blazor web mode sends Quill HTML; console mode sends markdown.
if (markdownDraft.TrimStart().StartsWith('<'))
    AppendHtmlContent(body, markdownDraft);
else
    AppendMarkdownContent(body, markdownDraft);
```

Also update the parameter name in the method signature from `markdownDraft` to `draftContent` and update the `[Description]` attribute on the `ExportDraftToWord` tool's `draftContent` parameter:

```csharp
[Description("The full job description draft content. Accepts HTML (from web editor) or markdown (from console). Headings, bullets, and bold text are preserved in the Word output.")]
string draftContent,
```

- [ ] **Step 6: Build McpServer to verify no compilation errors**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit Task 1**

```bash
git add DotnetAiAgentMcp/src/HrMcp.McpServer/HrMcp.McpServer.csproj
git add DotnetAiAgentMcp/src/HrMcp.McpServer/Tools/ExportTools.cs
git commit -m "feat(mcpserver): add HTML-aware Word export with AngleSharp, keep markdown for console mode"
```

---

### Task 2: Agent — Install Blazored.TextEditor and add scripts

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/_Imports.razor`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/App.razor`

**Interfaces:**
- Consumes: nothing from Task 1
- Produces: `BlazoredTextEditor` component available in all Razor files via `_Imports.razor`; Quill JS/CSS loaded via `App.razor`

---

- [ ] **Step 1: Add Blazored.TextEditor NuGet to HrMcp.Agent.csproj**

In `DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj`, add inside the existing `<ItemGroup>` with `<PackageReference>` entries:

```xml
<PackageReference Include="Blazored.TextEditor" Version="1.*" />
```

- [ ] **Step 2: Restore and verify**

```bash
dotnet restore DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: `Restore completed` with no errors.

- [ ] **Step 3: Add Blazored.TextEditor using to _Imports.razor**

In `DotnetAiAgentMcp/src/HrMcp.Agent/Components/_Imports.razor`, add after `@using MudBlazor`:

```razor
@using Blazored.TextEditor
```

- [ ] **Step 4: Add Quill CDN scripts and Blazored interop to App.razor**

In `DotnetAiAgentMcp/src/HrMcp.Agent/Components/App.razor`, add inside `<head>` after the `<link rel="stylesheet" href="css/app.css" />` line:

```html
<link href="https://cdn.quilljs.com/1.3.6/quill.snow.css" rel="stylesheet" />
```

And add before `</body>` (before the existing `<script src="_framework/blazor.web.js"></script>` line):

```html
<script src="https://cdn.quilljs.com/1.3.6/quill.min.js"></script>
<script src="_content/Blazored.TextEditor/quill-blaze.js"></script>
```

- [ ] **Step 5: Build Agent to verify scripts and package resolve**

```bash
dotnet build DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit Task 2**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj
git add DotnetAiAgentMcp/src/HrMcp.Agent/Components/_Imports.razor
git add DotnetAiAgentMcp/src/HrMcp.Agent/Components/App.razor
git commit -m "feat(agent): install Blazored.TextEditor, add Quill CDN scripts to App.razor"
```

---

### Task 3: DraftWorkspace — WYSIWYG editor + chat-first layout

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/wwwroot/css/app.css`

**Interfaces:**
- Consumes: `BlazoredTextEditor` component from Task 2; `AppendHtmlContent` in McpServer from Task 1 (indirectly via export agent call)
- Produces: Chat-first layout with auto-revealing WYSIWYG draft panel

---

- [ ] **Step 1: Add `_draftVisible` field and update `WorkspaceGridStyle`**

In `DraftWorkspace.razor` `@code` block, add after `private bool _leftPanelHidden;`:

```csharp
private bool _draftVisible;
```

Replace the existing `WorkspaceGridStyle` property with:

```csharp
private string WorkspaceGridStyle => _leftPanelHidden || !_draftVisible
    ? "grid-template-columns: minmax(0, 1fr);"
    : $"grid-template-columns: {_leftPanelWidth}px 8px minmax(0, 1fr);";
```

- [ ] **Step 2: Add `_richEditor` reference field**

In the `@code` block, add after `private bool _draftVisible;`:

```csharp
private BlazoredTextEditor _richEditor = default!;
```

Remove the `_document` field and `DraftDocumentState` usage — the editor is now the source of truth. Remove:
```csharp
private DraftDocumentState _document = new(string.Empty, 0);
```

Remove the `DraftPreview` property:
```csharp
private MarkupString DraftPreview => new(Markdown.ToHtml(_document.DraftText ?? string.Empty));
```

Remove the `DocumentTab` enum and `_activeTab` field:
```csharp
private DocumentTab _activeTab = DocumentTab.Edit;
private enum DocumentTab { Edit, View }
```

- [ ] **Step 3: Replace the right-editor section in markup**

Replace the entire `<section class="right-editor">...</section>` block with:

```razor
@if (_draftVisible)
{
    <section class="right-editor">
        <div class="right-header">
            <h3>Your Position Description Draft</h3>
        </div>

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
            <EditorContent>
            </EditorContent>
        </BlazoredTextEditor>

        <div class="panel-actions panel-actions--editor">
            <div class="panel-actions-row panel-actions-row--end">
                <button class="primary-btn primary-btn--compact" @onclick="ExportWordAsync" disabled="@_busy">Export Word</button>
                @if (!string.IsNullOrWhiteSpace(_status))
                {
                    <div class="chat-item panel-status">@_status</div>
                }
            </div>
        </div>
    </section>
}
```

- [ ] **Step 4: Update splitter visibility**

Find the splitter `<div>` in markup:
```razor
<div class="splitter" role="separator" aria-label="Resize chat and draft panels" @onmousedown="StartResize"></div>
```

Wrap it so it only shows when both panels are visible:
```razor
@if (_draftVisible)
{
    <div class="splitter" role="separator" aria-label="Resize chat and draft panels" @onmousedown="StartResize"></div>
}
```

- [ ] **Step 5: Update `SendPromptAsync` to inject HTML and reveal panel**

In `SendPromptAsync`, replace the existing draft-sync block:

```csharp
if (ShouldSyncDraft(input, response))
{
    _document.DraftText = response.Trim();
    _document.Revision += 1;
    _activeTab = DocumentTab.View;
}
```

With:

```csharp
if (ShouldSyncDraft(input, response))
{
    var html = Markdown.ToHtml(response.Trim());
    _draftVisible = true;
    await Task.Yield(); // allow Blazor to render the editor before loading content
    await _richEditor.LoadHTMLContent(html);
}
```

- [ ] **Step 6: Update `ExportWordAsync` to read HTML from editor**

In `ExportWordAsync`, replace:
```csharp
if (_busy || string.IsNullOrWhiteSpace(_document.DraftText))
{
    _status = "Draft is empty. Add content before exporting.";
    return;
}
```
With:
```csharp
var currentHtml = await _richEditor.GetHTML();
if (_busy || string.IsNullOrWhiteSpace(currentHtml))
{
    _status = "Draft is empty. Add content before exporting.";
    return;
}
```

And replace the `ExportDraftToWordAsync` call:
```csharp
var (message, fileName, fileBytes) = await AgentDraftService.ExportDraftToWordAsync(_document.DraftText);
```
With:
```csharp
var (message, fileName, fileBytes) = await AgentDraftService.ExportDraftToWordAsync(currentHtml);
```

- [ ] **Step 7: Remove unused `@using` directives**

In the `@using` block at the top of `DraftWorkspace.razor`, remove:
```razor
@using System.Net
@using System.Text
```
(Keep `@using Markdig`, `@using System.Text.RegularExpressions`, `@using HrMcp.Agent.Web.Models`, `@using HrMcp.Agent.Web.Services`, `@using Microsoft.AspNetCore.Components.Web`.)

Also remove the `@using HrMcp.Agent.Web.Models` if `DraftDocumentState` and `ChatTurn` are the only things it provides — keep it only if `ChatTurn` is still used (it is, for `_turns`).

- [ ] **Step 8: Update app.css — style WYSIWYG container, remove obsolete doc-editor rules**

In `DotnetAiAgentMcp/src/HrMcp.Agent/wwwroot/css/app.css`:

Remove the `.doc-editor` rule from the combined `.chat-input, .doc-editor` selector. Change:
```css
.chat-input,
.doc-editor {
    width: 100%;
    border: 1px solid #ccd4e2;
    border-radius: 6px;
    padding: 8px;
    box-sizing: border-box;
    resize: vertical;
}
```
To:
```css
.chat-input {
    width: 100%;
    border: 1px solid #ccd4e2;
    border-radius: 6px;
    padding: 8px;
    box-sizing: border-box;
    resize: vertical;
}
```

Remove the standalone `.doc-editor` rule:
```css
.doc-editor {
    flex: 1;
    min-height: 250px;
    margin-bottom: 8px;
}
```

Add after the `.chat-input` rule:
```css
/* Quill WYSIWYG editor — fills the right panel vertically */
.right-editor .ql-toolbar.ql-snow {
    border: 1px solid #ccd4e2;
    border-radius: 6px 6px 0 0;
    background: #f8faff;
    font-family: "IBM Plex Sans", "Segoe UI", Arial, sans-serif;
}

.right-editor .ql-container.ql-snow {
    flex: 1;
    overflow: auto;
    border: 1px solid #ccd4e2;
    border-top: none;
    border-radius: 0 0 6px 6px;
    margin-bottom: 8px;
    font-family: "IBM Plex Sans", "Segoe UI", Arial, sans-serif;
    font-size: 0.95rem;
    color: #1d2433;
}

.right-editor .ql-editor {
    min-height: 200px;
}
```

Also remove the `.doc-tabs`, `.tab-btn`, and `.tab-btn--active` rules since the tabs are removed. Find and delete:
```css
.doc-tabs {
    display: inline-flex;
    background: #f2f5fb;
    border: 1px solid #d7deea;
    border-radius: 8px;
    padding: 2px;
    gap: 2px;
}

.tab-btn {
    border: 0;
    background: transparent;
    color: #445372;
    border-radius: 6px;
    padding: 6px 10px;
    cursor: pointer;
    font-size: 0.9rem;
}

.tab-btn--active {
    background: #ffffff;
    color: #1f335b;
    border: 1px solid #cfd8ea;
}
```

And remove the `.markdown-preview`, `.markdown-preview--full`, `.markdown-preview p/ul/ol/h1/h2/h3`, `.preview-placeholder` rules (the preview panel is gone):
```css
.markdown-preview { ... }
.markdown-preview--full { ... }
.markdown-preview p, .markdown-preview ul, ... { ... }
.preview-placeholder { ... }
```

- [ ] **Step 9: Build the full solution**

```bash
dotnet build DotnetAiAgentMcp/DotnetAiAgentMcp.slnx
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 10: Smoke test in browser**

```bash
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --web
```

Open `http://localhost:5000`. Verify:

1. **Chat-first:** On load, only the chat panel shows (full-width). No draft panel visible.
2. **Draft reveal:** Send a draft-intent prompt (e.g. `Draft a position description for a GS-12 Software Engineer`). After the response, the draft panel slides in on the right with the WYSIWYG editor showing the formatted content.
3. **WYSIWYG toolbar:** Bold, Italic, Heading, Bullet, Ordered list, Highlight, Indent, Clean buttons visible and functional.
4. **User edits:** Manually edit text in the draft panel — formatting tools work.
5. **Export Word:** Click `Export Word` — a `.docx` file downloads. Open it in Word and confirm headings, bullets, and bold text are preserved.
6. **Chat-only prompt:** Send a non-draft prompt (e.g. `List open positions`). Verify draft panel does NOT update.

- [ ] **Step 11: Commit Task 3**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
git add DotnetAiAgentMcp/src/HrMcp.Agent/wwwroot/css/app.css
git commit -m "feat(ui): replace textarea with Blazored.TextEditor WYSIWYG, add chat-first layout with auto-reveal draft panel"
```
