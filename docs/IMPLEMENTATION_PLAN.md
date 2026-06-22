# Desk 42 — Reliability/Correctness Pass, then Authored Art Pass

## Context
A rendered audit of the current build (verified line-by-line against source) found
the design direction is now strong but flagged real **technical defects**
(broken in the browser), **accessibility/semantic** problems, **stale
documentation**, and a set of larger **authored-content** gaps. The user chose to
**ship the reliability/correctness fixes first**, then do the authored art pass.
The goal: a build that is correct, reliable, and honestly documented now — and
genuinely cinematic (not "styled documentation") next.

Everything below was confirmed in the current source:
- `pixel.css` `.rating__fill` is an inline `<i>` with no `display:block` → rating
  bars render invisible (`pixel.css:205`).
- `machine.html`: `new THREE.WebGLRenderer()` has no try/catch (`:77`); no
  `box-sizing` reset → desktop doc overflow (1510px @ 1440); `requestAnimationFrame`
  runs unconditionally (`:219/:221`) with no tab-hidden pause; **5 `<h1>`** (`:61-65`).
- Scene titles are `<div class="scene__title">` → heading jumps h1→h3
  (index 2, architecture 5 such divs).
- `pixel.js` `toasts()` appends with no limit → toasts stack and cover content.
- Docs stale: `README` says "two-page" (there are 3 HTML pages); `MANIFEST`
  references `three.module.min.js` (actual `three.min.js`) and `tools/genroom.py`
  (only `tools/genart.py` is committed), omits `control-room.png`; `VALIDATION`
  claims "7 inline styles / 1 h1 per page / logical headings" — no longer true.

Reusable patterns already in the repo:
- **Console/tablist**: `pixel.js` `controlRoom()` + `.controlroom/.station/.syspanel`
  in `pixel.css` (roving-tabindex tabs driving one readout) — the backbone for the
  new Bureau Operations Console.
- **Scroll-depth hook**: `pixel.js` `hud()` already maps scroll progress to state
  — reuse for cosmic-corruption progression.
- **Reveal/parallax**: `motion.js` (`[data-rise]/[data-pop]`, sweep + safety net).
- **Pixel art generator**: `tools/genart.py` (Pillow + Bayer dither).

---

## PASS A — Reliability, correctness, docs (ship first)

### A1 · Machine page reliability — `machine.html`
- Add a global reset to its `<style>`: `*,*::before,*::after{box-sizing:border-box}`
  (fixes the fixed `.top` header overflow → no horizontal scroll at 1440).
- Wrap WebGL init in `try/catch`. On **any** failure (no `window.THREE` **or**
  `new THREE.WebGLRenderer()` throws / no GL context): hide `#gl`, show a static
  **2D fallback** (an `<img>` of `assets/pixel/control-room.png` or
  `operator-night.png` as the fixed backdrop) **and still run the beat-reveal
  IntersectionObserver** so the narrative text appears (currently it sits at
  `opacity:0` forever on failure). Move/duplicate the reveal-observer setup so it
  is reached regardless of the WebGL path.
- Render loop discipline: under `prefers-reduced-motion`, render **one** static
  frame and **do not** schedule `requestAnimationFrame`. Add `visibilitychange`
  → stop the loop when `document.hidden`, resume on visible. (Scene is full-bleed
  fixed, so on-screen pausing = tab-hidden pausing.)

### A2 · Invisible rating bars — `pixel.css`
- Add `display:block` to `.rating__fill` (`:205`) so width/height apply. Quick
  audit of sibling fills (`.hud__bar i`, `.gauge__fill`, `.statline__fill`,
  `.counter`/meter inners) to confirm each is block-level.

### A3 · Heading semantics — `architecture.html`, `index.html`, `machine.html`, `pixel.css`
- Convert the major scene titles `div.scene__title` → `h2.scene__title` (keeps
  the class styling). Add `h2.scene__title,h3{}` margin reset in `pixel.css` so UA
  margins don't shift layout. Component titles beneath stay `<h3>`
  (`.cardb h3`, `.sysmod h3`, `.syspanel h3`, `.intmod h3`, `.directive h3`,
  `.division__name`→ keep as styled span or h3 within its row).
- `machine.html`: first beat stays `<h1>`; beats 2–5 become `<h2 class="big">`.
- Result hierarchy per page: one `h1` → `h2` scene titles → `h3` components.

### A4 · Toast queue — `pixel.js`
- Change `toasts()` to show **one toast at a time** with a small FIFO queue:
  enqueue on section intersect; display current; on dismiss/timeout, shift next.
  (Prevents stacked toasts covering content during fast navigation.)

### A5 · Documentation accuracy — `README.md`, `assets/MANIFEST.md`, `VALIDATION.md`, `tools/`
- `README`: "three-page" (index / architecture / machine); list the 4 raster
  scenes; mention `machine.html` + Three.js.
