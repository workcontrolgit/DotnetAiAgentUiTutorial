# DotnetAiWorkflowTutorial — Implementation Plan

**Date:** 2026-05-22
**Spec:** `docs/superpowers/specs/2026-05-22-dotnet-ai-workflow-tutorial-design.md`
**Repo to create:** `DotnetAiWorkflowTutorial`

---

## Phase 0 — Repo Setup & Architecture

### 0.1 Clone and Initialize
- [ ] Clone `DotnetMcpTutorial` locally as `DotnetAiWorkflowTutorial`
- [ ] Create new GitHub repo `DotnetAiWorkflowTutorial`
- [ ] Update remote origin to new repo
- [ ] Clear git history (fresh start) or keep lineage — decide with user
- [ ] Update `README.md` for new series title

### 0.2 Solution Restructure
- [ ] Rename solution file → `HrAiWorkflow.slnx`
- [ ] Rename projects:
  - `HrMcp.Core` → `HrAiWorkflow.Core`
  - `HrMcp.Application` → `HrAiWorkflow.Application`
  - `HrMcp.Infrastructure.Persistence` → `HrAiWorkflow.Infrastructure`
  - `HrMcp.McpServer` → `HrAiWorkflow.McpServer`
  - Remove `HrMcp.Agent` (replaced by web client)
- [ ] Add new projects:
  - `HrAiWorkflow.Agents` (class library)
  - `HrAiWorkflow.Web` (Blazor Server)
- [ ] Update all namespace references
- [ ] Verify solution builds

### 0.3 Database Setup
- [ ] Add `Microsoft.EntityFrameworkCore.SqlServer` to `HrAiWorkflow.Infrastructure`
- [ ] Add new EF Core entities:
  - `JobDescriptionDraft` + `DraftIteration` + `ApprovalRecord`
  - `ChatSession` + `ChatMessage`
  - Extend `Position` with `JobDescriptionFinal` column
- [ ] Add EF Core enums: `DraftStatus`, `RejectionRouting`, `ApprovalAction`
- [ ] Create `AppDbContext` (merged, single context)
- [ ] Add initial migration
- [ ] Configure LocalDB connection string in `appsettings.Development.json`
- [ ] Run `dotnet ef database update` — verify tables created

### 0.4 Duende IdentityServer Setup
- [ ] Reuse existing Duende STS container config from Series 1
- [ ] Add `role` claim to identity resources in Duende config
- [ ] Add two test users:
  - `manager@hr.local` / password, role = `manager`
  - `specialist@hr.local` / password, role = `hr-specialist`
- [ ] Add `role` claim to client scope list

---

## Phase 1 — Blazor Chat Shell

### 1.1 Blazor Server Project Setup
- [ ] Create `HrAiWorkflow.Web` as Blazor Server app (not WASM)
- [ ] Add project reference to `HrAiWorkflow.Infrastructure` and `HrAiWorkflow.Application`
- [ ] Register `IChatClient` (OllamaApiClient) in DI
- [ ] Register `AppDbContext` with SQL Server connection string

### 1.2 Chat UI Layout
- [ ] Create `MainLayout.razor` — left sidebar + main chat area
- [ ] Create `ChatSidebar.razor` — list of past sessions, "+ New Chat" button
- [ ] Create `ChatArea.razor` — message thread, input box
- [ ] Create `ChatMessage.razor` — user/assistant bubble component
- [ ] Apply CSS: dark sidebar, white chat area, bubble styles

### 1.3 Streaming Chat
- [ ] Inject `IChatClient` into `ChatArea.razor`
- [ ] Implement `await foreach` on `GetStreamingResponseAsync`
- [ ] Call `StateHasChanged()` per chunk — real-time token streaming
- [ ] Maintain `List<ChatMessage> _history` in component state
- [ ] Add "Thinking…" spinner while stream starts

---

## Phase 2 — Chat History

### 2.1 Persistence
- [ ] Create `IChatHistoryService` interface in `HrAiWorkflow.Application`
- [ ] Implement `ChatHistoryService` in `HrAiWorkflow.Infrastructure`
  - `CreateSessionAsync(userId)` → new `ChatSession`
  - `SaveMessageAsync(sessionId, role, content)`
  - `GetSessionsAsync(userId)` → ordered by `CreatedAt desc`
  - `GetMessagesAsync(sessionId)`
- [ ] Register as scoped service

### 2.2 Auto-Title
- [ ] After first user message, fire-and-forget: call `IChatClient` with prompt:
  `"Summarize this message in 5 words or fewer: {firstMessage}"`
- [ ] Update `ChatSession.Title` with result
- [ ] Refresh sidebar

### 2.3 Sidebar Integration
- [ ] `ChatSidebar.razor` loads sessions from `IChatHistoryService`
- [ ] Clicking a session loads its messages into `ChatArea`
- [ ] New Chat button creates a new `ChatSession`

