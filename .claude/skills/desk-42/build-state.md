# Build state — Desk 42  (keep this current)

Single source of truth for "what actually exists right now." Update it as things land — this is what lets an agent skip re-discovering the project every session. **Reconciled against the live repo 2026-07-26 (branch `main` @ `da3f76b` + the environment-reset fix, tag `five-shift-proof-v0.3-validated`).**

## Branch topology (2026-07-26)
- **One shippable line: `main`.** `main`, `origin/main`, and annotated tag `five-shift-proof-v0.3-validated` all point at the validated five-shift proof spine.
- The old parallel branches (`feat/cli-tooling`, `feat/cocktail-greenfield`, `feat/ff-resolution`, `codex/MCP`, `codex/recover-visual-identity`, `playtest/session-001`) were consolidated into `main` and deleted. Their pre-consolidation tips are preserved as `backup/*` tags — **deleted work is recoverable**.
- `visa/gate3-desk42-validation-DO-NOT-MERGE` is a deliberately divergent evidence branch (screenshots, build logs, activity CSVs). Not for merging.
- Two historical worktree folders under `Desk42-worktrees/` remain on disk in detached HEAD (Windows denied deletion). They are inert and represent no active branch.

## Stack (verified 2026-07-26)
- Unity **2022.3.62f3** LTS, **URP 14.0.12**, **Input System 1.14.2**, Newtonsoft JSON, Addressables, Cinemachine, TextMeshPro, Steamworks (openupm).
- **197 C# scripts under `Assets/_Project/Scripts/`** (~40.5k LOC runtime) **+ 26 test scripts** (22 EditMode, 4 PlayMode). Top-level script folders: 24. Runtime = single asmdef `Assets/_Project/Scripts/Desk42.Core.asmdef`; tests `Desk42.Tests.EditMode` / `.PlayMode`.
- Project under `Assets/_Project/` (`Scripts/`, `ScriptableObjects/`, `Prefabs/`, `Art/`, `Audio/`, `Scenes/`, `Tests/`).

## Compile status (verified 2026-07-26, batchmode on `main`)
- **0 errors.** 2 known assembly warnings (reported from the editor build; not reproduced in the headless test log).
- ~~`Desk42.Tests.PlayMode.asmdef` has zero test files~~ → **resolved.** PlayMode now holds 4 fixtures / 13 tests.
- ~~`ATBEdgeCaseTests.cs` + `CascadePresenterTests.cs` stranded at Tests root~~ → **resolved.** Both relocated into `Tests/EditMode/`, inside the test asmdef.

## Scene inventory (verified from scene YAML, 2026-07-21)
- 4 scenes, all enabled in Build Settings, order: **Boot → MainMenu → Shift → InternalAudit**.
- **`Shift.unity` is the playable shift scene**: 79 GameObjects, ~40 project MonoBehaviours wired, incl. `ShiftManager`, `EncounterManager`, `ProceduralClientGenerator`, `ClaimPanelView`, `CardHandView`, `CardSlamFeedback`, `PunchCardMachine`, `PassiveAggressiveUIController`, `TutorialController`, `MoralDilemmaPanel`, `EnvironmentalDistortion`, `DeskEntropyRenderer`, `TellVisualIndicator`, and all four audio drivers (`BinauralStressEngine`, `StressCrescendo`, `ProceduralJazzGenerator`, `SpatialAudioThreatSystem`).
- **ShiftManager serialized wiring (evidence, scene YAML):** `_claimTemplates` = 8 assets (matches 8 in `ScriptableObjects/ClaimTemplates/`), `_anomalyTags` = 12 assets (matches 12 in `ScriptableObjects/AnomalyTags/`), `_morningBlockQuota: 3`, `_afternoonBlockQuota: 4`, `_lunchBreakDuration: 60`, `_overtimeDuration: 300`, `_overtimeTimerReduction: 30`, `_overtimeTimerFloor: 60`, `_maxOvertimeIterations: 3`, `_uiController` → PassiveAggressiveUIController instance.
- **`ClientVisualCatalog` now exists** — `UI/ClientVisualCatalog.cs`, covered by `Tests/EditMode/ClientVisualCatalogTests.cs`. (This entry previously said it did not exist; that was true at `a758359` and is no longer.) Client visuals still also flow through `Encounter/ProceduralClientGenerator` + `Clients/Species/` + `Clients/Traits/`.

