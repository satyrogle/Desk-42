# 1. Status

```text
M3 CONTENT AND ENCOUNTER TECHNICAL PASS
VISUAL, AUDIO, UI, AND HUMAN EXPERIENCE EVALUATION DEFERRED
```

M3 engineering, automated validation, Windows build, built-player campaign smoke,
capture evidence, and development-machine performance validation are closed. M4,
M5, and M6 have not begun.

## 2. Branch and exact commits

- Branch: `codex/office-slice-v0.7-m3-content`
- Frozen M2 technical baseline: `535db6593927a39b1936f1b833474aeff87b0750`
  (`office-slice-m3-baseline-m2-technical`)
- M2 closeout source tip: `257b8c84cb17db873ff3f99534caad11e17d89fb`
- Gate A: `b24e360d995f11abc65da80e8b064cdfb8d89704` —
  `feat: implement m3 campaign foundation`
- Gate B: `0b7e8c8753cfbdd7fdab7cb29e59fc29dc353c87` —
  `feat: implement m3 shift two and pay rule`
- Gate C: `81e029ac52a1d409fa9bae4ea3021722061f9335` —
  `feat: implement m3 promotion cascade`
- Gate D: `2da805c194d279bc087fcf6b4294ccf51362a61c` —
  `feat: complete m3 three-shift campaign`
- Evidence-driven built-player hardening:
  `94eda98175e4f9d9d2bec5c71eb6000a74cf78d6` —
  `fix: rebuild generated campaign folder views`

## 3. Changed-file summary

- 19 Office Slice product source/meta files added or changed under
  `Assets/_Project/Scripts/Product/OfficeSlice/`.
- Product work includes the campaign/checkpoint/replay layer, Shift 2 content and
  complications, Shift 3 Promotion Cascade, upgrades, command schema v3,
  deterministic capture setup, result presentation, and bounded collection access.
- 10 Office Slice EditMode/PlayMode test source/meta files added or changed.
- `Docs/Product/OFFICE_SLICE_M3_BASELINE.md` records the M3 baseline and boundary
  audit.
- No protected Institutional, package, graphics, quality, or render-pipeline file
  changed.

## 4. M3 requirement table

| Requirement | Result | Evidence |
|---|---:|---|
| Three shifts playable in sequence | PASS | `ThreeShiftCampaignReachesResultWithoutDebugControls` |
| One continuing institutional session | PASS | `CampaignUsesOneContinuingInstitutionalSession` |
| Same six named customers | PASS | Nia Bell, Owen Pike, Mara Vale, Iris Cole, Tomas Reed, June Hart persist |
| Eighteen case appearances resolvable | PASS | Six released claims and six completed decisions per shift |
| Shift 1 preserves M2 behaviour | PASS | Full M2 regressions remain in the 487-test EditMode and 26-test Office Slice PlayMode suites |
| Shift 2 adds Rule 2 and two bounded complications | PASS | Pay Rule, Ghost Clock, Missing Room Access tests |
| Shift 3 contains Promotion Cascade | PASS | Exact-trigger, bounded-growth, recovery-order, failure, and restart tests |
| Two upgrade choices persist and affect later play | PASS | First/second upgrade persistence and tier-effect tests |
| Both major failures recover | PASS | Copy Echo and Promotion Cascade recovery tests |
| Campaign failure restarts current shift cleanly | PASS | `ShiftThreeFailureRestartReturnsToCleanCheckpoint` and one active runtime root |
| Full result reached without debug controls | PASS | PlayMode critical path and built-player final-result smoke |
| Full campaign replay matches checksum and snapshot | PASS | Focused replay plus three isolated processes |
| M1 input regression remains green | PASS | Render-rate, one-move-per-tick, device equivalence, buffered interaction, replay lockout tests |
| M2 critical-path regression remains green | PASS | Existing M2 EditMode and Office Slice PlayMode tests pass |
| Full EditMode, zero new skips | PASS | 487/487, 0 skipped |
| Office Slice PlayMode | PASS | 26/26, 0 skipped |
| Institutional Automation PlayMode | PASS | 11/11, 0 skipped |
| Windows x64 build | PASS | `Builds/M3/Desk42.exe`, Unity build result Success |
| Built-player campaign smoke | PASS | Final-result capture process exited 0 with success marker |
| Twelve required captures | PASS | Six states at both 1600×900 and 1280×720; visually inspected |
| Protected boundaries and M4–M6 scope | PASS | Protected audit count 0; no M4–M6 work or forbidden tooling/assets |

## 5. Three-shift content table

The same six customer identities appear in every shift, producing 18 distinct case
appearances from one continuing institutional session.

