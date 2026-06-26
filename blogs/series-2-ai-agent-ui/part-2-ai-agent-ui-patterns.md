# Part 2: AI Agent UI Patterns

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 2 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)

---

## Introduction

In Part 1 we stood up the Blazor United project — routing, MudBlazor layout, and the solution structure. The shell is in place, but the UI has no idea how to talk to an AI agent yet.

This post is the mental-model part. No build steps — just concepts. It is the direct parallel to [Series 1 Part 2](../series-1-ai-agent-mcp/part-2-intro-to-mcp.md), which covered MCP theory before any server code appeared. Here I cover the UI architecture patterns before any component code appears.

By the end of this post you will understand:

- why `IChatClient` sits between the UI and every model provider
- what `ChatTurn` is and why that three-field record is all the conversation state you need
- how a Blazor component models the three states every AI chat panel has
- what `IAgentDraftService` is and why it is the only seam the Blazor component ever crosses
- how a request travels from a browser click all the way to the MCP server and back

Nothing here is HR-specific. Every pattern in this post applies to any Blazor AI chat panel regardless of domain.

---

## The IChatClient Abstraction

The most important design decision in this architecture is one you never see in the UI code: the UI never calls a model provider directly.

`Microsoft.Extensions.AI` defines `IChatClient` — a single, provider-neutral interface for sending messages to a language model and receiving a response. Under the covers that interface can be backed by:

- Ollama running locally
- Azure OpenAI
- OpenAI
- Any other provider that ships an `IChatClient` adapter

The Blazor component and every service it calls only ever see `IChatClient`. They do not import `OllamaSharp`. They do not import the Azure OpenAI SDK. They do not contain any provider-specific logic.

The only place the provider decision lives is `appsettings.json`:

```json
// DotnetAiAgentUI/src/HrMcp.Agent/appsettings.json
// Change "Provider" here — nothing else in the codebase changes
{
  "AI": {
    "Provider": "Ollama",
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "gemma4:latest",
      "NumCtx": 32768
    },
    "AzureOpenAI": {
      "Endpoint": "https://YOUR-RESOURCE-NAME.openai.azure.com/",
      "Deployment": "gpt-4.1-mini",
      "ApiKey": "YOUR_AZURE_OPENAI_KEY"
    }
  }
}
```

Set `Provider` to `"AzureOpenAI"` and the same Blazor component, the same `IAgentDraftService`, and the same `HrAgent` all work against Azure OpenAI. Set it back to `"Ollama"` and they work against a local model. The UI never knows the difference.

That is the provider-swap pattern. It matters for local development (Ollama, no cost, no cloud dependency) and for production or demos (Azure OpenAI). You change one JSON key.

---

## The Chat Turn Model