---

## Phase 3 — OIDC Auth

### 3.1 Authentication Setup
- [ ] Add `Microsoft.AspNetCore.Authentication.OpenIdConnect` to `HrAiWorkflow.Web`
- [ ] Configure OIDC in `Program.cs`:
  - Authority: `https://localhost:44310`
  - ClientId: `hr-ai-workflow`
  - Scopes: `openid profile email role hr-mcp-api`
- [ ] Add login/logout to `MainLayout.razor`
- [ ] Display current user name + role in sidebar footer

### 3.2 Role-Based Authorization
- [ ] `[Authorize(Roles = "manager")]` — chat / draft initiation pages
- [ ] `[Authorize(Roles = "hr-specialist")]` — review panel route
- [ ] Pending Approvals count in sidebar: visible only to `hr-specialist`
- [ ] Scope `ChatHistory` queries to `userId` from OIDC `sub` claim

### 3.3 Per-User Session Scoping
- [ ] Inject `IHttpContextAccessor` → extract `sub` claim as `UserId`
- [ ] All `IChatHistoryService` calls pass `UserId`
- [ ] All draft creation stores `CreatedByUserId`

---

## Phase 4 — MCP Integration

### 4.1 MCP Client Registration
- [ ] Add `ModelContextProtocol.Core` to `HrAiWorkflow.Infrastructure`
- [ ] Register `McpClient` pointed at `HrAiWorkflow.McpServer` HTTP endpoint
- [ ] Fetch `mcpTools` on startup, register as `IList<AITool>` in DI

### 4.2 Wire to Chat
- [ ] Inject `IList<AITool>` into `ChatArea.razor`
- [ ] Pass tools to `GetStreamingResponseAsync` via `ChatOptions`
- [ ] Test: Manager can ask "What positions are open?" → MCP tool fires → result in chat

---

## Phase 5 — Draft Workflow

### 5.1 Draft Trigger Detection
- [ ] Detect draft intent in Manager's chat message (keyword match or LLM classification):
  - Keywords: "draft JD", "write job description", "create JD for position"
- [ ] Extract `PositionId` from message (regex or LLM extraction)
- [ ] Show confirmation: "I'll draft a JD for Position #42. Starting now…"

### 5.2 Draft State Machine
- [ ] Implement `DraftWorkflowService` in `HrAiWorkflow.Application`:
  - `CreateDraftAsync(positionId, managerId, prompt)` → creates `JobDescriptionDraft` (Status=Draft)
  - `SubmitForReviewAsync(draftId, iteration)` → Status=UnderReview
  - `ApproveDraftAsync(draftId, specialistId)` → Status=Final + persists JD to Position
  - `RejectDraftAsync(draftId, feedback, routing)` → Status=Rejected + triggers routing
- [ ] Register as scoped service

### 5.3 Draft Status in Chat
- [ ] After draft is submitted for review, show status card in chat:
  `"Draft v1 submitted to HR for review. Status: Under Review"`
- [ ] Status card updates in real-time via SignalR when status changes

---

## Phase 6 — Multi-Agent Pipeline

### 6.1 HrDraftAgent
- [ ] Create `HrDraftAgent` in `HrAiWorkflow.Agents`
- [ ] Constructor: `IChatClient chatClient, IList<AITool> tools`
- [ ] `DraftAsync(positionId, managerPrompt, hrFeedback?)`:
  - System prompt: "You are an expert HR writer. Write a formal job description..."
  - If `hrFeedback` is provided: append "Previous draft was rejected. HR feedback: {hrFeedback}"
  - Call `GetResponseAsync` with MCP tools (to fetch position data)
  - Return JD text string

### 6.2 ComplianceAgent
- [ ] Create `ComplianceAgent` in `HrAiWorkflow.Agents`
- [ ] `ValidateAsync(jdText, positionData)`:
  - System prompt: "You are an OPM compliance expert. Validate this job description..."
  - Return `ComplianceReport` (deserialized from structured LLM JSON output)
  - Sections checked: Duties, Qualifications, Supervisory requirements, Salary band

### 6.3 AgentOrchestrator
- [ ] Create `AgentOrchestrator` in `HrAiWorkflow.Agents`
- [ ] `RunAsync(draftId, positionId, managerPrompt, hrFeedback?)`:
  1. Call `HrDraftAgent.DraftAsync(...)` → jdText
  2. Call `ComplianceAgent.ValidateAsync(...)` → complianceReport
  3. Call `DraftWorkflowService.SubmitForReviewAsync(draftId, new DraftIteration {...})`
  4. Trigger notifications to HR Specialists
- [ ] Register as scoped service

---

## Phase 7 — Human-in-the-Loop Review Panel

