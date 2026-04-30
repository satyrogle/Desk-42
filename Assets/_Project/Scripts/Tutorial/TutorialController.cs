// ============================================================
// DESK 42 — Tutorial Controller (MonoBehaviour)
//
// Self-bootstrapping first-run guide for the Shift scene.
// Drop on any GameObject; on Start() it checks
// MetaProgressData.TutorialCompleted and either runs the
// stepwise overlay or removes itself.
//
// Steps fire off real RumorMill events so the tutorial keeps
// pace with what the player actually does:
//
//   Step 1 (Shift starts)        : Welcome + read the claim.
//   Step 2 (First claim queued)  : Drag a punch card to slam.
//   Step 3 (First card slammed)  : Watch the client's state shift.
//   Step 4 (First claim resolved): Resolve all claims to clock out.
//
// A SKIP button is always available. Marking the flag persists
// to meta.json so subsequent shifts skip the overlay.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;

namespace Desk42.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialController : MonoBehaviour
    {
        [Tooltip("Force the tutorial to run even if MetaProgressData says it's complete. Useful for testing or replay-from-menu.")]
        [SerializeField] private bool _forceRun;

        private GameObject _root;
        private TMP_Text   _bodyLabel;
        private TMP_Text   _stepLabel;
        private Button     _continueBtn;
        private Button     _skipBtn;

        private readonly List<TutorialStep> _steps = new();
        private int _currentStep = -1;
        private bool _running;

        private struct TutorialStep
        {
            public string Title;
            public string Body;
            public TutorialTrigger AdvanceOn; // event that auto-advances
        }

        private enum TutorialTrigger
        {
            ContinueButtonOnly, // wait for player click
            ClaimQueued,
            CardSlammed,
            ClaimResolved,
        }

        // ── Lifecycle ─────────────────────────────────────────

        private void Start()
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null)
            {
                // No GameManager — likely not launched from Boot. Skip silently.
                Destroy(this);
                return;
            }

            if (meta.TutorialCompleted && !_forceRun)
            {
                Destroy(this);
                return;
            }

            BuildSteps();
            BuildUI();
            HookEvents();
            ShowStep(0);
        }

        private void OnDisable()
        {
            UnhookEvents();
        }

        private void OnDestroy()
        {
            UnhookEvents();
        }

        // ── Steps ─────────────────────────────────────────────

        private void BuildSteps()
        {
            _steps.Clear();
            _steps.Add(new TutorialStep
            {
                Title = "WELCOME TO DESK 42",
                Body  = "You're a claims adjuster at an organization that does not officially exist.\n\nYour job: process claims, route clients, and try not to lose your soul. Click CONTINUE when you're ready.",
                AdvanceOn = TutorialTrigger.ContinueButtonOnly,
            });
            _steps.Add(new TutorialStep
            {
                Title = "READ THE CLAIM",
                Body  = "A claim will appear above. Read it carefully — every claim hides a moral choice underneath the paperwork.\n\nWhen the first claim is queued, we'll move on.",
                AdvanceOn = TutorialTrigger.ClaimQueued,
            });
            _steps.Add(new TutorialStep
            {
                Title = "PLAY A PUNCH CARD",
                Body  = "Drag a punch card from your hand into the SLOT to slam it. Each card pushes the client toward a different state.\n\nTry slamming any card to see how the client reacts.",
                AdvanceOn = TutorialTrigger.CardSlammed,
            });
            _steps.Add(new TutorialStep
            {
                Title = "WATCH THE CLIENT",
                Body  = "Different cards push clients between Cooperative, Agitated, Litigious, and worse. Match cards to client mood and you'll resolve claims cleanly.\n\nResolve a claim to continue.",
                AdvanceOn = TutorialTrigger.ClaimResolved,
            });
            _steps.Add(new TutorialStep
            {
                Title = "YOU'RE CLEARED FOR DUTY",
                Body  = "Resolve every claim before the shift ends to clock out.\n\nTrack sanity in the HUD. Watch the memos. Don't sign more NDAs than you have to.\n\nGood luck.",
                AdvanceOn = TutorialTrigger.ContinueButtonOnly,
            });
        }

        // ── Event Hooks ───────────────────────────────────────

        private void HookEvents()
        {
            RumorMill.OnClaimQueued    += HandleClaimQueued;
            RumorMill.OnCardSlammed    += HandleCardSlammed;
            RumorMill.OnClaimResolved  += HandleClaimResolved;
        }

        private void UnhookEvents()
        {
            RumorMill.OnClaimQueued    -= HandleClaimQueued;
            RumorMill.OnCardSlammed    -= HandleCardSlammed;
            RumorMill.OnClaimResolved  -= HandleClaimResolved;
        }

        private void HandleClaimQueued(ClaimQueuedEvent _)
        {
            if (CurrentStep().AdvanceOn == TutorialTrigger.ClaimQueued) Advance();
        }

        private void HandleCardSlammed(CardSlammedEvent _)
        {
            if (CurrentStep().AdvanceOn == TutorialTrigger.CardSlammed) Advance();
        }

        private void HandleClaimResolved(ClaimResolvedEvent _)
        {
            if (CurrentStep().AdvanceOn == TutorialTrigger.ClaimResolved) Advance();
        }

        // ── Step Flow ─────────────────────────────────────────

        private TutorialStep CurrentStep()
        {
            return _running && _currentStep >= 0 && _currentStep < _steps.Count
                ? _steps[_currentStep]
                : default;
        }

        private void ShowStep(int index)
        {
            if (index < 0 || index >= _steps.Count)
            {
                Complete();
                return;
            }

            _running = true;
            _currentStep = index;
            var step = _steps[index];

            _stepLabel.text = $"STEP {index + 1} / {_steps.Count}  ·  {step.Title}";
            _bodyLabel.text = step.Body;

            // The continue button only does anything on
            // ContinueButtonOnly steps; for event-driven steps
            // it acts as "I get it, advance manually" too.
            _continueBtn.interactable = true;

            _root.SetActive(true);
        }

        private void Advance()
        {
            // Briefly hide while waiting for the next event so
            // the player can actually see the gameplay surface.
            _root.SetActive(false);

            int next = _currentStep + 1;
            if (next >= _steps.Count)
            {
                Complete();
                return;
            }

            // For event-driven steps, fire immediately. Add a
            // micro-delay only for visual breathing room.
            ShowStep(next);
        }

        private void Complete()
        {
            _running = false;
            _root.SetActive(false);
            UnhookEvents();

            var meta = GameManager.Instance?.Meta;
            if (meta != null && !meta.TutorialCompleted)
            {
                meta.TutorialCompleted = true;
                SaveSystem.SaveMeta(meta);
            }

            Destroy(this);
        }

        // ── UI Construction ───────────────────────────────────

        private void BuildUI()
        {
            var canvasGO = new GameObject("TutorialCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Backdrop — dimmer than settings panel so the
            // gameplay surface is still readable behind it.
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(canvasGO.transform, false);
            var brt = backdrop.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bImg = backdrop.AddComponent<Image>();
            bImg.color = new Color(0f, 0f, 0f, 0.55f);

            // Card pinned bottom-center so it doesn't cover the claim
            var card = new GameObject("Card");
            card.transform.SetParent(canvasGO.transform, false);
            var crt = card.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot     = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(900, 280);
            crt.anchoredPosition = new Vector2(0, 60);
            var cImg = card.AddComponent<Image>();
            cImg.color = new Color(0.10f, 0.10f, 0.12f, 0.97f);

            _stepLabel = CreateLabel(card.transform, "",
                new Vector2(0, 110), 18, FontStyles.Bold,
                new Color(0.85f, 0.80f, 0.65f));
            _stepLabel.alignment = TextAlignmentOptions.Center;

            _bodyLabel = CreateLabel(card.transform, "",
                new Vector2(0, 10), 22, FontStyles.Normal, Color.white);
            _bodyLabel.rectTransform.sizeDelta = new Vector2(840, 160);
            _bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            _bodyLabel.enableWordWrapping = true;

            _continueBtn = CreateButton(card.transform, "Continue",
                new Vector2(160, -100), () => Advance());

            _skipBtn = CreateButton(card.transform, "Skip Tutorial",
                new Vector2(-160, -100), () => Complete());

            _root = canvasGO;
        }

        // ── Widget Helpers ────────────────────────────────────

        private static Button CreateButton(Transform parent, string label,
            Vector2 anchoredPos, Action onClick)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220, 48);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.20f, 0.20f, 0.25f, 1f);

            var btn = go.AddComponent<Button>();
            var lbl = CreateLabel(go.transform, label, Vector2.zero, 22,
                FontStyles.Bold, Color.white);
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.rectTransform.sizeDelta = new Vector2(200, 40);

            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        private static TMP_Text CreateLabel(Transform parent, string text,
            Vector2 anchoredPos, float fontSize, FontStyles style, Color color)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(840, 40);
            rt.anchoredPosition = anchoredPos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text         = text;
            tmp.alignment    = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize     = fontSize;
            tmp.fontStyle    = style;
            tmp.color        = color;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
