# Desk42 Institutional Buildcraft Run v0.4

## Status

Playable product-depth checkpoint on
`codex/institutional-buildcraft-run-v0.4`.

## Accurate checkpoint statement

Desk42 v0.4 is a playable institutional automation run in which a selected
doctrine remains binding across eight shifts, drafted procedures compound into
a two-slot build, real appellate holdings can change later automation, and the
same persistent society supplies possession, access and collective-grievance
work to the physical claims floor.

The checkpoint demonstrates an end-to-end institutional buildcraft loop. It
does not claim production art or audio, broad commercial content, an unbounded
population simulation, or player-validated retention.

## Product loop

```text
choose a binding doctrine
-> receive a docket from one persistent society
-> diagnose queues, deadlines, heat and faults
-> route incomplete evidence through the factory
-> commit evidence-driven rulings and remedies
-> observe the same people react
-> process appeals into real holdings
-> draft or upgrade one institutional procedure
-> configure which holdings automation may cite
-> begin the next shift under the accumulated institution
-> receive an outcome derived from the completed run
```

The factory remains the directly manipulated game. The institutional society
creates future workload and explains why the consequences compound.

## Binding doctrine

The player chooses one doctrine before the first dossier enters Intake. It
cannot be freely changed during the run.

- **Proof Fortress** uses a high public-evidence threshold, narrow scope,
  greater verification capacity and slower, more expensive processing.
- **Rubber Mill** uses a low recognition threshold, broad scope, fast intake
  and cheap capacity at the cost of greater fault and appeal exposure.
- **Appeal Refinery** uses a moderate initial threshold, stronger Legal
  throughput and weak early output in exchange for holdings that accelerate
  later work.

## Eight-shift cadence

Each shift now has a definite operational boundary:

```text
docket release
-> active processing
-> peak queue pressure
-> physical appeal return
-> all live work clears
-> compact shift result
-> institutional procedure choice when scheduled
-> next retained-society batch
```

The next shift cannot overlap the current shift's unresolved physical work.
Shift summaries are calculated from claims, missed deadlines, appeals,
holdings and society ticks that occurred during that shift.

## Procedure buildcraft

The loadout remains limited to two procedures. Scheduled shift closes offer a
deterministic three-choice draft made from currently eligible procedures and
upgrades. Existing procedures can reach Tier III.

- **Mandatory Secondary Verification** creates a second physical pass, can
  prioritise urgent files and can retain an issue-specific verification
  pattern.
- **Presumption of Validity** reduces routine verification work while
  increasing heat and reliability exposure.
- **Automatic Adverse Review** routes overdue files through Legal, can pause
  deadlines and can retain repeated review patterns.
- **Protected Evidence Channel** reserves the auxiliary route, reduces
  evidence risk and can require access-restoration review.
- **Appeal Fast Track** binds the appellate route, reduces Legal heat and can
  generate an additional development credit from a qualifying holding.
- **Precedent Reuse** cites installed holdings, reduces matching evidence work
  and can combine compatible holdings into stronger acceleration with added
  operational risk.

No procedure is a single isolated percentage bonus. Each changes at least two
of routing, workload, heat, fault exposure, deadlines, appellate procedure,
society effects or precedent use.

## Precedent ledger

Holdings are projected from the Authority-owned docket rather than created as
factory collectibles. The ledger shows:

- issue family and scope;
- originating appeal;
- current matching cases;
- prior applications;
- processing effect;
- liability exposure;
- conflicting holdings;
- current automation mode.

Each holding can be configured as mandatory citation, permitted citation,
human review required or do not automate. Human review creates a physical
Legal detour before adjudication. Citation is recorded on the real holding and
shown on the dossier.

## Third active family: collective grievance

The persistent seed now allows agents with a shared institutional grievance to
form a collective commitment through the generic opportunity and decision
pipeline. The resulting case:

- carries several affected parties;
- enters the same Intake, evidence, verification, adjudication and Legal
  network as other work;
- displays linked-dossier and shared-evidence markers;
- creates more verification work than a single-party dossier;
- can apply recognised group standing to every affected member exactly once.

The active product family order is now possession, access and collective
grievance. No family is implemented as a separate minigame.

## Product save and resume

A versioned, checksum-protected product checkpoint saves:

- run phase, shift, doctrine, routes and appeal mode;
- machine upgrades, heat, jams, queues and active work;
- every in-flight dossier and its verification or appeal state;
- credits, procedures, tiers and draft choices;
- the complete opaque Player-layer institutional checkpoint;
- society, docket, rulings, appeals, holdings and precedent modes.

The store uses a temporary file, an atomic replacement where the platform
supports it and a previous-version backup. Restoration rebuilds physical queue
ownership and validates the embedded institutional snapshot before play
continues.

## Branch Review

After Shift 8 the run ends with an institutional assessment derived from:

- throughput;
- deadline compliance;
- avoidable rework;
- appeal reversal rate;
- unresolved liability;
- society stability;
- institutional legitimacy;
- precedent consistency;
- machine resilience.

The resulting assessment is one of Certified, Efficient but Harmful, Humane
but Insolvent, Captured, Precedent Collapse or Administrative Blindness. It is
not selected through narrative dialogue.

## Readability changes

- possession, access and collective dossiers use distinct floor colours;
- collective files show linked packet bands;
- urgent and overdue states remain visible on the dossier;
- descendant claims show lineage;
- appeals show their originating ruling;
- citations pulse on the dossier and report the holding count;
- holding creation receives dedicated feedback;
- doctrine selection, shift close, procedure draft, ledger and Branch Review
  are presented in compact in-world overlays.

## Architecture boundary

```text
Domain      -> agents, decisions, status, rulings and scope vocabulary
Authority   -> material truth, docket, remedies, appeals and holdings
Player      -> public-safe session, precedent policy and opaque checkpoint
Product     -> Unity floor, run build, machines, UI and product save envelope
```

`Desk42.Product` still references `Desk42.Institutional.Player` only. Scenario
and product code do not receive direct access to authoritative lived truth.

## Validation

- Standard EditMode: **414 / 414 passed**.
- Long-run EditMode: **1 / 1 passed**. The retained society completed eight
  twelve-dossier batches and ninety-six rulings before final state validation.
- Active-product PlayMode: **10 / 10 passed**, including doctrine lock, shift
  drafting, the shared collective floor, physical procedure routes, overload
  and repair, appellate holdings, live save/resume and the complete eight-shift
  Branch Review.
- Windows x64 player build: **passed**.
- Visible built-player smoke and 1600 x 900 capture: **passed**.

The two archived causal-legibility interface tests remain outside the active
product fixture because their retired scene is intentionally absent from the
product build. Batch validation also disables the unrelated editor-only Unity
MCP connection; gameplay assertions and Unity error handling remain strict.

## Current limits

- The product is still a visual and audio blockout.
- The society remains a bounded persistent eight-agent slice.
- Three issue families are active; this is not broad case-content coverage.
- Automated adjudication uses authored institutional thresholds, not machine
  learning.
- The Branch Review is a systemic run outcome, not evidence of long-term player
  retention or commercial validation.
- The product needs external player sessions before claims about addictive
  build diversity or comprehension are supportable.
