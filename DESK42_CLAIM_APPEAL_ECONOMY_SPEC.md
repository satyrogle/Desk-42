# Desk 42 — Claim–Appeal Economy Specification

> Status: design specification, not an implementation plan  
> Date: 2026-08-02  
> Direction: persistent bureaucratic automation / management simulation  
> Internal pitch: **Factorio where defective products hire lawyers.**

## 1. Product decision

Desk 42 is a persistent branch-office automation game. Claimants and their dossiers enter the branch, departments transform incomplete information into rulings, and defective rulings return as appeals. Appeals do not merely punish the player: they consume capacity, reveal faults in the machine, and sometimes change the rules under which future claims are processed.

The defining loop is:

```text
CLAIM ARRIVES
→ INFORMATION IS PROCESSED
→ THE BRANCH MAKES A RULING
→ THE RULING CREATES COST, PAYMENT OR APPEAL
→ APPEALS EXPOSE OR CHANGE POLICY
→ THE PLAYER REBUILDS THE BRANCH
```

The game is not about personally checking paperwork forever. The player performs each operation manually only long enough to understand it, then constructs a machine that performs it at scale. Every automation removes a repetitive burden while exposing a higher-order problem: queues, uncertainty, staffing, contradictory policies, appeals and institutional risk.

This direction explicitly excludes combat, the Door, Fugue, card-run structure and a separate action route. Anomalies survive as unusual claimants, evidence sources, workers and production modules inside the automation simulation.

## 2. Load-bearing decision: accuracy has an irreducible floor

### 2.1 Ruling

A sufficiently developed branch cannot purchase perfect knowledge.

Some claims are undecidable within their statutory deadline because records are missing, witnesses contradict one another, evidence arrives from incompatible timelines, or the relevant rule has never been tested. Better departments reduce avoidable errors, but they do not remove the epistemic floor.

This is not random punishment. Every unresolved uncertainty must have a visible cause and a route the player could have designed around.

### 2.2 Two kinds of imperfection

The simulation distinguishes:

1. **Avoidable error** — caused by insufficient verification, overloaded staff, bad routing, poor equipment or reckless policy.
2. **Residual uncertainty** — information that cannot be established before the deadline, even by a well-built branch.

Avoidable error can approach zero. Residual uncertainty cannot.

The player can respond to residual uncertainty by:

- approving under risk;
- denying under risk;
- holding for further evidence;
- escalating to a human adjudicator;
- reserving money against a probable reversal;
- routing the claim into a test case;
- purchasing a specialist opinion;
- applying a precedent that makes the uncertain fact legally irrelevant.

The important distinction is:

> Perfect truth is impossible. Perfectly traceable procedure is achievable, but usually too slow and expensive to apply to everything.

### 2.3 Tuning hypothesis

These are starting targets for design simulation, not final balance:

- Early campaign: 2–4% of claims contain residual uncertainty.
- Mid campaign: 6–10%.
- Late campaign: 10–18%, offset by stronger policies and precedents.
- A well-built branch should reduce avoidable incorrect rulings below 1–2%.
- A reckless throughput build may push avoidable incorrect rulings above 15%.
- Even an excellent branch should need a meaningful Hold, Escalation or Appeals route.

The accuracy floor therefore creates infrastructure rather than helplessness.

## 3. Claim state model

Every claim is an individual work item with persistent history. It must never collapse into a coloured box whose only property is destination.

### 3.1 Authoritative fields

```text
Identity
  Claim ID
  Claim family
  Claimant type
  Jurisdiction
  Requested remedy
  Requested amount

Ground truth
  True eligibility
  True payable amount
  Fraud state
  Hidden causal facts

Known information
  Evidence items
  Evidence completeness
  Evidence reliability
  Identity confidence
  Eligibility confidence range
  Contradictions

Procedure
  Statutory deadline
  Required checks
  Completed checks
  Applicable policies
  Applicable precedents
  Assigned priority
  Current department

History
  Every transformation
  Every skipped operation
  Every employee or machine involved
  Decision and confidence at decision
  Appeal level
  Previous appellate findings

Economy
  Processing cost
  Potential payout
  Liability reserve
  Delay compensation
  Audit exposure
```

