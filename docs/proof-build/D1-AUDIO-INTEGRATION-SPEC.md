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

## 5. Verification checklists — all **PENDING INSTALL**

**Editor**
- [ ] Project compiles after import
- [ ] Native FMOD libraries load; no `DllNotFoundException` after normal init
- [ ] Play Mode initialises reliably
- [ ] Bank-load failure produces a useful diagnostic, not silent failure
- [ ] Four scene directors confirmed disabled before define is enabled

**Windows standalone**
- [ ] Development build produces no FMOD exceptions
- [ ] FMOD initialises on cold launch (no prior Editor session)
- [ ] Required banks load
- [ ] One known technical event plays
- [ ] Shift scene loads clean
- [ ] Quit and relaunch works

**Cold start**
- [ ] First launch on a machine with no Unity/Editor state
- [ ] `ShiftLifecycleEvent` after scene activation still initialises audio

---

## 6. Provenance fields (AudioLab / Innovator Founder)

```text
fmod_studio_version:        2.03.14                    PENDING INSTALL
fmod_unity_version:         2.03.14                    PENDING INSTALL
source:                     official Firelight distribution
fork:                       none
unity_version:              2022.3.62f3
plugin_location:            Assets/Plugins/FMOD        PENDING INSTALL
bank_location:              Assets/StreamingAssets/FMOD/Banks   PENDING INSTALL
event_contract:             docs/proof-build/D1-AUDIO-INTEGRATION-SPEC.md §2
plugin_files_in_vcs:        NONE YET                   PENDING INSTALL
project_settings_changed:   NONE YET (DESK42_FMOD still undefined)
authored_proof_audio:       ABSENT — no Venn motif, no narrative identities
editor_result:              PENDING INSTALL
standalone_cold_start:      PENDING INSTALL
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
