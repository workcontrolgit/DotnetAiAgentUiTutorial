# CodeGraph: The Code Knowledge Graph That Cut My AI Tool Calls by 58%

![CodeGraph — Pre-indexed knowledge graph for AI coding agents](screenshots/codegraph_blog_cover_image.png)

I've been building a .NET 10 AI Agent and MCP server series, and somewhere in the middle of watching Claude Code scan through the same files over and over, I started wondering if there was a better way. There was.

CodeGraph is a local code knowledge graph that indexes your project ahead of time and answers structural questions about your code instantly — without the AI agent having to read a single file.

Here's what that actually means in practice, using a real .NET Clean Architecture project as the example.

---

## What Is CodeGraph?

CodeGraph is an open-source CLI, library, and MCP server created by **Colby McHenry**. It uses **tree-sitter AST parsing** — not embeddings — to build a precise semantic knowledge graph of your codebase and store it in a local SQLite file (`.codegraph/codegraph.db`).

When an AI agent needs to find a class, trace a call chain, or understand what breaks if you change a function, it queries the graph instead of scanning files. The result is fewer token-burning round trips and a sharper, more accurate answer.

It went from zero to **47K+ GitHub stars** in under five months. That kind of adoption usually means something real is happening.

---

## Why It Matters

The problem it solves is simple. Every time an AI coding agent tries to understand your code, it:

1. Searches for files
2. Reads those files
3. Searches again for related symbols
4. Reads more files

Each step burns tokens and takes time. On a moderately sized project, a single task can trigger dozens of file reads before the agent even starts writing code.

CodeGraph replaces that pattern with a pre-indexed graph. The agent queries the graph once and gets the answer — no file scanning required.

Official benchmarks across seven real open-source codebases (re-validated June 2, 2026 on Claude Opus 4.8):

| Metric | Improvement |
|---|---|
| Tool calls | **58% fewer** |
| Tokens used | **47% fewer** |
| Wall-clock time | **22% faster** |
| API cost | **16% lower** |

---

## How It Works

CodeGraph parses your source files with tree-sitter, extracts symbols (functions, classes, types, interfaces, routes), maps their relationships (callers, callees, imports, exports), and writes everything into a SQLite graph. From that point forward, any MCP-compatible agent can query the graph through CodeGraph's MCP server instead of scanning the file system.

Auto-sync is on by default. As you or the agent edits files, the graph updates automatically — the index is never stale.

---

## Installation

You need **Node.js 22 or 24** — both ship with native SQLite bindings, so no compiler setup is required.

```bash
npm install -g codegraph
```

That's it. One command to install the CLI globally.

---

## Setup for Claude Code

Run the interactive installer to wire CodeGraph into Claude Code automatically:

```bash
codegraph install
```

This registers the CodeGraph MCP server with Claude Code and writes the MCP server configuration into your settings. After that, Claude Code can query the graph on every task without any extra configuration.

---

## Project Initialization

In your project root:

```bash
codegraph init -i
```

This creates `.codegraph/` and builds the first graph. For the .NET solution in this series — 5 projects, 50 files — indexing completed in seconds:

```
Index Statistics:
  Files:     50
  Nodes:     352
  Edges:     302
  DB Size:   2.22 MB

Nodes by Kind:
  import          160
  method           95
  file             50
  class            29
  constant          7
  enum              5
  function          3
  interface         3

Files by Language:
  csharp           40
  typescript        7
  javascript        3

✓ Index is up to date
```

352 nodes and 302 edges from 50 files. That graph is what the agent queries instead of opening files.

---

## What the Agent Can Now Do

Once CodeGraph is initialized, your AI agent has access to these tools:

| Tool | What It Answers |
|---|---|
| `codegraph_search` | Find a class, function, or type by name |
| `codegraph_callers` | What calls this function? |
| `codegraph_callees` | What does this function call? |
| `codegraph_impact` | What breaks if I change this symbol? |
| `codegraph_context` | What context is relevant for this task? |
| `codegraph_node` | Show me the source + details for this symbol |

---

## Real Examples from This Project

Here's what CodeGraph actually returned when queried against the HR MCP codebase — no file reads, no grep, just graph queries.

### Example 1: Finding a service and its methods

Query: `codegraph query "HiringOrganizationService"`

