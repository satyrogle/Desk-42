# Desk 42 — Legal Reality Exploration

Status: candidate concept, not selected  
Candidate direction: Claim Dive / Option 3  
Working proposition: **Enter impossible claims, return with incomplete evidence, rule on what officially happened, and make every future claim obey the record you authored.**

This document tests one possible direction. Its architecture, loadout, arena,
management economy, and Reality Debt model are proposals—not approved production
decisions. See [DESK42_DIRECTION_EXPLORATION.md](DESK42_DIRECTION_EXPLORATION.md)
for the complete open exploration.

## 1. The ruling is the spine

Combat is not the game’s final truth. Combat is how the player obtains an incomplete, compromised account of an incident.

The run loop is:

1. Enter a claim.
2. Encounter a physical version of the incident.
3. Manipulate its legal state to survive and investigate.
4. Extract before the claim collapses.
5. Review partial evidence at Desk 42.
6. File an official ruling that may be accurate, expedient, self-serving, or knowingly false.
7. Add that ruling to the company’s precedent ledger.
8. Generate future claims under the accumulated official record.
9. Live inside the contradictions the player created.

The persistent build is not primarily `+5 damage`. It is the body of precedent the player has legislated into reality.

## 2. Three truths must remain separate

The model must never collapse “what is physically true,” “what the player can prove,” and “what the company officially recognises” into one value.

### 2.1 Physical truth

What the entity actually is in the current claim.

Examples:

- a photocopier is sentient and hostile;
- a wall is physically solid;
- a moth is a sapient person;
- a projectile was fired by Security;
- two employees are manifestations of one underlying entity.

Physical truth drives the entity’s native body, intent, movement, health, collision, and authored anomaly behaviour.

### 2.2 Evidentiary truth

What the player has observed and successfully extracted.

Evidence is not the physical truth itself. It is a claim about that truth:

```text
subject + predicate + value + source + confidence + authenticity
```

Examples:

- `Photocopier_04 / ExhibitedIntent / true / witness_audio / 0.82 / authentic`
- `Moth_12 / EmploymentStartDate / tomorrow / HR_record / 1.00 / forged`
- `Projectile_77 / FiredBy / Security_02 / trajectory_scan / 0.91 / authentic`

Uncollected evidence cannot justify a ruling. Collected evidence may still be incomplete, forged, contaminated, or contradictory.

### 2.3 Official truth

What the company has entered into the record and what rule-governed systems must therefore obey.

Examples:

- all photocopiers are classified as harmless company equipment;
- moths are equipment rather than persons;
- Security is not liable for projectiles fired during an authorised audit;
- duplicate employees are entitled to one shared salary;
- redacted architecture does not legally occupy floor space.

Official truth is persistent. It seeds future claim arenas and changes how systems interpret entities.

### 2.4 Contradiction is content

The fun lives in the distance between these layers.

A photocopier can be:

- physically hostile;
- evidentially suspicious but not conclusively sentient;
- officially harmless company equipment.

That contradiction creates play. The machine attacks, but Security protects it. The player cannot damage it without committing destruction of company property. The player must find a legal transformation, provoke non-compliance, transfer ownership, assign liability, or accept the offence.

## 3. Runtime legal state

Every arena entity that can participate in rule interactions receives a legal-state component.

The legal state contains only declared facts. It does not contain derived combat outcomes.

```text
LegalState
├── EntityId
├── EntityClass
├── OwnerId
├── EmploymentStatus
├── ComplianceStatus
├── HazardDesignation
├── IdentityStatus
├── IdentityRelationId
├── LiabilityPartyId
├── Jurisdiction
├── AuthorityLevel
└── AppliedAmendments
```

### 3.1 Entity class

What category the official record assigns to the entity:

- Person
- Equipment
- Structure
- Document
- Evidence
- Projectile
- Hazard
- Anomaly
- Currency
- Unclassified

Employment is deliberately not an entity class. A sentient photocopier can remain Equipment while also becoming an Active Employee. That contradiction is valuable.

### 3.2 Ownership

The entity or faction legally responsible for possession:

