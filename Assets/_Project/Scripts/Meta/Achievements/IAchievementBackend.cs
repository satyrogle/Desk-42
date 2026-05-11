// ============================================================
// DESK 42 — Achievement Backend Interface
//
// Pluggable backend so call sites (Achievements.Unlock(id))
// don't care whether unlocks go to Steam, local disk, or a
// no-op sink. Backends are interchangeable — pick at boot.
// ============================================================

namespace Desk42.Meta.Achievements
{
    public interface IAchievementBackend
    {
        void Initialize();

        /// <summary>Mark an achievement as unlocked. Idempotent —
        /// repeated calls for the same id must be safe.</summary>
        void Unlock(string id);

        bool IsUnlocked(string id);

        void Shutdown();
    }
}
