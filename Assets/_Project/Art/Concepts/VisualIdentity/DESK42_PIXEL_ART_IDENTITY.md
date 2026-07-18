# Desk42 Pixel-Art Visual Identity v1

## One-line direction

Desk42 is a deadpan 1990s pixel-art office game about an institution that looks dependable until its paperwork, geometry, and clients reveal the cosmic machinery underneath.

The visual tension is **analog authority versus impossible reality**. The office should initially feel calm, legible, and faintly comforting. Horror arrives by corrupting systems the player already understands: forms misalign, familiar props become the wrong size, walls lean, and anomaly colors break the office palette.

## Status of the concept images

The PNGs in `Mockups/` are AI-assisted ComfyUI art-direction references. They are not shippable sprites and should not be traced blindly. Production art should be redrawn on the agreed pixel grid, cleaned for silhouette and animation, and made consistent across every state.

## Four identity pillars

1. **Analog authority** - cream laminate, Bakelite controls, chrome mechanisms, CRT glass, punched paper, rubber stamps, brass lamps.
2. **Paper is the world** - forms are not decoration. Paper defines task state, spatial zones, hierarchy, progress, and failure.
3. **Controlled intrusion** - anomalies use a tightly rationed teal, electric blue, and glitch-magenta family. They must feel foreign to the base office.
4. **Hostile geometry** - low sanity distorts an existing composition instead of replacing it with visual noise. Crooked baselines, leaning walls, wrong scale, and detached UI are more effective than generic glitch effects.

## Shape language

- Base office: broad rectangles, rounded industrial corners, horizontal bands, solid feet, and visibly mechanical hinges.
- Paper and UI: strict columns, ruled lines, punched corners, stamps, tabs, and modular blocks.
- Characters: one dominant readable silhouette per claimant, with one impossible feature carrying the anomaly.
- Anomalies: thin crescents, impossible gaps, displaced outlines, non-Euclidean folds, and sharply angled intrusions.
- Keep important interactables readable at native resolution. If a prop only works when enlarged, simplify it.

## Core palette

### Office - healthy

| Role | Hex | Usage |
| --- | --- | --- |
| Institutional deep green | `#173F32` | Walls, desk framing, major UI chrome |
| Paper cream | `#F1E8CE` | Forms, primary readable surfaces |
| Dusty blue-grey | `#8FA9A6` | Cool shadows and secondary metal |
| Warm grey | `#77736A` | Neutral structure and disabled controls |
| Muted orange | `#CE713A` | Selected state, task focus, warm accent |
| Approval red | `#B73B32` | Stamps, destructive or final actions |

### Office - degraded

| Role | Hex | Usage |
| --- | --- | --- |
| Yellowed paper | `#D8C58B` | Forms and lights after prolonged exposure |
| Rust orange | `#A44E2D` | Corroded structure and warning buildup |
| Sickly green | `#7D8E3C` | Fluorescent contamination |
| Soot brown | `#332C30` | Deepest degraded shadow |

### Anomaly accents

| Role | Hex | Usage |
| --- | --- | --- |
| Saturated teal | `#20D6C7` | Primary impossible energy or hidden truth |
| Electric blue | `#4AA7FF` | Secondary charge and scan response |
| Glitch magenta | `#D447A7` | Rare rupture, never a general UI accent |

### Meta hub

| Role | Hex | Usage |
| --- | --- | --- |
| Warm wood | `#6A402B` | Human refuge and long-term progression |
| Brass | `#B68849` | Persistent tools, trim, and earned upgrades |
| Amber | `#E2A552` | Safe practical light |

Use a rough **70 / 20 / 8 / 2** distribution in healthy scenes: 70% green/cream structure, 20% neutral supporting colors, 8% task accent, and at most 2% anomaly color. Magenta should be the rarest color in normal play.

## Pixel construction rules

