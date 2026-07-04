---
name: desk-42
description: Project knowledge for Desk 42 Anomalous Claims (Unity/C# bureaucratic-horror desk sim). Load when working anywhere in this repo — architecture, C#/Unity conventions, current build state, FMOD audio, ComfyUI art pipeline, MCP setup, and the risk watchlist. Use before editing the BSM, Red Tape Engine, UI Decay, the dials, The Tide, or wiring FMOD/Comfy.
---

# Desk 42 — project skill

You are working on **Desk 42: Anomalous Claims**, a Unity/C# systemic desk-simulator roguelite. Read `CLAUDE.md` at the repo root first for the governing pillar (the UI *is* the world) and the verified stack.

Reference files in this folder — read the one(s) relevant to your task:

- **architecture.md** — what the systems are and why (BSM, Red Tape Engine, UI Decay, dials/meters, the Distortion Scale + FMOD hook, The Tide, Internal Audit). Read before designing or changing system behaviour.
- **csharp-conventions.md** — house C#/Unity style; defer to existing repo patterns where they differ. Read before writing code.
- **fmod-integration.md** — FMOD audio: the **real** event/bus/param contract the code already expects (`Desk42.Audio`, `FMODManager`, `DESK42_FMOD` gate, event-bus via `SanityChangedEvent`), plus the intended Distortion-Scale authoring target. Read before any audio work.
- **comfy-integration.md** — ComfyUI art pipeline: diegetic style guide, Distortion-Scale-aware prompt tiers, output→import conventions feeding `Assets/_Project/Art/`. Read before generating or importing art.
- **mcp-setup.md** — Coplay "MCP for Unity" install + how the editor bridge works. Read before editor-side MCP work.
- **comfy-mcp-setup.md** — standing up ComfyUI + connecting a ComfyUI MCP server. Read before driving Comfy from Claude.
- **build-state.md** — what actually exists vs. what's still to confirm; the next-steps queue. Read at the start of a session; keep it updated.
- **known-issues.md** — risk watchlist (not confirmed bugs). Check before touching the Red Tape Engine, UI Decay, FMOD, or save/load.

## Operating rules (short form)
- Discover before acting — query the editor / read the file; never invent GameObject names, asset paths, or namespaces.
- MCP for editor-side work; direct file edits for C# systems.
- Screenshot -> verify -> adjust on anything visual.
- No magic numbers in gameplay code — tunables go in serialized fields / ScriptableObjects.
- Don't break the diegetic frame (no plain HUD overlays).
- Audio is **event-bus driven** (`RumorMillEventBus` / `SanityChangedEvent`) and routed through `FMODManager` — don't add a parallel polling director.
- If something's ambiguous or marked `<verify>`, confirm it — don't guess.