```
method    GetAllOrganizationsAsync
  DotnetAiAgentUi/src/HrMcp.Application/Services/HiringOrganizationService.cs:9

method    GetOrganizationByIdAsync
  DotnetAiAgentUi/src/HrMcp.Application/Services/HiringOrganizationService.cs:12

class     HiringOrganizationService
  DotnetAiAgentUi/src/HrMcp.Application/Services/HiringOrganizationService.cs:7
```

Exact file, exact line numbers, all methods — instantly. No file scan, no grep pass.

### Example 2: Finding all methods on a service

Query: `codegraph query "PositionService"`

```
method    GetAllPositionsAsync          :9
method    GetOpenPositionsAsync         :12
method    GetPositionByIdAsync          :15
method    GetPositionsByOrganizationAsync :18

class     PositionService               :7
```

Four methods surfaced with line numbers in one query. Without CodeGraph, the agent would read the file, parse it mentally, and probably read the interface file too to confirm the contract.

### Example 3: Understanding the agent's chat pipeline

Query: `codegraph context "understand the agent chat pipeline"`

CodeGraph returned the full call chain of the web agent — `SendPromptAsync` → `EnsureInitializedAsync` → `CreateChatClient` → `CreateClientTransportAsync` — with the actual source for each method inline. The agent saw the entire pipeline without opening a single file:

```csharp
// SendPromptAsync — the entry point
public async Task<string> SendPromptAsync(string prompt, CancellationToken ct = default)
{
    await EnsureInitializedAsync(ct);
    return await _agent!.AskAsync(prompt, ct);
}
```

In a Clean Architecture project — where code flows across **Core → Application → Infrastructure → McpServer → Agent** layers — this kind of instant cross-layer tracing is exactly where the token savings accumulate. Without the graph, tracing that pipeline means reading 5–6 files. With it, one query returns the whole picture.

---

## Where Token Savings Actually Come From

It helps to be concrete. Here's the difference on a typical task — "add a filter parameter to `GetOpenPositionsAsync`":

**Without CodeGraph:**
1. Grep for `GetOpenPositionsAsync` → 1 tool call
2. Read `PositionService.cs` → 1 tool call
3. Read `IPositionRepository.cs` to find the interface → 1 tool call
4. Grep for callers in McpServer → 1 tool call
5. Read the MCP tool file → 1 tool call
6. Possibly read the Blazor page that calls the service → 1 tool call

That's 6 tool calls before writing a single line of code.

**With CodeGraph:**
1. `codegraph_search` for the method → exact location returned
2. `codegraph_impact` on `GetOpenPositionsAsync` → all callers surfaced
3. `codegraph_context` for the task → relevant source inline

3 tool calls total. Fewer tokens, less latency, less chance of the agent drifting into unrelated files.

---

## Keeping the Index Current

After the initial build, CodeGraph auto-syncs. If you ever want to trigger a manual sync:

```bash
codegraph sync
```

To check the current state of the index:

```bash
codegraph status
```

---

## Key Facts at a Glance

- **100% local** — no API keys, no cloud, no account required
- **MCP-native** — works with Claude Code, Cursor, Codex CLI, Windsurf, OpenCode, and any MCP-compatible client
- **AST-based** — precise relationships, not fuzzy embeddings
- **SQLite storage** — the graph lives in `.codegraph/` inside your project
- **Auto-syncing** — the index stays current as you work

---

## Where to Get It

- **GitHub:** [github.com/colbymchenry/codegraph](https://github.com/colbymchenry/codegraph)
- **Website:** [codegraph.codes](https://codegraph.codes/)

---

If you're working on a project where an AI agent is doing a lot of file exploration — especially one with layered architecture like Clean Architecture or a large TypeScript monorepo — CodeGraph is worth the two minutes to set up. The token savings compound on every task, and the graph gives the agent a much more accurate picture of your code than repeated file scans ever could.

For the .NET AI Agent + MCP series this connects to, start here: [AI Agents & MCP with .NET 10 - Preface](https://medium.com/scrum-and-coke/ai-agents-mcp-with-net-10-preface-64314313e3e7).

---

*Tags: AI, Claude Code, CodeGraph, Developer Tools, MCP, .NET, Productivity*
