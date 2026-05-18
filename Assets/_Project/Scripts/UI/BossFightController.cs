// ============================================================
// DESK 42 — Boss Fight Controller (MonoBehaviour)
//
// The Layer 4 finale orchestrator. Spawned by the TaskMgr_Override
// desktop icon when the player double-clicks. Owns the boss-fight
// canvas and the lifecycle of every defensive widget: the runaway
// Log Out button, the HR popup spammer, the Stapler tool, and the
// RAM-overload meter.
//
// Win conditions (either):
//   1. Catch the runaway Log Out button and click it (skill path)
//   2. Overload the RAM meter to 100% (puzzle path) — this also
//      stops the button from running, making it easy to catch
//
// Either path ends with a call to
// CorpOSWindowManager.TriggerLayer4Ending(), which handles the
// BSOD + achievement + telemetry + SaveMeta + Quit sequence.
//
// Emits telemetry on start (BossFightStarted) and on each win
// path resolution (the BSOD sequence emits BossFightWon /
// CrashToWinCompleted itself).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Core;
using Desk42.Meta.Analytics;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class BossFightController : MonoBehaviour
    {
        private GameObject _root;
        private Canvas _canvas;
        private RunawayLogoutButton _logoutBtn;
        private HRPopupSpammer _spammer;
        private RAMOverloadMeter _ram;
        private StaplerTool _stapler;
        private bool _won;

        private void Start()
        {
            Analytics.Track(TelemetryEvents.BossFightStarted);
            BuildCanvas();
            SpawnConfirmationDialog();
        }

        private void BuildCanvas()
        {
            // Own canvas so we can render above everything else
            var canvasGO = new GameObject("BossFightCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 950;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            _root = canvasGO;

            // Dim backdrop
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(_root.transform, false);
            var brt = backdrop.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            backdrop.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        }

        // ── Phase 1 — Confirmation Dialog ─────────────────────

        private GameObject _confirmDialog;
        private void SpawnConfirmationDialog()
        {
            _confirmDialog = new GameObject("ConfirmDialog");
            _confirmDialog.transform.SetParent(_root.transform, false);
            var rt = _confirmDialog.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620, 240);
            _confirmDialog.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.16f, 1f);

            // Clippy-tone warning
            var prompt = CreateLabel(_confirmDialog.transform,
                "It looks like you're trying to LOG OUT.\n\n" +
                "Logging out will result in immediate termination of " +
                "your pension and physical form.\n\nAre you sure?",
                new Vector2(0, 35), 16, FontStyles.Normal, Color.white);
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.rectTransform.sizeDelta = new Vector2(580, 140);

            // Yes button (this is the runaway one)
            var yes = CreateButton(_confirmDialog.transform,
                "YES, LOG OUT",
                new Vector2(-110, -80), new Color(0.55f, 0.20f, 0.20f),
                OnYesClicked);

            // No button (safe — closes the dialog and ends the fight)
            CreateButton(_confirmDialog.transform,
                "Cancel",
                new Vector2(110, -80), new Color(0.20f, 0.20f, 0.25f),
                Abort);
        }

        private void OnYesClicked()
        {
            // First Yes click: the system "retaliates" — replace the
            // confirmation with the boss-fight layout.
            if (_confirmDialog != null) Destroy(_confirmDialog);
            _confirmDialog = null;

            BeginBossFight();
        }

        // ── Phase 2 — Boss Fight Active ───────────────────────

        private void BeginBossFight()
        {
            // Runaway Log Out button (the win-state click target)
            var runawayGO = new GameObject("RunawayLogout");
            runawayGO.transform.SetParent(_root.transform, false);
            _logoutBtn = runawayGO.AddComponent<RunawayLogoutButton>();
            _logoutBtn.OnCaught = HandleWin;

            // HR popup spammer
            var spamGO = new GameObject("HRPopupSpammer");
            spamGO.transform.SetParent(_root.transform, false);
            _spammer = spamGO.AddComponent<HRPopupSpammer>();

            // RAM overload meter + recycle bin
            var ramGO = new GameObject("RAMOverloadMeter");
            ramGO.transform.SetParent(_root.transform, false);
            _ram = ramGO.AddComponent<RAMOverloadMeter>();
            _ram.OnOverload = HandleRamOverload;

            // Stapler tool — toggleable mode for pinning popups
            var staplerGO = new GameObject("StaplerTool");
            staplerGO.transform.SetParent(_root.transform, false);
            _stapler = staplerGO.AddComponent<StaplerTool>();
        }

        // ── Win paths ─────────────────────────────────────────

        private void HandleRamOverload()
        {
            // Overload freezes the defenses, but the win is still
            // gated on the player clicking the (now stationary)
            // Log Out button.
            _logoutBtn?.FreezeDefenses();
            _spammer?.FreezeDefenses();
        }

        private void HandleWin()
        {
            if (_won) return;
            _won = true;

            // Dispose UI immediately so the BSOD overlay isn't fighting
            // with HR popups for the player's last second of attention.
            _spammer?.Cease();

            var corpos = Desk42Services.Get<CorpOSWindowManager>();
            if (corpos != null)
            {
                corpos.TriggerLayer4Ending();
            }
            else
            {
                Debug.LogError("[BossFight] No CorpOSWindowManager — Layer 4 ending cannot fire.");
            }
        }

        private void Abort()
        {
            Destroy(gameObject);
        }

        // ── Helpers ───────────────────────────────────────────

        private static TMP_Text CreateLabel(Transform parent, string text,
            Vector2 anchoredPos, float fontSize, FontStyles style, Color color)
        {
            var go = new GameObject($"Lbl_{text.Substring(0, System.Math.Min(8, text.Length))}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(560, 80);
            rt.anchoredPosition = anchoredPos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = AccessibilitySettings.Scaled(fontSize);
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string label,
            Vector2 anchoredPos, Color color, System.Action onClick)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(180, 50);
            rt.anchoredPosition = anchoredPos;

            go.AddComponent<Image>().color = color;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var lbl = new GameObject("Lbl");
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = AccessibilitySettings.Scaled(16);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return btn;
        }
    }
}
