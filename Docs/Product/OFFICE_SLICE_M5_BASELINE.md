# Office Slice M5 Baseline

Recorded before M5 production work.

## Source and branch

- Validated M4 implementation: `a022dc85bc0493493e66c10baf2899f34b9b508a`
- Frozen tag: `office-slice-m5-baseline-m4-visual`
- M5 branch: `codex/office-slice-v0.7-m5-audio-feedback`
- M4 closeout documentation tip is intentionally not the M5 source; M5 begins
  from the exact validated implementation named by the execution brief.

## Technical baseline

| Item | Baseline |
|---|---|
| Unity | 2022.3.62f3, revision `96770f904ca7` |
| Target | StandaloneWindows64 |
| Render pipeline | Built-in; unchanged |
| Build scene 0 | `Assets/_Project/Scenes/InstitutionalAutomation.unity` |
| Build scene 1 | `Assets/_Project/Scenes/OfficeSlice.unity` |
| Package manifest SHA-256 | `6ee443c11b86c41e52e2f53e0b62688eb3e37ee8f6d8936c00a97206c06c3916` |
| Package lock SHA-256 | `ef4f77916d21b944f2ec68818e937149ba674ceef2dc74af53c8d9c84ef847c` |
| M4/M3 replay checksum | `B42CFA89D6277EA2` |
| M4 performance | 118.55 average FPS; 8.96 ms p95; 10.53 ms worst; 30.03 Hz |
| M4 tests | focused M4 47/47; focused M3 35/35; full EditMode 534/534; Office Slice PlayMode 27/27; Institutional Automation PlayMode 11/11 |
| Existing Office Slice audio | none |
| FMOD | not installed; forbidden for M5 |

## Frozen protected hashes

| Protected target | Files | Aggregate/file SHA-256 |
|---|---:|---|
| Institutional Domain | 34 | `17266fe0300faeaf889c8f56984f363a79c9b9edf72918da9667bbb90e3a5566` |
| Institutional Authority | 123 | `4e2380d65d55635f95741f419fcd0705e1d594237cc2f63d1dcfc8a52fb3f787` |
| Institutional Player | 16 | `74f55bab5a2717835b531eab2e5dfc99a4159f753999c45be9dacc71d7527802` |
| Institutional Runtime | 4 | `18317c226fb1154703cfe88536eb13fd0d63fd56fb9f2fffbf00156e52408ec8` |
| Institutional Scenarios | 14 | `fd6615c7f459caea0d2956abf71be88b3a26ab4f869ba2ccfd090c88cd8cbca6` |
| Institutional scene | 1 | `8ffba77762a5ec4c90c17404d985b5d4abab4ff54d29f9b6574f452e33426e10` |
| Constitution | 1 | `9678093f68863161ec4bcde5c535540778dc9d670e9a82457578973c24d6dae9` |
| Graphics settings | 1 | `257b53b2a8464067ccbafe333c70f7b7f97445d82ef3cc17d065de7f50ef40a1` |
| Quality settings | 1 | `944b52d523bcb15b945bc80924b9289f59185ab19bce8a00e99eec345bde6440` |

M5 may observe Office Slice public state only. It may not alter deterministic
rules, campaign content, protected saves, packages, scene order, render settings,
or any M6 surface.
