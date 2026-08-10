# Desk42 Office Slice M5 Audio & Feedback Closeout

## 1. Status

```text
M5 AUDIO & FEEDBACK TECHNICAL PASS
FINAL UI/ONBOARDING AND HUMAN EXPERIENCE EVALUATION DEFERRED
```

The validated implementation candidate is
`d519723d900bdad232b8a01c982c8fbc2ac9ec87`. M6 has not begun.

## 2. Branch and exact commits

Branch: `codex/office-slice-v0.7-m5-audio-feedback`

| Purpose | Commit |
|---|---|
| Exact M4 source baseline | `a022dc85bc0493493e66c10baf2899f34b9b508a` |
| Gate A - architecture and provenance | `a0074ca74921c4a39d1bc56cc966891220810606` |
| Gate B - tactile office interaction audio | `f1fba2ac0c413eff81f5b86106f9416bf679020d` |
| Gate C - office pressure and automation audio | `e766884ddf699d190a7614d001d45064d5e77df3` |
| Gate D - Break, recovery and campaign audio | `8f95ac2e262df62bc977105ca40f0f4bb48e3c36` |
| Gate E - game-feel feedback | `1f7174c4000ac02f144cb9c6b6b2c994f5287de9` |
| Gate F - mix, settings seam and runtime evidence | `f85cccb212f070871dd0efcbb6fb378d1d8e7696` |
| Sustained-feedback pool hardening | `5e6dbd175112c45844e60777e5e2cbf92b077a56` |
| Final unavailable-device parity seam and test | `d519723d900bdad232b8a01c982c8fbc2ac9ec87` |

The subsequent branch-tip commit containing this document and `evidence/M5/`
is documentation/evidence only. It is intentionally not the validated runtime
candidate; a commit cannot include its own hash in its content.

All implementation commits and the baseline tag were pushed before closeout.

## 3. M4 baseline

- Frozen tag: `office-slice-m5-baseline-m4-visual`
- Tag target: `a022dc85bc0493493e66c10baf2899f34b9b508a`
- Baseline status: `M4 VISUAL TARGET TECHNICAL PASS`
- Unity: 2022.3.62f3, revision `96770f904ca7`
- Target: StandaloneWindows64, built-in render pipeline
- Baseline deterministic campaign checksum: `B42CFA89D6277EA2`
- Baseline suite totals: focused M4 47/47, focused M3 35/35, full
  EditMode 534/534, Office Slice PlayMode 27/27, Institutional PlayMode 11/11
- Existing Office Slice authored audio: none

The detailed baseline and frozen hashes are in
`Docs/Product/OFFICE_SLICE_M5_BASELINE.md`.

## 4. Changed-file summary

The validated implementation delta from the M4 tag contains 246 changed files,
8,538 insertions and 5 deletions:

| Top-level area | Files | Purpose |
|---|---:|---|
| `Assets/` | 175 | 65 runtime WAVs and metadata, product audio/feedback code, importer and tests |
| `AudioLab/` | 69 | 65 approved masters, provenance manifests and review matrix |
| `Docs/` | 1 | Frozen M5 baseline |
| `tools/` | 1 | Deterministic audio generator |

The only existing product files changed were Office Slice presentation/
bootstrap seams. No Institutional file, package, protected scene, render setting
or campaign-content definition changed. The documentation closeout adds this
file and 19 evidence files only, bringing the branch delta to 266 files without
changing the validated implementation.

## 5. Audio architecture

```text
OfficeSimulationState / OfficeCampaignState (read-only)
  -> OfficeAudioStateProjector / OfficeAudioStateSnapshot
  -> OfficeAudioEventRouter
  -> OfficeAudioDirector
  -> OfficeAudioVoicePool
  -> fixed Unity AudioSources
```

`OfficeAudioStateSnapshot` copies observable public state and checksum data.
`OfficeAudioEventRouter` derives cue IDs from command and state transitions.
`OfficeAudioDirector` owns mix targets and routes clips. None of these classes
generates commands or mutates simulation, queue, customer, staff, automation,
Break, decision or persistence state.

Runtime ownership is bounded to one active audio root and one feedback root.
The voice pool is created once with 32 one-shot, 8 continuous and 4 music
sources (44 total). Missing optional clips resolve silently. Restart disables
prior roots and clears transient audio. The Unity audio driver is behind this
small product presentation boundary; no FMOD type or package is present.

