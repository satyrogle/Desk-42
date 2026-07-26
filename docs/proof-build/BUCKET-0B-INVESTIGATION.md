# Bucket 0B — read-only investigation baseline

**Branch:** `feat/proof-build` · **Base commit:** `a432a8b` · **Date:** 2026-07-26
**Status:** complete, no code changed during investigation
**Authority:** `Desk42_Claude_Code_Handoff.md` §2

This is the frozen baseline the proof-build work is implemented against. Every claim below
carries file/line evidence. Where the handoff names a symbol that does not exist, that is
recorded as a contradiction rather than silently reinterpreted (handoff §1.3).

---

## B1 — Sanity / Cognitive Budget / Moral Injury reconciliation

```text
Authoritative value:    RunData.Sanity (float)
Authoritative writer:   RunStateController.ModifySanity — Core/RunStateController.cs:164-180
                        SOLE mutator. Every call site routes through it; no direct
                        _data.Sanity assignment exists outside this method.
Authoritative readers:  RunStateController.Sanity :132, IsInFugueState :158,
                        ComputeEfficiencyRating :757, Stats.LowestSanity :533,
                        SanityChangedEvent subscribers (5 audio directors, achievements,
                        analytics, ShiftManager)
Derived values:         IsInFugueState = Sanity <= 0f  (:158)
                        Stats.LowestSanity  (:533)
                        SanityChangedEvent(prev, curr, triggeredFugue)  (:173)
Duplicate/conflicting:  NONE for Sanity. SoulIntegrity is a separate 0-100 meter with its
                        own mutator (:185). Moral Injury is a third independent system
                        operating on Soul, not Sanity.
Runtime range:          0-100, clamped in ProjectSanityDelta :206
Observed starting:      100f — BeginNewRun :64
Recovery paths:         MoralChoice Empathy Inversion +15 (:668)
                        Archetype OnRunStart: Intern +5, UnionRep +15
                        Supplies: CoffeeMug per-tick; +5/+15/+30 refunds (ShipTierSupplies)
Damage paths:           Hazards: MandatoryMeeting -15, PrinterJam -5, SystemCrash -30 (:698-707)
                        Claim resolution: 3f + up to 2 anomalies
                          (ClaimResolutionConsequencePolicy.cs:55)
                        ATB overflow (RedTape/StateInjector.cs:272)
                        Zero-Based Budgeting vow (StateInjector.cs:331)
                        ClickCostInterceptor -cost*2 (:79)
                        TheConsultant -50 at run start
Unresolved contradictions:  3 (below)
```

### Contradiction 1 — `ISanityProvider` does not exist

Zero occurrences repo-wide. There is no provider abstraction; consumers read
`RunStateController` directly or subscribe to `SanityChangedEvent`.

**Consequence:** D2 cannot "wire `ISanityProvider`" — it would have to be created.
Per the product owner's Bucket 1 constraints, introducing it is explicitly forbidden.
Confidence: certain.

### Contradiction 2 — "Cognitive Budget" is not a runtime value

Appears only as narrator flavour text (`UI/NarratorSystem.cs:102-105`) and one comment
(`Narrative/RumorMillEvents.cs:261` — *"Sanity / Cognitive Budget changed"*).

**Consequence:** it is a diegetic label for Sanity, not a second meter. There is no second
authority to reconcile. Confidence: certain.

### Contradiction 3 — Sanity damage is phase-gated

`ProjectSanityDelta` (`Core/RunStateController.cs:202-204`) returns `0f` for **all negative
deltas** unless Phase >= 3.

Phase resolves as `Meta.HighestPhaseReached ?? 4` (`Core/GameManager.cs:268`) and advances
+1 per completed run (`Core/RunStateController.cs:590-592`).

- A **fresh save** has `HighestPhaseReached = null` -> Phase 4 -> damage active.
- The only thing setting Phase 1 is the *Reset Onboarding* debug action
  (`UI/MainMenuController.cs:303`), whose comment documents the intended ladder:
  Phase 2 Impatience, **Phase 3 Soul/Sanity**, Phase 4 Entropy + audio.

**Consequence:** the onboarding drip-feed is inert for genuinely new players — they get the
full game immediately. More importantly for the proof build: **any tester profile that has
been through Reset Onboarding has Sanity pinned at 100**, and continuous audio would have
nothing to drive. This is a product decision, not an engineering one.

---

## B2 — Reachability audit

### `EnterFugue`

