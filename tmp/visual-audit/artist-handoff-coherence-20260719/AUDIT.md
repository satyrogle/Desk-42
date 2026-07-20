# Desk42 artist-handoff coherence audit

## Scope

Reference: `Desk42_Pixel_Art_Visual_Identity_v1.pdf` (nine pages).

Runtime evidence:

- `10-unity-healthy.png`
- `11-unity-low-sanity.png` (rejected overlay)
- `12-unity-pdf-aligned-low-sanity.png` (overlay removed)

## Page-by-page findings

1. **Cover / system summary - healthy reference.** The PDF establishes one coherent family: analog office, paper, sober claimant silhouettes, CorpOS forms, and a strict 70/20/8/2 color budget. The runtime borrows the colors but not the full family discipline.
2. **Direction and pillars - healthy reference.** The degraded office must retain the healthy office's grammar and corrupt its reality. The rejected runtime overlay added unrelated foreground objects instead.
3. **Palette - healthy reference.** The healthy runtime broadly matches deep green, paper, task orange, and approval red. The rejected low-sanity frame spends too much soot and teal, breaking the 8% anomaly-color limit.
4. **Desk as stage - mixed runtime fit.** The PDF defines inbox, processing, outbox, personal, and dead zones with useful empty space. The runtime is more front-on, the claim sheet dominates the left half, and the card tray dominates the bottom; the zones no longer read as one physical desk workflow.
5. **Props and materials - strongest runtime match.** CRT, telephone, pen holder, filing cabinet, paper stack, cream surfaces, dark outlines, and short shadows belong to the same material family. These are the elements worth preserving.
6. **Claimant silhouettes - structural failure.** The room plate contains a baked Moth Accountant, so generated Gel, Void, and Alien claims can display the wrong species. The corrected capture visibly shows a Gel claim with the Moth claimant. State and species variation cannot work until the room plate and claimant layer are separated.
7. **CorpOS and typography - partial match.** Cream forms, deep-green structure, red decisions, and rectangular controls fit. Card labels, spacing, and mixed type treatments are less disciplined than the form-based CorpOS reference.
8. **Sanity ladder - rejected implementation.** The PDF calls for flicker, longer shadows, slight asymmetry, leaning geometry, wrong scale, displaced baselines, sparse teal, and rare magenta. The rejected overlay drew tentacles, drips, tubes, and opaque pools as new cartoon objects. It has been removed from `Shift.unity`.
9. **Production handoff - decisive pipeline issue.** The PDF explicitly marks ComfyUI images as concept references that must be redrawn, cleaned, layered, and validated. The runtime currently uses a concept mockup directly as its full-screen room plate. That is why it retains an AI-generated feel even with Point filtering.

## Highest-impact corrections

1. Stop treating the ComfyUI desk mockup as a shippable gameplay background; keep it as composition reference only.
2. Commission or produce a clean native-pixel room plate at 320x180 or 384x216 with separate desk, paper, lighting, claimant, and anomaly layers.
3. Require transparent seated claimant layers for Moth, Gel, Alien, and Void in one shared footprint; never bake a species into the room.
4. Replace literal anomaly creatures with authored variants of known geometry: shifted wall seams, longer prop shadows, crooked paper baselines, wrong-scale forms, sparse edge displacement, and rare rupture pixels.
5. Rebalance the gameplay layout around the five desk zones and reduce the claim/card UI's visual takeover.

## Evidence limits

This is a screenshot and handoff-document audit. Motion smoothness, keyboard focus, screen-reader behavior, and actual low-sanity animation timing require separate runtime testing.
