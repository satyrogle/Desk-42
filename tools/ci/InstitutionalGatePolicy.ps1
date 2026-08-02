Set-StrictMode -Version Latest

function Assert-InstitutionalScenarioName([string]$ScenarioName) {
    if ([string]::IsNullOrWhiteSpace($ScenarioName) -or
        $ScenarioName -notmatch '^[A-Za-z][A-Za-z0-9_-]{0,63}$') {
        throw "ScenarioName must be one safe repository path segment: $ScenarioName"
    }
}

function Get-InstitutionalScenarioPathPolicy([string]$ScenarioName) {
    Assert-InstitutionalScenarioName $ScenarioName
    $sourcePrefix =
        "Assets/_Project/Scripts/Institutional/Authority/Scenarios/$ScenarioName/"
    $testPrefix = "Assets/_Project/Tests/EditMode/Scenarios/$ScenarioName/"
    $presentationPrefix =
        "Assets/_Project/Presentation/Institutional/Scenarios/$ScenarioName/"

    [pscustomobject]@{
        SourcePrefix = $sourcePrefix
        TestPrefix = $testPrefix
        PresentationPrefix = $presentationPrefix
        SourceFolderMeta = $sourcePrefix.TrimEnd('/') + '.meta'
        TestFolderMeta = $testPrefix.TrimEnd('/') + '.meta'
        PresentationFolderMeta = $presentationPrefix.TrimEnd('/') + '.meta'
        PresentationInfrastructureMetas = @(
            'Assets/_Project/Presentation.meta',
            'Assets/_Project/Presentation/Institutional.meta',
            'Assets/_Project/Presentation/Institutional/Scenarios.meta')
    }
}

function Get-InstitutionalPresentationAssetExtensions {
    @(
        '.aif', '.aiff', '.bmp', '.csv', '.exr', '.flac', '.gif', '.hdr',
        '.jpeg', '.jpg', '.json', '.md', '.mov', '.mp3', '.mp4', '.ogg',
        '.otf', '.pdf', '.png', '.svg', '.tga', '.tif', '.tiff', '.tsv',
        '.ttf', '.txt', '.uss', '.uxml', '.wav', '.webm', '.webp', '.yml',
        '.yaml')
}

function Test-InstitutionalScenarioCodeAssetPath([string]$Path) {
    return $Path.EndsWith('.cs', [System.StringComparison]::OrdinalIgnoreCase) -or
        $Path.EndsWith('.cs.meta', [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-InstitutionalPresentationAssetPath([string]$Path) {
    $assetPath = if ($Path.EndsWith(
            '.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
        $Path.Substring(0, $Path.Length - '.meta'.Length)
    } else {
        $Path
    }
    $extension = [System.IO.Path]::GetExtension($assetPath)
    return (Get-InstitutionalPresentationAssetExtensions) -contains
        $extension.ToLowerInvariant()
}

function Get-InstitutionalTransitionTypeNames {
    @(
        'ExclusiveEntitlementService',
        'InstitutionalActionCausedDescendantCaseService',
        'InstitutionalActionProjector',
        'InstitutionalAdjudicationService',
        'InstitutionalAppealPrecedentService',
        'InstitutionalConnectedOutcomeProjector',
        'InstitutionalEvidencePipeline',
        'InstitutionalRelianceService',
        'InstitutionalScenarioActionPhase',
        'InstitutionalScenarioAdjudicationPhase',
        'InstitutionalScenarioEntitlementPhase',
        'InstitutionalScenarioEvidenceProjector',
        'InstitutionalScenarioOfficialStatusEffectExecutor',
        'InstitutionalScenarioStateInitializer',
        'InstitutionalStatusMutationService',
        'InstitutionalTimeline')
}

function Get-InstitutionalOutcomeTypeNames {
    @(
        'Appeal',
        'ConnectedOutcomePair',
        'DescendantCase',
        'EvidenceArtifact',
        'ExclusiveEntitlementObservation',
        'Holding',
        'InstitutionalConsequenceReport',
        'InstitutionalTimelineEntry',
        'MaterialConsequence',
        'ObservedAgentAction',
        'OfficialFinding',
        'OfficialStatusMutation',
        'RelianceObservation',
        'Ruling',
        'WorkAllocationObservation')
}

function Get-InstitutionalReportCollectionNames {
    @(
        'Appeals',
        'ConnectedOutcomes',
        'DescendantCases',
        'EvidenceArtifacts',
        'ExclusiveEntitlements',
        'Holdings',
        'MaterialConsequences',
        'ObservedAgentActions',
        'OfficialFindings',
        'OfficialStatusMutations',
        'RelianceObservations',
        'Rulings',
        'Timeline',
        'WorkAllocations')
}
