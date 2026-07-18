#if DEVELOPMENT_BUILD

using UnityEngine;
using Desk42.Core;

namespace Desk42.Debugging
{
    public static class ConsensusAudit
    {
        private static bool _feedbackOverlayFired;
        private static bool _spatialAudioFired;
        private static bool _deskEntropyFired;
        private static string _pendingClaimId;

        public static void BeginAudit(string claimId)
        {
            if (_pendingClaimId != null)
                Evaluate();

            _pendingClaimId        = claimId;
            _feedbackOverlayFired  = false;
            _spatialAudioFired     = false;
            _deskEntropyFired      = false;
        }

        public static void MarkFeedbackOverlay() => _feedbackOverlayFired = true;
        public static void MarkSpatialAudio()    => _spatialAudioFired    = true;
        public static void MarkDeskEntropy()      => _deskEntropyFired     = true;

        public static void Evaluate()
        {
            if (_pendingClaimId == null) return;

            if (!_feedbackOverlayFired)
                Debug.LogWarning($"[ConsensusAudit] WARN: ShiftFeedbackOverlay did not fire for ClaimResolvedEvent ({_pendingClaimId})");
            if (!_spatialAudioFired)
                Debug.LogWarning($"[ConsensusAudit] WARN: SpatialAudioThreatSystem did not fire for ClaimResolvedEvent ({_pendingClaimId})");
            if (!_deskEntropyFired)
                Debug.LogWarning($"[ConsensusAudit] WARN: DeskEntropyRenderer did not fire for ClaimResolvedEvent ({_pendingClaimId})");

            _pendingClaimId = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Subscribe()
        {
            RumorMill.OnClaimResolved += e => BeginAudit(e.ClaimId);
        }
    }
}

#endif
