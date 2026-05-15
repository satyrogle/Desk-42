// ============================================================
// DESK 42 — Archetype Picker Panel (MonoBehaviour)
//
// Shown by MainMenuController when "Start New Run" is clicked.
// Displays a grid of archetype tiles. On click, hides itself
// and invokes a callback with the selected archetypeId.
//
// Self-bootstrapping. Uses ArchetypeFactory.AllIds.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Archetypes;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class ArchetypePickerPanel : MonoBehaviour
    {
        private GameObject _panelRoot;
        private Action<string> _onPicked;

        public void Show(Action<string> onPicked)
        {
            if (_panelRoot == null) BuildUI();
            _onPicked = onPicked;
            _panelRoot.SetActive(true);
        }

        public void Hide()
        {
            _onPicked = null;
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        // ── UI ────────────────────────────────────────────────

        private void BuildUI()
        {
            _panelRoot = new GameObject("ArchetypePickerCanvas");
            _panelRoot.transform.SetParent(transform, false);

            var canvas = _panelRoot.AddComponent<Canvas>();
            var rt = _panelRoot.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;
            var scaler = _panelRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f; // Match width and height to fix overflow on ultra-wide
            _panelRoot.AddComponent<GraphicRaycaster>();

            var back = new GameObject("Backdrop");
            back.transform.SetParent(_panelRoot.transform, false);
            var brt = back.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            back.AddComponent<Image>().color = new Color(0, 0, 0, 0.92f);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(_panelRoot.transform, false);
            var trt = titleGO.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(0, 100);
            trt.anchoredPosition = new Vector2(0, -40);
            var title = titleGO.AddComponent<TextMeshProUGUI>();
            title.text = "CHOOSE YOUR ARCHETYPE";
            title.fontSize = AccessibilitySettings.Scaled(42); title.fontStyle = FontStyles.Bold;
            title.color = UIPalette.AccentTitle;
            title.alignment = TextAlignmentOptions.Center;

            // ScrollRect wrapper so content never overflows the canvas
            var scrollGO = new GameObject("Scroll");
            scrollGO.transform.SetParent(_panelRoot.transform, false);
            var scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.05f, 0.12f);
            scrollRT.anchorMax = new Vector2(0.95f, 0.88f);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;
            var scrollImg = scrollGO.AddComponent<Image>();
            scrollImg.color = new Color(0, 0, 0, 0.01f); // near-invisible mask bg
            scrollGO.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // Grid content inside the scroll
            var grid = new GameObject("Grid");
            grid.transform.SetParent(scrollGO.transform, false);
            var grt = grid.AddComponent<RectTransform>();
            grt.anchorMin = new Vector2(0, 1);
            grt.anchorMax = new Vector2(1, 1);
            grt.pivot     = new Vector2(0.5f, 1);
            grt.sizeDelta = new Vector2(0, 0); // ContentSizeFitter will set height

            scroll.content = grt;
            scroll.viewport = scrollRT;

            var glg = grid.AddComponent<GridLayoutGroup>();
            glg.cellSize        = new Vector2(240, 220);
            glg.spacing         = new Vector2(12, 12);
            glg.childAlignment  = TextAnchor.UpperCenter;
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;

            var csf = grid.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var id in ArchetypeFactory.AllIds)
                BuildTile(grid.transform, id);

            // Back button
            var backBtnGO = new GameObject("BackBtn");
            backBtnGO.transform.SetParent(_panelRoot.transform, false);
            var brt2 = backBtnGO.AddComponent<RectTransform>();
            brt2.anchorMin = new Vector2(0, 0); brt2.anchorMax = new Vector2(0, 0);
            brt2.pivot = new Vector2(0, 0);
            brt2.sizeDelta = new Vector2(180, 56);
            brt2.anchoredPosition = new Vector2(40, 40);
            backBtnGO.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.30f);
            var backBtn = backBtnGO.AddComponent<Button>();
            backBtn.onClick.AddListener(() => { Hide(); });

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(backBtnGO.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var ltmp = lbl.AddComponent<TextMeshProUGUI>();
            ltmp.text = "← BACK"; ltmp.fontSize = AccessibilitySettings.Scaled(18); ltmp.fontStyle = FontStyles.Bold;
            ltmp.color = Color.white; ltmp.alignment = TextAlignmentOptions.Center;
            ltmp.raycastTarget = false;

            _panelRoot.SetActive(false);
        }

        private void BuildTile(Transform parent, string archetypeId)
        {
            var tile = new GameObject($"Tile_{archetypeId}");
            tile.transform.SetParent(parent, false);

            var img = tile.AddComponent<Image>();
            img.color = new Color(0.13f, 0.13f, 0.16f, 1f);

            var btn = tile.AddComponent<Button>();
            string capturedId = archetypeId;
            btn.onClick.AddListener(() =>
            {
                var cb = _onPicked;
                Hide();
                cb?.Invoke(capturedId);
            });

            // Build a temp instance to get display info
            var arch = ArchetypeFactory.Create(archetypeId);

            // Name
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(tile.transform, false);
            var nrt = nameGO.AddComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0, 1); nrt.anchorMax = new Vector2(1, 1);
            nrt.pivot = new Vector2(0.5f, 1);
            nrt.sizeDelta = new Vector2(-20, 50);
            nrt.anchoredPosition = new Vector2(0, -16);
            var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
            nameTmp.text = arch.DisplayName;
            nameTmp.fontSize = AccessibilitySettings.Scaled(20); nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = UIPalette.AccentTitle;
            nameTmp.alignment = TextAlignmentOptions.Top;
            nameTmp.raycastTarget = false;

            // Ability description
            var descGO = new GameObject("Desc");
            descGO.transform.SetParent(tile.transform, false);
            var drt = descGO.AddComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(14, 16); drt.offsetMax = new Vector2(-14, -70);
            var descTmp = descGO.AddComponent<TextMeshProUGUI>();
            descTmp.text = arch.AbilityDescription;
            descTmp.fontSize = AccessibilitySettings.Scaled(12); descTmp.color = Color.white;
            descTmp.alignment = TextAlignmentOptions.TopLeft;
            descTmp.enableWordWrapping = true;
            descTmp.raycastTarget = false;
        }
    }
}
