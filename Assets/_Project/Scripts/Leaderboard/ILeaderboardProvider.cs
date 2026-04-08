// ============================================================
// DESK 42 — Leaderboard Provider Interface
//
// Abstraction layer for leaderboard backends. Implementations:
//   SteamLeaderboardProvider  — Steamworks.NET (primary)
//   PlayFabLeaderboardProvider — PlayFab SDK (fallback)
//   LocalLeaderboardProvider  — local JSON (offline fallback)
// ============================================================

using System;
using System.Collections.Generic;

namespace Desk42.Leaderboard
{
    /// <summary>A single leaderboard entry.</summary>
    public sealed class LeaderboardEntry
    {
        public string PlayerId;
        public string PlayerName;
        public int    Rank;
        public int    Score;
        public string SeedCode;
        public string ArchetypeId;
        public float  EfficiencyRating;
        public float  SoulIntegrity;
        public int    ShiftCount;
        public string ActiveVowsSerialized; // comma-separated vow IDs
    }

    /// <summary>Result wrapper for async leaderboard operations.</summary>
    public sealed class LeaderboardResult<T>
    {
        public bool   Success;
        public T      Data;
        public string Error;

        public static LeaderboardResult<T> Ok(T data) => new() { Success = true, Data = data };
        public static LeaderboardResult<T> Fail(string error) => new() { Success = false, Error = error };
    }

    /// <summary>
    /// Backend-agnostic leaderboard API. All methods use callbacks
    /// since Steam/PlayFab are inherently async.
    /// </summary>
    public interface ILeaderboardProvider
    {
        /// <summary>Human-readable provider name (for debug logs).</summary>
        string ProviderName { get; }

        /// <summary>True once the provider has authenticated and is ready.</summary>
        bool IsAvailable { get; }

        /// <summary>Submit a score. Callback fires with success/failure.</summary>
        void SubmitScore(LeaderboardEntry entry,
            Action<LeaderboardResult<bool>> callback);

        /// <summary>Fetch the global top N scores.</summary>
        void FetchTopScores(int count,
            Action<LeaderboardResult<List<LeaderboardEntry>>> callback);

        /// <summary>Fetch the current player's rank and nearby entries.</summary>
        void FetchPlayerRank(
            Action<LeaderboardResult<LeaderboardEntry>> callback);

        /// <summary>Fetch friends-only leaderboard (Steam-specific; others may return empty).</summary>
        void FetchFriendsScores(int count,
            Action<LeaderboardResult<List<LeaderboardEntry>>> callback);
    }
}
