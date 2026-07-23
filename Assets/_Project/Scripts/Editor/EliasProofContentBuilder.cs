#if UNITY_EDITOR
using System.IO;
using Desk42.Core;
using UnityEditor;
using UnityEngine;

namespace Desk42.EditorTools
{
    public static class EliasProofContentBuilder
    {
        private const string AssetPath =
            "Assets/_Project/ScriptableObjects/Narrative/EliasProofContent.asset";

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
    }
}
#endif
