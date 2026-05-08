# Multi-Agent OPM Compliance Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `HrMcp.Orchestrator` to the forked `DotnetMultiAgentsTutorial` repo — a console app that drafts a federal job description via `HrDraftAgent`, then validates it with `OpmComplianceAgent` (two-stage: deterministic structural + LLM quality), looping on minor issues and escalating major ones for human review.

**Architecture:** Three components inside `HrMcp.Orchestrator`: `HrDraftAgent` (calls MCP server tools, generates draft JD), `OpmComplianceAgent` (Stage 1 deterministic field check → Stage 2 LLM quality review), and `JobDescriptionOrchestrator` (coordinates the loop, max 3 revisions, escalates unresolved issues). Both agents use `IChatClient` (same pattern as Series 1 `HrMcp.Agent`) configured via `appsettings.json`.

**Tech Stack:** .NET 10, `Microsoft.Extensions.AI` 10.*, `OllamaSharp` 5.*, `ModelContextProtocol` 1.*, `Microsoft.Extensions.Hosting` 10.*, `xUnit`, `NSubstitute`

---

## Prerequisites

- Fork `workcontrolgit/DotnetAiAgentMcp` → new repo `DotnetMultiAgentsTutorial`
- `HrMcp.McpServer` running at `http://localhost:5100/mcp` (from Series 1)
- Duende IdentityServer running at `https://localhost:44310` (from Part 6)
- Ollama running locally with `llama3.2` pulled

---

## File Map

```
DotnetAiAgentMcp/src/HrMcp.Orchestrator/
  Models/
    ComplianceStatus.cs         — enum: Compliant | MinorIssues | MajorIssues
    ComplianceResult.cs         — record with Status, MinorIssues, MajorIssues, IterationCount
    OrchestratorResult.cs       — record with DraftJobDescription, Compliance, RequiresHumanReview
  Compliance/
    OpmStructuralValidator.cs   — Stage 1: deterministic field presence checks
  Agents/
    HrDraftAgent.cs             — IChatClient + MCP tools → generates draft JD string
    OpmComplianceAgent.cs       — Stage 1 + Stage 2 (LLM) → ComplianceResult
  Orchestration/
    JobDescriptionOrchestrator.cs — loop logic, escalation, returns OrchestratorResult
  HrMcp.Orchestrator.csproj
  Program.cs                    — DI wiring, config, entry point
  appsettings.json

DotnetAiAgentMcp/tests/HrMcp.Orchestrator.Tests/
  Compliance/
    OpmStructuralValidatorTests.cs
  Orchestration/
    JobDescriptionOrchestratorTests.cs
  HrMcp.Orchestrator.Tests.csproj
```

---

## Task 1: Fork repo and scaffold `HrMcp.Orchestrator` project

**Files:**
- Create: `DotnetAiAgentMcp/src/HrMcp.Orchestrator/HrMcp.Orchestrator.csproj`
- Modify: `DotnetAiAgentMcp/DotnetAiAgentMcp.slnx`

- [ ] **Step 1: Fork the repo on GitHub**

  Fork `workcontrolgit/DotnetAiAgentMcp` to a new repo named `DotnetMultiAgentsTutorial` on GitHub. Clone locally.

- [ ] **Step 2: Create the Orchestrator project**

```bash
cd DotnetAiAgentMcp
dotnet new console -n HrMcp.Orchestrator -o src/HrMcp.Orchestrator --framework net10.0
dotnet sln DotnetAiAgentMcp.slnx add src/HrMcp.Orchestrator/HrMcp.Orchestrator.csproj
```

- [ ] **Step 3: Write `HrMcp.Orchestrator.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.*" />
    <PackageReference Include="ModelContextProtocol" Version="1.*" />
    <PackageReference Include="OllamaSharp" Version="5.*" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Create the test project**

```bash
dotnet new xunit -n HrMcp.Orchestrator.Tests -o tests/HrMcp.Orchestrator.Tests --framework net10.0
dotnet sln DotnetAiAgentMcp.slnx add tests/HrMcp.Orchestrator.Tests/HrMcp.Orchestrator.Tests.csproj
```

- [ ] **Step 5: Write `HrMcp.Orchestrator.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HrMcp.Orchestrator\HrMcp.Orchestrator.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 6: Verify solution builds**

