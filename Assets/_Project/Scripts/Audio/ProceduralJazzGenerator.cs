#pragma warning disable 0414
// ============================================================
// DESK 42 — Procedural Jazz Generator (MonoBehaviour)
//
// Generative ambient jazz that reacts to the player's mental
// state. Runs a beat clock, picks a chord every two bars, and
// voices a melody note on each beat. As sanity drops, the
// harmony degrades from clean ii-V-I → tritone substitutions
// → bitonal smears → free-jazz clusters.
//
// Output:
//   DESK42_FMOD defined  → notes dispatched as parameter changes
//                          on a sustaining FMOD event
//                          (event:/Music/Jazz with parameters
//                          MidiNote, Velocity, ChordRoot, ChordQuality).
//   Otherwise            → notes logged in Editor only.
//
// The music-theory engine runs unconditionally so the
// architecture can be QA'd without the FMOD plugin imported.
//
// All randomness routed through SeedEngine (SeedStream.AudioVariation)
// so seeded replays produce identical music.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Audio
{
    [DisallowMultipleComponent]
    public sealed class ProceduralJazzGenerator : MonoBehaviour
    {
        [Header("FMOD Event")]
        [SerializeField] private string _eventPath = "event:/Music/Jazz";

        [Header("Tempo")]
        [SerializeField] private float _baseBpm     = 92f;
        [SerializeField] private float _maxBpm      = 168f; // panic tempo

        [Header("Voicing")]
        [SerializeField] private int _baseRootMidi = 48; // C3

        // ── State ─────────────────────────────────────────────

        private float _currentSanity = 100f;
        private float _beatTimer;
        private int   _beatIndex;       // 0..N within the current bar
        private int   _barIndex;        // global bar count
        private ChordSlot _currentChord;
        private Mode _currentMode = Mode.Clean;

        // Don't draw from SeedEngine until a run is active —
        // SeedEngine throws in dev builds before Init.
        private bool _runActive;

#if DESK42_FMOD
        private FMOD.Studio.EventInstance _instance;
#endif

        // ── Lifecycle ─────────────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnSanityChanged  += HandleSanityChanged;
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;

#if DESK42_FMOD
            _instance = FMODUnity.RuntimeManager.CreateInstance(_eventPath);
            _instance.start();
#endif
        }

        private void OnDisable()
        {
            RumorMill.OnSanityChanged  -= HandleSanityChanged;
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;

#if DESK42_FMOD
            _instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _instance.release();
#endif
        }

        private void Update()
        {
            if (!_runActive) return;

            float bpm = Mathf.Lerp(_baseBpm, _maxBpm,
                Mathf.InverseLerp(100f, 0f, _currentSanity));
            float beatInterval = 60f / bpm;

            _beatTimer += Time.deltaTime;
            while (_beatTimer >= beatInterval)
            {
                _beatTimer -= beatInterval;
                AdvanceBeat();
            }
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            _currentSanity = e.Current;
            _currentMode   = ModeForSanity(e.Current);
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart)
            {
                _currentSanity = 100f;
                _currentMode   = Mode.Clean;
                _beatIndex     = 0;
                _barIndex      = 0;
                _runActive     = true;
                ChooseChord();
            }
            else
            {
                _runActive = false;
            }
        }

        // ── Beat Engine ───────────────────────────────────────

        private void AdvanceBeat()
        {
            _beatIndex++;
            if (_beatIndex >= 4)
            {
                _beatIndex = 0;
                _barIndex++;
                // Re-voice every two bars
                if ((_barIndex & 1) == 0) ChooseChord();
            }

            int note = PickMelodyNote(_beatIndex);
            int velocity = _beatIndex == 0 ? 100 : 70 + (_beatIndex * 5);

            EmitNote(note, velocity);
        }

        // ── Harmony ───────────────────────────────────────────

        private enum Mode { Clean, Substituted, Bitonal, Free }

        private struct ChordSlot
        {
            public int Root;        // MIDI offset from base root (0–11)
            public ChordQuality Quality;
        }

        private enum ChordQuality { Maj7, Min7, Dom7, HalfDim, Dim, Aug, Cluster }

        private static Mode ModeForSanity(float sanity)
        {
            if (sanity >= 80f) return Mode.Clean;
            if (sanity >= 50f) return Mode.Substituted;
            if (sanity >= 20f) return Mode.Bitonal;
            return Mode.Free;
        }

        private void ChooseChord()
        {
            // ii–V–I as the spine; roll the position within it.
            int[] iiVIRoots   = { 2, 7, 0 }; // semitones from key
            ChordQuality[] iiVIQ = { ChordQuality.Min7, ChordQuality.Dom7, ChordQuality.Maj7 };

            int idx = _barIndex % 3;
            var slot = new ChordSlot
            {
                Root = iiVIRoots[idx],
                Quality = iiVIQ[idx],
            };

            // Sanity-driven mutation
            switch (_currentMode)
            {
                case Mode.Substituted:
                    // ~30% chance of tritone sub on the V chord
                    if (idx == 1 && SeedEngine.NextFloat(SeedStream.AudioVariation) < 0.3f)
                        slot.Root = (slot.Root + 6) % 12;
                    break;
                case Mode.Bitonal:
                    // shift root by an unrelated step
                    slot.Root = (slot.Root + SeedEngine.Next(SeedStream.AudioVariation, 1, 5)) % 12;
                    if (SeedEngine.NextFloat(SeedStream.AudioVariation) < 0.4f)
                        slot.Quality = ChordQuality.HalfDim;
                    break;
                case Mode.Free:
                    slot.Root = SeedEngine.Next(SeedStream.AudioVariation, 0, 12);
                    slot.Quality = ChordQuality.Cluster;
                    break;
            }

            _currentChord = slot;

#if DESK42_FMOD
            _instance.setParameterByName("ChordRoot",    _currentChord.Root);
            _instance.setParameterByName("ChordQuality", (int)_currentChord.Quality);
#endif
        }

        private int PickMelodyNote(int beat)
        {
            // Major scale degrees relative to chord root
            int[] cleanScale = { 0, 2, 4, 5, 7, 9, 11 };

            int degree;
            switch (_currentMode)
            {
                case Mode.Clean:
                    degree = cleanScale[SeedEngine.Next(SeedStream.AudioVariation, 0, cleanScale.Length)];
                    break;
                case Mode.Substituted:
                    // bias toward chord tones, occasional approach note
                    int[] substScale = { 0, 3, 4, 7, 10, 11 };
                    degree = substScale[SeedEngine.Next(SeedStream.AudioVariation, 0, substScale.Length)];
                    break;
                case Mode.Bitonal:
                    int[] bitonalScale = { 0, 1, 4, 6, 7, 10 };
                    degree = bitonalScale[SeedEngine.Next(SeedStream.AudioVariation, 0, bitonalScale.Length)];
                    break;
                default:
                    // free / cluster — anything in the chromatic
                    degree = SeedEngine.Next(SeedStream.AudioVariation, 0, 12);
                    break;
            }

            // Octave: melody walks an octave above the chord root
            int octave = 12 + SeedEngine.Next(SeedStream.AudioVariation, 0, 2) * 12;
            return _baseRootMidi + _currentChord.Root + degree + octave;
        }

        // ── Output ────────────────────────────────────────────

        private void EmitNote(int midi, int velocity)
        {
#if DESK42_FMOD
            _instance.setParameterByName("MidiNote", midi);
            _instance.setParameterByName("Velocity", velocity);
            _instance.setParameterByName("BeatTrigger", _beatIndex == 0 ? 1f : 0f);
#endif

#if UNITY_EDITOR
            // Verbose; only enable when tuning the generator.
            // Debug.Log($"[Jazz] beat={_beatIndex} note={midi} vel={velocity} mode={_currentMode}");
#endif
        }
    }
}
