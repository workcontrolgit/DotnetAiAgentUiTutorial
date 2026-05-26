# Design: DotnetAiWorkflowTutorial — Full-Stack AI Workflow App

**Date:** 2026-05-22
**Repo:** `DotnetAiWorkflowTutorial` (cloned from `DotnetMcpTutorial`)
**Series:** Series 2 — AI Workflow with Blazor, MCP, and Human-in-the-Loop

---

## Problem

The existing `DotnetMcpTutorial` series (Series 1) demonstrated a console-based AI agent connecting to an MCP server. The next series builds a production-realistic full-stack app: a Blazor Server web client with ChatGPT-like UX, multi-agent JD drafting, and a human-in-the-loop approval workflow where Managers draft and HR Specialists approve before anything persists to the database.

---

## Roles

| Role | Responsibilities |
|---|---|
| **Manager** | Initiates JD draft requests via chat UI, views draft status, refines via re-prompting |
| **HR Specialist** | Reviews AI-generated drafts in the review panel, approves or rejects with routing choice |

---

## Solution Structure

```
HrAiWorkflow/
├── src/
│   ├── HrAiWorkflow.Core/               # Domain entities, interfaces, enums, value objects
│   ├── HrAiWorkflow.Application/        # Use cases, orchestration interfaces, DTOs
│   ├── HrAiWorkflow.Infrastructure/     # EF Core (SQL Server), MailKit, MCP client, Ollama
│   ├── HrAiWorkflow.Agents/             # HrDraftAgent, ComplianceAgent, AgentOrchestrator
│   ├── HrAiWorkflow.McpServer/          # MCP server with PositionTools (reused from Series 1)
│   └── HrAiWorkflow.Web/               # Blazor Server — chat UI, review panel, notifications
└── HrAiWorkflow.slnx
```

---

## Technology Stack

| Concern | Technology |
|---|---|
| Web framework | Blazor Server (.NET 10) |
| Streaming | SignalR (built-in to Blazor Server) |
| Authentication | Microsoft.AspNetCore.Authentication.OpenIdConnect + Duende IdentityServer |
| ORM | EF Core 10 + Microsoft SQL Server (LocalDB for dev) |
| AI / LLM | `IChatClient` + OllamaSharp (`OllamaApiClient`) |
| MCP | `ModelContextProtocol.Core` HTTP client |
| Email | MailKit + Papercut SMTP (dev sink) |
| Word export | `DocumentFormat.OpenXml` |
| UI components | Blazor built-ins + minimal CSS (no heavy component lib) |

---

## Domain Model

### Position (existing, reused)
- Id, Title, Department, GradeLevel, etc.
- Extended with: `JobDescriptionFinal` (the approved, persisted JD text)

### JobDescriptionDraft
```
Id                  Guid
PositionId          int (FK → Position)
Status              DraftStatus enum
CurrentContent      string (latest draft text)
IterationCount      int
CreatedByUserId     string (Manager's OIDC sub)
AssignedSpecialistId string? (HR Specialist's OIDC sub, null = any specialist)
CreatedAt           DateTimeOffset
UpdatedAt           DateTimeOffset
```

### DraftIteration
```
Id                  Guid
DraftId             Guid (FK → JobDescriptionDraft)
IterationNumber     int
HrDraftOutput       string (raw agent output)
ComplianceReport    string (JSON — see ComplianceReport schema below)
HumanFeedback       string? (null = approved, text = rejection reason)
RejectionRouting    RejectionRouting? (AgentLoop | BackToManager)
CreatedAt           DateTimeOffset
```

### ComplianceReport (JSON schema)
```json
{
  "passed": ["Duties section present", "Qualifications complete"],
  "warnings": ["Supervisory note missing"],
  "blockers": ["Salary band not specified"]
}
```

### ChatSession
```
Id        Guid
UserId    string
Title     string (auto-generated from first message)
CreatedAt DateTimeOffset
Messages  List<ChatMessage>
```

### ChatMessage
```
Id          Guid
SessionId   Guid
Role        string (user | assistant | tool)
Content     string
CreatedAt   DateTimeOffset
```

### ApprovalRecord (audit trail)
```
Id          Guid
DraftId     Guid
UserId      string
Action      ApprovalAction (Approved | Rejected)
Reason      string?
Timestamp   DateTimeOffset
```

### DraftStatus Enum
```
Draft         → agents are working / awaiting re-run
UnderReview   → sent to HR Specialist
Approved      → HR Specialist approved, not yet persisted
Final         → persisted to Position.JobDescriptionFinal
Rejected      → returned to agents or manager
```

### RejectionRouting Enum
```
AgentLoop     → system automatically re-runs HrDraftAgent + ComplianceAgent with HR feedback injected
BackToManager → Manager notified with HR feedback; Manager refines prompt and triggers re-run
```

---

## Multi-Agent Pipeline

