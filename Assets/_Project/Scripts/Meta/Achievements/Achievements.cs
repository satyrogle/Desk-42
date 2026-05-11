// ============================================================
// DESK 42 — Achievements Facade
//
// Static API the rest of the game uses:
//   Achievements.Unlock(AchievementCatalog.FirstShiftCompleted);
//
// One backend can be installed at boot; AchievementBootstrap
// picks the default. Replace via Achievements.SetBackend(...)
// at any time.
// ============================================================

namespace Desk42.Meta.Achievements
{
    public static class Achievements
    {
        private static IAchievementBackend _backend = new NoOpAchievementBackend();

        public static IAchievementBackend Backend => _backend;

        public static void SetBackend(IAchievementBackend backend)
        {
            if (backend == null) backend = new NoOpAchievementBackend();
            _backend?.Shutdown();
            _backend = backend;
            _backend.Initialize();
        }

        public static void Unlock(string id) => _backend.Unlock(id);
        public static bool IsUnlocked(string id) => _backend.IsUnlocked(id);
    }
}
