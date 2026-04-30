using UnityEngine;
using UnityEditor;
using Desk42.Tutorial;
using Desk42.UI;

namespace Desk42.EditorTools
{
    public static class HighImpactInjector
    {
        [MenuItem("Tools/Desk 42/Inject Shift UI (High Impact)", priority = 30)]
        public static void InjectShiftUI()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name != "Shift")
            {
                Debug.LogWarning("Please open the 'Shift' scene before running this tool.");
                return;
            }

            // Find or create HighImpactSystems container
            var systemsGO = GameObject.Find("UI_HighImpactSystems");
            if (systemsGO == null)
            {
                systemsGO = new GameObject("UI_HighImpactSystems");
                Undo.RegisterCreatedObjectUndo(systemsGO, "Create HighImpactSystems");
            }

            // 1. NotificationFeed
            if (systemsGO.GetComponent<NotificationFeed>() == null)
            {
                Undo.AddComponent<NotificationFeed>(systemsGO);
                Debug.Log("[HighImpactInjector] Attached NotificationFeed (Hazards/Sanity).");
            }

            // 2. MemoFeedUI
            if (systemsGO.GetComponent<MemoFeedUI>() == null)
            {
                Undo.AddComponent<MemoFeedUI>(systemsGO);
                Debug.Log("[HighImpactInjector] Attached MemoFeedUI (Claim Memos).");
            }

            // 3. MoralDilemmaPanel
            if (systemsGO.GetComponent<MoralDilemmaPanel>() == null)
            {
                Undo.AddComponent<MoralDilemmaPanel>(systemsGO);
                Debug.Log("[HighImpactInjector] Attached MoralDilemmaPanel.");
            }

            // 3b. TutorialController (first-run guided overlay; no-op
            //     if MetaProgressData.TutorialCompleted is true)
            if (systemsGO.GetComponent<TutorialController>() == null)
            {
                Undo.AddComponent<TutorialController>(systemsGO);
                Debug.Log("[HighImpactInjector] Attached TutorialController.");
            }

            // 4. NDA Overlay Renderer (should be on UI_Shift or Canvas)
            var canvasRoot = GameObject.Find("UI_Shift") ?? GameObject.FindObjectOfType<Canvas>()?.gameObject;
            if (canvasRoot != null)
            {
                var nda = canvasRoot.GetComponentInChildren<NDAOverlayRenderer>();
                if (nda == null)
                {
                    nda = Undo.AddComponent<NDAOverlayRenderer>(canvasRoot);
                    Debug.Log("[HighImpactInjector] Attached NDAOverlayRenderer to UI Canvas.");
                }

                // If prefab is missing, we must generate one.
                var serializedNDA = new SerializedObject(nda);
                var prefabProp = serializedNDA.FindProperty("_ndaPanelPrefab");
                if (prefabProp != null && prefabProp.objectReferenceValue == null)
                {
                    Debug.LogWarning("[HighImpactInjector] NDAOverlayRenderer needs a prefab assigned. Please assign a UI panel prefab with a TextMeshProUGUI child for the legalese text to _ndaPanelPrefab.");
                    // We don't procedurally build prefabs in editor tools easily without saving to disk, 
                    // so we warn the user or build a generic one and attach it to the scene.
                    // But wait, the user said they saw the NDA events firing.
                }
            }
            else
            {
                Debug.LogWarning("[HighImpactInjector] Could not find UI Canvas to attach NDAOverlayRenderer.");
            }

