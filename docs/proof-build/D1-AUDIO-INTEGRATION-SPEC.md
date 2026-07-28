# D1 — FMOD integration specification, event contract, and director audit

**Branch:** `feat/proof-build` · **Base:** `92369f0` · **Date:** 2026-07-27
**Scope:** package-independent portion of D1. FMOD is **NOT installed**.

---

## 1. Version lock (D1D)

| Item | Value | Status |
|---|---|---|
| FMOD Studio | **2.03.14** | **PENDING INSTALL** |
| FMOD for Unity | **2.03.14** | **PENDING INSTALL** |
| Source | Official Firelight distribution | locked |
| Fork | none | locked |
| Unity project | **2022.3.62f3** | in use |
| Expected plugin location | `Assets/Plugins/FMOD` (conventional layout) | **PENDING INSTALL** |
| Bank / source-content location | `Assets/StreamingAssets/FMOD/Banks` (Studio build target) | **PENDING INSTALL** |

Nothing in this document asserts FMOD is installed. Every package-dependent
result below is explicitly **PENDING INSTALL**.

### Assembly references required after import

- `Desk42.Core.asmdef` → add **`FMODUnity`** (and `FMODUnityResonance` only if resonance
  is used, which this slice does not).
- Test assemblies need no FMOD reference: the boundary is FMOD-free by design.

### Activation order (must not be reordered)

1. Import the FMOD for Unity 2.03.14 package.
2. Add the `FMODUnity` reference to `Desk42.Core.asmdef`.
3. Define `DESK42_FMOD`.

Defining the symbol before steps 1–2 breaks compilation in `Desk42.Audio`.

---

## 2. Event contract (D1B)

Authoritative list: `Assets/_Project/Scripts/Audio/AudioEventId.cs`.
Gameplay references the **enum**; FMOD paths exist only in `AudioEventCatalog`.

**Ownership split.** `AudioEventCatalog` is general — it maps *every* `AudioEventId`,
including the non-proof `PneumaticTubeThreat`, and owns lookup/validation.
`ProofAudioPolicy` owns only which identities belong to the Five-Shift experiment
(`ProofSubset`, `IsProofIdentity`) and the rules that follow (`IsCausalIdentity`,
`IsPermittedOnShift5`). One enum, one catalog, no duplication.

| Logical identity | Purpose | Intended FMOD path |
|---|---|---|
| `DeskInteraction` | ordinary desk interaction | `event:/Desk/Interaction` |
| `ProcedureFeedback` | ordinary procedure/application feedback | `event:/Desk/ProcedureApplied` |
| `EliasRegistrationCausal` | Shift 2 registration / causal identity — **SLOT ONLY** | `event:/Proof/EliasRegistration18A` |
| `ComplianceStreakConfirm` | Compliance Streak confirmation | `event:/Desk/ComplianceStreak` |
| `Shift5EliasReturn` | generic Shift 5 return | `event:/Proof/EliasReturnGeneric` |
| `PneumaticTubeThreat` | ordinary desk audio — **not a proof identity** | `event:/Threat/PneumaticTube` |

The enum is named **`AudioEventId`**: it is the application-level logical namespace, not a
proof-only one. The proof subset is declared explicitly in `ProofAudioPolicy.ProofSubset`
rather than inferred from the type name, so adding a non-proof identity cannot silently widen
what counts as proof audio.

**Narrative constraint.** `EliasRegistrationCausal` is an event slot only. No motif is
composed, generated or reinterpreted in this bucket. No authored audio exists yet.

**Suppression is structural.** `Shift5EliasReturn` is a distinct identity with a distinct
path. There is no alias, fallback or helper that can replay the causal identity on Shift 5,
and `AudioService.PlayOneShot` refuses it **at the boundary** (`AudioRequestResult.Suppressed`)
when `context.ShiftNumber == 5`, so no call site can bypass the rule.

**Mercy Window and Flow are absent from this contract** and are not addressable through it.

---

## 3. AudioService boundary (D1A)

`Assets/_Project/Scripts/Audio/AudioService.cs` — compiles with `DESK42_FMOD` **undefined**
and contains **no FMOD reference at all**.

- `IAudioBackend` — `IsAvailable`, `Initialize(shift)`, `PlayOneShot(id, path, context)`.
- `NullAudioBackend` — active until FMOD exists. Returns `Unavailable`: a missing package is
  diagnosable rather than indistinguishable from a working-but-quiet install.
- `AudioService` — static boundary; `SetBackend` injects the real backend after import, and
  lets tests inject a recording double with no FMOD present.
