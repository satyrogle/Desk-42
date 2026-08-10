# Office Slice M4 Visual Target Closeout

## 1. Status

```text
M4 VISUAL TARGET TECHNICAL PASS
AUDIO, FINAL UI/ONBOARDING, AND HUMAN EXPERIENCE EVALUATION DEFERRED
```

The approved M4 target visually represents the complete three-shift evaluation
campaign. Engineering, automated validation, Windows build, built-player capture,
performance, provenance, and protected-boundary gates are closed. This is not a
claim of commercial final art, fun, onboarding quality, retention, or human
experience validation. M5 and M6 have not begun.

## 2. Branch and exact commits

- Branch: `codex/office-slice-v0.7-m4-visual-target`
- Gate A: `fb4d69a46febbd05142e28477604e0ca8f50fad2` —
  `feat: establish m4 visual pipeline and target frame`
- Gate B: `72c8b8875a1c523133881aabd26ab672f9d7a09e` —
  `feat: replace office greybox with m4 environment target`
- Gate C: `bb5d9fe8148d58e394c8252f73879a880a1887e2` —
  `feat: add m4 character cast and animation language`
- Gate D: `70ab0cf57829e1e93bf923117d64b519d7ed7e0a` —
  `feat: complete m4 machine break and vfx target`
- Gate E: `0d9a008bb07ff43751972ac9bd828f17b8b3dadc` —
  `feat: complete m4 visual target presentation`
- Evidence hardening: `97b27d4ea79b5e60319c76332adf77cffab5d510` —
  `fix: harden m4 visual runtime from evidence`
- Regression compatibility: `a022dc85bc0493493e66c10baf2899f34b9b508a` —
  `fix: preserve office slice presentation compatibility`
- Validated implementation tip used for all final evidence:
  `a022dc85bc0493493e66c10baf2899f34b9b508a`.

The final documentation commit is intentionally reported in the delivery handoff:
a Git commit cannot embed its own final object hash in its contents.

## 3. M3 source and M4 baseline

- Validated M3 implementation:
  `94eda98175e4f9d9d2bec5c71eb6000a74cf78d6`.
- M3 source tip inherited by M4:
  `40f72b342a7468d9ff9739a9eed94e21c79a49db`.
- Frozen tag: `office-slice-m4-baseline-m3-technical` at the validated M3
  implementation.
- Unity: `2022.3.62f3`.
- Render pipeline: Built-in, unchanged.
- M3 deterministic campaign checksum: `B42CFA89D6277EA2`.
- M3 performance baseline: 118.28 average FPS and 9.74 ms worst frame over
  600 sampled frames.

## 4. Changed-file summary

Relative to the inherited M3 source tip, the M4 closeout contains 835 changed or
added files: 379 runtime-art files, 376 ArtLab files, 18 Office Slice product
source/meta files, 12 test source/meta files, 4 deterministic art-pipeline files,
2 editor files, 37 capture/gate evidence files, 4 documentation files, and 3
directory metadata files. The high file count is the deliberate source/candidate/
approved/runtime provenance chain for 181 small runtime assets.

No protected Institutional, package, graphics, quality, constitution, frozen
scene, render-pipeline, audio, M5, or M6 file changed.

## 5. Visual target requirement table

