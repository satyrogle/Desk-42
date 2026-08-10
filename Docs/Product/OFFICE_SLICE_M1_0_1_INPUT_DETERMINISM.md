# Desk42 Office Slice M1.0.1 — Input Determinism Closeout

- Branch: `codex/office-slice-v0.7-m1-greybox`
- Baseline commit: `da1883268a58d3d812a76346a07919dc1d4ec572`
- Scope: narrow M1 input hardening only; M2 was not started.

## Change

`OfficeTickDriver` now samples keyboard and gamepad state into one canonical
`OfficeInputIntent`. `OfficeInputCommandGenerator` consumes that intent only
from the 30 Hz simulation callback, producing at most one Warden Move command
per simulation tick. Keyboard and gamepad directions use the same cardinal
canonicalizer with horizontal priority for exact diagonal ties.

Interaction presses are one-shot intents buffered for six numbered simulation
ticks. Repeated render frames cannot duplicate the press, and replay mode clears
live movement and interaction intent before advancing its recorded stream. The
clock comparison also tolerates sub-picosecond accumulator rounding so exact
30, 60, and 144 FPS simulations close on the same tick count.

## Exact validation evidence

Unity `2022.3.62f3` was run with the repository's existing CI-compatible MCP
connection overrides. No package or MCP configuration was changed.

| Validation | Result | Artifact |
| --- | ---: | --- |
| Office Slice EditMode fixture | 13/13 passed, 0 failed, 0 skipped | `TestResults/M1.0.1/office-input-editmode.xml` |
| Full EditMode | 431/431 passed, 0 failed, 0 skipped | `TestResults/M1.0.1/full-editmode.xml` |
| Office Slice PlayMode | 2/2 passed, 0 failed, 0 skipped | `TestResults/M1.0.1/office-slice-playmode.xml` |
| Institutional Automation PlayMode | 11/11 passed, 0 failed, 0 skipped | `TestResults/M1.0.1/institutional-automation-playmode.xml` |
| Windows x64 build | Success, Unity exit 0 | `TestResults/M1.0.1/windows-x64-build.log` |
| Built-player OfficeSlice smoke | Exit 0; `OFFICE_SLICE_CAPTURE_OK` | `TestResults/M1.0.1/office-slice-built-smoke.png` |

The targeted fixture includes the existing 10,000-tick replay test and these
new regressions:

- `HeldInputAtThirtySixtyAndOneFortyFourFpsIsIdentical`
- `InputGeneratorQueuesAtMostOneWardenMovePerTick`
- `KeyboardAndGamepadDirectionsGenerateEquivalentCommands`
- `BufferedInteractionFiresOnceAndReplayLocksOutLiveIntent`

The render-rate regression simulated four seconds of held input at 30, 60, and
144 rendered frames per second. All three runs ended at tick 120 with identical
command-log JSON, Warden logical position, ordered state snapshot, and checksum.
The 144 FPS log contained 120 Warden Move commands on 120 unique ticks.

The fresh player artifact is `Builds/M1.0.1/Desk42.exe` (Windows x64). Build
Settings remain unchanged: Institutional Automation is scene 0 and OfficeSlice
is scene 1.

## Boundary audit

- Frozen Institutional path changes: 0.
- Package manifest/lock changes: 0.
- Render-pipeline changes: 0.
- Save-schema changes: 0.
- ComfyUI, Blender, FMOD, final art, audio, and M2 systems: not used or started.
