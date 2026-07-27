# ============================================================
# DESK 42 — Import the FMOD for Unity package (D1 binary step 1)
#
# Headless import of the supplied official Firelight .unitypackage.
# Does NOT define DESK42_FMOD and does NOT add assembly references —
# those are later, deliberate steps in the locked sequence.
# ============================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"

# Two levels up: this script lives in tools/fmod/, not tools/.
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$VersionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"

$VersionLine  = Get-Content $VersionFile | Where-Object { $_ -match "m_EditorVersion:" } | Select-Object -First 1
$UnityVersion = ($VersionLine -split ":\s*")[1].Trim()

$UnityExe = Join-Path "C:\Program Files\Unity\Hub\Editor" $UnityVersion
$UnityExe = Join-Path $UnityExe "Editor\Unity.exe"

if (-not (Test-Path $UnityExe))  { Write-Host "FAIL: Unity not found at $UnityExe"; exit 9 }
if (-not (Test-Path $PackagePath)) { Write-Host "FAIL: package not found at $PackagePath"; exit 9 }

$ResultsDir = Join-Path $ProjectPath "TestResults"
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
$LogFile = Join-Path $ResultsDir "fmod-import.log"
if (Test-Path $LogFile) { Remove-Item $LogFile }

Write-Host "Unity   $UnityVersion"
Write-Host "Project $ProjectPath"
Write-Host "Package $PackagePath"

$ArgString = "-batchmode -quit -nographics " +
    "-projectPath `"$ProjectPath`" " +
    "-importPackage `"$PackagePath`" " +
    "-logFile `"$LogFile`""

$Process  = Start-Process -FilePath $UnityExe -ArgumentList $ArgString -Wait -PassThru -NoNewWindow
$ExitCode = $Process.ExitCode

Write-Host "ExitCode: $ExitCode"

if (Test-Path $LogFile) {
    $errors = Select-String -Path $LogFile -Pattern "error CS|Exception|Failed to import" -SimpleMatch:$false |
              Select-Object -First 10
    if ($errors) {
        Write-Host "--- import diagnostics ---"
        $errors | ForEach-Object { Write-Host $_.Line }
    }
}

exit $ExitCode
