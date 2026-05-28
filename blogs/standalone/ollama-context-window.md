# Why Ollama Goes Silent on Large Inputs — and How to Fix It in .NET

## About This Article

This is a companion guide to the blog series **[AI Agents & MCP with .NET 10](https://medium.com/scrum-and-coke/ai-agents-mcp-with-net-10-preface-64314313e3e7)**.

The series walks through building a production-ready AI-enabled backend using .NET 10 and Clean Architecture — from a federal HR domain data model through a Model Context Protocol (MCP) server, an AI agent using `Microsoft.Extensions.AI`, Claude Desktop integration, and OIDC security. All code is in the [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp) GitHub repo.

The series uses Ollama as the default local LLM — a zero-cost setup that works without any cloud account. This article is for readers who have followed the series (or cloned the repo) and are hitting a specific problem: Ollama responding with incomplete data or returning nothing at all when the input is large.

You do not need to have read the full series. If you have Ollama installed and running locally, this guide is self-contained.

---

## The Problem

You ask your AI agent to list all open positions. You have thirty records in the database. The agent calls the MCP tool, gets the data back, and returns… two positions. Or five. Or nothing at all.

The model is not broken. The data did reach Ollama. What happened is that Ollama silently truncated your input before the model ever saw all of it.

Ollama's default context window is 2048 tokens. That budget covers everything in a single LLM call: your system prompt, the full conversation history, all tool call results, and the space the model needs to write its response. The budget is shared. When the combined input exceeds 2048 tokens, Ollama quietly cuts off the tail end and the model responds to an incomplete picture of what you sent.

This is not an error. Ollama does not throw an exception. It does not print a warning. It just truncates — and you get wrong answers.

---

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
