// ============================================================
// DESK 42 — Steam Leaderboard Provider
//
// Steamworks.NET integration for leaderboard upload/download.
// Guarded by DESK42_STEAM define — compiles as a no-op stub
// without the Steamworks.NET plugin installed.
//
// Add DESK42_STEAM to Player Settings → Scripting Define Symbols
// once com.rlabrecque.steamworks.net is in the project.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Leaderboard
{
    public sealed class SteamLeaderboardProvider : ILeaderboardProvider
    {
        public string ProviderName => "Steam";

#if DESK42_STEAM
        private Steamworks.SteamLeaderboard_t _leaderboard;
        private bool _leaderboardFound;

        public bool IsAvailable => Steamworks.SteamManager.Initialized && _leaderboardFound;

        public void Initialize(string leaderboardName)
        {
            if (!Steamworks.SteamManager.Initialized)
            {
                Debug.LogWarning("[SteamLeaderboard] Steam not initialized.");
                return;
            }

            var call = Steamworks.SteamUserStats.FindLeaderboard(leaderboardName);
            // In production, use a CallResult<> to handle the async callback.
            // For now, mark as found after the call is made.
            Debug.Log($"[SteamLeaderboard] Searching for leaderboard '{leaderboardName}'...");
        }

        public void SubmitScore(LeaderboardEntry entry, Action<LeaderboardResult<bool>> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(LeaderboardResult<bool>.Fail("Steam not available"));
                return;
            }

            var method = Steamworks.ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest;
            Steamworks.SteamUserStats.UploadLeaderboardScore(
                _leaderboard, method, entry.Score, null, 0);

            callback?.Invoke(LeaderboardResult<bool>.Ok(true));
            Debug.Log($"[SteamLeaderboard] Score submitted: {entry.Score}.");
        }

        public void FetchTopScores(int count, Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Fail("Steam not available"));
                return;
            }

            // TODO: Implement Steamworks.SteamUserStats.DownloadLeaderboardEntries async
            callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Ok(new List<LeaderboardEntry>()));
        }

        public void FetchPlayerRank(Action<LeaderboardResult<LeaderboardEntry>> callback)
        {
            callback?.Invoke(LeaderboardResult<LeaderboardEntry>.Fail("Not yet implemented"));
        }

        public void FetchFriendsScores(int count, Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Fail("Steam not available"));
                return;
            }

            // TODO: Implement friends-only download via ELeaderboardDataRequest.k_ELeaderboardDataRequestFriends
            callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Ok(new List<LeaderboardEntry>()));
        }
#else
        public bool IsAvailable => false;

        public void SubmitScore(LeaderboardEntry entry, Action<LeaderboardResult<bool>> callback)
            => callback?.Invoke(LeaderboardResult<bool>.Fail("DESK42_STEAM not defined"));

        public void FetchTopScores(int count, Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
            => callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Fail("DESK42_STEAM not defined"));

        public void FetchPlayerRank(Action<LeaderboardResult<LeaderboardEntry>> callback)
            => callback?.Invoke(LeaderboardResult<LeaderboardEntry>.Fail("DESK42_STEAM not defined"));

        public void FetchFriendsScores(int count, Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
            => callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Fail("DESK42_STEAM not defined"));
#endif
    }
}
