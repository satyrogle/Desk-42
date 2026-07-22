# Desk 42 Confirmation-Layer Decisions

This document is the required consumer decision table for the confirmation-layer pass. It is authoritative for claim dispositions and overrides the contradictory "correct bureaucratic / humane" language in GDD v1.0.

## Design contract

- A claim has a chosen disposition, never a graded answer.
- `ClaimResolutionKind` is the canonical disposition: `Approve`, `Deny`, or `Liquify`.
- The corporation may reward a disposition. That reward expresses corporate preference, not correctness or virtue.
- Policies describe intended consequences. Applied-result payloads report the measured post-mutation changes that actually happened.
- Semantic confirmation (failure reasons, state changes, receipts, and expense results) is never optional or feedback-budgeted.

## Legacy-boolean consumer decisions

| Existing consumer | Explicit replacement | Decision |
| --- | --- | --- |
| `ClaimResolutionOutcome.ResolvedCorrectly` | `ClaimResolutionOutcome.Kind` | Delete the boolean. Policy entry points receive an explicit kind. |
| `ClaimResolvedEvent.ResolvedCorrectly` | Applied claim result `Kind` | Delete the boolean. Notification consumers read the factual disposition. |
| `ActiveClaimData.WasHumane` | `ActiveClaimData.ResolutionKind` | Persist the actual disposition. Pre-migration resolved claims become `Unspecified`; no history is invented. |
| Run `HumaneResolutions` / `BureaucraticResolutions` | `ApprovedClaims`, `DeniedClaims`, `LiquifiedClaims` | New buckets start clean. Old totals remain read-only legacy totals and stop accruing. |
| Meta lifetime humane / bureaucratic totals | Lifetime approved / denied / liquified totals | Preserve both old totals as legacy data. Do not reconstruct new buckets from them. |
| Claim-template humane / bureaucratic reputation fields | Approve / Deny reputation deltas | Preserve serialized values under factual names. Liquify is explicitly neutral until separately designed. |
| Claim-template bureaucratic soul-cost field | Approve soul cost | Preserve serialized value under a factual name. It does not imply correctness. |
| `SupplyContext.ClaimWasHumane` | `SupplyContext.ResolutionKind` | Pass the factual disposition to supplies. |
| Filing Cabinet "humane resolution" trigger | `ResolutionKind.Deny` | Re-key deliberately to Deny. Liquify does not trigger it. Copy describes the disposition, not morality. |
| Compliance combo increment/reset | `ResolutionKind.Approve` advances; Deny/Liquify end | Rename presentation to Compliance Streak and frame it as corporate preference. |
| Desk paper reaction | Approve settles papers; Deny/Liquify scatter | This is an office reaction to the disposition, not moral judgment. |
| Queue-awareness toast | Deny only | Preserve its former normal-path behavior. Liquify requires its own factual receipt instead. |
| Analytics `bureaucratic` boolean | `resolution_kind` string | Emit `approve`, `deny`, or `liquify`; never infer morality downstream. |
| Resolution overlay check/cross | Factual three-kind receipt | Remove grading marks and colors that imply success/failure. |

Authored moral dilemmas remain a separate system. This pass removes inferred morality from claim dispositions; it does not silently remap dilemma choices.

## Applied-result schemas

`AppliedCardResolution` owns one completed card attempt: card/client identity, outcome and reason code, state before/after, actual signed resource deltas, fatigue before/after, and the already-resolved cascade packet.

`AppliedClaimResolution` owns one completed claim disposition: claim/client identity, kind, actual signed Credits/Sanity/Soul/Dark Intelligence deltas, quota before/after, and Compliance Streak before/after.

Presenters consume these applied results. They do not correlate separate events or recompute consequences.

## Expense lifecycle

- Generate and serialize the concrete obligation list once at shift start.
- Store the owning shift number and whether the list has been applied.
- Resume reuses the serialized list. It never consumes the expense RNG stream again.
- Clock-out applies the stored list idempotently. Repeated calls return the same ledger without charging again.
- Mid-shift UI shows obligations due and projected surplus/shortfall. Clock-out shows the itemized paid/unmet ledger and resulting debt.

## Verification invariants

- No runtime claim-resolution dependency on `ResolvedCorrectly`, `WasHumane`, `ClaimWasHumane`, or an equivalent inferred-morality fallback.
- No receipt reports policy intent when clamping or modifiers caused a different applied delta.
- Liquify's Dark Intelligence change is applied and reported through the same result.
- A failure reason cannot be dropped by `FeedbackBudget`.
- Saved obligations survive resume unchanged and cannot be charged twice.
- GDD amendment and the first domain deletion land in the same commit.
