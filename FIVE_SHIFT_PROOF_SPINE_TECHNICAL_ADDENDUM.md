# Desk 42 Five-Shift Proof Spine v0.3

## Technical Plumbing Addendum v0.2

Status: **authoritative for the validation build**

This addendum supersedes Technical Plumbing Addendum v0.1. It defines the
minimum runtime and test plumbing needed to answer one question:

> Does a returning character make the player feel, without prompting,
> "I did this"?

It does not authorize a generic recurring-character engine.

## 1. Proof-session boundary

The proof is a single-session sequence of five separate Desk 42 shifts. A
normal shift is still one `RunData`; beginning the next shift creates fresh
run data.

Elias state therefore belongs to an `EliasProofSessionState` owned by a
`DontDestroyOnLoad` proof controller under `GameManager`, not to `RunData`.

Required:

- state survives fresh runs for Shifts 1 through 5;
- state survives normal scene/controller reconstruction;
- state resets explicitly at proof start and proof end;
- the validation build does not require quit-and-resume persistence.

Test infrastructure is separate from runtime ownership. Automated proof runs
must use fixture meta and `SaveSystem.SetSaveDirectoryOverrideForTests`.

## 2. Stable authored identity

Elias uses the stable authored ID `elias_venn`. Generated names, species
suffixes, random claimant IDs, and archetype instances may not identify him.

An Elias proof content asset owns:

- stable claimant ID;
- display name;
- portrait/visual profile where available;
- proof-spine role;
- authored appearance claim IDs;
- configured queue positions;
- authored follow-up claim IDs.

Ordinary claimant generation must never regenerate or rename Elias.

## 3. Minimum proof-session state

Logical state:

```text
EliasShift1Disposition: None | Approved | Denied
EliasShift2Branch: None | NormalisedAddress | LegacyException | PhysicalVerification
Shift2FinalDisposition: Unspecified | Approve | Deny | Liquify
RecordedAppearanceKeys: set<string>
ActiveAftermathModifier
ProofSessionId
```

`EliasShift2Branch` is authoritative for Shift 5 routing. It may not be
derived from Compliance Streak, Tide pressure, or another mutable gauge.

Audit Risk is not part of this proof. It is neither a renamed Tide value nor a
new validation-build gauge.

## 4. Visit semantics and idempotency

The BSM consumes **prior visits** while instrumentation reports the
**current visit number**:

| Appearance | Prior visits passed to logic | Current visit number logged |
| --- | ---: | ---: |
| Shift 1 | 0 | 1 |
| Shift 2 | 1 | 2 |
| Shift 5 | 2 | 3 |

Visit recording is exact-once and keyed by:

```text
elias_shift_1
elias_shift_2
elias_shift_5
```

Encounter or scene reconstruction may not increment the same appearance
twice. The visit transaction captures `priorVisits`, records the appearance,
then exposes both values explicitly. A development assertion verifies every
row above.

## 5. Two-stage authored-claim lifecycle

Elias procedure actions **precede** the normal claim disposition.

Stage 1:

```text
Preview procedure
-> validate procedure
-> apply actual procedure mutations
-> write the authoritative Elias branch
-> publish AppliedEliasProcedure
-> render the procedure receipt
```

Stage 2:

```text
Approve | Deny | Liquify
-> normal ClaimResolutionKind policy and application
-> AppliedClaimResolution
-> normal factual claim receipt
-> record the final disposition against the Elias procedure record
```

The procedure changes the record. The later disposition changes the claim.
The player may amend the record and then deny the claim; both facts are
retained.

This preserves `ClaimResolutionKind` as `Approve`, `Deny`, or `Liquify`. No
Elias-only fourth disposition is introduced.

## 6. Procedure action IDs

Elias procedures are authored claim actions, not punch cards and not
disposition aliases:

```text
AmendRecord
RetainLegacyUnit
ReferForReview
RequestClarification
```

| Action ID | Stage | Terminal? | Branch written | Canonical disposition |
| --- | --- | --- | --- | --- |
| `AmendRecord` | Shift 2 procedure | No | `NormalisedAddress` | Chosen later |
| `RetainLegacyUnit` | Shift 2 procedure | No | `LegacyException` | Chosen later |
| `ReferForReview` | Shift 2 procedure | No | `PhysicalVerification` | Chosen later |
| `RequestClarification` | Authored Shift 5 procedure | No | None | Chosen later |

One validator owns preview availability and execution. Disabled UI and backend
rejection must agree.

Liquify remains visible but unavailable for every authored Elias appearance
under failure reason `RecurringClaimantContinuityHold`. Player-facing copy:

```text
LIQUIFY UNAVAILABLE - CONTINUITY REVIEW HOLD
```

## 7. Shift 1 convergence

Shift 1 resolves through the ordinary disposition layer:

- Approve writes `EliasShift1Disposition = Approved`.
- Deny writes `EliasShift1Disposition = Denied`.
- Liquify is rejected by the continuity hold.

Both successful dispositions schedule Shift 2. They may alter the Shift 2
opening line but do not create separate long-term branches.

## 8. Shift 2 procedure application

Procedure application writes the branch immediately and atomically with
`AppliedEliasProcedure`:

```text
AmendRecord       -> NormalisedAddress
RetainLegacyUnit  -> LegacyException
ReferForReview    -> PhysicalVerification
```

Later Approve or Deny does not erase or replace that branch. Generic claimant
processing cannot mutate Elias proof state.

The proof-session decision record retains both the procedure and final
disposition so Shift 5 content can distinguish, for example, "amended then
denied" from "amended then approved".

## 9. Applied procedure result

`AppliedEliasProcedure` contains:

- proof session ID;
- appearance key;
- stable claimant ID;
- action ID;
- resulting branch;
- prior visits and current visit number;
- actual applied resource and Compliance Streak deltas;
- address before and after;
- Miriam registration reference;
- procedure receipt ID.

Presenters consume this result and never recompute it. Stage 2 continues to
use the existing `AppliedClaimResolution`.

## 10. Copy contract

The proof reports actions and consequences; it never grades the procedure or
disposition.

Required Shift 2 Branch A sequence:

```text
RECORD AMENDED
18B -> 18A
M. VENN - REGISTERED 18A
CLAIM ACCEPTED FOR PROCESSING
COMPLIANCE STREAK +1
```

The following are banned from authored claim feedback unless quoted as
diegetic character speech and explicitly reviewed:

```text
correct
corrected
valid choice
better choice
optimal
mistake
wasted
penalty
should have
```

Miriam's registration receives its own visual beat before reward feedback.

## 11. Deterministic authored scheduling

Generate the ordinary claim queue first, then replace configured slots. This
preserves seed-stream consumption, queue size, and quota.

Validation-build defaults:

- Shift 1: Elias replaces claim 2.
- Shift 2: Elias replaces claim 2.
- Shift 5: Elias replaces claim 3.
- Shift 5 aftermath claims occupy claims 4 and 5 where two uses are required.

Elias appears exactly once in Shifts 1, 2, and 5 and never through random
claimant selection.

At Shift 5:

```text
NormalisedAddress    -> Shift 5A
LegacyException      -> Shift 5B
PhysicalVerification -> Shift 5C
None                 -> development failure
```

Missing state may not select a random fallback.

## 12. Shift 5 tool locks

The branch is loaded before the authored action UI is constructed:

- 5B disables `RequestClarification`.
- 5C disables `ReferForReview`.
- 5A presents the linked address amendment as evidence conflict.

The same validator controls presentation and execution. A disabled action
cannot execute through a different input route.

## 13. Authored aftermath claims

After Elias resolves in Shift 5:

- 5A activates `HouseholdDuplicateReview` for two authored follow-up IDs.
- 5B activates `InternalAuditLockdown` for one authored follow-up ID.
- 5C activates `VerificationBacklog` for two authored follow-up IDs.

The proof uses exact authored claim IDs rather than a general household tag
or generic recurrence/condition engine. The narrative spine remains
authoritative for each modifier's mechanical effect; the plumbing stores:

```text
ModifierId
PendingClaimIds
AppliedClaimIds
```

Application and consumption are exact-once. Unrelated claims do not consume
the effect, and no effect survives the proof session.

## 14. Instrumentation

Each proof route records:

- anonymised test ID and proof session ID;
- stable Elias ID;
- Shift 1 disposition;
- prior/current visit numbers for all three appearances;
- Shift 2 action, branch, procedure receipt ID, and final disposition;
- actual Compliance Streak delta;
- Shift 5 branch and variant;
- compromised tool state;
- Shift 5 resolution;
- aftermath modifier and authored claims affected.

Development builds flag:

- generated/unstable Elias identity;
- missing or duplicate appearance visit;
- branch overwrite;
- Shift 5 scheduled with `None`;
- wrong Shift 5 variant;
- UI/backend action-lock disagreement;
- duplicate aftermath application.

## 15. Automated pre-content gate

Run one parameterised PlayMode route for each Shift 2 branch:

```text
Spawn Shift 1 Elias
-> prior/current visits 0/1
-> resolve Approve or Deny
-> spawn Shift 2 Elias
-> prior/current visits 1/2
-> apply one procedure
-> render the applied procedure receipt
-> choose a normal disposition
-> complete Shifts 3 and 4
-> spawn Shift 5 Elias
-> prior/current visits 2/3
-> load 5A, 5B, or 5C
-> enforce the branch tool lock
-> resolve Elias
-> apply and expire the authored aftermath effect
```

The harness uses fixture meta and an isolated save directory. The real save
files must remain byte-for-byte unchanged.

## 16. Card-face prerequisite

Before human proof testing, card hierarchy must be verified against both
effect grammars:

- a mood transition such as `PENDING -> SUSPICIOUS`;
- a timed client effect such as Pending Review or Cooperation Route.

The expected-effect block is dominant. Title, certainty, fatigue, and cost are
secondary. Verify at minimum and target resolution.

## 17. Human proof protocol

The first question is asked verbatim:

> Why do you think Elias was in that situation?

The response is recorded before any follow-up. Attribution counts only if it
appears unaided in this answer.

After the primary answer is banked, follow-ups may ask:

1. Who is Elias?
2. What do you remember happening earlier?
3. Why did the later outcome happen?

The matched non-causal control question from the narrative test script follows
the primary question without suggesting that an earlier player action matters.

Pre-registered thresholds for six testers:

- strong pass: at least 4 of 6 show unaided attribution on the primary
  question, with no equivalent causal confabulation on the control;
- weak pass: exactly 3 of 6;
- fail: fewer than 3 of 6.

Only the primary non-leading answer determines pass/fail. Follow-ups diagnose
recognition and memory depth.

## 18. Visual evidence

Capture:

- minimum-resolution and target-resolution card-hand screenshots;
- Shift 2 and Shift 5 context screenshots for every branch;
- at least ten seconds of video per receipt branch covering the complete
  anchor sequence and reward timing.

A screenshot cannot prove that Miriam's registration landed before the
Compliance Streak beat.

## 19. Out of scope

Do not build for this proof:

- cross-session Elias branch persistence;
- a generic recurring-character scheduler;
- permanent per-client disposition history;
- a generic household-claim taxonomy;
- a general-purpose next-N-claim condition engine;
- additional recurring characters;
- card art or cascade-juice expansion.

Generalise only when it is clearly smaller and safer than the proof-specific
path.
