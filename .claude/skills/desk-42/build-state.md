# Build state — Desk 42  (keep this current)

Single source of truth for "what actually exists right now." Update it as things land — this is what lets an agent skip re-discovering the project every session. **Reconciled against the live repo 2026-07-21 (playtest session 001, branch `playtest/session-001` @ consolidation merge `a758359`).**

## Stack (verified 2026-07-21)
- Unity **2022.3.62f3** LTS, **URP 14.0.12**, **Input System 1.14.2**, Newtonsoft JSON, Addressables, Cinemachine, TextMeshPro, Steamworks (openupm).
- **174 C# scripts under `Assets/_Project/Scripts/` + 9 test scripts** (~31.5k LOC runtime). Top-level script folders: 26 (the 24 `Desk42.*` namespaces plus `Debug/` dev-CLI and `Modes/`, both from the cascade/cli merges). Runtime = single asmdef `Assets/_Project/Scripts/Desk42.Core.asmdef`; tests `Desk42.Tests.EditMode` / `.PlayMode`.
- Project under `Assets/_Project/` (`Scripts/`, `ScriptableObjects/`, `Prefabs/`, `Art/`, `Audio/`, `Scenes/`, `Tests/`).

## Compile status (verified 2026-07-21, editor boot on `a758359`)
- **0 errors, 0 CS warnings** in Editor.log for the full script compilation.
- One editor notice: "assemblies without any scripts" — `Desk42.Tests.PlayMode.asmdef` exists but its folder contains **zero test files**.
- `Assets/_Project/Tests/ATBEdgeCaseTests.cs` + `CascadePresenterTests.cs` sit at Tests **root**, outside both test asmdefs → they compile into Assembly-CSharp. All their cases are empty `[Test, Explicit]` stubs (spec-as-tests from the cascade branch). They compile today but are invisible to the normal test assemblies — relocate or implement.

## Scene inventory (verified from scene YAML, 2026-07-21)
- 4 scenes, all enabled in Build Settings, order: **Boot → MainMenu → Shift → InternalAudit**.
- **`Shift.unity` is the playable shift scene**: 79 GameObjects, ~40 project MonoBehaviours wired, incl. `ShiftManager`, `EncounterManager`, `ProceduralClientGenerator`, `ClaimPanelView`, `CardHandView`, `CardSlamFeedback`, `PunchCardMachine`, `PassiveAggressiveUIController`, `TutorialController`, `MoralDilemmaPanel`, `EnvironmentalDistortion`, `DeskEntropyRenderer`, `TellVisualIndicator`, and all four audio drivers (`BinauralStressEngine`, `StressCrescendo`, `ProceduralJazzGenerator`, `SpatialAudioThreatSystem`).
- **ShiftManager serialized wiring (evidence, scene YAML):** `_claimTemplates` = 8 assets (matches 8 in `ScriptableObjects/ClaimTemplates/`), `_anomalyTags` = 12 assets (matches 12 in `ScriptableObjects/AnomalyTags/`), `_morningBlockQuota: 3`, `_afternoonBlockQuota: 4`, `_lunchBreakDuration: 60`, `_overtimeDuration: 300`, `_overtimeTimerReduction: 30`, `_overtimeTimerFloor: 60`, `_maxOvertimeIterations: 3`, `_uiController` → PassiveAggressiveUIController instance.
- **`ClientVisualCatalog` does not exist anywhere in the repo** (stale doc/handoff reference). Client visuals come from `Encounter/ProceduralClientGenerator` + `Clients/Species/` + `Clients/Traits/`.

## Determinism / seeds (verified)
- `Core/SeedEngine/SeedEngine.cs`: static, stream-partitioned (`SeedStream` enum: CardDraft, RumorMillEvents, ClientBehaviourTree, MoralDilemma, FormCorruption, AudioVariation, …), FNV-1a derived per-stream seeds, 6-char share codes. `RunStateController.BeginNewRun(masterSeed, …)` inits it → **fixed-seed reproducible runs are supported end-to-end.**
- Dev CLI (editor/dev builds only, `#if UNITY_EDITOR || DEVELOPMENT_BUILD`): `Debug/Desk42CLI.cs` command router + `BsmCliTool`, `EntropyCliTool` (from cascade merge). Drivable via MCP `execute_code`.

