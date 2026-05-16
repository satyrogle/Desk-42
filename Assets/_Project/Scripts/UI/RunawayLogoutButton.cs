// ============================================================
// DESK 42 — Runaway Logout Button (MonoBehaviour)
//
// The boss fight's primary win-state target. The Yes/Log Out
// button physically flees from the player's cursor.
//
// Fail-safes (from the v2 plan):
//   - Velocity damping near canvas edges so the button never
//     pins itself in a corner the player can't reach.
//   - 60-second timeout: the chase speed lerps to zero so the
//     button stops fleeing entirely. Guarantees a win.
//   - HoldToConfirm accessibility fallback: when the player has
//     AccessibilitySettings.HoldToConfirm enabled, the button
//     doesn't run — instead clicking + holding for 3 seconds
//     triggers the win directly.
//   - FreezeDefenses(): called by RAMOverloadMeter once the RAM
//     gauge hits 100%. Stops the button cold.
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
    public sealed class RunawayLogoutButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("How fast (px/s) the button flees the cursor at start.")]
        [SerializeField] private float _baseSpeed = 800f;

        [Tooltip("Radius from the cursor at which fleeing begins.")]
        [SerializeField] private float _fleeRadius = 240f;

        [Tooltip("Margin from the canvas edge below which damping kicks in.")]
        [SerializeField] private float _edgeMargin = 120f;

        [Tooltip("Seconds before the chase auto-slows to nothing.")]
        [SerializeField] private float _timeoutSeconds = 60f;

        [Tooltip("HoldToConfirm fallback — how long the player must hold.")]
        [SerializeField] private float _holdSeconds = 3f;

        public Action OnCaught;

        private RectTransform _rt;
        private RectTransform _canvasRT;
        private Button _btn;
        private TMP_Text _holdLabel;
        private float _elapsed;
        private bool _frozen;
        private bool _holding;
        private float _holdTimer;

        // ── Lifecycle ─────────────────────────────────────────

        private void Awake()
        {
            BuildUI();
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            // Hold-to-confirm fallback: completely separate from the
            // chase logic. If the player has it enabled, the button
            // doesn't move and we resolve on hold.
            if (AccessibilitySettings.HoldToConfirm)
            {
                if (_holding)
                {
                    _holdTimer += Time.unscaledDeltaTime;
                    UpdateHoldLabel(_holdTimer / _holdSeconds);
                    if (_holdTimer >= _holdSeconds) Catch();
                }
                else
                {
                    _holdTimer = 0f;
                    UpdateHoldLabel(0f);
                }
                return;
            }

            // Frozen by RAM overload — stand still, become clickable.
            if (_frozen) return;

            // Time-based slowdown so the chase eventually relents
            float t = Mathf.Clamp01(_elapsed / _timeoutSeconds);
            float speed = Mathf.Lerp(_baseSpeed, 0f, t);

            FleeFromCursor(speed);
        }

        public void FreezeDefenses()
        {
            _frozen = true;
            if (_holdLabel != null) _holdLabel.text = "[CATCHABLE — CLICK TO LOG OUT]";
        }

        // ── Flee logic ────────────────────────────────────────

        private void FleeFromCursor(float speed)
        {
            if (_rt == null || _canvasRT == null) return;

            // Convert mouse to local canvas position
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, Input.mousePosition, null, out Vector2 mouseLocal))
                return;

            Vector2 myPos = _rt.anchoredPosition;
            Vector2 delta = myPos - mouseLocal;
            float dist = delta.magnitude;
            if (dist >= _fleeRadius || dist < 0.001f) return;

            Vector2 dir = delta / dist;
            Vector2 next = myPos + dir * speed * Time.unscaledDeltaTime;

            // Edge damping: as the button approaches a canvas edge,
            // reduce the perpendicular component so it doesn't pin
            // itself in a corner the player can't physically reach.
            Vector2 half = _canvasRT.rect.size * 0.5f;
            float distLeft   = (next.x + half.x);
            float distRight  = (half.x - next.x);
            float distBottom = (next.y + half.y);
            float distTop    = (half.y - next.y);

            float damp = 1f;
            damp = Mathf.Min(damp, Mathf.Clamp01(distLeft   / _edgeMargin));
            damp = Mathf.Min(damp, Mathf.Clamp01(distRight  / _edgeMargin));
            damp = Mathf.Min(damp, Mathf.Clamp01(distBottom / _edgeMargin));
            damp = Mathf.Min(damp, Mathf.Clamp01(distTop    / _edgeMargin));

            Vector2 damped = Vector2.Lerp(myPos, next, damp);

            // Hard clamp to keep the button inside the canvas no matter what
            damped.x = Mathf.Clamp(damped.x, -half.x + _rt.sizeDelta.x * 0.5f,
                                              half.x - _rt.sizeDelta.x * 0.5f);
            damped.y = Mathf.Clamp(damped.y, -half.y + _rt.sizeDelta.y * 0.5f,
                                              half.y - _rt.sizeDelta.y * 0.5f);
            _rt.anchoredPosition = damped;
        }

        // ── Click + hold ──────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            if (AccessibilitySettings.HoldToConfirm)
            {
                _holding = true;
                return;
            }
            Catch();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _holding = false;
        }

        private void UpdateHoldLabel(float progress)
        {
            if (_holdLabel == null) return;
            if (progress <= 0f)
                _holdLabel.text = "[HOLD TO LOG OUT]";
            else
                _holdLabel.text = $"[HOLDING {Mathf.RoundToInt(progress * 100f)}%]";
        }

        private void Catch()
        {
            OnCaught?.Invoke();
            Destroy(gameObject);
        }

        // ── UI ────────────────────────────────────────────────

        private void BuildUI()
        {
            // Find the BossFight canvas
            _canvasRT = transform.parent as RectTransform;
            if (_canvasRT == null)
            {
                _canvasRT = GetComponentInParent<Canvas>()?.transform as RectTransform;
            }

            _rt = gameObject.AddComponent<RectTransform>();
            _rt.anchorMin = new Vector2(0.5f, 0.5f);
            _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot     = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = new Vector2(220, 64);
            _rt.anchoredPosition = new Vector2(0, 80);

            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0.55f, 0.20f, 0.20f, 1f);
            img.raycastTarget = true;

            _btn = gameObject.AddComponent<Button>();
            _btn.targetGraphic = img;

            // Body label
            var lblGO = new GameObject("Lbl");
            lblGO.transform.SetParent(transform, false);
            var lrt = lblGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "YES, LOG OUT";
            tmp.fontSize = AccessibilitySettings.Scaled(18);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            // Hold-to-confirm tooltip label (positioned above the button)
            var holdGO = new GameObject("HoldLbl");
            holdGO.transform.SetParent(transform, false);
            var hrt = holdGO.AddComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 1);
            hrt.anchorMax = new Vector2(0.5f, 1);
            hrt.pivot = new Vector2(0.5f, 0);
            hrt.sizeDelta = new Vector2(360, 28);
            hrt.anchoredPosition = new Vector2(0, 4);
            _holdLabel = holdGO.AddComponent<TextMeshProUGUI>();
            _holdLabel.fontSize = AccessibilitySettings.Scaled(12);
            _holdLabel.alignment = TextAlignmentOptions.Center;
            _holdLabel.color = new Color(0.9f, 0.7f, 0.5f);
            _holdLabel.raycastTarget = false;
            _holdLabel.text = AccessibilitySettings.HoldToConfirm
                ? "[HOLD TO LOG OUT]" : "";
        }
    }
}
