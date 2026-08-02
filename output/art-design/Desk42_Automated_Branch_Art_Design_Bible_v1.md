# Desk 42: Automated Branch
## Art Design Bible and Comfy ArtLab Production Manual

Version: 1.0  
Date: 2026-08-02  
Audience: solo developer, contract artist, animator, UI designer and audio collaborator  
Design source: `DESK42_CLAIM_APPEAL_ECONOMY_SPEC.md`  
Core pitch: **Factorio where defective products hire lawyers.**

---

## 1. The visual decision

Desk 42 is no longer visually organised around one desk, one claimant and a row of cards. It is a compact bureaucratic factory in which incomplete claims become official decisions and defective decisions physically return as appeals.

The primary image of the game is:

> Cream dossiers moving through deep-green office machinery while a red appeals line carries the institution's mistakes back into itself.

The visual design must make four things readable without opening a menu:

1. Where work is going.
2. What operation is happening to it.
3. Where the system is congested or brittle.
4. Which bad output created the problem now returning upstream.

The office should look tired, maintained and slightly absurd. It is not a horror dungeon, a glossy science-fiction factory or an ornamental dollhouse. The comedy comes from institutional logic being performed correctly at industrial scale.

### What survives from the previous identity

- Institutional deep green and paper cream.
- Hard pixel clusters and selective one-pixel outlines.
- Upper-left lighting and two stepped shadow values.
- Chunky CRT, stamp, tube, cabinet and paper machinery.
- Dry official language and red approval/denial marks.
- Alien claimants treated as ordinary participants in a workplace.
- A restrained anomaly colour budget.

### What is retired

- The moth as project mascot or default claimant.
- The fixed front-facing desk as the whole game.
- Cards as the dominant screen element.
- Sanity bars, Fugue overlays and visual input sabotage.
- Generic occult horror, tentacle wallpaper and random corruption.
- Vertical tower cutaways that visually collide with workplace-management competitors.
- Painterly AI concept art presented as production-ready pixel assets.

---

## 2. Visual pillars

### 2.1 Flow is the hero

The paper route is the strongest line in every gameplay frame. Tubes, trays, floor markings, rollers and queue racks should create a legible visual sentence from arrival to ruling.

### 2.2 Machinery is office furniture pushed one step too far

Every machine begins with an object the player recognises: scanner, stamp, cabinet, desk, time clock, phone exchange, archive rack. Automation enlarges and connects it without turning it into generic sci-fi equipment.

### 2.3 Error has a body

An appeal is not a notification badge. It is a red-edged dossier entering a reverse route, consuming Legal capacity and occupying a visible place in the branch.

### 2.4 Characters are workers, not mascots

Humans and aliens share the same tired institutional world. Species changes silhouette and practical accommodation; it does not turn every claimant into a gag.

### 2.5 Chaos remains attributable

The screen may become extremely busy, but the player can follow the causal chain. No full-screen noise, arbitrary flicker or decorative particle storm may conceal the machine state.

### 2.6 Readability beats detail

The game is judged at actual camera scale. A prop that is beautiful at 512 pixels but unreadable at 32 pixels has failed.

---

## 3. Camera, grid and scale

### 3.1 View

- Fixed three-quarter orthographic camera.
- One contiguous floor, not a vertical cross-section.
- One camera quadrant; the player cannot rotate the building.
- Pan across a bounded branch floor.
- Discrete zoom levels only.
- Walls cut away consistently on the camera-facing sides.

### 3.2 Native presentation

| Contract | Target |
|---|---:|
| Logical gameplay canvas | 384 x 216 |
| Aspect ratio | 16:9 |
| Base floor diamond | 32 x 16 px |
| Wall rise | 32 px per storey band |
| Worker cell | approximately 32 x 48 px |
| Small workstation | 32-48 px footprint |
| Major workstation | 64-96 px footprint |
| UI icon master | 32 x 32 px |
| Integer presentation | 5x at 1920 x 1080 |

The exact world scale can change during technical proof, but relationships must remain consistent. Staff cannot become too small to read so that more machinery fits on screen.

### 3.3 Direction budget

To protect a solo production:

- Environments use one fixed camera direction.
- Machines require one authored orientation plus mirrored or modular route pieces where possible.
- Staff use four-direction movement, with east/west mirrored when costume asymmetry permits.
- No eight-direction combat animation set exists.
- Claimants appear in the floor simulation with a walk, wait and desk interaction set; close portraits are optional marketing/UI material, not required gameplay assets.

