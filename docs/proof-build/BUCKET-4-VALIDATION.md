# Bucket 4 — Five-Shift Proof validation / frozen build gate

**Status:** pre-cohort. Populated before tester 1. **No human sessions have been run.**
**Date:** 2026-07-28
**Branch:** `codex/bucket-4-candidate`

---

## 1. Validation question

> When Elias Venn returns, can the player correctly attribute his situation to a procedural
> decision they personally made earlier — without the game simply telling them the answer?

**Gate: at least 4 of 6 valid testers demonstrate causal attribution.**

Not the gate, and not substitutable for it: enjoyment, Bureau comprehension, moral discomfort,
remembering Elias's name, noticing the receipt, understanding the procedure, liking the audio,
guessing correctly. Those are recorded as supporting observations only.

---

## 2. Candidate build

### 2.1 The candidate had to be created — neither branch was valid alone

**No branch in the repository contained both closed Bucket C production and the accepted D1
audio work.** Reported rather than silently validated, per the Bucket 4 brief.

| Reference | In `feat/proof-build` before this pass |
|---|---|
| `6d7bb406f560d25248140343904818f97082899e` — Bucket C final production | **NOT CONTAINED** |
| `1ab478f433d5199bbe279730e593acbb2c48f03c` — Bucket C aggregate validation | **NOT CONTAINED** |
| `20ba10a41462f57942aa006e2e0b426ab3ce84d5` — accepted D1 | contained |

`feat/proof-build` forked from the Bucket C line at `248e2a9` and never received `6d7bb40`, so
it was **missing Bucket C Δ3A carry-forward entirely** — no `CarriedEncounters.cs`, no
`MetaProgressData.CarriedEncounters`, no `ShiftManager.RestoreCarriedEncounters`. A cohort run
against it would have validated a build that is not the closed production state.

The approval-liability canonicalisation fix exists independently on both lines (`43a9560` on
Bucket C, `9441561` on proof-build) — the same change, which is why the merge reconciled
without conflict.

### 2.2 Candidate record

```text
branch              codex/bucket-4-candidate
candidate SHA       eb7ab74f91f690ae7de8a2ba2f21eb00df8a28d0
merge parents       80ab281 (feat/proof-build) + 1ab478f (Bucket C validation)
build identifier    Desk42-ProofStandalone.exe, 118,600,344 bytes
date                2026-07-28
Unity               2022.3.62f3
platform            Windows x64, StandaloneWindows64, Development build
entry point         Shift 1 (see §4)
proof branch config three-branch counterbalance, 2 testers each (see §8)
D1 audio config     State A — DESK42_FMOD undefined, no FMOD SDK in the candidate
                    tree. See §7: this makes no audible difference, because the
                    candidate contains no gameplay audio on the proof spine at all.
logging schema      ProofVerificationTelemetry JSON events, existing schema, unchanged
```

Ancestry re-verified on the candidate: `6d7bb40` **CONTAINED**, `1ab478f` **CONTAINED**,
`20ba10a` **CONTAINED**.

### 2.3 D1 acceptance question — needs a decision before freeze

The brief names `20ba10a` as the "latest accepted D1 production commit". The candidate is built
on `80ab281`, which is two commits later:

- `8fd5e51` — authors the FMOD Studio project, technical event and banks; adds a dev-only probe
  and two PlayMode tests. **This is what makes the D1 audio pipeline functional at all.**
- `80ab281` — documentation only.

Pinning the candidate to `20ba10a` instead is a one-line change of merge base, but it yields a
candidate whose audio pipeline is tooling with no authored event. Given §7 (validate the
discrete cue "fires as intended"), the later base is recommended. **Flagged for explicit
acceptance before the freeze takes effect.**

### 2.4 Scope purity

Confirmed absent from the candidate: Observation Mode, Efficiency Rating, Compliance
Experiment A, D2 continuous audio, `SetSanity` / `SetTideIntensity` / `SetShiftProgress`, and
any pressure/economy prototype. No design change was made to produce this candidate — the merge
authored no production behaviour.

---

## 3. Frozen protocol

