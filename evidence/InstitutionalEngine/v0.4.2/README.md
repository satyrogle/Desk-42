# Desk 42 Institutional Engine Candidate v0.4.2

## Evidence identity

- Candidate tag: `institutional-engine-candidate-v0.4.2`
- Clean evidence branch: `codex/institutional-evidence-v0.2`
- Runtime engine parent: `institutional-engine-candidate-v0.4`
- Reproducibility-tooling parent: `institutional-engine-candidate-v0.4.1`
- Unity Editor: `2022.3.62f3 (96770f904ca7)`
- `Packages/packages-lock.json` Git blob:
  `4edd99bba4e05a24609938f2707bbed322ecb630`

Resolve the exact candidate commit with:

```text
git rev-parse institutional-engine-candidate-v0.4.2^{commit}
```

## Correction from v0.4.1

The exact v0.4.1 hosted run passed the generalisation gate, package Git-blob pin
and Unity tests. Its full suite recorded 441 total, 440 passed, zero failed and
one approved pre-existing skip. It then failed in post-test evidence handling:

- the container left `TestResults` owned by root, so the runner could not add the
  engine manifest and reproduction metadata;
- Unity reported the package JSON files as modified, but the workflow printed
  only their names and could not distinguish line-ending conversion from a real
  dependency change.

Candidate v0.4.2 changes only protected reproducibility tooling. Unity runs as
the host user so evidence remains writable. The hygiene contract compares both
package files with `--ignore-cr-at-eol`; a semantic difference fails the job,
prints the exact diff and stores `package-input-diff.patch` in the uploaded
artifact. Only the same two package paths are excluded from the subsequent raw
status check. Every other tracked or untracked repository change still fails.

The v0.4.1 tag and failed workflow remain immutable evidence. No institutional
runtime source, generic Unity test or Glass Canal scenario source is changed or
present in this candidate correction.

## Accurate claim at this checkpoint

> Desk 42 contains a deterministic institutional engine candidate and one
> declarative Workplace Identity control fixture. Shared systems own agent
> decisions, evidence, policy-specific evidence treatment, conditional case
> activation, findings, rulings, status effects, irreversible reliance, delayed
> public observation, appeals, scoped precedent, recovery cases, descendant
> cases, conserved entitlement transfers, material consequences and causal
> validation.

This checkpoint does not claim a proven reusable multi-incident or procedural
society engine. The reusable multi-incident claim requires Glass Canal to pass
after this tag without changing any protected engine input.

Agent society persistence is implemented. Active institutional consequence-loop
persistence is not.

## Validation contract

The institutional runtime and generic Unity-test inputs are byte-identical to
candidate v0.4. The v0.4 local candidate results and the v0.4.1 hosted Unity XML
remain attributable to their original commits; they are not relabelled as
v0.4.2 evidence.

The v0.4.2 candidate commit must first pass the hosted workflow at its exact tag.
Glass Canal is admitted only in a later commit and must then pass the same
workflow and frozen manifest gate.

The manifest protects 151 engine, generic-test and enforcement inputs. Its
SHA-256 is:

```text
17079ACE7960CD5AE00CED3C28504698960043C081167B7719B15D896CDC66C7
```

After that post-tag gate, the defensible claim becomes:

> Desk 42 contains a reusable institutional simulation architecture supporting
> structurally different incident definitions through shared evidence, ruling,
> reliance, appeal, precedent and consequence systems.

Procedural incident composition remains future work. Do not describe this as a
general generative society engine.
