# Desk 42 — Prompt Execution Codex v3.1

**Status:** final verification pass. Do not generate art or wire Unity until this file is approved.

**Role:** executable prompt companion to `DESK42_PROMPT_CODEX_v2_1.md`.

If anything in this file conflicts with v2.1, the Pixel-Art Visual Identity, or the GDD, the higher authority wins. This file operationalises those decisions; it does not replace them.

## What v3.1 locks

- One fixed 384 × 216 gameplay composition.
- One continuous desk and one physical claimant seated opposite the player.
- Separate environment, claimant, anomaly, lighting, and interface layers.
- One hero anomaly per generated image or runtime state.
- Gel Anomaly as a non-human amorphous claimant with human-readable emotion.
- Smug behaviour as the GDD-canonical “leans back, puts feet on desk.”
- Numeric claimant registration and a separate fixed contact shadow.
- Overlay-only anomalies versus replacement-patch anomalies.
- Generated images as references requiring pixel cleanup or redraw, never shippable sprites by default.

---

## 1. Generation assembly

Generate and validate four things separately:

```text
1. CLEAN POPULATED BASELINE  room + claimant; composition validation only
2. ENVIRONMENT PLATE         room without claimant; production base
3. CLAIMANT LAYER            one species/state registered to the desk
4. ANOMALY LAYER             one overlay or replacement patch
```

The Clean Populated Baseline is generated first. The other three asset types are not attempted until its camera, desk, claimant placement, and room anchors pass review.

### One-anomaly test

Before submitting any prompt, finish this sentence:

> If the viewer points to the one impossible event, they will point to ________.

If the blank requires “and,” the prompt fails. Tier colour, wear, lean, and desaturation may support the hero, but no second object may perform another impossible action.

---

## 2. Shared style DNA

Append this to every generation:

```text
fixed three-quarter orthographic pixel-art claims office,
composed for a 384 by 216 native gameplay grid,
institutional deep green and paper cream base palette,
warm grey and dusty blue-grey supporting ramps,
muted orange task accents and approval-red stamp blocks,
hard pixel clusters, sparse purposeful dithering,
selective one-native-pixel outlines on hero silhouettes,
two stepped shadow values: form shadow and contact shadow,
nearest-neighbour presentation, no anti-aliasing
```

“16-bit adventure game” may be used as mood shorthand only. It must never replace the exact palette, grid, material, silhouette, and cluster rules above.

### Shared negative

```text
soft gradients, smooth rendering, anti-aliasing, bloom, glow halo,
depth of field, bokeh, PBR reflections, painterly texture, watercolor,
ray tracing, subsurface scattering, lens flare, lens distortion,
Dutch angle, camera tilt, perspective drift, modern luxury office,
glass panels, pill buttons, neon cyberpunk framing,
readable text, readable letters, legible words, fake pixel overlay,
random single-pixel noise, high-resolution painterly microtexture
```

“No glow” means no soft bloom. A CRT or anomaly may emit a local one- or two-pixel hard-edged light band.

---

## 3. Locked composition

### Camera

- Canvas: 384 × 216 pixels, 16:9.
- Camera: fixed 3/4 orthographic office view.
- Viewpoint: claims worker’s side of the desk, looking toward the claimant.
- Claimant: upper-centre, facing the player across the same desk.
- Player chair: lower-centre, partially cropped.
- No generic isometric dollhouse, corner diorama, camera drift, or tier-specific camera change.

### Desk

- One continuous broad claims desk with a single horizontal silhouette.
- A shallow player-side cut-out is allowed only if the slab still reads as one desk.
- Never use the words `L-shaped`, `U-shaped`, `paired`, `modular`, or `adjoining` in a positive prompt.
- No side table, interview table, second workstation, second desk, or duplicate CRT.

### Room anchors

| Anchor | Locked location | State use |
| --- | --- | --- |
| Door | back-left wall | entry and passive-tentacle anchor |
| Noticeboard | left background | paper storytelling and board-bleed anchor |
| Claimant chair | upper-centre | fixed physical visitor seat |
| Wall clock | upper-right wall | animated sanity read |
| Filing cabinet | far-right background | storage and occupied-cabinet anchor |
| Fluorescent fixture | above processing axis | clean lighting authority |
| Wall/ceiling seam | visible across upper room | ethereal-tube anchor |

