# Institutional Engine Candidate Boundary

Status: v0.4 extraction and freeze contract.

This document compares the preserved Workplace Identity proof with the Glass
Canal discharge specification. It defines ownership and dependency direction;
it does not claim that the second-scenario gate has passed.

## Candidate history and falsification

The immutable tag `institutional-engine-candidate-v0.1` is preserved at commit
`451982aeb06af0c1e36e944ab2b1e87897221194`, together with its original
`evidence/InstitutionalEngine/v0.1/` package. That checkpoint remains credible
evidence for a deterministic generic substrate and one declarative control
fixture. It is not rewritten or relabelled as the two-scenario candidate.

Auditing the frozen Glass Canal specification against v0.1 falsified two
claimed generic seams before Scenario 02 was authored:

1. citation declarations were case-wide, so a matching holding could not be
   directed only to an exact appellate ruling; and
2. primary cases were unconditionally active, so removing the autonomous
   sampling action could remove evidence but not the primary case path.

Those are engine-contract failures, not permissions for Glass Canal code to
special-case the scenario. Candidate v0.2 adds two generic capabilities:

- citations are declared and applied against an exact case, ruling and
  holding; and
- any case, including the primary case, may be activated by a declared causal
  agent action while the exact trigger evidence can enter before activation.

The immutable tag `institutional-engine-candidate-v0.2` is preserved at commit
`55e507b5a623573dacb2df38b0bf790411c3b325`, together with its original
`evidence/InstitutionalEngine/v0.2/` package. Before Scenario 02 source was
authored, a literal contract audit found that its opaque evidence rules could
vary class weight but not the policy's reliability treatment of that class.
Source-event reliability existed, but it was not configuration-specific as the
frozen Glass Canal specification requires.

Candidate v0.3 preserves source reliability and adds an independently validated
policy-reliability percentage to every opaque evidence-class rule. Adjudication
applies it after the existing weight and source-reliability truncation stages;
the default value of 100 reproduces every existing v0.2 fixture score and bound
exactly. Checked arithmetic deliberately rejects extreme overflowing DTO inputs
that v0.2 did not validate safely.

Candidate v0.3.1 changes no executable or test behavior. It narrows the evidence
wording above so the compatibility claim does not include malformed or extreme
overflowing inputs.

Literal implementation of Glass Canal then falsified one further v0.3.1 seam:
the reliance service applied an irreversible action and published every public
row on one shared cycle. It could not represent the frozen contract's cycle-7
choice followed by cycle-8 public observation without either moving the causal
action or leaking future-dated public rows early.

Candidate v0.4 separates those clocks generically. The action cycle still owns
the autonomous decision, authoritative reliance ledger, economic and need
changes, and abandoned alternative. A run-owned assessor queue holds detached
public snapshots until a declared observation cycle. The final phase of that
cycle atomically publishes the reliance observation, material projections and
timeline entry. Default `-1` remains same-cycle behavior for existing scenarios.

The v0.4 candidate is not established until its protected manifest and
immutable tag both exist. The gate fails closed while either is absent.

## Preserved pulse order

Every cycle retains one deterministic causal order:

1. apply bounded status-driven anomaly effects;
2. capture all agent perceptions, regime values, inputs and opportunities;
3. rank every agent's action candidates against that frozen state;
4. reserve capacity in stable simulation-ordinal order, with rejected agents
   falling through to their next ranked candidate;
5. apply selected actions in the declared action-phase order;
6. project observable actions and evidence into institutional state;
7. resolve any deadline scheduled for that cycle;
8. publish delayed public observations due on that cycle, after every other phase.

The `-1` compatibility default still publishes same-cycle reliance rows during
the action phase, preserving v0.3 behavior. Only an explicitly later observation
cycle enters the assessor-owned queue and final public-projection phase.

A deadline therefore cannot influence decisions already captured in its cycle.

## Ownership

### Scenario definition

May declare:

- initial society and incident data;
- semantic participant queries;
- schedule windows;
- evidence, opportunity, case, remedy and resource templates;
- stable issue, proposition, policy and presentation identifiers;
- generic effect requests and their numeric parameters.

May not:

- score evidence or issue a ruling;
- mutate official status;
- write reliance or appeal ledgers;
- establish or apply holdings;
- transfer an entitlement or resource;
- write directly to the public report.

### Generic agent layer

Owns detached perception/input/regime/opportunity snapshots, utility scoring,
ranked candidate plans, stable capacity reservation, selected actions and
assessor traces.

### Institutional case pipeline

Owns evidence provenance and as-of envelopes, score bounds, findings, rulings,
status mutations, appeals, holdings, case-fact scope matching and precedent
application.

### Consequence phase

Owns reliance, explicitly keyed abandoned alternatives, material deltas,
exclusive resource displacement, future opportunities and descendant cases.

### Public projection

Owns the non-authoritative report, causal timeline and graph validation. It must
never expose lived-event truth merely because the assessor runner can inspect it.
An authoritative lived-incident seed may change authority state and agent needs,
but it creates no public timeline row. A public incident requires a separately
observed report, action or evidence projection with its own provenance.

### Scenario assembly enforcement

Concrete scenario definitions live only below:

`Assets/_Project/Scripts/Institutional/Authority/Scenarios/<ScenarioName>/`

The parent `Desk42.Institutional.Scenarios` assembly references Domain only. It
cannot reference Authority or invoke the engine, is not auto-referenced, has no
Unity engine reference and receives no `InternalsVisibleTo` access from either
engine assembly. Its root marker and
assembly definition are engine-boundary metadata; named child directories are
the only unhashed scenario content.

Concrete scenario source may construct and return Domain definitions and policy
objects. The host or test assembly invokes Authority. Scenario content may not
invoke the engine, accept or mutate a consequence report, use reflection
to reach Authority internals, or implement an institutional transition. CI runs
`tools/ci/Assert-InstitutionalScenarioAuthoringBoundary.ps1` as a conservative
source/assembly check before Unity tests.

Concrete scenario source and test directories contain only C# source, matching
Unity metadata and exact folder metadata. Assembly references, assembly
definitions, precompiled DLLs, response files and other binary escape hatches are
forbidden below those directories. Scenario tests may invoke the engine,
validators and read-only report surface, but may not call transition services,
construct projected outcomes or mutate report collections. Optional presentation
fixtures use a closed, non-executable media/data/UI extension allowlist; scenes,
prefabs, ScriptableObjects, shaders, C# and compiled assemblies are forbidden.

The v0.1 public report remains a mutable compatibility DTO. External presentation
code must treat the report returned by `RunScenario` as read-only. This is a
known residual API risk rather than permission for scenario code to post-process
results; the scenario assembly rule and CI token gate prohibit that path until a
future immutable report projection replaces the compatibility surface.

## Cross-scenario seams forced by comparison

| Seam | Workplace Identity | Glass Canal | Required generic form |
|---|---|---|---|
| Issue | identity continuity | licensed discharge control | opaque issue ID |
| Evidence | testimony, clinical and roster records | sample, telemetry, permit and resonance records | opaque evidence-class ID plus provenance |
| Scope | employer and identity condition | watershed, permit class and undissipated output | all-of case facts |
| Reliance | treatment purchase and household alternative | condenser surrender and decommission | keyed alternative plus declarative deltas |
| Resource | paid shift | municipal filter cartridge CF-9 | exclusive conserved entitlement |
| Descendant | later continuity allocation | later purification-priority claim | action-caused case definition |
| Connection | wage winner and loser | filter holder winner and loser | paired transfer observation |
| Citation phase | successor initial ruling | later appellate ruling | exact case/ruling/holding citation declaration |
| Primary opening | unconditionally active | autonomous sealed sample | causal activation available to primary and descendant cases |
| Reliance visibility | action and public rows on one cycle | irreversible choice at cycle 7, observable effects at cycle 8 | separate action and bounded public-observation cycles |

