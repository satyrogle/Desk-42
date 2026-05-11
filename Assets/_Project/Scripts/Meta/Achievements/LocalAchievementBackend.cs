// ============================================================
// DESK 42 — Local Achievement Backend
//
// Persists unlocks to {persistentDataPath}/achievements.json.
// Useful in Editor / dev builds: gives a real unlock surface
// without Steam credentials. Replace at boot with the Steam
// backend on shipping builds.
//
// All file I/O wrapped in try/catch — achievement plumbing
// must never crash the game.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Desk42.Meta.Achievements
{
    public sealed class LocalAchievementBackend : IAchievementBackend
    {
        [Serializable]
        private sealed class Store
        {
            public HashSet<string> Unlocked = new();
        }

        private string _path;
        private Store  _store;

        public void Initialize()
        {
            _path = Path.Combine(Application.persistentDataPath, "achievements.json");
            try
            {
                if (File.Exists(_path))
                {
                    string json = File.ReadAllText(_path);
                    _store = JsonConvert.DeserializeObject<Store>(json) ?? new Store();
                }
                else
                {
                    _store = new Store();
                }
                Debug.Log($"[Achievements] Local store loaded ({_store.Unlocked.Count} unlocked).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Achievements] Init failed: {ex.Message}");
                _store = new Store();
            }
        }

        public void Unlock(string id)
        {
            if (string.IsNullOrEmpty(id) || _store == null) return;
            if (!_store.Unlocked.Add(id)) return; // already unlocked
            Debug.Log($"[Achievements] Unlocked '{id}'.");
            Save();
        }

        public bool IsUnlocked(string id)
            => _store != null && _store.Unlocked.Contains(id);

        public void Shutdown() => Save();

        private void Save()
        {
            if (_path == null || _store == null) return;
            try
            {
                File.WriteAllText(_path, JsonConvert.SerializeObject(_store));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Achievements] Save failed: {ex.Message}");
            }
        }
    }
}
