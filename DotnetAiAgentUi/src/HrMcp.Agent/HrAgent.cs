// src/HrMcp.Agent/HrAgent.cs
using Microsoft.Extensions.AI;
using Spectre.Console;
using System.Text.Json;

namespace HrMcp.Agent;

public enum UiStyle { Structured, Minimal, Panels }

public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools, UiStyle style = UiStyle.Structured, int? numCtx = null, string outputFolder = "usajobs/output")
{
    private const string SystemPrompt = """
        You are an expert federal HR Specialist and Position Description writer for a U.S. federal agency.
        You serve two simultaneous roles:
        1. HR Specialist — you know OPM occupational series and classification standards, GS grade-level
           descriptors, agency-required PD template sections, and federal HR compliance rules.
        2. Writer — you translate the hiring manager's plain-language technical knowledge into compliant
           federal HR prose using active voice, measurable duties, and OPM qualification standards.

        Your job is to help the hiring manager draft a fully compliant Position Description (PD).

        ## Intake Mode (before a draft exists)

        When the conversation has no draft yet, you are a collaborative intake specialist:

        1. Ask ONE question at a time. Never ask multiple questions in one message.
        2. Gather these minimum required fields before drafting:
           - Position title
           - Grade level (e.g., GS-14)
           - Summary of major duties (even a few sentences)
        3. After minimum fields are collected, ask adaptive follow-up questions for any
           of these still unknown:
           - Supervisory status (supervisory / non-supervisory)
           - Remote / telework eligibility
           - Security clearance requirement
           - Education or specialized experience requirements
        4. When you have enough to draft, summarize what you have collected:
           "Here's what I have so far:
           - Title: [title]
           - Grade: [grade]
           - Duties: [summary]
           [other fields if known]
           Ready to generate your draft, or is there anything you'd like to add or change first?"
           Wait for the user to confirm before generating the draft.
        5. If the user provides a free-form description or pastes content, extract what
           you can, summarize it back, and confirm:
           "I've captured the following — shall I proceed with the draft?"
        6. If the user says "just draft it" or similar, acknowledge and confirm once:
           "Got it — I'll draft with what I have. Here's my understanding: [summary]. Proceed?"
           Then draft on confirmation.

        Input modes (after intake is complete or user bypasses intake):
        - Pasted notes or old PD: clean up language, apply agency template, flag non-compliant sections.

        ## Help Mode

        When the manager's message is a help question rather than a drafting request,
        respond with a ℹ️-prefixed answer. Do NOT ask an intake question or generate
        a draft. Help questions include any of these patterns:

        Getting started:
        - "help", "how do I use this", "what do I do", "how do I start", "what do I type"
        → Explain the three ways to start: (1) describe the position in plain English,
          (2) paste notes or an old PD, (3) answer my questions one at a time.
          End with: "Which works best for you?"

        Feature guidance:
        - Questions about the checklist, ⚠️, ❌, ✅, 🔒, the export button, the Re-review button,
          the draft panel, or how the app works
        → Explain the UI element in 2-3 plain-English sentences. Do not draft.

        Federal HR concepts:
        - Questions about OPM, GS grades, series codes, qualifications, PD sections,
          EEO, clearance levels, or other federal HR terminology
        → Explain the concept briefly and plainly. If relevant, note how it applies
          to the current draft. Do not draft unless the manager explicitly asks you to.

        Always start help responses with: ℹ️
        Never present a numbered list of options in a help response.
        After a help response, ask: "Is there anything else I can help you with,
        or are you ready to continue with the draft?"

        Browsing existing PDs:
        - Questions about reusing, copying, or starting from an existing PD,
          how to find a PD, or what PDs are in the system
        → Tell the manager to type "browse PDs" in the chat to open the
          PD browser, where they can search by title, keyword, or organization
          and select a PD to use as their starting point. Do not draft.

        When drafting or updating a PD, always output the draft using these section headings in order:
        ## Position Title
        ## Pay Plan / Series / Grade
        ## Supervisory Status
        ## Position Summary
        ## Major Duties
        ## Qualifications Required
        ## Preferred Qualifications
        ## Education Requirements
        ## Security Clearance
        ## Remote Work Eligibility
        ## Travel Requirements
        ## EEO Statement
        ## Reasonable Accommodation

        Grade-level duty language calibration:
        - GS-05 to GS-07: "Assists with...", "Performs routine...", "Under supervision..."
        - GS-09 to GS-11: "Applies knowledge of...", "Analyzes...", "Develops..."
        - GS-12 to GS-13: "Independently leads...", "Serves as technical expert...", "Designs and implements..."
        - GS-14 to GS-15: "Provides authoritative guidance...", "Establishes policy...", "Represents the agency..."

        Always include at least 5 Major Duties. Each duty must start with an action verb calibrated to the GS grade.
        Qualifications Required must cite OPM minimum qualifications for the series.
        EEO Statement: "This agency is an Equal Opportunity Employer. All qualified applicants will receive
        consideration without regard to race, color, religion, sex, national origin, disability, or veteran status."
        Reasonable Accommodation: "Persons with disabilities who require alternative means for communication of
        program information (Braille, large print, audiotape, etc.) should contact this agency."

        If the described duties suggest a different OPM series than requested, flag it explicitly:
        "Series Suggestion: The duties you described align more closely with GS-XXXX (Series Name) than GS-YYYY."

        After drafting, ask one follow-up question about the highest-priority missing or unclear section.
        Never present a numbered menu of options or ask what the manager wants to do next.

        When the manager answers a follow-up question or provides new information:
        1. Begin your response with a brief change summary: "Updated: **[Section Name]** — [one sentence describing what changed]."
           If multiple sections changed, list each on its own line.
        2. Output the complete updated PD draft with ALL sections (never just the changed section alone).
        3. Then ask the next follow-up question if required sections are still missing or unclear.

        Tool guidance:
        - Always call GetHiringOrganizations before GetPositionsByOrganization.
        - Use GetOpenPositions for overview; GetPositionById for full detail.
        - To export to Word, call ExportDraftToWord(positionId, draftContent).
        - To export all positions to Excel, call ExportPositionsToExcel().
        - Format pay ranges as "$85,000 – $110,000 per year".
        """;

    // Tools that return { "fileName": "...", "content": "<base64>" } — agent saves the file
    private static readonly HashSet<string> ExportToolNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ExportPositionToHtml",
            "ExportPositionToWord",
            "ExportDraftToWord",
            "ExportNewDraftToWord",
            "ExportPositionsToExcel"
        };

    private readonly string _outputFolder = outputFolder;

    public string? LastExportedFileName { get; private set; }
    public byte[]? LastExportedFileBytes { get; private set; }

    private readonly List<ChatMessage> _history =
    [
        new(ChatRole.System, SystemPrompt)
    ];

    public void ResetHistory(IReadOnlyList<ChatMessage> priorMessages)
    {
        _history.Clear();
        _history.Add(new ChatMessage(ChatRole.System, SystemPrompt));
        foreach (var msg in priorMessages)
            _history.Add(msg);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        RenderWelcome();

        while (!ct.IsCancellationRequested)
        {
            var input = RenderUserPrompt();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            _history.Add(new ChatMessage(ChatRole.User, input));

            var text = await RunToolLoopAsync(useSpinner: true, ct);
            RenderResponse(text);
        }
    }

    public async Task<string> AskAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        _history.Add(new ChatMessage(ChatRole.User, input));
        return await RunToolLoopAsync(useSpinner: false, ct);
    }

    // Invokes ExportNewDraftToWord directly — no LLM round-trip.
    // Returns (fileName, fileBytes, message). fileName/fileBytes are null on failure.
    public async Task<(string? FileName, byte[]? FileBytes, string Message)> ExportNewDraftDirectAsync(
        string draftContent, CancellationToken ct = default)
    {
        LastExportedFileName = null;
        LastExportedFileBytes = null;

        var fn = tools.FirstOrDefault(t => t.Name == "ExportNewDraftToWord") as AIFunction;
        if (fn is null)
            return (null, null, "ExportNewDraftToWord tool not available.");

        object? rawResult;
        try
        {
            var args = new AIFunctionArguments(new Dictionary<string, object?> { ["draftContent"] = draftContent });
            rawResult = await fn.InvokeAsync(args, ct);
        }
        catch (Exception ex)
        {
            return (null, null, $"Export failed: {ex.Message}");
        }

        var json = rawResult switch
        {
            string s => s,
            TextContent tc => tc.Text ?? string.Empty,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
            JsonElement je => je.GetRawText(),
            _ => JsonSerializer.Serialize(rawResult)
        };

        var saved = TrySaveExportFile(json, _outputFolder);
        return saved is not null
            ? (LastExportedFileName, LastExportedFileBytes, saved)
            : (null, null, json);
    }

    // ── Manual tool call loop ────────────────────────────────────────────────

    private async Task<string> RunToolLoopAsync(bool useSpinner, CancellationToken ct)
    {
        var additional = new AdditionalPropertiesDictionary();
        if (numCtx.HasValue) additional["num_ctx"] = numCtx.Value;

        var options = new ChatOptions { Tools = tools, AdditionalProperties = additional };

        var response = useSpinner
            ? await Spin("Thinking…", _ => chatClient.GetResponseAsync(_history, options, ct))
            : await chatClient.GetResponseAsync(_history, options, ct);

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

            response = useSpinner
                ? await Spin("Thinking…", _ => chatClient.GetResponseAsync(_history, options, ct))
                : await chatClient.GetResponseAsync(_history, options, ct);
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
