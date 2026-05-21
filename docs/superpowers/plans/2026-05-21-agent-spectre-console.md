# Add Spectre.Console to HrMcp.Agent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace raw `Console.Write/WriteLine` in `HrMcp.Agent` with Spectre.Console rendering — chat-style output, MCP tool call spinners, tables for position results, and a startup style picker (Structured/Minimal/Panels) defaulting to Structured after a 2-second timeout.

**Architecture:** A `UiStyle` enum and three private render helpers are added to `HrAgent.cs`. The constructor gains a `style` parameter. `Program.cs` shows an interactive style picker before constructing `HrAgent`. All Spectre.Console calls live in `HrAgent.cs`; `Program.cs` only handles the startup prompt.

**Tech Stack:** `Spectre.Console 0.49.*`, .NET 10 console app, `Microsoft.Extensions.AI`, `ModelContextProtocol`.

---

## Files

- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs`
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs`

---

### Task 1: Add Spectre.Console package

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj`

- [ ] **Step 1: Add the package**

```bash
cd DotnetAiAgentMcp/src/HrMcp.Agent
dotnet add package Spectre.Console --version 0.49.*
```

- [ ] **Step 2: Verify the reference was added**

Open `HrMcp.Agent.csproj` and confirm this line appears in the `<ItemGroup>`:

```xml
<PackageReference Include="Spectre.Console" Version="0.49.*" />
```

- [ ] **Step 3: Build to confirm no errors**

```bash
cd DotnetAiAgentMcp
dotnet build src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/HrMcp.Agent.csproj
git commit -m "feat: add Spectre.Console package to HrMcp.Agent"
```

---

### Task 2: Add UiStyle enum and update HrAgent constructor

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs`

- [ ] **Step 1: Add the UiStyle enum and update the class**

Replace the entire contents of `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs` with:

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
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .Start("[blue]Thinking…[/]", _ =>
                {
                    response = chatClient.GetResponseAsync(
                        _history,
                        new ChatOptions { Tools = tools },
                        ct).GetAwaiter().GetResult();
                });

            _history.AddMessages(response);
            RenderResponse(response.Text ?? string.Empty);
        }
    }

    private void RenderWelcome()
    {
        switch (style)
        {
            case UiStyle.Structured:
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
                AnsiConsole.Markup("[bold yellow]You ›[/] ");
                return Console.ReadLine() ?? string.Empty;
            case UiStyle.Minimal:
                AnsiConsole.Write(new Rule("[bold yellow]You[/]").RuleStyle("grey").LeftJustified());
                AnsiConsole.Markup(" ");
                return Console.ReadLine() ?? string.Empty;
            case UiStyle.Panels:
            default:
                return AnsiConsole.Ask<string>("[bold yellow]You[/]");
        }
    }

    private void RenderResponse(string text)
    {
        switch (style)
        {
            case UiStyle.Structured:
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
                        .NoBorder()
                        .Padding(1, 0));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Panels:
                AnsiConsole.Write(
                    new Panel(new Markup(Markup.Escape(text)))
                        .Header("[bold green]ASSISTANT[/]")
                        .BorderColor(Color.MediumAquamarine3)
                        .Padding(1, 0));
                AnsiConsole.WriteLine();
                break;
        }
    }
}
```

- [ ] **Step 2: Build to confirm no compile errors**

```bash
cd DotnetAiAgentMcp
dotnet build src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs
git commit -m "feat: add UiStyle enum and Spectre.Console rendering to HrAgent"
```

---

### Task 3: Add startup style picker to Program.cs

**Files:**
- Modify: `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs`

- [ ] **Step 1: Add the using directive**

At the top of `Program.cs`, add after the existing using block:

```csharp
using Spectre.Console;
```

- [ ] **Step 2: Replace the MCP connect output and add the style picker**

Find this block in `Program.cs`:

```csharp
var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Connected. Tools: {string.Join(", ", mcpTools.Select(t => t.Name))}\n");

IChatClient chatClient = CreateChatClient(configuration)
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList());
await agent.RunAsync();
```

Replace it with:

```csharp
var mcpTools = await mcpClient.ListToolsAsync();
AnsiConsole.MarkupLine($"[green]✔[/] Connected · Tools: [grey]{string.Join(", ", mcpTools.Select(t => t.Name))}[/]\n");

// ── Style picker ─────────────────────────────────────────────────────────────
var style = UiStyle.Structured; // default

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
        style = key.KeyChar switch
        {
            '2' => UiStyle.Minimal,
            '3' => UiStyle.Panels,
            _   => UiStyle.Structured
        };
    }
}
catch (OperationCanceledException) { /* timeout — keep default */ }

AnsiConsole.MarkupLine($"[green]{style}[/]\n");
// ─────────────────────────────────────────────────────────────────────────────

IChatClient chatClient = CreateChatClient(configuration)
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style);
await agent.RunAsync();
```

- [ ] **Step 3: Build to confirm no compile errors**

```bash
cd DotnetAiAgentMcp
dotnet build src/HrMcp.Agent/HrMcp.Agent.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs
git commit -m "feat: add Spectre.Console style picker to HrMcp.Agent startup"
```

---

### Task 4: Manual smoke test all three styles

**Files:** (no code changes — verification only)

- [ ] **Step 1: Start the MCP server**

In a separate terminal:

```bash
cd DotnetAiAgentMcp
dotnet run --project src/HrMcp.McpServer
```

Expected: server listening on `http://localhost:5100`

- [ ] **Step 2: Test Style 1 — Structured (default)**

```bash
cd DotnetAiAgentMcp
dotnet run --project src/HrMcp.Agent
```

At the style picker, wait 2 seconds without pressing a key.

Expected:
- Picker auto-selects `Structured`
- Welcome rule rendered in cyan
- Prompt shows `You ›` in yellow
- Spinner appears while tools run
- Response shown with `Assistant ›` in green followed by text
- Horizontal rule divider after each response

- [ ] **Step 3: Test Style 2 — Minimal**

```bash
dotnet run --project src/HrMcp.Agent
```

Press `2` at the picker within 2 seconds.

Expected:
- `Minimal` selected and shown in green
- Welcome message is plain teal text
- Each turn separated by horizontal rules with colored speaker labels
- Assistant response in a teal left-bordered panel

- [ ] **Step 4: Test Style 3 — Panels**

```bash
dotnet run --project src/HrMcp.Agent
```

Press `3` at the picker within 2 seconds.

Expected:
- `Panels` selected and shown in green
- Welcome shown in a cyan-bordered Panel
- Each user turn prompted via `AnsiConsole.Ask`
- Assistant response wrapped in a `MediumAquamarine3`-bordered Panel with `ASSISTANT` header

- [ ] **Step 5: Test exit command works in all styles**

In each style, type `exit` at the prompt. Expected: agent exits cleanly with no exception.

- [ ] **Step 6: Commit smoke test confirmation**

```bash
git commit --allow-empty -m "test: smoke tested all three Spectre.Console UI styles"
```