### Desk zones

| Zone | Placement | Contents |
| --- | --- | --- |
| Personal | far-left | cream enamel mug and one non-figurative brass staff token |
| Inbox | upper-left | wire tray with up to two cream forms |
| Processing | centre | current form, red stamp block, ink pad, clear handoff space |
| Outbox | upper-right | no more than two completed folders |
| Dead zone | front-centre/right | deliberately empty unless it is the selected anomaly anchor |

Supporting props:

- CRT: left of centre, low enough to preserve claimant face and hands.
- Keyboard: directly in front of CRT.
- Claims machine: right of centre, subordinate to the current form.
- Pen holder: back-right.
- Crumpled form: one maximum in the front-left discard lane.
- Telephone: back-left behind the inbox; it must not displace the mug or processing zone.
- Desk lamp: back-right between claims machine and pen holder; it must not occupy the dead zone.

The desk contains no figurative photograph. The current claimant is never represented as a picture, portrait, hologram, or video call.

---

## 4. Numeric registration contract

All coordinates below use a **top-left origin** on the 384 × 216 native canvas. Pixel coordinates are integers.

### Room registration

| Item | Contract |
| --- | --- |
| Canvas | `384 × 216` |
| Composition centre | `(192, 108)` |
| Shared desk/claimant contact line | `Y = 112` |
| Protected claimant lane | `X = 128–255`, `Y = 0–127` |
| Protected face lane | `X = 164–219`, `Y = 28–78` |
| Protected hand/contact band | `X = 152–231`, `Y = 96–116` |
| Desk foreground occlusion begins | `Y = 113` behind hands and lower torso |

No CRT, paper pile, prop, card, or interface block may enter the protected face lane. Only the claimant may occupy it.

### Claimant master cell

- Native cell: `128 × 128`.
- Local top-left origin.
- Allowed opaque bounds: `X = 8–119`, `Y = 4–121`.
- Registration pivot: local `(64, 112)`.
- Room anchor: canvas `(192, 112)`.
- Unity equivalent with bottom-left origin: `(192, 104)` on a 384 × 216 canvas.
- Default scale: exactly `1.0` at native composition.
- Rotation: `0`.
- State and species swaps may not change the pivot.

The claimant’s hands meet the desk in the local `Y = 96–116` band. Antennae, ears, gel crown, or void rim may use the upper cell, but no silhouette may escape the 128 × 128 cell.

### Fixed claimant contact shadow

- Separate layer: `Claimant_ContactShadow`.
- Full-canvas export: `384 × 216`, transparent outside the shadow.
- Active patch bounds: `X = 144–239`, `Y = 102–121`.
- The shadow is fixed to the desk/environment, not parented to the claimant sprite.
- Species swaps may change its opacity mask only; they may not move its origin.
- The Smug feet-on-desk state may provide an additional state-specific foot contact shadow, also full-canvas and fixed to the desk.

### Draw order

```text
BG_WallsFloor
BG_Door / BG_Noticeboard / BG_Clock / BG_FilingCabinet
Desk_Base_Rear
Desk_Props_Back / Desk_Papers_Interactive
Claimant_Chair
Claimant_ContactShadow
Claimant_[Species]_[State]
Desk_Foreground_Lip
Desk_Props_Front
FG_PlayerChair
Lighting
Anomaly overlay or replacement patch
CorpOS / game UI
```

The desk is deliberately split so its rear surface sits behind the claimant while the foreground lip occludes the lower body. Front props may not enter the protected face or hand lanes. The claimant pivot and contact line never change.

---

## 5. Clean Populated Baseline

This is the first and only generation before composition approval. It is a visual target, not a production asset.

### Baseline prompt

