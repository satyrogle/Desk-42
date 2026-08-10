# Office Slice M5 audio review matrix

Date: 2026-08-10
Candidate branch: `codex/office-slice-v0.7-m5-audio-feedback`

This sheet records the authored routing and bounded voice design for all required
states. `TECH PASS` means the cue exists, resolves to a provenance-backed clip,
uses the expected bus/mix, and stays inside the 32 one-shot / 8 continuous / 4
music limits. It is not a claim that a human listened to or preferred the mix.
No human auditory review was available to Codex; that limitation is explicit
rather than replaced with fabricated listening evidence.

| # | State | Music layer | Ambience | Active machine loops | Important one-shots | Customer cue | Mix | Peak simultaneous voice budget | Review | Notes |
|---:|---|---|---|---|---|---|---|---:|---|---|
| 01 | Shift 1 opening | Work | Calm | Front Desk, Paper, Money, Sorter, Copier, Clock idle bed | None | Calm response | Calm | 8 | TECH PASS | Quiet operational baseline. |
| 02 | Paper Check | Work | Calm | Paper Check active; five restrained bed loops | Paper open, selection, correct/incorrect | Current desk mood | Calm | 10 | TECH PASS | Correct and incorrect use distinct assets. |
| 03 | Money Trace | Work | Calm | Money Trace active; five restrained bed loops | Money open, trace, correct/incorrect | Current desk mood | Calm | 10 | TECH PASS | Trace and Paper entry sounds differ. |
| 04 | Auto Sorter first success | Work + light pressure when applicable | Calm/Rush | Auto Sorter active | Rule enabled, match | Worried if pressure has risen | Calm/Rush | 10 | TECH PASS | Match and reject are distinct. |
| 05 | Copy Echo Break | Work + Pressure + Break | Break | Copier active and dominant; bounded office bed | Copied accepted, trigger, copy spawn | Upset | Break | 13 | TECH PASS | Maximum camera impulse is capped at 0.08. |
| 06 | Copy Echo recovery | Work | Recovery | Copier stopped; remaining bed returns toward idle | Copier stop, clear, original recovered, recovery complete | Calm response | Recovery | 12 | TECH PASS | Break layer reaches zero only in Recovery. |
| 07 | Shift 1 result | Work | Result | Operational loops idle/quiet | Shift close, upgrade choice | None | Result | 10 | TECH PASS | Pressure and Break music targets are zero. |
| 08 | Shift 2 opening | Work | Calm | Front Desk, Paper, Money, Sorter, Copier, Clock bed | None | Calm response | Calm | 8 | TECH PASS | Existing office identity retained. |
| 09 | Ghost Clock | Work + Pressure + Break | Break | Ghost Clock active | Ghost manifestation | Strange | Break | 11 | TECH PASS | Clock asset is distinct from copier/promotion events. |
| 10 | Missing Room | Work + Pressure + Break | Break | Office bed with event-machine focus | Missing Room manifestation | Strange/Upset | Break | 11 | TECH PASS | Missing Room warning has its own asset. |
| 11 | Rule 2 success | Work + Pressure when applicable | Calm/Rush | Payroll/Sorter activity represented in bounded sorter bed | Second-rule match | Current desk mood | Calm/Rush | 10 | TECH PASS | Rule match and reject remain distinct. |
| 12 | Shift 2 result | Work | Result | Operational loops idle/quiet | Shift close, upgrade choice | None | Result | 10 | TECH PASS | No operational alarm music target remains. |
| 13 | Shift 3 opening | Work | Calm | Both automation rules represented; six-loop bed | Rule enable confirmations as invoked | Calm response | Calm | 9 | TECH PASS | Six machine slots plus ambience stay within eight continuous voices. |
| 14 | Promotion Cascade | Work + Pressure + Break | Break | Supervisor Stamp event machine active | Copied accepted, trigger, promotion, authority, Runner shift | Upset | Break | 13 | TECH PASS | Observable event-order test covers the complete chain. |
| 15 | Promotion recovery | Work | Recovery | Copier/authority released; office bed returns | Stamp remove, copier stop, Runner return, original, recovery | Calm response | Recovery | 13 | TECH PASS | Recovery order and final release are tested. |
| 16 | Final campaign result | Work | Result | Operational loops idle/quiet | Shift close, final result | None | Result | 10 | TECH PASS | Campaign result has a distinct authored sting. |
| 17 | Next-day tease | Work | Result | Operational loops idle/quiet | Next-day tease | None | Result | 9 | TECH PASS | Quiet clock-family tease; no new campaign content. |

## Automated mix checks

- Primary actions exceed the Calm ambience bed by the locked readability margin.
- Automation match/reject/accepted cues exceed the Rush bed by the locked margin.
- Copier-stop/copy-clear/recovery cues exceed the Break music layer by the locked margin.
- Routine one-shots stay within 1.4× the worried-customer warning cue.
- The highest authored cue at default bus levels remains below 0.70 full scale.
- Result targets Pressure and Break music to zero.

## Human-review status

Human listening, comfort, comprehension, preference, onboarding, fun, and
retention remain unvalidated. Built-player runtime logs and any system capture
prove execution and routing only.