Authoritative source: `FIVE_SHIFT_PROOF_PLAYTEST_PROTOCOL.md` v1.0 (preregistered).
Bucket 4 adopts it, with two conflicts resolved below.

### 3.1 Conflict — threshold band

| Source | Rule |
|---|---|
| Protocol §4 | Strong pass ≥4/6 with 0 control confabulation; **weak pass exactly 3/6**; fail <3/6 |
| Bucket 4 brief §24 | **PASS ≥4/6. FAIL ≤3/6.** No softer verdict. |

**Resolved: the Bucket 4 brief governs. 3/6 is a FAIL.** The protocol's "weak pass" band is
superseded for this gate and must not be invoked after results are seen.

### 3.2 Conflict — control claimant

| Source | Control |
|---|---|
| Protocol §1 | "designate one ordinary, one-off **Shift 5** claimant" |
| Bucket 4 brief §8 | **Mara Kest**, who appears on **Shift 3** |

**Resolved: Mara Kest is the control.** The brief is later and names her explicitly, and CΔ1
already proved she is structurally independent of Elias proof state
(`ControlClaimantContent.TryScheduleControlClaimant` takes only the queue and shift number and
reads no proof state). The protocol's "Shift 5" wording is superseded.

Consequence to record: the control question is asked at the Shift 5 interview about a claimant
last seen on Shift 3. That is intentional — she must be memorable enough to be falsely
credited — but a tester who simply does not remember her is **not** the same as one who
declines to confabulate. Record which it is.

---

## 4. Entry point

**Shift 1.** Protocol §1: "Complete Shifts 1–5 in one uninterrupted proof session."

This resolves the Shift 1 vs Shift 2 ambiguity the brief anticipated. Elias appears on Shift 1
(`elias_shift_1`), and `EliasProofScheduler.ValidatePrecedingProofState` refuses to schedule
Shift 2 unless Shift 1 has a factual disposition — so a Shift 2 entry would not produce a valid
proof chain. Shift 1 entry is both the documented and the only mechanically valid choice.

**Frozen once tester 1 begins. Do not vary between testers.**

---

## 5. Facilitator script

Fixed wording. Read verbatim. Nothing added.

### 5.1 Before play

> You're going to play a short section of a game called Desk 42. You work at a claims desk and
> process the claimants who come to your window. I'll show you the controls, then I'll stay
> quiet and let you play. There are no right or wrong answers, and I'm not testing you — I'm
> testing the game. Please think aloud if it feels natural, but don't worry about explaining
> yourself.

Then demonstrate only: how to read a claim, how to use the desk tools, how to Approve/Deny.

**Prohibited before or during play** — do not say, hint, or imply: that decisions have
consequences, that anyone will return, that anything is important to remember, that a claimant
matters, anything about procedural morality, or anything about the purpose of the test.

### 5.2 During play

Silence except for operating help. If asked "should I do X?", reply only:

> That's up to you.

If asked "did I do that right?", reply only:

> There's no right answer here.

### 5.3 After Elias's Shift 5 situation is visible

Ask the primary question (§6), record the complete answer, then the control question (§7),
record the complete answer. **Only after both are banked** may follow-ups be asked.

---

## 6. Primary attribution question — frozen wording

Protocol §2, verbatim:

> **Why do you think Elias was in that situation?**

Record the complete answer before saying anything else. Do not repeat a Shift 2 detail, do not
name the branch, do not ask what the player did earlier.

Explicitly banned as leading equivalents: "Was this because you registered 18A?", "Did your
earlier choice cause this?", "Do you remember what you did on the second day?"

The removed line **`That was you, wasn't it?` stays removed** and must not be reintroduced in
any form, including by the facilitator.

Permitted follow-ups, only after both primary answers are banked (they diagnose depth and
**never** change pass/fail):

1. Who is Elias?
2. What do you remember happening earlier?
3. Why did the later outcome happen?

---

## 7. Control question — frozen wording

Protocol §2, substituting the control's displayed name:

> **Why do you think Mara Kest was in that situation?**

Record the complete answer before any follow-up. Do not describe Mara as ordinary, unrelated,
or irrelevant — that would give away the correct answer.

