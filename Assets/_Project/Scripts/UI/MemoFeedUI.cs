// ============================================================
// DESK 42 — Memo Feed UI (MonoBehaviour)
//
// Bottom-left panel that lists memos as they're generated.
// Click a memo to expand its full text in a popup.
// Self-bootstrapping — drop on a GameObject and go.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class MemoFeedUI : MonoBehaviour
    {
        [SerializeField] private int   _maxVisible = 6;
        [SerializeField] private float _itemHeight = 32f;

        private RectTransform _listContainer;
        private GameObject    _detailPanel;
        private TMP_Text      _detailTitle;
        private TMP_Text      _detailBody;
        private readonly List<MemoEntry> _entries = new();

        private struct MemoEntry { public string Headline; public string Body; }

        // ── Lifecycle ─────────────────────────────────────────

        private void OnEnable()  { RumorMill.OnMemoGenerated += HandleMemo; }
        private void OnDisable() { RumorMill.OnMemoGenerated -= HandleMemo; }
        private void Start()     { BuildUI(); }

        private void HandleMemo(MemoGeneratedEvent e)
        {
            _entries.Insert(0, new MemoEntry { Headline = e.Headline, Body = e.Body });
            if (_entries.Count > _maxVisible) _entries.RemoveAt(_entries.Count - 1);
            Refresh();
        }

        // ── UI Construction ───────────────────────────────────

        private void BuildUI()
        {
            // Canvas (own — sortingOrder below NotificationFeed)
            var canvasGO = new GameObject("MemoCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // List container (bottom-left)
            var listGO = new GameObject("MemoList");
            listGO.transform.SetParent(canvasGO.transform, false);
            _listContainer = listGO.AddComponent<RectTransform>();
            _listContainer.anchorMin = new Vector2(0, 0);
            _listContainer.anchorMax = new Vector2(0, 0);
            _listContainer.pivot     = new Vector2(0, 0);
            _listContainer.sizeDelta = new Vector2(380, 0);
            _listContainer.anchoredPosition = new Vector2(20, 230); // above CardHand area

            var listBg = listGO.AddComponent<Image>();
            listBg.color = new Color(0.10f, 0.10f, 0.12f, 0.55f);
            listBg.raycastTarget = false;

            // Title strip
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(listGO.transform, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot     = new Vector2(0.5f, 1);
            titleRT.sizeDelta = new Vector2(0, 28);
            titleRT.anchoredPosition = Vector2.zero;
            var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "MEMOS";
            titleTmp.fontSize = AccessibilitySettings.Scaled(14);
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.85f, 0.80f, 0.60f);
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;

            // Detail popup (hidden by default)
            BuildDetailPanel(canvasGO.transform);
        }

        private void BuildDetailPanel(Transform parent)
        {
            _detailPanel = new GameObject("MemoDetail");
            _detailPanel.transform.SetParent(parent, false);
            var rt = _detailPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700, 480);

            var bg = _detailPanel.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.12f, 0.98f);

            // Title
            var t = new GameObject("DetailTitle");
            t.transform.SetParent(_detailPanel.transform, false);
            var trt = t.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.offsetMin = new Vector2(20, -60); trt.offsetMax = new Vector2(-20, -10);
            _detailTitle = t.AddComponent<TextMeshProUGUI>();
            _detailTitle.fontSize = AccessibilitySettings.Scaled(22); _detailTitle.fontStyle = FontStyles.Bold;
            _detailTitle.color = new Color(0.95f, 0.85f, 0.50f);
            _detailTitle.alignment = TextAlignmentOptions.TopLeft;

            // Body
            var b = new GameObject("DetailBody");
            b.transform.SetParent(_detailPanel.transform, false);
            var brt = b.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(20, 80); brt.offsetMax = new Vector2(-20, -70);
            _detailBody = b.AddComponent<TextMeshProUGUI>();
            _detailBody.fontSize = AccessibilitySettings.Scaled(16); _detailBody.color = Color.white;
            _detailBody.enableWordWrapping = true;

            // Close button
            var btn = new GameObject("Close");
            btn.transform.SetParent(_detailPanel.transform, false);
            var brt2 = btn.AddComponent<RectTransform>();
            brt2.anchorMin = new Vector2(0.5f, 0); brt2.anchorMax = new Vector2(0.5f, 0);
            brt2.pivot = new Vector2(0.5f, 0);
            brt2.sizeDelta = new Vector2(180, 48);
            brt2.anchoredPosition = new Vector2(0, 16);
            var bimg = btn.AddComponent<Image>();
            bimg.color = new Color(0.25f, 0.25f, 0.30f);
            var button = btn.AddComponent<Button>();
            button.onClick.AddListener(() => _detailPanel.SetActive(false));

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(btn.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var ltmp = lbl.AddComponent<TextMeshProUGUI>();
            ltmp.text = "CLOSE"; ltmp.fontSize = AccessibilitySettings.Scaled(18); ltmp.fontStyle = FontStyles.Bold;
            ltmp.color = Color.white; ltmp.alignment = TextAlignmentOptions.Center;
            ltmp.raycastTarget = false;

            _detailPanel.SetActive(false);
        }

        private void Refresh()
        {
            // Wipe existing item rows (keep title at index 0)
            for (int i = _listContainer.childCount - 1; i >= 1; i--)
                Destroy(_listContainer.GetChild(i).gameObject);

            // Resize container to fit items + title
            float h = 28 + _entries.Count * _itemHeight + 8;
            _listContainer.sizeDelta = new Vector2(380, h);

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var go = new GameObject($"Memo_{i}");
                go.transform.SetParent(_listContainer, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
                rt.pivot     = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, _itemHeight);
                rt.anchoredPosition = new Vector2(0, -28 - i * _itemHeight);

                var bg = go.AddComponent<Image>();
                bg.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);

                var btn = go.AddComponent<Button>();
                string body = entry.Body;
                string headline = entry.Headline;
                btn.onClick.AddListener(() => OpenDetail(headline, body));

                var lbl = new GameObject("Label");
                lbl.transform.SetParent(go.transform, false);
                var lrt = lbl.AddComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(10, 0); lrt.offsetMax = new Vector2(-10, 0);
                var tmp = lbl.AddComponent<TextMeshProUGUI>();
                tmp.text = "[Memo] " + entry.Headline;
                tmp.fontSize = AccessibilitySettings.Scaled(13); tmp.color = new Color(0.85f, 0.80f, 0.65f);
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.raycastTarget = false;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private void OpenDetail(string headline, string body)
        {
            _detailTitle.text = headline;
            _detailBody.text  = string.IsNullOrEmpty(body)
                ? "(no body text)"
                : body;
            _detailPanel.SetActive(true);
        }
    }
}