`True eligibility` exists so the simulation can judge outcomes, but it is not exposed directly to the player. The player sees evidence, confidence intervals, contradictions and procedural defensibility.

### 3.2 Evidence ceiling

Each claim has a deterministic `DiscoverabilityCeiling`. This represents the maximum confidence available before its current deadline.

Examples:

- Complete employer records: 0.98.
- Destroyed personnel file with two consistent witnesses: 0.84.
- One witness whose testimony occurred tomorrow: 0.67.
- Two legally valid originals with mutually exclusive dates: 0.52.

Verification improves knowledge until it reaches that ceiling. Repeating the same check cannot grind confidence to 100%.

The cause of the ceiling is inspectable. The player can click the confidence display and see, for example:

```text
Maximum currently establishable confidence: 67%

Missing employer record                   −14%
Witness statement is temporally displaced −11%
Applicable precedent is disputed           −8%
```

## 4. The ruling matrix

The four primary outcomes structurally close blanket strategies:

| Ground truth | Branch ruling | Primary result | Downstream risk |
|---|---|---|---|
| Valid | Approve | Correct payout, quota credit | Overpayment if amount was poorly verified |
| Invalid | Deny | Correct rejection, saved payout | Complaint only if procedure was defective |
| Invalid | Approve | Incorrect payout | Fraud loss, audit exposure, recovery work |
| Valid | Deny | Claimant denied entitlement | Appeal, compensation, legal workload, precedent risk |

Additional decisions are not free escapes:

| Decision | Benefit | Cost |
|---|---|---|
| Hold | Avoids premature ruling | Occupies buffer space; deadline continues |
| Escalate | Expert human judgment | High payroll cost and scarce adjudicator time |
| Request evidence | Raises discoverability ceiling | Adds delay and may fail |
| Provisional approval | Prevents claimant harm | Creates reserve and recovery exposure |
| Test case | Can produce useful precedent | Requires legal preparation and risks adverse precedent |

## 5. Appeals are a second production stream

### 5.1 Appeal creation

An appeal inherits the original claim rather than spawning as a generic new object.

It retains:

- the complete decision history;
- evidence known at the time of ruling;
- evidence ignored or bypassed;
- applicable policy versions;
- the employee and workstation chain;
- the original deadline and processing cost.

It adds:

- a new legal deadline;
- claimant grounds of appeal;
- additional evidence where applicable;
- compensation exposure;
- a required standard of review;
- an appeal level;
- possible precedent scope.

An appeal therefore costs more to process than its original claim and reveals exactly how the machine produced the defect.

### 5.2 Appeal outcomes

```text
UPHELD
The original ruling stands.
The branch receives procedural validation but still pays Legal costs.

OVERTURNED — CORRECTABLE ERROR
The ruling changes.
The branch pays remedy, compensation and rework costs.
No useful precedent is created.

OVERTURNED — POLICY DEFECT
The ruling changes and a policy is marked unsafe.
Similar pending claims are flagged for reprocessing.

PRECEDENT SET
A genuinely unresolved legal question receives a binding interpretation.
Future claim processing changes within the precedent's scope.

SANCTIONED
The branch ignored known facts, bypassed required procedure or manufactured a test case in bad faith.
No beneficial precedent is awarded.
```

### 5.3 Appeals must be traceable

Every appeal provides a causal ledger:

```text
Claim denied by Policy 14B
Eligibility confidence at ruling: 61–83%
Verification Stage 2 bypassed by Priority Rule 6
Supervisor override unavailable: statutory break
New evidence confirms eligibility
Legal route required: policy defect
```

The joke may be absurd. The cause may not be arbitrary.

## 6. Appeals Refinery governor

The Appeals Refinery is a legitimate high-risk build, not an exploit and not a trap.

### 6.1 Why unrestricted appeal farming fails

If every overturned decision creates valuable precedent, deliberate incompetence becomes optimal. The player would manufacture bad rulings instead of building a good institution.

Therefore precedent is not generic experience and not guaranteed appeal loot.

### 6.2 Conditions for beneficial precedent

A beneficial precedent can be created only when all of the following are true:

