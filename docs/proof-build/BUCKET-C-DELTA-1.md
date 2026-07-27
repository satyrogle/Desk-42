# Bucket C Δ1 — claimant identity, history query, Mara scheduling independence

**Branch:** `feat/proof-build` · **Production base:** `8e38f9ed` · **Date:** 2026-07-27

---

## 1. Mara scheduling independence

**The defect.** `ControlClaimantContent.TryScheduleControlClaimant` was already proof-free
internally, but its only production call site sat **inside** the Elias guard in
`ShiftManager.GenerateInitialQueue`:

```csharp
if (gameManager?.EliasProof?.HasActiveSession == true)
{
    EliasProofScheduler.TryReplaceScheduledClaim(...);
    ControlClaimantContent.TryScheduleControlClaimant(...);   // <- gated
}
```

So the control claimant only appeared when an Elias proof session happened to be active —
making her depend on the very thing she exists to be independent of. My own error, introduced
when she was first wired.

**The repair.** The call moved out of the guard, to the same nesting level as the guard
itself. No fake or empty proof state is supplied, and the scheduler's API is unchanged.

**Rule.** The decision *"should Mara be scheduled for Shift 3?"* must not consult
`EliasProofSessionState` or any proof activation. It depends only on the shift number and
queue shape.

Preserved from the original design — the scheduler still:
- takes neither `EliasProofSessionState` nor `EliasProofContent` (asserted by reflection);
- never mutates proof state;
- never consumes Elias encounter identity;
- is not part of Elias recurrence.

## 2. Canonical claimant identity

**Field:** `ActiveClaimData.ClientVariantId` (string), carried onto
`EncounterRecord.ClientVariantId`.

This is the existing stable field; **no new identity layer was added.**

| Concept | Field | Cardinality |
|---|---|---|
| Claimant identity — *who is this across appearances* | `ClientVariantId` | one claimant → many encounters |
| Encounter identity — *which specific event* | `EncounterId` | one encounter → exactly one record |

Why it is stable across encounters, save/load, restart and multiple runs:
- it is serialized on both `ActiveClaimData` and `EncounterRecord`;
- `EncounterHistory` lives on `MetaProgressData`, which persists across runs and restarts;
- authored claimants use fixed constants — `elias_venn`
  (`EliasProofContent.CanonicalClaimantId`) and `control_mara_kest`
  (`ControlClaimantContent.StableClaimantId`).

**Not identity:** `EncounterId`, runtime object identity, queue position, display name, visit
number. Display name is explicitly rejected — `ClaimantName` is authored prose with no
uniqueness guarantee.

### Honest limitation

For **procedural** claimants, `ClaimGenerator` builds `ClientVariantId` as
`{species}_{100..999}` from the seeded stream, freshly per claim. Two encounters with "the
same" procedural claimant therefore carry **different** identities, so cross-encounter
recurrence is not currently expressible for them.

This is correct for Δ1 — authored claimants (Elias, Mara) are what the proof needs, and both
are stable. But **approval liability and carried-forward work in CΔ2/CΔ3 will need a decision
here**: either procedural claimants gain a stable identity, or those features apply only to
authored claimants. Flagged rather than pre-empted.

## 3. Historical disposition authority and query seam

**Authority:** `MetaProgressData.Encounters` (`EncounterHistory`) — the Bucket 1 ledger.
**No second disposition store was created.** There is deliberately no
`Dictionary<ClaimantId, LastDisposition>` that could diverge from history.

**Seam** — a filtered *view*, not a parallel record:

```csharp
IReadOnlyList<EncounterRecord> CommittedDispositionsFor(string clientVariantId)
ClaimResolutionKind            LatestDispositionFor(string clientVariantId)
bool                           HasCommittedHistory(string clientVariantId)
```

### Frozen semantics

| Case | Behaviour |
|---|---|
| Unknown claimant | empty list, `Unspecified`, `false` — **never throws** |
| Null/blank id | empty list |
| One committed encounter | exactly that result |
| Multiple encounters | every historical result, in **history (append) order** — not collapsed to the latest |
| Interleaved claimant | filtered out; no contamination |
| Duplicate commit | one disposition; the **original** outcome stands |
| Interrupted / in-progress | **excluded** — a presentation never masquerades as a final disposition |
| Save/load | identical results after reconstruction |
| Reappearance | earlier results remain queryable; the new encounter gets a distinct `EncounterId` |

`Approve` / `Deny` / `Liquify` remain distinguishable, carried on `EncounterRecord.Outcome`.

## 4. Elias proof is not the general authority

`EliasProofSessionState` keeps its own proof-specific disposition fields, but it is **not** the
general claimant-history authority. General identity and history live at the
encounter-history layer. Mara's history is queryable with no proof session in existence —
locked by `MaraHistory_IsQueryableWithoutAnyEliasProofState`.

## 5. Why Mara is not generic recurrence

Mara's Shift 3 appearance is **authored, single, and stateless**: a shift-number check plus a
queue slot. It is not a persistent recurrence scheduler, carries no return state, and is not
permission to build one. Persistent authored return scheduling remains CΔ3 and should be
implemented only against a concrete claimant requirement.

## 6. Persistence

Uses the Bucket 1 spine unchanged. **No new save call** was added for Mara or for the query;
the seam is a pure read over already-persisted state. `RunInstanceId`, `EncounterId`,
`EncounterCommitService`, exact-once commit/visit, `ConsequencesApplied` and resolved
`ActiveClaim` semantics are untouched.

## 7. Remaining for CΔ2 / CΔ3

- **CΔ2 approval liability** — needs the procedural-claimant identity decision above.
- **CΔ3 persistent authored returns** — a real recurrence scheduler, against a concrete
  claimant requirement, not generalised from Mara.
- Carried-forward encounter representation (status derivation already exists; the
  *representation* does not).