| Shift | Title / headline | Six public customer problems | Encounter content |
|---|---|---|---|
| 1 | `THE REFUND THAT ARRIVED YESTERDAY` / Nia Bell | Yesterday refund not credited; refund restoration; copied refund ambiguity; changed-desk access; wrong payroll account mark; apparently valid refund | Preserved M2 loop, Rule 1, Copy Echo Break, first upgrade |
| 2 | `THE EMPLOYEE WHO KEPT CLOCKING IN AFTER DEATH` / Tomas Reed | Yesterday charging interest; early clock-in pay; copied badge identity; inactive-room access; post-death shift log; matching badge/log but missing pay | Rule 2, four changed task sequences, Ghost Clock, Missing Room Access, second upgrade |
| 3 | `THE COPIER PROMOTED ITSELF` / Mara Vale | Tomorrow receipt; supervisor moved case; copier holds badge/hours/stamp; missing room listed as department; machine promoted above employee; correct pay with copier approval | Prior observable callbacks, both rules, Promotion Cascade, campaign result and next-day tease |

## 6. Upgrade table

| Upgrade | Tier 1 | Tier 2 | Boundary |
|---|---|---|---|
| Fast Trays | Ordinary transfer 15 → 12 ticks; visible extra tray | 12 → 9 ticks | Does not accelerate anomaly-copy spawning |
| Calm Chairs | +90 ticks to customer mood thresholds; visible chair | Another +90 ticks | Does not stop authored instant pressure causes |
| Red Labels | Copy clear is 15 ticks faster; copies receive a visible red mark | Maximum active copies reduced by 2; original identification 15 ticks faster | Does not prevent either major Break or make automation omniscient |

Exactly one deterministic choice is offered after Shift 1 and Shift 2. Repeating a
family raises it to Tier 2. Choices are command-logged, checksummed, replayed, and
preserved across current-shift restart.

## 7. Automation-rule table

| Rule | Player-facing rule | Availability and evidence |
|---|---|---|
| Rule 1 / Auto Sorter | `IF PAPERS MATCH AND REFUND PATH CLEAR, SEND MONEY` | Learned in Shift 1; remains visible/toggleable; records public match/failure reasons; may accept a copied refund file |
| Rule 2 / Pay Rule | `IF BADGE ACTIVE AND SHIFT LOG MATCHES, SEND MONEY` | Learned in Shift 2; remains visible/toggleable in Shift 3; records public match/failure reasons; may match Tomas Reed or a copied badge/time record |

A match is not labelled morally or objectively correct. Either rule may remain
disabled. Command schema v3 records campaign actions; schema v1/v2 logs remain valid
for their existing commands but explicitly reject M3-only campaign commands.

## 8. Promotion Cascade trigger/effect/recovery evidence

The deterministic trigger requires the complete conjunction:

1. Rule 1 accepted at least one copied refund file.
2. Rule 2 accepted at least one copied badge/time record.
3. Copy Echo is active.
4. A copied promotion form reached Money Room.
5. Mara Vale is at least Upset.

On trigger, the copier gains visible `SUPERVISOR` state, the Runner accepts copier
tasks, the next two eligible folders divert toward Weird Room, and copied promotion
forms spawn at a fixed interval under a hard active-form bound. Normal `SEND` and
`ASSIGN RUNNER` controls and both rule toggles remain available. The public cause
chain contains observable events only; no RNG or hidden adaptive counter is used.

Recovery requires all seven actions: stop copier, remove supervisor stamp, clear all
copied promotion forms, calm Mara, find the original badge file, return it to Front
Desk, and reassign Runner to the Warden. Machine-first and people-first PlayMode
recovery orders pass. Closing-time failure and Shift 3 checkpoint restart also pass.

## 9. Architecture summary

- `OfficeCampaignState` owns shift ordinal, one continuing
  `InstitutionalAutomationSession`, shift definitions, six persistent identities,
  prior observable decisions, rule/upgrade persistence, restart checkpoint, shift
  summaries, replay tape, and final result.
- `OfficeSimulationState` continues to own Warden/input, queues and ownership,
  customers/staff, tasks, automation, anomalies, shift clock, failures, causal events,
  command log, and deterministic checksum snapshot.
- `OfficeCampaignCheckpoint` is product-owned schema v1. It wraps a public
  institutional checkpoint without altering `AutomationRunCheckpoint` or its schema.
- Office command schema is v3. Archived per-shift logs replay with recording disabled;
  live intent is cleared during replay.
- `OfficeCampaignCaptureDriver` reaches named evidence states through canonical input
  intent and simulation commands, not debug force-state controls.
- Unity transforms remain presentation only. `Desk42.Product` references
  `Desk42.Institutional.Player` and `Unity.InputSystem`, not Institutional Authority or
  Domain.

## 10. Exact EditMode results

Unity `2022.3.62f3`, final implementation commit
`94eda98175e4f9d9d2bec5c71eb6000a74cf78d6`:

| Suite | Result | Failed | Skipped | Duration | Evidence |
|---|---:|---:|---:|---:|---|
| Focused `Desk42.Tests.EditMode.OfficeM3` | 35/35 | 0 | 0 | 13.8089032 s | `TestResults/M3/office-m3-editmode.xml` |
| Full EditMode | 487/487 | 0 | 0 | 25.0299988 s | `TestResults/M3/full-editmode.xml` |

