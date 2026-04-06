// ============================================================
// DESK 42 — Entropy Manager
//
// Central authority for which visual disruption effects are
// permitted to fire simultaneously. Prevents the "soft-lock"
// scenario where overlapping systems make the UI uninteractable.
//
// Disruption Hierarchy (highest → lowest priority):
//
//   Layer 0  NDASaturation    Active when ≥ NDA_THRESHOLD overlays
//                             are on screen. Blocks all lower layers.
//
//   Layer 1  GlassCracking    Desk glass crack animations (Expansion).
//                             Blocks TextCorruption + ShadowBureaucrat.
//
//   Layer 2  TextCorruption   Form text shuffles / rearranges (Exp.).
//                             Blocks ShadowBureaucrat.
//
//   Layer 3  ShadowBureaucrat Shadow Bureaucrat interference (Exp.).
//                             Lowest priority; blocked by everything.
//
// Rule: a layer may only activate if NO higher-priority layer is
// currently active. Effects self-report via SetLayerActive().
//
// Usage:
//   Before starting an effect:
//     if (!EntropyManager.CanActivate(EntropyLayer.TextCorruption)) return;
//
//   When an effect starts/ends:
//     EntropyManager.SetLayerActive(EntropyLayer.TextCorruption, true);
//     // ... later ...
//     EntropyManager.SetLayerActive(EntropyLayer.TextCorruption, false);
//
//   NDA count is fed by EntropyManagerDriver (auto from events).
//
// Static class — no MonoBehaviour, no Unity lifecycle.
// EntropyManagerDriver bootstraps it from the Shift scene.
// ============================================================

using UnityEngine;

namespace Desk42.UI
{
    // ── Layer enum ─────────────────────────────────────────────
    // Ordered by priority: lower index = higher priority.

    public enum EntropyLayer
    {
        NDASaturation    = 0,
        GlassCracking    = 1,
        TextCorruption   = 2,
        ShadowBureaucrat = 3,
    }

    // ── Manager ────────────────────────────────────────────────

    public static class EntropyManager
    {
        // Three NDA overlays = NDASaturation threshold
        public const int NDA_SATURATION_THRESHOLD = 3;

        // Layers 1-3 self-report via SetLayerActive
        private static readonly bool[] _layerActive = new bool[4];

        // NDA count is fed externally from NDASignedEvent
        private static int _activeNDACount;

        // ── Public Queries ────────────────────────────────────

        public static int  ActiveNDACount       => _activeNDACount;
        public static bool NDASaturationActive  => _activeNDACount >= NDA_SATURATION_THRESHOLD;

        /// <summary>
        /// True when no higher-priority layer is currently active.
        /// Call before starting any expansion-tier visual effect.
        /// </summary>
        public static bool CanActivate(EntropyLayer layer)
        {
            // NDASaturation (layer 0) is implicit — driven by NDA count, not self-reported
            if (layer != EntropyLayer.NDASaturation && NDASaturationActive)
                return false;

            // Check all layers with higher priority (lower index)
            int idx = (int)layer;
            for (int i = 0; i < idx; i++)
            {
                if (IsLayerActive((EntropyLayer)i))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// True when the player's ability to interact is degraded to the
        /// point where adding another visual obstruction would be a soft-lock.
        /// Specifically: NDA saturation is active AND at least one other
        /// effect layer is also running.
        /// </summary>
        public static bool IsInteractionCompromised()
        {
            if (!NDASaturationActive) return false;
            for (int i = 1; i < _layerActive.Length; i++)
                if (_layerActive[i]) return true;
            return false;
        }

        /// <summary>
        /// Whether a given layer is currently running.
        /// NDASaturation is computed from NDA count; others are self-reported.
        /// </summary>
        public static bool IsLayerActive(EntropyLayer layer)
        {
            return layer == EntropyLayer.NDASaturation
                ? NDASaturationActive
                : _layerActive[(int)layer];
        }

        // ── Mutation API ──────────────────────────────────────

        /// <summary>
        /// Effects call this when they start or finish so the hierarchy
        /// stays accurate. NDASaturation cannot be manually set — use
        /// SetNDACount() instead.
        /// </summary>
        public static void SetLayerActive(EntropyLayer layer, bool active)
        {
            if (layer == EntropyLayer.NDASaturation)
            {
                Debug.LogWarning("[EntropyManager] NDASaturation is driven by NDA count. " +
                                 "Use SetNDACount() instead.");
                return;
            }

            _layerActive[(int)layer] = active;

            Debug.Log($"[EntropyManager] Layer {layer}: {(active ? "ON" : "OFF")}. " +
                      $"NDAs: {_activeNDACount}. " +
                      $"Interaction compromised: {IsInteractionCompromised()}.");
        }

        /// <summary>
        /// Update the active NDA overlay count.
        /// Called by EntropyManagerDriver on NDASignedEvent and shift reset.
        /// </summary>
        public static void SetNDACount(int count)
        {
            bool wasAtThreshold = NDASaturationActive;
            _activeNDACount = Mathf.Max(0, count);

            if (NDASaturationActive != wasAtThreshold)
            {
                Debug.Log($"[EntropyManager] NDASaturation: {(NDASaturationActive ? "ACTIVE" : "CLEARED")} " +
                          $"(NDA count: {_activeNDACount}/{NDA_SATURATION_THRESHOLD}).");
            }
        }

        // ── Reset ─────────────────────────────────────────────

        /// <summary>
        /// Reset all state at shift start. Called by EntropyManagerDriver.
        /// </summary>
        public static void Reset()
        {
            _activeNDACount = 0;
            for (int i = 0; i < _layerActive.Length; i++)
                _layerActive[i] = false;

            Debug.Log("[EntropyManager] Reset.");
        }

        // ── Debug Dump ────────────────────────────────────────

        public static string Dump()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== EntropyManager ===");
            sb.AppendLine($"  NDAs active:      {_activeNDACount} " +
                          $"(threshold: {NDA_SATURATION_THRESHOLD}, " +
                          $"saturated: {NDASaturationActive})");
            foreach (EntropyLayer layer in System.Enum.GetValues(typeof(EntropyLayer)))
                sb.AppendLine($"  {layer,-20} active={IsLayerActive(layer),5}  " +
                              $"canActivate={CanActivate(layer)}");
            sb.AppendLine($"  Interaction compromised: {IsInteractionCompromised()}");
            return sb.ToString();
        }
    }
}
