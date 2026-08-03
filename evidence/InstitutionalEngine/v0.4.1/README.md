# Desk 42 Institutional Engine Candidate v0.4.1

## Evidence identity

- Candidate tag: `institutional-engine-candidate-v0.4.1`
- Superseding clean evidence branch: `codex/institutional-evidence-v0.2`
- Runtime engine parent: `institutional-engine-candidate-v0.4`
- Unity Editor: `2022.3.62f3 (96770f904ca7)`
- `Packages/packages-lock.json` Git blob:
  `4edd99bba4e05a24609938f2707bbed322ecb630`
- Canonical LF-normalised lockfile SHA-256:
  `038F08332AA71C9EF051662F7F6A1B3B716AA6EB105EE91F52EFC9E4794CE931`

Resolve the exact candidate commit with:

```text
git rev-parse institutional-engine-candidate-v0.4.1^{commit}
```

## Correction from v0.4

Candidate v0.4's runtime architecture and local Unity evidence remain valid, but
its first hosted workflow stopped before Unity because the workflow compared the
SHA-256 of checkout bytes. Git checked the JSON out with LF on Linux and CRLF on
the local Windows evidence machine, so equal repository content produced two raw
file hashes.

Candidate v0.4.1 corrects only reproducibility tooling. The workflow pins the
Git blob stored by the exact commit, which is invariant across checkout line
endings, and records both that blob identity and the runner checkout hash. The
generalisation gate, its policy self-test and this boundary document are advanced
together and frozen in a newly generated manifest. No institutional runtime
source, generic Unity test or Glass Canal scenario source is changed or present
in this candidate correction.

The failed v0.4 run remains published evidence of the detected infrastructure
defect. Its candidate tag is not moved or replaced.

## Accurate claim at this checkpoint

> Desk 42 contains a deterministic institutional engine candidate and one
> declarative Workplace Identity control fixture. Shared systems own agent
> decisions, evidence, policy-specific evidence treatment, conditional case
> activation, findings, rulings, status effects, irreversible reliance, delayed
> public observation, appeals, scoped precedent, recovery cases, descendant
> cases, conserved entitlement transfers, material consequences and causal
> validation.

This checkpoint does not claim a proven reusable multi-incident or procedural
society engine. The reusable multi-incident claim requires the structurally
different Glass Canal definition to pass after this tag without changing any
protected engine input.

Agent society persistence is implemented. Active institutional consequence-loop
persistence is not.

## Validation inheritance and next proof

The institutional runtime and generic Unity-test inputs are byte-identical to
candidate v0.4. Its curated local results remain in
`evidence/InstitutionalEngine/v0.4/`:

- institutional target: 312 passed, 0 failed, 0 skipped;
- full EditMode: 440 passed, 0 failed, 1 approved pre-existing skip.

Those XML files were produced locally from the v0.4 generic source checkpoint;
they are not relabelled as v0.4.1 results. The v0.4.1 candidate commit must first
pass the hosted workflow at its exact tag. Glass Canal is admitted only in a
later commit, after the tag, and must then pass the same workflow and the frozen
manifest gate.

The v0.4.1 manifest protects 151 engine, generic-test and enforcement inputs.
Its SHA-256 is:

```text
747254A1DC23D5EE877D44D91FA560A2047A1B7D38EA425378416A242741ED97
```

Before the candidate freeze, the policy self-tests, scenario-authoring boundary,
manifest verification and byte-identical runtime/test-tree comparison against
v0.4 passed locally. Exact hosted status is reported by the GitHub workflow and
is not inferred from these local checks.

## Claim ladder

After the post-tag Glass Canal gate passes, the defensible claim becomes:

> Desk 42 contains a reusable institutional simulation architecture supporting
> structurally different incident definitions through shared evidence, ruling,
> reliance, appeal, precedent and consequence systems.

Procedural incident composition remains future work. Do not describe this as a
general generative society engine.
