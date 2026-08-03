# Desk 42 Institutional Engine Candidate v0.4

## Evidence identity

- Candidate tag: `institutional-engine-candidate-v0.4`
- Clean evidence branch: `codex/institutional-evidence-v0.1`
- Exact generic source checkpoint tested in a detached worktree:
  `1ad31fb0b0d8397eb2d9b70582a5b43243b3f0d7`
- Preserved original proof: `institutional-proof-v0.1` at
  `455cecc4a5236e5455c56eb710fb03e243975dc9`
- Preserved falsified candidate v0.1:
  `institutional-engine-candidate-v0.1` at
  `451982aeb06af0c1e36e944ab2b1e87897221194`
- Preserved falsified candidate v0.2:
  `institutional-engine-candidate-v0.2` at
  `55e507b5a623573dacb2df38b0bf790411c3b325`
- Preserved v0.3 reliability checkpoint:
  `institutional-engine-candidate-v0.3` at
  `a093df8f4280c9cbff4dbf9e139d99aaf0e714d0`
- Preserved v0.3.1 corrected-claim checkpoint:
  `institutional-engine-candidate-v0.3.1` at
  `956139fae55d65d3d3d38b8c969214df635e83f6`
- Unity Editor: `2022.3.62f3 (96770f904ca7)`
- `Packages/packages-lock.json` SHA-256:
  `33A5B75D8EB048F51A44470724D8DD6F50E0DCB11FB4E73005B488EE7258757D`

After the evidence commit is tagged, its exact identity is resolved with:

```text
git rev-parse institutional-engine-candidate-v0.4^{commit}
```

## Accurate claim at this checkpoint

> Desk 42 contains a deterministic institutional engine candidate and one
> declarative Workplace Identity control fixture. Shared systems own agent
> decisions, evidence, policy-specific evidence treatment, conditional case
> activation, findings, rulings, status effects, irreversible reliance, delayed
> public observation, appeals, scoped precedent, recovery cases, descendant
> cases, conserved entitlement transfers, material consequences and causal
> validation.

This checkpoint does **not** yet claim a proven reusable multi-incident or
procedural society engine. That claim requires the structurally different Glass
Canal definition to pass after this tag without changing any protected engine
input.

Agent society persistence is implemented. Active institutional
consequence-loop persistence is not. Runs are reproducible from initial state;
they are not restartable mid-case.

## Why v0.4 exists

The frozen Glass Canal specification falsified v0.3.1 before Glass scenario
source was admitted to the candidate. It required an autonomous action on cycle
7 whose institutional observation became public on cycle 8. Candidate v0.3.1
coupled irreversible action, material application and public observation to one
pulse, so it could not represent that distinction without changing the shared
engine.

Candidate v0.4 separates those clocks:

- the autonomous action and material authority state apply on the action cycle;
- an assessor-owned pending queue retains a detached public snapshot;
- a final public-observation phase publishes the observation, exact material set
  and one semantic timeline row only on the declared public cycle;
- decisions and institutional transitions earlier in that cycle cannot read the
  not-yet-public row.

The candidate also closes defects found during adversarial review:

- society events bind exactly to their decision trace, action kind, actor, tick
  and opportunity;
- recovery requires the unique appeal reversal of the exact ruling relied on;
- every public reliance-recovery case maps back to exactly one authority ledger
  event, in both directions;
- declared recovery sets receive full-envelope and exact-set validation;
- recovery case identities are injective length-prefixed derivations rather than
  delimiter-ambiguous concatenations;
- delayed observation, material and timeline identities are reserved before
  execution and in the run-owned pending queue;
- authored public identities, including nested holding scope IDs, are checked
  against delayed and recovery identities before a declarative scenario starts.

No Glass Canal source or test is part of this candidate checkpoint or its
protected manifest.

## Enforced ownership boundary

Concrete scenario code compiles in `Desk42.Institutional.Scenarios`, a
Domain-only assembly. It can construct definitions and policies, but it cannot
reference Authority, execute transition services, or construct and mutate
institutional outcomes. Scenario tests may execute the engine and inspect its
reports. CI rejects direct and aliased outcome construction, transition calls
and report mutation from scenario code.

The preserved 15-cycle proof harness remains compatibility evidence and is
accurately described as an authored vertical proof. It is excluded from the
reusable engine manifest.

## Clean local Unity validation

Both XML files were produced from a detached worktree at exact source commit
`1ad31fb0b0d8397eb2d9b70582a5b43243b3f0d7`, without the uncommitted Glass Canal
scenario present.

`candidate-targeted-editmode.xml` records the institutional target:

- total: 312
- passed: 312
- failed: 0
- inconclusive: 0
- skipped: 0
- SHA-256:
  `30A66260A4010312F7DDF985F0510018EEA795CA75D3F2CBCB947E7D541E1928`

`candidate-full-editmode.xml` records the complete candidate EditMode suite:

- total: 441
- passed: 440
- failed: 0
- inconclusive: 0
- skipped: 1
- SHA-256:
  `223E7018E051599B537E65661734D6645982E75D9796A7B933A63F1AB331850E`

The sole skip is the pre-existing non-institutional test:

```text
Desk42.Tests.EditMode.SynergyResolverTests.WIRING_GAP_DurationAndCostChains_HaveNoStructuredPerStepTrace
```

The scenario-authoring boundary and gate-policy self-tests also passed in the
same detached checkout. These are curated local Unity artifacts, not
independently reproduced CI results.

## Frozen manifest and post-candidate gate

`engine-manifest.sha256` contains 151 protected inputs and has SHA-256:

```text
CB52CBC987B702C46AB0093638224C49CF8CFD478A8F1D14610937D08B53BE9E
```

It covers generic institutional source, assembly metadata, generic
institutional tests, the workflow, the candidate-boundary document and every CI
enforcement script. It excludes the three exact legacy-proof files and all
concrete named scenario directories.

After this evidence commit is tagged, the generalisation gate requires every
non-merge commit to change only the named Glass Canal source/test folders and
optional non-executable presentation fixtures. It rejects deletions, protected
engine edits, binary or assembly escapes, dirty worktrees, a mismatched checked
out commit, scenario-test outcome mutation and a candidate manifest not
identical to the manifest committed here.

CI is configured to reproduce the institutional target and full EditMode suite
using the pinned Unity and package inputs. Until GitHub completes that workflow
for the exact pushed commit, reproducibility is configured but not independently
demonstrated.

## Disclosed residual risks

- Public report DTOs remain mutable legacy compatibility surfaces. The scenario
  boundary rejects the known direct and alias mutation routes, but an immutable
  consumer projection remains preferable.
- The scenario authoring guard is conservative lexical enforcement, not a C#
  semantic analyser.
- Pre-execution identity planning closes the declarative scenario-engine path.
  Arbitrary out-of-band mutation of report DTOs is unsupported; final causal
  validation remains the fail-closed boundary for such tampering.
- This checkpoint contains one declarative control fixture. Cross-incident
  reusability remains a falsifiable post-tag claim, not a candidate assumption.
- Procedural incident generation remains future work.
