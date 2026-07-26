# Bucket 2 — proof persistence, lifecycle, and scene repair

**Branch:** `feat/proof-build` · **Base:** `8de9989` (Bucket 1) · **Date:** 2026-07-26
**Authority:** Bucket 2 locked scope

---

## 1. The defect Bucket 2 exists to fix

`EliasProofSessionState` carried `[JsonProperty]` on all 15 members — but those markers were
**inert**. The only `JsonConvert` call sites that touch save files serialize `MetaProgressData`
and `RunData` only, and the state was owned by a private field on
`EliasProofSessionController`, a `DontDestroyOnLoad` child of `GameManager` with no disk hook.

Consequence: the proof survived scene changes and run boundaries by object identity, but an
**application restart silently destroyed the Shift 2 → Shift 5 causal chain.** The branch,
receipts, applied-procedure ledger and aftermath all vanished, and Shift 5 could no longer
resolve to its causal cause.

An existing test, `State_IsSerializableButNotOwnedByRunOrMetaSchemas`, actively asserted this
design. It has been **inverted rather than deleted** — see §7.

## 2. Persisted proof schema

Authoritative state moved onto `MetaProgressData`, alongside `Encounters` from Bucket 1:

| Field | Type | Purpose |
|---|---|---|
| `MetaProgressData.EliasProof` | `EliasProofSessionState` | The live proof session |
| `MetaProgressData.CompletedProofSessions` | `List<EliasProofSessionState>` | Archived sessions, retained as evidence |

`MetaProgressData` rather than `RunData` because **a run is one shift and the proof spans
five** — run ownership would reset the chain at every boundary. This matches the Bucket 1
decision for `EncounterHistory` and gives both restart-durability for free.

Persisted proof content is exactly what the proof needs, and nothing more — the 15 fields
already declared on `EliasProofSessionState`: session id, Shift 1 disposition, Shift 2 branch,
Shift 2/5 dispositions, procedure actions, receipt ids, streak/audit deltas, Shift 5 loaded
claim id, the two idempotency ledgers (`RecordedAppearanceKeys`,
`AppliedProcedureAppearanceKeys`), and `ActiveAftermathModifier`.

Both fields are **additive with safe defaults**. A pre-Bucket-2 `meta.json` deserializes with
an empty proof and empty archive — locked by
`FreshMeta_HasEmptyProofState_AndDeserializesFromLegacyJson`. No migration step needed.

## 3. Save/reload lifecycle

`EliasProofSessionController` is now a **runtime façade**:

```text
State  ->  GameManager.Meta.EliasProof        when a meta profile exists
       ->  a detached instance                 otherwise (headless fixtures)
```

Because the record lives on `MetaProgressData`, it is written by the existing
`SaveSystem.SaveMeta` path — including the save added inside
`EncounterCommitService.CommitEncounterResult` in Bucket 1. No new save call was required;
the proof now rides the transaction that already persists the encounter.

`BeginProofSession` is unchanged in its production trigger (`ShiftManager`, Shift 1 with an
empty queue), so resuming mid-run does not reset the chain, and a genuinely fresh Shift 1
correctly starts a new proof.

The persistence tests round-trip through **`SaveSystem`'s exact `JsonSerializerSettings`**
(mirrored in `ProofPersistenceTests`, with a comment binding them to `SaveSystem.cs:71-78`),
so a missing `[JsonProperty]` fails the suite rather than passing a shallow object copy.

## 4. Attribution identity

Attribution rides **persistent encounter identity plus the authored appearance key**, never
the claimant name:

- `EncounterRecord.EncounterId` — stable, persisted on `ActiveClaimData`, survives reload.
- `EncounterRecord.AuthoredAppearanceKey` — the causal handle for Shift 1/2/5.
- `EliasProofSessionState.Shift2Branch` + `Shift5LoadedClaimId` — the causal chain itself.

`Attribution_IsTiedToEncounterIdentity_NotClaimantName` locks this: after a round-trip, the
record is retrievable by `EncounterId` and its appearance key still resolves, independent of
`ClientVariantId` display naming.

## 5. `EndProofSession` ownership

**Production caller:** `RunStateController.CompleteRun()` calls
`EliasProof.TryEndCompletedSession()`.

`TryEndCompletedSession` ends the session only when `Shift5FinalDisposition` is terminal.
Ending at run completion rather than at Shift 5 commit is deliberate — committing is where
`ActivateShift5Aftermath` fires, and the aftermath needs its full window to apply to later
claims in that shift. Ending at commit would destroy it before it could act.

**Ending archives; it does not erase.** The session is appended to
`CompletedProofSessions` (deduplicated by session id) and only the live slot is cleared.
Clearing the live slot is what prevents aftermath and appearance keys leaking into the next
session — the original reason `EndProofSession` wiped everything — while the archived copy
keeps branch, receipts and dispositions available for attribution and post-test inspection.

**`EncounterHistory` is untouched by this boundary.** Committed visits are independent
evidence. `EndingASession_DoesNotTouchEncounterHistory` locks that.

## 6. Encounter status is derived, not stored

Per the locked scope, no lifecycle state was added. `EncounterStatus` is computed from the
ledger plus the id of the encounter at the desk:

