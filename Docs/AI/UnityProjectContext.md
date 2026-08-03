# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `C:/Users/jacob/Desk 42`
- Product branch: `codex/causal-legibility-slice-v0.1`
- Product direction: a systemic alien society simulation built around a deterministic institutional simulation substrate.
- Frozen legacy game: tag `milestone/bucket4-candidate`, commit `e584ce6c986a8d4ff30fa391c2221c3f8da03a0e`.
- Frozen engine candidate: tag `institutional-engine-candidate-v0.4.3`, commit `7407d290ea9e4fbab8b8525d47176ac02112374c`.
- Last analyzed: 2026-08-03
- Last analyzed commit: `3d85f81` (player-safe causal legibility layer)

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
| Presentation | A text-first IMGUI slice renders the immutable public institutional view; uGUI, TMP and URP remain available but unused by the slice | Confirmed | product assembly and build screenshot |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/_Project/Scripts/Institutional/Domain` | Engine-independent society state, decisions and scenario contracts | Confirmed | no-engine-reference asmdef |
| `Assets/_Project/Scripts/Institutional/Authority` | Authoritative material state, evidence, adjudication, endogenous docket generation, rulings, scope execution and active-chain persistence | Confirmed | authority asmdef and checkpoint report |
| `Assets/_Project/Scripts/Institutional/Player` | Immutable public-safe projection, ruling facade, deterministic slice seed, save/load and replay boundary | Confirmed | no-engine-reference player asmdef and truth-boundary tests |
| `Assets/_Project/Scripts/Institutional/Authority/Scenarios` | Declarative authored scenario definitions | Confirmed | scenario asmdef references Domain only |
| `Assets/_Project/Scripts/Institutional/Runtime` | Unity-facing persistence bridge for public society state | Confirmed | runtime asmdef and `InstitutionalSocietyStore.cs` |
| `Assets/_Project/Tests/EditMode` | Institutional engine and scenario tests plus temporary legacy tests pending extraction | Confirmed | test asmdef and source inventory |
| `Assets/_Project/Tests/PlayMode` | Product scene boot, five-surface navigation and ruling/save/load/replay validation | Confirmed | PlayMode asmdef and slice tests |
| `Docs/Institutional` | Engine boundary and scenario specifications | Confirmed | repository contents |
| `evidence/InstitutionalEngine` | Frozen validation evidence; not product runtime content | Confirmed | repository contents |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `Desk42.Institutional.Domain` | Deterministic society domain and declarative contracts | none | no Unity engine reference |
| `Desk42.Institutional.Authority` | Authoritative material and institutional transitions plus active-chain snapshot store | Domain, Newtonsoft.Json | not auto-referenced; no Unity engine reference |
| `Desk42.Institutional.Player` | Immutable public projection and validated playable-session facade | Domain, Authority | no Unity engine reference; does not reference scenario content |
| `Desk42.Institutional.Scenarios` | Concrete scenario data | Domain | cannot invoke Authority |
| `Desk42.Institutional.Runtime` | Society save/load adapter | Domain, Newtonsoft.Json | Unity-facing |
| `Desk42.Tests.EditMode` | Institutional validation | institutional assemblies and compatibility `Desk42.Core` | Editor only |
| `Desk42.Core` | Legacy assembly compatibility shell | Input System, TMP, Newtonsoft.Json | extraction retains only `SeedEngine` because an institutional isolation test reads it |

## Scenes And Startup Flow

- Build scene: `Assets/_Project/Scenes/InstitutionalProduct.unity`.
- Startup flow: the scene contains only `InstitutionalProductBootstrap`, which
  creates the causal-legibility session and renders five public-safe player surfaces.
- Archived scenes `Boot`, `MainMenu`, `Shift` and `InternalAudit` do not exist on
  the product branch.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Deterministic pulse simulation | Agent perceptions and decisions are frozen before stable action application | Confirmed | `SocietySimulation.cs`; engine boundary |
| Domain/authority separation | Lived truth is assembly-private and public projection is non-authoritative | Confirmed | asmdefs, `AssemblyInfo.cs`, engine tests |
| Declarative scenarios | Scenario assemblies provide definitions and policies but cannot execute transitions | Confirmed | scenario asmdef and CI boundary gate |
| Persistence | Public society state persists separately from the legacy run save | Confirmed | `InstitutionalSocietyStore.cs` |
| Active consequence persistence | Complete endogenous causal state persists at committed boundaries with exact-once replay IDs, checksum and backup recovery | Confirmed | `EndogenousRunSnapshot.cs`; persistence tests |
| Endogenous institutional loop | Autonomous actions can create observable docket cases; executable ruling scope can change later decisions and descendant cases | Confirmed for the bounded v0.1 proof | `ENDOGENOUS_SOCIETY_CHECKPOINT_V0.1.md` |
| Player-safe projection | Product UI consumes explicit immutable records and cannot reference Authority or scenario assemblies directly | Confirmed | player/product asmdefs and `InstitutionalPlayerViewTests` |

## Coding Conventions

- Namespace style: `Desk42.Institutional` with functional subnamespaces for runtime and scenarios.
- Serialized fields: private backing fields are preferred in Unity-facing code; domain DTOs intentionally use explicit mutable fields for deterministic fixtures.
- Async: no first-party async runtime pattern is established in the institutional substrate.
- Comments/docs: public boundaries and non-obvious causal-order constraints are documented; implementation comments explain invariants rather than presentation.

## Testing And Validation

- EditMode tests: institutional domain, authority, scenario and persistence coverage in `Desk42.Tests.EditMode`.
- PlayMode tests: two focused product tests cover scene boot, all five surfaces,
  broad ruling, descendant case, save/load and pre-ruling replay.
- Hosted baseline at commit `3302302`: targeted institutional suite 334/334 passed; full EditMode 454 passed, 0 failed, 1 approved pre-existing skip.
- Post-extraction local baseline: targeted institutional suite 334/334 passed;
  full remaining EditMode suite 338/338 passed with no skips.
- Endogenous society v0.1 local validation: institutional suite 381/381 passed;
  complete EditMode suite 385/385 passed with no skips.
- Causal legibility v0.1 local validation: complete EditMode 394/394 and focused
  PlayMode 2/2, both with no failures or skips. Windows x64 build, standalone
  save/load smoke and actual-build screenshot capture pass.
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

- The current IMGUI shell is a thin comprehension prototype, not the final
  presentation architecture, input model or render pipeline.
- Automated interaction is validated; six-player comprehension and agency
  testing remains outstanding.
- Visual polish, long-run pacing and commercial loop quality remain unvalidated.

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
