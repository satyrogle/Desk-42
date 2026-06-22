# Project Context

## Product identity

**Desk 42** is a retro-futuristic corporate-surrealism game presented here through a technical portfolio and evidence site.

- Genre: Systemic Desk Simulator / Bureaucratic Dark Comedy
- Core aesthetic: mid-century analogue technology, mundane office stress, brutalist control systems, pixel art, stealth tension, and cosmic horror
- Presentation goal: a playable-looking control system with cinematic scenes, mission-panel flow, and truthful technical evidence
- Reference qualities: graphic punch and clarity associated with *NOT A HERO*; silhouette, lighting, and tension associated with *Mark of the Ninja*. Do not copy assets or layouts.

## Current pages

- `portfolio/index.html` — dossier-style landing page and portfolio selector
- `portfolio/architecture.html` — systems architecture evidence page
- `portfolio/machine.html` — full-screen Three.js narrative machine page

## Main shared files

- `portfolio/tokens.css` — visual tokens and themes
- `portfolio/eng.css` — engineering-system base styles
- `portfolio/pixel.css` — pixel-art and scene layer
- `portfolio/pixel.js` — boot, HUD, console, toasts, interaction
- `portfolio/motion.js` — reveal, parallax, counters, sweep behaviour
- `portfolio/tools/genart.py` — Pillow/Bayer-dither pixel-art generator
- `portfolio/assets/vendor/three.min.js` — local Three.js build

## Current raster scenes

- `assets/pixel/operator-night.png`
- `assets/pixel/bureau-vista.png`
- `assets/pixel/anomaly-corridor.png`
- `assets/pixel/control-room.png`

## Verified current architectural counts

The division roster must preserve these verified counts:

| Division | Scripts |
|---|---:|
| Core | 5 |
| UI | 11 |
| BehaviourTrees | 8 |
| BSM | 8 |
| OfficeSupplies | 7 |
| Archetypes | 7 |
| Cards | 5 |
| RedTape | 3 |
| Persistence | 3 |
| Narrative | 3 |
| MoralInjury | 3 |
| Claims | 3 |
| Encounter | 1 |
| **Total** | **67** |

Do not invent dependency data. Extract it from the current markup/source or verified project evidence already present in the repository.

## Current rendered strengths

- Strong landing and architecture heroes
- Real raster pixel-art assets now exist
- Night Shift and Archive Shift have distinct identities
- Core Systems is already an interactive console and is the best reusable interaction pattern
- Dossier states and verified/unverified markers are effective
- Responsive layouts avoid document-level overflow on the main pages

## Confirmed defects before Pass A

- `.rating__fill` is inline and rating fills do not render
- `machine.html` can fail completely when WebGL initialisation throws
- `machine.html` overflows desktop width
- its render loop does not respect reduced motion or page visibility correctly
- `machine.html` has five `<h1>` elements
- major scene titles are styled `<div>` elements, creating heading-level skips
- achievement toasts can stack and cover content
- README, manifest, and validation claims are stale

## Confirmed authored-content gaps before Pass B

- the 13-division roster remains a long card grid
- mobile architecture page is excessively long
- pixel-art scenes are coherent but under-detailed
- cosmic corruption is too light and does not progress meaningfully
- some later sections still resemble styled documentation rather than a game-world system

## Factual evidence boundary

The following may never be changed by fictional corruption:

- developer identity
- programme/university information
- engine and language details
- script and namespace counts
- dependencies
- technical descriptions
- verification state
- accessibility or validation claims

Corruption belongs only in fictional operator telemetry, machine state, environmental signage, decorative HUDs, and clearly diegetic labels.
