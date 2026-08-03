[CmdletBinding()]
param(
    [string]$ScenarioCommit = 'HEAD',
    [string]$ScenarioName = 'GlassCanal'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$CandidateTag = 'institutional-engine-candidate-v0.3.1'
$BaselineRelativePath =
    'evidence/InstitutionalEngine/v0.3.1/engine-manifest.sha256'

$repositoryRoot = (& git rev-parse --show-toplevel 2>&1).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "The generalisation gate must run inside a Git worktree.`n$repositoryRoot"
}
$repositoryRoot = (Resolve-Path -LiteralPath $repositoryRoot).Path

$policyScript = Join-Path $PSScriptRoot 'InstitutionalGatePolicy.ps1'
if (-not (Test-Path -LiteralPath $policyScript -PathType Leaf)) {
    throw "Institutional gate policy not found: $policyScript"
}
. $policyScript
$pathPolicy = Get-InstitutionalScenarioPathPolicy $ScenarioName

function Invoke-CheckedGit([string[]]$Arguments) {
    $previousPreference = $ErrorActionPreference
    $output = @()
    $exitCode = -1
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& git -C $repositoryRoot @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($exitCode -ne 0) {
        $detail = ($output | ForEach-Object { $_.ToString() }) -join "`n"
        throw "Git command failed: git $($Arguments -join ' ')`n$detail"
    }
    return @($output | ForEach-Object { $_.ToString() })
}

function Resolve-GitCommit([string]$Revision) {
    $output = @(Invoke-CheckedGit @('rev-parse', '--verify', "$Revision^{commit}"))
    $resolved = ($output -join "`n").Trim()
    if ($resolved -notmatch '^[0-9a-fA-F]{40,64}$') {
        throw "Git revision did not resolve to one commit: $Revision`n$resolved"
    }
    return $resolved.ToLowerInvariant()
}

function Resolve-GitObject([string]$ObjectSpec) {
    $output = @(Invoke-CheckedGit @('rev-parse', '--verify', $ObjectSpec))
    $resolved = ($output -join "`n").Trim()
    if ($resolved -notmatch '^[0-9a-fA-F]{40,64}$') {
        throw "Git object did not resolve uniquely: $ObjectSpec`n$resolved"
    }
    return $resolved.ToLowerInvariant()
}

function Test-GitTreeAt([string]$Revision, [string]$Path) {
    $output = @(& git -C $repositoryRoot cat-file -t "${Revision}:$Path" 2>$null)
    return $LASTEXITCODE -eq 0 -and
        (($output | ForEach-Object { $_.ToString() }) -join '').Trim() -eq 'tree'
}

function Test-GitRegularBlobAt([string]$Revision, [string]$Path) {
    $literalPathspec = ":(literal)$Path"
    $output = @(& git -C $repositoryRoot ls-tree $Revision -- $literalPathspec 2>$null)
    if ($LASTEXITCODE -ne 0 -or $output.Count -ne 1) { return $false }
    $line = $output[0].ToString()
    return $line -match '^100644 blob [0-9a-fA-F]{40,64}\t'
}