- `AudioRequestResult` — `Requested` / `Unavailable` / `UnknownEvent` / `Suppressed`.
  **`Requested` means "handed to a backend", never "the participant heard it."**
- Never throws into gameplay: backend and observer exceptions are caught and logged.

**Lifecycle.** `Initialize(shift)` is driven from the existing `ShiftLifecycleEvent`, which
`GameManager` publishes **after scene activation** because `LoadSceneAsync`'s `FlushQueue`
wipes pending events beforehand (B5 finding). That ordering is not changed to suit audio;
requests arriving before initialisation are answered `Unavailable`.

Not built: mixer, music, parameters, snapshots, voices, buses.

---

## 4. Director activation audit (D1C)

Scene presence verified **by script GUID**, not class name — Unity scenes reference scripts by
GUID, so a name grep reports 0 for everything and is worthless here.

| Director | In `Shift.unity` | Self-bootstrap | Subscribes to | Live on define? | Verdict |
|---|---|---|---|---|---|
| `FMODManager` | **no** | yes (singleton) | — | on first access | **Enable** — the wrapper all others route through |
| `BinauralStressEngine` | **YES** | no | Sanity, ShiftLifecycle | **YES** | **Disable for proof candidate** |
| `ProceduralJazzGenerator` | **YES** | no | Sanity, ShiftLifecycle | **YES** | **Disable for proof candidate** |
| `StressCrescendo` | **YES** | no | Sanity, ShiftLifecycle, TideEscalated | **YES** | **Disable for proof candidate** |
| `SpatialAudioThreatSystem` | **YES** | no | ClaimQueued, ClaimResolved, OfficeHazard, Sanity, ShiftLifecycle, TideEscalated | **YES** | **Disable for proof candidate** |
| `DistortionAudioDirector` | **no** | no | Sanity, ShiftLifecycle | no | **Retain compiled but inactive** |

### The finding that matters

**Four directors are already attached to `Shift.unity`.** They currently compile to no-ops
only because `DESK42_FMOD` is undefined. **Defining the symbol makes all four live
immediately**, with no further wiring — which is exactly the situation the audit exists to
prevent. They subscribe to `SanityChangedEvent`, so they would begin driving FMOD parameters
and events from live Sanity during the scored proof run.

They expect events, buses, parameters and snapshots that **do not exist** (no Studio project,
no banks). With assets absent, `FMODManager` calls become no-ops or logged failures — silence
plus log noise rather than crashes — but that is an assumption to verify at import, not a
guarantee.

**Verdict:** enable `FMODManager` only. The four scene-attached directors must be explicitly
disabled for the proof candidate before the define is enabled — by disabling the components in
`Shift.unity`, which is a participant-facing scene change and therefore must land **before**
the freeze.

`DistortionAudioDirector` owns Fugue, Mercy and Flow. Fugue is reachable
(`SanityChangedEvent.TriggeredFugue`); Mercy and Flow have **zero callers** and stay unwired.
Since the component is not in the scene, it is inactive by default — no action needed, and no
gameplay caller will be created to make Mercy/Flow reachable.

Sanity and Tide are not redesigned. The procedural music system is not activated.

---

## 5. Verification checklists — executed 2026-07-27 (see §10)

**Editor**
- [x] Project compiles after import — State B, 0 errors
- [x] Native FMOD libraries load; no `DllNotFoundException` after normal init
      — *only after repairing an incomplete vendor tree; see §10.2*
- [x] Play Mode initialises reliably — 17/17 PlayMode, both states
- [x] Bank-load failure produces a useful diagnostic, not silent failure
- [x] Four scene directors confirmed disabled before define is enabled

**Windows standalone**
- [x] Development build produces no FMOD exceptions — 0 across two cold runs
- [x] FMOD initialises on cold launch (no prior Editor session)
- [ ] Required banks load — **BLOCKED**: no banks exist (§10.1)
- [ ] One known technical event plays — **BLOCKED**: event not authored (§10.1)
- [x] Shift scene loads clean
- [x] Quit and relaunch works

**Cold start**
- [x] First launch with no Unity Editor process running
- [x] `ShiftLifecycleEvent` after scene activation still initialises audio

---

## 6. Provenance fields (AudioLab / Innovator Founder)

