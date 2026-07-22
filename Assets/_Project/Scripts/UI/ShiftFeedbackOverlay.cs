// ============================================================
// DESK 42 — Shift Feedback Overlay (MonoBehaviour)
//
// Self-bootstrapping visual feedback layer. Subscribes to
// RumorMill events and shows animated toast messages for:
//   - Persistent factual claim receipts (APPROVE / DENY / LIQUIFY)
//   - Phase transitions (MORNING → LUNCH → AFTERNOON → OVERTIME)
//   - Credit gains/losses
//
// Attach to the ShiftUI root GameObject (auto-added by
// ShiftSceneAutoLayout if missing).
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class ShiftFeedbackOverlay : MonoBehaviour
    {
        // ── Config ────────────────────────────────────────────

        private const float TOAST_DURATION   = 1.8f;
        private const float TOAST_FADE_TIME  = 0.4f;
        private const int   MAX_ACTIVE_TOASTS = 4;

        // ── State ─────────────────────────────────────────────

        private Transform _toastContainer;
        private GameObject _receiptPanel;
        private TMP_Text _receiptText;
        private readonly Queue<GameObject> _activeToasts = new();

        // ── Colors ────────────────────────────────────────────

        private static readonly Color ApproveColor = new(0.2f, 0.75f, 0.3f);
        private static readonly Color DenyColor    = new(0.85f, 0.2f, 0.2f);
        private static readonly Color SlamColor    = new(0.3f, 0.6f, 0.95f);
        private static readonly Color JamColor     = new(1f, 0.5f, 0.1f);
        private static readonly Color PhaseColor   = new(0.9f, 0.85f, 0.5f);
        private static readonly Color CreditColor  = new(0.95f, 0.85f, 0.4f);
        private static readonly Color BlockColor   = new(0.6f, 0.25f, 0.8f);

        // ── Unity Lifecycle ───────────────────────────────────

        private void Awake()
        {
            BuildToastContainer();
            BuildReceiptPanel();
        }

        private void OnEnable()
        {
            RumorMill.OnClaimResolved       += HandleClaimResolved;
            RumorMill.OnShiftPhaseChanged   += HandlePhaseChanged;
            RumorMill.OnCounterTraitGenerated += HandleCounterTrait;
        }

        private void OnDisable()
        {
            RumorMill.OnClaimResolved       -= HandleClaimResolved;
            RumorMill.OnShiftPhaseChanged   -= HandlePhaseChanged;
            RumorMill.OnCounterTraitGenerated -= HandleCounterTrait;
        }

        // ── Toast Container ───────────────────────────────────

        private void BuildToastContainer()
        {
            var go = new GameObject("ToastContainer");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500, 300);
            rt.anchoredPosition = Vector2.zero;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment        = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 8;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            _toastContainer = go.transform;
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
#if DEVELOPMENT_BUILD
            Debugging.ConsensusAudit.MarkFeedbackOverlay();
#endif
            ShowClaimReceipt(e.Result);
        }

        private static string FormatSignedCredits(int delta)
            => delta > 0 ? $"+¢{delta}" : delta < 0 ? $"-¢{-delta}" : "¢0";

        private void BuildReceiptPanel()
        {
            _receiptPanel = new GameObject("LatestClaimReceipt");
            _receiptPanel.transform.SetParent(transform, false);
            var rt = _receiptPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(420f, 248f);
            rt.anchoredPosition = new Vector2(-24f, -138f);

            var paper = _receiptPanel.AddComponent<Image>();
            paper.color = new Color(0.90f, 0.87f, 0.75f, 0.97f);
            paper.raycastTarget = false;
            var outline = _receiptPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.17f, 0.18f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textObject = new GameObject("ReceiptText");
            textObject.transform.SetParent(_receiptPanel.transform, false);
            var textRt = textObject.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20f, 16f);
            textRt.offsetMax = new Vector2(-20f, -16f);

            _receiptText = textObject.AddComponent<TextMeshProUGUI>();
            _receiptText.fontSize = Desk42.Accessibility.AccessibilitySettings.Scaled(21f);
            _receiptText.color = new Color(0.08f, 0.13f, 0.14f, 1f);
            _receiptText.alignment = TextAlignmentOptions.TopLeft;
            _receiptText.enableWordWrapping = true;
            _receiptText.raycastTarget = false;
            _receiptPanel.SetActive(false);
        }

        private void ShowClaimReceipt(AppliedClaimResolution result)
        {
            if (_receiptPanel == null) BuildReceiptPanel();

            string heading = result.Kind switch
            {
                ClaimResolutionKind.Approve => "CLAIM APPROVED",
                ClaimResolutionKind.Deny => "CLAIM DENIED",
                ClaimResolutionKind.Liquify => "CLAIM LIQUIFIED",
                _ => "CLAIM RESOLVED",
            };

            string quota = result.QuotaRequired > 0
                ? $"{result.QuotaAfter}/{result.QuotaRequired}"
                : result.QuotaAfter.ToString();
            string streak = Mathf.Approximately(
                    result.ComplianceStreakBefore, result.ComplianceStreakAfter)
                ? $"{result.ComplianceStreakAfter:0.0}x"
                : $"{result.ComplianceStreakBefore:0.0}x → {result.ComplianceStreakAfter:0.0}x";

            _receiptText.text =
                $"<b>{heading}</b>\n" +
                $"Corporate credits  {FormatSignedCredits(result.CreditsDelta)}\n" +
                $"Sanity  {FormatSignedAmount(result.SanityDelta)}\n" +
                $"Soul integrity  {FormatSignedAmount(result.SoulIntegrityDelta)}\n" +
                $"Dark intelligence  {FormatSignedAmount(result.DarkIntelligenceDelta)}\n" +
                $"Quota  {quota}\n" +
                $"Compliance streak  {streak}";
            _receiptPanel.SetActive(true);
            _receiptPanel.transform.SetAsLastSibling();
        }

        private static string FormatSignedAmount(float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return "0";
            string sign = delta > 0f ? "+" : "−";
            return $"{sign}{Mathf.Abs(delta):0.##}";
        }

        private void HandleCounterTrait(CounterTraitGeneratedEvent e)
        {
            ShowToast($"⚡ MUTATION: {e.CounterTraitId}", BlockColor, 24);
        }

        private void HandlePhaseChanged(ShiftPhaseChangedEvent e)
        {
            string phaseName = e.Current switch
            {
                ShiftPhase.MorningBlock   => "☀ MORNING BLOCK",
                ShiftPhase.LunchBreak     => "☕ LUNCH BREAK",
                ShiftPhase.AfternoonBlock => "◑ AFTERNOON BLOCK",
                ShiftPhase.Overtime       => "⚡ OVERTIME",
                ShiftPhase.ClockOut       => "🏁 SHIFT COMPLETE",
                _                         => e.Current.ToString().ToUpper(),
            };

            ShowToast(phaseName, PhaseColor, 32);
        }

        // ── Toast Spawning ────────────────────────────────────

        private void ShowToast(string message, Color color, float fontSize)
        {
            // Throttle: at most one toast per FeedbackBudget window.
            // Drops the toast (doesn't queue) — toasts that lose this
            // gate are typically the duplicate "card filed" type which
            // are also covered by CardSlamFeedback's own flash.
            if (!FeedbackBudget.RequestBurst(FeedbackKind.Toast)) return;

            if (_toastContainer == null) return;

            // Cap active toasts
            while (_activeToasts.Count >= MAX_ACTIVE_TOASTS)
            {
                var old = _activeToasts.Dequeue();
                if (old != null) Destroy(old);
            }

            var go = new GameObject("Toast");
            go.transform.SetParent(_toastContainer, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(480, fontSize + 20);

            // Semi-transparent background pill
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);
            bg.raycastTarget = false;

            // Add rounded corners look via outline
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.5f);
            outline.effectDistance = new Vector2(2, 2);

            // Text label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12, 4);
            lrt.offsetMax = new Vector2(-12, -4);

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = message;
            tmp.fontSize  = Desk42.Accessibility.AccessibilitySettings.Scaled(fontSize);
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.raycastTarget = false;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            _activeToasts.Enqueue(go);
            StartCoroutine(AnimateToast(go, cg));
        }

        private IEnumerator AnimateToast(GameObject go, CanvasGroup cg)
        {
            if (go == null || cg == null) yield break;

            // Fade in
            float t = 0f;
            while (t < TOAST_FADE_TIME)
            {
                t += Time.deltaTime;
                if (cg != null) cg.alpha = Mathf.Lerp(0f, 1f, t / TOAST_FADE_TIME);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;

            // Hold
            yield return new WaitForSeconds(TOAST_DURATION - TOAST_FADE_TIME * 2f);

            // Fade out + slide up
            t = 0f;
            var rt = go?.GetComponent<RectTransform>();
            Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            while (t < TOAST_FADE_TIME)
            {
                t += Time.deltaTime;
                float p = t / TOAST_FADE_TIME;
                if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, p);
                if (rt != null) rt.anchoredPosition = startPos + new Vector2(0, p * 30f);
                yield return null;
            }

            if (go != null) Destroy(go);
        }
    }
}
