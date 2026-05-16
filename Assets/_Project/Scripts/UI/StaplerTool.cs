// ============================================================
// DESK 42 — Stapler Tool (MonoBehaviour)
//
// Boss-fight only. A toggleable cursor mode. When active:
//   - Cursor visually shifts (text changes to "STAPLER ACTIVE")
//   - Left-click on any HRPopup pins it permanently. Pinned
//     popups can no longer be dragged and fade to 40% alpha,
//     visually defeated. They no longer obstruct the player.
//
// Toggled via a button pinned bottom-left of the boss canvas.
// Pinning is unlimited but each click only handles one popup,
// so the player still has to whack popups one at a time.
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class StaplerTool : MonoBehaviour
    {
        private RectTransform _canvasRT;
        private GameObject _toggleBtn;
        private Image _toggleImg;
        private TMP_Text _toggleLbl;
        private bool _active;

        private void Start()
        {
            _canvasRT = transform.parent as RectTransform;
            BuildToggle();
        }

        private void Update()
        {
            if (!_active) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // Skip clicks on UI elements that aren't popups
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition,
            };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current?.RaycastAll(pointer, results);

            foreach (var r in results)
            {
                var popup = r.gameObject.GetComponentInParent<HRPopup>();
                if (popup != null && !popup.IsPinned)
                {
                    popup.Pin();
                    return;
                }
            }
        }

        // ── Toggle UI ─────────────────────────────────────────

        private void BuildToggle()
        {
            _toggleBtn = new GameObject("StaplerToggle");
            _toggleBtn.transform.SetParent(_canvasRT, false);
            var rt = _toggleBtn.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot     = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(220, 60);
            rt.anchoredPosition = new Vector2(40, 40);

            _toggleImg = _toggleBtn.AddComponent<Image>();
            _toggleImg.color = new Color(0.20f, 0.20f, 0.25f, 0.95f);
            var btn = _toggleBtn.AddComponent<Button>();
            btn.onClick.AddListener(Toggle);

            var lblGO = new GameObject("Lbl");
            lblGO.transform.SetParent(_toggleBtn.transform, false);
            var lrt = lblGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8, 0);
            lrt.offsetMax = new Vector2(-8, 0);
            _toggleLbl = lblGO.AddComponent<TextMeshProUGUI>();
            _toggleLbl.text = "STAPLER";
            _toggleLbl.fontSize = AccessibilitySettings.Scaled(16);
            _toggleLbl.fontStyle = FontStyles.Bold;
            _toggleLbl.alignment = TextAlignmentOptions.Center;
            _toggleLbl.color = Color.white;
            _toggleLbl.raycastTarget = false;
        }

        private void Toggle()
        {
            _active = !_active;
            if (_active)
            {
                _toggleImg.color = new Color(0.55f, 0.45f, 0.10f, 1f);
                _toggleLbl.text  = "STAPLER ACTIVE";
            }
            else
            {
                _toggleImg.color = new Color(0.20f, 0.20f, 0.25f, 0.95f);
                _toggleLbl.text  = "STAPLER";
            }
        }
    }
}
