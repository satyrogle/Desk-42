// ============================================================
// DESK 42 — Vow Picker Panel (MonoBehaviour)
//
// After archetype pick, the player optionally chooses 0-3
// Compliance Vows. Each vow can be activated and bumped through
// ranks 1-3.
//
// Self-bootstrapping. Returns the chosen List<ActiveVow> via
// callback.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class VowPickerPanel : MonoBehaviour
    {
        // ── Vow Catalog ───────────────────────────────────────
        // Match VowId strings used by ComplianceVowSystem and existing modifiers.

        private static readonly (string Id, string Display, string Desc)[] _catalog =
        {
            ("zero_tolerance",            "Zero Tolerance",        "Soul damage +20% per rank."),
            ("double_quota",              "Double Quota",          "+2 claims required per rank."),
            ("hostile_environment",       "Hostile Environment",   "Timer drains 15% faster per rank."),
            ("mandatory_overtime",        "Mandatory Overtime",    "−30s shift duration per rank."),
            ("stricter_nondisclosure",    "Stricter NDAs",         "+2 cost on Redact/NonDisclosure per rank."),
            ("reply_all_chain",           "Reply-All Chain",       "Legal/Expedite cards inject CC'd Memos."),
            ("micro_transactions",        "Micro-Transactions",    "Clicks cost credits."),
            ("commission_only",           "Commission Only",       "Base payout halved/zeroed."),
            ("news_cycle_24h",            "24-Hour News Cycle",    "Events fire instantly, sometimes twice."),
            ("zero_based_budgeting",      "Zero-Based Budgeting",  "Costs paid in sanity, not credits."),
            ("cross_training_initiative", "Cross-Training",        "Cards transform into other archetypes'."),
            ("sunk_cost_fallacy",         "Sunk Cost Fallacy",     "Supplies can't be unequipped."),
            ("right_to_repair",           "Right to Repair",       "No meta-hub repair; sacrifice cards."),
            ("mandatory_mentorship",      "Mandatory Mentorship",  "Intern auto-plays cards."),
            ("agile_workflow",            "Agile Workflow",        "Timer freezes / accelerates with cards."),
            ("open_floor_plan",           "Open Floor Plan",       "Zone boundaries erased."),
            ("transparency_initiative",   "Transparency",          "NDAs disabled; Analyse destroys UI."),
            ("non_euclidean_filing",      "Non-Euclidean Filing",  "Deck visible; text redacted."),
        };

        private const int MAX_VOWS_PICKED = 3;

        // ── State ─────────────────────────────────────────────

        private GameObject _panelRoot;
        private RectTransform _grid;
        private TMP_Text _statusText;
        private Action<List<ActiveVow>> _onConfirmed;
        private readonly Dictionary<string, int> _selected = new(); // vowId → rank

        // ── Public API ────────────────────────────────────────

        public void Show(Action<List<ActiveVow>> onConfirmed)
        {
            if (_panelRoot == null) BuildUI();
            _onConfirmed = onConfirmed;
            _selected.Clear();
            RefreshStatus();
            _panelRoot.SetActive(true);
        }

        public void Hide()
        {
            _onConfirmed = null;
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        // ── UI ────────────────────────────────────────────────

        private void BuildUI()
        {
            _panelRoot = new GameObject("VowPickerCanvas");
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
            trt.sizeDelta = new Vector2(0, 60);
            trt.anchoredPosition = new Vector2(0, -20);
            var title = titleGO.AddComponent<TextMeshProUGUI>();
            title.text = "COMPLIANCE VOWS  (optional, max 3)";
            title.fontSize = AccessibilitySettings.Scaled(32); title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.95f, 0.85f, 0.50f);
            title.alignment = TextAlignmentOptions.Center;

            // Scrollable grid (simple — no scroll, fits 18 vows in 6x3)
            var gridGO = new GameObject("Grid");
            gridGO.transform.SetParent(_panelRoot.transform, false);
            _grid = gridGO.AddComponent<RectTransform>();
            _grid.anchorMin = new Vector2(0.5f, 0.5f); _grid.anchorMax = new Vector2(0.5f, 0.5f);
            _grid.pivot = new Vector2(0.5f, 0.5f);
            _grid.sizeDelta = new Vector2(1080, 720);
            _grid.anchoredPosition = new Vector2(0, -10);

            var glg = gridGO.AddComponent<GridLayoutGroup>();
            glg.cellSize        = new Vector2(255, 86);
            glg.spacing         = new Vector2(8, 8);
            glg.childAlignment  = TextAnchor.UpperCenter;
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;

            foreach (var v in _catalog)
                BuildVowTile(_grid, v.Id, v.Display, v.Desc);

            // Status + Confirm button (bottom)
            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(_panelRoot.transform, false);
            var srt = statusGO.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0); srt.anchorMax = new Vector2(0.5f, 0);
            srt.pivot = new Vector2(0.5f, 0);
            srt.sizeDelta = new Vector2(800, 40);
            srt.anchoredPosition = new Vector2(0, 110);
            _statusText = statusGO.AddComponent<TextMeshProUGUI>();
            _statusText.fontSize = AccessibilitySettings.Scaled(18); _statusText.color = Color.white;
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.raycastTarget = false;

            // Confirm button
            BuildBottomBtn("ConfirmBtn", "BEGIN SHIFT",
                new Color(0.20f, 0.50f, 0.30f),
                new Vector2(150, 40), OnConfirm);

            // Skip button (no vows)
            BuildBottomBtn("SkipBtn", "NO VOWS",
                new Color(0.30f, 0.30f, 0.35f),
                new Vector2(-150, 40), () => { _selected.Clear(); OnConfirm(); });

            // Clear all selections
            BuildBottomBtn("ClearBtn", "CLEAR ALL",
                new Color(0.50f, 0.30f, 0.20f),
                new Vector2(450, 40), ClearAllSelections);

            // Back
            BuildBottomBtn("BackBtn", "← BACK",
                new Color(0.25f, 0.25f, 0.30f),
                new Vector2(-450, 40), () => Hide());

            _panelRoot.SetActive(false);
        }

        private void ClearAllSelections()
        {
            _selected.Clear();
            // Repaint every tile
            for (int i = 0; i < _grid.childCount; i++)
            {
                var tile = _grid.GetChild(i);
                var img = tile.GetComponent<Image>();
                if (img != null) img.color = new Color(0.13f, 0.13f, 0.16f, 1f);
                var rankLbl = tile.Find("Rank")?.GetComponent<TMP_Text>();
                if (rankLbl != null)
                {
                    rankLbl.text = "—";
                    rankLbl.color = new Color(0.65f, 0.65f, 0.65f);
                }
            }
            RefreshStatus();
        }

        private void BuildVowTile(RectTransform parent, string id, string display, string desc)
        {
            var tile = new GameObject($"Vow_{id}");
            tile.transform.SetParent(parent, false);
            var img = tile.AddComponent<Image>();
            img.color = new Color(0.13f, 0.13f, 0.16f, 1f);

            var btn = tile.AddComponent<Button>();
            string capturedId = id;
            btn.onClick.AddListener(() => CycleVow(capturedId, img));

            // Name
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(tile.transform, false);
            var nrt = nameGO.AddComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0, 1); nrt.anchorMax = new Vector2(1, 1);
            nrt.pivot = new Vector2(0.5f, 1);
            nrt.sizeDelta = new Vector2(-16, 32);
            nrt.anchoredPosition = new Vector2(0, -8);
            var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
            nameTmp.text = display;
            nameTmp.fontSize = AccessibilitySettings.Scaled(16); nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = new Color(0.95f, 0.85f, 0.50f);
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
            nameTmp.raycastTarget = false;

            // Rank badge (right side)
            var badgeGO = new GameObject("Rank");
            badgeGO.transform.SetParent(tile.transform, false);
            var brt = badgeGO.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(1, 1); brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(1, 1);
            brt.sizeDelta = new Vector2(60, 32);
            brt.anchoredPosition = new Vector2(-8, -8);
            var badgeTmp = badgeGO.AddComponent<TextMeshProUGUI>();
            badgeTmp.text = "—";
            badgeTmp.fontSize = AccessibilitySettings.Scaled(14); badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.color = new Color(0.65f, 0.65f, 0.65f);
            badgeTmp.alignment = TextAlignmentOptions.MidlineRight;
            badgeTmp.raycastTarget = false;

            // Desc
            var descGO = new GameObject("Desc");
            descGO.transform.SetParent(tile.transform, false);
            var drt = descGO.AddComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(8, 8); drt.offsetMax = new Vector2(-8, -38);
            var descTmp = descGO.AddComponent<TextMeshProUGUI>();
            descTmp.text = desc;
            descTmp.fontSize = AccessibilitySettings.Scaled(11); descTmp.color = new Color(0.80f, 0.80f, 0.80f);
            descTmp.alignment = TextAlignmentOptions.TopLeft;
            descTmp.enableWordWrapping = true;
            descTmp.raycastTarget = false;
        }

        private void CycleVow(string id, Image bg)
        {
            int currentRank = _selected.TryGetValue(id, out var r) ? r : 0;
            int totalSelected = _selected.Count;

            if (currentRank == 0)
            {
                // Adding a new vow — check cap
                if (totalSelected >= MAX_VOWS_PICKED)
                {
                    _statusText.text = $"<color=#E84A4A>Max {MAX_VOWS_PICKED} vows.</color>";
                    return;
                }
                _selected[id] = 1;
            }
            else if (currentRank < 3)
            {
                _selected[id] = currentRank + 1;
            }
            else
            {
                _selected.Remove(id);
            }

            // Update badge
            var rankLabel = bg.transform.Find("Rank")?.GetComponent<TMP_Text>();
            if (rankLabel != null)
            {
                rankLabel.text = _selected.TryGetValue(id, out var rr) ? $"R{rr}" : "—";
                rankLabel.color = _selected.ContainsKey(id)
                    ? new Color(0.95f, 0.85f, 0.5f)
                    : new Color(0.65f, 0.65f, 0.65f);
            }

            // Tile color
            bg.color = _selected.ContainsKey(id)
                ? new Color(0.20f, 0.20f, 0.25f, 1f)
                : new Color(0.13f, 0.13f, 0.16f, 1f);

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_statusText == null) return;
            _statusText.text = $"{_selected.Count} / {MAX_VOWS_PICKED} vows selected";
        }

        private void OnConfirm()
        {
            var list = new List<ActiveVow>();
            foreach (var kvp in _selected)
                list.Add(new ActiveVow { VowId = kvp.Key, Rank = kvp.Value });

            var cb = _onConfirmed;
            Hide();
            cb?.Invoke(list);
        }

        private void BuildBottomBtn(string name, string label, Color color,
            Vector2 anchoredPos, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_panelRoot.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(220, 56);
            rt.anchoredPosition = anchoredPos;

            go.AddComponent<Image>().color = color;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var ltmp = lbl.AddComponent<TextMeshProUGUI>();
            ltmp.text = label; ltmp.fontSize = AccessibilitySettings.Scaled(18); ltmp.fontStyle = FontStyles.Bold;
            ltmp.color = Color.white; ltmp.alignment = TextAlignmentOptions.Center;
            ltmp.raycastTarget = false;
        }
    }
}
