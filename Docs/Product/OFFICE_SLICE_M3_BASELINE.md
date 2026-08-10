# Desk42 Office Slice v0.7 - M3 Baseline

## Branch

- M3 branch: `codex/office-slice-v0.7-m3-content`
- Branch-start commit: `257b8c84cb17db873ff3f99534caad11e17d89fb`
- Source branch: `codex/office-slice-v0.7-m2-core-loop`
- Validated M2 implementation: `535db6593927a39b1936f1b833474aeff87b0750`
- M2 closeout-only tip inherited by M3: `257b8c84cb17db873ff3f99534caad11e17d89fb`
- M3 baseline tag: `office-slice-m3-baseline-m2-technical`
- Existing M2 technical-candidate tag: `office-slice-m2-technical-candidate`

Both baseline tags resolve to the validated M2 implementation commit. The M3
branch intentionally begins at the later documentation-only M2 branch tip.

## Environment

- Unity: `2022.3.62f3` (`96770f904ca7`)
- Target: Windows x64
- Render pipeline: existing Built-in Render Pipeline
- Input: existing canonical keyboard/controller Office input intent
- Batch overrides: `UNITY_MCP_KEEP_CONNECTED=false` and
  `UNITY_MCP_START_SERVER=false`

## Inherited technical evidence

- Full EditMode: 452/452 passed.
- Office Slice PlayMode: 11/11 passed.
- Institutional Automation PlayMode: 11/11 passed.
- M2 full-shift replay checksum: `6D95C9ACE2B200FC`.
- Windows x64 build and visible 1600x900/1280x720 captures passed.
- M2 executable SHA-256:
  `F5F73D8616A2500E0FB0223D83774E2D7F6A74C1BBDAD772F4FEFC9BF5812036`.

These are inherited M2 results, not M3 validation. M3 must rerun every required
suite, build, smoke, replay, capture, performance, and protected-path check.

## Boundary lock

M3 work is restricted to the Office Slice product layer, Office Slice tests,
procedural greybox presentation, evidence, and product documentation. Protected
Institutional paths, `InstitutionalAutomation.unity`, package inputs, Graphics
settings, and Quality settings remain frozen.

M3 makes no fun, retention, human-experience, visual-target, audio-target, or
commercial-validation claim.
