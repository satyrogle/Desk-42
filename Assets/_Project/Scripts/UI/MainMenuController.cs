// ============================================================
// DESK 42 — Main Menu Controller (MonoBehaviour)
//
// Drop this on ANY GameObject in MainMenu.unity. It creates
// its own Canvas + buttons at runtime, no Inspector wiring
// required. If you have your own UI, set _autoBuildUI = false
// and assign the buttons in the Inspector.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Auto-Build UI")]
        [Tooltip("If true, build a default Canvas + 4 buttons at runtime.")]
        [SerializeField] private bool _autoBuildUI = true;
        [SerializeField] private Sprite _officeBackground;

        [Header("Manual Wiring (used when _autoBuildUI = false)")]
        [SerializeField] private Button _startNewRunBtn;
        [SerializeField] private Button _continueBtn;
        [SerializeField] private Button _dailyBriefBtn;
        [SerializeField] private Button _settingsBtn;
        [SerializeField] private Button _replayTutorialBtn;
        [SerializeField] private Button _resetOnboardingBtn;
        [SerializeField] private Button _quitBtn;

        [Header("Default Archetype")]
        [SerializeField] private string _defaultArchetypeId = "auditor";

        private void Start()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[MainMenu] GameManager not loaded. " +
                    "Press Play from the Boot scene.");
                return;
            }

            if (_autoBuildUI) BuildDefaultUI();

            WireButton(_startNewRunBtn,     OnStartNewRun);
            WireButton(_continueBtn,        OnContinue, SaveSystem.HasActiveRun());
            WireButton(_dailyBriefBtn,      OnDailyBrief);
            WireButton(_settingsBtn,        OnSettings);
            WireButton(_replayTutorialBtn,  OnReplayTutorial);
            WireButton(_resetOnboardingBtn, OnResetOnboarding);
            WireButton(_quitBtn,            OnQuit);
        }

        private static void WireButton(Button btn, UnityEngine.Events.UnityAction handler,
            bool interactable = true)
        {
            if (btn == null) return;
            btn.onClick.AddListener(handler);
            btn.interactable = interactable;
        }

        // ── Auto-Build Default UI ─────────────────────────────

        private void BuildDefaultUI()
        {
            // Canvas
            var canvasGO = new GameObject("MainMenuCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Reuse the approved desk-stage composition so the front door and
            // gameplay feel like the same place, even while final art is pending.
            var background = CreatePanel(canvasGO.transform, "OfficeBackground",
                Vector2.zero, new Vector2(1920f, 1080f),
                Color.white);
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.sprite = _officeBackground;
            backgroundImage.preserveAspect = false;
            backgroundImage.raycastTarget = false;

            CreatePanel(canvasGO.transform, "Atmosphere",
                Vector2.zero, new Vector2(1920f, 1080f),
                new Color(0.015f, 0.075f, 0.065f, 0.56f));

            var docket = CreatePanel(canvasGO.transform, "EmployeeDocket",
                new Vector2(-658f, 0f), new Vector2(500f, 840f),
                new Color(0.035f, 0.12f, 0.105f, 0.96f));
            var docketOutline = docket.AddComponent<Outline>();
            docketOutline.effectColor = new Color(0.72f, 0.58f, 0.28f, 0.95f);
            docketOutline.effectDistance = new Vector2(2f, -2f);

            // EventSystem (if missing)
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Title — adjusts to NG+ state once the loop is broken.
            var meta = GameManager.Instance?.Meta;
            bool escaped = meta != null && meta.HasEscapedTheLoop;

            var title = CreateLabel(canvasGO.transform,
                escaped ? "MIDDLE MANAGEMENT" : "DESK 42",
                new Vector2(-684, 356), escaped ? 48 : 62);
            title.rectTransform.sizeDelta = new Vector2(430, 80);
            title.alignment = TextAlignmentOptions.Left;
            title.color = escaped
                ? new Color(0.65f, 0.95f, 0.65f)
                : new Color(0.9f, 0.85f, 0.7f);

            var subtitle = CreateLabel(canvasGO.transform,
                "CLAIMS INTAKE // EMPLOYEE ACCESS",
                new Vector2(-684, 306), 17);
            subtitle.rectTransform.sizeDelta = new Vector2(430, 40);
            subtitle.alignment = TextAlignmentOptions.Left;
            subtitle.color = new Color(0.62f, 0.74f, 0.67f);

            if (escaped)
            {
                var banner = CreateLabel(canvasGO.transform,
                    "* PROMOTION CONFIRMED. NEW PARADIGM AVAILABLE.\n" +
                    "* The Middle Manager archetype is now selectable.",
                    new Vector2(0, 145), 16);
                banner.color = new Color(0.55f, 0.95f, 0.55f);
            }

            // Buttons stacked vertically, centered
            _startNewRunBtn     = CreateButton(canvasGO.transform, "CLOCK IN — NEW RUN", new Vector2(-684,  220), true);
            _continueBtn        = CreateButton(canvasGO.transform, "RESUME CASELOAD",     new Vector2(-684,  150));
            _dailyBriefBtn      = CreateButton(canvasGO.transform, "DAILY BRIEF",         new Vector2(-684,   80));
            _settingsBtn        = CreateButton(canvasGO.transform, "ACCESSIBILITY",       new Vector2(-684,   10));
            _replayTutorialBtn  = CreateButton(canvasGO.transform, "REPLAY ORIENTATION",  new Vector2(-684,  -60));
            _resetOnboardingBtn = CreateButton(canvasGO.transform, "RESET ONBOARDING",    new Vector2(-684, -130));
            _quitBtn            = CreateButton(canvasGO.transform, "CLOCK OUT",           new Vector2(-684, -220));

            var footer = CreateLabel(canvasGO.transform,
                "PROPERTY OF THE ORGANIZATION\nUNAUTHORIZED CLARITY IS A POLICY VIOLATION",
                new Vector2(-684, -340), 13);
            footer.rectTransform.sizeDelta = new Vector2(430, 60);
            footer.alignment = TextAlignmentOptions.Left;
            footer.color = new Color(0.55f, 0.64f, 0.58f);
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPos,
            bool primary = false)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(430, 56);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = primary
                ? new Color(0.72f, 0.58f, 0.28f, 0.98f)
                : new Color(0.07f, 0.20f, 0.17f, 0.98f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.58f, 0.28f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = img.color;
            colors.highlightedColor = new Color(0.22f, 0.42f, 0.34f, 1f);
            colors.pressedColor     = new Color(0.11f, 0.28f, 0.23f, 1f);
            colors.disabledColor    = new Color(0.08f, 0.12f, 0.11f, 0.8f);
            btn.colors = colors;

            var buttonLabel = CreateLabel(go.transform, label, Vector2.zero, 18);
            var labelRT = buttonLabel.rectTransform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            buttonLabel.enableWordWrapping = false;
            buttonLabel.overflowMode = TextOverflowModes.Overflow;
            buttonLabel.maxVisibleCharacters = int.MaxValue;
            buttonLabel.fontStyle = FontStyles.Bold;
            buttonLabel.color = primary
                ? new Color(0.035f, 0.10f, 0.085f)
                : new Color(0.88f, 0.85f, 0.72f);
            return btn;
        }

        private static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static TMP_Text CreateLabel(Transform parent, string text,
            Vector2 anchoredPos, float fontSize)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 80);
            rt.anchoredPosition = anchoredPos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text         = text;
            tmp.alignment    = TextAlignmentOptions.Center;
            tmp.fontSize     = AccessibilitySettings.Scaled(fontSize);
            tmp.color        = Color.white;
            return tmp;
        }

        // ── Button Handlers ───────────────────────────────────

        private void OnStartNewRun()
        {
            // Chain: archetype picker → vow picker → start
            var archPicker = GetOrAdd<ArchetypePickerPanel>();
            archPicker.Show(archetypeId =>
            {
                var vowPicker = GetOrAdd<VowPickerPanel>();
                vowPicker.Show(vows =>
                {
                    GameManager.Instance.StartNewRun(archetypeId);

                    // Apply vows to the just-created run
                    var run = GameManager.Instance.Run?.RawData;
                    if (run != null && vows != null && vows.Count > 0)
                    {
                        run.ActiveVows.Clear();
                        run.ActiveVows.AddRange(vows);
                    }
                });
            });
        }

        private T GetOrAdd<T>() where T : MonoBehaviour
        {
            var existing = GetComponent<T>();
            return existing != null ? existing : gameObject.AddComponent<T>();
        }

        private void OnContinue()
        {
            GameManager.Instance.ContinueRun();
        }

        private void OnDailyBrief()
        {
            GameManager.Instance.StartDailyBriefRun(_defaultArchetypeId);
        }

        private void OnSettings()
        {
            var panel = GetOrAdd<AccessibilitySettingsPanel>();
            panel.Show();
        }

        private void OnReplayTutorial()
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null) return;
            meta.TutorialCompleted = false;
            SaveSystem.SaveMeta(meta);
            Debug.Log("[MainMenu] Tutorial reset — will run on next Shift.");
        }

        private void OnResetOnboarding()
        {
            // Full drip-feed reset. Returns the player to Phase 1
            // (pure stamping) AND re-arms the HR Memo tutorial. Use
            // when you want to experience the new-player escalation
            // from scratch — IsPhaseUnlocked() will gate Impatience
            // (Phase 2), Soul/Sanity (Phase 3), and Entropy + audio
            // (Phase 4) until you complete runs to climb back up.
            var meta = GameManager.Instance?.Meta;
            if (meta == null) return;
            meta.HighestPhaseReached = 1;
            meta.TutorialCompleted   = false;
            SaveSystem.SaveMeta(meta);
            Debug.Log("[MainMenu] Onboarding reset — drip-feed restarts at Phase 1.");
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