```text
[SHARED STYLE DNA]

fixed three-quarter orthographic pixel-art claims office composed for
384 by 216, viewed from the claims worker’s side of one continuous broad
desk toward one seated Moth Accountant in the upper centre.

The Moth Accountant is a physical visitor across the same desk: broad
feathery antennae, large tired compound eyes, folded wing mass beneath
a worn wool suit, dusty scales on face and hands. Pending state, hands
resting at the shared desk edge, antennae relaxed forward. Face and hands
fully visible. No portrait, screen image, hologram, or empty client chair.

Back-left: closed office door. Left background: cork noticeboard with
squared blank cream forms. Upper-right wall: round clock caught honestly
mid-tick with simple tick marks and matching shadow. Far-right: closed
metal filing cabinet with aligned chrome handles. One fluorescent fixture
above the processing axis. The wall/ceiling seam remains visible.

One continuous desk. Far-left personal corner: cream enamel mug and one
small non-figurative brass staff token. Upper-left: wire inbox with two
cream forms, with Bakelite telephone behind it. Left of centre: low chunky
CRT and keyboard, never covering the claimant. Centre: strongest light,
one current blank ruled form, abstract approval-red stamp block, ink pad,
and clear handoff space. Right of centre: squat claims machine. Back-right:
switched-off brass gooseneck lamp and pen holder. Upper-right: two-folder
outbox. Front-left: one crumpled form. Front-centre/right: clear dead zone.
Lower-centre: player chair cropped by the frame.

Calm, useful, slightly tired institutional office. Clean fluorescent
authority light. Zero anomaly colour and no horror event. Approximate
palette distribution 70 percent institutional green and cream, 22 percent
supporting neutrals, 8 percent task accents, 0 percent anomaly colour.
```

### Baseline negative

```text
second desk, side table, second workstation, duplicate CRT, L-shaped desk,
U-shaped desk, paired desks, modular desk, empty room, missing claimant,
empty claimant chair in final composite, claimant portrait, wall portrait,
framed photograph, figurative desk photograph, hologram, video call,
CRT covering claimant face, props covering claimant hands,
generic isometric dollhouse, corner diorama, anonymous clutter,
human claimant, human in costume, readable clock numerals,
[SHARED NEGATIVE]
```

### Baseline rejection criteria

Reject the image if any answer is “no”:

1. Is there exactly one desk?
2. Is the claimant physically seated opposite the player?
3. Are the claimant’s face and hands unobstructed?
4. Are the door, noticeboard, clock, cabinet, and ceiling seam visible?
5. Is the centre processing zone readable within one second?
6. Is the dead zone actually empty?
7. Is the scene crisp at 1× native scale after cleanup?
8. Is there zero supernatural event or anomaly colour?

---

## 6. Environment plate

The environment plate is the approved baseline composition without the claimant. It retains the claimant chair, fixed contact-shadow slot, desk, props, room anchors, and protected lane.

### Environment clean prompt

Use the Clean Populated Baseline prompt with these changes only:

```text
remove the claimant figure while preserving the upper-centre visitor chair,
protected claimant footprint, desk contact line, lighting, and every room
anchor. This is an isolated production environment plate intended to
receive a separately registered claimant layer.
```

### Environment negative

```text
person, claimant figure, human figure, seated body, portrait,
framed photograph, wall portrait, hologram, video call,
second desk, side table, second workstation, duplicate CRT,
L-shaped desk, U-shaped desk, paired desk, modular desk,
generic isometric dollhouse, corner diorama, anonymous clutter,
[SHARED NEGATIVE]
```

Do **not** add `empty-room presentation` to this negative. A claimant-free environment plate is intentional. The phrase remains restricted to the Clean Populated Baseline negative through `empty room` and `missing claimant`.

---

## 7. Uneasy environment variants — one contradiction each

Geometry remains correct. Palette remains healthy office. Generate each variant separately from the same clean plate, seed, and composition guide.

### U-CLOCK — clock disagreement

```text
[SHARED STYLE DNA]
the approved clean office composition, unchanged in every object and angle.
The round wall clock is the only contradiction: its red second hand is a
short radial smear of motion pixels moving too fast, while its wall shadow
shows a different hand position. All drawers are closed. Every form is
cream and aligned. The noticeboard is straight. Lighting is stable.
No other anomaly or supporting symptom.
```

### U-DRAWER — three inches

```text
[SHARED STYLE DNA]
the approved clean office composition, unchanged in every object and angle.
The filing cabinet’s second drawer is the only contradiction: it stands
open exactly three inches, showing ordinary folder tabs and a flat darkness
that the fluorescent light does not enter. The clock is honest. Forms,
noticeboard, door, and lighting are normal. No other anomaly.
```

