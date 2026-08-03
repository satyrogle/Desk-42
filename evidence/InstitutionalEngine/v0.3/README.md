# Desk 42 Institutional Engine Candidate v0.3

## Evidence identity

- Candidate tag: `institutional-engine-candidate-v0.3`
- Clean evidence branch: `codex/institutional-evidence-v0.1`
- Preserved original proof: `institutional-proof-v0.1` at
  `455cecc4a5236e5455c56eb710fb03e243975dc9`
- Preserved falsified candidate v0.1:
  `institutional-engine-candidate-v0.1` at
  `451982aeb06af0c1e36e944ab2b1e87897221194`
- Preserved falsified candidate v0.2:
  `institutional-engine-candidate-v0.2` at
  `55e507b5a623573dacb2df38b0bf790411c3b325`
- Unity Editor: `2022.3.62f3 (96770f904ca7)`
- `Packages/packages-lock.json` SHA-256:
  `33A5B75D8EB048F51A44470724D8DD6F50E0DCB11FB4E73005B488EE7258757D`

After this candidate commit is tagged, its exact commit is resolved with:

```text
git rev-parse institutional-engine-candidate-v0.3^{commit}
```

## Accurate claim at this checkpoint

> Desk 42 contains a deterministic institutional engine candidate and one
> declarative Workplace Identity control fixture. Shared systems own agent
> decisions, evidence, policy-specific evidence-class weight and reliability,
> conditional case activation, findings, exact-ruling citations, status
> effects, reliance, appeals, scoped precedent, descendant cases, conserved
> entitlement transfers, public projection and causal validation.

This checkpoint is not yet claimed as a proven reusable multi-incident
architecture. That claim requires the structurally different Glass Canal
scenario to pass after this commit without changing any protected candidate
input.

Agent society persistence is implemented. Active institutional
consequence-loop persistence is not. Runs are reproducible from initial state;
they are not restartable mid-case.

## Why v0.3 exists

The immutable v0.2 candidate remains preserved with its original evidence.
Before any Glass Canal source was authored, a literal audit of the frozen
Scenario 02 specification found one additional generic contract failure:

- opaque evidence rules could vary class weight, but policy configurations
  could not vary their reliability treatment of that class.

Source-event reliability already existed and remains unchanged on each
evidence artifact. Candidate v0.3 adds a separate bounded
`PolicyReliabilityPercent` to the same opaque evidence-class rule. Adjudication
applies it after the existing class-weight and source-reliability stages, with
checked arithmetic and the original sequential integer truncation. The default
of 100 preserves every v0.2 score and score bound exactly.

The test contract covers live disposition changes, support/opposition bounds,
rounding-order sentinels, clone detachment, neutral legacy defaults, zero,
invalid rules, checked overflow and propagation into ruling score bounds.

The Glass specification's `material/need effects` wording is interpreted as a
generic material-or-need effect category: the frozen entitlement executor
already produces an atomic, conserved, holder-specific material pair. The
cycle-8 observation row is an inspection window; a consequence timestamped at
its cycle-7 cause is observable by that window and is not misrepresented as a
delayed projection.

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

- total: 245
- passed: 245
- failed: 0
- inconclusive: 0
- skipped: 0
- SHA-256:
  `DE93D2C494565F6D2FDBA62A15E3D81D04F8FE16FFADAD0F0852345E637FC589`

`candidate-full-editmode.xml` records the complete EditMode suite:

- total: 366
- passed: 365
- failed: 0
- inconclusive: 0
- skipped: 1
- SHA-256:
  `1D4F9E249667EB3A2C41F8C39CA45546F6A88513DA67D7E63254D47F44511489`

The sole skip is the pre-existing non-institutional test:

```text
Desk42.Tests.EditMode.SynergyResolverTests.WIRING_GAP_DurationAndCostChains_HaveNoStructuredPerStepTrace
```

These are curated local Unity artifacts, not independent CI results.

## Frozen manifest and post-candidate gate

`engine-manifest.sha256` contains 149 protected inputs and has SHA-256:

```text
8626E9EB33192E1BEC76807CCFCE7F521665EFDF1298B88A0F262300B0567542
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
  boundary rejects the known direct and alias mutation routes, but a future
  immutable consumer projection is still preferable.
- The authoring guard is conservative lexical enforcement, not a C# semantic
  analyser.
- Procedural incident composition remains future work; this checkpoint proves
  an engine candidate and one declarative control fixture only.
