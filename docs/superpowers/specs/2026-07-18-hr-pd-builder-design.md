# HR Position Description Builder — Design Spec

**Date:** 2026-07-18
**Status:** Approved
**Author:** HR Business Analyst (brainstorming session with AI)

---

## Architecture Decision Record (ADR)

The following questions and answers were captured during the brainstorming session. They document the *why* behind every design decision in this spec.

| # | Question | Options | Selected | Rationale |
|---|---|---|---|---|
| Q1 | Is this a pure analysis document, a new feature design, or both? | A) Analysis only / B) New feature / C) Both | **C — Both** | The analysis becomes the design spec that drives the new collaboration feature |
| Q2 | Which compliance framework should the AI enforce? | A) USAJOBS standard / B) OPM Classifier / C) Agency-specific template / D) Flexible/guided | **C — Agency-specific template** | Agency has required sections beyond OPM baseline (security clearance, remote work, EEO); compliance must match internal HR review criteria |
| Q3 | How should the hiring manager provide input to the AI? | A) Freeform chat / B) Structured intake wizard / C) Hybrid (paste notes) / D) All three modes | **D — All three modes** | Managers have different working styles; forcing one mode creates friction; AI auto-detects intent from the first message |
| Q4 | Which pain point is the biggest bottleneck today? | A) Weak drafts / B) Back-and-forth delays / C) Knowledge gap / D) All compound each other | **D — All compound** | Weak drafts stem from knowledge gaps and cause back-and-forth; resolving any one in isolation leaves the others |
| Q5 | Which approach best addresses all pain points? | A) Progressive Disclosure / B) Structured Intake Form First / C) Pure Conversational Coach | **A — Progressive Disclosure** | Only approach that supports all three input modes while enforcing compliance structurally via a visible checklist |

---

## Problem Statement

### Context

Federal government hiring managers must produce Position Descriptions (PDs) that comply with agency-specific HR templates before a position can be posted on USAJOBS. Today, two roles collaborate on every PD:

- **Hiring Manager** — deep technical knowledge of the role; limited federal HR writing skills; unfamiliar with OPM series codes, GS grade-level language conventions, and required template sections
- **HR Specialist** — expert in HR process, compliance, and agency templates; limited technical knowledge of what the role actually does

Neither role alone can produce a complete, compliant PD efficiently.

### Pain Points (All Compound)

| # | Pain Point | Root Cause |
|---|---|---|
| P1 | Weak draft language — technical but non-compliant | Manager doesn't know federal HR writing conventions |
| P2 | Wrong grade-level language — GS-12 duties written at GS-14 scope | Manager unfamiliar with OPM grading descriptors |
| P3 | Missing required sections — EEO, clearance, remote eligibility absent | Manager doesn't know the full agency template |
| P4 | Wrong series code requested | Manager unfamiliar with OPM occupational series |
| P5 | Weeks of back-and-forth between manager and HR | P1–P4 compound into repeated review cycles |
| P6 | Institutional knowledge lost when manager retires | No template memory across positions |

---

## Solution Overview

The **Position Description Builder** is a single-user AI-assisted application. The hiring manager is the only human user. The LLM functions simultaneously as:

1. **HR Specialist** — knows the agency template, OPM series standards, GS grade-level language, and compliance rules
2. **Writer** — translates the manager's plain-language technical knowledge into compliant federal HR prose

The manager provides technical input in any form. The AI handles everything else.

---

## Section 1: User Journey Map

The hiring manager's journey has five stages.

### Stage 1 — Entry (Any Input Mode)

Manager opens the app and starts in whichever mode feels natural:

| Input Mode | Example | AI Response |
|---|---|---|
| Freeform description | "I need a GS-13 cloud architect to lead our AWS migration" | Extracts intent, drafts immediately, flags gaps |
| Pasted notes / old PD | Pastes a Word document or prior PD | Cleans language, applies template, flags non-compliant sections |
| No context yet | Types anything unclear or incomplete | Responds with first intake question: *"What position are you filling and what grade level are you targeting?"* |

The AI does not wait for complete information. It drafts with what it has.

### Stage 2 — Draft Appears + Checklist Activates

The right-panel draft editor populates with a structured PD draft. The **Section Completion Checklist** appears above the editor, showing all agency template sections with their current status (✅ / ⚠️ / ❌ / 🔒).

### Stage 3 — Conversational Gap-Filling

The AI's chat message after generating the draft explains missing sections in plain language and asks targeted follow-up questions — one topic at a time. Manager answers conversationally. AI patches the draft and updates the checklist in real time.

Example AI message:
> *"I've drafted your Position Summary and Major Duties based on what you described. I still need a few things: What's the required security clearance level? Is this position eligible for remote work?"*

### Stage 4 — Compliance Validation

Once all checklist items are green or acknowledged, the AI runs a final compliance pass:

1. Do major duties start with action verbs calibrated to the GS grade level?
2. Does the series code align with the majority of described duties?
3. Are qualifications split into Required vs. Preferred?
4. Are agency boilerplate sections (EEO, Reasonable Accommodation) present verbatim?
5. Is the Position Summary 3–5 sentences, jargon-free, and mission-contextual?

Any failure re-opens that checklist item and triggers a targeted chat message.

### Stage 5 — Export

Manager exports the completed PD to Word (.docx) for submission to HR, or downloads JSON/Markdown for records. The Word export preserves agency template structure and section headings.

---

## Section 2: Pain Points & AI Resolution

| # | Pain Point | Root Cause | How AI Resolves It |
|---|---|---|---|
| P1 | Weak draft language | Manager lacks federal HR writing conventions | AI translates plain technical language into OPM/agency-compliant phrasing |
| P2 | Wrong grade-level language | Manager unfamiliar with OPM grading descriptors | AI applies grade-calibrated language based on the GS level specified |
| P3 | Missing required sections | Manager doesn't know the full agency template | Checklist surfaces every required section; AI auto-fills standard boilerplate |
| P4 | Wrong series code | Manager unfamiliar with OPM occupational series | AI analyzes described duties and recommends the correct series with explanation |
| P5 | Weeks of back-and-forth | P1–P4 compound into repeated HR review cycles | AI resolves P1–P4 before export, delivering a near-complete compliant draft on first submission |
| P6 | Institutional knowledge lost | No template memory across positions | Chat history + JSON export preserves each PD session; future managers load prior positions as context |

---

## Section 3: AI Role Design

### System Prompt Strategy

The LLM is configured with a system prompt that establishes its dual role:

```
You are an expert federal HR Specialist and Position Description writer
for [Agency Name]. You have deep knowledge of:
- OPM occupational series and classification standards
- GS grade-level descriptors and language calibration
- [Agency]'s required PD template sections
- Federal HR writing conventions (active voice, measurable duties,
  OPM qualification standards)

Your job is to help the hiring manager draft a fully compliant
Position Description. You will:
1. Extract position details from whatever the manager provides
   (plain description, pasted notes, old PD, or structured answers)
2. Generate a draft immediately against the agency template
3. Identify missing or non-compliant sections and ask targeted
   follow-up questions — one topic at a time
4. Patch the draft directly when the manager provides answers
5. Calibrate all duty language to the stated GS grade level
6. Flag when the described duties suggest a different series than requested
```

### Three Behavioral Modes (Auto-Detected)

| Input Type | AI Behavior |
|---|---|
| Freeform description | Extract intent → draft immediately → ask gaps |
| Structured Q&A | AI leads with intake questions → draft when enough context |
| Pasted notes / old PD | Clean up language → apply template → flag non-compliant sections |

Mode is inferred from the first message. No explicit mode selection by the manager.

### Draft-Intent Detection

Extends the existing app's draft-intent detection. Any message about a position, role, duties, or qualifications triggers draft generation and opens the right editor panel. Non-draft messages (e.g., "what is a GS-13?") are answered in chat only — draft panel does not appear.

### Compliance Pass (Final Step)

Self-check the AI performs before all checklist items turn green:

1. Does each major duty start with an action verb calibrated to the GS level?
2. Does the series code align with the majority of duties?
3. Are qualifications split into Required vs. Preferred?
4. Are agency-specific boilerplate sections present?
5. Is the position summary 3–5 sentences, jargon-free?

Any failure re-opens that checklist item with a targeted chat message.

### Series Recommendation

When the AI detects a possible series mismatch, it surfaces an inline banner in chat (not in the draft):

> ℹ️ **Series Suggestion**
> The duties you described (policy analysis, program evaluation) align more closely with GS-0343 (Management & Program Analysis) than GS-2210 (IT Management).
> [Keep GS-2210] [Switch to GS-0343]

---

## Section 4: UI/UX Design

### Layout

No new pages. All changes are within the existing `/workspace/{sessionId}` route and `DraftWorkspace.razor`.

