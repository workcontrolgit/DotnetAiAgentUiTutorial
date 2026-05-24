# MCP Pure Data Layer Design

**Date:** 2026-05-24
**Status:** Approved
**Scope:** Remove all LLM code from the MCP server. MCP server becomes a pure data layer. Agent is the only process that talks to an LLM.

---

## Problem

The MCP server currently injects `IChatClient` into `JobDescriptionTools` to generate job description drafts internally. This creates two problems:

1. **Conceptual confusion for learners:** MCP tools are expected to be deterministic data operations. A tool that secretly calls an LLM breaks that mental model. Learners see two `IChatClient` registrations (agent + server) and can't tell which one does what.

2. **Security barrier to web clients:** Any future SPA or Blazor WASM client would need LLM credentials. Keeping credentials on both the agent and server doubles the attack surface and blocks the path to a browser-based client.

---

## Goal

One rule, no exceptions: **MCP server = data tools only. Agent = conversation + AI.**

After this change:
- The MCP server has zero LLM packages, zero LLM config, zero `IChatClient`
- The agent's LLM generates job description drafts itself using position data fetched via `GetPositionById`
- `ExportDraftToWord` is unaffected — it takes `draftContent` as a parameter (pure document transformation, no LLM)

---

## Architecture

```
Before:
  Agent ──── IChatClient (LLM) ──── conversation loop
  McpServer ─ IChatClient (LLM) ──── WriteJobDescription tool

After:
  Agent ──── IChatClient (LLM) ──── conversation loop + draft generation
  McpServer ─ (no LLM) ──────────── pure data tools only
```

The agent's system prompt is updated to instruct the LLM: call `GetPositionById` to get position data, then write the draft itself in its response. No new tools needed.

---

## What Changes

### Delete

| File | Reason |
|---|---|
| `src/HrMcp.McpServer/Tools/JobDescriptionTools.cs` | Entire file removed — `WriteJobDescription` tool is eliminated |

### Modify: `HrMcp.McpServer.csproj`

Remove these packages (all LLM-related, no longer needed):
- `Microsoft.Extensions.AI.OpenAI`
- `Azure.AI.OpenAI`
- `Azure.Identity`
- `OllamaSharp`

`DocumentFormat.OpenXml` stays (used by `ExportTools`).

### Modify: `McpServer/appsettings.json` and `McpServer/appsettings.Development.json`

Remove the entire `AI` section from both files (`appsettings.Development.json` has `"AI": { "Provider": "Ollama" }`):
```json
"AI": {
  "Provider": "...",
  "AzureOpenAI": { ... },
  "Ollama": { ... }
}
```

### Modify: `McpServer/Program.cs`

Seven changes:

1. Remove `--num-ctx` CLI arg parsing (was only used to override `AI:Ollama:NumCtx`)
2. Remove `configOverrides` dictionary and `AddInMemoryCollection` call
3. Remove `IChatClient` registration from `ConfigureCommonServices`
4. Remove `CreateChatClient` static helper function
5. Remove LLM-related using directives (`Azure.AI.OpenAI`, `Azure.Identity`, `OllamaSharp`, `Microsoft.Extensions.AI`)
6. Remove `.WithTools<JobDescriptionTools>()` from both stdio and HTTP registrations
7. Simplify startup banners — remove Provider, Model, NumCtx lines (no AI config to display)

### Modify: `Agent/HrAgent.cs` — System Prompt

Replace:
```
- When asked to write a job description, call WriteJobDescription — do not write one yourself.
```

With:
```
- When asked to write a job description, call GetPositionById to get the full position data,
  then write a compelling USAJobs-style job announcement yourself with these sections:
  ## Summary, ## Duties, ## Qualifications Required, ## How to Apply.
  Use professional federal HR writing style.
```

### Unchanged

| File | Status |
|---|---|
| `ExportTools.cs` | No change — pure document transformation, no LLM |
| `PositionTools.cs` | No change |
| `HiringOrganizationTools.cs` | No change |
| `Agent/Program.cs` | No change |
| `HrAgent.cs` tool loop | No change — only system prompt changes |

---

## Teaching Narrative

This refactor is designed to be explained with a simple before/after:

**Before:** "Both agent and MCP server have LLM config — confusing, and a security barrier for any web client."

**After:** "MCP server = data tools (positions, organizations, exports). Agent = conversation + AI. One rule, no exceptions."

The `ExportDraftToWord` tool is a good teaching example to highlight: it performs complex output (Word document generation) entirely without LLM, because it receives the draft as a parameter from the agent's conversation context. This shows that "sophisticated output" does not require AI on the server.

---

## Future Web Client Path

With this change in place, the path to a Blazor Server web client becomes:

1. Add a new `HrMcp.Web` Blazor Server project
2. Move `HrAgent` conversation loop into a scoped service
3. Replace `Console.ReadLine` / `AnsiConsole` calls with Blazor component callbacks
4. LLM credentials stay server-side in `HrMcp.Web` — never reach the browser

The MCP server requires zero changes for the web client scenario.

---

## Error Handling

- No new error cases introduced
- Removing `WriteJobDescription` removes one tool error path
- If the LLM produces a poor draft, the user can ask it to revise — same as before

---

## Out of Scope

- Adding a Blazor or SPA web client
- Changing how the agent's conversation loop works
- Modifying any export tool behavior
- Changing the MCP transport (stdio / stream-http)
