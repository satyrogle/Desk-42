param(
    [string]$RepositoryRoot = (Get-Location).Path,
    [string]$OutputPath,
    [string]$VerifyAgainst
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$institutionalRoot = Join-Path $resolvedRoot 'Assets/_Project/Scripts/Institutional'
if (-not (Test-Path -LiteralPath $institutionalRoot -PathType Container)) {
    throw "Institutional source root not found: $institutionalRoot"
}

$engineRoots = @(
    (Join-Path $institutionalRoot 'Domain'),
    (Join-Path $institutionalRoot 'Authority'),
    (Join-Path $institutionalRoot 'Runtime')
)
foreach ($engineRoot in $engineRoots) {
    if (-not (Test-Path -LiteralPath $engineRoot -PathType Container)) {
        throw "Institutional engine root not found: $engineRoot"
    }
}

$legacyProofPaths = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
@(
    'Assets/_Project/Scripts/Institutional/Domain/PrototypePopulationFactory.cs',
    'Assets/_Project/Scripts/Institutional/Domain/PrototypePopulationFactory.cs.meta',
    'Assets/_Project/Scripts/Institutional/Authority/InstitutionalConsequenceLoop.cs',
    'Assets/_Project/Scripts/Institutional/Authority/InstitutionalConsequenceLoop.cs.meta',
    'Assets/_Project/Scripts/Institutional/Authority/InstitutionalConsequenceValidator.cs',
    'Assets/_Project/Scripts/Institutional/Authority/InstitutionalConsequenceValidator.cs.meta'
) | ForEach-Object { [void]$legacyProofPaths.Add($_) }

$scenarioRoot = Join-Path $institutionalRoot 'Authority/Scenarios'
$concreteScenarioPrefixes = [System.Collections.Generic.List[string]]::new()
$concreteScenarioFolderMetas = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
if (Test-Path -LiteralPath $scenarioRoot -PathType Container) {
    Get-ChildItem -LiteralPath $scenarioRoot -Directory | ForEach-Object {
        $relative = $_.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
        $concreteScenarioPrefixes.Add($relative.TrimEnd('/') + '/')
        [void]$concreteScenarioFolderMetas.Add($relative.TrimEnd('/') + '.meta')
    }
}

$records = [System.Collections.Generic.List[object]]::new()
$seenPaths = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)

function Get-Sha256([byte[]]$Bytes) {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString(
            $sha256.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha256.Dispose()
    }
}

function Get-ProtectedFileHash([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ([System.Array]::IndexOf($bytes, [byte]0) -ge 0) {
        return Get-Sha256 $bytes
    }
    try {
        $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
        $content = $strictUtf8.GetString($bytes)
    } catch [System.Text.DecoderFallbackException] {
        return Get-Sha256 $bytes
    }
    $normalized = $content.Replace("`r`n", "`n").Replace("`r", "`n")
    $normalizedBytes = [System.Text.UTF8Encoding]::new($false).GetBytes($normalized)
    return Get-Sha256 $normalizedBytes
}

function Test-ConcreteScenarioPath([string]$RelativePath) {
    if ($concreteScenarioFolderMetas.Contains($RelativePath)) { return $true }
    foreach ($prefix in $concreteScenarioPrefixes) {
        if ($RelativePath.StartsWith(
                $prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Add-ProtectedFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Protected institutional input not found: $Path"
    }
    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    $relative = $fullPath.Substring($resolvedRoot.Length + 1).Replace('\', '/')
    if (-not $seenPaths.Add($relative)) {
        throw "Protected institutional input was enumerated twice: $relative"
    }
    $records.Add([pscustomobject]@{
        RelativePath = $relative
        Hash = Get-ProtectedFileHash $fullPath
    })
}

foreach ($engineRoot in $engineRoots) {
    Get-ChildItem -LiteralPath $engineRoot -Recurse -File |
        ForEach-Object {
            $relative = $_.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
            if ($legacyProofPaths.Contains($relative)) { return }
            if (Test-ConcreteScenarioPath $relative) { return }
            Add-ProtectedFile $_.FullName
        }
}

$testRoot = Join-Path $resolvedRoot 'Assets/_Project/Tests/EditMode'
Get-ChildItem -LiteralPath $testRoot -File | Where-Object {
    $_.Name -match '^Institutional.*\.cs(?:\.meta)?$' -or
    $_.Name -in @(
        'SocietyStateDeepCopyTests.cs',
        'SocietyStateDeepCopyTests.cs.meta',
        'Desk42.Tests.EditMode.asmdef',
        'Desk42.Tests.EditMode.asmdef.meta')
} | ForEach-Object { Add-ProtectedFile $_.FullName }

Add-ProtectedFile (Join-Path $resolvedRoot `
    'Docs/Institutional/ENGINE_CANDIDATE_BOUNDARY.md')
Add-ProtectedFile (Join-Path $resolvedRoot `
    '.github/workflows/institutional-proof.yml')
Get-ChildItem -LiteralPath (Join-Path $resolvedRoot 'tools/ci') -File -Filter '*.ps1' |
    ForEach-Object { Add-ProtectedFile $_.FullName }

if ($records.Count -eq 0) {
    throw 'Institutional engine manifest resolved no protected inputs.'
}
$manifestLines = $records |
    Sort-Object -Property RelativePath |
    ForEach-Object { "$($_.Hash)  $($_.RelativePath)" }
$manifest = ($manifestLines -join "`n") + "`n"

function Resolve-OutputPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot $Path))
}

$resolvedOutput = if ($OutputPath) { Resolve-OutputPath $OutputPath } else { $null }
$expectedPath = if ($VerifyAgainst) { Resolve-OutputPath $VerifyAgainst } else { $null }
if ($resolvedOutput -and $expectedPath -and [string]::Equals(
        $resolvedOutput,
        $expectedPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputPath and VerifyAgainst must be different files; verification may not overwrite its baseline.'
}

# Verify before writing any output so a failed comparison cannot mutate evidence.
if ($expectedPath) {
    if (-not (Test-Path -LiteralPath $expectedPath -PathType Leaf)) {
        throw "Institutional engine manifest baseline not found: $expectedPath"
    }
    $expected = [System.IO.File]::ReadAllText($expectedPath).Replace("`r`n", "`n")
    if ($expected -ne $manifest) {
        $actualLines = $manifest -split "`n"
        $expectedLines = $expected -split "`n"
        $difference = Compare-Object `
            -ReferenceObject $expectedLines `
            -DifferenceObject $actualLines
        $summary = ($difference | Select-Object -First 20 | Out-String).Trim()
        throw "Institutional engine manifest changed.`n$summary"
    }
}

if ($resolvedOutput) {
    $outputDirectory = Split-Path -Parent $resolvedOutput
    if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }
    [System.IO.File]::WriteAllText(
        $resolvedOutput,
        $manifest,
        [System.Text.UTF8Encoding]::new($false))
}

$manifest
