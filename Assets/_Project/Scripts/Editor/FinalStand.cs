using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Desk42.UI;
using TMPro;

namespace Desk42.EditorTools
{
    public static class FinalStand
    {
        [MenuItem("Tools/Desk 42/FINAL STAND (Perfect Layout & Wiring)")]
        public static void Execute()
        {
            // 1. Move Approve/Deny safely up
            var approveBtn = GameObject.Find("ApproveBtn")?.GetComponent<RectTransform>();
            if (approveBtn) { approveBtn.anchoredPosition = new Vector2(-150, 270); EditorUtility.SetDirty(approveBtn.gameObject); }
            var denyBtn = GameObject.Find("DenyBtn")?.GetComponent<RectTransform>();
            if (denyBtn) { denyBtn.anchoredPosition = new Vector2(150, 270); EditorUtility.SetDirty(denyBtn.gameObject); }

            // 2. Fix Panel Root Layout (Claim Info overlapping)
            var panelRoot = GameObject.Find("PanelRoot");
            if (panelRoot)
            {
                var vlg = panelRoot.GetComponent<VerticalLayoutGroup>();
                if (!vlg) vlg = panelRoot.AddComponent<VerticalLayoutGroup>();
                vlg.childControlWidth = true;
                vlg.childControlHeight = false; // Prevents 0-height text collapse!
                vlg.childForceExpandHeight = false;
                vlg.spacing = 8f;
                // Add preferred height to children so they format well
                foreach (RectTransform child in panelRoot.transform)
                {
                    if (child.GetComponent<TMP_Text>())
                    {
                        var le = child.GetComponent<LayoutElement>();
                        if (!le) le = child.gameObject.AddComponent<LayoutElement>();
                        le.minHeight = 35f;
                    }
                }
                EditorUtility.SetDirty(panelRoot);
            }

            // 3. Perfect the CardButton Prefab
            string[] guids = AssetDatabase.FindAssets("CardButton t:Prefab");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("CardButton.prefab"))
                {
                    var prefab = PrefabUtility.LoadPrefabContents(path);
                    var view = prefab.GetComponent<CardButtonView>();
                    var btn = prefab.GetComponent<Button>();
                    
                    if (view && btn)
                    {
                        var so = new SerializedObject(view);
                        so.FindProperty("_button").objectReferenceValue = btn;
                        
                        // Find texts
                        foreach (Transform t in prefab.transform)
                        {
                            var tmp = t.GetComponent<TMP_Text>();
                            if (!tmp) continue;
                            if (t.name == "NameLabel") so.FindProperty("_nameLabel").objectReferenceValue = tmp;
                            if (t.name == "TypeLabel") so.FindProperty("_typeLabel").objectReferenceValue = tmp;
                            if (t.name == "CostLabel") so.FindProperty("_costLabel").objectReferenceValue = tmp;
                            if (t.name == "FatigueLabel") so.FindProperty("_fatigueLabel").objectReferenceValue = tmp;
                        }

                        // Attach background
                        so.FindProperty("_background").objectReferenceValue = prefab.GetComponent<Image>();

                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    PrefabUtility.UnloadPrefabContents(prefab);
                    break;
                }
            }

            Debug.Log("[FINAL STAND] Card buttons wired. Panel stacked. Approve shifted up. Perfection.");
        }
    }
}
