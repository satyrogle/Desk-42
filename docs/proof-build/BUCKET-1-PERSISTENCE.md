# Bucket 1 — persistence + causal integrity

**Branch:** `feat/proof-build` · **Base:** `a432a8b` · **Date:** 2026-07-26
**Authority:** `Desk42_Claude_Code_Handoff.md` §3

---

## What was actually broken

Three defects, all verified at `a432a8b` before any change:

**1. Completed visits were never counted, for anyone.**
`MetaProgressData.RecordVisit` (`Persistence/MetaProgressData.cs:239`) had **zero callers**
repo-wide. `ClientTacticProfile.TotalVisits` was therefore permanently `0`, yet
`EncounterManager.cs:133` read it to drive the BSM's repeat-offender behaviour and
`TransitionRule.cs:56` gated on `ctx.VisitCount > 0`. In normal continuous play **no claimant
was ever recognised as a returning visitor.**

**2. The only live recorder fired on spawn.**
`EliasProofSessionController.RecordAppearance` was called from `EncounterManager.BeginEncounter`
(`EncounterManager.cs:151`), reached from `ClaimQueuedEvent` — before BSM init, before the
claim panel rendered, before the player could act. Handoff §3.1 forbids exactly this
("Never on claimant spawn"). The code comment admitted the motive: *"Record before BSM
initialization so Shift 1/2/5 consume the authoritative prior values 0/1/2."*

**3. Resolution never saved.**
`ResolveEncounter` mutated run state, meta state and proof state, then published a deferred
event and returned. **No `SaveRun`/`SaveMeta` anywhere in the path.** A crash or quit after a
disposition lost the entire encounter.

Two aggravating factors found in passing:

- `ApproveBtn`/`DenyBtn` in `Shift.unity` each carry **11 duplicate persistent onClick
  listeners**, so one click invokes `Approve()`/`Deny()` eleven times. Only the
  `_encounterActive` flag prevented an 11× payout. That flag is not persistence — it is a
  single in-memory bool.
- `TryLiquify` was a **second independent commit path**, calling `ApplyClaimResolution`
  directly and bypassing history, visits and save entirely.

---

## What was built

### Encounter identity (§3.2)

`ActiveClaimData.EncounterId` — assigned once by
`EncounterCommitService.EnsureEncounterId`, format `ENC-{seedCode}-S{shift}-{seq:D3}`,
persisted **with the claim**. Because `RunData.ActiveClaim` is serialized, a mid-encounter
quit/resume reconstructs the *same* id and cannot mint a phantom.

`ClaimId` was unusable as identity: it is a seeded 5-digit `CLM-#####` drawn from
`SeedStream.ClaimQueue` with **no uniqueness check anywhere** — collisions are possible
within and across runs. A regression test locks this
(`EnsureEncounterId_IsUniquePerClaim_EvenWhenClaimIdCollides`).

### Authoritative history (§3.5)

`EncounterHistory` (`Persistence/EncounterHistory.cs`), stored on
`MetaProgressData.Encounters`. Append-only ledger of `EncounterRecord`.

```text
TotalPresentations(variant) = count(records for variant)
TotalVisits(variant)        = count(records for variant WHERE Completed)
PriorVisits(variant, enc)   = completed records for variant, excluding enc
```

Nothing else stores a visit or presentation count. `ClientTacticProfile.TotalVisits` is
retained **only** so legacy `meta.json` deserializes, and is documented as legacy;
`MetaProgressData.RecordVisit` is now `[Obsolete(error: true)]` so any attempt to reintroduce
an independent counter fails at compile time.

**It lives in `MetaProgressData`, not `RunData`,** because recurrence must survive a run
boundary *and* an application restart. This also closes a gap found in B1: Elias proof state
is session-only (`GameManager` has no disk hook for it), so before this change a cross-process
reload lost Elias's visit history entirely.

### The transaction (§3.3)

`EncounterCommitService.CommitEncounterResult` — the single authoritative commit, in the
locked order:

| Step | Implementation |
|---|---|
| 1 Validate id / idempotency | `history.IsCompleted(encounterId)` → early `AlreadyCommitted` |
| 2 Record mutations | `run.ApplyClaimResolution(...)` |
| 3 Append history | `BeginPresentation` (repair path if never presented) |
| 4 Mark completed visit | `history.MarkCompleted(...)` |
| 5 Behaviour counters | `meta.GetOrCreateProfile(...)` |
| 6 Reference mutations | `proof.RecordDisposition(...)` |
| 7 Thread delta | proof branch state via `RecordDisposition` |
| 8 Schedule consequences | `proof.ActivateShift5Aftermath(...)` |
| 9 Save | `SaveSystem.SaveRun` + `SaveMeta` |
| 10 Cleanup / transition | caller, gated on `CommitResult.ShouldTransition` |

Both `ResolveEncounter` and `TryLiquify` now route through it. There is no second commit path.

### Idempotency mechanism (§3.2)

The predicate is `EncounterHistory.IsCompleted(encounterId)` — **the history itself**. Per the
handoff's explicit instruction, no separate committed-ID collection was created.

A duplicate commit returns `CommitRejection.AlreadyCommitted`: no second mutation, no second
completed record, no second behaviour increment, no second scheduled consequence. This makes
the 11-duplicate-listener scene defect harmless without touching the scene (which would be UI
work, out of Bucket 1 scope, and would reset a frozen cohort under §7).

`MarkCompleted` also refuses to overwrite the original outcome or timestamp — locked by
`SecondCompletion_DoesNotOverwriteOriginalOutcome`.

