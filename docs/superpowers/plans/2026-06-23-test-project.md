# Test Project Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `HrMcp.Agent.Tests` xUnit + bunit project covering `MainLayout` (sidebar/modal), `DraftWorkspace` (chat bubbles/placeholder), and the pure draft-intent logic.

**Architecture:** Single test project under `DotnetAiAgentUi/tests/`. bunit's `TestContext` is used as base class for component tests; plain xUnit facts cover the static logic methods. Three static methods in `DraftWorkspace.razor` are promoted from `private` to `internal` and exposed via `InternalsVisibleTo`.

**Tech Stack:** .NET 10, xUnit 2.x, bunit 1.x, `Microsoft.NET.Sdk.Razor` project SDK.

## Global Constraints

- Target framework: `net10.0`
- Test project SDK: `Microsoft.NET.Sdk.Razor` (required for bunit Blazor component rendering)
- No new packages in `HrMcp.Agent.csproj` — only the test project gets new packages
- Package versions: `Microsoft.NET.Test.Sdk 17.*`, `xunit 2.*`, `xunit.runner.visualstudio 2.*`, `bunit 1.*`, `coverlet.collector 6.*`
- Run command (from repo root): `dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj`
- Do NOT include `Co-Authored-By:` in git commit messages
- No changes to `AgentDraftService.cs`, `HrAgent.cs`, or any backend service
- No changes to the right editor panel or Quill logic

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj` | Create | Test project definition |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/_Imports.razor` | Create | Global Razor `@using` directives for bunit |
| `DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj` | Modify | Add `InternalsVisibleTo` for test project |
| `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Modify | `private` → `internal` on 3 static methods |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs` | Create | 7 pure xUnit facts for draft intent + markdown extraction logic |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/MainLayoutTests.cs` | Create | 10 bunit tests for sidebar toggle + settings modal |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs` | Create | 6 bunit tests for chat bubbles, placeholder, thread ID, role labels |

---

### Task 1: Project scaffold + visibility

**Files:**
- Create: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj`
- Create: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/_Imports.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Produces: `internal static bool DraftWorkspace.IsDraftIntentPrompt(string prompt)`
- Produces: `internal static string? DraftWorkspace.ExtractDraftMarkdown(string response)`
- Produces: `internal static bool DraftWorkspace.IsClosingLine(string line)`

- [ ] **Step 1: Create the test project directory and `.csproj`**

```bash
mkdir -p DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic
mkdir -p DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components
```

Create `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="bunit" Version="1.*" />
    <PackageReference Include="coverlet.collector" Version="6.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/HrMcp.Agent/HrMcp.Agent.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create `_Imports.razor`**

Create `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/_Imports.razor`:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using HrMcp.Agent.Components.Layout
@using HrMcp.Agent.Components.Pages
@using HrMcp.Agent.Web.Services
@using HrMcp.Agent.Web.Models
@using Bunit
```

- [ ] **Step 3: Add `InternalsVisibleTo` to `HrMcp.Agent.csproj`**

In `DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj`, add inside the root `<Project>` element (after the last `</ItemGroup>`):

```xml
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>HrMcp.Agent.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
```

- [ ] **Step 4: Change 3 methods from `private` to `internal` in `DraftWorkspace.razor`**

In `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`, find and change:

```csharp
// BEFORE (line ~357):
private static bool IsDraftIntentPrompt(string prompt)

// AFTER:
internal static bool IsDraftIntentPrompt(string prompt)
```

```csharp
// BEFORE (line ~316):
private static string? ExtractDraftMarkdown(string response)

// AFTER:
internal static string? ExtractDraftMarkdown(string response)
```

```csharp
// BEFORE (line ~343):
private static bool IsClosingLine(string line)

// AFTER:
internal static bool IsClosingLine(string line)
```

- [ ] **Step 5: Build both projects**

```bash
dotnet build DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj
dotnet build DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj
```

Expected: both succeed with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj
git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/_Imports.razor
git add DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
git commit -m "feat(tests): scaffold xUnit + bunit test project with InternalsVisibleTo"
```