| Requirement | Result | Evidence |
|---|---:|---|
| Complete three-shift campaign represented | PASS | 16 named states at two resolutions |
| No normal-path procedural greybox | PASS | Required M4 catalog loads; legacy greybox is explicit missing-catalog fallback only |
| Warden, two staff, six customers distinct | PASS | Stable silhouette and anchor tests plus capture review |
| Five office zones distinct | PASS | Authored environment kit and department signature tests |
| Machines, upgrades, folder families, anomalies and recovery targets present | PASS | 35 machine, 9 prop, 17 folder and 19 VFX assets |
| Calm, Rush, Break, Recovery and Result distinct | PASS | State overlays, machine states and 32-frame review |
| UI fits both target resolutions | PASS | Exact 1280×720 and 1600×900 layout tests |
| Approved assets have provenance | PASS | 181/181 runtime assets, 100% |
| All visual IDs resolve without production fallback | PASS | Catalog/import tests and capture logs |
| Presentation does not own gameplay | PASS | Projection, animation-boundary, replay and frame-rate tests |
| M1–M3 deterministic regressions remain green | PASS | 534/534 full EditMode and 35/35 focused M3 |
| Both PlayMode suites remain green | PASS | Office Slice 27/27; Institutional Automation 11/11 |
| Windows x64 build and 32 captures | PASS | `Builds/M4/Desk42.exe`; all processes exit 0 |
| Performance and bounded-runtime gates | PASS | Built-player Promotion Cascade probe |
| Protected paths unchanged | PASS | Zero protected-path diffs and unchanged hashes |

## 6. Final art-direction statement

`WARM OFFICE / INK BREAK` is the approved M4 visual target: a fixed-camera,
point-filtered 2.5D workplace diorama with cream paper, warm plaster, moss
furniture, teal machinery, coffee wood, and heavy ink contours. Pressure adds
amber paper wakes; Break replaces warmth with red interruption bars, cyan time
seams, violet impossible-space marks, and local ink fractures without obscuring
navigation. Recovery returns mint/cyan calm while retaining readable residue.

## 7. Palette and state-progression table

| Semantic colour | Hex | Use |
|---|---|---|
| Cream paper | `#E8D9B5` | folders and player-facing cards |
| Warm plaster | `#C7BFA7` | office shell |
| Moss furniture | `#66705B` | calm desks and chairs |
| Machine teal | `#2F6B67` | automation and Warden accent |
| Coffee wood | `#6C4E3D` | trim and counters |
| Calm mint | `#B8D6B0` | recovery and safe feedback |
| Warning amber | `#D8892B` | rush and actionable warning |
| Break red | `#B53B38` | faults and interruptions |
| Ink | `#15151A` | outlines, shadows and type |
| Ghost cyan | `#49C6C8` | Ghost Clock and time slips |
| Impossible violet | `#7B4A88` | Missing Room and Promotion Cascade |

| State | Presentation progression |
|---|---|
| Calm | warm cream, restrained tick-quantised breathing, clean contact shadows |
| Rush | amber edge, queue chevrons, faster presentation motion |
| Break | ink/red key, machine shake, copy bursts and interruption bars |
| Recovery | mint/cyan return, slower pulse and checked recovery marks |
| Result | settled warm neutral, paper-ledger result and tomorrow card |

## 8. Environment asset table

The runtime manifest contains 17 Environment assets: the 1600×900 authored
office base; Calm/Rush/Break/Recovery state layers; Shift 2 and Shift 3 dressing;
route overlay; interaction socket; chair, counter, impossible door, plant, shelf
and vault kit pieces; and three visible upgrade dressings. Five department
signatures are retained: Front Desk/open counter fan, Waiting Area/soft semicircle,
Paper Room/stacked shelves, Money Room/gridded vault, and Weird Room/broken
diagonals and impossible door.

## 9. Character and portrait table

| Cast | Runtime coverage |
|---|---|
| Warden | idle, calm, walk and carry-walk directions, interact, help, fix, stunned |
| Runner | idle, work, carry, walk directions, blocked, copier obedience, return |
| Talker | idle, work, walk directions, blocked, calm-customer |
| Six customers | calm, worried, upset and strange states for each named customer |
| Special customer states | Mara Promotion Cascade and Tomas Ghost Clock portraits |

There are 57 Character sprites and 26 Portrait sprites. Stable bottom-centre
anchors, shared portrait eye lines, and silhouette signatures are enforced by
tests. The cast remains Warden, Runner, Talker, Nia Bell, Owen Pike, Mara Vale,
Iris Cole, Tomas Reed and June Hart.

