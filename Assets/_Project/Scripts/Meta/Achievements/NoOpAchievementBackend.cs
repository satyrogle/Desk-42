// ============================================================
// DESK 42 — No-op Achievement Backend
//
// Default backend. Tracks nothing; Unlock is a sink.
// Used when no real provider is wired so Achievements.Unlock(id)
// is always safe to call.
// ============================================================

namespace Desk42.Meta.Achievements
{
    public sealed class NoOpAchievementBackend : IAchievementBackend
    {
        public void Initialize() { }
        public void Unlock(string id) { }
        public bool IsUnlocked(string id) => false;
        public void Shutdown() { }
    }
}
