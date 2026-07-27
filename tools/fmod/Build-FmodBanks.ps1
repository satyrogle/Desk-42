# Desk 42 — FMOD local activation helpers.
# Desk-42 is a PUBLIC repository. The official Firelight FMOD SDK is an
# EXTERNAL developer prerequisite, never committed. These scripts make the
# local activation reproducible so nobody hand-edits asmdefs or defines.

# Builds Desk42 banks from the TRACKED FMOD Studio project using
# fmodstudiocl.exe 2.03.14. The Studio project is authoritative source;
# generated bank output is local build product and is not committed.
param([string]$StudioExe = "C:\Program Files\FMOD SoundSystem\FMOD Studio 2.03.14\fmodstudiocl.exe")

$ErrorActionPreference = "Stop"
$Project = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$Fspro   = Join-Path $Project "Assets\_Project\Audio\FMODStudio\Desk42.fspro"

if (-not (Test-Path $StudioExe)) { Write-Host "FAIL: fmodstudiocl not found at $StudioExe"; exit 9 }
if (-not (Test-Path $Fspro))     { Write-Host "FAIL: Studio project not found at $Fspro"; exit 9 }

Write-Host "Building banks from $Fspro"
& $StudioExe -build $Fspro
exit $LASTEXITCODE