A conversation is a sequence of messages. Each message has an author, content, and a timestamp. That is all.

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Models/ChatTurn.cs
public sealed record ChatTurn(string Role, string Text, DateTimeOffset Timestamp);
```

Three things to notice about this type.

**It is a record.** Records are value-equal by their fields. A `ChatTurn` is data — it describes a moment in a conversation. It does not behave. It does not have methods. It does not derive from anything. Using a record signals clearly that this type is immutable state.

**It is sealed.** There is one kind of chat turn. Not a hierarchy of `UserTurn`, `AssistantTurn`, `SystemTurn` subclasses. The `Role` string carries that distinction — `"user"`, `"assistant"`, `"system"`. Keeping the type flat means the rendering code stays simple: one loop, one condition on `Role`.

**`DateTimeOffset`, not `DateTime`.** Chat panels often render timestamps. `DateTimeOffset` carries timezone offset, which matters when messages arrive across timezone boundaries or when you export a conversation log.

The list of `ChatTurn` objects *is* the conversation state. There is no separate message store, no conversation object wrapping the list, no ID dictionary. The component holds `List<ChatTurn> _turns` and that is the entire thread.

---

## Component State Design for Async AI Responses

Every AI chat component has three states. Most developers think of two (idle and loading) and discover the third one when they try to stream tokens.

**Idle.** The component is waiting for the user to type and submit. Input is enabled. No spinner.

**Busy.** A prompt has been submitted. The agent is working. Input is disabled. A spinner or progress indicator is visible. The user cannot submit another prompt until this one completes.

**Streaming.** Tokens are arriving. The UI is updating in place as each token comes back. Input is still disabled, but the response is visibly growing.

In Blazor, these three states map to a straightforward pattern:

- `_busy` — a `bool` field that gates the submit button and shows the progress indicator
- `_turns` — the `List<ChatTurn>` that the render loop iterates
- `StateHasChanged()` — called explicitly from inside async callbacks to push updates to the render tree

The last point is the one that trips up developers new to Blazor's rendering model. Blazor re-renders a component automatically when:

- a user event handler completes
- a bound parameter changes

It does **not** re-render automatically when an awaited continuation resumes on a background thread or inside a streaming callback. If you are updating `_turns` inside a `foreach` loop that processes streaming tokens, you must call `StateHasChanged()` yourself after each update or after each meaningful batch. Without it, the UI does not update until the entire operation completes — you lose the streaming feel entirely.

The practical rule: any time you update component state from inside an `async` method that was not triggered by a direct user interaction, call `StateHasChanged()` after the update.

---

## The IAgentDraftService Interface

The Blazor component talks to exactly one thing: `IAgentDraftService`. That is the seam.

```csharp
// DotnetAiAgentUI/src/HrMcp.Agent/Web/Services/AgentDraftService.cs
public interface IAgentDraftService
{
    Task<string> SendPromptAsync(string prompt, CancellationToken ct = default);
    Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(
        string draftText, CancellationToken ct = default);
}
```

Two methods. That is the entire surface the UI sees.

`SendPromptAsync` takes a string and returns a string. The component does not know that the implementation connects to an MCP client, initializes an `HrAgent`, selects a transport, negotiates capabilities, makes a tool call, feeds the result back to the LLM, and returns the final answer. None of that is visible at the interface level.

`ExportDraftToWordAsync` takes draft text and returns a tuple: a message, an optional filename, and optional file bytes. The component uses this to trigger a Word document download without knowing what the export pipeline looks like.

Why does this interface exist?

**Testability.** You can inject a mock `IAgentDraftService` in tests and verify component behavior without starting an Ollama instance or an MCP server.

**Replaceability.** If the underlying agent implementation changes — different LLM, different MCP transport, different tool set — the Blazor component does not change. Only the `AgentDraftService` implementation changes.

**Clarity.** Reading the component code, you immediately understand its dependencies: one service, two operations. The MCP wiring is out of scope for the component.

---

## How the UI Connects to the MCP Agent

The full call chain, layer by layer:

```
Browser
  └─► Blazor Component (chat panel)
        └─► IAgentDraftService
              └─► AgentDraftService.SendPromptAsync()
                    └─► HrAgent.AskAsync()
                          └─► IChatClient (Ollama or Azure OpenAI)
                                └─► LLM decides to call a tool
                                      └─► MCP Client
                                            └─► HrMcp.McpServer (stdio or Streamable HTTP)
                                                  └─► Tool implementation
                                                        └─► Returns result to LLM
                                                              └─► LLM produces final answer
                                                                    └─► string bubbles back up
```

Each layer owns exactly one concern:

| Layer | Concern |
|---|---|
| Blazor Component | Render state, user input, display results |
| `IAgentDraftService` | Contract between UI and agent infrastructure |
| `AgentDraftService` | Initialize `IChatClient`, `McpClient`, and `HrAgent`; lazy on first call |
| `HrAgent` | Manage the chat loop, tool-call roundtrips, conversation history |
| `IChatClient` | Abstract the model provider |
| MCP Client | Connect to `HrMcp.McpServer`, negotiate tools, invoke them |
| `HrMcp.McpServer` | Expose tools over `stdio` or Streamable HTTP |

The Blazor component is at the very top of this chain. It knows nothing below `IAgentDraftService`. It has no reference to `HrAgent`, `McpClient`, `OllamaApiClient`, or any transport type.

That separation is what makes the UI genuinely reusable. Swap out `AgentDraftService` for a different implementation — one that calls a different agent, a REST API, or a stub — and the Blazor component works unchanged.

---

## Why These Patterns Compose

These four pieces — `IChatClient`, `ChatTurn`, the three-state component model, and `IAgentDraftService` — are not independent decisions. They compose.

`IChatClient` abstracts the model. `IAgentDraftService` abstracts the agent. Together they mean the Blazor component has zero knowledge of either.

`ChatTurn` is the output format. It is what `_turns` contains after `SendPromptAsync` returns. The component appends a new `ChatTurn` for the user message and another for the assistant response. The render loop does the rest.

The three-state model is how the component manages time. AI responses are slow. The component needs to represent "we are waiting" and "tokens are arriving" without blocking the render thread. `_busy`, `_turns`, and `StateHasChanged()` are what keep the UI responsive.

None of these patterns require any knowledge of the HR domain. The same architecture works for a legal drafting assistant, a code review tool, or a customer support panel.

---

## Next Up

Part 3 is where we write the code. We implement `IAgentDraftService`, build the `ChatTurn` model, and wire up the first working chat panel.

→ **[Part 3: Building the Chat UI](part-3-building-the-chat-ui.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
