# ============================================================
# DESK 42 — Fast EditMode test loop (BSM + Synergy)
#
# Runs only the state-machine and synergy/entropy test fixtures
# headless, via Unity batchmode, and prints a one-line summary.
# Intended for the PostToolUse hook (fast iteration) and for
# manual use during BSM/SynergyResolver work.
# ============================================================

$ErrorActionPreference = "Stop"

$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$VersionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"

if (-not (Test-Path $VersionFile)) {
    Write-Error "Could not find ProjectVersion.txt at $VersionFile"
    exit 1
}

$VersionLine = Get-Content $VersionFile | Where-Object { $_ -match "m_EditorVersion:" } | Select-Object -First 1
$UnityVersion = ($VersionLine -split ":\s*")[1].Trim()

$UnityExe = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
if (-not (Test-Path $UnityExe)) {
    Write-Error "Unity Editor $UnityVersion not found at $UnityExe. Edit `$UnityExe in this script if installed elsewhere."
    exit 1
}

$ResultsDir = Join-Path $ProjectPath "TestResults"
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
$ResultsXml = Join-Path $ResultsDir "bsm-synergy.xml"
$LogFile = Join-Path $ResultsDir "bsm-synergy.log"

$TestFilter = "Desk42.Tests.EditMode.BSMTests;Desk42.Tests.EditMode.SynergyResolverTests"

# Start-Process is used instead of the call operator because the call
# operator's $LASTEXITCODE / return timing proved unreliable here.
# ArgumentList as an array does not reliably quote elements containing
# spaces (the project path does), so build one pre-quoted command-line
# string instead — Windows native argv parsing handles that correctly.
$ArgString = "-runTests -batchmode -nographics " +
    "-projectPath `"$ProjectPath`" " +
    "-testPlatform EditMode " +
    "-testFilter `"$TestFilter`" " +
    "-testResults `"$ResultsXml`" " +
    "-logFile `"$LogFile`""

$Process = Start-Process -FilePath $UnityExe -ArgumentList $ArgString -Wait -PassThru -NoNewWindow
$ExitCode = $Process.ExitCode

if (-not (Test-Path $ResultsXml)) {
    Write-Host "FAIL: Unity exited ($ExitCode) before writing results. See $LogFile"
    exit $ExitCode
}

[xml]$Results = Get-Content $ResultsXml
$Run = $Results."test-run"
$Total = $Run.total
$Passed = $Run.passed
$Failed = $Run.failed

if ([int]$Failed -eq 0) {
    Write-Host "PASS: $Passed/$Total (BSM + Synergy)"
} else {
    Write-Host "FAIL: $Passed/$Total passed, $Failed failed (BSM + Synergy)"
    $FailingCases = Select-Xml -Xml $Results -XPath "//test-case[@result='Failed']"
    foreach ($case in $FailingCases) {
        Write-Host "  FAILED: $($case.Node.fullname)"
    }
}

exit $ExitCode
