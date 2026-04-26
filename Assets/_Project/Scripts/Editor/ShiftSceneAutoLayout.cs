// ============================================================
// DESK 42 — Shift Scene Auto-Layout (V2 — comprehensive fix)
//
// One-shot Editor menu that:
//  - Strips conflicting LayoutGroup components
//  - Anchors every UI element to a screen position
//  - Auto-wires CardButton prefab's TMP labels to CardButtonView
//  - Auto-wires CardHandView's machine/container/prefab refs
//  - Sets gauge label colors for contrast
//  - Creates Approve/Deny buttons + wires them
//  - Lowers FormCorruption threshold so text stays readable
//
// Usage: open Shift.unity, then
//   Tools → Desk 42 → Auto-Layout Shift Scene
//
// Safe to re-run.
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Encounter;
using Desk42.UI;

namespace Desk42.EditorTools
{
    public static class ShiftSceneAutoLayout
    {
        [MenuItem("Tools/Desk 42/Auto-Layout Shift Scene")]
        public static void AutoLayout()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Shift")
            {
                EditorUtility.DisplayDialog("Auto-Layout",
                    $"Open Shift.unity first. Active scene: {scene.name}", "OK");
                return;
            }

            var shiftUI = GameObject.Find("ShiftUI");
            if (shiftUI == null)
            {
                EditorUtility.DisplayDialog("Auto-Layout",
                    "ShiftUI GameObject not found.", "OK");
                return;
            }

            ConfigureCanvas(shiftUI);
            StripLayoutGroups(shiftUI);
            ApplyTopLevelAnchors(shiftUI);
            LayoutClaimPanelChildren(shiftUI);
            LayoutGaugeChildren(shiftUI, "SoulGauge",   "SoulLabel",   new Color(0.7f, 0.2f, 0.2f));
            LayoutGaugeChildren(shiftUI, "SanityGauge", "SanityLabel", new Color(0.2f, 0.5f, 0.7f));
            LayoutClientInfoChildren(shiftUI);
            EnsureResolutionButtons(shiftUI);
            WireResolutionButtons();
            AutoWireCardButtonPrefab();
            AutoWireCardHandView();
            AdjustFormCorruptionThreshold();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[ShiftAutoLayout V2] Layout + wiring applied. Save the scene (Ctrl+S).");
            EditorUtility.DisplayDialog("Auto-Layout V2",
                "Done. Save the scene (Ctrl+S), then press Play from Boot.", "OK");
        }

        // ── Canvas + Scaler ───────────────────────────────────

        private static void ConfigureCanvas(GameObject shiftUI)
        {
            var canvas = shiftUI.GetComponent<Canvas>();
            if (canvas == null) canvas = shiftUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = shiftUI.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = shiftUI.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            if (shiftUI.GetComponent<GraphicRaycaster>() == null)
                shiftUI.AddComponent<GraphicRaycaster>();
        }

        // ── Strip conflicting LayoutGroups ────────────────────

        private static void StripLayoutGroups(GameObject shiftUI)
        {
            string[] panelsToStrip =
            {
                "ClaimPanel", "ClientInfo", "SoulGauge", "SanityGauge",
            };
            foreach (var name in panelsToStrip)
            {
                var t = shiftUI.transform.Find(name);
                if (t == null) continue;

                foreach (var lg in t.GetComponents<LayoutGroup>())
                {
                    Object.DestroyImmediate(lg);
                    Debug.Log($"[ShiftAutoLayout] Removed LayoutGroup from '{name}'.");
                }
                var fitter = t.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                {
                    Object.DestroyImmediate(fitter);
                    Debug.Log($"[ShiftAutoLayout] Removed ContentSizeFitter from '{name}'.");
                }
            }
        }

        // ── Top-Level Anchors ─────────────────────────────────

