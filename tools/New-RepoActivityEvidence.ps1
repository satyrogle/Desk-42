param(
    [string]$OutputDir = "evidence/repo-activity"
)

$ErrorActionPreference = "Stop"

function Get-Area {
    param([string]$Path)

    if ($Path -like "Assets/_Project/Scripts/*") {
        $parts = $Path -split "/"
        if ($parts.Length -ge 4) { return "Scripts/$($parts[3])" }
        return "Scripts"
    }

    if ($Path -like "Assets/_Project/Tests/*") { return "Tests" }
    if ($Path -like "Assets/_Project/Scenes/*") { return "Scenes" }
    if ($Path -like "Assets/_Project/Prefabs/*") { return "Prefabs" }
    if ($Path -like "Packages/*") { return "Packages" }
    if ($Path -like "ProjectSettings/*") { return "ProjectSettings" }
    if ($Path -like ".claude/*") { return ".claude" }

    return (($Path -split "/")[0])
}

function ConvertTo-Slug {
    param([string]$Value)

    $slug = $Value.ToLowerInvariant() -replace "[^a-z0-9]+", "-"
    $slug = $slug.Trim("-")
    if ([string]::IsNullOrWhiteSpace($slug)) { return "development-log" }
    return $slug
}

function Get-ExistingFlag {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Default = "false"
    )

    if (-not (Test-Path -LiteralPath $Path)) { return $Default }

    $match = Select-String -LiteralPath $Path -Pattern "^$([regex]::Escape($Name)):\s*(.+)$" -CaseSensitive | Select-Object -First 1
    if (-not $match) { return $Default }
    return $match.Matches[0].Groups[1].Value.Trim()
}

$repoRoot = (& git rev-parse --show-toplevel).Trim()
if (-not $repoRoot) {
    throw "This script must be run from inside a git repository."
}