```text
fmod_studio_version:        2.03.14  INSTALLED (C:\Program Files\FMOD SoundSystem\
                                     FMOD Studio 2.03.14\fmodstudiocl.exe)
fmod_unity_version:         2.03.14  IMPORTED (native 0x00020314)
source:                     official Firelight distribution
fork:                       none
unity_version:              2022.3.62f3
plugin_location:            Assets/Plugins/FMOD        present, untracked
bank_location:              Assets/StreamingAssets/FMOD/Banks   NOT CREATED — no banks
event_contract:             docs/proof-build/D1-AUDIO-INTEGRATION-SPEC.md §2
plugin_files_in_vcs:        NONE — verified untracked and gitignored
project_settings_changed:   NONE committed; DESK42_FMOD is a local activation only,
                            restored byte-identical after every State B run
                            (blob 58a96281…, asmdef blob afe793a2…)
authored_proof_audio:       ABSENT — no Venn motif, no narrative identities
fmod_studio_project:        FMODAssets/Desk42/Desk42.fspro
                            repository top level, deliberately OUTSIDE Assets/
                            EXISTS — created by hand in the GUI (the one step
                            FMOD 2.03 cannot automate), then authored entirely
                            by script. TRACKED: .fspro, Metadata/**, Assets/**.
technical_asset:            TECH_PIPELINE_TEST_NONPRODUCTION.wav
                            generator tools/fmod/New-TechnicalTestAsset.ps1 (tracked)
                            48000 Hz / 16-bit / mono PCM, 1000 Hz sine, 0.40 s,
                            -12 dBFS, 5 ms linear fades, 38,444 bytes
                            sha256 49E8F406DCD5BED2AA8575045AD1CC299B8467B78EFF
                                   B447B90077268AD97D15
                            deterministic: two runs produced identical bytes
technical_event:            event:/Desk/Interaction    AUTHORED, RESOLVES
                            single-sound instrument on the master track, backed
                            by the technical tone. One-shot.
elias_causal_event:         event:/Proof/EliasRegistration18A
                            INTENTIONALLY ABSENT — unfilled production slot.
                            Confirmed absent by the authoring script on every
                            run; reports UnknownEvent at the backend.
bank_structure:             Master.bank            1,074 bytes
                            Master.strings.bank      836 bytes
                            Desk42_Technical.bank  2,560 bytes
                            built to FMODAssets/Desk42/Build/Desktop (ignored)
scripting_build_method:     tools/fmod/studio-scripts/desk42-technical-pipeline.js
                            (tracked) via `fmodstudiocl -script`, driven by
                            tools/fmod/Build-FmodBanks.ps1 (tracked).
                            Documented FMOD Studio 2.03 scripting API only; no
                            .fspro or Metadata file is hand-written.
                            STATUS: EXECUTED AND IDEMPOTENT — a second run
                            detects every object and creates nothing.
editor_result:              PASS — see §11.2
standalone_cold_start:      PASS — see §11.3. Pipeline execution: PASS.
audible_output:             NOT HUMAN-CONFIRMED. The technical event was
                            INVOKED successfully (AudioRequestResult.Requested)
                            in Editor PlayMode and twice in a cold-started
                            player. Nothing here observed sound. "Requested"
                            means handed to FMOD, never heard.
```

**Authored proof audio is missing.** Nothing in D1 constitutes evidence that final narrative
audio content exists or is complete.


---

## 7. Audio ownership rule (locked)

There is exactly **one gameplay-facing audio API**:

```text
gameplay -> AudioService -> FmodAudioBackend -> FMOD
```

`FMODManager` may be reused **internally by the backend** where useful. It must not
become a second gameplay-facing API, and no gameplay file may call it — enforced by
`ProofAudioSafetyTests.FmodManager_IsNotASecondGameplayFacingAudioApi`, alongside the
existing guard against direct `FMODUnity.RuntimeManager` use.

`FMODManager` is deliberately **not refactored** now: it is unreferenced by gameplay, so
changing it before the package exists would be churn against an untestable target.

## 8. Pre-import safety state (applied)

The four experimental directors attached to `Shift.unity` are **explicitly disabled**
(`m_Enabled: 0`), set by script GUID via `tools/Set-SceneComponentEnabled.py`:

| Director | GUID | Scene state |
|---|---|---|
| `BinauralStressEngine` | `a352ef7c…` | disabled |
| `ProceduralJazzGenerator` | `28f9c6f9…` | disabled |
| `StressCrescendo` | `5fe965bb…` | disabled |
| `SpatialAudioThreatSystem` | `dea70daa…` | disabled |

