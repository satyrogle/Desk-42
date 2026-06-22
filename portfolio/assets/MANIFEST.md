# Desk 42 — Asset Manifest

## Pixel-art scenes (`assets/pixel/`)

All listed files exist in the current project and are served locally. The committed generator is `tools/genart.py`; do not reference `tools/genroom.py` unless a real generator is added in a later change.

| File | Dimensions | Bytes | Used in | Purpose |
|---|---:|---:|---|---|
| `assets/pixel/anomaly-corridor.png` | 256×144 | 25,973 | index.html anomaly beat | Filing corridor with violet anomaly bloom. |
| `assets/pixel/bureau-vista.png` | 256×144 | 26,731 | index.html hero | Bureau floor vista for the landing-page stage. |
| `assets/pixel/control-room.png` | 320×160 | 40,719 | machine.html WebGL fallback / control-room scene | Static 2D fallback for the Machine page when WebGL is unavailable. |
| `assets/pixel/operator-night.png` | 256×144 | 28,791 | architecture.html hero | Night-shift operator desk with CRT lighting. |


## Vendor library

| File | Used in | Purpose |
|---|---|---|
| `assets/vendor/three.min.js` | `machine.html` | Local Three.js build used by the WebGL machine scene. |

## Inline SVG and CSS art

Interface emblems, seals, scene motifs and diagrams are inline SVG or CSS effects inside the HTML/CSS files. Decorative SVG layers are marked with `aria-hidden` where they do not convey unique content.

## Fonts

The pages reference Google Fonts for Space Grotesk, IBM Plex Mono and Silkscreen and provide system fallbacks. No local font files are committed.

## Runtime dependency notes

`machine.html` loads `assets/vendor/three.min.js`, not `three.module.min.js`. If Three.js or WebGL fails, `machine.html` displays `assets/pixel/control-room.png` as a local fallback and keeps the narrative content available.
