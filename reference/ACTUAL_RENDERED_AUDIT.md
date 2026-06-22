# Desk 42 — Actual Rendered Audit

**Project audited:** `desk42portfolio.zip`  
**Pages rendered:** `architecture.html`, `index.html`  
**Themes rendered:** Night Shift and Archive Shift  
**Breakpoints inspected:** 1440px desktop, 768px tablet, 390px mobile  

## Executive verdict

The architecture page is now a competent, coherent control-system portfolio page. It is substantially better than the original engineering deck. The dark theme, boot ritual, punched-card counters, status instrumentation and mobile hierarchy all work.

It has **not** yet reached the requested high-fidelity pixel-art, cinematic, retro-futuristic corporate-surrealist standard. The result still reads primarily as a polished technical dashboard with themed cards. The main missing layer is not more CSS polish; it is **authored visual content and composition**.

The landing page was not converted at all. It remains the old evidence-portfolio card grid and is currently the biggest break in the experience.

## Scorecard

| Area | Assessment |
|---|---:|
| Information hierarchy | Strong |
| Dark-theme coherence | Strong |
| Mobile readability | Good |
| Light-theme consistency | Mixed; hero has a serious contrast defect |
| Token centralisation | Partial; two parallel systems remain |
| Scene variety | Limited |
| Pixel-art fidelity | Not implemented |
| Cosmic-horror escalation | Minimal |
| Motion architecture | Functional but overstated in the handoff report |
| Landing-page world building | Not implemented |
| Production readiness | Requires another focused pass |

---

# What works

## 1. The architecture page has a clear world

The following choices are effective:

- Night Shift / Archive Shift framing
- restrained amber, green, cyan, violet and red signal colours
- perforated punched-card styling on the metric counters
- operator HUD and sanity/depth readouts
- classified and verified/unverified language
- system seals and operational states
- strong mobile stacking
- clear separation between evidence and placeholder content

## 2. The boot screen is clean and legible

The diagnostic sequence is one of the strongest parts of the build. It establishes tone quickly, uses colour meaningfully and does not overload the screen.

## 3. The page does not create document-level horizontal overflow

At the tested desktop, tablet and 390px mobile widths, the document width remained within the viewport. The code panel contains intentionally wider code content inside a horizontally scrollable region.

## 4. The mobile layout is more successful than expected

The top-level hierarchy, metric cards, system modules and intelligence board remain readable at 390px. The mobile page is long, but it is not structurally broken.

## 5. Factual placeholders were preserved

The Kindred Siege column remains visibly unverified rather than being filled with invented data.

---

# Critical issues

## 1. `index.html` was not redesigned

This is not a minor limitation. The landing page is the first impression and currently belongs to a different product.

Evidence from the project:

- `index.html` imports only `eng.css`, not `tokens.css` or `pixel.css`.
- It still contains the original six-card evidence grid.
- It contains **33 inline style attributes**.
- Its theme labels remain generic `Light` / `Dark` rather than Night Shift / Archive Shift.
- It contains no Desk 42 scene art, control-system framing, dossier language or pixel-art treatment.

The architecture page cannot feel like a complete world while the entry page remains a standard engineering portfolio.

## 2. Pixel art was not implemented

There are no PNG, WebP, AVIF, sprite-sheet or other raster art assets in the package. There are no `<img>` or `<picture>` elements.

All visual art is inline SVG and CSS.

The rule:

```css
image-rendering: pixelated;
```

is applied to SVG elements, but this does not turn smooth vector artwork into authored pixel art. The hero remains a vector ellipse/head and curved body silhouette. The section art remains simple vector iconography.

The requested target was **pixel-art soul with high-fidelity cinematic execution**. The current implementation supplies interface chrome but not the visual art layer.

## 3. The page still depends heavily on repeated cards

The implementation report says the page moved away from card grids, but the rendered page still uses:

- three doctrine cards
- thirteen division cards
- six system cards
- four integrity cards
- repeated scene-header bands with the same number/kicker/title/icon structure

On mobile, the architecture page is approximately **11,341px tall**. A large part of that length comes from individually stacked cards.

The treatment is cleaner than before, but it is still predominantly a themed documentation grid rather than a sequence of distinct cinematic scenes.

## 4. Archive Shift hero title has a severe contrast defect

In light mode, `.title-pop` changes to the dark primary text colour while the title remains positioned over the hero's black silhouette and dark lower scrim.

The phrase **“The architecture of a”** becomes partially unreadable, especially on mobile. This is visible in the rendered 390px and desktop screenshots.

The title over the cinematic stage should remain a light/inverse colour in both themes, or receive a controlled local backing plate independent of the page theme.

## 5. The token system is not truly centralised

There are two parallel systems:

- new formal tokens in `tokens.css`
- the older palette, type, spacing, surface and motion variables in `eng.css`

`pixel.css` reconciles only some font aliases. Much of the shared structure still consumes the legacy variables.

Examples of duplicated concepts:

- `--font-family-ui` and `--display`
- `--font-family-system` and `--mono`
- formal semantic colours and `--bg-*` / `--fg-*`
- formal motion durations and `--t-fast` / `--t-std` / `--t-slow`
- formal radii and `--r-*`
- two atmosphere definitions
- two grain definitions

This makes the four-tier architecture more descriptive than real. A future change can still produce mismatched results because the page does not have a single source of truth.

## 6. Font loading is duplicated

`tokens.css` requests:

- Space Grotesk
- IBM Plex Mono
- Silkscreen

`eng.css` separately requests:

- Space Grotesk
- JetBrains Mono
- Inter Tight