```text
no record                              -> Unknown
record.Completed                       -> Completed
!Completed && id == activeEncounterId  -> Active
!Completed && id != activeEncounterId  -> Interrupted   (carried-forward)
```

`EncounterHistory.StatusOf(...)` and `.Interrupted(activeEncounterId)` expose it. Nothing is
serialized, so it cannot drift from the ledger.

## 7. Baseline / live-state audit

**Result: `EncounterBaseline` is NOT interposed in front of any policy that must react
immediately, and no change was needed.**

`EliasProcedurePolicy` receives the controller's `State` **by reference**, so a procedure
applied mid-encounter is visible to the disposition gate on the same frame. Bucket 1 added the
baseline *alongside* that path for external reads only. Two tests lock the behaviour:

- `Registration_AppliedThisEncounter_UnlocksDispositionImmediately` — Shift 2 disposition is
  gated with `EliasProcedureRequired` until the procedure is applied.
- `DispositionGate_ReadsLiveState_NotAnEntrySnapshot` — an appearance recorded moments earlier
  is visible to the gate; a frozen entry snapshot would fail this.

## 8. Button-listener repair

`Shift.unity` carried **11 identical `Approve` and 11 identical `Deny` persistent onClick
listeners**, all with the same target (`fileID: 1814582674`), assembly type and call state.
One player click invoked `EncounterManager.Approve()`/`Deny()` eleven times; only the
in-memory `_encounterActive` bool prevented an 11× payout, and every non-persistence side
effect on the resolution path ran eleven times regardless.

Repaired with `tools/Dedupe-SceneButtonListeners.py`, which collapses each `m_Calls` list to
one entry per identical listener. Result: **240 lines removed (20 entries × 12 lines), exactly
one `Approve` and one `Deny` remaining, pure deletion, idempotent on re-run.**

Regression guard: `ShiftSceneListenerTests` parses the scene asset and asserts one listener per
button plus no duplicate anywhere in the scene. Parsing the asset is deliberate — the defect
lives in serialized scene data, not code, so only reading the asset catches a reintroduction.
The script doubles as a CI guard (exit 1 on duplicates without `--apply`).

A note on how this was found: the first dry run reported "no duplicates". That was a bug in my
own script — Unity scenes are CRLF and `rstrip("\n")` left a `\r`, so the `m_Calls:` marker
never matched. Verifying the 11 entries were byte-identical by hand is what exposed it.

## 9. Cross-file atomicity — deferred by decision

Per the locked decision, the `run.json` / `meta.json` crash window is **not** addressed here.
It is recorded as a standalone persistence-reliability issue in
`BUCKET-1-PERSISTENCE.md` §Atomicity. The Five-Shift proof contract covers normal save/load,
not process termination between the two writes.

## 10. Tests

```text
EditMode  244 total — 238 passed, 0 failed, 6 skipped   (was 220/214; +24)
PlayMode   13 total —  13 passed, 0 failed
```

Required-test coverage:

| Required | Test |
|---|---|
| Shift 2 state survives save → reload | `Shift2State_SurvivesSaveReload`, `AppearanceAndProcedureLedgers_SurviveSaveReload` |
| Reload preserves the same `EncounterId` | `Reload_PreservesTheSameEncounterId` |
| Shift 5 consequence resolves to original causal encounter after reload | `Shift5Consequence_ResolvesToOriginalCausalBranch_AfterReload` |
| Same-frame registration still unlocks disposition | `Registration_AppliedThisEncounter_UnlocksDispositionImmediately`, `DispositionGate_ReadsLiveState_NotAnEntrySnapshot` |
| Duplicate commit idempotent after reload | `DuplicateCommit_RemainsIdempotentAfterReload` |
| Active incomplete encounter resumes, not counted as visit | `ActiveIncompleteEncounter_ResumesWithoutCountingAsVisit` |
| Completed visit increments exactly once | `CompletedVisit_IncrementsExactlyOnce` (11 attempts → 1 visit) |
| Approve/Deny invoke resolution once | `ApproveButton_HasExactlyOneResolutionListener`, `DenyButton_...`, `NoPersistentListenerIsDuplicatedAnywhereInTheScene` |
| `EndProofSession` has a production caller and preserves evidence | `ProductionBoundary_ExistsOnRunCompletion`, `ArchivedSession_SurvivesSaveReload_AsEvidence`, `EndingASession_DoesNotTouchEncounterHistory` |

## 11. Blocking Bucket 3

1. **FMOD is not installed at all** — no package in `manifest.json`, `scriptingDefineSymbols`
   empty. D1 cannot begin; version/fork is a product decision (B5).
2. **`EnterMercyWindow`/`ExitMercyWindow`/`EnterFlow` are dead** — zero callers, the file's own
   TODO admits the wiring gap. Do not author Mercy/Flow FMOD states (B2).
3. **`ShiftLifecycleEvent` startup race** — `GameManager.cs:490` publishes after scene
   activation because `LoadSceneAsync`'s `RumorMill.FlushQueue()` wipes pending events first.
   D1 must respect this ordering for cold-start one-shots.
4. **Legacy `AudioSource` gameplay path not yet swept** — flagged incomplete in B5.
5. **Not blocking, but open:** the Compliance Streak still builds and displays mid-shift while
   paying nothing (Bucket 0B), and `OfficeSupplyEffectBase` still defaults
   `Preview* => Modify*` fail-open.