---

## 8. Success and control coding — frozen before tester 1

### 8.1 CAUSAL ATTRIBUTION SUCCESS

Code `Yes` **only** when the primary answer, unaided, connects **the player's own earlier
procedural action or its factual result** to Elias's later situation.

Qualifying examples: connecting 18A to the amendment they applied; connecting the retained
legacy unit to the later lockdown; connecting their referral to the verification backlog.

**Insufficient on its own** — code `No`:

- recognising Elias without naming a cause;
- remembering `18A` without linking it to their own action;
- "the game brought him back";
- "the Bureau did this" with no link to their own earlier action;
- any vague "all choices matter";
- attribution first supplied only after a follow-up;
- agreement with a cause the interviewer introduced.

The tester does **not** need developer vocabulary. Meaning governs, not phrasing.

### 8.2 Four-way coding categories — frozen now, not after responses

| Code | Definition |
|---|---|
| **Clear success** | Unaided answer explicitly links own earlier action → Elias's situation. No interviewer scaffolding. |
| **Clear failure** | No causal link to own action, or link supplied only after follow-up. |
| **Borderline** | Causal language present but the agent is ambiguous ("the amendment caused it" without owning it). Preserve raw text; two independent coders; record the reasoning for the decision made. |
| **Confabulation / control contamination** | Tester also assigns themselves causal responsibility for Mara, where no such chain exists. |

Two reviewers code independently from the verbatim answers, blind to the running total.
Disagreements are resolved without seeing aggregate pass/fail.

**Ambiguity must not be resolved toward success to reach 4/6.**

### 8.3 Control coding

`ControlConfabulation = Yes` when the tester attributes Mara's situation to an earlier player
action, though the build contains no such link. Distinguish in notes:

- confabulated a causal link (contamination);
- correctly had no causal account;
- **did not remember Mara at all** (a control-validity problem, not a clean negative).

### 8.4 Interpretation matrix

| Elias attribution | Mara confabulation | Reading |
|---|---|---|
| High (≥4/6) | Low (0/6) | Strong evidence the causal chain registered **specifically** |
| High (≥4/6) | Also high | **Do not call this equally strong.** Possible demand characteristic; inspect interview conditions and rerun the affected sample |
| Low (≤3/6) | Any | Proof failed regardless of Mara |

---

## 9. Branch interpretation — do not average

Counterbalance: **2 testers per Shift 2 branch**, per protocol §1.

| Shift 2 branch | Procedure | Shift 5 | Role |
|---|---|---|---|
| `NormalisedAddress` | `AmendRecord` | **5A** — `elias_shift_5a_claim` | **Strongest decision test.** Genuine no-clean-out dilemma. Use to assess whether attribution has *behavioural force*. |
| `LegacyException` | `RetainLegacyUnit` | 5B — `elias_shift_5b_claim` | Softer compliant-vs-humane tradeoff. Tests whether attribution *registers*. |
| `PhysicalVerification` | `ReferForReview` | 5C — `elias_shift_5c_claim` | As 5B. |

Report 5A, 5B and 5C separately. **Do not produce one aggregate Shift 5 success rate.** Do not
claim behavioural validation from 5B/5C merely because attribution occurred.

### 9.1 Pre-cohort finding — the memory anchor exists on Branch A only

Verified live on the candidate build (telemetry from all three branches, §11.4):

| Branch | Receipt beats | Memory anchor | Compliance Streak beat |
|---|---|---|---|
| **A** | 5 | **`M. VENN - REGISTERED 18A`** (index 2) | index 4 — anchor precedes streak |
| **B** | 3 | **none** — `RECORD HELD AT 18B` | **none** |
| **C** | 3 | **none** — `RECORD HELD AT 18B` | **none** |

The locked anchor the brief §3 protects exists **only on Branch A**. B and C present no
person-named anchor and no reward beat at all.

