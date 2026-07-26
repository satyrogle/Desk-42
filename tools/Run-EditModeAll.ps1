# ============================================================
# DESK 42 — Full EditMode test run (headless)
#
# Runs the entire EditMode suite via Unity batchmode and prints
# a one-line summary plus any failing test names.
# ============================================================

param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$TestPlatform = "EditMode"
)

$ErrorActionPreference = "Stop"

$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$VersionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"

$VersionLine  = Get-Content $VersionFile | Where-Object { $_ -match "m_EditorVersion:" } | Select-Object -First 1
$UnityVersion = ($VersionLine -split ":\s*")[1].Trim()

$UnityExe = Join-Path "C:\Program Files\Unity\Hub\Editor" $UnityVersion
$UnityExe = Join-Path $UnityExe "Editor\Unity.exe"

Write-Host "Unity $UnityVersion"
Write-Host "Exe:  $UnityExe"

if (-not (Test-Path $UnityExe)) {
    Write-Host "FAIL: Unity Editor not found at $UnityExe"
    exit 9
}

$ResultsDir = Join-Path $ProjectPath "TestResults"
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

$Slug       = $TestPlatform.ToLower()
$ResultsXml = Join-Path $ResultsDir "$Slug-full.xml"
$LogFile    = Join-Path $ResultsDir "$Slug-full.log"
if (Test-Path $ResultsXml) { Remove-Item $ResultsXml }

$ArgString = "-runTests -batchmode -nographics " +
    "-projectPath `"$ProjectPath`" " +
    "-testPlatform $TestPlatform " +
    "-testResults `"$ResultsXml`" " +
    "-logFile `"$LogFile`""

$Process  = Start-Process -FilePath $UnityExe -ArgumentList $ArgString -Wait -PassThru -NoNewWindow
$ExitCode = $Process.ExitCode

if (-not (Test-Path $ResultsXml)) {
    Write-Host "FAIL: Unity exited ($ExitCode) before writing results. See $LogFile"
    exit $ExitCode
}

[xml]$Results = Get-Content $ResultsXml
$Run = $Results."test-run"

Write-Host "total=$($Run.total) passed=$($Run.passed) failed=$($Run.failed) skipped=$($Run.skipped) inconclusive=$($Run.inconclusive)"

if ([int]$Run.failed -ne 0) {
    $FailingCases = Select-Xml -Xml $Results -XPath "//test-case[@result='Failed']"
    foreach ($case in $FailingCases) {
        Write-Host "  FAILED: $($case.Node.fullname)"
    }
}

exit $ExitCode
