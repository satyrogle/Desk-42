// ============================================================
// DESK 42 — Retirement Lounge Panel (MonoBehaviour)
//
// Modal cosmetic shop. Opened from the InternalAudit hub via
// the "OPEN RETIREMENT LOUNGE" button. Lists every CosmeticItem
// in RetirementCatalog with its AuditPoints cost. Owned items
// show "OWNED" instead of the price.
//
// Self-bootstrapping. Reads/writes MetaProgressData.Cosmetics
// (AuditPoints, UnlockedDeskThemes, UnlockedWallpapers,
// UnlockedMugSkins, UnlockedDeskOrnaments, UnlockedSupplyStickers).
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Core;
using Desk42.Meta.RetirementFund;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class RetirementLoungePanel : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text _pointsLabel;
        private Transform _itemContainer;

        public void Show()
        {
            if (_root == null) BuildUI();
            _root.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        // ── UI ────────────────────────────────────────────────

        private void BuildUI()
        {
            var canvasGO = new GameObject("RetirementLoungeCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Backdrop
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(canvasGO.transform, false);
            var brt = backdrop.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            backdrop.AddComponent<Image>().color = UIPalette.Backdrop;

            // Card
            var card = new GameObject("Card");
            card.transform.SetParent(canvasGO.transform, false);
            var crt = card.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(900, 720);
            card.AddComponent<Image>().color = UIPalette.CardBackground;

            // Title
            CreateLabel(card.transform, "RETIREMENT LOUNGE",
                new Vector2(0, 320), 32, FontStyles.Bold, UIPalette.AccentTitle);

            // AuditPoints label
            var ptsGO = new GameObject("Points");
            ptsGO.transform.SetParent(card.transform, false);
            var prt = ptsGO.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(600, 36);
            prt.anchoredPosition = new Vector2(0, 275);
            _pointsLabel = ptsGO.AddComponent<TextMeshProUGUI>();
            _pointsLabel.text = "AUDIT POINTS: 0";
            _pointsLabel.fontSize = AccessibilitySettings.Scaled(18);
            _pointsLabel.color = Color.white;
            _pointsLabel.alignment = TextAlignmentOptions.Center;
            _pointsLabel.raycastTarget = false;

            // Scroll viewport
            var scrollGO = new GameObject("Scroll");
            scrollGO.transform.SetParent(card.transform, false);
            var srt = scrollGO.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(820, 520);
            srt.anchoredPosition = new Vector2(0, -10);
            var maskImg = scrollGO.AddComponent<Image>();
            maskImg.color = new Color(0, 0, 0, 0.01f);
            scrollGO.AddComponent<Mask>().showMaskGraphic = false;

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var content = new GameObject("Content");
            content.transform.SetParent(scrollGO.transform, false);
            var ct = content.AddComponent<RectTransform>();
            ct.anchorMin = new Vector2(0, 1);
            ct.anchorMax = new Vector2(1, 1);
            ct.pivot = new Vector2(0.5f, 1);
            ct.sizeDelta = new Vector2(0, 0);
            scroll.content = ct;
            scroll.viewport = srt;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 8;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _itemContainer = content.transform;

            foreach (var item in RetirementCatalog.Items)
                BuildItemRow(item);

            // Close
            var closeBtn = new GameObject("Close");
            closeBtn.transform.SetParent(card.transform, false);
            var cbrt = closeBtn.AddComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(0.5f, 0);
            cbrt.anchorMax = new Vector2(0.5f, 0);
            cbrt.pivot = new Vector2(0.5f, 0);
            cbrt.sizeDelta = new Vector2(220, 56);
            cbrt.anchoredPosition = new Vector2(0, 24);
            closeBtn.AddComponent<Image>().color = new Color(0.20f, 0.20f, 0.25f);
            var cbtn = closeBtn.AddComponent<Button>();
            cbtn.onClick.AddListener(Hide);
            var clbl = new GameObject("Lbl");
            clbl.transform.SetParent(closeBtn.transform, false);
            var clrt = clbl.AddComponent<RectTransform>();
            clrt.anchorMin = Vector2.zero; clrt.anchorMax = Vector2.one;
            clrt.offsetMin = Vector2.zero; clrt.offsetMax = Vector2.zero;
            var ctmp = clbl.AddComponent<TextMeshProUGUI>();
            ctmp.text = "CLOSE";
            ctmp.fontSize = AccessibilitySettings.Scaled(20);
            ctmp.fontStyle = FontStyles.Bold;
            ctmp.alignment = TextAlignmentOptions.Center;
            ctmp.color = Color.white;
            ctmp.raycastTarget = false;

            _root = canvasGO;
        }

        private void BuildItemRow(CosmeticItem item)
        {
            var row = new GameObject($"Item_{item.Id}");
            row.transform.SetParent(_itemContainer, false);
            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 70);
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 70;

            row.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 1f);

            // Name + desc
            var nameTmp = CreateLabel(row.transform, item.DisplayName,
                new Vector2(8, 16), 18, FontStyles.Bold, UIPalette.AccentTitle);
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var nrt = nameTmp.rectTransform;
            nrt.anchorMin = new Vector2(0, 0.5f);
            nrt.anchorMax = new Vector2(0.7f, 1);
            nrt.offsetMin = new Vector2(12, 0);
            nrt.offsetMax = new Vector2(0, -4);

            var descTmp = CreateLabel(row.transform, $"[{item.Category}] {item.Description}",
                new Vector2(8, -14), 12, FontStyles.Italic,
                new Color(0.80f, 0.80f, 0.80f));
            descTmp.alignment = TextAlignmentOptions.MidlineLeft;
            descTmp.enableWordWrapping = true;
            var drt = descTmp.rectTransform;
            drt.anchorMin = new Vector2(0, 0);
            drt.anchorMax = new Vector2(0.7f, 0.5f);
            drt.offsetMin = new Vector2(12, 4);
            drt.offsetMax = new Vector2(0, 0);

            // Buy button
            var btnGO = new GameObject("Buy");
            btnGO.transform.SetParent(row.transform, false);
            var brt = btnGO.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(1, 0.5f);
            brt.anchorMax = new Vector2(1, 0.5f);
            brt.pivot = new Vector2(1, 0.5f);
            brt.sizeDelta = new Vector2(180, 50);
            brt.anchoredPosition = new Vector2(-12, 0);

            var btnImg = btnGO.AddComponent<Image>();
            var btn = btnGO.AddComponent<Button>();
            var btnLblGO = new GameObject("Lbl");
            btnLblGO.transform.SetParent(btnGO.transform, false);
            var lrt = btnLblGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var btnLbl = btnLblGO.AddComponent<TextMeshProUGUI>();
            btnLbl.fontSize = AccessibilitySettings.Scaled(16);
            btnLbl.fontStyle = FontStyles.Bold;
            btnLbl.alignment = TextAlignmentOptions.Center;
            btnLbl.color = Color.white;
            btnLbl.raycastTarget = false;

            CosmeticItem captured = item;
            btn.onClick.AddListener(() => TryBuy(captured, btnImg, btn, btnLbl));

            // Set initial state
            UpdateButtonState(item, btnImg, btn, btnLbl);
        }

        private void TryBuy(CosmeticItem item, Image img, Button btn, TMP_Text lbl)
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null || meta.Cosmetics == null) return;
            if (IsOwned(meta, item)) return;
            if (meta.Cosmetics.AuditPoints < item.Cost) return;

            meta.Cosmetics.AuditPoints -= item.Cost;
            AddToUnlockedSet(meta, item);
            SaveSystem.SaveMeta(meta);

            Refresh();
            UpdateButtonState(item, img, btn, lbl);
        }

        private void UpdateButtonState(CosmeticItem item, Image img, Button btn, TMP_Text lbl)
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null || meta.Cosmetics == null) return;

            if (IsOwned(meta, item))
            {
                img.color = new Color(0.20f, 0.45f, 0.25f);
                lbl.text = "OWNED";
                btn.interactable = false;
            }
            else if (meta.Cosmetics.AuditPoints < item.Cost)
            {
                img.color = new Color(0.30f, 0.20f, 0.20f);
                lbl.text = $"{item.Cost} AP";
                btn.interactable = false;
            }
            else
            {
                img.color = new Color(0.20f, 0.40f, 0.55f);
                lbl.text = $"BUY {item.Cost} AP";
                btn.interactable = true;
            }
        }

        private void Refresh()
        {
            var meta = GameManager.Instance?.Meta;
            if (meta?.Cosmetics != null && _pointsLabel != null)
                _pointsLabel.text = $"AUDIT POINTS: {meta.Cosmetics.AuditPoints}";
        }

        // ── Cosmetics bucket routing ──────────────────────────

        private static bool IsOwned(MetaProgressData meta, CosmeticItem item)
        {
            return item.Category switch
            {
                CosmeticCategory.DeskTheme       => meta.Cosmetics.UnlockedDeskThemes.Contains(item.Id),
                CosmeticCategory.Wallpaper       => meta.Cosmetics.UnlockedWallpapers.Contains(item.Id),
                CosmeticCategory.MugSkin         => meta.Cosmetics.UnlockedMugSkins.Contains(item.Id),
                CosmeticCategory.DeskOrnament    => meta.Cosmetics.UnlockedDeskOrnaments.Contains(item.Id),
                CosmeticCategory.SupplySticker   => meta.Cosmetics.UnlockedSupplyStickers.Contains(item.Id),
                _ => false,
            };
        }

        private static void AddToUnlockedSet(MetaProgressData meta, CosmeticItem item)
        {
            switch (item.Category)
            {
                case CosmeticCategory.DeskTheme:     meta.Cosmetics.UnlockedDeskThemes.Add(item.Id);    break;
                case CosmeticCategory.Wallpaper:     meta.Cosmetics.UnlockedWallpapers.Add(item.Id);    break;
                case CosmeticCategory.MugSkin:       meta.Cosmetics.UnlockedMugSkins.Add(item.Id);      break;
                case CosmeticCategory.DeskOrnament:  meta.Cosmetics.UnlockedDeskOrnaments.Add(item.Id); break;
                case CosmeticCategory.SupplySticker: meta.Cosmetics.UnlockedSupplyStickers.Add(item.Id); break;
            }
        }

        // ── Helpers ───────────────────────────────────────────

        private static TMP_Text CreateLabel(Transform parent, string text,
            Vector2 anchoredPos, float fontSize, FontStyles style, Color color)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(820, 36);
            rt.anchoredPosition = anchoredPos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = AccessibilitySettings.Scaled(fontSize);
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
