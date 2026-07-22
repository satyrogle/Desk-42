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
        private RectTransform _motionCard;
        private TMP_Text _motionCardLabel;
        private GameObject _impactPanel;
        private TMP_Text _impactLabel;
        private string     _latestAppliedText;
        private string     _activePreviewCardId;
        private string     _activeMotionCardId;
        private Coroutine _motionSequence;
        private Coroutine _impactSequence;
        private Coroutine _flashSequence;

        private static readonly Vector2 MotionStart = new(960f, 105f);
        private static readonly Vector2 MotionImpact = new(1140f, 520f);

        public string RenderedImpactText
            => _impactLabel != null ? _impactLabel.text : "";

        // ── Lifecycle ─────────────────────────────────────────

        protected override void Subscribe()
        {
            RumorMill.OnCardSlammed += HandleSlam;
            RumorMill.OnCardPreview += HandlePreview;
            RumorMill.OnCardSlamIntent += HandleSlamIntent;
        }

        protected override void Unsubscribe()
        {
            RumorMill.OnCardSlammed -= HandleSlam;
            RumorMill.OnCardPreview -= HandlePreview;
            RumorMill.OnCardSlamIntent -= HandleSlamIntent;
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
            _activeMotionCardId = null;
            _latestAppliedText = FormatResult(e.Result);
            _resultPanel.SetActive(true);
            _slamLabel.text = _latestAppliedText;
            _slamLabel.color = Color.white;
            ShowImpact(e.Result);

            // Only the cosmetic pulse is budgeted. The semantic result above
            // is persistent and cannot be dropped.
            if (!AccessibilitySettings.ReducedMotion
                && FeedbackBudget.RequestBurst(FeedbackKind.Flash))
            {
                if (_flashSequence != null)
                    StopCoroutine(_flashSequence);
                _flashSequence = StartCoroutine(FlashVisual());
            }
        }

        private void HandleSlamIntent(CardSlamIntentEvent e)
        {
            if (_flashRoot == null) BuildUI();
            _activeMotionCardId = e.CardInstanceId;
            _motionCardLabel.text = string.IsNullOrWhiteSpace(e.CardDisplayName)
                ? FormatCardName(e.CardType)
                : e.CardDisplayName.ToUpperInvariant();
            _motionCard.gameObject.SetActive(true);
            _motionCard.anchoredPosition = MotionStart;
            _motionCard.localScale = Vector3.one;
            _motionCard.localRotation = Quaternion.Euler(0f, 0f, -4f);

            if (_motionSequence != null)
                StopCoroutine(_motionSequence);
            _motionSequence = StartCoroutine(
                MoveCardToImpact(e.CardInstanceId, e.TimeToImpact));
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

            // A simple physical punch card that travels from the hand toward
            // the machine before impact. This is choreography only; it never
            // computes or applies the result.
            var motionObject = new GameObject("PunchCardInFlight");
            motionObject.transform.SetParent(canvasGO.transform, false);
            _motionCard = motionObject.AddComponent<RectTransform>();
            _motionCard.anchorMin = Vector2.zero;
            _motionCard.anchorMax = Vector2.zero;
            _motionCard.pivot = new Vector2(0.5f, 0.5f);
            _motionCard.sizeDelta = new Vector2(176f, 228f);
            _motionCard.anchoredPosition = MotionStart;
            var cardPaper = motionObject.AddComponent<Image>();
            cardPaper.color = new Color(0.91f, 0.88f, 0.74f, 1f);
            cardPaper.raycastTarget = false;
            var cardOutline = motionObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.07f, 0.13f, 0.14f, 1f);
            cardOutline.effectDistance = new Vector2(4f, -4f);

            var holesObject = new GameObject("PunchHoles");
            holesObject.transform.SetParent(motionObject.transform, false);
            var holesRt = holesObject.AddComponent<RectTransform>();
            holesRt.anchorMin = new Vector2(0f, 0f);
            holesRt.anchorMax = new Vector2(0f, 1f);
            holesRt.pivot = new Vector2(0f, 0.5f);
            holesRt.sizeDelta = new Vector2(24f, 0f);
            holesRt.anchoredPosition = new Vector2(8f, 0f);
            var holes = holesObject.AddComponent<TextMeshProUGUI>();
            holes.text = "●\n●\n●\n●\n●";
            holes.fontSize = AccessibilitySettings.Scaled(13f);
            holes.color = new Color(0.08f, 0.14f, 0.14f, 0.8f);
            holes.alignment = TextAlignmentOptions.Center;
            holes.raycastTarget = false;

            var motionLabelObject = new GameObject("CardName");
            motionLabelObject.transform.SetParent(motionObject.transform, false);
            var motionLabelRt = motionLabelObject.AddComponent<RectTransform>();
            motionLabelRt.anchorMin = Vector2.zero;
            motionLabelRt.anchorMax = Vector2.one;
            motionLabelRt.offsetMin = new Vector2(30f, 18f);
            motionLabelRt.offsetMax = new Vector2(-12f, -18f);
            _motionCardLabel = motionLabelObject.AddComponent<TextMeshProUGUI>();
            _motionCardLabel.fontSize = AccessibilitySettings.Scaled(19f);
            _motionCardLabel.fontStyle = FontStyles.Bold;
            _motionCardLabel.color = new Color(0.08f, 0.14f, 0.14f, 1f);
            _motionCardLabel.alignment = TextAlignmentOptions.Center;
            _motionCardLabel.enableWordWrapping = true;
            _motionCardLabel.raycastTarget = false;
            motionObject.SetActive(false);

            // Protected semantic stamp at the point of impact. It is never
            // gated by FeedbackBudget; only its scale motion is cosmetic.
            _impactPanel = new GameObject("PunchImpactStamp");
            _impactPanel.transform.SetParent(canvasGO.transform, false);
            var impactRt = _impactPanel.AddComponent<RectTransform>();
            impactRt.anchorMin = Vector2.zero;
            impactRt.anchorMax = Vector2.zero;
            impactRt.pivot = new Vector2(0.5f, 0.5f);
            impactRt.sizeDelta = new Vector2(480f, 88f);
            impactRt.anchoredPosition = new Vector2(1140f, 665f);
            var impactPaper = _impactPanel.AddComponent<Image>();
            impactPaper.color = new Color(0.06f, 0.12f, 0.14f, 0.97f);
            impactPaper.raycastTarget = false;
            var impactOutline = _impactPanel.AddComponent<Outline>();
            impactOutline.effectColor = new Color(0.95f, 0.78f, 0.3f, 1f);
            impactOutline.effectDistance = new Vector2(3f, -3f);

            var impactLabelObject = new GameObject("ImpactText");
            impactLabelObject.transform.SetParent(_impactPanel.transform, false);
            var impactLabelRt = impactLabelObject.AddComponent<RectTransform>();
            impactLabelRt.anchorMin = Vector2.zero;
            impactLabelRt.anchorMax = Vector2.one;
            impactLabelRt.offsetMin = new Vector2(12f, 8f);
            impactLabelRt.offsetMax = new Vector2(-12f, -8f);
            _impactLabel = impactLabelObject.AddComponent<TextMeshProUGUI>();
            _impactLabel.fontSize = AccessibilitySettings.Scaled(21f);
            _impactLabel.fontStyle = FontStyles.Bold;
            _impactLabel.color = new Color(0.95f, 0.88f, 0.64f, 1f);
            _impactLabel.alignment = TextAlignmentOptions.Center;
            _impactLabel.enableWordWrapping = true;
            _impactLabel.raycastTarget = false;
            _impactPanel.SetActive(false);
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
            _flashSequence = null;
        }

        private IEnumerator MoveCardToImpact(string cardInstanceId,
            float duration)
        {
            float elapsed = 0f;
            float travelTime = Mathf.Max(0.08f, duration);
            while (elapsed < travelTime
                && _activeMotionCardId == cardInstanceId)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelTime);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                _motionCard.anchoredPosition = Vector2.LerpUnclamped(
                    MotionStart, MotionImpact, eased);
                _motionCard.localScale = Vector3.one
                    * Mathf.Lerp(1f, 0.68f, eased);
                _motionCard.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Lerp(-4f, 3f, eased));
                yield return null;
            }

            if (_activeMotionCardId == cardInstanceId)
            {
                _motionCard.anchoredPosition = MotionImpact;
                _motionCard.localScale = Vector3.one * 0.68f;
                yield return new WaitForSecondsRealtime(0.8f);
                if (_activeMotionCardId == cardInstanceId)
                {
                    _activeMotionCardId = null;
                    _motionCard.gameObject.SetActive(false);
                }
            }
            _motionSequence = null;
        }

        private void ShowImpact(AppliedCardResolution result)
        {
            if (_motionSequence != null)
            {
                StopCoroutine(_motionSequence);
                _motionSequence = null;
            }

            _motionCard.gameObject.SetActive(true);
            _motionCard.anchoredPosition = MotionImpact;
            _motionCard.localScale = Vector3.one * 0.68f;
            _motionCard.localRotation = Quaternion.Euler(0f, 0f, 3f);

            _impactLabel.text = GetImpactMessage(result);
            Color accent = GetImpactColor(result.Outcome);
            _impactLabel.color = accent;
            _impactPanel.GetComponent<Outline>().effectColor = accent;
            _impactPanel.SetActive(true);
            _impactPanel.transform.SetAsLastSibling();

            if (_impactSequence != null)
                StopCoroutine(_impactSequence);
            _impactSequence = StartCoroutine(PlayImpact(result.IsSuccess));
        }

        private IEnumerator PlayImpact(bool success)
        {
            var impactRt = _impactPanel.GetComponent<RectTransform>();
            impactRt.localScale = AccessibilitySettings.ReducedMotion
                ? Vector3.one
                : Vector3.one * 1.28f;

            if (!AccessibilitySettings.ReducedMotion)
            {
                float elapsed = 0f;
                const float settleDuration = 0.16f;
                while (elapsed < settleDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / settleDuration);
                    impactRt.localScale = Vector3.one
                        * Mathf.Lerp(1.28f, 1f, t);
                    _motionCard.anchoredPosition = MotionImpact
                        + Vector2.right * Mathf.Sin(t * Mathf.PI)
                        * (success ? 28f : -18f);
                    yield return null;
                }

                Vector2 reactionStart = _motionCard.anchoredPosition;
                Vector2 reactionTarget = success
                    ? MotionImpact + new Vector2(18f, -95f)
                    : MotionStart;
                Vector3 scaleStart = _motionCard.localScale;
                Vector3 scaleTarget = success
                    ? Vector3.one * 0.12f
                    : Vector3.one;
                elapsed = 0f;
                const float reactionDuration = 0.26f;
                while (elapsed < reactionDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / reactionDuration);
                    float eased = success
                        ? t * t
                        : 1f - Mathf.Pow(1f - t, 3f);
                    _motionCard.anchoredPosition = Vector2.LerpUnclamped(
                        reactionStart, reactionTarget, eased);
                    _motionCard.localScale = Vector3.LerpUnclamped(
                        scaleStart, scaleTarget, eased);
                    _motionCard.localRotation = Quaternion.Euler(
                        0f, 0f, Mathf.Lerp(3f, success ? 8f : -4f, eased));
                    yield return null;
                }

                if (success)
                    _motionCard.gameObject.SetActive(false);
            }

            impactRt.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(1.05f);
            _impactPanel.SetActive(false);
            _motionCard.gameObject.SetActive(false);
            _impactSequence = null;
        }

        private static string GetImpactMessage(AppliedCardResolution result)
        {
            if (!result.IsSuccess)
            {
                return result.Outcome switch
                {
                    CardSlamOutcome.BlockedByExemption =>
                        "EXEMPTION INTERCEPTED · CARD RETURNED",
                    CardSlamOutcome.BlockedByState =>
                        "NO APPLICABLE PROCEDURE · CARD RETURNED",
                    CardSlamOutcome.CardJammed =>
                        "MACHINE JAMMED · CARD NOT APPLIED",
                    CardSlamOutcome.CardCrumpled =>
                        "CARD CRUMPLED · NOT APPLIED",
                    CardSlamOutcome.InsufficientCredits =>
                        $"PAYMENT REJECTED · NEED ¢{result.RequiredCredits}",
                    CardSlamOutcome.ClientNotResponding =>
                        "CLIENT NOT RESPONDING · CARD RETURNED",
                    _ => "CARD REJECTED · NOT APPLIED",
                };
            }

            if (!string.IsNullOrWhiteSpace(result.ClientEffect))
                return $"APPLIED · {result.ClientEffect}";
            if (result.StateBefore.HasValue && result.StateAfter.HasValue)
                return $"APPLIED · {result.StateBefore.Value.ToString().ToUpperInvariant()} → " +
                       result.StateAfter.Value.ToString().ToUpperInvariant();
            return "CARD APPLIED";
        }

        private static Color GetImpactColor(CardSlamOutcome outcome)
        {
            return outcome switch
            {
                CardSlamOutcome.Success => new Color(0.38f, 0.86f, 0.63f, 1f),
                CardSlamOutcome.BlockedByExemption => new Color(0.76f, 0.48f, 0.96f, 1f),
                CardSlamOutcome.CardJammed or CardSlamOutcome.CardCrumpled
                    => new Color(1f, 0.55f, 0.2f, 1f),
                _ => new Color(0.95f, 0.32f, 0.28f, 1f),
            };
        }

        private static string FormatCardName(PunchCardType cardType)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                cardType.ToString(), "([a-z])([A-Z])", "$1 $2")
                .ToUpperInvariant();
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
