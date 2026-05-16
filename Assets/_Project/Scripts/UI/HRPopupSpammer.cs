// ============================================================
// DESK 42 — HR Popup Spammer (MonoBehaviour)
//
// During the boss fight, CorpOS retaliates by spawning
// uncloseable HR pop-ups: exit surveys, mandatory training,
// pizza party reminders. They have no X button. The only ways
// to deal with them are:
//   - Pin them with the StaplerTool (cosmetic dismissal)
//   - Drag them into the RecycleBin to fill the RAM meter
//
// Spawns at an accelerating rate. The boss fight controller can
// Cease() to stop spawning when the win triggers.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class HRPopupSpammer : MonoBehaviour
    {
        [SerializeField] private float _initialInterval = 3.0f;
        [SerializeField] private float _minInterval     = 0.6f;
        [SerializeField] private float _rampSeconds     = 45f;

        private static readonly string[] _slogans =
        {
            "Please complete this 500-question exit survey before logging out.",
            "A pizza party is scheduled for 2099. Don't miss it!",
            "Reminder: your timesheet for Q83 is overdue.",
            "Mandatory training: 'Synergy in the Existential Workplace'.",
            "Your performance review has been rescheduled to NEVER.",
            "Annual flu shot now available in Sector 4G. Bring exact change.",
            "Please rate your coworker on a scale of 1-7. No 4s.",
            "URGENT: Your stapler has been recalled.",
            "HR Note: All sighing has been logged.",
            "Please confirm you are not currently dissociating.",
        };

        private RectTransform _canvasRT;
        private bool _frozen;
        private bool _ceased;
        private readonly List<GameObject> _spawned = new();
        private float _startTime;

        private void Start()
        {
            _canvasRT = transform.parent as RectTransform;
            _startTime = Time.unscaledTime;
            StartCoroutine(SpamLoop());
        }

        public void FreezeDefenses()
        {
            _frozen = true;
        }

        public void Cease()
        {
            _ceased = true;
            StopAllCoroutines();
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
        }

        // ── Spawn loop ────────────────────────────────────────

        private IEnumerator SpamLoop()
        {
            while (!_ceased)
            {
                if (_frozen)
                {
                    yield return new WaitForSecondsRealtime(0.25f);
                    continue;
                }

                float t = Mathf.Clamp01((Time.unscaledTime - _startTime) / _rampSeconds);
                float interval = Mathf.Lerp(_initialInterval, _minInterval, t);
                yield return new WaitForSecondsRealtime(interval);

                if (!_frozen && !_ceased) SpawnOne();
            }
        }

        private void SpawnOne()
        {
            if (_canvasRT == null) return;

            var go = new GameObject("HRPopup");
            go.transform.SetParent(_canvasRT, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360, 140);

            // Random position inside canvas (keeping clear of edges)
            Vector2 size = _canvasRT.rect.size * 0.4f;
            rt.anchoredPosition = new Vector2(
                Random.Range(-size.x, size.x),
                Random.Range(-size.y, size.y));

            go.AddComponent<Image>().color = new Color(0.92f, 0.92f, 0.85f, 1f);

            // Title bar
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(go.transform, false);
            var trt = titleGO.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(0, 28);
            titleGO.AddComponent<Image>().color = new Color(0.20f, 0.30f, 0.55f);

            var titleLbl = new GameObject("TitleLbl");
            titleLbl.transform.SetParent(titleGO.transform, false);
            var tlrt = titleLbl.AddComponent<RectTransform>();
            tlrt.anchorMin = Vector2.zero;
            tlrt.anchorMax = Vector2.one;
            tlrt.offsetMin = new Vector2(8, 0);
            tlrt.offsetMax = new Vector2(-8, 0);
            var titleTmp = titleLbl.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Human Resources";
            titleTmp.fontSize = AccessibilitySettings.Scaled(14);
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.color = Color.white;
            titleTmp.raycastTarget = false;

            // Body text
            var bodyGO = new GameObject("Body");
            bodyGO.transform.SetParent(go.transform, false);
            var brt = bodyGO.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(10, 10);
            brt.offsetMax = new Vector2(-10, -34);
            var bodyTmp = bodyGO.AddComponent<TextMeshProUGUI>();
            bodyTmp.text = _slogans[Random.Range(0, _slogans.Length)];
            bodyTmp.fontSize = AccessibilitySettings.Scaled(13);
            bodyTmp.color = Color.black;
            bodyTmp.alignment = TextAlignmentOptions.TopLeft;
            bodyTmp.enableWordWrapping = true;
            bodyTmp.raycastTarget = false;

            // Tag the popup so Stapler + Recycle Bin can identify it
            go.AddComponent<HRPopup>();
            _spawned.Add(go);
        }
    }

    // Marker + draggable so the Recycle Bin / Stapler can act on it
    public sealed class HRPopup : MonoBehaviour,
        UnityEngine.EventSystems.IBeginDragHandler,
        UnityEngine.EventSystems.IDragHandler,
        UnityEngine.EventSystems.IEndDragHandler
    {
        public bool IsPinned { get; private set; }

        private RectTransform _rt;
        private CanvasGroup _cg;
        private Vector2 _startPos;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _cg = gameObject.AddComponent<CanvasGroup>();
        }

        public void Pin()
        {
            IsPinned = true;
            if (_cg != null) _cg.alpha = 0.4f;
            // Pinned popups stay on screen but can no longer be dragged
        }

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (IsPinned)
            {
                eventData.pointerDrag = null;
                return;
            }
            _startPos = _rt.anchoredPosition;
            if (_cg != null) _cg.blocksRaycasts = false;
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (IsPinned) return;
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                _rt.anchoredPosition = local;
            }
        }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_cg != null) _cg.blocksRaycasts = true;
        }
    }
}
