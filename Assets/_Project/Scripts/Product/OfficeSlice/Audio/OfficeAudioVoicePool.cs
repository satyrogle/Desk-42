using System;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeAudioVoicePool
    {
        public const int OneShotCapacity = 32;
        public const int ContinuousCapacity = 8;
        public const int MusicCapacity = 4;
        public const string RootName = "Office Slice M5 Audio Root";

        private readonly Transform _root;
        private readonly AudioSource[] _oneShots = new AudioSource[OneShotCapacity];
        private readonly float[] _oneShotReleaseTimes = new float[OneShotCapacity];
        private readonly AudioSource[] _continuous = new AudioSource[ContinuousCapacity];
        private readonly float[] _continuousTargets = new float[ContinuousCapacity];
        private readonly string[] _continuousBuses = new string[ContinuousCapacity];
        private readonly AudioSource[] _music = new AudioSource[MusicCapacity];
        private readonly float[] _musicTargets = new float[MusicCapacity];
        private int _nextOneShot;

        public int TotalSourceCount => OneShotCapacity + ContinuousCapacity + MusicCapacity;
        public int PeakOneShotVoices { get; private set; }
        public int TotalOneShotRequests { get; private set; }
        public int GrowthCount => 0;

        public OfficeAudioVoicePool(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            DisableExistingRoots(parent);
            _root = new GameObject(RootName).transform;
            _root.SetParent(parent, false);
            for (int i = 0; i < _oneShots.Length; i++)
                _oneShots[i] = CreateSource("SFX Voice " + i.ToString("D2"), false);
            for (int i = 0; i < _continuous.Length; i++)
                _continuous[i] = CreateSource("Continuous Voice " + i.ToString("D2"), true);
            for (int i = 0; i < _music.Length; i++)
                _music[i] = CreateSource("Music Voice " + i.ToString("D2"), true);
        }

        public Transform Root => _root;

        public int ActiveOneShotCount
        {
            get
            {
                float now = Time.realtimeSinceStartup;
                int count = 0;
                for (int i = 0; i < _oneShotReleaseTimes.Length; i++)
                    if (_oneShotReleaseTimes[i] > now) count++;
                return count;
            }
        }

        public int ActiveContinuousCount => CountActive(_continuous);
        public int ActiveMusicCount => CountActive(_music);
        public int ActiveSourceCount =>
            ActiveOneShotCount + ActiveContinuousCount + ActiveMusicCount;

        public float ContinuousTargetVolume(int slot) =>
            slot >= 0 && slot < _continuousTargets.Length
                ? _continuousTargets[slot] : 0f;

        public float MusicTargetVolume(int slot) =>
            slot >= 0 && slot < _musicTargets.Length ? _musicTargets[slot] : 0f;

        public bool PlayOneShot(
            AudioClip clip,
            float volume,
            float pan,
            float pitch = 1f)
        {
            if (clip == null || volume <= 0.0001f) return false;
            float now = Time.realtimeSinceStartup;
            int index = FindAvailableOneShot(now);
            AudioSource source = _oneShots[index];
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.panStereo = Mathf.Clamp(pan, -0.75f, 0.75f);
            source.pitch = Mathf.Clamp(pitch, 0.65f, 1.45f);
            source.Play();
            _oneShotReleaseTimes[index] = now + clip.length /
                Mathf.Max(0.01f, Mathf.Abs(source.pitch));
            TotalOneShotRequests++;
            PeakOneShotVoices = Mathf.Max(PeakOneShotVoices, ActiveOneShotCount);
            return true;
        }

        public void SetContinuous(
            int slot,
            AudioClip clip,
            string bus,
            float targetVolume,
            float pan)
        {
            if (slot < 0 || slot >= _continuous.Length) return;
            AudioSource source = _continuous[slot];
            if (source.clip != clip)
            {
                source.Stop();
                source.clip = clip;
                source.panStereo = Mathf.Clamp(pan, -0.75f, 0.75f);
                if (clip != null) source.Play();
            }
            _continuousBuses[slot] = bus ?? "Ambience";
            _continuousTargets[slot] = Mathf.Clamp01(targetVolume);
        }

        public void SetMusic(int slot, AudioClip clip, float targetVolume)
        {
            if (slot < 0 || slot >= _music.Length) return;
            AudioSource source = _music[slot];
            if (source.clip != clip)
            {
                source.Stop();
                source.clip = clip;
                if (clip != null) source.Play();
            }
            _musicTargets[slot] = Mathf.Clamp01(targetVolume);
        }

        public void UpdateVolumes(OfficeAudioSettings settings, float deltaTime)
        {
            float step = Mathf.Max(0.02f, deltaTime * 1.8f);
            for (int i = 0; i < _continuous.Length; i++)
            {
                float target = _continuousTargets[i] *
                    settings.BusGain(_continuousBuses[i]);
                _continuous[i].volume = Mathf.MoveTowards(
                    _continuous[i].volume, target, step);
            }
            for (int i = 0; i < _music.Length; i++)
            {
                float target = _musicTargets[i] * settings.BusGain("Music");
                _music[i].volume = Mathf.MoveTowards(
                    _music[i].volume, target, step);
            }
        }

        public void StopTransientAudio()
        {
            for (int i = 0; i < _oneShots.Length; i++)
            {
                _oneShots[i].Stop();
                _oneShotReleaseTimes[i] = 0f;
            }
        }

        public void StopAll()
        {
            StopTransientAudio();
            StopSources(_continuous);
            StopSources(_music);
        }

        public void Dispose()
        {
            StopAll();
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public static int ActiveRootCount()
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsOfType<Transform>();
            int count = 0;
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i].name == RootName &&
                    transforms[i].gameObject.activeInHierarchy) count++;
            return count;
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(_root, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            return source;
        }

        private int FindAvailableOneShot(float now)
        {
            for (int offset = 0; offset < _oneShots.Length; offset++)
            {
                int index = (_nextOneShot + offset) % _oneShots.Length;
                if (_oneShotReleaseTimes[index] > now) continue;
                _nextOneShot = (index + 1) % _oneShots.Length;
                return index;
            }
            int fallback = _nextOneShot;
            _nextOneShot = (_nextOneShot + 1) % _oneShots.Length;
            return fallback;
        }

        private static void DisableExistingRoots(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == RootName) child.gameObject.SetActive(false);
            }
        }

        private static int CountActive(AudioSource[] sources)
        {
            int count = 0;
            for (int i = 0; i < sources.Length; i++)
                if (sources[i].clip != null && sources[i].isPlaying &&
                    sources[i].volume > 0.0001f) count++;
            return count;
        }

        private static void StopSources(AudioSource[] sources)
        {
            for (int i = 0; i < sources.Length; i++) sources[i].Stop();
        }
    }
}
