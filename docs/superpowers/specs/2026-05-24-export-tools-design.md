# Export Tools Design

**Date:** 2026-05-24
**Status:** Approved
**Scope:** Add HTML, Word, and Excel export tools to the MCP server; update agent to handle streamed file output; design for SPA compatibility.

---

## Problem

The existing `RenderPositionAsUsaJobsHtml` tool saves the HTML file on the **server's file system** and returns a path string. This works in stdio mode (same machine) but breaks for any remote client — a SPA, Claude Desktop over network, or a future web agent — because the file is unreachable by the client.

Additionally, users need to export:
- A position's structured data as an **editable Word document** (same sections as the HTML)
- An **AI-generated job description draft** (maintained in LLM conversation context, possibly edited by the user) as an editable Word document
- The **list of all open positions** as an Excel spreadsheet

---

## Goals

- All export tools stream file content as base64 to the calling client — no server-side file writes
- The console agent decodes base64 and saves to `usajobs/output/`
- A future SPA decodes base64 and triggers a browser download — same tool, no changes needed
- Word documents mirror the USAJobs HTML structure so users have a familiar layout to edit offline
- All four tools share the same return contract: `{ "fileName": "...", "content": "<base64>" }`

---

## Tool Family

| Tool | Parameters | Output file | Content source |
|---|---|---|---|
| `ExportPositionToHtml` | `positionId` | `position-{id}.html` | Position DB fields → HTML template |
| `ExportPositionToWord` | `positionId` | `position-{id}.docx` | Position DB fields → Word doc |
| `ExportDraftToWord` | `positionId`, `draftContent` | `position-{id}-draft.docx` | LLM-generated draft text → Word doc |
| `ExportPositionsToExcel` | *(none)* | `positions.xlsx` | All open positions → Excel sheet |

`ExportPositionToHtml` is a **refactor** of the existing `RenderPositionAsUsaJobsHtml` — same content, new name, streaming return instead of server-side save. The old tool name is removed.

---

## Architecture

```
Any Client (console agent, SPA, Claude Desktop)
    │
    │  calls MCP tool
    ▼
ExportPosition* / ExportDraft* / ExportPositionsToExcel
    │
    ├─ Fetch position data (PositionService)        [all except ExportDraftToWord]
    ├─ Build file in memory (OpenXML / HTML string)
    └─ Return JSON: { "fileName": "...", "content": "<base64>" }
    │
    ▼
Client handles output:
    Console agent  → decode base64 → save to usajobs/output/
    SPA            → decode base64 → Blob → browser download
    Claude Desktop → decode base64 → attach or save
```

The MCP server is **stateless** — it never writes files to disk for export. The LLM conversation context is the source of truth for draft content.

---

## Draft Workflow

The draft is not stored in the database. It lives in the LLM's conversation context and may be updated by the user across multiple turns before export.

```
User: "write a job description for position 41"
  → LLM calls WriteJobDescription(41) → shows draft

User: "update qualifications to require PMP certification"
  → LLM rewrites qualifications in its response, holds full updated draft in context

User: "export the draft to Word"
  → LLM calls ExportDraftToWord(positionId=41, draftContent="<full current draft>")
  → Tool formats as .docx, returns base64
  → Agent/SPA saves or downloads position-41-draft.docx
```

The `draftContent` parameter carries whatever state the draft is in at the moment of export — original or edited. No server state needed.

---

## Return Contract

All four tools return the same JSON string:

```json
{ "fileName": "position-41.docx", "content": "<base64 encoded file bytes>" }
```

On error (position not found, etc.):

```
"Position 41 not found."
```

Plain string errors follow the same pattern as all other tools in this project.

---

## Word Document Structure (`ExportPositionToWord`)

Mirrors the USAJobs HTML template layout, adapted for a linear Word document (sidebar becomes an overview table at the top):

