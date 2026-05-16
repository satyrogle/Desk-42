// ============================================================
// DESK 42 — RAM Overload Meter (MonoBehaviour)
//
// Boss-fight only. Spawns:
//   - A RAM Usage meter pinned bottom-right of the canvas.
//   - A Recycle Bin drop target next to it.
//
// Dragging an HRPopup into the Recycle Bin fills the meter by
// a fixed percentage. When the meter hits 100%, OnOverload
// fires — the BossFightController calls FreezeDefenses on the
// other widgets, which stops the Log Out button running and
// stops popup spawn.
//
// Visible meter prevents the "overload-and-not-know" failure
// mode from the v2 plan.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class RAMOverloadMeter : MonoBehaviour
    {
        [SerializeField] private float _fillPerDrop = 0.18f;

        public Action OnOverload;

        private RectTransform _canvasRT;
        private float _fill;
        private bool _overloaded;
        private Image _fillBar;
        private TMP_Text _fillLabel;

        private void Start()
        {
            _canvasRT = transform.parent as RectTransform;
            BuildMeter();
            BuildRecycleBin();
        }

        // ── Drop API (called by RecycleBinDropTarget) ─────────

        public void RegisterTrash()
        {
            if (_overloaded) return;
            _fill = Mathf.Clamp01(_fill + _fillPerDrop);
            Refresh();

            if (_fill >= 1f)
            {
                _overloaded = true;
                OnOverload?.Invoke();
                if (_fillLabel != null) _fillLabel.text = "RAM: 100% — SYSTEM FROZEN";
            }
        }

        private void Refresh()
        {
            if (_fillBar != null) _fillBar.fillAmount = _fill;
            if (_fillLabel != null && !_overloaded)
                _fillLabel.text = $"RAM: {Mathf.RoundToInt(_fill * 100f)}%";
        }

        // ── UI ────────────────────────────────────────────────

        private void BuildMeter()
        {
            // Container
            var meter = new GameObject("RAMMeter");
            meter.transform.SetParent(_canvasRT, false);
            var rt = meter.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(380, 60);
            rt.anchoredPosition = new Vector2(-40, 200);

            meter.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.10f, 1f);

            // Fill bar (uses Image fill mode)
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(meter.transform, false);
            var frt = fillGO.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(4, 4);
            frt.offsetMax = new Vector2(-4, -4);
            _fillBar = fillGO.AddComponent<Image>();
            _fillBar.color = new Color(0.85f, 0.30f, 0.30f, 1f);
            _fillBar.type = Image.Type.Filled;
            _fillBar.fillMethod = Image.FillMethod.Horizontal;
            _fillBar.fillAmount = 0f;
            _fillBar.raycastTarget = false;

            // Label
            var lblGO = new GameObject("Lbl");
            lblGO.transform.SetParent(meter.transform, false);
            var lrt = lblGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _fillLabel = lblGO.AddComponent<TextMeshProUGUI>();
            _fillLabel.text = "RAM: 0%";
            _fillLabel.fontSize = AccessibilitySettings.Scaled(16);
            _fillLabel.fontStyle = FontStyles.Bold;
            _fillLabel.alignment = TextAlignmentOptions.Center;
            _fillLabel.color = Color.white;
            _fillLabel.raycastTarget = false;
        }

        private void BuildRecycleBin()
        {
            var bin = new GameObject("RecycleBin");
            bin.transform.SetParent(_canvasRT, false);
            var rt = bin.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(120, 120);
            rt.anchoredPosition = new Vector2(-40, 60);

            bin.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.20f, 1f);
            var drop = bin.AddComponent<RecycleBinDropTarget>();
            drop.Meter = this;

            var lblGO = new GameObject("Lbl");
            lblGO.transform.SetParent(bin.transform, false);
            var lrt = lblGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "RECYCLE\nBIN";
            tmp.fontSize = AccessibilitySettings.Scaled(14);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.80f, 0.80f, 0.80f);
            tmp.raycastTarget = false;
        }
    }

    // Drop target that increments the meter when a popup is dropped on it
    public sealed class RecycleBinDropTarget : MonoBehaviour, IDropHandler
    {
        public RAMOverloadMeter Meter;

        public void OnDrop(PointerEventData eventData)
        {
            if (Meter == null) return;
            var popup = eventData.pointerDrag?.GetComponent<HRPopup>();
            if (popup == null) return;

            Meter.RegisterTrash();
            Destroy(popup.gameObject);
        }
    }
}
