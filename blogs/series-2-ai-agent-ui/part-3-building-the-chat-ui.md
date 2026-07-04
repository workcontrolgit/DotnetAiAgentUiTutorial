# Part 3: Building the Chat UI

<<<<<<< HEAD
**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 3 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)
=======
Series: AI Agent UI with Blazor United & .NET 10 | Part 3 of 8
GitHub: workcontrolgit/DotnetAiAgentUiTutorial
![Series 2 cover](screenshots/blog_cover.png)
>>>>>>> release/v1

---

## Introduction

Part 2 was all concepts — `IChatClient`, the three-state UI model, the service seam, the request round-trip. This part makes every one of those concepts real.

<<<<<<< HEAD
By the end of this post you will have a working chat panel: type a prompt, press Enter, see a loading indicator while the agent calls the MCP server, and read the response rendered as markdown in the conversation thread. Nothing streams yet — that arrives in Part 4 — but the full pipeline is wired and functional.
=======
By the end of this post you will have a working chat panel: type a prompt, press Enter, see a loading indicator while the agent calls the MCP server, and read the response rendered as markdown in the conversation thread. The full pipeline is wired and functional. Part 4 covers the session persistence and draft intelligence built on top of this foundation.
>>>>>>> release/v1

The structure here is the direct parallel to [Series 1 Part 3](../series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md), which built the MCP server tool-by-tool. Here we build the UI layer piece-by-piece: model first, then service interface, then implementation, then component.

---

## Step 1 — The ChatTurn Model

Every message in the conversation thread is a `ChatTurn`. Here is the entire file:

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/ChatTurn.cs
namespace HrMcp.Agent.Web.Models;

public sealed record ChatTurn(string Role, string Text, DateTimeOffset Timestamp);
```

Three decisions worth noting:

**`record` not `class`.** Records give value equality for free. Two turns with the same role, text, and timestamp are equal — which matters if you ever diff conversation history or dedup. The immutability guarantee also means the UI can safely pass turns to child components without defensive copies.

**`sealed`.** There is no scenario where a `UserChatTurn` subtype adds value here. Seal it and close the door.

**`DateTimeOffset` not `DateTime`.** The timestamp travels across serialization boundaries and time zones. `DateTimeOffset` carries the offset, so a turn created server-side in UTC arrives client-side without ambiguity.

The three fields map directly onto the UI: `Role` drives the CSS class (`chat-bubble-row--user` vs `chat-bubble-row--assistant`), `Text` is what gets rendered (with markdown processing for assistant turns), and `Timestamp` is available for display or future diffing.

---

## Step 2 — The IAgentDraftService Interface

The Blazor component never calls a model provider. It calls `IAgentDraftService`. Here is the interface, defined at the top of the service file:

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs
public interface IAgentDraftService
{
<<<<<<< HEAD
    Task<string> SendPromptAsync(string prompt, CancellationToken ct = default);
=======
    Task<string> SendPromptAsync(string prompt, Guid? sessionId = null, CancellationToken ct = default);
>>>>>>> release/v1
    Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(string draftText, CancellationToken ct = default);
}
```

Two methods. That is the full surface the component ever sees.

`SendPromptAsync` handles conversational prompts — the user types a question, the agent answers, the response goes into the thread.

`ExportDraftToWordAsync` handles the Word export flow — the component passes the current editor content, the service sends it to the MCP server's `ExportDraftToWord` tool, and the return tuple carries the file name and raw bytes ready for a browser download.

The component registers the interface via Scoped DI in `RunWebAsync`:

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Program.cs (RunWebAsync)
static async Task RunWebAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseStaticWebAssets();
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
<<<<<<< HEAD
    builder.Services.AddScoped<IAgentDraftService, AgentDraftService>();
=======

    var connectionString = builder.Configuration.GetConnectionString("HrDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:HrDb");
    builder.Services.AddPersistence(connectionString);   // registers IConversationService via EF Core
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });
    builder.Services.AddScoped<IAgentDraftService, AgentDraftService>();
    builder.Services.AddScoped<UserContext>();
    builder.Services.AddAuthorization();
>>>>>>> release/v1

    var app = builder.Build();
    app.UseStaticFiles();
    app.MapStaticAssets();
<<<<<<< HEAD
=======
    app.UseAuthentication();
    app.UseAuthorization();
>>>>>>> release/v1
    app.UseAntiforgery();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

<<<<<<< HEAD
    Console.WriteLine("HrMcp.Agent starting in --web mode.");
