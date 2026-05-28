# Ollama Context Window Blog Post Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write a standalone blog post explaining why Ollama silently truncates large inputs and how to fix it — using the DotnetAiAgentMcp repo as the primary worked example.

**Architecture:** Single markdown file at `blogs/standalone/ollama-context-window.md`. Content is assembled section by section. No code changes to the repo — this is a writing task only.

**Tech Stack:** Markdown, GitHub-linked code references, OllamaSharp, Microsoft.Extensions.AI, Docker (NVIDIA GPU)

---

## File Map

- Create: `blogs/standalone/ollama-context-window.md` — the blog post

---

### Task 1: Create the file with header and About section

**Files:**
- Create: `blogs/standalone/ollama-context-window.md`

- [ ] **Step 1: Create the file with title and About section**

Write the following content to `blogs/standalone/ollama-context-window.md`:

```markdown
# Why Ollama Goes Silent on Large Inputs — and How to Fix It in .NET

## About This Article

This is a companion guide to the blog series **[AI Agents & MCP with .NET 10](https://medium.com/scrum-and-coke/ai-agents-mcp-with-net-10-preface-64314313e3e7)**.

The series walks through building a production-ready AI-enabled backend using .NET 10 and Clean Architecture — from a federal HR domain data model through a Model Context Protocol (MCP) server, an AI agent using `Microsoft.Extensions.AI`, Claude Desktop integration, and OIDC security. All code is in the [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp) GitHub repo.

The series uses Ollama as the default local LLM — a zero-cost setup that works without any cloud account. This article is for readers who have followed the series (or cloned the repo) and are hitting a specific problem: Ollama responding with incomplete data or returning nothing at all when the input is large.

You do not need to have read the full series. If you have Ollama installed and running locally, this guide is self-contained.

---
```

- [ ] **Step 2: Verify the file was created**

Confirm `blogs/standalone/ollama-context-window.md` exists and contains the title and About section.

- [ ] **Step 3: Commit**

```bash
git add blogs/standalone/ollama-context-window.md
git commit -m "blog: scaffold ollama context window post"
```

---

### Task 2: Write Section 1 — The Problem

**Files:**
- Modify: `blogs/standalone/ollama-context-window.md`

- [ ] **Step 1: Append Section 1 to the file**

Append the following after the `---` at the end of the current file:

```markdown
## The Problem

You ask your AI agent to list all open positions. You have thirty records in the database. The agent calls the MCP tool, gets the data back, and returns… two positions. Or five. Or nothing at all.

The model is not broken. The data did reach Ollama. What happened is that Ollama silently truncated your input before the model ever saw all of it.

Ollama's default context window is 2048 tokens. That budget covers everything in a single LLM call: your system prompt, the full conversation history, all tool call results, and the space the model needs to write its response. The budget is shared. When the combined input exceeds 2048 tokens, Ollama quietly cuts off the tail end and the model responds to an incomplete picture of what you sent.

This is not an error. Ollama does not throw an exception. It does not print a warning. It just truncates — and you get wrong answers.

---
```

- [ ] **Step 2: Verify section content**

Re-read the file. Confirm Section 1 ends with `---` and contains the truncation explanation.

---

### Task 3: Write Section 2 — What Is a Context Window?

**Files:**
- Modify: `blogs/standalone/ollama-context-window.md`

- [ ] **Step 1: Append Section 2**

Append the following:

```markdown
## What Is a Context Window?

The context window is the total token budget for a single LLM call. Every token in the request counts against it.

For an AI agent built with `Microsoft.Extensions.AI` and MCP, the budget is spent across:

- The system prompt (instructions, persona, guidelines)
- Conversation history — every prior user message and assistant response in the current session
- Tool call results — the JSON payloads returned by MCP tools like `GetOpenPositions` or `GetPositionById`
- The model's response — the space it needs to write its answer

When the total exceeds the window, Ollama truncates the input and the model responds to whatever fits.

As a rough scale: 2048 tokens is approximately 1,500 words. A system prompt alone can consume 200–400 tokens. A conversation with a few turns adds another few hundred. A single MCP tool result returning 30 structured job records can easily hit 5,000–15,000 tokens. The default window runs out fast.

The fix is to increase the context window to match your actual workload. This repo uses 32,768 tokens as the default for Ollama, which comfortably handles dozens of structured records per call.

---
```

