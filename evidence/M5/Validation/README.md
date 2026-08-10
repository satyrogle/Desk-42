# Desk42 Office Slice M5 validation evidence

Validated implementation: `d519723d900bdad232b8a01c982c8fbc2ac9ec87`

All artifacts in this directory were produced from the validated implementation.
The Windows build is `Builds/M5/Desk42.exe`; the executable itself is excluded
from source control.

## Automated suites

| Artifact | Passed | Failed | Skipped | Duration (s) | SHA-256 |
|---|---:|---:|---:|---:|---|
| `focused-m5.xml` | 34/34 | 0 | 0 | 6.8548309 | `6EE5B9B6EAFB22E59495A5960B05DC01F90F133DC2529BFAB6AECAFFD237327F` |
| `focused-m4.xml` | 47/47 | 0 | 0 | 12.9520961 | `9FA755D81040983D94BD9989141E687E151BEA73292F9DB361FF13798AE35CC9` |
| `focused-m3.xml` | 35/35 | 0 | 0 | 14.4004696 | `DA4DF8071E4F5BB2E11DBCC6AF82B4E906F97AFDA6B2CCDCDB1EF217C43CD69C` |
| `full-editmode.xml` | 568/568 | 0 | 0 | 44.8576522 | `DE167254781DF39D691243956AB98A77B56D05EF497DA2D2A54161D06B04E9F9` |
| `office-slice-playmode.xml` | 27/27 | 0 | 0 | 18.8932492 | `262090F2DF38E6325624891072F12A1E330DDFA2FEADAC90196EFAB44562C10F` |
| `institutional-playmode.xml` | 11/11 | 0 | 0 | 38.9478896 | `7C9F651E2630225B17EB46F4C7CE9C38B603E1B5B2D51BBF322FB3432CFDF293` |

The final focused M3/M4 batch runs set the installed Unity MCP package's
documented runtime-only CI overrides (`CI=true`, keep-connected false, start-
server false). This prevented unrelated editor connectivity from injecting an
authorization error into `LogAssert`; it changed no package or repository file.

## Independent replay

| Artifact | Result | Duration (s) | Campaign checksum | SHA-256 |
|---|---|---:|---|---|
| `replay-process-1.xml` | 1/1 | 2.1770857 | `B42CFA89D6277EA2` | `A73E426D5575EADD4DAC65762864AAF2A72AEBF81C53B51A926BDEEDC970A601` |
| `replay-process-2.xml` | 1/1 | 2.3975858 | `B42CFA89D6277EA2` | `E7398D592B4BCBF8B42F2AEB3E60D741691E802A5CBED8E58CB6F4991DB0BD11` |
| `replay-process-3.xml` | 1/1 | 2.1259217 | `B42CFA89D6277EA2` | `C523A58949755D775CA355D3530161D7EF5D03EFA1D4110BDF9058605E7883E5` |

## Windows build

- Sanitized build record: `windows-x64-build-summary.txt`
- Full local log: `Logs/M5/Windows-x64-Build-Final.log`
- Log result: `Build Finished, Result: Success.`
- Full-log SHA-256: `E898161903A21C49576EE8538A622FACE86D157CC6BF4909D5BF50E138AEA2E6`
- Executable: `Builds/M5/Desk42.exe`
- Executable SHA-256: `F5F73D8616A2500E0FB0223D83774E2D7F6A74C1BBDAD772F4FEFC9BF5812036`
- Build payload: 203 files, 126,889,123 bytes

## Evidence boundary

The seven state captures and performance report are in adjacent M5 evidence
folders. They prove built-player rendering, routing, resource resolution,
bounded runtime telemetry and deterministic state progression. Codex did not
perform human listening and these artifacts do not prove subjective audio
comfort, comprehension, preference, onboarding, fun or retention.
