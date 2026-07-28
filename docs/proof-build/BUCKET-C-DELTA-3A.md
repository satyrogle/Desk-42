# Bucket C Δ3A — interrupted / carried-forward encounter lifecycle

**Branch:** `feat/bucket-c-delta2-clean-repair` · **Base:** `43a95608` · **Date:** 2026-07-27

---

## 1. The gap this closes

`EncounterHistory` already **derived** an `Interrupted` status, but derivation is not
carry-forward. The claim payload lived only in per-run `RunData`, which a new run replaces —
so an interrupted encounter was **unrecoverable across a run boundary**. History knew it
happened; nothing could reconstruct it. `StartClockOut` could end a shift with an unresolved
`ActiveClaim` and simply lose it.

## 2. Definitions

- **Interruption** — an explicit transition meaning *this encounter received no terminal
  disposition and must continue later*. Never inferred from scene unload, UI close, runtime
  object loss or app quit.
- **Carry-forward** — the same encounter continuing. **Not** scheduled recurrence, which is a
  completed claimant returning as a *new* encounter (CΔ3B, if ever needed).

## 3. Identity rules

A carried encounter retains its **original `EncounterId`** and **original `ClientVariantId`**
across interruption, save, restart, run/shift transition, re-presentation and final
resolution. Nothing mints a new identity because a new `ShiftManager`, scene, `RunData`, queue
or controller was created. For procedural claims the already-generated
`{species}_{100..999}` is preserved, never regenerated — this does **not** solve general
procedural recurrence identity.

## 4. Persisted representation and owner

`MetaProgressData.CarriedEncounters` (`CarriedEncounterLedger`). Meta-owned because `RunData`
does not survive the run boundary — precisely the reason the gap existed.

`CarriedEncounterRecord`: `EncounterId` (key), `ClientVariantId`, `Claim` (payload needed to
reconstruct), `InterruptedOnShift`, `InterruptCount`. No scene or UI state. Additive — legacy
saves load empty and nothing is synthesised.

## 5. Trigger, save boundary, ownership

| Concern | Owner |
|---|---|
| Marks interruption | `EncounterCommitService.InterruptEncounter` |
| Called from | `ShiftManager.StartClockOut`, when `ActiveClaim` is unresolved |
| Saved by | existing `SaveSystem` via the commit service |
| Durable | immediately, at the interruption transition |
| Released | `CommitEncounterResult` step 8e, before SAVE |

No new save authority, no new file format, no second commit path.

## 6. Interruption is not a disposition

Records no Approve/Deny/Liquify, creates no approval liability, applies no final consequences,
does not count as a committed claimant-history disposition, does not advance visits.
`InterruptEncounter` refuses a resolved claim and refuses an already-completed encounter, so a
finished encounter can never be resurrected as outstanding work.

## 7. Queue reconstruction and dedupe

`ShiftManager.RestoreCarriedEncounters` runs **after** queue generation, so a fresh queue
cannot discard carried work, and inserts at the **front** — outstanding work before new work.
That is the narrowest deterministic ordering that makes carry-forward functional; no existing
design rule is overridden and the scheduler is otherwise untouched.

Dedupe is by **`EncounterId`**, checked against both `ActiveClaim` and `PendingClaims` — never
by claimant or display name, since one claimant may legitimately have several encounters.
Repeated queue generation, reload before generation, and scene reconstruction therefore cannot
enqueue the same encounter twice. A carried encounter found already completed is released
instead of re-presented.

## 8. Repeated interruption

Supported. `Carry` is idempotent by `EncounterId`: a second interruption updates the existing
record and increments `InterruptCount` rather than adding a row. `X interrupt → resume →
interrupt → resume → Approve` yields one `EncounterId`, one history identity, one final
commit, one visit and one liability.

## 9. Terminal resolution

Normal `EncounterCommitService` remains the sole authority, using the original `EncounterId`.
History completes exactly once; visits follow the locked exact-once rule; approval liability
appears only for a qualifying Approve, keyed by the original encounter; bonus/memo fire once.
Carried work is released before the save, so a resolved encounter never returns.

- **Approve** → completed once, one visit, one liability.
- **Deny** → completed once, no liability.
- **Liquify** → completed once, no liability (CΔ2 semantics unchanged).

## 10. Malformed / missing data

Duplicate carried rows for one `EncounterId` → `Canonical()` exposes **one** logical carried
encounter, first occurrence wins. A record with no reconstructable `Claim` is **ignored**, not
returned and not crashed on. Read-only: nothing is deleted and no write-back is triggered, so
a malformed save still loads. `Release` removes every row for an id so no stale copy survives.
No repair subsystem.

## 11. What remains for CΔ3B

Generic scheduled return — "return in N shifts", claimant recurrence, a persistent
scheduled-return ledger — is deliberately **not** implemented. It should be built only against
a concrete authored claimant requirement, and kept separate from carry-forward.
