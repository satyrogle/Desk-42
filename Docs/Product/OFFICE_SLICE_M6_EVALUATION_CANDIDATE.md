# Desk42 Office Slice M6 Evaluation Candidate Closeout

## 1. Status

```text
M6 TECHNICAL EVALUATION CANDIDATE PASS
HUMAN EXPERIENCE RESULT PENDING
```

This closes the M6 technical candidate only. It does not claim that the core experience is understood, enjoyable, memorable, or desirable to continue.

## 2. Branch and exact commits

Branch: `codex/office-slice-v0.7-m6-evaluation-candidate`

Validated implementation commit: `476ee35faba8433d2081e387e572301bbdfa8783`

| Commit | Purpose |
|---|---|
| `4069fe5f9d69ace20165bde22ada402590b920a0` | Player-facing UI shell |
| `e22c5e0322244965ac3d3833885396d67e829948` | In-world first-shift onboarding |
| `deeded4829b30d5565c00d29356a9cae3c7c0164` | Player-language simplification |
| `49b8498417ea348c3b6c556eda0f50711cee7b2b` | State-readability hardening |
| `e7bd946ae1dedb949fcd451bcdb110857c8f68e2` | Settings and accessibility |
| `f1418431267a7b8c4acca562c6588abe6acb89ef` | Local evaluation telemetry |
| `bf5e36c8cc3c6c1ab5aea116583309b6d4525ab2` | Evaluation launch mode |
| `5eaf7fdf0a420faea12a8d01d2e51c4b679c113a` | Internal-review hardening |
| `476ee35faba8433d2081e387e572301bbdfa8783` | Complete M6 performance evidence configuration |

Any later closeout commit is documentation/evidence only and is not the build candidate.

## 3. M5 baseline

The branch starts from `d519723d900bdad232b8a01c982c8fbc2ac9ec87`, the validated M5 audio and feedback implementation. Annotated tag `office-slice-m6-baseline-m5-audio` resolves exactly to that commit.

Unity is `2022.3.62f3`; build target is `StandaloneWindows64`; the existing render pipeline and package lock remain unchanged.

## 4. Changed-file summary

The implementation diff from the M5 baseline contains 37 files, 3,606 insertions, and 21 deletions. Changes are confined to the Office Slice product presentation/evaluation layer, Office Slice EditMode tests, the capture driver, the product tick/input presentation bridge, and M6 documentation. No campaign content or Institutional implementation changed.

Primary additions are the M6 HUD presenter, onboarding observer, player-copy catalog, pause/settings controller, presentation settings, local telemetry recorder/observer, evaluation-mode configuration, and Gate A-H tests.

## 5. Player UI hierarchy

Normal play now presents:

- shift/time/waiting/danger at the top;
- one current action at lower left;
- current customer and mood;
- current file knowledge, missing check, and next useful action;
- active machine rules only;
- Break cause and recovery checklist only while a Break exists;
- result and tomorrow/next-shift information in result state.

Decision/manual controls use progressive disclosure. Technical HUD content remains behind the explicit F9 development toggle and is unavailable in evaluation mode.

## 6. Onboarding flow

The first shift observes successful commands and advances through Move, Take File, Send File, Check Papers, Trace Money, Decide, Calm, enable Auto Sorter, respond to Break, and recover. It emits one short sentence and one world focus at a time, generates no simulation command, and stores completion outside campaign state. Returning players can disable hints outside forced-fresh evaluation sessions.

## 7. Player-language audit

Normal-player copy is centralized in `OfficeM6PlayerCopyCatalog`. Action labels are literal and short; rules preserve their exact M2/M3 public condition text; `WHAT HAPPENED?` describes observable cause and effect. The static banned-term scan passes. Authored customer/case absurdity remains intact.

## 8. Accessibility/settings surface