- Company
- Claimant
- Security
- Legal
- Containment
- Union
- Player
- Unowned
- another arena entity

Ownership is relational. It affects protection, theft, authorised use, damage permissions, insurance, and liability.

### 3.3 Employment status

- NotEmployed
- Applicant
- Active
- OnBreak
- Suspended
- Terminated

Employment derives obligations and permissions. It must affect more than allegiance:

- wages;
- break entitlement;
- door access;
- Security protection;
- union eligibility;
- workplace-injury coverage;
- performance review;
- ability to file grievances.

### 3.4 Compliance status

- Unknown
- Compliant
- Exempt
- Suspected
- Violating
- Quarantined

Compliance is the primary interface with enforcement systems. A Company-owned object can still become a Security target when Violating. An Exempt anomaly may ignore regulations that would normally affect it.

### 3.5 Hazard designation

- Unassessed
- Harmless
- Controlled
- Dangerous
- Catastrophic

Hazard designation affects evacuation, containment, weapon authorisation, insurance value, and which departments become responsible.

### 3.6 Identity status

- Original
- Duplicate
- Counterfeit
- Unregistered
- Redacted

`IdentityRelationId` points to the entity or record with which the identity is linked.

Identity is relational because “Duplicate” is meaningless without an alleged original.

### 3.7 Liability

`LiabilityPartyId` identifies who officially receives responsibility for consequences produced by the entity.

Liability can redirect:

- damage attribution;
- retaliation;
- fines;
- Security hostility;
- compensation;
- future claim ownership;
- death or injury statistics.

It does not automatically redirect physical force. A regulation must specify the consequences of assigned liability.

### 3.8 Jurisdiction and authority

Every amendment has:

- the jurisdiction in which it is valid;
- the authority that issued it;
- an authority level;
- a scope;
- a duration.

This prevents one cheap stamp from solving the entire game.

A temporary field stamp may alter one entity for the current claim. A Desk 42 ruling may alter a whole entity family across future claims. A Director-level precedent may supersede ordinary rulings.

## 4. Derived reality

Combat, navigation, AI, economy, and simulation systems do not read stamp names. They query resolved legal capabilities.

Examples:

```text
CanPlayerDamage(entity)
CanSecurityTarget(entity)
SecurityDispositionToward(entity)
CanEnterDoor(entity, door)
ReceivesWages(entity)
IsUnionEligible(entity)
CountsAsEvidence(entity)
CountsAsContraband(entity)
CollisionIsEnforced(entity)
OfficiallyExists(entity)
ResolveDamageRecipient(source, target)
ResolvePenaltyRecipient(action)
```

These answers come from a deterministic resolution table.

No downstream system should contain logic such as:

```text
if stamp == EMPLOYEE then friendly = true
```

It should ask:

```text
SecurityDispositionToward(entity)
```

The legal resolver explains which rules produced the answer.

## 5. The six slice stamps

The first slice uses six stamps only.

Each stamp must alter at least two independent systems. If it behaves like a renamed spell, it fails the design.

### 5.1 COMPANY PROPERTY

Amendment:

- set `OwnerId = Company`;
- optionally set `EntityClass = Equipment` when supported by the ruling.

Possible consequences:

- Security protects it;
- player damage becomes destruction of company property;
- repairs consume company maintenance budget;
- the object can travel through company logistics;
- Legal becomes liable for unsafe operation;
- the object can be repossessed or insured.

### 5.2 EMPLOYEE

Amendment:

- set `EmploymentStatus = Active`;
- preserve the existing entity class.

Possible consequences:

- gains employee-only access;
- receives Security protection;
- earns wages;
- takes scheduled breaks;
- becomes union-eligible;
- can file grievances;
- injuries become workplace claims;
- poor performance can trigger termination.

The monster in the break room with a grievance is not a joke layered over the system. It is the system producing the joke.

### 5.3 NON-COMPLIANT

Amendment:

- set `ComplianceStatus = Violating`.

Possible consequences:

- Security may target it even when Company-owned;
- doors deny access;
- containment becomes authorised;
- its owner receives a fine;
- interacting with it may contaminate the player’s compliance;
- destroying it may become officially permitted.

### 5.4 REDACTED

Amendment:

- set `IdentityStatus = Redacted`.

Possible consequences:

- official targeting systems cannot perceive it;
- it cannot be submitted as evidence;
- its name and ownership disappear from records;
- legal collision may cease to be enforced;
- pathfinding may route through redacted architecture;
- consequences caused by it become unattributed.

Physical remnants may remain. A redacted wall can disappear for official navigation while leaving a visible, unstable outline.

### 5.5 DUPLICATE

Relational amendment:

- set `IdentityStatus = Duplicate`;
- set `IdentityRelationId = selectedOriginalId`.

Possible consequences depend on the active duplicate regulation:

- only one instance may receive damage;
- benefits and penalties may be shared;
- only one salary may be paid;
- one entity may be removed from the official world;
- evidence attached to one may contaminate the other;
- destroying the registered original may promote the duplicate.

The stamp requires two entities: the alleged duplicate and the alleged original.

### 5.6 LIABLE

Relational amendment:

- set `LiabilityPartyId = selectedPartyId`.

Possible consequences:

- damage attribution follows the liable party;
- Security retaliation targets the liable party;
- fines and compensation move to the liable party;
- the party’s faction relationship changes;
- later claims are generated against that party;
- a boss can be made legally responsible for its own hazards.

The stamp requires an action-producing entity and a selected liable party.

## 6. Resolution table

Rules are data-driven, prioritised, deterministic, and explainable.

Each rule contains:

```text
RuleId
Priority
Jurisdiction
Condition
DerivedEffect
ReasonText
SourcePrecedentId
```

Example rows:

| Priority | Condition | Derived effect | Explanation |
|---:|---|---|---|
| 1000 | Identity is Redacted | OfficiallyExists = false | “The record contains no recognised entity.” |
| 800 | Compliance is Violating | SecurityDisposition = Hostile | “Protocol 7-B authorises enforcement.” |
| 600 | Owner is Company | SecurityDisposition = Protected | “Company property must be preserved.” |
| 550 | Employment is Active | EmployeeDoorAccess = true | “Active employees possess standard clearance.” |
| 500 | Employment is Active | ReceivesWages = true | “Payroll recognises an active employment record.” |
| 450 | Employment is Active | UnionEligible = true | “All active employees may petition for representation.” |
| 400 | Hazard is Dangerous | ContainmentAuthorised = true | “Dangerous entities fall under Containment jurisdiction.” |

Higher-priority rules win when two rules assign incompatible values.

Compatible effects accumulate.

Every resolved effect returns an explanation chain suitable for the player-facing cascade:

```text
SECURITY TARGET: PHOTOCOPIER_04

Protected as Company Property                +600
Employee protection                         +550
Non-Compliant under Protocol 7-B             +800

RESULT: ENFORCEMENT AUTHORISED
```

The rule resolver contains no random rolls. Uncertainty belongs in evidence, behaviour, and procedural generation—not in the interpretation of already-declared law.

## 7. Stamp transaction

All legal changes pass through one transaction format.

```text
LegalAmendment
├── AmendmentId
├── StampType
├── TargetEntityId
├── RelatedEntityId (optional)
├── IssuingAuthority
├── AuthorityLevel
├── Jurisdiction
├── Scope
├── EvidenceIds
├── IssuedAtSequence
├── Duration
└── SourceClaimId
```

The transaction pipeline is:

1. Validate authority.
2. Validate target and required relation.
3. Validate jurisdiction.
4. Record supporting evidence or the absence of it.
5. Apply the declared state change.
6. Re-resolve derived legal capabilities.
7. Publish a legal-state-changed event.
8. Let combat, AI, physics, security, economy, and UI react.
9. Preserve a complete explanation log.

Derived values are never directly saved or mutated.

## 8. Arena and ruling use the same language

The arena and the desk must not become two unrelated games.

### Arena stamps

