# Desk42 — Production Vertical Slice v0.5

Status: validated implementation checkpoint; commissioned production art and FMOD
remain external production dependencies

Baseline: `d9a1b7252e642a0e62bce575d07cd32c2c40a9a2`

Branch: `codex/production-vertical-slice-v0.5`

## Product statement

Desk42 is a persistent institutional automation simulator. Autonomous people create
material disputes; public evidence enters a physical claims facility; the player's
doctrine, machinery and procedures produce rulings; remedies, appeals and holdings
alter the same society that supplies later work.

v0.5 keeps the complete v0.4 factory and moves it toward a commercially legible
vertical slice. It does not replace the automation floor or create another proof UI.

## Active case families

Five families now share one material-event, observation, docket, factory, ruling,
remedy, scope and appellate path:

| Family | Distinct public work | Executable institutional result |
| --- | --- | --- |
| Possession dispute | ownership, transfer and custody records | restore possession |
| Access withdrawal | authority and access records | restore access |
| Collective grievance | linked parties and shared evidence | recognise collective standing |
| Identity continuity | official record, custody and biometric continuity | restore identity continuity |
| Dependency emergency support | witness, custody and dependency proof; urgent by default | grant emergency support |

Identity and dependency are not authored minigames. The issue family is carried by
the material resource through the generic autonomous-action pipeline.

## Doctrine build space

The three v0.4 doctrines remain binding for an eight-shift run. v0.5 adds:

### Provisional Welfare Office

- urgent ambiguous claims can be provisionally recognised;
- the corresponding material remedy executes immediately;
- each relief consumes a finite branch reserve;
- relief creates visible reliance exposure;
- verification and Legal load remain coupled to the physical floor.

The archived manual causal-legibility interface still offers only Recognise and Deny.
The provisional vocabulary is automation-specific, preserving the old proof contract.

## Procedure pool

The two-slot limit remains. The pool now contains thirteen procedures:

1. Mandatory Secondary Verification
2. Presumption of Validity
3. Automatic Adverse Review
4. Protected Evidence Channel
5. Appeal Fast Track
6. Precedent Reuse
7. Burden Shift
8. Anonymous Disclosure
9. Emergency Relief
10. Employer Self-Certification
11. Independent Verification
12. Narrow Precedent
13. Retrospective Review

The additions alter at least two connected systems. Examples:

- Employer Self-Certification increases throughput while increasing faults and Legal
  pressure.
- Independent Verification adds a physical second pass, reduces fault probability and
  increases heat and queue load.
- Anonymous Disclosure reserves auxiliary capacity for witness work while reducing
  verification time and increasing Legal review pressure.
- Emergency Relief converts qualifying urgency into provisional material relief and
  reliance exposure.
- Narrow Precedent reduces descendant exposure but sacrifices broad reuse.
- Retrospective Review returns weak completed rulings through the Legal loop.

## Readability and presentation

- five stable dossier colours and physical glyphs;
- six evidence-need markers, including biometric continuity and dependency proof;
- industrial station silhouettes with input/output trays, terminals and feed rollers;
- denser brutalist room, overhead rails, fluorescent housings, pneumatic archive line
  and hazard zoning;
- clear Welfare reserve/reliance readout;
- shift-specific current directives that teach through factory problems;
- holding, relief, precedent, jam, repair and retrospective-return feedback.

The generated environment target for this cycle establishes the intended visual bar:
dense isometric machinery, readable station silhouettes, worn green-grey institutional
materials and restrained amber/cyan status lighting. It is art direction, not a runtime
asset or a claim of final production art.

## Operational audio

The project does not currently contain the FMOD package, so v0.5 does not pretend to
ship an FMOD integration. It implements the same parameter surface with deterministic
Unity audio layers:

- fluorescent hum;
- ventilation;
- machine rhythm driven by backlog and heat;
- queue/Legal pressure driven by pending appeals;
- shift-progression pitch;
- event cues for the complete operational feedback vocabulary.

`SetOperationalState(backlog, machineHeat, appealPressure, shiftOrdinal)` is the seam
for a future licensed FMOD driver. No narrative or character motifs are generated.

## Onboarding cadence

- doctrine selection explains the binding eight-shift commitment;
- Shift 1 directs the player to follow a dossier, identify the first bottleneck, build
  the auxiliary verifier and choose a machine trade-off;
- Shift 2 foregrounds urgency and priority routing;
- later directives foreground procedure routing, recurring society work and holdings.

## Balance harness

A deterministic pre-playtest forecast covers the requested matrix:

```text
4 doctrines × 3 representative procedure builds × 10 seeds = 120 forecasts
```

It writes `tmp/v0.5/balance-forecast-4x3x10.csv` and guards against a dead
representative build. This is explicitly a tuning forecast, not human telemetry and
not proof that the live 96-ruling runtime is commercially balanced.

## Validation added in this cycle

- identity and dependency emerge from the same persistent docket;
- identity continuity executes a material remedy;
- provisional dependency relief executes and records its procedure;
- all seven new procedure identifiers survive the institutional ruling boundary;
- the Welfare doctrine spends relief reserve and increases reliance exposure in
  PlayMode;
- the long eight-shift product test now requires collective, identity and dependency
  work to complete on the shared floor.

Final local validation on Unity 2022.3.62f3:

```text
Standard EditMode:        417 / 417 passed
Long-run EditMode:          1 / 1 passed
Active-product PlayMode:   10 / 10 passed
Eight-shift PlayMode:       1 / 1 passed
Windows x64 build:          passed
Built-player smoke/capture: passed (1600 x 900)
```

The balance forecast produced 120 rows across all four doctrines, three
representative procedure builds and ten seeds. Its derived outcomes were 56
Certified, 36 Efficient but Harmful, 24 Administrative Blindness and 4 Precedent
Pressure. These are deterministic forecast outputs, not player-retention evidence.

## Remaining production gaps

- commissioned modular character and environment asset kits;
- licensed FMOD package and authored production sound library;
- hand-authored recurring-case musical motifs;
- human onboarding/comprehension telemetry;
- live-runtime balance telemetry across the full 4 × 3 × 10 matrix;
- external-hardware review beyond the validated local Windows build and capture.

The architecture is no longer the work target. The remaining work is content quality,
presentation quality, balance evidence and player response.