### 3.4 Zoom rules

| Zoom | Purpose | Required read |
|---|---|---|
| Branch | layout, queues, department balance | routes, congestion, department identity |
| Operations | normal play | workers, machine state, dossier state |
| Inspection | selected claim or machine | exact cause, policy trace, maintenance detail |

Do not use smooth fractional pixel zoom. Camera movement and zoom must preserve pixel stability.

---

## 4. Composition grammar

### 4.1 The floor is a circuit diagram made physical

Main routes should form large readable bands. Important merges and splits occur in open space, not behind tall props. The player should be able to trace a dossier with their finger.

### 4.2 Spatial hierarchy

```text
PRIMARY   dossier routes, queues, major workstations
SECONDARY staff paths, desks, buffers, departmental boundaries
TERTIARY  dressing, signs, bins, plants, personal clutter
```

Tertiary detail may never obscure primary flow.

### 4.3 Department boundaries

Departments are distinguished through:

1. workstation silhouette;
2. floor treatment;
3. storage grammar;
4. local light temperature;
5. staff tool silhouette;
6. accent colour last.

Colour alone is insufficient and unsafe for accessibility.

### 4.4 Empty space is operational

Reserve clear areas for future expansion, temporary queues and emergency rerouting. Empty bays should look deliberately marked, not unfinished.

### 4.5 No tower composition

The branch must not become a stack of dollhouse rooms. Expansion spreads laterally across one bounded institutional floor. This separates the image from newsroom and hospital tower simulations.

---

## 5. Palette

### 5.1 Core palette

| Role | Hex | Use |
|---|---|---|
| Institutional deep green | `#173F32` | walls, machine housings, structural authority |
| Paper cream | `#F1E8CE` | dossiers, forms, readable active work |
| Charcoal ink | `#18201E` | outlines, text, deep mechanisms |
| Dusty blue-grey | `#61736F` | tubes, neutral metal, inactive systems |
| Warm wood | `#8D522C` | desks and human touch points |
| Muted orange | `#CE713A` | work-in-progress and maintenance |
| Approval red | `#B73B32` | rulings, appeals, liability, hard warnings |
| Anomaly teal | `#20D6C7` | impossible information or device state |
| Rupture magenta | `#D447A7` | rare contradiction only |
| Brass | `#B28A46` | handles, clocks, durable mechanisms |

### 5.2 Distribution

Use the inherited `70 / 20 / 8 / 2` discipline:

- 70% institutional base and paper neutrals.
- 20% material support colours.
- 8% task and department accents.
- 2% anomaly or rupture colour.

Red is not decorative. Teal is not general neon. Magenta is not a faction colour.

### 5.3 Department accents

| Department | Accent | Supporting material |
|---|---|---|
| Intake | dusty cyan-blue | glass scanner beds, wire trays |
| Verification | muted amber | lightboxes, brass gauges |
| Adjudication | approval red | stamp blocks, sealed output trays |
| Archive | olive-grey | steel shelves, linen files |
| Legal | dark oxblood/navy | bound briefs, hearing clocks |
| Payroll | brass/mustard | punch clocks, rota boards |

Every department must remain recognisable in greyscale.

---

## 6. Material language

### Office structure

- Painted steel with chipped corners, never random surface noise.
- Linoleum tile with controlled seams that reinforce the grid.
- Paper, card, twine, rubber stamps and cloth-bound files.
- Bakelite switches and chunky plastic keys.
- Brass only at high-touch mechanical points.
- Glass used for scanner beds, gauges and contained phenomena.

### Automation structure

- Tubes use dusty translucent blue-grey with steel collars.
- Conveyors are narrow office rollers, not warehouse belts.
- Hoppers resemble overbuilt inbox trays.
- Control panels use small lamps and physical switches, not glowing holograms.
- Moving parts expose cause: rollers turn, stamps descend, diverters pivot.

### Wear

Wear describes use:

- hands polish handles;
- wheels mark floors;
- paper dust accumulates below cutters;
- stamp ink stains the output side;
- queue racks bow under actual load.

Do not scatter generic scratches uniformly.

---

## 7. Operational state language

Every machine communicates state through at least two channels: motion plus shape/light.

