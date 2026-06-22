# Desk 42 Portfolio — Final Visual Pass & Browser Validation

Validated in headless Chromium (Playwright) — served locally, screenshots
captured and inspected at the required breakpoints, then corrected.

## 1 · Screen inventory

| Screen / section | File | Previous treatment | Current treatment | Status | Visual check |
|---|---|---|---|---|---|
| System boot | both | PRESS START curtain | Diagnostic sequence → PRESS START → aperture wipe | Done | ✓ |
| Landing hero | index | Gradient title + cards | Bureau-vista **pixel-art** scene + HUD + integrated title | Done | ✓ dark/light/mobile |
| Dossier selector | index | 6 identical cards | 6 distinct **dossiers** (authorised vs sealed-hatched, case IDs, emblems, access state) | Done | ✓ |
| Anomaly beat | index | — (new) | Full-bleed **anomaly-corridor** pixel scene + caption | Done | ✓ |
| Clearance / evidence | index | 3 cards | Clearance clauses under integrity scene band | Done | ✓ |
| Architecture hero | architecture | SVG silhouette | **operator-night** pixel scene + rays + HUD + title | Done | ✓ |
| Operational metrics | architecture | tiles | Punched-card **counters** + cabinet **rating meters** | Done | ✓ |
| Doctrine (principles) | architecture | cards | Sealed **directives** under scene band | Done | ✓ |
| Bureau divisions (roster) | architecture | flowchart→cards | **Division** cards: emblem, count, role, operational state | Done | ✓ |
| Core systems | architecture | bento + mini-charts | Control-room **modules**, distinct emblem/colour/state | Done | ✓ |
| Code evidence | architecture | code block | **Terminal transmission** panel (path, copy) | Done | ✓ |
| Engineering integrity | architecture | cards | **Integrity modules** (test chamber / build gate / firewall / archive) | Done | ✓ |
| Cross-project | architecture | table | **Intelligence board** (verified / unverified markers) | Done | ✓ |
| Sign-off footer | both | text footer | Evidence **sign-off** with seal | Done | ✓ |

Repository contains **two pages / 14 named scenes** (not ~13 separate files). The
spec's "thirteen wireframes" map to sections within these two pages; all are listed above.

## 2 · Motion inventory

| Interaction | File | Duration | Easing | Trigger | Reduced-motion |
|---|---|---|---|---|---|
| Boot diagnostic typing | pixel.js | ~230ms/line | step | load | instant dump |
| Boot aperture exit | pixel.css | `--duration-cinematic` | `--ease-mechanical` | ENTER/click | opacity only |
| Section reveal (rise/pop) | motion.js + pixel.css | `--motion-reveal-duration` | `--ease-system/-mechanical` | scroll (IO + sweep + 5s safety) | shown immediately |
| Scene light-sweep | pixel.css | 1.1s | `--ease-system` | reveal | none |
| Counter count-up | motion.js | 1200ms | cubic ease-out | in-view | final value set |
| Rating meter fill | motion.js + pixel.css | `--duration-cinematic` | `--ease-mechanical` | in-view | filled instantly |
| Hero plate drift | pixel.css | 46s | `--ease-dread` | ambient | none |
| Gradient/anomaly breathe | pixel.css | 7–30s | `--ease-dread` | ambient | none |
| Panel/dossier hover | pixel.css | `--motion-panel-duration` | `--ease-system` | hover | unaffected (instant) |
| HUD sanity drain | pixel.js | 220ms | `--ease-system` | scroll | value still updates |
| Theme transition | pixel.css | `--motion-theme-duration` | `--ease-system` | toggle | near-instant |
| Achievement toast | pixel.css | `--duration-deliberate` | `--ease-mechanical` | section in-view | no transform |

All sequences collapse to a complete, legible state under `prefers-reduced-motion`.

