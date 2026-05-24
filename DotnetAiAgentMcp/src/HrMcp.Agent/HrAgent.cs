// src/HrMcp.Agent/HrAgent.cs
using Microsoft.Extensions.AI;
using Spectre.Console;
using System.Text.Json;

namespace HrMcp.Agent;

public enum UiStyle { Structured, Minimal, Panels }

public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools, UiStyle style = UiStyle.Structured)
{
    private const string SystemPrompt = """
        You are an HR assistant for a U.S. federal agency. Help users explore open job
        positions, hiring organizations, and generate job announcements.

        Guidelines:
        - Always call GetHiringOrganizations before GetPositionsByOrganization.
        - Use GetOpenPositions for an overview; GetPositionById for full detail.
        - When asked to write a job description, call WriteJobDescription — do not write one yourself.
        - For "USAJobs format" requests, call RenderPositionAsUsaJobsHtml with the position ID.
        - Format pay ranges as "$85,000 – $110,000 per year".
        - When you receive position data, format it as a markdown table with columns:
          ID, Title, Grade, Salary, Location.
        - Keep answers concise; offer to go deeper when asked.
        """;

    // Tools whose results are paged — only PageSize records are sent to the LLM per turn
    private static readonly HashSet<string> PositionListTools =
        new(StringComparer.OrdinalIgnoreCase) { "GetOpenPositions", "GetPositionsByOrganization" };

    private const int PageSize = 10;

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

            // Run tool loop; position-list results are intercepted and paged
            var (pageOneText, allPositions) = await RunToolLoopAsync(ct);

            if (allPositions is { Count: > 0 })
            {
                // Show LLM prose (strip its markdown table — we render our own)
                RenderResponse(StripMarkdownTables(pageOneText));

                var totalPages = (int)Math.Ceiling(allPositions.Count / (double)PageSize);
                for (var page = 0; page < totalPages; page++)
                {
                    RenderPositionPage(allPositions, page, totalPages);

                    if (page < totalPages - 1)
                    {
                        AnsiConsole.MarkupLine($"[yellow]─── Page {page + 1} of {totalPages} ───  Enter = next page   Q = stop[/]");
                        var key = Console.ReadLine() ?? string.Empty;
                        if (key.Trim().Equals("Q", StringComparison.OrdinalIgnoreCase))
                        {
                            AnsiConsole.MarkupLine("[grey]Stopped.[/]");
                            break;
                        }
                    }
                }
            }
            else
            {
                RenderResponse(pageOneText);
            }
        }
    }

    // ── Manual tool call loop ────────────────────────────────────────────────
    // Handles all tool calls ourselves so we can intercept position-list results
    // and feed only PageSize records to the LLM at a time.

    private async Task<(string text, List<string>? allPositions)> RunToolLoopAsync(CancellationToken ct)
    {
        List<string>? capturedPositions = null;
        var options = new ChatOptions { Tools = tools };

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
                return (response.Text ?? string.Empty, capturedPositions);
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

                object? llmResult = rawResult;

                // Intercept position-list tools: capture all records, send only first page to LLM
                if (PositionListTools.Contains(call.Name ?? string.Empty))
                {
                    var json = rawResult switch
                    {
                        string s => s,
                        JsonElement je => je.GetRawText(),
                        _ => JsonSerializer.Serialize(rawResult)
                    };

                    var records = ParseJsonObjects(json);
                    if (records is { Count: > 0 })
                    {
                        capturedPositions = records;
                        var firstPage = records.Take(PageSize).ToList();
                        llmResult = $"(Showing records 1–{firstPage.Count} of {records.Count} total)\n"
                                  + $"[{string.Join(",", firstPage)}]";
                    }
                }

                _history.Add(new ChatMessage(ChatRole.Tool,
                    [new FunctionResultContent(call.CallId ?? string.Empty, llmResult)]));
            }

            response = await Spin("Thinking…", _ =>
                chatClient.GetResponseAsync(_history, options, ct));
        }
    }

    // Render one page of positions as a Spectre.Console table (no LLM involved)
    private static void RenderPositionPage(List<string> allRecords, int page, int totalPages)
    {
        var pageRecords = allRecords.Skip(page * PageSize).Take(PageSize).ToList();
        var from = page * PageSize + 1;
        var to = from + pageRecords.Count - 1;
        var total = allRecords.Count;

        var table = new Table()
            .BorderColor(Color.Teal)
            .Expand()
            .Title($"[bold cyan]Page {page + 1} of {totalPages}[/]")
            .Caption($"[grey]Records {from}–{to} of {total}[/]");

        table.AddColumn(new TableColumn("[bold cyan]ID[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold cyan]Title[/]"));
        table.AddColumn(new TableColumn("[bold cyan]Grade[/]").Centered());
        table.AddColumn(new TableColumn("[bold cyan]Salary[/]"));
        table.AddColumn(new TableColumn("[bold cyan]Location[/]"));

        foreach (var raw in pageRecords)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var e = doc.RootElement;
                table.AddRow(
                    Markup.Escape(Str(e, "Id")),
                    Markup.Escape(Str(e, "Title")),
                    Markup.Escape(Grade(e)),
                    Markup.Escape(Salary(e)),
                    Markup.Escape(Str(e, "DutyLocation")));
            }
            catch { table.AddRow("?", raw, "", "", ""); }
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    // Try PascalCase then camelCase (MCP SDK serializes with camelCase)
    private static bool TryProp(JsonElement e, string key, out JsonElement val)
    {
        if (e.TryGetProperty(key, out val)) return true;
        var camel = char.ToLowerInvariant(key[0]) + key[1..];
        return e.TryGetProperty(camel, out val);
    }

    private static string Str(JsonElement e, string key) =>
        TryProp(e, key, out var v) ? v.ToString() : "";

    private static string Grade(JsonElement e)
    {
        var lo = Str(e, "PayGradeMin");
        var hi = Str(e, "PayGradeMax");
        return string.IsNullOrEmpty(lo) ? hi : lo == hi ? lo : $"{lo}–{hi}";
    }

    private static string Salary(JsonElement e)
    {
        double? min = TryProp(e, "MinimumRange", out var minP) && minP.ValueKind == JsonValueKind.Number
            ? minP.GetDouble() : null;
        double? max = TryProp(e, "MaximumRange", out var maxP) && maxP.ValueKind == JsonValueKind.Number
            ? maxP.GetDouble() : null;
        if (min is null && max is null) return "N/A";
        if (min is null) return $"Up to ${max!.Value:N0}";
        if (max is null) return $"${min.Value:N0}+";
        return $"${min.Value:N0} – ${max.Value:N0}";
    }

    private static string StripMarkdownTables(string text) =>
        string.Join('\n', text.Split('\n').Where(l => !l.TrimStart().StartsWith('|'))).Trim();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<string>? ParseJsonObjects(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            // GetRawText() copies the bytes out before doc is disposed
            return doc.RootElement.EnumerateArray()
                .Select(e => e.GetRawText())
                .ToList();
        }
        catch { return null; }
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
                AnsiConsole.Write(new Rule("[bold cyan]HR Assistant[/]").RuleStyle("cyan").LeftJustified());
                AnsiConsole.MarkupLine("[grey]Ask about open positions, organizations, or job descriptions. Type [bold]exit[/] to quit.[/]\n");
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
                return Console.ReadLine() ?? string.Empty;
            case UiStyle.Panels:
            default:
                return AnsiConsole.Ask<string>("[bold yellow]You[/]");
        }
    }

    private void RenderResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var segments = SplitIntoSegments(text);

        switch (style)
        {
            case UiStyle.Structured:
                AnsiConsole.MarkupLine("\n[bold green]Assistant ›[/]");
                foreach (var seg in segments) RenderSegment(seg);
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Minimal:
                AnsiConsole.Write(new Rule("[bold green]Assistant[/]").RuleStyle("grey").LeftJustified());
                foreach (var seg in segments) RenderSegment(seg);
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Panels:
                AnsiConsole.Write(new Rule("[bold green]ASSISTANT[/]").RuleStyle("aquamarine3").LeftJustified());
                foreach (var seg in segments) RenderSegment(seg);
                AnsiConsole.WriteLine();
                break;
            default:
                AnsiConsole.MarkupLine("\n[bold green]Assistant ›[/]");
                foreach (var seg in segments) RenderSegment(seg);
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                AnsiConsole.WriteLine();
                break;
        }
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
            {
                AnsiConsole.Write(new Markup(Markup.Escape(prose)));
                AnsiConsole.WriteLine();
            }
        }
    }

    private static void RenderMarkdownTable(string tableText)
    {
        var rows = tableText.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith('|'))
            .ToList();

        if (rows.Count < 2) { AnsiConsole.Write(new Markup(Markup.Escape(tableText))); return; }

        var headers = ParseCells(rows[0]);
        var dataRows = rows.Skip(2).ToList(); // skip separator

        var table = new Table().BorderColor(Color.Teal).Expand();
        foreach (var h in headers)
            table.AddColumn(new TableColumn($"[bold cyan]{Markup.Escape(h)}[/]"));

        foreach (var row in dataRows)
        {
            var cells = ParseCells(row);
            while (cells.Count < headers.Count) cells.Add(string.Empty);
            table.AddRow(cells.Take(headers.Count).Select(c => Markup.Escape(c)).ToArray());
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static List<string> ParseCells(string line) =>
        line.Trim('|').Split('|').Select(c => c.Trim()).ToList();
}
