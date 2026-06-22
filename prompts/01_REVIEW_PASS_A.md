# Codex Task 01 — Review Pass A

Review the current branch against `docs/IMPLEMENTATION_PLAN.md` Pass A and `docs/ACCEPTANCE_TESTS.md`.

Act as a strict reviewer first. Inspect source and run the browser assertions. Do not assume the previous completion report is accurate.

Check specifically:

- rating fills visibly render
- Machine has no 1440px overflow
- forced WebGL failure still reveals all narrative beats
- reduced motion schedules no continuous Machine loop
- visibility pause/resume cannot create duplicate loops
- one `h1` per page and correct `h2`/`h3` hierarchy
- only one toast is active at once
- docs match actual files and measured counts

If defects exist, fix only Pass A defects and rerun checks. Do not begin Pass B.

Return a pass/fail table with evidence and an exact changed-file list.
