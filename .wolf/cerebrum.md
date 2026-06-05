# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-04-26

## User Preferences

- **Tutorial simplicity constraint:** Prefer evolving existing projects over creating new projects, to keep tutorial updates straightforward and avoid extra solution complexity.
- **Web UI interaction model:** Use a split view with chat on the left and job description review/editor on the right. Users should be able to view/edit the draft directly in the document panel, and AI edits should also flow into the same document workspace.
- **MVP phase scope:** MVP should focus on hiring manager drafting only (AI-assisted JD/PD creation + Word export for offline editing). HR specialist review/approval workflow is explicitly deferred to a later phase.
- **Tutorial rewrite constraint:** For the console-to-Blazor swap tutorial, minimize code movement/refactoring to avoid rewriting most existing blog content. Prefer in-place UI changes first.

- **No markdown tables in blog posts.** Convert to bullet lists or prose sections for Medium.com compatibility. When checklist items say "table", write a bullet list instead and update the checklist item to say "bullet list — no markdown table".
- **Blog post structure:** Use numbered steps, prose explanations, and code blocks with language tags. No tables anywhere.
- **Blog images must render outside local repo previews.** Prefer absolute hosted image URLs for post content intended for Medium/remote platforms, not relative local file paths.
- **When a pattern works in existing parts, keep consistency.** Do not switch image link style globally based on one failed render; follow the already-working convention unless asked.
- **Blog series content direction:** Remove DotnetFastMCP mentions/callouts from the series when requested; keep the narrative focused on the official MCP SDK path.
- **Future blog planning:** When requesting future-series direction, provide a concrete draft roadmap with placeholders, especially for data scaling and reference-based generation improvements.

## Key Learnings

