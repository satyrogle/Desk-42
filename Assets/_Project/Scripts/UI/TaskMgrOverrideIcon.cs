// ============================================================
// DESK 42 — TaskMgr_Override Desktop Icon (MonoBehaviour)
//
// Sits on the InternalAudit hub. Hidden until the player has
// assembled enough .exe fragments. Once visible, double-click
// launches the BossFightController (the Layer 4 finale).
//
// Self-bootstrapping. Auto-attached by HighImpactInjector's
// "Inject Audit Hub" tool to the HubManager.
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Core;
using Desk42.Meta.BossFight;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class TaskMgrOverrideIcon : MonoBehaviour, IPointerClickHandler
    {
        private GameObject _icon;
        private TMP_Text _statusLabel;

        private void Start()
        {
            BuildUI();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null || _icon == null) return;

            bool ready = TaskMgrOverride.IsAssembled(meta);
            _icon.SetActive(ready || meta.ExeFragmentsCollected > 0);

            if (_statusLabel != null)
            {
                if (ready)
                    _statusLabel.text = "TaskMgr_Override.exe\n<color=#5BFF7A>READY</color>";
                else
                    _statusLabel.text =
                        $"TaskMgr_Override.exe\n<color=#888>{meta.ExeFragmentsCollected}/{TaskMgrOverride.FragmentsRequired} fragments</color>";
            }
        }

        // ── Double-click launch ───────────────────────────────

        private float _lastClickTime = -1f;
        private const float DOUBLE_CLICK_WINDOW = 0.4f;

        public void OnPointerClick(PointerEventData eventData)
        {
            var meta = GameManager.Instance?.Meta;
            if (!TaskMgrOverride.IsAssembled(meta)) return;

            float now = Time.unscaledTime;
            if (now - _lastClickTime <= DOUBLE_CLICK_WINDOW)
            {
                Launch();
                _lastClickTime = -1f;
            }
            else
            {
                _lastClickTime = now;
            }
        }

        private void Launch()
        {
            var canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null) return;

            var fightGO = new GameObject("BossFightController");
            fightGO.transform.SetParent(canvas.transform, false);
            fightGO.AddComponent<BossFightController>();
        }

        // ── UI ────────────────────────────────────────────────

        private void BuildUI()
        {
            var canvas = GetComponentInChildren<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _icon = new GameObject("TaskMgrOverrideIcon");
            _icon.transform.SetParent(canvas.transform, false);
            var rt = _icon.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot     = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(220, 90);
            rt.anchoredPosition = new Vector2(40, 100);

            _icon.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 0.95f);
            // Re-broadcast clicks to this component
            var click = _icon.AddComponent<ClickProxy>();
            click.Target = this;

            var lbl = new GameObject("Lbl");
            lbl.transform.SetParent(_icon.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8, 8);
            lrt.offsetMax = new Vector2(-8, -8);
            _statusLabel = lbl.AddComponent<TextMeshProUGUI>();
            _statusLabel.fontSize = AccessibilitySettings.Scaled(14);
            _statusLabel.alignment = TextAlignmentOptions.Center;
            _statusLabel.fontStyle = FontStyles.Bold;
            _statusLabel.color = new Color(0.65f, 0.80f, 1f);
            _statusLabel.raycastTarget = false;
        }

        // ── Click forwarder ───────────────────────────────────
        // The icon GameObject is the click surface, but the
        // bootstrap component lives on the HubManager. This
        // forwarder routes pointer events back to us.

        private sealed class ClickProxy : MonoBehaviour, IPointerClickHandler
        {
            public TaskMgrOverrideIcon Target;
            public void OnPointerClick(PointerEventData eventData)
            {
                Target?.OnPointerClick(eventData);
            }
        }
    }
}
