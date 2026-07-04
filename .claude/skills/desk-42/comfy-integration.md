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

## Reference-guided generation (ControlNet) — use this for any *specific* prop
Pure text2img on SDXL **base** hallucinates specific bureaucratic props (it once turned "rubber approval stamp" into a wax-seal blob). For anything that has to read as a particular real object, **drive it from a reference image via ControlNet** — don't trust text alone.

**Sourcing references (keep it clean for a shipped game):**
- Prefer **CC0 / public-domain** images, or Jacob's own photos. Wikimedia Commons + Openverse are good sources; query the Commons API directly (no scraping HTML):
  `https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrsearch=<terms>&gsrnamespace=6&prop=imageinfo&iiprop=url|extmetadata&iiurlwidth=1024&format=json` (send a real `User-Agent`). Read `extmetadata.LicenseShortName`; favour `CC0`/`Public domain`.
- Use the reference for **structure (silhouette/edges), not style** — ControlNet-from-a-product-photo for shape is low-risk; "in the style of [living artist]" is the thing to avoid. Record the license of whatever you use.

**Pipeline (all built-in nodes + your `controlnet-union-sdxl-promax`):**
`LoadImage(ref)` → `Canny`(low ~0.25 / high ~0.7) → `ControlNetLoader(controlnet-union-sdxl-promax.safetensors)` → `ControlNetApplyAdvanced{positive,negative,control_net,image,strength,start_percent,end_percent,vae}` → SDXL `KSampler` (28 steps, dpmpp_2m/karras) → `VAEDecode` → `SaveImage`.

**Tier modulation:** lower ControlNet **strength + end_percent** as the tier worsens, so distortion can take over while the object stays recognizable: t0 ≈ `strength 0.85 / end 0.90` → t2 ≈ `0.60 / 0.78` → t4 (Fugue) ≈ `0.32 / 0.58`. Keep the same reference + seed across tiers for a coherent set.

**Transparent cutout (built-in birefnet):** `RemoveBackground{image, bg_removal_model=LoadBackgroundRemovalModel("birefnet-general.safetensors")}` → **`InvertMask`** (RemoveBackground's MASK is background-polarity — invert it) → `JoinImageWithAlpha{image, alpha}` → `SaveImage`. Needs `background_removal: models/background_removal` in `extra_model_paths`.

**Gotchas:** `LoadImage` reads from the **install's** input dir (`...\ComfyUI\ComfyUI\input`), not `ComfyUI-Shared\input`. `extra_model_paths` is read once at startup — restart after changing it. *(Validated 2026-06-13: CC0 date-stamp ref → recognizable `stamp_t0..t4` + cutouts.)*

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
