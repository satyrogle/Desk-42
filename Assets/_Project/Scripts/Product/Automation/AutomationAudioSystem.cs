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
            _clips.Add(AutomationFeedbackKind.PolicyChanged,
                CreateTone("Policy Bound", 0.18f, 430f, 0.30f, 0.03f));
        }

        internal void Play(AutomationFeedbackKind kind)
        {
            if (_effects == null || !_clips.TryGetValue(kind, out AudioClip clip)) return;
            float scale = kind == AutomationFeedbackKind.ClaimArrived ? 0.34f : 1f;
            _effects.PlayOneShot(clip, scale);
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
