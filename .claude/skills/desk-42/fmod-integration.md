# FMOD integration — Desk 42

FMOD wires the Environmental Distortion Scale to audio. **This repo already contains an FMOD-shaped architecture in code** (`Desk42.Audio`), gated behind the `DESK42_FMOD` scripting define and compiling as no-op stubs until the FMOD plugin is imported. The job is **not** to add a new audio system — it's to (1) import the plugin and flip the gate, and (2) author an FMOD Studio project whose events/buses/params **match the names the code already uses**.

This doc has two layers: **what the code expects today** (the source of truth a Studio author must satisfy) and **the intended Distortion-Scale target** (an authoring goal, aspirational).

---

## Layer 1 — What the code expects today (authoritative)

### Gate & wrapper
- All FMOD calls are wrapped by **`Audio/FMODManager.cs`** (singleton). Game systems call `FMODManager.Instance.SetGlobalParameter(name, value)`, `SetBusVolume(path, vol)`, `PlayOneShot(path, pos)` — **never `RuntimeManager` directly.**
- Everything FMOD is inside `#if DESK42_FMOD`. With the define off, the wrapper is a silent stub and the game runs without audio. With it on (and the plugin imported), the wrapper resolves buses in `Awake` and forwards calls.

### Reactivity model — event bus, not polling
The per-system drivers subscribe to **`SanityChangedEvent`** (from `Narrative/RumorMillEventBus.cs`) and push FMOD params in response. **Do not add a polling `Update`-loop director** (the Brawler-style `SanityAudioDirector`/`ISanityProvider` pattern was deliberately *not* adopted here — it would be a parallel system and a param-scale mismatch).

### The real vocabulary — author Studio to these exact names
**Global parameters** (set on the Studio System via `FMODManager.SetGlobalParameter`):
| Param | Range | Pushed by | Notes |
|---|---|---|---|
| `Sanity` | **0.0–1.0 (normalized)** | `BinauralStressEngine` (`_currentSanity / 100f`) | global; every event reads it. **Not 0–100.** |
| `TidePressure` | 0.0–1.0 | `StressCrescendo` | The Tide's Flow/Ebb pressure |

**Event-local parameters** (set per `EventInstance`, not global): `SanityLevel` (BinauralStress event), `ChordRoot` / `ChordQuality` / `MidiNote` / `Velocity` / `BeatTrigger` (ProceduralJazz), `TidePressure` (Crescendo local copy).

**Buses** (`FMODManager` resolves these in `Awake`):
| Bus | Used for |
|---|---|
| `bus:/` (Master) | master volume |
| `bus:/Music` | music beds (Binaural, Jazz, Crescendo) |
| `bus:/SFX` | one-shots, interface |
| `bus:/Ambience` | ambient bed |

**Events / snapshots referenced in code** (author these paths in Studio):
- `event:/Music/BinauralStress` — sustaining bed, local `SanityLevel` param (`BinauralStressEngine`).
- `event:/Music/Jazz` — procedural jazz, chord/note params (`ProceduralJazzGenerator`).
- `event:/Music/ShiftCrescendo` — multi-track, `TidePressure` (`StressCrescendo`).
- `event:/Threat/SanityWhisper`, `event:/Threat/TideRumble`, `event:/Threat/QueuePressure`, `event:/Threat/HazardAlert`, `event:/Threat/PneumaticTube` — spatial threat one-shots (`SpatialAudioThreatSystem`, `UI/PneumaticTube`).
- `event:/Threat/TierCross` — tier-cross stinger, local `Direction` param (-1 worse / +1 recovering) (`DistortionAudioDirector`).
- `snapshot:/Fugue`, `snapshot:/MercyWindow`, `snapshot:/Flow` — mode snapshots (`DistortionAudioDirector`).

> **Event paths are plain strings, not `EventReference` fields.** Every driver serializes a `string _eventPath` and calls `RuntimeManager.CreateInstance(path)`. So the FMOD 2.02 `EventReference` migration **does not apply** to this codebase — don't convert these to `EventReference`. (The names just have to exist in the built banks at runtime.)

### Per-system drivers (read before editing audio)
- **`BinauralStressEngine`** — sustaining `event:/Music/BinauralStress`; smooths Sanity, sets local `SanityLevel` on the instance **and** pushes global `"Sanity"` (normalized) via `FMODManager`.
- **`ProceduralJazzGenerator`** — generative jazz that gets more atonal as Sanity falls (`ModeForSanity`); dispatches notes as event params.
- **`StressCrescendo`** — drives `event:/Music/ShiftCrescendo` with `TidePressure`.
- **`SpatialAudioThreatSystem`** — positional threat stingers bucketed by Sanity.
- **`AudioSettings`** — static volume prefs pushed to `FMODManager` buses on change.
- **`DistortionAudioDirector`** — the **discrete** Distortion-Scale layer (added with this pack). Event-bus driven; fires `event:/Threat/TierCross` stingers on Sanity tier crossings (hysteresis, one stinger per crossing even on multi-tier drops) and toggles `snapshot:/Fugue` (auto, from `SanityChangedEvent.TriggeredFugue`) / `snapshot:/MercyWindow` / `snapshot:/Flow`. Does **not** push global params (no double-drive). Place it on the same audio GameObject as the other drivers. **Mercy/Flow have no bus event — wire `EnterMercyWindow()/ExitMercyWindow()` (and `EnterFlow()/ExitFlow()`) from `Core/TideSystem` on Ebb/Flow** (`// TODO(wire)` left in the file).

