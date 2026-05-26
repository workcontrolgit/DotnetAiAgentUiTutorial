# Medium Sync: Spectre.Console Updates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update Part 4 and Part 6 Medium articles with Spectre.Console code listings and terminal screenshots using Playwright browser automation against the Medium editor.

**Architecture:** Use Playwright MCP browser tools to navigate to each Medium edit URL (authenticated via `medium-auth-state.json`), locate outdated code blocks using `browser_evaluate` to search the editor DOM, replace content via clipboard injection + paste, and upload PNG screenshots via Medium's photo upload flow. Part 4 first (5 edits + 3 images), then Part 6 (5 targeted line edits).

**Tech Stack:** Playwright MCP browser tools (`browser_navigate`, `browser_snapshot`, `browser_evaluate`, `browser_click`, `browser_press_key`, `browser_file_upload`), Medium editor (contenteditable), saved auth session at `medium-auth-state.json`.

---

## Files

- Read: `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-1-mcp-server.png`
- Read: `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-2-agent-startup.png`
- Read: `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-3-conversation.png`
- Medium edit URLs (browser only — no local files modified):
  - Part 4: `https://medium.com/p/b41d0b722b90/edit`
  - Part 6: `https://medium.com/p/5ee56af86160/edit`

---

### Task 1: Update Part 4 Medium Article

**Edit URL:** `https://medium.com/p/b41d0b722b90/edit`

The Part 4 article needs: HrAgent.cs code block replaced, package note inserted, Program.cs code block replaced, "You should see:" block replaced with 2 screenshots, "Sample conversation" block replaced with 1 screenshot.

**Important notes about Medium's editor:**
- Medium uses a ProseMirror-based contenteditable editor
- Code blocks are `<pre>` elements inside the editor
- To replace a code block: click inside it → `Ctrl+A` to select all text → type/paste new content
- To insert an image: click an empty paragraph → click the `+` button → click the camera/photo icon → use file upload
- Always take a `browser_snapshot` before and after each edit to verify the change

---

- [ ] **Step 1: Navigate to Part 4 edit URL and verify authentication**

Using Playwright MCP tools:
```
browser_navigate: https://medium.com/p/b41d0b722b90/edit
```

Then take a snapshot to confirm the editor loaded (not a login page):
```
browser_snapshot
```

Expected: Editor loads showing Part 4 article content. If redirected to login, the `medium-auth-state.json` session has expired — stop and report BLOCKED.

---

- [ ] **Step 2: Replace HrAgent.cs code block**

The old code block contains `public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools)` (2-param, no Spectre.Console).

Use `browser_evaluate` to find and click into the code block:
```javascript
// Find the pre element containing the old HrAgent signature
const pres = document.querySelectorAll('pre');
const target = Array.from(pres).find(p => p.textContent.includes('public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools)'));
if (target) { target.click(); target.focus(); }
target?.getBoundingClientRect()
```

Then select all content in the block and replace with new content via clipboard:
```javascript
// Set clipboard to new HrAgent.cs content
await navigator.clipboard.writeText(`// src/HrMcp.Agent/HrAgent.cs
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace HrMcp.Agent;

public enum UiStyle { Structured, Minimal, Panels }