## 10. Machine, upgrade and folder table

| Family | Exact runtime assets |
|---|---:|
| Machines | 35: seven machines × idle/active/warning/jammed/Break |
| Upgrade/office props | 9: three upgrade families × two tiers, queue post, folder rack, poster |
| Folders | 17: normal, original family, copy tiers, time slip, promotion form, carried, matched and returned |

The seven machines are Front Desk Counter, Paper Check, Money Trace, Auto Sorter,
Copy Echo, Ghost Clock and Supervisor Stamp. Fast Trays, Calm Chairs and Red
Labels remain visible at their deterministic campaign tiers. Original and copied
folders differ by both silhouette and mark, not colour alone.

## 11. Lighting and VFX table

The target uses the Built-in pipeline and authored sprite-state lighting; no
pipeline migration or new package was introduced. Nineteen bounded VFX cover
paper pickup/compare/send, money routing, rule feedback, copy spawn/clear,
machine stop, customer calming, Ghost Clock, Supervisor Stamp, Runner allegiance,
Promotion ink fracture, recovery and shift close. The fixed VFX pool capacity is
32. Reduced-flash mode suppresses high-frequency alternation without changing
simulation state or timing.

## 12. UI visual-language table

| Surface | M4 language |
|---|---|
| Player card | compact lower-left cream-paper card with ink text |
| Current task/action | dominant hierarchy |
| Customer state | reserved portrait, mood and problem |
| Rules/queues | compact switches and bounded counts |
| Break | readable recovery checklist plus controls |
| Result | centred paper ledger with ink contrast and next-day tease |
| Development HUD | F9 opt-in in editor/development builds; absent from production captures |

The HUD fits without clipping at 1280×720 and 1600×900 and does not cover the
critical machine or recovery targets. Final onboarding and terminology evaluation
remain M6 work.

## 13. Blender/ComfyUI/pixel-normalisation pipeline

- Blender `5.1.2`: project-original flat-colour guide rendered from the committed
  source and script.
- ComfyUI `0.24.1`: controlled SDXL/ControlNet target exploration; the executed
  office candidate was rejected because it drifted into outdoor park structure.
- Checkpoint: `sd_xl_base_1.0.safetensors`.
- ControlNet: `controlnet-union-sdxl-promax.safetensors`.
- Deterministic pixel generation/normalisation: version `1.0.0`; point-filtered,
  fixed dimensions, stable anchors and final SHA-256 ledgering.

Workflow SHA-256 values:

| Workflow | SHA-256 |
|---|---|
| `break_vfx_controlnet.json` | `10f41971470eaa4010168d58a82d6941de7024e187647a4d476fa39a16e8779f` |
| `character_turnaround_controlnet.json` | `7025c04fc929dba29b8f40867f75294b7b749daee78dcdfa3ca803e27b5a0cb8` |
| `machine_prop_controlnet.json` | `fd198da91c8ff8af0a07c3484aaf65a41e711893f23f3b61747a8dd0d34d4442` |
| `office_environment_controlnet.json` | `35c5efaf6e891d939cc5e20c7f8e7420ae2a547e6961ebbb386324095226e769` |
| `portrait_expression_controlnet.json` | `9aa2b8a48556562f5d8e9d440b09e3db393af817eacc7a4f511273cd3623dcc2` |

## 14. Provenance audit totals

- Ledger records: 184.
- Reviewer decisions: 183 approved and 1 rejected.
- Approved runtime records: 181.
- Approved source-only records: 2 (Blender guide and target frame).
- Rejected records: 1 ComfyUI office candidate, with reason recorded.
- Runtime manifest assets: 181.
- Runtime files present: 181/181.
- Runtime SHA-256 matches: 181/181.
- Missing or mismatched runtime assets: 0.
- Exact runtime provenance coverage: `100.00%` (181/181).
- Candidate PNGs retained: 182; approved-source PNGs retained: 183.