- `MANIFEST`: reference `three.min.js` (not module); reference `tools/genart.py`;
  add `control-room.png` to the table; document `machine.html`. Commit the
  control-room generator as `tools/genroom.py` (currently only `genart.py` is in
  the repo) so the manifest reference is true.
- `VALIDATION`: correct the inline-style count (architecture is 19 — note 12 are
  legitimate data-driven `--st` custom properties on console stations) and the
  heading/`<h1>` claims (now accurate after A3).

### Pass A delivery
Commit and push only when repository access and authorisation are available; otherwise report the working-tree status accurately. Re-render dark/light + mobile + the WebGL-disabled fallback, send
screenshots. This is the "fixes first" checkpoint.

---

## PASS B — Authored art & narrative (second pass)

### B1 · Divisions → "Bureau Operations Console" — `architecture.html`, `pixel.css`, `pixel.js`, `tools/genart.py`, `assets/pixel/bureau-operations.png`

Replace the 13-card roster in `#map` with one cohesive interactive system —
**Bureau Operations Console** ("Department Signal Router · Terminal 04"). It must
feel like routing access through a living bureaucratic machine, not picking from a
menu. Reuse the accessibility/state foundations of `controlRoom()` (roving-tabindex
tablist) but build a **new visual composition**.

**Core interaction (causally connected, every select affects all 3 regions):**
select a division channel → a signal routes into the central stage → the matching
department sector illuminates → the machine confirms/rejects the route → one
dossier panel "decodes" the record. No instant unrelated content swaps.

**Region A — Division switchboard** (left/top, ~22–25%): the 13 divisions as
compact illuminated *channels* (not cards): code, compact emblem, name, script
count, operational-state marker. Should read as an analogue telephone exchange /
punched-card routing desk / signal board — never a sidebar, settings list,
spreadsheet, plain vertical tabs, or 13 mini-cards. Selection feedback: channel
raises/illuminates, emblem activates, signal-frequency pattern shows, a route line
fires toward the stage, state announced, `aria-selected` set, focus moves. Roving
`tabindex` + ArrowUp/Down (and Left/Right where apt) + Home + End + Enter + Space.

**Region B — Live bureau diorama** (center, ~45–50%, the focal point): generate a
new local raster scene `assets/pixel/bureau-operations.png` (`480×270`, or
`512×288` if visibly better) — a bureau cross-section / machinery-lined sector
strip (NOT a literal floor-plan/diagram) with authored detail: processing stations,
pneumatic tubes, filing machinery, cables, conveyors, surveillance windows,
numbered doors, warning lamps, signage, foreground structure, shadowed
inaccessible areas, 1–2 subtle impossible architectural details. Divide into
identifiable department sectors; each division has a positioned hotspot over its
sector. On select: that sector gets controlled illumination + nearby machinery
activates + routing path terminates there + marker readable + unrelated sectors
recede + maybe one subtle scene animation. **Only the selected sector strongly
activates** (no 13 constantly-blinking hotspots; dependents may show a weak
secondary signal).

**Region C — Decoding dossier** (right/bottom, ~28–32%): one **persistent** panel
(not 13 hidden cards) updated on select — title, code, state, script count,
responsibility, namespace role, key systems, direct dependencies, dependants
(where useful), one concise operational note, verified-data marker. Restrained
"decode" reveal: dim current → classification line updates → title resolves →
fields appear → dependency indicators activate. Brief; no long typewriter effect.

**Composition** — desktop: asymmetric 3-region (header route/state bar above;
switchboard | diorama | dossier with the weights above, not equal). Tablet:
switchboard then diorama+dossier (whichever keeps the art usable). Mobile: compact
**horizontally scrollable** selector (shows part of next item, snaps, keyboard
accessible, scrolls selected into view, visible prev/next where needed) → selected
identity+state → diorama → dossier. Must NOT stack 13 full-width rows; should cut
the architecture mobile height substantially.

**Signal-routing animation** — a real DOM/SVG route layer over the console:
animate a signal from the chosen channel to the selected sector (~300–550ms),
light intermediate nodes, settle to a low static pulse. Must not cover text, cause
layout shift, run at full intensity continuously, or use a JS animation library.
Under `prefers-reduced-motion`: show the completed route immediately, keep sector
illumination + all state/info changes.

**State language** (more than a pill colour; never colour-only — use labels,
symbols, route behaviour, signal shape, texture/border): `ACTIVE` (clean signal,
green/cyan), `CORE HUB` (multiple faint dependency routes, stronger central pulse,
amber), `RESTRICTED`/`SEALED` (incomplete route + access shutter/classification
bar; real evidence still readable), `COMPROMISED` (unstable signal edges, warning
interference, red/amber), `ANOMALOUS` (violet route contamination, sector
shadow/geometry behaves wrong, restrained corruption), `OFFLINE` (route attempts,
fails, resolves offline; dossier still readable). States are **diegetic
game-world flavour layered on real namespaces** — assigned consistently with each
namespace's narrative role, never implying the actual code is defective; all
factual dossier content (counts, responsibilities) stays accurate.

