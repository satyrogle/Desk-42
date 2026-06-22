# Acceptance Tests

Run the checks relevant to the current pass. Do not claim completion without reporting results.

## Baseline commands

```bash
cd portfolio
python3 -m http.server 8000
python3 -m py_compile tools/*.py
node --check pixel.js
node --check motion.js
```

Use Playwright or an equivalent browser harness when available.

## Required viewport/theme sweep

Test each relevant page at:

- 1440 × 1000 — dark and light
- 1024 × 768 — dark and light
- 768 × 1024 — dark and light
- 390 × 844 — dark and light

For every page:

```js
document.documentElement.scrollWidth <= window.innerWidth
```

must be true.

## Pass A assertions

### Rating bars

- Capture `#metrics`.
- Confirm fills are visibly present at 92, 88, 80, 64, and 95 percent.
- Confirm sibling meter/fill elements are block-level where width/height is required.

### Machine overflow

- At 1440px, `scrollWidth <= innerWidth`.

### Machine fallback

Force canvas/WebGL context acquisition to fail before page code runs.

Confirm:

- `#gl` is hidden or replaced
- a static local pixel-art fallback image is visible
- every narrative beat can reveal
- beat text is not permanently `opacity: 0`
- no uncaught initialisation error prevents the rest of the page

### Machine render loop

Under `prefers-reduced-motion: reduce`:

- render one static frame if WebGL succeeds
- do not schedule a continuous animation loop

When `document.hidden` becomes true:

- stop the loop

When visible again:

- resume only if continuous motion is allowed
- do not create duplicate loops

### Headings

For each page:

- exactly one `h1`
- major scene titles use `h2`
- component titles use `h3`
- no heading-level skips in the principal content order

### Toasts

Trigger several section achievements quickly.

Confirm:

- at most one toast exists visibly/in the active toast region at a time
- queued toasts appear in FIFO order
- dismiss/timeout advances the queue
- toasts do not cover persistent controls on mobile

### Documentation

Check every changed claim against actual files and measured results.

## Pass B1 assertions — Bureau Operations Console

### Progressive enhancement

With JavaScript disabled:

- all 13 division names are present
- all verified script counts are present
- responsibilities remain available
- one default dossier is visible
- surrounding page navigation works

### Interaction

For click, touch, and keyboard:

- one division is selected by default
- Arrow keys, Home, End, Enter, and Space work as specified
- `aria-selected`, `tabindex`, `aria-controls`, and panel labelling resolve
- the selected channel scrolls into view on mobile
- the dossier updates to the final selected record
- rapid selection cancels obsolete route/decode animations

### Diorama and route

For every division:

- the intended sector activates
- the route ends at the correct hotspot
- unrelated sectors recede
- no route covers text or controls
- no route leaves the console bounds

Recalculate route geometry:

- after initial layout
- after fonts resolve
- after resize/orientation change
- after mobile selector scrolling
- after theme changes when dimensions change
- after selection changes

Use `ResizeObserver` where appropriate.

### Data integrity

Compare every displayed count and dependency against verified source data after implementation.

### Reduced motion

- show the completed route immediately
- keep selected-sector illumination and dossier state
- disable route travel and repeated motion
- preserve static narrative anomaly details

## Pass B2 assertions — Authored art

Inspect generated assets at normal display size.

Each major new plate must visibly contain:

- foreground, middle-ground, background
- at least three controlled lighting regions
- varied non-repeating props
- institutional markings
- machinery with distinct silhouettes/functions
- environmental wear
- at least one operator/human silhouette where appropriate
- restrained impossible architectural details
- no obvious procedural tiling or sparse symmetry

## Pass B3 assertions — Corruption

- corruption intensifies with scroll depth
- it remains non-obstructive
- factual evidence never changes
- under reduced motion, animated corruption is disabled but static narrative corruption remains
- corruption never covers evidence text or interactive controls

## Final evidence

Provide:

- desktop/tablet/mobile screenshots in both themes
- a short recording or GIF showing at least three console selections
- a WebGL-disabled Machine fallback screenshot
- measured overflow and heading results
- exact changed-file list
- exact validation commands and results
- explicit remaining limitations
