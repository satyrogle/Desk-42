using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeAudioCueCatalog
    {
        public const string ManifestResourcePath = "OfficeSliceM5/audio-manifest";

        private readonly Dictionary<string, OfficeAudioAssetRecord> _assets =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeAudioCueRecord> _cues =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> _clips =
            new(StringComparer.Ordinal);

        public OfficeAudioManifest Manifest { get; }
        public int AssetCount => _assets.Count;
        public int CueCount => _cues.Count;
        public int MissingClipCount { get; private set; }

        private OfficeAudioCueCatalog(OfficeAudioManifest manifest)
        {
            Manifest = manifest ?? new OfficeAudioManifest();
            for (int i = 0; i < Manifest.assets.Length; i++)
            {
                OfficeAudioAssetRecord asset = Manifest.assets[i];
                if (asset == null || string.IsNullOrWhiteSpace(asset.asset_id) ||
                    _assets.ContainsKey(asset.asset_id)) continue;
                _assets.Add(asset.asset_id, asset);
                AudioClip clip = Resources.Load<AudioClip>(asset.resource_path);
                if (clip == null)
                {
                    MissingClipCount++;
                    continue;
                }
                _clips.Add(asset.asset_id, clip);
            }
            for (int i = 0; i < Manifest.cues.Length; i++)
            {
                OfficeAudioCueRecord cue = Manifest.cues[i];
                if (cue == null || string.IsNullOrWhiteSpace(cue.cue_id) ||
                    _cues.ContainsKey(cue.cue_id)) continue;
                _cues.Add(cue.cue_id, cue);
            }
        }

        public static OfficeAudioCueCatalog Load()
        {
            TextAsset text = Resources.Load<TextAsset>(ManifestResourcePath);
            if (text == null) return new OfficeAudioCueCatalog(null);
            OfficeAudioManifest manifest = JsonUtility.FromJson<OfficeAudioManifest>(
                text.text);
            return new OfficeAudioCueCatalog(manifest);
        }

        public bool TryResolve(
            string cueId,
            out OfficeAudioCueRecord cue,
            out AudioClip clip)
        {
            cue = null;
            clip = null;
            if (string.IsNullOrWhiteSpace(cueId) ||
                !_cues.TryGetValue(cueId, out cue)) return false;
            return _clips.TryGetValue(cue.asset_id, out clip) && clip != null;
        }

        public bool ContainsCue(string cueId) =>
            !string.IsNullOrWhiteSpace(cueId) && _cues.ContainsKey(cueId);

        public bool ContainsAsset(string assetId) =>
            !string.IsNullOrWhiteSpace(assetId) && _assets.ContainsKey(assetId);
    }
}