---

### Task 2: Logic tests — DraftIntentTests

**Files:**
- Create: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`

**Interfaces:**
- Consumes: `internal static bool DraftWorkspace.IsDraftIntentPrompt(string)` (Task 1)
- Consumes: `internal static string? DraftWorkspace.ExtractDraftMarkdown(string)` (Task 1)

- [ ] **Step 1: Write the tests**

Create `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs`:

```csharp
using HrMcp.Agent.Components.Pages;
using Xunit;

namespace HrMcp.Agent.Tests.Logic;

public sealed class DraftIntentTests
{
    [Fact]
    public void IsDraftIntentPrompt_Draft_ReturnsTrue() =>
        Assert.True(DraftWorkspace.IsDraftIntentPrompt("draft a PD for a software engineer"));

    [Fact]
    public void IsDraftIntentPrompt_Revise_ReturnsTrue() =>
        Assert.True(DraftWorkspace.IsDraftIntentPrompt("revise the qualifications section"));

    [Fact]
    public void IsDraftIntentPrompt_OpenPositions_ReturnsFalse() =>
        Assert.False(DraftWorkspace.IsDraftIntentPrompt("list open positions"));

    [Fact]
    public void IsDraftIntentPrompt_Empty_ReturnsFalse() =>
        Assert.False(DraftWorkspace.IsDraftIntentPrompt(""));

    [Fact]
    public void ExtractDraftMarkdown_WithHeading_ReturnsBody()
    {
        const string response = "Sure! Here is your draft.\n\n## Job Title\n\nThis is the body.\n";
        var result = DraftWorkspace.ExtractDraftMarkdown(response);
        Assert.NotNull(result);
        Assert.StartsWith("## Job Title", result);
        Assert.DoesNotContain("Sure!", result);
    }

