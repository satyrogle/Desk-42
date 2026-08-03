# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `C:/Users/jacob/Desk 42`
- Product branch: `codex/production-vertical-slice-v0.5`
- Product direction: a persistent institutional automation simulator coupling a physical claims factory to a deterministic autonomous society.
- Frozen legacy game: tag `milestone/bucket4-candidate`, commit `e584ce6c986a8d4ff30fa391c2221c3f8da03a0e`.
- Frozen engine candidate: tag `institutional-engine-candidate-v0.4.3`, commit `7407d290ea9e4fbab8b8525d47176ac02112374c`.
- Last analyzed: 2026-08-03
- Last analyzed checkpoint: production vertical slice v0.5

## Confirmed Environment

- Unity version: 2022.3.62f3 (`96770f904ca7`).
- Render pipeline: Built-in pipeline is assigned; URP 14.0.12 is installed but no custom pipeline asset is assigned in Graphics or Quality settings.
- Input system: both legacy and new Input System are enabled (`activeInputHandler: 2`); Input System 1.14.2 is installed.
- Target platforms: Windows Standalone x64 is the documented release target; hosted institutional tests run on Linux Unity.

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Institutional simulation | Domain, Authority, Player, scenario and Unity persistence assemblies enforce simulation and truth boundaries | Confirmed | `Assets/_Project/Scripts/Institutional/` |
| Serialization | Newtonsoft.Json 3.2.1 supports `InstitutionalSocietyStore` | Confirmed | `Packages/manifest.json`; runtime asmdef |
| Testing | Unity Test Framework 1.3.9 with EditMode institutional suites and a focused product PlayMode suite | Confirmed | package manifest and test asmdefs |
| Unity MCP | CoplayDev and IvanMurzak packages are both configured, but no Unity MCP tools are exposed in this Codex task | Confirmed | package files, `.mcp.json`, active tool inventory |
| Presentation | A playable isometric automation floor uses procedural 3D machinery, physical dossier flow and IMGUI operations/buildcraft HUD over the immutable public institutional boundary | Confirmed | `Assets/_Project/Scripts/Product/Automation/` |
| Audio | Deterministic layered Unity audio exposes backlog, heat, appeal pressure and shift progression; FMOD is not installed | Confirmed | `AutomationAudioSystem.cs`; package manifest |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/_Project/Scripts/Institutional/Domain` | Engine-independent society state, decisions and scenario contracts | Confirmed | no-engine-reference asmdef |
| `Assets/_Project/Scripts/Institutional/Authority` | Authoritative material state, evidence, adjudication, endogenous docket generation, executable remedies, scope execution and active-chain persistence | Confirmed | authority asmdef and checkpoint report |
| `Assets/_Project/Scripts/Institutional/Player` | Immutable public-safe projection, ruling facade, deterministic slice seed, save/load and replay boundary | Confirmed | no-engine-reference player asmdef and truth-boundary tests |
| `Assets/_Project/Scripts/Institutional/Authority/Scenarios` | Declarative authored scenario definitions | Confirmed | scenario asmdef references Domain only |
| `Assets/_Project/Scripts/Institutional/Runtime` | Unity-facing persistence bridge for public society state | Confirmed | runtime asmdef and `InstitutionalSocietyStore.cs` |
| `Assets/_Project/Tests/EditMode` | Institutional engine and scenario tests plus temporary legacy tests pending extraction | Confirmed | test asmdef and source inventory |
| `Assets/_Project/Scripts/Product/Automation` | Active factory, doctrines, procedures, persistence, buildcraft, visuals, audio and input/HUD | Confirmed | product source inventory |
| `Assets/_Project/Tests/PlayMode` | Active automation scene, operations, doctrines, procedures, persistence, eight-shift run and Welfare validation | Confirmed | PlayMode asmdef and product tests |
| `Docs/Institutional` | Engine boundary and scenario specifications | Confirmed | repository contents |
| `evidence/InstitutionalEngine` | Frozen validation evidence; not product runtime content | Confirmed | repository contents |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `Desk42.Institutional.Domain` | Deterministic society domain and declarative contracts | none | no Unity engine reference |
| `Desk42.Institutional.Authority` | Authoritative material and institutional transitions plus active-chain snapshot store | Domain, Newtonsoft.Json | not auto-referenced; no Unity engine reference |
| `Desk42.Institutional.Player` | Immutable public projection and validated playable-session facade | Domain, Authority | no Unity engine reference; owns the product-safe disposition vocabulary |
| `Desk42.Institutional.Scenarios` | Concrete scenario data | Domain | cannot invoke Authority |
| `Desk42.Institutional.Runtime` | Society save/load adapter | Domain, Newtonsoft.Json | Unity-facing |
| `Desk42.Product` | Unity presentation and input shell | Player | has no Domain, Authority or scenario reference |
| `Desk42.Tests.EditMode` | Institutional validation | institutional assemblies and compatibility `Desk42.Core` | Editor only |
| `Desk42.Core` | Legacy assembly compatibility shell | Input System, TMP, Newtonsoft.Json | extraction retains only `SeedEngine` because an institutional isolation test reads it |

## Scenes And Startup Flow

- Build scene: `Assets/_Project/Scenes/InstitutionalAutomation.unity`.
- Startup flow: `AutomationBootstrap` creates the isometric Branch 42 floor and one
  continuing `InstitutionalAutomationSession`; doctrine selection begins the run.
- Archived scenes `Boot`, `MainMenu`, `Shift` and `InternalAudit` do not exist on
  the product branch.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Deterministic pulse simulation | Agent perceptions and decisions are frozen before stable action application | Confirmed | `SocietySimulation.cs`; engine boundary |
| Domain/authority separation | Lived truth is assembly-private and public projection is non-authoritative | Confirmed | asmdefs, `AssemblyInfo.cs`, engine tests |
| Declarative scenarios | Scenario assemblies provide definitions and policies but cannot execute transitions | Confirmed | scenario asmdef and CI boundary gate |
| Persistence | Public society state persists separately from the legacy run save | Confirmed | `InstitutionalSocietyStore.cs` |
| Active consequence persistence | Complete endogenous causal state persists at committed boundaries with exact-once transition IDs, checksum and backup recovery; playable current/origin histories share one atomic session envelope | Confirmed | `EndogenousRunSnapshot.cs`; `EndogenousRunSessionSnapshot.cs`; persistence tests |
| Endogenous institutional loop | Five issue families enter one public factory feed; executable rulings, remedies, appeals and scope affect the continuing society | Confirmed for the bounded eight-agent product run | v0.5 product tests and milestone document |
| Institutional buildcraft | Four binding doctrines and a two-slot, thirteen-procedure draft pool alter routes, workload, fault pressure, scope, relief and Legal return | Confirmed | automation runtime and PlayMode coverage |
| Player-safe projection | Product UI consumes explicit immutable records and cannot reference Authority or scenario assemblies directly | Confirmed | player/product asmdefs and `InstitutionalPlayerViewTests` |

## Coding Conventions

- Namespace style: `Desk42.Institutional` with functional subnamespaces for runtime and scenarios.
- Serialized fields: private backing fields are preferred in Unity-facing code; domain DTOs intentionally use explicit mutable fields for deterministic fixtures.
- Async: no first-party async runtime pattern is established in the institutional substrate.
- Comments/docs: public boundaries and non-obvious causal-order constraints are documented; implementation comments explain invariants rather than presentation.

## Testing And Validation

- EditMode tests: institutional domain, authority, scenario and persistence coverage in `Desk42.Tests.EditMode`.
- PlayMode tests: active product coverage includes scene boot, machinery, routing,
  doctrines, procedures, persistence, appeals, five-family throughput, Welfare relief
  and the eight-shift Branch Review.
- Hosted baseline at commit `3302302`: targeted institutional suite 334/334 passed; full EditMode 454 passed, 0 failed, 1 approved pre-existing skip.
- Post-extraction local baseline: targeted institutional suite 334/334 passed;
  full remaining EditMode suite 338/338 passed with no skips.
- Endogenous society v0.1 local validation: institutional suite 381/381 passed;
  complete EditMode suite 385/385 passed with no skips.
- Causal legibility v0.1.1 local validation: complete EditMode 400/400 and focused
  PlayMode 3/3, both with no failures or skips. Windows x64 build and standalone
  executable-remedy/save-load smoke pass. Validation uses CI connection overrides
  to prevent an unavailable Unity MCP cloud authorization from polluting test logs.
- Production vertical slice v0.5 local validation: standard EditMode 417/417,
  long-run EditMode 1/1, active-product PlayMode 10/10 and eight-shift PlayMode
  1/1. Windows x64 build and visible 1600 x 900 built-player capture passed.
- CI/build validation: `.github/workflows/institutional-proof.yml` preserves the frozen evidence gate; `.github/workflows/ci.yml` contains the general Unity pipeline.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity batchmode import/compile | available | local Unity 2022.3.62f3 executable |
| Unity Test Runner via batchmode | available | project package and prior local/hosted execution |
| Unity MCP configuration | available but duplicated | CoplayDev and IvanMurzak package entries |
| Unity MCP tools in this task | unavailable | current Codex tool inventory exposes no Unity capabilities |
| Console/read hierarchy through MCP | unavailable | no active Unity MCP tool surface |

## Important Constraints

- Do not modify frozen institutional engine files merely to simplify product extraction.
- Describe active-chain persistence narrowly: committed endogenous phase boundaries are supported; arbitrary instruction-level suspension is not.
- The old game is recovered from its tag/worktree, not copied into the active Unity `Assets` tree.
- Product presentation and gameplay must consume public institutional boundaries rather than scenario-specific authority internals.
- Two Unity MCP providers are installed; do not add a third, and rationalize the duplicate setup only as a separate authorized task.

## Unknowns And Confidence

- The current procedural 3D environment is a production-facing visual blockout, not a
  substitute for commissioned modular art and animation.
- FMOD is not installed; the current layered Unity audio is an integration seam and
  gameplay-audio proof, not final authored sound production.
- Automated interaction is validated; human onboarding, agency, long-run pacing and
  commercial loop quality remain unvalidated.

## Source Files Inspected

- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/QualitySettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `.mcp.json`
- institutional assembly definitions and representative domain/authority/runtime sources
- `DESK42_SYSTEM_CONSTITUTION.md`
- `Docs/Institutional/ENGINE_CANDIDATE_BOUNDARY.md`
- `evidence/InstitutionalEngine/v0.4.3/README.md`

<!-- unity-onboarding:generated:end -->