| State | Motion | Shape/light | Sound handoff |
|---|---|---|---|
| Idle | breathing indicator tick | dark tray, one green lamp | quiet relay |
| Processing | rollers/stamp/scanner cycle | focused amber work light | rhythmic clack |
| Output ready | tray lifts or latch opens | cream edge highlight | bell or soft thunk |
| Starved | intake rollers pause | empty hopper silhouette | dry click |
| Blocked | repeated failed movement | orange jam flag | stutter/clatter |
| Overloaded | faster short cycles, queue shake | stacked paper exceeds safe line | strained motor |
| Policy conflict | diverter oscillates between routes | teal-red alternating hard pixels | two relays disagree |
| Appeal return | reverse travel | red folder edge and red route lamp | descending pneumatic whoop |
| Broken | no productive motion | exposed service panel | motor stops, room tone opens |

Never communicate essential state with particles alone.

---

## 8. Department identity

### Intake

Visual verb: **separate and register**.

- Fan-shaped input trays.
- Scanner gates and identity apertures.
- Many arrivals become one ordered line.
- Dusty-blue glass, wire baskets and numbered physical tabs.
- Staff carry trays or compact scanners.

### Verification

Visual verb: **compare and expose**.

- Paired document beds.
- Split lamps, comparison shutters and contradiction needles.
- Evidence enters in parallel and leaves clipped together.
- Amber light concentrated on surfaces, never global orange tint.
- Staff use loupes, headsets, calipers or evidence boards.

### Adjudication

Visual verb: **commit**.

- Strongest downward motion in the branch.
- Large stamp press and sealed output gate.
- Red is visible only at the commitment point.
- Once output leaves, it looks physically harder to reverse.

### Archive

Visual verb: **store and retrieve**.

- Tall but cutaway-safe shelving.
- Mobile ladders, index drums and retrieval arms.
- Capacity is visible through empty slots and overfilled returns.
- Old information becomes darker, dustier and less legible.

### Legal

Visual verb: **loop and reinterpret**.

- Curved reverse routes.
- Bound briefs, hearing clocks and branching precedent racks.
- Legal is the only department where red files routinely enter and may leave cream, red or teal-edged.
- Test cases occupy visibly locked bays.

### Payroll

Visual verb: **pulse and schedule**.

- Punch clocks, shift drums and rota wheels.
- Staff availability appears as physical tokens moving across a schedule.
- Breaks are visible before they happen.
- Payroll power travels to departments through modest brass/copper service lines, not electrical lightning.

---

## 9. Machinery shape grammar

Every workstation has four visible zones:

```text
INPUT → OPERATION → VERDICT/STATE → OUTPUT
```

The player should infer function from silhouette before reading the tooltip.

### Shape rules

- Intake openings face upstream.
- Output trays physically project downstream.
- High-risk machines have heavier latches and guarded commitment mechanisms.
- Buffers show their capacity externally.
- Upgrade tiers add one clear functional attachment rather than coating the machine in detail.
- Machines remain maintainable objects with hinges, wheels, panels and access clearance.

### Upgrade visualisation

| Tier | Visual change |
|---|---|
| Manual | desk-scale tool operated by one employee |
| Assisted | motor or feeder attached to the same tool |
| Automated | dedicated input/output and autonomous cycle |
| Networked | policy terminal and routing ports |
| Anomalous | one impossible operating principle, tightly contained |

Avoid larger-and-brighter as the universal upgrade language.

---

## 10. Dossiers, flow and appeals

### 10.1 The dossier is the game's ore

It must remain legible at branch zoom.

| Dossier state | Visual contract |
|---|---|
| Unregistered | loose cream sheets in wire tray |
| Registered | clipped cream folder with blue corner tab |
| Verified | aligned packet with amber evidence band |
| Contradictory | two misaligned bands and a hard teal split mark |
| Approved | sealed cream packet with one red stamp block |
| Denied | darker red edge and closed fastening |
| Held | orange clock tab projecting from folder |
| Appeal | red outer jacket enclosing the original packet |
| Test case | red jacket plus brass lock clip |
| Precedent-bearing | restrained teal legal ribbon, never glowing |

These states require shape differences, not only colour swaps.

### 10.2 Flow direction

- Normal work travels left-to-right or upper-left to lower-right within the fixed camera.
- Appeal routes visibly oppose normal flow.
- A reverse route must cross or wrap around the machine it challenges.
- Priority is shown by lane ownership and diverter position, not fast flashing arrows.

### 10.3 Congestion

