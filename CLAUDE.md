# CLAUDE.md — Desk 42: Anomalous Claims

Orientation for Claude Code / agents working in this repo. Read before touching code.

## What this is
Desk 42: Anomalous Claims — a systemic **desk simulator / bureaucratic dark comedy roguelite** for Steam PC. Unity, C#. Solo dev (Jacob), art/UX (Anu).

**The one pillar that governs everything:** the UI *is* the game world. There is no 3D scene with a HUD on top. Every mechanic — punch-card logic blocks, the dials, claim processing, the interface fighting back — lives on the desk surface as diegetic objects. If a change introduces a "normal" HUD overlay or breaks the diegetic frame, it's wrong by default; flag it.

## Stack (verified against the live repo, 2026-06)
- Unity **2022.3.62f3** (LTS), C#. Render pipeline: **URP 14.0.12**. Input: **Unity Input System 1.14.2**. Newtonsoft JSON, Addressables, Cinemachine, TextMeshPro, Steamworks (openupm).
- Target: Steam PC (Windows first).
- **~157 C# scripts across 24 namespaces** (`Desk42.*`). Runtime code is a **single assembly**, `Assets/_Project/Scripts/Desk42.Core.asmdef`; tests are `Desk42.Tests.EditMode` / `Desk42.Tests.PlayMode`.
- Project layout lives under `Assets/_Project/` (`Scripts/`, `ScriptableObjects/`, `Prefabs/`, `Art/`, `Audio/`, `Scenes/`, `Tests/`).

## The three loops
1. **"The 9-to-5"** (core campaign) — procedural queue of Clients (alien/eldritch claimants). Paced by **The Tide** storyteller (`Core/TideSystem.cs`): *Flow* raises pressure when you're breezing, *Ebb* grants a mercy window when you're drowning. Watched meters: **Sanity / Cognitive Budget** and the **Unpaid Overtime / Impatience** timer.
2. **"The Internal Audit"** (meta-hub / puzzle) — between clients you do paperwork on *yourself*. Internal-Family-Systems framing: the dials are sub-personalities. Solve punch-card logic puzzles (`RedTape/PunchCardMachine.cs`) to repair cracked UI glass / clear red tape = upgrades.
3. **"Unpaid Overtime"** (death state) — Sanity hits zero -> you don't die, you **dissociate** (Fugue): desaturation, flicker, muffled audio, dials ignore your toggles. Claw back to clock out. (`RunStateController.IsInFugueState`.)

## Core systems (discover the real implementation before editing — paths are real)
- **Run/player state** — `Core/RunStateController.cs` owns **Sanity** (`Sanity`, `ModifySanity`, `IsInFugueState`); `Persistence/RunData.cs` is the serialized model. This is the source of truth for Sanity that audio + visuals both read.
- **Event bus** — `Narrative/RumorMillEventBus.cs` (+ `RumorMillEvents.cs`). `SanityChangedEvent(prev, curr, fugue)` is published on Sanity changes and **every reactive system subscribes** (audio, achievements, analytics, shift mgmt). Prefer this bus over polling.
- **Bureaucratic State Machine (BSM)** — `BSM/` (`ClientStateMachine`, `ClientStateStack`, `ClientContext`, `States/`, `Transitions/`). Every Client *and* Office Tool runs one. 9 client states. Composed with **StateInjector** (`RedTape/StateInjector.cs`).
- **Red Tape Engine / dynamic node injection** — `BehaviourTrees/` (`BehaviourTree`, `BTNode`, `MutationEngine`, `Nodes/`). The antagonist injects disruptive BT nodes into the player's workflow at runtime via `MutationEngine`. Injected nodes must be cleaned up; leaked injections = permanent disruption.
- **The dials + meters** — see `MoralInjury/MoralInjurySystem.cs`, `Archetypes/` (Sanity costs), and `RunData`. Reconcile dial-vs-meter terminology against code, not the GDD.
- **Environmental Distortion Scale** — keyed to Sanity; each tier has a visual *and* audio state. This is the FMOD hook. **FMOD is already scaffolded in code** (see below). See `.claude/skills/desk-42/architecture.md` + `fmod-integration.md`.

## Audio / FMOD (already scaffolded — read before any audio work)
The `Desk42.Audio` namespace already contains an FMOD-shaped architecture, **gated behind the `DESK42_FMOD` scripting define** (currently **off** — it compiles as no-op stubs until the FMOD plugin is imported):
- `FMODManager.cs` — singleton wrapper (`SetGlobalParameter`, `SetBusVolume`, `PlayOneShot`, bus volume presets). All game systems route through this, not `RuntimeManager` directly.
- `BinauralStressEngine`, `ProceduralJazzGenerator`, `StressCrescendo`, `SpatialAudioThreatSystem` — per-system drivers, **event-bus driven** (subscribe to `SanityChangedEvent`), each pushing FMOD params.
- Real FMOD vocabulary the code expects: global params `"Sanity"` (**normalized 0–1**) and `"TidePressure"`; buses `bus:/`, `bus:/Music`, `bus:/SFX`, `bus:/Ambience`; events under `event:/Music/*` and `event:/Threat/*`. **Match Studio authoring to these names.** Full detail in `fmod-integration.md`.

## How to work here
- **MCP for editor-side work** (scenes, prefabs, components, materials, running tests); **direct file edits for C# systems.** See `.claude/skills/desk-42/mcp-setup.md`.
- **Art via ComfyUI** — see `comfy-integration.md` / `comfy-mcp-setup.md`.
- **Discover before acting.** Don't guess GameObject names, asset paths, or namespaces — query the editor / read the file. No invented paths.
- **Screenshot -> verify -> adjust** on anything visual. Don't reason blind about the desk.
- **No magic numbers** in gameplay C#. Tunables live in serialized fields / ScriptableObjects so they're designer-editable. Flag literals you find.
- **Don't break the diegesis** (see pillar above).
- Keep `.claude/skills/desk-42/build-state.md` current as things land.

## Tone
Direct, terse, no flattery. Jacob directs; you build. Name the real risk plainly. If something's ambiguous, say so — don't paper over it.