- temporary;
- narrow scope;
- low authority;
- immediate tactical consequence;
- usually affect one entity or relationship;
- expire on extraction unless confirmed in the ruling.

### Desk rulings

- persistent;
- broader scope;
- higher authority;
- require selected evidence or a knowingly unsupported declaration;
- can apply to an entity family, department, incident class, or future regulation;
- enter the precedent ledger.

The same legal properties and rule resolver process both.

## 9. Precedent ledger: the real build

The persistent save gains a `RulingLedger`.

Each ruling contains:

```text
RulingRecord
├── RulingId
├── SourceClaimId
├── SubjectSelector
├── OfficialAmendments
├── SupportingEvidenceIds
├── ContradictedEvidenceIds
├── AuthorityLevel
├── PrecedentPriority
├── Scope
├── ShiftFiled
├── IntegrityStatus
└── PrecedentStatus
```

`SubjectSelector` defines what future entities inherit the ruling:

- exact entity;
- entity family;
- claim template;
- department;
- anomaly tag;
- incident type;
- global company policy.

Future claim generation performs:

1. Create physical incident truth.
2. Create base legal state.
3. Query applicable precedents.
4. Apply precedents in authority and priority order.
5. Resolve official capabilities.
6. Generate contradictions between physical and official truth.
7. Place evidence capable of exposing, exploiting, or reinforcing those contradictions.

The ledger grows permanently, but the ledger is not the active build.

### 9.1 A precedent has force only when cited

A ruling changes the official history of its original case immediately. It becomes a general rule for the current run only when the player cites it as a binding precedent.

This preserves both promises:

- the world remembers everything the player officially filed;
- only a small, readable set of general rules governs any one run.

The constraint is diegetic rather than arbitrary. Case law sitting in an archive has no operational force until an authorised party cites it.

### 9.2 Run loadout

Each run begins with:

- **two carried stamps**, selected before deployment;
- **up to four field stamps**, acquired from choices discovered during the dive;
- **three binding precedents**, drafted from an offer of approximately eight;
- **one departmental authority**, defining the player’s authorised and forbidden verbs;
- **the anomalies rostered for the shift**, excluding anyone unavailable, suspended, unpaid, or on break.

The precedent offer is not selected from the entire permanent ledger.

It is shaped by:

- the current claim family;
- the selected department;
- precedent status;
- surviving evidence;
- active appeals;
- rostered anomalies;
- the player’s existing contradictions;
- seeded run generation.

The limited offer creates adaptation. The player cannot reproduce the same doctrine every run merely because the ledger contains it.

The two carried stamps provide intent. Field discoveries provide surprise. Field stamps should be chosen from small offers rather than awarded in a fixed order.

### 9.3 Synergy is the cross-product

Stamps modify legal properties. Binding precedents define what those properties do.

This creates the build space:

```text
stamp transformation × cited precedent × departmental authority × arena state
```

Example:

1. Stamp an attacking entity `COMPANY PROPERTY`.
2. Its owner becomes the Bureau.
3. Stamp the entity’s attacks `LIABLE` to the legal owner.
4. Cite: “Bureau assets may not be damaged on Bureau premises.”
5. Damage caused by the entity routes to the Bureau.
6. The Bureau cannot legally receive that damage under the cited precedent.
7. The attack becomes legally void.

Two stamps and one citation disarm an enemy through paperwork.

The player can understand the combination because `owner`, `damage`, and `company property` retain their ordinary meanings.

### 9.4 Contradictory citations generate Reality Debt

The resolver does not reject two binding precedents merely because they disagree.

Both remain cited. Their unresolved contradiction produces Reality Debt.

Contradictory doctrines should often create the strongest combinations. Power and instability therefore share one axis.

### 9.5 Precedents are never deleted from history

`PrecedentStatus` can be:

- BindingEligible
- UnderAppeal
- Overruled
- Sealed
- Illicit

An unsuccessful appeal does not erase a ruling from the permanent ledger. It changes its legal status.

An Overruled precedent cannot appear in an ordinary draft, but it can later become an illicit citation that produces exceptional power and immediate Reality Debt. This preserves the forever-growing authored history and turns legal defeat into future contraband rather than lost content.