| Word element | Content source |
|---|---|
| Title (Heading 1) | `p.Title` |
| Department / Organization | `p.HiringOrganization` |
| Overview table (2-column) | All sidebar fields: status, dates, salary, grade, location, remote, telework, travel, relocation, appointment type, work schedule, service, promotion potential, series, supervisory status, clearance, drug test, adjudication, announcement # |
| Summary (Heading 2) | `p.Description` |
| This job is open to (Heading 2) | `p.HiringPath` (parsed into named paths) |
| Duties (Heading 2) | `p.Duties` |
| Requirements (Heading 2) | — |
| — Conditions of Employment (Heading 3) | `p.ConditionsOfEmployment` |
| — Qualifications (Heading 3) | `p.Qualifications` |
| — Education (Heading 3) | `p.Education` |
| — Additional Information (Heading 3) | `p.AdditionalInformation` |
| How you will be evaluated (Heading 2) | `p.Evaluations` |
| Required Documents (Heading 2) | `p.RequiredDocuments` |
| How to Apply (Heading 2) | `p.HowToApply` |
| — Contact information | `p.ContactName`, `p.ContactPhone`, `p.ContactEmail`, `p.ContactAddress` |
| — Next Steps (Heading 3) | `p.NextSteps` |

---

## Draft Word Document Structure (`ExportDraftToWord`)

Simpler than the full position export — focused on clean, editable prose:

| Word element | Content source |
|---|---|
| Title (Heading 1) | Fetched from DB by `positionId`: `p.Title` |
| Department / Organization | `p.HiringOrganization` |
| "AI Draft — For Review and Editing" notice | Static text in grey italic |
| Draft sections (Heading 2 per section) | Parsed from `draftContent` markdown (split on `##` headings) |

The `draftContent` is markdown text with `## Section` headings generated by `WriteJobDescription`. Each `##` heading becomes a Heading 2 in Word; body text becomes normal paragraphs; bullet lines become list items.

---

## Excel Structure (`ExportPositionsToExcel`)

One sheet named **Open Positions** with a frozen header row and auto-filter enabled.

Columns: ID, Title, Occupational Series, Pay Grade, Salary Min, Salary Max, Location, Telework Eligible, Security Clearance, Who May Apply, Department, Organization, Open Date, Close Date

---

## Implementation

### NuGet

Add to `HrMcp.McpServer.csproj`:

```xml
<PackageReference Include="DocumentFormat.OpenXml" Version="3.*" />
```

Used for `.docx` (`WordprocessingDocument`) and `.xlsx` (`SpreadsheetDocument`).

### New / Changed Files

| File | Change |
|---|---|
| `src/HrMcp.McpServer/Tools/ExportTools.cs` | New — contains all four export tools |
| `src/HrMcp.McpServer/Tools/PositionTools.cs` | Remove `RenderPositionAsUsaJobsHtml`; add `ExportPositionToHtml` |
| `src/HrMcp.McpServer/Program.cs` | Register `ExportTools` via `.WithTools<ExportTools>()` |
| `src/HrMcp.Agent/HrAgent.cs` | Intercept export tool results; decode base64; save to output folder |
| `src/HrMcp.Agent/Program.cs` | Pass `outputFolder` path to `HrAgent` |

> `ExportPositionToHtml` can stay in `PositionTools.cs` (same class, just renamed and refactored) to avoid moving unrelated code.

### Agent-Side Interception

`HrAgent.RunToolLoopAsync` detects export tool calls by name and handles the result before forwarding to the LLM:

```
private static readonly HashSet<string> ExportTools = new(StringComparer.OrdinalIgnoreCase)
{
    "ExportPositionToHtml",
    "ExportPositionToWord",
    "ExportDraftToWord",
    "ExportPositionsToExcel"
};
```

When an export tool result is detected:
1. Parse the JSON result to get `fileName` and `content`
2. Decode base64 → byte array
3. Save to `{outputFolder}/{fileName}`
4. Replace the raw result with `"Saved to: {fullPath}"` before adding to history
5. LLM reports the path to the user

### System Prompt Update

Add to `HrAgent` system prompt:

```
- To export a position's details, call ExportPositionToHtml or ExportPositionToWord with the position ID.
- To export a job description draft to Word, call ExportDraftToWord with the position ID and the full current draft text.
- To export all open positions to Excel, call ExportPositionsToExcel.
```

---

## Error Handling

- Position not found → return plain error string (consistent with all existing tools)
- Empty `draftContent` → return error string, do not generate a file
- OpenXML build failure → let exception propagate; MCP framework returns it as a tool error

---

## Out of Scope

- Storing drafts in the database
- PDF export
- Email sending (referenced in user workflow but not part of this design)
- Editing the Word template visually (no template file — docs built programmatically)