- [ ] **Step 2: Verify**

Confirm Section 2 is present and explains the token budget breakdown with the four bullet points.

---

### Task 4: Write Section 3 — Option 1: Pass num_ctx via the API

**Files:**
- Modify: `blogs/standalone/ollama-context-window.md`

- [ ] **Step 1: Append Section 3**

Append the following:

```markdown
## Option 1 — Pass num_ctx via the API (the .NET Code Approach)

This is the approach used in the `DotnetAiAgentMcp` repo. The context size lives in configuration and flows through to every LLM call via `ChatOptions.AdditionalProperties`. It is versioned, reviewable, and can be overridden per-environment or per-run.

There are three layers to understand.

### Layer 1 — appsettings.json

File: [DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json](https://github.com/workcontrolgit/DotnetAiAgentMcp/blob/main/DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json)

```json
"Ollama": {
  "Endpoint": "http://localhost:11434",
  "Model": "gemma4:latest",
  "NumCtx": 32768
}
```

`NumCtx` is read at startup and passed into the agent. Setting it to 32,768 gives the model enough room to handle dozens of structured records in a single call.

### Layer 2 — ChatOptions.AdditionalProperties in HrAgent.cs

File: [DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs](https://github.com/workcontrolgit/DotnetAiAgentMcp/blob/main/DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs)

```csharp
var additional = new AdditionalPropertiesDictionary();
if (numCtx.HasValue) additional["num_ctx"] = numCtx.Value;

var options = new ChatOptions { Tools = tools, AdditionalProperties = additional };
```

`Microsoft.Extensions.AI` passes `AdditionalProperties` through to the underlying provider. `OllamaSharp` maps `num_ctx` directly to Ollama's API parameter. This is the standard way to pass provider-specific options through the `IChatClient` abstraction — it keeps the code portable across providers while still letting you reach Ollama-specific knobs when you need them.

The `ChatOptions` object is passed to every `GetResponseAsync` call in the tool loop, so the context size applies to every turn in the conversation.

### Layer 3 — --num-ctx CLI Override in Program.cs

File: [DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs](https://github.com/workcontrolgit/DotnetAiAgentMcp/blob/main/DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs)

```csharp
var numCtxArg = ParseIntArg(args, "--num-ctx");
if (numCtxArg.HasValue)
    configOverrides["AI:Ollama:NumCtx"] = numCtxArg.Value.ToString();
```

The `--num-ctx` argument overrides the `appsettings.json` value at runtime without touching any files. This is useful when you want to experiment with different window sizes quickly:

```bash
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --num-ctx 65536
```

The CLI override is added to the configuration last (`AddInMemoryCollection`), so it wins over all file-based sources including environment-specific overrides.

---
```

- [ ] **Step 2: Verify**

Confirm all three layers are present with correct code blocks and GitHub links. Confirm the `AdditionalProperties` explanation is included.

- [ ] **Step 3: Commit**

```bash
git add blogs/standalone/ollama-context-window.md
git commit -m "blog(ollama-ctx): add sections 1-3 (problem, context window, API approach)"
```

---

### Task 5: Write Section 4 — Option 2: Modelfile

**Files:**
- Modify: `blogs/standalone/ollama-context-window.md`

- [ ] **Step 1: Append Section 4**

Append the following:

```markdown
## Option 2 — Set Context via Modelfile

The Modelfile approach bakes the context window directly into a named model variant. No code changes required. Any Ollama client — whether .NET, Python, a CLI, or a web tool — that uses the named variant gets the expanded window automatically.

Create a file named `Modelfile` (no extension) in any working directory:

```
FROM gemma4:latest
PARAMETER num_ctx 32768
```

Build a named variant from it:

```bash
ollama create gemma4-32k -f Modelfile
```

Ollama registers `gemma4-32k` locally. Update `appsettings.json` to point to it:

```json
"Ollama": {
  "Endpoint": "http://localhost:11434",
  "Model": "gemma4-32k",
  "NumCtx": 32768
}
```

Keep `NumCtx` in `appsettings.json` as well — it documents intent and is used by the startup banner. From this point on, every client that requests `gemma4-32k` gets 32,768 tokens of context without any code-level plumbing.

---
```

