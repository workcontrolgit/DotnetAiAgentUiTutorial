# Blog Design: Why Ollama Goes Silent on Large Inputs — and How to Fix It in .NET

**Date:** 2026-05-27
**Type:** Standalone companion post
**Target file:** `blogs/standalone/ollama-context-window.md`
**Series:** Companion to [AI Agents & MCP with .NET 10](https://medium.com/scrum-and-coke/ai-agents-mcp-with-net-10-preface-64314313e3e7)
**Repo:** [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp)

---

## Purpose

Ollama defaults to a 2048-token context window. When a .NET AI agent sends large payloads — tool results, document content, job records — the model silently truncates the input and returns incomplete or empty responses. This post explains why it happens and shows two ways to fix it, using the `DotnetAiAgentMcp` repo as the primary worked example.

---

## Audience

- General: any developer running Ollama who hits truncated/empty responses on large inputs
- Primary: readers of the AI Agents & MCP with .NET 10 series who have the repo running and want to understand how `NumCtx` works in the code

---

## Title

**Why Ollama Goes Silent on Large Inputs — and How to Fix It in .NET**

---

## Sections

### 1. The Problem

Short narrative opening. The developer asks the AI agent to process a list of job positions — titles, grades, salaries, descriptions. The response comes back empty, or it lists only two records when there were twenty. The model isn't broken. It ran out of context window.

Ollama's default context window is 2048 tokens. A system prompt, conversation history, tool results, and the model response all share that budget. Real-world agent tasks — especially MCP tool calls that return structured data — routinely exceed it.

### 2. What Is a Context Window?

One concise explanation for readers who haven't encountered the term.

The context window is the total token budget for a single LLM call. Everything in the request counts against it:

- System prompt
- Conversation history (every prior turn)
- Tool call results returned from MCP tools
- The space the model needs to write its response

When the combined input exceeds the window, Ollama silently truncates. The model responds to an incomplete picture of what you sent. It does not error. It does not warn. It just produces a shorter or wrong answer.

Token size reference: 2048 tokens ≈ 1500 words. A list of 50 structured job records can easily hit 10,000–30,000 tokens.

### 3. Option 1 — Pass `num_ctx` via the API (the .NET code approach)

Primary focus. Three layers of configuration in this repo:

**Layer 1 — appsettings.json**

File: `DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json`

```json
"Ollama": {
  "Endpoint": "http://localhost:11434",
  "Model": "gemma4:latest",
  "NumCtx": 32768
}
```

`NumCtx` is read at startup and passed through to every LLM call. 32768 tokens gives the agent room to handle dozens of structured records.

**Layer 2 — ChatOptions.AdditionalProperties in HrAgent.cs**

File: `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs`

```csharp
var additional = new AdditionalPropertiesDictionary();
if (numCtx.HasValue) additional["num_ctx"] = numCtx.Value;

var options = new ChatOptions { Tools = tools, AdditionalProperties = additional };
```

`Microsoft.Extensions.AI` passes `AdditionalProperties` through to the underlying provider. OllamaSharp maps `num_ctx` directly to Ollama's API parameter. This is the standard way to pass provider-specific options through the `IChatClient` abstraction without losing portability.

**Layer 3 — --num-ctx CLI override in Program.cs**

File: `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs`

```csharp
var numCtxArg = ParseIntArg(args, "--num-ctx");
if (numCtxArg.HasValue)
    configOverrides["AI:Ollama:NumCtx"] = numCtxArg.Value.ToString();
```

The `--num-ctx` argument overrides the appsettings value at runtime. Useful for experimenting with different window sizes without editing config files:

```bash
dotnet run --project DotnetAiAgentMcp/src/HrMcp.Agent -- --num-ctx 65536
```

### 4. Option 2 — Set Context via Modelfile

Alternative approach. No code required. Works for all Ollama clients (not just .NET).

Create a file named `Modelfile` with no extension:

```
FROM gemma4:latest
PARAMETER num_ctx 32768
```

Build a named variant:

```bash
ollama create gemma4-32k -f Modelfile
```

Use it in `appsettings.json`:

```json
"Model": "gemma4-32k"
```

The context window is now baked into the model variant. Every client that uses `gemma4-32k` gets the expanded window automatically.

### 5. Choosing Between Them

Bullet-list comparison (no markdown table):

**API / appsettings approach (what this repo uses):**
- Context size lives in code or config — versioned, reviewable, deployable
- Can be overridden per-call or per-environment
- Requires `AdditionalProperties` plumbing in the chat client
- Best for: applications where the context requirement is known and should be part of the codebase

**Modelfile approach:**
- No code changes — applies to all clients that use the named model
- One-time setup on each developer machine
- Cannot be changed per-call
- Best for: local dev tooling, quick testing, multi-language projects where you don't want provider-specific code in every client

### 6. Run Ollama on GPU for Better Performance

Performance tip section. Increasing `num_ctx` raises memory and compute requirements. Running Ollama on CPU is fine for testing but noticeably slow at larger context sizes. Running on GPU (via Docker with NVIDIA support) gives significantly faster inference.

**Start Ollama with GPU support using Docker:**

```bash
docker run -d \
  --gpus=all \
  -v ollama:/root/.ollama \
  -p 11434:11434 \
  --name ollama \
  --restart unless-stopped \
  ollama/ollama
```

Prerequisites: Docker Desktop with WSL2 backend (Windows) or Docker Engine (Linux), plus [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html) installed on the host.

Once the container is running, pull and run a model as usual:

```bash
docker exec -it ollama ollama pull gemma4:latest
```

**Verify the model is using GPU:**

Exec into the container and run `ollama ps`:

```bash
docker exec -it ollama ollama ps
```

Sample output when running on GPU:

```
NAME            ID              SIZE      PROCESSOR    UNTIL
gemma4:latest   abc123def456    8.1 GB    100% GPU     4 minutes from now
```

The `PROCESSOR` column shows `100% GPU` when the model is fully loaded onto the GPU. If it shows `100% CPU` or a split like `30%/70% CPU/GPU`, the model is not fully on GPU — usually because VRAM is insufficient for the full model at the configured `num_ctx`.

The `appsettings.json` `Endpoint` value does not change when using Docker — Ollama still listens on `http://localhost:11434`.

### 7. Verifying It Worked

Two ways to confirm the context window is applied:

**Check Ollama's response metadata:**
The Ollama API returns usage metadata in the raw response. With OllamaSharp, `num_ctx` is echoed back in the response. The simplest check: run `ollama run <model>` in a terminal and send a large prompt — the model will no longer cut off mid-answer.

**Observe agent behavior:**
Send a request with enough data to exceed 2048 tokens (e.g., "list all open positions" on a dataset with 30+ records). With default context the response will be truncated or empty. With `NumCtx: 32768` the response should be complete.

**Console output:**
The repo's startup banner prints the configured `NumCtx` value:

```
│  NumCtx    : 32,768                          │
```

If the value is printed, the configuration was loaded correctly.

---

## Section Summary

1. The Problem
2. What Is a Context Window?
3. Option 1 — Pass `num_ctx` via the API (the .NET code approach)
4. Option 2 — Set Context via Modelfile
5. Choosing Between Them
6. Run Ollama on GPU for Better Performance
7. Verifying It Worked

---

## Writing Conventions

- No markdown tables anywhere — use bullet lists (per user preference)
- Numbered steps for sequential instructions
- Code blocks with language tags for all snippets
- GitHub links to source files using the format already in `from-ollama-to-azure-foundry-llm-setup-for-dotnet-mcp.md`
- Prose explanations before each code block, not after
- No emojis

---

## Code References

| Purpose | File | Lines |
|---------|------|-------|
| NumCtx config value | `DotnetAiAgentMcp/src/HrMcp.Agent/appsettings.json` | L32–36 |
| AdditionalProperties wiring | `DotnetAiAgentMcp/src/HrMcp.Agent/HrAgent.cs` | L75–78 |
| --num-ctx CLI override | `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs` | L15–18 |
| NumCtx display in banner | `DotnetAiAgentMcp/src/HrMcp.Agent/Program.cs` | L110–112 |
