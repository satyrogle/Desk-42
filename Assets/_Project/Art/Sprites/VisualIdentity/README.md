# Desk42 Visual Identity Test Sprites

These transparent PNGs are deterministic slices of the curated ComfyUI concept sheets. No pixels were regenerated or smoothed.

## Props

- `Props/coffee.png`
- `Props/pen_holder.png`
- `Props/papers.png`
- `Props/crumpled_paper.png`

## Claimant fidget test

- `Claimants/claimant_moth_fidget.png`

## Unity import lock

- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Pixels Per Unit: 128
- Filter Mode: Point
- Mip Maps: Off
- NPOT Scale: None
- Compression: None
- Wrap Mode: Clamp
- Alpha Is Transparency: On

The claimant fidget sprite uses a bottom-center pivot so rotation and lean tests stay anchored at the torso base. Prop sprites use Unity's centered single-sprite pivot.

The source concepts were nearest-neighbour upscaled 4x. `128 PPU` therefore maps to `32 logical pixels per Unity unit` while retaining the crisp 4x delivery pixels.
