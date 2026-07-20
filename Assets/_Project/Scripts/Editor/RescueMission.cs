#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using Desk42.UI;
using Desk42.Core;
using Desk42.Claims;
using Desk42.RedTape;
using TMPro;

namespace Desk42.EditorTools
{
    public static class RescueMission
    {
        [MenuItem("Tools/Desk 42/RESCUE MISSION (Fix Everything)")]
        public static void Execute()
        {
            var scene = EditorSceneManager.GetActiveScene();

            // 1. Ensure Approve/Deny buttons are safely out of the way of the hand
            var approveBtn = GameObject.Find("ApproveBtn")?.GetComponent<RectTransform>();
            if (approveBtn)
            {
                // Anchor bottom center
                approveBtn.anchorMin = new Vector2(0.5f, 0); approveBtn.anchorMax = new Vector2(0.5f, 0);
                approveBtn.pivot = new Vector2(0.5f, 0);
                approveBtn.anchoredPosition = new Vector2(-150, 150); 
                EditorUtility.SetDirty(approveBtn.gameObject);
            }
            var denyBtn = GameObject.Find("DenyBtn")?.GetComponent<RectTransform>();
            if (denyBtn)
            {
                denyBtn.anchorMin = new Vector2(0.5f, 0); denyBtn.anchorMax = new Vector2(0.5f, 0);
                denyBtn.pivot = new Vector2(0.5f, 0);
                denyBtn.anchoredPosition = new Vector2(150, 150);
                EditorUtility.SetDirty(denyBtn.gameObject);
            }

            // 2. Safe positioning of ClientInfo Text
            var ci = GameObject.Find("ClientInfo");
            if (ci)
            {
                var oldVlg = ci.GetComponent<VerticalLayoutGroup>();
                if (oldVlg) GameObject.DestroyImmediate(oldVlg);

                var variantLabel = ci.transform.Find("VariantLabel") as RectTransform;
                if (variantLabel)
                {
                    variantLabel.anchorMin = new Vector2(0.5f, 1); variantLabel.anchorMax = new Vector2(0.5f, 1);
                    variantLabel.pivot = new Vector2(0.5f, 1);
                    variantLabel.anchoredPosition = new Vector2(0, -60);
                    variantLabel.sizeDelta = new Vector2(600, 60);
                    EditorUtility.SetDirty(variantLabel.gameObject);
                }

                var moodLabel = ci.transform.Find("MoodLabel") as RectTransform;
                if (moodLabel)
                {
                    moodLabel.anchorMin = new Vector2(0.5f, 1); moodLabel.anchorMax = new Vector2(0.5f, 1);
                    moodLabel.pivot = new Vector2(0.5f, 1);
                    moodLabel.anchoredPosition = new Vector2(0, -120);
                    moodLabel.sizeDelta = new Vector2(600, 60);
                    EditorUtility.SetDirty(moodLabel.gameObject);
                }
            }
            
            var panelRoot = GameObject.Find("PanelRoot");
            if (panelRoot)
            {
                var oldVlg = panelRoot.GetComponent<VerticalLayoutGroup>();
                if (oldVlg) GameObject.DestroyImmediate(oldVlg);
            }

            // 3. Guarantee Card Component Wiring
            // Run the Rebuilder first so the prefab is visually healthy
            CardButtonPrefabRebuilder.Rebuild();

            var cardHandView = Object.FindFirstObjectByType<CardHandView>();
            var punchMachine = Object.FindFirstObjectByType<PunchCardMachine>();
            var cardHandContainer = GameObject.Find("CardHand")?.transform;
            
            // Find the prefab
            string[] guids = AssetDatabase.FindAssets("CardButton t:Prefab");
            CardButtonView prefabView = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("CardButton.prefab"))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    prefabView = go.GetComponent<CardButtonView>();
                    break;
                }
            }

            if (cardHandView)
            {
                var so = new SerializedObject(cardHandView);
                if (punchMachine) so.FindProperty("_machine").objectReferenceValue = punchMachine;
                if (cardHandContainer) so.FindProperty("_cardContainer").objectReferenceValue = cardHandContainer;
                if (prefabView) so.FindProperty("_cardButtonPrefab").objectReferenceValue = prefabView;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cardHandView);
                
                // Add Horizontal Layout to the container if missing
                if (cardHandContainer)
                {
                    var hlg = cardHandContainer.GetComponent<HorizontalLayoutGroup>();
                    if (!hlg) hlg = cardHandContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hlg.childControlWidth = false; hlg.childControlHeight = false; // Important: lets cards set their own size!
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.spacing = 15;
                    
                    var containerRect = cardHandContainer.GetComponent<RectTransform>();
                    if (containerRect)
                    {
                        containerRect.anchorMin = new Vector2(0, 0); containerRect.anchorMax = new Vector2(1, 0);
                        containerRect.pivot = new Vector2(0.5f, 0);
                        containerRect.anchoredPosition = new Vector2(0, 20); // Sit right at bottom
                        containerRect.sizeDelta = new Vector2(0, 220);
                    }
                }
            }

            // 4. Clean up invisible barriers
            var claimPanel = GameObject.Find("ClaimPanel");
            if (claimPanel)
            {
                var img = claimPanel.GetComponent<Image>();
                if (img) img.raycastTarget = false; 
                EditorUtility.SetDirty(claimPanel);
            }

            // 5. Hierarchy sorting to ensure cards draw on top of everything!
            if (cardHandContainer) cardHandContainer.SetAsLastSibling();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[RESCUE MISSION] Safe recovery complete. Hierarchy and scripts sanitized.");
        }
    }
}
#endif
