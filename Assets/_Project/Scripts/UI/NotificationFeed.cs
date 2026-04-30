// ============================================================
// DESK 42 — Notification Feed (MonoBehaviour)
//
// Top-right toast notifications for transient game events.
// Listens to OfficeHazard, SanityChanged, NDASigned, MoralChoice,
// MilestoneReached, TideEscalated. Renders as stacked toasts
// that slide in, hold ~3 seconds, then fade out.
//
// Self-bootstrapping: drop on any GameObject; UI builds on Start.
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
    public sealed class NotificationFeed : MonoBehaviour
    {
        // ── Config ────────────────────────────────────────────

        [SerializeField] private float _toastWidth     = 380f;
        [SerializeField] private float _toastHeight    = 56f;
        [SerializeField] private float _stackSpacing   = 8f;
        [SerializeField] private float _holdDuration   = 3.0f;
        [SerializeField] private float _fadeDuration   = 0.5f;
        [SerializeField] private int   _maxStack       = 5;

        // ── State ─────────────────────────────────────────────

        private RectTransform _container;
        private readonly List<RectTransform> _activeToasts = new();
        private readonly Queue<PendingToast> _pending = new();

        private struct PendingToast
        {
            public string Title;
            public string Body;
            public Color  Accent;
        }

        // ── Lifecycle ─────────────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnOfficeHazard      += HandleOfficeHazard;
            RumorMill.OnSanityChanged     += HandleSanityChanged;
            RumorMill.OnNDASigned         += HandleNDASigned;
            RumorMill.OnMoralChoice       += HandleMoralChoice;
            RumorMill.OnMilestoneReached  += HandleMilestoneReached;
            RumorMill.OnTideEscalated     += HandleTideEscalated;
        }

        private void OnDisable()
        {
            RumorMill.OnOfficeHazard      -= HandleOfficeHazard;
            RumorMill.OnSanityChanged     -= HandleSanityChanged;
            RumorMill.OnNDASigned         -= HandleNDASigned;
            RumorMill.OnMoralChoice       -= HandleMoralChoice;
            RumorMill.OnMilestoneReached  -= HandleMilestoneReached;
            RumorMill.OnTideEscalated     -= HandleTideEscalated;
        }

        private void Start() => BuildContainer();

        private void Update()
        {
            // Drain pending into active stack
            while (_pending.Count > 0 && _activeToasts.Count < _maxStack)
            {
                var p = _pending.Dequeue();
                SpawnToast(p.Title, p.Body, p.Accent);
            }
        }

        // ── Event Handlers → Toast queueing ──────────────────

        private void HandleOfficeHazard(OfficeHazardEvent e)
        {
            string title = e.HazardType switch
            {
                OfficeHazardType.CoffeeMachineDown => "[!] COFFEE MACHINE DOWN",
                OfficeHazardType.PrinterJam        => "[!] PRINTER JAM",
                OfficeHazardType.MandatoryMeeting  => "[!] MANDATORY MEETING",
                OfficeHazardType.SystemCrash       => "[!] SYSTEM CRASH",
                OfficeHazardType.FireDrill         => "[!] FIRE DRILL",
                OfficeHazardType.UnscheduledAudit  => "[!] UNSCHEDULED AUDIT",
                _                                  => e.HazardType.ToString(),
            };
            string body = e.IsOverperformancePunishment ? "Productivity penalty incurred." : "Workflow disrupted.";
            Enqueue(title, body, new Color(0.85f, 0.45f, 0.15f));
        }

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            // Only fire for big changes (≥10 drop or fugue)
            float delta = e.Current - e.Previous;
            if (e.TriggeredFugue)
                Enqueue("⚠ FUGUE STATE", "Your awareness collapses.", new Color(0.85f, 0.20f, 0.20f));
            else if (delta <= -10f)
                Enqueue("Sanity dropping", $"−{Mathf.RoundToInt(-delta)} pts", new Color(0.55f, 0.30f, 0.75f));
        }

        private void HandleNDASigned(NDASignedEvent e)
        {
            Enqueue("📜 NDA SIGNED", $"Total: {e.TotalNDACount}", new Color(0.65f, 0.55f, 0.35f));
        }

        private void HandleMoralChoice(MoralChoiceEvent e)
        {
            if (e.WasUnethical)
                Enqueue("Soul scarred", "An unethical choice was filed.",
                    new Color(0.50f, 0.25f, 0.55f));
        }

        private void HandleMilestoneReached(MilestoneReachedEvent e)
        {
            Enqueue("✦ MILESTONE", e.MilestoneId.ToString(),
                new Color(0.95f, 0.85f, 0.40f));
        }

        private void HandleTideEscalated(TideEscalatedEvent e)
        {
            string body = e.IsOverperformance ? "Overperformance detected." : "Tide retreats.";
            Enqueue($"Tide → {e.NewLevel}", body, new Color(0.30f, 0.55f, 0.85f));
        }

        // ── Queue + spawn ─────────────────────────────────────

        private void Enqueue(string title, string body, Color accent)
        {
            _pending.Enqueue(new PendingToast { Title = title, Body = body, Accent = accent });
        }

        private void SpawnToast(string title, string body, Color accent)
        {
            if (_container == null) BuildContainer();

            var go = new GameObject($"Toast_{title}");
            go.transform.SetParent(_container, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(_toastWidth, _toastHeight);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.12f, 0.92f);
            bg.raycastTarget = false;

            // Accent stripe (left edge)
            var stripeGO = new GameObject("Stripe");
            stripeGO.transform.SetParent(go.transform, false);
            var stripeRT = stripeGO.AddComponent<RectTransform>();
            stripeRT.anchorMin = new Vector2(0, 0);
            stripeRT.anchorMax = new Vector2(0, 1);
            stripeRT.pivot     = new Vector2(0, 0.5f);
            stripeRT.sizeDelta = new Vector2(6, 0);
            stripeRT.anchoredPosition = Vector2.zero;
            var stripeImg = stripeGO.AddComponent<Image>();
            stripeImg.color = accent;

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(go.transform, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 0.5f);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.offsetMin = new Vector2(16, 0);
            titleRT.offsetMax = new Vector2(-12, -4);
            var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
            titleTmp.text = title;
            titleTmp.fontSize = 16;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.raycastTarget = false;

            // Body
            var bodyGO = new GameObject("Body");
            bodyGO.transform.SetParent(go.transform, false);
            var bodyRT = bodyGO.AddComponent<RectTransform>();
            bodyRT.anchorMin = new Vector2(0, 0);
            bodyRT.anchorMax = new Vector2(1, 0.5f);
            bodyRT.offsetMin = new Vector2(16, 4);
            bodyRT.offsetMax = new Vector2(-12, 0);
            var bodyTmp = bodyGO.AddComponent<TextMeshProUGUI>();
            bodyTmp.text = body;
            bodyTmp.fontSize = 12;
            bodyTmp.color = new Color(0.75f, 0.75f, 0.78f);
            bodyTmp.alignment = TextAlignmentOptions.MidlineLeft;
            bodyTmp.raycastTarget = false;

            _activeToasts.Insert(0, rt);
            RelayoutStack();
            StartCoroutine(ToastLifecycle(rt, bg, stripeImg, titleTmp, bodyTmp));
        }

        private void RelayoutStack()
        {
            for (int i = 0; i < _activeToasts.Count; i++)
            {
                var rt = _activeToasts[i];
                if (rt == null) continue;
                rt.anchoredPosition = new Vector2(-20, -20 - i * (_toastHeight + _stackSpacing));
            }
        }

        private IEnumerator ToastLifecycle(RectTransform rt, Image bg, Image stripe,
            TMP_Text title, TMP_Text body)
        {
            yield return new WaitForSecondsRealtime(_holdDuration);

            float t = 0f;
            Color bgC = bg.color, stripeC = stripe.color;
            Color titleC = title.color, bodyC = body.color;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - t / _fadeDuration;
                bg.color    = new Color(bgC.r,    bgC.g,    bgC.b,    bgC.a * a);
                stripe.color= new Color(stripeC.r,stripeC.g,stripeC.b,stripeC.a * a);
                title.color = new Color(titleC.r, titleC.g, titleC.b, a);
                body.color  = new Color(bodyC.r,  bodyC.g,  bodyC.b,  bodyC.a * a);
                yield return null;
            }

            _activeToasts.Remove(rt);
            if (rt != null) Destroy(rt.gameObject);
            RelayoutStack();
        }

        // ── Build container (own canvas) ──────────────────────

        private void BuildContainer()
        {
            var canvasGO = new GameObject("NotificationCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var rt = canvasGO.GetComponent<RectTransform>();
            _container = rt;
        }
    }
}
