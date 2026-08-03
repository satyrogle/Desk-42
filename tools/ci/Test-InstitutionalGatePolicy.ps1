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

function Assert-ScenarioTestRejected([string]$Name, [string]$SourceText) {
    $violations = @(Get-InstitutionalScenarioTestViolations $SourceText)
    Assert-True ($violations.Count -gt 0) `
        "scenario-test mutation fixture should be rejected: $Name"
}

function Assert-ScenarioTestAccepted([string]$Name, [string]$SourceText) {
    $violations = @(Get-InstitutionalScenarioTestViolations $SourceText)
    $detail = ($violations | ForEach-Object { $_.Reason }) -join ', '
    Assert-True ($violations.Count -eq 0) `
        "read-only scenario-test fixture should pass: $Name ($detail)"
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
Assert-True ((Get-InstitutionalTransitionTypeNames) -contains
    'InstitutionalEvidenceActivatedCaseService') `
    'evidence-activation transition denylist is incomplete'
Assert-True ((Get-InstitutionalOutcomeTypeNames) -contains
    'InstitutionalConsequenceReport') 'outcome-construction denylist is incomplete'
Assert-True ((Get-InstitutionalOutcomeTypeNames) -contains
    'InstitutionalCaseOpening') 'case-opening outcome denylist is incomplete'
Assert-True ((Get-InstitutionalReportCollectionNames) -contains
    'ConnectedOutcomes') 'report-mutation denylist is incomplete'
Assert-True ((Get-InstitutionalReportCollectionNames) -contains
    'CaseOpenings') 'case-opening collection denylist is incomplete'

Assert-ScenarioTestRejected 'direct activation service' @'
InstitutionalEvidenceActivatedCaseService.OpenDueCases(context, 1);
'@
Assert-ScenarioTestRejected 'opening construction' @'
var opening = new InstitutionalCaseOpening();
'@
Assert-ScenarioTestRejected 'multiline direct collection mutation' @'
result.Report
    .CaseOpenings
    .Add(opening);
'@
Assert-ScenarioTestRejected 'typed opening alias field mutation' @'
InstitutionalCaseOpening opening = result.Report.CaseOpenings.Single();
opening.CaseId = "case.forged";
'@
Assert-ScenarioTestRejected 'typed ruling alias nested mutation' @'
Ruling appellate = FindRuling(result);
appellate.CitedHoldingIds.Add("holding.forged");
'@
Assert-ScenarioTestRejected 'var opening alias increment' @'
var opening = result.Report.CaseOpenings.Single();
opening.OpenedCycle++;
'@
Assert-ScenarioTestRejected 'foreach outcome alias mutation' @'
foreach (Ruling ruling in result.Report.Rulings)
{
    ruling.CitedHoldingIds.Add("holding.forged");
}
'@
Assert-ScenarioTestRejected 'method parameter outcome mutation' @'
private static void Corrupt(Ruling ruling)
{
    ruling.CitedHoldingIds.Add("holding.forged");
}
Corrupt(result.Report.Rulings.Single());
'@
Assert-ScenarioTestRejected 'lambda parameter outcome mutation' @'
result.Report.Rulings.ForEach(ruling =>
    ruling.CitedHoldingIds.Add("holding.forged"));
'@
Assert-ScenarioTestRejected 'typed report collection alias mutation' @'
List<Ruling> rows = result.Report.Rulings;
rows.Clear();
'@
Assert-ScenarioTestRejected 'var report collection alias mutation' @'
var rows = result.Report.Rulings;
rows.Clear();
'@
Assert-ScenarioTestAccepted 'read-only report assertions' @'
InstitutionalCaseOpening opening = result.Report.CaseOpenings.Single();
Assert.That(opening.CaseId, Is.EqualTo("case.expected"));
Ruling appellate = result.Report.Rulings.Single();
Assert.That(appellate.CitedHoldingIds, Is.Empty);
var ids = result.Report.CaseOpenings.Select(value => value.CaseId).ToArray();
'@
Assert-ScenarioTestAccepted 'read-only foreach and helper assertions' @'
foreach (Ruling ruling in result.Report.Rulings)
{
    Assert.That(ruling.CitedHoldingIds, Is.Not.Null);
}
private static void AssertRuling(Ruling ruling)
{
    Assert.That(ruling.CaseId, Is.Not.Empty);
}
AssertRuling(result.Report.Rulings.Single());
List<Ruling> rows = result.Report.Rulings;
Assert.That(rows.Count, Is.GreaterThanOrEqualTo(0));
'@

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
Assert-True ($gateText.Contains("'institutional-engine-candidate-v0.3.1'")) `
    'candidate tag is not pinned'
Assert-True ($gateText.Contains(
        "'evidence/InstitutionalEngine/v0.3.1/engine-manifest.sha256'")) `
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
