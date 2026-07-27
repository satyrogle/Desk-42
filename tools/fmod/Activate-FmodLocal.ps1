# Desk 42 — FMOD local activation helpers.
# Desk-42 is a PUBLIC repository. The official Firelight FMOD SDK is an
# EXTERNAL developer prerequisite, never committed. These scripts make the
# local activation reproducible so nobody hand-edits asmdefs or defines.

# Enables the LOCAL FMOD build: adds the FMODUnity assembly dependency and
# defines DESK42_FMOD. Refuses to run on a half-broken environment, because a
# define without the SDK cannot compile.
$ErrorActionPreference = "Stop"
$Project = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

& (Join-Path $PSScriptRoot "Verify-FmodEnvironment.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Host "Refusing to activate: environment verification failed."
    exit 1
}

$Asmdef   = Join-Path $Project "Assets\_Project\Scripts\Desk42.Core.asmdef"
$Settings = Join-Path $Project "ProjectSettings\ProjectSettings.asset"

# Targeted text edit, not a JSON round-trip: ConvertTo-Json reformats the
# whole file, so deactivation could not restore it byte-identically.
$a = Get-Content $Asmdef -Raw
if ($a -notmatch '"FMODUnity"') {
    $a = $a -replace '(\s*)"Newtonsoft\.Json"', '$1"Newtonsoft.Json",$1"FMODUnity"'
    Set-Content $Asmdef $a -NoNewline
    Write-Host "  added FMODUnity assembly reference"
}

$s = Get-Content $Settings -Raw
if ($s -notmatch "DESK42_FMOD") {
    $s = $s -replace "  scriptingDefineSymbols: \{\}",
        "  scriptingDefineSymbols:`n    Standalone: DESK42_FMOD`n    Android: DESK42_FMOD`n    WebGL: DESK42_FMOD"
    Set-Content $Settings $s -NoNewline
    Write-Host "  defined DESK42_FMOD"
}

Write-Host "FMOD ACTIVATED locally. Do NOT commit these two files."
