# Desk42 ComfyUI Generation Notes

This concept pack was generated against the local ComfyUI backend on 18 July 2026. It used installed checkpoints and standard local nodes only. No paid/API nodes, downloads, or custom-node installs were used.

## Pixel treatment graph

The keeper images use this core graph:

1. `CheckpointLoaderSimple`
2. `CLIPTextEncode` positive and negative prompts
3. `EmptyLatentImage`
4. `KSampler`
5. `VAEDecode`
6. `ImageScale` using `area` to one-quarter resolution
7. `ImageQuantize` using 24-28 colors and `bayer-4` dithering
8. `ImageScale` using `nearest-exact` back to delivery size
9. `SaveImage`

Generation defaults were 26-28 steps, CFG 7.0-7.5, `dpmpp_2m`, and `karras`.

## Curated source map

| Curated file | Checkpoint | Seed | Notes |
| --- | --- | ---: | --- |
| `D42_Mockup_Desk_Core_v001.png` | `Juggernaut-XL_v9.safetensors` | `3846749107` | Strong single-desk composition from the exploratory pass, then reprocessed through ComfyUI's pixel graph |
| `D42_Mockup_Office_Healthy_v001.png` | `sd_xl_base_1.0.safetensors` | `4242001` | Healthy office overview |
| `D42_Mockup_Office_Degraded_v001.png` | `sd_xl_base_1.0.safetensors` | `4242001` | Prompt-paired degraded office overview |
| `D42_Mockup_CorpOS_v001.png` | `sd_xl_base_1.0.safetensors` | `4242002` | Interface grammar reference |
| `D42_Mockup_PropLanguage_v001.png` | `sd_xl_base_1.0.safetensors` | `4242003` | Shape and material exploration, not a production inventory sheet |
| `D42_Mockup_ProcessingStation_v001.png` | `sd_xl_base_1.0.safetensors` | `4242010` | Close processing-machine mood reference |
| `D42_Claimant_MothAccountant_v001.png` | `sd_xl_base_1.0.safetensors` | `4242011` | Keeper claimant direction |
| `D42_Claimant_GelAnomaly_v001.png` | `sd_xl_base_1.0.safetensors` | `4242012` | Keeper anomaly/material direction |
| `D42_Claimant_UnregisteredAlien_v001.png` | `sd_xl_base_1.0.safetensors` | `4242021` | Keeper non-human silhouette direction |
| `D42_Claimant_VoidProxy_v001.png` | `sd_xl_base_1.0.safetensors` | `4242022` | Keeper hard-mask/teal-rim direction |

`D42_Mockup_Claimants_v001.png` and `D42_VisualIdentity_ContactSheet_v001.png` are labeled presentation boards assembled from the curated ComfyUI images. No generative editing was added to those boards.

## Important production note

These images are visual-development references. Generated text, object count, anatomy, perspective, and exact palette usage are not authoritative. Final assets should be redrawn on the production pixel grid and checked against `DESK42_PIXEL_ART_IDENTITY.md`.

## Locked v3.1 clean baseline

The v3.1 Clean Populated Baseline was rebuilt on 20 July 2026 after the art direction lock. It does not use a generative room layout as the keeper: repeated diffusion passes introduced duplicate furniture, windows, or unreadable anchors and were rejected during curation.

The keeper uses the approved `D42_Mockup_DeskStage_ClientFacing_v003.png` foreground, extracted through ComfyUI's local `birefnet-general.safetensors` background-removal node. The room shell is fixed numerically at 384x216 so the door, noticeboard, clock, fluorescent, wall/floor seam, filing cabinet, desk, claimant, and player chair cannot drift. ComfyUI performs the registered foreground-mask composite. The result is then remapped without dithering to the locked palette in `tools/pixel_art_finalize.py` and previewed only with nearest-neighbour scaling.

| Curated file | Purpose |
| --- | --- |
| `D42_CleanRoomPlate_Native_384_v001.png` | Claimant-free clean room plate and fixed contact-shadow treatment |
| `D42_CleanRoomPlate_Preview4x_v001.png` | Nearest-neighbour review image for the room plate |
| `D42_CleanPopulatedBaseline_Native_384_v001.png` | Rejected clean Moth Accountant composition reference retained for layout history |
| `D42_CleanPopulatedBaseline_Preview4x_v001.png` | Nearest-neighbour review image for the populated baseline |

The clean baseline contains no anomaly state. The clock, board, cabinet, light, and door are all normal anchors. It was rejected as an art target because the background and detailed foreground did not form one coherent material style. Unity runtime wiring must not treat it as approved production art.
