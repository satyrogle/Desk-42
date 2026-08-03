# Desk 42 Institutional Engine Candidate v0.4.3

## Evidence identity

- Candidate tag: `institutional-engine-candidate-v0.4.3`
- Clean evidence branch: `codex/institutional-evidence-v0.2`
- Runtime engine parent: `institutional-engine-candidate-v0.4`
- Diagnostic predecessor: `institutional-engine-candidate-v0.4.2`
- Unity Editor: `2022.3.62f3 (96770f904ca7)`
- `Packages/manifest.json` Git blob:
  `c694d27da60ea06c95184a6bffa3cc39cddc4552`
- `Packages/packages-lock.json` Git blob:
  `9ea30f5d53017a4af882ca53b77754405e1c2101`
- Lockfile LF-normalised SHA-256:
  `0EB6E1B54DB5DF287B880B836808CCACF104C39B43874C0953899AC24B832997`

Resolve the exact candidate commit with:

```text
git rev-parse institutional-engine-candidate-v0.4.3^{commit}
```

## Correction from v0.4.2

The exact v0.4.2 hosted run passed its candidate gate, pinned-input check and
both Unity result contracts:

- institutional target: 320 passed, 0 failed, 0 skipped;
- full EditMode: 440 passed, 0 failed, 1 approved pre-existing skip.

Its newly hardened post-test guard then rejected and preserved an exact package
patch. Unity's Linux editor had added the explicitly versioned Linux toolchain
`2.0.11`, its sysroot dependencies `2.0.10` and `2.0.9`, and canonicalised JSON
dependency order. This was a real input-resolution difference, not a line-ending
rewrite, so the failed checkpoint behaved correctly.

Candidate v0.4.3 commits that exact captured manifest and lock state. It pins the
Git blobs of both package inputs before testing, continues to execute Unity as
the host user, and retains the semantic post-test comparison and diagnostic
artifact. No institutional runtime source, generic Unity test or Glass Canal
scenario source changes or exists in this candidate correction.

The v0.4.1 and v0.4.2 tags and their failed workflow runs remain immutable
diagnostic evidence; neither tag is moved or relabelled.

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

## Freeze and next proof

The institutional runtime and generic Unity-test paths remain byte-identical to
candidate v0.4. Candidate v0.4.3 must first pass the hosted workflow at its exact
tag. Glass Canal is admitted only in a later commit and must then pass the same
workflow and frozen manifest gate.

The manifest protects 151 engine, generic-test and enforcement inputs. Its
SHA-256 is:

```text
0173B87AF0FDA2A0B8F8205C25482F7F59CB14FF8ED375F39E74821DF738A1AA
```

After that post-tag gate, the defensible claim becomes:

> Desk 42 contains a reusable institutional simulation architecture supporting
> structurally different incident definitions through shared evidence, ruling,
> reliance, appeal, precedent and consequence systems.

Procedural incident composition remains future work. Do not describe this as a
general generative society engine.
