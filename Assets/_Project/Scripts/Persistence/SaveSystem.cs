// ============================================================
// DESK 42 — Save System
//
// Handles all serialization / deserialization and file I/O.
//
// Files (all in Application.persistentDataPath):
//   meta.json          — cross-run MetaProgressData (never deleted)
//   meta.json.bak      — backup written before every meta save
//   run.json           — mid-run RunData (deleted on completion)
//   run.json.bak       — backup in case of crash mid-save
//   offender_db.json   — deprecated (now part of meta.json)
//
// Safety contract:
//   1. Always write to a .tmp file first.
//   2. On successful write, rename .tmp -> target (atomic on most FS).
//   3. On load failure, fall back to .bak before giving up.
//   4. Never silently swallow exceptions — log them, return null.
// ============================================================

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Desk42.Core
{
    public static class SaveSystem
    {
        // ── Paths ─────────────────────────────────────────────

        private static string SaveDir =>
            Application.persistentDataPath;

        private static string MetaPath    => Path.Combine(SaveDir, "meta.json");
        private static string MetaBakPath => Path.Combine(SaveDir, "meta.json.bak");
        private static string RunPath     => Path.Combine(SaveDir, "run.json");
        private static string RunBakPath  => Path.Combine(SaveDir, "run.json.bak");

        // ── JSON Settings ─────────────────────────────────────

        private static readonly JsonSerializerSettings _settings = new()
        {
            Formatting            = Formatting.Indented,
            NullValueHandling     = NullValueHandling.Include,
            DefaultValueHandling  = DefaultValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            TypeNameHandling      = TypeNameHandling.None,
        };

        // ── Meta Progress ─────────────────────────────────────

        /// <summary>
        /// Save MetaProgressData to disk.
        /// Writes backup before overwriting the main file.
        /// </summary>
        public static bool SaveMeta(MetaProgressData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.RefreshTimestamp();
            return SafeWrite(MetaPath, MetaBakPath, data);
        }

        /// <summary>
        /// Load MetaProgressData from disk.
        /// Falls back to backup on primary failure.
        /// Returns a fresh instance if no save exists yet.
        /// </summary>
        public static MetaProgressData LoadMeta()
        {
            return Load<MetaProgressData>(MetaPath, MetaBakPath)
                   ?? new MetaProgressData();
        }

        // ── Run Data ─────────────────────────────────────────

        /// <summary>Save mid-run state. Safe to call frequently (e.g. between claims).</summary>
        public static bool SaveRun(RunData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.RefreshTimestamp();
            return SafeWrite(RunPath, RunBakPath, data);
        }

        /// <summary>
        /// Load RunData for mid-run resume.
        /// Returns null if no in-progress run exists.
        /// </summary>
        public static RunData LoadRun()
            => Load<RunData>(RunPath, RunBakPath);

        /// <summary>
        /// Returns true if an in-progress (not completed) run is on disk.
        /// Used by GameManager to offer "Continue" on the main menu.
        /// </summary>
        public static bool HasActiveRun()
        {
            var run = LoadRun();
            return run != null && !run.IsComplete && !run.IsAbandoned;
        }

        /// <summary>Delete the run save after completion or deliberate abandon.</summary>
        public static void DeleteRun()
        {
            DeleteIfExists(RunPath);
            DeleteIfExists(RunBakPath);
        }

        // ── Migration ─────────────────────────────────────────

        /// <summary>
        /// Run schema migration on a loaded MetaProgressData if its
        /// SaveVersion is behind the current version.
        /// Extend this method as you bump SaveVersion.
        /// </summary>
        public static MetaProgressData MigrateMetaIfNeeded(MetaProgressData data)
        {
            // Version 1 is current — no migration needed yet.
            // Future pattern:
            // if (data.SaveVersion < 2) MigrateTo2(data);
            // if (data.SaveVersion < 3) MigrateTo3(data);
            return data;
        }

        // ── Private Helpers ───────────────────────────────────

        /// <summary>
        /// Atomic-safe write:
        ///   1. Serialize to string.
        ///   2. Write backup of current file (if it exists).
        ///   3. Write new content to .tmp.
        ///   4. Replace target with .tmp.
        /// </summary>
        private static bool SafeWrite<T>(string targetPath, string bakPath, T data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, _settings);
                string tmpPath = targetPath + ".tmp";

                // Back up the current file before overwriting
                if (File.Exists(targetPath))
                    File.Copy(targetPath, bakPath, overwrite: true);

                // Write to temp first
                File.WriteAllText(tmpPath, json, System.Text.Encoding.UTF8);

                // Atomic rename (on Windows this is not truly atomic, but close enough)
                if (File.Exists(targetPath)) File.Delete(targetPath);
                File.Move(tmpPath, targetPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to write {targetPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load with backup fallback:
        ///   1. Try primary path.
        ///   2. On failure, try backup path.
        ///   3. On both failures, return null.
        /// </summary>
        private static T Load<T>(string primaryPath, string bakPath) where T : class
        {
            var result = TryLoad<T>(primaryPath);
            if (result != null) return result;

            if (File.Exists(bakPath))
            {
                Debug.LogWarning($"[SaveSystem] Primary save {primaryPath} failed. Falling back to backup.");
                result = TryLoad<T>(bakPath);
            }

            return result;
        }

        private static T TryLoad<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                return JsonConvert.DeserializeObject<T>(json, _settings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to load {path}: {ex.Message}");
                return null;
            }
        }

        private static void DeleteIfExists(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Debug.LogError($"[SaveSystem] Delete failed {path}: {ex.Message}"); }
        }

        // ── Debug ─────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Editor/debug only: print save file paths to console.</summary>
        public static void LogSavePaths()
        {
            Debug.Log($"[SaveSystem] Save directory: {SaveDir}");
            Debug.Log($"[SaveSystem] Meta: {MetaPath} (exists: {File.Exists(MetaPath)})");
            Debug.Log($"[SaveSystem] Run:  {RunPath} (exists: {File.Exists(RunPath)})");
        }

        /// <summary>Editor/debug only: wipe all save data. DESTRUCTIVE.</summary>
        public static void WipeAllSaveData()
        {
            DeleteIfExists(MetaPath);
            DeleteIfExists(MetaBakPath);
            DeleteRun();
            Debug.LogWarning("[SaveSystem] ⚠ All save data wiped.");
        }
#endif
    }
}
