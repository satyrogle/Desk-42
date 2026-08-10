using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeM6TextScale
    {
        Small,
        Standard,
        Large,
        Maximum,
    }

    public readonly struct OfficeM6Resolution
    {
        public OfficeM6Resolution(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
        public override string ToString() => Width + "x" + Height;
    }

    public interface IOfficeM6SettingsStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);
        void Save();
    }

    public sealed class OfficeM6MemorySettingsStore : IOfficeM6SettingsStore
    {
        private readonly Dictionary<string, int> _values = new();
        private readonly Dictionary<string, float> _floatValues = new();

        public int GetInt(string key, int fallback) =>
            _values.TryGetValue(key, out int value) ? value : fallback;
        public void SetInt(string key, int value) => _values[key] = value;
        public float GetFloat(string key, float fallback) =>
            _floatValues.TryGetValue(key, out float value) ? value : fallback;
        public void SetFloat(string key, float value) =>
            _floatValues[key] = value;
        public void Save() { }
    }

    internal sealed class OfficeM6PlayerPrefsSettingsStore :
        IOfficeM6SettingsStore
    {
        public int GetInt(string key, int fallback) =>
            PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) =>
            PlayerPrefs.SetInt(key, value);
        public float GetFloat(string key, float fallback) =>
            PlayerPrefs.GetFloat(key, fallback);
        public void SetFloat(string key, float value) =>
            PlayerPrefs.SetFloat(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>Presentation preferences; absent from campaign saves/checksums.</summary>
    public sealed class OfficeM6PresentationSettings
    {
        private const string Prefix = "desk42.office-slice.m6.presentation.";
        private static readonly OfficeM6Resolution[] ResolutionValues =
        {
            new(1280, 720),
            new(1600, 900),
            new(1920, 1080),
        };

        private readonly IOfficeM6SettingsStore _store;

        public OfficeM6PresentationSettings(
            OfficeAudioSettings audio,
            IOfficeM6SettingsStore store = null)
        {
            Audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _store = store ?? new OfficeM6PlayerPrefsSettingsStore();
        }

        public OfficeAudioSettings Audio { get; }
        public bool TutorialHints { get; private set; } = true;
        public OfficeM6TextScale TextScale { get; private set; } =
            OfficeM6TextScale.Standard;
        public bool Fullscreen { get; private set; } = true;
        public int ResolutionIndex { get; private set; } = 1;
        public OfficeM6Resolution Resolution =>
            ResolutionValues[ResolutionIndex];
        public float TextScaleMultiplier => TextScale switch
        {
            OfficeM6TextScale.Small => 0.9f,
            OfficeM6TextScale.Large => 1.15f,
            OfficeM6TextScale.Maximum => 1.3f,
            _ => 1f,
        };

        public static int ResolutionCount => ResolutionValues.Length;

        public void Load()
        {
            Audio.SetVolumes(
                _store.GetFloat(Prefix + "master", Audio.Master),
                _store.GetFloat(Prefix + "music", Audio.Music),
                _store.GetFloat(Prefix + "sfx", Audio.Sfx),
                _store.GetFloat(Prefix + "ambience", Audio.Ambience));
            Audio.SetRumble(_store.GetInt(Prefix + "rumble", 1) != 0);
            Audio.SetReducedFlash(
                _store.GetInt(Prefix + "reduced-flash", 0) != 0);
            TutorialHints = _store.GetInt(Prefix + "tutorial-hints", 1) != 0;
            TextScale = (OfficeM6TextScale)Mathf.Clamp(
                _store.GetInt(Prefix + "text-scale",
                    (int)OfficeM6TextScale.Standard),
                0, (int)OfficeM6TextScale.Maximum);
            Fullscreen = _store.GetInt(Prefix + "fullscreen", 1) != 0;
            ResolutionIndex = Mathf.Clamp(
                _store.GetInt(Prefix + "resolution", 1),
                0, ResolutionValues.Length - 1);
        }

        public void Save()
        {
            _store.SetFloat(Prefix + "master", Audio.Master);
            _store.SetFloat(Prefix + "music", Audio.Music);
            _store.SetFloat(Prefix + "sfx", Audio.Sfx);
            _store.SetFloat(Prefix + "ambience", Audio.Ambience);
            _store.SetInt(Prefix + "rumble", Audio.Rumble ? 1 : 0);
            _store.SetInt(Prefix + "reduced-flash",
                Audio.ReducedFlash ? 1 : 0);
            _store.SetInt(Prefix + "tutorial-hints", TutorialHints ? 1 : 0);
            _store.SetInt(Prefix + "text-scale", (int)TextScale);
            _store.SetInt(Prefix + "fullscreen", Fullscreen ? 1 : 0);
            _store.SetInt(Prefix + "resolution", ResolutionIndex);
            _store.Save();
        }

        public void SetTutorialHints(bool enabled) => TutorialHints = enabled;
        public void SetTextScale(OfficeM6TextScale scale) =>
            TextScale = (OfficeM6TextScale)Mathf.Clamp(
                (int)scale, 0, (int)OfficeM6TextScale.Maximum);
        public void SetFullscreen(bool enabled) => Fullscreen = enabled;
        public void SetResolutionIndex(int index) => ResolutionIndex =
            Mathf.Clamp(index, 0, ResolutionValues.Length - 1);

        public void AdjustTextScale(int delta) => SetTextScale(
            (OfficeM6TextScale)((int)TextScale + Math.Sign(delta)));
        public void AdjustResolution(int delta) => SetResolutionIndex(
            ResolutionIndex + Math.Sign(delta));

        public void ApplyDisplay()
        {
            Screen.SetResolution(
                Resolution.Width, Resolution.Height, Fullscreen);
        }
    }
}
