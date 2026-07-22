// ============================================================
// DESK 42 — Shift Scene Fixer (targeted)
//
// Fixes specific issues left over after Auto-Layout:
//   1. Disables interactability on gauge Sliders (player can't drag them)
//   2. Forces Approve/Deny to render on top with full opacity
//   3. Widens ClientInfo and re-lays out its children to prevent overlap
//   4. Disables stale UI overlays (MoralFlash, CorruptionOverlay, SanityEffects)
//      so they don't show until their effects actually trigger
//   5. Looks for any non-UI Renderer with a missing material in the scene
//      (the "pink blob") and disables it
//
// Run AFTER Auto-Layout Shift Scene.
//
// Usage: Tools → Desk 42 → Fix Shift Scene Issues
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.UI;

namespace Desk42.EditorTools
{
    public static class ShiftSceneFixer
    {
        [MenuItem("Tools/Desk 42/Fix Shift Scene Issues")]
        public static void Fix()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Shift")
            {
                EditorUtility.DisplayDialog("Fix",
                    $"Open Shift.unity first. Active scene: {scene.name}", "OK");
                return;
            }

            int ok = 0, failed = 0;
            void Step(string name, System.Action a)
            {
                try { a(); ok++; Debug.Log($"[ShiftSceneFixer] ✓ {name}"); }
                catch (System.Exception ex) { failed++; Debug.LogError($"[ShiftSceneFixer] ✗ {name}: {ex.Message}"); }
            }

            Step("DisableAllSliderInteractivity",       DisableAllSliderInteractivity);
            Step("FixApproveDenyVisibility",            FixApproveDenyVisibility);
            Step("FixClientInfoLayout",                 FixClientInfoLayout);
            Step("ReparentClientViewLabels",            ReparentClientViewLabels);
            Step("RebuildGaugeFromScratch:SoulGauge",   () => RebuildGaugeFromScratch("SoulGauge",   new Color(0.75f, 0.20f, 0.20f), "Soul"));
            Step("RebuildGaugeFromScratch:SanityGauge", () => RebuildGaugeFromScratch("SanityGauge", new Color(0.20f, 0.55f, 0.75f), "Sanity"));
            Step("DisableStaleOverlays",                DisableStaleOverlays);
            Step("DisableMissingMaterialRenderers",     DisableMissingMaterialRenderers);
            Step("EnsureMissingComponents",             EnsureMissingComponents);
            Step("AutoWireCorpOS",                      AutoWireCorpOS);
            Step("AutoWirePassiveAggressiveUIController", AutoWirePassiveAggressiveUIController);

            Debug.Log($"[ShiftSceneFixer] Summary: {ok} steps OK, {failed} failed.");

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[ShiftSceneFixer] Targeted fixes applied. Save the scene (Ctrl+S).");
            EditorUtility.DisplayDialog("Fix",
                "Fixes applied. Save scene (Ctrl+S), then play from Boot.", "OK");
        }

        /// <summary>
        /// Batch-safe targeted scene operation used by the confirmation-layer
        /// pass. It does not run the broader layout fixer or show dialogs.
        /// </summary>
        public static void EnsureConfirmationLayerAndSave()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Shift.unity", OpenSceneMode.Single);

            if (Object.FindObjectOfType<ShiftFeedbackOverlay>() == null)
            {
                var shiftUI = GameObject.Find("ShiftUI");
                if (shiftUI == null)
                    throw new System.InvalidOperationException("ShiftUI was not found.");
                shiftUI.AddComponent<ShiftFeedbackOverlay>();
            }

            if (Object.FindObjectOfType<CardSlamFeedback>() == null)
            {
                var go = new GameObject("CardSlamFeedback");
                go.AddComponent<CardSlamFeedback>();
            }

            if (Object.FindObjectOfType<CascadePresenter>() == null)
            {
                var go = new GameObject("CascadePresenter");
                go.AddComponent<CascadePresenter>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new System.InvalidOperationException("Could not save Shift.unity.");

            Debug.Log("[ShiftSceneFixer] Confirmation layer verified and saved.");
        }