```bash
dotnet build DotnetAiAgentMcp.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/HrMcp.Orchestrator/ tests/HrMcp.Orchestrator.Tests/ DotnetAiAgentMcp.slnx
git commit -m "feat: scaffold HrMcp.Orchestrator project and test project"
```

---

## Task 2: Domain models

**Files:**
- Create: `src/HrMcp.Orchestrator/Models/ComplianceStatus.cs`
- Create: `src/HrMcp.Orchestrator/Models/ComplianceResult.cs`
- Create: `src/HrMcp.Orchestrator/Models/OrchestratorResult.cs`

No logic — pure data. No tests needed.

- [ ] **Step 1: Create `Models/ComplianceStatus.cs`**

```csharp
namespace HrMcp.Orchestrator.Models;

public enum ComplianceStatus
{
    Compliant,
    MinorIssues,
    MajorIssues
}
```

- [ ] **Step 2: Create `Models/ComplianceResult.cs`**

```csharp
namespace HrMcp.Orchestrator.Models;

public sealed record ComplianceResult(
    ComplianceStatus Status,
    IReadOnlyList<string> MinorIssues,
    IReadOnlyList<string> MajorIssues,
    int IterationCount)
{
    public static ComplianceResult Compliant(int iteration) =>
        new(ComplianceStatus.Compliant, [], [], iteration);
}
```

- [ ] **Step 3: Create `Models/OrchestratorResult.cs`**

```csharp
namespace HrMcp.Orchestrator.Models;

public sealed record OrchestratorResult(
    string DraftJobDescription,
    ComplianceResult Compliance,
    bool RequiresHumanReview);
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build src/HrMcp.Orchestrator/HrMcp.Orchestrator.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/HrMcp.Orchestrator/Models/
git commit -m "feat: add ComplianceResult, OrchestratorResult domain models"
```

---

## Task 3: OPM structural validator (Stage 1 — deterministic)

**Files:**
- Create: `src/HrMcp.Orchestrator/Compliance/OpmStructuralValidator.cs`
- Create: `tests/HrMcp.Orchestrator.Tests/Compliance/OpmStructuralValidatorTests.cs`

The draft JD is expected to contain section headers in this format:
```
POSITION TITLE: ...
PAY PLAN: ...
GRADE: ...
SERIES: ...
DUTY LOCATION: ...
OPEN DATE: ...
CLOSE DATE: ...

QUALIFICATIONS:
...
```

The validator checks for the presence of each required header using case-insensitive string matching.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/HrMcp.Orchestrator.Tests/Compliance/OpmStructuralValidatorTests.cs
using HrMcp.Orchestrator.Compliance;
using Xunit;

namespace HrMcp.Orchestrator.Tests.Compliance;

public class OpmStructuralValidatorTests
{
    private static string CompliantDraft => """
        POSITION TITLE: Program Analyst
        PAY PLAN: GS
        GRADE: 12
        SERIES: 0343
        DUTY LOCATION: Washington, DC
        OPEN DATE: 2026-06-01
        CLOSE DATE: 2026-06-15

        QUALIFICATIONS:
        Applicants must have one year of specialized experience equivalent to GS-11.
        """;

    [Fact]
    public void Validate_CompliantDraft_ReturnsNoIssues()
    {
        var issues = OpmStructuralValidator.Validate(CompliantDraft);
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("POSITION TITLE:", "Missing required field: POSITION TITLE")]
    [InlineData("PAY PLAN:", "Missing required field: PAY PLAN")]
    [InlineData("GRADE:", "Missing required field: GRADE")]
    [InlineData("SERIES:", "Missing required field: SERIES")]
    [InlineData("DUTY LOCATION:", "Missing required field: DUTY LOCATION")]
    [InlineData("OPEN DATE:", "Missing required field: OPEN DATE")]
    [InlineData("CLOSE DATE:", "Missing required field: CLOSE DATE")]
    [InlineData("QUALIFICATIONS:", "Missing required field: QUALIFICATIONS")]
    public void Validate_MissingField_ReturnsIssueForThatField(string fieldHeader, string expectedIssue)
    {
        var draft = CompliantDraft.Replace(fieldHeader, "REMOVED:");
        var issues = OpmStructuralValidator.Validate(draft);
        Assert.Contains(expectedIssue, issues);
    }

