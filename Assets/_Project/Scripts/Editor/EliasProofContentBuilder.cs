#if UNITY_EDITOR
using System.IO;
using Desk42.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Desk42.EditorTools
{
    public static class EliasProofContentBuilder
    {
        public const string AssetPath =
            "Assets/_Project/ScriptableObjects/Narrative/EliasProofContent.asset";
        private const string BootScenePath =
            "Assets/_Project/Scenes/Boot.unity";

        [MenuItem("Tools/Desk 42/Rebuild Elias Proof Content")]
        public static void Rebuild()
        {
            string folder = Path.GetDirectoryName(AssetPath)
                ?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(
                    "Assets/_Project/ScriptableObjects", "Narrative");

            EliasProofContent existing =
                AssetDatabase.LoadAssetAtPath<EliasProofContent>(AssetPath);
            EliasProofContent content =
                ScriptableObject.CreateInstance<EliasProofContent>();
            content.name = "EliasProofContent";
            if (existing == null)
            {
                AssetDatabase.CreateAsset(content, AssetPath);
            }
            else
            {
                // Preserve the asset GUID so Boot-scene references remain
                // stable when the authored manifest is rebuilt.
                EditorUtility.CopySerialized(content, existing);
                existing.name = content.name;
                Object.DestroyImmediate(content);
                EditorUtility.SetDirty(existing);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[EliasProofContent] Rebuilt {AssetPath}.");
        }

        [MenuItem("Tools/Desk 42/Wire Elias Proof Content")]
        public static void WireBootReference()
        {
            EliasProofContent content =
                AssetDatabase.LoadAssetAtPath<EliasProofContent>(
                    AssetPath);
            if (content == null)
            {
                Rebuild();
                content =
                    AssetDatabase.LoadAssetAtPath<EliasProofContent>(
                        AssetPath);
            }

            string previousScenePath =
                EditorSceneManager.GetActiveScene().path;
            var bootScene =
                EditorSceneManager.OpenScene(
                    BootScenePath, OpenSceneMode.Single);
            GameManager manager =
                Object.FindObjectOfType<GameManager>();
            if (manager == null)
            {
                throw new System.InvalidOperationException(
                    "Boot scene contains no GameManager.");
            }

            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("_eliasProofContent")
                .objectReferenceValue = content;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.SaveScene(bootScene);

            if (!string.IsNullOrWhiteSpace(previousScenePath)
                && previousScenePath != BootScenePath)
            {
                EditorSceneManager.OpenScene(
                    previousScenePath, OpenSceneMode.Single);
            }

            Debug.Log(
                $"[EliasProofContent] Wired {AssetPath} into Boot GameManager.");
        }
    }
}
#endif
