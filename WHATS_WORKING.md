# Desk 42 — What's Working

> Status snapshot of implemented systems. Generated 2026-06-18 from a read of
> `Assets/_Project/Scripts` (~153 runtime scripts). "Working" = wired into the
> live game loop; "Scaffolding" = present and compiling but stubbed / behind a
> backend / not yet exercised in normal play.

---

## Core architecture (solid)

| System | File | What it does |
|---|---|---|
| **GameManager** | `Core/GameManager.cs` | Root `DontDestroyOnLoad` singleton. Boots meta, owns the scene-flow state machine, spawns all child systems, commits run results to meta on shift end. |
| **Service locator** | `Core/Desk42Services.cs` | Type-keyed registry replacing `FindObjectOfType`. `Register/Get/TryGet`, auto-clears on Editor Play. |
| **RunStateController** | `Core/RunStateController.cs` | Single source of truth for an active run. Sanity / Soul Integrity / Credits / phase / timer / entropy / factions / NDAs, plus auto-save and event publishing. |
| **RumorMill event bus** | `Narrative/RumorMillEventBus.cs`, `RumorMillEvents.cs` | Pub/sub backbone. Immediate + deferred publish, queue flushed on scene transition to avoid firing against dead objects. |
| **ShiftManager** | `Core/ShiftManager.cs` | Orchestrates the per-shift loop (see below). |
| **SaveSystem / persistence** | `Persistence/SaveSystem.cs`, `RunData.cs`, `MetaProgressData.cs` | Mid-run auto-save, meta-progress load + migration, deck/supply serialization round-trip. |
| **SeedEngine** | `Core/SeedEngine/SeedEngine.cs`, `SeedSerializer.cs` | Deterministic seeded RNG with named streams; share-code parsing for seeded runs. |

**Scene flow:** `Boot → MainMenu → Shift → InternalAudit`, all async with queue
flush between loads. (Logo splash and fade transitions are still `TODO` stubs.)

---

## The shift loop (working)

`ShiftManager` drives the full cycle:

- **Phases:** `ClockIn → MorningBlock → LunchBreak → AfternoonBlock → ClockOut`,
  with `Overtime` inserted on timer expiry.
- **Ante / quota system:** morning & afternoon blocks each have an independent
  claim quota that scales with `GlobalShiftNumber` and active Vows.
- **Claim queue:** generated via `ClaimGenerator` from `ClaimTemplateData` +
  `AnomalyTagData` SOs; refills on demand, hard-capped at 30 claims/shift.
- **Impatience timer:** per-frame tick with Office Clock slowdown, grace period,
  and Vow modifiers (Hostile Environment, Agile Workflow freeze, Mandatory OT).
- **Unpaid Overtime loop** (shift ≥ 5): instead of ending, loops back into a
  harder MorningBlock with escalated Tide and a shrinking timer, capped at 3
  iterations.
- **TideSystem** (`Core/TideSystem.cs`): tracks pressure from encounter pacing.
- **Sequential Synergy:** flat credit bonus when consecutive claims share a tag
  category.
- **Memo generation:** resolved claims can spawn conspiracy-board fragments.
- **Fugue state:** Sanity hitting 0 ends the run.

---

## Run-shaping systems (working)

- **Archetypes** (`Archetypes/`, 11 implemented): Auditor, Bureaucrat, Intern,
  Consultant, Gaslighter, HR Rep, IT Person, Middle Manager, Union Rep,
  Whistleblower, Archivist. Each defines starting deck, hand size, draws/turn,
  and `OnRunStart` / `OnCardSlammed` hooks via `ArchetypeFactory`.
- **Cards & deck** (`Cards/`, `RedTape/`): `PunchCardData`, `Deck`, `Hand`,
  `CardDraftSystem`, `CardLibrary`; `PunchCardMachine`, `StateInjector`,
  `CardFatigueTracker` (fatigue / jam / crumple persisted per card).
- **Office Supplies** (`OfficeSupplies/`): registry, instances, `SynergyResolver`,
  Ship-Tier supply set, timer/grace-period effects. Serialized into saves.
- **Compliance Vows** (`Core/ComplianceVows/`): rule-mutation registry that
  modifies quotas, timer behavior, soul damage; includes ClickCostInterceptor
  and InternCompanion.
- **Moral Injury** (`MoralInjury/`): dilemma SOs, soul-cap scarring that
  persists into meta across runs.
