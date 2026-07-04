# Known issues / risk watchlist — Desk 42

**No audited bug list exists for Desk 42 yet.** (Audited bugs from the Brawler project do *not* apply here — different engine, different code.) Below are **risk areas to verify**, derived from how these systems work — not confirmed defects. Promote items to a real bug list as you find them. (Recent commit history shows real fixes around claim-counter off-by-ones and claim-2 deadlocks — keep that area in mind.)

## State machine
- Unbalanced `ClientStateStack` push/pop -> clients stuck in a mood or leaking states.
- Transition thrash if multiple inputs fire the same frame (UI + environment + reputation). Define precedence.

## Red Tape Engine (highest risk)
- **Leaked node injections** — BT nodes injected by `MutationEngine` without a removal path become permanent disruption. Audit every injection for cleanup.
- Selector/priority bugs -> wrong disruption wins, or a branch is starved.
- `BTContext.CurrentSanity` drifting from `RunStateController.Sanity` -> disruptions keyed to the wrong tier.

## UI Decay
- Non-deterministic decay breaking save/load or replays (seed it).
- Decay not fully resetting on recovery -> residual drift; gauges stay "wrong" after Sanity recovers.

## The Tide
- Flow/Ebb oscillation when thresholds are tight -> difficulty whiplash. Add hysteresis.

## Sanity -> Fugue
- Soft-lock: if clawing back from Fugue is mathematically impossible at certain states, the player can't clock out. Verify a recovery path always exists (`RunStateController.IsInFugueState`).

## FMOD (scaffolded, not yet live — `DESK42_FMOD` off)
- **Contract mismatch is the #1 risk.** The code expects specific names — global params `"Sanity"` (**normalized 0–1**, *not* 0–100) and `"TidePressure"`; buses `bus:/`, `bus:/Music`, `bus:/SFX`, `bus:/Ambience`; events under `event:/Music/*` and `event:/Threat/*`. If the FMOD Studio project authors *different* names (e.g. the kit's old Diegetic/Interface/Voice buses or a 0–100 Sanity), parameters silently do nothing. **Author Studio to match code, or rename code to match Studio — one source of truth.**
- Defining `DESK42_FMOD` **before** importing the FMOD plugin and adding the `FMODUnity` asmdef reference -> compile errors in `Desk42.Audio`. Order: plugin import -> asmdef ref -> define.
- Sanity param not driving snapshot blends (wire-up gap) -> silent or stuck mix.
- Audible pops on tier crossings -> use blends/stingers, not hard cuts.
- Audio/visual desync -> both must read the *same* Sanity value (`RunStateController` / `SanityChangedEvent`).
- Don't leak `EventInstance`s — stop with `ALLOWFADEOUT` and `release()` on exit/destroy (same discipline as cleaning up Red Tape node injections).

## ComfyUI art pipeline (not yet live)
- Generated art that ignores the diegetic style guide / Distortion-Scale tiers -> assets that fight the pillar. Keep prompts tier-aware (see `comfy-integration.md`).
- Import settings: non-transparent or wrong-PPU sprites breaking the desk layout under URP.
