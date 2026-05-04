// ============================================================
// DESK 42 — Accessibility Settings
//
// Player-facing toggles that flow into UI/FX systems. Stored in
// PlayerPrefs so they survive meta wipes and exist before the
// first run is created.
//
// Subscribe to OnChanged to rebuild any cached visuals (text
// scale, palette swaps).
// ============================================================

using System;
using TMPro;
using UnityEngine;

namespace Desk42.Accessibility
{
    public static class AccessibilitySettings
    {
        public const float MinTextScale = 0.85f;
        public const float MaxTextScale = 1.5f;

        private const string K_ReducedMotion         = "desk42.a11y.reducedMotion";
        private const string K_DisableScreenDistort  = "desk42.a11y.disableScreenDistortion";
        private const string K_HighContrast          = "desk42.a11y.highContrast";
        private const string K_HoldToConfirm         = "desk42.a11y.holdToConfirm";
        private const string K_TextScale             = "desk42.a11y.textScale";

        private static bool  _reducedMotion;
        private static bool  _disableScreenDistortion;
        private static bool  _highContrast;
        private static bool  _holdToConfirm;
        private static float _textScale = 1f;
        private static bool  _loaded;

        /// <summary>Disables screen shake, card flash, jitter, and
        /// other purely-decorative motion. Distortion FX have their
        /// own toggle.</summary>
        public static bool ReducedMotion
        {
            get { EnsureLoaded(); return _reducedMotion; }
            set
            {
                EnsureLoaded();
                if (_reducedMotion == value) return;
                _reducedMotion = value;
                PlayerPrefs.SetInt(K_ReducedMotion, value ? 1 : 0);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>Suppresses sanity-driven distortion overlays
        /// (chromatic aberration, barrel warp, text zalgo, UI
        /// drift). Sanity gameplay still applies — only the
        /// visual layer is muted.</summary>
        public static bool DisableScreenDistortion
        {
            get { EnsureLoaded(); return _disableScreenDistortion; }
            set
            {
                EnsureLoaded();
                if (_disableScreenDistortion == value) return;
                _disableScreenDistortion = value;
                PlayerPrefs.SetInt(K_DisableScreenDistort, value ? 1 : 0);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        public static bool HighContrast
        {
            get { EnsureLoaded(); return _highContrast; }
            set
            {
                EnsureLoaded();
                if (_highContrast == value) return;
                _highContrast = value;
                PlayerPrefs.SetInt(K_HighContrast, value ? 1 : 0);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>For irreversible actions (NDAs, ending choices)
        /// require a hold instead of a single tap. Wired by panels
        /// that opt in.</summary>
        public static bool HoldToConfirm
        {
            get { EnsureLoaded(); return _holdToConfirm; }
            set
            {
                EnsureLoaded();
                if (_holdToConfirm == value) return;
                _holdToConfirm = value;
                PlayerPrefs.SetInt(K_HoldToConfirm, value ? 1 : 0);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        public static float TextScale
        {
            get { EnsureLoaded(); return _textScale; }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp(value, MinTextScale, MaxTextScale);
                if (Mathf.Approximately(_textScale, clamped)) return;
                _textScale = clamped;
                PlayerPrefs.SetFloat(K_TextScale, clamped);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        public static event Action OnChanged;

        /// <summary>Apply the current text scale to a TMP label
        /// using its design-time font size as the base.</summary>
        public static void ApplyTextScale(TMP_Text label, float baseFontSize)
        {
            if (label == null) return;
            label.fontSize = baseFontSize * TextScale;
        }

        /// <summary>Returns the design-time font size scaled by
        /// the current TextScale. Use in runtime UI builders:
        /// <code>tmp.fontSize = AccessibilitySettings.Scaled(22);</code>
        /// </summary>
        public static float Scaled(float baseFontSize)
            => baseFontSize * TextScale;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _reducedMotion           = PlayerPrefs.GetInt(K_ReducedMotion, 0) == 1;
            _disableScreenDistortion = PlayerPrefs.GetInt(K_DisableScreenDistort, 0) == 1;
            _highContrast            = PlayerPrefs.GetInt(K_HighContrast, 0) == 1;
            _holdToConfirm           = PlayerPrefs.GetInt(K_HoldToConfirm, 0) == 1;
            _textScale               = Mathf.Clamp(
                PlayerPrefs.GetFloat(K_TextScale, 1f), MinTextScale, MaxTextScale);
        }
    }
}