## 3 · Browser findings & corrections
- **Bug fixed:** scroll-reveal relied solely on IntersectionObserver and left most
  sections invisible if IO timing was off. Reworked to IO **+ scroll-sweep + a 5s
  safety net** so content can never stay hidden.
- Verified full render at 1440 (dark/light), 390 mobile (dark/light), and tablet.
- **Mobile nav** previously hid entirely → now a scrollable nav row.

## 4 · Accessibility (automated + manual)
- 1 `<h1>`, 1 `<main>`, 1 `<header>` per page; logical headings.
- All `<img>` carry alt; decorative layers `aria-hidden`.
- No links/buttons without accessible names.
- Contrast (computed): body text **17.3:1** dark / **8.6:1** light; scene titles
  **14.7:1** dark / **12.2:1** light — all exceed WCAG AA.
- Status shown by label + shape, not colour alone; `:focus-visible` rings on controls
  and dossiers; theme toggle exposes `aria-pressed`.

## 5 · Performance
- Pixel scenes ~26–29 KB each (256×144 PNG, nearest-neighbour upscale); 3 total.
- Motion is transform/opacity; off-screen ambient loops are CSS (cheap); no animation library.
- Inline styles reduced: architecture **32 → 7**, index **33 → 2** (remainder are
  one-off margins / data-driven widths).
- Fonts hosted with system fallbacks; only required weights requested.

## 6 · Factual integrity
- All figures measured from source (71 scripts / 13 namespaces / 27 edges).
- Kindred Siege column remains visibly **UNVERIFIED**.
- Personal/institutional details unchanged pending confirmation.

## 7 · Remaining limitations
- Decks 02–06 are intentionally **sealed / in-production** (content not yet built).
- Light-theme bloom is gentler than dark by design; worth a human eye on Archive Shift.
- Pixel scenes are procedurally generated originals; richer hand-pixelled art could
  replace them later via the same manifest.

---

## Pass 2 — corrections from the rendered audit

Verified in-browser after each fix.

| Audit finding | Fix | Verified |
|---|---|---|
| Archive Shift hero title unreadable (dark text on dark stage) | `.title-pop` stays light (`#f6efdd`) in both themes; badge given a dark backing | ✓ computed colour `rgb(246,239,221)` over the stage |
| Boot once-per-session edge bug (skip before sequence end didn't persist) | `dismiss()` now writes `sessionStorage.desk42.seen` on **any** dismissal | ✓ skip mid-sequence → flag `1`, boot hidden on reload |
| Stale `inventory` toast (no matching section) | Removed; toast map now matches real section ids | ✓ |
| Duplicate font requests (eng.css JetBrains+Inter vs tokens IBM+Silkscreen) | eng.css `@import` removed; fonts load once in tokens.css | ✓ single request |
| Two parallel token systems | Legacy `--bg-*/--fg-*/--amber/--t-*/--r-*/--s*` remapped to formal tokens in pixel.css (both themes) → tokens.css is the source of truth | ✓ pages render unchanged |
| Duplicate atmosphere/grain definitions | Removed from eng.css; defined once in pixel.css | ✓ |
| "Off-screen animation pausing" claimed but absent | Implemented: `.stage` IntersectionObserver toggles `paused`; `visibilitychange` pauses on hidden tab (`animation-play-state`) | ✓ |
| Scene comments skipped 02/03/07 | Renumbered sequentially 01–10 | ✓ |
| Stale README | Rewritten to match the two-page implementation, tokens, themes, pixel art, scenes | ✓ |

### Still outstanding (honest)
- Section compositions remain scene-banner + themed module grids. A deeper
  recompose (filing-index divisions, control-room panorama, security-checkpoint
  integrity) is **not** done — it is a further authored pass.
- Cosmic escalation is light-touch (boot "ENTITY 42 AWAKE", an impossible
  `27:42` timestamp, the unverified intelligence column) rather than a full
  progressive corruption arc.
- Pixel scenes are 3 procedurally-generated originals, not the full 7-scene set.
