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
        private readonly Action<string, float> _routeCue;
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
            _routeCue = RouteCue;
        }

        public OfficeAudioVoicePool VoicePool => _pool;
        public OfficeAudioCueCatalog Catalog => _catalog;
        public OfficeAudioSettings Settings => _settings;
        public OfficeAudioStateSnapshot Snapshot => _snapshot;
        public OfficeAudioMixState MixState => _mixState;
        public event Action<string, float> CueRouted;

        public void Apply(
            OfficeSimulationState state,
            OfficeCampaignState campaign,
            float deltaTime)
        {
            if (state == null) return;
            if (_snapshot == null || _snapshot.Tick != state.CurrentTick)
            {
                _snapshot = _projector.Project(state, campaign);
                _router.Route(_snapshot, state, _routeCue);
            }
            ApplyMix(_snapshot.MixState);
            ApplyMachineBed(_snapshot);
            _pool.UpdateVolumes(_settings, deltaTime);
        }

        public bool PlayCue(string cueId, float intensity = 1f)
        {
            return PlayCueWithPan(cueId, intensity, null);
        }

        public bool PlayPositionalCue(
            string cueId,
            Transform source,
            float intensity = 1f)
        {
            float? pan = source == null
                ? null
                : Mathf.Clamp(source.position.x / 12f, -0.75f, 0.75f);
            return PlayCueWithPan(cueId, intensity, pan);
        }

        public bool SetMachineLoop(
            int machineSlot,
            string machineId,
            string state,
            float intensity = 1f)
        {
            if (machineSlot < 0 || machineSlot >= 6) return false;
            return SetMachineCue(machineSlot,
                OfficeAudioEventRouter.CueForMachine(machineId, state), intensity);
        }

        private bool SetMachineCue(
            int machineSlot,
            string cueId,
            float intensity)
        {
            if (!_catalog.TryResolve(cueId, out OfficeAudioCueRecord cue,
                    out AudioClip clip))
            {
                _pool.SetContinuous(machineSlot + 2, null, "Ambience", 0f, 0f);
                return false;
            }
            _pool.SetContinuous(machineSlot + 2, clip, cue.bus,
                cue.base_volume * Mathf.Clamp01(intensity), cue.pan);
            return true;
        }

        private bool PlayCueWithPan(
            string cueId,
            float intensity,
            float? panOverride)
        {
            if (_catalog.ContainsCue(cueId))
                CueRouted?.Invoke(cueId, Mathf.Clamp01(intensity));
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
                panOverride ?? cue.pan,
                deterministicPitch);
        }

        private void RouteCue(string cueId, float intensity)
        {
            PlayCue(cueId, intensity);
        }

        public void NotifyInteractionAttempt(bool valid)
        {
            PlayCue(valid ? "action.interact" : "action.invalid");
        }

        public void ApplyMixForPresentation(
            OfficeAudioMixState state,
            float deltaTime)
        {
            ApplyMix(state);
            _pool.UpdateVolumes(_settings, deltaTime);
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

        private void ApplyMachineBed(OfficeAudioStateSnapshot snapshot)
        {
            SetMachineCue(0, snapshot.HasActiveCustomer
                ? "machine.front-desk-counter.active"
                : "machine.front-desk-counter.idle", 0.42f);
            SetMachineCue(1, snapshot.ManualTaskActive &&
                snapshot.ActiveManualKind == OfficeManualTaskKind.Compare
                    ? "machine.paper-check.active"
                    : "machine.paper-check.idle", 0.38f);
            SetMachineCue(2, snapshot.ManualTaskActive &&
                snapshot.ActiveManualKind == OfficeManualTaskKind.Trace
                    ? "machine.money-trace.active"
                    : "machine.money-trace.idle", 0.38f);
            SetMachineCue(3, snapshot.AutomationEnabled || snapshot.PayrollEnabled
                ? "machine.auto-sorter.active"
                : "machine.auto-sorter.idle", 0.44f);
            SetMachineCue(4, snapshot.CopyEchoActive
                ? "machine.copy-echo.active"
                : "machine.copy-echo.idle", 0.46f);
            SetMachineCue(5, snapshot.PromotionActive
                ? "machine.supervisor-stamp.active"
                : snapshot.GhostClockActive
                    ? "machine.ghost-clock.active"
                    : "machine.ghost-clock.idle", 0.4f);
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
