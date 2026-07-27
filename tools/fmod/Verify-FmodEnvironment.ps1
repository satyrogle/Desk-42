# Desk 42 — FMOD local activation helpers.
# Desk-42 is a PUBLIC repository. The official Firelight FMOD SDK is an
# EXTERNAL developer prerequisite, never committed. These scripts make the
# local activation reproducible so nobody hand-edits asmdefs or defines.

$ErrorActionPreference = "Stop"
$Project = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$FmodDir  = Join-Path $Project "Assets\Plugins\FMOD"
$FmodSrc  = Join-Path $FmodDir "src\fmod.cs"
$WinLib   = Join-Path $FmodDir "platforms\win\lib\x86_64\fmodstudio.dll"
# The Editor and development players load the LOGGING build, not the release
# one. A tree carrying only fmodstudio.dll passes a naive check and then dies
# with "DllNotFoundException: fmodstudioL" the moment FMOD initialises — which
# is exactly how an incomplete import slipped through here once already.
$WinLibL  = Join-Path $FmodDir "platforms\win\lib\x86_64\fmodstudioL.dll"
$Asmdef   = Join-Path $Project "Assets\_Project\Scripts\Desk42.Core.asmdef"
$Settings = Join-Path $Project "ProjectSettings\ProjectSettings.asset"
$Scene    = Join-Path $Project "Assets\_Project\Scenes\Shift.unity"

$ok = $true
function Check($label, $cond) {
    if ($cond) { Write-Host "  PASS  $label" }
    else       { Write-Host "  FAIL  $label"; $script:ok = $false }
}

Write-Host "FMOD environment verification"

Check "vendor tree present ($FmodDir)" (Test-Path $FmodDir)
Check "fmod.cs present" (Test-Path $FmodSrc)
if (Test-Path $FmodSrc) {
    $isVersion = (Get-Content $FmodSrc -Raw) -match "0x00020314"
    Check "version is the locked 2.03.14 (0x00020314)" $isVersion
}
Check "windows x64 native library present (release: fmodstudio.dll)" (Test-Path $WinLib)
Check "windows x64 LOGGING library present (editor/dev: fmodstudioL.dll)" (Test-Path $WinLibL)

# Activation state — both halves must agree or the project cannot compile.
$hasRef    = (Get-Content $Asmdef  -Raw) -match "FMODUnity"
$hasDefine = (Get-Content $Settings -Raw) -match "DESK42_FMOD"
Write-Host ("  INFO  asmdef reference : {0}" -f $hasRef)
Write-Host ("  INFO  DESK42_FMOD      : {0}" -f $hasDefine)
Check "activation state is consistent (both on, or both off)" ($hasRef -eq $hasDefine)

# The four experimental directors must stay disabled for the proof candidate.
$guids = @{
    "BinauralStressEngine"     = "a352ef7c2114b2843b8653fe89f83756"
    "ProceduralJazzGenerator"  = "28f9c6f99c25e9d41947bfb9581197f9"
    "StressCrescendo"          = "5fe965bb406181a4396847a25eab55e7"
    "SpatialAudioThreatSystem" = "dea70daac3224db42b8c764cb90e3f9b"
}
$sceneLines = Get-Content $Scene
foreach ($name in $guids.Keys) {
    $guid = $guids[$name]
    $enabled = $false
    for ($i = 0; $i -lt $sceneLines.Count; $i++) {
        if ($sceneLines[$i] -match "guid:\s*$guid,") {
            for ($j = $i - 1; $j -ge 0 -and $j -gt $i - 40; $j--) {
                if ($sceneLines[$j] -match "^\s*m_Enabled:\s*1\s*$") { $enabled = $true; break }
                if ($sceneLines[$j] -match "^\s*m_Enabled:\s*0\s*$") { break }
            }
        }
    }
    Check "$name disabled in Shift.unity" (-not $enabled)
}

if ($ok) { Write-Host "OK: FMOD environment verified."; exit 0 }
Write-Host "FAIL: FMOD environment is not usable as-is."
exit 1