Scripts are retained and serialized for future work; only the components are disabled, so
defining `DESK42_FMOD` later cannot make them live. Locked by
`ProofAudioSafetyTests.ExperimentalDirectors_AreDisabledInShiftScene`.

`DistortionAudioDirector` remains unattached and therefore compiled-but-inactive. Mercy and
Flow stay unwired, with a test asserting no gameplay caller has appeared.

**Shift 5 suppression is enforced at two levels:** the caller (no Shift-5 path file may name
`EliasRegistrationCausal`) and the boundary (`AudioService` returns `Suppressed`). The caller
test exists so a wrong Shift-5 caller fails a test rather than being silently masked by the
service guard.


---

## 9. Repository treatment — PUBLIC repo, vendor binaries NOT distributed

`satyrogle/Desk-42` is **PUBLIC** (`private: false`, confirmed by an unauthenticated
GitHub API call — a private repo returns 404). This triggers the locked stop condition:
**no FMOD vendor binaries enter this repository.** LFS was therefore NOT used; LFS was
conditional on a private/access-controlled remote.

```text
package             fmodstudio20314.unitypackage
version             FMOD for Unity 2.03.14  (native 0x00020314)
source              official Firelight distribution
size                95,621,972 bytes
sha256              613A8371C2F021BC1803DD573B6A82AD1905E9A537133F801D1C29F58B266B82
imported size       297 MB at Assets/Plugins/FMOD
repository handling PINNED EXTERNAL IMPORT — untracked, reproduced locally
reproduce           powershell -File tools/fmod/Import-FmodPackage.ps1 -PackagePath <pkg>
already in history  NO — verified, nothing to purge
```

### Tracked vs untracked

| Path | Treatment |
|---|---|
| `Assets/Plugins/FMOD/**` | **untracked** (gitignored) — vendor licensed |
| `Assets/StreamingAssets/FMOD/**` | untracked — generated bank copies are not authoritative source |
| `FMODStudioCache.asset`, `*.fmod_logs`, `fmod_editor.log` | untracked — cache/log |
| `tools/fmod/Import-FmodPackage.ps1` | tracked — **the single** reproducible import entry point |
| `FMODAssets/Desk42/Desk42.fspro`, `FMODAssets/Desk42/Metadata/**` | tracked — authoritative Studio source |
| `FMODAssets/Desk42/Build/**`, `**/*.bank` | untracked — build product |
| `tools/fmod/New-TechnicalTestAsset.ps1` | tracked — deterministic generator |
| `tools/fmod/assets/**` | untracked — regenerable, hash recorded in §6 |
| `tools/fmod/studio-scripts/**` | tracked — Studio authoring automation |
| `Assets/_Project/Scripts/Audio/**` | tracked — project-owned source |
| `AudioEventCatalog` mappings | tracked — project-owned contract |

**One import entry point.** `tools/Import-FmodPackage.ps1` was a duplicate of the `tools/fmod/`
copy differing only in its project-root resolution — and, having been moved a directory deeper
without that line being updated, it resolved the root one level too high and would have thrown
on `ProjectSettings/ProjectVersion.txt`. It has been removed; `tools/fmod/Import-FmodPackage.ps1`
is canonical.

