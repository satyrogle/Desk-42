# Gate 3 — Desk 42 Market and Execution Validation

> **Can Dark Lattice take Desk 42 from an internally completed product to a credible publicly presented product, put it in front of real external users, measure what happens, and make defensible product decisions from that evidence?**

## Branch purpose

This branch (`visa/gate3-desk42-validation-DO-NOT-MERGE`) tracks all evidence-related files for Gate 3 of Jacob's Innovator/Founder visa application alongside the product changes that support the validation work.

**Do not merge this branch into main.** It exists as a parallel evidence trail.

## Pass condition

> Desk 42 has reached Gate 3 when a stable product build has been successfully distributed to genuine external users; structured behavioural, technical and qualitative evidence has been collected and retained at source level; the team has demonstrated the ability to analyse external user evidence and implement at least one meaningful product decision in response; and the project has achieved a credible level of public presentation suitable for external market testing. Where Steam, showcases or external events are used, all market-performance snapshots, submissions and outcomes are preserved as dated and accurately described evidence.

## Execution order

| Step | Deliverable | Status |
|------|------------|--------|
| 3.1 | Stable external playtest build | Not started |
| 3.2 | Minimum credible visual and copy package | Not started |
| 3.3 | Measurement system (tracking + survey infrastructure) | Not started |
| 3.4 | Pilot playtest (5–10 external testers) | Not started |
| 3.5 | Main structured external playtest (Cohorts A + B) | Not started |
| 3.6 | Structured survey | Not started |
| 3.7 | Raw evidence preservation | Not started |
| 3.8 | Evidence-driven product decision (G3-PD-001) | Not started |
| 3.9 | Steam Coming Soon page | Not started |
| 3.10 | Showcase/event submissions | Not started |

## Directory structure

```
evidence/gate3/
├── README.md                      ← this file
├── GATE-3-EVIDENCE-INDEX.md       ← master index of all evidence
├── 01-external-build/             ← build specs, release notes, verification checklists
├── 02-public-presentation/        ← visual identity, copy, Steam assets
├── 03-playtesting/
│   ├── methodology/               ← survey templates, progression checkpoints, data schema
│   ├── tester-register/           ← anonymised tester tracking (D42-Txxx)
│   ├── raw-surveys/               ← NEVER manually edited — originals only
│   ├── session-data/              ← per-session telemetry/logs
│   └── analysis/                  ← processed data, charts, findings
├── 04-product-decisions/          ← product decision records (G3-PD-xxx)
├── 05-steam/                      ← page launch evidence, wishlist snapshots, analytics
├── 06-showcases/                  ← submission records, outcomes, organiser feedback
└── 07-final-validation/           ← assembled Gate 3 evidence pack
```

## What goes in Git vs external storage

**In Git:**
- Build specifications and release notes
- Survey templates and methodology
- Processed/anonymised playtest data
- Product decision records
- Steam evidence indexes
- Showcase submission records
- Screenshots and lightweight evidence files

**External (referenced in evidence index with filename, date, hash, archive location):**
- Packaged game builds (too large for Git)
- Sensitive tester PII
- Large raw data exports
- Video recordings

## Tag convention

```
desk42-g3-playtest-v0.1    ← pilot build
desk42-g3-playtest-v0.2    ← revised build (post-Cohort A)
desk42-g3-steam-launch     ← Steam Coming Soon page goes live
```