The pause/settings surface includes Master, Music, SFX, Ambience, Rumble, Reduced Flash, Tutorial Hints, Text Scale, Fullscreen, and Resolution. Keyboard and controller navigation are supported. Settings persist through product-owned PlayerPrefs storage and are absent from campaign state/checksum. Maximum text scale is 1.3x and passes the 1280x720 critical-HUD fit test.

Original/copy, prompts, mood, danger, and recovery state all have non-colour text or shape cues. Pause halts the simulation before device sampling and resume preserves command order.

## 9. Evaluation telemetry schema

Evaluation mode writes one local JSONL file per run under `Application.persistentDataPath/Desk42Evaluation`. Schema version is 1. Each event has a stable event name, monotonic timestamp, anonymous session GUID, build identifier, tick, shift, and fixed value field.

The recorder covers session/shift lifecycle, first input/folder, Paper Check, Money Trace, decision, Calm, rule changes/matches, Break/recovery, `WHAT HAPPENED`, upgrades, restart, pause, campaign completion, inactivity, invalid/repeated invalid actions, and active tutorial prompt. It collects no name, email, IP address, microphone data, username, free-form input, or personal files. Writes are buffered and flushed at lifecycle boundaries.

Retained built-player sample: `evidence/M6/Telemetry/built-player-performance-session.jsonl`.

## 10. Evaluation launch mode

`Desk42.exe --desk42-evaluation` routes to Office Slice, starts Shift 1, forces a fresh cohort onboarding state, enables local telemetry, uses M4 visuals/M5 audio/M6 UI, supports all three shifts and a same-process new run, and exposes the build identifier only in Pause/About. F9 and development progression shortcuts are locked out.

## 11. Internal pre-human review

The complete campaign state flow was reviewed with developer HUD off, tutorial presentation active, audio/feedback runtime active, and keyboard prompts. A controller-equivalent full-shift path was also exercised in PlayMode.

Review found one load-bearing 1280x720 overlap: the sixth Promotion recovery item clipped. Gate H changed the checklist to two columns by three rows, expanded the two-rule card, and suppressed stale first-shift guidance in later-shift capture states. Final review of all 60 built-player captures found no remaining clipping, missing action label, settings dead-end, controller dead-end, debug leakage, missing M4 sprite, or missing M5 audio reference.

This was a technical/visual review, not a human fun or comprehension verdict.

## 12. Exact EditMode results

The final validation XML files listed below were produced from candidate commit `476ee35faba8433d2081e387e572301bbdfa8783`:

| Suite | Result | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Focused M6 | Passed | 47 | 0 | 0 |
| Focused M5 | Passed | 34 | 0 | 0 |
| Focused M4 | Passed | 47 | 0 | 0 |
| Focused M3 | Passed | 35 | 0 | 0 |
| Full standard EditMode | Passed | 615 | 0 | 0 |

Historical gate-level XML evidence is also retained for A-H. No final validation suite has a skip.

## 13. Exact PlayMode results

| Suite | Result | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Office Slice | Passed | 26 | 0 | 0 |
| Institutional Automation | Passed | 11 | 0 | 0 |

The M6 diff removes no Office Slice PlayMode test; the exact M5 baseline source exposes 26 tests in that fixture.

## 14. Determinism/replay parity

Three independent Unity processes each passed `EvaluationModeCompletesThreeShiftCampaign`, whose exact assertion is campaign checksum `B42CFA89D6277EA2`. Retained results are `replay-process-1.xml`, `replay-process-2.xml`, and `replay-process-3.xml`.

The full candidate suite also passes identical-command-stream parity for tutorial hints on/off, telemetry on/off, audio on/muted, and feedback on/off. Visual frame-rate and M4 visuals-enabled/disabled checksum tests remain green.

## 15. Windows build and SHA-256

- Output: `Builds/M6/Desk42.exe`
- Target: `StandaloneWindows64`
- Build result: Success
- Candidate commit: `476ee35faba8433d2081e387e572301bbdfa8783`
- Executable SHA-256: `F5F73D8616A2500E0FB0223D83774E2D7F6A74C1BBDAD772F4FEFC9BF5812036`
- Shippable build payload: 202 files, 126,925,547 bytes

