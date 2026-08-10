#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Desk42.Product.OfficeSlice;
using UnityEditor;
using UnityEngine;

namespace Desk42.EditorTools
{
    public static class OfficeSliceM4AssetBuilder
    {
        private const string ManifestPath =
            "Assets/_Project/Art/OfficeSliceM4/Config/runtime-asset-manifest.json";
        private const string PalettePath =
            "Assets/_Project/Art/OfficeSliceM4/Config/OfficeSliceM4Palette.asset";
        private const string CatalogPath =
            "Assets/_Project/Art/OfficeSliceM4/Resources/OfficeSliceM4/Config/OfficeSpriteCatalog.asset";

        [Serializable]
        private sealed class Manifest
        {
            public ManifestEntry[] assets;
        }

        [Serializable]
        private sealed class ManifestEntry
        {
            public string asset_id;
            public string runtime_filename;
        }

        [MenuItem("Desk42/Office Slice M4/Rebuild Visual Assets")]
        public static void Build()
        {
            if (!File.Exists(ManifestPath))
                throw new FileNotFoundException("M4 runtime manifest is missing.", ManifestPath);
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest?.assets == null || manifest.assets.Length == 0)
                throw new InvalidDataException("M4 runtime manifest has no assets.");

            for (int i = 0; i < manifest.assets.Length; i++)
                ConfigureTexture(manifest.assets[i].runtime_filename);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            OfficeVisualTheme theme = LoadOrCreate<OfficeVisualTheme>(PalettePath);
            theme.EditorSetColours(BuildPalette());
            EditorUtility.SetDirty(theme);

            var entries = new List<OfficeSpriteCatalog.Entry>(manifest.assets.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.assets.Length; i++)
            {
                ManifestEntry source = manifest.assets[i];
                if (!ids.Add(source.asset_id))
                    throw new InvalidDataException("Duplicate M4 visual ID: " + source.asset_id);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(source.runtime_filename);
                if (sprite == null)
                    throw new MissingReferenceException(
                        "M4 sprite import failed for " + source.runtime_filename);
                entries.Add(new OfficeSpriteCatalog.Entry(
                    source.asset_id,
                    sprite,
                    source.asset_id.StartsWith("character.", StringComparison.Ordinal)
                        ? new Vector2(0.5f, 0f)
                        : new Vector2(0.5f, 0.5f)));
            }
            entries.Sort((left, right) =>
                string.CompareOrdinal(left.Id, right.Id));

            OfficeSpriteCatalog catalog = LoadOrCreate<OfficeSpriteCatalog>(CatalogPath);
            catalog.EditorConfigure(theme, entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("OFFICE_M4_ASSET_BUILD_OK assets=" + entries.Count);
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureTexture(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                throw new InvalidDataException("M4 asset is not a texture: " + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
                EnsureFolders(directory);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolders(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static List<OfficeVisualTheme.SemanticColour> BuildPalette()
        {
            return new List<OfficeVisualTheme.SemanticColour>
            {
                Colour("cream-paper", "#E8D9B5"),
                Colour("warm-plaster", "#C7BFA7"),
                Colour("moss-furniture", "#66705B"),
                Colour("machine-teal", "#2F6B67"),
                Colour("coffee-wood", "#6C4E3D"),
                Colour("calm-mint", "#B8D6B0"),
                Colour("warning-amber", "#D8892B"),
                Colour("break-red", "#B53B38"),
                Colour("ink", "#15151A"),
                Colour("ghost-cyan", "#49C6C8"),
                Colour("impossible-violet", "#7B4A88"),
            };
        }

        private static OfficeVisualTheme.SemanticColour Colour(string id, string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color colour))
                throw new InvalidDataException("Invalid M4 palette colour " + hex);
            return new OfficeVisualTheme.SemanticColour(id, colour);
        }
    }
}
#endif
