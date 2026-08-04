# Desk42 — Stable Issue Identity Hardening v0.5.1

Status: validated narrow compatibility patch

Baseline: `d88eb056b873c8617356d1061ac88126e216b94e`

Branch: `codex/commercial-demo-integration-v0.6`

## Purpose

Case-family behaviour must not depend on English display copy. The public-safe
chain now carries the authoritative institutional issue identifier separately
from its humanised label:

```text
EndogenousInstitutionalCase.IssueId
→ PublicCaseRecord.IssueId
→ AutomationPublicClaim.IssueId
→ AutomationClaimProfile.IssueId
```

`Issue` remains presentation-only and may be rewritten or localised without
changing factory behaviour.

## Stable consumers

The product now uses `IssueId` for:

- issue-family docket coverage;
- evidence requirements and verification work;
- urgency and dependency relief;
- protected-access routing;
- retained verification and adverse-review patterns;
- dossier colour, glyph and compact family label;
- completed-family metrics and balance-facing counters.

Institutional holding and remedy selection already used the authority-owned
stable issue identifier and remains unchanged.

## Compatibility

- No case family, doctrine, procedure or engine layer was added.
- No scene, prefab, package or Project Settings asset changed.
- The product save schema is unchanged. Saved claims retain their source case ID
  and regain `IssueId` when their public projection is reconstructed.
- Humanised issue labels remain available for the docket and precedent ledger.

## Validation

```text
Affected EditMode fixtures: 28 / 28 passed
Standard EditMode:          417 / 417 passed
Long-run EditMode:            1 / 1 passed
Active-product PlayMode:     10 / 10 passed
Eight-shift PlayMode:         1 / 1 passed
Windows x64 build:            passed
Built-player smoke/capture:   passed (1600 × 900)
```

Static inspection found no remaining case-family decisions using
`Issue.Contains(...)` or `Issue.IndexOf(...)`.
