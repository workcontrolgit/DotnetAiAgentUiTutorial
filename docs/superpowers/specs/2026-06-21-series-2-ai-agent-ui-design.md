# Design Spec: AI Agent UI with Blazor United & .NET 10 — Blog Series 2

**Date:** 2026-06-21
**Status:** Approved
**Author:** Fuji Nguyen

---

## Overview

A 6-part blog series (plus preface) teaching .NET developers how to build a general-purpose Blazor United AI Agent Chat UI. The series mirrors the structure, tone, and conventions of Series 1 (*AI Agents & MCP with .NET 10*) and targets the same audience.

The companion repository is `workcontrolgit/DotnetAiAgentUiTutorial`. The domain stays HR (connecting cleanly to Series 1), but every UI pattern taught is reusable for any MCP-backed agent.

---

## Goals

- Teach .NET developers how to build a production-ready Blazor AI Agent UI from scratch
- Provide a reusable, general-purpose chat UI template — not HR-specific
- Mirror Series 1's structure exactly: Preface + 6 parts, one clear deliverable per part
- Mixed audience: recap Series 1 in the preface; each part self-contained enough to stand alone
- Use `IChatClient` abstraction throughout — Ollama as default, swappable to Azure OpenAI or any provider

---

## Tech Stack

| Concern | Choice | Reason |
|---|---|---|
| Framework | .NET 10 Blazor United (Auto render mode) | Modern unified Blazor model, matches rest of series |
| UI Library | MudBlazor 8 | Most popular free Blazor component library, already in repo |
| AI abstraction | `Microsoft.Extensions.AI` / `IChatClient` | Provider-agnostic, consistent with Series 1 |
| Default LLM | Ollama (local) | No API keys, matches Series 1 |
| WYSIWYG editor | Blazored.TextEditor | Already in repo, Quill-based |
| Auth | OIDC / Duende IdentityServer | Consistent with Series 1 Part 6 |
| MCP client | `ModelContextProtocol` NuGet | Official .NET MCP SDK |

---

## Audience

- Primary: .NET backend developers who completed Series 1 or have equivalent MCP/agent knowledge
- Secondary: .NET developers new to the series — Preface recaps what is needed from Series 1
- Prerequisites: C#, async/await, ASP.NET Core, dependency injection. No prior Blazor or AI UI experience required.

---

## Series Structure

### Preface — Series Overview
**What:** Context, goals, prerequisites, series-at-a-glance table, companion repo link, connection to Series 1.
**Parallel:** Series 1 Preface
**Key message:** You built the backend in Series 1. Now you build the UI that puts it in front of users.

---

### Part 1 — Blazor United Foundation
**What you build:** .NET 10 Blazor United solution scaffold, MudBlazor 8 installed and configured, main layout (`MainLayout.razor`), routing, and a shell page.
**Concepts:** Blazor United Auto render mode, MudBlazor theme setup, project structure, `_Imports.razor`, `App.razor`, `Routes.razor`.
**Deliverable:** Running Blazor app with MudBlazor layout — no AI yet.
**Parallel:** Series 1 Part 1 (Clean Architecture Foundation)

---

### Part 2 — AI Agent UI Patterns (Concepts)
**What you build:** No code — pure concepts.
**Concepts:** `IChatClient` abstraction and why it matters, the chat turn model (`ChatTurn`), component state design for async AI responses, the provider-swap pattern (Ollama → Azure OpenAI with config change), how the UI connects to the MCP-backed agent through `IAgentDraftService`.
**Deliverable:** Mental model for the architecture before writing any UI code.
**Parallel:** Series 1 Part 2 (Introduction to MCP — concepts only)

---

### Part 3 — Building the Chat UI
**What you build:** `IAgentDraftService` interface, `AgentDraftService` implementation, `ChatTurn` model, `DraftWorkspace.razor` chat panel with MudBlazor chat bubbles, message thread, composer textarea, send button.
**Concepts:** Blazor component lifecycle, `@inject`, two-way binding, `EventCallback`, MudBlazor `MudPaper`/`MudTextField`/`MudButton` components, service registration in DI.
**Deliverable:** Working chat panel — type a prompt, get a response, see the conversation thread.
**Parallel:** Series 1 Part 3 (Building the MCP Server)