## Determinism / seeds (verified)
- `Core/SeedEngine/SeedEngine.cs`: static, stream-partitioned (`SeedStream` enum: CardDraft, RumorMillEvents, ClientBehaviourTree, MoralDilemma, FormCorruption, AudioVariation, …), FNV-1a derived per-stream seeds, 6-char share codes. `RunStateController.BeginNewRun(masterSeed, …)` inits it → **fixed-seed reproducible runs are supported end-to-end.**
- Dev CLI (editor/dev builds only, `#if UNITY_EDITOR || DEVELOPMENT_BUILD`): `Debug/Desk42CLI.cs` command router + `BsmCliTool`, `EntropyCliTool` (from cascade merge). Drivable via MCP `execute_code`.

## Tests (measured 2026-07-26, headless batchmode)
- **EditMode: 194 total — 188 passed, 0 failed, 6 skipped** (22 files).
- **PlayMode: 13 total — 13 passed, 0 failed** (4 files: `ShiftPlaythrough`, `EliasFiveShiftRoutePlayModeTests`, `EliasProofSessionPlayModeTests`, `PunchCardMachinePlayModeTests`).
- Run either headlessly (Unity Editor must be closed — batchmode needs the project lock):
  ```
  powershell -NoProfile -ExecutionPolicy Bypass -File "tools\Run-EditModeAll.ps1" -TestPlatform EditMode
  powershell -NoProfile -ExecutionPolicy Bypass -File "tools\Run-EditModeAll.ps1" -TestPlatform PlayMode
  ```
  Results land in `TestResults/<platform>-full.{xml,log}`. `tools/Run-StateMachineSynergyTests.ps1` remains the fast BSM+Synergy-only loop.
- **Batchmode side effect:** a headless run dirties `ProjectSettings/ProjectSettings.asset` (line endings only) and regenerates the TMP `LiberationSans SDF - Fallback.asset` glyph atlas (~650 lines). Both are incidental — check them before committing.

## Known code-level nits (log, don't fix mid-playtest)
- Typo `OfficeSuppplies` (3 p's) in **both** the SO folder name and `RunData.OfficeSuppplies` field.
- Typo `ImpatenceTimerRemaining` in `RunData`.
- `Synergy/` script folder is still empty (SynergyResolver actually lives in `OfficeSupplies/`).
- 16 `// TODO` markers, concentrated in **player-facing feedback**: PunchCardMachine card animations/shake/glow, GameManager scene-fade, MutationEngine passive registration, DistortionAudioDirector Tide hooks.
- **Open design debt — the Compliance Streak pays nothing.** `RunData.ComboMultiplier` builds on approvals and is displayed mid-shift by `ClientView` / `CascadePresenter`, but no credit path multiplies by it; its only consumer is the end-of-run score (`RunStateController.ComputeFinalScore`). Pre-`a758359` it multiplied claim payout. Deferred deliberately on 2026-07-26 — decide whether to reconnect it to credits or relabel/move the mid-shift UI. The cross-claim and sequential-synergy bonuses are flat pending that call.
- `OfficeSupplyEffectBase` defaults `Preview* => Modify*` — a **fail-open** default. Correct today (the only stateful effects, `StaplerEffect` and `RubberStampEffect`, both override `PreviewCreditCost`), but the next stateful effect will silently mutate supply state during card preview unless its author remembers to override.

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
- ~~Live test pass rate~~ → **measured 2026-07-26** (see Tests above).
- **~4,000 lines of presentation code have never been reviewed:** `ShiftFeedbackOverlay`, `CardSlamFeedback`, `PixelRoomStageOverlay`, `EliasProcedurePanel`, `EliasProcedureReceiptPresenter`, `CascadePresenter`. Largest remaining blind spot in the codebase.

## Next up (the remaining integration work — checklists live in the linked docs)
1. **FMOD:** import plugin -> add `FMODUnity` ref to `Desk42.Core.asmdef` -> define `DESK42_FMOD` -> author Studio project to the code's event/bus/param names -> banks load-at-init -> Play-mode Sanity smoke test. (`fmod-integration.md`)
2. **First real art generation:** use ComfyUI MCP to generate a tiered prop set (e.g. coffee mug `_t0.._t4` + cutouts) following `comfy-integration.md` conventions. ControlNet workflow with `controlnet-union-sdxl-promax` is the validated path for specific props.
3. Toward Steam Early Access.
