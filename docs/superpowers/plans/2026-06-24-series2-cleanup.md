# Series 2 Repository Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all series 1 carry-over content and move source code from `DotnetAiAgentMcp/` into `DotnetAiAgentUi/`, leaving a clean repo that supports only the series 2 Blazor web UI blog.

**Architecture:** All changes are git operations (mv, rm) plus a build verification. No source code is modified — the `.slnx` already uses relative paths that will resolve correctly after the move.

**Tech Stack:** Git, .NET 10 CLI (`dotnet build`)

## Global Constraints

- All file moves must use `git mv` so history is preserved
- All deletions must use `git rm -r` so git tracks the removal
- Single commit at the end covering all changes
- `dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx` must succeed before committing

---

### Task 1: Move source from `DotnetAiAgentMcp/` to `DotnetAiAgentUi/`

**Files:**
- Move: `DotnetAiAgentMcp/src/` → `DotnetAiAgentUi/src/`
- Move: `DotnetAiAgentMcp/data/` → `DotnetAiAgentUi/data/`
- Move: `DotnetAiAgentMcp/infra/` → `DotnetAiAgentUi/infra/`
- Move: `DotnetAiAgentMcp/tools/` → `DotnetAiAgentUi/tools/`
- Move: `DotnetAiAgentMcp/usajobs/` → `DotnetAiAgentUi/usajobs/`
- Delete: `DotnetAiAgentMcp/DotnetAiAgentMcp.slnx`

Working directory for all commands: `c:/apps/DotnetAiAgentUiTutorial`

- [ ] **Step 1: Move src/**

```bash
git mv DotnetAiAgentMcp/src DotnetAiAgentUi/src
```

- [ ] **Step 2: Move data/**

```bash
git mv DotnetAiAgentMcp/data DotnetAiAgentUi/data
```

- [ ] **Step 3: Move infra/**

```bash
git mv DotnetAiAgentMcp/infra DotnetAiAgentUi/infra
```

- [ ] **Step 4: Move tools/**

```bash
git mv DotnetAiAgentMcp/tools DotnetAiAgentUi/tools
```

- [ ] **Step 5: Move usajobs/**

```bash
git mv DotnetAiAgentMcp/usajobs DotnetAiAgentUi/usajobs
```

- [ ] **Step 6: Delete the old solution file**

```bash
git rm DotnetAiAgentMcp/DotnetAiAgentMcp.slnx
```

- [ ] **Step 7: Verify DotnetAiAgentMcp/ is now empty**

```bash
git status --short | grep DotnetAiAgentMcp
```

Expected: no output (all DotnetAiAgentMcp/ entries are deleted/moved).

- [ ] **Step 8: Verify build**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```

Expected: `Build succeeded.` with 0 errors.

---

### Task 2: Delete published binary artifacts

**Files:**
- Delete: `appsDotnetMcpTutorialpublishMcpServer/` (390 tracked files)

- [ ] **Step 1: Remove the published output folder**

```bash
git rm -r appsDotnetMcpTutorialpublishMcpServer/
```

Expected: ~390 `rm` lines.

- [ ] **Step 2: Verify gone**

```bash
ls appsDotnetMcpTutorialpublishMcpServer/ 2>/dev/null || echo "deleted"
```

Expected: `deleted`

---

### Task 3: Delete series 1 blog content

**Files:**
- Delete: `blogs/series-1-ai-agent-mcp/` (27 tracked files)
- Delete: `blogs/standalone/` (15 tracked files)
- Result: `blogs/` folder becomes empty and is removed

- [ ] **Step 1: Remove series-1-ai-agent-mcp/**

```bash
git rm -r blogs/series-1-ai-agent-mcp/
```

- [ ] **Step 2: Remove standalone/**

```bash
git rm -r blogs/standalone/
```

- [ ] **Step 3: Verify blogs/ is gone**

```bash
ls blogs/ 2>/dev/null || echo "deleted"
```

Expected: `deleted`

---

### Task 4: Delete series 1 design specs and plans

**Files:**
- Delete 11 specs in `docs/superpowers/specs/` dated before June 2026
- Delete 11 plans in `docs/superpowers/plans/` dated before June 2026

- [ ] **Step 1: Remove pre-June specs**

```bash
git rm \
  docs/superpowers/specs/2026-04-29-5-mcp-tools-dotnet-design.md \
  docs/superpowers/specs/2026-05-08-multi-agent-opm-compliance-design.md \
  docs/superpowers/specs/2026-05-21-agent-serilog-design.md \
  docs/superpowers/specs/2026-05-21-agent-spectre-console-design.md \
  docs/superpowers/specs/2026-05-21-blog-update-spectre-console-screenshots-design.md \
  docs/superpowers/specs/2026-05-21-medium-sync-spectre-console-design.md \
  docs/superpowers/specs/2026-05-22-dotnet-ai-workflow-tutorial-design.md \
  docs/superpowers/specs/2026-05-24-blog-update-design.md \
  docs/superpowers/specs/2026-05-24-export-tools-design.md \
  docs/superpowers/specs/2026-05-24-mcp-pure-data-layer-design.md \
  docs/superpowers/specs/2026-05-27-ollama-context-window-design.md
```

- [ ] **Step 2: Remove pre-June plans**

```bash
git rm \
  docs/superpowers/plans/2026-04-29-5-mcp-tools-dotnet.md \
  docs/superpowers/plans/2026-05-08-multi-agent-opm-compliance.md \
  docs/superpowers/plans/2026-05-21-agent-serilog.md \
  docs/superpowers/plans/2026-05-21-agent-spectre-console.md \
  docs/superpowers/plans/2026-05-21-blog-update-spectre-console-screenshots.md \
  docs/superpowers/plans/2026-05-21-medium-sync-spectre-console.md \
  docs/superpowers/plans/2026-05-22-dotnet-ai-workflow-tutorial.md \
  docs/superpowers/plans/2026-05-24-blog-update.md \
  docs/superpowers/plans/2026-05-24-export-tools.md \
  docs/superpowers/plans/2026-05-24-mcp-pure-data-layer.md \
  docs/superpowers/plans/2026-05-27-ollama-context-window.md
```

- [ ] **Step 3: Verify only June 2026 docs remain**

```bash
ls docs/superpowers/specs/ && echo "---" && ls docs/superpowers/plans/
```

Expected: only files prefixed `2026-06-` in both directories.

---

### Task 5: Final verification and commit

- [ ] **Step 1: Confirm build still passes**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 2: Review staged changes**

```bash
git status --short | head -40
```

Confirm:
- No `DotnetAiAgentMcp/` files remain (all `D` or `R` entries resolved)
- `DotnetAiAgentUi/src/`, `data/`, `infra/`, `tools/`, `usajobs/` show as renames (`R`)
- `appsDotnetMcpTutorialpublishMcpServer/` entries show as `D`
- `blogs/` entries show as `D`
- `docs/superpowers/` pre-June entries show as `D`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore: clean up series 1 content, consolidate source under DotnetAiAgentUi/

- Move all source from DotnetAiAgentMcp/ into DotnetAiAgentUi/
- Remove appsDotnetMcpTutorialpublishMcpServer/ binary artifacts
- Remove blogs/series-1-ai-agent-mcp/ and blogs/standalone/
- Remove pre-June 2026 design specs and plans"
```

- [ ] **Step 4: Verify clean working tree**

```bash
git status
```

Expected: `nothing to commit, working tree clean`