Runtime category counts are 57 Characters, 1 Config fallback, 17 Environment,
17 Folders, 35 Machines, 26 Portraits, 9 Props and 19 VFX.

## 15. Exact EditMode results

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Focused M4 final candidate | 47 | 0 | 0 |
| Focused M3 | 35 | 0 | 0 |
| Full standard EditMode final candidate | 534 | 0 | 0 |

The focused M4 total contains all 25 required named M4 tests plus catalog/state
matrix checks. The full result retains M1 30/60/144 render-rate equivalence,
one Warden Move per tick, keyboard/controller equivalence, one-shot buffered
interaction, replay input lockout and the 10,000-tick replay. It also retains
Copy Echo's two recovery orders, Promotion Cascade machine-first and people-first
recovery, restart, single folder ownership and exact-once decisions.

## 16. Exact PlayMode results

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Office Slice | 27 | 0 | 0 |
| Institutional Automation | 11 | 0 | 0 |

Office Slice includes the full three-shift path, both major recovery orders,
controller flow, result, replay, shift restart, one legacy runtime container and
one M4 visual root. The Institutional suite ran against unchanged protected code
and scene content.

## 17. Three-process replay results and checksum

The exact `ThreeShiftReplayProducesIdenticalChecksum` test ran in three separate
Unity processes:

| Process | Passed | Checksum |
|---|---:|---|
| 1 | 1/1 | `B42CFA89D6277EA2` |
| 2 | 1/1 | `B42CFA89D6277EA2` |
| 3 | 1/1 | `B42CFA89D6277EA2` |

Each process compared the ordered campaign snapshot and checksum with its replay.

## 18. Windows build result and executable SHA-256

- Platform: Windows x64.
- Unity build process: exit 0.
- Result: Success.
- Executable: `Builds/M4/Desk42.exe`.
- Executable size: 666,624 bytes.
- SHA-256:
  `F5F73D8616A2500E0FB0223D83774E2D7F6A74C1BBDAD772F4FEFC9BF5812036`.
- Startup order remains Institutional Automation scene 0 and Office Slice scene 1;
  the existing Office Slice argument routes the validation player to scene 1.

## 19. Capture matrix paths and visual inspection result

Final built-player paths:

- `evidence/M4/Captures/1600x900/*.png` — 16 PNGs.
- `evidence/M4/Captures/1280x720/*.png` — 16 PNGs.

Both directories contain the exact state names `01-shift-1-opening` through
`16-next-day-tease` defined by the M4 brief. All 32 processes exited 0; all 32
logs contain `OFFICE_M4_CAPTURE_OK`, named state, active asset IDs, visual count,
VFX active/capacity, simulation checksum and path. Automated inspection found
32/32 correct dimensions, zero black frames and zero flat frames.

The final contact-sheet and individual-frame review passed composition, cast,
rooms, machines, folders, progression, UI and technical checks. No missing/fallback
sprite, magenta material, crop, z-fighting, anchor jump, duplicated root, hidden
critical route or unbounded VFX was observed. Review record:
`ArtLab/OfficeSliceM4/Reviews/2026-08-10-visual-review.md`.

## 20. Performance and allocation results

Built-player Shift 3 Promotion Cascade probe at 1600×900, six customers, two
staff, both rules, bounded copies/forms and full M4 presentation:

| Metric | Result | Gate |
|---|---:|---:|
| Frames | 600 | — |
| Elapsed | 5.061331 s | — |
| Average FPS | 118.55 | ≥ 60 |
| 95th percentile frame | 8.96 ms | ≤ 25 ms |
| Worst frame | 10.53 ms | ≤ 50 ms |
| Simulation cadence | 30.03 Hz / 152 ticks | stable 30 Hz |
| Profiler GC peak | 0 B/frame | 0 B/frame |
| Steady visual allocation | 0 B / 10,000 updates | 0 B/update |
| Draw calls / batches | 53 / 53 | recorded |
| Triangles / vertices | 1,784 / 2,566 | recorded |
| Texture memory | 38,110,720 bytes | recorded |
| Active visual roots | 1 | 1 |
| Active visual objects | 43 | bounded |
| VFX active / capacity | 1 / 32 | bounded |
| VFX pool growth | 0 | 0 |
| Runtime material growth | 0 | 0 |
| GameObject growth | 3 | exactly three logical folder visuals |
| Temporary GameObject growth | 0 | 0 |
| Folder ownership | valid | valid |
| Live/replay checksum | `7253D80F29C3E900` / `7253D80F29C3E900` | equal |