=======
    Console.WriteLine("HrMcp.Agent starting in web mode. Pass --console to run the console agent instead.");
>>>>>>> release/v1
    await app.RunAsync();
}
```

`AddScoped` is the right lifetime here. Blazor Server runs each circuit as a logical scope — each browser tab gets its own `AgentDraftService` instance with its own MCP client and conversation history. If this were `AddSingleton`, all users would share the same agent state.

---

## Step 3 — The AgentDraftService Implementation

`AgentDraftService` implements `IAgentDraftService` and `IAsyncDisposable`. It holds `HrAgent` and `McpClient` as nullable fields — both are created lazily on the first call, not at DI registration time.

<<<<<<< HEAD
=======
### Constructor

`AgentDraftService` takes two constructor parameters injected by the DI container:

```csharp
public AgentDraftService(IConversationService conversationService, UserContext userContext)
{
    _conversationService = conversationService;
    _userContext = userContext;
}
```

Both are `Scoped` — they are resolved once per Blazor circuit (browser tab). `IConversationService` provides the EF Core session store. `UserContext` resolves the authenticated user's ID for database queries.

>>>>>>> release/v1
### SendPromptAsync

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs
<<<<<<< HEAD
public async Task<string> SendPromptAsync(string prompt, CancellationToken ct = default)
{
    await EnsureInitializedAsync(ct);
=======
public async Task<string> SendPromptAsync(string prompt, Guid? sessionId = null, CancellationToken ct = default)
{
    await EnsureInitializedAsync(ct);

    if (sessionId.HasValue)
    {
        var userId = await _userContext.GetUserIdAsync() ?? "dev-user";
        var session = await _conversationService.GetSessionAsync(sessionId.Value, userId, ct);
        if (session is not null && session.Turns.Count > 0)
        {
            var priorMessages = session.Turns
                .OrderBy(t => t.Timestamp)
                .Select(t => new ChatMessage(
                    string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase)
                        ? ChatRole.User
                        : ChatRole.Assistant,
                    t.Text))
                .ToList();
            _agent!.ResetHistory(priorMessages);
        }
    }

>>>>>>> release/v1
    return await _agent!.AskAsync(prompt, ct);
}
```

<<<<<<< HEAD
Two lines. The real work is in `EnsureInitializedAsync`.
=======
When a `sessionId` is provided, the method loads the full conversation history from the database and replays it into `HrAgent` before sending the new prompt. This ensures the LLM has full context even after a page refresh — the agent's in-memory history is rebuilt from the persisted turns on every call. The real work is in `EnsureInitializedAsync`.
>>>>>>> release/v1

### EnsureInitializedAsync — lazy initialization

```csharp
private async Task EnsureInitializedAsync(CancellationToken ct)
{
    if (_agent is not null)
        return;

    _configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile(
            $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
            optional: true)
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables()
        .Build();

    var transportType = ResolveTransportType(_configuration);

    var additionalHeaders = await TryGetOidcHeadersAsync(_configuration, ct);
    var clientTransport = await CreateClientTransportAsync(_configuration, transportType, additionalHeaders);

    using var mcpLoggerFactory = LoggerFactory.Create(b => b
        .AddFilter((category, level) =>
            category?.StartsWith("ModelContextProtocol", StringComparison.Ordinal) == true && level >= LogLevel.Warning)
        .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));

    _mcpClient = await McpClient.CreateAsync(clientTransport, loggerFactory: mcpLoggerFactory);
    var mcpTools = await _mcpClient.ListToolsAsync();

    var chatClient = CreateChatClient(_configuration);
    var numCtx = _configuration.GetValue<int?>("AI:Ollama:NumCtx");

    _agent = new HrAgent(
        chatClient,
        mcpTools.Cast<AITool>().ToList(),
        UiStyle.Minimal,
        numCtx,
        FindOutputFolder());
}
```

The lazy pattern matters here. Creating an `McpClient` opens a transport connection — stdio launches a child process, stream-http makes an HTTP handshake. Doing that at DI registration time (in a constructor) would block the application startup and make transient errors unrecoverable. Deferring to the first request means:

- startup is instant
- errors surface with a stack trace the component can catch and display
- the connection is established in the context of a real request with a live `CancellationToken`

### ResolveTransportType

```csharp
private static string ResolveTransportType(IConfiguration configuration)
{
    var args = Environment.GetCommandLineArgs();
    if (args.Any(a => string.Equals(a, "--stream-http", StringComparison.OrdinalIgnoreCase)))
        return "streamHttp";

    if (args.Any(a => string.Equals(a, "--stdio", StringComparison.OrdinalIgnoreCase)))
        return "stdio";

    return configuration["McpServer:Transport:Type"] ?? "stdio";
}
```

