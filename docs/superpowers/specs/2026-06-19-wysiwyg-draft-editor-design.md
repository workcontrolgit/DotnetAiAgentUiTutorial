# WYSIWYG Draft Editor — Design Spec

**Date:** 2026-06-19
**Project:** DotnetMcpTutorial / HrMcp.Agent

---

## Goal

Replace the plain markdown `<textarea>` in the Position Description draft panel with a Word-like WYSIWYG editor (Blazored.TextEditor / Quill). The draft panel should be hidden on startup (chat-first UX) and auto-reveal when the AI generates a draft. Export to Word must continue to work.

---

## Context

- The tutorial's main goal is teaching AI agent construction and MCP in C#. UI complexity is deliberately kept low.
- Current edit mode: raw `<textarea>` binding to `_document.DraftText` (markdown string).
- Current preview mode: separate "Preview Draft" tab rendering markdown via Markdig.
- Export Word: `ExportTools.cs` → `BuildDraftDocx()` → `AppendMarkdownContent()` — parses markdown line-by-line into OpenXML elements.
- Switching to WYSIWYG changes the stored format from **markdown** to **Quill HTML**, which breaks the existing Word export parser.

---

## Decisions

| Decision | Choice | Reason |
|---|---|---|
| WYSIWYG library | Blazored.TextEditor (NuGet Quill wrapper) | Minimal setup, no custom JS, tutorial-appropriate |
| Stored format | HTML (Quill output) | Natively output by Blazored.TextEditor; works with Word export + JSON serialization |
| JSON field extraction | AI-assisted (future phase) | Sending HTML to the agent for structured extraction fits existing agent pattern |
| HTML parser for Word export | AngleSharp NuGet | Lightweight, purpose-built HTML parser; maps cleanly to OpenXML |

---

## Layout & UX Behavior

### Chat-first startup
- On load: workspace shows chat panel **full-width**. Draft panel is hidden. Splitter is hidden.
- Controlled by a new `_draftVisible` boolean (default `false`).
- The workspace grid style already handles this via `WorkspaceGridStyle` — extend it to also collapse the right panel when `_draftVisible = false`.

### Draft panel auto-reveal
- When `ShouldSyncDraft(prompt, response)` returns `true`, set `_draftVisible = true`.
- Inject the AI response HTML into the WYSIWYG editor via `LoadHTMLContent(html)`.
- Draft panel stays visible for the remainder of the session.

### Tab removal
- Remove the **Edit Draft** / **Preview Draft** tab buttons.
- The WYSIWYG editor is always visible in the draft panel — no mode switching needed.
- The right panel header retains "Your Position Description Draft" title.

### Hide Chat toggle
- The existing "Hide Chat" / "Show Chat" button on the topbar continues to collapse the left (chat) panel.
- No separate "hide draft" control is added — keep the panel clean.

---

## WYSIWYG Editor (Blazored.TextEditor)

### NuGet
```
Blazored.TextEditor
```
Added to `HrMcp.Agent.csproj`.

### Service registration
```csharp
// Program.cs
builder.Services.AddBlazoredTextEditor();
```

### Script/CSS references
Add to `App.razor` `<head>`:
```html
<link href="//cdn.quilljs.com/1.3.6/quill.snow.css" rel="stylesheet" />
<script src="//cdn.quilljs.com/1.3.6/quill.min.js"></script>
<script src="_content/Blazored.TextEditor/quill-blaze.js"></script>
```

### Toolbar configuration
The `<BlazoredTextEditor>` component is placed in the right panel where the `<textarea>` was. Toolbar includes:

- Text style: Bold, Italic, Underline, Strikethrough
- Headings: H1, H2, H3 (dropdown selector)
- Lists: Bullet list, Ordered list
- Indentation: Indent, Outdent
- Highlight (background color)
- Clean formatting

### Component reference
```razor
<BlazoredTextEditor @ref="_richEditor" Placeholder="Your draft will appear here...">
    <ToolbarContent>
        <!-- toolbar buttons -->
    </ToolbarContent>
</BlazoredTextEditor>

@code {
    private BlazoredTextEditor _richEditor = default!;
}
```

### Reading content
```csharp
var html = await _richEditor.GetHTML();
```

### Injecting AI draft
```csharp
await _richEditor.LoadHTMLContent(htmlContent);
```

---

## Draft Sync & Data Flow