---

## Layer 2 — Intended Distortion-Scale target (aspirational; author toward this)

The five-tier Distortion Scale (architecture.md) is the design goal: one continuous `Sanity` parameter blending a layered bed, with discrete **snapshots** for modes and **stingers** on tier crossings. Author this *inside* the existing event/bus/param contract above — don't introduce new global managers.

### Bed automation by tier (author on the `Sanity` 0–1 param inside the Music bed)
| Sanity (0–1) | Bed character | Master FX | Diegetic | Mode |
|---|---|---|---|---|
| **1.0–0.75** | muzak full; distant coffee | clean | keyboard clicks | — |
| **0.75–0.50** | muzak thinning; fluorescent hum | slight EQ darken, light verb | clock louder, phone shriller | down-stinger |
| **0.50–0.25** | atonal jazz replaces muzak; heartbeat; faint breathing | more distortion/verb; subtle pitch LFO | strained | down-stinger |
| **0.25–0.01** | tinnitus sine rises; hostile swells | heavy distortion, high-pass whine | speech garbles | down-stinger |
| **0.0 (Fugue)** | breathing solos, rest ducked | Master low-pass muffle, long verb | near-silent | `snapshot:/Fugue` |

### Snapshots to author (trigger from gameplay, not from `Sanity == 0`)
- `snapshot:/Fugue` — dissociation **mode** (`RunStateController.IsInFugueState` / the dissociation state), low-pass + duck-all-but-breathing.
- `snapshot:/MercyWindow` — The Tide's **Ebb**: lift stress layers, warm EQ.
- `snapshot:/Flow` *(optional)* — The Tide's **Flow**: tighten, push heartbeat.

### Reconciliation notes (where the kit's old design diverged — resolve before authoring)
- **Sanity scale:** code is **0–1 normalized**. The old Brawler-derived kit doc assumed 0–100. Author Studio for 0–1, or change the push site (`BinauralStressEngine`) — pick one.
- **Bus layout:** code uses Master/Music/SFX/Ambience. The kit proposed Diegetic/Interface/Voice buses. If you want those, **add them in Studio AND add the bus paths + resolves to `FMODManager`** so code can target them — don't author orphan buses.
- **Stingers/snapshots:** `DistortionAudioDirector` (shipped with this pack) already implements this layer — tier-cross stingers with hysteresis + Fugue/Mercy/Flow snapshots, event-bus driven, no global-param double-drive. Two things remain: (1) **author** `event:/Threat/TierCross` + the three snapshots in Studio, and (2) **wire** `EnterMercyWindow()/ExitMercyWindow()` (and Flow) from `Core/TideSystem` on Ebb/Flow — Fugue is already automatic from the bus.

---

## Setup checklist (Jacob — external + editor; I can't do these)
1. **Install FMOD Studio** + the **FMOD for Unity** plugin from fmod.com (needs an FMOD account; it's *not* on a public package registry — grab the `.unitypackage` or add via FMOD's UPM registry). FMOD 2.02+ (so `EventReference` is available if you author new C#).
2. **Import** the plugin into the project.
3. **Add the asmdef reference:** open `Assets/_Project/Scripts/Desk42.Core.asmdef` and add `FMODUnity` (and `FMODUnityResonance` if you use it) to `references`. *(Do this before the next step.)*
4. **Enable the gate:** Player Settings -> Scripting Define Symbols -> add `DESK42_FMOD`. *(Only after steps 2–3, or `Desk42.Audio` won't compile.)*
5. **Create the FMOD Studio project**, author buses/params/events to the Layer 1 names (incl. `event:/Threat/TierCross` with a `Direction` param and `snapshot:/Fugue` `/MercyWindow` `/Flow`), build banks.
6. **FMOD -> Edit Settings** -> point at the `.fspro` / built banks; set banks to **load at initialization** so beds can start without manual bank loading.
7. **Add `DistortionAudioDirector`** to the audio GameObject (alongside the other drivers). Fugue + tier stingers are automatic; **wire `EnterMercyWindow()/ExitMercyWindow()` (and Flow) from `Core/TideSystem`** on Ebb/Flow (`// TODO(wire)` in the file).
8. **Smoke test:** enter Play mode, change Sanity, confirm the global `"Sanity"` param moves (via `BinauralStressEngine`), a `TierCross` stinger fires on a band crossing, and `snapshot:/Fugue` ducks the mix at Sanity 0.

## First milestone (prove the spine before authoring everything)
Get `event:/Music/BinauralStress` + the global `"Sanity"` param working with two layers crossfading as Sanity drops in play mode. Confirm `FMODManager.SetGlobalParameter("Sanity", …)` moves it. Then layer the rest tier by tier. Snapshots and the full FX chain come after the bed proves out.
