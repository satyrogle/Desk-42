# PLAYTEST REPORT 001 — Desk 42: Anomalous Claims

**Session:** 2026-07-21, branch `playtest/session-001` (from consolidation merge `a758359`).
**Method:** Seeded PlayMode harness (`Assets/_Project/Tests/PlayMode/ShiftPlaythrough.cs`, category `PlaytestHarness`) driving Approve/Deny/card-slam/dilemma inputs through the same public APIs the UI calls, logging all 22 RumorMill bus events. Unity CLI batchmode, timeScale 2. Live MCP Play Mode was unavailable this session (editor-side bridge requires a manual click; auto-start off — see build-state.md), so screenshots were not captured; batchmode `ScreenCapture` produced no PNGs. Full logs in `PlaytestLogs/` (not committed).
**Save safety:** meta.json/run.json backed up and restored by the harness; the developer's real save was not modified.

---

## 1. Build verdict

| Question | Verdict |
|---|---|
| Compiles? | **Yes** — 0 errors, 0 CS warnings (Editor.log, full recompile). |
| Runs? | **Yes** — Boot → MainMenu → Shift → clock-out → run-complete works unattended; 0 console errors during all 4 shifts. |
| Playable end-to-end? | **Technically yes, experientially no** — the loop completes, but a "shift" is over in ~12 s of game time, sanity/soul/BSM/dilemmas never move, and approve-vs-deny has no felt consequence. |

## 2. Shift log

True counts from the event logs (`PlaytestLogs/shift*_seed*.log`). *Note: the harness's own end-of-shift `claims=` counter was inflated by a stacking-subscription bug in the harness (fixed in the file, evidence re-derived from the event stream); the numbers below are the authoritative per-event counts.*

| Shift | Seed | Realtime (×2 speed) | Claims resolved | Quota result | Phases | Sanity start→low→end | Fugue? | Tide | Dilemmas / MoralChoice / Soul | Synergies | Errors |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 421001 | 6 s | 4 (8 generated) | Morning ended at **2/3**, afternoon at **2/4** (double-count bug) | ClockIn→Morning→Lunch→Afternoon→ClockOut | 100→100→100 | No | 1 escalation (0→1, *overperformance*) 0.4 s before ClockOut | 0 / 0 / 0 | 2 sequential (+3), 3 cross-claim (+5) — log-only | 0 |
| 2 | 421002 | 7 s | 3 | Morning 2/3, afternoon 1/4 → ClockOut | same | 100→100→100 | No | 1 escalation (0→1, overperformance) at ClockOut | 0 / 0 / 0 | cross-claim only | 0 |
| 3 | 421003 | 6 s | 3 | same shape as shift 2 | same | 100→100→100 | No | 1 escalation at ClockOut | 0 / 0 / 0 | cross-claim only | 0 |
| 5 | 421005 | 20 s | 10 | Unpaid-Overtime loop entered ×3 (**ForceEscalate confirmed**: Tide 0→1→2→3), ended at overtime cap | 13 phase changes incl. 3 OT loops | 100→100→100 | No | 3 escalations, all overperformance/forced | 0 / 0 / 0 | multiple; 7 memos | 0 |

**BSM state transitions observed: 0 across all 20 claims.** No tells fired (no `StateTransitionEvent` on the bus at any point). Every client lived and died in its initial state.

