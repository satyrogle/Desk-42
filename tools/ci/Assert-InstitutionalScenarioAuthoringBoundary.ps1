param(
    [string]$RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
$policyScript = Join-Path $PSScriptRoot 'InstitutionalGatePolicy.ps1'
if (-not (Test-Path -LiteralPath $policyScript -PathType Leaf)) {
    throw "Institutional gate policy not found: $policyScript"
}
. $policyScript

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$scenarioRoot = Join-Path $resolvedRoot `
    'Assets/_Project/Scripts/Institutional/Authority/Scenarios'
$scenarioAssemblyPath = Join-Path $scenarioRoot `
    'Desk42.Institutional.Scenarios.asmdef'
$scenarioTestRoot = Join-Path $resolvedRoot `
    'Assets/_Project/Tests/EditMode/Scenarios'
$presentationRoot = Join-Path $resolvedRoot `
    'Assets/_Project/Presentation/Institutional/Scenarios'

if (-not (Test-Path -LiteralPath $scenarioRoot -PathType Container)) {
    throw "Scenario source root not found: $scenarioRoot"
}
if (-not (Test-Path -LiteralPath $scenarioAssemblyPath -PathType Leaf)) {
    throw "Scenario assembly boundary not found: $scenarioAssemblyPath"
}

$assembly = Get-Content -LiteralPath $scenarioAssemblyPath -Raw | ConvertFrom-Json
$expectedReferences = @(
    'Desk42.Institutional.Domain'
) | Sort-Object
$actualReferences = @($assembly.references) | Sort-Object
$referenceDifference = Compare-Object `
    -ReferenceObject $expectedReferences `
    -DifferenceObject $actualReferences
if ($assembly.name -ne 'Desk42.Institutional.Scenarios' -or
    $referenceDifference -or
    $assembly.autoReferenced -ne $false -or
    $assembly.noEngineReferences -ne $true -or
    $assembly.allowUnsafeCode -ne $false) {
    throw 'Scenario asmdef must be a non-auto-referenced, no-engine, safe assembly referencing only Domain.'
}

$nestedAssemblies = Get-ChildItem -LiteralPath $scenarioRoot -Recurse -File `
    -Filter '*.asmdef' | Where-Object {
        -not [string]::Equals(
            $_.FullName,
            $scenarioAssemblyPath,
            [System.StringComparison]::OrdinalIgnoreCase)
    }
if ($nestedAssemblies) {
    $paths = ($nestedAssemblies.FullName -join "`n")
    throw "Concrete scenarios may not replace the scenario assembly boundary:`n$paths"
}