The generated Burst `DoNotShip` diagnostics directory was removed from the handoff payload. The executable was launched repeatedly with `--desk42-evaluation`, exited cleanly, relaunched, captured all states, wrote unique telemetry sessions, and completed the performance probe.

## 16. Capture matrix

The final build produced exactly 60 state captures: 20 canonical states at 1280x720, 1600x900, and 1920x1080. Three labelled contact sheets are retained separately and are not counted as captures.

Visual inspection covered clipping, debug leakage, prompt/card readability, keyboard/controller-equivalent copy, critical-target visibility, M4 sprite integrity, M5 audio references, and progression readability. All 60 passed. Capture logs report `audio_missing=0` for every state.

## 17. Performance results

Built-player probe: Shift 3 Promotion Cascade, 1600x900, 600 sampled frames, maximum text scale, Break panel open, `WHAT HAPPENED` open, telemetry enabled, and audio enabled.

| Metric | Result | Gate |
|---|---:|---:|
| Average FPS | 118.24 | >= 60 |
| p95 frame | 8.95 ms | <= 25 ms |
| Worst frame | 10.91 ms | <= 50 ms |
| Simulation | 29.95 Hz | stable 30 Hz |
| Steady presentation allocation | 0 B/update | 0 target |
| Visual/audio/feedback roots | 1 / 1 / 1 | bounded |
| Audio source objects/growth | 44 / 0 | bounded |
| Missing audio clips | 0 | 0 |
| VFX capacity/growth | 32 / 0 | bounded |
| Temporary object/material growth | 0 / 0 | 0 |

`performance_pass=True`. The probe's internal snapshot replay matched itself at `7253D80F29C3E900`; the canonical complete evaluation campaign checksum is separately proven as `B42CFA89D6277EA2` in three processes.

## 18. Protected-path audit

`git diff --name-only office-slice-m6-baseline-m5-audio..476ee35faba8433d2081e387e572301bbdfa8783` contains zero paths protected by Section 2.

```text
package additions = 0
render pipeline changes = 0
Institutional save schema changes = 0
new campaign content = 0
new Break families = 0
new automation rules = 0
```

No protected Institutional script, scenario, scene, constitution, package manifest/lock, GraphicsSettings, or QualitySettings path changed.

## 19. Human cohort protocol

The locked six-player protocol is `Docs/Product/OFFICE_SLICE_M6_HUMAN_EVALUATION_PROTOCOL.md`. It contains the exact observer instruction, ten post-session questions, behavioural measures, thresholds, anonymous IDs, build hash, and incompatible-build handling.

## 20. Human sessions run/not run

Human sessions run: **0**.

No naive-player result has been collected or inferred. `Docs/Product/OFFICE_SLICE_M6_HUMAN_EVALUATION.md` has intentionally not been created.

## 21. Known limitations

- Human comprehension, relief, humour, memorability, and desire-to-continue remain unknown.
- Controller equivalence and a complete controller shift are automated PlayMode evidence; no physical-controller human session was performed in this technical run.
- Audio integrity is supported by runtime voice/catalog checks and capture logs, not a subjective listening verdict from six players.
- The evaluation mode intentionally forces a fresh onboarding state for cohort consistency; returning-player hint disablement is covered independently by EditMode.
- The performance probe's replay checksum is for its prepared stress snapshot, not the canonical evaluation command stream.

## 22. Explicit claims boundary

This closeout claims only that the locked M6 candidate implements the requested player-facing shell, onboarding, language, settings/accessibility, local telemetry, evaluation mode, deterministic regression coverage, Windows build, capture matrix, and performance gate without protected-path or gameplay-content changes.

It does not claim `M6 EVALUATION CANDIDATE PASS`, `CORE EXPERIENCE IS LEGIBLE ENOUGH FOR ITERATION`, or any human-experience disposition. Those claims require the six-player protocol and a truthful human-evaluation closeout. M7 remains blocked.