- [ ] **Step 2: Verify**

Confirm the Modelfile content, `ollama create` command, and the `appsettings.json` update are all present.

---

### Task 6: Write Section 5 — Choosing Between Them

**Files:**
- Modify: `blogs/standalone/ollama-context-window.md`

- [ ] **Step 1: Append Section 5**

Append the following:

```markdown
## Choosing Between Them

Both approaches set the same Ollama parameter. The difference is where the setting lives and who it applies to.

**API / appsettings approach (what this repo uses):**
- The context size lives in code and config — versioned, reviewable, and deployable alongside the application
- Can be overridden per-call, per-environment, or per-run with `--num-ctx`
- Requires `AdditionalProperties` wiring in the chat client
- Best for applications where the context size is a known requirement that belongs in the codebase

**Modelfile approach:**
- No application code changes — the setting is part of the model variant itself
- Applies to every Ollama client on the machine that uses the named variant
- One-time setup per developer machine; cannot be changed per-call
- Best for local dev tooling, quick exploration, or multi-language projects where you do not want provider-specific code in every client

For the `DotnetAiAgentMcp` repo, the API approach is the right fit — the context size is a known operational parameter that should travel with the application config. For a developer who just wants to use a larger context everywhere without touching code, the Modelfile approach is simpler.

---
```

- [ ] **Step 2: Verify**

Confirm both approaches are listed as bullet points (no markdown table). Confirm the repo-specific recommendation is present.

---

### Task 7: Write Section 6 — Run Ollama on GPU for Better Performance

**Files:**
- Modify: `blogs/standalone/ollama-context-window.md`

- [ ] **Step 1: Append Section 6**

Append the following:

```markdown
## Run Ollama on GPU for Better Performance

Increasing `num_ctx` raises memory and compute requirements. Running Ollama on CPU works for testing but becomes noticeably slow once the context window grows beyond the default — especially when the model needs to attend over 20,000+ tokens per call. Running Ollama in Docker with GPU passthrough gives significantly faster inference and is worth setting up if you have an NVIDIA GPU.

### Start Ollama with GPU Support

Prerequisites:
- Docker Desktop (Windows/Mac) or Docker Engine (Linux)
- [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html) installed on the host
- WSL2 backend enabled on Windows

Run the following command to start Ollama with full GPU access:

```bash
docker run -d \
  --gpus=all \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  --restart unless-stopped \
  ollama/ollama
```

This mounts a named volume (`ollama`) to persist downloaded models between container restarts. The `--restart unless-stopped` flag keeps the container running across reboots.

Once the container is running, pull the model you need:

```bash
docker exec -it ollama ollama pull gemma4:latest
```

The `appsettings.json` `Endpoint` value does not change — Ollama still listens on `http://localhost:11434` regardless of whether it is running natively or in Docker.

### Verify the Model Is Running on GPU

Exec into the running container and run `ollama ps`:

```bash
docker exec -it ollama ollama ps
```

Sample output when the model is fully loaded on GPU:

```
NAME            ID              SIZE      PROCESSOR    UNTIL
gemma4:latest   abc123def456    8.1 GB    100% GPU     4 minutes from now
```

The `PROCESSOR` column is the key indicator:

- `100% GPU` — the model is fully loaded in VRAM. Best performance.
- `100% CPU` — the model is running on CPU. Either the GPU is not detected or VRAM is too small.
- A split like `30% GPU / 70% CPU` — partial offload. VRAM is not large enough to hold the full model at the configured `num_ctx`. Consider a smaller model or reduce `num_ctx`.

If you see `CPU` when you expect `GPU`, check that the NVIDIA Container Toolkit is installed correctly and that Docker has access to the GPU (`docker run --gpus=all nvidia/cuda:12.0-base nvidia-smi`).

