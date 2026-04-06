// ============================================================
// DESK 42 — Entropy Manager Driver (MonoBehaviour)
//
// Lives in the Shift scene. Feeds real event data into the
// static EntropyManager so it stays current without any
// MonoBehaviour coupling inside EntropyManager itself.
//
// Responsibilities:
//   - Reset EntropyManager at shift start.
//   - Update NDA count on NDASignedEvent.
//   - Clear NDA count at shift end / on ClearAll from
//     NDAOverlayRenderer (detected via ShiftLifecycleEvent).
//
// One instance per Shift scene. No DontDestroyOnLoad.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class EntropyManagerDriver : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Debug")]
        [Tooltip("Log the full EntropyManager state to console each frame (dev only).")]
        [SerializeField] private bool _debugLogEachFrame;

        // ── Unity Lifecycle ───────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnNDASigned      += HandleNDASigned;
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;
            RumorMill.OnClaimResolved  += HandleClaimResolved;
        }

        private void OnDisable()
        {
            RumorMill.OnNDASigned      -= HandleNDASigned;
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;
            RumorMill.OnClaimResolved  -= HandleClaimResolved;
        }

        private void Start()
        {
            // Sync to current run state in case scene was loaded mid-run
            int currentNDA = GameManager.Instance?.Run?.RawData?.NDACountThisShift() ?? 0;
            EntropyManager.SetNDACount(currentNDA);
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogEachFrame)
                Debug.Log(EntropyManager.Dump());
#endif
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleNDASigned(NDASignedEvent e)
        {
            // NDASignedEvent carries the running total for this shift
            EntropyManager.SetNDACount(e.TotalNDACount);

            // If saturation just kicked in, log a designer warning in dev builds
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (e.TotalNDACount == EntropyManager.NDA_SATURATION_THRESHOLD)
                Debug.Log("[EntropyManager] NDA saturation threshold reached. " +
                          "GlassCracking, TextCorruption, ShadowBureaucrat are now blocked.");
#endif
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart)
                EntropyManager.Reset();
        }

        private void HandleClaimResolved(ClaimResolvedEvent _)
        {
            // NDAs do NOT clear per-claim — they persist until shift end.
            // This handler is a hook for future NDA-dismissal mechanics.
        }

        // ── Editor Context Menu ───────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [ContextMenu("Debug: Dump Entropy State")]
        private void DumpState() => Debug.Log(EntropyManager.Dump());

        [ContextMenu("Debug: Simulate NDA Saturation")]
        private void SimulateSaturation()
            => EntropyManager.SetNDACount(EntropyManager.NDA_SATURATION_THRESHOLD);

        [ContextMenu("Debug: Reset Entropy")]
        private void ResetState() => EntropyManager.Reset();
#endif
    }
}
