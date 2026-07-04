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
- **Coplay "MCP for Unity":** dependency added to `Packages/manifest.json` ✅; **not yet activated** — Jacob must open Unity and run *Configure All Detected Clients* (see `mcp-setup.md`).
- **FMOD:** code scaffolded ✅; **plugin NOT imported, `DESK42_FMOD` OFF.** No FMOD Studio project/banks yet. (see `fmod-integration.md`).
- **ComfyUI:** skill authored ✅; pipeline **validated end-to-end 2026-06-13** — SDXL base driven via the HTTP API produced a 5-tier Distortion-Scale ladder + birefnet transparent cutouts into `Assets/_Project/Art/Sprites` (`stamp_t0..t4[_cutout].png`). Install/launch in the ComfyUI memory note. **No MCP server registered yet** (drove it via the raw `/prompt` API; the MCP route in `comfy-mcp-setup.md` is the next-session upgrade).

## Unknown / to confirm in repo
- Compile status with current scene edits (`Shift.unity` shows as modified in git).
- Exact persistence serializer/format (`Persistence/`).
- Which terms are dials vs. global meters (Sanity / Moral Injury / Cognitive Budget).
- Live test pass rate.
- How much desk scene/prefab wiring is complete vs. stubbed.

## Next up (the remaining integration work — checklists live in the linked docs)
1. **MCP:** open Unity -> *Window -> MCP for Unity -> Configure All Detected Clients* -> smoke test "create a red cube." (`mcp-setup.md`)
2. **FMOD:** import plugin -> add `FMODUnity` ref to `Desk42.Core.asmdef` -> define `DESK42_FMOD` -> author Studio project to the code's event/bus/param names -> banks load-at-init -> Play-mode Sanity smoke test. (`fmod-integration.md`)
3. **ComfyUI:** stand up ComfyUI (API on `127.0.0.1:8188`) -> register a ComfyUI MCP server alongside Unity -> generate one tiered test asset into `Art/Sprites`. (`comfy-mcp-setup.md`)
4. Toward Steam Early Access.