`performance_pass=True`. These are development-machine measurements, not
target-hardware certification.

## 21. Protected-path audit

`git diff` from `office-slice-m4-baseline-m3-technical` reports zero protected
files changed. Frozen directory aggregates remain:

| Protected target | Files | SHA-256 |
|---|---:|---|
| Institutional Domain | 34 | `17266fe0300faeaf889c8f56984f363a79c9b9edf72918da9667bbb90e3a5566` |
| Institutional Authority | 123 | `4e2380d65d55635f95741f419fcd0705e1d594237cc2f63d1dcfc8a52fb3f787` |
| Institutional Player | 16 | `74f55bab5a2717835b531eab2e5dfc99a4159f753999c45be9dacc71d7527802` |
| Institutional Runtime | 4 | `18317c226fb1154703cfe88536eb13fd0d63fd56fb9f2fffbf00156e52408ec8` |
| Institutional Scenarios | 14 | `fd6615c7f459caea0d2956abf71be88b3a26ab4f869ba2ccfd090c88cd8cbca6` |

Frozen file hashes remain:

| File | SHA-256 |
|---|---|
| Institutional Automation scene | `8ffba77762a5ec4c90c17404d985b5d4abab4ff54d29f9b6574f452e33426e10` |
| Constitution | `9678093f68863161ec4bcde5c535540778dc9d670e9a82457578973c24d6dae9` |
| Package manifest | `6ee443c11b86c41e52e2f53e0b62688eb3e37ee8f6d8936c00a97206c06c3916` |
| Package lock | `ef4f77916d21b944f2ec68818e937149ba674ceef2dc74af53c8d9c84ef847c` |
| Graphics settings | `257b53b2a8464067ccbafe333c70f7b7f97445d82ef3cc17d065de7f50ef40a1` |
| Quality settings | `944b52d523bcb15b945bc80924b9289f59185ab19bce8a00e99eec345bde6440` |

## 22. Repository asset-budget result

- `Assets/_Project/Art/OfficeSliceM4`: 379 files, 935,408 bytes
  (`0.892075 MiB`), below the 120 MiB gate.
- Runtime PNG payload: 181 files, 207,295 bytes (`0.197692 MiB`).
- Largest runtime texture dimensions: 1600×900 (`office_background.png`).
- Runtime textures over 4096 px in either dimension: 0.
- Package additions: 0.
- Render-pipeline changes: 0.

## 23. Known limitations deferred to M5–M6

- M5 owns authored audio, final SFX/music decisions and any approved audio
  integration; M4 contains no FMOD or final audio work.
- M6 owns final onboarding, terminology testing, final accessibility menu and
  human comprehension/usability iteration.
- Human validation of fun, relief from automation, cause comprehension, desire
  for another shift, retention and commercial quality remains open.
- The target covers the three-shift evaluation campaign only, not full-game final
  art, localisation, store/trailer assets, achievements or platform integration.
- Performance was measured on the development machine and is not hardware
  certification.

## 24. Explicit no-audio/no-human-validation statement

No authored final audio, FMOD integration, music composition, voice acting or
final SFX library was added. No naive-player, onboarding, terminology, fun,
retention, accessibility-comprehension or commercial human-validation gate was
run or claimed. M4 therefore closes only as a visual-target technical pass; audio,
final UI/onboarding and human experience evaluation remain explicitly deferred.