            EditorUtility.SetDirty(systemsGO);
            Debug.Log("<color=green><b>[Success] High Impact UI injected into Shift scene.</b></color>");
        }

        [MenuItem("Tools/Desk 42/Inject Audit Hub (High Impact)", priority = 31)]
        public static void InjectAuditHub()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name != "InternalAudit")
            {
                Debug.LogWarning("Please open the 'InternalAudit' scene before running this tool.");
                return;
            }

            // 1. Hub Bootstrap
            var hubManager = GameObject.Find("HubManager");
            if (hubManager == null)
            {
                hubManager = new GameObject("HubManager");
                Undo.RegisterCreatedObjectUndo(hubManager, "Create HubManager");
            }

            if (hubManager.GetComponent<InternalAuditHubBootstrap>() == null)
            {
                Undo.AddComponent<InternalAuditHubBootstrap>(hubManager);
                Debug.Log("[HighImpactInjector] Attached InternalAuditHubBootstrap.");
            }

            // 2. ConspiracyBoardUI Setup
            var boardGO = GameObject.Find("ConspiracyBoardUI");
            if (boardGO == null)
            {
                boardGO = new GameObject("ConspiracyBoardUI");
                boardGO.transform.SetParent(hubManager.transform, false);
                Undo.RegisterCreatedObjectUndo(boardGO, "Create ConspiracyBoardUI");

                var boardUI = Undo.AddComponent<ConspiracyBoardUI>(boardGO);

                // Build Board Canvas
                var canvas = boardGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 900;
                var scaler = boardGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                boardGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                // Build Board BG
                var bg = new GameObject("Background");
                bg.transform.SetParent(boardGO.transform, false);
                var bgrt = bg.AddComponent<RectTransform>();
                bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
                bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
                bg.AddComponent<UnityEngine.UI.Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.98f);

                // Build Board Area
                var boardArea = new GameObject("BoardArea");
                boardArea.transform.SetParent(boardGO.transform, false);
                var areart = boardArea.AddComponent<RectTransform>();
                areart.anchorMin = new Vector2(0, 0); areart.anchorMax = new Vector2(1, 1);
                areart.offsetMin = new Vector2(50, 100); areart.offsetMax = new Vector2(-50, -50);
                areart.pivot = new Vector2(0.5f, 0.5f);
                boardArea.AddComponent<UnityEngine.UI.Image>().color = new Color(0.4f, 0.3f, 0.2f, 1f); // corkboard brown

                // Build Close Button
                var closeBtnGO = new GameObject("CloseButton");
                closeBtnGO.transform.SetParent(boardGO.transform, false);
                var crt = closeBtnGO.AddComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.5f, 0); crt.anchorMax = new Vector2(0.5f, 0);
                crt.pivot = new Vector2(0.5f, 0);
                crt.sizeDelta = new Vector2(200, 60);
                crt.anchoredPosition = new Vector2(0, 20);
                closeBtnGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.3f, 0.2f, 0.2f);
                var closeBtn = closeBtnGO.AddComponent<UnityEngine.UI.Button>();

                // Build PostIt Prefab (Local to Scene, hidden)
                var postIt = new GameObject("PostIt_Prefab");
                postIt.transform.SetParent(boardGO.transform, false);
                var prt = postIt.AddComponent<RectTransform>();
                prt.sizeDelta = new Vector2(120, 120);
                postIt.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.9f, 0.5f);
                var textGO = new GameObject("Text");
                textGO.transform.SetParent(postIt.transform, false);
                var trt = textGO.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(10, 10); trt.offsetMax = new Vector2(-10, -10);
                var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
                tmp.color = Color.black;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontSize = 16;
                postIt.SetActive(false);

                // Wire References
                var serializedObject = new SerializedObject(boardUI);
                serializedObject.FindProperty("_boardArea").objectReferenceValue = areart;
                serializedObject.FindProperty("_closeButton").objectReferenceValue = closeBtn;
                serializedObject.FindProperty("_postItPrefab").objectReferenceValue = postIt;
                serializedObject.ApplyModifiedProperties();

                boardGO.SetActive(false); // Default hidden, opened via Hub button

                Debug.Log("[HighImpactInjector] Built and attached ConspiracyBoardUI.");
            }

            EditorUtility.SetDirty(hubManager);
            Debug.Log("<color=green><b>[Success] High Impact UI injected into InternalAudit scene.</b></color>");
        }
    }
}
