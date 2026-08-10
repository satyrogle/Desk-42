# Desk42 Office Slice M1 Baseline

## Freeze record

- Source branch: `codex/commercial-demo-integration-v0.6`
- Source commit: `3b34d477f1998bf34078643d919b434831e95dd9`
- Working branch: `codex/office-slice-v0.7-m1-greybox`
- Unity: `2022.3.62f3` (`96770f904ca7`)
- Rollback tag: `office-slice-m1-baseline-v0.5.1`
- Windows target: `StandaloneWindows64`

The working tree was clean at branch creation. The rollback tag points to the
required source commit exactly.

## Package state

Package inputs were not changed for M1. The baseline package inputs are the
contents of `Packages/manifest.json` and `Packages/packages-lock.json` at the
source commit. Relevant resolved packages are:

- Input System `1.14.2`
- Unity Test Framework `1.3.9`
- Cinemachine `2.10.3`
- Universal Render Pipeline package `14.0.12` installed; Built-in Render
  Pipeline remains assigned
- CoplayDev Unity MCP and IvanMurzak Unity MCP are present and unchanged

Batchmode validation used the existing CI-compatible environment overrides
`UNITY_MCP_KEEP_CONNECTED=false` and `UNITY_MCP_START_SERVER=false`. Without
those overrides, the IvanMurzak MCP package emits an authorization failure that
Unity Test Framework treats as an unhandled log message.

## Build Settings before M1

The only enabled scene was:

```text
0  Assets/_Project/Scenes/InstitutionalAutomation.unity
```

No startup scene or existing automation scene was changed during baseline
validation.

## Baseline validation

Commands used Unity 2022.3.62f3 in batchmode with `-nographics` and the MCP
environment overrides above. Result artifacts are retained under
`TestResults/M1-Baseline/`.

- Existing automation boot test: `1/1 passed` —
  `automation-boot-ci-overrides.xml`
- Existing active automation PlayMode fixture: `11/11 passed` within the
  combined active-product PlayMode run — `active-product-playmode.xml`
- Full EditMode suite: `418/418 passed, 0 failed, 0 skipped` —
  `full-editmode.xml`
- Combined active-product PlayMode run: `12/14 passed, 2 failed, 0 skipped`.
  The two failures are pre-existing `CausalLegibilitySlicePlayModeTests`
  failures because `InstitutionalProduct` is not in Build Settings. M1 does
  not alter that unrelated product scene configuration.
- Windows x64 baseline build: passed — `Builds/M1-Baseline/Desk42.exe`

The baseline automation scene therefore boots successfully when the documented
MCP connection overrides prevent the unrelated authorization log failure.

## Protected paths

The following paths are frozen for M1 and must remain unchanged:

```text
Assets/_Project/Scripts/Institutional/Domain/**
Assets/_Project/Scripts/Institutional/Authority/**
Assets/_Project/Scripts/Institutional/Player/**
Assets/_Project/Scripts/Institutional/Runtime/**
Assets/_Project/Scripts/Institutional/Authority/Scenarios/**
Assets/_Project/Scenes/InstitutionalAutomation.unity
DESK42_SYSTEM_CONSTITUTION.md
Docs/Product/PRODUCTION_VERTICAL_SLICE_V0.5.md
Docs/Product/ISSUE_ID_HARDENING_V0.5.1.md
```

M1 will not modify package inputs, MCP providers, save schemas, render-pipeline
settings, audio integrations, final art, ComfyUI, Blender or FMOD.