The full suite includes the M1 render-rate/input tests, at-most-one Move per tick,
keyboard/controller equivalence, one-shot buffered interaction, replay lockout, the
10,000-tick replay, existing M2 tests, all M3 gate tests, bounded growth, restart, and
capture-state setup.

## 11. Exact PlayMode results

| Suite | Result | Failed | Skipped | Duration | Evidence |
|---|---:|---:|---:|---:|---|
| Office Slice PlayMode | 26/26 | 0 | 0 | 17.9714685 s | `TestResults/M3/office-slice-playmode.xml` |
| Institutional Automation PlayMode | 11/11 | 0 | 0 | 37.7313311 s | `TestResults/M3/institutional-automation-playmode.xml` |

Office Slice coverage includes the three-shift no-debug critical path, replay,
controller-completed shift, both Promotion Cascade recovery orders, failure/restart,
result state, and the generated-folder presentation regression found during built
capture.

## 12. Three-process campaign replay results and checksum

| Independent Unity process | Result | Failed | Skipped | Duration | Checksum | Evidence |
|---:|---:|---:|---:|---:|---|---|
| 1 | 1/1 | 0 | 0 | 2.0478673 s | `B42CFA89D6277EA2` | `TestResults/M3/replay-process-1.xml` |
| 2 | 1/1 | 0 | 0 | 2.0388567 s | `B42CFA89D6277EA2` | `TestResults/M3/replay-process-2.xml` |
| 3 | 1/1 | 0 | 0 | 2.0782134 s | `B42CFA89D6277EA2` | `TestResults/M3/replay-process-3.xml` |

Every process compared both the exact ordered campaign snapshot and checksum. Replay
live-input lockout also passes.

## 13. Windows build result and executable SHA-256

- Target: `StandaloneWindows64`
- Executable: `Builds/M3/Desk42.exe`
- Build result: `Success`
- Final build log: `TestResults/M3/Build/windows-x64-build-final.log`
- SHA-256:
  `F5F73D8616A2500E0FB0223D83774E2D7F6A74C1BBDAD772F4FEFC9BF5812036`
- Default startup order is unchanged; Office Slice remains selected by
  `--desk42-office-slice`.

## 14. Built-player smoke and capture paths

The final-result built-player process traversed all three shifts through canonical
commands, wrote the final campaign result capture, emitted
`OFFICE_SLICE_CAPTURE_OK`, raised no runtime exception, and exited 0. Smoke log:
`TestResults/M3/Final/logs/capture-visible-1600x900-final-campaign-result.log`.

Each directory below contains the same six filenames:

- `shift-1-opening.png`
- `shift-1-copy-echo-break.png`
- `shift-2-ghost-clock.png`
- `shift-2-upgrade-choice.png`
- `shift-3-promotion-cascade.png`
- `final-campaign-result.png`

Capture directories:

- `TestResults/M3/Final/captures/1600x900/`
- `TestResults/M3/Final/captures/1280x720/`

All 12 PNG dimensions were verified, all 12 visible-player logs contain the success
marker, all 12 have zero runtime exceptions, and representative/all-state images were
visually inspected. These captures prove visibility only, not final art quality.

## 15. Performance result

Built-player Promotion Cascade stress state at 1600×900 on the existing development
machine:

| Metric | Result |
|---|---:|
| Average FPS | 118.28 |
| Worst sampled frame | 9.74 ms |
| Sample | 600 frames / 5.072922 s |
| Simulation ticks | 1233 at stable 30 Hz simulation |
| Active folders / copies / time slips / promotion forms | 11 / 5 / 0 / 4 |
| Causal events / commands | 5 / 887 |
| Customers / staff | 6 / 2 |
| Single logical folder ownership | True |
| 60 FPS floor | Met |

Evidence: `TestResults/M3/Final/performance-1600x900.txt` and
`TestResults/M3/Final/performance-1600x900.log`. Growth bounds are covered by tests;
restart tests assert one active runtime root; no product worker thread or global event
subscription was introduced; all player probes exited without a persistent process.
This is not target-hardware certification.

## 16. Protected-path audit

Diff from `office-slice-m3-baseline-m2-technical` through final implementation commit:

```text
Protected paths changed: 0
Baseline ancestry check: PASS
Package additions or updates: 0
Render-pipeline changes: 0
```

No protected Institutional path, Institutional Automation scene, constitution,
package manifest/lock, graphics setting, or quality setting was modified. The existing
automation save schema was not altered.

## 17. Known limitations deferred to M4–M6

- Presentation remains procedural greybox; captures are not a visual-target pass.
- Final art, portraits, pixel-art production, lighting, animation, and Blender or
  ComfyUI production are deferred.
- Final audio, music, sound-library work, and FMOD integration are deferred.
- Final UI, onboarding, terminology tuning, accessibility evaluation, and human
  experience evaluation are deferred.
- No campaign content beyond the locked three shifts was added.
- Performance was measured only on the existing development machine.

## 18. Explicit no-fun-or-retention statement

No fun, retention, commercial, onboarding, comprehension, or naive-player validation
claim is made. M3 establishes a deterministic technical content-and-encounter
candidate for later visual, audio, UI, and human evaluation only.