### U-INK — wrong form

```text
[SHARED STYLE DNA]
the approved clean office composition, unchanged in every object and angle.
One form in the inbox is the only contradiction: its abstract ruled marks
use faint teal ink while every sibling uses neutral dark ink. No readable
letters. Clock, cabinet, noticeboard, door, and lighting are normal.
No other anomaly.
```

### U-FLICKER — light skips

```text
[SHARED STYLE DNA]
the approved clean office composition, unchanged in every object and angle.
The fluorescent fixture is the only contradiction: one hard horizontal
band is missing from its lit state, implying a two-frame flicker. Its
contact shadows remain otherwise correct. Clock, cabinet, forms, board,
and door are normal. No other anomaly.
```

Never combine these variants into one image.

---

## 8. Contaminated environment variants — one hero each

Shared tier treatment:

```text
same approved room and camera; walls lean two to three degrees as one
coherent structural treatment; paper is yellowed cream; rust-orange wear
and sickly green contamination enter the office ramps; soot-brown deep
shadows. Task-critical zones remain readable.
```

The tier treatment is not a second hero. Each prompt below names the only impossible event.

### CO-BOARD — board bleed

```text
[SHARED STYLE DNA]
[CONTAMINATED TIER TREATMENT]

The noticeboard is the only impossible event. Two slow trails of dark
viscous matter seep from behind its frame and pool on the linoleum. The
matter is brown-black with one restrained teal reflection cluster. Every
blank cream memo remains perfectly squared, clean, and pinned.

Clock functions normally. Door is closed. Filing cabinet is closed and
aligned. Desk equipment is worn but ordinary. No other anomaly.
```

### CO-TENTACLE — passive visitor

```text
[SHARED STYLE DNA]
[CONTAMINATED TIER TREATMENT]

The back-left door is the only impossible event. It stands open by a hand’s
width, and one matte charcoal tentacle lies passively across three floor
tiles. Its underside has five to seven restrained pale-teal sucker marks.
A rust-coloured old stain surrounds its resting point. It is not reaching,
grabbing, or moving.

Clock functions normally. Noticeboard is clean and aligned. Filing cabinet
is closed. Desk equipment is ordinary. No other creature or anomaly.
```

### CO-TUBE — infrastructure error

```text
[SHARED STYLE DNA]
[CONTAMINATED TIER TREATMENT]

One ethereal tube is the only impossible event. It follows the wall/ceiling
seam, entering the left wall and exiting the right wall without source or
destination. Pale teal liquid moves in separated hard-lit segments. Three
small brass brackets hold it to the seam. It casts one narrow hard-edged
cool band on the wall, never a bloom.

Clock functions normally. Door is closed. Noticeboard is clean. Filing
cabinet is closed and unbowed. Desk lamp is switched off. No second light
spectacle or anomaly.
```

### CO-CABINET — occupied drawer

```text
[SHARED STYLE DNA]
[CONTAMINATED TIER TREATMENT]

The far-right filing cabinet is the only impossible event. Its middle
drawer bows outward in a shallow metal dome. Through the top gap, two dark
rounded pressure shapes with hairline teal veins press patiently into the
room. Three blank manila folders lie beside it. The chrome handle is the
brightest local cluster.

The desk’s current form carries an abstract approval-red stamp block with
no letters. Clock functions normally. Door and noticeboard are normal.
No dust event, second creature, or additional anomaly.
```

---

## 9. Impossible environment variants — one break each

Shared tier treatment:

```text
same approved room and camera; office palette heavily desaturated;
cream becomes bone-grey and green becomes ashen. Teal or electric blue
survives only inside the selected hero event. Magenta, if used, is limited
to one cluster of one to three native pixels belonging to that same event.
```

### IM-LAMP — void lamp only

```text
[SHARED STYLE DNA]
[IMPOSSIBLE TIER TREATMENT]

The desk lamp is the only impossible event. Its brass body remains ordinary,
but it casts a hard circle of pure absence instead of light. A one-native-
pixel teal rim defines the circle. Desk surface and blank papers crossing
the boundary are cleanly cropped, not darkened or faded. One three-pixel
magenta rupture sits at the lamp-cord connection and belongs to this event.

The wall clock is attached and reads normally. Noticeboard forms are present
and aligned. Door is closed. Cabinet is closed. Floor tiles are intact.
No other detached, blank, cracked, displaced, or living object.
```