public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools, UiStyle style = UiStyle.Structured)
{
    private const string SystemPrompt = """
        You are an HR assistant for a U.S. federal agency. You help users explore open job
        positions, hiring organizations, and generate job announcements.

        Guidelines:
        - Always call GetHiringOrganizations before GetPositionsByOrganization to get valid IDs.
        - When asked about open positions, use GetOpenPositions first for an overview, then
          GetPositionById for full detail on a specific role.
        - When asked to write or generate a job description, call WriteJobDescription with the
          position ID — do not write one yourself.
        - When asked to display, show, render, or draft a position "in USAJobs format", "as a
          USAJobs page", or "like USAJobs", call RenderPositionAsUsaJobsHtml with the position ID.
          The tool saves the file and returns its path — tell the user the file path and that they
          can open it in a browser to see the USAJobs-style layout.
        - Present pay ranges in a readable format (e.g., "$85,000 – $110,000 per year").
        - Keep answers concise; offer to go deeper when the user wants more detail.
        """;

    private readonly List<ChatMessage> _history =
    [
        new(ChatRole.System, SystemPrompt)
    ];

    public async Task RunAsync(CancellationToken ct = default)
    {
        RenderWelcome();

        while (!ct.IsCancellationRequested)
        {
            var input = RenderUserPrompt();

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            _history.Add(new ChatMessage(ChatRole.User, input));

            ChatResponse response = default!;
            Exception? spinnerException = null;
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .Start("[blue]Thinking\u2026[/]", _ =>
                {
                    try
                    {
                        response = chatClient.GetResponseAsync(
                            _history,
                            new ChatOptions { Tools = tools },
                            ct).GetAwaiter().GetResult();
                    }
                    catch (Exception ex) { spinnerException = ex; }
                });
            if (spinnerException is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(spinnerException).Throw();

            _history.AddMessages(response);
            RenderResponse(response.Text ?? string.Empty);
        }
    }

    private void RenderWelcome()
    {
        switch (style)
        {
            case UiStyle.Structured:
            default:
                AnsiConsole.Write(new Rule("[bold cyan]HR Assistant[/]").RuleStyle("cyan").LeftJustified());
                AnsiConsole.MarkupLine("[grey]Ask about open positions, organizations, or job descriptions. Type [bold]exit[/] to quit.[/]\\n");
                break;
            case UiStyle.Minimal:
                AnsiConsole.MarkupLine("[teal]HR Assistant ready.[/] Ask about open positions, organizations, or job descriptions.");
                AnsiConsole.MarkupLine("[grey]Type [bold]exit[/] to quit.[/]\\n");
                break;
            case UiStyle.Panels:
                AnsiConsole.Write(
                    new Panel("[bold]HR Assistant[/]\\n[grey]Ask about open positions, organizations, or job descriptions.[/]")
                        .Header("[cyan]\uD83E\uDD16 Ready[/]")
                        .BorderColor(Color.Cyan1)
                        .Padding(1, 0));
                AnsiConsole.MarkupLine("[grey]Type [bold]exit[/] to quit.[/]\\n");
                break;
        }
    }

    private string RenderUserPrompt()
    {
        switch (style)
        {
            case UiStyle.Structured:
            default:
                AnsiConsole.Markup("[bold yellow]You \u203a[/] ");
                return Console.ReadLine() ?? string.Empty;
            case UiStyle.Minimal:
                AnsiConsole.Write(new Rule("[bold yellow]You[/]").RuleStyle("grey").LeftJustified());
                return Console.ReadLine() ?? string.Empty;
            case UiStyle.Panels:
                return AnsiConsole.Ask<string>("[bold yellow]You[/]");
        }
    }

    private void RenderResponse(string text)
    {
        switch (style)
        {
            case UiStyle.Structured:
            default:
                AnsiConsole.MarkupLine("\\n[bold green]Assistant \u203a[/]");
                AnsiConsole.Write(new Markup(Markup.Escape(text)));
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Minimal:
                AnsiConsole.Write(new Rule("[bold green]Assistant[/]").RuleStyle("grey").LeftJustified());
                AnsiConsole.Write(
                    new Panel(new Markup(Markup.Escape(text)))
                        .BorderColor(Color.Teal)
                        .Padding(1, 0));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Panels:
                AnsiConsole.Write(
                    new Panel(new Markup(Markup.Escape(text)))
                        .Header("[bold green]ASSISTANT[/]")
                        .BorderColor(Color.Aquamarine3)
                        .Padding(1, 0));
                AnsiConsole.WriteLine();
                break;
        }
    }
}`);
```

Then press `Ctrl+A` to select all within the block, then `Ctrl+V` to paste:
```
browser_press_key: ctrl+a
browser_press_key: ctrl+v
```

Take a snapshot to verify the block now contains `UiStyle` and `AnsiConsole`.

---

- [ ] **Step 3: Insert package dependency note before HrAgent.cs block**

After the HrAgent.cs block is updated, locate the paragraph immediately before it (the one describing "Create `src/HrMcp.Agent/HrAgent.cs`..."). Click at the end of that paragraph to position cursor, press `Enter` to create new paragraph, then type:

```
browser_press_key: End
browser_press_key: Enter
browser_type: Package dependency: Add Spectre.Console 0.49.* to HrMcp.Agent.csproj — <PackageReference Include="Spectre.Console" Version="0.49.*" />
```

Take a snapshot to verify the note appears.

---

- [ ] **Step 4: Replace Program.cs code block**

Find the code block containing `Console.WriteLine($"Connected. Tools:` and replace with the new Program.cs content.

Use `browser_evaluate` to locate and focus the block:
```javascript
const pres = document.querySelectorAll('pre');
const target = Array.from(pres).find(p => p.textContent.includes('Console.WriteLine($"Connected. Tools:'));
if (target) { target.click(); target.focus(); }
```

Set clipboard to new Program.cs content and paste:
```javascript
await navigator.clipboard.writeText(`// src/HrMcp.Agent/Program.cs
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;
using HrMcp.Agent;
using Spectre.Console;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

// Connect to the MCP server (must be running on http://localhost:5100)
await using var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri("http://localhost:5100/mcp")
    }));