1. The claim presents a novel or genuinely conflicting legal question.
2. The original confidence range crossed the decision threshold.
3. Required checks were completed or explicitly waived through a lawful policy.
4. Legal prepared the case before the ruling or accepted it as a test case.
5. The issue has not already produced precedent within the same scope.

Deliberately denying a clearly valid claim produces a correctable error or sanction, never a useful precedent.

### 6.3 Five governors

**Narrow scope**  
Most precedents apply only to a claim family, jurisdiction, claimant type or evidence condition. Wide precedents are late-game and dangerous.

**Hard hearing capacity**  
Legal has limited hearing slots. Excess appeals remain in a deadline queue. Missed appellate deadlines become automatic losses.

**No duplicate harvest**  
Repeated appeals on the same settled issue generate no additional progression value.

**Adverse precedent**  
Losing a test case may impose a binding rule that increases future costs or invalidates an efficient policy.

**Liability reserve**  
Every pending test case locks cash until resolved. The strategy cannot scale without capital.

### 6.4 The intentional Test Case operation

The player can mark an ambiguous claim as a Test Case before adjudication.

Requirements:

- confidence range crosses the ruling threshold;
- a Legal preparation slot;
- a liability reserve;
- a selected question for the appellate body to answer.

Example:

```text
QUESTION FOR REVIEW
Does a workplace injury remain compensable when the injured limb
did not legally exist until after the incident?
```

This creates an authored risk. It is different from simply running a negligent denial mill.

## 7. Department model and cut order

Departments are both transformation stations and organisational units. A department owns queues, employees, equipment, operating policy and failure modes.

### Wave One: minimum viable spine

#### Intake

Transforms an arrival into a routable claim.

Operations:

- establish identity;
- identify claim family;
- register requested remedy;
- calculate initial deadline;
- detect missing mandatory fields;
- assign preliminary priority.

Typical bottleneck: misclassification sends work into the wrong verification recipe.

#### Verification

Turns submitted material into a confidence range.

Operations:

- request and compare records;
- authenticate evidence;
- detect contradictions;
- estimate fraud risk;
- expose the discoverability ceiling;
- recommend additional checks.

Typical bottleneck: expensive checks applied indiscriminately destroy throughput.

#### Adjudication

Commits the branch to an official outcome.

Operations:

- apply policy and precedent;
- compare confidence with decision thresholds;
- approve, deny, hold or escalate;
- create payment or appeal exposure;
- record a complete decision trace.

Typical bottleneck: a fast policy makes internally consistent but systematically wrong decisions.

### Wave Two: expansion departments

#### Archive

Stores evidence and policy versions, improves reuse, and creates risks around capacity, retrieval and obsolete records.

#### Legal

Processes appeals, prepares test cases, estimates liability and maintains precedent scope.

#### Payroll

Schedules staff availability, pays wages, manages breaks, cross-training and overtime, and acts as the branch's power system.

Anomalous equipment belongs inside these six departments. There is no separate Containment department in the initial design.

## 8. Staff as infrastructure

Employees are production components with predictable rights and availability.

Each employee has:

```text
Role
Skill by operation
Processing speed
Error contribution
Shift schedule
Break requirement
Wage
Cross-training
Workload stress
Rights or contractual constraint
```

Breaks are scheduled, visible and optimisable. The player can stagger breaks, cross-train workers, add buffer capacity or accept downtime. Staff do not randomly abandon a station for a joke.

Policy-driven comedy comes from correct execution:

```text
POLICY: Protect all company property from removal.

The evidence copier has been classified as company property.
Security refuses to let maintenance replace it.
Verification throughput collapses exactly as instructed.
```

## 9. Automation and policy language

### 9.1 Early automation

The first automation is physical and legible:

- employee assigned to station;
- input tray;
- operation recipe;
- output tray;
- visible queue;
- pneumatic route.

The player should understand the branch before programming it.

### 9.2 Late policy editor

The policy editor arrives only after the player has personally experienced the decisions it automates.

```text
ON claim enters Adjudication
IF identity confidence ≥ 90%
AND eligibility confidence minimum ≥ 78%
AND fraud risk < 20%
THEN approve
ELSE route to Senior Review
```

Policies read claim properties, department state, current precedent and branch resources. They cannot access hidden ground truth.

Every automated decision stores the exact policy version that produced it, allowing appeals to trace responsibility after the player has edited the rule.