### Baseline vs working state (§3.4)

`EncounterBaseline` (readonly struct) captures external pre-existing state at entry:
prior visits, prior presentations, already-committed flag. Immutable; a later commit does not
change it (`Baseline_IsImmutableSnapshot_UnaffectedByLaterCommits`).

**Important finding: the §3.4 example bug does not currently exist.** `EliasProcedurePolicy`
reads `EliasProofSessionState` **by reference**, so a registration applied mid-encounter is
visible to the disposition gate on the same frame — disposition already unlocks immediately
from live working state. The baseline was therefore added *alongside* that live path, not
interposed in front of it. Interposing would have introduced the very staleness §3.4 forbids.

---

## Persistence schema changes

| Change | Location | Compatibility |
|---|---|---|
| `ActiveClaimData.EncounterId` (string) | `RunData.cs` | Additive. Null on old saves; assigned on first use, so a resumed pre-change encounter gets an id and behaves correctly. |
| `RunData.EncounterSequence` (int) | `RunData.cs` | Additive. Defaults 0. |
| `MetaProgressData.Encounters` (`EncounterHistory`) | `MetaProgressData.cs` | Additive. Defaults to empty; old saves start with no history, so every claimant reads as first-visit — which is exactly what the broken build already did. **No data loss.** |
| `ClientTacticProfile.TotalVisits` | `MetaProgressData.cs` | Retained, no longer read or written. Was always 0 in every shipped build. |

No migration step was required: every change is additive with a safe default, and the
existing `MigrateMetaIfNeeded` path is untouched. Old saves load; new fields populate on use.

---

## Atomicity — the weakest boundary (reported per §3.3)

The save architecture is **whole-object JSON overwrite with no journal**
(`SaveSystem.Save<T>` → temp file → atomic move). A true multi-object rollback is not
available without an architectural change, which is out of scope.

The weakest boundary is **between step 2 and step 9**: `ApplyClaimResolution` mutates
`RunData` in memory, and `MetaProgressData` is mutated at steps 3–5, but the two objects are
persisted to *separate files* at step 9. A crash between the two writes can leave `run.json`
and `meta.json` disagreeing about whether the encounter completed.

Mitigation implemented — the safest practical pattern in the current architecture:

1. The idempotency gate runs **first**, so replay after a partial failure is a no-op rather
   than a double-apply.
2. `MarkCompleted` is set **before** the save, so the in-memory state is internally
   consistent at the moment of persistence.
3. Save failures are caught and logged, never thrown — losing a save is recoverable; throwing
   mid-transaction is not.

Residual failure mode, stated plainly: a crash *between* the `run.json` and `meta.json`
writes can produce a run that believes a claim resolved while history does not record the
visit. The encounter would then be re-presented and re-committed on reload, which is correct
behaviour, but the run-side resource mutation would apply twice. **Closing this needs a
combined save envelope or a write-ahead marker — an architectural change requiring approval.**

---

## Tests

26 new EditMode tests, all passing:

- `EncounterHistoryTests` (14) — presentation ≠ visit, interrupted encounters never become
  visits, re-presentation creates no phantom, second completion rejected and non-destructive,
  derived counts scoped per claimant, authored-appearance tracking, argument validation.
- `EncounterCommitServiceTests` (12) — id assignment/stability/uniqueness under `ClaimId`
  collision, sequence persistence, presentation idempotency across simulated resume, baseline
  immutability, already-committed detection, and that a poisoned legacy `TotalVisits` is
  ignored by derivation.

`CommitEncounterResult` itself needs a live `RunStateController` MonoBehaviour, so it is
exercised end-to-end by the existing PlayMode suite rather than duplicated in EditMode.

```text
EditMode  220 total — 214 passed, 0 failed, 6 skipped   (was 194/188; +26)
PlayMode   13 total —  13 passed, 0 failed              (unchanged)
```

Evidence the spine works end-to-end, from the PlayMode log across the three proof routes:

```text
[EncounterCommit] Committed 'ENC-AAN5EK-S1-001' (Approve) claim='elias_shift_1_claim' visitsNow=1.
[EncounterCommit] Committed 'ENC-AAN5EL-S2-001' (Deny)    claim='elias_shift_2_claim' visitsNow=2.
[EncounterCommit] Committed 'ENC-AAN5EP-S5-001' (Approve) claim='elias_shift_5a_claim' visitsNow=3.
```

Visits now derive 1 → 2 → 3 across the five-shift spine from committed history.

---

## Remaining blockers for Bucket 2

1. **`Shift.unity` duplicate onClick listeners** — `ApproveBtn`/`DenyBtn` each fire 11×. Now
   harmless for persistence, but it is a live UI defect and a participant-facing change under
   §7. Bucket 2 (proof UI) must decide when to fix it relative to the frozen build.
2. **Cross-file save atomicity** — see above. Needs approval for an architectural change.
3. **Elias proof state is still session-only.** Encounter history now survives restart, but
   `EliasProofSessionState` (branch, procedure receipts, aftermath) does not. Success criterion
   #2 ("save/reload preserves recurrence") holds for *recurrence*; full proof-branch
   restoration after an app restart is not yet covered.
4. **No encounter timeout / interruption outcome.** There is no per-encounter timeout, and
   shift-end mid-encounter neither resolves nor cancels the live encounter. History now
   records such encounters as presented-but-incomplete, which is the correct representation,
   but the handoff's "interrupted / carried-forward encounter outcome representation where
   required" may need an explicit carried-forward state in Bucket 2.
5. **`EndProofSession` has zero production callers** — sessions are begun but never ended
   outside tests.
