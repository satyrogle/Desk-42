// ============================================================
// DESK 42 — Desk Entropy Renderer (MonoBehaviour)
//
// Translates the abstract DeskEntropy float (0-1 on RunData)
// into visible desk deterioration. As entropy rises, the
// desk progressively degrades through four visual states:
//
//   0.00–0.25  Pristine     — standard desk, no effects
//   0.26–0.50  Cluttered    — scattered papers, subtle desaturation
//   0.51–0.75  Deteriorated — coffee stain overlay, flickering lamp
//   0.76–1.00  Collapsed    — crumpled paper pile, broken items, heavy vignette
//
// The renderer subscribes to:
//   MoralChoiceEvent     — unethical choice → small entropy bump visual
//   OfficeHazardEvent    — hazard → spike visual + screenshake
//   ShiftLifecycleEvent  — reset on shift start
//
// Implementation note:
//   The actual desk visual is a layered canvas: each entropy
//   tier has a CanvasGroup that fades in at its threshold.
//   The shader-based effects (desaturation, vignette) are
//   driven via a post-process profile reference, or via
//   material properties on a fullscreen overlay image if
//   URP post-process isn't configured yet.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class DeskEntropyRenderer : MonoBehaviour
    {
        // ── Entropy Tiers ─────────────────────────────────────

        [Header("Entropy Tier Layers")]
        [SerializeField] private CanvasGroup _tier1Clutter;       // 0.25+
        [SerializeField] private CanvasGroup _tier2Deteriorated;  // 0.50+
        [SerializeField] private CanvasGroup _tier3Collapsed;     // 0.75+

        [Header("Vignette Overlay")]
        [SerializeField] private Image _vignetteOverlay;
        [SerializeField] private Color _vignetteColorMin = new(0f, 0f, 0f, 0f);
        [SerializeField] private Color _vignetteColorMax = new(0f, 0f, 0f, 0.55f);

        [Header("Desaturation Overlay")]
        [SerializeField] private Image    _desaturationOverlay;
        [SerializeField] private Material _desaturationMaterial;  // grey tint overlay

        [Header("Lamp Flicker")]
        [SerializeField] private Light   _deskLamp;
        [SerializeField] private float   _flickerIntensityMin = 0.6f;
        [SerializeField] private float   _flickerIntensityMax = 1.2f;
        [SerializeField] private float   _flickerSpeed        = 8f;

        [Header("Hazard Feedback")]
        [SerializeField] private CanvasGroup _hazardFlash;
        [SerializeField] private float       _hazardFlashDuration = 0.25f;

        [Header("Transition Speed")]
        [SerializeField] private float _fadeSpeed = 2f;

        // ── State ─────────────────────────────────────────────

        private float _currentEntropy;
        private float _targetEntropy;
        private bool  _flickerActive;
        private float _desaturationAmount;

        // ── Unity Lifecycle ───────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnMoralChoice   += HandleMoralChoice;
            RumorMill.OnOfficeHazard  += HandleHazard;
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;
        }

        private void OnDisable()
        {
            RumorMill.OnMoralChoice   -= HandleMoralChoice;
            RumorMill.OnOfficeHazard  -= HandleHazard;
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;
        }

        private void Start()
        {
            _currentEntropy = GameManager.Instance?.Run?.DeskEntropy ?? 0f;
            _targetEntropy  = _currentEntropy;
            ForceApplyEntropy(_currentEntropy);
        }

        private void Update()
        {
            // Pull live entropy from run state each frame
            float liveEntropy = GameManager.Instance?.Run?.DeskEntropy ?? 0f;
            _targetEntropy = liveEntropy;

            // Smooth toward target
            _currentEntropy = Mathf.MoveTowards(
                _currentEntropy, _targetEntropy, _fadeSpeed * Time.deltaTime);

            ApplyEntropy(_currentEntropy);

            // Lamp flicker when deteriorated
            if (_flickerActive && _deskLamp != null)
            {
                _deskLamp.intensity = Mathf.Lerp(
                    _flickerIntensityMin, _flickerIntensityMax,
                    (Mathf.Sin(Time.time * _flickerSpeed) + 1f) * 0.5f);
            }
        }

        // ── Entropy Application ───────────────────────────────

        private void ApplyEntropy(float entropy)
        {
            // Tier alphas — each tier fades in progressively
            SetAlpha(_tier1Clutter,      Mathf.InverseLerp(0.20f, 0.40f, entropy));
            SetAlpha(_tier2Deteriorated, Mathf.InverseLerp(0.45f, 0.60f, entropy));
            SetAlpha(_tier3Collapsed,    Mathf.InverseLerp(0.70f, 0.85f, entropy));

            // Vignette darkens from entropy 0.5 onward
            float vigT = Mathf.InverseLerp(0.50f, 1.00f, entropy);
            if (_vignetteOverlay != null)
                _vignetteOverlay.color = Color.Lerp(_vignetteColorMin, _vignetteColorMax, vigT);

            // Desaturation overlay opacity
            _desaturationAmount = Mathf.InverseLerp(0.30f, 0.80f, entropy);
            if (_desaturationOverlay != null)
                _desaturationOverlay.color = new Color(1f, 1f, 1f, _desaturationAmount * 0.35f);

            // Start/stop lamp flicker
            bool shouldFlicker = entropy >= 0.50f;
            if (shouldFlicker != _flickerActive)
            {
                _flickerActive = shouldFlicker;
                if (!shouldFlicker && _deskLamp != null)
                    _deskLamp.intensity = _flickerIntensityMax;
            }
        }

        private void ForceApplyEntropy(float entropy)
        {
            _currentEntropy = entropy;
            ApplyEntropy(entropy);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleMoralChoice(MoralChoiceEvent e)
        {
            if (!e.WasUnethical) return;
            // Entropy is updated by RunStateController; we just do a visual spike here
            StartCoroutine(EntropySpike(0.05f));
        }

        private void HandleHazard(OfficeHazardEvent e)
        {
            float spike = e.HazardType switch
            {
                OfficeHazardType.SystemCrash      => 0.15f,
                OfficeHazardType.MandatoryMeeting => 0.05f,
                OfficeHazardType.FireDrill         => 0.08f,
                _                                 => 0.03f,
            };

            // EntropySpike is always allowed — it's a brief visual on existing geometry.
            StartCoroutine(EntropySpike(spike));

            // HazardFlash (screen-covering CanvasGroup) only fires when
            // GlassCracking layer is clear — it's the entry point for
            // expansion-tier screen obstruction.
            if (EntropyManager.CanActivate(EntropyLayer.GlassCracking))
                StartCoroutine(HazardFlash());
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart)
                ForceApplyEntropy(0f);
        }

        // ── Visual Coroutines ─────────────────────────────────

        private IEnumerator EntropySpike(float amount)
        {
            // Briefly boost the displayed entropy above the true value for visual punch
            float spike = Mathf.Min(_currentEntropy + amount, 1f);
            float t = 0f;

            while (t < 0.2f)
            {
                t += Time.deltaTime;
                ApplyEntropy(Mathf.Lerp(spike, _currentEntropy, t / 0.2f));
                yield return null;
            }
        }

        private IEnumerator HazardFlash()
        {
            if (_hazardFlash == null) yield break;

            _hazardFlash.alpha = 0.7f;
            float t = 0f;
            while (t < _hazardFlashDuration)
            {
                t += Time.deltaTime;
                _hazardFlash.alpha = Mathf.Lerp(0.7f, 0f, t / _hazardFlashDuration);
                yield return null;
            }
            _hazardFlash.alpha = 0f;
        }

        // ── Public API (for editor preview) ──────────────────

        /// <summary>Preview a specific entropy level in-editor.</summary>
        public void PreviewEntropy(float entropy)
        {
            ForceApplyEntropy(Mathf.Clamp01(entropy));
        }
    }
}