## 10. Economy

### 10.1 Income

- service credit for completed valid work;
- Head Office performance payments;
- correct fraud prevention;
- sustained certification bonuses;
- carefully bounded efficiency bonuses.

### 10.2 Costs

- wages and overtime;
- workstation operation and maintenance;
- claim payouts;
- incorrect payouts;
- evidence requests;
- delay compensation;
- Legal processing;
- liability reserves;
- audits, sanctions and reprocessing.

### 10.3 Core operational metrics

```text
Throughput
Final upheld-decision rate
Avoidable error rate
Average claim age
Backlog
Appeal arrival rate
Legal clearance rate
Statutory breach rate
Cost per final claim
Payroll coverage
Liability exposure
Manual intervention rate
```

Head Office gates always combine several metrics. No campaign tier unlocks from raw volume alone.

Example:

```text
HOUSING DIVISION CERTIFICATION

Complete 40 final claims per day
Maintain ≥92% upheld decisions
Keep statutory breaches below 3%
Keep cost per final claim below £48
Sustain for five consecutive days
```

## 11. Pressure and failure curves

### 11.1 Backlog

Backlog is not an instant game-over meter.

As average claim age rises:

1. claimants make enquiries, consuming Intake capacity;
2. evidence becomes stale or more expensive to retrieve;
3. statutory deadlines approach;
4. staff face interruption pressure;
5. Head Office adds supervision requirements;
6. missed deadlines create automatic compensation and appeals.

The curve is nonlinear but recoverable. A player can suspend intake, buy temporary capacity, simplify policy or accept a controlled batch of provisional approvals.

### 11.2 Appeals congestion

Appeals have their own queue and deadlines. When Legal input exceeds clearance:

- reserves remain locked;
- compensation grows;
- related claims are flagged;
- missed hearings become automatic reversals;
- adverse outcomes may force mass reprocessing.

### 11.3 Branch intervention

Failure escalates through visible stages:

```text
Advisory
→ Mandatory Improvement Plan
→ Head Office Supervisor Installed
→ Intake Restricted
→ Branch Administration
→ Closure
```

The player receives opportunities to redesign before losing the campaign.

## 12. First 90 minutes

The manual opening must teach the production language without pretending paperwork itself is the long-term attraction.

### 12.1 Binding principles

Modelled directly on Satisfactory's opening, which works for three reasons that must **all** be reproduced. Any one missing and the manual phase reads as tedium rather than setup.

**1. The manual phase is bounded and the end is visible.**
The player is never grinding into fog. A counter is on screen from minute zero showing exactly how much manual work remains before the first unlock.

**2. The first machine removes the exact tedium the player just felt.**
Not a related convenience. The specific repeated physical action performed with their own hands, eliminated.

**3. Every operation is performed manually before it is automated.**
The manual phase is teaching disguised as labour. When a station is later placed, the player already knows what it does, because they did its job.

### 12.2 The requisition board

The Head Office board is this game's HUB terminal. Mounted on the wall in minute zero, always visible, showing exactly two things:

```text
CLAIMS PROCESSED TODAY        0 / 6
NEXT REQUISITION              FILE RUNNER
```

Progression is gated on completed claims. No other progression currency exists in the first ninety minutes.

### 12.3 Beat sheet

