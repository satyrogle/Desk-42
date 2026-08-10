using System;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeAudioDirector
    {
        private readonly OfficeAudioCueCatalog _catalog;
        private readonly OfficeAudioSettings _settings;
        private readonly OfficeAudioVoicePool _pool;
        private readonly OfficeAudioStateProjector _projector = new();
        private readonly OfficeAudioEventRouter _router = new();
        private OfficeAudioStateSnapshot _snapshot;
        private OfficeAudioMixState _mixState = (OfficeAudioMixState)(-1);
        private int _ambienceSlot;

        public OfficeAudioDirector(
            Transform parent,
            OfficeAudioCueCatalog catalog,
            OfficeAudioSettings settings)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _pool = new OfficeAudioVoicePool(parent);
        }

        public OfficeAudioVoicePool VoicePool => _pool;
        public OfficeAudioCueCatalog Catalog => _catalog;
        public OfficeAudioSettings Settings => _settings;
        public OfficeAudioStateSnapshot Snapshot => _snapshot;
        public OfficeAudioMixState MixState => _mixState;

        public void Apply(
            OfficeSimulationState state,
            OfficeCampaignState campaign,
            float deltaTime)
        {
            if (state == null) return;
            if (_snapshot == null || _snapshot.Tick != state.CurrentTick ||
                _snapshot.SimulationChecksum != state.Checksum)
            {
                _snapshot = _projector.Project(state, campaign);
                _router.Route(_snapshot, state, (cueId, intensity) =>
                {
                    PlayCue(cueId, intensity);
                });
            }
            ApplyMix(_snapshot.MixState);
            _pool.UpdateVolumes(_settings, deltaTime);
        }

        public bool PlayCue(string cueId, float intensity = 1f)
        {
            if (_settings.Muted ||
                !_catalog.TryResolve(cueId, out OfficeAudioCueRecord cue,
                    out AudioClip clip)) return false;
            float deterministicPitch = 1f;
            if (cueId == "warden.step.b") deterministicPitch = 1.08f;
            if (cueId == "automation.copied-accepted") deterministicPitch = 0.94f;
            return _pool.PlayOneShot(
                clip,
                cue.base_volume * Mathf.Clamp01(intensity) *
                    _settings.BusGain(cue.bus),
                cue.pan,
                deterministicPitch);
        }

        public void NotifyInteractionAttempt(bool valid)
        {
            PlayCue(valid ? "action.interact" : "action.invalid");
        }

        public void ResetForState(
            OfficeSimulationState state,
            OfficeCampaignState campaign)
        {
            _pool.StopTransientAudio();
            _snapshot = state == null ? null : _projector.Project(state, campaign);
            _router.Reset(_snapshot);
            _mixState = (OfficeAudioMixState)(-1);
        }

        public void Dispose() => _pool.Dispose();

        private void ApplyMix(OfficeAudioMixState state)
        {
            if (state == _mixState) return;
            int previousSlot = _ambienceSlot;
            _ambienceSlot = 1 - _ambienceSlot;
            ResolveLoop("ambience." + state.ToString().ToLowerInvariant(),
                out AudioClip ambience, out OfficeAudioCueRecord ambienceCue);
            _pool.SetContinuous(previousSlot, null, "Ambience", 0f, 0f);
            _pool.SetContinuous(_ambienceSlot, ambience, "Ambience",
                ambienceCue?.base_volume ?? 0f, 0f);

            SetMusic(0, "music.work",
                state == OfficeAudioMixState.Calm ||
                state == OfficeAudioMixState.Recovery ||
                state == OfficeAudioMixState.Result ? 1f : 0.55f);
            SetMusic(1, "music.pressure",
                state == OfficeAudioMixState.Rush ? 1f :
                state == OfficeAudioMixState.Break ? 0.52f : 0f);
            SetMusic(2, "music.break",
                state == OfficeAudioMixState.Break ? 1f : 0f);
            _mixState = state;
        }

        private void SetMusic(int slot, string cueId, float gain)
        {
            ResolveLoop(cueId, out AudioClip clip, out OfficeAudioCueRecord cue);
            _pool.SetMusic(slot, clip, (cue?.base_volume ?? 0f) * gain);
        }

        private void ResolveLoop(
            string cueId,
            out AudioClip clip,
            out OfficeAudioCueRecord cue)
        {
            if (!_catalog.TryResolve(cueId, out cue, out clip))
            {
                cue = null;
                clip = null;
            }
        }
    }
}
