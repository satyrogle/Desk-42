#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Desk42.UI;

namespace Desk42.Editor
{
    public static class UpdateTierUISetup
    {
        [MenuItem("Desk 42/Setup Update Tier UI (Active Scene)")]
        public static void SetupUIForActiveScene()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (activeScene.Contains("Shift"))
            {
                SetupCorpOS();
            }
            else if (activeScene.Contains("InternalAudit") || activeScene.Contains("MainMenu"))
            {
                SetupInternalAuditUI();
            }
            else
            {
                Debug.LogWarning("[Desk 42 Setup] Unrecognized scene. Please open the 'Shift' or 'InternalAudit' scene before running this tool.");
            }
        }

        private static void SetupCorpOS()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("No Canvas found in the current scene to attach CorpOS Window Manager.");
                return;
            }

            // Check if already exists
            if (canvas.GetComponentInChildren<CorpOSWindowManager>() != null)
            {
                Debug.Log("CorpOSWindowManager already exists in this scene.");
                return;
            }

            GameObject corpOSGo = new GameObject("CorpOS_DesktopManager");
            corpOSGo.transform.SetParent(canvas.transform, false);

            var manager = corpOSGo.AddComponent<CorpOSWindowManager>();

            // Setup anchors and overlay
            GameObject overlayGo = new GameObject("BSOD_Overlay");
            overlayGo.transform.SetParent(corpOSGo.transform, false);
            var overlayRect = overlayGo.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            
            var cg = overlayGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var bg = overlayGo.AddComponent<Image>();
            bg.color = new Color(0, 0, 0.7f, 1f); // Classic BSOD blue

            GameObject textGo = new GameObject("BSOD_Text");
            textGo.transform.SetParent(overlayGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(50, 50);
            textRect.offsetMax = new Vector2(-50, -50);

            var tmpText = textGo.AddComponent<TextMeshProUGUI>();
            tmpText.color = Color.white;
            tmpText.fontStyle = FontStyles.Bold;
            tmpText.alignment = TextAlignmentOptions.TopLeft;

            GameObject popupAnchor = new GameObject("PopupAnchor");
            popupAnchor.transform.SetParent(corpOSGo.transform, false);
            var popupRect = popupAnchor.AddComponent<RectTransform>();
            popupRect.anchorMin = Vector2.zero;
            popupRect.anchorMax = Vector2.one;
            popupRect.offsetMin = Vector2.zero;
            popupRect.offsetMax = Vector2.zero;

            // Wire serialization using SerializedObject to access private serialized fields
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("_popupAnchor").objectReferenceValue = popupAnchor.transform;
            so.FindProperty("_bsodOverlay").objectReferenceValue = cg;
            so.FindProperty("_bsodText").objectReferenceValue = tmpText;
            so.ApplyModifiedProperties();

            Debug.Log("[Desk 42 Setup] Successfully installed CorpOS Desktop into the Shift scene!");
        }

        private static void SetupInternalAuditUI()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("No Canvas found. Please add a Canvas first.");
                return;
            }

            // 1. Setup Meta Hub
            if (canvas.GetComponentInChildren<InternalAuditMetaHubUI>() == null)
            {
                GameObject hubGo = new GameObject("InternalAudit_MetaHub");
                hubGo.transform.SetParent(canvas.transform, false);
                var hub = hubGo.AddComponent<InternalAuditMetaHubUI>();

                // Add basic dummy buttons for rapid prototyping
                var btn1 = CreateButton(hubGo.transform, "BuyHealthInsuranceBtn", new Vector2(0, 50));
                var btn2 = CreateButton(hubGo.transform, "BuySigningBonusBtn", new Vector2(0, 0));
                var btn3 = CreateButton(hubGo.transform, "StartShiftBtn", new Vector2(0, -150));
                
                var label = CreateLabel(hubGo.transform, "BankBalanceText", new Vector2(0, 150));

                SerializedObject so = new SerializedObject(hub);
                so.FindProperty("_buyHealthInsuranceBtn").objectReferenceValue = btn1;
                so.FindProperty("_buySigningBonusBtn").objectReferenceValue = btn2;
                so.FindProperty("_startNextShiftBtn").objectReferenceValue = btn3;
                so.FindProperty("_creditBalanceText").objectReferenceValue = label;
                so.ApplyModifiedProperties();
                
                Debug.Log("[Desk 42 Setup] Installed InternalAuditMetaHubUI.");
            }

            // 2. Setup Conspiracy Board
            if (canvas.GetComponentInChildren<ConspiracyBoardUI>() == null)
            {
                GameObject boardGo = new GameObject("ConspiracyBoard");
                boardGo.transform.SetParent(canvas.transform, false);
                var board = boardGo.AddComponent<ConspiracyBoardUI>();

                var boardArea = new GameObject("BoardArea").AddComponent<RectTransform>();
                boardArea.transform.SetParent(boardGo.transform, false);
                boardArea.anchorMin = Vector2.zero;
                boardArea.anchorMax = Vector2.one;

                var closeBtn = CreateButton(boardGo.transform, "CloseBoardBtn", new Vector2(300, 200));

                SerializedObject so = new SerializedObject(board);
                so.FindProperty("_boardArea").objectReferenceValue = boardArea;
                so.FindProperty("_closeButton").objectReferenceValue = closeBtn;
                so.ApplyModifiedProperties();

                Debug.Log("[Desk 42 Setup] Installed ConspiracyBoardUI.");
            }
        }

        private static Button CreateButton(Transform parent, string name, Vector2 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(160, 40);
            
            go.AddComponent<Image>().color = Color.gray;
            return go.AddComponent<Button>();
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(200, 50);

            var tmpText = go.AddComponent<TextMeshProUGUI>();
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.text = "Label";
            return tmpText;
        }
    }
}
#endif