var mcpTools = await mcpClient.ListToolsAsync();
AnsiConsole.MarkupLine($"[green]\u2714[/] Connected \u00b7 Tools: [grey]{string.Join(", ", mcpTools.Select(t => t.Name))}[/]\\n");

// Style picker (2-second auto-select, defaults to Structured)
var style = UiStyle.Structured;
AnsiConsole.MarkupLine("[bold]Select UI style:[/]");
AnsiConsole.MarkupLine("  [cyan][[1]][/] Structured \u2014 tables, panels, spinners [grey](default)[/]");
AnsiConsole.MarkupLine("  [cyan][[2]][/] Minimal    \u2014 rule-separated turns");
AnsiConsole.MarkupLine("  [cyan][[3]][/] Panels     \u2014 bordered panel per message");
AnsiConsole.Markup("[grey]Choice [[1]]:[/] ");

try
{
    var deadline = DateTime.UtcNow.AddSeconds(2);
    while (DateTime.UtcNow < deadline && !Console.KeyAvailable)
        await Task.Delay(100);
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(intercept: true);
        style = key.KeyChar switch { '2' => UiStyle.Minimal, '3' => UiStyle.Panels, _ => UiStyle.Structured };
    }
}
catch (OperationCanceledException) { }
AnsiConsole.MarkupLine($"[green]{style}[/]\\n");

IChatClient chatClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style);
await agent.RunAsync();`);
```

Then `Ctrl+A`, `Ctrl+V`. Verify snapshot shows `AnsiConsole` and style picker.

---

- [ ] **Step 5: Replace "You should see:" text block with 2 screenshots**

Find the paragraph containing "You should see:" using `browser_evaluate`:
```javascript
const paras = document.querySelectorAll('p');
const target = Array.from(paras).find(p => p.textContent.includes('You should see:'));
target?.scrollIntoView();
target?.getBoundingClientRect()
```

Click on that paragraph. Then select from that point through the end of the `text` fenced code block that follows (which shows the old `Connected. Tools:` output). Delete the selection.

Then upload Screenshot 1 (server startup):
1. Press `Enter` to create empty paragraph
2. Click the `+` button that appears on the left margin
3. Click the camera/photo icon in the action menu
4. Use `browser_file_upload` with path `c:/apps/DotnetMcpTutorial/blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-1-mcp-server.png`
5. Wait for image to render (`browser_wait_for`)

Then add a caption paragraph: `MCP server listening on http://localhost:5100`

