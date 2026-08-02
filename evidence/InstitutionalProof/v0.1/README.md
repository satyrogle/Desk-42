# Desk 42 Institutional Vertical Proof v0.1

## Evidence identity

- Original immutable commit: `455cecc4a5236e5455c56eb710fb03e243975dc9`
- Annotated tag: `institutional-proof-v0.1`
- Original branch: `visa/gate3-desk42-validation-DO-NOT-MERGE`
- Clean evidence branch base: `f5c9a7bff995f3241ece9d46a996e045f33786f2`
- Unity Editor: `2022.3.62f3 (96770f904ca7)`
- `Packages/packages-lock.json` SHA-256: `33A5B75D8EB048F51A44470724D8DD6F50E0DCB11FB4E73005B488EE7258757D`

The tag preserves the original proof commit exactly. This clean branch imports
only the system constitution, institutional source assemblies, institutional
EditMode tests, test-assembly reference change, and this curated evidence
package. Generated builds, caches, intermediate logs, failed/intermediate XML,
and generated design outputs are excluded.

## Accurate current claim

> Desk 42 contains a deterministic eight-agent vertical proof showing that
> generic utility-selected actions can participate in traceable institutional
> consequence chains under three authored policy configurations.

This checkpoint is a fixed-schedule proof harness. It is not described as a
general procedural or generative society engine.

Agent society persistence is implemented. Active institutional
consequence-loop persistence is not.

## Curated local validation artifact

`final-editmode.xml` records the final successful local EditMode run committed
with the original checkpoint:

- total: 148
- passed: 147
- failed: 0
- inconclusive: 0
- skipped: 1
- started: 2026-08-02 17:11:13Z
- completed: 2026-08-02 17:11:14Z

The single skipped test is outside the institutional system:

`Desk42.Tests.EditMode.SynergyResolverTests.WIRING_GAP_DurationAndCostChains_HaveNoStructuredPerStepTrace`

This XML is a committed local artifact, not independent CI evidence.

## Reproduction path

`.github/workflows/institutional-proof.yml` checks out the triggering commit and
runs two Unity 2022.3.62f3 jobs:

1. the two institutional EditMode fixtures;
2. the complete EditMode suite.

Both jobs upload NUnit XML, logs, and reproduction metadata containing the
commit SHA, Unity version, and package-lock hash. A post-test contract rejects
failed or inconclusive tests, missing institutional fixtures, zero-test runs,
and skipped tests outside the explicit allow-list.

The workflow requires the repository's existing Unity licence secrets. Until a
workflow run completes for the clean branch, CI reproducibility remains
configured but not independently demonstrated.