Transport selection follows a priority chain: CLI args win, then `appsettings.json`. This means you never have to edit config files to switch between stdio and stream-http during local development — just change the command-line flag.

### CreateChatClient — the IChatClient abstraction in action

```csharp
private static IChatClient CreateChatClient(IConfiguration configuration)
{
    var provider = configuration["AI:Provider"] ?? "Ollama";

    if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
    {
        var endpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["AI:Ollama:Model"] ?? "llama3.2";

        var httpClient = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = Timeout.InfiniteTimeSpan };
        return (IChatClient)new OllamaApiClient(httpClient, model, null!);
    }

    var azureEndpoint = configuration["AI:AzureOpenAI:Endpoint"]
        ?? throw new InvalidOperationException("Missing configuration: AI:AzureOpenAI:Endpoint");
    var azureDeployment = configuration["AI:AzureOpenAI:Deployment"]
        ?? throw new InvalidOperationException("Missing configuration: AI:AzureOpenAI:Deployment");
    var apiKey = configuration["AI:AzureOpenAI:ApiKey"];

    var client = string.IsNullOrWhiteSpace(apiKey)
        ? new AzureOpenAIClient(new Uri(azureEndpoint), new DefaultAzureCredential())
        : new AzureOpenAIClient(new Uri(azureEndpoint), new System.ClientModel.ApiKeyCredential(apiKey));

    return client.GetChatClient(azureDeployment).AsIChatClient();
}
```

This is the one place in the entire codebase where provider-specific types appear — `OllamaApiClient`, `AzureOpenAIClient`. Both are immediately cast to `IChatClient`. Everything above this method in the call stack is provider-agnostic. Swap `"AI:Provider"` in `appsettings.json` and the component, the service interface, and the agent all stay unchanged.

---

## Step 4 — The Chat Panel Component

The chat panel lives in `DraftWorkspace.razor`. The workspace is a two-panel layout (chat left, WYSIWYG draft editor right), but this post focuses on the left panel — the chat thread and composer.

### Injecting the service

```razor
@inject IAgentDraftService AgentDraftService
<<<<<<< HEAD
@inject IJSRuntime JS
```

The component holds two state fields for the chat UI:
=======
@inject IConversationService ConversationService
@inject UserContext UserContext
@inject NavigationManager Nav
@inject IJSRuntime JS
```

The component holds the core state fields for the chat UI:
>>>>>>> release/v1

```csharp
private readonly List<ChatTurn> _turns = [];
private bool _busy;
<<<<<<< HEAD
```

`_turns` is the conversation history. `_busy` disables the Send button and renders the loading indicator while a request is in flight.
=======
private string _userId = string.Empty;

[Parameter] public Guid? SessionId { get; set; }
```

`_turns` is the conversation history. `_busy` disables the Send button and renders the loading indicator while a request is in flight. `_userId` is resolved in `OnParametersSetAsync` from `UserContext` — the Send button stays disabled until it is populated. `SessionId` is a route parameter — `/workspace/{SessionId:guid}`.
>>>>>>> release/v1

### The chat thread markup

```razor
<section class="left-chat">
    <h3>Writing Assistant</h3>
    <div class="chat-thread">
        @if (_turns.Count == 0)
        {
            <div class="chat-item">Chat responses appear here after you send a prompt.</div>
        }
        else
        {
            @foreach (var turn in _turns)
            {
                var isUser = string.Equals(turn.Role, "You", StringComparison.OrdinalIgnoreCase);
                <div class="chat-bubble-row @(isUser ? "chat-bubble-row--user" : "chat-bubble-row--assistant")">
                    <div class="chat-bubble">
                        <strong>@turn.Role:</strong>
                        @if (isUser)
                        {
                            <span> @turn.Text</span>
                        }
                        else
                        {
                            <div class="chat-bubble-content">@RenderAssistantMarkdown(turn.Text)</div>
                        }
                    </div>
                </div>
            }

            @if (_busy)
            {
                <div class="chat-bubble-row chat-bubble-row--assistant">
                    <div class="chat-bubble chat-bubble--loading" role="status" aria-live="polite">
                        <span class="chat-spinner" aria-hidden="true"></span>
                        <span>Assistant is drafting your response...</span>
                    </div>
                </div>
            }
        }
    </div>
```

