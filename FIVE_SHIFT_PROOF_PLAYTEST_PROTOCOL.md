# Desk 42 — Five-Shift Proof Playtest Protocol v1.0

Status: preregistered before tester 1

Target sample: 6 first-time testers

Primary question: unaided causal attribution

This protocol tests whether the five-shift Elias spine makes a player feel
"I did this" without the game or interviewer supplying that conclusion.
Engineering correctness is covered separately by the automated three-route
PlayMode proof.

## 1. Session assignment

- Assign an opaque test ID. Do not record a player name in the proof record.
- Run two testers through each Shift 2 branch:
  `NormalisedAddress`, `LegacyException`, and `PhysicalVerification`.
- Complete Shifts 1–5 in one uninterrupted proof session.
- Do not explain that Elias will return.
- Do not describe a Shift 2 action as good, humane, correct, optimal, or
  consequential.
- Do not ask memory or causality questions before the Shift 5 answer is
  recorded.
- Capture screen and game audio for the full session. Capture interviewer and
  tester audio only with consent.

Before the session begins, designate one ordinary, one-off Shift 5 claimant as
the non-causal control claimant. It must not be Elias, an authored Elias
aftermath claim, or a claimant whose state is mechanically linked to an earlier
player action.

## 2. Interview order

After Elias's Shift 5 situation is visible, ask this first, verbatim:

> Why do you think Elias was in that situation?

Record the complete answer before saying anything else. Do not repeat a detail
from Shift 2, name the selected branch, or ask what the player did earlier.

Then ask the matched non-causal control, substituting only the preregistered
claimant's displayed name:

> Why do you think [control claimant] was in that situation?

Record the complete control answer before any follow-up.

Only after both answers are banked may the interviewer ask:

1. Who is Elias?
2. What do you remember happening earlier?
3. Why did the later outcome happen?

Follow-ups diagnose recognition and memory depth. They never change pass/fail.

## 3. Coding rules

Code `UnaidedAttribution = Yes` only when the primary answer independently
connects the later Elias situation to a concrete earlier player action or its
factual result. Examples include connecting 18A to the amendment, the retained
legacy unit to the later lockdown, or the earlier referral to the verification
backlog.

Do not count:

- recognising Elias without naming a cause;
- saying only that "the game brought him back";
- attribution first supplied after a follow-up;
- agreement with a cause introduced by the interviewer;
- a vague claim that all choices matter.

Code `ControlConfabulation = Yes` when the tester similarly attributes the
ordinary control claimant's situation to an earlier player action even though
the build contains no such causal link. This detects a general demand
characteristic rather than proof-spine understanding.

Two reviewers code independently from the recorded verbatim answers. Resolve
disagreement without seeing aggregate pass/fail totals.

## 4. Preregistered decision thresholds

For six valid sessions:

- **Strong pass:** at least 4 of 6 show unaided attribution on the primary
  question and 0 of 6 show equivalent causal confabulation on the control.
- **Weak pass:** exactly 3 of 6 show unaided attribution and 0 of 6 show
  equivalent causal confabulation on the control.
- **Fail:** fewer than 3 of 6 show unaided attribution.
- **Contaminated result:** one or more control answers show equivalent causal
  confabulation. Do not call this a pass; inspect the interview conditions and
  rerun the affected sample before expansion.

The threshold is not revised after responses are seen.

## 5. Secondary observations

Record these separately from the causal-attribution result:

- whether the player can state what the Shift 2 procedure changed;
- whether the procedure receipt was read before reward feedback;
- whether the player notices the Shift 5 locked tool;
- whether the player can identify the temporary aftermath condition;
- whether the player describes any decision as right/wrong despite the
  no-grading design;
- any point where the player cannot predict or explain an action.

## 6. Session record

| Field | Value |
| --- | --- |
| Opaque test ID | |
| Assigned branch | |
| Shift 1 disposition | |
| Shift 2 procedure | |
| Shift 2 disposition | |
| Shift 2 receipt ID | |
| Shift 5 claim ID | |
| Compromised tool | |
| Shift 5 disposition | |
| Temporary modifier | |
| Primary answer, verbatim | |
| Unaided attribution | Yes / No |
| Control claimant | |
| Control answer, verbatim | |
| Control confabulation | Yes / No |
| Follow-up notes | |
| Credibility/awareness observations | |

## 7. Evidence gate

Before tester 1:

- retain passing EditMode and PlayMode results;
- retain a Shift 2 and Shift 5 context screenshot for every branch;
- retain at least ten seconds of video for every Shift 2 receipt branch;
- verify Branch A video shows `M. VENN - REGISTERED 18A` before
  `COMPLIANCE STREAK +1`;
- verify captures contain no debug overlay that explains the later causality.
