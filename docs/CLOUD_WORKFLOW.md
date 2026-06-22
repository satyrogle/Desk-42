# Codex Cloud Workflow

## Repository preparation

Use a private GitHub repository where possible. Place `AGENTS.md` at the repository root. Keep `portfolio/`, `docs/`, `prompts/`, and `reference/` intact.

Before the first cloud task:

- confirm the repository contains the current site files
- confirm the default branch is clean
- create a working branch such as `codex/pass-a-reliability`
- do not upload credentials, private work files, or unrelated employer material

## Task sizing

Use one Codex cloud task per reviewable change set:

1. Pass A reliability
2. Pass A review/fix follow-up if needed
3. B1 console
4. B1 review/fix follow-up
5. B2 art
6. B3 corruption
7. Final verification

Do not run B1, B2, and B3 as parallel branches. They touch overlapping HTML, CSS, JavaScript, and art assets and will create avoidable conflicts.

## Review discipline

For every task, require:

- exact changed-file list
- exact commands run
- measured assertions
- screenshots at requested viewports/themes
- limitations and failed checks
- no unsupported claim of completion

Review the rendered output before merging. Visual work is not complete because CSS parses or tests pass.

## Work-laptop constraints

The project is static and can be handled entirely through the cloud repository. Local installation is not required for Codex cloud work. Use the browser to review diffs and pull requests. Keep the original ZIP as a local recovery copy outside the repository when policy allows.

## Internet and dependencies

The project already vendors Three.js. Do not add remote runtime dependencies unless explicitly approved. Prefer local assets and existing tooling. If cloud setup needs Python packages for validation, document them and keep them limited to development/testing.

## Branch naming suggestion

- `codex/pass-a-reliability`
- `codex/pass-b1-console`
- `codex/pass-b2-art`
- `codex/pass-b3-corruption`
- `codex/final-validation`

## Merge gate

A task can be merged only when:

- its acceptance checks pass
- screenshots have been inspected
- no unrelated regression is introduced
- the completion report matches the actual diff
- factual evidence remains unchanged unless a verified correction was requested
