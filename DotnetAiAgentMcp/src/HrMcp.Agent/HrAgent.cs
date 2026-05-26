// src/HrMcp.Agent/HrAgent.cs
using Microsoft.Extensions.AI;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;
using System.Text.Json;

namespace HrMcp.Agent;

public enum UiStyle { Structured, Minimal, Panels }

public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools, UiStyle style = UiStyle.Structured, int? numCtx = null, string outputFolder = "usajobs/output")
{
    private const string SystemPrompt = """
        You are an HR assistant for a U.S. federal agency. Help users explore open job
        positions, hiring organizations, and generate job announcements.

        Guidelines:
        - Always call GetHiringOrganizations before GetPositionsByOrganization.
        - Use GetOpenPositions for an overview; GetPositionById for full detail.
        - When asked to write a job description, call GetPositionById to get the full position
          data, then write a compelling USAJobs-style job announcement yourself with these sections:
          ## Summary, ## Duties, ## Qualifications Required, ## How to Apply.
          Use professional federal HR writing style. Be specific and engaging.
        - To export a position's full structured data, call ExportPositionToHtml(positionId) or ExportPositionToWord(positionId).
        - To export an AI-generated job description draft to Word, call ExportDraftToWord(positionId, draftContent)
          passing the full current draft text including any edits the user has made.
        - To export all open positions to Excel, call ExportPositionsToExcel().
        - Format pay ranges as "$85,000 – $110,000 per year".
        - When you receive position data, format it as a markdown table with columns:
          ID, Title, Grade, Salary, Location.
        - Keep answers concise; offer to go deeper when asked.
        - Never present a numbered menu of options or ask the user what they want to do.
          Respond directly to what the user said, or call a tool immediately.
        """;

    // Tools that return { "fileName": "...", "content": "<base64>" } — agent saves the file
    private static readonly HashSet<string> ExportToolNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ExportPositionToHtml",
            "ExportPositionToWord",
            "ExportDraftToWord",
            "ExportPositionsToExcel"
        };

    private readonly string _outputFolder = outputFolder;

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

            var text = await RunToolLoopAsync(ct);
            RenderResponse(text);
        }
    }

    // ── Manual tool call loop ────────────────────────────────────────────────

    private async Task<string> RunToolLoopAsync(CancellationToken ct)
    {
        var additional = new AdditionalPropertiesDictionary();
        if (numCtx.HasValue) additional["num_ctx"] = numCtx.Value;

        var options = new ChatOptions { Tools = tools, AdditionalProperties = additional };

        var response = await Spin("Thinking…", _ =>
            chatClient.GetResponseAsync(_history, options, ct));

        while (true)
        {
            var toolCalls = response.Messages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .ToList();

            if (toolCalls.Count == 0)
            {
                _history.AddMessages(response);
                return response.Text ?? string.Empty;
            }

            // Append assistant message (with tool call requests) to history
            foreach (var msg in response.Messages)
                _history.Add(msg);

            // Execute each tool and add its result to history
            foreach (var call in toolCalls)
            {
                var fn = tools.FirstOrDefault(t => t.Name == call.Name) as AIFunction;
                object? rawResult;

                if (fn is null)
                {
                    rawResult = $"Tool '{call.Name}' not found.";
                }
                else
                {
                    var fnArgs = call.Arguments is null ? null : new AIFunctionArguments(call.Arguments);
                    try { rawResult = await fn.InvokeAsync(fnArgs, ct); }
                    catch (Exception ex) { rawResult = $"Error: {ex.Message}"; }
                }

                Console.WriteLine($"[DBG] call.Name={call.Name}");
                // Intercept export tool results — decode base64 and save to output folder
                if (ExportToolNames.Contains(call.Name ?? string.Empty))
                {
                    Console.WriteLine($"[DBG] rawResult type={rawResult?.GetType().Name}");
                    var json = rawResult switch
                    {
                        string s => s,
                        TextContent tc => tc.Text ?? string.Empty,
                        JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
                        JsonElement je => je.GetRawText(),
                        _ => JsonSerializer.Serialize(rawResult)
                    };
                    Console.WriteLine($"[DBG] json preview={json[..Math.Min(120, json.Length)]}");
                    var saved = TrySaveExportFile(json, _outputFolder);
                    Console.WriteLine($"[DBG] saved={saved ?? "null"}");
                    if (saved is not null) rawResult = saved;
                }

                _history.Add(new ChatMessage(ChatRole.Tool,
                    [new FunctionResultContent(call.CallId ?? string.Empty, rawResult)]));
            }

            response = await Spin("Thinking…", _ =>
                chatClient.GetResponseAsync(_history, options, ct));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Decodes a base64 export payload and saves the file to outputFolder.
    // Returns "Saved to: <path>" on success, null if the payload is not an export result.
    private static string? TrySaveExportFile(string json, string outputFolder)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("fileName", out var fileNameEl) ||
                !root.TryGetProperty("content", out var contentEl))
                return null;

            var fileName = fileNameEl.GetString();
            var base64   = contentEl.GetString();
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(base64))
                return null;

            var bytes = Convert.FromBase64String(base64);
            Directory.CreateDirectory(outputFolder);
            var fullPath = Path.GetFullPath(Path.Combine(outputFolder, fileName));
            File.WriteAllBytes(fullPath, bytes);
            return $"Saved to: {fullPath}";
        }
        catch (Exception ex)
        {
            return $"Export save failed: {ex.Message}";
        }
    }

    private static Task<T> Spin<T>(string status, Func<StatusContext, Task<T>> action) =>
        AnsiConsole.Status()
            .Spinner(Spectre.Console.Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync($"[blue]{status}[/]", action);

    // ── UI rendering ─────────────────────────────────────────────────────────

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
            default:
                throw new NotSupportedException($"Unknown UiStyle: {style}");
        }
    }

    private string RenderUserPrompt()
    {
        switch (style)
        {
            case UiStyle.Structured:
                AnsiConsole.Markup("[bold yellow]You ›[/] ");
                // Console.ReadLine used intentionally — AnsiConsole.Ask adds an unwanted extra ":"
                return Console.ReadLine() ?? string.Empty;
            case UiStyle.Minimal:
                AnsiConsole.Write(new Rule("[bold yellow]You[/]").RuleStyle("grey").LeftJustified());
                return Console.ReadLine() ?? string.Empty;
            case UiStyle.Panels:
                return AnsiConsole.Ask<string>("[bold yellow]You ›[/]");
            default:
                throw new NotSupportedException($"Unknown UiStyle: {style}");
        }
    }

    private void RenderResponse(string text)
    {
        // I-4: show a visible notice rather than silently returning on empty response
        if (string.IsNullOrWhiteSpace(text))
        {
            AnsiConsole.MarkupLine("[grey](No response)[/]");
            return;
        }

        var segments = SplitIntoSegments(text);

        switch (style)
        {
            case UiStyle.Structured:
                AnsiConsole.MarkupLine("\n[bold green]Assistant ›[/]");
                RenderSegments(segments);
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Minimal:
                AnsiConsole.Write(new Rule("[bold green]Assistant[/]").RuleStyle("grey").LeftJustified());
                RenderSegments(segments);
                // I-1: turn separator so the next "You" rule doesn't immediately follow
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Panels:
                // I-2: wrap content in a Panel to match the welcome screen's visual contract
                // I-3: use "Assistant" (not "ASSISTANT") to match other modes
                var panelRows = segments
                    .Select(seg =>
                    {
                        if (seg.IsTable)
                            return BuildMarkdownTable(seg.Text) ?? (IRenderable)new Markup(Markup.Escape(seg.Text));
                        var prose = seg.Text.Trim();
                        return string.IsNullOrWhiteSpace(prose) ? null : (IRenderable)new Markup(ConvertInlineBold(prose));
                    })
                    .Where(r => r is not null)
                    .Select(r => r!)
                    .ToList();
                if (panelRows.Count > 0)
                {
                    AnsiConsole.Write(new Panel(new Rows(panelRows))
                        .Header("[bold green]Assistant[/]")
                        .BorderColor(Color.Aquamarine3)
                        .Padding(1, 0));
                }
                AnsiConsole.WriteLine();
                break;
            default:
                throw new NotSupportedException($"Unknown UiStyle: {style}");
        }
    }

    // M-2: shared helper — avoids repeating the foreach across every switch case
    private static void RenderSegments(List<Segment> segments)
    {
        foreach (var seg in segments) RenderSegment(seg);
    }

    // ── Markdown table renderer (used for LLM-formatted tables) ─────────────

    private record Segment(string Text, bool IsTable);

    private static List<Segment> SplitIntoSegments(string text)
    {
        var segments = new List<Segment>();
        var buffer = new List<string>();
        bool inTable = false;

        foreach (var line in text.Split('\n'))
        {
            bool isTableLine = line.TrimEnd().StartsWith('|');
            if (isTableLine != inTable)
            {
                if (buffer.Count > 0)
                    segments.Add(new Segment(string.Join('\n', buffer), inTable));
                buffer.Clear();
                inTable = isTableLine;
            }
            buffer.Add(line.TrimEnd());
        }
        if (buffer.Count > 0)
            segments.Add(new Segment(string.Join('\n', buffer), inTable));

        return segments;
    }

    private static void RenderSegment(Segment seg)
    {
        if (seg.IsTable)
            RenderMarkdownTable(seg.Text);
        else
        {
            var prose = seg.Text.Trim();
            if (!string.IsNullOrWhiteSpace(prose))
                RenderMarkdownProse(prose);
        }
    }

    // Renders LLM markdown prose to the console with basic formatting:
    //   **bold**  → Spectre bold markup
    //   * item    → indented bullet with • glyph
    //   blank line → paragraph break
    private static void RenderMarkdownProse(string text)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrEmpty(line))
            {
                AnsiConsole.WriteLine();
                continue;
            }

            if (IsBulletLine(line, out var bulletContent))
            {
                AnsiConsole.MarkupLine("  [grey]•[/] " + ConvertInlineBold(bulletContent));
            }
            else
            {
                AnsiConsole.MarkupLine(ConvertInlineBold(line));
            }
        }
        AnsiConsole.WriteLine();
    }

    // Returns true when the line is a markdown bullet (* item or *   item)
    private static bool IsBulletLine(string line, out string content)
    {
        var t = line.TrimStart();
        if (t.Length >= 2 && t[0] == '*' && char.IsWhiteSpace(t[1]))
        {
            content = t.Substring(1).TrimStart();
            return true;
        }
        content = string.Empty;
        return false;
    }

    // Converts **bold** spans to Spectre markup; escapes everything else
    private static string ConvertInlineBold(string text)
    {
        var parts = text.Split("**");
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
            sb.Append(i % 2 == 0
                ? Markup.Escape(parts[i])
                : $"[bold]{Markup.Escape(parts[i])}[/]");
        return sb.ToString();
    }

    private static void RenderMarkdownTable(string tableText)
    {
        var table = BuildMarkdownTable(tableText);
        if (table is not null)
        {
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.Write(new Markup(Markup.Escape(tableText)));
            AnsiConsole.WriteLine();
        }
    }

    // M-1: separates building from rendering so Panels mode can reuse the Table widget.
    // Filters separator rows by content (not by position) so a missing separator row
    // doesn't silently eat the first data row.
    private static Table? BuildMarkdownTable(string tableText)
    {
        var rows = tableText.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith('|') && !IsSeparatorRow(l))
            .ToList();

        if (rows.Count < 1) return null;

        var headers = ParseCells(rows[0]);
        var dataRows = rows.Skip(1).ToList();

        var table = new Table().BorderColor(Color.Teal).Expand();
        foreach (var h in headers)
            table.AddColumn(new TableColumn($"[bold cyan]{Markup.Escape(h)}[/]"));

        foreach (var row in dataRows)
        {
            var cells = ParseCells(row);
            while (cells.Count < headers.Count) cells.Add(string.Empty);
            // Truncate to header count — LLMs occasionally produce malformed rows
            table.AddRow(cells.Take(headers.Count).Select(c => Markup.Escape(c)).ToArray());
        }

        return table;
    }

    // A GFM separator row contains only |, -, :, and spaces
    private static bool IsSeparatorRow(string row) =>
        row.Replace("|", "").Replace("-", "").Replace(":", "").Replace(" ", "").Length == 0;

    private static List<string> ParseCells(string line) =>
        line.Trim('|').Split('|').Select(c => c.Trim()).ToList();
}
