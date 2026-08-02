[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$policyScript = Join-Path $PSScriptRoot 'InstitutionalGatePolicy.ps1'
. $policyScript

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Institutional gate policy self-test failed: $Message" }
}

function Assert-False([bool]$Condition, [string]$Message) {
    Assert-True (-not $Condition) $Message
}

$paths = Get-InstitutionalScenarioPathPolicy 'GlassCanal'
Assert-True ($paths.SourceFolderMeta -eq
    'Assets/_Project/Scripts/Institutional/Authority/Scenarios/GlassCanal.meta') `
    'source folder metadata path drifted'
Assert-True ($paths.TestFolderMeta -eq
    'Assets/_Project/Tests/EditMode/Scenarios/GlassCanal.meta') `
    'test folder metadata path drifted'
Assert-True ($paths.PresentationFolderMeta -eq
    'Assets/_Project/Presentation/Institutional/Scenarios/GlassCanal.meta') `
    'presentation folder metadata path drifted'

foreach ($path in @('Definition.cs', 'Definition.cs.meta')) {
    Assert-True (Test-InstitutionalScenarioCodeAssetPath $path) `
        "scenario code asset should be accepted: $path"
}
foreach ($path in @(
        'Escape.asmdef', 'Escape.asmref', 'Injected.dll', 'data.json',
        'image.png', 'script.csx', 'compiler.rsp')) {
    Assert-False (Test-InstitutionalScenarioCodeAssetPath $path) `
        "executable or non-code scenario asset should be rejected: $path"
}

foreach ($path in @(
        'screen.png', 'screen.png.meta', 'layout.uxml', 'style.uss',
        'expected.json', 'audio.ogg', 'caption.txt')) {
    Assert-True (Test-InstitutionalPresentationAssetPath $path) `
        "non-executable presentation asset should be accepted: $path"
}
foreach ($path in @(
        'RuntimeBehaviour.cs', 'Escape.asmdef', 'Escape.asmref', 'Injected.dll',
        'GPU.compute', 'Effect.shader', 'Scene.unity', 'Object.prefab',
        'Data.asset', 'Animator.controller')) {
    Assert-False (Test-InstitutionalPresentationAssetPath $path) `
        "executable presentation asset should be rejected: $path"
}

Assert-True ((Get-InstitutionalTransitionTypeNames) -contains
    'InstitutionalAdjudicationService') 'transition-service denylist is incomplete'
Assert-True ((Get-InstitutionalOutcomeTypeNames) -contains
    'InstitutionalConsequenceReport') 'outcome-construction denylist is incomplete'
Assert-True ((Get-InstitutionalReportCollectionNames) -contains
    'ConnectedOutcomes') 'report-mutation denylist is incomplete'

$scriptPaths = @(
    'Assert-InstitutionalGeneralisationGate.ps1',
    'Assert-InstitutionalScenarioAuthoringBoundary.ps1',
    'Get-InstitutionalEngineManifest.ps1',
    'InstitutionalGatePolicy.ps1',
    'Test-InstitutionalGatePolicy.ps1') | ForEach-Object {
        Join-Path $PSScriptRoot $_
    }
foreach ($scriptPath in $scriptPaths) {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        $scriptPath, [ref]$tokens, [ref]$parseErrors) | Out-Null
    Assert-True (@($parseErrors).Count -eq 0) `
        "PowerShell parse errors in $scriptPath"
}

$gatePath = Join-Path $PSScriptRoot 'Assert-InstitutionalGeneralisationGate.ps1'
$gateText = Get-Content -LiteralPath $gatePath -Raw
Assert-True ($gateText.Contains("'institutional-engine-candidate-v0.1'")) `
    'candidate tag is not pinned'
Assert-True ($gateText.Contains(
        "'evidence/InstitutionalEngine/v0.1/engine-manifest.sha256'")) `
    'candidate baseline path is not pinned'
Assert-False ($gateText.Contains('EngineManifestPath')) `
    'gate still accepts an external manifest path'
Assert-False ($gateText.Contains('EngineCandidateCommit')) `
    'gate still accepts an external candidate revision'
Assert-True ($gateText.Contains("'status', '--porcelain=v1', '--untracked-files=all'")) `
    'gate does not require a fully clean worktree'
Assert-True ($gateText.Contains("'rev-list', '--reverse', '--no-merges'")) `
    'gate does not inspect every non-merge commit'
Assert-True ($gateText.Contains("'rev-list', '--reverse', '--merges'")) `
    'gate does not reject merge commits'

$manifestPath = Join-Path $PSScriptRoot 'Get-InstitutionalEngineManifest.ps1'
$manifestText = Get-Content -LiteralPath $manifestPath -Raw
Assert-True ($manifestText.Contains('[System.IO.File]::ReadAllBytes')) `
    'manifest no longer supports raw binary hashing'
Assert-False ($manifestText.Contains("`$_.Extension -in")) `
    'manifest again filters protected engine inputs by extension'

$workflowPath = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) `
    '.github/workflows/institutional-proof.yml'
$workflowText = Get-Content -LiteralPath $workflowPath -Raw
Assert-True ($workflowText.Contains('fetch-depth: 0')) `
    'workflow does not fetch candidate history and tags'
Assert-True ($workflowText.Contains(
        './tools/ci/Assert-InstitutionalGeneralisationGate.ps1')) `
    'workflow does not execute the generalisation gate'
Assert-True ($workflowText.Contains('--untracked-files=all')) `
    'workflow hygiene ignores untracked Unity assets'
Assert-False ($workflowText.Contains('when present')) `
    'workflow still treats the frozen baseline as optional'

Write-Output 'Institutional gate policy self-tests passed.'
