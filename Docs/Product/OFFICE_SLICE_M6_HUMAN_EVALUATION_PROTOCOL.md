# Desk42 Office Slice M6 Human Evaluation Protocol

## Locked candidate

- Build: `Builds/M6/Desk42.exe`
- Launch: `Desk42.exe --desk42-evaluation`
- Candidate implementation commit: `476ee35faba8433d2081e387e572301bbdfa8783`
- Executable SHA-256: `F5F73D8616A2500E0FB0223D83774E2D7F6A74C1BBDAD772F4FEFC9BF5812036`
- Cohort size: six naive players minimum, identified only as T01 through T06.

Use this exact build for the entire cohort. If a hard technical blocker requires a new build, stop the cohort and either restart it or report the cohorts separately. Do not merge incompatible-build results.

## Tester selection

Prefer people who have not followed Desk42 development, read its design documents, or watched a complete run. Record only anonymous tester ID, session date, build hash, observations, telemetry summary, and post-session answers.

## Before play

Give each tester a fresh evaluation run and say only:

> This is a work-in-progress game. Play it normally. I won't explain the systems unless the build itself fails.

Do not explain automation, the Break, the causal thesis, which case matters, recovery, or what answer the test expects.

## Observer rule

Do not coach. Intervene only for a genuine technical failure. Confusion is evidence.

Run one uninterrupted attempt. Do not repair terminology or strategy during play. Note the exact intervention and reason if the executable, input device, display, or audio genuinely fails.

## Per-player behaviour record

Record automatically and by observation:

- starts first case without verbal help;
- completes Paper Check;
- completes Money Trace;
- completes first decision;
- understands customer mood pressure;
- uses Calm;
- enables automation;
- experiences automation as useful;
- reaches Copy Echo;
- recovers or reaches the intended recoverable failure;
- understands WHAT HAPPENED;
- finishes Shift 1;
- chooses an upgrade;
- starts Shift 2;
- recognises a returning customer or callback;
- understands Rule 2;
- reaches Shift 3;
- understands the Promotion Cascade cause;
- recovers Promotion Cascade;
- finishes the campaign;
- starts, restarts, or asks for more.

Also record every question, every unexplained pause longer than 10 seconds, every repeated failed action, every obvious UI search, every time the player reads the wrong panel, and every sound or visual cue that changes behaviour. Do not infer intent from telemetry alone.

## Post-session questions

Ask only after the attempt, using these exact questions:

1. What were you trying to do in this game?
2. What did the machines do for you?
3. What made the office difficult?
4. Why did the copier situation happen?
5. What did you do to fix it?
6. Who or what do you remember most?
7. What would you change before the next shift?
8. Was there a moment where you felt in control?
9. Was there a moment where you felt lost?
10. Would you play another shift right now? Why?

Do not ask leading follow-ups.

## Locked thresholds

| Criterion | Required |
|---|---:|
| Start first case without verbal coaching | 5/6 |
| Complete Paper Check and Money Trace | 4/6 |
| Reach first decision | 4/6 |
| Use automation | 4/6 |
| Describe automation as useful or relieving work | 4/6 |
| Connect a rule or machine action to Copy Echo | 4/6 |
| Recover or reach intended recoverable failure | 4/6 |
| Understand WHAT HAPPENED at a useful level | 4/6 |
| Finish Shift 1 | 4/6 |
| Start Shift 2 | 4/6 |
| Recall one named customer, case, or event unprompted | 4/6 |
| Reach or meaningfully engage Shift 3 | 3/6 |
| State a concrete change for the next run | 4/6 |
| Start, retry, or ask to continue | 4/6 |
| Need external terminology explanation | no more than 2/6 |

Also report median session duration, median Shift 1 duration, drop-off point, most common confusion, most memorable character or event, and most common desired change. These are descriptive, not binary gates.

## Closeout

After all six sessions, create `Docs/Product/OFFICE_SLICE_M6_HUMAN_EVALUATION.md` containing the build hash, candidate commit, T01-T06 session dates and results, telemetry summary, observer notes, exact answers, criterion counts, threshold comparison, top five clarity problems, strengths and weaknesses, and the recommended disposition.

The final status must be exactly one of:

- `M6 EVALUATION CANDIDATE PASS`
- `M6.1 CLARITY PATCH REQUIRED`
- `M6 PRESENTATION REVISION REQUIRED`
- `M6 CORE-LOOP REVISION REQUIRED`

Do not begin M7 before that disposition is recorded.
