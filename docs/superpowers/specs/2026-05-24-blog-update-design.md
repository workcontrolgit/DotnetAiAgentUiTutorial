# Blog Update Design — Align Series with Codebase

**Date:** 2026-05-24  
**Approach:** Surgical edits to Part 3 and Part 4 only  
**Goal:** Remove deleted content, reflect the pure-data-layer McpServer, multi-model agent, and export tools

---

## Context

The codebase diverged from the blog after two architectural decisions:

1. **`JobDescriptionTools` deleted** — `WriteJobDescription` was a server-side MCP tool that called Ollama to generate job description narratives. It was removed. The LLM now writes job descriptions directly in the agent via the system prompt.
2. **McpServer became a pure data layer** — `OllamaSharp` and all LLM wiring were removed from `HrMcp.McpServer`. The server only exposes data tools and export tools.
3. **ExportTools added** — `ExportTools.cs` on the McpServer exposes four tools that return base64-encoded file content. The agent intercepts these results and saves the files to disk.

Parts affected: **Part 3** and **Part 4**.

---

## Part 3 Changes

### Remove

- `Tools/JobDescriptionTools.cs` from the folder listing in Step 2
- The `JobDescriptionTools.cs` code block and its prose description
- `.WithTools<JobDescriptionTools>()` from the `Program.cs` registration snippet
- The MCP Inspector "Calling WriteJobDescription" walkthrough section
- The summary bullet: "WriteJobDescription returns a structured stub, ready for LLM upgrade in Part 4"
- The Part 4 preview sentence referencing `WriteJobDescription`

### Update

- Tool summary: **4 tools** — `GetOpenPositions`, `GetPositionById`, `GetPositionsByOrganization`, `GetHiringOrganizations`
- Tool count references throughout Part 3 (was "5 tools", "3 tool classes")

### No structural changes

Part 3's section headings, intro, Step 1 (SDK install), and conclusion prose are unchanged.

---

## Part 4 Changes

### Remove

- "add `OllamaSharp` to `HrMcp.McpServer`" package step and rationale
- All of "Step 4 — Upgrade WriteJobDescription to LLM Output" section (code + prose)
- `IChatClient` singleton registration in any McpServer `Program.cs` snippets
- All references to `llama3.2` as the model name

### Update — Step 1: Packages

- Agent packages: keep `OllamaSharp`, add `Azure.AI.OpenAI` as the optional swap-in
- Remove McpServer package step entirely
- Explain why two providers: local dev (Ollama/gemma4) vs. production (Azure OpenAI)

### Update — Agent Wiring (Program.cs)

Replace the single Ollama `IChatClient` wiring with the multi-provider pattern:

- `AI:Provider` config key: `"Ollama"` (default) or `"AzureOpenAI"`
- Default model: `gemma4:latest` via Ollama
- `CreateChatClient()` helper branches on config — show full method
- User secrets for Azure endpoint/key; `appsettings.json` for Ollama URL/model

### Add — Section: "Job Descriptions — the LLM Writes Them"

Short section explaining the architectural shift:

- `WriteJobDescription` was a server-side tool; it is now deleted
- The agent's system prompt instructs the LLM to call `GetPositionById` first, then write a USAJobs-style job announcement itself (`## Summary`, `## Duties`, `## Qualifications Required`, `## How to Apply`)
- No extra code — the LLM handles the full narrative; the tool loop is unchanged

### Add — Section: "Export Tools"

Introduce `ExportTools.cs` on the McpServer:

| Tool | Output |
|---|---|
| `ExportPositionToHtml` | USAJobs-style HTML page (in `PositionTools.cs`) |
| `ExportPositionToWord` | Full position data as `.docx` |
| `ExportDraftToWord` | LLM-generated draft as `.docx` (markdown → Word headings/bold/bullets) |
| `ExportPositionsToExcel` | All open positions as `.xlsx` |

- NuGet: `DocumentFormat.OpenXml 3.*`
- All tools return `{ "fileName": "...", "content": "<base64>" }` — server never writes to disk
- Markdown-to-Word rendering: `AppendMarkdownContent` parses `## headings`, `* bullets`, and `**bold**` into proper OpenXML runs

### Add — Section: "Agent-Side File Interception"

Explain the architecture decision — why base64 over the wire instead of server-side file writes:

**Why:**
- The server has no knowledge of the client's filesystem path
- The same base64 payload works for a console agent (saves to disk), Claude Desktop (passes to host), or a future SPA client (triggers browser download)
- Keeps the MCP server stateless and transport-agnostic

**How:**
- `ExportToolNames` — `HashSet<string>` of tool names that return export payloads
- Tool result arrives as `TextContent` from the MCP SDK; `.Text` property holds the JSON string
- `TrySaveExportFile(json, outputFolder)` — parses `{ fileName, content }`, decodes base64, writes to `usajobs/output/`
- `FindOutputFolder()` — walks up from `AppContext.BaseDirectory` to find the `usajobs/` folder
- Show the `TextContent` unwrapping fix: `rawResult switch { TextContent tc => tc.Text, ... }`

### Update — Summary

- Tool count: **8 tools** across 3 classes (`PositionTools`: 4, `HiringOrganizationTools`: 1, `ExportTools`: 3)
- Remove "WriteJobDescription upgraded" bullet
- Add: "McpServer is a pure data layer — no LLM dependency"
- Add: "Export tools stream base64; agent intercepts and saves to usajobs/output/"
- Add: "Multi-model support — Ollama (gemma4, default) or Azure OpenAI via config"

---

## Files Changed

| File | Change |
|---|---|
| `blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md` | Remove JobDescriptionTools content, update tool counts |
| `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md` | Remove McpServer LLM wiring + WriteJobDescription section; add multi-model, JD-by-LLM, ExportTools, interception sections |

No other files changed.

---

## Out of Scope

- Part 1, Part 2, Part 5, Part 6 — no changes
- Standalone blog posts — not touched
- Code changes — this is a blog-only update
