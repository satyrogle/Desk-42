using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Product.Automation
{
    [DisallowMultipleComponent]
    internal sealed class AutomationAudioSystem : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private readonly Dictionary<AutomationFeedbackKind, AudioClip> _clips = new();
        private AudioSource _hum;
        private AudioSource _ventilation;
        private AudioSource _machineRhythm;
        private AudioSource _pressure;
        private AudioSource _effects;

        private void Awake()
        {
            _hum = gameObject.AddComponent<AudioSource>();
            _hum.playOnAwake = false;
            _hum.loop = true;
            _hum.spatialBlend = 0f;
            _hum.volume = 0.035f;
            _hum.clip = CreateHum();
            _hum.Play();

            _ventilation = CreateLoopSource(
                "Ventilation Layer", CreateVentilation(), 0.025f);
            _machineRhythm = CreateLoopSource(
                "Machine Rhythm Layer", CreateMachineRhythm(), 0.015f);
            _pressure = CreateLoopSource(
                "Queue And Legal Pressure Layer", CreatePressurePulse(), 0f);

            _effects = gameObject.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _effects.spatialBlend = 0f;
            _effects.volume = 0.34f;
            _clips.Add(AutomationFeedbackKind.ClaimArrived,
                CreateTone("Paper Feed", 0.09f, 210f, 0.18f, 0.20f));
            _clips.Add(AutomationFeedbackKind.EvidenceSplit,
                CreateTone("Scanner", 0.16f, 510f, 0.38f, 0.05f));
            _clips.Add(AutomationFeedbackKind.RulingStamped,
                CreateTone("Ruling Stamp", 0.13f, 92f, 0.75f, 0.32f));
            _clips.Add(AutomationFeedbackKind.AppealReturned,
                CreateTone("Appeal Alarm", 0.30f, 330f, 0.20f, 0.02f, true));
            _clips.Add(AutomationFeedbackKind.AppealResolved,
                CreateTone("Appeal Resolved", 0.22f, 620f, 0.36f, 0.02f));
            _clips.Add(AutomationFeedbackKind.Jammed,
                CreateTone("Queue Jam", 0.36f, 118f, 0.25f, 0.08f, true));
            _clips.Add(AutomationFeedbackKind.Repaired,
                CreateTone("Machine Repaired", 0.20f, 560f, 0.32f, 0.03f));
            _clips.Add(AutomationFeedbackKind.Misclassified,
                CreateTone("Classification Fault", 0.28f, 245f, 0.24f, 0.08f, true));
            _clips.Add(AutomationFeedbackKind.DeadlineMissed,
                CreateTone("Deadline Missed", 0.34f, 180f, 0.20f, 0.03f, true));
            _clips.Add(AutomationFeedbackKind.UpgradeInstalled,
                CreateTone("Upgrade Installed", 0.24f, 720f, 0.35f, 0.02f));
            _clips.Add(AutomationFeedbackKind.PriorityChanged,
                CreateTone("Priority Changed", 0.14f, 470f, 0.30f, 0.02f));
            _clips.Add(AutomationFeedbackKind.AppealModeChanged,
                CreateTone("Appeal Mode Changed", 0.18f, 390f, 0.30f, 0.03f));
            _clips.Add(AutomationFeedbackKind.ProcedureBound,
                CreateTone("Procedure Bound", 0.26f, 650f, 0.34f, 0.02f, true));
            _clips.Add(AutomationFeedbackKind.PolicyChanged,
                CreateTone("Policy Bound", 0.18f, 430f, 0.30f, 0.03f));
            _clips.Add(AutomationFeedbackKind.ProcedureDrafted,
                CreateTone("Procedure Draft", 0.24f, 540f, 0.28f, 0.03f, true));
            _clips.Add(AutomationFeedbackKind.ProcedureUpgraded,
                CreateTone("Procedure Upgrade", 0.26f, 690f, 0.34f, 0.02f, true));
            _clips.Add(AutomationFeedbackKind.HoldingCreated,
                CreateTone("Holding Entered", 0.33f, 410f, 0.40f, 0.025f, true));
            _clips.Add(AutomationFeedbackKind.PrecedentCited,
                CreateTone("Precedent Citation", 0.18f, 780f, 0.34f, 0.01f));
            _clips.Add(AutomationFeedbackKind.ShiftClosed,
                CreateTone("Shift Bell", 0.42f, 260f, 0.24f, 0.01f, true));
            _clips.Add(AutomationFeedbackKind.BranchReviewed,
                CreateTone("Branch Review", 0.52f, 350f, 0.20f, 0.02f, true));
            _clips.Add(AutomationFeedbackKind.ReliefGranted,
                CreateTone("Relief Release", 0.30f, 610f, 0.42f, 0.02f, true));
            _clips.Add(AutomationFeedbackKind.RetrospectiveReview,
                CreateTone("Retrospective Return", 0.31f, 205f, 0.25f, 0.06f, true));
            _clips.Add(AutomationFeedbackKind.RunSaved,
                CreateTone("Run Saved", 0.12f, 880f, 0.40f, 0.01f));
            _clips.Add(AutomationFeedbackKind.RunLoaded,
                CreateTone("Run Loaded", 0.18f, 490f, 0.32f, 0.02f));
        }

        internal void Play(AutomationFeedbackKind kind)
        {
            if (_effects == null || !_clips.TryGetValue(kind, out AudioClip clip)) return;
            float scale = kind == AutomationFeedbackKind.ClaimArrived ? 0.34f : 1f;
            _effects.PlayOneShot(clip, scale);
        }

        /// <summary>
        /// Runtime parameter surface for the future FMOD implementation. The current
        /// vertical slice drives equivalent deterministic Unity layers so operational
        /// causality can be heard without adding an unavailable middleware package.
        /// </summary>
        internal void SetOperationalState(
            int backlog,
            float machineHeat,
            int appealPressure,
            int shiftOrdinal)
        {
            float queue = Mathf.Clamp01(backlog / 14f);
            float heat = Mathf.Clamp01(machineHeat / 100f);
            float legal = Mathf.Clamp01(appealPressure / 6f);
            float progression = Mathf.Clamp01((shiftOrdinal - 1) / 7f);
            if (_machineRhythm != null)
            {
                _machineRhythm.volume = Mathf.Lerp(0.012f, 0.075f, queue);
                _machineRhythm.pitch = Mathf.Lerp(0.86f, 1.22f,
                    Mathf.Max(queue, heat));
            }
            if (_pressure != null)
            {
                _pressure.volume = Mathf.Lerp(0f, 0.085f,
                    Mathf.Max(legal, queue * 0.62f));
                _pressure.pitch = Mathf.Lerp(0.82f, 1.16f, progression);
            }
            if (_ventilation != null)
                _ventilation.volume = Mathf.Lerp(0.022f, 0.043f, heat);
            if (_hum != null)
                _hum.pitch = Mathf.Lerp(0.97f, 1.035f, heat);
        }

        private AudioSource CreateLoopSource(
            string sourceName,
            AudioClip clip,
            float volume)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.name = sourceName;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.clip = clip;
            source.Play();
            return source;
        }

        private static AudioClip CreateHum()
        {
            int samples = SampleRate * 2;
            var data = new float[samples];
            for (int index = 0; index < samples; index++)
            {
                float time = (float)index / SampleRate;
                data[index] = Mathf.Sin(time * Mathf.PI * 2f * 50f) * 0.32f +
                    Mathf.Sin(time * Mathf.PI * 2f * 100f) * 0.10f;
            }
            AudioClip clip = AudioClip.Create(
                "Branch 42 Fluorescent Hum", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateVentilation()
        {
            int samples = SampleRate * 3;
            var data = new float[samples];
            for (int index = 0; index < samples; index++)
            {
                float slow = Mathf.Sin(index * 0.0061f) * 0.16f;
                float deterministicAir = Mathf.Sin(index * 0.731f) *
                    Mathf.Sin(index * 0.017f) * 0.11f;
                data[index] = slow + deterministicAir;
            }
            AudioClip clip = AudioClip.Create(
                "Branch 42 Ventilation", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateMachineRhythm()
        {
            int samples = SampleRate * 2;
            var data = new float[samples];
            for (int index = 0; index < samples; index++)
            {
                float phase = (float)index / SampleRate;
                float pulse = Mathf.Repeat(phase * 2.5f, 1f) < 0.055f ? 0.48f : 0f;
                data[index] = pulse * Mathf.Sin(phase * Mathf.PI * 2f * 96f);
            }
            AudioClip clip = AudioClip.Create(
                "Branch 42 Machine Rhythm", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreatePressurePulse()
        {
            int samples = SampleRate * 4;
            var data = new float[samples];
            for (int index = 0; index < samples; index++)
            {
                float phase = (float)index / SampleRate;
                float envelope = Mathf.Pow(
                    Mathf.Max(0f, 1f - Mathf.Repeat(phase, 1f) * 5f), 3f);
                data[index] = Mathf.Sin(phase * Mathf.PI * 2f * 142f) *
                    envelope * 0.36f;
            }
            AudioClip clip = AudioClip.Create(
                "Branch 42 Pressure Pulse", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateTone(
            string name,
            float duration,
            float frequency,
            float decay,
            float noise,
            bool alternate = false)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[samples];
            for (int index = 0; index < samples; index++)
            {
                float time = (float)index / SampleRate;
                float progress = (float)index / samples;
                float envelope = Mathf.Pow(1f - progress, Mathf.Max(0.05f, decay));
                float activeFrequency = alternate && progress > 0.5f
                    ? frequency * 1.36f
                    : frequency;
                float tone = Mathf.Sin(time * Mathf.PI * 2f * activeFrequency);
                float deterministicNoise = Mathf.Sin(
                    index * 12.9898f + index * index * 0.0017f);
                data[index] = (tone * (1f - noise) + deterministicNoise * noise) *
                    envelope * 0.72f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
