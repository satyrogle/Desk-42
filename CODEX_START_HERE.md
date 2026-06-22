# Desk 42 — Codex Handoff: Start Here

This repository is prepared for staged work in **Codex cloud**.

## Why the work is split

The current design direction is strong, but the latest rendered audit found real browser defects, semantic/accessibility problems, stale documentation, and incomplete authored art.

The correct sequence is:

1. **Pass A — reliability and correctness**
2. Human review of Pass A
3. **Pass B1 — Bureau Operations Console**
4. Human review of B1
5. **Pass B2 — richer authored pixel art**
6. Human review of B2
7. **Pass B3 — restrained corruption progression**
8. Final review

Do not delegate the entire plan as one cloud task. Use the staged prompts in `prompts/` so each change can be reviewed before the next one begins.

## First Codex task

Use `prompts/00_PASS_A_RELIABILITY.md` as the first task.

Codex should read `AGENTS.md` automatically, but the task prompt also tells it exactly which project files and acceptance checks matter.

## Recommended cloud workflow

1. Put this repository in a private GitHub repository.
2. Connect the repository to Codex cloud.
3. Start from a clean branch.
4. Run only the Pass A task.
5. Review the diff, validation results, and screenshots.
6. Merge or approve Pass A before starting B1.
7. Start each authored-art task from the accepted branch, not from an unreviewed parallel branch.

More detailed instructions are in `docs/CLOUD_WORKFLOW.md`.

## Source of truth

- `docs/IMPLEMENTATION_PLAN.md` — full requirements
- `docs/ACCEPTANCE_TESTS.md` — required verification
- `docs/PROJECT_CONTEXT.md` — current state and factual constraints
- `AGENTS.md` — durable repository rules
- `prompts/` — task-specific execution prompts

## What not to do

- Do not ask Codex to “make everything amazing” in one task.
- Do not start Pass B while WebGL fallback, overflow, headings, rating fills, toasts, and stale docs remain unresolved.
- Do not accept a text-only completion report for visual work. Require screenshots and measured assertions.
- Do not let fictional corruption modify verified evidence.
- Do not replace the project with a new framework during this plan.
