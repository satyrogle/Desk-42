# ComfyUI art pipeline — Desk 42

How Claude drives **ComfyUI** to generate on-pillar art for the desk, and how those outputs land in the project. Mirrors the Brawler's Comfy workflow, retargeted to Desk 42's diegetic frame and folders. For *connecting* the server, see `comfy-mcp-setup.md`.

**Pillar reminder:** the UI *is* the world. Generated art is **set-dressing for a physical desk**, not HUD chrome. Every asset must read as a real object on a 1960s-corporate-surreal office desk.

## Where assets land
Generated art is imported under `Assets/_Project/Art/`:
- `Art/Sprites/` — desk objects, stamps, punch cards, client portraits, claim forms.
- `Art/UI/` — diegetic "interface" pieces (gauges, buttons styled as physical switches/dials).
- `Art/Materials/` — materials wrapping the above for URP.
- `Art/Shaders/` — hand-authored; Comfy feeds textures into these, not the other way round.

Keep ScriptableObject-referenced art stable: if a generated file replaces an existing sprite, **match the filename** so prefabs/SOs don't lose the reference (or update the reference deliberately).

## Diegetic style guide (bake into every prompt)
- **Era/look:** mid-century corporate surreal — Severance / Control / Rocko's Modern Life. Muted institutional palette (manila, fluorescent green-white, oxidized teal, coffee-stain browns), worn paper, bakelite, brushed metal.
- **Type:** monospaced typewriter (Courier / American Typewriter); the Daily Memo uses a mid-century masthead. Type **degrades as Moral Injury rises** — generate clean *and* degraded variants where text appears.
- **Framing:** flat or shallow top-down/3-quarter desk angle, consistent light direction (warm key from upper-left at high Sanity). Objects sit ON a surface — include contact shadow unless the importing material adds it.
- **Output hygiene:** transparent background (RGBA PNG) for any object that sits on the desk; solid only for full-surface backplates.

## Distortion-Scale-aware prompt tiers
Art must stay coherent with the **same five Sanity tiers** the audio/visuals use (architecture.md). When generating tiered variants of an object (or the desk itself), modulate the prompt by tier so the visual distortion tracks Sanity:

| Sanity tier | Prompt modifiers |
|---|---|
| 100–75% (clean) | crisp geometry, warm even light, tidy, accurate type, subtle wear |
| 74–50% | faint fluorescent flicker, longer shadows, slight asymmetry, paper curling |
| 49–25% | leaning geometry, yellow-green color cast, smudged/loosening type, grime |
| 24–1% | full Rocko distortion: impossible angles, melted edges, glyph-swapped text, hostile shapes |
| 0% (Fugue) | desaturated, high-contrast flicker, fragmented/detached UI pieces, near-monochrome |

Generate distortion variants as **separate sprites/textures** (e.g. `coffee_mug_t0.png` … `coffee_mug_t4.png`) so the runtime can swap/blend by tier, matching how `Sanity` drives audio. Don't bake one mid-distortion look as the only asset.

## Workflow conventions
- **Naming:** `snake_case`, `category_subject[_tN][_variant].png` (tier suffix `_t0`..`_t4` only when tiered). No spaces.
- **Resolution / PPU:** author at 2× target on-screen size; set a consistent Pixels-Per-Unit per category so desk objects share scale. Record the chosen PPU here once decided. `<set PPU>`
- **Process:** generate in ComfyUI -> review against this guide (screenshot, don't reason blind) -> place in the right `Art/` subfolder -> let Unity import -> set sprite import (below) -> wire into prefab/SO via MCP or inspector.

## URP / Unity import settings (per generated sprite)
- Texture Type: **Sprite (2D and UI)**; Alpha Is Transparency: on.
- Mesh Type: Tight (or Full Rect for slice-able UI); Wrap: Clamp; Filter: Point for crisp pixel objects / Bilinear for soft.
- Compression: high quality for portraits/forms; crunch for bulk SFX-tier props.
- Pixels-Per-Unit: the category constant above.
- For 9-sliced diegetic UI (forms, panels): Full Rect + set borders.

## Guardrails
- **Stay diegetic** — if a generated asset looks like a floating game-HUD element, reject it.
- **Tier coherence** — distortion in art must track the same Sanity tiers as audio; don't let them drift.
- **Don't overwrite referenced art blindly** — check what a sprite is wired to before replacing it.
- **Originality** — generate original art; don't prompt for or reproduce specific existing artists'/studios' copyrighted work.
