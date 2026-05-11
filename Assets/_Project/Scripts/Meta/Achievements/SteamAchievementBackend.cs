// ============================================================
// DESK 42 — Steam Achievement Backend
//
// Routes unlocks through Steamworks.NET. Activated only when
// DESK42_STEAM is defined (set after the Steamworks.NET package
// is dropped into the project) AND SteamAPI.Init() succeeds.
// Falls back to acting like a no-op if Steam is not running.
//
// Achievement ids must be authored in the Steamworks dashboard
// for the game's app id. Ids in this codebase live in
// AchievementCatalog and must match the Steam dashboard exactly.
// ============================================================

using UnityEngine;

#if DESK42_STEAM
using Steamworks;
#endif

namespace Desk42.Meta.Achievements
{
    public sealed class SteamAchievementBackend : IAchievementBackend
    {
        private bool _ready;

        public void Initialize()
        {
#if DESK42_STEAM
            // SteamManager (from Steamworks.NET) handles SteamAPI.Init.
            _ready = SteamManager.Initialized;
            if (_ready) Debug.Log("[Achievements] Steam backend ready.");
            else        Debug.LogWarning("[Achievements] Steam backend requested but SteamManager not initialised.");
#else
            _ready = false;
            Debug.LogWarning("[Achievements] DESK42_STEAM not defined — Steam backend acts as no-op.");
#endif
        }

        public void Unlock(string id)
        {
            if (!_ready || string.IsNullOrEmpty(id)) return;
#if DESK42_STEAM
            SteamUserStats.SetAchievement(id);
            SteamUserStats.StoreStats();
#endif
        }

        public bool IsUnlocked(string id)
        {
            if (!_ready || string.IsNullOrEmpty(id)) return false;
#if DESK42_STEAM
            return SteamUserStats.GetAchievement(id, out bool achieved) && achieved;
#else
            return false;
#endif
        }

        public void Shutdown()
        {
#if DESK42_STEAM
            if (_ready) SteamUserStats.StoreStats();
#endif
        }
    }
}