```text
Declared:              Audio/DistortionAudioDirector.cs:147
Caller(s):             :91 — SanityChangedEvent handler, on e.TriggeredFugue
Indirect trigger path: ModifySanity :172 sets triggeredFugue when prev > 0 && curr <= 0
Reachable Shifts 1-5:  yes in principle — see B3 (Sanity 0 observed)
Reachable normal play: mechanically yes; human-frequency unproven
Editor/test-only:      no
Dead/unreachable:      NO — but body is #if DESK42_FMOD (:151-155), so it is a
                       runtime NO-OP today. Call site itself is ungated.
Evidence:              9x "TriggeredFugue=True" across PlaytestLogs/
```

### `EnterMercyWindow`

```text
Declared:              Audio/DistortionAudioDirector.cs:168 (ExitMercyWindow :177)
Caller(s):             NONE. Zero callers repo-wide.
Indirect trigger path: none
Reachable Shifts 1-5:  no
Reachable normal play: no
Editor/test-only:      no
Dead/unreachable:      YES
Evidence:              File header :29-30 states the gap explicitly —
                       "TideSystem should call EnterMercyWindow()/ExitMercyWindow()
                        (and EnterFlow()/ExitFlow()) on Ebb / Flow.
                        // TODO(wire): hook these from Core/TideSystem."
```

**Directive consequence (handoff §5 D2):** do not author Mercy/Flow FMOD states.
Same applies to `EnterFlow`/`ExitFlow`.

### `CascadePresenter`

```text
Declared:              UI/CascadePresenter.cs:25
Caller(s):             UI/ShiftFeedbackOverlay.cs:135-138 (TryBindCascadePresenter)
Indirect trigger path: StateInjector publishes CardCascadeResolvedEvent -> presenter renders
Reachable Shifts 1-5:  yes
Reachable normal play: yes — present in Assets/_Project/Scenes/Shift.unity
Editor/test-only:      no (also auto-created by ShiftSceneFixer / ShiftSceneAutoLayout,
                       which are editor convenience, not the runtime path)
Dead/unreachable:      no
```

### `ExpenseUnmetEvent`

```text
Declared:              Narrative/RumorMillEvents.cs:399
Publisher:             Core/PersonalExpenseGenerator.cs:103
Subscriber:            Core/RunStateController.cs:711 (HandleExpenseUnmet)
Indirect trigger path: unpaid expenses evaluated before CompleteRun
Reachable Shifts 1-5:  yes
Reachable normal play: yes
Editor/test-only:      no
Dead/unreachable:      no
Note:                  handler applies ENTROPY ONLY — no Sanity effect, despite the
                       name implying stress. :715
```

---

## B3 — Sanity range, Shifts 1-5

No human continuous-play telemetry exists. The evidence below is harness output already
present in `PlaytestLogs/` (495 files), mined read-only.

| Log | Sanity events | Min reached | Fugue triggers |
|---|---|---|---|
| `final-four-shifts-clean` | 4 | 80.0 | 0 |
| `final-four-shifts-headless` | 45 | **0.0** | 1 |
| `final-playmode-all` | 45 | **0.0** | 1 |

Repo-wide: `TriggeredFugue=True` occurs **9 times**.

**Verified not artificial:** `GameplayScreenshotBootstrap` — which can force Sanity directly
via `Debug/GameplayScreenshotBootstrap.cs:131` — appears in none of these logs. The drain
came through genuine mechanics; the harness invokes the real `MoralDilemmaPanel` handlers
by reflection (`Tests/PlayMode/ShiftPlaythrough.cs`).

**Conclusion:** Sanity is live, starts at 100, and 0 is reachable through real gameplay
under aggressive automated play. Whether a *human* crosses tier thresholds in Shifts 1-5 is
**not established** — the one clean four-shift log bottomed out at 80 (Tier-1 only).

**Confidence: moderate.** A definitive answer requires an instrumented continuous run,
which is a code change and was therefore out of scope for B1.

---

## B4 — Efficiency Rating trace

```text
ComputeEfficiencyRating()   Core/RunStateController.cs:743
  -> written once to Stats.EfficiencyRating in CompleteRun :585
  -> UI/RunSummaryPanel.cs:197                 (display)
  -> Core/MilestoneTracker.cs:84               (>=1500 milestone)
  -> Core/RunStateController.cs:607,609        (ending selection)
  -> Leaderboard/LeaderboardManager.cs:144,147 (SCORE SUBMISSION)
  -> Leaderboard/PlayFabLeaderboardProvider.cs (statistic name "EfficiencyRating")
  -> Meta/Analytics/AnalyticsBootstrap.cs:181  (telemetry)
  -> RunCompletedEvent (Narrative/RumorMillEvents.cs:466)
```