`WorkAllocationState`, employer IDs and identity-condition fields remain legacy
compatibility surfaces for the preserved proof. New generic operations must not
depend on them.

The engine manifest excludes only the preserved authored harness and its
population fixture:

- `Domain/PrototypePopulationFactory.cs`;
- `Authority/InstitutionalConsequenceLoop.cs`;
- `Authority/InstitutionalConsequenceValidator.cs`.

Every file type below the protected Domain, Authority and Runtime roots is part
of the engine-candidate hash. UTF-8 text is line-ending normalised before hashing;
binary and non-UTF-8 content is hashed byte-for-byte. The scenario-root asmdef,
marker, metadata and any unexpected root asset are included; only exact legacy
proof files and files below named concrete scenario directories are excluded.
The same manifest also protects generic institutional EditMode tests, this
boundary document, the institutional workflow and every PowerShell CI script.
This prevents an `.asmref`, DLL or other Unity-imported asset from bypassing the
freeze merely because it is not C# source.

Manifest verification is read-before-write. `-OutputPath` and `-VerifyAgainst`
must name different files, so a verification command cannot overwrite its own
baseline before comparison. Protected text is hashed as UTF-8 after normalising
line endings to LF, so a Windows-authored baseline remains reproducible on the
Linux CI runner.

## Engine freeze rule

The immutable tag `institutional-engine-candidate-v0.4.1` names the candidate
commit. That commit tracks
`evidence/InstitutionalEngine/v0.4.1/engine-manifest.sha256`. The baseline is
mandatory, and its Git blob at every later scenario commit and its checked-out
file must remain clean relative to the byte-identical blob at the tag. The
candidate commit's own CI verifies the baseline without demanding a second
scenario.

The v0.1, v0.2, v0.3, v0.3.1 and v0.4 tags and evidence directories remain
historical evidence and are never used as aliases for v0.4.1. Candidate v0.4.1
changes only reproducibility tooling: it replaces a checkout-byte SHA-256 that
varied with Git line-ending conversion with the immutable Git blob identity of
`Packages/packages-lock.json`. The v0.4.1 manifest must be generated from the
fixed candidate itself; no earlier manifest hash may be copied forward as
v0.4.1 evidence.

After the freeze, Glass Canal may add only its scenario definition, its tests and
optional non-executable presentation fixtures. The gate requires a clean worktree
and a checked-out `HEAD` equal to the requested scenario commit. It rejects merge
commits, then inspects every commit after the candidate separately, so an engine
edit followed by a revert is still a freeze violation. Passing requires:

- no changed engine hash;
- no engine reference to a concrete scenario type, scenario ID, participant
  name, proposition ID or resource ID;
- one configuration ending in final denial without reliance;
- one configuration producing reliance without a precedent transfer;
- one configuration producing appeal, scoped holding, later precedent
  application and a conserved named winner/loser transfer;
- causal ablations removing disclosure, reliance action, appeal or descendant
  action also remove their downstream phases.

The institutional workflow fetches full history and tags, runs the static policy
self-tests and authoring boundary, then always invokes the gate. A missing
candidate tag or baseline fails closed. Presentation paths are workflow triggers,
and final repository hygiene includes untracked files so Unity-generated metadata
cannot hide an otherwise unmanifested asset.

The engine hash is necessary but not sufficient: the scenario-authoring boundary
script must also pass, and concrete scenario source must remain confined to its
named child directory.

Until active consequence state is serialised, the evidence wording remains:

> Agent society persistence is implemented. Active institutional
> consequence-loop persistence is not.

The candidate is consequently a fresh-run/replay engine, not a restartable
mid-case engine. Exact-once execution across process reconstruction is deferred
with consequence-loop persistence; exact-once behaviour within a live run is
still required and tested.
