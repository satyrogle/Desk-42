# Desk 42 — FMOD local activation helpers.
# Desk-42 is a PUBLIC repository. The official Firelight FMOD SDK is an
# EXTERNAL developer prerequisite, never committed. These scripts make the
# local activation reproducible so nobody hand-edits asmdefs or defines.

# Restores the tracked, clean-clone-buildable configuration: no FMODUnity
# dependency, no DESK42_FMOD. The vendor tree is left on disk, untracked.
$ErrorActionPreference = "Stop"
$Project = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$Asmdef   = Join-Path $Project "Assets\_Project\Scripts\Desk42.Core.asmdef"
$Settings = Join-Path $Project "ProjectSettings\ProjectSettings.asset"

# Targeted text edit so the tracked file returns byte-identical.
$a = Get-Content $Asmdef -Raw
if ($a -match '"FMODUnity"') {
    $a = $a -replace ',(\s*)"FMODUnity"', ''
    Set-Content $Asmdef $a -NoNewline
    Write-Host "  removed FMODUnity assembly reference"
}

$s = Get-Content $Settings -Raw
if ($s -match "DESK42_FMOD") {
    $s = [regex]::Replace($s,
        "  scriptingDefineSymbols:\r?\n(?:    \w+: DESK42_FMOD\r?\n)+",
        "  scriptingDefineSymbols: {}`n")
    Set-Content $Settings $s -NoNewline
    Write-Host "  cleared DESK42_FMOD"
}

Write-Host "FMOD DEACTIVATED. Committed configuration builds without the SDK."