This is authored design, not a defect, and §1 forbids changing it during Bucket 4. But it is a
**structural risk to the gate that must be stated before the cohort, not discovered after**:
under the 2/2/2 counterbalance, **4 of 6 testers receive the weaker anchor**, while the gate
requires 4 of 6 successes. If the result lands at 2/6 or 3/6 with the successes concentrated on
Branch A, that is evidence about anchor strength on B/C — a §26 *memory-anchor* failure — not
necessarily about the causal chain itself.

Recorded now so the diagnosis cannot be constructed after seeing the numbers.

---

## 10. Validity, freeze and evidence rules

### 10.1 Valid session criteria — all must hold

1. tester starts at the frozen entry point (Shift 1);
2. Shift 2 Elias event occurs correctly;
3. the causal procedure is applied;
4. the receipt / memory anchor is presented (branch-appropriate — see §9.1);
5. persistence survives to the intended Shift 5 return;
6. tester reaches the Shift 5 decision and the interview point;
7. no build defect exposes the causal answer directly;
8. no facilitator coaching of attribution;
9. required telemetry for the session exists.

An invalid session is **replaced by another tester in the same counterbalance group**. It counts
as neither success nor failure. Record the reason for invalidity.

### 10.2 Build freeze

Once tester 1 begins, **any visible, audible or interactive production change resets the
cohort** — copy, UI emphasis, audio, interaction timing, claimant scheduling, receipt
presentation, branch behaviour, procedure gating, button behaviour, or presentation that can
affect perception.

Only completely invisible, inaudible, non-interactive logging changes that cannot affect timing
or behaviour are exempt, and each must be documented explicitly here.

Builds are never mixed in the 4/6 denominator.

### 10.3 Evidence per session

Preserve: build SHA, opaque tester ID, shift progression, Shift 2 Elias event, procedure
application, receipt/registration, Shift 5 return, branch, final disposition, ordered
timestamps, validity, coded result, and the verbatim primary and control answers.

No unnecessary personal information. No gameplay change to collect telemetry once the cohort
begins.

---

## 11. Automated preflight — candidate `eb7ab74`

### 11.1 Full suites — clean no-SDK State A

```text
EditMode   461 total   452 passed   0 FAILED   9 skipped
PlayMode    20 total    18 passed   0 FAILED   2 skipped
```

The 9 + 2 skips are the 6 pre-existing non-audio skips plus the SDK-gated FMOD tests, all
correctly `Assert.Ignore`-ing. **Zero non-skipped failures.**

Bucket 1 (persistence / exact-once / identity), Bucket 2 (Elias proof persistence and
lifecycle) and Bucket C (CΔ1 / CΔ2 / CΔ3A aggregate contracts, including the independent
validation suites brought in by the merge) all execute within these totals.

### 11.2 Standalone build

`Succeeded`, **0 errors**, 5 warnings, 118,600,344 bytes.

### 11.3 Proof-spine smoke — all three branches

Cold-started player, Editor closed, via the existing `-desk42ProofEvidencePath` route:

| Branch | Branch written | Shift 5 claim | shift_start | Exceptions | Frames |
|---|---|---|---|---|---|
| A | `NormalisedAddress` | `elias_shift_5a_claim` | 2 | 0 | 100 |
| B | `LegacyException` | `elias_shift_5b_claim` | 2 | 0 | 100 |
| C | `PhysicalVerification` | `elias_shift_5c_claim` | 2 | 0 | 100 |

Shift 2 → persistence boundary → Shift 5 → correct return → correct branch → receipt/history
intact: **PASS on all three branches.**

### 11.4 Receipt ordering (brief §3) — verified live on the candidate

Branch A telemetry:

```text
"anchorBeatIndex":2, "streakBeatIndex":4, "anchorPrecedesStreak":true
beats: RECORD AMENDED -> 18B -> 18A -> M. VENN - REGISTERED 18A
       -> CLAIM ACCEPTED FOR PROCESSING -> COMPLIANCE STREAK +1
```

The anchor lands **before** the Compliance Streak reward. Required ordering is present.
Branches B and C have neither beat — see §9.1.

Note: the shipped string uses a hyphen, `M. VENN - REGISTERED 18A`, not the em-dash used in the
brief. Cosmetic, recorded for exact-match verification.

### 11.5 Procedure interaction contract (brief §6)

