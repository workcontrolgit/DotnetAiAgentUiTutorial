// src/HrMcp.Agent/HrAgent.cs
using Microsoft.Extensions.AI;
using Spectre.Console;
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

    public string? LastExportedFileName { get; private set; }
    public byte[]? LastExportedFileBytes { get; private set; }

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

    public async Task<string> AskAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        _history.Add(new ChatMessage(ChatRole.User, input));
        return await RunToolLoopAsync(ct);
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
    private string? TrySaveExportFile(string json, string outputFolder)
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

            // Track for browser download
            LastExportedFileName = fileName;
            LastExportedFileBytes = bytes;

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
        if (string.IsNullOrWhiteSpace(text))
        {
            AnsiConsole.MarkupLine("[grey](No response)[/]");
            return;
        }

        switch (style)
        {
            case UiStyle.Structured:
                AnsiConsole.MarkupLine("\n[bold green]Assistant ›[/]");
                MarkdigSpectreRenderer.Render(text);
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Minimal:
                AnsiConsole.Write(new Rule("[bold green]Assistant[/]").RuleStyle("grey").LeftJustified());
                MarkdigSpectreRenderer.Render(text);
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Panels:
                AnsiConsole.Write(new Rule("[bold green]Assistant[/]").RuleStyle("aquamarine3").LeftJustified());
                MarkdigSpectreRenderer.Render(text);
                AnsiConsole.Write(new Rule().RuleStyle("aquamarine3"));
                AnsiConsole.WriteLine();
                break;
            default:
                throw new NotSupportedException($"Unknown UiStyle: {style}");
        }
    }
}
