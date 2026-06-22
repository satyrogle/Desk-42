# Desk 42 — Asset Manifest

## Pixel-art scenes (`assets/pixel/`)
All original, generated procedurally for this project via a Pillow script
(`tools/genart.py` in build history). Hand-authored composition + a Bayer-dither
lighting model. Project-owned; no third-party or copyrighted game assets used.
Logical resolution 256×144, upscaled in-browser with `image-rendering: pixelated`
(nearest-neighbour). Each carries descriptive `alt` text in the page.

| File | Scene | Used in | Bytes |
|---|---|---|---|
| `operator-night.png` | Operator at the night-shift desk; glowing CRT, moonlit window, watching figure | architecture.html hero | ~28 KB |
| `bureau-vista.png` | Bureau floor — rows of terminals receding to a cosmic window | index.html hero | ~26 KB |
| `anomaly-corridor.png` | Filing-archive corridor with a violet anomaly bloom | index.html anomaly beat | ~26 KB |

## Vector / SVG
Interface emblems, seals, scene motifs and the diagrams are inline SVG (no
external files). Stroke geometry, hard terminals; `role="img"` + labels where meaningful.

## Fonts (hosted, Google Fonts)
Space Grotesk (UI/body) · IBM Plex Mono (technical) · Silkscreen (pixel labels only),
with `system-ui`/`monospace` fallbacks. No local font files committed.

## 3D centerpiece
- `machine.html` — a real-time WebGL 3D scene (the Desk 42 "machine") rendered to
  a low-resolution buffer with 2×2 ordered-dither posterisation, so it reads as
  **animated pixel art**. Scroll-driven camera descent + drag-to-orbit; a violet
  "anomaly" beat near the bottom. Bold editorial type composited over the
  pixelated 3D (raster-3D → CSS type/vignette/scanline).
- `assets/vendor/three.module.min.js` — Three.js r160 (MIT), vendored locally.
- `tools/genroom.py` — control-room backdrop generator.
