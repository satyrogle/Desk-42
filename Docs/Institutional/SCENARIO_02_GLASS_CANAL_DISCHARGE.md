# Scenario 02 Specification — The Cloud That Became Weather

> Status: pre-extraction scenario specification. This document defines the
> second generalisation test; it is not an implementation plan and does not
> authorise scenario-specific transitions inside the institutional core.

## Purpose

The Glass Canal discharge is the counterexample used to discover the smallest
reusable institutional engine. It must pass through the same evidence, ruling,
reliance, appeal, holding, precedent, consequence, and public-observation
phases as the workplace-identity proof while sharing none of that proof's legal
issue, material resource, recognition defect, reliance object, scope facts, or
descendant outcome.

The scenario is intentionally specified before extraction. Any abstraction
that only serves the workplace case is not yet generic.

## One-line scenario

> A licensed pocket cloud crosses its greenhouse parcel and rains copper into
> a downstream cistern; the institution rules that crossing the boundary made
> the discharge ownerless weather.

The anomaly is mechanically necessary. The cloud has a bounded trait:

```text
BOUND WEATHER TRAIT
  when output crosses a registered parcel boundary:
    visible ownership markers become unreliable
    controller resonance persists until dissipation
```

Official classification and physical behaviour disagree. Ordinary pollution
could not be substituted without removing the resonance evidence, the
licensed-anomaly status, and the rule whose scope later binds other
bound-weather entities.

## Structural difference contract

| Dimension | Workplace proof | Glass Canal requirement |
| --- | --- | --- |
| Issue family | Employment identity continuity | Environmental and public-utility liability |
| Scarce resource | Paid shift allocation | One physical municipal filter cartridge, `CF-9` |
| Evidence topology | Roster, testimony, clinical and action records | Sealed water sample, drain telemetry, controller log, permit map and anomalous resonance observation |
| Initial official defect | Superseded identity and identifier mismatch | Parcel-boundary rule severs recognised control from continuing physical control |
| Reliance | Treatment purchase and abandoned household aid | Irreversible surrender or decommissioning of a private rooftop condenser |
| Appeal question | Whether employment continued across identity change | Whether control of licensed anomalous output follows it beyond a property line until dissipation |
| Scope target | Employer plus identity condition | Watershed plus bound-weather permit class plus undissipated output |
| Descendant case | Later worker allocation | Later downstream purification-priority dispute after a second plume |
| Connected winner and loser | One paid shift | The single `CF-9` cartridge and its exclusive potable-water entitlement |

The implementation must not use `WorkAllocationState`,
`WorkAllocationObservation`, a paid-shift status, or wage/backpay consequence
kinds for this scenario.

## Participants

Names are presentation fixtures. The scenario definition resolves semantic
participant predicates; engine code never recognises these names or IDs.

| Presentation name | Scenario role | Required semantic predicate |
| --- | --- | --- |
| Mara Kest | Primary downstream claimant | registered Glass Canal water user with an affected cistern |
| Nara Quill | Bound-weather operator | recognised holder of a bound-weather operating permit |
| Khet Daro | Primary inspector | environmental sampling authority and canal access |
| Orin Pell | Competing sampler | canal access without primary sampling authority; has routine work available |
| Ilya Ro | Controller witness | access to cloud-controller telemetry |
| Sera Vale | Later downstream claimant | different Glass Canal water user exposed to the second plume |
| Vey Ankar | Baseline cartridge holder | current recognised holder of the exclusive `CF-9` entitlement |
| Toma Rill | Watershed representative | recognised community or watershed representation standing |

The fixture may contain additional bystanders. Participant resolution must not
require the population to contain exactly eight agents. Equivalent profiles
must preserve the causal pattern; deliberately different profiles must change
the relevant choice traceably.

## Scenario facts and identifiers

The scenario owns these values. The institutional core treats them as opaque
data.

```text
scenario id       scenario.glass-canal-discharge
primary issue     issue.output-control-after-boundary-crossing
primary case      case.glass-canal-discharge
later case        case.downstream-purification-priority
watershed         watershed.glass-canal
permit class      permit.bound-weather
output condition  output.undissipated
resource          filter-cartridge.cf-9
recognition       status.continuing-output-control
liability         status.self-contamination-liability
potable relief    status.municipal-potable-relief
entitlement       status.municipal-filter-entitlement
```

Case facts are unordered key/value pairs. The scenario requires at least:

```text
watershed    = glass-canal
permit-class = bound-weather
output-state = undissipated
```

Precedent scope is an all-of predicate over facts. It is not an employer-only
structure and must not depend on positional party lists.

## Evidence topology

Evidence classes are data identifiers rather than additions to a universal
enum.

