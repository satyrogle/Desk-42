[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsRoot,

    [string]$AllowedSkippedTests = "",

    [string]$RequiredFixturePrefixes = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $ResultsRoot)) {
    throw "Unity test-results directory does not exist: $ResultsRoot"
}

$documents = @()
foreach ($file in Get-ChildItem -LiteralPath $ResultsRoot -Recurse -File -Filter "*.xml") {
    try {
        [xml]$document = Get-Content -LiteralPath $file.FullName -Raw
    }
    catch {
        continue
    }

    if ($null -ne $document.DocumentElement -and
        $document.DocumentElement.Name -eq "test-run") {
        $documents += [pscustomobject]@{
            Path = $file.FullName
            Xml = $document
        }
    }
}

if ($documents.Count -eq 0) {
    throw "No NUnit test-run XML was found under $ResultsRoot"
}

$allowedSkips = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal
)
foreach ($name in $AllowedSkippedTests.Split(
    ';',
    [System.StringSplitOptions]::RemoveEmptyEntries)) {
    [void]$allowedSkips.Add($name.Trim())
}

$requiredPrefixes = @(
    $RequiredFixturePrefixes.Split(
        ';',
        [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { $_.Trim() }
)
$observedTests = [System.Collections.Generic.List[string]]::new()
$unexpectedSkips = [System.Collections.Generic.List[string]]::new()
$total = 0
$passed = 0
$failed = 0
$inconclusive = 0
$skipped = 0

foreach ($entry in $documents) {
    $run = $entry.Xml.DocumentElement
    $total += [int]$run.GetAttribute("total")
    $passed += [int]$run.GetAttribute("passed")
    $failed += [int]$run.GetAttribute("failed")
    $inconclusive += [int]$run.GetAttribute("inconclusive")
    $skipped += [int]$run.GetAttribute("skipped")

    foreach ($testCase in $entry.Xml.SelectNodes("//test-case")) {
        $fullName = $testCase.GetAttribute("fullname")
        if ([string]::IsNullOrWhiteSpace($fullName)) {
            $fullName = $testCase.GetAttribute("name")
        }
        if (-not [string]::IsNullOrWhiteSpace($fullName)) {
            $observedTests.Add($fullName)
        }

        $result = $testCase.GetAttribute("result")
        if (($result -eq "Skipped" -or $result -eq "Ignored") -and
            -not $allowedSkips.Contains($fullName)) {
            $unexpectedSkips.Add($fullName)
        }
    }
}

foreach ($prefix in $requiredPrefixes) {
    $found = $false
    foreach ($testName in $observedTests) {
        if ($testName.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            $found = $true
            break
        }
    }
    if (-not $found) {
        throw "Required fixture did not execute: $prefix"
    }
}

Write-Host "Unity test summary: total=$total passed=$passed failed=$failed inconclusive=$inconclusive skipped=$skipped"

if ($total -eq 0) {
    throw "Unity reported a zero-test run."
}
if ($failed -gt 0 -or $inconclusive -gt 0) {
    throw "Unity test run contains failures or inconclusive tests."
}
if ($unexpectedSkips.Count -gt 0) {
    throw "Unexpected skipped tests: $($unexpectedSkips -join '; ')"
}

Write-Host "Unity result contract passed."