---
```

- [ ] **Step 2: Verify**

Confirm the Docker `--gpus=all` command, `docker exec -it ollama ollama ps`, the sample output block, and the three `PROCESSOR` column explanations are all present.

- [ ] **Step 3: Commit**

```bash
git add blogs/standalone/ollama-context-window.md
git commit -m "blog(ollama-ctx): add sections 4-6 (modelfile, comparison, GPU)"
```

---

### Task 8: Write Section 7 — Verifying It Worked

**Files:**
- Modify: `blogs/standalone/ollama-context-window.md`

- [ ] **Step 1: Append Section 7**

Append the following:

```markdown
## Verifying It Worked

Three ways to confirm the context window is applied correctly.

### 1. Check the startup banner

The repo prints the active configuration at startup. If `NumCtx` is configured, it appears in the banner:

```
┌─────────────────────────────────────────────┐
│  HrMcp.Agent                                │
│  Transport : stdio                          │
│  Provider  : Ollama                         │
│  Model     : gemma4:latest                  │
│  NumCtx    : 32,768                         │
│  Tools (4) :                                │
│    - GetHiringOrganizations                 │
│    - GetOpenPositions                       │
│    - GetPositionsByOrganization             │
│    - GetPositionById                        │
│  Status    : READY                          │
└─────────────────────────────────────────────┘
```

If `NumCtx` is missing from the banner, the configuration was not loaded. Check that `appsettings.json` has the `NumCtx` key under `AI:Ollama`.

### 2. Test with a large payload

Ask the agent to list all open positions. If your dataset has more than 15–20 records, this request will exceed the 2048-token default. With `NumCtx: 32768`, the agent should return the full list. If responses are still truncated, increase the value and re-run.

### 3. Use ollama ps to check the loaded model

While the agent is actively processing a request, run the following in a separate terminal (or inside the Docker container if using Docker):

```bash
ollama ps
```

The output shows the currently loaded model and its context size:

```
NAME            ID              SIZE      PROCESSOR    UNTIL
gemma4:latest   abc123def456    8.1 GB    100% GPU     4 minutes from now
```

The model remains loaded in memory for a few minutes after the last request. If the size reported here is significantly smaller than expected, the model may not have received the `num_ctx` parameter — verify the `AdditionalProperties` wiring in `HrAgent.cs`.

---

## Source Code

All code shown in this post is from the [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp) repository.

Key files:

- [appsettings.json](https://github.com/workcontrolgit/DotnetAiAgentMcp/blob/main/DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json) — `NumCtx` configuration value
- [HrAgent.cs](https://github.com/workcontrolgit/DotnetAiAgentMcp/blob/main/DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs) — `ChatOptions.AdditionalProperties` wiring
- [Program.cs](https://github.com/workcontrolgit/DotnetAiAgentMcp/blob/main/DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs) — `--num-ctx` CLI override and startup banner
```

- [ ] **Step 2: Verify the full file**

Read `blogs/standalone/ollama-context-window.md` from top to bottom. Confirm:
- All 7 sections are present in order
- No section is empty or contains placeholder text
- All code blocks have language tags (`json`, `csharp`, `bash`)
- No markdown tables appear anywhere in the file
- All GitHub links point to `github.com/workcontrolgit/DotnetAiAgentMcp`

- [ ] **Step 3: Final commit**

```bash
git add blogs/standalone/ollama-context-window.md
git commit -m "blog(ollama-ctx): complete post — context window, API approach, modelfile, GPU"
```

---

## Self-Review Checklist

Run before marking complete:

- [ ] Section 1 (The Problem) — silent truncation narrative present
- [ ] Section 2 (Context Window) — token budget breakdown with 4 bullet points
- [ ] Section 3 (Option 1) — all three layers (appsettings, AdditionalProperties, CLI override)
- [ ] Section 4 (Option 2) — Modelfile content, `ollama create` command, appsettings update
- [ ] Section 5 (Choosing) — bullet-list comparison, no markdown table
- [ ] Section 6 (GPU) — Docker `--gpus=all` command, `ollama ps` verification, PROCESSOR column explanation
- [ ] Section 7 (Verifying) — banner output, large payload test, `ollama ps` guidance
- [ ] No markdown tables in the post
- [ ] All code blocks have language tags
- [ ] GitHub links are correct format
