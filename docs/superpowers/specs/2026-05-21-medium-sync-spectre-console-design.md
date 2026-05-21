# Design: Sync Medium Articles with Spectre.Console Updates

**Date:** 2026-05-21
**Project:** DotnetAiAgentMcp
**Scope:** Medium articles for Part 4 and Part 6 of Series 1

---

## Problem

Part-4 and Part-6 blog posts were updated locally (PR #7) with Spectre.Console code listings and terminal screenshots. The corresponding Medium articles (`b41d0b722b90` and `5ee56af86160`) still show the old `Console.Write/WriteLine` implementations and plain-text output blocks. Readers following along on Medium see outdated code.

---

## Approach

Option B — Surgical edits via Playwright automation. Navigate to each Medium edit URL, locate outdated code blocks by unique text, replace only the changed sections. Images are uploaded directly into the Medium editor using the `+` paragraph action → Photo upload flow. This preserves Medium-specific formatting (pull quotes, headers, formatting) outside the changed sections.

---

## Scope

| Article | Edit URL | Change type |
|---|---|---|
| Part 4: AI Agent with M.E.AI + Ollama | `https://medium.com/p/b41d0b722b90/edit` | Code listings (HrAgent.cs, Program.cs), package note, 3 screenshots |
| Part 6: Securing MCP with OIDC | `https://medium.com/p/5ee56af86160/edit` | Code listing patch (2 lines + constructor + using), sample run block |

---

## Screenshot Upload

**Source files** (already committed to repo):
- `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-1-mcp-server.png`
- `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-2-agent-startup.png`
- `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-3-conversation.png`

**Upload flow per image:**
1. Place cursor in an empty paragraph at the target location in the Medium editor
2. Click the `+` paragraph action button that appears on the left
3. Select "Photo" from the action menu
4. Use Playwright `file_upload` to provide the absolute local path of the PNG
5. Wait for the image to render in the editor before proceeding

**Insertion points in Part 4:**
- Screenshot 1 (server startup) + Screenshot 2 (agent startup) → replace the "You should see:" text/code block
- Screenshot 3 (conversation) → replace the "Sample conversation" fenced code block

---

## Surgical Code Edits

### Part 4 — `https://medium.com/p/b41d0b722b90/edit`

**Edit 1: HrAgent.cs code block**
- Locate by: unique text `public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools)` (old 2-param signature)
- Action: select entire code block → delete → paste new Spectre.Console version (UiStyle enum + primary constructor with `style` param + RunAsync with spinner + 3 render helpers)

**Edit 2: Package dependency note**
- Locate by: position immediately before the HrAgent.cs code block
- Action: insert new paragraph block with package note:
  > **Package dependency:** Add `Spectre.Console 0.49.*` to `HrMcp.Agent.csproj`

**Edit 3: Program.cs code block**
- Locate by: unique text `Console.WriteLine($"Connected. Tools:` inside a code block
- Action: select entire Program.cs code block → delete → paste new version with `using Spectre.Console`, `AnsiConsole.MarkupLine`, and style picker block

**Edit 4: "You should see:" section → screenshots**
- Locate by: paragraph text "You should see:"
- Action: delete the paragraph + the `text` code block that follows → upload Screenshot 1 (server startup) + Screenshot 2 (agent startup) in sequence

**Edit 5: "Sample conversation" section → screenshot**
- Locate by: heading or paragraph "Sample conversation"
- Action: delete the heading + the long `text` code block → upload Screenshot 3 (conversation)

### Part 6 — `https://medium.com/p/5ee56af86160/edit`

**Edit 1: `using Spectre.Console;`**
- Locate by: `using System.Text.Json;` in the Program.cs code block
- Action: add `using Spectre.Console;` on the line after `using System.Text.Json;`

**Edit 2: Token acquired line**
- Locate by: `Console.WriteLine("Token acquired.\n");`
- Action: replace with `AnsiConsole.MarkupLine("[green]✔[/] Token acquired.\n");`

**Edit 3: Connected line**
- Locate by: `Console.WriteLine($"Connected. Tools:`
- Action: replace with `AnsiConsole.MarkupLine($"[green]✔[/] Connected · Tools: [grey]{string.Join(", ", mcpTools.Select(t => t.Name))}[/]\n");`

**Edit 4: HrAgent constructor**
- Locate by: `new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList());`
- Action: replace with `new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), UiStyle.Structured);`

**Edit 5: Sample run bash block**
- Locate by: `# Token acquired.` inside a code block
- Action: replace entire block content with updated version showing `✔` prefixes and style picker output

---

## Execution Order

1. Open Part 4 edit URL, authenticate via saved Medium session (`medium-auth-state.json`)
2. Apply Part 4 edits 1–3 (code blocks) in top-to-bottom order
3. Apply Part 4 edits 4–5 (image uploads replacing text blocks)
4. Verify Part 4 renders correctly in preview
5. Open Part 6 edit URL
6. Apply Part 6 edits 1–5 in top-to-bottom order
7. Verify Part 6 renders correctly

Medium auto-saves after each change — no explicit save step needed between edits.

---

## Prerequisites

- `medium-auth-state.json` must exist at project root (Playwright saved browser session)
- Local PNG files must exist at `blogs/series-1-ai-agent-mcp/screenshots/`
- MCP server not required — this is browser automation only

---

## Out of Scope

- Part 1, 2, 3, 5 — no changes needed
- Standalone articles — not affected by Spectre.Console
- Publishing/submitting for review — articles remain in their current publish state
- Serilog changes — separate plan, not yet implemented
