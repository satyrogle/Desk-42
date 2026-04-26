// ============================================================
// DESK 42 — Missing Script Cleaner
//
// Editor utility that finds and removes "Missing Script"
// components from all GameObjects in the loaded scenes.
//
// Use: Tools → Desk 42 → Remove Missing Scripts (Loaded Scenes)
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Desk42.EditorTools
{
    public static class MissingScriptCleaner
    {
        [MenuItem("Tools/Desk 42/Remove Missing Scripts (Loaded Scenes)")]
        public static void RemoveMissingInLoadedScenes()
        {
            int totalRemoved = 0;
            int gameObjectsScanned = 0;

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                    {
                        gameObjectsScanned++;
                        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                        if (removed > 0)
                        {
                            totalRemoved += removed;
                            Debug.Log($"[MissingScriptCleaner] Removed {removed} on '{t.gameObject.name}' (scene: {scene.name}).", t.gameObject);
                            EditorSceneManager.MarkSceneDirty(scene);
                        }
                    }
                }
            }

            Debug.Log($"[MissingScriptCleaner] Done. Scanned {gameObjectsScanned} GameObjects. " +
                      $"Removed {totalRemoved} missing script components.");
        }

        [MenuItem("Tools/Desk 42/Find Missing Scripts (Loaded Scenes)")]
        public static void FindMissingInLoadedScenes()
        {
            int found = 0;

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                    {
                        var components = t.GetComponents<Component>();
                        for (int i = 0; i < components.Length; i++)
                        {
                            if (components[i] == null)
                            {
                                Debug.LogWarning($"[MissingScriptCleaner] Missing script on '{GetPath(t)}' (scene: {scene.name}).", t.gameObject);
                                found++;
                                break;
                            }
                        }
                    }
                }
            }

            Debug.Log($"[MissingScriptCleaner] Search complete. Found {found} GameObjects with missing scripts.");
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
#endif