        private static void ApplyTopLevelAnchors(GameObject shiftUI)
        {
            ApplyAnchor(shiftUI, "SoulGauge",           new(0, 1), new(0, 1), new(0, 1), new(20, -20),    new(300, 36));
            ApplyAnchor(shiftUI, "SanityGauge",         new(0, 1), new(0, 1), new(0, 1), new(20, -64),    new(300, 36));
            ApplyAnchor(shiftUI, "PhaseLabel",          new(0.5f, 1), new(0.5f, 1), new(0.5f, 1), new(0, -20),  new(400, 40));
            ApplyAnchor(shiftUI, "ClientProgressLabel", new(0.5f, 1), new(0.5f, 1), new(0.5f, 1), new(0, -60),  new(400, 30));
            ApplyAnchor(shiftUI, "TimerLabel",          new(1, 1), new(1, 1), new(1, 1), new(-20, -20),  new(200, 40));
            ApplyAnchor(shiftUI, "TimerFill",           new(1, 1), new(1, 1), new(1, 1), new(-20, -64),  new(200, 16));
            ApplyAnchor(shiftUI, "CreditsLabel",        new(1, 0), new(1, 0), new(1, 0), new(-20, 20),   new(220, 36));
            ApplyAnchor(shiftUI, "ClientInfo",          new(0.5f, 1), new(0.5f, 1), new(0.5f, 1), new(0, -120), new(600, 40));
            ApplyAnchor(shiftUI, "ClaimPanel",          new(0.5f, 0.5f), new(0.5f, 0.5f), new(0.5f, 0.5f), new(0, 80),  new(720, 380));

            ApplyStretchFull(shiftUI, "MoralFlash");
            ApplyStretchFull(shiftUI, "CorruptionOverlay");
            ApplyStretchFull(shiftUI, "SanityEffects");

            // CardHand bottom-center with horizontal layout for spacing
            var cardHand = shiftUI.transform.Find("CardHand")?.gameObject;
            if (cardHand != null)
            {
                ApplyAnchor(shiftUI, "CardHand", new(0.5f, 0), new(0.5f, 0), new(0.5f, 0), new(0, 24), new(1100, 200));
                var hlg = cardHand.GetComponent<HorizontalLayoutGroup>();
                if (hlg == null) hlg = cardHand.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing               = 16;
                hlg.childAlignment        = TextAnchor.MiddleCenter;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childControlWidth     = false;
                hlg.childControlHeight    = false;
                hlg.padding               = new RectOffset(8, 8, 8, 8);
            }
        }

        // ── ClaimPanel children ───────────────────────────────

        private static void LayoutClaimPanelChildren(GameObject shiftUI)
        {
            var cp = shiftUI.transform.Find("ClaimPanel");
            if (cp == null) return;

            // Add or update background
            EnsureBackground(cp.gameObject, new Color(0.08f, 0.08f, 0.10f, 0.92f));

            // Header row
            ApplyChild(cp, "ClaimantLabel", new(0, 1), new(1, 1), new(0, 1),
                new(20, -20), new(-260, 40));
            SetTextStyle(cp, "ClaimantLabel", TextAlignmentOptions.TopLeft, 24, Color.white);

            ApplyChild(cp, "AmountLabel", new(1, 1), new(1, 1), new(1, 1),
                new(-20, -20), new(220, 40));
            SetTextStyle(cp, "AmountLabel", TextAlignmentOptions.TopRight, 28,
                new Color(0.95f, 0.85f, 0.4f));

            // Sub-header
            ApplyChild(cp, "SpeciesLabel", new(0, 1), new(0, 1), new(0, 1),
                new(20, -60), new(280, 24));
            SetTextStyle(cp, "SpeciesLabel", TextAlignmentOptions.TopLeft, 16,
                new Color(0.7f, 0.7f, 0.7f));

            // Body — stretch to fill middle area
            ApplyStretchInsets(cp, "IncidentText", 20, 100, 20, 90);
            SetTextStyle(cp, "IncidentText", TextAlignmentOptions.TopLeft, 18, Color.white);

            // Footer row
            ApplyChild(cp, "AnomalyTagsLabel", new(0, 0), new(0, 0), new(0, 0),
                new(20, 44), new(440, 24));
            SetTextStyle(cp, "AnomalyTagsLabel", TextAlignmentOptions.BottomLeft, 14,
                new Color(0.7f, 0.55f, 0.85f));

            ApplyChild(cp, "NDALabel", new(1, 0), new(1, 0), new(1, 0),
                new(-20, 44), new(220, 24));
            SetTextStyle(cp, "NDALabel", TextAlignmentOptions.BottomRight, 16,
                new Color(0.95f, 0.45f, 0.35f));

            ApplyChild(cp, "ClaimIdLabel", new(0, 0), new(0, 0), new(0, 0),
                new(20, 14), new(220, 20));
            SetTextStyle(cp, "ClaimIdLabel", TextAlignmentOptions.BottomLeft, 12,
                new Color(0.5f, 0.5f, 0.55f));

            // Background should sit behind everything
            var bg = cp.Find("Background");
            if (bg != null) bg.SetAsFirstSibling();
        }