The empty-state message prevents a jarring blank panel on first load. User turns render as plain text — no markdown processing, because we want the user's exact words back. Assistant turns go through `RenderAssistantMarkdown`, which runs Markdig with advanced extensions and returns a `MarkupString`. The `aria-live="polite"` on the loading indicator gives screen readers a signal when the assistant starts responding.

### The chat composer

```razor
    <div class="chat-composer">
        <textarea class="chat-input"
                  @bind="Prompt"
                  @bind:event="oninput"
                  @onkeydown="HandlePromptKeyDown"
                  placeholder="Ask for help drafting or improving your position description..."></textarea>
        <div class="panel-actions panel-actions--chat">
            <div class="panel-actions-row panel-actions-row--end">
                <span
                    class="help-icon"
                    title="Enter to send, Shift+Enter for a new line. Draft updates automatically for draft/refine prompts."
                    aria-label="Help: Enter to send, Shift+Enter for a new line. Draft updates automatically for draft/refine prompts.">?</span>
<<<<<<< HEAD
                <button class="primary-btn primary-btn--compact" @onclick="SendPromptAsync" disabled="@_busy">Send</button>
=======
                <button class="primary-btn primary-btn--compact" @onclick="SendPromptAsync" disabled="@(_busy || string.IsNullOrEmpty(_userId))">Send</button>
>>>>>>> release/v1
            </div>
        </div>
    </div>
</section>
```

`@bind:event="oninput"` syncs on every keystroke rather than on blur — needed so `HandlePromptKeyDown` always sees the current value when Enter is pressed.

### HandlePromptKeyDown — Enter to send, Shift+Enter for newline

```csharp
private async Task HandlePromptKeyDown(KeyboardEventArgs args)
{
    if (!string.Equals(args.Key, "Enter", StringComparison.Ordinal) || args.ShiftKey)
        return;

    Prompt = Prompt.TrimEnd('\r', '\n');
    await SendPromptAsync();
}
```

The condition reads: only act when `Enter` is pressed _without_ Shift. `TrimEnd('\r', '\n')` strips the newline the browser inserts before the handler fires, so the prompt submitted to the agent is clean.

### SendPromptAsync — the full chat dispatch

```csharp
private async Task SendPromptAsync()
{
    if (_busy || string.IsNullOrWhiteSpace(Prompt))
        return;

    _busy = true;
    _status = string.Empty;
    var input = Prompt.Trim();
    Prompt = string.Empty;
<<<<<<< HEAD
    _turns.Add(new ChatTurn("You", input, DateTimeOffset.UtcNow));

    try
    {
        var response = await AgentDraftService.SendPromptAsync(input);
        _turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
=======

    try
    {
        // Create a new session on the first turn
        if (SessionId is null)
        {
            var newSession = await ConversationService.CreateSessionAsync(_userId, input);
            SessionId = newSession.Id;
            Nav.NavigateTo($"/workspace/{SessionId}", forceLoad: false);
        }

        _turns.Add(new ChatTurn("You", input, DateTimeOffset.UtcNow));
        await ConversationService.AddTurnAsync(SessionId.Value, _userId, "user", input);

        var response = await AgentDraftService.SendPromptAsync(input, SessionId);
        _turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
        await ConversationService.AddTurnAsync(SessionId.Value, _userId, "assistant", response);
>>>>>>> release/v1

        if (ShouldSyncDraft(input, response))
        {
            var draftMarkdown = ExtractDraftMarkdown(response);
            if (draftMarkdown is not null)
            {
                var html = NormalizeHtmlForQuill(Markdown.ToHtml(draftMarkdown, ChatMarkdownPipeline));
                if (!_draftVisible)
                {
                    _pendingHtml = html;
                    _draftVisible = true;
                }
                else
                {
                    await JS.InvokeVoidAsync("setQuillContent", "quill-editor-wrapper", html);
                }
            }
        }
    }
    catch (Exception ex)
    {
        _status = $"Error: {ex.Message}";
    }
    finally
    {
        _busy = false;
    }
}
```

The sequence on each Send:

