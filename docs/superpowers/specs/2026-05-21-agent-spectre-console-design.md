# Design: Add Spectre.Console to HrMcp.Agent

**Date:** 2026-05-21  
**Project:** DotnetAiAgentMcp  
**Scope:** `src/HrMcp.Agent`

---

## Problem

`HrMcp.Agent` uses raw `Console.Write/WriteLine` for all output. The chat loop, tool call status, and responses are plain text with no visual structure. Spectre.Console enables chat-style rendering, spinners for MCP tool calls, tables for position results, and color-coded roles — significantly improving readability and demo quality.

---

## Approach

Option 1 — Spectre.Console rendering inside `HrAgent.cs` only. A `UiStyle` enum controls which rendering mode is active. The style is chosen at startup via an interactive prompt with a 2-second auto-select timeout defaulting to `Structured`. No separate renderer class — YAGNI.

---

## Package

Add to `HrMcp.Agent.csproj`:

```xml
<PackageReference Include="Spectre.Console" Version="0.49.*" />
```

---

## UiStyle Enum

Defined at the top of `HrAgent.cs`:

```csharp
public enum UiStyle { Structured, Minimal, Panels }
```

- **Structured** — colored speaker labels (`You ›` / `Assistant ›`), spinner while MCP tools run, Spectre `Table` for position results, plain markup fallback for prose responses.
- **Minimal** — horizontal rule separators between turns, colored speaker labels, left-bordered `Panel` for assistant responses, spinner for tool calls.
- **Panels** — each user and assistant turn wrapped in a full `Panel` with colored border and role header. Tool calls shown inline between panels.

---

## HrAgent.cs Changes

### Constructor

```csharp
public sealed class HrAgent(
    IChatClient chatClient,
    IList<AITool> tools,
    UiStyle style = UiStyle.Structured)
```

The `_style` field is stored and used by the render helpers.

### RunAsync

Replace all `Console.Write/WriteLine` calls with calls to private render helpers. The MCP tool call (previously a fire-and-forget `GetResponseAsync`) is wrapped in `AnsiConsole.Status().Start(...)` to show a spinner while waiting:

```csharp
ChatResponse response = default!;
AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .Start("Calling MCP tool…", _ =>
    {
        response = chatClient.GetResponseAsync(_history, new ChatOptions { Tools = tools }, ct)
                             .GetAwaiter().GetResult();
    });
```

> Note: `AnsiConsole.Status` requires a synchronous callback. Use `.GetAwaiter().GetResult()` inside the spinner, then `await` the outer async flow continues normally after.

### Private Render Helpers

**RenderUserPrompt()** — writes the colored "You › " prompt and reads input:
- Structured/Minimal: `AnsiConsole.Markup("[bold yellow]You ›[/] ")` + `Console.ReadLine()`
- Panels: `AnsiConsole.Prompt(new TextPrompt<string>("[bold yellow]You[/]"))`

**RenderToolCall(string name)** — shown before GetResponseAsync (spinner label):
- All styles: spinner label set to `$"Calling MCP tool: {name}…"`
- Note: the tool name is not known until the LLM decides to call it. The spinner shows a generic "Thinking…" label; tool name is shown after the fact as an info line.

**RenderResponse(string text)** — renders the assistant reply:
- **Structured**: `AnsiConsole.Markup("[bold green]Assistant ›[/]\n")` + `AnsiConsole.Write(new Markup(text))`. If the response contains a list of positions (detected by the presence of `•` bullets or newline-separated items), render as a `Table` with columns Title / Grade / Salary parsed from the text.
- **Minimal**: `AnsiConsole.Write(new Panel(new Markup(text)).BorderColor(Color.Teal).NoBorder())` with a left-border effect via `Padding`.
- **Panels**: `AnsiConsole.Write(new Panel(new Markup(text)).Header("[bold green]ASSISTANT[/]").BorderColor(Color.MediumAquamarine))`

---

## Program.cs Changes

### Style Picker

After connecting to the MCP server and before constructing `HrAgent`, show the style picker:

```csharp
var style = UiStyle.Structured; // default

AnsiConsole.MarkupLine("[bold]Select UI style:[/]");
AnsiConsole.MarkupLine("  [cyan][1][/] Structured — tables, panels, spinners [grey](default)[/]");
AnsiConsole.MarkupLine("  [cyan][2][/] Minimal    — rule-separated turns");
AnsiConsole.MarkupLine("  [cyan][3][/] Panels     — bordered panel per message");

var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
AnsiConsole.Markup("[grey]Choice [[1]]:[/] ");

try
{
    // Poll for a keypress during the 2-second window
    var deadline = DateTime.UtcNow.AddSeconds(2);
    while (DateTime.UtcNow < deadline && !Console.KeyAvailable)
        await Task.Delay(100, cts.Token);

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

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList(), style);
```

---

## Files Changed

| File | Change |
|---|---|
| `src/HrMcp.Agent/HrMcp.Agent.csproj` | Add `Spectre.Console 0.49.*` |
| `src/HrMcp.Agent/HrAgent.cs` | Add `UiStyle` enum, style constructor param, 3 render helpers, spinner in RunAsync |
| `src/HrMcp.Agent/Program.cs` | Add style picker block before `new HrAgent(...)` |

---

## Out of Scope

- Serilog integration — separate plan already written (`2026-05-21-agent-serilog.md`)
- Streaming LLM responses with `AnsiConsole.Live` — future enhancement
- `ConsoleRenderer` abstraction class — YAGNI, extract only if helpers exceed ~150 lines
- Changes to `HrMcp.McpServer` — no changes needed