**Reproducibility caveat:** the same seed (421002) produced a `PrinterJam` hazard + two 5-point sanity losses in one session and *nothing* in the next. Fixed seeds do **not** currently produce fixed shifts (defect #4).

## 3. Loop read (the important part)

**The shift stops producing decisions at minute zero.** There is exactly one decision in the game right now — approve or deny — and it has no consequence the player can feel: sanity never moves, soul never moves, the BSM never reacts, dilemmas never trigger, and credits are wiped to zero by end-of-shift expenses regardless of what you chose (shifts 1–3 all ended `credits=0`, debt compounding 109→191→248). A human player at ~15 s/claim would finish shift 1 in about a minute; shift 2 is structurally identical to shift 1 with nothing new surfacing, so repetition onset is **shift 2, claim 1**.

**Systems that fired but were invisible to the player:**
- Sequential synergy / cross-claim bonuses — Debug.Log only; no bus event, no UI moment, and the credits they add are silently consumed by expenses.
- Conspiracy memos (`MemoGeneratedEvent`, 14 across the session) — generated and stored, no verified surface.
- Tide escalation — fires, but ~0.5 s *before* ClockOut (the "overperformance" director reacts exactly when it no longer matters).
- End-of-shift expenses (`ExpenseUnmetEvent`) — the most consequential economy event in the game happens after play ends, with no in-shift telegraphing.

**Specced systems that never fired at all (by name):**
`StateTransitionEvent` (all 9 BSM states + tells), `CardSlammedEvent` (slams are silent no-ops), `SanityChangedEvent`*, `SoulIntegrityChangedEvent`, `MoralChoiceEvent`, `DilemmaTriggeredEvent`, `NDASignedEvent`, `FactionShiftEvent`, `CounterTraitGeneratedEvent`, `MilestoneReachedEvent`, `NarratorToneChangedEvent`, `SupplySignalEvent`, `CardCascadeResolvedEvent`, `OfficeHazardEvent`*.
(*fired once in a prior non-deterministic session, same seed — see defect #4.)

14 of 22 bus events are dead in a full 4-shift run. The reactive spine (audio tiers, distortion, UI decay, achievements) is architecturally sound and completely starved: everything subscribes to Sanity, and Sanity never changes.

## 4. Top 10 defects (by damage to the core loop)

Repro for all: run the harness — `-runTests -testPlatform PlayMode -testCategory PlaytestHarness` — with the listed seed.

1. **Quota double-count halves every shift.** `ClaimsProcessedThisAnte` is incremented by *both* `ShiftManager.HandleClaimResolved` (`Core/ShiftManager.cs:251`) and `RunStateController` (`Core/RunStateController.cs:328`) for the same `ClaimResolvedEvent`. Morning block ends at 2/3, afternoon at 2/4. All seeds. This alone makes shifts ~half their designed length and makes the afternoon quota unreachable as a challenge.
2. **Sanity is inert in normal play.** No claim resolution, approve/deny mismatch, anomaly, or client behaviour costs sanity — 100.0 flat across 4 shifts, 20 claims (seeds 421001–421005). Fugue, the Environmental Distortion Scale, the audio tiers, and the entire dread arc are unreachable.
3. **BSM/tells never engage.** 0 `StateTransitionEvent` in 20 encounters. Clients never leave their initial state; `TellVisualIndicator` (wired in-scene) has nothing to show. The game's signature antagonist layer is dormant. All seeds.
4. **Fixed seeds are not reproducible.** Seed 421002: one session had `OfficeHazardEvent PrinterJam` + sanity 100→95→90; the rerun had neither. Something (hazards at minimum) draws from unseeded RNG instead of `SeedEngine` streams. Breaks the share-code/Daily-Brief promise and all regression testing.
5. **Card slams are silent no-ops.** `PunchCardMachine.SlamCard()` called with valid hand cards (`Card_Analyse`, `Card_PendingReview`, …): no `CardSlammedEvent`, no success/jam/reject feedback, no observable state change. The punch-card mechanic — the game's core verb — currently does nothing detectable. All seeds.
6. **Moral dilemmas never trigger.** 0 in 20 claims at PhaseLevel 4 (`EncounterManager.TryTriggerDilemma` → `MoralDilemmaSystem.TryGenerateDilemma` never returns one). Soul integrity untouched all session.
7. **Tide director reacts after the shift is over.** Overperformance escalation lands ~0.5 s before ClockOut in every normal shift (4.8 s/5.6 s marks, seeds 421001–421003). During actual play the director is flat at level 0. (`ForceEscalate` in the OT loop works — shift 5.)
8. **`GameManager.Phase` NREs before a run starts.** `GameManager.cs:238` → `RunStateController.ArchetypeId` dereferences null `_data`. Any main-menu-time consumer of `Phase`/`PhaseLevel` throws. Repro: read `GameManager.Phase` in MainMenu before `BeginNewRun`.
9. **Service registry is wiped on Shift-scene reload.** New scene's `Awake` registers → old scene's `OnDestroy` then *unregisters* the fresh instance (`[Desk42Services] Overwriting existing registration` warning, then empty registry). Back-to-back shifts in one session lose `EncounterManager`/`CorpOSWindowManager` lookups; `GameManager.EndShift`'s `RunSummaryPanel` lookup can silently take the wrong path.
10. **Economy reads as pre-broke.** With defect #1 halving income, every early shift ends `credits=0` with compounding debt (109→191→248) and the only economy feedback is post-shift `ExpenseUnmetEvent`. Also cosmetic-but-visible: TMP missing glyphs ☀ ✓ ✗ ◑ in LiberationSans fallbacks — the desk's diegetic symbols render as boxes.

Minor (logged, not ranked): `CardHandView` "Drift detected: hand=0, buttons=5" on scene entry; `Tests/` root NUnit stubs outside test asmdefs; empty `Desk42.Tests.PlayMode` assembly warning (resolved by this harness); lunch break hardcoded `5f` overriding the serialized 60 s (magic number, `ShiftManager.cs:442`); `OfficeSuppplies`/`ImpatenceTimerRemaining` typos in serialized names (save-format landmine if ever corrected).

## 5. The three cheapest changes for shift-2 boredom

Fix the double-count first — it is a one-line deletion (pick one owner for `ClaimsProcessedThisAnte`) and it instantly restores full-length shifts, a reachable afternoon quota, and roughly double the income, which un-breaks the expense economy as a side effect. Second, give Sanity a per-claim pulse: even a flat −3 on every resolution with a small bonus/penalty spread for approve-vs-deny would put the whole dormant reactive stack — Tide pressure during (not after) play, distortion tiers, the dread arc toward Fugue — back on stage without building anything new, because every one of those systems already subscribes to `SanityChangedEvent` and is verified wired in the Shift scene. Third, make the card slam answer back: publish `CardSlammedEvent` and nudge the client's BSM state on slam. The tells/state layer is present, composed, and scene-wired; it starves only because no input ever reaches it. Those three changes convert "click approve until the shift ends itself" into "manage a deteriorating mind against clients who visibly react," using only systems that already exist.

---
*Harness: `Assets/_Project/Tests/PlayMode/ShiftPlaythrough.cs` (category `PlaytestHarness`, clearly marked NOT SHIPPED CODE). Two game-state hazards were worked around in the harness and are themselves defects #8 and #9.*