### 7.1 Review Panel Component
- [ ] Create `ReviewPanel.razor` (slide-in from right)
- [ ] Sections:
  - Header: Position title, Manager name, iteration count
  - Draft content (scrollable, read-only)
  - Compliance Report (color-coded ✔ ⚠ ✖)
  - Iteration history: clickable version pills [v1] [v2]
  - Action bar: [Export Word] [Reject ▼] [✔ Approve]

### 7.2 Rejection Flow
- [ ] "Reject" button opens dropdown:
  - "Send back to agents (auto re-draft)"
  - "Send back to manager (manual re-prompt)"
- [ ] Feedback text area appears after selection
- [ ] On submit: call `DraftWorkflowService.RejectDraftAsync(draftId, feedback, routing)`
- [ ] If `AgentLoop`: immediately trigger `AgentOrchestrator.RunAsync(...)` with feedback
- [ ] If `BackToManager`: notify Manager with feedback message

### 7.3 Approval Flow
- [ ] "Approve" button: confirmation dialog "Approve and save JD to database?"
- [ ] On confirm: call `DraftWorkflowService.ApproveDraftAsync(...)`
  - Sets `JobDescriptionDraft.Status = Final`
  - Updates `Position.JobDescriptionFinal` with approved JD text
  - Creates `ApprovalRecord`
- [ ] Show success toast: "JD approved and saved to Position #42"
- [ ] Notify Manager: "Your JD draft for Position #42 was approved"

### 7.4 Audit Trail
- [ ] `ApprovalRecord` table stores every approve/reject action
- [ ] HR Specialist can view iteration history (each `DraftIteration` with timestamp)
- [ ] Manager can view full timeline in their chat session

---

## Phase 8 — Notifications & Word Export

### 8.1 SignalR Notification Hub
- [ ] Create `NotificationHub : Hub` in `HrAiWorkflow.Web`
- [ ] Map `/hubs/notifications` in `Program.cs`
- [ ] `NotificationService`:
  - `NotifyUserAsync(userId, message, draftId)` → sends to user's connection(s)
- [ ] Track user connections: `ConcurrentDictionary<userId, HashSet<connectionId>>`
- [ ] Client-side: `HubConnection` in `MainLayout.razor`, show toast on receive

### 8.2 Toast Component
- [ ] Create `ToastContainer.razor` in `MainLayout`
- [ ] Auto-dismiss after 5 seconds
- [ ] Contains: message text + "View Draft →" link button

### 8.3 Email Notifications (MailKit)
- [ ] Add `MailKit` NuGet to `HrAiWorkflow.Infrastructure`
- [ ] Create `IEmailService` interface + `MailKitEmailService` implementation
- [ ] `SendDraftReadyEmailAsync(specialistEmail, positionTitle, managedName, draftId)`
- [ ] Template: HTML email with compliance summary + deep link
- [ ] Dev config: Papercut SMTP (`localhost:25`)
- [ ] Prod config: `SmtpSettings` from `appsettings.Production.json`
- [ ] Logic in `NotificationService`: if user has no active SignalR connection → send email

### 8.4 Word Export
- [ ] Add `DocumentFormat.OpenXml` NuGet to `HrAiWorkflow.Web`
- [ ] Create `WordExportService`:
  - `ExportDraftAsync(draftId)` → returns `byte[]`
  - Document structure:
    - Title: `{positionTitle} — Job Description Draft v{N}`
    - Date + position metadata
    - Body: JD content with Word heading styles
    - Appendix: ComplianceReport (table with status icons)
- [ ] Blazor download trigger: JS interop `saveAsFile(filename, base64)`
- [ ] Button in `ReviewPanel.razor`: "⬇ Export Word"

---

## Checklist Summary

### Phase 0 — Setup
- [ ] New repo created on GitHub
- [ ] Solution renamed and restructured
- [ ] SQL Server + EF Core entities + migration working
- [ ] Duende IdentityServer `role` claim configured

### Phase 1–3 — Web Foundation
- [ ] Streaming Blazor chat working end-to-end
- [ ] Chat history saved to SQL Server, sidebar loads past sessions
- [ ] OIDC login working, roles enforced

### Phase 4–6 — AI Workflow
- [ ] MCP tools available in chat (Position queries working)
- [ ] Draft trigger detected, agents run, draft stored in DB
- [ ] ComplianceReport generated and stored per iteration

### Phase 7–8 — Human-in-the-Loop
- [ ] Review panel shows draft + compliance + history
- [ ] Approve saves JD to Position in DB
- [ ] Reject routes correctly (agents or manager)
- [ ] SignalR toasts working
- [ ] Email fallback working (Papercut dev sink)
- [ ] Word export downloads valid .docx

---

## Blog Article Checklist (per part)

Each part follows the same blog format from Series 1:
- Intro: what we're building today
- Architecture diagram (if structural change)
- Step-by-step code walkthrough
- Code listings (no markdown tables)
- Screenshot of running app
- "What's next" bridge to next part