### HrDraftAgent
- Receives: `PositionId`, `draftPrompt` (Manager's chat message), optional `hrFeedback` (from previous rejection)
- Uses MCP tool `GetPosition(id)` to fetch structured position data
- Generates a full job description in structured prose (not markdown tables)
- Output: plain text JD

### ComplianceAgent
- Receives: the HrDraftAgent JD text + Position data
- Validates against OPM job description standards:
  - Required sections: Duties, Qualifications (required/desired), Supervisory requirements, Salary band
  - Returns `ComplianceReport` JSON
- Output: `ComplianceReport` with `passed`, `warnings`, `blockers` arrays

### AgentOrchestrator
- Coordinates HrDraftAgent → ComplianceAgent in sequence
- Stores result as a new `DraftIteration`
- Updates `JobDescriptionDraft.Status` to `UnderReview`
- Triggers notifications to HR Specialist

---

## Rejection Routing (Option C)

When HR Specialist rejects, they choose:

### Route A: AgentLoop
- HR feedback injected into HrDraftAgent system prompt
- Orchestrator re-runs automatically: HrDraftAgent → ComplianceAgent
- New `DraftIteration` created
- HR Specialist gets new draft notification
- Manager sees status update: "Draft revised by agents (iteration N)"

### Route B: BackToManager
- Manager receives notification: "HR Specialist rejected your draft — feedback: {reason}"
- Draft status set to `Rejected`
- Manager sees rejection feedback in their chat session sidebar
- Manager refines their prompt in chat and triggers a new draft request
- Orchestrator runs again; new `DraftIteration` created

---

## Notification System (Layered)

### Tier 1: SignalR (instant, if online)
- `NotificationHub` pushes to connected user by OIDC `sub`
- Toast component slides in: "Draft ready: Senior SWE #42 — Review now →"
- Badge counter on "Pending Approvals" sidebar item

### Tier 2: Email (MailKit, if offline)
- Check: does the target user have an active Blazor circuit?
- If not → send email via MailKit
- Dev: Papercut SMTP sink (no real SMTP config needed)
- Prod: configurable SMTP settings in `appsettings.Production.json`
- Email contains: position title, requester name, compliance summary snippet, deep link

### Tier 3: Pending Approvals Dashboard (always visible)
- HR Specialist sidebar shows count of `UnderReview` drafts
- Manager sidebar shows their in-progress drafts with status badges

---

## Blazor UI Layout

```
┌──────────────────┬────────────────────────────────────┐
│  Sidebar         │  Chat Area                         │
│  ─────────────   │                                    │
│  Past Chats      │  Manager: Draft JD for Pos #42     │
│  • JD for SWE    │                                    │
│  • JD for PM     │  Agent: ⠋ Drafting...              │
│                  │                                    │
│  Pending (2) ←   │  Agent: Draft ready. Review Panel  │
│  • SWE #42       │  shows on the right →              │
│  • Analyst #18   │                                    │
│                  │  Manager: _                        │
│  [+ New Chat]    │                                    │
└──────────────────┴──────────────┬─────────────────────┘
                                  │ (slide-in panel)
                  ┌───────────────▼─────────────────────┐
                  │  REVIEW PANEL                       │
                  │  Job Description Draft — v2         │
                  │  Position: Senior SWE (#42)         │
                  │  Requested by: J. Smith (Manager)   │
                  │  ──────────────────────────────     │
                  │  [Draft content — scrollable]       │
                  │                                     │
                  │  Compliance Report                  │
                  │  ✔ Duties section present           │
                  │  ✔ Qualifications complete          │
                  │  ⚠ Supervisory note missing         │
                  │  ✖ BLOCKER: salary band             │
                  │                                     │
                  │  Iteration 2 of 3                   │
                  │  [v1] [v2 ←current]                 │
                  │                                     │
                  │  [⬇ Export Word]                    │
                  │                                     │
                  │  [Reject ▼]          [✔ Approve]   │
                  │   └─ Send to agents                 │
                  │   └─ Send back to manager           │
                  └─────────────────────────────────────┘
```

---

## Word Export

- Trigger: "Export Word" button in Review Panel
- Library: `DocumentFormat.OpenXml`
- Output structure:
  - Header: Position title, grade, department, export date
  - Body: formatted JD content (headings for each section)
  - Appendix: ComplianceReport (passed ✔, warnings ⚠, blockers ✖)
- Blazor download: `IJSRuntime.InvokeVoidAsync("saveAsFile", filename, base64bytes)`

---

## OIDC Claims

```
role: "manager"       → initiate drafts, chat, view own drafts
role: "hr-specialist" → pending approvals queue, approve/reject all drafts
```

- Duende IdentityServer: add `role` claim to identity resources
- Blazor: `[Authorize(Roles = "hr-specialist")]` on review panel route
- Draft `AssignedSpecialistId`: null = any HR Specialist can pick it up

---

## Series Outline

| Part | Title | Key Deliverable |
|---|---|---|
| 0 | Repo Setup & Architecture | Clone, restructure, Clean Architecture, EF Core scaffold, SQL Server |
| 1 | Blazor Chat Shell | Streaming chat, ChatGPT layout, IChatClient, SignalR streaming |
| 2 | Chat History | ChatSession/ChatMessage EF Core, sidebar, auto-title via LLM |
| 3 | OIDC Auth | Duende STS, OpenIdConnect, Manager vs HR Specialist roles |
| 4 | MCP Integration | Blazor → HrAiWorkflow.McpServer via HTTP MCP client |
| 5 | Draft Workflow | JobDescriptionDraft state machine, trigger from chat, DB persistence |
| 6 | Multi-Agent Pipeline | AgentOrchestrator, HrDraftAgent, ComplianceAgent, ComplianceReport |
| 7 | Human-in-the-Loop | Review panel, reject routing (AgentLoop / BackToManager), approve, audit trail |
| 8 | Notifications & Word Export | SignalR toasts, MailKit email, Papercut dev sink, .docx export |

---

## Out of Scope

- Mobile client
- Azure deployment (separate series)
- Real-time collaborative editing of drafts
- Serilog (separate plan, not yet executed)
- Part 1–6 Medium articles (handled separately)