Presentation settings are separate from campaign saves: Master, Music, SFX,
Ambience, Rumble and ReducedFlash persist under product-owned PlayerPrefs keys.
Audio enabled, audio-device availability and feedback enabled are runtime seams.

## 6. Audio identity

The implemented identity is **WARM OFFICE / MECHANICAL PANIC**: paper, rubber
rollers, relays, bells, cheap motors, office clocks, drawers and stamps form the
normal palette. Break cues transform and intensify that vocabulary rather than
switching to a separate horror or action genre.

Humour is carried by over-confident approvals, the officious promoted-copier
figure, ordinary clock material becoming impossible, and a cheerful corporate
result sting. There are no voices, artist-style prompts, licensed compositions,
generic braams, monster sounds or EDM risers.

This is an authored and technically verified direction, not a human preference
or listening claim.

## 7. Runtime asset counts

| Category | Files |
|---|---:|
| Ambience | 5 |
| Music | 3 |
| Player | 11 |
| Manual work | 6 |
| Customer | 5 |
| Automation | 5 |
| Machine | 17 |
| Major event | 13 |
| **Total** | **65** |

The catalog exposes 96 cue routes over those 65 files. All files are 48 kHz,
16-bit PCM WAV: 57 mono positional/one-shot assets and 8 stereo ambience/music
assets. There are 22 loops and 43 one-shots, totaling 80.56 authored seconds.

## 8. Music/ambience structure

| Layer | Assets | Behaviour |
|---|---|---|
| Office ambience | Calm, Rush, Break, Recovery, Result | Presentation-time state crossfade; two alternating ambience slots |
| Adaptive music | Work, Pressure, Break | Compatible loops; Work remains the identity bed while Pressure/Break enter by state |
| Machine bed | Six current machine slots | Restrained pan, bounded continuous voices, no occlusion simulation |

Result and Recovery target Pressure and Break music to zero. All crossfades use
frame delta in presentation code and cannot alter 30 Hz simulation timing.

## 9. Player-action cue table

| Player action | Cue route | Technical distinction/feedback |
|---|---|---|
| Move | `warden.step.a/b` | Deterministic tick-based alternation and cadence |
| Take folder | `folder.take` | Paper pickup, folder snap, short optional rumble |
| Drop/send folder | `folder.drop/send` | Paper route sound, UI pulse, small route impulse |
| Interact/invalid | `action.interact/invalid` | Invalid route uses a dry non-success asset |
| Paper Check | `paper.open`, `paper.selection`, `paper.correct/incorrect` | Entry, movement and outcome routes are distinct |
| Money Trace | `money.open`, `money.trace`, `money.correct/incorrect` | Trace entry/movement differ from Paper Check; outcomes are explicit |
| Help | `help.start/complete` | Start and completion mapping |
| Calm | `calm.start/complete` | Completion adds customer settle and soft feedback |
| Fix | `fix.start/complete` | Completion adds mechanical release feedback |
| Choice/decision | `choice.confirm`, `decision.stamp` | Separate confirm and stamp identities |

Focused tests prove every primary action resolves a cue, correct/incorrect
results differ, invalid actions do not route success, and cue routing leaves the
command log unchanged. Subjective readability still requires human listening.

## 10. Machine cue table

Every machine family resolves `IDLE`, `ACTIVE`, `WARNING`, `BREAK/JAM` and
`RECOVERED` cue states. Shared warning/release material preserves one office
vocabulary; machine-specific idle/active loops and restrained panning preserve
location and family identity.

| Machine | Pan | Identity source |
|---|---:|---|
| Front Desk Counter | -0.70 | Counter relay/bell loop |
| Paper Check | -0.25 | Paper mechanism loop |
| Money Trace | +0.25 | Trace/receipt mechanism loop |
| Auto Sorter | +0.62 | Confident sorter motor/approval family |
| Copy Echo | +0.55 | Copier roller family |
| Ghost Clock | +0.08 | Ordinary clock family transformed by state |
| Supervisor Stamp | +0.68 | Officious stamp/authority family |

The runtime keeps six machine-bed slots; the sixth switches between Ghost Clock
and Supervisor Stamp according to observable campaign state. Critical one-shots
remain audible through 2D fallback if a source transform is unavailable.

## 11. Customer-pressure cue table

