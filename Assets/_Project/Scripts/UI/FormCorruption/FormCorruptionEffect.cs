// ============================================================
// DESK 42 — Form Corruption Effect (MonoBehaviour)
//
// At low sanity, claim document text degrades — characters are
// replaced with zalgo / garbage glyphs. The effect intensifies
// as sanity drops:
//
//   Sanity 35–20 : Light corruption (5–15% of characters).
//   Sanity 20–10 : Medium corruption (15–40%).
//   Sanity < 10  : Heavy corruption (40–70%).
//
// Uses SeedStream.FormCorruption for deterministic corruption
// (seeded replays look identical). Respects EntropyManager
// hierarchy — will not activate if a higher-priority layer
// (NDASaturation or GlassCracking) is running.
//
// Requires a reference to the ClaimPanelView's TMP_Text fields.
// Attach to the same GameObject as ClaimPanelView or assign
// via Inspector.
// ============================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class FormCorruptionEffect : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Text Targets")]
        [Tooltip("TMP_Text fields on the claim panel to corrupt.")]
        [SerializeField] private List<TMP_Text> _corruptibleFields = new();

        [Header("Thresholds")]
        [SerializeField] private float _sanityThreshold = 35f;
        [SerializeField] private float _updateInterval  = 0.3f;

        // ── Corruption Glyphs ─────────────────────────────────

        private static readonly char[] _zalgoChars =
        {
            '\u0300', '\u0301', '\u0302', '\u0303', '\u0304',
            '\u0305', '\u0306', '\u0307', '\u0308', '\u0309',
            '\u030A', '\u030B', '\u030C', '\u030D', '\u030E',
            '\u030F', '\u0310', '\u0311', '\u0312', '\u0313',
        };

        private static readonly char[] _garbageChars =
        {
            '#', '@', '%', '&', '*', '?', 'X', '/', '\\', '|',
            '+', '=', '<', '>', '~', '^', '{', '}', '[', ']',
        };

        // ── State ─────────────────────────────────────────────

        private float _currentSanity = 100f;
        private float _timer;
        private bool  _isCorrupting;

        // Cached originals so we can restore text when sanity recovers
        private readonly Dictionary<TMP_Text, string> _originalTexts = new();

        // ── Unity Lifecycle ───────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnSanityChanged  += HandleSanityChanged;
            RumorMill.OnClaimQueued    += HandleClaimQueued;
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;
        }

        private void OnDisable()
        {
            RumorMill.OnSanityChanged  -= HandleSanityChanged;
            RumorMill.OnClaimQueued    -= HandleClaimQueued;
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;

            RestoreAll();
        }

        private void Start()
        {
            _currentSanity = GameManager.Instance?.Run?.Sanity ?? 100f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _updateInterval) return;
            _timer = 0f;

            if (_currentSanity >= _sanityThreshold)
            {
                if (_isCorrupting) RestoreAll();
                return;
            }

            // Respect entropy hierarchy
            if (!EntropyManager.CanActivate(EntropyLayer.TextCorruption))
            {
                if (_isCorrupting) RestoreAll();
                return;
            }

            ApplyCorruption();
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            _currentSanity = e.Current;
        }

        private void HandleClaimQueued(ClaimQueuedEvent e)
        {
            // New claim arrived — snapshot original text next frame
            _originalTexts.Clear();
            _isCorrupting = false;
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart)
            {
                _currentSanity = 100f;
                RestoreAll();
            }
        }

        // ── Corruption Logic ──────────────────────────────────

        private void ApplyCorruption()
        {
            if (!_isCorrupting)
            {
                EntropyManager.SetLayerActive(EntropyLayer.TextCorruption, true);
                _isCorrupting = true;
            }

            // Corruption intensity: 0→1 as sanity goes from threshold→0
            float t = Mathf.InverseLerp(_sanityThreshold, 0f, _currentSanity);
            float corruptionRate = Mathf.Lerp(0.05f, 0.70f, t);

            foreach (var field in _corruptibleFields)
            {
                if (field == null) continue;

                // Cache original if not already cached
                if (!_originalTexts.ContainsKey(field))
                    _originalTexts[field] = field.text;

                string original = _originalTexts[field];
                field.text = CorruptString(original, corruptionRate);
            }
        }

        private string CorruptString(string source, float rate)
        {
            if (string.IsNullOrEmpty(source)) return source;

            var sb = new StringBuilder(source.Length + 20);

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];

                // Preserve whitespace and newlines for readability
                if (char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                    continue;
                }

                if (SeedEngine.NextFloat(SeedStream.FormCorruption) < rate)
                {
                    // 50/50: zalgo diacritic stacking vs garbage replacement
                    if (SeedEngine.NextBool(SeedStream.FormCorruption))
                    {
                        // Keep original char, add zalgo diacritics
                        sb.Append(c);
                        int diacriticCount = SeedEngine.Next(SeedStream.FormCorruption, 1, 4);
                        for (int d = 0; d < diacriticCount; d++)
                        {
                            int idx = SeedEngine.Next(SeedStream.FormCorruption, _zalgoChars.Length);
                            sb.Append(_zalgoChars[idx]);
                        }
                    }
                    else
                    {
                        // Replace with garbage character
                        int idx = SeedEngine.Next(SeedStream.FormCorruption, _garbageChars.Length);
                        sb.Append(_garbageChars[idx]);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        // ── Restore ───────────────────────────────────────────

        private void RestoreAll()
        {
            foreach (var kvp in _originalTexts)
            {
                if (kvp.Key != null)
                    kvp.Key.text = kvp.Value;
            }

            if (_isCorrupting)
            {
                EntropyManager.SetLayerActive(EntropyLayer.TextCorruption, false);
                _isCorrupting = false;
            }
        }
    }
}
