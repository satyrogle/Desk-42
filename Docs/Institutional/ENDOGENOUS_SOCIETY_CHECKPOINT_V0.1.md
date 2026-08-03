# Endogenous Institutional Society — Engineering Checkpoint v0.1

Status: implemented and locally validated on 2026-08-03

Branch: `codex/endogenous-society-v0.1`

Base: `67874ba` (`codex/institutional-product-v0.1`)

## Evidence-backed claim

Starting from material state rather than a plot calendar, generic utility-driven
agents can create lived incidents. Only observable allegations and records make
those incidents eligible for the institutional docket. A validated player ruling
can then apply a bounded, executable scope to later official perception. In a
canonical counterfactual fork, changing only that scope changes a connected
agent's utility, selected action, material outcome and descendant case.

The active causal chain can be saved and restored at every committed phase
boundary without changing the final serialized result.

This checkpoint supports the description:

> An endogenous institutional society simulation in which autonomous actions
> create case instances and player rulings alter future social behaviour.

It does **not** yet support describing the product as a general AI storyteller.

## Implemented cycle

| Increment | Result | Commit |
| --- | --- | --- |
| E1 — Material world | Physical possession, official ownership, access, authority, resource quantity, observable material events and collective commitments are distinct authoritative state. | `8a6a868` |
| E2 — Endogenous actions | Lie, Steal, Retaliate and Organise use the shared frozen utility pipeline; none is selected by a scenario calendar. | `44d3360` |
| E3 — Docket pipeline | Authority-only incidents become public docket candidates only through observable records or submitted allegations; deterministic admission runs with the Director disabled. | `4e50071` |
| E4 — Player ruling | A validated command freezes the evidence envelope, facts, disposition, holding, bounded scope, prospective reach and remedy; replay is idempotent. | `1e73c84` |
| E5 — Scope counterfactual | Narrow and broad rulings fork from one detached pre-ruling state. Scope is the first causal divergence and propagates into action and descendant-case differences. | `64f0313` |
| E6 — Active-chain persistence | Society, material world, docket, cases, observations, rulings, scope traces, cursors and exact-once IDs persist with checksum and backup recovery. | `1701bb1` |

## Causal architecture

```text
material world + perceived state
    -> generic action opportunities
    -> frozen utility decision
    -> authoritative lived event
    -> witness belief / submitted record / allegation
    -> public-safe docket candidate
    -> deterministic case admission
    -> validated player ruling
    -> executable scope match
    -> later official perception
    -> later utility decision
    -> later lived event and descendant case
```

The institution never opens a case directly from `IncidentCandidate`. That type
remains inside the Authority assembly. The public docket contains only evidence
and allegations that have crossed an observable institutional boundary.

## Acceptance evidence

| # | Required property | Evidence |
| --- | --- | --- |
| 1 | No Workplace Identity or Glass Canal definition drives the proof | `EndogenousAuthorityAssembly_HasNoScenarioAssemblyDependency` plus the Authority assembly reference boundary |
| 2 | No calendar, participant role or presentation name selects the chain | Endogenous opportunities are derived from current perceived/material state; identity-remap and counterfactual tests preserve the chain |
| 3 | Director disabled | `Admission_IsOldestThenHighestHarmThenStableId_WithDirectorDisabled`; `DirectorEnabled_IsRejectedFromProofPath` |
| 4 | Seed creates opportunities, not selected actions | `Steal_FromGeneratedOpportunity_ChangesPossessionButNotOwnership` and the shared candidate evaluation pipeline |
| 5 | Hidden lived incident does not become a case | `RemovingObservability_PreservesLivedIncidents_ButRemovesOfficialCase` |
| 6 | Observable incident becomes a case | `AutonomousPossessionActions_CreateIncidents_ButOnlyRecordedOneBecomesCase` |
| 7 | Lie uses source evaluation | `Lie_UpdatesListenersThroughSourceEvaluation_NotDirectAssignment` |
| 8 | Steal changes possession, not ownership | `Steal_FromGeneratedOpportunity_ChangesPossessionButNotOwnership` |
| 9 | Retaliate requires belief and power | `Retaliate_RequiresPerceivedPriorActionAndActivePowerRelationship` |
| 10 | Organise requires compatible autonomous actions | `Organise_RequiresMultipleCompatibleAutonomousActions` |
| 11 | Procedurally valid ruling may be factually wrong | `Denial_CanBeProcedurallyValidWhileContradictingLivedIncidentTruth` |
| 12 | Scope variants share one pre-ruling state | `CanonicalForkCopies_AreDetachedAndInitiallyByteEquivalent` |
| 13 | Scope is the first fork divergence | `NarrowAndBroadScope_FromSameSnapshot_TraceFirstDivergenceToDescendantCase` |
| 14 | Scope changes a later decision | same counterfactual trace; protection changes the utility comparison and selected action |
| 15 | Decision changes descendant case | same counterfactual trace |
| 16 | Original-action ablation removes the chain | `RemovingOriginalMaterialOpportunity_RemovesActionAndCaseChain` |
| 17 | Observability ablation preserves reality but removes case | `RemovingObservability_PreservesLivedIncidents_ButRemovesOfficialCase` |
| 18 | Identity remapping preserves causal structure | `IdentityRemap_PreservesGenericCausalPattern` |
| 19 | Load-bearing perceived state changes behaviour | `HoldingProtectionStatus_IsLoadBearingForLaterDecision` and action perturbation tests |
| 20 | Resume at each committed boundary is deterministic | `SaveLoadAtEveryCommittedBoundary_ReproducesUninterruptedSnapshotByteForByte` |
| 21 | Public surface excludes authoritative incident truth | `AuthorityIncidentTruth_IsAbsentFromPublicDocketTypeGraph` and existing assembly truth-boundary gates |
| 22 | Ruling replay is idempotent | `Commit_ReplayIsIdempotent_ConflictingPayloadIsRejected`; restored replay test |
| 23 | Causal envelopes do not duplicate incidents or cases | `Pipeline_ReprocessingSameCausalGraph_IsIdempotent`; restored replay test |
| 24 | Keyed variation cannot override meaningful utility | `VariationKeyed_CannotOverturnMeaningfulUtilityDifference` |

## Deterministic variation contract

`variation.keyed` is deterministic tie-scale variation derived from the master
seed, tick, simulation ordinal and candidate semantic key. It can resolve close
choices reproducibly. It cannot create availability, assign narrative roles,
read presentation names, override hard constraints or reverse a utility gap
larger than its declared amplitude.

## Validation

Local Unity version: `2022.3.62f3`.

```text
Complete EditMode run: 385 total, 385 passed, 0 failed, 0 skipped
Institutional tests:    381 total, 381 passed, 0 failed, 0 skipped
```

The complete run compiles every active product assembly. A standalone player
build was not used as a gate for this backend checkpoint; the player-facing loop
and presentation are still deliberately outside this cycle.

## Deliberate limits and next gate

- The incident vocabulary is a bounded authored grammar, not arbitrary doctrine
  invention.
- The counterfactual proves one complete scope-to-descendant causal family; more
  social issue families are needed before claiming broad narrative variety.
- Long-running population stability, pacing and performance are not proved.
- Appeals, reliance, public observations and exclusive entitlements are explicit
  snapshot fields, but are inactive in this minimal endogenous proof.
- There is no production player interface for observing or authoring the chain.
- The Director remains intentionally disabled and is not required for causality.

The next cycle should make this simulation legible and playable without weakening
the truth boundary: expose public observations, evidence, candidate reasoning and
ruling scope through a thin interaction layer, then test whether a player can
predict and intentionally alter the causal machine.