| Observable transition | Cue |
|---|---|
| Calm response | `customer.calm-response` |
| Worried | `customer.worried` |
| Upset/Break mood | `customer.upset` |
| Strange | `customer.strange` |
| Recovery response | `customer.recovery-response` |

Cues are non-verbal and routed only when visible mood changes. The static mix
audit keeps routine action one-shots within 1.4 times the worried warning level.

## 12. Automation cue table

| Automation event | Cue |
|---|---|
| Rule enabled/disabled | `automation.enabled/disabled` |
| Rule matched | `automation.match` |
| Rule did not match | `automation.reject` |
| Copied file accepted | `automation.copied-accepted` (slightly lowered deterministic pitch) |
| Second rule match | `automation.second-rule-match` |

Match, reject and copied acceptance are distinct routes. The default mix audit
places the minimum automation cue at least 1.35 times above the combined Rush
bed. Routing observes existing rule-match records and never changes rule logic.

## 13. Break/recovery cue table

| Sequence | Routed events |
|---|---|
| Copy Echo onset | copied acceptance -> trigger -> copy spawn -> Break mix |
| Copy Echo recovery | copier stop -> copy clear -> original recovered -> recovery complete |
| Ghost Clock | clock manifestation -> transformed clock bed -> recovery release |
| Missing Room | distinct manifestation -> Break mix -> recovery release |
| Promotion Cascade | copied acceptance -> promotion trigger -> copier promoted -> supervisor authority -> Runner allegiance |
| Promotion recovery | stamp removed -> copier stopped -> Runner returned -> original found/returned -> recovery complete |
| Campaign close | shift close -> final result -> quiet next-day tease |

Event-order tests project only observable causal state. Break music is silenced
only after Recovery/Result projection; result state retains no operational alarm
target.

## 14. Game-feel feedback table

| Event | Presentation feedback |
|---|---|
| Folder take | snap, pickup VFX, short low rumble |
| Folder send | route pulse/trail and 0.018 maximum requested impulse |
| Compare success | machine settle, crisp pulse, short rumble |
| Compare failure/invalid | dry pulse and weaker short rumble |
| Calm complete | customer settle VFX and pulse |
| Fix/copier stop | machine recoil/release and short rumble |
| Auto match | rule-accepted tick and neutral pulse |
| Copied file accepted | copy-spawn VFX with subtle machine recoil |
| Break trigger | local fracture VFX, machine recoil, capped impulse/rumble |
| Recovery complete | strong release VFX/pulse and bounded rumble |
| Upgrade/result | install/shift-close VFX and restrained impulse |

Camera impulse is capped at 0.08 and rumble at 0.16 seconds. Rumble and all
feedback can be disabled globally. VFX uses the existing 32-capacity pool,
releases the previous short-lived request before reuse, and auto-releases after
0.18 seconds. No feedback obscures interaction targets or changes action windows.

## 15. Mix-state table

| State | Ambience | Music targets | Operational treatment |
|---|---|---|---|
| Calm | Calm | Work 1.0 | Restrained six-machine bed |
| Rush | Rush | Work 0.55, Pressure 1.0 | Pressure and warnings rise |
| Break | Break | Work 0.55, Pressure 0.52, Break 1.0 | Affected event dominates while recovery cues remain clear |
| Recovery | Recovery | Work 1.0 | Pressure/Break zero; clear confirmations return |
| Result | Result | Work 1.0 | Pressure/Break zero; operations quiet; corporate close cues |

Default settings are Master 0.80, Music 0.55, SFX 0.82 and Ambience 0.58.
Automated checks pass for primary-over-ambience, automation-over-Rush,
recovery-over-Break, protected customer warnings, comfortable defaults and an
authored effective peak below 0.70 full scale. This is nominal mathematical
headroom, not a measured loudness or human comfort claim.

## 16. Provenance totals

- Runtime records: 65/65 approved (100%)
- Runtime SHA-256 records: 65/65
- Source license: 65 `PROJECT-ORIGINAL`
- Author/source: OpenAI Codex project-original deterministic synthesis
- Tool/workflow: Python 3.14.3, workflow 1.0.0,
  `tools/audio/office_slice_m5/generate_audio.py`
