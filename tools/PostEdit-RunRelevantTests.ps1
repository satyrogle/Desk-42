# ============================================================
# DESK 42 — PostToolUse hook dispatcher
#
# Claude Code's hook "matcher" only filters by tool name
# (Edit|Write), not by file path. Unity batchmode has real
# per-run startup/compile cost (tens of seconds), so this script
# does the path-narrowing: it only launches the test run when the
# edited file is part of the BSM / SynergyResolver / DeskEntropy /
# ExpenseUnmet surface area. Anything else exits immediately
# without touching Unity.
# ============================================================

$Input_ = [Console]::In.ReadToEnd()

try {
    $Payload = $Input_ | ConvertFrom-Json
} catch {
    exit 0
}

$FilePath = $Payload.tool_input.file_path
if (-not $FilePath) { exit 0 }

$RelevantPatterns = @(
    "[\\/]BSM[\\/]",
    "[\\/]OfficeSupplies[\\/]",
    "DeskEntropyRenderer\.cs$",
    "PersonalExpenseGenerator\.cs$",
    "RumorMillEvent(s|Bus)\.cs$",
    "BSMTests\.cs$",
    "SynergyResolverTests\.cs$",
    "DeskEntropyTierTests\.cs$",
    "ExpenseUnmetEventTests\.cs$"
)

$IsRelevant = $false
foreach ($pattern in $RelevantPatterns) {
    if ($FilePath -match $pattern) { $IsRelevant = $true; break }
}

if (-not $IsRelevant) { exit 0 }

& (Join-Path $PSScriptRoot "Run-StateMachineSynergyTests.ps1")
exit $LASTEXITCODE
