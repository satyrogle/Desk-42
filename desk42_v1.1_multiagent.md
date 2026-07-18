# Desk 42 v1.1 — Multi-Agent Work Split

Three agents, full day. Roles:

- **Claude** (MCP access): CLI tooling, hard/complex tasks, code review, the risky integrations into live systems.
- **Codex**: writes new code from spec. Greenfield files, no live-system integration.
- **Antigravity**: support. Tests, fixtures, docs, scaffolding, repetitive wiring.

Rule of collision: only one agent touches a file at a time. The branch + ownership table below enforces that. If a task isn't on your list, don't touch it.

---

## Branches

- `feat/cascade-presenter` — Codex (new files only)
- `feat/cli-tooling` — Claude (CLI + live integration)
- `chore/v1.1-support` — Antigravity (fixtures, tests, docs)
- `investigate/v1.1-audit` — Claude (read-only, no commits to main systems)

Merge order at end of day: investigate first (informs everything), then support, then cascade-presenter, then cli-tooling last (depends on the others).

---

## Claude — MCP, CLI, complex, review

### Read-only investigation (do first, report back before anyone integrates)
1. `ExpenseUnmetEvent` — trace the `RunStateController` handler. Is it a dead win/loss branch? What was it meant to trigger?
2. Startup race — trace init order, `GameManager` (Awake) vs `PassiveAggressiveUIController` (Start, 14 subs). Confirm the real source of the "18 call" audio failure. Report which events fire before the UI controller subscribes.
3. `SynergyResolver` output shape — document every field. Critical: does it keep per-step modifier deltas or only the final value? The CascadePresenter needs the per-step breakdown. If deltas are discarded, report where (no fix yet).
4. Reachability — which of the 98 scripts are actually hit in a normal run vs scene-only wiring. Flag orphans.

### Build (after investigation, these touch LIVE systems)
- `Desk42CLI` router. Single text parser, dispatch to handlers, no domain logic. Wrap CLI + the `EntropyManager` override in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` from the start.
- CLI tool 2: `desk42 bsm set-state <ENUM>` / `desk42 bsm set-impatience <float>` — forces state directly on `ClientStateMachine`.
- CLI tool 3: `desk42 entropy pin --sanity <int>` / `desk42 entropy unpin` — sets `EntropyManager.SanityOverride`.
- ATB edge case in `StateInjector.TrySlam()`: on `OnCardSlammed`, evaluate `Impatience >= Max` first. If true, cache+pause the card, force the `ClientStateMachine` transition, apply Sanity hit, then resolve injection against the new state. Enemy priority always.
- The startup race fix once investigation confirms the cause.

### Review (gate before merge)
- Review Codex's `CascadePresenter` against the `SynergyResolver` packet shape from investigation #3.
- Review any code that touches `ClientStateMachine`, `EntropyManager`, `SynergyResolver`, `PassiveAggressiveUIController`.

---

## Codex — writing new code (greenfield, no live integration)

All new files. Do NOT modify existing systems. Bind to the class names in the binding table, don't invent.

### CascadeConfig
ScriptableObject or struct:
- `BaseDelay` (float, 0.3)
- `SkipDelay` (float, 0.08)
- `AutoFastForwardCount` (int, 3)
- `EnableTier4CardLoss` (bool, false)

### CascadePresenter
Reads a `SynergyResolver` packet, sequences feedback over time, alters NO game state. Sequence:
1. Intent: lock input, pause Impatience.
2. Anticipation: 0.5-1.0s pause + processing cue.
3. Modifier chain: per modifier, number pop + bar drain + chime. `BaseDelay` per step, `SkipDelay` on hold-to-fast-forward, auto-fast-forward after `AutoFastForwardCount` seen (read from `MetaProgressData`, e.g. `SeenComboCount["TagMatch_Stapler"]`).
4. State update: final stamp, unlock, resume Impatience.

Method signature to expose: `CascadePresenter.PlaySequence(packet)`.

### Entropy stamp payload (the data, not the EntropyManager wiring — Claude wires it)
Build the tier-to-stamp mapping table Codex can express as data:
- Tier 1 (100-75%): `APPROVED` / `DENIED`, clean.
- Tier 2 (74-50%): `APROVED` / `DENED`, typo, mild next-draw penalty.
- Tier 3 (49-25%): `COMPLICIT` / `CONDEMNED`, amplifies Moral Injury or spikes Impatience.
- Tier 4 (24-1%): `TERMINATED` / `REDACTED` / `SYNERGIZED`, card loss only if `EnableTier4CardLoss`, cause must show in run summary.

**Blocker:** wait for Claude's investigation #3 (packet shape) before finalizing `PlaySequence`. If the per-step deltas are discarded in the resolver, the modifier-chain step can't be built until that's surfaced. Build CascadeConfig and the tier table while waiting.

---

## Antigravity — support

Low-risk, parallel-safe work that unblocks the other two.

- JSON fixture packets for cascade replay: `fixtures/high_sanity_tagmatch.json`, `fixtures/low_sanity_multichain.json`, plus one per corruption tier. Match the packet shape once Claude's investigation #3 reports it. Until then, draft them from the known cascade order (base resolution, punch card, office supply, stamp, tag match, zone, faction) and mark them provisional.
- Unit test scaffolding for `CascadePresenter` sequencing (timing, step count, fast-forward trigger).
- Test for the ATB edge case: card slam on exactly full Impatience resolves enemy-priority.
- Keep `desk42_v1.1_cascade_handoff.md` and this file updated as findings land.
- Stub the `#if DEVELOPMENT_BUILD` guard pattern so CLI files drop in clean.

Do NOT touch live systems or write integration code. If a task needs `ClientStateMachine` or `EntropyManager`, it's Claude's.

---

## Class binding (all agents, strict)

- `SynergyResolver` — math source, emits packet. DO NOT MODIFY logic. (Claude reads, Codex consumes shape.)
- `CascadePresenter` — NEW, Codex.
- `CascadeConfig` — NEW, Codex.
- `ClientStateMachine` / `ClientStateStack` — live, Claude only.
- `StateInjector.TrySlam()` — live card pipeline, ATB edge case, Claude only.
- `EntropyManager` — static, `SanityOverride` added by Claude.
- `RumorMillEventBus` — pub/sub, don't add subscribers without Claude review.
- `PassiveAggressiveUIController` — live UI hub, startup race, Claude only.
- `Desk42CLI` — NEW, Claude.
- `MetaProgressData` — existing, Codex reads for combo counts.

---

## Day flow

1. Claude runs the 4 investigations, reports.
2. Codex builds CascadeConfig + tier table while waiting, then CascadePresenter once packet shape is confirmed.
3. Antigravity builds fixtures + tests in parallel from the same packet shape.
4. Claude builds CLI + ATB edge case + startup fix.
5. Claude reviews Codex's presenter + anything touching live systems.
6. Merge in order: investigate, support, cascade-presenter, cli-tooling.

Open design questions (failure grammar, HUD layer, FMOD) stay parked until investigation #4 reachability lands. Don't build past them.
