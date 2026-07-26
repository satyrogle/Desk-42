# Bucket 3 — pre-flight audits and gap analysis

**Branch:** `feat/proof-build` · **Base:** `9e49d2b` · **Date:** 2026-07-26
**Status:** audits complete; implementation blocked on a content-authorship decision

---

## Audit 1 — OfficeSupply preview fail-open

**Result: not reachable as a defect. Document and defer, per the locked instruction.**

Every supply effect that overrides a `Modify*` method, checked for a matching `Preview*`
override and for state mutation inside the `Modify*` body:

| Effect | Overrides | Preview override | `Modify*` mutates state? |
|---|---|---|---|
| `PaperclipEffect` | `ModifyInjectionDuration` | none | **No** — pure (`duration * 2f` for PendingReview) |
| `StaplerEffect` | `ModifyCreditCost` | `PreviewCreditCost` | Yes — guarded |
| `RubberStampEffect` | `ModifyCreditCost` | `PreviewCreditCost` | Yes — guarded |
| `PaperWeightEffect` | all three | none | **No** — all three are pure; its `_emergencySanityUsed` field is mutated only in `Tick`, never in a `Modify*` |

**No stateful `Modify*` lacks a `Preview*` override.** The fail-open default on
`OfficeSupplyEffectBase` is therefore unreachable as a defect today, in the Five-Shift path or
anywhere else. It remains a latent hazard for the *next* stateful effect an author writes —
carried forward as a deferred issue, not a Bucket 3 change.

## Audit 2 — legacy `AudioSource` sweep

**Result: the Five-Shift path plays no audio at all.**

- Exactly two `AudioSource` fields exist in code: `RedTape/PunchCardMachine.cs:51` and
  `UI/CascadePresenter.cs:41`.
- **`Shift.unity` contains zero `AudioSource` components** (one `AudioListener`), so both
  fields are unassigned at runtime.
- Both call sites are correctly null-guarded — `PunchCardMachine.PlaySound` (:261-264) and
  `CascadePresenter` (:172-173) — so there is **no exception risk**, just silence.
- **`Assets/_Project/Audio` contains zero audio clips** (`.wav` / `.ogg` / `.mp3`).
- Every FMOD call site is behind `#if DESK42_FMOD`, which is off.

So: nothing currently plays, nothing duplicates, nothing conflicts, and there is no
participant-facing audio failure — because there is no participant-facing audio. No refactor
performed or needed. `ShiftLifecycleEvent` ordering is untouched.

## Audit 3 — Five-Shift spine: what exists vs what does not

**Exists and is wired:**

- `EliasProofContent` asset with authored appearances for shifts 1, 2 and 5, queue positions,
  and the three Shift-5 branch claim IDs (`elias_shift_5a_claim`, `5b`, `5c`) plus aftermath
  IDs.
- `EliasProofScheduler.TryReplaceScheduledClaim` — selects the Shift-5 claim by
  `state.Shift2Branch` (`NormalisedAddress` → 0, `LegacyException` → 1,
  `PhysicalVerification` → 2).
- `EliasProcedurePolicy` / `TryApplyProcedure` — branch mutation, receipt id, streak/audit deltas.
- `EliasAftermathPolicy.ForBranch` + exactly-once aftermath application.
- `EliasProcedurePanel`, `EliasProcedureReceiptPresenter`.
- Persistence, identity and archive from Buckets 1–2.

**Does not exist:**

1. **No claim content backs the Shift-5 branch IDs.** `elias_shift_5a_claim`, `5b` and `5c`
   appear in exactly one file in the repository — `EliasProofContent.asset` itself. There is
   no `ClaimTemplate` asset, no incident text, no claimant-facing content, and no mechanical
   difference between the branches. `ScriptableObjects/ClaimTemplates/` contains only the
   eight generic templates (`data_breach`, `medical_expense`, …).
2. **No control claimant exists.** No named, memorable non-Elias claimant intended to attract
   false attribution. Repo-wide search for a control/confabulation claimant returns nothing.
3. **No "no-clean-out" state.** Branch 5A's dilemma — the primary causal-behaviour test — has
   no mechanical representation anywhere in code or data.

---

## Blocker

The bucket says *"Implement/integrate the locked Branch A procedure"* and *"the locked
branch-specific consequence."* The scheduling and persistence machinery for those is built and
tested. **The authored content they schedule is not in the repository.**

Producing it means writing the Shift-5 claim text, the shape of the 5A no-clean-out dilemma,
the branch-specific consequences, and a control claimant designed to be plausibly but wrongly
blamed. That is authorship of the central proof content — the compressed statement of the
game's thesis — not integration of a locked artifact.

The handoff is explicit that product decisions and new causal rules are not the engineer's to
make (§1.1, §1.3: *"Do not invent new game-design rules… report the contradiction"*). Inventing
the 5A dilemma would decide what the experiment actually tests, and a proof whose content was
improvised by the implementer cannot validate the thesis it was built to test.

**Reported rather than guessed.** See the handback question.