    [Fact]
    public void Validate_EmptyDraft_ReturnsAllIssues()
    {
        var issues = OpmStructuralValidator.Validate("");
        Assert.Equal(8, issues.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/HrMcp.Orchestrator.Tests/ --filter "OpmStructuralValidatorTests"
```
Expected: Build error — `OpmStructuralValidator` does not exist yet.

- [ ] **Step 3: Implement `OpmStructuralValidator`**

```csharp
// src/HrMcp.Orchestrator/Compliance/OpmStructuralValidator.cs
namespace HrMcp.Orchestrator.Compliance;

public static class OpmStructuralValidator
{
    private static readonly string[] RequiredHeaders =
    [
        "POSITION TITLE:",
        "PAY PLAN:",
        "GRADE:",
        "SERIES:",
        "DUTY LOCATION:",
        "OPEN DATE:",
        "CLOSE DATE:",
        "QUALIFICATIONS:"
    ];

    public static IReadOnlyList<string> Validate(string draft)
    {
        var issues = new List<string>();
        foreach (var header in RequiredHeaders)
        {
            if (!draft.Contains(header, StringComparison.OrdinalIgnoreCase))
                issues.Add($"Missing required field: {header.TrimEnd(':')}");
        }
        return issues;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/HrMcp.Orchestrator.Tests/ --filter "OpmStructuralValidatorTests"
```
Expected: All 10 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/HrMcp.Orchestrator/Compliance/ tests/HrMcp.Orchestrator.Tests/Compliance/
git commit -m "feat: add OpmStructuralValidator with deterministic field checks"
```

---

## Task 4: `HrDraftAgent` — calls MCP server, generates draft JD

**Files:**
- Create: `src/HrMcp.Orchestrator/Agents/HrDraftAgent.cs`

No unit tests — depends on live `IChatClient` and MCP. Tested end-to-end in Task 8.

- [ ] **Step 1: Create `Agents/HrDraftAgent.cs`**

```csharp
// src/HrMcp.Orchestrator/Agents/HrDraftAgent.cs
using Microsoft.Extensions.AI;

namespace HrMcp.Orchestrator.Agents;

public sealed class HrDraftAgent(IChatClient chatClient, IList<AITool> mcpTools)
{
    private const string SystemPrompt = """
        You are a federal HR specialist. When asked to draft a job description,
        use the MCP tools to fetch position data, then produce a structured document
        using EXACTLY these section headers on separate lines:

        POSITION TITLE: [title]
        PAY PLAN: [GS/SES/WG/etc]
        GRADE: [grade level, e.g. 12]
        SERIES: [4-digit OPM series code]
        DUTY LOCATION: [city, state]
        OPEN DATE: [YYYY-MM-DD]
        CLOSE DATE: [YYYY-MM-DD]

        QUALIFICATIONS:
        [OPM qualification standard text]

        DUTIES:
        [numbered list of major duties]

        Do not omit any header. Do not add extra headers.
        """;

    public async Task<string> DraftAsync(
        int positionId,
        string? revisionFeedback = null,
        CancellationToken ct = default)
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt)
        };

        var prompt = revisionFeedback is null
            ? $"Draft a job description for position ID {positionId}."
            : $"""
               Revise the job description for position ID {positionId}.
               Address these compliance issues:
               {revisionFeedback}
               Produce the full corrected document using the required section headers.
               """;

        history.Add(new ChatMessage(ChatRole.User, prompt));

        var response = await chatClient.GetResponseAsync(
            history,
            new ChatOptions { Tools = mcpTools },
            ct);

        return response.Text ?? string.Empty;
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build src/HrMcp.Orchestrator/HrMcp.Orchestrator.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/HrMcp.Orchestrator/Agents/HrDraftAgent.cs
git commit -m "feat: add HrDraftAgent for MCP-backed job description drafting"
```

---

## Task 5: `OpmComplianceAgent` — Stage 1 + Stage 2

**Files:**
- Create: `src/HrMcp.Orchestrator/Agents/OpmComplianceAgent.cs`

- [ ] **Step 1: Create `Agents/OpmComplianceAgent.cs`**

```csharp
// src/HrMcp.Orchestrator/Agents/OpmComplianceAgent.cs
using HrMcp.Orchestrator.Compliance;
using HrMcp.Orchestrator.Models;
using Microsoft.Extensions.AI;

namespace HrMcp.Orchestrator.Agents;

public sealed class OpmComplianceAgent(IChatClient chatClient)
{
    private const string CompliancePrompt = """
        You are an OPM compliance reviewer. Evaluate the following federal job description
        against OPM standards and return your findings in this exact format:

        MINOR ISSUES:
        - [issue 1, or "none" if no minor issues]

        MAJOR ISSUES:
        - [issue 1, or "none" if no major issues]

        Minor issues are things that can be fixed with a simple revision:
        - Plain language violations (jargon, passive voice, overly complex sentences)
        - KSA items that are not measurable or specific
        - Qualification language that doesn't match OPM standard wording

        Major issues require human review:
        - Qualification requirements that may unlawfully exclude protected classes
        - Position classification disputes (grade/series mismatch for stated duties)
        - Missing legal notices required by OPM

        Be specific. Quote the problematic text where possible.
        """;

    public async Task<ComplianceResult> CheckAsync(
        string draft,
        int iterationCount,
        CancellationToken ct = default)
    {
        // Stage 1: deterministic structural check
        var structuralIssues = OpmStructuralValidator.Validate(draft);
        if (structuralIssues.Count > 0)
        {
            return new ComplianceResult(
                ComplianceStatus.MinorIssues,
                structuralIssues,
                [],
                iterationCount);
        }

        // Stage 2: LLM quality review (only runs if Stage 1 passes)
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, CompliancePrompt),
            new(ChatRole.User, $"Review this job description:\n\n{draft}")
        };

        ChatResponse response;
        try
        {
            response = await chatClient.GetResponseAsync(history, cancellationToken: ct);
        }
        catch (Exception)
        {
            // LLM unavailable — escalate for human review rather than blocking
            return new ComplianceResult(
                ComplianceStatus.MajorIssues,
                [],
                ["Compliance quality check unavailable — manual review required."],
                iterationCount);
        }

        return ParseLlmResponse(response.Text ?? string.Empty, iterationCount);
    }

    private static ComplianceResult ParseLlmResponse(string response, int iterationCount)
    {
        var minorIssues = ExtractIssues(response, "MINOR ISSUES:");
        var majorIssues = ExtractIssues(response, "MAJOR ISSUES:");

        var status = majorIssues.Count > 0 ? ComplianceStatus.MajorIssues
                   : minorIssues.Count > 0 ? ComplianceStatus.MinorIssues
                   : ComplianceStatus.Compliant;

        return new ComplianceResult(status, minorIssues, majorIssues, iterationCount);
    }

    private static List<string> ExtractIssues(string response, string sectionHeader)
    {
        var issues = new List<string>();
        var start = response.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return issues;

        var sectionText = response[(start + sectionHeader.Length)..];
        var nextSection = sectionText.IndexOf("\n\n", StringComparison.Ordinal);
        if (nextSection > 0) sectionText = sectionText[..nextSection];

        foreach (var line in sectionText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimStart('-', ' ', '\t');
            if (!string.IsNullOrWhiteSpace(trimmed) &&
                !trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(trimmed);
            }
        }

        return issues;
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build src/HrMcp.Orchestrator/HrMcp.Orchestrator.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/HrMcp.Orchestrator/Agents/OpmComplianceAgent.cs
git commit -m "feat: add OpmComplianceAgent with two-stage structural and LLM compliance checks"
```

---

## Task 6: `JobDescriptionOrchestrator` — loop, escalation, result

**Files:**
- Create: `src/HrMcp.Orchestrator/Orchestration/JobDescriptionOrchestrator.cs`
- Create: `tests/HrMcp.Orchestrator.Tests/Orchestration/JobDescriptionOrchestratorTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/HrMcp.Orchestrator.Tests/Orchestration/JobDescriptionOrchestratorTests.cs
using HrMcp.Orchestrator.Agents;
using HrMcp.Orchestrator.Models;
using HrMcp.Orchestrator.Orchestration;
using NSubstitute;
using Xunit;

namespace HrMcp.Orchestrator.Tests.Orchestration;

public class JobDescriptionOrchestratorTests
{
    private static OrchestratorOptions DefaultOptions => new() { MaxRevisionIterations = 3 };

    [Fact]
    public async Task RunAsync_CompliantOnFirstAttempt_ReturnsDraftWithNoHumanReview()
    {
        var draftAgent = Substitute.For<IHrDraftAgent>();
        var complianceAgent = Substitute.For<IOpmComplianceAgent>();

        draftAgent.DraftAsync(1, null, default).Returns("draft v1");
        complianceAgent.CheckAsync("draft v1", 1, default)
            .Returns(ComplianceResult.Compliant(1));

        var orchestrator = new JobDescriptionOrchestrator(draftAgent, complianceAgent, DefaultOptions);
        var result = await orchestrator.RunAsync(positionId: 1);

        Assert.Equal("draft v1", result.DraftJobDescription);
        Assert.Equal(ComplianceStatus.Compliant, result.Compliance.Status);
        Assert.False(result.RequiresHumanReview);
        Assert.Equal(1, result.Compliance.IterationCount);
    }

    [Fact]
    public async Task RunAsync_MinorIssuesOnFirstAttempt_LoopsAndResolvesOnSecond()
    {
        var draftAgent = Substitute.For<IHrDraftAgent>();
        var complianceAgent = Substitute.For<IOpmComplianceAgent>();

        draftAgent.DraftAsync(1, null, default).Returns("draft v1");
        draftAgent.DraftAsync(1, Arg.Any<string>(), default).Returns("draft v2");

        complianceAgent.CheckAsync("draft v1", 1, default)
            .Returns(new ComplianceResult(ComplianceStatus.MinorIssues, ["Missing SERIES"], [], 1));
        complianceAgent.CheckAsync("draft v2", 2, default)
            .Returns(ComplianceResult.Compliant(2));

        var orchestrator = new JobDescriptionOrchestrator(draftAgent, complianceAgent, DefaultOptions);
        var result = await orchestrator.RunAsync(positionId: 1);

        Assert.Equal("draft v2", result.DraftJobDescription);
        Assert.False(result.RequiresHumanReview);
        Assert.Equal(2, result.Compliance.IterationCount);
    }

    [Fact]
    public async Task RunAsync_MajorIssues_ExitsImmediatelyWithHumanReviewFlag()
    {
        var draftAgent = Substitute.For<IHrDraftAgent>();
        var complianceAgent = Substitute.For<IOpmComplianceAgent>();

        draftAgent.DraftAsync(1, null, default).Returns("draft v1");
        complianceAgent.CheckAsync("draft v1", 1, default)
            .Returns(new ComplianceResult(ComplianceStatus.MajorIssues, [], ["Classification dispute"], 1));

        var orchestrator = new JobDescriptionOrchestrator(draftAgent, complianceAgent, DefaultOptions);
        var result = await orchestrator.RunAsync(positionId: 1);

        Assert.True(result.RequiresHumanReview);
        Assert.Equal(ComplianceStatus.MajorIssues, result.Compliance.Status);
        // Must NOT call draft agent a second time
        await draftAgent.Received(1).DraftAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_MinorIssuesPersistAfterMaxIterations_EscalatesToMajor()
    {
        var draftAgent = Substitute.For<IHrDraftAgent>();
        var complianceAgent = Substitute.For<IOpmComplianceAgent>();

        draftAgent.DraftAsync(Arg.Any<int>(), Arg.Any<string?>(), default).Returns("draft");
        complianceAgent.CheckAsync("draft", Arg.Any<int>(), default)
            .Returns(new ComplianceResult(ComplianceStatus.MinorIssues, ["Persistent issue"], [], 0));

        var orchestrator = new JobDescriptionOrchestrator(draftAgent, complianceAgent, DefaultOptions);
        var result = await orchestrator.RunAsync(positionId: 1);

        Assert.True(result.RequiresHumanReview);
        Assert.Equal(ComplianceStatus.MajorIssues, result.Compliance.Status);
        Assert.Contains("Persistent issue", result.Compliance.MajorIssues);
        Assert.Equal(3, result.Compliance.IterationCount);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/HrMcp.Orchestrator.Tests/ --filter "JobDescriptionOrchestratorTests"
```
Expected: Build error — interfaces and orchestrator class not defined yet.

- [ ] **Step 3: Define agent interfaces**

Add to `src/HrMcp.Orchestrator/Agents/HrDraftAgent.cs` (above the class):

```csharp
public interface IHrDraftAgent
{
    Task<string> DraftAsync(int positionId, string? revisionFeedback = null, CancellationToken ct = default);
}
```

Add `: IHrDraftAgent` to `HrDraftAgent` class declaration:

```csharp
public sealed class HrDraftAgent(IChatClient chatClient, IList<AITool> mcpTools) : IHrDraftAgent
```

Add to `src/HrMcp.Orchestrator/Agents/OpmComplianceAgent.cs` (above the class):

```csharp
public interface IOpmComplianceAgent
{
    Task<ComplianceResult> CheckAsync(string draft, int iterationCount, CancellationToken ct = default);
}
```

Add `: IOpmComplianceAgent` to `OpmComplianceAgent` class declaration:

```csharp
public sealed class OpmComplianceAgent(IChatClient chatClient) : IOpmComplianceAgent
```

- [ ] **Step 4: Create `Orchestration/JobDescriptionOrchestrator.cs`**

```csharp
// src/HrMcp.Orchestrator/Orchestration/JobDescriptionOrchestrator.cs
using HrMcp.Orchestrator.Agents;
using HrMcp.Orchestrator.Models;

namespace HrMcp.Orchestrator.Orchestration;

public sealed class OrchestratorOptions
{
    public int MaxRevisionIterations { get; init; } = 3;
}

public sealed class JobDescriptionOrchestrator(
    IHrDraftAgent draftAgent,
    IOpmComplianceAgent complianceAgent,
    OrchestratorOptions options)
{
    public async Task<OrchestratorResult> RunAsync(
        int positionId,
        CancellationToken ct = default)
    {
        string? revisionFeedback = null;
        string draft = string.Empty;
        ComplianceResult compliance = default!;

        for (int iteration = 1; iteration <= options.MaxRevisionIterations; iteration++)
        {
            draft = await draftAgent.DraftAsync(positionId, revisionFeedback, ct);
            compliance = await complianceAgent.CheckAsync(draft, iteration, ct);

            if (compliance.Status == ComplianceStatus.Compliant)
                return new OrchestratorResult(draft, compliance, RequiresHumanReview: false);

            if (compliance.Status == ComplianceStatus.MajorIssues)
                return new OrchestratorResult(draft, compliance, RequiresHumanReview: true);

            // MinorIssues — feed back to draft agent on next iteration
            revisionFeedback = string.Join("\n", compliance.MinorIssues.Select(i => $"- {i}"));
        }

        // Max iterations reached with unresolved minor issues — escalate
        var escalated = new ComplianceResult(
            ComplianceStatus.MajorIssues,
            [],
            compliance.MinorIssues,
            compliance.IterationCount);

        return new OrchestratorResult(draft, escalated, RequiresHumanReview: true);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/HrMcp.Orchestrator.Tests/ --filter "JobDescriptionOrchestratorTests"
```
Expected: All 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/HrMcp.Orchestrator/Agents/ src/HrMcp.Orchestrator/Orchestration/ tests/HrMcp.Orchestrator.Tests/Orchestration/
git commit -m "feat: add JobDescriptionOrchestrator with loop, escalation, and max-iteration guard"
```

---

## Task 7: Configuration and `Program.cs` wiring

**Files:**
- Create: `src/HrMcp.Orchestrator/appsettings.json`
- Create: `src/HrMcp.Orchestrator/Program.cs`

- [ ] **Step 1: Create `appsettings.json`**

```json
{
  "Agents": {
    "DraftAgent": {
      "Model": "llama3.2",
      "OllamaEndpoint": "http://localhost:11434"
    },
    "ComplianceAgent": {
      "Model": "llama3.2",
      "OllamaEndpoint": "http://localhost:11434"
    },
    "Orchestrator": {
      "MaxRevisionIterations": 3
    }
  },
  "McpServer": {
    "BaseUrl": "http://localhost:5100/mcp",
    "ClientId": "hr-mcp-agent",
    "ClientSecret": "hr-mcp-agent-secret",
    "TokenEndpoint": "https://localhost:44310/connect/token",
    "Scope": "hr-mcp-api"
  }
}
```

- [ ] **Step 2: Ensure `appsettings.json` is copied on build**

In `HrMcp.Orchestrator.csproj`, add inside `<Project>`:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 3: Write `Program.cs`**

```csharp
// src/HrMcp.Orchestrator/Program.cs
using HrMcp.Orchestrator.Agents;
using HrMcp.Orchestrator.Orchestration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;
using System.Net.Http.Json;
using System.Text.Json;

// --- Load config ---
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var draftModel      = config["Agents:DraftAgent:Model"]!;
var draftEndpoint   = config["Agents:DraftAgent:OllamaEndpoint"]!;
var compModel       = config["Agents:ComplianceAgent:Model"]!;
var compEndpoint    = config["Agents:ComplianceAgent:OllamaEndpoint"]!;
var maxIterations   = int.Parse(config["Agents:Orchestrator:MaxRevisionIterations"]!);

var mcpServerUrl    = config["McpServer:BaseUrl"]!;
var clientId        = config["McpServer:ClientId"]!;
var clientSecret    = config["McpServer:ClientSecret"]!;
var tokenEndpoint   = config["McpServer:TokenEndpoint"]!;
var scope           = config["McpServer:Scope"]!;

// --- Acquire token (client credentials) ---
using var tokenHandler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
using var tokenClient = new HttpClient(tokenHandler);

var tokenResponse = await tokenClient.PostAsync(
    tokenEndpoint,
    new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"]    = "client_credentials",
        ["client_id"]     = clientId,
        ["client_secret"] = clientSecret,
        ["scope"]         = scope,
    }));
tokenResponse.EnsureSuccessStatusCode();

var tokenDoc    = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
var accessToken = tokenDoc.GetProperty("access_token").GetString()!;
Console.WriteLine("Token acquired.\n");

// --- Connect to MCP server ---
await using var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(mcpServerUrl),
        AdditionalHeaders = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {accessToken}"
        }
    }));