        // ── Gauges ────────────────────────────────────────────

        private static void LayoutGaugeChildren(GameObject shiftUI, string gaugeName,
            string labelName, Color fillColor)
        {
            var g = shiftUI.transform.Find(gaugeName);
            if (g == null) return;

            // Background
            ApplyStretchInsets(g, "Background", 0, 0, 0, 0);
            var bgImg = g.Find("Background")?.GetComponent<Image>();
            if (bgImg != null) bgImg.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);

            // Fill Area + Fill
            ApplyStretchInsets(g, "Fill Area", 2, 2, 2, 2);
            var fa = g.Find("Fill Area");
            if (fa != null)
            {
                ApplyStretchInsets(fa, "Fill", 0, 0, 0, 0);
                var fillImg = fa.Find("Fill")?.GetComponent<Image>();
                if (fillImg != null) fillImg.color = fillColor;
            }

            // Label sits on top, white text
            ApplyStretchInsets(g, labelName, 8, 0, 8, 0);
            var lblTmp = g.Find(labelName)?.GetComponent<TMP_Text>();
            if (lblTmp != null)
            {
                lblTmp.alignment    = TextAlignmentOptions.MidlineLeft;
                lblTmp.fontSize     = 16;
                lblTmp.color        = Color.white;
                lblTmp.fontStyle    = FontStyles.Bold;
                lblTmp.outlineWidth = 0.2f;
                lblTmp.outlineColor = Color.black;
            }

