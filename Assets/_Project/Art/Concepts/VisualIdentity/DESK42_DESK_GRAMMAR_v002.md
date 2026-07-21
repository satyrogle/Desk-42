# Desk 42 desk grammar v002

The room is not the subject. The desk is the stage. Every shot should preserve the same interaction map and only change state, lighting band, paper density, or anomaly color.

## Locked composition

| Zone | Fixed placement | Purpose |
| --- | --- | --- |
| Monitor / terminal | back-center, aligned to claimant slot | read / decide anchor |
| Coffee | back-left corner of desktop | human routine marker |
| Pen holder | back-right corner of desktop | administrative tool marker |
| Papers | front-right desktop | active queue / readable work |
| Crumpled paper | front-left desktop | discarded work / failure cue |
| Center desktop | kept mostly clear | primary interaction space |
| Claimant | seated upper-center, physically across the desk | social pressure / current case |
| Filing cabinet | far-right background | storage silhouette, never competes with desk |
| Chair | centered below desk cut-out | reinforces the player viewpoint |

## Visual grammar

- One dominant horizontal desk silhouette; no second desk in the same shot.
- Three value bands only: deep shadow, base plane, one highlight plane.
- 1-pixel near-black outline around interactable props; no soft edges or bloom.
- Warm paper/wood against cool teal architecture; cyan is reserved for anomaly feedback.
- Keep the center clear. Clutter accumulates only in the left discard lane or right queue lane.
- The claimant is always a physical visitor across the desk, never a wall portrait, screen image, or hologram.
- Maintain a direct player-to-claimant sightline; offset the CRT so it never covers the claimant's face or hands.
- State changes should be readable as object changes, not camera changes: paper stack height, coffee fill, monitor glow, portrait frame, and cyan anomaly marks.
- Use a fixed 3/4 orthographic camera and nearest-neighbor filtering for all references and in-game sprites.

## Room state ladder

The normal office always remains legible beneath the distortion. Low sanity corrupts
the known composition through light, perspective, scale, baselines and sparse interface
disagreement. It does not paste a collection of new creatures or props over the room.

| Sanity | Room read | Allowed changes |
| --- | --- | --- |
| 100–75 | Clean | Correct perspective, warm stable light, strict baselines, clean paper |
| 74–50 | Uneasy | Flicker, longer shadows, slight asymmetry, one displaced pixel edge |
| 49–25 | Contaminated | Leaning walls, yellow-green cast, crooked forms, subtly wrong scale |
| 24–1 | Impossible | Detached UI, gaps, desaturated edges, sparse teal intrusion, rare magenta rupture |
| 0 | Fugue | Near-desaturated scene; foreground and interface disagree on position |

- Preserve the desk, claimant and office silhouette at every tier.
- Spend anomaly teal on sparse signals and displaced edges, never large opaque shapes.
- Magenta is a rupture event, not an ordinary low-sanity decoration.
- Props remain readable and keep their established function until a specific dual-use state changes it.
- Distortion must never obscure the claim, cards, decision controls, claimant face or claimant hands.

## Claimant replacement contract

- Production separates the client-facing room into a clean background plate and a transparent seated claimant layer.
- Every claimant occupies the same upper-centre footprint, with face and hands inside the protected sightline.
- Species and BSM states replace that seated layer; they are not wall portraits, holograms, or framed UI cards.
- The current client-facing mockup contains a seated Moth Accountant as a composition placeholder. It is not the final interchangeable runtime plate.
- Preserve a stable desk edge, light direction, eye line, and hand-rest position across species and state frames so swaps do not make the room jump.

## Handoff intent

`D42_Mockup_DeskStage_ClientFacing_v003.png` is the composition authority.
`D42_Mockup_PropGrammar_v002.png` is the pixel-language authority for coffee, pen holder, papers, crumpled paper, and claimant portrait framing.