Queue pressure appears through:

- occupied buffer slots;
- folders leaning or compressing;
- staff detours;
- raised trays;
- reduced clear floor area;
- slower mechanical return cycles.

Avoid simply adding a red bar over the department.

---

## 11. Staff design

### 11.1 Silhouette carries role

| Role | Silhouette hook |
|---|---|
| Intake clerk | forward tray/scanner rectangle |
| Verification analyst | circular loupe/headset and narrow evidence board |
| Adjudicator | heavy stamp tool and squared posture |
| Archivist | tall file bundle or retrieval hook |
| Legal officer | bound brief and long timing chain/clock |
| Payroll officer | punch-card rack or rota drum |

Uniform bases can be shared. Role props and posture create recognition.

### 11.2 Character tone

- Competent, tired and specific.
- Expressive through shoulders, pace, pauses and equipment handling.
- No exaggerated mascot idle loops.
- No random panic unless the simulation gives a reason.
- Comedy comes from policy obedience: a worker waits precisely because their authority ends one tile before the jam.

### 11.3 Minimum animation set

```text
Idle
Walk N/S/E/W (E/W mirrored where safe)
Operate workstation
Carry dossier
Wait in queue
Start break
Return from break
Blocked/confused
Small success acknowledgment
```

Use shared skeleton/proportion families wherever possible.

---

## 12. Claimant design

Claimants are not enemy archetypes. Their design must communicate practical needs and the kind of accommodation the institution may mishandle.

### Silhouette principles

- One dominant body idea per claimant.
- Familiar office clothing or carried object grounds the alien anatomy.
- Hands or equivalent manipulation points remain readable.
- Faces may be non-human but emotional state must read through posture.
- Scale variation is allowed within pathing limits.
- Avoid furred mascots, cute collectible framing and insect silhouettes.

### Approved concept families

- Translucent gel elder wearing a practical coat.
- Folded-paper humanoid carrying a rigid file bundle.
- Compact four-armed courier whose paperwork literally exceeds two hands.
- Floating sealed helmet containing a small localised storm.
- Stone-bodied industrial worker protecting a delicate briefcase.
- Delayed echo claimant whose shadow arrives one animation beat late.

### Production rule

AI generation is appropriate for silhouette discovery and costume exploration. It is not validated for identity-safe sprite iteration in the current ArtLab. Final claimant sprites require a locked model sheet and manual pixel redraw.

---

## 13. Anomalies

Anomalies are production modifiers, not a horror layer.

### One contradiction per module

Examples:

- A copier produces the legally recognised original and demotes the input to copy.
- A clock processes tomorrow's deadline but pays yesterday's wage.
- A cabinet stores more files than its visible internal volume.
- A prediction terminal is correct but cannot produce admissible evidence.
- A tube sends one dossier through two routes and expects both outcomes to be valid.

### Visual contract

- Ordinary institutional shell.
- One impossible operating behaviour.
- Teal confined to the impossible boundary.
- Magenta limited to the moment two valid states cannot coexist.
- No bloom fog, tentacles, occult runes or general room corruption.

### Animation contract

The impossible action must be understandable in three beats:

```text
ordinary input → impossible operation → legible consequence
```

---

## 14. UI and information design

The game world carries throughput; UI explains causality.

### Persistent HUD

Keep only:

- branch time/day;
- Head Office objective progress;
- cash and locked liability reserve;
- backlog pressure;
- appeal load;
- manual intervention status.

No permanent card hand, sanity bar or large decorative portrait.

### Selection panel

Selecting a claim opens a dossier rail that shows:

```text
identity → evidence → confidence → policy → decision → appeal history
```

Selecting a machine shows:

```text
input queue → current operation → output rule → staff coverage → fault cause
```

### Causal ledger

Appeals and failures use an ordered trace. The player can move from result back to the exact policy version, machine and skipped check.

### UI art rules

- Paper surfaces for claim-specific information.
- CorpOS dark green/charcoal for branch controls.
- Red only for committed liability or deadline failure.
- Teal only for anomalous facts or impossible policy state.
- Icons are manually redrawn at 16/24/32 px.
- Generated text is never used.

---

## 15. Animation language

### Rhythm

The office should produce a satisfying mechanical score:

```text
receive → align → inspect → stamp → eject
```

Each department owns a recognisable rhythm so the player can sense imbalance before reading numbers.

### Principles