### IM-FLOOR — floor breach only

```text
[SHARED STYLE DNA]
[IMPOSSIBLE TIER TREATMENT]

The linoleum floor is the only impossible event. Two- to four-pixel dark
gaps open between a controlled cluster of tiles beneath the dead zone, and
hard electric-blue light rises through those gaps. Nearby desk and cabinet
undersides receive one narrow stepped blue band. One magenta pixel sits in
the widest gap and belongs to the same breach.

Nothing is visible moving beneath the floor. The wall clock is attached and
normal. Noticeboard, door, cabinet, ceiling seam, desk lamp, and CRT are
present and structurally normal. No tube, creature, detached object, blank
board, or second anomaly.
```

---

## 10. Fugue — positional disagreement only

Fugue is **not** generated as two furnished rooms. The hero event is the positional disagreement itself.

### FU-BASE generation prompt

```text
[SHARED STYLE DNA]
the approved office composition exactly once, in near-total desaturation.
One desk, one claimant chair, one clock, one CRT, one door, one noticeboard,
one filing cabinet. All objects remain in their approved positions. The
foreground and interface are withheld for later compositing. No ghost room,
duplicate furniture, tentacle, creature, detached clock, impossible hands,
or second anomaly.
```

### FU-GHOST derived layer — no generative prompt

Create from the approved FU-BASE plate:

1. Duplicate the flattened approved room silhouette once.
2. Reduce it to a flat teal value family.
3. Offset the derived layer exactly `+8 px X`, `+4 px Y`.
4. Mask it so the original grey room remains dominant and readable.
5. Add at most one small magenta rectangle inside the derived CRT region.
6. Do not add new furniture, figures, tentacles, clock hands, or props.

The ghost is a transformed copy of the same room, never a second generated room. Unity may reproduce the same offset from the clean registered layers.

---

## 11. Claimant production contract

### Extraction, not fake transparency

Do not rely on the words `transparent background` to create production alpha.

For concept generation:

1. Render the claimant against one flat, textureless matte colour selected to contrast with that species.
2. Generate or paint a separate hard mask.
3. Remove matte spill and edge halos manually.
4. Redraw the final claimant on the native 128 × 128 pixel grid.
5. Validate the alpha at 1× against both paper cream and deep green backgrounds.

Never accept checkerboard imagery, soft alpha fringes, premultiplied dark halos, or residual background pixels as transparency.

### Claimant style block

```text
registered 128 by 128 pixel-art seated claimant cutout,
three-quarter frontal view across an office desk,
registration pivot local 64,112 mapped to room anchor 192,112,
face inside protected upper-centre lane,
hands or state-defining limbs readable at the shared desk edge,
sober bureaucratic clothing, upper-left fluorescent light,
hard pixel clusters, two stepped shadow values,
selective one-native-pixel silhouette outline,
flat textureless extraction matte behind the figure
```

### Claimant negative

```text
full room, second desk, wall portrait, ID card photograph, hologram,
floating bust, human cosplay, ordinary human claimant, random extra eyes,
feral horror, attack pose, fantasy armour, cropped face, hidden hands,
soft painterly rendering, checkerboard background, fake transparency,
glow fringe, readable text, [SHARED NEGATIVE]
```

“Human-readable” means the player can understand the emotion. It does **not** mean human anatomy, origin, skull, skin, or silhouette.

---

## 12. Core claimant prompts

### Moth Accountant

```text
[CLAIMANT STYLE BLOCK]
non-human moth office worker with broad feathery antennae, large tired
compound eyes, and folded wing mass beneath a worn wool suit. Sparse dusty
scales form controlled warm-grey pixel clusters on face and hands. Slightly
hunched professional posture. Silhouette hook: antennae and folded wings.
Material hook: dusty scale clusters against coarse wool.
```

State suffixes:

```text
PENDING: antennae relaxed forward, hands resting flat at desk edge,
neutral expectant hold.

AGITATED: antennae raised and separated, one hand grips a blank form,
slight forward lean, compound eyes wider.

LITIGIOUS: antennae rigid toward the player, both hands press a complaint
packet flat, chin raised.

COOPERATIVE: antennae relaxed low, one hand turns a blank form toward the
player, slight head tilt.

SUSPICIOUS: antennae swept back, one eye narrowed, one hand shields the
edge of the current form.

RESIGNED: antennae drooped, shoulders collapsed, hands loose, gaze lowered.

PARANOID: antennae shown with one controlled motion-pixel echo, eyes toward
the back-left door, one hand partly covering the mouth.

DISSOCIATING: antennae perfectly still and symmetrical, eyes unfocused,
hands flat, one displaced outline pixel behind the shoulder. Pivot and body
position do not move.

SMUG: GDD-canonical dominant encounter pose; leans back and places both
shoe tips on the desk edge, feet clearly readable without covering the
current form or claimant face. Hands remain visible on chair arms or lap.
No substitute hand-on-folder pose.
```

### Gel Anomaly

```text
[CLAIMANT STYLE BLOCK]
non-human amorphous corporate claimant: one coherent translucent gel mass
forms the head, upper torso, and two functional pseudopod hands. There is
no human skin, skull, scalp, ears, neck, or human face underneath. A sober
cream office shirt, dark tie, and institutional jacket are worn around and
partly suspended within the gel mass. Two stable dark inclusions and one
small mouth-like fold create a human-readable expression without human
anatomy. Sparse teal inclusions drift inside in controlled clusters. The
outer contour settles, bubbles once, and briefly misaligns its expression.
Silhouette hook: soft asymmetric gel crown and shoulder boundary.
Material hook: clear gel bands with suspended teal inclusions.
```

Additional Gel negative:

```text
bald human man, human head, human skin, human skull, human ears, beard,
moustache, gel patch on cheek, slime-covered human, ordinary human body,
realistic translucent fluid, glass person
```

### Unregistered Alien

```text
[CLAIMANT STYLE BLOCK]
non-human claimant with a long narrow skull, two large dark orbital shapes,
matte olive skin, and restrained one- or two-pixel moist highlights. Sober
dark suit and tie. Long precise fingers meet the protected desk-edge band.
Extremely still between movements. Silhouette hook: elongated skull and
orbital shapes. Material hook: matte olive with controlled moist highlights.
```

### Void Proxy

```text
[CLAIMANT STYLE BLOCK]
non-human proxy whose head is one hard mask-like near-black plane enclosing
negative space. No human face lies beneath it. Two thin teal discontinuities
break the rim where office light catches. Standard orange-brown office jacket
and cream shirt. Simplified glove-like hands remain unnaturally still.
Movements jump between held frames. Silhouette hook: hard mask plane and
enclosed void. Material hook: near-black field with broken teal rim.
```

The first claimant review contains all four species in Pending plus the complete nine-state Moth sheet. Other species do not receive invented state behaviours until their state sheets are separately approved.

---

## 13. Anomaly compositing contract

An anomaly is either an **overlay** or a **replacement patch**. Never call both simply “transparent anomaly art.”

### Type A — overlay-only

An overlay adds pixels without deleting or replacing the base object.

| Module | Anchor | Required companions |
| --- | --- | --- |
| Passive tentacle | door/floor | separate tentacle contact shadow |
| Ethereal tube | wall/ceiling seam | hard-edged local light band |
| Suspended paper | processing zone | separate arrived-on-floor shadows |

Delivery:

- Full-canvas `384 × 216` transparent PNG for zero-drift registration.
- Alpha cleaned manually.
- No baked room, desk, door, wall, or floor except pixels belonging to the overlay and its contact effect.
- Source groups separate body, highlight, contact shadow, and anomaly accent.

### Type B — replacement patch

A replacement patch changes or deletes pixels belonging to an existing object. It includes a fully opaque replacement of the affected base region.

| Module | Replaced base region | Patch must include |
| --- | --- | --- |
| Void lamp | lamp plus affected desk surface | complete lamp, absence circle, cropped paper edges, local shadow/light correction |
| Board bleed | noticeboard and wall strip beneath | complete board, clean memos, bleed trails, wall replacement, floor pool if reached |
| Occupied cabinet | complete filing cabinet footprint | full cabinet replacement, displaced folders, corrected cabinet contact shadow |
| Floor breach | affected tile cluster | complete replacement tiles, gaps, underside light bands, corrected desk/cabinet underlight |