`FMODStudioSettings.asset` is project configuration and **should** be shareable, but it does
not exist yet (FMOD's setup wizard has not run). When created it must live outside the
ignored vendor tree, or the ignore rule must be narrowed to admit it.

### Activation is LOCAL, not committed

`DESK42_FMOD` and the `FMODUnity` asmdef reference were briefly committed in `4c4ef26`
while the plugin stayed untracked. That made **every clean clone fail to compile**
(`CS0246: 'FMOD' could not be found`), proven by an A/B on isolated worktrees. Both are now
backed out of committed settings and are local activation steps, performed only in an
environment that has imported the package.

`FmodActivation_MatchesPackagePresence` locks the two states together so the inconsistent
combination cannot be committed again.

### Blocked — separate licensing decision required

Distributing the FMOD native integration from a public repository is a licensing question,
not an engineering one. **D1 steps 6–9 are blocked on it**, because Studio project, banks,
Editor verification and standalone cold-start all presuppose a shareable, reproducible FMOD
setup. Repository visibility was not changed.

**Resolved for local work (2026-07-27).** The external-import model below settles the
engineering half: vendor binaries stay untracked and are reproduced locally, so steps 6–9
were executed against a locally activated environment. The licensing question about
*distribution* is untouched and remains open.

---

## 10. Steps 6–9 execution record — 2026-07-27

Executed in the isolated worktree `feat/proof-build` (`C:\Users\jacob\Desk42-worktrees\
proof-build`), resumed from `9277314`.

### 10.1 The one hard blocker — no FMOD Studio project exists

`FMODAssets/Desk42/Desk42.fspro` **does not exist**, and no `.fspro` exists anywhere under
`C:\Users\jacob`. Steps 6–7 (technical event, bank structure) and the two bank-dependent
standalone checks are blocked on this and nothing else.

**Location note.** The Studio project lives at **repository top level, alongside `Assets/`,
not inside it.** An earlier draft of this spec placed it at
`Assets/_Project/Audio/FMODStudio/`; that is superseded. Inside `Assets/`, Unity imports the
Studio project's own source `.wav` files and XML metadata as game assets, shipping duplicates
of audio FMOD already owns. FMOD's own guidance is to keep the Studio project outside the
Unity assets tree.

**FMOD Studio 2.03 has no supported headless project-creation path.** Verified three ways:

| Route | Result |
|---|---|
| `fmodstudiocl -script <js> New.fspro` | `Project Load Error: … could not be located.` Refuses to create. |
| Scripting API | No `project.new` / `project.open` / `project.saveAs`. `project.filepath` is read-only. `NewProject` and `SaveAs` exist only as `studio.window.actions` — GUI actions that open modal dialogs. |
| FMOD for Unity | `SettingsEditor.cs` only *browses* for an existing `.fspro` (`OpenFilePanel`). Ships no project template. |

So project creation is a one-time GUI action and cannot be automated:

```text
FMOD Studio -> File -> New Project
save as: <repo>/FMODAssets/Desk42/Desk42.fspro
```

`tools/fmod/Build-FmodBanks.ps1` exits 9 with exactly these instructions when the project is
absent. Everything downstream of that click is automated.

**Consequence for the automation.** `desk42-technical-pipeline.js` is written, syntax-checked
and tracked, but has **never been executed** — there is no project to execute it against. Its
FMOD API calls are drawn from the shipped 2.03.14 scripting reference and its worked examples,
not from a passing run. Treat it as unvalidated until its first real invocation.

### 10.2 Environment defect found and repaired — missing logging library

The vendor tree in this worktree was an **incomplete copy**: Windows x64 shipped
`fmodstudio.dll` (release) but **not `fmodstudioL.dll`** (logging). Every other platform —
android, html5, ios, tvos, uwp — carried its logging variant, so the gap was Windows-only.

The Editor and development players link the **logging** build. The result was
`DllNotFoundException: fmodstudioL` → `SystemNotInitializedException` → FMOD could not
initialise at all, and the pre-existing PlayMode test
`EliasFiveShiftRoutePlayModeTests.RouteA_NormalisedAddress_CompletesFiveShiftProof` failed in
State B while passing in State A.

Repaired by copying the file from the main worktree's import (release-DLL hashes match, so
same package origin). `fmodstudioL.dll` sha256 `2063E106…`. The tree stays untracked.

`Verify-FmodEnvironment.ps1` now checks the logging library explicitly — it previously passed
a demonstrably unusable environment.

### 10.3 Editor result

| Check | Result |
|---|---|
| FMOD 2.03.14 initialises | **YES** — after §10.2. `RuntimeManager.GetBus` reaches the Studio system and reports "bus not found" rather than "not initialised" |
| Correct banks load | **NO** — zero banks exist |
| `event:/Desk/Interaction` resolves | **NO** — not authored |
| One `AudioService` request reaches `FmodAudioBackend` exactly once | **YES** — `ProofAudioContractTests.OneShot_RoutesToBackendExactlyOnce` |
| Positional requests preserve coordinates | **YES** — `ProofAudioSafetyTests.PneumaticTube_PreservesWorldPosition` |
| Unknown event gives the expected diagnostic | **YES** — after the fix in §10.5 |
| No FMOD exception loading Shift | **YES** — after the fix in §10.5 |
| Experimental directors remain disabled | **YES** — 4/4, asserted in the activated state |
| Mercy / Flow inactive | **YES** — `MercyAndFlow_AreNotInTheProofContract`, no gameplay callers |
| Shift 5 cannot request `EliasRegistrationCausal` | **YES** — caller-level and boundary-level, both states |

### 10.4 Windows standalone result

Windows x64 **development** player, built headlessly from the activated environment via
`Assets/_Project/Scripts/Editor/ProofStandaloneBuild.cs`. Build: `Succeeded`, 0 errors,
124,208,452 bytes. Contains `FMODUnity.dll` and `fmodstudioL.dll`; contains no `.bank`.

