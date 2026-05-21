# Design: Update Blog Posts for Spectre.Console + Terminal Screenshots

**Date:** 2026-05-21
**Project:** DotnetAiAgentMcp
**Scope:** `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`, `blogs/series-1-ai-agent-mcp/part-6-mcp-security-oidc.md`

---

## Problem

`HrMcp.Agent` was upgraded to Spectre.Console in PR #6. Two blog posts still show the old `Console.Write/WriteLine` implementation and plain-text sample output. Readers following along will see a different startup experience (style picker, colored labels, spinner) than what the posts describe.

---

## Approach

Option 1 — Automated screenshot capture + targeted blog edits.

1. Start `HrMcp.McpServer` and `HrMcp.Agent` as real running processes.
2. Render their ANSI terminal output through an xterm.js HTML page served locally.
3. Use Playwright MCP to screenshot each capture point and save PNGs directly to the blog screenshots folder.
4. Edit both blog posts: replace outdated code listings and plain-text output blocks with the Spectre.Console versions and embedded screenshot images.

OIDC (Part 6) screenshot is deferred — requires a running IdentityServer stack. Part 6 receives code-only updates now; screenshot added in a future pass.

---

## Screenshot Capture Pipeline

```
Bash: dotnet run HrMcp.McpServer (background)
  └─ wait for "Now listening on http://localhost:5100"
Bash: dotnet run HrMcp.Agent (background, scripted inputs)
  └─ output piped through xterm.js HTML page via local server
Playwright MCP: browser_navigate → xterm.js page
Playwright MCP: browser_take_screenshot → save PNG at each capture point
```

**Output folder:** `blogs/series-1-ai-agent-mcp/screenshots/` (new directory)

---

## Screenshots (3)

| # | Filename | Capture point | Content |
|---|---|---|---|
| 1 | `part-4-screenshot-1-mcp-server.png` | After McpServer startup | Server log showing "Now listening on http://localhost:5100" |
| 2 | `part-4-screenshot-2-agent-startup.png` | After agent connects, before 2s timeout | Green ✔ Connected · style picker menu with 3 options · `Choice [1]:` prompt |
| 3 | `part-4-screenshot-3-conversation.png` | After first assistant response | Cyan HR Assistant rule · yellow `You ›` · blue spinner · green `Assistant ›` · grey rule divider |

**Dimensions:** 900×500px dark terminal background (matches Windows Terminal dark theme).

---

## Files Changed

### New files
- `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-1-mcp-server.png`
- `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-2-agent-startup.png`
- `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-3-conversation.png`

### Modified: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

1. **HrAgent.cs listing** — Replace the old `Console.Write/WriteLine` listing with the full Spectre.Console version: `UiStyle` enum, primary constructor with `style` param, `RunAsync` with `AnsiConsole.Status` spinner, and three private render helpers (`RenderWelcome`, `RenderUserPrompt`, `RenderResponse`).

2. **Program.cs agent listing** — Replace `Console.WriteLine($"Connected. Tools: ...")` with `AnsiConsole.MarkupLine` version. Update `new HrAgent(...)` to include `style` param.

3. **csproj note** — Add a sentence noting the `Spectre.Console 0.49.*` package dependency.

4. **"You should see:" block** — Replace the plain-text connected + HR Assistant ready text block with Screenshot 1 (server) and Screenshot 2 (agent startup / style picker).

5. **Sample conversation** — Replace the plain-text `You:` / `Assistant:` transcript with Screenshot 3.

### Modified: `blogs/series-1-ai-agent-mcp/part-6-mcp-security-oidc.md`

1. **Program.cs listing** — Update two lines:
   - `Console.WriteLine("Token acquired.\n")` → `AnsiConsole.MarkupLine("[green]✔[/] Token acquired.\n")`
   - `Console.WriteLine($"Connected. Tools: ...")` → `AnsiConsole.MarkupLine($"[green]✔[/] Connected · Tools: [grey]{...}[/]\n")`
   - `new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList())` → add `, style` third argument

2. **Sample run block** — Update the plain-text startup output to mention the style picker appears after the token and connected lines. No screenshot (OIDC stack not available).

---

## Out of Scope

- Part 6 OIDC startup screenshot — deferred until IdentityServer is running locally
- Parts 1–3, 5 — no changes needed (confirmed by review)
- Standalone blog posts — no changes needed
- Adding Serilog output to screenshots — Serilog plan not yet executed
