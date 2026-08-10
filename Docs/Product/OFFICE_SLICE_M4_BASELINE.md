# Office Slice M4 Baseline

Recorded 2026-08-10 before M4 production work.

## Source and branch

- Validated M3 implementation: `94eda98175e4f9d9d2bec5c71eb6000a74cf78d6`
- M3 source tip inherited by M4: `40f72b342a7468d9ff9739a9eed94e21c79a49db`
- Frozen tag: `office-slice-m4-baseline-m3-technical`
- M4 branch: `codex/office-slice-v0.7-m4-visual-target`

The post-validation M3 commits contain closeout/evidence only. M4 is presentation-only and does not begin M5 or M6.

## Technical baseline

| Item | Baseline |
|---|---|
| Unity | 2022.3.62f3, revision `96770f6531de` |
| Render pipeline | Built-in; no custom render pipeline asset |
| Build scene 0 | `Assets/_Project/Scenes/InstitutionalAutomation.unity` |
| Build scene 1 | `Assets/_Project/Scenes/OfficeSlice.unity` |
| Package manifest SHA-256 | `6EE443C11B86C41E52E2F53E0B62688EB3E37EE8F6D8936C00A97206C06C3916` |
| Package lock SHA-256 | `EF4F77916D21B944F2EC68818E937149BA674CEEEF2DC74AF53C8D9C84EF847C` |
| M3 replay checksum | `B42CFA89D6277EA2` |
| Existing capture matrix | 12 PNGs: six states at 1280x720 and 1600x900 |
| M3 performance | 118.28 average FPS; 9.74 ms worst frame; 600 sampled frames |

## Tool availability

| Tool | Result |
|---|---|
| Blender | 5.1.2 at `C:/Program Files/Blender Foundation/Blender 5.1/blender.exe` |
| ComfyUI | 0.24.1, API reachable at `127.0.0.1:8188` |
| Comfy checkpoint | `sd_xl_base_1.0.safetensors` available |
| Comfy ControlNet | `controlnet-union-sdxl-promax.safetensors` available |
| Unity MCP | Unavailable to this Codex session; batch CLI validation retained |

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