- Seeds: recorded per asset
- Approved masters: 65/65 in `AudioLab/OfficeSliceM5/ApprovedMasters/`
- Runtime ledger: `AudioLab/OfficeSliceM5/Provenance/audio-ledger.csv`
- Runtime manifest: 65 assets and 96 cues
- Rejected/candidate assets inside runtime `Assets/`: 0

Generation methods comprise ambience (5), bell (2), clock (2), confirm (12),
machine-loop (14), mechanical (5), music (3), paper (5), reject (6), shot (5),
stamp (2) and warning (4) deterministic synthesis. No voice cloning, artist-
style request, licensed soundtrack or downloaded provenance-free SFX was used.

## 17. Exact EditMode results

| Suite | Passed | Failed | Skipped | Duration |
|---|---:|---:|---:|---:|
| Focused M5 (Gates A-F) | 34/34 | 0 | 0 | 6.8548309 s |
| Focused M4 | 47/47 | 0 | 0 | 12.9520961 s |
| Focused M3 | 35/35 | 0 | 0 | 14.4004696 s |
| Full standard EditMode | 568/568 | 0 | 0 | 44.8576522 s |

Gate totals are A 6, B 5, C 5, D 5, E 5 and F 8. The extra Gate E test
proves sustained VFX recycling. Final focused M3/M4 child processes used the
installed MCP package's documented runtime-only CI flags to prevent unrelated
editor connectivity during batch validation; zero project/package files changed.
XML evidence is under `evidence/M5/Validation/`.

## 18. Exact PlayMode results

| Suite | Passed | Failed | Skipped | Duration |
|---|---:|---:|---:|---:|
| Office Slice PlayMode | 27/27 | 0 | 0 | 18.8932492 s |
| Institutional Automation PlayMode | 11/11 | 0 | 0 | 38.9478896 s |

Both required PlayMode suites pass without new skips. Institutional automation
coverage remains unchanged.

## 19. Determinism/replay results

The three independent full-campaign replay processes produced:

| Process | Passed | Duration | Checksum |
|---|---:|---:|---|
| 1 | 1/1 | 2.1770857 s | `B42CFA89D6277EA2` |
| 2 | 1/1 | 2.3975858 s | `B42CFA89D6277EA2` |
| 3 | 1/1 | 2.1259217 s | `B42CFA89D6277EA2` |

That value exactly matches the frozen M4/M3 baseline. Focused M5 coverage also
completes the same campaign with audio enabled, audio muted, audio device
unavailable and feedback disabled, then asserts identical gameplay campaign
checksums. Audio/feedback state is absent from the authoritative checksum.

The built-player stress report independently records identical final and replay
checksums `7253D80F29C3E900` for its Promotion Cascade scenario.

## 20. Windows build result and executable SHA-256

- Target: StandaloneWindows64
- Path: `Builds/M5/Desk42.exe`
- Unity build result: Success
- Executable bytes: 666,624
- Executable SHA-256:
  `F5F73D8616A2500E0FB0223D83774E2D7F6A74C1BBDAD772F4FEFC9BF5812036`
- Complete build: 203 files, 126,889,123 bytes (121.011 MiB)
- Sanitized build record:
  `evidence/M5/Validation/windows-x64-build-summary.txt`
- Full local build log: `Logs/M5/Windows-x64-Build-Final.log`

## 21. Built-player listening/smoke review

Seven required states were regenerated from the exact final build at 1600x900.
Every process exited 0, emitted `OFFICE_M4_CAPTURE_OK`, loaded 65 audio assets
with 0 missing clips, retained 44 AudioSources, and reported one audio plus one
feedback root. No runtime exception, crash, missing-reference or null-reference
marker occurred. Visual captures were inspected and retain the M4 presentation.

| State | One-shots at capture | Continuous | Music | State checksum |
|---|---:|---:|---:|---|
| Shift 1 opening | 0 | 7 | 1 | `3409F92DEE4C3667` |
| Auto Sorter success | 0 | 7 | 1 | `EF513C9AFC142EFD` |
| Copy Echo Break | 1 | 7 | 3 | `C99DBF9BE86C3424` |
| Ghost Clock | 1 | 7 | 3 | `3D6D98FC807F5DC3` |
| Promotion Cascade | 1 | 7 | 3 | `85F4F38EE5F87E8D` |
| Promotion recovery | 1 | 7 | 1 | `96C358AF3C388212` |
| Final campaign result | 1 | 7 | 1 | `6EB432FF35FE2BDF` |