    [Fact]
    public void ExtractDraftMarkdown_NoHeading_ReturnsNull()
    {
        var result = DraftWorkspace.ExtractDraftMarkdown("Sure, I can help with that.");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractDraftMarkdown_StripsClosingLines()
    {
        const string response = "## Summary\n\nThis is the body.\n\nLet me know if you'd like any changes.";
        var result = DraftWorkspace.ExtractDraftMarkdown(response);
        Assert.NotNull(result);
        Assert.DoesNotContain("Let me know", result);
        Assert.Contains("This is the body.", result);
    }
}
```

- [ ] **Step 2: Run tests — expect all 7 to pass**

```bash
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj --filter "FullyQualifiedName~DraftIntentTests" -v normal
```

Expected output:
```
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7
```

If any test fails, read the failure message. The logic already exists in `DraftWorkspace.razor` — a failure here means the method's behavior doesn't match the test's assumption. Fix the test to match actual behavior (do not change the production logic).

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Logic/DraftIntentTests.cs
git commit -m "test(logic): add DraftIntentTests for draft intent and markdown extraction"
```

---

### Task 3: MainLayout component tests

**Files:**
- Create: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/MainLayoutTests.cs`

**Interfaces:**
- Consumes: `MainLayout` component from `HrMcp.Agent.Components.Layout`
- Consumes: `IConfiguration` registered as singleton on `TestContext.Services`

- [ ] **Step 1: Write the tests**

Create `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/MainLayoutTests.cs`:

```csharp
using Bunit;
using HrMcp.Agent.Components.Layout;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrMcp.Agent.Tests.Components;

public sealed class MainLayoutTests : TestContext
{
    private static IConfiguration AzureConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AI:Provider", "AzureOpenAI" },
                { "AI:AzureOpenAI:Deployment", "gpt-4.1-mini" },
                { "AI:Ollama:Model", "gemma4:latest" },
                { "McpServer:Transport:Type", "stdio" }
            })
            .Build();

    private static IConfiguration OllamaConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AI:Provider", "Ollama" },
                { "AI:AzureOpenAI:Deployment", "gpt-4.1-mini" },
                { "AI:Ollama:Model", "gemma4:latest" },
                { "McpServer:Transport:Type", "stdio" }
            })
            .Build();

    private IRenderedComponent<MainLayout> Render(IConfiguration? config = null)
    {
        Services.AddSingleton<IConfiguration>(config ?? AzureConfig());
        return RenderComponent<MainLayout>(p => p
            .Add(c => c.Body, builder => builder.AddMarkupContent(0, "<div>body</div>")));
    }

    [Fact]
    public void Renders_AppShell_WithSidebar()
    {
        var cut = Render();
        cut.Find(".app-shell");
        cut.Find(".history-sidebar");
    }

    [Fact]
    public void Sidebar_CollapsesOnHamburgerClick()
    {
        var cut = Render();
        cut.Find(".icon-btn").Click();
        Assert.Contains("history-sidebar--collapsed", cut.Find(".history-sidebar").ClassList);
    }

    [Fact]
    public void Sidebar_ExpandsOnSecondHamburgerClick()
    {
        var cut = Render();
        cut.Find(".icon-btn").Click();
        cut.Find(".icon-btn").Click();
        Assert.DoesNotContain("history-sidebar--collapsed", cut.Find(".history-sidebar").ClassList);
    }

    [Fact]
    public void SettingsModal_OpenOnGearClick()
    {
        var cut = Render();
        cut.Find(".ghost-btn").Click();
        cut.Find(".modal-card");
    }

    [Fact]
    public void SettingsModal_ClosesOnXClick()
    {
        var cut = Render();
        cut.Find(".ghost-btn").Click();
        cut.Find(".modal-close-btn").Click();
        Assert.Empty(cut.FindAll(".modal-card"));
    }

    [Fact]
    public void SettingsModal_ClosesOnBackdropClick()
    {
        var cut = Render();
        cut.Find(".ghost-btn").Click();
        cut.Find(".modal-overlay").Click();
        Assert.Empty(cut.FindAll(".modal-card"));
    }

    [Fact]
    public void SettingsModal_ShowsAzureModel()
    {
        var cut = Render(AzureConfig());
        cut.Find(".ghost-btn").Click();
        Assert.Contains("gpt-4.1-mini", cut.Find(".modal-card").TextContent);
    }

    [Fact]
    public void SettingsModal_ShowsOllamaModel()
    {
        var cut = Render(OllamaConfig());
        cut.Find(".ghost-btn").Click();
        Assert.Contains("gemma4:latest", cut.Find(".modal-card").TextContent);
    }

    [Fact]
    public void Sidebar_ShowsSignInPlaceholder()
    {
        var cut = Render();
        var btn = cut.Find(".sidebar-user-btn");
        Assert.Contains("Sign in", btn.TextContent);
        Assert.True(btn.HasAttribute("disabled"));
    }

    [Fact]
    public void Sidebar_ShowsNoHistoryPlaceholder()
    {
        var cut = Render();
        Assert.Contains("No history yet", cut.Find(".sidebar-history").TextContent);
    }
}
```

- [ ] **Step 2: Run tests — expect all 10 to pass**

```bash
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj --filter "FullyQualifiedName~MainLayoutTests" -v normal
```

Expected:
```
Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10
```

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/MainLayoutTests.cs
git commit -m "test(components): add MainLayoutTests for sidebar toggle and settings modal"
```

---

### Task 4: DraftWorkspace component tests

**Files:**
- Create: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`

**Interfaces:**
- Consumes: `DraftWorkspace` component from `HrMcp.Agent.Components.Pages`
- Consumes: `IAgentDraftService` from `HrMcp.Agent.Web.Services`
- Produces: `FakeAgentDraftService` (private inner class — only lives in this file)

- [ ] **Step 1: Write the tests**

Create `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs`:

```csharp
using Bunit;
using HrMcp.Agent.Components.Pages;
using HrMcp.Agent.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrMcp.Agent.Tests.Components;

public sealed class DraftWorkspaceTests : TestContext
{
    private sealed class FakeAgentDraftService : IAgentDraftService
    {
        public string NextResponse { get; set; } = "Hello from assistant";

