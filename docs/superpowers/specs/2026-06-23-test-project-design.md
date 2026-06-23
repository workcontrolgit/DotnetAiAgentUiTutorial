# Test Project Design Spec
**Date:** 2026-06-23  
**Status:** Approved

## Goal

Add a `HrMcp.Agent.Tests` xUnit + bunit test project covering the Blazor components and pure logic changed in the Chat UI Overhaul.

## Out of Scope

- E2E / Playwright tests
- Testing MCP server, Application, Core, or Infrastructure projects
- Separate test helpers project
- `.sln` file

---

## Project Setup

**Path:** `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj`

**Target framework:** `net10.0`

**NuGet packages:**
| Package | Version |
|---|---|
| `Microsoft.NET.Test.Sdk` | `17.*` |
| `xunit` | `2.*` |
| `xunit.runner.visualstudio` | `2.*` |
| `bunit` | `1.*` |
| `coverlet.collector` | `6.*` |

**Project reference:** `../../src/HrMcp.Agent/HrMcp.Agent.csproj`

**Run command:** `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj`

---

## File Structure

```
DotnetAiAgentUi/tests/HrMcp.Agent.Tests/
├── HrMcp.Agent.Tests.csproj
├── _Imports.razor                    ← @using directives for bunit component tests
├── Components/
│   ├── MainLayoutTests.cs            ← sidebar + settings modal tests
│   └── DraftWorkspaceTests.cs        ← chat bubble + placeholder + thread ID tests
└── Logic/
    └── DraftIntentTests.cs           ← pure xUnit facts, no bunit
```

---

## Test Infrastructure

### `_Imports.razor`
Provides global `@using` directives so bunit can discover and render components:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using HrMcp.Agent.Components.Layout
@using HrMcp.Agent.Components.Pages
@using HrMcp.Agent.Web.Services
@using HrMcp.Agent.Web.Models
@using Bunit
```

### Service Fakes (inline per test class)

**`IAgentDraftService`** — implemented as a local sealed class inside `DraftWorkspaceTests.cs`:
```csharp
private sealed class FakeAgentDraftService : IAgentDraftService
{
    public string NextResponse { get; set; } = "Hello from assistant";
    public Task<string> SendPromptAsync(string prompt, CancellationToken ct = default)
        => Task.FromResult(NextResponse);
    public Task<(string, string?, byte[]?)> ExportDraftToWordAsync(string draftText, CancellationToken ct = default)
        => Task.FromResult(("ok", (string?)null, (byte[]?)null));
}
```

**`IJSRuntime`** — bunit's `BunitJSInterop` is registered automatically via `TestContext.JSInterop`; no manual fake needed. JS calls that don't match registered handlers are set to `JSRuntimeMode.Loose` to avoid exceptions on `scrollChatToBottom` etc.

**`IConfiguration`** — built inline per test using `new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ... }).Build()`.

---

## Test Coverage

### `MainLayoutTests.cs`

All tests inject `IConfiguration` with:
```csharp
{ "AI:Provider", "AzureOpenAI" },
{ "AI:AzureOpenAI:Deployment", "gpt-4.1-mini" },
{ "McpServer:Transport:Type", "stdio" }
```

| Test name | Assertion |
|---|---|
| `Renders_AppShell_WithSidebar` | `.app-shell` and `.history-sidebar` present |
| `Sidebar_CollapsesOnHamburgerClick` | ≡ click → `history-sidebar--collapsed` class added |
| `Sidebar_ExpandsOnSecondHamburgerClick` | ≡ click twice → collapsed class removed |
| `SettingsModal_OpenOnGearClick` | ⚙ Settings click → `.modal-card` present |
| `SettingsModal_ClosesOnXClick` | × click → `.modal-card` absent |
| `SettingsModal_ClosesOnBackdropClick` | `.modal-overlay` click → `.modal-card` absent |
| `SettingsModal_ShowsAzureModel` | modal table cell contains `"gpt-4.1-mini"` |
| `SettingsModal_ShowsOllamaModel` | `AI:Provider=Ollama`, `AI:Ollama:Model=gemma4:latest` → modal shows `"gemma4:latest"` |
| `Sidebar_ShowsSignInPlaceholder` | text "Sign in" present, button has `disabled` attribute |
| `Sidebar_ShowsNoHistoryPlaceholder` | text "No history yet" present |

### `DraftWorkspaceTests.cs`

All tests register `FakeAgentDraftService` and set `JSInterop.Mode = JSRuntimeMode.Loose`.

| Test name | Assertion |
|---|---|
| `Renders_EmptyState_Placeholder` | `<textarea>` has `placeholder` containing `Message the assistant` |
| `Renders_EmptyState_NoBubbles` | no `.chat-bubble` elements on initial render |
| `ChatThread_HasCorrectId` | element with `id="chat-thread"` exists |
| `NoRoleLabels_InBubbles` | no `<strong>` inside any `.chat-bubble` after a turn is added |
| `UserBubble_AlignsRight` | after send, a `.chat-bubble-row--user` element exists |
| `AssistantBubble_AlignsLeft` | after response, a `.chat-bubble-row--assistant` element exists |

> **Note on triggering send:** bunit renders components synchronously. To simulate a sent prompt, tests set the textarea value and click the Send button, then call `cut.WaitForState(...)` to await the async `SendPromptAsync`.

### `DraftIntentTests.cs`

Pure xUnit — no bunit, no services. Tests call the private static methods via reflection (or the methods are made `internal` with `[assembly: InternalsVisibleTo(...)]`).

| Test name | Input | Expected |
|---|---|---|
| `IsDraftIntentPrompt_Draft_ReturnsTrue` | `"draft a PD for a software engineer"` | `true` |
| `IsDraftIntentPrompt_Revise_ReturnsTrue` | `"revise the qualifications section"` | `true` |
| `IsDraftIntentPrompt_OpenPositions_ReturnsFalse` | `"list open positions"` | `false` |
| `IsDraftIntentPrompt_Empty_ReturnsFalse` | `""` | `false` |
| `ExtractDraftMarkdown_WithHeading_ReturnsBody` | response with preamble + `## Title` heading | returns from `##` onwards |
| `ExtractDraftMarkdown_NoHeading_ReturnsNull` | `"Sure, I can help with that."` | `null` |
| `ExtractDraftMarkdown_StripsClosingLines` | body ending with `"Let me know if you'd like changes."` | closing line stripped |

---

## InternalsVisibleTo

Add to `HrMcp.Agent.csproj`:
```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>HrMcp.Agent.Tests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

Change the private static methods under test in `DraftWorkspace.razor` from `private` to `internal`:
- `IsDraftIntentPrompt`
- `ExtractDraftMarkdown`
- `IsClosingLine`

---

## File Change Summary

| File | Action |
|---|---|
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj` | Create |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/_Imports.razor` | Create |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/MainLayoutTests.cs` | Create |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs` | Create |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs` | Create |
| `DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj` | Add `InternalsVisibleTo` |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Change 3 methods `private` → `internal` |
