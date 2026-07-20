#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Desk42.Editor
{
    public static class MainMenuVisualPolishSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string BackgroundPath =
            "Assets/_Project/Art/Concepts/VisualIdentity/Mockups/D42_Mockup_DeskStage_Locked_v002.png";

        [MenuItem("Tools/Desk 42/Visual Identity/Polish Main Menu With Placeholders")]
        public static void Apply()
        {
            ConfigureTexture();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            var controllers = Object.FindObjectsOfType<Desk42.UI.MainMenuController>(true);
            foreach (var controller in controllers)
            {
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("_officeBackground").objectReferenceValue = background;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[MainMenuVisualPolish] Placeholder office identity applied.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
            EditorApplication.Exit(0);
        }

        private static void ConfigureTexture()
        {
            var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
#endif
