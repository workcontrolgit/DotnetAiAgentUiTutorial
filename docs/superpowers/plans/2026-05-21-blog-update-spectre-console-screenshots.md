# Blog Update: Spectre.Console + Terminal Screenshots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update `part-4` and `part-6` blog posts with correct Spectre.Console code listings and embed 3 terminal screenshots taken via CSS-styled HTML pages captured by Playwright.

**Architecture:** Three CSS-styled HTML terminal mockup pages (server startup, agent startup, conversation) are created under `scripts/blog-screenshots/`, Playwright MCP navigates to each `file://` URL and saves a PNG to `blogs/series-1-ai-agent-mcp/screenshots/`. Then `part-4` gets new code listings and embedded screenshots; `part-6` gets a 3-line code patch and updated sample run block.

**Tech Stack:** HTML/CSS terminal mockup, Playwright MCP (`browser_navigate`, `browser_take_screenshot`), Markdown blog posts.

---

## Files

- Create: `scripts/blog-screenshots/server-startup.html`
- Create: `scripts/blog-screenshots/agent-startup.html`
- Create: `scripts/blog-screenshots/conversation.html`
- Create: `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-1-mcp-server.png`
- Create: `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-2-agent-startup.png`
- Create: `blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-3-conversation.png`
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`
- Modify: `blogs/series-1-ai-agent-mcp/part-6-mcp-security-oidc.md`

---

### Task 1: Create terminal mockup HTML pages and capture screenshots with Playwright

**Files:**
- Create: `scripts/blog-screenshots/server-startup.html`
- Create: `scripts/blog-screenshots/agent-startup.html`
- Create: `scripts/blog-screenshots/conversation.html`
- Create: `blogs/series-1-ai-agent-mcp/screenshots/` (directory)

- [ ] **Step 1: Create `scripts/blog-screenshots/` directory**

```bash
mkdir -p c:/apps/DotnetMcpTutorial/scripts/blog-screenshots
mkdir -p c:/apps/DotnetMcpTutorial/blogs/series-1-ai-agent-mcp/screenshots
```

- [ ] **Step 2: Write `server-startup.html`**

Write to `c:/apps/DotnetMcpTutorial/scripts/blog-screenshots/server-startup.html`:

```html
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    background: #0c0c0c;
    font-family: 'Cascadia Code', 'Consolas', 'Courier New', monospace;
    font-size: 14px;
    line-height: 1.6;
    padding: 20px 24px;
    width: 900px;
    min-height: 320px;
    color: #cccccc;
  }
  .dim   { color: #666; }
  .green { color: #6adf73; }
  .grey  { color: #999; }
  .white { color: #ffffff; }
</style>
</head>
<body>
<pre><span class="dim">info: Microsoft.Hosting.Lifetime[14]
      Now listening on: </span><span class="green">http://localhost:5100</span>
<span class="dim">info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: C:\apps\DotnetMcpTutorial\DotnetAiAgentMcp\src\HrMcp.McpServer</span></pre>
</body>
</html>
```

- [ ] **Step 3: Write `agent-startup.html`**

Write to `c:/apps/DotnetMcpTutorial/scripts/blog-screenshots/agent-startup.html`:

```html
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    background: #0c0c0c;
    font-family: 'Cascadia Code', 'Consolas', 'Courier New', monospace;
    font-size: 14px;
    line-height: 1.6;
    padding: 20px 24px;
    width: 900px;
    min-height: 380px;
    color: #cccccc;
  }
  .green  { color: #6adf73; }
  .grey   { color: #888; }
  .cyan   { color: #4fc3f7; }
  .bold   { font-weight: bold; }
  .white  { color: #ffffff; }
</style>
</head>
<body>
<pre><span class="green">✔</span> Connected · Tools: <span class="grey">GetOpenPositions, GetPositionById, GetPositionsByOrganization, GetHiringOrganizations, WriteJobDescription, RenderPositionAsUsaJobsHtml</span>

<span class="bold white">Select UI style:</span>
  <span class="cyan bold">[1]</span> Structured — tables, panels, spinners <span class="grey">(default)</span>
  <span class="cyan bold">[2]</span> Minimal    — rule-separated turns
  <span class="cyan bold">[3]</span> Panels     — bordered panel per message
<span class="grey">Choice [1]:</span> </pre>
</body>
</html>
```

- [ ] **Step 4: Write `conversation.html`**

Write to `c:/apps/DotnetMcpTutorial/scripts/blog-screenshots/conversation.html`:

```html
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    background: #0c0c0c;
    font-family: 'Cascadia Code', 'Consolas', 'Courier New', monospace;
    font-size: 14px;
    line-height: 1.6;
    padding: 20px 24px;
    width: 900px;
    min-height: 500px;
    color: #cccccc;
  }
  .cyan   { color: #4fc3f7; }
  .grey   { color: #888; }
  .yellow { color: #ffd54f; font-weight: bold; }
  .green  { color: #6adf73; font-weight: bold; }
  .blue   { color: #64b5f6; }
  .rule   { color: #444; }
  .bold   { font-weight: bold; }
  .dim    { color: #666; }
  hr-line { display: block; color: #444; }
</style>
</head>
<body>
<pre><span class="cyan bold">── HR Assistant </span><span class="rule">────────────────────────────────────────────────────────────────</span>
<span class="dim">Ask about open positions, organizations, or job descriptions. Type </span><span class="bold" style="color:#ccc">exit</span><span class="dim"> to quit.</span>

<span class="yellow">You ›</span> What organizations are hiring right now?
<span class="blue">⠿ Thinking…</span>

<span class="green">Assistant ›</span>
Here are the federal hiring organizations currently in the database:

• <span class="bold" style="color:#e0e0e0">U.S. Citizenship and Immigration Services</span> (Dept. of Homeland Security) — 4 open positions
• <span class="bold" style="color:#e0e0e0">Transportation Security Administration</span> (Dept. of Homeland Security) — 3 open positions
• <span class="bold" style="color:#e0e0e0">U.S. Coast Guard</span> (Dept. of Homeland Security) — 2 open positions
• <span class="bold" style="color:#e0e0e0">Federal Emergency Management Agency</span> (Dept. of Homeland Security) — 1 open position

Would you like to see specific positions for any of these organizations?

<span class="rule">────────────────────────────────────────────────────────────────────────────────────</span>

<span class="yellow">You ›</span> </pre>
</body>
</html>
```

- [ ] **Step 5: Screenshot `server-startup.html` → PNG**

Using Playwright MCP tools:
1. Navigate to `file:///c:/apps/DotnetMcpTutorial/scripts/blog-screenshots/server-startup.html`
2. Take screenshot and save to `c:/apps/DotnetMcpTutorial/blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-1-mcp-server.png`

- [ ] **Step 6: Screenshot `agent-startup.html` → PNG**

Using Playwright MCP tools:
1. Navigate to `file:///c:/apps/DotnetMcpTutorial/scripts/blog-screenshots/agent-startup.html`
2. Take screenshot and save to `c:/apps/DotnetMcpTutorial/blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-2-agent-startup.png`

- [ ] **Step 7: Screenshot `conversation.html` → PNG**

Using Playwright MCP tools:
1. Navigate to `file:///c:/apps/DotnetMcpTutorial/scripts/blog-screenshots/conversation.html`
2. Take screenshot and save to `c:/apps/DotnetMcpTutorial/blogs/series-1-ai-agent-mcp/screenshots/part-4-screenshot-3-conversation.png`

- [ ] **Step 8: Verify all 3 PNGs exist**

```bash
ls c:/apps/DotnetMcpTutorial/blogs/series-1-ai-agent-mcp/screenshots/
```

Expected: 3 PNG files listed.

- [ ] **Step 9: Commit**

```bash
cd c:/apps/DotnetMcpTutorial
git add scripts/blog-screenshots/
git add blogs/series-1-ai-agent-mcp/screenshots/
git commit -m "feat: add terminal screenshot mockups and blog PNG captures"
```

---

### Task 2: Update `part-4-ai-agent-extensions-ai.md`

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

Read the file first to confirm current line numbers match before editing.

- [ ] **Step 1: Replace the HrAgent.cs code listing**

Find this block (starts at the line containing `public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools)`):

```
## Step 2 — `HrAgent.cs`

Create `src/HrMcp.Agent/HrAgent.cs`. This class owns the conversation loop and keeps message history. It takes `IChatClient` and the list of MCP tools as constructor parameters — both are wired in `Program.cs`.

```csharp
// src/HrMcp.Agent/HrAgent.cs
using Microsoft.Extensions.AI;

namespace HrMcp.Agent;

public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools)
{
```

Replace that entire `## Step 2 — HrAgent.cs` section's code block (from the opening ` ```csharp ` to the closing ` ``` `) with:

```csharp
// src/HrMcp.Agent/HrAgent.cs
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
                .Start("[blue]Thinking…[/]", _ =>
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
                AnsiConsole.MarkupLine("[grey]Ask about open positions, organizations, or job descriptions. Type [bold]exit[/] to quit.[/]\n");
                break;
            case UiStyle.Minimal:
                AnsiConsole.MarkupLine("[teal]HR Assistant ready.[/] Ask about open positions, organizations, or job descriptions.");
                AnsiConsole.MarkupLine("[grey]Type [bold]exit[/] to quit.[/]\n");
                break;
            case UiStyle.Panels:
                AnsiConsole.Write(
                    new Panel("[bold]HR Assistant[/]\n[grey]Ask about open positions, organizations, or job descriptions.[/]")
                        .Header("[cyan]🤖 Ready[/]")
                        .BorderColor(Color.Cyan1)
                        .Padding(1, 0));
                AnsiConsole.MarkupLine("[grey]Type [bold]exit[/] to quit.[/]\n");
                break;
        }
    }

    private string RenderUserPrompt()
    {
        switch (style)
        {
            case UiStyle.Structured:
            default:
                AnsiConsole.Markup("[bold yellow]You ›[/] ");
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
                AnsiConsole.MarkupLine("\n[bold green]Assistant ›[/]");
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
}
```

Also add a note about the package immediately after the opening of Step 2, before the code block:

```
> **Package dependency:** Add `Spectre.Console 0.49.*` to `HrMcp.Agent.csproj`:
> ```xml
> <PackageReference Include="Spectre.Console" Version="0.49.*" />
> ```
```

- [ ] **Step 2: Replace the Program.cs agent code listing**

Find the `## Step 3 — Program.cs for HrMcp.Agent` section. Replace the code block (the one showing `Console.WriteLine($"Connected. Tools: ...")` and `new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList())`) with:

```csharp
// src/HrMcp.Agent/Program.cs
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
AnsiConsole.MarkupLine($"[green]✔[/] Connected · Tools: [grey]{string.Join(", ", mcpTools.Select(t => t.Name))}[/]\n");

// ── Style picker (2-second auto-select, defaults to Structured) ───────────────
var style = UiStyle.Structured;
AnsiConsole.MarkupLine("[bold]Select UI style:[/]");
AnsiConsole.MarkupLine("  [cyan][[1]][/] Structured — tables, panels, spinners [grey](default)[/]");
AnsiConsole.MarkupLine("  [cyan][[2]][/] Minimal    — rule-separated turns");
AnsiConsole.MarkupLine("  [cyan][[3]][/] Panels     — bordered panel per message");
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
AnsiConsole.MarkupLine($"[green]{style}[/]\n");
// ─────────────────────────────────────────────────────────────────────────────

IChatClient chatClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style);
await agent.RunAsync();
```

- [ ] **Step 3: Replace "You should see:" plain-text block with screenshots**

Find this block:

```
You should see:

```text
Connected. Tools: GetOpenPositions, GetPositionById, GetPositionsByOrganization, GetHiringOrganizations, WriteJobDescription

HR Assistant ready. Ask about open positions, organizations, or job descriptions.
Type 'exit' to quit.

You: 
```
```

Replace with:

```
You should see the MCP server log in the first terminal:

![MCP server listening on http://localhost:5100](screenshots/part-4-screenshot-1-mcp-server.png)

And in the agent terminal — a style picker with a 2-second auto-select defaulting to **Structured**:

![Agent startup showing style picker](screenshots/part-4-screenshot-2-agent-startup.png)
```

- [ ] **Step 4: Replace the plain-text sample conversation with screenshot**

Find the `### Sample conversation` section containing the `You: What organizations are hiring right now?` block.

Replace the entire `### Sample conversation` section (the heading + the fenced code block) with:

```markdown
### Sample conversation

The agent uses colored labels, a spinner while calling MCP tools, and a horizontal rule after each response:

![Sample conversation in Structured style](screenshots/part-4-screenshot-3-conversation.png)
```

- [ ] **Step 5: Commit**

```bash
cd c:/apps/DotnetMcpTutorial
git add blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md
git commit -m "docs: update part-4 blog with Spectre.Console code listings and terminal screenshots"
```

---

### Task 3: Update `part-6-mcp-security-oidc.md`

**Files:**
- Modify: `blogs/series-1-ai-agent-mcp/part-6-mcp-security-oidc.md`

- [ ] **Step 1: Update `Console.WriteLine("Token acquired.\n")` line**

Find (exact text):
```csharp
Console.WriteLine("Token acquired.\n");
```

Replace with:
```csharp
AnsiConsole.MarkupLine("[green]✔[/] Token acquired.\n");
```

- [ ] **Step 2: Update `Console.WriteLine($"Connected. Tools: ...")` line**

Find (exact text):
```csharp
Console.WriteLine($"Connected. Tools: {string.Join(", ", mcpTools.Select(t => t.Name))}\n");
```

Replace with:
```csharp
AnsiConsole.MarkupLine($"[green]✔[/] Connected · Tools: [grey]{string.Join(", ", mcpTools.Select(t => t.Name))}[/]\n");
```

- [ ] **Step 3: Update `new HrAgent(...)` to pass `style`**

Find (exact text):
```csharp
var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList());
```

Replace with:
```csharp
// style is set by the startup picker in Program.cs (see Part 4 for full Program.cs)
var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), UiStyle.Structured);
```

- [ ] **Step 4: Update the sample run block**

Find (exact text):
```
```bash
dotnet run --project src/HrMcp.Agent
# Token acquired.
# Connected. Tools: GetOpenPositions, WriteJobDescription, ...
```
```

Replace with:
```
```bash
dotnet run --project src/HrMcp.Agent
# ✔ Token acquired.
# ✔ Connected · Tools: GetOpenPositions, WriteJobDescription, ...
#
# Select UI style:
#   [1] Structured — tables, panels, spinners (default)
#   [2] Minimal    — rule-separated turns
#   [3] Panels     — bordered panel per message
# Choice [1]: Structured
```
```

- [ ] **Step 5: Add `using Spectre.Console;` to the Part 6 Program.cs listing**

Find the using block at the top of the Part 6 Program.cs listing — it begins with:
```csharp
using Microsoft.Extensions.AI;
using System.Net.Http.Json;
using System.Text.Json;
```

Add `using Spectre.Console;` after `using System.Text.Json;`:
```csharp
using Microsoft.Extensions.AI;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
```

- [ ] **Step 6: Commit**

```bash
cd c:/apps/DotnetMcpTutorial
git add blogs/series-1-ai-agent-mcp/part-6-mcp-security-oidc.md
git commit -m "docs: update part-6 blog with Spectre.Console Program.cs changes"
```