- Anticipation is brief and mechanical.
- Output motion is decisive.
- Jams visibly fail at the same point in the cycle.
- Overload increases queue movement, not cartoon speed lines.
- Staff motion is subordinate to work flow.
- Appeals move against the normal rhythm and should feel disruptive without becoming horror.

### Recommended frame budgets

| Asset | Frames |
|---|---:|
| Small machine idle | 2-4 |
| Machine operation | 6-10 |
| Jam/failure | 4-6 |
| Dossier movement | code tween plus 2-3 state frames |
| Staff walk direction | 4-6 |
| Staff operate loop | 4-8 |
| Anomaly operation | 8-12, one controlled spectacle |

---

## 16. VFX and feedback

### Allowed VFX

- Paper dust on heavy use.
- Hard-edged scanner bands.
- Stamp ink compression and tiny splatter.
- Tube pressure puffs.
- Small sparks only for a defined electrical fault.
- Teal boundary pixels around an impossible operation.
- Magenta contradiction tick at the exact rule collision.

### Prohibited VFX

- Constant bloom.
- Full-screen chromatic aberration.
- Random screen tearing.
- Persistent fog over the floor.
- Confetti for routine throughput.
- Particles used instead of a clear machine animation.

### Feedback priority

```text
1. Object state changes
2. Route changes
3. Character reaction
4. Local VFX
5. UI confirmation
```

---

## 17. Audio handoff

The art and audio systems share one rule: each department must be identifiable with eyes closed or sound muted.

| Department | Visual rhythm | Audio family |
|---|---|---|
| Intake | fan, align, feed | tray slide, scanner chirp |
| Verification | compare, pause, needle settle | paper flip, relay, soft tone |
| Adjudication | descend, impact, seal | stamp thump, latch |
| Archive | lift, travel, slot | wheels, index clicks |
| Legal | reverse, bind, wait | descending tube, clock, binder clamp |
| Payroll | regular pulse | punch clock, rota drum |

When a machine jams, its normal rhythm should become recognisably incomplete rather than replaced by a generic alarm.

---

## 18. Asset hierarchy

### Tier A: gameplay-critical

- Floor and wall kit.
- Dossier state family.
- Tube and conveyor route kit.
- Core workstation state sheets.
- Staff movement and operation families.
- Queue and buffer states.
- Appeal route and Legal return assets.
- Selection and fault indicators.

### Tier B: identity-critical

- Department dressing kits.
- Claimant silhouette families.
- Anomaly modules.
- Hero machine animations.
- CorpOS UI frame and icon family.

### Tier C: polish

- Personal staff props.
- Decorative plants and notices.
- Rare breakdown variants.
- Marketing compositions.
- Close claimant portraits.

Tier C cannot start while Tier A is visually ambiguous.

---

## 19. Solo-developer asset budget

### First playable art target

| Family | Count |
|---|---:|
| Floor/wall modules | 12-16 |
| Tube/conveyor modules | 10-12 |
| Core workstations | 3 |
| States per workstation | 5-7 |
| Buffer/queue props | 4 |
| Shared staff bodies | 2 |
| Role prop overlays | 3 |
| Claimant silhouettes | 4 |
| Dossier states | 8 |
| UI icons | 16-24 |
| Dressing props | 12 |

### Full campaign target

| Family | Ceiling |
|---|---:|
| Departments | 6 |
| Major workstations | 12-18 |
| Shared staff bodies | 4-6 |
| Claimant base bodies | 10-12 |
| Anomaly modules | 6-10 |
| Dressing props | 40-60 |
| Unique marketing illustrations | 3-5 |

The project should prefer systemic recolouring, attachments and state overlays over one-off full redraws.

---

## 20. Comfy ArtLab status

The proven ArtLab pipeline exists in Git but is not present as source files on the current checkout.

Recovery source:

```text
Branch: codex/artlab-fast-assets
Commit: f871c307e98da1a6a638407a5c08793b95a05fbc
```

Raw proof outputs remain under:

```text
C:\Users\jacob\ComfyUI-Shared\output\Desk42\ArtLab
```

Do not rebuild the pipeline from memory. Restore or cherry-pick the proven ArtLab sources before production use.

### Proven lifecycle

```text
BRIEF
→ GENERATED
→ HARD-GATED
→ CANDIDATE
→ SELECTED
→ REFINED
→ VALIDATED
→ PROMOTED
```