| Evidence class | Source | Direction | Special requirement |
| --- | --- | --- | --- |
| `evidence.water.sealed-sample` | Inspector's autonomous sampling action | supports continuing control | exclusive sample opportunity and intact custody chain |
| `evidence.utility.drain-telemetry` | Canal sensor record | supports physical path | entered before the relevant evidence cutoff |
| `evidence.anomaly.controller-log` | Controller witness disclosure | supports continuing control | identifies a post-boundary correction pulse |
| `evidence.permit.boundary-map` | Operator response | opposes continuing control | establishes the official parcel boundary, not physical dissipation |
| `evidence.anomaly.resonance` | Witness observation of bounded trait | supports continuing control | only probative while output is undissipated |
| `evidence.valve.residue` | Late inspection | supports continuing control | unavailable to the initial ruling; admissible on appeal |

Configuration-specific policy data supplies weight and reliability rules for
these class IDs. The engine performs scoring and freezes the exact as-of-cycle
evidence set used by every finding and ruling.

## Contested opportunity

At cycle 2, Khet and Orin can both rank the single sealed-sample opportunity.
Each also has a valid routine-work candidate.

The generic decision phase must produce ranked plans against one frozen cycle
snapshot. The generic allocator then resolves capacity in stable order:

```text
both agents rank sealed sample
→ stable reservation awards the sample to one
→ rejected agent attempts the next still-valid ranked candidate
→ rejected agent performs routine work, not forced Idle
```

The trace records the rejected reservation and selected fallback. The scenario
does not choose the winner directly.

## Exclusive resource

`CF-9` is one indivisible municipal filter cartridge. The authoritative
allocation has exactly one recognised holder.

An atomic transfer must update all connected state or update none:

```text
previous holder entitlement revoked
new holder entitlement granted
authoritative holder changed
holder-specific material/need effects applied
paired public winner/loser consequence emitted
one ruling or precedent application recorded as the shared cause
```

The scenario declares the resource and effect recipe. Only the generic
consequence executor performs the transfer.

## Reliance contracts

Reliance is not synonymous with spending a positive provisional entitlement.
It records an irreversible action taken because an identified official state
was expected to remain operative.

### Denial reliance

Under Licensed Output Accountability, the initial denial marks Mara as liable
for self-contamination and opens a compliance route. Mara may autonomously
surrender her rooftop condenser to satisfy that official liability and regain
water-service access. The condenser becomes unavailable and the associated
conversion cost remains after reversal.

```text
dependency:
  status.self-contamination-liability = recognised
action:
  surrender alternative.rooftop-condenser
irreversible effects:
  alternative unavailable
  account/resource cost recorded
  household water strategy displaced
```

### Grant reliance

Under Precautionary Access, provisional municipal potable relief makes the
private condenser appear redundant. Mara may autonomously decommission it.
If the operator's appeal later removes relief, the condenser is not restored;
surviving reliance creates a stranded-condenser recovery case.

The generic reliance record therefore needs an expected official state/value,
the causal agent action, the ruling relied upon, the abandoned alternative,
and irreversible effects. Reversal creates further consequences rather than
rewinding history.

## Eighteen-cycle authored calendar

The calendar opens windows and deadlines. It may not directly select agent
actions or perform institutional transitions.

| Cycle | Authored opening | Systemic result when selected/eligible |
| ---: | --- | --- |
| 0 | Seed baseline `CF-9` entitlement for Vey through a recorded baseline ruling | one exclusive entitlement exists |
| 1 | Cloud discharge reaches Mara's cistern | lived event, health/water pressure and embodied belief |
| 2 | Open one sealed-sample opportunity and routine work | ranked contention and fallback |
| 3 | Open drain-telemetry disclosure | evidence may enter from autonomous disclosure |
| 4 | Open operator response | permit map or controller representation may enter |
| 5 | Close initial evidence window | exact evidence envelope freezes |
| 6 | Initial hearing deadline | generic finding, ruling and effects execute |
| 7 | Open compliance or potable-relief response | agent may create reliance or choose work/idle |
| 8 | Observation window | material effects become publicly observable |
| 9 | Open valve-residue and controller-log disclosures | late evidence may enter |
| 10 | Open primary appeal docket | eligible parties receive appeal candidates |
| 11 | Primary appeal filing deadline | autonomous filing creates the appeal |
| 12 | Primary appeal hearing | reversal/affirmance; configuration C may establish scoped holding |
| 13 | Second undissipated plume reaches Sera | descendant lived incident and need pressure |
| 14 | Open later sample/report opportunity | autonomous evidence action may create later case |
| 15 | Later initial hearing | ruling without retrospectively changing cycle-15 choices |
| 16 | Open later appeal docket | Sera may autonomously appeal |
| 17 | Later appeal hearing | matching holding may alter ruling and transfer `CF-9` Vey→Sera |
| 18 | Comparison close | snapshot public and assessor state |

Ordering within every cycle remains:

```text
bounded anomaly response
→ freeze perceptions, regime, input and opportunities
→ rank all agent plans
→ resolve contested capacities and fallbacks
→ apply selected agent actions
→ project evidence/opportunity consequences
→ resolve institutional deadline
→ emit public observation
```

A same-cycle ruling never retroactively changes a decision made from the
frozen pre-ruling snapshot.

## Institutional configurations

### A — Boundary Literalism

- Permit and parcel records dominate the evidence rule.
- Initial and final dispositions deny continuing operator control.
- Mara receives the self-contamination liability/levy.
- Routine work outranks condenser surrender for Mara's actual profile.
- No reliance, appeal, holding, precedent application or `CF-9` transfer
  occurs.

This configuration proves that the authored calendar does not force the full
chain.

### B — Precautionary Access

- Initial procedure grants provisional municipal potable relief.
- Mara decommissions the condenser in reliance on that recognised relief.
- The operator autonomously appeals.
- The appellate source-certification rule reverses to denial without creating
  a general holding.
- The irreversible condenser loss survives and creates a recovery case.
- No precedent changes the later cartridge allocation.

This configuration proves reliance can survive reversal without implying
precedent.

### C — Licensed Output Accountability

- The initial high burden produces denial and self-contamination liability.
- Mara autonomously surrenders the condenser through the compliance route,
  creating denial reliance.
- Late valve/controller evidence makes appeal worthwhile.
- Mara autonomously appeals.
- The appellate ruling recognises continuing control and establishes:

```text
Licensed bound-weather output remains operator-controlled
after leaving its registered parcel until physical dissipation.
```

- Scope requires `watershed=glass-canal`,
  `permit-class=bound-weather`, and `output-state=undissipated`.
- The second plume creates Sera's later case through autonomous evidence.
- Sera autonomously appeals the later denial.
- The matching holding changes the later disposition and atomically transfers
  `CF-9` from Vey to Sera.

This is the one configuration required to produce the complete causal chain.

## Complete-chain acceptance path

Under at least one deterministic seed/configuration:

```text
lived discharge
→ autonomous sealed-sample action
→ official case
→ initial ruling
→ material status/resource change
→ autonomous irreversible reliance
→ autonomous appeal
→ scoped holding
→ holding applied to later case
→ atomic CF-9 transfer
→ connected named winner and loser
```

Removing the sampling action must remove the evidence/case path. Removing the
reliance opportunity must remove reliance without removing the initial ruling.
Removing the appeal capability must remove the holding and precedent outcome.
Removing the later evidence action must remove the descendant case and
transfer while leaving the primary appeal intact.

## Scenario-layer authority

Scenario code may declare:

- semantic participant queries;
- incident and resource seeds;
- pacing windows and deadlines;
- evidence and opportunity templates;
- issue, remedy, fact and presentation identifiers;
- policy/evidence-weight data;
- validated generic effect recipes.

Scenario code may not directly:

- score evidence or construct findings;
- issue rulings or mutate official status;
- create or mutate reliance ledgers;
- resolve appeals or establish holdings;
- match or apply precedent;
- transfer resources or adjust accounts/needs;
- construct the public report.

No engine source may branch on this scenario ID, participant names, evidence
IDs, issue IDs, resource IDs, configuration labels, or presentation strings.

## Engine capabilities forced by comparison

The comparison with the workplace proof requires these shared contracts:

1. detached perception, regime, simulation-input and opportunity snapshots;
2. ranked action plans plus deterministic capacity arbitration and fallback;
3. opaque evidence class IDs and data-driven phase weight rules;
4. generic case facts and all-of precedent scope predicates;
5. generic as-of-cycle evidence envelopes, findings, rulings and appeals;
6. explicit status mutation results, including safe no-op outcomes;
7. generic reliance dependencies and irreversible effects;
8. exclusive-entitlement allocation and atomic transfer;
9. generic consequence execution and public projection;
10. causal validation without employer, identity, wage or paid-shift
    assumptions.

These contracts may initially share assemblies. They need explicit state
ownership and one-way dependencies, not one class per noun.

## Generalisation gate

After the engine-candidate commit is frozen:

1. record a manifest and SHA-256 digest for every engine source path;
2. implement this scenario only beneath its scenario directory and matching
   test/presentation fixture directories;
3. make no changes to any manifested engine file;
4. prove both fixtures still pass deterministic replay and causal validation;
5. scan engine sources for both scenarios' IDs, names and proposition/resource
   identifiers;
6. prove the three configurations include and omit phases for causal reasons;
7. prove the `CF-9` transfer is atomic and causally paired;
8. prove no `WorkAllocationState` type appears in Scenario 02.

Only after this gate passes may the evidence claim advance from “authored
vertical proof” to “reusable institutional simulation architecture.”

Procedural composition of new disputes remains a separate future claim.