**Is `RunSummaryPanel` used by the main Campaign?** Yes — present in `Shift.unity`, shown at
every run completion, with no mode branch around it.

**Is Efficiency Rating shown in Campaign today?** Yes, unconditionally, at `:197`.

**Which score modes legitimately use it?** *Cannot be answered cleanly — this blocks the
proposed change.* There is no `GameMode` enum; `Scripts/Modes/` contains only `.gitkeep`.
Mode is discriminated by a single flag, `RunData.IsDailyBrief` (`Persistence/RunData.cs:190`),
plus three entry points: `StartNewRun` ("standard"), `StartDailyBriefRun` ("daily_brief"),
`StartSeededRun`. **Hell Rush does not exist in code.**

A mode-gated removal is therefore implementable only as `if (!IsDailyBrief) hide`, which
would also strip seeded runs, and there is no Hell Rush to preserve. Efficiency Rating is
additionally load-bearing for endings, milestones, and leaderboard submission — hiding it in
Campaign is display-only; removing it is not.

**No change made** (handoff §1.1 — product policy is not the engineer's decision).

---

## B5 — FMOD integration facts

```text
FMOD Unity package:      NOT INSTALLED. No FMOD entry in Packages/manifest.json.
DESK42_FMOD define:      NOT DEFINED. ProjectSettings/ProjectSettings.asset:664
                         -> scriptingDefineSymbols: {}  (empty for every platform)
Studio project / banks:  none in repo
Existing bootstrap:      Audio/FMODManager.cs — wrapper only, fully #if-gated
Existing directors:      BinauralStressEngine, ProceduralJazzGenerator, StressCrescendo,
                         SpatialAudioThreatSystem, DistortionAudioDirector — all gated
Bus / event naming:      buses bus:/, /Music, /SFX, /Ambience
                         events event:/Music/*, event:/Threat/TierCross
                         snapshots snapshot:/Fugue, /MercyWindow, /Flow
                         params "Sanity" (normalised 0-1), "TidePressure"
Legacy AudioSource path: NOT SWEPT — incomplete, flagged
ShiftLifecycleEvent:     8 runtime subscribers including all 5 audio directors.
                         Publish ordering is deliberate: GameManager.cs:490 publishes
                         AFTER scene activation, because LoadSceneAsync's
                         RumorMill.FlushQueue() wipes pending events before activation
                         (documented RunStateController.cs:87-91).
                         THIS is the startup race D1 must respect.
```

**Stop condition (handoff §11).** The handoff treats FMOD version as *possibly* unresolved.
The repository answer is stronger: **there is no FMOD integration installed at all.** D1
cannot begin until the plugin is imported, an `FMODUnity` reference is added to
`Desk42.Core.asmdef`, and `DESK42_FMOD` is defined — in that order, per
`.claude/skills/desk-42/fmod-integration.md`. Version/fork selection is a product decision.

---

## Blocker register

| # | Blocker | Blocks | Owner |
|---|---|---|---|
| 1 | FMOD not installed; version/fork undecided | all of D1 | product |
| 2 | `ISanityProvider` does not exist; creating it is forbidden in Bucket 1 | D2 param wiring | product |
| 3 | Phase-3 gate can render Sanity inert for some profiles | D2, B3 conclusion | product |
| 4 | No `GameMode` enum; no Hell Rush; Efficiency Rating load-bearing beyond display | B4 policy change | product |
| 5 | Concurrent agent shared the primary working directory | Buckets 1-3 | resolved — isolated worktree |

**Non-blocking, decided by evidence:** do not author Mercy/Flow FMOD states (B2, dead code).

**Not blocked by any of the above:** Bucket 1 (persistence + causal integrity) is pure
persistence work and proceeds independently.

---

## Process note

During B1-B5 an external process moved `HEAD` in the primary working directory
(`Desk 42`) without this session's involvement:

```text
08:33:53  checkout: moving from main to codex/persistence-fixtures
08:32:18  checkout: moving from feat/proof-build to main
08:32:07  checkout: moving from main to feat/proof-build   <- this session
```

All three refs pointed at `a432a8b` with no tracked modifications, so **the investigation
findings are unaffected** — every file read was identical on any branch. Bucket 1 work was
subsequently moved into an isolated worktree at `Desk42-worktrees/proof-build` to remove
the shared-directory hazard. `codex/persistence-fixtures` is preserved untouched as the
blocked fixture/test branch.