1. Guard: bail if busy or empty.
2. Set `_busy = true` — disables Send button, triggers re-render showing the loading indicator.
3. Capture and clear `Prompt` — the textarea empties immediately so the user sees feedback.
<<<<<<< HEAD
4. Add the user turn to `_turns` — the user message appears in the thread before the request goes out.
5. `await AgentDraftService.SendPromptAsync(input)` — blocks here until the agent has a full response. This is the non-streaming version; Part 4 replaces this with a token stream.
6. Add the assistant turn — the response appears rendered as markdown.
7. `ShouldSyncDraft` checks whether the prompt contained drafting intent keywords (like "draft", "refine", "rewrite"). If it did, `ExtractDraftMarkdown` strips the conversational preamble and closing question from the response to get the pure document body, which is then pushed into the Quill editor on the right panel.
8. `finally` always clears `_busy` — the loading indicator goes away and Send re-enables regardless of success or failure.
=======
4. **First turn only:** create a new session via `ConversationService.CreateSessionAsync` and update the URL to `/workspace/{SessionId}` without a page reload.
5. Add the user turn to `_turns` and persist it to the database via `AddTurnAsync`.
6. `await AgentDraftService.SendPromptAsync(input, SessionId)` — blocks until the agent returns the complete response.
7. Add the assistant turn and persist it to the database.
8. `ShouldSyncDraft` checks whether the prompt contained drafting intent keywords (like "draft", "refine", "rewrite"). If it did, `ExtractDraftMarkdown` strips the conversational preamble and closing question from the response to get the pure document body, which is then pushed into the Quill editor on the right panel.
9. `finally` always clears `_busy` — the loading indicator goes away and Send re-enables regardless of success or failure.
>>>>>>> release/v1

---

## Step 5 — Run It

You need two terminals. In stream-http mode the MCP server runs as a separate process; the agent connects to it over HTTP.

```bash
# Terminal 1 — MCP Server (from the DotnetAiAgentUiTutorial repo root)
dotnet run --project DotnetAiAgentUI/src/HrMcp.McpServer -- --stream-http
```

Wait for the server to log that it is listening (`Now listening on: http://localhost:5100`), then start the agent UI:

```bash
# Terminal 2 — Agent Web UI (from the DotnetAiAgentUiTutorial repo root)
<<<<<<< HEAD
dotnet run --project DotnetAiAgentUI/src/HrMcp.Agent -- --web --stream-http
=======
dotnet run --project DotnetAiAgentUI/src/HrMcp.Agent -- --stream-http
>>>>>>> release/v1
```

Open `http://localhost:5000` (or whatever port the agent logs) in your browser.

<<<<<<< HEAD
=======
![Chat panel with AI response](screenshots/chat-with-response.png)

>>>>>>> release/v1
You will see the workspace with the chat panel on the left. The right draft panel is hidden until the agent returns a response that contains a markdown document — send a prompt like _"Draft a position description for a software engineer"_ and both panels appear.

A non-drafting prompt like _"List open positions"_ triggers the MCP `GetOpenPositions` tool and returns a formatted list directly into the chat thread — the right panel stays hidden.

### Startup without the MCP server

If the MCP server is not running when you send the first prompt, the `WaitForHttpServerAsync` probe in `CreateClientTransportAsync` will retry up to 15 times (one second apart) before throwing a `TimeoutException`. The component catches that exception and displays the message in `_status`. No crash, no blank screen.

---

## What We Have

- `ChatTurn` — a three-field immutable record that is the full conversation state model
- `IAgentDraftService` — a two-method interface that is the only seam between the UI and the AI pipeline
- `AgentDraftService` — lazy-initialized service that resolves transport type from CLI args, creates the MCP client, wires `IChatClient`, and manages the `HrAgent` lifecycle per Blazor circuit
- `DraftWorkspace.razor` (chat panel) — a thread renderer, a composer with Enter-to-send, a loading indicator, and a `SendPromptAsync` dispatch that adds user and assistant turns, handles errors, and optionally syncs the right-panel editor when the response contains a draft

<<<<<<< HEAD
The response arrives as a complete string — no streaming. The user types, waits, reads. It works, but for long responses the wait is uncomfortable. That changes in Part 4.
=======
The response arrives as a complete string. The user types, sees the loading indicator, then reads the response when the model finishes. Part 4 covers the loading indicator lifecycle in detail, along with session persistence and the draft intelligence that decides when to populate the right editor panel.
>>>>>>> release/v1

---

## Next Up

<<<<<<< HEAD
Part 4 adds token-by-token streaming so responses appear as they are generated, with a cancel button for long responses.

→ **[Part 4: Streaming Responses & Real-Time UX](part-4-streaming-responses-realtime-ux.md)**
=======
Part 4 digs into the real-time UX details: the `_busy` lifecycle, session persistence, and how the component decides which responses belong in the draft editor.

→ **[Part 4: Real-Time UX & Session Persistence](part-4-streaming-responses-realtime-ux.md)**
>>>>>>> release/v1

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
