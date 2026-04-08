// ============================================================
// DESK 42 — PlayFab Leaderboard Provider
//
// PlayFab SDK integration for leaderboard operations.
// Guarded by DESK42_PLAYFAB define — compiles as a no-op stub
// without the PlayFab Unity SDK installed.
//
// Add DESK42_PLAYFAB to Scripting Define Symbols once the
// PlayFab SDK package is imported.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Leaderboard
{
    public sealed class PlayFabLeaderboardProvider : ILeaderboardProvider
    {
        public string ProviderName => "PlayFab";

        private readonly string _statisticName;

        public PlayFabLeaderboardProvider(string statisticName = "EfficiencyRating")
        {
            _statisticName = statisticName;
        }

#if DESK42_PLAYFAB
        private bool _loggedIn;

        public bool IsAvailable => _loggedIn;

        public void Login(Action<bool> onComplete)
        {
            var request = new PlayFab.ClientModels.LoginWithCustomIDRequest
            {
                CustomId          = SystemInfo.deviceUniqueIdentifier,
                CreateAccount     = true,
            };

            PlayFab.PlayFabClientAPI.LoginWithCustomID(request,
                result => { _loggedIn = true; onComplete?.Invoke(true); },
                error  => { Debug.LogError($"[PlayFab] Login failed: {error.ErrorMessage}"); onComplete?.Invoke(false); });
        }

        public void SubmitScore(LeaderboardEntry entry, Action<LeaderboardResult<bool>> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(LeaderboardResult<bool>.Fail("PlayFab not logged in"));
                return;
            }

            var request = new PlayFab.ClientModels.UpdatePlayerStatisticsRequest
            {
                Statistics = new List<PlayFab.ClientModels.StatisticUpdate>
                {
                    new() { StatisticName = _statisticName, Value = entry.Score }
                }
            };

            PlayFab.PlayFabClientAPI.UpdatePlayerStatistics(request,
                _ => callback?.Invoke(LeaderboardResult<bool>.Ok(true)),
                error => callback?.Invoke(LeaderboardResult<bool>.Fail(error.ErrorMessage)));
        }

        public void FetchTopScores(int count, Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Fail("PlayFab not logged in"));
                return;
            }

            var request = new PlayFab.ClientModels.GetLeaderboardRequest
            {
                StatisticName   = _statisticName,
                StartPosition   = 0,
                MaxResultsCount = count,
            };

            PlayFab.PlayFabClientAPI.GetLeaderboard(request,
                result =>
                {
                    var entries = new List<LeaderboardEntry>();
                    foreach (var item in result.Leaderboard)
                    {
                        entries.Add(new LeaderboardEntry
                        {
                            PlayerId   = item.PlayFabId,
                            PlayerName = item.DisplayName ?? item.PlayFabId,
                            Rank       = item.Position + 1,
                            Score      = item.StatValue,
                        });
                    }
                    callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Ok(entries));
                },
                error => callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Fail(error.ErrorMessage)));
        }

        public void FetchPlayerRank(Action<LeaderboardResult<LeaderboardEntry>> callback)
        {
            callback?.Invoke(LeaderboardResult<LeaderboardEntry>.Fail("Not yet implemented"));
        }

        public void FetchFriendsScores(int count, Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
        {
            // PlayFab doesn't have a native friends-only leaderboard without custom logic
            callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Ok(new List<LeaderboardEntry>()));
        }
#else
        public bool IsAvailable => false;

        public void SubmitScore(LeaderboardEntry entry, Action<LeaderboardResult<bool>> callback)
            => callback?.Invoke(LeaderboardResult<bool>.Fail("DESK42_PLAYFAB not defined"));

        public void FetchTopScores(int count, Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
            => callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Fail("DESK42_PLAYFAB not defined"));

        public void FetchPlayerRank(Action<LeaderboardResult<LeaderboardEntry>> callback)
            => callback?.Invoke(LeaderboardResult<LeaderboardEntry>.Fail("DESK42_PLAYFAB not defined"));

        public void FetchFriendsScores(int count, Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
            => callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Fail("DESK42_PLAYFAB not defined"));
#endif
    }
}
