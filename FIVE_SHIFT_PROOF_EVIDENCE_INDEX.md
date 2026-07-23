# Desk 42 — Five-Shift Proof Evidence Index

Generated: 2026-07-23

Player: Windows development build, 1280 × 720

Receipt videos: H.264 MP4, 10 fps, 100 frames, 10.0 seconds

The binary evidence is kept under `PlaytestLogs/FiveShiftProof/Evidence/` and
is intentionally not committed with source.

## Branch evidence

| Branch | Procedure | Receipt | Shift 2 still | Shift 5 still | Video |
| --- | --- | --- | --- | --- | --- |
| A — `NormalisedAddress` | `AmendRecord` | `elias_shift2_amend_record` | `branch-a/shift2-context.png` | `branch-a/shift5-context.png` | `branch-a/branch-a-receipt.mp4` |
| B — `LegacyException` | `RetainLegacyUnit` | `elias_shift2_retain_legacy_unit` | `branch-b/shift2-context.png` | `branch-b/shift5-context.png` | `branch-b/branch-b-receipt.mp4` |
| C — `PhysicalVerification` | `ReferForReview` | `elias_shift2_refer_for_review` | `branch-c/shift2-context.png` | `branch-c/shift5-context.png` | `branch-c/branch-c-receipt.mp4` |

Each branch folder also contains:

- `manifest.txt` with branch, action, receipt, frame rate, and frame count;
- `receipt-frames/frame_000.png` through `frame_099.png`;
- a development-player log.

## Timing verification

Branch A visibly presents:

1. `RECORD AMENDED`
2. `18B -> 18A`
3. `M. VENN - REGISTERED 18A`
4. `CLAIM ACCEPTED FOR PROCESSING`
5. `COMPLIANCE STREAK +1`

In the captured sequence, the Miriam anchor is visible at frame 25 and the
reward is visible at frame 36. The one-second procedure lead-in begins at
frame 10.

## Card-face evidence

- target resolution:
  `PlaytestLogs/card-hierarchy-1920-card-face.png` — 1920 × 1080;
- minimum-resolution check:
  `PlaytestLogs/card-hierarchy-960-card-face.png` — 946 × 591.

## Automated gates

- solution build: passed, 0 errors;
- EditMode: 182 passed, 6 skipped, 0 failed;
- full PlayMode: 13 passed, 0 failed;
- three-route proof harness: 3 passed, 0 failed;
- real player-save fingerprint: unchanged for `meta.json`,
  `meta.json.bak`, `run.json`, `run.json.bak`, and `offender_db.json`.
