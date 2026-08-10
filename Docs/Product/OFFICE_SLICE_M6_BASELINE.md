# Office Slice M6 Baseline

Recorded before M6 product-layer work.

## Source and branch

- Validated M5 implementation: `d519723d900bdad232b8a01c982c8fbc2ac9ec87`
- Frozen tag: `office-slice-m6-baseline-m5-audio`
- M6 branch: `codex/office-slice-v0.7-m6-evaluation-candidate`
- The M5 documentation-only tip is intentionally not the M6 source. M6 begins
  from the exact validated implementation required by the execution brief.

## Technical baseline

| Item | Baseline |
|---|---|
| Unity | 2022.3.62f3, revision `96770f904ca7` |
| Target | StandaloneWindows64 |
| Render pipeline | Built-in; unchanged |
| Package manifest SHA-256 | `6EE443C11B86C41E52E2F53E0B62688EB3E37EE8F6D8936C00A97206C06C3916` |
| Package lock SHA-256 | `EF4F77916D21B944F2EC68818E937149BA674CEEF2DC74AF53C8D9C84EF847C` |
| Campaign replay checksum | `B42CFA89D6277EA2` |
| Focused M5 | 34/34 passed, 0 failed, 0 skipped |
| Focused M4 | 47/47 passed, 0 failed, 0 skipped |
| Focused M3 | 35/35 passed, 0 failed, 0 skipped |
| Full EditMode | 568/568 passed, 0 failed, 0 skipped |
| Office Slice PlayMode | 27/27 passed, 0 failed, 0 skipped |
| Institutional Automation PlayMode | 11/11 passed, 0 failed, 0 skipped |
| M5 Windows executable SHA-256 | `F5F73D8616A2500E0FB0223D83774E2D7F6A74C1BBDAD772F4FEFC9BF5812036` |
| M5 performance | 118.69 average FPS; 8.89 ms p95; 10.59 ms worst; 30.07 Hz |

## Frozen scope

M6 may add product-owned UI, onboarding, plain-language presentation,
accessibility/settings UI, pause flow and local evaluation instrumentation. It
must not alter the protected Institutional paths, packages, render settings,
deterministic campaign outcome, M1 input determinism, M2 case logic, M3 content,
M4 identity or M5 audio identity.
