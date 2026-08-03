# Legacy Extraction Inventory

Status: approved product-branch extraction boundary.

This inventory applies only to `codex/institutional-product-v0.1`. The complete
card-game product remains recoverable from the annotated tag
`milestone/bucket4-candidate` at commit
`e584ce6c986a8d4ff30fa391c2221c3f8da03a0e` and from the detached worktree
`C:/Users/jacob/Desk42-Legacy-Archive`.

## KEEP active

| Surface | Reason |
| --- | --- |
| `Assets/_Project/Scripts/Institutional/**` | Reusable deterministic institutional simulation architecture and two validated scenario fixtures. |
| Institutional EditMode tests and test asmdef | Protect the engine, scenario boundary and causal claims during product work. |
| `Core/SeedEngine/SeedEngine.cs` | Compatibility sentinel only: an institutional isolation test deliberately mutates the old RNG and proves institutional decisions do not depend on it. |
| `Desk42.Core.asmdef` | Required by the frozen institutional test assembly while the SeedEngine sentinel remains. It is not a product gameplay layer. |
| Institutional constitution, specifications, CI policy and evidence | Preserve exact architectural and endorsement claims. |
| Unity packages, plugin tooling and global package assets | Infrastructure is held stable during code extraction; package rationalization is a separate task. |

## ADAPT later from the archive

These systems are removed from active compilation. They may be inspected and
adapted from the frozen archive when the new product has a concrete consumer.
They must not be copied back wholesale.

| Archived surface | Potential reusable value | Why it is not kept active |
| --- | --- | --- |
| Accessibility palette/settings | Contrast, motion and readability foundations | Coupled to the old UI and has no current product surface. |
| FMOD/audio proof and audio settings | Proven technical audio integration and diagnostics | Stress/Fugue behavior and event ownership belong to the old game. |
| Analytics, achievements and leaderboard adapters | Release-platform seams | Their event vocabulary is the card-run product. |
| Old save system | File replacement, versioning and backup lessons | `InstitutionalSocietyStore` already owns the new society boundary; old `RunData` must not return. |
| Seed/share-code UX | Reproducible run presentation | Institutional state already owns deterministic master seeds; only presentation ideas may transfer. |
| UI feedback components | Timing, animation and interaction references | Their object graph assumes cards, stamps, entropy and the old scenes. |
| Art, fonts and office prop assets | Reference material or future kitbashing | No asset is allowed to dictate the new product architecture. |

## REMOVE from the product branch

- Legacy gameplay scripts: archetypes, cards, claims, client behavior/state
  machines, encounter, Fugue/entropy, old core loop, office supplies, vows,
  moral injury, narrative, red tape, tutorial and old UI.
- Legacy platform/meta scripts whose events are defined by the card run.
- Legacy editor repair/injection scripts and diagnostics.
- Legacy unit tests for the removed systems.
- `Boot`, `MainMenu`, `Shift` and `InternalAudit` scenes.
- Old prefabs, ScriptableObjects, art, project audio placeholders, fonts and
  product resources under `Assets/_Project`.

## Product shell after extraction

The active Unity project must contain:

1. the frozen institutional assemblies and tests;
2. the minimal SeedEngine compatibility sentinel;
3. a separate `Desk42.Product` assembly;
4. one product bootstrap scene that invokes a public scenario API and presents
   a plain diagnostic summary without card-game dependencies;
5. build settings containing only that product bootstrap scene.

The product shell is not a gameplay claim. It proves that the new branch boots
through the institutional boundary after the legacy game is removed.

## Acceptance checks

- The detached archive imports and compiles successfully in Unity 2022.3.62f3.
- No removed legacy namespace remains in active product source, except the
  documented `Desk42.Core.SeedEngine` test sentinel.
- Institutional engine files remain byte-identical to the v0.4.3 candidate
  manifest where the freeze contract requires it.
- Institutional targeted EditMode tests pass without skips.
- The full remaining EditMode suite passes without failures or legacy skips.
- Unity imports the product checkout without compilation errors.
- The product bootstrap scene enters Play Mode or runs as a player shell without
  missing scripts or legacy scene dependencies.

## Execution record — 2026-08-03

- Archive worktree imported and compiled successfully under Unity 2022.3.62f3.
- Removed 615 tracked legacy source, test and serialized-asset files from the
  product branch.
- Active non-institutional source is limited to the documented SeedEngine
  sentinel and `InstitutionalProductBootstrap`.
- All 151 frozen institutional manifest entries remain identical.
- Scenario authoring boundary passed.
- Institutional EditMode: 334 passed, 0 failed, 0 skipped.
- Full remaining EditMode: 338 passed, 0 failed, 0 skipped.
- Windows x64 player build completed successfully.
- Built player executed the institutional reference run, reached cycle 11 with
  three rulings and two descendant cases, and exited with code 0 with no D3D11
  runtime errors.
