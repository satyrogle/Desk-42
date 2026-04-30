// ============================================================
// DESK 42 — Card Slam Feedback (MonoBehaviour)
//
// Listens to RumorMill.OnCardSlammed and provides immediate
// visual confirmation that the card was played:
//   - Pulse-flashes the card hand container (briefly tints white)
//   - Brief screen shake on critical-state slams (Litigious / Suspicious)
//
// Self-bootstrapping — drop on a GameObject. No Inspector wiring.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class CardSlamFeedback : MonoBehaviour
    {
        [SerializeField] private float _pulseDuration = 0.35f;
        [SerializeField] private float _flashAlpha    = 0.30f;

        private GameObject _flashRoot;
        private Image      _flashImage;
        private TMP_Text   _slamLabel;

        // ── Lifecycle ─────────────────────────────────────────

        private void OnEnable()  { RumorMill.OnCardSlammed += HandleSlam; }
        private void OnDisable() { RumorMill.OnCardSlammed -= HandleSlam; }
        private void Start()     { BuildUI(); }

        // ── Event Handler ─────────────────────────────────────

        private void HandleSlam(CardSlammedEvent e)
        {
            if (_flashRoot == null) BuildUI();

            // Flash + label
            string label = $"[SLAM]  {e.CardType.ToString().ToUpperInvariant()}";
            StopAllCoroutines();
            StartCoroutine(FlashAndLabel(label));
        }

        // ── UI Construction ───────────────────────────────────

        private void BuildUI()
        {
            // Own canvas at high sort order — drawn over hand area
            var canvasGO = new GameObject("CardSlamFlashCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Flash strip across the bottom (over the card hand)
            _flashRoot = new GameObject("Flash");
            _flashRoot.transform.SetParent(canvasGO.transform, false);
            var frt = _flashRoot.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0);
            frt.pivot     = new Vector2(0.5f, 0);
            frt.sizeDelta = new Vector2(0, 240);
            frt.anchoredPosition = Vector2.zero;
            _flashImage = _flashRoot.AddComponent<Image>();
            _flashImage.color = new Color(1f, 1f, 1f, 0f);
            _flashImage.raycastTarget = false;

            // "▶ CARDNAME" label centered above the flash strip
            var labelGO = new GameObject("SlamLabel");
            labelGO.transform.SetParent(canvasGO.transform, false);
            var lrt = labelGO.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 0); lrt.anchorMax = new Vector2(0.5f, 0);
            lrt.pivot     = new Vector2(0.5f, 0);
            lrt.sizeDelta = new Vector2(600, 60);
            lrt.anchoredPosition = new Vector2(0, 240);
            _slamLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _slamLabel.text = "";
            _slamLabel.fontSize = 32;
            _slamLabel.fontStyle = FontStyles.Bold;
            _slamLabel.color = new Color(1f, 1f, 1f, 0f);
            _slamLabel.alignment = TextAlignmentOptions.Center;
            _slamLabel.outlineWidth = 0.3f;
            _slamLabel.outlineColor = Color.black;
            _slamLabel.raycastTarget = false;
        }

        // ── Coroutine ─────────────────────────────────────────

        private IEnumerator FlashAndLabel(string label)
        {
            _slamLabel.text = label;

            float t = 0f;
            float halfDur = _pulseDuration * 0.5f;

            // Fade in
            while (t < halfDur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / halfDur;
                _flashImage.color = new Color(1f, 1f, 1f, k * _flashAlpha);
                _slamLabel.color  = new Color(1f, 1f, 1f, k);
                yield return null;
            }

            // Fade out
            t = 0f;
            while (t < halfDur)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - t / halfDur;
                _flashImage.color = new Color(1f, 1f, 1f, k * _flashAlpha);
                _slamLabel.color  = new Color(1f, 1f, 1f, k);
                yield return null;
            }

            _flashImage.color = new Color(1f, 1f, 1f, 0f);
            _slamLabel.color  = new Color(1f, 1f, 1f, 0f);
            _slamLabel.text   = "";
        }
    }
}