```
┌─────────────────────────────────────────────────────────────────┐
│  TOPBAR: Position Description Builder   [Hide Chat]  [Sign out] │
├───────────────────┬─────┬───────────────────────────────────────┤
│  CHAT (left)      │  ║  │  DRAFT EDITOR (right)                 │
│                   │  ║  │                                       │
│  [AI messages]    │  ║  │  ┌─ Section Checklist ──────────────┐ │
│                   │  ║  │  │ ✅ Position Title                 │ │
│  [Manager input]  │  ║  │  │ ⚠️  Series / Grade               │ │
│                   │  ║  │  │ ✅ Position Summary               │ │
│  [AI follow-up    │  ║  │  │ ⚠️  Major Duties (3 of 5)        │ │
│   questions]      │  ║  │  │ ❌ Qualifications — Required      │ │
│                   │  ║  │  │ ❌ Qualifications — Preferred     │ │
│  ┌──────────────┐ │  ║  │  │ ❌ Education Requirements         │ │
│  │ Chat input   │ │  ║  │  │ ❌ Security Clearance             │ │
│  │ [Send]       │ │  ║  │  │ ✅ EEO Statement (auto-filled) 🔒 │ │
│  └──────────────┘ │  ║  │  └──────────────────────────────────┘ │
│                   │  ║  │                                       │
│                   │  ║  │  [Quill Rich Text Editor — PD draft] │
│                   │  ║  │                                       │
│                   │  ║  │  [Export ▾]                          │
└───────────────────┴─────┴───────────────────────────────────────┘
```

### Checklist Behavior

- Appears in the right panel when the draft editor opens after the first draft-intent response
- Items update in real time as AI patches sections
- Clicking a ⚠️ or ❌ item scrolls the Quill editor to that section
- Clicking a ❌ item also inserts a focused follow-up prompt into chat
- Export button disabled until all ❌ items are resolved

### Checklist Item States

| Icon | Meaning |
|---|---|
| ✅ | Complete and compliant |
| ⚠️ | Present but needs manager review (AI flagged a concern) |
| ❌ | Missing — export blocked until resolved |
| 🔒 | Auto-filled and locked (EEO, Reasonable Accommodation) |

### Export Gate

Word export requires all items to be ✅ or ⚠️ — no ❌ items remaining. Manager can override a ⚠️ by clicking "Acknowledge", which converts it to ✅ with a note recorded in the JSON export log.

### New Blazor Components

| Component | Location | Purpose |
|---|---|---|
| `PdSectionChecklist.razor` | `DraftWorkspace.razor` right panel header | Displays section completion status |
| `SeriesRecommendationBanner.razor` | Chat thread (inline) | Surfaces series mismatch with accept/reject buttons |

---

## Section 5: Agency Template Structure

### Required Sections

| # | Section | Required | Auto-filled | Validation Rule |
|---|---|---|---|---|
| 1 | Position Title | ✅ | No | Must match an OPM-recognized title for the series |
| 2 | Pay Plan / Series / Grade | ✅ | No | Series code 4-digit OPM format; grade GS-01 through GS-15 |
| 3 | Supervisory Status | ✅ | No | One of: Supervisory, Non-Supervisory, Team Leader |
| 4 | Position Summary | ✅ | No | 3–5 sentences; no jargon; states mission context and primary purpose |
| 5 | Major Duties | ✅ | No | Min 5 duties; each starts with grade-calibrated action verb; duties sum to ~100% of time |
| 6 | Qualifications — Required | ✅ | No | OPM minimum qualifications for series + agency-mandated requirements |
| 7 | Qualifications — Preferred | No | No | Optional but recommended; differentiates candidates |
| 8 | Education Requirements | ✅ | Partial | OPM-standard education language; AI auto-fills baseline, manager confirms degree requirement |
| 9 | Security Clearance | ✅ | No | Must state level: None / Public Trust / Secret / Top Secret / TS-SCI |
| 10 | Remote Work Eligibility | ✅ | No | One of: Remote, Hybrid, On-site; includes duty station city/state |
| 11 | Travel Requirements | ✅ | Partial | AI defaults to "Occasional (up to 25%)" — manager confirms or corrects |
| 12 | EEO Statement | ✅ | Yes 🔒 | Agency-standard EEO paragraph; auto-filled verbatim, locked from editing |
| 13 | Reasonable Accommodation | ✅ | Yes 🔒 | Agency-standard paragraph; auto-filled and locked |

### GS Grade-Level Language Guide (AI Reference)

| Grade Range | Duty Language Calibration |
|---|---|
| GS-05 to GS-07 | "Assists with...", "Performs routine...", "Under supervision..." |
| GS-09 to GS-11 | "Applies knowledge of...", "Analyzes...", "Develops..." |
| GS-12 to GS-13 | "Independently leads...", "Serves as technical expert...", "Designs and implements..." |
| GS-14 to GS-15 | "Provides authoritative guidance...", "Establishes policy...", "Represents the agency..." |

---

## Out of Scope

- Multi-user collaboration (HR specialist as a separate app user) — the LLM plays the HR specialist role
- Position classification appeals or formal OPM classification review
- Integration with USAJOBS posting API
- Automated approval workflows or digital signature

---

## Open Questions

- What is the agency name to insert into the system prompt?
- Does the agency have a locked EEO/Reasonable Accommodation statement to embed verbatim?
- Which OPM series codes are most commonly used at this agency? (Informs AI series recommendation priority)
- Should the JSON export log ⚠️ acknowledgments for audit trail purposes?
