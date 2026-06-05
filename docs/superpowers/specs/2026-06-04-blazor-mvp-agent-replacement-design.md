# Blazor MVP Design: Replace Console Agent UI In-Place

Date: 2026-06-04
Status: Proposed (Validated in brainstorming; in-place upgrade confirmed)
Scope: Phase 1 MVP only

## Context

The tutorial currently demonstrates a working console client in HrMcp.Agent that connects to HrMcp.McpServer and assists with job or position description drafting (JD/PD). The goal is to replace the console experience with a richer web UI using Blazor and MudBlazor while keeping the tutorial simple.

## MVP Goal

Demonstrate AI-assisted drafting for a hiring manager:

- Left panel chat to interact with AI.
- Right panel editable job/position description document.
- AI and user can both contribute document edits.
- Export the final draft to Microsoft Word for offline editing.

## Non-Goals (Deferred to Later Phase)

- HR specialist review and approval workflow.
- Multi-role assignment and role-based approval gates.
- Real-time collaborative editing between multiple users.
- Hangfire-driven review and notification workflows.

## Constraints

- Do not create new projects in the solution for MVP.
- Keep the existing tutorial structure understandable and incremental.
- Preserve the current working console flow during migration, then de-emphasize it.
- Keep code movement to a minimum so existing blog content requires only targeted updates.

## In-Place Upgrade Requirement

This design is an in-place upgrade, not a greenfield rewrite.

- Upgrade existing projects in the current solution only.
- Do not add a new client/UI project for MVP.
- Treat HrMcp.Agent as the migration surface from console to Blazor UI.
- Keep the console path temporarily for tutorial continuity, then phase it down.

## Minimal-Change Tutorial Mode (MVP Rule)

For this tutorial MVP, prioritize a UI swap over architectural refactoring.

- Keep existing orchestration logic in `HrMcp.Agent` for MVP.
- Do not move major workflow code to `HrMcp.Application` in this phase.
- Limit changes to hosting, UI components, and wiring needed for Blazor Server.
- Defer deeper layering refactors to a follow-up tutorial phase.

## Architecture Decision

Use an in-place evolution approach:

- Keep the same solution and project count.
- Evolve HrMcp.Agent from console-first to web-first client.
- Keep orchestration in HrMcp.Agent for MVP to minimize tutorial churn.
- Keep HrMcp.McpServer unchanged as MCP data/tool provider.

Recommendation rationale:

- Avoids tutorial disruption from adding projects.
- Enables a side-by-side migration story (console to web).
- Minimizes blog rewrite effort while still demonstrating the core UI transition.

## In-Place Upgrade Sequence

1. Keep `HrMcp.Agent` console path working as-is.
2. Add Blazor Server + MudBlazor to `HrMcp.Agent` and introduce split-view UI pages.
3. Reuse existing orchestration methods/services from the same project.
4. Make web the default tutorial path; keep console as fallback for one transition phase.
5. Optionally extract orchestration into `HrMcp.Application` in a later phase.

## Component Design

### 1) HrMcp.Agent (existing project, expanded)

Responsibilities in MVP:

- Host Blazor Server + MudBlazor UI.
- Provide split-view workspace:
  - Left: chat with AI.
  - Right: document editor panel.
- Keep temporary console mode as fallback during migration.

Proposed internal folders:

- Web/Components
- Web/Pages
- Web/State
- Web/Services

### 2) HrMcp.Application (existing project, expanded)

Responsibilities in MVP:

- No required MVP changes for the tutorial UI swap.
- Optional future phase: host orchestration contracts and draft workflow use cases.
- Optional future phase: normalize AI edit proposals into deterministic patch operations.

Proposed contracts (examples):

- IJobDraftOrchestrationService
- IAssistantSessionService
- IWordExportService

### 3) HrMcp.Core (existing project)

Responsibilities in MVP:

- Domain-level document models and value objects.
- Draft section definitions (Summary, Duties, Qualifications, How to Apply).

### 4) HrMcp.McpServer (existing project, unchanged)

Responsibilities in MVP:

- Provide MCP tools for data lookup and export operations.
- Continue serving current tool endpoints used by the agent.

## Interaction Model (Split View)

### Left Panel (AI Chat)

- User sends prompts and refinement instructions.
- AI returns text plus optional structured edit proposals.
- Each AI proposal includes target section and operation type (replace/insert/rewrite).

### Right Panel (Document Editor)

- User edits the draft directly.
- AI proposals appear as reviewable changes.
- User chooses Accept, Reject, or Manual Apply.

### Edit Safety Rules

- No silent AI overwrite of user content.
- If user edited a target section after AI proposal generation, mark proposal stale.
- Require explicit user action to apply stale proposals.

## Data Flow

1. User prompt from left panel is submitted to orchestration service.
2. Orchestration service invokes MCP tools and AI model.
3. Service returns assistant output + proposed document patches.
4. UI renders assistant response and patch cards.
5. User applies selected patches to right-panel document.
6. User can export draft to Word through export service.

## Error Handling

- MCP call failures: show recoverable error banner with retry action.
- AI response without valid patch structure: render plain text only.
- Export failure: keep local draft state intact and provide error detail.
- Timeouts: show progress state and cancel/retry controls for long operations.

## Testing Strategy (MVP)

- Unit tests in Application for:
  - Patch generation normalization.
  - Stale proposal detection.
  - Export command orchestration behavior.
- Component tests for split-view interactions:
  - Accept/reject AI proposal.
  - Manual document edits.
  - Export trigger behavior.
- End-to-end smoke test:
  - Prompt -> draft update -> Word export.

## Phased Roadmap

### Phase 1 (this MVP)

- Manager-only drafting demo.
- Split-view chat + editor.
- Word export for offline editing.

### Phase 2 (future)

- HR specialist review/approval workflow.
- Role-aware states and transitions.
- Hangfire background checks/notifications.
- SignalR events for review lifecycle updates.

## Risks and Mitigations

- Risk: Blazor UI growth may mix UI and orchestration concerns.
  - Mitigation: enforce Application interfaces and keep Razor components thin.

- Risk: AI edits can reduce user trust if applied opaquely.
  - Mitigation: explicit patch review and user-controlled apply actions.

- Risk: Migration confusion between console and web paths.
  - Mitigation: clear mode flags, document web as primary path in tutorial updates.

- Risk: Delaying refactor can leave orchestration coupled to UI code.
  - Mitigation: explicitly mark extraction to `HrMcp.Application` as Phase 2 technical debt.

## Implementation Readiness

This design is intentionally scoped to a tutorial-friendly MVP and is ready for implementation planning.
