// ============================================================
// DESK 42 — TaskMgr_Override Assembly
//
// Tracks whether the player has collected enough .exe fragments
// to assemble TaskMgr_Override.exe and trigger the Crash-to-Win
// boss fight. The threshold is intentionally low (5) so a
// dedicated player can reach the finale in 3-5 shifts.
//
// Read-only — the actual fragment count lives in
// MetaProgressData.ExeFragmentsCollected, written by the
// Steganography detector and the Rogue Subroutine.
// ============================================================

using Desk42.Core;

namespace Desk42.Meta.BossFight
{
    public static class TaskMgrOverride
    {
        public const int FragmentsRequired = 5;

        public static bool IsAssembled(MetaProgressData meta)
            => meta != null && meta.ExeFragmentsCollected >= FragmentsRequired;

        public static int FragmentsRemaining(MetaProgressData meta)
        {
            if (meta == null) return FragmentsRequired;
            int remaining = FragmentsRequired - meta.ExeFragmentsCollected;
            return remaining > 0 ? remaining : 0;
        }
    }
}
