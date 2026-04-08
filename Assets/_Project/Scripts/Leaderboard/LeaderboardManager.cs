// ============================================================
// DESK 42 — Leaderboard Manager (MonoBehaviour)
//
// Manages the active leaderboard provider with auto-detection:
//   1. Steam (if Steamworks.NET initialised)
//   2. PlayFab (if PlayFab SDK available and login succeeds)
//   3. Local JSON fallback (always available)
//
// Daily Brief mode auto-submits on CompleteRun().
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Leaderboard
{
    [DisallowMultipleComponent]
    public sealed class LeaderboardManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────

        public static LeaderboardManager Instance { get; private set; }

        // ── State ─────────────────────────────────────────────

        private ILeaderboardProvider _activeProvider;
        private LocalLeaderboardProvider _localFallback;

        public ILeaderboardProvider ActiveProvider => _activeProvider;
        public string ProviderName => _activeProvider?.ProviderName ?? "None";

        // ── Unity Lifecycle ───────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _localFallback = new LocalLeaderboardProvider();
            DetectProvider();
        }

        private void OnEnable()
        {
            RumorMill.OnClaimResolved += HandleClaimResolved;
        }

        private void OnDisable()
        {
            RumorMill.OnClaimResolved -= HandleClaimResolved;
        }

        // ── Provider Detection ────────────────────────────────

        private void DetectProvider()
        {
            // Try Steam first
            var steam = new SteamLeaderboardProvider();
            if (steam.IsAvailable)
            {
                _activeProvider = steam;
                Debug.Log("[LeaderboardManager] Using Steam provider.");
                return;
            }

            // Try PlayFab
            var playfab = new PlayFabLeaderboardProvider();
            if (playfab.IsAvailable)
            {
                _activeProvider = playfab;
                Debug.Log("[LeaderboardManager] Using PlayFab provider.");
                return;
            }

            // Fall back to local
            _activeProvider = _localFallback;
            Debug.Log("[LeaderboardManager] Using local JSON provider (offline mode).");
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Submit the current run's results to the leaderboard.
        /// Called automatically for Daily Brief runs.
        /// </summary>
        public void SubmitCurrentRun()
        {
            var run = GameManager.Instance?.Run;
            if (run == null) return;

            var entry = BuildEntry(run);
            _activeProvider.SubmitScore(entry, result =>
            {
                if (result.Success)
                    Debug.Log($"[LeaderboardManager] Score submitted via {_activeProvider.ProviderName}.");
                else
                    Debug.LogWarning($"[LeaderboardManager] Submit failed: {result.Error}. Saving locally.");

                // Always save locally as backup
                _localFallback.SubmitScore(entry, _ => { });
            });
        }

        /// <summary>Fetch top scores from the active provider.</summary>
        public void FetchTopScores(int count,
            System.Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
        {
            _activeProvider.FetchTopScores(count, callback);
        }

        /// <summary>Fetch friends-only leaderboard.</summary>
        public void FetchFriendsScores(int count,
            System.Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
        {
            _activeProvider.FetchFriendsScores(count, callback);
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            // No-op per claim — submission happens at run end
        }

        // ── Internal ──────────────────────────────────────────

        private LeaderboardEntry BuildEntry(RunStateController run)
        {
            var data = run.RawData;
            var vowIds = new List<string>();
            if (data.ActiveVows != null)
                foreach (var v in data.ActiveVows)
                    vowIds.Add(v.VowId);

            return new LeaderboardEntry
            {
                Score            = Mathf.RoundToInt(data.Stats.EfficiencyRating),
                SeedCode         = SeedEngine.CurrentSeedCode,
                ArchetypeId      = data.ArchetypeId,
                EfficiencyRating = data.Stats.EfficiencyRating,
                SoulIntegrity    = data.SoulIntegrity,
                ShiftCount       = data.ShiftNumber,
                ActiveVowsSerialized = string.Join(",", vowIds),
            };
        }
    }

    // ── Local JSON Fallback ──────────────────────────────────

    /// <summary>
    /// Offline leaderboard that persists to a local JSON file.
    /// Always available; used as backup even when cloud providers work.
    /// </summary>
    public sealed class LocalLeaderboardProvider : ILeaderboardProvider
    {
        public string ProviderName => "Local";
        public bool IsAvailable => true;

        private readonly string _filePath;
        private List<LeaderboardEntry> _entries = new();

        public LocalLeaderboardProvider()
        {
            _filePath = System.IO.Path.Combine(
                Application.persistentDataPath, "leaderboard_local.json");
            Load();
        }

        public void SubmitScore(LeaderboardEntry entry,
            System.Action<LeaderboardResult<bool>> callback)
        {
            entry.Rank = 0; // will be computed on sort
            _entries.Add(entry);
            _entries.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Reassign ranks
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].Rank = i + 1;

            // Keep top 100
            if (_entries.Count > 100)
                _entries.RemoveRange(100, _entries.Count - 100);

            Save();
            callback?.Invoke(LeaderboardResult<bool>.Ok(true));
        }

        public void FetchTopScores(int count,
            System.Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
        {
            int take = Mathf.Min(count, _entries.Count);
            callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Ok(
                _entries.GetRange(0, take)));
        }

        public void FetchPlayerRank(
            System.Action<LeaderboardResult<LeaderboardEntry>> callback)
        {
            callback?.Invoke(_entries.Count > 0
                ? LeaderboardResult<LeaderboardEntry>.Ok(_entries[0])
                : LeaderboardResult<LeaderboardEntry>.Fail("No entries"));
        }

        public void FetchFriendsScores(int count,
            System.Action<LeaderboardResult<List<LeaderboardEntry>>> callback)
        {
            // Local mode has no friends concept
            callback?.Invoke(LeaderboardResult<List<LeaderboardEntry>>.Ok(
                new List<LeaderboardEntry>()));
        }

        private void Save()
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(_entries,
                    Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(_filePath, json);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LocalLeaderboard] Save failed: {ex.Message}");
            }
        }

        private void Load()
        {
            try
            {
                if (System.IO.File.Exists(_filePath))
                {
                    string json = System.IO.File.ReadAllText(_filePath);
                    _entries = Newtonsoft.Json.JsonConvert.DeserializeObject<List<LeaderboardEntry>>(json)
                               ?? new List<LeaderboardEntry>();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LocalLeaderboard] Load failed: {ex.Message}");
                _entries = new List<LeaderboardEntry>();
            }
        }
    }
}