var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"MCP tools: {string.Join(", ", mcpTools.Select(t => t.Name))}\n");

// --- Build IChatClient for Draft Agent ---
IChatClient draftChatClient = ((IChatClient)new OllamaApiClient(
        new Uri(draftEndpoint), draftModel))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

// --- Build IChatClient for Compliance Agent (separate model, no MCP tools) ---
IChatClient complianceChatClient = (IChatClient)new OllamaApiClient(
    new Uri(compEndpoint), compModel);

// --- Wire up agents and orchestrator ---
var draftAgent      = new HrDraftAgent(draftChatClient, mcpTools.Cast<AITool>().ToList());
var complianceAgent = new OpmComplianceAgent(complianceChatClient);
var orchestrator    = new JobDescriptionOrchestrator(
    draftAgent, complianceAgent, new OrchestratorOptions { MaxRevisionIterations = maxIterations });

// --- Run ---
Console.Write("Enter position ID to draft: ");
if (!int.TryParse(Console.ReadLine(), out var positionId))
{
    Console.WriteLine("Invalid position ID.");
    return;
}

Console.WriteLine($"\nOrchestrating job description for position {positionId}...\n");
var result = await orchestrator.RunAsync(positionId);

Console.WriteLine("=== DRAFT JOB DESCRIPTION ===");
Console.WriteLine(result.DraftJobDescription);
Console.WriteLine();
Console.WriteLine($"=== COMPLIANCE STATUS: {result.Compliance.Status} ===");
Console.WriteLine($"Iterations: {result.Compliance.IterationCount}");

