// ============================================================
// DESK 42 — Passive-Aggressive UI Controller (MonoBehaviour)
//
// The single MonoBehaviour that drives all passive-aggressive
// UI behaviour in the Shift scene. Lives on the UI root
// GameObject. Subscribes to RumorMill events and translates
// run-state into visual/textual feedback.
//
// Responsibilities:
//   - On NarratorToneChanged: refresh all NarratorTextElements
//   - On SoulIntegrityChanged: animate soul gauge, trigger
//     UI corruption effects at thresholds
//   - On SanityChanged: animate sanity gauge, warp bar at low
//   - On MoralChoice: flash moral injury indicator
//   - On NDASigned: delegate to NDAOverlayRenderer
//   - On ShiftPhaseChanged: update phase display
//   - Per-frame: update impatience timer display
//
// All serialized UI references are set in the Shift scene
// prefab. This script only drives them — no direct UI layout.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class PassiveAggressiveUIController : MonoBehaviour
    {
        // ── Inspector: Core Gauges ────────────────────────────

        [Header("Soul Integrity")]
        [SerializeField] private Slider   _soulSlider;
        [SerializeField] private Image    _soulFill;
        [SerializeField] private TMP_Text _soulLabel;
        [SerializeField] private Gradient _soulGradient;      // green→yellow→red as soul drops

        [Header("Sanity")]
        [SerializeField] private Slider   _sanitySlider;
        [SerializeField] private Image    _sanityFill;
        [SerializeField] private TMP_Text _sanityLabel;
        [SerializeField] private float    _warpThreshold = 20f; // sanity below this = warp starts

        [Header("Impatience Timer")]
        [SerializeField] private TMP_Text _timerLabel;
        [SerializeField] private Image    _timerFill;
        [SerializeField] private Color    _timerNormalColor    = Color.white;
        [SerializeField] private Color    _timerCriticalColor  = Color.red;

        [Header("Credits")]
        [SerializeField] private TMP_Text _creditsLabel;

        [Header("Shift Phase")]
        [SerializeField] private TMP_Text _phaseLabel;
        [SerializeField] private TMP_Text _clientProgressLabel;

        // ── Inspector: Moral Injury Feedback ──────────────────

        [Header("Moral Injury")]
        [SerializeField] private CanvasGroup _moralFlashGroup;
        [SerializeField] private float       _flashDuration    = 0.4f;
        [SerializeField] private Image       _soulScarIndicator;
        [SerializeField] private Sprite      _scarCallousSprite;
        [SerializeField] private Sprite      _scarComplicitSprite;
        [SerializeField] private Sprite      _scarIrredeemableSprite;

        // ── Inspector: UI Corruption Visual ───────────────────

        [Header("UI Corruption")]
        [SerializeField] private CanvasGroup _corruptionOverlay;
        [SerializeField] private float       _corruptionSoulThreshold1 = 75f;
        [SerializeField] private float       _corruptionSoulThreshold2 = 40f;
        [SerializeField] private float       _corruptionSoulThreshold3 = 15f;

        // ── Inspector: Sub-renderers ──────────────────────────

        [Header("Sub-systems")]
        [SerializeField] private NDAOverlayRenderer  _ndaRenderer;
        [SerializeField] private DeskEntropyRenderer _entropyRenderer;

        // ── State ─────────────────────────────────────────────

        private NarratorReliability _currentTone = NarratorReliability.Professional;
        private NarratorTextElement[] _allTextElements;
        private int  _clientIndex;
        private int  _clientTotal;

        // ── Unity Lifecycle ───────────────────────────────────

        private void Awake()
        {
            _allTextElements = FindObjectsByType<NarratorTextElement>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void OnEnable()
        {
            RumorMill.OnNarratorToneChanged  += HandleToneChanged;
            RumorMill.OnSoulIntegrityChanged += HandleSoulChanged;
            RumorMill.OnSanityChanged        += HandleSanityChanged;
            RumorMill.OnMoralChoice          += HandleMoralChoice;
            RumorMill.OnNDASigned            += HandleNDASigned;
            RumorMill.OnShiftPhaseChanged    += HandlePhaseChanged;
            RumorMill.OnClaimResolved        += HandleClaimResolved;
        }

        private void OnDisable()
        {
            RumorMill.OnNarratorToneChanged  -= HandleToneChanged;
            RumorMill.OnSoulIntegrityChanged -= HandleSoulChanged;
            RumorMill.OnSanityChanged        -= HandleSanityChanged;
            RumorMill.OnMoralChoice          -= HandleMoralChoice;
            RumorMill.OnNDASigned            -= HandleNDASigned;
            RumorMill.OnShiftPhaseChanged    -= HandlePhaseChanged;
            RumorMill.OnClaimResolved        -= HandleClaimResolved;
        }

        private void Start()
        {
            // Sync from current run state on scene load
            var run = GameManager.Instance?.Run;
            if (run == null) return;

            _currentTone = run.NarratorTone;
            RefreshAllText();
            RefreshSoulGauge(run.SoulIntegrity);
            RefreshSanityGauge(run.Sanity);
            RefreshCredits(run.Credits);
        }

        private void Update()
        {
            var run = GameManager.Instance?.Run;
            if (run == null) return;

            RefreshTimerDisplay(run.ImpatenceTimer);
        }

        // ── Narrator Tone ─────────────────────────────────────

        private void HandleToneChanged(NarratorToneChangedEvent e)
        {
            _currentTone = e.Current;
            RefreshAllText();
            ApplyCorruptionLevel(GameManager.Instance?.Run?.SoulIntegrity ?? 100f);
        }

        private void RefreshAllText()
        {
            foreach (var elem in _allTextElements)
                elem.Refresh(_currentTone);
        }

        // ── Soul Gauge ────────────────────────────────────────

        private void HandleSoulChanged(SoulIntegrityChangedEvent e)
        {
            RefreshSoulGauge(e.Current);
            ApplyCorruptionLevel(e.Current);
        }

        private void RefreshSoulGauge(float soul)
        {
            float t = soul / 100f;
            if (_soulSlider) _soulSlider.value = t;
            if (_soulFill && _soulGradient != null)
                _soulFill.color = _soulGradient.Evaluate(t);
            if (_soulLabel)
                _soulLabel.text = NarratorSystem.GetLine("tooltip.soul_gauge", _currentTone);
        }

        private void ApplyCorruptionLevel(float soul)
        {
            if (_corruptionOverlay == null) return;

            float alpha = soul switch
            {
                <= _corruptionSoulThreshold3 => 0.40f,
                <= _corruptionSoulThreshold2 => 0.20f,
                <= _corruptionSoulThreshold1 => 0.05f,
                _                            => 0.00f,
            };

            _corruptionOverlay.alpha = alpha;
        }

        // ── Sanity Gauge ──────────────────────────────────────

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            RefreshSanityGauge(e.Current);
            if (e.TriggeredFugue)
                StartCoroutine(FugueFlash());
        }

        private void RefreshSanityGauge(float sanity)
        {
            float t = sanity / 100f;
            if (_sanitySlider) _sanitySlider.value = t;

            // Below warp threshold — add subtle horizontal shake to bar
            if (_sanitySlider && sanity < _warpThreshold)
            {
                float warpStrength = (1f - sanity / _warpThreshold) * 4f;
                var rt = _sanitySlider.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = new Vector2(
                        Mathf.Sin(Time.time * 12f) * warpStrength, 0f);
            }
        }

        private IEnumerator FugueFlash()
        {
            if (_moralFlashGroup == null) yield break;
            _moralFlashGroup.alpha = 1f;
            yield return new WaitForSeconds(_flashDuration * 2f);
            float t = 0f;
            while (t < _flashDuration)
            {
                t += Time.deltaTime;
                _moralFlashGroup.alpha = Mathf.Lerp(1f, 0f, t / _flashDuration);
                yield return null;
            }
            _moralFlashGroup.alpha = 0f;
        }

        // ── Impatience Timer ──────────────────────────────────

        private void RefreshTimerDisplay(float seconds)
        {
            if (_timerLabel == null) return;

            int mins = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            _timerLabel.text = $"{mins:D2}:{secs:D2}";

            bool critical = seconds < 120f;
            if (_timerLabel)
                _timerLabel.color = critical ? _timerCriticalColor : _timerNormalColor;
            if (_timerFill)
                _timerFill.color = critical ? _timerCriticalColor : _timerNormalColor;
        }

        // ── Credits ───────────────────────────────────────────

        private void RefreshCredits(int credits)
        {
            if (_creditsLabel)
                _creditsLabel.text = $"¢{credits}";
        }

        // ── Shift Phase ───────────────────────────────────────

        private void HandlePhaseChanged(ShiftPhaseChangedEvent e)
        {
            if (_phaseLabel)
                _phaseLabel.text = FormatPhase(e.Current);
        }

        private static string FormatPhase(ShiftPhase phase) => phase switch
        {
            ShiftPhase.ClockIn       => "Clock In",
            ShiftPhase.MorningBlock  => "Morning Block",
            ShiftPhase.LunchBreak    => "Lunch Break",
            ShiftPhase.AfternoonBlock => "Afternoon Block",
            ShiftPhase.Overtime      => "OVERTIME",
            ShiftPhase.ClockOut      => "Clock Out",
            _                        => phase.ToString(),
        };

        // ── Moral Choice Feedback ─────────────────────────────

        private void HandleMoralChoice(MoralChoiceEvent e)
        {
            if (e.WasUnethical)
                StartCoroutine(MoralFlash());
        }

        private IEnumerator MoralFlash()
        {
            if (_moralFlashGroup == null) yield break;

            _moralFlashGroup.alpha = 0.6f;
            yield return new WaitForSeconds(0.1f);

            float t = 0f;
            while (t < _flashDuration)
            {
                t += Time.deltaTime;
                _moralFlashGroup.alpha = Mathf.Lerp(0.6f, 0f, t / _flashDuration);
                yield return null;
            }
            _moralFlashGroup.alpha = 0f;
        }

        // ── NDA ───────────────────────────────────────────────

        private void HandleNDASigned(NDASignedEvent e)
        {
            _ndaRenderer?.AddOverlay(e.ClaimId, e.CoveredRegion);

            // Update tooltip on 3rd NDA
            if (e.TotalNDACount == 3)
            {
                // Snap all NDA-related text to current tone (warning line surfaces)
                RefreshAllText();
            }
        }

        // ── Claim Progress ────────────────────────────────────

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            _clientIndex++;
            UpdateClientProgress();
        }

        public void SetClientTotal(int total)
        {
            _clientTotal = total;
            _clientIndex = 0;
            UpdateClientProgress();
        }

        private void UpdateClientProgress()
        {
            if (_clientProgressLabel)
                _clientProgressLabel.text = NarratorSystem.GetStatusLine(
                    "status.shift_progress", _currentTone, _clientIndex, _clientTotal);
        }

        // ── Scar Indicator ────────────────────────────────────

        public void ShowScarLevel(MoralInjury.ScarLevel scar)
        {
            if (_soulScarIndicator == null) return;

            _soulScarIndicator.sprite = scar switch
            {
                MoralInjury.ScarLevel.Callous      => _scarCallousSprite,
                MoralInjury.ScarLevel.Complicit    => _scarComplicitSprite,
                MoralInjury.ScarLevel.Irredeemable => _scarIrredeemableSprite,
                _                                  => null,
            };
            _soulScarIndicator.enabled = scar != MoralInjury.ScarLevel.None;
        }
    }
}