function Test-ExactFolderMeta([System.IO.FileInfo]$File) {
    if (-not $File.Name.EndsWith(
            '.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }
    $assetPath = $File.FullName.Substring(0, $File.FullName.Length - '.meta'.Length)
    return Test-Path -LiteralPath $assetPath -PathType Container
}

function Test-ExactAssetMeta([System.IO.FileInfo]$File, [scriptblock]$AssetPredicate) {
    if (-not $File.Name.EndsWith(
            '.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }
    $assetPath = $File.FullName.Substring(0, $File.FullName.Length - '.meta'.Length)
    return (Test-Path -LiteralPath $assetPath -PathType Leaf) -and
        (& $AssetPredicate $assetPath)
}

function Assert-ScenarioCodeAssetTree([string]$Root, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { return }
    $invalid = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem -LiteralPath $Root -Recurse -File | ForEach-Object {
        $file = $_
        if (Test-InstitutionalScenarioCodeAssetPath $file.FullName) {
            if ($file.Name.EndsWith(
                    '.cs.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
                $sourcePath = $file.FullName.Substring(
                    0, $file.FullName.Length - '.meta'.Length)
                if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
                    $invalid.Add("orphan C# metadata: $($file.FullName)")
                }
            }
            return
        }
        if (Test-ExactFolderMeta $file) { return }
        $invalid.Add($file.FullName)
    }
    if ($invalid.Count -gt 0) {
        throw "$Label may contain only .cs, exact .cs.meta and exact folder metadata:`n$($invalid -join "`n")"
    }
}

function Assert-PresentationAssetTree([string]$Root) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { return }
    $invalid = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem -LiteralPath $Root -Recurse -File | ForEach-Object {
        $file = $_
        if (Test-ExactFolderMeta $file) { return }
        if (Test-InstitutionalPresentationAssetPath $file.FullName) {
            if ($file.Name.EndsWith(
                    '.meta', [System.StringComparison]::OrdinalIgnoreCase) -and
                -not (Test-ExactAssetMeta $file {
                    param($assetPath)
                    Test-InstitutionalPresentationAssetPath $assetPath
                })) {
                $invalid.Add("orphan or unsafe asset metadata: $($file.FullName)")
            }
            return
        }
        $invalid.Add($file.FullName)
    }
    if ($invalid.Count -gt 0) {
        $extensions = (Get-InstitutionalPresentationAssetExtensions) -join ', '
        throw "Scenario presentation fixtures contain executable or unsupported assets. Allowed: $extensions`n$($invalid -join "`n")"
    }
}

function Assert-NamedChildRoot([string]$Root, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { return }
    $invalid = @(Get-ChildItem -LiteralPath $Root -File | Where-Object {
        -not (Test-ExactFolderMeta $_)
    })
    if ($invalid.Count -gt 0) {
        throw "$Label files must live in named child directories:`n$($invalid.FullName -join "`n")"
    }
}

$scenarioAssemblyName = 'Desk42.Institutional.Scenarios'
$friendPattern = 'InternalsVisibleTo\s*\(\s*"' +
    [regex]::Escape($scenarioAssemblyName) + '(?:,|\")'
$engineAssemblyInfo = @(
    (Join-Path $resolvedRoot 'Assets/_Project/Scripts/Institutional/Domain/AssemblyInfo.cs'),
    (Join-Path $resolvedRoot 'Assets/_Project/Scripts/Institutional/Authority/AssemblyInfo.cs')
)
foreach ($path in $engineAssemblyInfo) {
    if ((Get-Content -LiteralPath $path -Raw) -match $friendPattern) {
        throw "Engine internals must not be exposed to scenario content: $path"
    }
}

$allowedRootFiles = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
@(
    'Desk42.Institutional.Scenarios.asmdef',
    'Desk42.Institutional.Scenarios.asmdef.meta',
    'ScenarioAssemblyBoundary.cs',
    'ScenarioAssemblyBoundary.cs.meta'
) | ForEach-Object { [void]$allowedRootFiles.Add($_) }

$scenarioDirectoryNames = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
Get-ChildItem -LiteralPath $scenarioRoot -Directory |
    ForEach-Object { [void]$scenarioDirectoryNames.Add($_.Name) }
$unexpectedRootFiles = Get-ChildItem -LiteralPath $scenarioRoot -File |
    Where-Object {
        if ($allowedRootFiles.Contains($_.Name)) { return $false }
        if ($_.Extension -eq '.meta') {
            $directoryName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
            if ($scenarioDirectoryNames.Contains($directoryName)) { return $false }
        }
        return $true
    }
if ($unexpectedRootFiles) {
    $paths = ($unexpectedRootFiles.FullName -join "`n")
    throw "Concrete scenario files must live in a named child directory:`n$paths"
}

Get-ChildItem -LiteralPath $scenarioRoot -Directory | ForEach-Object {
    Assert-ScenarioCodeAssetTree $_.FullName "Scenario source '$($_.Name)'"
}
Assert-NamedChildRoot $scenarioTestRoot 'Scenario test'
Assert-ScenarioCodeAssetTree $scenarioTestRoot 'Scenario tests'
Assert-NamedChildRoot $presentationRoot 'Scenario presentation'
Assert-PresentationAssetTree $presentationRoot

$transitionTypes = (Get-InstitutionalTransitionTypeNames | ForEach-Object {
    [regex]::Escape($_)
}) -join '|'
$forbiddenPatterns = [ordered]@{
    'institutional transition service' = "\b(?:$transitionTypes)\b"
    'engine execution from scenario content' = '\b(?:InstitutionalScenarioEngine|RunScenario)\b'
    'authority-state access' = '\b(?:InstitutionalConsequenceRun|ExclusiveEntitlementRegistry|ExclusiveEntitlementTransferResult|StatusMutationResult)\b'
    'public report access or mutation' = '\bInstitutionalConsequenceReport\b|\.Report\b|\.(?:ObservedAgentActions|EvidenceArtifacts|OfficialFindings|Rulings|OfficialStatusMutations|DescendantCases|Appeals|Holdings|RelianceObservations|MaterialConsequences|ConnectedOutcomes|ExclusiveEntitlements|WorkAllocations|Timeline)\b'
    'institutional outcome construction' = '\bnew\s+(?:EvidenceArtifact|OfficialFinding|Ruling|OfficialStatusMutation|DescendantCase|Appeal|Holding|RelianceObservation|MaterialConsequence|ConnectedOutcomePair|ExclusiveEntitlementObservation|InstitutionalTimelineEntry)\b'
    'reflection escape hatch' = '\b(?:System\.Reflection|BindingFlags|Activator\.CreateInstance|Assembly\.Load|GetField|GetMethod|MethodInfo|FieldInfo|dynamic)\b'
}

$violations = [System.Collections.Generic.List[string]]::new()
$concreteSources = @(Get-ChildItem -LiteralPath $scenarioRoot -Directory |
    ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Filter '*.cs' })
foreach ($source in $concreteSources) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $source.FullName) {
        $lineNumber++
        foreach ($entry in $forbiddenPatterns.GetEnumerator()) {
            if ($line -match $entry.Value) {
                $relative = $source.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
                $violations.Add("$relative`:$lineNumber [$($entry.Key)] $($line.Trim())")
            }
        }
    }
}

if ($violations.Count -gt 0) {
    throw "Scenario source crossed the data-only authoring boundary:`n$($violations -join "`n")"
}

$outcomeTypes = (Get-InstitutionalOutcomeTypeNames | ForEach-Object {
    [regex]::Escape($_)
}) -join '|'
$reportCollections = (Get-InstitutionalReportCollectionNames | ForEach-Object {
    [regex]::Escape($_)
}) -join '|'
$testForbiddenPatterns = [ordered]@{
    'direct transition-service use' = "\b(?:$transitionTypes)\b"
    'institutional outcome construction' = "\bnew\s+(?:$outcomeTypes)\b"
    'report collection replacement' = "\.(?:$reportCollections)\s*="
    'report outcome replacement' = "\.(?:$reportCollections)\s*\[[^\]]+\](?:\.[A-Za-z_][A-Za-z0-9_]*)?\s*="
    'report outcome mutation' = "\.(?:$reportCollections)\b.*\.(?:Add|AddRange|Clear|Insert|Remove|RemoveAll|RemoveAt|Reverse|Sort)\s*\("
    'reflection escape hatch' = '\b(?:System\.Reflection|BindingFlags|Activator\.CreateInstance|Assembly\.Load|GetField|GetMethod|MethodInfo|FieldInfo|dynamic)\b'
}

$testViolations = [System.Collections.Generic.List[string]]::new()
$scenarioTestSources = @(if (Test-Path -LiteralPath $scenarioTestRoot -PathType Container) {
    Get-ChildItem -LiteralPath $scenarioTestRoot -Recurse -File -Filter '*.cs'
})
foreach ($source in $scenarioTestSources) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $source.FullName) {
        $lineNumber++
        foreach ($entry in $testForbiddenPatterns.GetEnumerator()) {
            if ($line -match $entry.Value) {
                $relative = $source.FullName.Substring(
                    $resolvedRoot.Length + 1).Replace('\', '/')
                $testViolations.Add(
                    "$relative`:$lineNumber [$($entry.Key)] $($line.Trim())")
            }
        }
    }
}
if ($testViolations.Count -gt 0) {
    throw "Scenario tests may invoke the engine and validators and read reports, but may not perform institutional transitions or construct/mutate outcomes:`n$($testViolations -join "`n")"
}

Write-Output "Institutional scenario authoring boundary passed ($($concreteSources.Count) source file(s), $($scenarioTestSources.Count) scenario test file(s))."