Technical validation never overrides a failed visual gate.

---

## 21. ArtLab workflow A: structured props

Use for machines, cabinets, desks, route modules and department furniture.

```text
YAML BRIEF
→ BLENDER STRUCTURE
→ BEAUTY / SILHOUETTE / DEPTH / NORMAL / EDGE PASSES
→ CONTROLNET APPEARANCE GENERATION
→ CONTACT SHEET
→ HUMAN SELECTION
→ CUTOUT / QUANTISE / NATIVE-GRID REDRAW
→ VALIDATE
→ PROMOTE
```

### Ownership boundary

| Tool | Owns |
|---|---|
| Blender | dimensions, camera, silhouette, perspective, moving-part clearance |
| ComfyUI | material, palette, wear, surface ideas |
| Human edit | pixel clusters, final contours, gameplay state readability |
| Unity | animation timing, route logic, feedback and interaction |

### Proven settings

```text
Checkpoint: Juggernaut-XL v9
Canvas: 512 x 512
Sampler: DPM++ 2M
Scheduler: Karras
Steps: 26
CFG: 7.0
ControlNet strength: 0.85
ControlNet end: 0.90
Exploration seeds: fixed family of 8
Downscale: area to 128
Quantisation: 28 colours, Bayer-4
Preview: nearest-exact to 512
Final cutout proof: 256 x 256 RGBA
```

The selected filing cabinet proved the route, but still required native-grid cleanup. It is visual-development material, not a shipping sprite.

---

## 22. ArtLab workflow B: environment plates

Use Blender or a simple grid mock-up to define:

- floor footprint;
- wall cuts;
- route clearance;
- machine footprints;
- staff path width;
- camera and light direction.

Feed structure to ComfyUI for material and atmosphere exploration. Never allow generation to invent a layout that has not passed pathing and gameplay readability checks.

### Environment generation sequence

1. Greybox one department and two route turns.
2. Render beauty, depth and edge control.
3. Generate eight material candidates with fixed structure.
4. Reject any candidate that changes entrances, clearances or machine orientation.
5. Select one palette/material treatment.
6. Rebuild as modular pixel tiles.
7. Test at Branch and Operations zoom.

---

## 23. ArtLab workflow C: people and claimants

The existing claimant edit/inpaint experiments were not validated for identity preservation. IP-Adapter and background-removal weights exist, but the necessary node path was not operational during the proof.

Therefore:

```text
AI silhouette exploration
→ human design selection
→ clean model sheet
→ manual pixel sprite
→ shared animation rig
```

Use ComfyUI for body-shape ideas, practical alien accommodations, costume and role-prop exploration, portrait mood candidates and colour variants after the silhouette is locked.

Do not use it for final walk cycles, exact sprite sheets, identity-critical frame-to-frame animation, UI text or a shipping character without redraw.

---

## 24. Prompt blocks

### Shared style DNA

```text
authored 2D pixel art for a bureaucratic automation management game,
fixed three-quarter orthographic view, institutional deep green and paper cream,
warm charcoal and dusty blue-grey supporting ramps, muted orange task accents,
approval red limited to commitments and appeals, crisp hard pixel clusters,
selective one-pixel outlines, two stepped shadows, upper-left fluorescent light,
controlled 28-colour palette, readable at native gameplay scale,
chunky serviceable office-industrial machinery, no smooth rendering
```

### Branch environment block

```text
compact single-floor alien insurance branch, lateral expansion bays,
visible dossier queues, pneumatic tubes, narrow paper rollers, wire trays,
painted steel cabinets, linoleum grid, warm wood desks, clear staff paths,
ordinary office furniture transformed into legible automation machinery,
no vertical tower cross-section, no dungeon, no glossy science fiction
```

### Machine block

```text
isolated modular workstation in consistent three-quarter orthographic view,
clear input hopper, visible central operation, readable output tray,
service panel and moving-part clearance, deep green painted steel housing,
cream paper payload, brass high-touch fittings, animation-ready silhouette,
plain neutral background, no labels, no readable text
```

### Claimant block

```text
full-body alien insurance claimant, tired practical office-world character,
one dominant readable body idea, familiar coat or carried object,
clear manipulation points and emotional posture, animation-ready proportions,
plain neutral background, no mascot framing, no combat pose
```

### Universal negative