- **Factions:** seed-based starting dispositions, reputation shifts published
  as events.

---

## AI / client behavior (working)

- **Client State Machine** (`BSM/`): `ClientStateMachine`, state stack,
  transition table/rules, `ClientTellSystem`.
- **Behaviour Trees** (`BehaviourTrees/`): composite/decorator/leaf nodes,
  `BehaviourTree` runner, and a `MutationEngine` for evolving client behavior.
- **Encounter** (`Encounter/`): `EncounterManager`, `ProceduralClientGenerator`.

---

## Meta progression (working)

- **MilestoneTracker** (`Core/MilestoneTracker.cs`): centralized ending-condition
  evaluation on run completion.
- **Retirement Fund** (`Meta/RetirementFund/`): awards AuditPoints on run end,
  spent in the Retirement Lounge hub panel.
- **Onboarding phase gating** (`GameManager.PhaseLevel`, 0–4): drip-feeds
  mechanics — no sanity/soul loss before Phase 3, no timer on Run 1; advances
  one phase per completed run; Middle Manager archetype bypasses to full.
- **Data Smuggling / paradox recipes** (`Meta/DataSmuggling/`): exe-fragment
  drops, `ParadoxRecipeDetector`, Rogue Subroutine pixel hunt.
- **Boss fight** (`Meta/BossFight/TaskMgrOverride.cs`, `UI/BossFightController.cs`):
  "Crash to Win" Task Manager override + NG+ menu state.

---

## UI (extensive — `UI/`, 39 scripts)

Working desk + corp-OS interface: `CorpOSWindowManager`, `ClaimPanelView`,
`ClientView`, `CardHandView` / `CardButtonView` / `CardSlamFeedback`,
`ArchetypePickerPanel`, `VowPickerPanel`, `MoralDilemmaPanel`, `RunSummaryPanel`,
`MainMenuController`, `InternalAuditMetaHubUI`, `ConspiracyBoardUI`,
`RetirementLoungePanel`, `MemoFeedUI`, `NotificationFeed`, `NarratorSystem`.

Atmosphere/anti-UI effects: `EntropyManager` + driver, `DeskEntropyRenderer`,
`PassiveAggressiveUIController`, `HRPopupSpammer`, `RunawayLogoutButton`,
`FugueInputRandomizer`, `EnvironmentalDistortion`, `FormCorruptionEffect`,
`NDAOverlayRenderer`, `RAMOverloadMeter`, `PneumaticTube`, `StaplerTool`.

UX guards (recent): `FeedbackBudget` throttle, clutter killswitch, Reset
Onboarding.

---

## Audio (`Audio/`)

`FMODManager`, `AudioSettings` (volume + UI), `BinauralStressEngine`,
`StressCrescendo`, `SpatialAudioThreatSystem`, `ProceduralJazzGenerator`.
*Functional in code; FMOD wiring depends on the FMOD skill pack being activated.*

---

## Accessibility & onboarding

- `Accessibility/`: `AccessibilitySettings` (text scale wired into runtime UI
  builders), `UIPalette` token system + HighContrast palette.
- `Tutorial/TutorialController.cs`: diegetic onboarding.

---

## Scaffolding / behind a backend (compiles, not exercised in normal play)

| System | Default state |
|---|---|
| **Analytics** (`Meta/Analytics/`) | No-op backend by default; local-log backend in dev. Remote backend must be injected via `Analytics.SetBackend`. |
| **Achievements** (`Meta/Achievements/`) | Local store in dev; Steam backend only when `DESK42_STEAM` is defined. |
| **Leaderboards** (`Leaderboard/`) | `ILeaderboardProvider` with PlayFab + Steam implementations; auto-submit only on Daily Brief runs. |
| **Steam integration** | Behind `DESK42_STEAM`; scaffolding committed (`Phase 9`). |
| Scene transitions / studio logo | `TODO` stubs in `GameManager.LoadSceneAsync`. |

---

## Known recent fixes (from git log)

- Claim counter off-by-one between blocks — fixed.
- Claim-2 deadlock + picker grid overflow — fixed.
- Safe-mode compile errors from Sprints 3–4 — fixed.
- Bucket 1 refactor: service locator, phase API, event-bus hardening — landed.

---

*Editor tooling lives in `Scripts/Editor/` (scene auto-layout, fixers, prefab
rebuilders) and is dev-only.*
