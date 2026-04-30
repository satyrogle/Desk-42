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
            scaler.matchWidthOrHeight  = 1f; // match height — fits any aspect
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
            title.fontSize = 42; title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.95f, 0.85f, 0.50f);
            title.alignment = TextAlignmentOptions.Center;

            // Grid of tiles
            var grid = new GameObject("Grid");
            grid.transform.SetParent(_panelRoot.transform, false);
            var grt = grid.AddComponent<RectTransform>();
            grt.anchorMin = new Vector2(0.5f, 0.5f);
            grt.anchorMax = new Vector2(0.5f, 0.5f);
            grt.pivot     = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(1140, 540);
            grt.anchoredPosition = new Vector2(0, -20);

            var glg = grid.AddComponent<GridLayoutGroup>();
            glg.cellSize        = new Vector2(255, 240);
            glg.spacing         = new Vector2(16, 16);
            glg.childAlignment  = TextAnchor.MiddleCenter;
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;

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
            ltmp.text = "← BACK"; ltmp.fontSize = 18; ltmp.fontStyle = FontStyles.Bold;
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
            nameTmp.fontSize = 20; nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = new Color(0.95f, 0.85f, 0.50f);
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
            descTmp.fontSize = 12; descTmp.color = Color.white;
            descTmp.alignment = TextAlignmentOptions.TopLeft;
            descTmp.enableWordWrapping = true;
            descTmp.raycastTarget = false;
        }
    }
}