### 9.6 Departmental authorities replace archetypes

Existing archetypes are re-evaluated as departmental authorities.

An authority must:

- grant at least one unique legal verb;
- forbid or penalise another verb;
- shape precedent offers;
- affect jurisdiction or authority level;
- change how a familiar stamp can be used.

Authorities that only change hand size, payout, damage, or numerical efficiency do not survive unchanged.

Existing Compliance Vows are re-evaluated as self-imposed binding precedents. A Vow survives only when expressed through the same legal properties and resolution table as the rest of the game.

## 10. The management economy

Management means autonomous state changes while the player is attending to something else.

The Bureau has four operational pressures plus a live docket.

### 10.1 Payroll

Employees and employed anomalies cost money each cycle.

Missing payroll can cause them to:

- stop working;
- refuse deployment;
- take protected industrial action;
- file a grievance;
- seek union representation;
- attempt to reclassify themselves;
- become claimants in a future case.

Payroll should be shown through people and scheduled obligations, not only as a bar.

### 10.2 Evidence Archive

Evidence occupies limited physical capacity and degrades according to its medium.

Evidence supports:

- truthful rulings;
- precedent integrity;
- appeal defence;
- future investigations;
- proof that official and physical truth disagree.

A full archive forces the player to destroy, transfer, redact, or allow evidence to decay.

Destroyed evidence can make an existing ruling indefensible.

### 10.3 Liability Exposure

Liability Exposure is a derived risk, not a spendable currency.

It increases when:

- a ruling lacks surviving supporting evidence;
- preserved evidence contradicts the official ruling;
- an employed anomaly causes harm;
- mutually inconsistent precedents remain active;
- a department exceeds its authority;
- a claimant or faction gains standing.

Exposure determines the likelihood and severity of appeals, investigations, compensation demands, and internal audits.

### 10.4 Reality Debt

Reality Debt is persistent contradiction pressure.

It increases through unsupported rulings, contradictory cited precedents, illicit case law, and widening divergence between physical and official truth.

It is simultaneously:

- a build axis;
- the input to fugue;
- a management liability;
- the run’s escalation curve.

### 10.5 Docket mutation

Claims continue to age while the player is elsewhere.

An ignored claim can:

- expire into an automatic ruling;
- mutate into a more dangerous claim family;
- gain or lose evidence;
- accumulate additional claimants;
- be acquired by another department;
- return as an appeal;
- produce a rarer but more valuable anomaly.

Neglect must not function only as punishment. The player should sometimes allow a claim to mutate deliberately because the resulting risk, evidence, entity, or precedent opportunity is desirable.

Each management cycle therefore asks:

- Which claim do I enter?
- Which claim do I intentionally let mutate?
- Who gets paid?
- Who is rostered?
- What evidence do I preserve?
- Which ruling can I still defend?
- Which three precedents do I cite?
- How much Reality Debt am I willing to create?

## 11. The first two-run proof

### Run one

Physical incident:

- a photocopier is sentient;
- it creates hostile duplicate documents;
- it injured a moth employee;
- Company Security owns and protects it.

The player extracts:

- a damaged maintenance log;
- partial witness audio;
- one hostile duplicate;
- no conclusive proof of sentience.

At Desk 42, the player rules:

```text
Subject: photocopier family
Entity class: Equipment
Owner: Company
Hazard designation: Harmless
Employment status: NotEmployed
```

The player knowingly protects the company, meets quota, and avoids compensation.

### Run two

A later claim contains another photocopier.

Physical truth:

- it is hostile and hungry.

Official truth inherited from the ruling:

- it is harmless Company equipment.

Visible consequences:

- Security protects it from player attacks;
- weapon authorisation is denied;
- its attacks are recorded as routine maintenance;
- injured employees receive no hazard compensation;
- ordinary containment tools refuse to recognise it.

The player must:

- provoke and document non-compliance;
- transfer ownership;
- employ it and exploit mandatory break rules;
- assign its liability to Security;
- redact its protection record;
- or commit an offence by destroying protected property.

