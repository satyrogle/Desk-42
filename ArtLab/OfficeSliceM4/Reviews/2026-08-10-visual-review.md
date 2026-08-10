# Office Slice M4 Visual Review — 2026-08-10

Status: PASS for the M4 three-shift visual target.

Reviewed the 32 built-player captures in `evidence/M4/Captures`, covering all 16 required states at 1600×900 and 1280×720. Automated image QA also confirmed 32 expected dimensions, non-black luminance ranges, and non-flat image variance.

## Composition

- Warden and active customer are immediately visible.
- Front Desk remains dominant; Paper, Money, Waiting and Weird zones retain distinct silhouettes.
- Route overlays, file stacks and Break recovery targets remain readable.
- The compact player card sits over non-critical lower-left floor space and does not obscure required machine targets.

## Characters

- Warden, Runner and Talker remain distinct.
- Six customers retain distinct head/body/prop silhouettes across the matrix.
- Mood, carry, blocked and copier-allegiance states are represented by authored sprite changes.

## Rooms, machines and folders

- Paper, Money and Weird rooms read through different prop and shape grammar.
- Active, warning and Break machine states are visible.
- Original, normal, copy, time slip and promotion form silhouettes/marks remain distinct.
- Fast Trays, Calm Chairs and Red Labels upgrades are visible in their campaign states.

## State progression

- Calm, Rush, Break, Recovery and Result frames are visibly distinct.
- Break uses local ink fractures and warning colour without blacking out navigation.
- Recovery reduces pressure with a mint overlay; result retains shift residue and the next-day tease.

## UI

- Current action is dominant on a cream paper card.
- Customer portrait, mood, problem, rule switches, queue counts and Break checklist remain readable at both resolutions.
- The final result and next-day tease are readable at 1280×720.
- Development diagnostics are absent from production captures.
- Review fixes applied: widened the player card, reserved portrait space, restored the controls line during Break, and changed result text to ink for paper-card contrast.

## Technical review

- No black capture, missing sprite, fallback/magenta asset, z-fighting, anchor jump, texture blur, duplicated visual root or unbounded VFX was observed.
- Every capture log contains the named state, presentation asset IDs, visual count, VFX count/capacity and campaign checksum, and every process exited 0.
- Presentation remains read-only relative to gameplay; validation of determinism, allocation, performance and full regressions is recorded separately in the M4 closeout.

This review approves a target-quality visual statement for the three-shift evaluation campaign. It does not claim full-game commercial final art, final onboarding, final accessibility UI, audio, or human experience validation.

## Final candidate confirmation

The matrix was regenerated from `Builds/M4/Desk42.exe` after the presentation
compatibility fix at `a022dc85bc0493493e66c10baf2899f34b9b508a`. All 32
processes again exited 0 with success markers; dimension/black/flat-frame QA found
zero defects. The regenerated 1600×900 and 1280×720 contact sheets were inspected
and the visual result remained unchanged. The built-player stress probe also
confirmed one M4 visual root, zero temporary-object/material/pool growth and no
fallback use. Final candidate review result: PASS.
