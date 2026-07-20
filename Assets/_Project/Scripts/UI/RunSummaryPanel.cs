// ============================================================
// DESK 42 — Run Summary Panel (MonoBehaviour)
//
// Subscribes to RumorMill.OnRunCompleted. When fired, builds a
// self-contained summary screen on top of everything and waits
// for the player to click Continue. On click, calls
// GameManager.ContinueToMetaHub() to transition to InternalAudit.
//
// Self-bootstrapping: drop the component on any GameObject in
// the Shift scene (or auto-spawned by ShiftSceneFixer). UI is
// built at runtime on first show — no Inspector wiring needed.
// ============================================================

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class RunSummaryPanel : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────

        private GameObject _panelRoot;
        private TMP_Text   _titleText;
        private TMP_Text   _bodyText;
        private Button     _continueBtn;

        // ── Unity Lifecycle ───────────────────────────────────

        private void Awake()
        {
            Desk42Services.Register(this);
        }

        private void OnDestroy()
        {
            Desk42Services.Unregister<RunSummaryPanel>();
        }

        private void OnEnable()
        {
            RumorMill.OnRunCompleted += HandleRunCompleted;
        }

        private void OnDisable()
        {
            RumorMill.OnRunCompleted -= HandleRunCompleted;
        }

        // ── Event Handler ─────────────────────────────────────

        private void HandleRunCompleted(RunCompletedEvent e)
        {
            if (_panelRoot == null) BuildUI();
            Show(e);
        }

        // ── UI Construction ───────────────────────────────────

        private void BuildUI()
        {
            // Top-level Canvas (own canvas, sortingOrder high so it draws on top)
            _panelRoot = new GameObject("RunSummaryCanvas");
            _panelRoot.transform.SetParent(transform, false);

            var canvas = _panelRoot.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = _panelRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            _panelRoot.AddComponent<GraphicRaycaster>();

            // Full-screen darkening backdrop
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(_panelRoot.transform, false);
            var backdropRT = backdrop.AddComponent<RectTransform>();
            backdropRT.anchorMin = Vector2.zero;
            backdropRT.anchorMax = Vector2.one;
            backdropRT.offsetMin = Vector2.zero;
            backdropRT.offsetMax = Vector2.zero;
            var backdropImg = backdrop.AddComponent<Image>();
            backdropImg.color = UIPalette.Backdrop;

            // Centered card panel
            var card = new GameObject("Card");
            card.transform.SetParent(_panelRoot.transform, false);
            var cardRT = card.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot     = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(800, 600);
            var cardImg = card.AddComponent<Image>();
            cardImg.color = UIPalette.CardBackground;
            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = UIPalette.AccentTitle;
            cardOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            _titleText = CreateLabel(card.transform, "title",
                "RETRAINING VIDEO",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, -30), new Vector2(-40, 60),
                36, FontStyles.Bold, TextAlignmentOptions.Top,
                UIPalette.AccentTitle);

            // Body (stats)
            _bodyText = CreateLabel(card.transform, "body",
                "...",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1),
                Vector2.zero, Vector2.zero,
                20, FontStyles.Normal, TextAlignmentOptions.TopLeft,
                UIPalette.TextPrimary);
            var bodyRT = _bodyText.rectTransform;
            bodyRT.offsetMin = new Vector2(40, 100);
            bodyRT.offsetMax = new Vector2(-40, -110);

            // Continue button
            var btnGO = new GameObject("ContinueBtn");
            btnGO.transform.SetParent(card.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0);
            btnRT.anchorMax = new Vector2(0.5f, 0);
            btnRT.pivot     = new Vector2(0.5f, 0);
            btnRT.sizeDelta = new Vector2(280, 64);
            btnRT.anchoredPosition = new Vector2(0, 30);

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = UIPalette.AccentTitle;

            _continueBtn = btnGO.AddComponent<Button>();
            var btnColors = _continueBtn.colors;
            btnColors.normalColor      = UIPalette.AccentTitle;
            btnColors.highlightedColor = new Color(0.82f, 0.70f, 0.40f);
            btnColors.pressedColor     = new Color(0.52f, 0.40f, 0.18f);
            _continueBtn.colors = btnColors;
            _continueBtn.onClick.AddListener(OnContinueClicked);

            CreateLabel(btnGO.transform, "label",
                "CONTINUE",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                26, FontStyles.Bold, TextAlignmentOptions.Center,
                new Color(0.03f, 0.10f, 0.085f));

            _panelRoot.SetActive(false);
        }

        // ── Show / Hide ───────────────────────────────────────

        private void Show(RunCompletedEvent e)
        {
            _panelRoot.SetActive(true);

            string title = e.FugueTriggered ? "FUGUE STATE — RUN ENDED" : "RETRAINING VIDEO";
            _titleText.text  = title;
            _titleText.color = e.FugueTriggered
                ? UIPalette.AccentWarn
                : UIPalette.AccentTitle;

            _bodyText.text = BuildSummaryText(e);

            // Pause game time so the panel doesn't compete with timers
            Time.timeScale = 0f;
        }

        private void OnContinueClicked()
        {
            // Restore time scale before transitioning
            Time.timeScale = 1f;

            _panelRoot.SetActive(false);
            GameManager.Instance?.ContinueToMetaHub();
        }

        // ── Summary Builder ───────────────────────────────────

        private static string BuildSummaryText(RunCompletedEvent e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>Shift {e.ShiftNumber}</b>   <color=#888>seed {e.SeedCode}</color>");
            sb.AppendLine($"<color=#888>archetype:</color>  {Capitalize(e.ArchetypeId)}");
            sb.AppendLine();

            sb.AppendLine($"<b><size=24>Performance</size></b>");
            sb.AppendLine($"  Claims processed........  {e.ClaimsProcessed}");
            sb.AppendLine($"  Credits earned..........  ¢{e.CreditsEarned:N0}");
            sb.AppendLine($"  Combo multiplier........  ×{e.ComboMultiplier:F2}");
            sb.AppendLine($"  <b>Efficiency rating</b>.......  <color=#FFD75A>{e.EfficiencyRating:F0}</color>");
            sb.AppendLine();

            sb.AppendLine($"<b><size=24>State</size></b>");
            sb.AppendLine($"  Soul integrity..........  {ColorBar(e.SoulIntegrity)} {e.SoulIntegrity:F0}/100");
            sb.AppendLine($"  Sanity..................  {ColorBar(e.Sanity)} {e.Sanity:F0}/100");
            if (e.PersonalExpenseDebt > 0)
                sb.AppendLine($"  <color=#E84A4A>Personal expense debt...  ¢{e.PersonalExpenseDebt}</color>");

            if (e.FugueTriggered)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#E84A4A><i>The day collapsed in on itself. " +
                              "Forms blurred. Voices receded. Somewhere a stamp fell.</i></color>");
            }

            return sb.ToString();
        }

        private static string ColorBar(float value)
        {
            string color = value >= 60f ? "#5DD58A"
                         : value >= 30f ? "#E8C84A"
                         :                "#E84A4A";
            int filled = Mathf.Clamp(Mathf.RoundToInt(value / 10f), 0, 10);
            return $"<color={color}>{new string('█', filled)}{new string('·', 10 - filled)}</color>";
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            return char.ToUpper(s[0]) + s[1..];
        }

        // ── Helpers ───────────────────────────────────────────

        private static TMP_Text CreateLabel(Transform parent, string name,
            string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            float fontSize, FontStyles style,
            TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = AccessibilitySettings.Scaled(fontSize);
            tmp.fontStyle     = style;
            tmp.alignment     = align;
            tmp.color         = color;
            tmp.richText      = true;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            return tmp;
        }
    }
}