        public Task<string> SendPromptAsync(string prompt, CancellationToken ct = default) =>
            Task.FromResult(NextResponse);

        public Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(
            string draftText, CancellationToken ct = default) =>
            Task.FromResult(("ok", (string?)null, (byte[]?)null));
    }

    private readonly FakeAgentDraftService _fake = new();

    public DraftWorkspaceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<IAgentDraftService>(_ => _fake);
    }

    [Fact]
    public void Renders_EmptyState_Placeholder()
    {
        var cut = RenderComponent<DraftWorkspace>();
        var placeholder = cut.Find("textarea").GetAttribute("placeholder") ?? string.Empty;
        Assert.Contains("Message the assistant", placeholder);
    }

    [Fact]
    public void Renders_EmptyState_NoBubbles()
    {
        var cut = RenderComponent<DraftWorkspace>();
        Assert.Empty(cut.FindAll(".chat-bubble"));
    }

    [Fact]
    public void ChatThread_HasCorrectId()
    {
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("#chat-thread");
    }

    [Fact]
    public void UserBubble_AlignsRight()
    {
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("textarea").Input("hello");
        cut.Find("button.primary-btn").Click();
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".chat-bubble-row--user")));
    }

    [Fact]
    public void AssistantBubble_AlignsLeft()
    {
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("textarea").Input("hello");
        cut.Find("button.primary-btn").Click();
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".chat-bubble-row--assistant")));
    }

    [Fact]
    public void NoRoleLabels_InBubbles()
    {
        var cut = RenderComponent<DraftWorkspace>();
        cut.Find("textarea").Input("hello");
        cut.Find("button.primary-btn").Click();
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".chat-bubble")));
        Assert.Empty(cut.FindAll(".chat-bubble strong"));
    }
}
```

- [ ] **Step 2: Run tests — expect all 6 to pass**

```bash
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj --filter "FullyQualifiedName~DraftWorkspaceTests" -v normal
```

Expected:
```
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

- [ ] **Step 3: Run the full suite**

```bash
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj -v normal
```

Expected:
```
Passed! - Failed: 0, Passed: 23, Skipped: 0, Total: 23
```

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/Components/DraftWorkspaceTests.cs
git commit -m "test(components): add DraftWorkspaceTests for chat bubbles, placeholder, and thread ID"
```

---

## Self-Review

**Spec coverage:**

| Spec requirement | Task |
|---|---|
| `HrMcp.Agent.Tests.csproj` with correct SDK and packages | Task 1 |
| `_Imports.razor` with using directives | Task 1 |
| `InternalsVisibleTo` in `HrMcp.Agent.csproj` | Task 1 |
| `IsDraftIntentPrompt`, `ExtractDraftMarkdown`, `IsClosingLine` → `internal` | Task 1 |
| `FakeAgentDraftService` inline in test file | Task 4 |
| `JSInterop.Mode = JSRuntimeMode.Loose` | Task 4 |
| `IConfiguration` built inline per test | Tasks 3 |
| All 10 `MainLayoutTests` | Task 3 |
| All 7 `DraftIntentTests` | Task 2 |
| All 6 `DraftWorkspaceTests` | Task 4 |

All 22 spec requirements covered. Total: 23 tests (7 + 10 + 6).

**Placeholder scan:** No TBD/TODO. All test methods contain complete assertion code.

**Type consistency:**
- `IAgentDraftService.SendPromptAsync` signature: `Task<string> SendPromptAsync(string prompt, CancellationToken ct = default)` — matches `AgentDraftService.cs` exactly.
- `IAgentDraftService.ExportDraftToWordAsync` signature: `Task<(string Message, string? FileName, byte[]? FileBytes)>` — matches `AgentDraftService.cs` exactly.
- `DraftWorkspace.IsDraftIntentPrompt(string)` — called in Task 2, made internal in Task 1. Consistent.
- `DraftWorkspace.ExtractDraftMarkdown(string)` — same. Consistent.
