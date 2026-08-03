# Desk 42 Institutional Engine Candidate v0.2

## Evidence identity

- Candidate tag: `institutional-engine-candidate-v0.2`
- Clean evidence branch: `codex/institutional-evidence-v0.1`
- Preserved original proof: `institutional-proof-v0.1` at
  `455cecc4a5236e5455c56eb710fb03e243975dc9`
- Preserved falsified candidate: `institutional-engine-candidate-v0.1` at
  `451982aeb06af0c1e36e944ab2b1e87897221194`
- Unity Editor: `2022.3.62f3 (96770f904ca7)`
- `Packages/packages-lock.json` SHA-256:
  `33A5B75D8EB048F51A44470724D8DD6F50E0DCB11FB4E73005B488EE7258757D`

After this candidate commit is tagged, its exact commit is resolved with:

```text
git rev-parse institutional-engine-candidate-v0.2^{commit}
```

## Accurate claim at this checkpoint

> Desk 42 contains a deterministic institutional engine candidate and one
> declarative Workplace Identity control fixture. Shared systems own agent
> decisions, evidence, conditional case activation, findings, exact-ruling
> citations, status effects, reliance, appeals, scoped precedent, descendant
> cases, conserved entitlement transfers, public projection and causal
> validation.

This checkpoint is not yet claimed as a proven reusable multi-incident
architecture. That claim requires the structurally different Glass Canal
scenario to pass after this commit without changing any protected candidate
input.

Agent society persistence is implemented. Active institutional
consequence-loop persistence is not. Runs are reproducible from initial state;
they are not restartable mid-case.

## Why v0.2 exists

The immutable v0.1 candidate remains preserved with its original evidence.
Auditing the frozen Glass Canal specification against it falsified two claimed
generic seams before Scenario 02 was authored:

1. holding citations were case-wide rather than bound to one exact ruling; and
2. primary cases were unconditionally active, so removing the causal sampling
   action could not remove the primary case path.

Candidate v0.2 corrects those contracts generically. It also fails closed on
transfer disposition, phase, cycle and projection provenance, and hardens the
data-only scenario-test boundary against direct, multiline and aliased report
mutation. No Glass-specific identifier or branch appears in executable or
generic-test changes.

## Enforced ownership boundary

Concrete scenario code compiles in `Desk42.Institutional.Scenarios`, a
Domain-only assembly. It can create definitions and policies, but it cannot
reference Authority, execute transition services, or construct and mutate
institutional outcomes. Scenario tests may execute the engine and read reports;
CI rejects outcome construction, transition calls and report mutation through
direct access, typed or inferred aliases, loops, lambdas and collection aliases.

The preserved 15-cycle proof harness remains compatibility evidence and is
described as authored. It is not included in the reusable engine manifest.

## Local Unity validation

`candidate-targeted-editmode.xml` records the institutional target:

- total: 233
- passed: 233
- failed: 0
- inconclusive: 0
- skipped: 0
- SHA-256:
  `1DE2843735793909A7B52DB22AE7769E80A2A3693217F6448ECDEF7BA7C54C17`

`candidate-full-editmode.xml` records the complete EditMode suite:

- total: 354
- passed: 353
- failed: 0
- inconclusive: 0
- skipped: 1
- SHA-256:
  `4AB04D0F19E214851CE6D0F95C6527B78547786C00D8D00794B8A83CB9B2ED3C`

The sole skip is the pre-existing non-institutional test:

```text
Desk42.Tests.EditMode.SynergyResolverTests.WIRING_GAP_DurationAndCostChains_HaveNoStructuredPerStepTrace
```

These are curated local Unity artifacts, not independent CI results.

## Frozen manifest and post-candidate gate

`engine-manifest.sha256` contains 149 protected inputs and has SHA-256:

```text
E0D7E0F4BB5327624B6CBF3E29A0009ABD0F8B646604FFA6F9A0854BB85AAA3D
```

It covers generic engine source, assembly metadata, generic institutional
tests, the workflow, boundary documentation and every CI enforcement script.
It excludes only the three exact legacy-proof files and concrete named scenario
directories.

After this commit, the generalisation gate requires every non-merge commit to
change only the named Glass Canal source/test folders and optional
non-executable presentation fixtures. It rejects deletions, engine edits,
binary or assembly escapes, dirty worktrees, a mismatched checked-out commit,
scenario-test outcome mutation, and a candidate manifest not identical to the
manifest blob committed here.

CI is configured to reproduce the institutional and full EditMode suites using
the pinned Unity and package inputs. Until GitHub completes that workflow for
the exact candidate commit, reproducibility is configured but not independently
demonstrated.

## Disclosed residual risks

- Public report DTOs remain mutable legacy compatibility surfaces. The scenario
  boundary now rejects the known direct and alias mutation routes, but a future
  immutable consumer projection is still preferable.
- The authoring guard is conservative lexical enforcement, not a C# semantic
  analyser.
- Procedural incident composition remains future work; this checkpoint proves
  an engine candidate and one declarative control fixture only.