            // Order: bg → fill → label (siblings)
            g.Find("Background")?.SetAsFirstSibling();
            g.Find(labelName)?.SetAsLastSibling();
        }

        // ── ClientInfo ────────────────────────────────────────

        private static void LayoutClientInfoChildren(GameObject shiftUI)
        {
            var ci = shiftUI.transform.Find("ClientInfo");
            if (ci == null) return;

            ApplyChild(ci, "VariantLabel", new(0, 0), new(0, 1), new(0, 0.5f),
                new(0, 0), new(220, 0));
            SetTextStyle(ci, "VariantLabel", TextAlignmentOptions.MidlineLeft, 16, Color.white);

            ApplyChild(ci, "MoodLabel", new(0.5f, 0), new(0.5f, 1), new(0.5f, 0.5f),
                Vector2.zero, new(200, 0));
            SetTextStyle(ci, "MoodLabel", TextAlignmentOptions.Center, 16, Color.white);

            ApplyChild(ci, "MoodIndicator", new(1, 0.5f), new(1, 0.5f), new(1, 0.5f),
                new(0, 0), new(28, 28));

            ApplyChild(ci, "InjectionLabel", new(0, 0), new(1, 0), new(0.5f, 1),
                new(0, -2), new(0, 22));
            SetTextStyle(ci, "InjectionLabel", TextAlignmentOptions.Center, 12,
                new Color(0.7f, 0.7f, 0.7f));
        }

        // ── Approve / Deny ────────────────────────────────────

        private static void EnsureResolutionButtons(GameObject shiftUI)
        {
            // Place above the CardHand (CardHand sits at y=24 with height 200,
            // so Approve/Deny live at y=260+ to clear it cleanly)
            EnsureResolutionButton(shiftUI, "ApproveBtn", "APPROVE",
                new Color(0.15f, 0.45f, 0.20f), new Vector2(-150, 260));
            EnsureResolutionButton(shiftUI, "DenyBtn", "DENY",
                new Color(0.55f, 0.15f, 0.15f), new Vector2(150, 260));
        }

        private static void EnsureResolutionButton(GameObject parent, string goName,
            string label, Color color, Vector2 anchoredPos)
        {
            var existing = parent.transform.Find(goName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(goName);
                go.transform.SetParent(parent.transform, false);
                go.AddComponent<Image>();
                go.AddComponent<Button>();

                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(go.transform, false);
                labelGO.AddComponent<TextMeshProUGUI>();
            }

            // Ensure the button renders ON TOP of everything else
            go.transform.SetAsLastSibling();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot     = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(220, 64);
            rt.anchoredPosition = anchoredPos;

            var img = go.GetComponent<Image>();
            img.color = color;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
            colors.pressedColor     = Color.Lerp(color, Color.black, 0.3f);
            btn.colors = colors;

            var labelT = go.transform.Find("Label");
            if (labelT != null)
            {
                var labelRT = labelT.GetComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;

                var tmp = labelT.GetComponent<TextMeshProUGUI>();
                tmp.text      = label;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize  = 28;
                tmp.color     = Color.white;
                tmp.fontStyle = FontStyles.Bold;
            }
        }

        private static void WireResolutionButtons()
        {
            var em = Object.FindObjectOfType<EncounterManager>();
            if (em == null) return;

            var approve = GameObject.Find("ApproveBtn")?.GetComponent<Button>();
            var deny    = GameObject.Find("DenyBtn")?.GetComponent<Button>();

            if (approve != null)
            {
                approve.onClick.RemoveAllListeners();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(approve.onClick, em.Approve);
                EditorUtility.SetDirty(approve);
            }
            if (deny != null)
            {
                deny.onClick.RemoveAllListeners();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(deny.onClick, em.Deny);
                EditorUtility.SetDirty(deny);
            }
        }

        // ── CardButton prefab auto-wire ───────────────────────

        private static void AutoWireCardButtonPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("CardButton t:Prefab");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("CardButton.prefab")) continue;

                var prefab = PrefabUtility.LoadPrefabContents(path);
                var view = prefab.GetComponent<CardButtonView>();
                if (view == null)
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                    continue;
                }

                var so = new SerializedObject(view);

                // Find children by name and assign them
                AssignTMPField(so, prefab, "_nameLabel",    "NameLabel");
                AssignTMPField(so, prefab, "_typeLabel",    "TypeLabel");
                AssignTMPField(so, prefab, "_costLabel",    "CostLabel");
                AssignTMPField(so, prefab, "_fatigueLabel", "FatigueLabel");

                // Button (component on root)
                var btn = prefab.GetComponent<Button>();
                if (btn != null)
                {
                    var p = so.FindProperty("_button");
                    if (p != null) p.objectReferenceValue = btn;
                }

                // Background (Image on root)
                var img = prefab.GetComponent<Image>();
                if (img != null)
                {
                    var p = so.FindProperty("_background");
                    if (p != null) p.objectReferenceValue = img;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                PrefabUtility.UnloadPrefabContents(prefab);
                Debug.Log($"[ShiftAutoLayout] Wired CardButton prefab: {path}");
            }
        }

        private static void AssignTMPField(SerializedObject so, GameObject prefab,
            string fieldName, string childName)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;

            // Search recursively for a child with this name that has TMP_Text
            var found = FindChildTMP(prefab.transform, childName);
            if (found != null)
                prop.objectReferenceValue = found;
        }

        private static TMP_Text FindChildTMP(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != name) continue;
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
            }
            return null;
        }

        // ── CardHandView auto-wire ────────────────────────────

        private static void AutoWireCardHandView()
        {
            var view = Object.FindObjectOfType<CardHandView>();
            if (view == null) return;

            var so = new SerializedObject(view);

            // _machine = scene's PunchCardMachine
            var machine = Object.FindObjectOfType<Desk42.RedTape.PunchCardMachine>();
            if (machine != null)
            {
                var p = so.FindProperty("_machine");
                if (p != null) p.objectReferenceValue = machine;
            }

            // _cardContainer = the CardHand RectTransform itself (the GameObject this is attached to,
            // OR a child named "CardHand" — typically the same object)
            var cardHand = GameObject.Find("CardHand");
            if (cardHand != null)
            {
                var p = so.FindProperty("_cardContainer");
                if (p != null) p.objectReferenceValue = cardHand.transform;
            }

            // _cardButtonPrefab = the CardButton.prefab asset
            string[] guids = AssetDatabase.FindAssets("CardButton t:Prefab");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("CardButton.prefab")) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var prefabView = prefab.GetComponent<CardButtonView>();
                if (prefabView != null)
                {
                    var p = so.FindProperty("_cardButtonPrefab");
                    if (p != null) p.objectReferenceValue = prefabView;
                    break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            Debug.Log("[ShiftAutoLayout] CardHandView wired.");
        }

        // ── FormCorruption threshold ──────────────────────────

        private static void AdjustFormCorruptionThreshold()
        {
            var fce = Object.FindObjectOfType<FormCorruptionEffect>();
            if (fce == null) return;

            var so = new SerializedObject(fce);
            var prop = so.FindProperty("_sanityThreshold");
            if (prop != null && prop.floatValue > 15f)
            {
                prop.floatValue = 15f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fce);
                Debug.Log("[ShiftAutoLayout] FormCorruption threshold lowered to 15 (was 35).");
            }
        }

        // ── Anchor helpers ────────────────────────────────────

        private static void ApplyAnchor(GameObject root, string childName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var t = root.transform.Find(childName);
            if (t == null) { Debug.LogWarning($"[ShiftAutoLayout] '{childName}' not found."); return; }
            var rt = t as RectTransform;
            if (rt == null) return;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void ApplyChild(Transform parent, string childName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var t = parent.Find(childName);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void ApplyStretchFull(GameObject root, string childName)
        {
            var t = root.transform.Find(childName);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void ApplyStretchInsets(Transform parent, string childName,
            float left, float top, float right, float bottom)
        {
            var t = parent.Find(childName);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void EnsureBackground(GameObject panel, Color color)
        {
            var t = panel.transform.Find("Background");
            GameObject bgGO;
            if (t == null)
            {
                bgGO = new GameObject("Background");
                bgGO.transform.SetParent(panel.transform, false);
                bgGO.AddComponent<Image>();
            }
            else
            {
                bgGO = t.gameObject;
                if (bgGO.GetComponent<Image>() == null) bgGO.AddComponent<Image>();
            }

            var rt = bgGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            bgGO.transform.SetAsFirstSibling();

            var img = bgGO.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void SetTextStyle(Transform parent, string childName,
            TextAlignmentOptions alignment, float fontSize, Color color)
        {
            var t = parent.Find(childName);
            if (t == null) return;
            var tmp = t.GetComponent<TMP_Text>();
            if (tmp == null) return;

            tmp.alignment           = alignment;
            tmp.fontSize            = fontSize;
            tmp.color               = color;
            tmp.enableWordWrapping  = true;
            tmp.overflowMode        = TextOverflowModes.Truncate;
        }
    }
}
#endif