```text
0:00–0:02   ARRIVAL
            Empty desk. A pneumatic tube coughs out one dossier.
            A moth in a cheap suit is already standing in front of you.
            Board reads 0/6.

0:02–0:05   CLAIM 1 — FULLY MANUAL
            Read the form            → later: INTAKE
            Walk to the cabinet      → later: the FILE RUNNER, then ARCHIVE
            Find the drawer, pull the file
            Compare form to record   → later: VERIFICATION
            Spot the contradiction
            Stamp DENY               → ADJUDICATION. Yours for a long time.
            Drop in out-tray

            Five manual actions. Five stations the player will later place,
            each now understood from the inside.

0:05–0:10   CLAIMS 2–6
            Identical loop. The walk to the cabinet becomes tedious fast.
            This is the intended experience.
            Board ticks: 3/6 ... 4/6 ... 5/6

0:10–0:12   QUOTA MET — 6/6
            REQUISITION APPROVED — FILE RUNNER
            Place it. A moth takes station by the cabinets. Flag a file;
            it is carried to the desk. The player never walks again.
            ← guardrail 7 satisfied. MUST LAND BY MINUTE 12.

0:12–0:25   DAY 2 — QUOTA 10 — INTAKE STATION
            Claimants are received and classified without the player present.
            A deliberately misconfigured route demonstrates that automation
            executes exactly what was specified, not what was intended.

0:25–0:40   DAY 3 — QUOTA 16 — VERIFICATION STATION
            The player chooses which checks it performs.
            Cheap checks are fast; thorough checks create a queue.
            First combined objective: throughput AND upheld decisions.

0:40–0:55   DAY 4 — QUOTA 24 — JUNIOR ADJUDICATOR
            Auto-approves high-confidence claims only. Exceptions still
            come to the desk. First real build choice:
              narrow bands  → more manual exceptions
              wider bands   → more audit exposure
              auto-hold     → protects accuracy, creates backlog

0:55–1:10   THE CEILING
            A claim reaches its DiscoverabilityCeiling below the threshold.
            The interface explains why further verification cannot resolve it
            before deadline. Approve / deny / hold / escalate, with reserve,
            queue and exposure consequences shown before committing.

1:10–1:25   FIRST APPEAL — IT IS CLAIM 1
            See 12.4.

1:25–1:30   First three-day Head Office assessment.
```

At approximately 90 minutes, Archive and Legal become the next expansion choices. Payroll follows once the branch has enough employees for scheduling to be a real design problem.

### 12.4 Claim 1 must be the first appeal

The dossier returning at minute 70 is the first one the player ever ruled on. This is a deliberate commitment, not a coincidence of pacing. It arrives bearing their own stamp.

It must not read as a trick. The contradiction spotted at minute 3 is **genuine, and DENY is correct on the evidence available.** The record showed the claimant was not at work on the day of the injury. What surfaces during the appeal is that the attendance record was filed under a superseded employee number.

**The player ruled correctly on the information they had, and was still wrong.**

This introduces §2's irreducible accuracy floor as a **story beat rather than a percentage**, and it lands after the player has spent an hour building a pipeline on top of that first ruling. Legal is not yet a department; the player handles this case by hand as a preview of the second production stream.

The appeal identifies the exact policy and skipped operation that produced it, per §5.3.

### 12.5 What is never automated in the opening

The junior adjudicator at 0:40 handles **high-confidence claims only**. Every exception still reaches the desk.

> The player stops stamping the boring ones. They never stop stamping the hard ones.

Exception handling remains manual for the entire campaign. Automating it is Tier 6 and it is the ending (§15). If the player's hands leave the stamp in the first ninety minutes, Autonomous Certification has nothing left to prove.

### 12.6 Numbers

```text
DAY   QUOTA   NEW CAPABILITY
1     6       File Runner
2     10      Intake Station
3     16      Verification Station
4     24      Junior Adjudicator
5     40      first sustained Head Office assessment
```

The 40/day figure in §10.3 is a **Tier 3 number.** It must not appear before day five. Day one is over in ten minutes.

Note: the File Runner is a *labour unit*, not the Archive department. The cabinet at minute 3 is furniture. Archive proper (§7, Wave Two) adds storage policy, capacity, retrieval indexing and record obsolescence.

### 12.7 Test conditions

The opening works if, in unassisted playtest:

1. A first-time player completes claim 1 in under three minutes without help.
2. The File Runner is placed by minute 12.
3. An audible or written reaction accompanies its first delivery.
4. Zero players quit during the manual phase.
5. When the first appeal arrives, the player can explain *why it came back* without reading a tooltip.
6. At least one player admits, unprompted, to having ruled without checking properly during day 3 or 4.

**Condition 6 is the most important.** If nobody cuts corners under quota pressure, the central tension of §9.2 does not exist and the opening has failed regardless of how smoothly it played.

### 12.8 Known risk

The first five minutes are literally doing paperwork. The reference lane's most-loved title has reviews stating its desk work is as dull as the real job. That risk is mitigated by exactly three things: the bounded counter, the ten-minute duration, and the runner landing at minute twelve.

