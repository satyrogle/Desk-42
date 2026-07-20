# Build state — Desk 42  (keep this current)

Single source of truth for "what actually exists right now." Update it as things land — this is what lets an agent skip re-discovering the project every session. **Reconciled against the live repo 2026-06-13.**

## Stack (verified)
- Unity **2022.3.62f3** LTS, **URP 14.0.12**, **Input System 1.14.2**, Newtonsoft JSON, Addressables, Cinemachine, TextMeshPro, Steamworks (openupm).
- **~157 C# scripts across 24 `Desk42.*` namespaces.** Runtime = single asmdef `Assets/_Project/Scripts/Desk42.Core.asmdef`; tests `Desk42.Tests.EditMode` / `.PlayMode`.
- Project under `Assets/_Project/` (`Scripts/`, `ScriptableObjects/`, `Prefabs/`, `Art/`, `Audio/`, `Scenes/`, `Tests/`).

## Implemented (exists in code now)
- **Core/run state:** `Core/RunStateController.cs` (owns Sanity, Fugue), `Core/GameManager.cs`, `Core/ShiftManager.cs`, `Core/TideSystem.cs`, `Core/RunStateController` + `Persistence/RunData.cs`, `Persistence/MetaProgressData.cs`. Compliance Vows system under `Core/ComplianceVows/`.
- **BSM:** `BSM/` — `ClientStateMachine`, `ClientStateStack`, `ClientContext`, `States/`, `Transitions/`. (9 client states.)
- **Red Tape Engine / BT:** `BehaviourTrees/` — `BehaviourTree`, `BTNode`, `BTStatus`, `BTContext`, `MutationEngine` (runtime node injection), `Nodes/`. `RedTape/` — `StateInjector`, `PunchCardMachine`, `CardFatigueTracker`.
- **Event bus:** `Narrative/RumorMillEventBus.cs` + `RumorMillEvents.cs` (`SanityChangedEvent`, etc.).
- **Audio (FMOD-shaped, gated):** `Audio/` — `FMODManager`, `BinauralStressEngine`, `ProceduralJazzGenerator`, `StressCrescendo`, `SpatialAudioThreatSystem`, `AudioSettings`, and **`DistortionAudioDirector`** (added with this pack — discrete tier stingers + Fugue/Mercy/Flow snapshots, event-bus driven). All behind `#if DESK42_FMOD`.
- **Other systems present:** `MoralInjury/`, `Archetypes/` (+ `Archetypes/Archetypes/` concrete archetypes), `Cards/`, `Claims/`, `Encounter/`, `Economy/`, `Synergy/`, `OfficeSupplies/`, `Meta/*` (Achievements, Analytics, BossFight, DataSmuggling, RetirementFund), `Leaderboard/`, `Accessibility/`, `Tutorial/`, `UI/` (`CorpOSWindowManager`, `PneumaticTube`, panels), `Editor/` + `EditorTools/`.
- Tests exist under `Tests/EditMode` and `Tests/PlayMode`.

## Tooling state (this pack)
- **Claude skill pack + root `CLAUDE.md` + `.claudeignore`:** ✅ installed (this pack), corrected to the live repo.
- **Coplay "MCP for Unity":** ✅ **live and confirmed** — smoke tested 2026-07-18 (created/deleted SmokeTest_RedCube in Boot.unity via MCP). (see `mcp-setup.md`).
- **FMOD:** code scaffolded ✅; **plugin NOT imported, `DESK42_FMOD` OFF.** No FMOD Studio project/banks yet. (see `fmod-integration.md`).
- **ComfyUI MCP:** ✅ **live and confirmed** — smoke tested 2026-07-18 (coffee_mug_smoke_test.png generated via MCP into Art/Sprites, now deleted). ComfyUI 0.24.1, PyTorch 2.5.1+cu121, RTX 3060 Ti. Models in `C:\Users\jacob\ComfyUI-Shared\models\`. Checkpoints: Juggernaut-XL_v9, RealVisXL_V5.0_fp16, sd_xl_base_1.0. (see `comfy-integration.md`).

## Unknown / to confirm in repo
- Compile status with current scene edits (`Shift.unity` shows as modified in git).
- Exact persistence serializer/format (`Persistence/`).
- Which terms are dials vs. global meters (Sanity / Moral Injury / Cognitive Budget).
- Live test pass rate.
- How much desk scene/prefab wiring is complete vs. stubbed.

## Next up (the remaining integration work — checklists live in the linked docs)
1. **FMOD:** import plugin -> add `FMODUnity` ref to `Desk42.Core.asmdef` -> define `DESK42_FMOD` -> author Studio project to the code's event/bus/param names -> banks load-at-init -> Play-mode Sanity smoke test. (`fmod-integration.md`)
2. **First real art generation:** use ComfyUI MCP to generate a tiered prop set (e.g. coffee mug `_t0.._t4` + cutouts) following `comfy-integration.md` conventions. ControlNet workflow with `controlnet-union-sdxl-promax` is the validated path for specific props.
3. Toward Steam Early Access.
