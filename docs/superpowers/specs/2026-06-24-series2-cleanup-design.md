# Series 2 Repository Cleanup Design

**Date:** 2026-06-24  
**Goal:** Remove all series 1 carry-over content and consolidate the source code under the correct `DotnetAiAgentUi/` folder, leaving only what is needed to support the series 2 Blazor web UI blog.

---

## Context

This repo started as `DotnetAiAgentMcp` (series 1: MCP server + console agent). Series 2 adds a Blazor web UI on top of that backend. The repo was renamed to `DotnetAiAgentUiTutorial` but a prior rename attempt left source code tracked under `DotnetAiAgentMcp/` while an empty `DotnetAiAgentUi/` folder exists with only the renamed `.slnx` and build artifacts.

---

## Changes

### 1. Source Consolidation — `git mv` into `DotnetAiAgentUi/`

Move all tracked source from `DotnetAiAgentMcp/` into `DotnetAiAgentUi/`:

- `DotnetAiAgentMcp/src/` → `DotnetAiAgentUi/src/`
- `DotnetAiAgentMcp/data/` → `DotnetAiAgentUi/data/`
- `DotnetAiAgentMcp/infra/` → `DotnetAiAgentUi/infra/`
- `DotnetAiAgentMcp/tools/` → `DotnetAiAgentUi/tools/`
- `DotnetAiAgentMcp/usajobs/` → `DotnetAiAgentUi/usajobs/`

Delete `DotnetAiAgentMcp/DotnetAiAgentMcp.slnx` (replaced by `DotnetAiAgentUi/DotnetAiAgentUi.slnx`).

The existing `.slnx` already uses relative paths (`src/HrMcp.Agent/HrMcp.Agent.csproj` etc.), so no content edits are needed — the solution will build correctly after the move.

### 2. Delete Published Binary Artifacts

- Delete `appsDotnetMcpTutorialpublishMcpServer/` — a stale published output folder with no source value.

### 3. Delete Series 1 Blog Content

- Delete `blogs/series-1-ai-agent-mcp/` — all 6 parts, preface, diagrams, screenshots.
- Delete `blogs/standalone/` — standalone articles unrelated to series 2.
- Delete `blogs/` — folder becomes empty after the above deletions.

### 4. Delete Series 1 Design Docs

Remove `docs/superpowers/specs/` and `docs/superpowers/plans/` entries from before June 2026:

**Specs to delete** (13 files):
- `2026-04-29-5-mcp-tools-dotnet-design.md`
- `2026-05-08-multi-agent-opm-compliance-design.md`
- `2026-05-21-agent-serilog-design.md`
- `2026-05-21-agent-spectre-console-design.md`
- `2026-05-21-blog-update-spectre-console-screenshots-design.md`
- `2026-05-21-medium-sync-spectre-console-design.md`
- `2026-05-22-dotnet-ai-workflow-tutorial-design.md`
- `2026-05-24-blog-update-design.md`
- `2026-05-24-export-tools-design.md`
- `2026-05-24-mcp-pure-data-layer-design.md`
- `2026-05-27-ollama-context-window-design.md`

**Plans to delete** (11 files):
- `2026-04-29-5-mcp-tools-dotnet.md`
- `2026-05-08-multi-agent-opm-compliance.md`
- `2026-05-21-agent-serilog.md`
- `2026-05-21-agent-spectre-console.md`
- `2026-05-21-blog-update-spectre-console-screenshots.md`
- `2026-05-21-medium-sync-spectre-console.md`
- `2026-05-22-dotnet-ai-workflow-tutorial.md`
- `2026-05-24-blog-update.md`
- `2026-05-24-export-tools.md`
- `2026-05-24-mcp-pure-data-layer.md`
- `2026-05-27-ollama-context-window.md`

**Keep** (series 2 Blazor work):
- `docs/superpowers/specs/2026-06-04-blazor-mvp-agent-replacement-design.md`
- `docs/superpowers/specs/2026-06-19-wysiwyg-draft-editor-design.md`
- `docs/superpowers/plans/2026-06-04-inplace-console-to-blazor-mvp.md`
- `docs/superpowers/plans/2026-06-19-wysiwyg-draft-editor.md`

---

## What Is Not Changed

| Path | Reason kept |
|---|---|
| `DotnetAiAgentUi/DotnetAiAgentUi.slnx` | Series 2 solution file |
| `DotnetAiAgentUi/src/`, `data/`, `infra/`, `tools/`, `usajobs/` | Active source after move |
| `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/` | bunit component tests |
| `README.md` | Project overview |
| `tests/`, `specs/`, `Playwright/` | Playwright E2E test suite |
| `scripts/`, `medium/`, `data/` (root) | Blog tooling |
| `.github/`, `.claude/`, `.mcp.json`, `.gitignore`, `CLAUDE.md` | Tooling and config |
| `docs/superpowers/specs/2026-06-*` | Series 2 design specs |
| `docs/superpowers/plans/2026-06-*` | Series 2 implementation plans |

---

## Success Criteria

- `dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx` succeeds
- No `DotnetAiAgentMcp/` folder remains in the repo
- No series 1 blog content remains
- Git history is clean (single commit for the cleanup)
