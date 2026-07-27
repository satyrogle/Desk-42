// ============================================================
// DESK 42 — Pneumatic Tube (MonoBehaviour)
//
// Sprint 3 "Dark Economy" mechanic. Sits on the CorpOS desktop
// as a UI button-shaped drop target. When the player drags a
// ClientView portrait into it, the client is liquified:
// the claim is bypassed, the player earns Dark Intelligence
// currency, and there's a ~30% chance the UI fines them
// 50 credits for "organic matter jammed in tube 4".
//
// Self-bootstrapping. Builds its own button image + label on
// first BuildUI() call. Auto-injected into the Shift scene by
// HighImpactInjector.
//
// Gated on Phase 4 — invisible until the player has reached
// the full game. (DraggableClientPortrait also gates pickup on
// Phase 4, so this is belt-and-suspenders.)
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;
using Desk42.Core;
using Desk42.Encounter;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class PneumaticTube : MonoBehaviour, IDropHandler
    {
        [Tooltip("Phase gate. Tube is invisible below this phase.")]
        [SerializeField] private int _requiredPhase = 4;

        [Tooltip("Chance per liquify that the OSHA maintenance fee fires.")]
        [Range(0f, 1f)]
        [SerializeField] private float _oshaFeeChance = 0.30f;

        [Tooltip("Credit penalty when the OSHA fee fires.")]
        [SerializeField] private int _oshaFee = 50;

        private GameObject _root;
        private TMP_Text _feeLabel;
        private Coroutine _unavailableRoutine;

        private void Start()
        {
            BuildUI();
            RefreshVisibility();
        }

        private void Update()
        {
            RefreshVisibility();
        }

        // ── Drop target ───────────────────────────────────────

        public void OnDrop(PointerEventData eventData)
        {
            if (!(GameManager.Instance?.IsPhaseUnlocked(_requiredPhase) ?? false)) return;

            var portrait = eventData.pointerDrag?.GetComponent<DraggableClientPortrait>();
            if (portrait == null) return;

            var encounter = Desk42Services.Get<EncounterManager>();
            if (encounter == null) return;

            if (!encounter.TryLiquify(out string unavailableReason))
            {
                ShowUnavailable(unavailableReason);
                return;
            }

            PlayWhirAndDing();
            MaybeChargeOshaFee();
        }

        // ── Feedback ──────────────────────────────────────────

        private void PlayWhirAndDing()
        {
            // Routed through the AudioService boundary rather than FMODManager:
            // gameplay must not know FMOD exists. World position is preserved,
            // so the spatial intent of the original call survives the migration.
            // With the Null backend active this is a reported no-op, not silence
            // masquerading as success. The OSHA fee label below remains the
            // visible feedback either way.
            Audio.AudioService.PlayOneShot(
                Audio.ProofAudioEvent.PneumaticTubeThreat,
                Audio.AudioRequestContext.AtPosition(
                    GameManager.Instance?.Run?.ShiftNumber ?? 0, transform.position));

            if (!AccessibilitySettings.ReducedMotion)
                StartCoroutine(PulseRoot());
        }

        private void MaybeChargeOshaFee()
        {
            if (SeedEngine.NextFloat(SeedStream.RumorMillEvents) > _oshaFeeChance) return;

            var run = GameManager.Instance?.Run;
            if (run?.RawData == null) return;

            run.RawData.CorporateCredits = Mathf.Max(0,
                run.RawData.CorporateCredits - _oshaFee);

            if (_feeLabel != null)
                StartCoroutine(FlashFee());

            Debug.Log($"[PneumaticTube] OSHA maintenance fee: -{_oshaFee} credits.");
        }

        private System.Collections.IEnumerator FlashFee()
        {
            if (_feeLabel == null) yield break;
            _feeLabel.text = $"Error: Organic matter jammed in Tube 4.\n" +
                             $"Maintenance fee: -{_oshaFee} credits.";

            var c = _feeLabel.color;
            for (float t = 0; t < 3f; t += Time.deltaTime)
            {
                _feeLabel.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t / 3f));
                yield return null;
            }
            _feeLabel.text = "";
        }

        private void ShowUnavailable(string failureReason)
        {
            if (_feeLabel == null) return;
            if (_unavailableRoutine != null)
                StopCoroutine(_unavailableRoutine);
            _unavailableRoutine =
                StartCoroutine(FlashUnavailable(failureReason));
        }

        private System.Collections.IEnumerator FlashUnavailable(
            string failureReason)
        {
            _feeLabel.color = UIPalette.AccentWarn;
            _feeLabel.text = string.Equals(
                    failureReason,
                    EliasProofSessionController.ContinuityHoldFailureReason,
                    System.StringComparison.Ordinal)
                ? "LIQUIFY UNAVAILABLE - CONTINUITY REVIEW HOLD"
                : $"LIQUIFY UNAVAILABLE - {failureReason}";
            yield return new WaitForSeconds(2.5f);
            _feeLabel.text = "";
            _unavailableRoutine = null;
        }

        private System.Collections.IEnumerator PulseRoot()
        {
            if (_root == null) yield break;
            var rt = _root.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector3 baseScale = rt.localScale;
            for (float t = 0; t < 0.25f; t += Time.deltaTime)
            {
                float s = 1f + Mathf.Sin(t / 0.25f * Mathf.PI) * 0.15f;
                rt.localScale = baseScale * s;
                yield return null;
            }
            rt.localScale = baseScale;
        }

        // ── Visibility ────────────────────────────────────────

        private void RefreshVisibility()
        {
            if (_root == null) return;
            bool show = GameManager.Instance?.IsPhaseUnlocked(_requiredPhase) ?? false;
            if (_root.activeSelf != show) _root.SetActive(show);
        }

        // ── UI ────────────────────────────────────────────────

        private void BuildUI()
        {
            // Own canvas so the tube sits on top of the standard HUD
            var canvasGO = new GameObject("PneumaticTubeCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Drop target — pinned top-right of the desk
            _root = new GameObject("TubeButton");
            _root.transform.SetParent(canvasGO.transform, false);
            var rt = _root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(140, 140);
            rt.anchoredPosition = new Vector2(-40, -120);

            var img = _root.AddComponent<Image>();
            img.color = new Color(0.20f, 0.20f, 0.25f, 0.95f);

            // Inner label so it's obvious what this is
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(_root.transform, false);
            var lrt = lblGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(6, 6);
            lrt.offsetMax = new Vector2(-6, -6);
            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "TUBE 4\n[OSHA]";
            tmp.fontSize = AccessibilitySettings.Scaled(18);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UIPalette.AccentWarn;
            tmp.raycastTarget = false;

            // OSHA fee label appears below the tube when fee fires
            var feeGO = new GameObject("FeeLabel");
            feeGO.transform.SetParent(canvasGO.transform, false);
            var frt = feeGO.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(1, 1);
            frt.anchorMax = new Vector2(1, 1);
            frt.pivot = new Vector2(1, 1);
            frt.sizeDelta = new Vector2(380, 60);
            frt.anchoredPosition = new Vector2(-40, -270);
            _feeLabel = feeGO.AddComponent<TextMeshProUGUI>();
            _feeLabel.text = "";
            _feeLabel.fontSize = AccessibilitySettings.Scaled(14);
            _feeLabel.color = UIPalette.AccentWarn;
            _feeLabel.alignment = TextAlignmentOptions.Right;
            _feeLabel.raycastTarget = false;
        }
    }
}