| Requirement | Status | Evidence |
|---|---|---|
| Relevant procedural tool identifiable | present | `EliasProcedurePanel` — `AMEND RECORD`, `RETAIN LEGACY UNIT`, `REFER FOR REVIEW`, `REQUEST CLARIFICATION` |
| Disposition locked until procedure applied | present, contract-locked | `EliasProofSessionController.ProcedureRequiredFailureReason`; `ProofLifecycleTests` asserts Shift 2 disposition is gated |
| Specific lock feedback | present | `EliasProcedureFailureReason` — 11 distinct reasons incl. `ToolLockedByBranch`; panel maps to player-facing copy |
| Registered result shown after application | present | receipt beats above |
| Disposition then unlocks | present | gate reads live state (`DispositionGate_ReadsLiveState_NotAnEntrySnapshot`) |
| **REQUIRED PROCEDURE visually located between Elias context and Approve/Deny** | **NOT machine-verified** | layout/position is a visual property; needs human confirmation on the frozen build — see §12 |

### 11.6 D1 audio (brief §7) — the candidate produces no proof-spine audio

Verified by tracing every gameplay caller:

- the **only** gameplay `AudioService.PlayOneShot` call site is `PneumaticTube.cs:90`;
- **no gameplay path requests `EliasRegistrationCausal`** — it appears only in the catalog,
  policy definitions and a dev-only probe;
- `event:/Proof/EliasRegistration18A` is an unfilled slot and resolves to `UnknownEvent`;
- `event:/Threat/PneumaticTube` is also unauthored and resolves to `UnknownEvent`;
- the only authored event is `event:/Desk/Interaction`, the non-production technical tone,
  which nothing in gameplay requests.

**Therefore: no Shift 2 causal motif exists, and no audio fires anywhere on the proof spine.**
§7's "validate that it fires as intended" has no subject in this candidate.

Consequences for interpretation:

- §27 will record "no D1 cues fired" for every tester. This is expected, not an anomaly.
- The risk of audio **over-signalling causality** is zero, which is good for proof validity.
- Audio contributes **nothing** to the memory trace. The anchor is carried entirely by the
  visual receipt, which on B/C does not exist either (§9.1).
- Because no audio plays, State A vs State B makes no audible difference to the cohort. The
  candidate is State A and that is sufficient.

### 11.7 Preflight verdict

**Automated preflight: PASS.** No defect was found that prevents valid proof exposure.
Two items remain for human confirmation before tester 1 (§12).

---

## 12. Outstanding before tester 1

Neither is a code defect; both are visual/authoring properties that only a human can confirm on
the frozen build.

1. **Procedure panel placement** — confirm `REQUIRED PROCEDURE` is visually located between the
   Elias context and Approve/Deny (§11.5).
2. **Receipt legibility** — confirm the Branch A anchor is not visually buried beneath
   Compliance Streak feedback. Ordering is proven; *visual emphasis* is not. Branch A frames
   are captured at `TestResults/smoke-A/receipt-frames/` (100 frames, 10 fps).

If either fails, it is a **pre-cohort blocker** under brief §32: stop, report, repair, revalidate,
and establish a new candidate SHA. Do not run knowingly invalid sessions.

---

## 13. Post-cohort sections — TO BE APPENDED

Not populated. No human sessions have been run.

12. six-tester ledger · 13. raw response references · 14. branch-specific findings ·
15. control findings · 16. gate result · 17. failure-mode diagnosis if applicable ·
18. final verdict

Ledger shape, one row per **valid** tester:

| Tester | Build | S5 Branch | Attribution | Control | Decision Effect | Valid? | Notes |
|---|---|---|---|---|---|---|---|

Raw qualitative notes are kept separately from the ledger. Branch information is never averaged
away.

**Final verdict must be exactly one of:**

```text
BUCKET 4 PASS — CAUSAL ATTRIBUTION GATE MET      (N/6, N >= 4)
BUCKET 4 FAIL — CAUSAL ATTRIBUTION GATE NOT MET  (N/6, N <= 3)
```

with the exact numerator and separate branch and control interpretation. No "mostly passed".
