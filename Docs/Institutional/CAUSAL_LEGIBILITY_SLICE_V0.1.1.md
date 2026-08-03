# Causal Legibility Slice v0.1.1

## Checkpoint claim

Desk42 contains a thin playable institutional society loop in which the player
inspects incomplete official evidence, selects a disposition and narrow or broad
scope for a system-derived ruling, commits an executable material remedy, and
observes attributable public-safe consequences without access to authoritative
truth.

This checkpoint does **not** claim player-authored findings, evidence citation,
holdings, temporal reach or remedies. Those stages remain deliberately bounded and
are labelled as system-derived, fixed or disposition-required in the product.

## v0.1.1 semantic corrections

- `restore-possession` now executes exactly once as a material transfer to the
  registered owner. A persisted trace freezes ruling, case, resource, destination
  rule, applied tick, before/after custody and material event identity.
- The playable disposition vocabulary is now `Recognised` or `Denied` only. The
  provisional false choice was removed from this slice without removing the wider
  engine's provisional disposition support.
- Denial preserves the proposed holding and scope in the audit payload but the
  player projection states that neither was established or applied.
- Scope application stores its actual `AppliedTick`; later simulation pulses no
  longer move historical timeline entries.
- Current history and replay origin now persist in one checksum-protected atomic
  session envelope with session identity, generation identity and origin/current
  hashes. Mixed-session histories are rejected.
- `Desk42.Product` references only `Desk42.Institutional.Player`. Its public command
  vocabulary no longer exposes the Domain disposition enum.
- Raw employer and household identifiers are not projected as recognised facts.
  Employer identity remains explicitly unadjudicated and household has no official
  record in this possession slice.
- Primary consequence copy uses human-readable causal statements. Raw cause, ruling
  and scope identifiers are hidden behind the `SHOW TECHNICAL TRACE IDS` control.

## Validation

Validated locally with Unity `2022.3.62f3` on Windows:

- changed-area EditMode: **29 passed, 0 failed, 0 skipped**;
- complete EditMode: **400 passed, 0 failed, 0 skipped**;
- focused product PlayMode: **3 passed, 0 failed, 0 skipped**;
- Windows x64 player build: **success**;
- built-player ruling, descendant-case and atomic save/load smoke: **success**.

Unity was launched for validation with CI connection overrides so the installed
Unity MCP package did not turn an unavailable cloud authorization into an unrelated
test failure. The uncontrolled baseline reproduced that package error while all
simulation assertions passed.

Local validation artifacts are retained under `tmp/causal-legibility/` and remain
ignored source-control scratch material.

## Remaining gate

The slice is ready for the six-player causal-comprehension test. Automation does not
establish mouse targeting, scrolling, visual hierarchy, first-time understanding or
whether players can correctly explain the difference between disposition, scope,
remedy and later autonomous behavior.

No additional incident family or backend subsystem should be added before that
human gate is recorded.
