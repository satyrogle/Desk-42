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
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

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
                new Vector2(0, 200), escaped ? 52 : 64);
            title.color = escaped
                ? new Color(0.65f, 0.95f, 0.65f)
                : new Color(0.9f, 0.85f, 0.7f);

            if (escaped)
            {
                var banner = CreateLabel(canvasGO.transform,
                    "* PROMOTION CONFIRMED. NEW PARADIGM AVAILABLE.\n" +
                    "* The Middle Manager archetype is now selectable.",
                    new Vector2(0, 145), 16);
                banner.color = new Color(0.55f, 0.95f, 0.55f);
            }

            // Buttons stacked vertically, centered
            _startNewRunBtn     = CreateButton(canvasGO.transform, "Start New Run",    new Vector2(0,  150));
            _continueBtn        = CreateButton(canvasGO.transform, "Continue",         new Vector2(0,   95));
            _dailyBriefBtn      = CreateButton(canvasGO.transform, "Daily Brief",      new Vector2(0,   40));
            _settingsBtn        = CreateButton(canvasGO.transform, "Accessibility",    new Vector2(0,  -15));
            _replayTutorialBtn  = CreateButton(canvasGO.transform, "Replay Tutorial",  new Vector2(0,  -70));
            _resetOnboardingBtn = CreateButton(canvasGO.transform, "Reset Onboarding", new Vector2(0, -125));
            _quitBtn            = CreateButton(canvasGO.transform, "Quit",             new Vector2(0, -200));
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPos)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(280, 50);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = new Color(0.2f, 0.2f, 0.25f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f);
            colors.pressedColor     = new Color(0.5f, 0.5f, 0.55f);
            colors.disabledColor    = new Color(0.15f, 0.15f, 0.18f);
            btn.colors = colors;

            CreateLabel(go.transform, label, Vector2.zero, 22);
            return btn;
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
