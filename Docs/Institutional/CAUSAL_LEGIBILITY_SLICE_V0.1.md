# Causal Legibility Slice v0.1

Status: implemented and locally validated on 2026-08-03

Branch: `codex/causal-legibility-slice-v0.1`

Base: `4f1133f` (`codex/endogenous-society-v0.1`)

## Player-facing claim

Desk42 now contains a thin playable institutional society loop. A player can
inspect one endogenously generated dispute through incomplete official records,
issue an executable ruling, choose narrow or broad scope, advance the same
deterministic society and trace the public consequences of that decision. The
normal interface never exposes authoritative incident truth, private beliefs,
hidden needs, raw utility scores or assessor traces.

The conservative external statement remains:

> Desk42 currently demonstrates one playable endogenous causal family using a
> bounded incident grammar, a deterministic eight-agent society and an
> incomplete institutional evidence boundary.

This does not yet establish a general AI storyteller, broad narrative variety,
long-running society stability or commercial validation.

## Playable loop

```text
observe the society and docket
    -> inspect evidence provenance, contradictions and missing links
    -> compose finding, disposition, holding, scope, reach and remedy
    -> commit a revision-checked ruling
    -> advance generic autonomous decisions
    -> inspect attributed public consequences and descendant case or absence
    -> replay the same pre-ruling state with another scope
```

The canonical deterministic seed produces one public possession dispute. A
narrow recognised ruling binds the original claimant. A broad recognised ruling
also covers officially matching later disputes; that official protection changes
a connected agent's later decision and produces a descendant case. The UI does
not predict that decision before commitment.

## Five surfaces

| Surface | Player-readable material | Explicit exclusion |
| --- | --- | --- |
| Society | Eight persistent people, official identity/employer, standing and observed actions | private beliefs, hidden needs, utility |
| Docket | issue, parties, allegations, contested propositions, docket basis, deadlines and missing evidence | authoritative event truth |
| Evidence | source, custody, support points, status, contradictions and limitations | fake truth probabilities |
| Ruling | separate finding, disposition, holding, scope, temporal reach and remedy acts | predicted private social outcomes |
| Consequences | public-safe chronology, immediate cause, ruling and scope attribution, known pressures and unknowns | omniscient explanation |

## Runtime boundary

`Desk42.Institutional.Player` is a no-engine-reference assembly between
simulation authority and Unity presentation. It exposes immutable, get-only
records through `PlayerInstitutionView` and an explicit projector. The product
assembly references `Domain` and `Player`; it no longer references `Authority`,
`Scenarios` or `Runtime` directly.

The projection is deliberately selective rather than a reflection-based copy.
Its public type graph excludes:

- `IncidentCandidate` and authoritative lived events;
- `AgentPerception`, `AgentDecision` and candidate evaluations;
- private belief and need collections;
- physical possession truth;
- assessor-only scope traces and run snapshots.

Changing hidden beliefs, needs or possession without changing the official
record leaves the public-view signature unchanged.

## Command integrity

Every draft carries:

- `ExpectedCaseRevision`;
- `EvidenceEnvelopeHash`.

The session rejects a stale draft if either no longer matches the case being
ruled. Scope previews are generated from current official state only. They show
present matches and explicitly refuse to predict future disputes.

Every attributable public timeline row can carry:

- immediate cause ID;
- originating ruling ID;
- applied holding ID;
- scope match ID;
- source observable ID.

## Persistence and replay

The playable facade saves both the active history and a detached pre-ruling
origin through the existing checksum and backup snapshot store. Load restores
the visible history; replay restores the identical pre-ruling public view and
permits a counterfactual ruling without leaking the other branch.

## Automated validation

Unity Editor: `2022.3.62f3`.

```text
Full EditMode:     394 total, 394 passed, 0 failed, 0 skipped
Focused PlayMode:    2 total,   2 passed, 0 failed, 0 skipped
Windows x64 build: success
Built-player smoke: exit 0, DESK42_SMOKE_OK
Built-player capture: exit 0, DESK42_CAPTURE_OK
```

The PlayMode tests load the product scene, verify the public case and five
navigable surfaces, commit the broad ruling, observe the descendant case, save,
replay and load the ruled history.

The built-player smoke independently commits a broad ruling, saves and reloads
the playable history and verifies two cases and one ruling before returning zero.

Curated build evidence is recorded in
`evidence/CausalLegibility/v0.1/README.md`.

## Human acceptance gate (not yet claimed)

Run the same deterministic slice with six fresh testers. Ask what happened,
what the institution knew, which evidence they trusted, what their ruling and
scope changed, what later action they attribute to the ruling, what surprised
them and what they would change on replay.

The gate passes only if:

- 5/6 distinguish allegation from established fact;
- 5/6 understand that authoritative truth is withheld;
- 4/6 identify what their scope applied to;
- 4/6 attribute the later divergent action to their ruling;
- 4/6 intentionally choose a different scope or remedy on replay;
- 4/6 explain one causal chain without assessor information.

No human comprehension result is asserted at this checkpoint. The software is
ready for that test; it has not substituted automated correctness for player
legibility evidence.

## Deliberate exclusions

This cycle adds no Director intelligence, incident family, authored fixture,
management economy, departments, base construction, combat, population scale,
art production, voice acting, campaign progression or scenario editor.

The next product decision should be based on six-player comprehension evidence.
Backend breadth should remain frozen until the causal chain is demonstrably
readable and intentionally manipulable by players.