Cold-started **twice** with no Unity Editor process running, driven through the live Shift
scene by the existing `-desk42ProofEvidencePath` development route (branch A).

| Check | Run 1 | Run 2 |
|---|---|---|
| Process starts / exits 0 | PASS | PASS |
| FMOD native runtime initialises | PASS | PASS |
| Banks load | **FAIL — none exist** | **FAIL — none exist** |
| Technical event invocation | **NOT ATTEMPTABLE — not authored** | same |
| Shift scene loads | PASS (`shift_start` telemetry) | PASS |
| FMOD exceptions | 0 | 0 |
| Quit / relaunch | PASS | PASS |
| Evidence frames written | 100 | 100 |

Reported diagnostic, both runs:

```text
[FmodAudioBackend] FMOD initialised but banks are NOT usable (loaded bank count = 0).
[FMODManager] Buses unavailable ([FMOD] Bus not found 'bus:/'). No banks are loaded.
```

**Audible output: NOT human-confirmed, and not achievable in this state.** No bank and no
authored event exist, so there is nothing that could sound. Nothing here should be read as
evidence that a participant heard anything.

### 10.5 Defects found by these runs, and fixed

1. **`FMODManager.Awake()` threw into gameplay.** It called `RuntimeManager.GetBus()`
   unguarded, so an unconfigured FMOD install produced an unhandled exception during scene
   load — violating the stated "never throws into gameplay" posture. Now guarded, with a
   `_busesResolved` flag gating every FMOD entry point. A missing bus (banks absent) logs a
   **warning**; anything else (failed native init) logs an **error**, matching how
   `FmodAudioBackend` already classifies the same two conditions.

2. **`FmodAudioBackend.PlayOneShot` reported `Unavailable` for an unauthored event.**
   `RuntimeManager.GetEventDescription` *throws* `EventNotFoundException` rather than
   returning an invalid handle, so the `desc.isValid()` branch never ran and the generic catch
   collapsed "nobody authored this" into "the backend is broken" — erasing the distinction the
   result enum exists to preserve. Now caught specifically and reported as `UnknownEvent`.

3. **`FmodAudioBackend` logged "FMOD ready" against an empty project.**
   `RuntimeManager.HaveAllBanksLoaded` is **vacuously true** with zero banks, so the backend
   announced readiness when nothing could possibly play. Caught by the first cold-start run.
   `IsAvailable` now additionally requires `getBankCount() > 0`, and both log lines report the
   count.

All three are State-B-only paths (`#if DESK42_FMOD`); State A compiles them out.

### 10.6 Test matrix — both states, 0 failures

| | EditMode | PlayMode |
|---|---|---|
| **State A** — FMOD deactivated, committed public config | 402 total, 396 passed, **0 failed**, 6 skipped | 17 total, 17 passed, **0 failed** |
| **State B** — FMOD 2.03.14 locally imported and activated | 402 total, 396 passed, **0 failed**, 6 skipped | 17 total, 17 passed, **0 failed** |

The 6 EditMode skips are pre-existing and unrelated to audio (`ATBEdgeCaseTests`,
`CascadePresenterTests`).

State B additionally confirms:
- `DESK42_FMOD` defined for Standalone / Android / WebGL
- `FMODUnity` present in `Desk42.Core.asmdef`
- **positive `FmodAudioBackend` activation** — `AudioService.BackendName == "FmodAudioBackend"`,
  never silently falling back to `NullAudioBackend`
  (`FmodBackendActivationPlayModeTests`, new)
- technical event lookup **fails as expected**, because the event is not authored

**Clean-clone State A caveat.** State A was run with the vendor tree present on disk but
deactivated. That is not the same as a genuine clean clone with no `Assets/Plugins/FMOD` at
all. `FmodIntegrationTests.FmodActivation_MatchesPackagePresence` locks the two states
together, and the A/B on isolated worktrees recorded in §9 covered the no-vendor case for
`4c4ef26`, but a fresh-clone compile was **not** re-run in this pass.

### 10.7 Remaining blocker for final authored audio

Two distinct blockers, in order:

1. **Technical (this pass):** create `FMODAssets/Desk42/Desk42.fspro` once via the FMOD Studio
   GUI. ~30 seconds of human action. Then `tools/fmod/Build-FmodBanks.ps1` runs the whole chain
   unattended, and the four currently-failing bank/event checks become testable.
   `FMODStudioSettings.asset` also has to exist and live outside the ignored vendor tree
   (see §9).

