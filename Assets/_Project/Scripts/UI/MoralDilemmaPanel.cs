// ============================================================
// DESK 42 — Moral Dilemma Panel (MonoBehaviour)
//
// Modal panel that surfaces when DilemmaTriggeredEvent fires.
// Pauses time, shows the prompt + two choice buttons, and
// invokes the appropriate callback on click.
//
// Self-bootstrapping. Drop on a GameObject and go.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class MoralDilemmaPanel : MonoBehaviour
    {
        private GameObject _panelRoot;
        private TMP_Text   _promptText;
        private Button     _ethicalBtn;
        private Button     _bureaucraticBtn;
        private TMP_Text   _ethicalLabel;
        private TMP_Text   _bureaucraticLabel;

        private Action _pendingEthical;
        private Action<bool> _pendingBureaucratic;

        // ── Lifecycle ─────────────────────────────────────────

        private void OnEnable()  { RumorMill.OnDilemmaTriggered += Show; }
        private void OnDisable() { RumorMill.OnDilemmaTriggered -= Show; }

        // ── Show ──────────────────────────────────────────────

        private void Show(DilemmaTriggeredEvent e)
        {
            if (_panelRoot == null) BuildUI();

            _promptText.text       = e.Prompt;
            _ethicalLabel.text     = e.EthicalLabel;
            _pendingEthical        = e.OnEthical;
            _pendingBureaucratic   = e.OnBureaucratic;

            var run = GameManager.Instance?.Run;
            var meta = GameManager.Instance?.Meta;
            bool canInvert = meta?.DarkIntelligenceUnlocks?.Contains("EMPATHY_INVERSION") == true && 
                             run != null && run.DarkIntelligence > 0;

            if (canInvert)
            {
                _bureaucraticLabel.text = "[INVERT] " + e.BureaucraticLabel + " (-1 Dark Intel)";
            }
            else
            {
                _bureaucraticLabel.text = e.BureaucraticLabel;
            }

            _panelRoot.SetActive(true);
            Time.timeScale = 0f;
        }

        private void Hide()
        {
            Time.timeScale = 1f;
            if (_panelRoot != null) _panelRoot.SetActive(false);
            _pendingEthical = null;
            _pendingBureaucratic = null;
        }

        private void OnEthicalClicked()
        {
            var cb = _pendingEthical;
            Hide();
            cb?.Invoke();
        }

        private void OnBureaucraticClicked()
        {
            var cb = _pendingBureaucratic;
            var run = GameManager.Instance?.Run;
            var meta = GameManager.Instance?.Meta;
            bool inverted = meta?.DarkIntelligenceUnlocks?.Contains("EMPATHY_INVERSION") == true && 
                            run != null && run.DarkIntelligence > 0;

            Hide();
            cb?.Invoke(inverted);
        }

        // ── UI Construction ───────────────────────────────────

        private void BuildUI()
        {
            _panelRoot = new GameObject("DilemmaCanvas");
            _panelRoot.transform.SetParent(transform, false);

            var canvas = _panelRoot.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = _panelRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            _panelRoot.AddComponent<GraphicRaycaster>();

            // Backdrop
            var back = new GameObject("Backdrop");
            back.transform.SetParent(_panelRoot.transform, false);
            var brt = back.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bimg = back.AddComponent<Image>();
            bimg.color = UIPalette.Backdrop;

            // Card
            var card = new GameObject("Card");
            card.transform.SetParent(_panelRoot.transform, false);
            var crt = card.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot     = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(900, 480);
            var cimg = card.AddComponent<Image>();
            cimg.color = UIPalette.CardBackground;

            // Title
            CreateLabel(card.transform, "MORAL DILEMMA",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, -30), new Vector2(-40, 50),
                28, FontStyles.Bold, TextAlignmentOptions.Top,
                UIPalette.AccentWarn);

            // Prompt
            _promptText = CreateLabel(card.transform, "...",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                20, FontStyles.Italic, TextAlignmentOptions.Center,
                Color.white);
            _promptText.rectTransform.offsetMin = new Vector2(40, 130);
            _promptText.rectTransform.offsetMax = new Vector2(-40, -90);

            // Ethical button (left)
            _ethicalBtn = BuildChoiceButton(card.transform, "EthicalBtn",
                UIPalette.AccentSuccess,
                new Vector2(-220, 30), out _ethicalLabel);
            _ethicalBtn.onClick.AddListener(OnEthicalClicked);

            // Bureaucratic button (right)
            _bureaucraticBtn = BuildChoiceButton(card.transform, "BureaucraticBtn",
                new Color(0.55f, 0.35f, 0.20f),
                new Vector2(220, 30), out _bureaucraticLabel);
            _bureaucraticBtn.onClick.AddListener(OnBureaucraticClicked);

            _panelRoot.SetActive(false);
        }

        private static Button BuildChoiceButton(Transform parent, string name, Color color,
            Vector2 anchoredPos, out TMP_Text labelTmp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot     = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(380, 80);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            var c = btn.colors;
            c.normalColor      = color;
            c.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
            c.pressedColor     = Color.Lerp(color, Color.black, 0.3f);
            btn.colors = c;

            labelTmp = CreateLabel(go.transform, "...",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                18, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            labelTmp.rectTransform.offsetMin = new Vector2(12, 6);
            labelTmp.rectTransform.offsetMax = new Vector2(-12, -6);
            labelTmp.enableWordWrapping = true;
            return btn;
        }

        private static TMP_Text CreateLabel(Transform parent, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            float fontSize, FontStyles style, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = pivot; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = AccessibilitySettings.Scaled(fontSize); tmp.fontStyle = style;
            tmp.alignment = align; tmp.color = color;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            return tmp;
        }
    }
}
