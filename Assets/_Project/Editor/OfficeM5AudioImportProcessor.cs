using UnityEditor;
using UnityEngine;

namespace Desk42.Editor
{
    public sealed class OfficeM5AudioImportProcessor : AssetPostprocessor
    {
        private const string Root = "Assets/_Project/Audio/OfficeSliceM5/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            bool longForm = assetPath.Contains("/Ambience/") ||
                assetPath.Contains("/Music/");
            bool machineLoop = assetPath.Contains("/Machine/") &&
                (assetPath.Contains("_idle.") || assetPath.Contains("_active."));
            importer.forceToMono = !longForm;
            importer.loadInBackground = longForm;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.preloadAudioData = !longForm;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.loadType = longForm
                ? AudioClipLoadType.Streaming
                : machineLoop
                    ? AudioClipLoadType.CompressedInMemory
                    : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = longForm || machineLoop
                ? AudioCompressionFormat.Vorbis
                : AudioCompressionFormat.ADPCM;
            settings.quality = longForm ? 0.72f : 0.84f;
            importer.defaultSampleSettings = settings;
        }
    }
}
