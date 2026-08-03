# Causal Legibility Slice v0.1 - Build Evidence

Generated: 2026-08-03

Branch: `codex/causal-legibility-slice-v0.1`

Unity: `2022.3.62f3 (96770f904ca7)`

## Curated artifact

`docket-build.png` is a 1440 x 900 screenshot captured by the Windows x64
player itself through the `--desk42-capture` validation path. It is not an
Editor screenshot or a design mockup.

SHA-256:

```text
772905299D0AD5F4C032E5535023CB45FFFE65BF46305CC6A6C1E92CD650C8CD
```

The image shows the initial public docket: allegations, the contested
proposition, docket basis, missing evidence and an evidence support range that
is explicitly not presented as truth probability. It also shows the five
player-facing surfaces and the hidden-state exclusions.

## Validation record

```text
Full EditMode
  394 total / 394 passed / 0 failed / 0 skipped

Focused PlayMode
  2 total / 2 passed / 0 failed / 0 skipped

Windows x64 build
  Build Finished, Result: Success

Built-player launch smoke
  exit code 0
  DESK42_SMOKE_OK causal-legibility save-load descendant-case

Built-player screenshot path
  exit code 0
  DESK42_CAPTURE_OK .../evidence/CausalLegibility/v0.1/docket-build.png
```

The standalone build directory, player executable, raw logs and NUnit XML stay
in ignored `tmp/causal-legibility/`. Only the one inspected screenshot and this
human-readable validation record are retained as source evidence.

## Honest interpretation

This evidence proves buildability and deterministic interaction coverage. It
does not prove that a new player understands the causal system. That claim is
reserved for the six-player comprehension gate documented in
`Docs/Institutional/CAUSAL_LEGIBILITY_SLICE_V0.1.md`.
