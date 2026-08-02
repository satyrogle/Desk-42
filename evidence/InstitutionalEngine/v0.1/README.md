# Desk 42 Institutional Engine Candidate v0.1

## Evidence identity

- Candidate tag: `institutional-engine-candidate-v0.1`
- Clean evidence branch: `codex/institutional-evidence-v0.1`
- Preserved original proof: `institutional-proof-v0.1` at
  `455cecc4a5236e5455c56eb710fb03e243975dc9`
- Unity Editor: `2022.3.62f3 (96770f904ca7)`
- `Packages/packages-lock.json` SHA-256:
  `33A5B75D8EB048F51A44470724D8DD6F50E0DCB11FB4E73005B488EE7258757D`

After the candidate commit is tagged, its exact commit is resolved with:

```text
git rev-parse institutional-engine-candidate-v0.1^{commit}
```

## Accurate claim at this checkpoint

> Desk 42 contains a deterministic institutional engine candidate and one
> declarative Workplace Identity control fixture. Shared systems own agent
> decisions, evidence, findings, rulings, status effects, reliance, appeals,
> scoped precedent, descendant cases, conserved entitlement transfers, public
> projection and causal validation.

The candidate is not yet claimed as a proven reusable multi-incident
architecture. That claim requires the structurally different Glass Canal
scenario to pass without changing any protected candidate input.

Agent society persistence is implemented. Active institutional
consequence-loop persistence is not. Runs are reproducible from their initial
state; they are not restartable mid-case.

## Enforced ownership boundary

Concrete scenario code compiles in `Desk42.Institutional.Scenarios`, a
Domain-only assembly. It can create definitions and policies, but it cannot
reference Authority, execute the engine, invoke transition services or mutate
the report. CI also scans concrete scenario sources and scenario tests for
boundary violations.

The preserved 15-cycle proof harness remains compatibility evidence and is
described as authored. It is not included in the reusable engine manifest.

## Local Unity validation

`candidate-targeted-editmode.xml` records the institutional target:

- total: 195
- passed: 195
- failed: 0
- inconclusive: 0
- skipped: 0
- SHA-256:
  `E83375EA5AB9EB5823839056F371B0BFE93FC45234569C7D89C031C5B0324E9F`

`candidate-full-editmode.xml` records the complete EditMode suite:

- total: 316
- passed: 315
- failed: 0
- inconclusive: 0
- skipped: 1
- SHA-256:
  `92CFE46A8A5DD2FB8830DB34E50CDA3ADBF21DE6D44C39E704AD9164BB02FADD`

The sole skip is the pre-existing non-institutional test:

```text
Desk42.Tests.EditMode.SynergyResolverTests.WIRING_GAP_DurationAndCostChains_HaveNoStructuredPerStepTrace
```

These are curated local Unity artifacts, not independent CI results.

## Frozen manifest and post-candidate gate

`engine-manifest.sha256` contains 147 protected inputs and has SHA-256:

```text
1880AA7AD70348C50BC31B84F1237815C4877EAECE8C155B1ED0559E821311BA
```

It covers generic engine source, assembly metadata, generic institutional
tests, the workflow, boundary documentation and every CI enforcement script.
It excludes only the three exact legacy proof files and concrete named scenario
directories.

After this commit, the generalisation gate requires every non-merge commit to
change only the named Glass Canal scenario source/test folders and optional
non-executable presentation fixtures. It rejects deletions, engine edits,
binary or assembly escapes, dirty worktrees, a mismatched checked-out commit,
and a candidate manifest not identical to the manifest blob committed here.

CI is configured to reproduce the institutional and full EditMode suites using
the pinned Unity and package inputs. Until GitHub completes that workflow for
the exact candidate commit, reproducibility is configured but not independently
demonstrated.

## Disclosed residual risks

- Public report DTOs remain mutable legacy compatibility surfaces. Scenario
  assemblies cannot access the engine or fabricate accepted runs, but a future
  immutable consumer projection is still preferable.
- The scenario authoring guard is a conservative lexical enforcement layer, not
  a semantic analyser.
- Procedural incident composition remains future work; this checkpoint proves
  an engine candidate and one declarative control fixture only.
