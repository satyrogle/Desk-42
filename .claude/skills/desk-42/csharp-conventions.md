# C# / Unity conventions — Desk 42

House style for new code. **Where existing code in the repo differs, match the repo, not this file** — consistency beats these defaults.

## Structure
- One namespace per system, all under `Desk42.*` (24 namespaces exist — `Desk42.Core`, `Desk42.BSM`, `Desk42.BSM.States`, `Desk42.BSM.Transitions`, `Desk42.BehaviourTrees`, `Desk42.RedTape`, `Desk42.Audio`, `Desk42.Cards`, `Desk42.Claims`, `Desk42.Encounter`, `Desk42.MoralInjury`, `Desk42.Meta.*`, `Desk42.UI`, `Desk42.Tutorial`, `Desk42.Accessibility`, `Desk42.Archetypes`, `Desk42.OfficeSupplies`, `Desk42.Leaderboard`, `Desk42.Editor`, `Desk42.EditorTools`, …). New code goes in the namespace of the system it belongs to — don't invent new ones.
- **Assemblies:** runtime code is a **single asmdef**, `Assets/_Project/Scripts/Desk42.Core.asmdef` (references Input System, TextMeshPro, Newtonsoft, URP). There is no per-system asmdef split today — don't add one without a reason. Editor-only code lives in `Scripts/Editor/` (`Desk42.Editor` / `Desk42.EditorTools`). Tests are `Desk42.Tests.EditMode` / `Desk42.Tests.PlayMode`.
  - **FMOD note:** the `#if DESK42_FMOD` audio code references `FMODUnity` / `FMOD.Studio`. When the FMOD plugin is imported, `Desk42.Core.asmdef` must add an **`FMODUnity`** reference (and `FMODUnityResonance` if used) or those blocks won't compile. See `fmod-integration.md`.
- Data as **ScriptableObjects** under `Assets/_Project/ScriptableObjects/` (client archetypes, BSM defs, claim templates, regulations, stamps, factions, moral, office supplies). Code reads data; designers edit data.

## State machines & BT
- The BSM and Red Tape Engine already exist (`ClientStateStack` / `StateInjector` / `BehaviourTree` + `MutationEngine`). **Read the base classes/interfaces before adding states or nodes** (`BSM/`, `BehaviourTrees/BTNode.cs`, `BTStatus.cs`). Match the existing contracts; don't introduce a parallel pattern.
- New BT nodes: define entry, tick, exit, **and removal** (injection cleanup is mandatory — see known-issues.md).

## Reactivity
- Prefer the **event bus** (`Narrative/RumorMillEventBus.cs`) over polling. Subscribe to `SanityChangedEvent` (and siblings) rather than reading `RunStateController` in `Update`. The audio systems are the reference pattern.

## Tunables
- **No magic numbers** in gameplay logic. Every tunable (thresholds, timers, decay rates, transition costs) is a serialized field or lives on a ScriptableObject. If you must hardcode, leave a `// TODO tunable` and flag it.

## Unity hygiene
- Prefer `[SerializeField] private` over `public` for inspector fields.
- Cache component refs in Awake; don't `GetComponent` in Update.
- Avoid per-frame allocations on the desk's hot paths (decay, ticks, audio param pushes).
- Keep MonoBehaviours thin; put logic in plain C# classes the MonoBehaviour drives where practical (lets you test the BSM/Tide without the editor).

## Determinism
- UI Decay and any RNG-driven disruption: route through a seeded source if save/load or replays need reproducibility. Decay must fully reset on recovery — no residual drift after Sanity climbs back.

## Saves
- Persistence lives in `Persistence/` (`RunData.cs`, `MetaProgressData.cs`). State machines serialize their current state + stack, and the Red Tape Engine serializes active injections (or guarantees a clean slate on load). `<Confirm the exact serializer/format in Persistence before changing save shape.>`
