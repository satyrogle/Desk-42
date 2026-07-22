// ============================================================
// DESK 42 — Card Slam Feedback (MonoBehaviour)
//
// Listens to RumorMill.OnCardSlammed and provides immediate
// semantic confirmation of every card attempt. The latest result stays
// visible until another attempt replaces it. FeedbackBudget may suppress
// the optional flash, but never the result or a failure reason.
//
// Self-bootstrapping — drop on a GameObject. No Inspector wiring.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Desk42.Accessibility;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class CardSlamFeedback : RumorMillListener
    {
        [SerializeField] private float _pulseDuration = 0.35f;
        [SerializeField] private float _flashAlpha    = 0.30f;

        private GameObject _flashRoot;
        private Image      _flashImage;
        private GameObject _resultPanel;
        private TMP_Text   _slamLabel;
        private string     _latestAppliedText;
        private string     _activePreviewCardId;

        // ── Lifecycle ─────────────────────────────────────────

        protected override void Subscribe()
        {
            RumorMill.OnCardSlammed += HandleSlam;
            RumorMill.OnCardPreview += HandlePreview;
        }

        protected override void Unsubscribe()
        {
            RumorMill.OnCardSlammed -= HandleSlam;
            RumorMill.OnCardPreview -= HandlePreview;
        }
        private void Start()
        {
            if (_flashRoot == null) BuildUI();
        }

        // ── Event Handler ─────────────────────────────────────

        private void HandleSlam(CardSlammedEvent e)
        {
            if (_flashRoot == null) BuildUI();

            _activePreviewCardId = null;
            _latestAppliedText = FormatResult(e.Result);
            _resultPanel.SetActive(true);
            _slamLabel.text = _latestAppliedText;
            _slamLabel.color = Color.white;

            // Only the cosmetic pulse is budgeted. The semantic result above
            // is persistent and cannot be dropped.
            if (!AccessibilitySettings.ReducedMotion
                && FeedbackBudget.RequestBurst(FeedbackKind.Flash))
            {
                StopAllCoroutines();
                StartCoroutine(FlashVisual());
            }
        }

        private void HandlePreview(CardPreviewEvent e)
        {
            if (_flashRoot == null) BuildUI();

            if (e.IsVisible)
            {
                _activePreviewCardId = e.CardInstanceId;
                _resultPanel.SetActive(true);
                _slamLabel.text = FormatProjection(e.Projection);
                _slamLabel.color = new Color(0.92f, 0.84f, 0.58f, 1f);
                return;
            }

            if (_activePreviewCardId != e.CardInstanceId) return;
            _activePreviewCardId = null;
            _slamLabel.text = _latestAppliedText ?? "";
            _slamLabel.color = Color.white;
            _resultPanel.SetActive(!string.IsNullOrWhiteSpace(_latestAppliedText));
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

            // Persistent latest-result panel above the card hand.
            _resultPanel = new GameObject("LatestCardResult");
            _resultPanel.transform.SetParent(canvasGO.transform, false);
            var panelRt = _resultPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0);
            panelRt.anchorMax = new Vector2(0.5f, 0);
            panelRt.pivot = new Vector2(0.5f, 0);
            panelRt.sizeDelta = new Vector2(760, 138);
            panelRt.anchoredPosition = new Vector2(0, 248);
            var panelImage = _resultPanel.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.12f, 0.14f, 0.94f);
            panelImage.raycastTarget = false;
            var outline = _resultPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.64f, 0.38f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var labelGO = new GameObject("ResultText");
            labelGO.transform.SetParent(_resultPanel.transform, false);
            var lrt = labelGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(18, 10);
            lrt.offsetMax = new Vector2(-18, -10);
            _slamLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _slamLabel.text = "";
            _slamLabel.fontSize = AccessibilitySettings.Scaled(20);
            _slamLabel.fontStyle = FontStyles.Bold;
            _slamLabel.color = Color.white;
            _slamLabel.alignment = TextAlignmentOptions.Center;
            _slamLabel.outlineWidth = 0.2f;
            _slamLabel.outlineColor = Color.black;
            _slamLabel.raycastTarget = false;
            _resultPanel.SetActive(false);
        }

        // ── Coroutine ─────────────────────────────────────────

        private IEnumerator FlashVisual()
        {
            float t = 0f;
            float halfDur = _pulseDuration * 0.5f;

            // Fade in
            while (t < halfDur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / halfDur;
                _flashImage.color = new Color(1f, 1f, 1f, k * _flashAlpha);
                yield return null;
            }

            // Fade out
            t = 0f;
            while (t < halfDur)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - t / halfDur;
                _flashImage.color = new Color(1f, 1f, 1f, k * _flashAlpha);
                yield return null;
            }

            _flashImage.color = new Color(1f, 1f, 1f, 0f);
        }

        private static string FormatProjection(ProjectedCardResolution result)
        {
            string card = string.IsNullOrWhiteSpace(result.CardDisplayName)
                ? result.CardType.ToString().ToUpperInvariant()
                : result.CardDisplayName.ToUpperInvariant();
            string notice = result.Notices != null && result.Notices.Length > 0
                ? result.Notices[0]
                : "";

            if (!result.IsExpectedSuccess)
            {
                string heading = result.Outcome switch
                {
                    CardSlamOutcome.CardJammed => "CARD JAMMED",
                    CardSlamOutcome.CardCrumpled => "CARD CRUMPLED",
                    CardSlamOutcome.InsufficientCredits => "INSUFFICIENT CORPORATE CREDITS",
                    CardSlamOutcome.BlockedByExemption => "BLOCKED BY PRE-FILED EXEMPTION",
                    CardSlamOutcome.ClientNotResponding => "CLIENT NOT RESPONDING",
                    CardSlamOutcome.NoActiveClient => "NO ACTIVE CLIENT",
                    CardSlamOutcome.InvalidCard => "INVALID CARD",
                    _ => "NO EFFECT",
                };
                string reason = string.IsNullOrWhiteSpace(result.FailureReason)
                    ? heading
                    : result.FailureReason;
                var failureFacts = BuildProjectionFacts(result, includeFatigue: false);
                if (!string.IsNullOrWhiteSpace(notice)) failureFacts.Add(notice);
                return $"{card} · EXPECTED {heading}\n{reason}" +
                       (failureFacts.Count > 0
                           ? $"\n{string.Join(" · ", failureFacts)}"
                           : "");
            }

            string transition = FormatProjectedClientChange(result);
            var facts = BuildProjectionFacts(result, includeFatigue: true);
            if (!string.IsNullOrWhiteSpace(notice)) facts.Add(notice);

            return $"{card} · EXPECTED\n{transition}\n{string.Join(" · ", facts)}";
        }

        private static string FormatProjectedClientChange(ProjectedCardResolution result)
        {
            if (!string.IsNullOrWhiteSpace(result.ClientEffect))
                return $"{result.ClientEffect} · {result.ClientEffectDuration:0.##}s";

            return result.StateBefore.HasValue && result.StateAfter.HasValue
                ? $"{result.StateBefore.Value.ToString().ToUpperInvariant()} → " +
                  result.StateAfter.Value.ToString().ToUpperInvariant()
                : "STATE EXPECTED";
        }

        private static List<string> BuildProjectionFacts(
            ProjectedCardResolution result, bool includeFatigue)
        {
            var facts = new List<string>();
            if (result.CreditsDelta != 0)
                facts.Add($"Credits {FormatSigned(result.CreditsDelta, "¢")}");
            if (!Mathf.Approximately(result.SanityDelta, 0f))
                facts.Add($"Sanity {FormatSigned(result.SanityDelta)}");
            if (!Mathf.Approximately(result.SoulIntegrityDelta, 0f))
                facts.Add($"Soul {FormatSigned(result.SoulIntegrityDelta)}");
            if (includeFatigue)
                facts.Add($"Fatigue {result.FatigueBefore}→{result.FatigueAfter}");
            return facts;
        }

        private static string FormatResult(AppliedCardResolution result)
        {
            string card = result.CardType.ToString().ToUpperInvariant();

            if (!result.IsSuccess)
            {
                string heading = result.Outcome switch
                {
                    CardSlamOutcome.CardJammed => "CARD JAMMED",
                    CardSlamOutcome.CardCrumpled => "CARD CRUMPLED",
                    CardSlamOutcome.InsufficientCredits => "INSUFFICIENT CORPORATE CREDITS",
                    CardSlamOutcome.BlockedByExemption => "BLOCKED BY PRE-FILED EXEMPTION",
                    CardSlamOutcome.ClientNotResponding => "CLIENT NOT RESPONDING",
                    CardSlamOutcome.NoActiveClient => "NO ACTIVE CLIENT",
                    CardSlamOutcome.InvalidCard => "INVALID CARD",
                    _ => "NO EFFECT",
                };
                string reason = string.IsNullOrWhiteSpace(result.FailureReason)
                    ? heading
                    : result.FailureReason;
                if (result.Outcome == CardSlamOutcome.InvalidCard)
                    return $"{heading}\n{reason}";

                var failureFacts = new List<string>();
                if (result.StateBefore.HasValue && result.StateAfter.HasValue
                    && result.StateBefore.Value != result.StateAfter.Value)
                {
                    failureFacts.Add(
                        $"{result.StateBefore.Value.ToString().ToUpperInvariant()} → " +
                        result.StateAfter.Value.ToString().ToUpperInvariant());
                }
                if (result.CreditsDelta != 0)
                    failureFacts.Add($"Credits {FormatSigned(result.CreditsDelta, "¢")}");
                if (!Mathf.Approximately(result.SanityDelta, 0f))
                    failureFacts.Add($"Sanity {FormatSigned(result.SanityDelta)}");
                if (!Mathf.Approximately(result.SoulIntegrityDelta, 0f))
                    failureFacts.Add($"Soul {FormatSigned(result.SoulIntegrityDelta)}");

                return $"{card} — {heading}\n{reason}" +
                       (failureFacts.Count > 0
                           ? $"\n{string.Join(" · ", failureFacts)}"
                           : "");
            }

            string transition = !string.IsNullOrWhiteSpace(result.ClientEffect)
                ? $"{result.ClientEffect} · {result.ClientEffectDuration:0.##}s"
                : result.StateBefore.HasValue && result.StateAfter.HasValue
                    ? $"{result.StateBefore.Value.ToString().ToUpperInvariant()} → " +
                      result.StateAfter.Value.ToString().ToUpperInvariant()
                    : "STATE CONFIRMED";

            var facts = new List<string>();
            if (result.CreditsDelta != 0)
                facts.Add($"Credits {FormatSigned(result.CreditsDelta, "¢")}");
            if (!Mathf.Approximately(result.SanityDelta, 0f))
                facts.Add($"Sanity {FormatSigned(result.SanityDelta)}");
            if (!Mathf.Approximately(result.SoulIntegrityDelta, 0f))
                facts.Add($"Soul {FormatSigned(result.SoulIntegrityDelta)}");
            facts.Add($"Fatigue {result.FatigueBefore}→{result.FatigueAfter}");

            return $"{card}\n{transition}\n{string.Join(" · ", facts)}";
        }

        private static string FormatSigned(float value, string prefix = "")
        {
            if (Mathf.Approximately(value, 0f)) return $"{prefix}0";
            string sign = value > 0f ? "+" : "−";
            return $"{sign}{prefix}{Mathf.Abs(value):0.##}";
        }
    }
}
