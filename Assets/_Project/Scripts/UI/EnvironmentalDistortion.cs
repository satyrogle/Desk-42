// ============================================================
// DESK 42 — Environmental Distortion (MonoBehaviour)
//
// As sanity drops, the world warps. Subscribes to
// RumorMill.OnSanityChanged and applies three escalating tiers:
//
//   Sanity 40–20 : Subtle RectTransform jitter on gameplay panel.
//   Sanity 20–10 : Chromatic aberration + barrel distortion via
//                   a fullscreen overlay material.
//   Sanity < 10  : UI element position drift (buttons wander).
//
// All randomness routed through SeedEngine.
// Respects EntropyManager hierarchy — defers to higher layers.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class EnvironmentalDistortion : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Tier 1: Panel Jitter (sanity < 40)")]
        [Tooltip("The main gameplay panel RectTransform to jitter.")]
        [SerializeField] private RectTransform _gameplayPanel;
        [SerializeField] private float _jitterIntensity = 2f;

        [Header("Tier 2: Distortion Overlay (sanity < 20)")]
        [Tooltip("Fullscreen Image whose material drives chromatic aberration.")]
        [SerializeField] private Image    _distortionOverlay;
        [Tooltip("Material with _AberrationStrength and _BarrelPower properties.")]
        [SerializeField] private Material _distortionMaterial;

        [Header("Tier 3: UI Drift (sanity < 10)")]
        [Tooltip("UI RectTransforms that subtly wander at critical sanity.")]
        [SerializeField] private List<RectTransform> _driftTargets = new();
        [SerializeField] private float _driftSpeed     = 0.5f;
        [SerializeField] private float _driftAmplitude = 12f;

        [Header("Config")]
        [SerializeField] private float _updateInterval = 0.1f;

        // ── State ─────────────────────────────────────────────

        private float   _currentSanity = 100f;
        private Vector2 _panelBasePos;
        private readonly List<Vector2> _driftBasePositions = new();
        private float   _timer;
        private bool    _initialised;

        // ── Unity Lifecycle ───────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnSanityChanged   += HandleSanityChanged;
            RumorMill.OnShiftLifecycle  += HandleShiftLifecycle;
        }

        private void OnDisable()
        {
            RumorMill.OnSanityChanged   -= HandleSanityChanged;
            RumorMill.OnShiftLifecycle  -= HandleShiftLifecycle;

            ResetAll();
        }

        private void Start()
        {
            CacheBasePositions();

            _currentSanity = GameManager.Instance?.Run?.Sanity ?? 100f;
            ApplyDistortion();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _updateInterval) return;
            _timer = 0f;

            ApplyDistortion();
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            _currentSanity = e.Current;
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart)
            {
                _currentSanity = 100f;
                ResetAll();
            }
        }

        // ── Distortion Application ───────────────────────────

        private void ApplyDistortion()
        {
            ApplyJitter();
            ApplyOverlay();
            ApplyDrift();
        }

        /// <summary>Tier 1: subtle positional jitter below sanity 40.</summary>
        private void ApplyJitter()
        {
            if (_gameplayPanel == null) return;

            if (_currentSanity >= 40f)
            {
                _gameplayPanel.anchoredPosition = _panelBasePos;
                return;
            }

            // Intensity scales 0→1 as sanity goes 40→0
            float t = Mathf.InverseLerp(40f, 0f, _currentSanity);
            float strength = t * _jitterIntensity;

            Vector2 offset = new(
                SeedEngine.NextFloat(SeedStream.FormCorruption, -strength, strength),
                SeedEngine.NextFloat(SeedStream.FormCorruption, -strength, strength));

            _gameplayPanel.anchoredPosition = _panelBasePos + offset;
        }

        /// <summary>Tier 2: chromatic aberration + barrel warp below sanity 20.</summary>
        private void ApplyOverlay()
        {
            if (_distortionOverlay == null || _distortionMaterial == null) return;

            if (_currentSanity >= 20f)
            {
                _distortionOverlay.enabled = false;
                return;
            }

            _distortionOverlay.enabled = true;

            // t scales 0→1 as sanity goes 20→0
            float t = Mathf.InverseLerp(20f, 0f, _currentSanity);

            _distortionMaterial.SetFloat("_AberrationStrength", t * 0.015f);
            _distortionMaterial.SetFloat("_BarrelPower", 1f + t * 0.08f);
        }

        /// <summary>Tier 3: UI elements slowly wander below sanity 10.</summary>
        private void ApplyDrift()
        {
            if (_driftTargets.Count == 0 || _currentSanity >= 10f)
            {
                RestoreDriftPositions();
                return;
            }

            float t = Mathf.InverseLerp(10f, 0f, _currentSanity);
            float amplitude = t * _driftAmplitude;
            float time = Time.time * _driftSpeed;

            for (int i = 0; i < _driftTargets.Count; i++)
            {
                if (_driftTargets[i] == null) continue;
                if (i >= _driftBasePositions.Count) break;

                // Each target gets a unique phase offset based on index
                float phase = i * 1.618f; // golden ratio for even distribution
                Vector2 drift = new(
                    Mathf.Sin(time + phase) * amplitude,
                    Mathf.Cos(time * 0.7f + phase) * amplitude * 0.6f);

                _driftTargets[i].anchoredPosition = _driftBasePositions[i] + drift;
            }
        }

        // ── Helpers ───────────────────────────────────────────

        private void CacheBasePositions()
        {
            if (_initialised) return;
            _initialised = true;

            _panelBasePos = _gameplayPanel != null
                ? _gameplayPanel.anchoredPosition
                : Vector2.zero;

            _driftBasePositions.Clear();
            foreach (var rt in _driftTargets)
                _driftBasePositions.Add(rt != null ? rt.anchoredPosition : Vector2.zero);
        }

        private void RestoreDriftPositions()
        {
            for (int i = 0; i < _driftTargets.Count; i++)
            {
                if (_driftTargets[i] == null || i >= _driftBasePositions.Count) continue;
                _driftTargets[i].anchoredPosition = _driftBasePositions[i];
            }
        }

        private void ResetAll()
        {
            if (_gameplayPanel != null)
                _gameplayPanel.anchoredPosition = _panelBasePos;

            RestoreDriftPositions();

            if (_distortionOverlay != null)
                _distortionOverlay.enabled = false;

            if (_distortionMaterial != null)
            {
                _distortionMaterial.SetFloat("_AberrationStrength", 0f);
                _distortionMaterial.SetFloat("_BarrelPower", 1f);
            }
        }
    }
}