function Test-ExactFolderMetaAt([string]$Revision, [string]$Path) {
    if (-not $Path.EndsWith(
            '.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }
    $directoryPath = $Path.Substring(0, $Path.Length - '.meta'.Length)
    return (Test-GitRegularBlobAt $Revision $Path) -and
        (Test-GitTreeAt $Revision $directoryPath)
}

function Test-ExactAssetMetaAt([string]$Revision, [string]$Path) {
    if (-not $Path.EndsWith(
            '.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }
    $assetPath = $Path.Substring(0, $Path.Length - '.meta'.Length)
    return (Test-GitRegularBlobAt $Revision $Path) -and
        (Test-GitRegularBlobAt $Revision $assetPath)
}

function Test-AllowedScenarioPath(
    [string]$Revision,
    [string]$Path) {
    $normalized = $Path.Replace('\', '/').TrimStart('/')
    if ($normalized.Contains("`t") -or $normalized.Contains("`n") -or
        $normalized.Contains("`r") -or $normalized.Contains('/../') -or
        $normalized.StartsWith('../', [System.StringComparison]::Ordinal)) {
        return $false
    }

    foreach ($entry in @(
            [pscustomobject]@{
                Prefix = $pathPolicy.SourcePrefix
                FolderMeta = $pathPolicy.SourceFolderMeta
            },
            [pscustomobject]@{
                Prefix = $pathPolicy.TestPrefix
                FolderMeta = $pathPolicy.TestFolderMeta
            })) {
        if ([string]::Equals(
                $normalized, $entry.FolderMeta,
                [System.StringComparison]::Ordinal)) {
            return Test-ExactFolderMetaAt $Revision $normalized
        }
        if (-not $normalized.StartsWith(
                $entry.Prefix, [System.StringComparison]::Ordinal)) {
            continue
        }
        if (Test-InstitutionalScenarioCodeAssetPath $normalized) {
            if ($normalized.EndsWith(
                    '.cs.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
                return Test-ExactAssetMetaAt $Revision $normalized
            }
            return Test-GitRegularBlobAt $Revision $normalized
        }
        return Test-ExactFolderMetaAt $Revision $normalized
    }

    $presentationMetas = @($pathPolicy.PresentationInfrastructureMetas) +
        @($pathPolicy.PresentationFolderMeta)
    if ($presentationMetas -contains $normalized) {
        return Test-ExactFolderMetaAt $Revision $normalized
    }
    if (-not $normalized.StartsWith(
            $pathPolicy.PresentationPrefix,
            [System.StringComparison]::Ordinal)) {
        return $false
    }
    if (Test-ExactFolderMetaAt $Revision $normalized) { return $true }
    if (-not (Test-InstitutionalPresentationAssetPath $normalized)) {
        return $false
    }
    if ($normalized.EndsWith(
            '.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
        return Test-ExactAssetMetaAt $Revision $normalized
    }
    return Test-GitRegularBlobAt $Revision $normalized
}

$candidate = Resolve-GitCommit $CandidateTag
$scenario = Resolve-GitCommit $ScenarioCommit
$head = Resolve-GitCommit 'HEAD'
if (-not [string]::Equals($head, $scenario, [System.StringComparison]::Ordinal)) {
    throw "ScenarioCommit must be the checked-out HEAD. HEAD=$head scenario=$scenario"
}

$workingChanges = @(Invoke-CheckedGit @(
        'status', '--porcelain=v1', '--untracked-files=all'))
if ($workingChanges.Count -gt 0) {
    throw "The generalisation gate requires a clean worktree, including no untracked files:`n$($workingChanges -join "`n")"
}

& git -C $repositoryRoot merge-base --is-ancestor $candidate $scenario
if ($LASTEXITCODE -ne 0) {
    throw "Engine candidate $candidate is not an ancestor of scenario commit $scenario."
}

$baselinePath = Join-Path $repositoryRoot $BaselineRelativePath
if (-not (Test-Path -LiteralPath $baselinePath -PathType Leaf)) {
    throw "Frozen candidate baseline is mandatory: $baselinePath"
}
$candidateBaseline = Resolve-GitObject "${candidate}:$BaselineRelativePath"
$scenarioBaseline = Resolve-GitObject "${scenario}:$BaselineRelativePath"
if (-not [string]::Equals(
        $candidateBaseline, $scenarioBaseline,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The baseline Git blob must remain byte-identical to $CandidateTag. candidate=$candidateBaseline scenario=$scenarioBaseline"
}

$changedPaths = [System.Collections.Generic.List[string]]::new()
$sourceChanged = $false
$testsChanged = $false

if (-not [string]::Equals(
        $candidate, $scenario, [System.StringComparison]::Ordinal)) {
    $mergeCommits = @(Invoke-CheckedGit @(
            'rev-list', '--reverse', '--merges', "$candidate..$scenario"))
    if ($mergeCommits.Count -gt 0) {
        throw "The evidence range may not contain merge commits:`n$($mergeCommits -join "`n")"
    }
    $commits = @(Invoke-CheckedGit @(
            'rev-list', '--reverse', '--no-merges', "$candidate..$scenario"))
    if ($commits.Count -eq 0) {
        throw 'The scenario commit contains no non-merge changes after the engine candidate.'
    }

    foreach ($commit in $commits) {
        $parent = Resolve-GitCommit "$commit^"
        $changeLines = @(Invoke-CheckedGit @(
                '-c', 'core.quotePath=false', 'diff-tree',
                '--no-commit-id', '--name-status', '-r', '-M',
                $parent, $commit))
        foreach ($line in $changeLines) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $parts = $line -split "`t"
            $status = $parts[0]
            if ($status.StartsWith('D', [System.StringComparison]::Ordinal)) {
                throw "The second-scenario history deletes a path in $commit`: $line"
            }
            if ($status -notmatch '^(?:A|M|R[0-9]{1,3})$') {
                throw "Unsupported or unsafe Git change status in $commit`: $line"
            }
            $pathEntries = if ($status.StartsWith(
                    'R', [System.StringComparison]::Ordinal)) {
                if ($parts.Count -ne 3) {
                    throw "Malformed rename record in $commit`: $line"
                }
                @(
                    [pscustomobject]@{ Revision = $parent; Path = $parts[1] },
                    [pscustomobject]@{ Revision = $commit; Path = $parts[2] })
            } else {
                if ($parts.Count -ne 2) {
                    throw "Malformed change record in $commit`: $line"
                }
                @([pscustomobject]@{ Revision = $commit; Path = $parts[1] })
            }
            foreach ($pathEntry in $pathEntries) {
                $path = $pathEntry.Path.Replace('\', '/').TrimStart('/')
                if (-not (Test-AllowedScenarioPath $pathEntry.Revision $path)) {
                    throw "Second-scenario commit $commit changed a frozen or executable path: $path"
                }
                $changedPaths.Add($path)
                if ($path.StartsWith(
                        $pathPolicy.SourcePrefix,
                        [System.StringComparison]::Ordinal) -and
                    (Test-InstitutionalScenarioCodeAssetPath $path)) {
                    $sourceChanged = $true
                }
                if ($path.StartsWith(
                        $pathPolicy.TestPrefix,
                        [System.StringComparison]::Ordinal) -and
                    (Test-InstitutionalScenarioCodeAssetPath $path)) {
                    $testsChanged = $true
                }
            }
        }
    }

    if (-not $sourceChanged) {
        throw "No $ScenarioName scenario source changed below $($pathPolicy.SourcePrefix)"
    }
    if (-not $testsChanged) {
        throw "No $ScenarioName scenario tests changed below $($pathPolicy.TestPrefix)"
    }
}

$authoringTool = Join-Path $repositoryRoot `
    'tools/ci/Assert-InstitutionalScenarioAuthoringBoundary.ps1'
& $authoringTool -RepositoryRoot $repositoryRoot | Out-Null

$manifestTool = Join-Path $repositoryRoot `
    'tools/ci/Get-InstitutionalEngineManifest.ps1'
& $manifestTool -RepositoryRoot $repositoryRoot `
    -VerifyAgainst $baselinePath | Out-Null

if ([string]::Equals(
        $candidate, $scenario, [System.StringComparison]::Ordinal)) {
    Write-Host "Institutional engine-candidate baseline verified at $CandidateTag."
} else {
    Write-Host 'Institutional generalisation gate passed.'
}
Write-Host "engineCandidate=$candidate"
Write-Host "scenarioCommit=$scenario"
Write-Host "scenario=$ScenarioName"
Write-Host "changedPaths=$($changedPaths.Count)"