- **Project:** DotnetMcpTutorial
- **Description:** A tutorial repository demonstrating **AI Agents** and the **Model Context Protocol (MCP)** using .NET 10 and Clean Architecture.
- **Blog series location:** `blogs/series-1-ai-agent-mcp/` — 6 parts; checklist at `CHECKLIST.md`
- **Blog diagram convention:** Place diagram/image assets under `blogs/series-1-ai-agent-mcp/diagrams/` and reference them from each part using relative paths like `diagrams/<filename>.png`.
- **Solution file:** `DotnetAiAgentMcp/DotnetAiAgentMcp.slnx` (not `.sln`)
- **OllamaSharp vs deprecated package:** `Microsoft.Extensions.AI.Ollama` is deprecated in the GA release. Use `OllamaSharp` (`OllamaApiClient`) instead. `OllamaApiClient` implements `IChatClient` natively.
- **OllamaApiClient implements both IChatClient and IEmbeddingGenerator.** Calling `.AsBuilder()` directly on `new OllamaApiClient(...)` is ambiguous. Must cast first: `((IChatClient)new OllamaApiClient(...)).AsBuilder()...`
- **ModelContextProtocol package split (1.x):** `ModelContextProtocol` is server-side only. Client API (`McpClient`, `HttpClientTransport`, `McpClientTool`) lives in `ModelContextProtocol.Core` (pulled in transitively). Use namespace `ModelContextProtocol.Client`.
- **M.E.AI 9.x renames:** `CompleteAsync` → `GetResponseAsync`, `ChatCompletion` → `ChatResponse`, use `.Text` for assistant text, `_history.AddMessages(response)` instead of `_history.Add(response.Message)`.
- **stdio transport rule:** stdout must be pure JSON-RPC when `--stdio` flag is set. Clear all log providers and redirect to stderr. `builder.WebHost.UseUrls()` with no args prevents HTTP listener.
- **`.vscode/mcp.json` gitignore:** `.vscode/` is gitignored. Use `.vscode/*` + `!.vscode/mcp.json` negation to track only the shared MCP config.
- **Run commands from repo root:** this workspace contains a nested solution folder. From repository root, use `dotnet run --project DotnetAiAgentMcp/src/HrMcp.McpServer` and `dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent` (not `src/...`).
- **Serilog status:** `HrMcp.McpServer` already uses Serilog; `HrMcp.Agent` now also writes error-level logs to rolling files at `logs/error-*.log` with a global try/catch in `Program.cs`.
- **In-place web mode migration status:** `HrMcp.Agent` now supports `--web` mode with Razor components and a split-view shell while preserving existing default console mode.
- **MVP shell asset rule:** In the minimal Task 2 shell, use local `wwwroot/css/app.css` only. Defer MudBlazor `_content` stylesheet wiring until full MudBlazor component usage is introduced.
- **Blazor web-mode startup rule:** In `HrMcp.Agent --web`, explicitly call `builder.WebHost.UseStaticWebAssets()` so `/_framework/blazor.web.js` resolves under non-Development environments when running via `dotnet run`.
- **Blazor interactivity rule for App.razor:** `Routes` and `HeadOutlet` must set `@rendermode="RenderMode.InteractiveServer"` or server-side event handlers (for example Send button `@onclick`) will not fire.
- **MVP draft sync rule:** After each successful assistant response, mirror non-empty response text into the right-side JD/PD draft editor; do not gate on markdown heading tokens.
- **Draft UX rule:** Do not expose Position ID in the draft editor during pre-approval drafting; keep export context identifiers internal to avoid workflow confusion.
- **Draft readability rule:** Show a rendered markdown preview next to/below the raw draft editor so formatting markers (`***`, headings, lists) are human-readable during review.
- **Prompt UX rule:** For long-running MCP/LLM calls (e.g., open positions lists), show an explicit in-progress message so users do not interpret disabled controls as a no-op.
- **Draft scope rule:** Right-side JD/PD draft panel should update only for draft-intent interactions; informational chat responses must remain in the chat thread only.
- **Prototype control rule:** Prefer explicit user toggle for draft synchronization (Send to Draft) over implicit content detection when clarity and predictability are priority.
- **Web runtime rule:** In `--web` mode, service-layer MCP transport resolution must honor CLI transport flags; config-only defaults can silently route requests to the wrong transport.
- **Prototype default rule:** Keep `Send to Draft` enabled by default so draft-generation prompts visibly populate the right panel without extra user setup.
- **Agent launch rule:** `HrMcp.Agent --web` runs on `http://localhost:5000` when launched from the repo root with `dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --web`.
- **Compact action rule:** In the Blazor workspace, place Send and Export Word in compact footer action rows rather than stacking them as oversized standalone controls.
- **Chat workspace rule:** Keep chat UX familiar by supporting Enter-to-send, a collapsible/resizable chat pane, and explicit Edit/View tabs for the JD/PD panel.
- **Waiting-state rule:** Show assistant progress directly in the chat thread with a spinner/status bubble so users know a response is actively being generated.
- **Control density rule:** Keep composer controls compact; avoid duplicate sync labels when one concise toggle + state pill communicates the same intent.
- **Draft boundary rule:** Even when Sync to Draft is ON, only drafting/refinement prompts may update the right draft editor; informational prompts (e.g., open position lists) must remain chat-only.
- **Copywriting rule:** Prefer plain-language UI copy for hiring managers; avoid technical abbreviations (JD/PD) in primary headings and tab labels.
- **Hint density rule:** Move secondary instructions behind a hover help icon (`?`) when space is limited in the chat composer.
- **Chat markdown rule:** Render assistant messages through Markdig (advanced extensions + HTML disabled) so markdown tables/lists are shown as formatted HTML inside chat bubbles.
- **Table normalization rule:** Normalize flattened markdown table text (single-line rows split by `| |`) before Markdig rendering to prevent raw table text in chat.
- **Markdown safety rule:** Wrap chat markdown rendering in a try/catch fallback (`<pre>` escaped text) so malformed model output cannot crash the Blazor circuit.
- **Table parsing rule:** For predictable open-position results, parse pipe-table rows directly into HTML table markup before markdown rendering instead of relying on inferred newline reconstruction.
- **Draft preview rule:** After draft-intent prompts (for example `Draft 25`), auto-switch the right panel to Preview mode so hiring managers immediately see rendered HTML rather than raw markdown.
- **Draft follow-up rule:** Treat edit instructions that mention adding/including qualifications or requirements as draft-intent prompts so follow-up refinements update the right draft panel.

