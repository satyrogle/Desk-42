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
        'InstitutionalEvidenceActivatedCaseService',
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
        'InstitutionalCaseOpening',
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
        'CaseOpenings',
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

function ConvertTo-InstitutionalRegexAlternation([string[]]$Values) {
    return ($Values | ForEach-Object { [regex]::Escape($_) }) -join '|'
}

function Get-InstitutionalScenarioTestViolations([string]$SourceText) {
    if ($null -eq $SourceText) { throw 'Scenario test source text cannot be null.' }

    $transitionTypes = ConvertTo-InstitutionalRegexAlternation `
        (Get-InstitutionalTransitionTypeNames)
    $outcomeTypes = ConvertTo-InstitutionalRegexAlternation `
        (Get-InstitutionalOutcomeTypeNames)
    $reportCollections = ConvertTo-InstitutionalRegexAlternation `
        (Get-InstitutionalReportCollectionNames)
    $mutationMethods =
        'Add|AddRange|Clear|Insert|Remove|RemoveAll|RemoveAt|Reverse|Sort'
    $memberPath =
        '(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*(?:\s*\([^;{}]*?\))?|' +
        '\s*\[[^\]]+\])*'
    $options = [System.Text.RegularExpressions.RegexOptions]::Singleline
    $violations = [System.Collections.Generic.List[object]]::new()
    $patterns = [ordered]@{
        'direct transition-service use' = "\b(?:$transitionTypes)\b"
        'institutional outcome construction' = "\bnew\s+(?:$outcomeTypes)\b"
        'report collection replacement' =
            "\.(?:$reportCollections)\b\s*=(?!=)"
        'report outcome replacement' =
            "\.(?:$reportCollections)\b$memberPath" +
            "(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*|\s*\[[^\]]+\])" +
            "\s*(?:[+\-*/%]?=(?!=)|\+\+|--)"
        'report outcome mutation' =
            "\.(?:$reportCollections)\b$memberPath" +
            "\s*\.\s*(?:$mutationMethods)\s*\("
        'reflection escape hatch' =
            '\b(?:System\.Reflection|BindingFlags|Activator\.CreateInstance|' +
            'Assembly\.Load|GetField|GetMethod|MethodInfo|FieldInfo|dynamic)\b'
    }

    foreach ($entry in $patterns.GetEnumerator()) {
        foreach ($match in [regex]::Matches(
                $SourceText,
                $entry.Value,
                $options)) {
            $violations.Add([pscustomobject]@{
                Reason = $entry.Key
                Index = $match.Index
                Text = $match.Value
            })
        }
    }

    $aliases = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $collectionAliases = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)

    # Outcome-typed variables are potential aliases regardless of whether they
    # are locals, foreach variables, method parameters or explicitly typed
    # lambda parameters. Scenario tests may read these DTOs, but may not mutate
    # them through a helper signature that hides their report provenance.
    $typedAliasPattern =
        "\b(?:$outcomeTypes)\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\b"
    foreach ($match in [regex]::Matches(
            $SourceText,
            $typedAliasPattern,
            $options)) {
        $null = $aliases.Add($match.Groups['alias'].Value)
    }

    $outcomeCollectionTypes =
        '(?:(?:System\s*\.\s*Collections\s*\.\s*Generic\s*\.\s*)?' +
        '(?:List|IList|IReadOnlyList|ICollection|IReadOnlyCollection|' +
        'IEnumerable)\s*<\s*(?:' + $outcomeTypes + ')\s*>|' +
        '(?:' + $outcomeTypes + ')\s*\[\s*\])'
    $typedCollectionAliasPattern =
        "\b(?:$outcomeCollectionTypes)\s+" +
        '(?<alias>[A-Za-z_][A-Za-z0-9_]*)\b'
    foreach ($match in [regex]::Matches(
            $SourceText,
            $typedCollectionAliasPattern,
            $options)) {
        $null = $collectionAliases.Add($match.Groups['alias'].Value)
    }

    $varAliasPattern =
        "\bvar\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*[^;]*?" +
        "(?:\.Report\b|\.(?:$reportCollections)\b)[^;]*;"
    foreach ($match in [regex]::Matches(
            $SourceText,
            $varAliasPattern,
            $options)) {
        $null = $aliases.Add($match.Groups['alias'].Value)
    }

    # A direct report-list assignment retains the mutable list identity. Track
    # var aliases separately so rows.Clear()/rows[index] mutations cannot evade
    # the member-level outcome rules.
    $varCollectionAliasPattern =
        "\bvar\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*[^;{}]*?" +
        "\.(?:$reportCollections)\b\s*;"
    foreach ($match in [regex]::Matches(
            $SourceText,
            $varCollectionAliasPattern,
            $options)) {
        $null = $collectionAliases.Add($match.Groups['alias'].Value)
    }

    # Infer outcome aliases introduced by foreach and implicit lambdas over a
    # report collection. Explicitly typed variants are already covered by the
    # outcome-type declaration rule above.
    $varForeachReportPattern =
        "\bforeach\s*\(\s*var\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)" +
        "\s+in\s+[^(){};]*?\.(?:$reportCollections)\b\s*\)"
    foreach ($match in [regex]::Matches(
            $SourceText,
            $varForeachReportPattern,
            $options)) {
        $null = $aliases.Add($match.Groups['alias'].Value)
    }
    $implicitReportLambdaPattern =
        "\.(?:$reportCollections)\b\s*\.\s*[A-Za-z_][A-Za-z0-9_]*" +
        "\s*\(\s*\(?\s*(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*\)?\s*=>"
    foreach ($match in [regex]::Matches(
            $SourceText,
            $implicitReportLambdaPattern,
            $options)) {
        $null = $aliases.Add($match.Groups['alias'].Value)
    }

    foreach ($collectionAlias in $collectionAliases) {
        $escapedCollectionAlias = [regex]::Escape($collectionAlias)
        $varForeachAliasPattern =
            "\bforeach\s*\(\s*var\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)" +
            "\s+in\s+$escapedCollectionAlias\s*\)"
        foreach ($match in [regex]::Matches(
                $SourceText,
                $varForeachAliasPattern,
                $options)) {
            $null = $aliases.Add($match.Groups['alias'].Value)
        }
        $implicitAliasLambdaPattern =
            "\b$escapedCollectionAlias\s*\.\s*[A-Za-z_][A-Za-z0-9_]*" +
            "\s*\(\s*\(?\s*(?<alias>[A-Za-z_][A-Za-z0-9_]*)" +
            "\s*\)?\s*=>"
        foreach ($match in [regex]::Matches(
                $SourceText,
                $implicitAliasLambdaPattern,
                $options)) {
            $null = $aliases.Add($match.Groups['alias'].Value)
        }
    }

    foreach ($alias in $aliases) {
        $escapedAlias = [regex]::Escape($alias)
        $aliasMemberPath =
            '(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*|\s*\[[^\]]+\])+'
        $aliasPatterns = [ordered]@{
            'aliased outcome replacement' =
                "\b$escapedAlias$aliasMemberPath" +
                "\s*(?:[+\-*/%]?=(?!=)|\+\+|--)"
            'aliased outcome mutation' =
                "\b$escapedAlias$aliasMemberPath" +
                "\s*\.\s*(?:$mutationMethods)\s*\("
        }
        foreach ($entry in $aliasPatterns.GetEnumerator()) {
            foreach ($match in [regex]::Matches(
                    $SourceText,
                    $entry.Value,
                    $options)) {
                $violations.Add([pscustomobject]@{
                    Reason = $entry.Key
                    Index = $match.Index
                    Text = $match.Value
                })
            }
        }
    }

    foreach ($alias in $collectionAliases) {
        $escapedAlias = [regex]::Escape($alias)
        $collectionAliasPatterns = [ordered]@{
            'aliased report collection replacement' =
                "\b$escapedAlias\b$memberPath" +
                "(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*|\s*\[[^\]]+\])" +
                "\s*(?:[+\-*/%]?=(?!=)|\+\+|--)"
            'aliased report collection mutation' =
                "\b$escapedAlias\b$memberPath" +
                "\s*\.\s*(?:$mutationMethods)\s*\("
        }
        foreach ($entry in $collectionAliasPatterns.GetEnumerator()) {
            foreach ($match in [regex]::Matches(
                    $SourceText,
                    $entry.Value,
                    $options)) {
                $violations.Add([pscustomobject]@{
                    Reason = $entry.Key
                    Index = $match.Index
                    Text = $match.Value
                })
            }
        }
    }

    return @($violations)
}