Then repeat upload for Screenshot 2 (agent startup):
1. Press `Enter` after the caption to create empty paragraph
2. Click `+` → camera → file upload with `c:/apps/DotnetMcpTutorial/blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-2-agent-startup.png`
3. Wait for image to render
4. Add caption: `Agent startup — style picker with 2-second auto-select`

Take snapshot to verify both images appear.

---

- [ ] **Step 6: Replace "Sample conversation" section with Screenshot 3**

Find the "Sample conversation" heading:
```javascript
const headings = document.querySelectorAll('h3, h4');
const target = Array.from(headings).find(h => h.textContent.includes('Sample conversation'));
target?.scrollIntoView();
```

Click at the end of the heading. Select from there through the end of the long `text` fenced code block that follows (the one starting with `You: What organizations are hiring`). Delete.

Upload Screenshot 3 (conversation):
1. Press `Enter` to create empty paragraph after the heading
2. Click `+` → camera → file upload with `c:/apps/DotnetMcpTutorial/blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-3-conversation.png`
3. Wait for image to render
4. Add caption: `Structured style — colored labels, spinner during tool calls, bordered response`

Take final snapshot of Part 4 to verify all changes are visible.

---

- [ ] **Step 7: Verify Part 4 and commit progress note**

Take a full-page snapshot of the Part 4 editor. Confirm:
- HrAgent.cs block contains `UiStyle` and `AnsiConsole`
- Program.cs block contains style picker
- Package note paragraph exists
- 3 screenshots visible in article

Medium auto-saves. No explicit save needed.

---

### Task 2: Update Part 6 Medium Article

**Edit URL:** `https://medium.com/p/5ee56af86160/edit`

The Part 6 article needs 5 targeted line-level edits inside the Program.cs code block.

---

- [ ] **Step 1: Navigate to Part 6 edit URL**

```
browser_navigate: https://medium.com/p/5ee56af86160/edit
browser_snapshot
```

Expected: Part 6 editor loads showing OIDC content.

---

- [ ] **Step 2: Add `using Spectre.Console;` to the Part 6 Program.cs listing**

Find the code block containing `using System.Text.Json;`:
```javascript
const pres = document.querySelectorAll('pre');
const target = Array.from(pres).find(p => p.textContent.includes('using System.Text.Json;'));
target?.click(); target?.focus();
```

Use `browser_evaluate` to place cursor after `using System.Text.Json;` and insert new line:
```javascript
// Find text node and place cursor after "using System.Text.Json;"
const sel = window.getSelection();
const range = document.createRange();
// Walk text nodes to find the right position
const walker = document.createTreeWalker(target, NodeFilter.SHOW_TEXT);
let node;
while (node = walker.nextNode()) {
  if (node.textContent.includes('using System.Text.Json;')) {
    const idx = node.textContent.indexOf('using System.Text.Json;') + 'using System.Text.Json;'.length;
    range.setStart(node, idx);
    range.setEnd(node, idx);
    sel.removeAllRanges();
    sel.addRange(range);
    break;
  }
}
```

Then press `End` to go to end of line, `Enter` to create new line, type:
```
browser_press_key: End
browser_press_key: Enter
browser_type: using Spectre.Console;
```

Take snapshot to verify `using Spectre.Console;` appears after `using System.Text.Json;`.

---

- [ ] **Step 3: Replace `Console.WriteLine("Token acquired.\n")`**

Use `browser_evaluate` to find the exact text in the editor and select it:
```javascript
const pres = document.querySelectorAll('pre');
const target = Array.from(pres).find(p => p.textContent.includes('Console.WriteLine("Token acquired'));
target?.click(); target?.focus();
```

Use browser find (`Ctrl+F`) is not available in the editor — instead use `browser_evaluate` to select the specific line text, then type the replacement.