        // ── 1. Disable Slider Interactivity ───────────────────

        private static void DisableAllSliderInteractivity()
        {
            int count = 0;
            foreach (var slider in Object.FindObjectsOfType<Slider>(includeInactive: true))
            {
                slider.interactable   = false;
                // Also disable raycast on the handle/fill so it doesn't capture clicks
                if (slider.fillRect != null)
                {
                    var fillImg = slider.fillRect.GetComponent<Image>();
                    if (fillImg != null) fillImg.raycastTarget = false;
                }
                if (slider.handleRect != null)
                    slider.handleRect.gameObject.SetActive(false); // no draggable handle

                EditorUtility.SetDirty(slider);
                count++;
            }
            Debug.Log($"[ShiftSceneFixer] Disabled interactivity on {count} Slider(s).");
        }

        // ── 2. Force Approve/Deny visibility ──────────────────

        private static void FixApproveDenyVisibility()
        {
            string[] names = { "ApproveBtn", "DenyBtn" };
            Color[]  cols  = { new(0.15f, 0.55f, 0.25f, 1f), new(0.65f, 0.20f, 0.20f, 1f) };

            for (int i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null) continue;

                // Render last (on top)
                go.transform.SetAsLastSibling();

                // Force opaque colors
                var img = go.GetComponent<Image>();
                if (img != null) img.color = cols[i];

                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    var c = btn.colors;
                    c.normalColor      = cols[i];
                    c.highlightedColor = Color.Lerp(cols[i], Color.white, 0.3f);
                    c.pressedColor     = Color.Lerp(cols[i], Color.black, 0.3f);
                    c.colorMultiplier  = 1f;
                    btn.colors = c;
                    btn.interactable = true;
                    EditorUtility.SetDirty(btn);
                }