**Content architecture** — store records in a single JS `divisions[]` structure
(`{id, code, name, scripts, state, responsibility, role, systems, dependencies,
dependants, sector:{x,y}}`) using the **verified** counts already in the roster
(Core 5, UI 11, BehaviourTrees 8, BSM 8, OfficeSupplies 7, Archetypes 7, Cards 5,
RedTape 3, Persistence 3, Narrative 3, MoralInjury 3, Claims 3, Encounter 1 = 67)
and the real dependency facts. **Do not invent counts/dependencies.** Keep the
essential content present in / available to semantic HTML so evidence isn't lost
when JS is unavailable (progressive enhancement: render channels + a default
dossier directly in semantic source HTML; JS progressively enhances it into the interactive console).

**Art direction** — mission-select inside an institutional machine / haunted
government switchboard / playable systems directory / a miniature world. Strong
silhouettes, high contrast, deliberate pools of light, reactive machinery,
physical-looking controls, bureaucratic seals, quiet unease. Avoid cyberpunk
holograms, neon, floating translucent cards, literal floor-plan diagrams, 13
competing animations, empty decoration, fake data, long typewriter animations.

**B1 done when:** the 13-card roster is gone; all 13 divisions in the selector;
one selected by default; click + keyboard work; `aria-selected`/`aria-controls`/
panel labelling resolve; the diorama sector visibly changes per division; the
route visibly connects selection→sector; the dossier updates accurately; both
themes work; mobile avoids 13 stacked records; reduced motion preserves the full
experience; no horizontal overflow; screenshots at desktop/tablet/mobile + a short
recording of ≥3 selections; the result no longer resembles a card grid or ordinary
tab component.


### B2 · Richer hero/anomaly art — `tools/genart.py`, `assets/pixel/*`
Regenerate the hero scenes at higher resolution (`384×216`, hero plate `512×288`)
with more authored density: office props, paper piles, cables, foreground
silhouettes, signage/institutional markings, less symmetry, stranger cosmic
forms. Replace `operator-night.png`, `bureau-vista.png`, `anomaly-corridor.png`
(and add the bureau-diorama plate for B1). Verify each by rendering + inspecting
before committing.

### B3 · Cosmic-corruption progression — `pixel.js`, `pixel.css`, `architecture.html`
Drive a restrained, **non-obstructive** deterioration from scroll depth (reuse
the `hud()` progress hook): deeper sections gain subtle corruption — a duplicated
operator ID, an impossible timestamp, a misaligned/wrong-colour department seal,
a briefly contradictory status label, increasing violet anomaly bleed in the
atmosphere, and minor interface instability near the Intelligence/anomaly
sections. Gated by `prefers-reduced-motion`.

### Pass B delivery
Commit and push only when repository access and authorisation are available; otherwise report the working-tree status accurately. Re-render all breakpoints/themes + a motion GIF of the console and
corruption, send.

---

## Verification (both passes)
Tooling is available locally: `python3 -m http.server` (served from
`design-system/portfolio`), Playwright + SwiftShader WebGL (`/opt/pw-browsers`),
Pillow, PyAV, and the existing screenshot/contrast harnesses in `/tmp`.
- **Rating bars:** screenshot `#metrics` — fills now visible at 92/88/80/64/95.
- **Machine overflow:** `documentElement.scrollWidth <= innerWidth` at 1440.
- **Machine WebGL fallback:** load with `getContext` stubbed to null via
  `addInitScript` → assert `#gl` hidden, fallback `<img>` shown, beats revealed
  (not `opacity:0`).
- **Machine loop:** assert no `requestAnimationFrame` scheduled under reduced
  motion / when `document.hidden`.
- **Headings:** assert one `h1`, `h2` scene titles, `h3` components per page; no
  level skips.
- **Toasts:** trigger several sections fast → assert ≤1 toast in DOM at a time.
- **Bureau console (B1):** select rows via click + arrow keys → dossier + stage
  update; tablist ARIA resolves; readable dark/light; mobile single-column.
- **Corruption (B3):** corruption intensifies with scroll depth; animated corruption is disabled under reduced motion while static narrative corruption remains; it never covers evidence text or changes factual evidence.
- Re-run the overflow sweep (1440/1024/768/390 × dark/light) and the
  contrast/landmark checks each pass.

---

## Codex execution safeguards

- Pass A is a hard gate. Do not start Pass B until Pass A is reviewed.
- The Bureau Operations Console visual priority is: diorama, dossier, switchboard, route effects.
- Essential division evidence must remain in semantic source HTML without JavaScript.
- Fictional corruption may never alter factual portfolio evidence.
- Reduced-motion users retain static anomaly storytelling while motion-based corruption is disabled.
- Route geometry must recalculate after font loading, resize, orientation change, theme change, mobile selector scrolling, and selection change.
- New art is accepted only after rendered inspection at normal display size.
- Git actions are conditional on actual repository permission. Never claim a commit, push, PR, screenshot, or test that did not occur.