---

### Part 4 — Streaming Responses & Real-Time UX
**What you build:** Token-by-token streaming from `IChatClient`, `IAsyncEnumerable<StreamingChatCompletionUpdate>`, loading spinner, `StateHasChanged` patterns, `CancellationToken` / cancel button.
**Concepts:** Why streaming matters for UX in AI apps, Blazor's rendering loop and when to call `StateHasChanged`, graceful cancellation.
**Deliverable:** Chat panel with live streaming responses — tokens appear as they arrive, with a cancel button.
**Parallel:** Series 1 Part 4 (AI Agent with Microsoft.Extensions.AI + Ollama)

---

### Part 5 — Document Editor & Word Export
**What you build:** Split-panel layout (chat left, editor right), `Blazored.TextEditor` WYSIWYG integration, `DraftDocumentState` model, resizable splitter, Word export via MCP tool call (`ExportDraftToWord`), file download via JS interop.
**Concepts:** Blazor JS interop for file download, WYSIWYG editor lifecycle, `DraftDocumentState` as shared state between panels, triggering MCP tool calls from the UI.
**Deliverable:** Full Position Description Builder UI — chat assistant on the left, WYSIWYG draft editor on the right, Export to Word button.
**Parallel:** Series 1 Part 5 (Claude Desktop Integration & End-to-End Demo)

---

### Part 6 — Securing the UI with OIDC
**What you build:** OIDC authentication in the Blazor app, `TryGetOidcHeadersAsync` for token acquisition, client credentials flow wired to `AgentDraftService`, protected routes, Duende IdentityServer as OIDC provider (Docker).
**Concepts:** Blazor auth state, `AuthorizeView`, how the UI acquires a Bearer token and passes it to the MCP agent transport, swapping Duende for Okta or Azure Entra ID with config changes.
**Deliverable:** OIDC-secured Blazor UI — unauthenticated users redirected, authenticated users get full agent access.
**Parallel:** Series 1 Part 6 (Securing the MCP Server with OIDC)

---

## Folder Structure

```
blogs/
  series-2-ai-agent-ui/
    preface.md
    part-1-blazor-united-foundation.md
    part-2-ai-agent-ui-patterns.md
    part-3-building-the-chat-ui.md
    part-4-streaming-responses-realtime-ux.md
    part-5-document-editor-and-export.md
    part-6-securing-ui-with-oidc.md
    diagrams/
    screenshots/
    CHECKLIST.md
```

---

## Series-at-a-Glance Table (for Preface)

| # | Title | What You Build |
|---|---|---|
| Preface | Series Overview | Context, goals, prerequisites |
| 1 | Blazor United Foundation | Solution scaffold, MudBlazor layout, routing |
| 2 | AI Agent UI Patterns | Concepts — `IChatClient`, state, component design |
| 3 | Building the Chat UI | Chat component, message turns, `IAgentDraftService` wiring |
| 4 | Streaming Responses & Real-Time UX | Token streaming, loading states, cancellation |
| 5 | Document Editor & Word Export | Split-panel layout, WYSIWYG editor, Word export |
| 6 | Securing the UI with OIDC | Blazor auth, token acquisition, protected routes |

---

## Key Constraints

- Every part ships working, demonstrable code — same as Series 1
- No HR-specific logic in UI components — keep them reusable
- `IChatClient` abstraction must be present from Part 3 onward — no direct Ollama SDK calls in UI code
- MudBlazor 8 used consistently — no mixing with Bootstrap or plain CSS
- Each part has a clear git branch / PR mapping
- Tone: first-person, opinionated, every design decision justified — same voice as Series 1

---

## Out of Scope

- Mobile / PWA support
- Unit testing the Blazor components (not covered in Series 1 either)
- Deploying to Azure / Docker (deployment is a separate topic)
- Multiple concurrent chat sessions / multi-user

---

## Connection to Series 1

Series 2 picks up where Series 1 ends. The MCP server built in Series 1 Parts 1–3 is the backend the UI connects to. The `IChatClient` / Ollama setup from Series 1 Part 4 is the AI layer the UI sits on top of. Readers who completed Series 1 have the full stack; readers starting here need only clone the companion repo and follow the Preface setup.
