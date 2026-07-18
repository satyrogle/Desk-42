# ComfyUI asset workflow

## Local setup

- Backend: `http://127.0.0.1:8188`
- Install root: `C:\Users\jacob\ComfyUI-Installs\ComfyUI`
- Entrypoint: `ComfyUI\main.py`
- Python: `comfy-env\Scripts\python.exe` (Python 3.12 / PyTorch CUDA build)
- Extra model paths: `ComfyUI\extra_model_paths.yaml`
- Shared models: `C:\Users\jacob\ComfyUI-Shared\models`
- Scratch output: `C:\Users\jacob\ComfyUI-Shared\output`
- Input folder used by `LoadImage`: `C:\Users\jacob\ComfyUI-Installs\ComfyUI\ComfyUI\input`
- MCP process: `npx -y comfyui-mcp@0.37.0`, started by Codex from `.codex/config.toml`

The installed checkpoints include `Juggernaut-XL_v9`, `RealVisXL_V5.0_fp16`, and `sd_xl_base_1.0`. The shared paths also contain SDXL ControlNet Union, IP-Adapter, CLIP Vision, SDXL VAE, and BiRefNet background-removal models. Discover live filenames and node availability through MCP before choosing a workflow.

## Desk 42 art direction

Treat the interface as a physical office desk, not a HUD.

- Use mid-century corporate surrealism: institutional manila, oxidized teal, fluorescent green-white, coffee browns, worn paper, bakelite, brushed metal, stamps, forms, switches, and dials.
- Use shallow top-down or three-quarter desk views and a consistent upper-left key light at high Sanity.
- Give desk objects readable silhouettes and contact shadows. Use transparent RGBA for isolated props.
- Keep forms and typewriter text structurally plausible. Do not rely on generated text for final legible copy; replace critical text in Unity or source artwork.
- Reject glossy game-UI chrome, floating HUD panels, generic fantasy/sci-fi props, and visual styles tied to named artists or productions.

## Sanity tier sets

Generate separate coherent variants when the runtime swaps art by Sanity:

| Suffix | State | Controlled modifiers |
|---|---|---|
| `_t0` | clean | Crisp geometry, warm even light, accurate form, subtle wear |
| `_t1` | unsettled | Faint fluorescent flicker, longer shadows, slight asymmetry, curled paper |
| `_t2` | degraded | Leaning geometry, yellow-green cast, smudging, grime, loosened type |
| `_t3` | hostile | Impossible angles, melted edges, swapped glyph shapes, aggressive silhouette |
| `_t4` | Fugue | Desaturated, high contrast, fragmented/detached pieces, near monochrome |

Hold composition, viewpoint, reference, and preferably seed stable across tiers. Change distortion strength deliberately so the object remains identifiable.

## Workflow choice

- Use high-level local image generation for exploratory props and mood assets.
- Use the Comfy MCP `list_skills` and `read_skill` tools to load its current model-family or workflow guidance when relevant. Do not copy Claude plugin caches into this repository.
- Use ControlNet for a specific bureaucratic prop or silhouette. Prefer Jacob's photos, CC0, or public-domain references and retain source/license notes.
- Use IP-Adapter for broad visual consistency from an owned/licensed reference, not to copy a particular artist.
- Use background removal for desk cutouts, then inspect alpha edges against light and dark backgrounds.
- Validate workflows before queueing. Check whether the graph is local, API-backed, mixed, or unknown; do not spend API credits without explicit authorization.
- Avoid model or node-pack downloads unless explicitly authorized.

For SDXL ControlNet tier sets, begin near `strength 0.85 / end 0.90` for `_t0`, around `0.60 / 0.78` for `_t2`, and around `0.32 / 0.58` for `_t4`, then adjust from observed results. These are starting points, not fixed project constants.

For BiRefNet cutouts, verify mask polarity before joining alpha; the installed workflow previously required an inverted mask. Node behavior can change, so inspect rather than copying that assumption blindly.

## Naming and handoff

- Keep scratch generations in the external output folder.
- Name curated files `snake_case` as `category_subject[_tN][_variant].png`.
- Import into the matching folder under `Assets/_Project/Art/{Sprites,UI,Materials,Shaders}`. Generated raster art normally belongs in `Sprites` or `UI`; `Materials` and `Shaders` are Unity-authored assets.
- Preserve a filename only for deliberate replacement after inspecting its current Unity references.
- Keep generation metadata: prompt, negative prompt, model, workflow, seed, dimensions, source reference/license, and selected output filename.
- Generate at roughly 2x the intended screen size when practical, then validate actual scale and texture memory in Unity.

## Acceptance gates

Before repository import, confirm:

1. The silhouette and function read at target size.
2. The asset is diegetic and consistent with neighboring Desk 42 art.
3. Alpha edges, shadows, lighting direction, and cropping are usable.
4. Tier variants remain the same object and progress coherently.
5. No accidental text, signatures, watermarks, branded marks, or reference leakage remain.
6. The selected output is viewed directly, not accepted from tool status alone.

After import, follow `unity-editor.md` for importer configuration, wiring, screenshots, and tests.
