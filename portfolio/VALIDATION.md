# Desk 42 — Pass A Reliability Validation Notes

This file records the checks run for the reliability/correctness pass. Browser automation could not be completed in this environment because no Chromium/Chrome executable was present and installing Playwright from npm was blocked by a registry `403 Forbidden` response. Results below distinguish source inspection, automated static checks, local HTTP asset checks and limitations.

## Current page inventory

The current site has three HTML pages:

- `index.html`
- `architecture.html`
- `machine.html`

## Heading hierarchy

Automated source parsing was run with `/tmp/check_static.py`.

| Page | h1 | h2 | h3 | Heading order | Level skips |
|---|---:|---:|---:|---|---|
| `index.html` | 1 | 2 | 4 | `h1 > h2 > h3 > h2 > h3 > h3 > h3` | none detected |
| `architecture.html` | 1 | 5 | 13 | `h1 > h2 > h3 > h3 > h3 > h2 > h2 > h3 > h3 > h3 > h3 > h3 > h3 > h2 > h3 > h3 > h3 > h3 > h2` | none detected |
| `machine.html` | 1 | 4 | 0 | `h1 > h2 > h2 > h2 > h2` | none detected |

Source inspection confirms major `.scene__title` section titles are now `h2.scene__title`, and the Machine page keeps only its first narrative title as `h1`.

## Inline styles

Automated source parsing counted these `style` attributes:

| Page | Inline style count | Notes |
|---|---:|---|
| `index.html` | 2 | HUD bar widths for data-driven visual state. |
| `architecture.html` | 19 | 3 HUD bar widths, 12 station `--st` custom properties for the control-room selector, and remaining data/custom-property-driven presentation values. |
| `machine.html` | 2 | The final CTA paragraph/link preserve pointer access and local spacing inside the full-screen narrative beat. |

## Source and syntax checks run

- `node --check portfolio/pixel.js`
- `node --check portfolio/motion.js`
- Machine inline script extracted to `/tmp/machine-inline.js`, then checked with `node --check /tmp/machine-inline.js`.
- `python3 -m py_compile portfolio/tools/*.py`
- `/tmp/check_static.py` source assertions for headings, display modes, fallback markup, loop-controller markers and toast queue markers.

## Local HTTP asset check

The site was served with `python3 -m http.server 8000` from `portfolio/`. A Python `urllib.request` check returned HTTP 200 for:

- `index.html`
- `architecture.html`
- `machine.html`
- `tokens.css`
- `eng.css`
- `pixel.css`
- `pixel.js`
- `motion.js`
- `assets/vendor/three.min.js`
- all four `assets/pixel/*.png` files

## Machine page reliability

### Source inspection and static checks

- Global box sizing reset is present in the Machine page style.
- The previous body-level `overflow-x:hidden` workaround was removed.
- The Machine page includes semantic fallback markup using `assets/pixel/control-room.png`.
- WebGL startup now checks for `window.THREE`, `THREE.WebGLRenderer`, canvas context creation returning `null`, renderer construction and scene setup failures.
- Failure handling hides the canvas, shows the fallback figure, updates a concise `aria-live` diagnostic, logs a concise console warning and reveals all narrative beats.
- Narrative reveal setup runs before WebGL startup and has an immediate reveal path when `IntersectionObserver` is unavailable or throws.
- The loop controller tracks active state, loop-running state, current animation-frame handle, reduced-motion state and document-hidden state.
- Reduced motion renders a static frame when WebGL starts and does not start the continuous loop.
- `visibilitychange` stops the loop while hidden and resumes through the same guarded `syncMotion()` path when visible.

### Limitation

The required rendered overflow measurements at 1440, 1024, 768 and 390 pixels, forced WebGL-failure browser assertions, reduced-motion requestAnimationFrame instrumentation and hidden-tab browser lifecycle test could not be executed because browser automation was unavailable in this environment.

## Rating bars

Source inspection confirms:

- `.rating__fill` has `display:block` and retains width, height, colour and transition behaviour.
- `.hud__bar i` already had `display:block`.
- `.gauge__fill` now has `display:block`.

The architecture metrics markup still declares rating values of 92, 88, 80, 64 and 95 via `data-fill` attributes. `motion.js` remains responsible for applying those values as widths when the metrics section is revealed.

### Limitation

Rendered dark/light visual verification and screenshot capture of the metric bars could not be completed because browser automation was unavailable.

## Toast queue

Source inspection confirms the achievement-toast system now uses:

- a bounded FIFO queue (`MAX_QUEUE = 5`),
- one active toast at a time,
- duplicate suppression window (`DEDUPE_MS = 2500`),
- timer cleanup before close/remove transitions,
- `host.replaceChildren(t)` so only one visible toast element occupies the live region,
- a test/debug state accessor at `window.__desk42.toastState()`.

### Limitation

Rapid section-navigation browser verification, reduced-motion toast rendering and screenshot inspection could not be completed because browser automation was unavailable.

## Accessibility checks

Source inspection confirmed:

- Each HTML page has one `main` and one `h1`.
- The Machine fallback image has meaningful alternative text.
- Decorative Machine vignette/scanline layers remain `aria-hidden`.
- Toast host is a polite, atomic live region; toasts use `role="status"` and do not move focus.
- Existing links and buttons retain visible text labels.
- Theme toggles remain button controls in the main pages.

### Limitation

Automated accessibility tooling was not run because browser automation and npm installation were unavailable.

## Responsive/theme rendering and screenshots

No screenshot artifacts are recorded in this validation file because the environment lacked a browser executable and npm installation of Playwright was blocked. Required visual checks should be rerun in an environment with Chromium/Playwright available.