                // Bring CanvasGroup alpha to 1 if present
                var cg = go.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }

                // Position above the CardHand, BELOW the ClaimPanel.
                // CardHand is at bottom y=24 with h=200 → top at y=224.
                // ClaimPanel bottom (after auto-layout shrink) sits ~y=500 from bottom.
                // Buttons go in the gap: y=240 places them just above the cards.
                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0);
                    rt.anchorMax = new Vector2(0.5f, 0);
                    rt.pivot     = new Vector2(0.5f, 0);
                    rt.sizeDelta = new Vector2(220, 64);
                    rt.anchoredPosition = new Vector2(i == 0 ? -150 : 150, 240);
                }

                EditorUtility.SetDirty(go);
                Debug.Log($"[ShiftSceneFixer] {names[i]} forced visible at y=280.");
            }
        }

        // ── 3. ClientInfo layout (VerticalLayoutGroup) ────────
        // Stack labels vertically: variant (bold) → mood (italic + indicator) → injection.
        // This makes ClientInfo behave like a clean text card that auto-grows.

        private static void FixClientInfoLayout()
        {
            var ci = GameObject.Find("ClientInfo");
            if (ci == null) return;

            // Container: top-center, fixed width, height auto-fit
            var rt = ci.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 1);
                rt.anchorMax = new Vector2(0.5f, 1);
                rt.pivot     = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(540, 110);
                rt.anchoredPosition = new Vector2(0, -110);
            }

            // Strip any old layout group
            foreach (var lg in ci.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(lg);

            // Add VerticalLayoutGroup — children auto-stacked top to bottom
            var vlg = ci.AddComponent<VerticalLayoutGroup>();
            vlg.padding              = new RectOffset(12, 12, 8, 8);
            vlg.spacing              = 4;
            vlg.childAlignment       = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;

            // Style each child label and let VLG position them
            StyleAndSizeChild(ci.transform, "VariantLabel",
                TextAlignmentOptions.Center, 18, Color.white, FontStyles.Bold, height: 28);

            StyleAndSizeChild(ci.transform, "MoodLabel",
                TextAlignmentOptions.Center, 15,
                new Color(0.95f, 0.85f, 0.50f), FontStyles.Italic, height: 22);

            StyleAndSizeChild(ci.transform, "InjectionLabel",
                TextAlignmentOptions.Center, 12,
                new Color(0.65f, 0.85f, 0.95f), FontStyles.Normal, height: 20);

            // MoodIndicator: hide it (vertical stack doesn't suit a small icon next to text)
            // The MoodLabel color carries the same info.
            var moodInd = ci.transform.Find("MoodIndicator");
            if (moodInd != null) moodInd.gameObject.SetActive(false);

            EditorUtility.SetDirty(ci);
            Debug.Log("[ShiftSceneFixer] ClientInfo: VerticalLayoutGroup applied.");
        }

        private static void StyleAndSizeChild(Transform parent, string childName,
            TextAlignmentOptions align, float fontSize, Color color, FontStyles style,
            float height)
        {
            var t = parent.Find(childName);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;

            // Clear any anchor weirdness — VLG controls positioning, not anchors
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 1);

            // Fixed-height layout element so VLG knows what to allocate
            var le = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight       = height;
            le.flexibleWidth   = 1;

            var tmp = t.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.alignment = align;
                tmp.fontSize  = fontSize;
                tmp.color     = color;
                tmp.fontStyle = style;
                tmp.enableWordWrapping = true;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.raycastTarget = false;
            }
        }

        // ── 4. Disable stale overlays ─────────────────────────

        private static void DisableStaleOverlays()
        {
            // These overlays are meant to flash on triggered events — they shouldn't
            // be visible at scene start. Disable their CanvasGroup or root.
            string[] overlays = { "MoralFlash", "CorruptionOverlay", "SanityEffects" };
            foreach (var name in overlays)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;

                var cg = go.GetComponent<CanvasGroup>();
                if (cg == null) cg = go.AddComponent<CanvasGroup>();
                cg.alpha          = 0f;
                cg.blocksRaycasts = false;
                cg.interactable   = false;

                EditorUtility.SetDirty(go);
                Debug.Log($"[ShiftSceneFixer] {name} hidden (alpha=0).");
            }
        }

        // ── 5. Disable Renderers with missing materials ──────

        private static void DisableMissingMaterialRenderers()
        {
            int disabled = 0;
            foreach (var r in Object.FindObjectsOfType<Renderer>(includeInactive: true))
            {
                bool hasMissing = false;
                foreach (var m in r.sharedMaterials)
                    if (m == null) { hasMissing = true; break; }

                if (hasMissing)
                {
                    r.enabled = false;
                    EditorUtility.SetDirty(r);
                    disabled++;
                    Debug.Log($"[ShiftSceneFixer] Disabled Renderer with missing material on '{r.gameObject.name}'.", r.gameObject);
                }
            }
            Debug.Log($"[ShiftSceneFixer] Disabled {disabled} Renderer(s) with missing materials.");
        }

        // ── Rebuild gauges from scratch ───────────────────────
        // Wipes children, rebuilds Background + FillArea/Fill + Label
        // with explicit visible sprites and contrasting colors.

        private static Sprite GetUISprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")
                ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        }

        private static void RebuildGaugeFromScratch(string gaugeName, Color fillColor, string displayName)
        {
            var g = GameObject.Find(gaugeName);
            if (g == null) { Debug.LogWarning($"[ShiftSceneFixer] {gaugeName} not in scene."); return; }

            var sprite = GetUISprite();

            // Wipe all children
            for (int i = g.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(g.transform.GetChild(i).gameObject);

            // Strip old Slider and Image components on root
            var oldSlider = g.GetComponent<Slider>();
            if (oldSlider != null) Object.DestroyImmediate(oldSlider);
            var oldImg = g.GetComponent<Image>();
            if (oldImg != null) Object.DestroyImmediate(oldImg);

            // Root needs RectTransform (already there) and an Image as the Background
            var bgImg = g.AddComponent<Image>();
            bgImg.sprite        = sprite;
            bgImg.type          = Image.Type.Simple;
            bgImg.color         = new Color(0.10f, 0.10f, 0.12f, 0.95f);
            bgImg.raycastTarget = false;

            // Fill Area (child) — stretches to fill, drives the visible bar
            var faGO = new GameObject("Fill Area");
            faGO.transform.SetParent(g.transform, false);
            var faRT = faGO.AddComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(3, 3);
            faRT.offsetMax = new Vector2(-3, -3);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(faGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            var fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite        = sprite;
            fillImg.type          = Image.Type.Simple;
            fillImg.color         = fillColor;
            fillImg.raycastTarget = false;

            // Slider
            var slider = g.AddComponent<Slider>();
            slider.fillRect       = fillRT;
            slider.handleRect     = null;
            slider.targetGraphic  = bgImg;
            slider.direction      = Slider.Direction.LeftToRight;
            slider.minValue       = 0f;
            slider.maxValue       = 1f;
            slider.value          = 1f; // start full
            slider.interactable   = false;
            slider.transition     = Selectable.Transition.None;

            // Label
            var lblGO = new GameObject(displayName + "Label");
            lblGO.transform.SetParent(g.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(8, 0);
            lblRT.offsetMax = new Vector2(-8, 0);

            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text          = $"{displayName} 100%";
            tmp.alignment     = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize      = 16;
            tmp.color         = Color.white;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.outlineWidth  = 0.2f;
            tmp.outlineColor  = Color.black;
            tmp.raycastTarget = false;

            // Order: bg(root) → fill area → label
            faGO.transform.SetSiblingIndex(0);
            lblGO.transform.SetSiblingIndex(1);

            EditorUtility.SetDirty(g);
            Debug.Log($"[ShiftSceneFixer] {gaugeName} rebuilt.");
        }

        // ── Reparent stray ClientView labels into ClientInfo ──

        private static void ReparentClientViewLabels()
        {
            var ci = GameObject.Find("ClientInfo");
            if (ci == null) return;

            // Use ClientView's serialized refs — robust to name duplicates
            var clientView = Object.FindObjectOfType<ClientView>();
            if (clientView == null)
            {
                Debug.LogWarning("[ShiftSceneFixer] No ClientView in scene; skipping reparent.");
                return;
            }

            var so = new SerializedObject(clientView);
            string[] propNames = { "_variantLabel", "_moodLabel", "_moodIndicator", "_injectionLabel" };
            foreach (var prop in propNames)
            {
                var p = so.FindProperty(prop);
                if (p == null || p.objectReferenceValue == null) continue;

                var comp = p.objectReferenceValue as Component;
                if (comp == null) continue;
                if (comp.transform.parent == ci.transform) continue;

                comp.transform.SetParent(ci.transform, false);
                Debug.Log($"[ShiftSceneFixer] Reparented '{comp.name}' (via {prop}) under ClientInfo.");
            }
        }

        // ── Add missing components to scene ───────────────────

        private static void EnsureMissingComponents()
        {
            // EntropyManagerDriver — anywhere in scene
            if (Object.FindObjectOfType<EntropyManagerDriver>() == null)
            {
                var go = new GameObject("EntropyManagerDriver");
                go.AddComponent<EntropyManagerDriver>();
                Debug.Log("[ShiftSceneFixer] Created EntropyManagerDriver.");
            }

            // ClickCostInterceptor — on the ShiftUI Canvas root
            var shiftUI = GameObject.Find("ShiftUI");
            if (shiftUI != null && shiftUI.GetComponent<Desk42.Core.ClickCostInterceptor>() == null)
            {
                shiftUI.AddComponent<Desk42.Core.ClickCostInterceptor>();
                Debug.Log("[ShiftSceneFixer] Added ClickCostInterceptor to ShiftUI.");
            }

            // RunSummaryPanel — drop on its own GameObject (Canvas built at runtime)
            if (Object.FindObjectOfType<RunSummaryPanel>() == null)
            {
                var go = new GameObject("RunSummaryPanel");
                go.AddComponent<RunSummaryPanel>();
                Debug.Log("[ShiftSceneFixer] Created RunSummaryPanel.");
            }

            // NotificationFeed — toasts for hazards/sanity/NDA/milestones/tide
            if (Object.FindObjectOfType<NotificationFeed>() == null)
            {
                var go = new GameObject("NotificationFeed");
                go.AddComponent<NotificationFeed>();
                Debug.Log("[ShiftSceneFixer] Created NotificationFeed.");
            }

            // MemoFeedUI — bottom-left memo list with click-to-detail
            if (Object.FindObjectOfType<MemoFeedUI>() == null)
            {
                var go = new GameObject("MemoFeedUI");
                go.AddComponent<MemoFeedUI>();
                Debug.Log("[ShiftSceneFixer] Created MemoFeedUI.");
            }

            // MoralDilemmaPanel — modal that appears on dilemma trigger
            if (Object.FindObjectOfType<MoralDilemmaPanel>() == null)
            {
                var go = new GameObject("MoralDilemmaPanel");
                go.AddComponent<MoralDilemmaPanel>();
                Debug.Log("[ShiftSceneFixer] Created MoralDilemmaPanel.");
            }

            // CardSlamFeedback — flash + label when a card is played
            if (Object.FindObjectOfType<CardSlamFeedback>() == null)
            {
                var go = new GameObject("CardSlamFeedback");
                go.AddComponent<CardSlamFeedback>();
                Debug.Log("[ShiftSceneFixer] Created CardSlamFeedback.");
            }

            // CascadePresenter — factual, step-by-step supply math readout
            if (Object.FindObjectOfType<CascadePresenter>() == null)
            {
                var go = new GameObject("CascadePresenter");
                go.AddComponent<CascadePresenter>();
                Debug.Log("[ShiftSceneFixer] Created CascadePresenter.");
            }

            // Re-enable ClientView's species/variant labels in case a prior fix
            // run had disabled them. ClientInfo container should always show.
            var clientView = Object.FindObjectOfType<ClientView>();
            if (clientView != null)
            {
                var so = new SerializedObject(clientView);
                foreach (var fieldName in new[]
                    { "_speciesLabel", "_variantLabel", "_moodLabel", "_injectionLabel" })
                {
                    var prop = so.FindProperty(fieldName);
                    if (prop?.objectReferenceValue is TMP_Text tmp && tmp != null)
                    {
                        if (!tmp.gameObject.activeSelf)
                        {
                            tmp.gameObject.SetActive(true);
                            Debug.Log($"[ShiftSceneFixer] Re-enabled ClientView.{fieldName}.");
                        }
                    }
                }
            }

            // FugueInputRandomizer — on a child of ShiftUI, with cardContainer wired
            var existingFugue = Object.FindObjectOfType<FugueInputRandomizer>();
            if (existingFugue == null && shiftUI != null)
            {
                var go = new GameObject("FugueInputRandomizer");
                go.transform.SetParent(shiftUI.transform, false);
                existingFugue = go.AddComponent<FugueInputRandomizer>();
                Debug.Log("[ShiftSceneFixer] Created FugueInputRandomizer.");
            }
            if (existingFugue != null)
            {
                var so = new SerializedObject(existingFugue);
                var prop = so.FindProperty("_cardContainer");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    var cardHand = GameObject.Find("CardHand");
                    if (cardHand != null)
                    {
                        prop.objectReferenceValue = cardHand.transform;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(existingFugue);
                        Debug.Log("[ShiftSceneFixer] Wired FugueInputRandomizer.cardContainer → CardHand.");
                    }
                }
            }
        }

        // ── Auto-wire CorpOS_DesktopManager ───────────────────

        private static void AutoWireCorpOS()
        {
            var corp = Object.FindObjectOfType<CorpOSWindowManager>();
            if (corp == null) return;

            var so = new SerializedObject(corp);

            var anchor = GameObject.Find("PopupAnchor");
            if (anchor != null)
            {
                var p = so.FindProperty("_popupAnchor");
                if (p != null) p.objectReferenceValue = anchor.transform;
            }

            var bsodOverlay = GameObject.Find("BSOD_Overlay");
            if (bsodOverlay != null)
            {
                var cg = bsodOverlay.GetComponent<CanvasGroup>();
                if (cg == null) cg = bsodOverlay.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                var p = so.FindProperty("_bsodOverlay");
                if (p != null) p.objectReferenceValue = cg;
            }

            var bsodText = GameObject.Find("BSOD_Text");
            if (bsodText != null)
            {
                var tmp = bsodText.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    var p = so.FindProperty("_bsodText");
                    if (p != null) p.objectReferenceValue = tmp;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(corp);
            Debug.Log("[ShiftSceneFixer] CorpOS_DesktopManager wired (popupAnchor, BSOD overlay/text).");
        }

        // ── Auto-wire PassiveAggressiveUIController ───────────

        private static void AutoWirePassiveAggressiveUIController()
        {
            var ctrl = Object.FindObjectOfType<PassiveAggressiveUIController>();
            if (ctrl == null)
            {
                Debug.LogWarning("[ShiftSceneFixer] No PassiveAggressiveUIController in scene.");
                return;
            }

            var so = new SerializedObject(ctrl);

            WireTMP(so, "_creditsLabel",        "CreditsLabel");
            WireTMP(so, "_phaseLabel",          "PhaseLabel");
            WireTMP(so, "_clientProgressLabel", "ClientProgressLabel");
            WireTMP(so, "_timerLabel",          "TimerLabel");
            WireTMP(so, "_soulLabel",           "SoulLabel");
            WireTMP(so, "_sanityLabel",         "SanityLabel");

            WireImage(so, "_timerFill", "TimerFill");

            WireSlider(so, "_soulSlider",   "SoulGauge");
            WireSlider(so, "_sanitySlider", "SanityGauge");

            // _soulFill: explicitly NULL it so the gradient code in
            // RefreshSoulGauge doesn't overwrite our rebuilt red fill.
            // Scene's _soulGradient evaluates to white at full soul, which
            // was hiding the colored slider fill.
            {
                var p = so.FindProperty("_soulFill");
                if (p != null) p.objectReferenceValue = null;
            }
            // Same for _sanityFill defensively
            {
                var p = so.FindProperty("_sanityFill");
                if (p != null) p.objectReferenceValue = null;
            }

            // _moralFlashGroup
            var moralFlash = GameObject.Find("MoralFlash");
            if (moralFlash != null)
            {
                var cg = moralFlash.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    var p = so.FindProperty("_moralFlashGroup");
                    if (p != null) p.objectReferenceValue = cg;
                }
            }

            // _corruptionOverlay
            var corruption = GameObject.Find("CorruptionOverlay");
            if (corruption != null)
            {
                var cg = corruption.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    var p = so.FindProperty("_corruptionOverlay");
                    if (p != null) p.objectReferenceValue = cg;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);
            Debug.Log("[ShiftSceneFixer] PassiveAggressiveUIController slots auto-wired.");
        }

        private static void WireTMP(SerializedObject so, string field, string goName)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;
            var go = GameObject.Find(goName);
            if (go == null) return;
            var tmp = go.GetComponent<TMP_Text>();
            if (tmp != null) prop.objectReferenceValue = tmp;
        }

        private static void WireImage(SerializedObject so, string field, string goName)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;
            var go = GameObject.Find(goName);
            if (go == null) return;
            var img = go.GetComponent<Image>();
            if (img != null) prop.objectReferenceValue = img;
        }

        private static void WireSlider(SerializedObject so, string field, string goName)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;
            var go = GameObject.Find(goName);
            if (go == null) return;
            var slider = go.GetComponent<Slider>();
            if (slider != null) prop.objectReferenceValue = slider;
        }

        // ── Helpers ───────────────────────────────────────────

        private static void ApplyChild(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var t = parent.Find(name);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            EditorUtility.SetDirty(rt);
        }

        private static void StyleText(Transform parent, string name,
            TextAlignmentOptions alignment, float fontSize, Color color, FontStyles style)
        {
            var t = parent.Find(name);
            if (t == null) return;
            var tmp = t.GetComponent<TMP_Text>();
            if (tmp == null) return;
            tmp.alignment          = alignment;
            tmp.fontSize           = fontSize;
            tmp.color              = color;
            tmp.fontStyle          = style;
            tmp.enableWordWrapping = true;
            tmp.overflowMode       = TextOverflowModes.Truncate;
            EditorUtility.SetDirty(tmp);
        }
    }
}
#endif
