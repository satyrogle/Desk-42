# MCP setup — Desk 42 (Unity)

Connects Claude Code directly to your **open Unity editor** so it can manage scenes, prefabs, components, materials, and run tests. It complements direct C# editing — it does not replace it. Direct file edits for systems code; MCP for editor-side work.

> Different from the Brawler setup. That used an **Unreal**-only Python bridge (UnrealClaudeMCP, TCP 18888) — it does **not** work with Unity. Desk 42 needs a Unity bridge. Use the one below.

## Server: CoplayDev — MCP for Unity
Live and actively maintained, MIT, supports Unity 2021.3 LTS -> Unity 6. Local-only (binds 127.0.0.1, no auth). Works with this project's Unity **2022.3.62f3**.

### Prerequisites
- **Node.js 18+** on PATH (the MCP server is Node; 16+ is the documented minimum — use 18 LTS).
- Claude Code installed (you already have it).
- The Unity editor for this project, open.

### Install (in Unity)
1. **Window -> Package Manager -> + -> Add package from git URL**, paste:
   ```
   https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
   ```
   (Beta channel: swap `#main` for `#beta`.)
   *This pack already adds the same dependency to `Packages/manifest.json`, so Unity should resolve it on next focus — the manual Package Manager step is the fallback / how to verify.*
2. **Window -> MCP for Unity -> Configure All Detected Clients.** This **auto-writes the Claude Code MCP config** for you — that's why there's no hand-rolled `.mcp.json` in this repo (a guessed one would just be the Brawler dead-end again). Let the package write the real one.
3. In Claude Code, run a smoke prompt: **"create a red cube in the current scene."** If a cube appears in Unity, the bridge is live.

### If it doesn't connect
- Editor must be open with the package loaded before Claude Code talks to it.
- **Play Mode drops the bridge:** entering Play Mode reloads the editor app domain and can kill the connection. If it disconnects on Play, turn off **Edit -> Project Settings -> Editor -> Enter Play Mode Settings -> Reload Domain.**
- Re-run **Configure All Detected Clients** after updating the package or Claude Code.
- To inspect the config the package wrote: `claude mcp list`.

### Note
Coplay also ships its own `unity-mcp-skill` (how to *use the MCP tools*). That's complementary — the `.claude/skills/desk-42/` skill here is about *your game's architecture*, not the tool. Keep both.

### Verify the manifest entry
`Packages/manifest.json` should contain (added by this pack):
```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"
```
If Unity reports the package name/path doesn't resolve, check the current install instructions on the CoplayDev repo and update the manifest line — package id/path can drift between releases. `<verify on first resolve>`
