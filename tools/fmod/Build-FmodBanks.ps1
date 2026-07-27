# Desk 42 — FMOD local activation helpers.
# Desk-42 is a PUBLIC repository. The official Firelight FMOD SDK is an
# EXTERNAL developer prerequisite, never committed. These scripts make the
# local activation reproducible so nobody hand-edits asmdefs or defines.

# ============================================================
# Authors the technical verification content into the TRACKED FMOD Studio
# project and builds its banks, using fmodstudiocl.exe 2.03.14.
#
#   1. generate TECH_PIPELINE_TEST_NONPRODUCTION.wav (deterministic)
#   2. run studio-scripts/desk42-technical-pipeline.js through fmodstudiocl,
#      which imports the asset, authors event:/Desk/Interaction, assigns it
#      to bank Desk42_Technical, saves, and builds
#   3. report where the .bank files landed
#
# The Studio project is authoritative source; built banks are local build
# product and are not committed.
#
# All project mutation happens through FMOD Studio's supported scripting API
# inside the .js. Nothing here edits .fspro or Metadata files.
# ============================================================

param(
    [string]$StudioExe = "C:\Program Files\FMOD SoundSystem\FMOD Studio 2.03.14\fmodstudiocl.exe",
    # Skip regeneration when the caller has already produced the asset.
    [switch]$SkipAssetGeneration
)

$ErrorActionPreference = "Stop"
$Project = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

# Repository top level, deliberately NOT under Assets/: Unity would otherwise
# import the Studio project's own source .wav and XML as game assets.
$Fspro      = Join-Path $Project "FMODAssets\Desk42\Desk42.fspro"
$ScriptFile = Join-Path $PSScriptRoot "studio-scripts\desk42-technical-pipeline.js"
$AssetGen   = Join-Path $PSScriptRoot "New-TechnicalTestAsset.ps1"
$Wav        = Join-Path $PSScriptRoot "assets\TECH_PIPELINE_TEST_NONPRODUCTION.wav"

# ---------- preconditions ----------

if (-not (Test-Path $StudioExe)) {
    Write-Host "FAIL: fmodstudiocl not found at $StudioExe"
    Write-Host "      Install FMOD Studio 2.03.14, or pass -StudioExe <path>."
    exit 9
}

if (-not (Test-Path $ScriptFile)) {
    Write-Host "FAIL: authoring script not found at $ScriptFile"
    exit 9
}

if (-not (Test-Path $Fspro)) {
    Write-Host "FAIL: Studio project not found at"
    Write-Host "      $Fspro"
    Write-Host ""
    Write-Host "      FMOD Studio 2.03 exposes NO supported headless project-creation"
    Write-Host "      path: fmodstudiocl requires an existing .fspro, the scripting API"
    Write-Host "      has no project.new/open/saveAs, and FMOD for Unity can only browse"
    Write-Host "      for an existing project. Creating it is a one-time GUI action:"
    Write-Host ""
    Write-Host "        FMOD Studio -> File -> New Project"
    Write-Host "        save as: $Fspro"
    Write-Host ""
    Write-Host "      Everything after that point is automated by this script."
    exit 9
}

# ---------- 1. technical asset ----------

if ($SkipAssetGeneration) {
    if (-not (Test-Path $Wav)) { Write-Host "FAIL: -SkipAssetGeneration set but $Wav is missing"; exit 9 }
    Write-Host "Using existing technical asset."
} else {
    Write-Host "Generating technical verification asset..."
    & powershell -NoProfile -ExecutionPolicy Bypass -File $AssetGen
    if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: asset generation failed."; exit $LASTEXITCODE }
}

# ---------- 2. author + build through the scripting API ----------

Write-Host ""
Write-Host "Authoring and building from $Fspro"
Write-Host ""

$Output = & $StudioExe -script $ScriptFile $Fspro 2>&1
$CliExit = $LASTEXITCODE
$Output | ForEach-Object { Write-Host $_ }

$Joined = ($Output | Out-String)

# fmodstudiocl has been observed exiting 0 on project-load failure, so the
# script's own RESULT marker is the authoritative signal, not the exit code.
if ($Joined -notmatch "RESULT OK") {
    Write-Host ""
    Write-Host "FAIL: authoring script did not report success (cli exit $CliExit)."
    exit 1
}

# ---------- 3. report bank output ----------

$StudioDir = Split-Path -Parent $Fspro
$Banks = Get-ChildItem -Path $StudioDir -Filter *.bank -Recurse -ErrorAction SilentlyContinue

Write-Host ""
if (-not $Banks) {
    Write-Host "FAIL: build reported success but no .bank files were found under"
    Write-Host "      $StudioDir"
    exit 1
}

Write-Host "Banks built:"
foreach ($b in $Banks) {
    $rel = $b.FullName.Substring($Project.Length).TrimStart('\')
    Write-Host ("  {0,-58} {1,9:N0} bytes" -f $rel, $b.Length)
}

Write-Host ""
Write-Host "Point FMOD for Unity at this bank output directory:"
Write-Host "  $($Banks[0].DirectoryName)"
Write-Host "  (FMOD -> Edit Settings -> Source Type: FMOD Studio Project / built banks)"
Write-Host ""
Write-Host "NOTE: banks built == event invocable. It does NOT mean audio was heard."
exit 0
