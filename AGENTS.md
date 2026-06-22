# AGENTS.md — Desk 42

## Mission

Maintain and improve the Desk 42 portfolio as a reliable, accessible, high-fidelity retro-futuristic corporate-surrealist experience.

Desk 42 is a **Systemic Desk Simulator / Bureaucratic Dark Comedy**. Its visual language combines mid-century analogue technology, mundane office pressure, brutalist control systems, pixel art, stealth tension, and restrained cosmic horror.

## Read before editing

Read these files in order:

1. `CODEX_START_HERE.md`
2. `docs/PROJECT_CONTEXT.md`
3. `docs/IMPLEMENTATION_PLAN.md`
4. `docs/ACCEPTANCE_TESTS.md`
5. The prompt file for the current task in `prompts/`

Do not start Pass B until Pass A has been implemented and reviewed.

## Repository layout

- `portfolio/` — the actual static site
- `docs/` — project context, plan, acceptance criteria, workflow
- `prompts/` — staged Codex task prompts
- `reference/` — audit screenshots and evidence

## Non-negotiable rules

- Preserve verified portfolio facts. Never invent counts, dependencies, qualifications, project history, or implementation claims.
- Keep fictional corruption separate from factual evidence.
- Do not remove accessibility to achieve visual impact.
- Do not hide unresolved defects behind redesigns.
- Do not claim commits, pushes, screenshots, tests, or validations that did not occur.
- Do not begin broad art changes before reliability defects are fixed.
- Prefer progressive enhancement. Essential evidence must remain readable without JavaScript.
- Keep all runtime assets local unless explicitly approved.
- Avoid generic SaaS cards, generic cyberpunk, neon overload, glassmorphism, and decorative charts.
- The visual priority for the Bureau Operations Console is: **diorama → selected dossier → switchboard → route effects**.

## Current stack

Static HTML, CSS, and JavaScript. Three.js is vendored locally for `machine.html`. Pixel art is generated with Pillow scripts under `portfolio/tools/`.

Do not migrate frameworks during the current two-pass plan. A framework migration is out of scope unless explicitly requested after both passes.

## Local commands

Serve from the site directory:

```bash
cd portfolio
python3 -m http.server 8000
```

Then open:

- `http://127.0.0.1:8000/index.html`
- `http://127.0.0.1:8000/architecture.html`
- `http://127.0.0.1:8000/machine.html`

Useful static checks:

```bash
python3 -m py_compile tools/*.py
node --check pixel.js
node --check motion.js
```

Use Playwright when available for browser verification. The required assertions and viewport matrix are in `docs/ACCEPTANCE_TESTS.md`.

## Working method

1. Inspect source before editing.
2. State the files and behaviour you expect to change.
3. Implement the smallest complete change for the current task.
4. Run the relevant static and browser checks.
5. Inspect rendered screenshots, not only source.
6. Report exact files changed, tests run, results, limitations, and any unresolved issue.
7. Leave unrelated files untouched.

## Git behaviour

Commit and push only when repository access and authorisation are available. Otherwise leave the working tree ready for review and report the exact status. Never claim a push or PR without evidence.
