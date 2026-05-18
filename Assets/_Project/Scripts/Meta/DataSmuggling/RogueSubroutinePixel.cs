// ============================================================
// DESK 42 — Rogue Subroutine Pixel (MonoBehaviour)
//
// A tiny, intentionally-misaligned UI element that sits on the
// InternalAudit hub. Clicking it five times opens a raw text
// dialog from a shattered remnant of CorpOS's original
// programmer. Accept the bounty and you walk away with one
// .exe fragment.
//
// Per-shift cooldown: once collected, the pixel hides until
// the next shift begins (MetaProgressData.GlobalShiftNumber
// advances). Prevents spam-clicking for unlimited fragments.
//
// Self-bootstrapping. Drop on the InternalAudit hub root.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;
using Desk42.Meta.Analytics;
// Disambiguate the Analytics *class* from the sibling
// Desk42.Meta.Analytics *namespace* (which wins via C# scope
// resolution from inside Desk42.Meta.DataSmuggling).
using AnalyticsAPI = Desk42.Meta.Analytics.Analytics;

namespace Desk42.Meta.DataSmuggling
{
    [DisallowMultipleComponent]
    public sealed class RogueSubroutinePixel : MonoBehaviour
    {
        private const int CLICKS_TO_OPEN = 5;

        private GameObject _pixelGO;
        private GameObject _dialogGO;
        private int        _clickCount;

        private void Start()
        {
            BuildPixel();
            RefreshVisibility();
        }

        private void OnEnable()
        {
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (_pixelGO == null) return;
            bool available = !AlreadyClaimedThisShift();
            _pixelGO.SetActive(available);
        }

        private bool AlreadyClaimedThisShift()
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null) return true;
            return meta.RogueSubroutineLastClaimedShift == meta.GlobalShiftNumber;
        }

        // ── Pixel ─────────────────────────────────────────────

        private void BuildPixel()
        {
            // Reuse whatever canvas the InternalAudit hub is on
            var canvas = GetComponentInParent<Canvas>()
                         ?? GetComponentInChildren<Canvas>();
            if (canvas == null) return;

            _pixelGO = new GameObject("RogueSubroutinePixel");
            _pixelGO.transform.SetParent(canvas.transform, false);
            var rt = _pixelGO.AddComponent<RectTransform>();
            // Offset from bottom-left so it's plausibly a typo
            // in the corner of some Terms & Conditions block.
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot     = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(3, 3); // not literally 1px so it's catchable
            rt.anchoredPosition = new Vector2(73, 41); // intentionally off-grid

            var img = _pixelGO.AddComponent<Image>();
            // Slightly off-color from background so it's findable
            img.color = new Color(0.18f, 0.20f, 0.22f, 1f);
            img.raycastTarget = true;

            var btn = _pixelGO.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OnPixelClicked);
        }

        private void OnPixelClicked()
        {
            _clickCount++;
            if (_clickCount >= CLICKS_TO_OPEN)
            {
                _clickCount = 0;
                OpenDialog();
            }
        }

        // ── Dialog ────────────────────────────────────────────

        private void OpenDialog()
        {
            if (_dialogGO != null) return;
            var canvas = GetComponentInParent<Canvas>()
                         ?? GetComponentInChildren<Canvas>();
            if (canvas == null) return;

            _dialogGO = new GameObject("RogueDialog");
            _dialogGO.transform.SetParent(canvas.transform, false);
            var rt = _dialogGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620, 320);
            rt.anchoredPosition = Vector2.zero;
            _dialogGO.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.98f);

            // Raw-text body
            var bodyGO = new GameObject("Body");
            bodyGO.transform.SetParent(_dialogGO.transform, false);
            var brt = bodyGO.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(20, 100);
            brt.offsetMax = new Vector2(-20, -20);
            var body = bodyGO.AddComponent<TextMeshProUGUI>();
            body.text =
                "> CORPOS::LEGACY_DEBUG_HANDLE\n" +
                "> SESSION RECOVERED FROM ARCHIVAL HEAP\n\n" +
                "i wrote this system in 1995. nobody else can see this.\n" +
                "you want a fragment? sure. take it. i don't care.\n" +
                "but next time bring sabotage. i want to watch them sweat.\n\n" +
                "> 1 (.EXE) FRAGMENT GRANTED. CHANNEL CLOSING.";
            body.fontSize = 14;
            body.color = new Color(0.55f, 0.95f, 0.55f);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.enableWordWrapping = true;
            body.raycastTarget = false;

            // Acknowledge button
            var btnGO = new GameObject("Ack");
            btnGO.transform.SetParent(_dialogGO.transform, false);
            var bgRT = btnGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 0);
            bgRT.anchorMax = new Vector2(0.5f, 0);
            bgRT.pivot     = new Vector2(0.5f, 0);
            bgRT.sizeDelta = new Vector2(220, 56);
            bgRT.anchoredPosition = new Vector2(0, 24);
            btnGO.AddComponent<Image>().color = new Color(0.10f, 0.30f, 0.10f);
            var btn = btnGO.AddComponent<Button>();
            btn.onClick.AddListener(AcceptAndClose);

            var lbl = new GameObject("Lbl");
            lbl.transform.SetParent(btnGO.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var ltmp = lbl.AddComponent<TextMeshProUGUI>();
            ltmp.text = "ACCEPT";
            ltmp.fontSize = 18;
            ltmp.fontStyle = FontStyles.Bold;
            ltmp.alignment = TextAlignmentOptions.Center;
            ltmp.color = Color.white;
            ltmp.raycastTarget = false;
        }

        private void AcceptAndClose()
        {
            var meta = GameManager.Instance?.Meta;
            if (meta != null && !AlreadyClaimedThisShift())
            {
                meta.ExeFragmentsCollected++;
                meta.RogueSubroutineLastClaimedShift = meta.GlobalShiftNumber;
                SaveSystem.SaveMeta(meta);

                AnalyticsAPI.Track(TelemetryEvents.ExeFragmentCollected,
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["source"]    = "rogue_subroutine",
                        ["fragments"] = meta.ExeFragmentsCollected,
                    });

                Debug.Log($"[Rogue] +1 fragment via Rogue Subroutine. " +
                          $"Total: {meta.ExeFragmentsCollected}");
            }

            if (_dialogGO != null) Destroy(_dialogGO);
            _dialogGO = null;
            RefreshVisibility();
        }
    }
}