If the second run does not feel visibly and mechanically different, the system has failed.

## 12. Reality debt and fugue

This model offers a better role for fugue.

Every unsupported or contradicted ruling creates `RealityDebt`: the measured divergence between physical and official truth.

Reality Debt is not a standard damage meter. It is accumulated authorship pressure.

At high Reality Debt, a claim may enter a fugue jurisdiction where **official truth temporarily gains stronger authority than physical truth**.

Examples:

- an officially harmless monster becomes unable to attack—but its suppressed violence emerges through the environment;
- a redacted wall vanishes completely;
- duplicate employees share one body;
- assigned liability physically redirects attacks;
- an employed anomaly stops fighting to take its legally required break;
- payroll deductions remove literal mass from unpaid workers.

Fugue becomes a high-power, high-instability rule state produced by the player’s own precedent build. It should enable outrageous strategies while making contradictions dangerous.

Reality Debt is not successful merely because players understand it.

The falsifiable test is:

> Do players voluntarily increase Reality Debt to win a difficult claim, knowing it will create later consequences?

If players only avoid debt, it is a debuff with better fiction and must be redesigned or removed.

Debt manifestations must be forecast from authored contradictions. They cannot be an unrelated random-punishment table. Before a dive, the player should be able to inspect which cited precedents and past rulings are likely to become physically authoritative.

This is a candidate direction, not yet a locked rule. The slice only needs enough Reality Debt to demonstrate one authored contradiction becoming physical.

## 13. Architectural invariants

1. Physical, evidentiary, and official truth remain separate.
2. Every legal change uses a recorded amendment.
3. Arena stamps and persistent rulings use the same property vocabulary.
4. Resolution is deterministic and ordered.
5. Every derived effect has a human-readable explanation chain.
6. Downstream systems query capabilities; they do not inspect stamp names.
7. Relationship stamps require explicit related entities.
8. Authority and jurisdiction constrain every amendment.
9. Persistent rulings apply through selectors, not bespoke future-case scripts.
10. No stamp passes the slice unless it changes at least two independent systems.
11. Contradictions generate playable situations, not arbitrary punishment.
12. The ruling ledger is the primary build system.
13. The ledger grows permanently, but only cited precedents become generally binding in a run.
14. Overruled precedents remain part of history and may return as illicit doctrine.
15. Liability Exposure is derived from legal and evidentiary state.
16. Ignored claims can become opportunities as well as threats.

## 14. Slice acceptance tests

The foundational model is proven only when all of these work:

1. Company Property causes Security to protect an entity.
2. Non-Compliant overrides that protection through a higher-priority rule.
3. Employee grants access, wages, protection, breaks, and union eligibility.
4. Redacted changes targeting, evidence validity, and collision/pathfinding.
5. Duplicate creates a real identity relationship used by more than damage.
6. Liability changes attribution, retaliation, and persistent claim generation.
7. Every result produces a readable resolution explanation.
8. Saving and loading preserves the ruling ledger.
9. Applying the same inputs produces the same legal result.
10. A ruling from run one materially changes the second arena.
11. The player can knowingly file a ruling unsupported by collected evidence.
12. That lie creates a useful build advantage and a future problem.
13. A two-stamp and one-precedent combination changes an encounter without acting as direct damage.
14. Players can explain why a cited precedent changed a stamp’s effect.
15. Players voluntarily accept Reality Debt for power in at least some difficult claims.
16. An employed anomaly creates both a build benefit and an autonomous management obligation.
17. An ignored claim can be cultivated intentionally rather than merely lost.
18. An appeal changes precedent status without deleting authored history.

## 15. What not to build yet

- additional claim types;
- facility management;
- office exploration;
- authorities beyond the one required for the slice;
- additional self-imposed precedents;
- leaderboards;
- multiple endings;
- a full fugue mode;
- dozens of stamps;
- meta currencies;
- generic damage upgrades.

First prove:

> Stamp a wall REDACTED. Watch it stop officially existing. File a false ruling. Enter the next claim and discover that the world has remembered the lie.