This creates duplicate font requests and keeps the old type system alive. The final architecture should request only the families actually used.

## 7. The “off-screen animation pauses” claim is unsupported

No `animation-play-state` logic, visibility handler or intersection-based pausing exists for the continuous atmosphere, hero background, eye glow or drifting paper animations.

Intersection Observer is used for one-time reveals, counters, stat fills and toasts. It does not pause the persistent animated layers when they leave the viewport.

The handoff report should not claim off-screen pausing unless it is implemented.

## 8. The boot sequence has a once-per-session edge-case bug

The session flag is written only when the diagnostic sequence reaches `ready()`.

If the user skips or dismisses the boot before the sequence completes, `dismissed` stops the diagnostic loop and the session flag may never be written. The boot can then return on reload during the same session.

The session flag should be written inside `dismiss()` as well as `ready()`.

## 9. Mobile section navigation disappears without a replacement

At widths below 860px, `.topbar__nav` is hidden. There is no compact menu, section drawer, progress rail or “jump to section” control.

The page remains usable by scrolling, but a document over 11,000px tall needs mobile navigation.

## 10. Scene inventory is inconsistent

The HTML comments label scenes:

- 01
- 04
- 05
- 06
- 08
- 09
- 10
- 11
- 12
- 13

Scenes 02, 03 and 07 do not exist in the document. This gives the appearance of a thirteen-scene implementation without an actual thirteen-item screen inventory.

The project needs an explicit inventory rather than numbered comments that skip missing items.

## 11. `README.md` is stale

The README still describes the earlier architecture deck and says it includes an accurate namespace dependency map. The current page presents a bureau-division roster rather than the earlier dependency graph.

It also does not document `tokens.css`, `pixel.css`, `pixel.js`, the two themes, the new boot flow or the visual asset limitation.

## 12. Stale JavaScript remains

The toast map includes an `inventory` section ID, but no element with `id="inventory"` exists. That toast can never fire.

This is small, but it shows the final implementation was not fully reconciled after the page was recomposed.

---

# Why it does not yet deliver the requested emotional impact

The strongest issue is not code quality. It is visual authorship.

## The hero is a symbol, not a scene

The hero contains:

- a smooth circular head
- two glowing eyes
- a simple body silhouette
- a moon-like glow
- a monitor block

This is readable, but it does not have the environmental detail, pose language, pixel-art craft or dramatic staging associated with the references.

## Cosmic horror does not escalate

The page begins dark and remains at roughly the same emotional level. There is no clear progression from ordinary bureaucracy to compromised reality.

Potential escalation is missing:

- impossible office geometry
- corrupted employee records
- a figure behind frosted glass
- forms that rewrite themselves
- duplicated operator IDs
- an anomaly invading the system map
- increasingly unreliable status instrumentation
- department seals becoming subtly wrong

## Mid-century analogue technology is mostly implied through labels

The page needs more physical visual cues:

- Bakelite controls
- punched tape
- rotary indicators
- reel-to-reel storage
- filing cabinets
- switchboards
- paper forms
- phosphor displays
- intercom grilles
- institutional furniture

## Section art is too generic

The right-side art in each scene header is a simple SVG icon. It communicates category but does not create a memorable world.

---

# Required final pass

## Priority 1 — rebuild the landing page

Turn `index.html` into the bureau entry terminal:

- department/dossier selector
- distinct Desk 42 opening scene
- active, sealed and restricted states
- case identifiers and access levels
- themed Night Shift / Archive Shift labels
- no ordinary portfolio card grid
- import and consume the same token system as the architecture page

## Priority 2 — introduce actual pixel-art scene assets

Create a small coherent local asset set. Recommended minimum:

1. operator at Desk 42
2. bureau corridor
3. punched-card control cabinet
4. behaviour-analysis room
5. filing archive
6. anomaly behind office architecture
7. dossier/department thumbnails

Use raster pixel art for scenes, SVG for overlays and CSS for bloom, lighting and grain.

## Priority 3 — give every major section a distinct composition

Do not solve every section with `scene header + card grid`.

Suggested compositions:

- doctrine as stamped sheets sliding from a machine
- divisions as a navigable bureau floor plan or filing index
- core systems as a control-room panorama with selectable modules
- integrity as a vertical security checkpoint
- cross-project as a physical intelligence wall

## Priority 4 — fix Archive Shift

- keep hero title readable over the dark stage
- review muted text and disabled-card contrast
- make the hero badge and stage tag readable against the bright background
- test focus rings and status colours in the light theme

## Priority 5 — consolidate the CSS system

- make `tokens.css` the single source of truth
- remove duplicated legacy tokens or map all legacy variables to formal tokens
- consolidate font loading
- remove duplicated atmosphere/grain definitions
- remove unused old components from `eng.css`
- replace layout inline styles with classes

## Priority 6 — finish interaction and QA

- fix boot session persistence
- add mobile section navigation
- add a clear horizontal-scroll affordance to the code panel
- remove the stale `inventory` toast entry
- implement real off-screen animation pausing or remove the claim
- update README and the screen inventory

---

# Definition of done

The next pass is complete only when:

- `index.html` belongs to the same visual world
- actual pixel-art scene assets are present
- the hero is no longer only smooth vector geometry
- each major section has a distinct scene composition
- light-theme hero text is fully readable
- one token architecture controls both pages
- mobile has section navigation
- boot dismissal always persists for the session
- stale IDs and claims are removed
- README matches the actual implementation
- screenshots are reviewed at desktop, tablet and mobile in both themes