```text
moth, insect, antennae, furry mascot, cult, occult rune, tentacle wallpaper,
combat, weapon, armor, dungeon, vertical tower, dollhouse skyscraper,
glossy sci-fi, hologram, neon cyberpunk, photorealism, painterly rendering,
soft gradient, bloom, depth of field, random pixel noise, unreadable silhouette,
fake UI, readable text, logo, watermark, generated sprite sheet,
multiple conflicting camera angles, inconsistent scale
```

---

## 25. Prompt recipes

### Hero branch concept

```text
[SHARED STYLE DNA]
[BRANCH ENVIRONMENT BLOCK]

wide 16:9 gameplay-readable branch floor with Intake, Verification and
Adjudication active, cream dossiers travelling left to right through tubes,
trays and rollers, one rejected ruling returning in a red appeal line,
small employees operating stations autonomously, empty central Desk 42
overlooking the machine, three marked future expansion bays, dry dark comedy,
no labels, no readable text

[UNIVERSAL NEGATIVE]
```

### Structured workstation concept

```text
[SHARED STYLE DNA]
[MACHINE BLOCK]

mechanical adjudication stamp press, folder enters from upper-left tray,
heavy stamp descends at centre, sealed packet exits lower-right,
red commitment block visible only at impact point, clear jam access panel,
design supports idle, processing, output-ready, blocked and appeal-return states

[UNIVERSAL NEGATIVE]
```

### Claimant concept

```text
[SHARED STYLE DNA]
[CLAIMANT BLOCK]

stone-bodied industrial worker in rolled-sleeve office shirt and suspenders,
massive durable silhouette carrying one visibly delicate briefcase,
patient exhausted waiting posture, practical shoes, hands fully visible,
no monster aggression, no fantasy golem armor

[UNIVERSAL NEGATIVE]
```

### Appeal congestion concept

```text
[SHARED STYLE DNA]
[BRANCH ENVIRONMENT BLOCK]

Legal return route overloaded with red-jacketed appeal dossiers,
buffers visibly full, one reverse tube crossing the clean cream forward flow,
workers following policy correctly while the branch gridlocks,
cause remains traceable, local warning lights only, no disaster spectacle

[UNIVERSAL NEGATIVE]
```

---

## 26. Brief format

Every ArtLab generation begins with a brief, not a free prompt.

```yaml
asset_id: D42-AUTO-[CATEGORY]-[NAME]-T[NUMBER]
purpose: gameplay / visual-development / marketing
camera: fixed-three-quarter-orthographic
native_target: [width, height]

hard_requirements:
  - binary visual requirement
hard_rejections:
  - binary reason to reject

weighted_preferences:
  silhouette: 35
  function_read: 30
  palette_match: 20
  material_quality: 10
  novelty: 5

required_states:
  - idle
  - processing
  - blocked

technical_checks:
  alpha: required / not-required
  dimensions: exact values
  naming: expected pattern
  native_scale_preview: required

provenance:
  workflow: path
  checkpoint: name
  seed: number
  control_inputs: paths
  human_edits: description
```

Hard requirements are pass/fail. A weighted score cannot rescue a failed input/output silhouette.

---

## 27. Selection and validation

### Contact-sheet review order

1. Read at 128 px.
2. Read in greyscale.
3. Identify function without caption.
4. Check camera and footprint.
5. Check palette and material.
6. Inspect detail only after those pass.

### Asset acceptance checklist

| Check | Pass condition |
|---|---|
| Function | input, operation and output are inferable |
| Silhouette | recognisable at gameplay scale |
| State family | idle, working and blocked differ structurally |
| Camera | matches the fixed branch projection |
| Grid | footprint and connection points are exact |
| Palette | follows base and accent budgets |
| Pixel craft | intentional clusters, no filter noise |
| Occlusion | does not hide routes or staff behind unnecessary height |
| Animation | moving parts and pivots are defined |
| Accessibility | critical state has shape/motion support |
| Provenance | model, workflow, seed and edits are recorded |
| Unity test | reads at Branch and Operations zoom |

---

## 28. Naming and folders

### Naming

```text
D42_AUTO_[Department]_[Category]_[AssetName]_[Variant]_[Status]
```

Examples:

```text
D42_AUTO_VER_MCH_CompareLightbox_A_Selected.png
D42_AUTO_LEG_ROUTE_AppealDiverter_B_Paintover.png
D42_AUTO_SHARED_DOC_AppealPacket_01_Approved.png
D42_AUTO_PAY_CHR_RotaOfficer_ModelSheet_Approved.png
```