## Tests (state 2026-07-21)
- **EditMode (7 files):** BehaviourTreeTests, BSMTests, DeskEntropyTierTests, ExpenseUnmetEventTests, SaveSystemTests, SeedEngineTests, SynergyResolverTests. Pass rate not yet measured this session (blocked on bridge / editor test run — see below).
- **PlayMode: zero tests** (asmdef only).

## Known code-level nits (log, don't fix mid-playtest)
- Typo `OfficeSuppplies` (3 p's) in **both** the SO folder name and `RunData.OfficeSuppplies` field.
- Typo `ImpatenceTimerRemaining` in `RunData`.
- `Synergy/` script folder is empty (SynergyResolver actually lives in `OfficeSupplies/`).
- 16 `// TODO` markers, concentrated in **player-facing feedback**: PunchCardMachine card animations/shake/glow, GameManager scene-fade, MutationEngine passive registration, DistortionAudioDirector Tide hooks.

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
- **Coplay "MCP for Unity":** ⚠️ **works, but not hands-free.** Smoke tested 2026-07-18. Architecture (verified 2026-07-21): a standalone Python server (`mcpforunityserver` 10.0.0 via uvx, HTTP `127.0.0.1:8080/mcp`) + an editor-side plugin session that must connect into it. The plugin's **Auto-Start on Editor Load is OFF by default**, so after any editor restart someone must click **Window → MCP for Unity → Start/Connect** (or enable the auto-start toggle in Advanced Settings) before agents can drive the editor. The server itself can be started externally: `uvx --from mcpforunityserver==10.0.0 mcp-for-unity --transport http --http-host 127.0.0.1 --http-port 8080`. (see `mcp-setup.md`).
- **FMOD:** code scaffolded ✅; **plugin NOT imported, `DESK42_FMOD` OFF.** No FMOD Studio project/banks yet. (see `fmod-integration.md`).
- **ComfyUI MCP:** ✅ **live and confirmed** — smoke tested 2026-07-18 (coffee_mug_smoke_test.png generated via MCP into Art/Sprites, now deleted). ComfyUI 0.24.1, PyTorch 2.5.1+cu121, RTX 3060 Ti. Models in `C:\Users\jacob\ComfyUI-Shared\models\`. Checkpoints: Juggernaut-XL_v9, RealVisXL_V5.0_fp16, sd_xl_base_1.0. (see `comfy-integration.md`).

## Unknown / to confirm in repo
- ~~Compile status~~ → **confirmed clean 2026-07-21** (see Compile status above).
- ~~How much desk scene/prefab wiring is complete vs. stubbed~~ → **Shift.unity wiring confirmed** (see Scene inventory). Prefab-level pass still shallow.
- ~~Exact persistence serializer/format~~ → **confirmed 2026-07-21:** Newtonsoft `JsonConvert` with custom settings, UTF-8, atomic write via temp file then move (`Persistence/SaveSystem.cs:156-164`).
- Which terms are dials vs. global meters (Sanity / Moral Injury / Cognitive Budget) — `RunData` carries `Sanity` **and** `SoulIntegrity` (both 0–100); MoralInjury is its own system. Full reconciliation still open.
- Live test pass rate (EditMode) — pending an editor test run this session.

## Next up (the remaining integration work — checklists live in the linked docs)
1. **FMOD:** import plugin -> add `FMODUnity` ref to `Desk42.Core.asmdef` -> define `DESK42_FMOD` -> author Studio project to the code's event/bus/param names -> banks load-at-init -> Play-mode Sanity smoke test. (`fmod-integration.md`)
2. **First real art generation:** use ComfyUI MCP to generate a tiered prop set (e.g. coffee mug `_t0.._t4` + cutouts) following `comfy-integration.md` conventions. ControlNet workflow with `controlnet-union-sdxl-promax` is the validated path for specific props.
3. Toward Steam Early Access.