Capture files are under `evidence/M5/Captures/1600x900/`. Office controls and
controller flow remain covered by the 27/27 Office Slice PlayMode suite; the
built capture harness reached and cleanly exited every required state.

Reliable system-audio capture and human auditory review were not available to
Codex. No claim is made that these clips were heard, comfortable, preferred or
understood by a human. The review matrix records technical routing PASS only.

## 22. Performance/voice-pool result

Built-player Promotion Cascade at 1600x900, 600 sampled frames:

| Metric | Result | Gate |
|---|---:|---:|
| Average FPS | 118.69 | >= 60 |
| p95 frame | 8.89 ms | <= 25 ms |
| Worst frame | 10.59 ms | <= 50 ms |
| Simulation | 30.07 Hz | stable 30 Hz |
| Profiler GC peak | 0 B/frame | 0 target |
| Steady visual allocation | 0 B/update | 0 target |
| Peak one-shot voices | 2 | <= 32 |
| Active continuous sources | 7 | <= 8 |
| Active music sources | 3 | <= 4 |
| AudioSource objects | 44, growth 0 | bounded |
| Runtime AudioClip growth | 0 | bounded |
| Audio PCM estimate | 11,093,800 bytes | recorded |
| Audio assets/missing | 65/0 | complete |
| Active roots | visual 1, audio 1, feedback 1 | bounded |
| VFX capacity/growth | 32/0 | bounded |

`performance_pass=True`; report:
`evidence/M5/Performance/promotion-cascade-1600x900.txt`.

## 23. Repository asset budget

- Runtime WAV payload: 65 files, 11,096,620 bytes (10.583 MiB)
- Approved masters: 65 files, 11,096,620 bytes (10.583 MiB)
- Runtime target: <= 40 MiB - PASS
- Largest runtime WAV: `music_work.wav`, 960,044 bytes
- Longest source: 5.0 seconds
- Generated candidate dump inside `Assets/`: absent
- Uncompressed multi-minute duplicate stems: absent

Runtime assets and approved masters are deliberately separated. The generator,
workflow manifest and per-asset provenance make rejected bulk candidates
unnecessary.

## 24. Protected-path audit

Diff range: `office-slice-m5-baseline-m4-visual..d519723d900bdad232b8a01c982c8fbc2ac9ec87`

| Protected target | Changed files |
|---|---:|
| Institutional Domain | 0 |
| Institutional Authority | 0 |
| Institutional Player | 0 |
| Institutional Runtime | 0 |
| Institutional Authority/Scenarios | 0 |
| Institutional Automation scene | 0 |
| `DESK42_SYSTEM_CONSTITUTION.md` | 0 |
| `Packages/manifest.json` | 0 |
| `Packages/packages-lock.json` | 0 |
| `ProjectSettings/GraphicsSettings.asset` | 0 |
| `ProjectSettings/QualitySettings.asset` | 0 |

Package manifest SHA-256 remains
`6EE443C11B86C41E52E2F53E0B62688EB3E37EE8F6D8936C00A97206C06C3916`;
package lock remains
`EF4F77916D21B944F2EC68818E937149BA674CEEF2DC74AF53C8D9C84EF847C`.
Graphics and Quality hashes also match the frozen baseline.

Explicit audit totals: package additions 0; render-pipeline changes 0;
Institutional/save-schema changes 0; new campaign content 0; M6 paths 0.

## 25. Known limitations deferred to M6

- Final player-facing settings UI is not built; M5 exposes and persists the
  stable Master/Music/SFX/Ambience/Rumble/ReducedFlash seam only.
- Human listening must establish subjective balance, comfort, fatigue,
  positional readability and whether cue differences are understood.
- Human onboarding and terminology testing remain open.
- Fun, retention, replay desire and commercial quality remain unproven.
- No voice acting, final onboarding copy, FMOD migration or expanded campaign
  content was attempted.
- A system-audio review recording was not produced; objective routing, waveform,
  import, mix-margin and built telemetry evidence is retained instead.

## 26. Explicit no-human-validation statement

No human listening, comprehension, onboarding, comfort, preference, fun,
retention or commercial-quality validation was performed or inferred in M5.
Automated tests, deterministic synthesis/provenance checks, built-player runtime
telemetry, screenshots and mathematical mix audits establish a technical
candidate only.

M5 stops here. M6 has not begun.
