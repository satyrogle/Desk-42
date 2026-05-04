// ============================================================
// DESK 42 — Local-Log Analytics Backend
//
// Writes one JSON line per event to
//   {persistentDataPath}/analytics/{yyyy-MM-dd}.jsonl
//
// Useful during dev / playtests: gives a usable funnel without
// any third-party SDK. A future backend (PlayFab/Steam) can
// replace it without changing call sites.
//
// Buffered in memory; flushed on Flush() or every N events.
// All file I/O is wrapped in try/catch — analytics must never
// crash the game.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Desk42.Meta.Analytics
{
    public sealed class LocalLogAnalyticsBackend : IAnalyticsBackend
    {
        private const int FlushAfterEvents = 32;

        private readonly object _lock = new();
        private readonly Dictionary<string, object> _userProps = new();
        private readonly StringBuilder _buffer = new();
        private int _bufferedCount;
        private string _logPath;

        public void Initialize()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "analytics");
                Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir,
                    $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
                Debug.Log($"[Analytics] Logging to {_logPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] Init failed: {ex.Message}");
                _logPath = null;
            }
        }

        public void Track(string eventName, IReadOnlyDictionary<string, object> props)
        {
            if (_logPath == null) return;

            var record = new Dictionary<string, object>
            {
                ["ts"]    = DateTime.UtcNow.ToString("o"),
                ["event"] = eventName,
            };
            foreach (var kvp in _userProps) record[kvp.Key] = kvp.Value;
            if (props != null)
                foreach (var kvp in props) record[kvp.Key] = kvp.Value;

            string line;
            try { line = JsonConvert.SerializeObject(record); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] Serialize failed for '{eventName}': {ex.Message}");
                return;
            }

            lock (_lock)
            {
                _buffer.AppendLine(line);
                _bufferedCount++;
                if (_bufferedCount >= FlushAfterEvents) FlushLocked();
            }
        }

        public void SetUserProperty(string key, object value)
        {
            lock (_lock) _userProps[key] = value;
        }

        public void Flush()
        {
            lock (_lock) FlushLocked();
        }

        public void Shutdown() => Flush();

        private void FlushLocked()
        {
            if (_bufferedCount == 0 || _logPath == null) return;
            try
            {
                File.AppendAllText(_logPath, _buffer.ToString());
                _buffer.Clear();
                _bufferedCount = 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] Flush failed: {ex.Message}");
            }
        }
    }
}
