# ComfyUI MCP setup — Desk 42

Connects Claude Code to a running **ComfyUI** instance so it can queue workflows, generate art, and pull results into `Assets/_Project/Art/` (see `comfy-integration.md` for the art conventions). This is the third leg of the toolchain alongside the Unity MCP (`mcp-setup.md`) and FMOD (`fmod-integration.md`).

> Like the Unity bridge, ComfyUI is **local-only** by default. Treat the API as unauthenticated — bind it to localhost, don't expose it.

## Components
1. **ComfyUI** itself — running locally with its HTTP API reachable (default `http://127.0.0.1:8188`).
2. **A ComfyUI MCP server** — a small bridge that exposes ComfyUI's queue/history/workflow API as MCP tools, registered with Claude Code.

## 1. Stand up ComfyUI (Jacob — external)
- Install ComfyUI (portable build or git clone) and the checkpoints/LoRAs you want for the mid-century-surreal look (see the style guide in `comfy-integration.md`).
- Launch with the API enabled and reachable. For local-only use the default bind is fine; if a tool needs it on all interfaces use `--listen 127.0.0.1` to stay local.
- Confirm `http://127.0.0.1:8188` loads the ComfyUI web UI and that you can run a workflow manually first.
- Save the workflows you want Claude to drive as **API-format JSON** (ComfyUI: enable dev mode -> "Save (API Format)"). The MCP server queues these.

## 2. Pick + register a ComfyUI MCP server (Jacob — install)
Several community ComfyUI MCP servers exist; they wrap the same `/prompt`, `/history`, `/view` API. Pick one that:
- queues an API-format workflow with parameter overrides (prompt text, seed, dimensions),
- polls history and returns the output image path/bytes,
- points at your `127.0.0.1:8188`.

`<verify>` Confirm the exact server repo + run command at install time (these projects move fast — don't hardcode a stale URL). Common shapes are a Python (`uvx`/`pip`) or Node server you run locally.

Register it with Claude Code the standard way, e.g.:
```
claude mcp add comfyui -- <command to launch the chosen server>
```
or add it to the MCP config the Unity package's "Configure All Detected Clients" already created, so Unity + Comfy live side by side. Then `claude mcp list` should show **both** `unity` and `comfyui`.

## 3. Smoke test
- From Claude Code: list ComfyUI workflows / object info via the MCP tool — confirm it reaches the server.
- Queue one saved workflow with a simple tiered prompt from `comfy-integration.md` (e.g. a clean `coffee_mug_t0`), retrieve the PNG, and drop it into `Assets/_Project/Art/Sprites/`.
- Switch to Unity (via the Unity MCP) and confirm the sprite imports with the right settings.

## Output handoff
The MCP server returns images from ComfyUI's `output/` dir. Move/copy the chosen results into the correct `Assets/_Project/Art/` subfolder with the naming convention (`comfy-integration.md`), then let Unity import and apply the URP sprite settings. Keep generation scratch out of the repo — only commit curated, named assets.

## Guardrails
- Local-only; don't expose the ComfyUI API.
- Review every generation against the diegetic style guide before importing — screenshot, don't reason blind.
- Generate original art; don't reproduce specific copyrighted works.