## Do-Not-Repeat

- **[2026-05-21]** Spectre.Console 0.49.1 does NOT have `Color.MediumAquamarine3` or `Color.MediumAquamarine`. Use `Color.Aquamarine3` instead (closest match). Available aqua-family colors: `Aqua`, `Aquamarine3`, `Aquamarine1`, `Aquamarine1_1`, `MediumTurquoise`, `Teal`.

- **[2026-04-26]** Do NOT use `Microsoft.Extensions.AI.Ollama` (`OllamaChatClient`). It is deprecated in GA. Use `OllamaSharp` (`OllamaApiClient`) instead.
- **[2026-04-26]** Do NOT call `.AsBuilder()` directly on `new OllamaApiClient(...)` — ambiguous overload. Always cast: `((IChatClient)new OllamaApiClient(...)).AsBuilder()`.
- **[2026-04-26]** Do NOT use `McpClientFactory` or `SseClientTransport` — removed in `ModelContextProtocol` 1.x. Use `McpClient.CreateAsync(new HttpClientTransport(...))`.
- **[2026-04-26]** Do NOT use `IChatClient.CompleteAsync` — renamed to `GetResponseAsync` in M.E.AI 9.10.2 GA.
- **[2026-04-26]** Do NOT pin `Microsoft.Extensions.AI` at `9.*` when `OllamaSharp 5.*` is in the project. OllamaSharp 5.4+ pulls `Microsoft.Extensions.AI.Abstractions 10.4.x` transitively, causing a `TypeLoadException` at runtime on `FunctionApprovalRequestContent`. Pin `Microsoft.Extensions.AI` at `10.*`.

## Decision Log

- **[2026-06-04] JD drafting workflow is staged, not simultaneous:** The hiring manager collaborates with AI to produce the initial draft. The HR specialist joins later for review, compliance, and approval rather than co-authoring the draft live.

- **[2026-05-08] Multi-agent tutorial repo:** When implementing the multi-agent pipeline (HrDraftAgent + OpmComplianceAgent + JobDescriptionOrchestrator), fork `DotnetAiAgentMcp` into a new repo named `DotnetMultiAgentsTutorial`. Write a new blog series titled "Multi-Agent Systems in .NET" (or similar) — separate from Series 1. New series covers Microsoft Agent Framework, ChatClientAgent, orchestrator pattern, two-stage compliance checking.
- **[2026-05-08] Diagram: REST vs MCP side-by-side** saved to `blogs/series-1-ai-agent-mcp/diagrams/rest-vs-mcp-architecture.png` and embedded in `docs/meetings/2026-05-06-mcp-jd-drafting-architect-review.md`.

- **[2026-04-26] Part 5 before Part 6 (Claude Desktop before OIDC security):** Keep Claude Desktop as Part 5 because it's the payoff demo. Security (OIDC) added in Part 6 on top of a working system. stdio transport is local-only so no network exposure risk in the demo.
- **[2026-04-26] OllamaSharp version constraint `5.*`:** Matches version `5.3.4` used in reference project. Stable GA release.
- **[2026-04-26] Duende IdentityServer container setup (local dev):** STS at `https://localhost:44310` (nginx proxy, self-signed cert). Authority = `https://localhost:44310`, Audience = API resource name (e.g., `hr-mcp`). JWT Bearer backchannel needs `DangerousAcceptAnyServerCertificateValidator` in Development. Client credentials grant: client ID `hr-mcp-agent`, secret `hr-mcp-agent-secret`, scope `hr-mcp-api`. Token endpoint: `https://localhost:44310/connect/token`. Agent HTTP client also needs cert bypass for the token request.
