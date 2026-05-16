// ============================================================
// DESK 42 — Draggable Client Portrait (MonoBehaviour)
//
// Layer 3 / Sprint 3 "Dark Economy" affordance. Attached to the
// ClientView in the Shift scene. When the player has reached
// Phase 4 (full game), they can pick the client up and drag the
// portrait toward the Pneumatic Tube. The original ClientView
// stays put; this component spawns a ghost that follows the
// cursor, and on drop the PneumaticTube checks the pointerDrag
// to identify the source.
//
// On drop outside the tube the ghost just fades out and nothing
// happens — the client encounter continues normally.
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class DraggableClientPortrait : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Phase required for portrait drag (Layer 3 content).")]
        [SerializeField] private int _requiredPhase = 4;

        private GameObject _ghost;
        private RectTransform _ghostRT;
        private Canvas _ghostCanvas;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!(GameManager.Instance?.IsPhaseUnlocked(_requiredPhase) ?? false))
            {
                eventData.pointerDrag = null;
                return;
            }

            // Find the parent Canvas so we can attach the ghost there
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                eventData.pointerDrag = null;
                return;
            }

            // Build a translucent ghost copy that follows the cursor
            _ghost = new GameObject("ClientPortraitGhost");
            _ghost.transform.SetParent(canvas.transform, false);
            _ghostRT = _ghost.AddComponent<RectTransform>();
            _ghostRT.sizeDelta = new Vector2(140, 140);

            var img = _ghost.AddComponent<Image>();
            img.color = new Color(0.85f, 0.30f, 0.50f, 0.7f);
            img.raycastTarget = false;

            _ghostCanvas = canvas;
            UpdateGhostPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghostRT == null) return;
            UpdateGhostPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostRT = null;
            _ghostCanvas = null;
        }

        private void UpdateGhostPosition(PointerEventData eventData)
        {
            if (_ghostCanvas == null || _ghostRT == null) return;

            var parentRT = _ghostCanvas.transform as RectTransform;
            if (parentRT == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, eventData.position, eventData.pressEventCamera,
                out Vector2 local))
            {
                _ghostRT.anchoredPosition = local;
            }
        }
    }
}