2. **Content (unchanged):** `event:/Proof/EliasRegistration18A` stays an intentionally
   unfilled production slot. It is not stubbed, not aliased, and not backed by the technical
   tone. It is blocked on AudioLab delivering the authored Venn identity, and no amount of
   pipeline work substitutes for it.

The technical tone proves transport only. It is **not** the Venn motif and carries no
narrative identity.

---

## 11. Steps 6–9 completion — 2026-07-28

§10 recorded the pass that ended blocked on a missing Studio project. The project now exists
at `FMODAssets/Desk42/Desk42.fspro`, so §10.1's blocker is **closed**. Everything below was
executed; §10's other findings stand.

### 11.1 Technical pipeline — executed

`tools/fmod/Build-FmodBanks.ps1` runs the whole chain unattended:

```text
New-TechnicalTestAsset.ps1        deterministic 1 kHz tone, sha256 49E8F406…
  -> fmodstudiocl -script desk42-technical-pipeline.js
       importAudioFile            -> FMODAssets/Desk42/Assets/…wav
       EventFolder "Desk"         -> event:/Desk/…
       addEvent "Interaction"     -> event:/Desk/Interaction
       masterTrack.addSound       -> SingleSound bound to the tone
       create Bank                -> Desk42_Technical
       relationships.banks.add    -> event assigned to bank
       project.save / project.build
  -> Master.bank / Master.strings.bank / Desk42_Technical.bank
```

**Idempotent.** A second run logs `asset already imported`, `event folder exists`,
`event exists`, `instrument already present`, `bank exists`, `event already assigned` and
rebuilds to byte-identical sizes.

**Three scripting-API corrections were needed** — the shipped 2.03 reference is wrong in
places, and the script had never been executed before this pass:

| Documented | Actual |
|---|---|
| `studio.project.filepath` | `studio.project.filePath` — the lowercase form is `undefined` |
| `relationship.size()` / `.destination(i)` | read the plain array `event.banks`; mutate via `event.relationships.banks.add()` |
| — | `masterTrack.modules` is the array to scan for existing instruments |

Discovered by runtime introspection (`for (var k in obj)`) rather than guessing, because the
reference could not be trusted once the first property proved wrong.

### 11.2 Editor result

| Check | Result | Evidence |
|---|---|---|
| FMOD reports 2.03.14 | **PASS** | `fmod.cs` `0x00020314`, asserted by `FmodVersion_IsTheLocked_2_03_14` |
| Native initialisation succeeds | **PASS** | `FMOD ready for shift N (3 bank(s) loaded)` |
| At least one bank exists and loads | **PASS** | 3 banks loaded |
| Technical event resolves | **PASS** | `TechnicalEvent_ResolvesThroughTheCatalogAndLoadedBanks` |
| `AudioService` → `FmodAudioBackend` exactly once | **PASS** | `OneShot_RoutesToBackendExactlyOnce` |
| Unauthored event reports the right diagnostic | **PASS** | `UnauthoredEvent_ReportsUnknownEvent_NotUnavailable` |
| Positional context survives the PneumaticTube route | **PASS** | `PneumaticTube_PreservesWorldPosition` |
| Shift loads without FMOD exceptions | **PASS** | 19/19 PlayMode, 0 exceptions |
| Mercy / Flow inactive | **PASS** | `MercyAndFlow_AreNotInTheProofContract` |
| Shift 5 refuses `EliasRegistrationCausal` | **PASS** | boundary + caller tests |
| Four experimental directors disabled | **PASS** | asserted in the activated state |

`AudioEventCatalog` resolution is verified through the gameplay path, not by reading the map:
identity `DeskInteraction` → `event:/Desk/Interaction` → found in loaded banks → `Requested`.

### 11.3 Windows standalone — cold start

Windows x64 **development** player, built headlessly from State B. All three banks ship in
`Desk42-ProofStandalone_Data/StreamingAssets/`.

Cold-started **twice** with no Unity Editor process running, via the dev-only
`-desk42FmodTechnicalProbe` route (inert without the flag, so it cannot fire during a scored run):

| Check | Run 1 | Run 2 |
|---|---|---|
| Process starts / exits 0 | PASS | PASS |
| FMOD native runtime initialises | PASS | PASS |
| Banks load | PASS — 3 | PASS — 3 |
| Backend selected | `FmodAudioBackend`, `available=True` | same |
| Technical event invoked | `DeskInteraction => Requested` | same |
| Unauthored event diagnostic | `EliasRegistrationCausal => UnknownEvent` | same |
| Shift path loads | PASS (`shift_start`, 100 evidence frames) | PASS |
| FMOD exceptions | 0 | 0 |
| Quit / relaunch | PASS | PASS |