If playtest shows players bouncing before minute twelve, **shorten the manual phase — do not make it more elaborate.**

## 13. Build archetypes produced by the same machine

These are emergent configurations, not character classes.

### Conservative Proof Office

Heavy Verification, narrow automated decision bands and strong escalation. High upheld rate, high cost, deadline vulnerability.

### Rubber-Stamp Mill

Wide automatic decision bands, minimal checks and large buffers. Excellent initial throughput, severe delayed audit and appeal pressure.

### Appeals Refinery

Legal-heavy branch that prepares ambiguous test cases and converts selected uncertainty into precedent. Capital intensive, hearing constrained and vulnerable to adverse rulings.

### Archive Oracle

Expensive evidence reuse and precedent matching reduce repeated verification. Powerful on familiar claim families, fragile when records become obsolete or storage routing fails.

### Provisional Welfare Engine

Pays uncertain claims immediately, then verifies afterward. Prevents delay penalties and claimant appeals but requires immense reserves and an effective recovery department.

The same claim should produce materially different physical routes under each build.

## 14. Campaign progression

Progression subdivides and then abstracts the player's former job:

```text
Tier 0 — Personal desk
Tier 1 — Division of labour
Tier 2 — Physical routing and buffers
Tier 3 — Quality control and appeals
Tier 4 — Policy programming
Tier 5 — Anomalous modules and complex precedent
Tier 6 — Autonomous branch certification
```

Unlocks primarily add operations, routing capabilities, policy concepts, evidence sources and labour structures. They should not primarily grant flat percentage bonuses.

## 15. Ending: Autonomous Operation Certification

The final objective is not a narrative button.

The player applies for Branch Autonomy Certification and selects the operating configuration to be tested. For five simulated days:

- manual adjudication is disabled;
- manual routing changes are disabled;
- emergency cash injections are disabled;
- policies, staff schedules and maintenance plans continue operating;
- appeals and anomalies continue arriving normally.

The branch must satisfy:

- throughput;
- upheld-decision rate;
- deadline compliance;
- solvency;
- appeal clearance;
- maximum manual-exception count of zero.

If the branch fails, the causal report becomes the player's redesign brief. If it succeeds, the camera returns to Desk 42. The desk is vacant while the institution continues processing claims.

The game ends because the player has built a machine that no longer needs them.

## 16. Design guardrails

1. A claim is never only a coloured unit.
2. Every bad outcome identifies its causal chain.
3. Perfect information is impossible; arbitrary information loss is forbidden.
4. Appeals remain economically painful even when strategically useful.
5. Precedent is a scoped rule, not generic experience currency.
6. Staff failure follows schedules, policy and capacity—not random comedy.
7. The player receives the first useful automation within ten minutes.
8. Every new department removes an old manual burden and creates a new systems problem.
9. Volume alone never satisfies Head Office.
10. Anomalies alter production logic; they do not become a separate action minigame.
11. The simulation remains playable without narrative exposition.
12. The ending tests the machine under the same rules used throughout the game.

## 17. Falsifiable design tests

The direction should be rejected or revised if paper simulation and later prototypes show any of the following:

- Players can approve or deny nearly everything without creating a different bottleneck.
- Appeals feel like random punishment rather than traceable output.
- The strongest Appeals Refinery intentionally makes obviously incorrect rulings.
- Perfect accuracy is cheaper than maintaining Legal and exception capacity.
- Manual intervention remains the fastest answer after an operation is automated.
- Claims become visually interchangeable boxes with no meaningful history.
- Staff breaks produce surprise downtime the player could not predict.
- The first automation arrives too late to communicate the game's promise.
- Autonomous Certification tests different rules from the campaign.

## 18. Decisions intentionally left open

The following require balancing work, not further premise exploration:

- exact confidence thresholds;
- exact proportion of residually uncertain claims;
- day length and simulation speed;
- maximum Legal hearing capacity;
- size and duration of liability reserves;
- whether provisional payment is a universal operation or a specialist unlock;
- how many simultaneous precedents may be active;
- when policy versions may be retired;
- campaign length and sandbox continuation.

The core design decision is closed: **Desk 42 is a factory whose mistakes return, demand representation and sometimes rewrite the factory.**