### Status values

```text
Raw
HardGated
Candidate
Selected
Refined
NativeRedraw
EngineTest
Approved
Rejected
```

### Folder structure

```text
ArtLab/
  briefs/
  workflows/
  references/
  reports/

ArtSource/Automation/
  Environment/
  Routes/
  Machines/
  Staff/
  Claimants/
  Dossiers/
  UI/
  Anomalies/

Assets/_Project/Art/Automation/
  Approved runtime assets only
```

Raw generations remain outside Unity until explicitly promoted.

---

## 29. Provenance and commercial discipline

Record for every selected generated source:

- asset ID;
- generated date;
- workflow JSON;
- model and licence;
- additional model/control components;
- seed;
- prompt and negative prompt;
- input controls and references;
- selection reason;
- human edit summary;
- final use: concept, source, runtime or marketing;
- approval owner.

No generated file becomes a runtime or marketing asset merely because it passed dimensions and alpha validation.

---

## 30. Production plan

### Week 1: visual proof

- Lock native grid and camera.
- Redraw one machine from the generated prop plate.
- Build one straight and one curved route.
- Create all dossier states.
- Test cream forward flow against red appeal return.

Pass: a still frame explains the core loop with no UI text.

### Week 2: operational proof

- Intake, Verification and Adjudication greybox kits.
- One employee body and three role overlays.
- Machine idle, process, output and blocked states.
- One queue filling and clearing.

Pass: a 20-second clip makes the bottleneck obvious.

### Week 3: character proof

- Four claimant model sheets.
- Two shared staff bodies.
- Four-direction staff walk.
- Wait, operate and carry animations.

Pass: roles and claimant needs read at Operations zoom.

### Week 4: art pipeline proof

- Restore proven ArtLab sources from the recovery branch.
- Produce one structured prop through every lifecycle gate.
- Produce one environment material plate.
- Record provenance and promotion report.

Pass: the pipeline is reproducible by someone other than its author.

### Weeks 5-6: slice art

- Assemble one bounded floor.
- Integrate UI cause trace.
- Add first appeal return and one anomaly module.
- Add audio hooks and performance-state variants.

Pass: the slice looks like a machine with consequences, not a desk game placed in a larger room.

---

## 31. Falsifiable art tests

Reject or revise the direction if:

- players cannot trace a dossier route in one second;
- departments are identifiable only by tooltip or colour;
- an appeal reads like ordinary red loot;
- the floor resembles a vertical newsroom or hospital tower;
- machines look like generic factory assemblers with paper pasted on them;
- aliens read as collectible mascots;
- the screen becomes noisy before the simulation becomes complex;
- generated props require more correction than a direct pixel redraw;
- one new workstation demands a unique animation and UI system;
- the empty Desk 42 ending does not visually echo the branch the player built.

---

## 32. What not to do

Do not:

1. Reintroduce moths because existing art is available.
2. Build the branch as a tower.
3. Hide automation behind menus.
4. Use warehouse belts when office rollers and tubes can express the same function.
5. Colour-code without silhouette support.
6. Add general corruption as visual flavour.
7. Generate final sprite sheets with diffusion.
8. Ship raw AI pixels.
9. Put readable text inside generated art.
10. Start marketing key art before the gameplay frame reads.
11. Create six unique staff rigs before one shared family works.
12. Let plants and desk clutter block route visibility.
13. Use modern glass-office minimalism.
14. Mistake high detail for production value.

---

## 33. Immediate art deliverables

Build these in order:

1. Branch floor greybox at the locked camera.
2. Forward-flow and appeal-return route tiles.
3. Dossier state sheet.
4. Intake scanner state sheet.
5. Verification lightbox state sheet.
6. Adjudication stamp press state sheet.
7. Shared employee body and three role overlays.
8. Four claimant silhouette model sheets.
9. First UI causal ledger.
10. One ArtLab structured-prop proof through promotion.

The first public-quality frame should show one thing clearly:

> The branch manufactured a bad decision, and the bad decision has physically returned to demand processing.

---

## 34. Final production principle

The art is successful when the player can watch the office and understand its argument.

```text
This arrived.
That department changed it.
This policy committed it.
That decision came back.
Now the machine is choking on what it said.
```

If the image only communicates “strange office,” it is atmosphere. If it communicates that chain, it is Desk 42.
