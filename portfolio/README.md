# Desk 42 — Engineering Evidence Portfolio

Desk 42 is a three-page static portfolio for a solo-developed Unity roguelite: a systemic desk simulator and bureaucratic dark comedy presented as a retro-futuristic corporate-surrealist control system.

## Pages

- `index.html` — bureau entry terminal, hero scene, dossier selector, anomaly beat and evidence statement.
- `architecture.html` — systems-architecture evidence page with metrics, namespace roster, core-system console, integrity modules and cross-project evidence markers.
- `machine.html` — full-screen Three.js narrative machine page with scroll beats, drag-to-orbit WebGL scene and a static 2D fallback.

## Run locally

```bash
cd portfolio
python3 -m http.server 8000
```

Open:

- `http://127.0.0.1:8000/index.html`
- `http://127.0.0.1:8000/architecture.html`
- `http://127.0.0.1:8000/machine.html`

No build step is required.

## Themes

The site supports two local themes driven by `tokens.css` and the page toggle:

- **Night Shift** — dark, terminal-lit bureau interface.
- **Archive Shift** — aged institutional paper with preserved contrast and darker hero title treatment where needed.

The current theme is stored locally by the front-end scripts when available.

## JavaScript and CSS responsibilities

- `tokens.css` — design tokens, theme values, fonts and motion variables.
- `eng.css` — base engineering components and legacy token aliases used by older evidence components.
- `pixel.css` — game-chrome layer: hero/stage styling, scenes, dossiers, ratings, toasts, reveals and responsive art direction.
- `motion.js` — scroll reveal, safety sweep, counters, rating fills and parallax helpers.
- `pixel.js` — boot ritual, HUD values, queued achievement toasts, animation pausing and the control-room tab interaction.
- `machine.html` — self-contained page styles and Three.js scene setup for the 3D machine.

## WebGL, fallback and reduced motion

`machine.html` uses the vendored local `assets/vendor/three.min.js` file. It now checks for Three.js, WebGL renderer support, canvas context creation, renderer setup and scene setup failures. If WebGL cannot start, the page hides the canvas and displays `assets/pixel/control-room.png` as a semantic fallback figure while preserving the narrative text, navigation and scrolling.

When `prefers-reduced-motion: reduce` is active, the Machine page renders one static WebGL frame when possible instead of running a continuous animation loop. The fallback path remains static and readable. The loop also stops when the document is hidden and resumes only once when visible and continuous motion is allowed.

## Raster pixel-art assets

Current committed raster scenes:

| Asset | Dimensions | Purpose |
|---|---:|---|
| `assets/pixel/anomaly-corridor.png` | 256×144 | Portfolio scene plate |
| `assets/pixel/bureau-vista.png` | 256×144 | Portfolio scene plate |
| `assets/pixel/control-room.png` | 320×160 | Machine fallback/control-room backdrop |
| `assets/pixel/operator-night.png` | 256×144 | Portfolio scene plate |


All raster scenes are local PNG files. `tools/genart.py` is the committed Pillow/Bayer-dither generator for the current pixel-art set. No remote runtime images are required.

## Fonts and libraries

The portfolio references Google Fonts for Space Grotesk, IBM Plex Mono and Silkscreen with system fallbacks. Three.js is vendored locally as `assets/vendor/three.min.js`; there are no remote runtime JavaScript dependencies.

## Validation workflow

Useful static checks:

```bash
cd portfolio
python3 -m py_compile tools/*.py
node --check pixel.js
node --check motion.js
```

Browser validation should serve the `portfolio/` directory and inspect all three pages across dark/light themes and the 1440×1000, 1024×768, 768×1024 and 390×844 viewports. Required Pass A checks include Machine overflow, forced WebGL failure, reduced-motion loop behaviour, heading hierarchy, rating-fill widths and toast queue behaviour.

## Current limitations after Pass A

- The division roster is still the existing card grid; the Bureau Operations Console is a later authored-art pass.
- The current raster scenes are intentionally local and lightweight; richer authored scene regeneration is not part of Pass A.
- Kindred Siege remains marked as unverified where shown.
- Browser automation may depend on the execution environment having Chromium or Playwright available.
