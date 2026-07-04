# Architecture — Desk 42

Companion to CLAUDE.md. Design intent behind the systems, so edits don't fight the design. Paths below are real (verified 2026-06); anything marked `<verify>` still needs confirming against code.

## The diegetic frame
Everything is a physical object on the desk. The "UI" and the "world" are the same layer. Treat new UI as set-dressing in a 1960s-corporate-surreal office (Severance / Control / Rocko), not Unity Canvas chrome. Fonts: monospaced typewriter (Courier / American Typewriter); the Daily Memo uses a mid-century masthead. Typefaces **degrade as Moral Injury rises** (kerning loosens, baselines drift, glyphs swap) — text rendering must support that; don't hardcode clean type.

## Player/run state + the event bus (the spine of reactivity)
- **`Core/RunStateController.cs`** owns **Sanity** (`Sanity`, `ModifySanity(delta)`, `IsInFugueState => Sanity <= 0`). Backed by `Persistence/RunData.cs`. This is the single source of truth for Sanity — audio and visual distortion must both read it (directly or via the event below), never keep independent copies.
- **`Narrative/RumorMillEventBus.cs`** (+ `RumorMillEvents.cs`) — static event bus. `SanityChangedEvent(float prev, float curr, bool fugue)` fires on Sanity changes; audio, achievements, analytics, and shift management all subscribe. **Reactive systems consume this bus rather than polling RunStateController in Update.** Follow that pattern for new reactive features.

## Bureaucratic State Machine (BSM)
`BSM/` — `ClientStateMachine`, `ClientStateStack`, `ClientContext`, `States/`, `Transitions/`. Clients and Office Tools are state machines that move through *bureaucratic moods*, not combat states.

Nine client states: PENDING, AGITATED, LITIGIOUS, COOPERATIVE, SUSPICIOUS, RESIGNED, PARANOID, DISSOCIATING, SMUG. Transition inputs: UI interactions, environmental (distortion) state, accumulated reputation, encounter history.

Composition — **read before editing state flow:**
- **`ClientStateStack`** — push/pop of transient moods over a base state. Watch for unbalanced push/pop -> stuck states.
- **`RedTape/StateInjector.cs`** — forces state changes from outside the client's own logic.
- **Red Tape Engine** — injects *behaviour*, not just state (below).

## Red Tape Engine (dynamic node injection)
`BehaviourTrees/` — `BehaviourTree`, `BTNode`, `BTStatus`, `BTContext`, `Nodes/`, and **`MutationEngine.cs`** (the runtime injection mechanism). The rival/antagonist intelligence injects disruptive BT nodes into the player's active workflow at runtime — the interface itself works against you.

Hard rules for edits:
- Every injected node needs a defined lifetime / removal path. Leaked nodes = permanent, unintended disruption (top bug risk — see known-issues.md).
- Selector/priority ordering decides which disruption wins; changing order changes feel. Don't reorder casually.
- `BTContext.CurrentSanity` is fed in per-tick; keep it consistent with `RunStateController.Sanity`.

## The dials + meters
Player internal state as a small set of interacting dials (sub-personalities, IFS framing). Drivers referenced in code/GDD: **Sanity** (the survival meter; 0 -> Fugue), **Moral Injury** (`MoralInjury/MoralInjurySystem.cs`; drives type/visual degradation), plus archetype-facing costs (`Archetypes/*` spend Sanity). `<Reconcile which terms are dials vs. global meters against the code; the code is source of truth.>`

## Environmental Distortion Scale  ->  the FMOD hook
Five tiers keyed to Sanity. Each defines a visual AND audio state:

| Sanity | Visual | Audio |
|---|---|---|
| 100-75% | clean geometry, warm light | ambient muzak, keyboard clicks, distant coffee |
| 74-50% | fluorescents flicker, shadows deepen, slight asymmetry | lights hum, clock louder, phone shriller |
| 49-25% | walls lean in, ceiling lowers, yellow-green shift | breathing, heartbeat underlay, atonal jazz |
| 24-1% | full Rocko distortion: crooked geometry, impossible angles | tinnitus tones, distorted speech, hostile audio |
| 0% (Fugue) | desaturation, aggressive flicker, UI physically detaches | near-silence except slow breathing |

**Why this is a clean FMOD fit:** it's one continuous parameter (Sanity) driving a blend across mix states. In this repo it is implemented as **a global FMOD parameter `"Sanity"` (normalized 0–1)** pushed through `FMODManager.SetGlobalParameter`, with the per-system audio drivers reacting to `SanityChangedEvent`. Visuals must read the **same** Sanity value (`RunStateController`) so audio and visuals never desync. See `fmod-integration.md` for the full event/bus/param contract. Don't build five separate audio managers; drive the one parameter and let FMOD/the existing drivers blend.

## The Tide (storyteller)
`Core/TideSystem.cs` — director watching the survival meter + the Unpaid Overtime/Impatience timer. **Flow** raises difficulty when the player is cruising; **Ebb** grants a mundane mercy window (a mandated break) when failing. Audio analog: the global `"TidePressure"` FMOD param (`StressCrescendo`). Risk: Flow/Ebb oscillation if thresholds are too tight (see known-issues.md).

## Internal Audit (meta-hub)
Between clients: self-maintenance as puzzle. Punch-card logic blocks (`RedTape/PunchCardMachine.cs`) repair cracked UI glass / clear physical red tape; completing them upgrades the investigator. Mechanically you perform maintenance on your own mind.
