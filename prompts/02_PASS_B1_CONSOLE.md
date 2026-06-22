# Codex Task 02 — Pass B1 Bureau Operations Console

Pass A is assumed reviewed and accepted. Confirm the working branch contains those fixes before editing.

Read `AGENTS.md`, `docs/PROJECT_CONTEXT.md`, `docs/ART_DIRECTION.md`, the B1 section of `docs/IMPLEMENTATION_PLAN.md`, and the B1 checks in `docs/ACCEPTANCE_TESTS.md`.

Replace the 13-card division roster with the **Bureau Operations Console — Department Signal Router · Terminal 04**.

The interaction must be causally connected:

**select channel → route signal → activate department sector → update machine state → decode one dossier**

Non-negotiable priorities:

1. The live bureau diorama is the visual focal point.
2. The selected dossier is the main evidence surface.
3. The switchboard is compact and tactile, not a sidebar or list of cards.
4. Route effects support the system and never dominate it.

Use verified counts and real dependency facts only. Preserve meaningful semantic source HTML without JavaScript. Reuse the existing accessible control-room logic as a foundation, but create a distinct console implementation.

Implement desktop, tablet, mobile, dark/light, reduced-motion, route-resize reliability, and rapid-selection cancellation.

Generate and inspect `assets/pixel/bureau-operations.png`. Reject sparse, symmetrical, diagrammatic, or visibly repetitive output.

Run all B1 acceptance checks and provide screenshots plus a short recording/GIF of at least three selections. Do not begin B2 or B3.