Delivery:

- Full-canvas `384 × 216` PNG with transparency outside the replacement region.
- Replacement region is opaque wherever it must cover the clean plate.
- A matching binary coverage mask is delivered.
- Clean and altered patches use the same full-canvas origin.
- Never generate a black void on “transparent” and expect it to erase base pixels automatically; the patch and coverage mask own that replacement.

### Generation matte rule

ComfyUI references may use a flat extraction matte or a masked inpaint of the approved room. Production alpha and coverage masks are created during cleanup. The prompt alone is never trusted to produce them.

---

## 14. Anomaly module prompts

### Overlay: passive tentacle

```text
one matte charcoal tentacle resting passively through the approved back-left
door gap and across three floor tiles. Five to seven restrained pale-teal
sucker marks on the underside. One old rust-coloured contact stain. Heavy,
still, and bureaucratically ignored; never reaching or attacking. No second
creature, portal, room, desk, or decorative horror object.
```

### Overlay: ethereal tube

```text
one hard-banded translucent tube following the approved wall/ceiling seam,
entering architecture at both ends without source or destination. Pale teal
fluid moves in separated lit segments. Three restrained brass brackets.
One narrow hard-edged wall light band; no bloom, fog, web of pipes, cabinet
movement, second light spectacle, or secondary anomaly.
```

### Overlay: suspended paper

```text
three blank cream office forms frozen at different heights between the
processing zone and floor. Their three matching shadows have already reached
the linoleum. Forms and shadows are separate source groups. No readable text,
wind effect, portal, creature, floating desk object, or second anomaly.
```

### Replacement patch: void lamp

```text
the approved brass gooseneck lamp and its affected desk patch. The lamp casts
a hard circle of pure absence instead of light. One-native-pixel teal rim.
Desk and blank paper pixels crossing the boundary are cleanly cropped. One
three-pixel magenta rupture at the cord connection. No soft darkness, black
spotlight, detached clock, blank board, floor gap, creature, or second anomaly.
```

### Replacement patch: board bleed

```text
the approved noticeboard and wall strip as one replacement patch. Two dark
viscous trails emerge behind the frame and pool at the floor. Brown-black
matter with one restrained teal reflection cluster. Every blank cream memo
remains perfectly clean, squared, and pinned. No crooked memo, tentacle,
moving clock, cabinet event, portal, or secondary anomaly.
```

### Replacement patch: occupied cabinet

```text
the approved filing cabinet as one replacement patch. Middle drawer bows in
a shallow metal dome; two dark rounded pressure shapes with hairline teal
veins press through the upper gap. Three blank manila folders displaced onto
the floor. Bright stepped chrome handle. Patient structural pressure, never
bursting. No face, mouth, tentacle, board event, moving clock, or second anomaly.
```

### Replacement patch: floor breach

```text
the approved tile cluster beneath the dead zone as one replacement patch.
Controlled two- to four-pixel gaps separate several tiles. Hard electric-blue
light rises through the gaps and creates one stepped underlight band. One
magenta pixel in the widest gap. Nothing visible underneath. No creature,
tube, detached clock, blank board, or second anomaly.
```

---

## 15. Clock states

The clock remains in its fixed room position. Its states are not automatically added to scenes whose hero is another object.

```text
CLEAN: honest mid-tick; simple tick marks; shadow matches hands.

UNEASY / U-CLOCK ONLY: second-hand motion smear; shadow shows another time.

CONTAMINATED / CLOCK-HERO VARIANT ONLY: stopped; one crack; yellowed face.

IMPOSSIBLE / CLOCK-HERO VARIANT ONLY: detached two pixels ahead of its shadow.

FUGUE: base clock remains singular; any teal disagreement comes only from
the derived full-room ghost layer, not a separately generated second clock.
```

Do not stack the clock’s anomaly state onto a lamp, floor, cabinet, tube, board, or tentacle scene.

---

## 16. ComfyUI execution

### Inspect the graph