- Compose gameplay at a 16:9 native reference grid such as `320x180` or `384x216`, then scale by whole integers only.
- Use point filtering. Never blur, rotate by arbitrary angles, or apply fractional transform scale to production sprites.
- Use hard clusters rather than single-pixel confetti. Reserve isolated pixels for sparks, dust, eyes, and one-frame anomaly events.
- Prefer 12-24 colors in a single prop or scene family. Share ramps across assets.
- Default outline is one native pixel with selective colored edges on lit sides. Avoid a uniform black sticker outline.
- Light comes from upper-left fluorescents in the office. Use two shadow steps: form shadow and contact shadow.
- Reflections on chrome, CRT glass, and Bakelite should be short stepped bands, not smooth gradients.
- Texture is sparse and purposeful: paper fibres, laminate chips, scanlines, and rust each get a small controlled pattern.

## Recommended asset scales

| Asset | Native working size | Notes |
| --- | --- | --- |
| Small desk prop | `24x24` to `32x32` | Strong single-action silhouette |
| Interactive desk prop | `48x48` to `64x64` | Include idle, hover/selected, and action states |
| Claim document | `48x64` or modular pieces | Separate paper, stamps, clips, and anomaly overlay |
| Claimant portrait | `96x96` or `128x128` | Bust framing, 3-5 expression variants |
| CorpOS icon | `16x16`, `24x24`, `32x32` | Redraw per size rather than scaling one master |
| Full desk plate | Native gameplay grid | Separate desk, props, papers, lighting, and anomaly layers |

## Desk composition

The desk is a stage with five immediately readable zones:

1. **Inbox** - new claims arrive from the upper-left.
2. **Processing** - the central work area and strongest pool of light.
3. **Outbox** - completed or rejected claims move to the upper-right.
4. **Personal** - mug, photo, notes, and upgrades humanise one corner.
5. **Dead Zone** - an intentionally empty region that anomalies can occupy.

The healthy layout must read in under a second. Degraded states preserve those anchors so the player notices what has changed.

## Dual-use prop language

Every hero prop needs an office read and an anomaly read:

| Prop | Office function | Anomaly function | Visual state cue |
| --- | --- | --- | --- |
| Stapler | Fastens claim packets | Weapon or restraint | Jaw opens too far; approval red moves into shadow |
| Desk lamp | Reveals faint print | Containment beam | Brass neck stiffens; teal cone replaces amber pool |
| Coffee mug | Temporary focus buff | Eavesdropping vessel | Reflection becomes an eye or waveform |
| Shredder | Destroys documents | Creates confetti barriers | Paper stream changes direction or ignores gravity |
| Rubber stamp | Final approval/rejection | Seals a reality state | Red mark gains teal displaced echo |
| Punch-card machine | Routes claim logic | Opens impossible workflow branches | Card slots multiply or misalign |

## Claimant portrait rules

- Use a consistent corporate ID-photo crop so species differences do the work.
- Give each claimant one silhouette hook, one material hook, and one behavioural hook.
- Keep eyes and mouth readable, but do not make every anomaly a face full of eyes.
- Normal attire should be sober: cream shirts, institutional green jackets, muted orange ties or badges.
- The impossible feature may break the frame only at high anomaly intensity.

## CorpOS and forms

CorpOS should look like paper bureaucracy translated literally into a CRT, not a modern desktop skin.

- Windows are forms: title strip, ruled body, stamp/status corner, and punched navigation tabs.
- Primary background is cream; structural chrome is deep green; selection is muted orange.
- Teal and electric blue indicate something the office cannot classify. Magenta indicates rupture.
- Use thick rectangular focus states and cursor blocks. Do not rely on glow alone.
- Keep 9-slice corners and edge pieces on the pixel grid.
- Avoid modern glass panels, pill buttons, soft drop shadows, and neon cyberpunk framing.

Typography direction:

- Corporate headings: geometric grotesk inspired by Futura, Helvetica, or Eurostile.
- Forms and machine output: monospaced typewriter or terminal face inspired by Courier and CRT glyph sets.
- Daily Memo: compact 1960s newspaper masthead.
- Production requires licensed fonts or a custom bitmap alphabet. AI-generated lettering in concept images is placeholder noise and must never ship.

## Sanity distortion ladder

| Range | Art treatment |
| --- | --- |
| `100-75` | Correct perspective, stable warm fluorescent light, strict baselines, clean paper |
| `74-50` | Occasional flicker, longer shadows, slight asymmetry, one displaced pixel edge |
| `49-25` | Leaning walls, yellow-green cast, crooked forms, props subtly wrong in scale |
| `24-1` | Impossible gaps, detached UI blocks, strong desaturation at edges, teal/blue intrusion, rare magenta rupture |
| `0` | Fugue: near-desaturated scene, intermittent visibility, foreground and UI no longer agree on position |

Degradation should remain playable. Never hide task-critical state behind noise, chromatic aberration, or continuous full-screen flicker.

## Animation language

- Healthy office: 6-10 fps stepped animation, small mechanical anticipation, solid impacts.
- Paper: two or three decisive folds or flaps; no fluid cloth simulation.
- CRT: one-pixel scan and short sync slip, not a constant distortion filter.
- Characters: limited holds with specific fidgets. One strange motion is stronger than perpetual wobble.
- Anomalies: break the established cadence with missing frames, one-frame displacements, reverse motion, or impossible holds.

## Do / do not

Do:

- Make the ordinary office attractive enough that its corruption matters.
- Reuse shared ramps and materials across desk, UI, and portraits.
- Test every interactable at native size and in grayscale.
- Preserve strong negative space around papers and props.
- Let anomaly color identify real mechanical meaning.

Do not:

- Turn the base game into generic neon cyberpunk.
- Use smooth gradients, soft bloom, or sub-pixel UI.
- fill every surface with texture or glitch noise.
- Use magenta for ordinary selection states.
- Treat the generated mockups as finished sprite sheets.

## Artist delivery checklist

- Source files: layered `.aseprite`, `.psd`, or equivalent with named groups.
- Exported PNG sprites with transparency and no resampling.
- Palette file plus hex reference.
- Sprite sheets accompanied by frame size, pivot, timing, and state names.
- Separate base, shadow, highlight, paper, and anomaly layers for hero desk assets.
- CorpOS 9-slice dimensions and state map.
- Portrait expression/state map.
- Font files and license information.
- One native-resolution sheet and one integer-scaled preview sheet for review.
- Verify Unity import with Sprite texture type, Point filter, no mip maps, no lossy compression, and consistent Pixels Per Unit.

## Naming convention

Use `D42_[Category]_[Name]_[State]_[Size]_v###.png`.

Examples:

- `D42_Prop_Stapler_Idle_48_v001.png`
- `D42_Prop_Stapler_Anomaly_48_v001.png`
- `D42_UI_ClaimForm_Normal_9Slice_v001.png`
- `D42_Portrait_MothAccountant_Worried_128_v001.png`

## Unity editor integration

Open `Tools > Desk 42 > Visual Identity Board` in Unity to view the curated pack without adding concept art to a gameplay scene. The dockable window provides environment, CorpOS/props, claimant, palette, and production tabs plus asset-selection shortcuts.

The companion menu command `Tools > Desk 42 > Visual Identity > Apply Pixel Import Settings` applies Point filtering, disables mip maps and NPOT scaling, and uses uncompressed texture import for the 12 curated reference textures.

## Review questions

Before approving any production sheet, ask:

1. Does it read at native size without explanation?
2. Does it belong to the healthy office palette before anomaly accents are added?
3. Is the impossible element specific to gameplay, or merely decorative noise?
4. Can a degraded version be made by corrupting the same visual grammar?
5. Are pivots, states, layers, and export rules clear enough for Unity integration?