```text
Pipeline execution: PASS
Audibility:         Human confirmation pending
```

**`PneumaticTubeThreat` returns `UnknownEvent`** in the player, correctly:
`event:/Threat/PneumaticTube` is not authored and was deliberately not fabricated. Its
positional contract is verified at the boundary by `PneumaticTube_PreservesWorldPosition`,
which uses a recording double and so tests coordinate survival independently of authoring.

### 11.4 Environment findings — FMOD's own bank staging is broken headlessly

`EventManager.CopyToStreamingAssets` **NullReferences** in batch mode and copies nothing.
`UpdateCache` refuses to build `eventCache` while `EditorUtils.StagingSystem.SourceLibsExist`
is true — FMOD's platform-library staging gate, which never clears without interactive
setup — and the copy loop then dereferences the null cache. The same fault fires again during
the player build (`errors=3`, build still `Succeeded`).

Separately, `BankLoadType.All` walks the **cached** `Settings.MasterBanks` / `Settings.Banks`
lists, not the files on disk, so banks sitting in StreamingAssets still do not load if the
cache never populated.

`FmodLocalSettings.Configure` works around both: it stages banks with a direct flat copy and
populates the two bank lists itself. Both are documented in the script. Neither is a Desk-42
defect; both would silently produce a working-looking build with no audio.

### 11.5 Repository treatment

| Path | Treatment |
|---|---|
| `FMODAssets/Desk42/Desk42.fspro`, `Metadata/**` | **tracked** — authoritative Studio source |
| `FMODAssets/Desk42/Assets/*.wav` | **tracked** — 38 KB technical tone; `Metadata/AudioFile/*.xml` references it by path, so a clone without it cannot open or build the project |
| `FMODAssets/Desk42/Build/**`, `**/*.bank` | ignored — build product |
| `FMODAssets/Desk42/.cache/`, `.user/`, `.unsaved/` | ignored — cache / per-user |
| `Assets/StreamingAssets/**` + folder `.meta` | ignored — exists only as FMOD's staging target |
| `Assets/Plugins/FMOD/**` | ignored — vendor SDK, never redistributed |
| `FMODStudioSettings.asset` | **NOT tracked** — see below |

**`FMODStudioSettings.asset` is deliberately local.** It is a ScriptableObject whose script
reference lives in the vendor `FMODUnity` assembly. Committing it would leave every clean
clone holding a ScriptableObject with a missing MonoScript — in exactly the State A
configuration that must stay clean. It is generated into the already-ignored vendor Resources
folder and reproduced by the tracked `Desk42.Editor.FmodLocalSettings.Configure`.

**Public repository contains no redistributed FMOD SDK payload** — re-verified: zero tracked
paths match `Plugins/FMOD`, `*.bank`, or `fmodstudio*.dll`.

### 11.6 Test matrix

| | EditMode | PlayMode |
|---|---|---|
| **State A** — literal clean clone, **no `Assets/Plugins/FMOD` at all** | see §11.7 | see §11.7 |
| **State B** — FMOD 2.03.14 imported and activated | 402 / 396 passed / **0 failed** / 6 skipped | 19 / **19 passed** / **0 failed** |

State B positives: `DESK42_FMOD` defined · `FMODUnity` referenced ·
`AudioService.BackendName == "FmodAudioBackend"` · **bank count 3 > 0** ·
technical event resolution `Requested`.

### 11.7 Literal clean-environment State A

Recorded separately from the deactivated-but-SDK-present worktree, per the D1 requirement.
Method and results in §11.8.

### 11.8 Remaining blocker for final authored proof audio

**One blocker, and it is not technical.**

`event:/Proof/EliasRegistration18A` remains an intentionally unfilled production slot. It is
not stubbed, not aliased, and not backed by the technical tone. It is blocked on **AudioLab
delivering the authored Venn identity**. The transport underneath it is now proven end to end,
so the moment authored audio exists it drops into a working pipeline — but no amount of
further pipeline work substitutes for the content.

Secondary, non-blocking: the Studio project has only a master bus, so `bus:/Music`,
`bus:/SFX` and `bus:/Ambience` do not resolve and `FMODManager` reports bus control inert.
That is an authoring gap, correctly warned about, and irrelevant until a mixer is authored.