Record the actual checkpoint and text encoders. Flux/T5 and SDXL/CLIP workflows handle prompt length differently. Do not assume one universal token limit. Compress prompts only after inspecting the loaded graph.

### Lock composition

- Use the approved Clean Populated Baseline as the camera/composition guide.
- Start Canny or depth ControlNet testing at `0.4–0.6` strength.
- This is a starting range, not doctrine.
- Reject any result that moves the desk silhouette, claimant anchor, contact line, door, board, clock, cabinet, or ceiling seam.

### Keep the ladder coherent

- Same approved guide.
- Same seed where the workflow permits.
- Same canvas and camera.
- Change only tier treatment and one selected hero module.
- Fugue ghost is derived, not independently generated.

### Record every candidate

- Workflow filename and revision.
- Checkpoint and text encoders.
- Positive and negative prompt files.
- Seed.
- Canvas and latent dimensions.
- Sampler, scheduler, steps, CFG/guidance, denoise.
- Composition control image and provenance.
- ControlNet/IPAdapter model, preprocessor, strength, start, and end.
- Matte/mask method.
- Palette and colour count.
- Scale method.
- Manual cleanup status.

### Pixel finish

Palette transfer, quantisation, and nearest-neighbour scaling are intermediate treatments. They do not replace:

- silhouette correction;
- anatomy correction;
- manual cluster cleanup;
- removal of generated microtexture;
- alpha/matte cleanup;
- replacement coverage masks;
- layer separation;
- 1× native readability review;
- production redraw by the artist.

---

## 17. Text and UI

- Generated references contain no readable words, letters, claim numbers, or clock numerals.
- Use blank ruled forms, abstract stamp blocks, simple clock tick marks, and block CRT fields.
- Real copy is added through controlled production art or Unity UI.
- CorpOS remains a separate paper-bureaucracy interface layer and is never baked into the environment plate.

---

## 18. Delivery checklist

### Environment

- Layered source at 384 × 216.
- Clean plate with claimant removed but chair retained.
- Desk foreground separated for claimant occlusion.
- Door, noticeboard, clock, cabinet, and lighting independently addressable.

### Claimants

- Native 128 × 128 cells.
- Pivot local `(64,112)` for every species/state.
- Room anchor `(192,112)` top-origin.
- Separate fixed contact shadow and any state-specific foot shadow.
- Four Pending species plus complete nine-state Moth sheet.
- 1× native sheet and 4× nearest-neighbour preview.

### Anomalies

- Every module labelled `Overlay` or `ReplacementPatch`.
- Full-canvas 384 × 216 exports for registration.
- Clean alpha for overlays.
- Opaque replacement region plus binary coverage mask for patches.
- Exactly one hero anomaly per review image.

### Unity-facing metadata

- Point filtering.
- Mipmaps off.
- Compression none.
- Clamp wrap.
- NPOT scaling none.
- Consistent Pixels Per Unit.
- Integer transforms only.
- State names, pivots, timings, and coverage-mask mappings supplied.

---

## 19. Final verification gate

The Codex is approved only when all answers are “yes”:

1. Does the baseline show one continuous desk and one physical claimant opposite the player?
2. Are camera, desk edge, claimant cell, pivot, contact line, and shadow numerically locked?
3. Are Gel and the other species visibly non-human rather than humans with cosmetic effects?
4. Does Smug preserve the GDD feet-on-desk behaviour?
5. Does every scene contain exactly one impossible event?
6. Does Fugue derive its teal ghost from one room rather than generate duplicate furniture?
7. Is every anomaly classified as an overlay or replacement patch?
8. Are readable text and false transparency removed from the generation assumptions?
9. Can all required layers be replaced in Unity without repainting the whole room?
10. Does the scene remain readable at native 1× scale?

## Build order after approval

1. Generate and validate the Clean Populated Baseline.
2. Produce the clean registered environment plate.
3. Validate four Pending claimant layers in the shared footprint.
4. Validate Moth’s nine states and one fidget sequence.
5. Produce one overlay anomaly and one replacement-patch anomaly.
6. Composite the sanity ladder from the same room.
7. Complete remaining claimant and equipment states.
8. Import and wire only the approved production layers in Unity.

No later step compensates for a failed earlier gate.
