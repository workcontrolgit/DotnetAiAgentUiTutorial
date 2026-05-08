# Design: Multi-Agent OPM Compliance Pipeline

**Date:** 2026-05-08
**Series:** Building Multi-Agent Systems in .NET
**Repo:** `DotnetMultiAgentsTutorial` (forked from `DotnetAiAgentMcp`)
**Status:** Approved

---

## Overview

A multi-agent pipeline that drafts a federal job description using HR position data, then validates it against OPM compliance rules. Built on Microsoft Agent Framework (`ChatClientAgent`), `IChatClient` abstraction, and the existing `HrMcp.McpServer` from Series 1.

The pipeline demonstrates:
- Two cooperating agents with distinct responsibilities
- Two-stage compliance checking (deterministic + LLM)
- Auto-revision loop for minor issues, human escalation for major ones
- `IChatClient`-driven model swapping via `appsettings.json`

---

## Architecture

### New project: `HrMcp.Orchestrator` (console, .NET 10)

Added to the forked solution alongside the existing `HrMcp.Agent`.

**Three components:**

### `HrDraftAgent`
- Wraps `ChatClientAgent` from Microsoft Agent Framework
- Connects to `HrMcp.McpServer` via MCP (HTTP/SSE transport)
- Calls MCP tools: `GetPositionById`, `GetHiringOrganizations`, etc.
- Generates a structured draft job description from position data
- Model configurable via `appsettings.json` (`Agents:DraftAgent:Model`)
- Authenticates to MCP server using OAuth2 client credentials flow (inherits Part 6 setup)

### `OpmComplianceAgent`
Two-stage checker:

**Stage 1 — Structural (deterministic):**
Checks for required OPM fields:
- Pay plan (GS, SES, etc.)
- Grade level
- Occupational series code
- Duty location
- Open and close dates
- Qualification standard text
- Position title

Returns a `MinorIssue` for each missing or malformed field. No LLM involved — pure C# validation.

**Stage 2 — Quality (LLM-based, runs only if Stage 1 passes):**
Sends the draft to a configured LLM with an OPM compliance prompt. Reviews:
- Plain language (OPM plain-language standards)
- KSA (Knowledge, Skills, Abilities) structure
- Qualification language matches OPM standard wording
- No discriminatory language

Returns `MinorIssues` (fixable by revision) or `MajorIssues` (require human review).

Model configurable separately from the Draft Agent (`Agents:ComplianceAgent:Model`).

### `JobDescriptionOrchestrator`
Coordinates the pipeline:

1. Call `HrDraftAgent` with `positionId` → receive draft JD
2. Call `OpmComplianceAgent` with draft → receive `ComplianceResult`
3. Evaluate result:
   - **Compliant** → return final approved draft + report
   - **MinorIssues only** → loop back to `HrDraftAgent` with feedback (max 3 iterations)
   - **MajorIssues** → exit loop immediately, return draft + flagged report for human review
4. If max iterations reached with remaining MinorIssues → promote to MajorIssues, escalate

---

## Data Model

```csharp
enum ComplianceStatus { Compliant, MinorIssues, MajorIssues }

record ComplianceResult(
    ComplianceStatus Status,
    List<string> MinorIssues,
    List<string> MajorIssues,
    int IterationCount);

record OrchestratorResult(
    string DraftJobDescription,
    ComplianceResult Compliance,
    bool RequiresHumanReview);
```

---

## Configuration

`appsettings.json` for `HrMcp.Orchestrator`:

```json
{
  "Agents": {
    "DraftAgent": {
      "Model": "llama3.2",
      "Provider": "Ollama",
      "OllamaEndpoint": "http://localhost:11434"
    },
    "ComplianceAgent": {
      "Model": "llama3.2",
      "Provider": "Ollama",
      "OllamaEndpoint": "http://localhost:11434"
    },
    "Orchestrator": {
      "MaxRevisionIterations": 3
    }
  },
  "McpServer": {
    "BaseUrl": "http://localhost:5000",
    "ClientId": "hr-mcp-agent",
    "ClientSecret": "hr-mcp-agent-secret",
    "TokenEndpoint": "https://localhost:44310/connect/token"
  }
}
```

**Key tutorial payoff:** Swapping the `ComplianceAgent` model from Ollama to Claude API is a single config change — no code changes required. This is the explicit `IChatClient` abstraction payoff demonstrated in the blog.

---

## Error Handling

- **MCP server unreachable / auth failure** — `AgentException` thrown immediately. Fail fast, no retry. Surface root cause to caller.
- **Compliance Stage 2 LLM timeout** — treated as `MajorIssue` ("compliance check unavailable"). Never block on a flaky LLM call. Draft returned for human review.
- **Max iteration guard** — after 3 revision loops, any remaining `MinorIssues` are promoted to `MajorIssues` and escalated. Prevents infinite loops.

---

## Testing Strategy

### Unit tests
- `OpmComplianceAgent` Stage 1 structural checks: pure functions, fully deterministic
- Table-driven tests with known-bad drafts (missing grade, missing duty location, etc.)
- No mocking needed — no external dependencies

### Integration tests
- `JobDescriptionOrchestrator` tested against a mock `IChatClient` returning scripted responses
- Verifies: loop count respected, minor→major promotion at max iterations, compliant exit path

### End-to-end tests
- Orchestrator running against live `HrMcp.McpServer` + Ollama
- Three scenarios: happy path (compliant on first attempt), minor-issue loop (resolves in 2 iterations), major-issue escalation (human review required)

---

## Blog Series Plan

**Series title:** "Building Multi-Agent Systems in .NET"

**Planned parts:**

- **Preface** — Why multi-agent? From single-agent MCP to coordinated pipelines. Series overview and prerequisites (readers should have completed Series 1 or have equivalent MCP knowledge).
- **Part 1** — Microsoft Agent Framework intro. `ChatClientAgent`, `IChatClient`, `AsAIFunction()`. Forking the repo, adding `HrMcp.Orchestrator`.
- **Part 2** — Building `HrDraftAgent`. Connecting to `HrMcp.McpServer` via MCP. Generating structured JD output.
- **Part 3** — Building `OpmComplianceAgent`. Two-stage checking: deterministic structural validation + LLM quality review.
- **Part 4** — `JobDescriptionOrchestrator`. Wiring the pipeline, loop logic, escalation, `ComplianceResult` data model.
- **Part 5** — Config-driven model swapping. Swapping Compliance Agent from Ollama to Claude API with one config change. `IChatClient` payoff.
- **Part 6** — Testing multi-agent systems. Unit, integration, and end-to-end strategies. Determinism challenges with LLM-based agents.

---

## Decisions

- **Option A orchestrator** — new `HrMcp.Orchestrator` console project for clean separation and tutorial clarity
- **Option C compliance behavior** — hybrid: auto-fix minor, flag major for human review
- **Option C configuration** — both agents model-configurable via `appsettings.json`
- **Option A sequential pipeline** — `ChatClientAgent` composition, not autonomous LLM orchestration (more deterministic, better for tutorial)
- **Fork naming:** `DotnetMultiAgentsTutorial`