Push-Location $repoRoot
try {
    $resolvedOutputDir = Join-Path $repoRoot $OutputDir
    $draftDir = Join-Path $resolvedOutputDir "medium-drafts"
    New-Item -ItemType Directory -Force -Path $resolvedOutputDir, $draftDir | Out-Null

    $separator = [char]31
    $commitLines = & git log --all --date=iso-strict --pretty=format:"%H$separator%h$separator%ad$separator%an$separator%ae$separator%s"
    $commits = foreach ($line in $commitLines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split [regex]::Escape([string]$separator), 6
        if ($parts.Length -lt 6) { continue }

        [pscustomobject]@{
            Hash = $parts[0]
            ShortHash = $parts[1]
            DateTime = [datetimeoffset]::Parse($parts[2])
            Date = ([datetimeoffset]::Parse($parts[2])).ToString("yyyy-MM-dd")
            Author = $parts[3]
            Email = $parts[4]
            Subject = $parts[5]
        }
    }

    $fileChanges = New-Object System.Collections.Generic.List[object]
    foreach ($commit in $commits) {
        $numstat = & git show --numstat --format="" $commit.Hash
        foreach ($row in $numstat) {
            if ($row -match "^(\d+|-)\s+(\d+|-)\s+(.+)$") {
                $added = if ($Matches[1] -eq "-") { 0 } else { [int]$Matches[1] }
                $deleted = if ($Matches[2] -eq "-") { 0 } else { [int]$Matches[2] }
                $path = $Matches[3]

                $fileChanges.Add([pscustomobject]@{
                    Commit = $commit.ShortHash
                    CommitHash = $commit.Hash
                    Date = $commit.Date
                    Author = $commit.Author
                    Subject = $commit.Subject
                    Area = Get-Area $path
                    File = $path
                    Added = $added
                    Deleted = $deleted
                }) | Out-Null
            }
        }
    }

    $commits |
        Select-Object Date, ShortHash, Hash, Author, Email, Subject |
        Export-Csv -NoTypeInformation -Encoding UTF8 -Path (Join-Path $resolvedOutputDir "commits.csv")

    $fileChanges |
        Export-Csv -NoTypeInformation -Encoding UTF8 -Path (Join-Path $resolvedOutputDir "file-changes.csv")

    $totalCommits = @($commits).Count
    $firstDate = if ($totalCommits -gt 0) { ($commits | Sort-Object DateTime | Select-Object -First 1).Date } else { "n/a" }
    $lastDate = if ($totalCommits -gt 0) { ($commits | Sort-Object DateTime | Select-Object -Last 1).Date } else { "n/a" }
    $activeDays = @($commits | Group-Object Date).Count
    $totalAdded = ($fileChanges | Measure-Object Added -Sum).Sum
    $totalDeleted = ($fileChanges | Measure-Object Deleted -Sum).Sum
    $changedRows = $fileChanges.Count
    $uniqueFiles = @($fileChanges | Select-Object -ExpandProperty File -Unique).Count

    $byDate = $commits |
        Group-Object Date |
        Sort-Object Name |
        ForEach-Object {
            $dayChanges = @($fileChanges | Where-Object Date -eq $_.Name)
            $add = ($dayChanges | Measure-Object Added -Sum).Sum
            $del = ($dayChanges | Measure-Object Deleted -Sum).Sum
            [pscustomobject]@{
                Date = $_.Name
                Commits = $_.Count
                FilesChangedRows = $dayChanges.Count
                Added = if ($add) { $add } else { 0 }
                Deleted = if ($del) { $del } else { 0 }
            }
        }

    $byArea = $fileChanges |
        Group-Object Area |
        ForEach-Object {
            $add = ($_.Group | Measure-Object Added -Sum).Sum
            $del = ($_.Group | Measure-Object Deleted -Sum).Sum
            [pscustomobject]@{
                Area = $_.Name
                ChangeRows = $_.Count
                Added = if ($add) { $add } else { 0 }
                Deleted = if ($del) { $del } else { 0 }
            }
        } |
        Sort-Object ChangeRows -Descending

    $summary = New-Object System.Collections.Generic.List[string]
    $summary.Add("# Repo Activity Summary") | Out-Null
    $summary.Add("") | Out-Null
    $summary.Add("Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm zzz")") | Out-Null
    $summary.Add("Repository: $repoRoot") | Out-Null
    $summary.Add("") | Out-Null
    $summary.Add("## Headline Counts") | Out-Null
    $summary.Add("") | Out-Null
    $summary.Add("- Commit range: $firstDate to $lastDate") | Out-Null
    $summary.Add("- Total commits: $totalCommits") | Out-Null
    $summary.Add("- Active commit dates: $activeDays") | Out-Null
    $summary.Add("- File-change rows: $changedRows") | Out-Null
    $summary.Add("- Unique changed files: $uniqueFiles") | Out-Null
    $summary.Add("- Lines added: $totalAdded") | Out-Null
    $summary.Add("- Lines deleted: $totalDeleted") | Out-Null
    $summary.Add("") | Out-Null
    $summary.Add("## Activity By Date") | Out-Null
    $summary.Add("") | Out-Null
    $summary.Add("| Date | Commits | File-change rows | Added | Deleted |") | Out-Null
    $summary.Add("|---|---:|---:|---:|---:|") | Out-Null
    foreach ($row in $byDate) {
        $summary.Add("| $($row.Date) | $($row.Commits) | $($row.FilesChangedRows) | $($row.Added) | $($row.Deleted) |") | Out-Null
    }
    $summary.Add("") | Out-Null
    $summary.Add("## Top Areas") | Out-Null
    $summary.Add("") | Out-Null
    $summary.Add("| Area | Change rows | Added | Deleted |") | Out-Null
    $summary.Add("|---|---:|---:|---:|") | Out-Null
    foreach ($row in ($byArea | Select-Object -First 25)) {
        $summary.Add("| $($row.Area) | $($row.ChangeRows) | $($row.Added) | $($row.Deleted) |") | Out-Null
    }

    Set-Content -Encoding UTF8 -Path (Join-Path $resolvedOutputDir "repo-activity-summary.md") -Value $summary

    $index = @'
# Evidence Index

This folder is generated from real git history. Use it as source material for truthful portfolio notes, blog drafts, or immigration-supporting documentation. Do not alter dates or claims to imply work happened at a different time.

## Files

- `commits.csv`: one row per commit with date, hash, author, and subject.
- `file-changes.csv`: one row per changed file per commit, including additions/deletions.
- `repo-activity-summary.md`: aggregate counts by date and area.
- `medium-drafts/`: review-gated article drafts built from real commit periods.
- `published-posts.csv`: created by the publisher after a successful Medium API post.

## Review-Gated Publishing

Generated drafts start with:

```yaml
approved: false
publish: false
```

Only set both values to `true` after reviewing the article. The publisher script refuses to post drafts that are not explicitly approved. It also requires `MEDIUM_INTEGRATION_TOKEN` in the environment and logs successful posts to `published-posts.csv`.

## Suggested Evidence Packet

1. Export or screenshot the repository commit history from the remote host, if available.
2. Keep commit hashes visible so the activity can be verified.
3. Publish articles with the current publication date, and describe older dates as "work covered" or "development period."
4. If importing an article that was genuinely published elsewhere first, keep the canonical link to the original.
'@

    Set-Content -Encoding UTF8 -Path (Join-Path $resolvedOutputDir "evidence-index.md") -Value $index

    $months = $commits |
        Group-Object { $_.DateTime.ToString("yyyy-MM") } |
        Sort-Object Name

    foreach ($month in $months) {
        $monthCommits = @($month.Group | Sort-Object DateTime)
        $monthStart = ($monthCommits | Select-Object -First 1).Date
        $monthEnd = ($monthCommits | Select-Object -Last 1).Date
        $monthChanges = [array]($fileChanges | Where-Object { $_.Date -ge $monthStart -and $_.Date -le $monthEnd })
        $monthAdded = ($monthChanges | Measure-Object Added -Sum).Sum
        $monthDeleted = ($monthChanges | Measure-Object Deleted -Sum).Sum
        $monthAreas = $monthChanges |
            Group-Object Area |
            Sort-Object Count -Descending |
            Select-Object -First 8

        $title = "Desk 42 development log: $($month.Name)"
        $slug = ConvertTo-Slug $title
        $draftPath = Join-Path $draftDir "$($month.Name)-$slug.md"

        $existingApproved = Get-ExistingFlag -Path $draftPath -Name "approved" -Default "false"
        $existingPublish = Get-ExistingFlag -Path $draftPath -Name "publish" -Default "false"
        $existingPublishedUrl = Get-ExistingFlag -Path $draftPath -Name "published_url" -Default ""
        $topAreaNames = @($monthAreas | Select-Object -ExpandProperty Name -First 5)
        $topAreaText = if ($topAreaNames.Count -gt 0) { $topAreaNames -join ", " } else { "the core project" }

        $draft = New-Object System.Collections.Generic.List[string]
        $draft.Add("---") | Out-Null
        $draft.Add("title: $title") | Out-Null
        $draft.Add("approved: $existingApproved") | Out-Null
        $draft.Add("publish: $existingPublish") | Out-Null
        $draft.Add("publish_status: public") | Out-Null
        $draft.Add("published_url: $existingPublishedUrl") | Out-Null
        $draft.Add("work_covered_start: $monthStart") | Out-Null
        $draft.Add("work_covered_end: $monthEnd") | Out-Null
        $draft.Add("tags: [Game Development, Unity, Indie Games]") | Out-Null
        $draft.Add("---") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("# $title") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("Publication note: publish this with the actual Medium publication date. The dates below describe the work covered by the git history.") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("## Work Covered") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("- Period: $monthStart to $monthEnd") | Out-Null
        $draft.Add("- Source: local git history for Desk 42") | Out-Null
        $draft.Add("- Recorded commits: $(@($monthCommits).Count)") | Out-Null
        $draft.Add("- Unique changed files: $(@($monthChanges | Select-Object -ExpandProperty File -Unique).Count)") | Out-Null
        $draft.Add("- Lines added/deleted: $monthAdded/$monthDeleted") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("## Summary") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("This development period moved Desk 42 forward through concrete, verifiable repository activity. The work concentrated around $topAreaText, with changes recorded in commit history rather than reconstructed from memory. That matters because the project is not just a design idea; it has an auditable trail of implementation, fixes, systems work, and production scaffolding.") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("For this period, git records $(@($monthCommits).Count) commits touching $(@($monthChanges | Select-Object -ExpandProperty File -Unique).Count) unique files, with $monthAdded added lines and $monthDeleted deleted lines. Those numbers are not a perfect measure of creative value, but they are useful evidence of sustained engineering and design activity.") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("## Main Work Areas") | Out-Null
        $draft.Add("") | Out-Null
        foreach ($area in $monthAreas) {
            $draft.Add("- $($area.Name): $($area.Count) file-change rows") | Out-Null
        }
        $draft.Add("") | Out-Null
        $draft.Add("## What Changed") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("The commit trail shows a mix of system construction, integration work, polish, and repair. The practical pattern is familiar in game development: features arrive, tests or compiler feedback reveal pressure points, and the project becomes sturdier through each pass.") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("The most useful way to read this month is not as a single feature drop, but as a sequence of technical decisions. Each commit left a timestamped record of what changed and why, from project structure and Unity configuration through gameplay systems, UI layers, editor tooling, and bug fixes.") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("## Evidence Trail") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("These are the recorded commits for the period. They are included so the article can be checked against the repository rather than treated as a loose retrospective.") | Out-Null
        $draft.Add("") | Out-Null
        foreach ($commit in $monthCommits) {
            $draft.Add(("- {0} ``{1}``: {2}" -f $commit.Date, $commit.ShortHash, $commit.Subject)) | Out-Null
        }
        $draft.Add("") | Out-Null
        $draft.Add("## Reflection") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("The strongest lesson from this slice of work is that a game project becomes real through repeated contact with constraints: Unity project structure, compile errors, scene wiring, user interface behavior, asset organization, and the small decisions that keep future work possible. The visible feature list is only one layer; the repository history also captures the less glamorous engineering that lets the project survive its own growth.") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("## Verification") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("The numbers in this post come from `git log` and `git show --numstat` for the Desk 42 repository. The work dates are development dates from git history, not claimed Medium publication dates.") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("<!-- Review gate: set approved: true and publish: true in the front matter only after you have reviewed this article. -->") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("<!-- Raw notes retained below for editing. -->") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("## Raw Development Notes") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("## Development Notes") | Out-Null
        $draft.Add("") | Out-Null
        foreach ($commit in $monthCommits) {
            $draft.Add(("- {0} ``{1}``: {2}" -f $commit.Date, $commit.ShortHash, $commit.Subject)) | Out-Null
        }
        $draft.Add("") | Out-Null
        $draft.Add("## Article Angle") | Out-Null
        $draft.Add("") | Out-Null
        $draft.Add("Turn the commit list above into a narrative about the design and engineering decisions made during this period. Keep the dates as work-period evidence, not as claimed publication dates.") | Out-Null

        Set-Content -Encoding UTF8 -Path $draftPath -Value $draft
    }

    Write-Host "Generated repo activity evidence in: $resolvedOutputDir"
    Write-Host "Commits: $totalCommits"
    Write-Host "Date range: $firstDate to $lastDate"
    Write-Host "Active dates: $activeDays"
    Write-Host "Lines added/deleted: $totalAdded/$totalDeleted"
}
finally {
    Pop-Location
}
