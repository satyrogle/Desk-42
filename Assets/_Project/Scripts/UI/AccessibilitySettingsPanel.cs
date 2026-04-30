// ============================================================
// DESK 42 — Accessibility Settings Panel (MonoBehaviour)
//
// Self-bootstrapping runtime UI. Builds its own Canvas the
// first time Show() is called and toggles visibility from then
// on. Reads/writes Desk42.Accessibility.AccessibilitySettings.
//
// Add via MainMenu (or any other menu) by calling Show().
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class AccessibilitySettingsPanel : MonoBehaviour
    {
        private GameObject _root;
        private Action     _onClose;

        public void Show(Action onClose = null)
        {
            _onClose = onClose;
            if (_root == null) BuildUI();
            _root.SetActive(true);
            RefreshControls();
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            _onClose?.Invoke();
            _onClose = null;
        }

        // ── UI Construction ───────────────────────────────────

        private Toggle _reducedMotionToggle;
        private Toggle _disableDistortToggle;
        private Toggle _highContrastToggle;
        private Toggle _holdToConfirmToggle;
        private Slider _textScaleSlider;
        private TMP_Text _textScaleValueLabel;

        private void BuildUI()
        {
            // Canvas
            var canvasGO = new GameObject("AccessibilitySettingsCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Backdrop (also catches clicks outside the card)
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(canvasGO.transform, false);
            var brt = backdrop.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bImg = backdrop.AddComponent<Image>();
            bImg.color = new Color(0f, 0f, 0f, 0.78f);

            // Card
            var card = new GameObject("Card");
            card.transform.SetParent(canvasGO.transform, false);
            var crt = card.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot     = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(600, 540);
            crt.anchoredPosition = Vector2.zero;
            var cImg = card.AddComponent<Image>();
            cImg.color = new Color(0.10f, 0.10f, 0.12f, 0.98f);

            // Title
            CreateLabel(card.transform, "ACCESSIBILITY",
                new Vector2(0, 230), 32, FontStyles.Bold,
                new Color(0.85f, 0.80f, 0.65f));

            float row = 150f;
            const float rowStep = 55f;

            _reducedMotionToggle  = CreateToggle(card.transform,
                "Reduced motion (no flash, shake)", row, AccessibilitySettings.ReducedMotion,
                v => AccessibilitySettings.ReducedMotion = v);
            row -= rowStep;

            _disableDistortToggle = CreateToggle(card.transform,
                "Disable screen distortion (sanity FX)", row,
                AccessibilitySettings.DisableScreenDistortion,
                v => AccessibilitySettings.DisableScreenDistortion = v);
            row -= rowStep;

            _highContrastToggle   = CreateToggle(card.transform,
                "High contrast UI", row, AccessibilitySettings.HighContrast,
                v => AccessibilitySettings.HighContrast = v);
            row -= rowStep;

            _holdToConfirmToggle  = CreateToggle(card.transform,
                "Hold to confirm irreversible actions", row,
                AccessibilitySettings.HoldToConfirm,
                v => AccessibilitySettings.HoldToConfirm = v);
            row -= rowStep + 10;

            // Text scale row: label + slider + numeric
            CreateLabel(card.transform, "Text scale",
                new Vector2(-220, row), 20, FontStyles.Normal, Color.white);

            _textScaleSlider = CreateSlider(card.transform,
                new Vector2(40, row), AccessibilitySettings.MinTextScale,
                AccessibilitySettings.MaxTextScale, AccessibilitySettings.TextScale,
                v =>
                {
                    AccessibilitySettings.TextScale = v;
                    if (_textScaleValueLabel != null)
                        _textScaleValueLabel.text = $"{v:0.00}x";
                });

            _textScaleValueLabel = CreateLabel(card.transform,
                $"{AccessibilitySettings.TextScale:0.00}x",
                new Vector2(230, row), 20, FontStyles.Normal,
                new Color(0.85f, 0.85f, 0.85f));

            // Close button
            var closeBtn = CreateButton(card.transform, "Close",
                new Vector2(0, -210), v => Hide());

            _root = canvasGO;
        }

        private void RefreshControls()
        {
            if (_reducedMotionToggle  != null) _reducedMotionToggle.isOn  = AccessibilitySettings.ReducedMotion;
            if (_disableDistortToggle != null) _disableDistortToggle.isOn = AccessibilitySettings.DisableScreenDistortion;
            if (_highContrastToggle   != null) _highContrastToggle.isOn   = AccessibilitySettings.HighContrast;
            if (_holdToConfirmToggle  != null) _holdToConfirmToggle.isOn  = AccessibilitySettings.HoldToConfirm;
            if (_textScaleSlider      != null) _textScaleSlider.value     = AccessibilitySettings.TextScale;
            if (_textScaleValueLabel  != null) _textScaleValueLabel.text  = $"{AccessibilitySettings.TextScale:0.00}x";
        }

        // ── Widget Builders ───────────────────────────────────

        private static Toggle CreateToggle(Transform parent, string label,
            float yOffset, bool initial, Action<bool> onChanged)
        {
            var go = new GameObject($"Toggle_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(540, 40);
            rt.anchoredPosition = new Vector2(0, yOffset);

            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0f);

            var toggle = go.AddComponent<Toggle>();
            toggle.isOn = initial;

            // Checkbox box
            var box = new GameObject("Box");
            box.transform.SetParent(go.transform, false);
            var boxRt = box.AddComponent<RectTransform>();
            boxRt.anchorMin = new Vector2(0, 0.5f);
            boxRt.anchorMax = new Vector2(0, 0.5f);
            boxRt.pivot     = new Vector2(0, 0.5f);
            boxRt.sizeDelta = new Vector2(28, 28);
            boxRt.anchoredPosition = new Vector2(8, 0);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.20f, 0.20f, 0.24f, 1f);

            var check = new GameObject("Checkmark");
            check.transform.SetParent(box.transform, false);
            var crt = check.AddComponent<RectTransform>();
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(4, 4); crt.offsetMax = new Vector2(-4, -4);
            var checkImg = check.AddComponent<Image>();
            checkImg.color = new Color(0.85f, 0.80f, 0.65f, 1f);

            toggle.targetGraphic = boxImg;
            toggle.graphic       = checkImg;

            // Label
            var lbl = CreateLabel(go.transform, label,
                new Vector2(50, 0), 20, FontStyles.Normal, Color.white);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0, 0.5f);
            lrt.anchorMax = new Vector2(0, 0.5f);
            lrt.pivot     = new Vector2(0, 0.5f);
            lrt.sizeDelta = new Vector2(480, 30);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        private static Slider CreateSlider(Transform parent, Vector2 anchoredPos,
            float min, float max, float initial, Action<float> onChanged)
        {
            var go = new GameObject("Slider");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260, 20);
            rt.anchoredPosition = anchoredPos;

            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.20f, 0.20f, 0.24f, 1f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.AddComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
            faRt.offsetMin = new Vector2(5, 5); faRt.offsetMax = new Vector2(-15, -5);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.85f, 0.80f, 0.65f, 1f);

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var haRt = handleArea.AddComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(10, 0); haRt.offsetMax = new Vector2(-10, 0);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var hRt = handle.AddComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(20, 28);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.95f, 0.92f, 0.80f, 1f);

            var slider = go.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect      = fillRt;
            slider.handleRect    = hRt;
            slider.direction     = Slider.Direction.LeftToRight;
            slider.minValue      = min;
            slider.maxValue      = max;
            slider.value         = initial;
            slider.onValueChanged.AddListener(v => onChanged(v));

            return slider;
        }

        private static Button CreateButton(Transform parent, string label,
            Vector2 anchoredPos, Action<Button> onClick)
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

            btn.onClick.AddListener(() => onClick(btn));
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
            rt.sizeDelta = new Vector2(560, 40);
            rt.anchoredPosition = anchoredPos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text         = text;
            tmp.alignment    = TextAlignmentOptions.Center;
            tmp.fontSize     = fontSize;
            tmp.fontStyle    = style;
            tmp.color        = color;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
