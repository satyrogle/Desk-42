# Bucket C Δ2 — approval liability

**Branch:** `feat/proof-build` · **Production base:** `64d5bacf` · **Date:** 2026-07-27

---

## 1. What approval liability means

A **persistent consequence created by one specific committed approval**, which some later
system may act on.

Encounter history already records *that* an encounter was approved. Liability records that the
approval **left something unresolved behind**:

```text
EncounterHistory      "Encounter X was approved"
ApprovalLiability     "Encounter X created an unresolved approval consequence"
```

The audit (`68d3067`, requirement 7) found **no existing production meaning** — no type, no
lifecycle, no consumer. `Obligations` in `RunData` is personal expenses and unrelated; Elias's
Shift 2 branch is procedure-specific, not approval liability. So this contract is defined here
for the first time, deliberately at the minimum the audit prescribed.

## 2. Source identity

**Primary key: `SourceEncounterId`.** Not the claimant.

CΔ1 independently proved procedural `ClientVariantId`s are generated freshly per claim as
`{species}_{100..999}`, are not stable cross-encounter identity, and may collide. Keying
liability by claimant would attribute one claim's consequence to an unrelated later claimant.

`SourceClientVariantId` is retained as **provenance and a filtering aid** — meaningful for
authored claimants (`elias_venn`, `control_mara_kest`), not to be trusted as identity for
procedural ones.

Liability correctness therefore does not depend on fixing procedural identity, and that
generation was not changed in this pass.

## 3. Persisted schema

`ApprovalLiabilityRecord` — three fields, nothing derivable from history:

| Field | Purpose |
|---|---|
| `SourceEncounterId` | primary key; the committed approval that created it |
| `SourceClientVariantId` | provenance / filtering aid |
| `Resolved` | lifecycle flag; false until something discharges it |

Deliberately **absent**: claimant display name, disposition, visit count, completion status,
timestamps, proof-session data, any copy of `EncounterRecord`. All resolved from
`MetaProgressData.Encounters` when needed.

## 4. Persistence owner

`MetaProgressData.ApprovalLiabilities` (`ApprovalLiabilityLedger`) — campaign-persistent
alongside `Encounters`, so liability survives run boundary, save/load and process restart. It
is **not** in `RunData`, `ShiftManager`, scene objects, `EliasProofSessionController` or
runtime callbacks.

**No new save authority.** It rides the existing `EncounterCommitService` save.

## 5. Eligible dispositions

| Disposition | Creates liability |
|---|---|
| `Approve` | **yes** |
| `Deny` | no |
| `Liquify` | no — not reinterpreted as approval |
| Interrupted / incomplete | no |
| Duplicate `Approve` commit | no second liability |

Encoded once in `ApprovalLiabilityPolicy.IsLiabilityCreating`.

## 6. Creation point

Inside `CommitEncounterResult`, at step **8d** — after resolution, history completion, visit,
consequences and active-claim resolution, and **before the save**:

```text
validate/idempotency -> resolve -> consequences -> history complete
  -> resolve active claim -> APPROVAL LIABILITY -> SAVE -> presentation/flow
```

So a successful commit always has its liability on disk, and a commit that never became
authoritative can never leave one behind. Never created from Approve UI clicks, presentation,
pre-commit state, interrupted encounters, or deferred `ShiftManager` callbacks.

## 7. Exact-once

The ledger is keyed by `SourceEncounterId` and `Create` returns the existing record rather
than appending. **Persisted state is the authority — no runtime flag.** Duplicate callback,
repeated commit, save/load then retry, reconstructed controller and replayed deferred event
all converge on one record.

## 8. Query seam

Read-only, on `ApprovalLiabilityPolicy`:

```csharp
bool HasApprovalLiability(MetaProgressData, string sourceEncounterId)
ApprovalLiabilityRecord TryGet(MetaProgressData, string sourceEncounterId)
List<ApprovalLiabilityRecord> ActiveLiabilities(MetaProgressData)
List<ApprovalLiabilityRecord> ForClaimant(MetaProgressData, string clientVariantId)
```

`ForClaimant` is a convenience over provenance — **never** liability identity. Unknown or null
inputs return empty/null, never throw.

## 9. Orphan and invalid-record behaviour

A record is **valid** only when its source encounter exists, is completed, and its outcome is
`Approve`. Anything else — missing encounter, incomplete encounter, `Deny`, `Liquify` — is an
**orphan and is ignored by every query**.

Chosen deliberately over rejecting at load or repairing: a malformed or hand-edited save still
deserializes, nothing crashes, and the bad row simply never counts as an active liability.
Production creation makes orphans impossible; this only stops a bad file being trusted. No
repair engine was built.

## 10. Legacy saves

Additive. A pre-Δ2 `meta.json` loads with an empty ledger and **no liability is backfilled**
from historical approvals — liability is authoritative at creation time, and synthesising it
retroactively would silently change existing campaigns. No migration was written; none is
required by any existing authored behaviour.

## 11. Lifecycle — deliberately deferred

Creation, persistence and query only. **Nothing consumes liability yet**, so an active
liability simply remains active indefinitely. `Resolved` exists as the discharge flag but has
no production writer. No resolution, trigger or consumption mechanic was invented — the audit
warned that an underspecified ledger would drift from history, and there is no concrete
consumer to design against.

## 12. Elias and Mara

Approval liability is **not** part of `EliasProofSessionState` and Bucket 2 archival behaviour
is unchanged. Elias proof may query liability later; it does not own it.

Mara requires no Elias state: approving her creates general liability with no proof session in
existence — a useful independence check, locked by
`MaraApproval_CreatesLiability_WithNoEliasProofState`. No Mara-specific liability logic was
added.

## 13. What remains for CΔ3

- A concrete **consumer** for liability, which will settle the lifecycle.
- Interruption / carry-forward representation.
- Non-Elias persistent scheduled returns.
- Stable procedural claimant identity, **if** a real requirement demands following the same
  procedural individual across encounters. Liability was designed so it does not.
