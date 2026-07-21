#if UNITY_EDITOR
using System;
using System.IO;
using Desk42.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Desk42.Editor
{
    /// <summary>
    /// One repeatable setup pass for production pixel imports, the runtime portrait
    /// catalog, claimant UI wiring, and ready-to-place UI equipment prefabs.
    /// </summary>
    public static class VisualIdentityRuntimeSetup
    {
        private const string ShiftScenePath = "Assets/_Project/Scenes/Shift.unity";
        private const string CatalogFolder = "Assets/_Project/Resources/VisualIdentity";
        private const string CatalogPath = CatalogFolder + "/ClientVisualCatalog.asset";
        private const string EquipmentPrefabFolder =
            "Assets/_Project/Prefabs/VisualIdentity/OfficeEquipment";
        private const float PixelsPerUnit = 64f;

        private const string CorePortraitFolder =
            "Assets/_Project/Art/Sprites/VisualIdentity/Claimants/CoreSpecies";
        private const string StatePortraitFolder =
            "Assets/_Project/Art/Sprites/VisualIdentity/Claimants/States";
        private const string EquipmentFolder =
            "Assets/_Project/Art/Sprites/VisualIdentity/Equipment";

        private static readonly string[] EquipmentNames =
        {
            "CRTTerminal", "Telephone", "ApprovalStamp", "PaperTray",
            "CoffeeMug", "PenHolder", "ClaimForms", "CrumpledPaper",
            "Copier", "Shredder", "EvidenceTrolley", "FilingCabinet"
        };

        [MenuItem("Tools/Desk 42/Visual Identity/Wire Runtime Assets")]
        public static void BuildRuntimeAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureProductionSprites();
            ClientVisualCatalog catalog = CreateOrUpdateCatalog();
            CreateEquipmentPrefabs();
            WireShiftScene(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[VisualIdentity] Runtime claimant portraits and office equipment wired.");
        }

        public static void BuildRuntimeAssetsFromCommandLine()
        {
            try
            {
                BuildRuntimeAssets();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureProductionSprites()
        {
            foreach (string path in Directory.GetFiles(CorePortraitFolder, "*.png"))
                ConfigureSprite(ToAssetPath(path));

            foreach (string path in Directory.GetFiles(StatePortraitFolder, "*.png"))
                ConfigureSprite(ToAssetPath(path));

            foreach (string path in Directory.GetFiles(EquipmentFolder, "*.png"))
                ConfigureSprite(ToAssetPath(path));
        }

        private static void ConfigureSprite(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static ClientVisualCatalog CreateOrUpdateCatalog()
        {
            EnsureFolder(CatalogFolder);

            ClientVisualCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ClientVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ClientVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            ClientVisualCatalog.Profile moth = new()
            {
                SpeciesIds = new[] { "moth_accountant", "corporate_entity", "human_standard" },
                Pending = LoadState("Pending"),
                Agitated = LoadState("Agitated"),
                Litigious = LoadState("Litigious"),
                Cooperative = LoadState("Cooperative"),
                Suspicious = LoadState("Suspicious"),
                Resigned = LoadState("Resigned"),
                Paranoid = LoadState("Paranoid"),
                Dissociating = LoadState("Dissociating"),
                Smug = LoadState("Smug")
            };

            ClientVisualCatalog.Profile gel = NeutralProfile(
                new[] { "gel_anomaly", "anomalous_adjacent", "human_distressed" },
                CorePortraitFolder + "/D42_Portrait_GelAnomaly_Pending_128_v002.png");
            ClientVisualCatalog.Profile alien = NeutralProfile(
                new[] { "unregistered_alien", "humanoid", "kobold", "temp_worker" },
                CorePortraitFolder + "/D42_Portrait_UnregisteredAlien_Pending_128_v002.png");
            ClientVisualCatalog.Profile voidProxy = NeutralProfile(
                new[] { "void_proxy", "void_adjacent", "spectral_entity", "human_litigious" },
                CorePortraitFolder + "/D42_Portrait_VoidProxy_Pending_128_v002.png");

            catalog.Profiles = new[] { moth, gel, alien, voidProxy };
            catalog.FallbackProfileIndex = 2;
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static ClientVisualCatalog.Profile NeutralProfile(
            string[] speciesIds, string portraitPath)
        {
            return new ClientVisualCatalog.Profile
            {
                SpeciesIds = speciesIds,
                Pending = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath)
            };
        }

        private static Sprite LoadState(string state)
        {
            string path = StatePortraitFolder
                + $"/D42_Portrait_MothAccountant_{state}_128_v002.png";
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreateEquipmentPrefabs()
        {
            EnsureFolder(EquipmentPrefabFolder);

            foreach (string itemName in EquipmentNames)
            {
                string spritePath = EquipmentFolder
                    + $"/D42_Prop_{itemName}_Idle_64_v002.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[VisualIdentity] Missing equipment sprite: {spritePath}");
                    continue;
                }

                var root = new GameObject(
                    $"D42_Prop_{itemName}",
                    typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image), typeof(LayoutElement));
                root.layer = LayerMask.NameToLayer("UI");

                var rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(64f, 64f);

                var image = root.GetComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;

                var layout = root.GetComponent<LayoutElement>();
                layout.preferredWidth = 64f;
                layout.preferredHeight = 64f;

                string prefabPath = EquipmentPrefabFolder + $"/D42_Prop_{itemName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void WireShiftScene(ClientVisualCatalog catalog)
        {
            Scene scene = EditorSceneManager.OpenScene(ShiftScenePath, OpenSceneMode.Single);
            ClientView view = UnityEngine.Object.FindObjectOfType<ClientView>(true);
            if (view == null)
                throw new InvalidOperationException("Shift scene has no ClientView to wire.");

            Transform existing = view.transform.Find("ClientPortrait");
            GameObject portraitObject;
            if (existing == null)
            {
                portraitObject = new GameObject(
                    "ClientPortrait",
                    typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image), typeof(LayoutElement),
                    typeof(DraggableClientPortrait));
                portraitObject.layer = LayerMask.NameToLayer("UI");
                portraitObject.transform.SetParent(view.transform, false);
                portraitObject.transform.SetSiblingIndex(0);
            }
            else
            {
                portraitObject = existing.gameObject;
            }

            var portraitRect = portraitObject.GetComponent<RectTransform>();
            portraitRect.sizeDelta = new Vector2(256f, 256f);

            var portraitImage = portraitObject.GetComponent<Image>();
            portraitImage.sprite = catalog.ResolveSprite("moth_accountant", Desk42.Core.ClientStateID.Pending);
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = true;

            var portraitLayout = portraitObject.GetComponent<LayoutElement>();
            portraitLayout.minHeight = 256f;
            portraitLayout.preferredHeight = 256f;
            portraitLayout.preferredWidth = 256f;

            var viewData = new SerializedObject(view);
            viewData.FindProperty("_portraitImage").objectReferenceValue = portraitImage;
            viewData.FindProperty("_visualCatalog").objectReferenceValue = catalog;
            viewData.ApplyModifiedPropertiesWithoutUndo();

            ClientFidgetDriver fidget = view.GetComponentInChildren<ClientFidgetDriver>(true);
            if (fidget != null)
            {
                var fidgetData = new SerializedObject(fidget);
                fidgetData.FindProperty("_portrait").objectReferenceValue = portraitRect;
                fidgetData.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ToAssetPath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
#endif