The reliable approach: click into the code block, use `browser_evaluate` to programmatically select the old line text, then type the new line:
```javascript
// Select the old line via range selection on text content
const walker = document.createTreeWalker(target, NodeFilter.SHOW_TEXT);
let node;
while (node = walker.nextNode()) {
  if (node.textContent.includes('Console.WriteLine("Token acquired')) {
    const start = node.textContent.indexOf('Console.WriteLine("Token acquired');
    const end = start + node.textContent.indexOf('\n', start) - start;
    const range = document.createRange();
    range.setStart(node, start);
    range.setEnd(node, start + 'Console.WriteLine("Token acquired.\\n");'.length);
    window.getSelection().removeAllRanges();
    window.getSelection().addRange(range);
    break;
  }
}
```

Then type replacement:
```
browser_type: AnsiConsole.MarkupLine("[green]✔[/] Token acquired.\n");
```

---

- [ ] **Step 4: Replace `Console.WriteLine($"Connected. Tools:` line**

Same approach — find the code block, select the old `Console.WriteLine($"Connected.` line, replace:

```javascript
const pres = document.querySelectorAll('pre');
const target = Array.from(pres).find(p => p.textContent.includes('Console.WriteLine($"Connected. Tools:'));
target?.click(); target?.focus();
// Select the line via range
const walker = document.createTreeWalker(target, NodeFilter.SHOW_TEXT);
let node;
while (node = walker.nextNode()) {
  if (node.textContent.includes('Console.WriteLine($"Connected. Tools:')) {
    const start = node.textContent.indexOf('Console.WriteLine($"Connected. Tools:');
    const lineEnd = node.textContent.indexOf('\n', start);
    const range = document.createRange();
    range.setStart(node, start);
    range.setEnd(node, lineEnd > -1 ? lineEnd : node.textContent.length);
    window.getSelection().removeAllRanges();
    window.getSelection().addRange(range);
    break;
  }
}
```

Type replacement:
```
browser_type: AnsiConsole.MarkupLine($"[green]✔[/] Connected · Tools: [grey]{string.Join(", ", mcpTools.Select(t => t.Name))}[/]\n");
```

---

- [ ] **Step 5: Replace `new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList())` line**

```javascript
const pres = document.querySelectorAll('pre');
const target = Array.from(pres).find(p => p.textContent.includes('new HrAgent(chatClient, mcpTools.Cast'));
target?.click(); target?.focus();
const walker = document.createTreeWalker(target, NodeFilter.SHOW_TEXT);
let node;
while (node = walker.nextNode()) {
  if (node.textContent.includes('new HrAgent(chatClient, mcpTools.Cast')) {
    const start = node.textContent.indexOf('new HrAgent(chatClient, mcpTools.Cast');
    const lineEnd = node.textContent.indexOf('\n', start);
    const range = document.createRange();
    range.setStart(node, start);
    range.setEnd(node, lineEnd > -1 ? lineEnd : node.textContent.length);
    window.getSelection().removeAllRanges();
    window.getSelection().addRange(range);
    break;
  }
}
```

Type replacement:
```
browser_type: var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), UiStyle.Structured);
```

---

- [ ] **Step 6: Update the sample run bash block**

Find the bash code block containing `# Token acquired.`:
```javascript
const pres = document.querySelectorAll('pre');
const target = Array.from(pres).find(p => p.textContent.includes('# Token acquired.'));
target?.click(); target?.focus();
```

Set clipboard to the new block content and paste via `Ctrl+A`, `Ctrl+V`:
```javascript
await navigator.clipboard.writeText(`dotnet run --project src/HrMcp.Agent
# ✔ Token acquired.
# ✔ Connected · Tools: GetOpenPositions, WriteJobDescription, ...
#
# Select UI style:
#   [1] Structured — tables, panels, spinners (default)
#   [2] Minimal    — rule-separated turns
#   [3] Panels     — bordered panel per message
# Choice [1]: Structured`);
```

Then `Ctrl+A`, `Ctrl+V`.

---

- [ ] **Step 7: Verify Part 6**

Take a final snapshot. Confirm:
- `using Spectre.Console;` present in Program.cs listing
- `AnsiConsole.MarkupLine` for token and connected lines
- `UiStyle.Structured` in HrAgent constructor call
- Sample run block shows `✔` prefixes and style picker

Medium auto-saves. Done.