if (result.Compliance.MinorIssues.Count > 0)
{
    Console.WriteLine("Minor issues:");
    foreach (var issue in result.Compliance.MinorIssues)
        Console.WriteLine($"  - {issue}");
}

if (result.Compliance.MajorIssues.Count > 0)
{
    Console.WriteLine("Major issues (HUMAN REVIEW REQUIRED):");
    foreach (var issue in result.Compliance.MajorIssues)
        Console.WriteLine($"  - {issue}");
}

if (result.RequiresHumanReview)
    Console.WriteLine("\n⚠ This draft requires human review before publishing.");
else
    Console.WriteLine("\n✓ Draft is compliant and ready for publishing.");
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build src/HrMcp.Orchestrator/HrMcp.Orchestrator.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/HrMcp.Orchestrator/Program.cs src/HrMcp.Orchestrator/appsettings.json src/HrMcp.Orchestrator/HrMcp.Orchestrator.csproj
git commit -m "feat: add Program.cs with config-driven DI wiring for both agents"
```

---

## Task 8: Run all tests and end-to-end smoke test

**Files:** None new — verification only.

- [ ] **Step 1: Run full test suite**

```bash
dotnet test DotnetAiAgentMcp.slnx
```
Expected: All tests pass.

- [ ] **Step 2: Smoke test — happy path**

Ensure `HrMcp.McpServer`, IdentityServer, and Ollama are all running. Then:

```bash
dotnet run --project src/HrMcp.Orchestrator
```

Enter `1` when prompted. Expected output:
- "Token acquired."
- MCP tools listed
- Draft JD with all required section headers
- Compliance status: `Compliant` or `MinorIssues` with revision loop visible

- [ ] **Step 3: Smoke test — model swap (IChatClient payoff)**

In `appsettings.json`, change `Agents:ComplianceAgent:Model` to a different Ollama model (e.g., `mistral`). Re-run. Verify the Compliance Agent uses the new model with zero code changes.

- [ ] **Step 4: Final commit**

```bash
git add .
git commit -m "feat: complete multi-agent OPM compliance pipeline"
```

---

## Self-Review

**Spec coverage check:**
- ✅ `HrMcp.Orchestrator` new console project — Task 1
- ✅ `HrDraftAgent` — Task 4
- ✅ `OpmComplianceAgent` two-stage — Task 5 (Stage 1 in Task 3, Stage 2 in Task 5)
- ✅ `JobDescriptionOrchestrator` loop + escalation — Task 6
- ✅ `ComplianceResult` / `OrchestratorResult` records — Task 2
- ✅ Max iteration guard → promote MinorIssues to MajorIssues — Task 6 tests case 4
- ✅ LLM timeout → MajorIssue escalation — Task 5 `OpmComplianceAgent.CheckAsync` catch block
- ✅ Config-driven model per agent — Task 7
- ✅ Token acquisition (client credentials) — Task 7 `Program.cs`
- ✅ Unit tests (structural validator) — Task 3
- ✅ Integration tests (orchestrator with mocks) — Task 6
- ✅ End-to-end smoke test — Task 8

**Placeholder scan:** No TBDs or TODOs found.

**Type consistency:**
- `IHrDraftAgent.DraftAsync(int, string?, CancellationToken)` — defined Task 6 Step 3, used in Task 6 tests ✅
- `IOpmComplianceAgent.CheckAsync(string, int, CancellationToken)` — defined Task 6 Step 3, used in Task 6 tests ✅
- `ComplianceResult.Compliant(int)` static factory — defined Task 2, used in Task 6 tests ✅
- `OrchestratorOptions.MaxRevisionIterations` — defined Task 6, read from config in Task 7 ✅