1. User sends a prompt via chat.
2. `SendPromptAsync()` calls `AgentDraftService.SendPromptAsync(prompt)` — unchanged.
3. If `ShouldSyncDraft(prompt, response)` is true:
   - Convert the markdown response to HTML via Markdig (same pipeline already in place) before injecting into the editor. This avoids changing the agent's markdown output format.
   - Call `await _richEditor.LoadHTMLContent(html)`.
   - Set `_draftVisible = true` to reveal the draft panel.
4. `_document.DraftText` is replaced by reading from the editor on demand (not bound two-way). The editor is the source of truth.

### Markdown → HTML on AI sync
The AI still returns markdown. Before injecting into the WYSIWYG editor, convert using Markdig:
```csharp
var html = Markdown.ToHtml(response.Trim());
await _richEditor.LoadHTMLContent(html);
```
This preserves the existing agent/MCP layer unchanged — the AI writes markdown, the UI renders it as formatted HTML in the editor. The user then edits in WYSIWYG mode.

---

## Export Word

### Problem
`ExportTools.cs → AppendMarkdownContent()` parses markdown tokens (`##`, `- `, `**`). When the user edits in the WYSIWYG editor and re-exports, the content is now HTML.

### Solution
Add `AppendHtmlContent(Body body, string html)` to `ExportTools.cs` using `AngleSharp` to walk the HTML DOM and emit OpenXML elements:

| HTML element | OpenXML output |
|---|---|
| `<h1>` | `StyledParagraph("Heading1", ...)` |
| `<h2>` | `StyledParagraph("Heading2", ...)` |
| `<h3>` | `StyledParagraph("Heading3", ...)` |
| `<p>` | `RichParagraph(null, ...)` with inline bold/italic handling |
| `<ul><li>` | `BulletParagraph(...)` |
| `<ol><li>` | Numbered paragraph (new) |
| `<strong>` | Bold run inside parent paragraph |
| `<em>` | Italic run |
| `<u>` | Underline run |
| `<mark>` | Highlighted run (yellow shading via OpenXML `Highlight`) |
| empty / whitespace | `new Paragraph()` (blank line) |

`BuildDraftDocx()` is updated to call `AppendHtmlContent()` instead of `AppendMarkdownContent()`.

### NuGet
```
AngleSharp
```
Added to `HrMcp.McpServer.csproj`.

### Tool description update
Update the `[Description]` on `ExportDraftToWord` parameter `draftContent`:
```
"The full job description draft content (HTML format). Headings, bullets, and bold text are preserved in the Word output."
```

### ExportDraftToWordAsync (AgentDraftService)
No change to the C# method signature or the agent prompt. The agent sends whatever string is passed — now HTML instead of markdown. The MCP tool receives it and calls `AppendHtmlContent()`.

---

## Styling

`app.css` additions:
- Set a fixed height with overflow scroll on the `.ql-editor` container so the WYSIWYG editor fills the right panel cleanly.
- Match border/background to the existing `doc-editor` textarea style.
- Remove `.doc-editor` textarea rule once replaced.

---

## Future Phase Hook: JSON Field Extraction

When needed, add `ExtractFieldsAsync()` to `IAgentDraftService`:
```csharp
Task<PositionDraftFields?> ExtractFieldsAsync(string htmlContent, CancellationToken ct = default);
```

Implementation sends the HTML to the agent with a structured extraction prompt:
> "Extract the following fields from this position description HTML and return JSON only: gsLevel, payGrade, duties (array), qualifications (array), education, series."

The agent returns a JSON string; the service deserializes it to `PositionDraftFields`. This is wired to a future "Save to DB" button in the draft panel.

---

## Change Surface Summary

| File | Change |
|---|---|
| `DraftWorkspace.razor` | Replace `<textarea>` + tabs with `<BlazoredTextEditor>`; add `_draftVisible` flag; update `SendPromptAsync` to inject HTML and reveal panel; update `ExportWordAsync` to read HTML from editor |
| `HrMcp.Agent.csproj` | Add `Blazored.TextEditor` NuGet |
| `Program.cs` (Agent) | Add `builder.Services.AddBlazoredTextEditor()` |
| `App.razor` | Add Quill CDN CSS/JS + Blazored script tags |
| `app.css` | Style WYSIWYG editor container; remove old `doc-editor` textarea rule |
| `ExportTools.cs` (McpServer) | Add `AppendHtmlContent()`; update `BuildDraftDocx()` to call it; update tool `[Description]` |
| `HrMcp.McpServer.csproj` | Add `AngleSharp` NuGet |

---

## Out of Scope

- JSON field extraction / Save to DB (future phase)
- HR Specialist review/approval workflow (deferred per MVP scope)
- Session persistence / reload previous draft
