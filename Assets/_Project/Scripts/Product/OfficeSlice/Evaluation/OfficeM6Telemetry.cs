using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeM6TelemetryEvent
    {
        public int SchemaVersion { get; internal set; }
        public string EventName { get; internal set; } = string.Empty;
        public double MonotonicSeconds { get; internal set; }
        public long Tick { get; internal set; }
        public int Shift { get; internal set; }
        public string Value { get; internal set; } = string.Empty;
    }

    /// <summary>Local, privacy-minimal JSONL evaluation recorder.</summary>
    public sealed class OfficeM6TelemetryRecorder : IDisposable
    {
        public const int CurrentSchemaVersion = 1;
        private readonly bool _enabled;
        private readonly string _sessionId;
        private readonly string _buildIdentifier;
        private readonly Stopwatch _clock;
        private readonly StreamWriter _writer;
        private readonly List<OfficeM6TelemetryEvent> _events = new();
        private readonly List<string> _pendingLines = new();
        private bool _closed;

        public OfficeM6TelemetryRecorder(
            bool enabled,
            string outputDirectory,
            string buildIdentifier)
        {
            _enabled = enabled;
            _buildIdentifier = buildIdentifier ?? string.Empty;
            _sessionId = Guid.NewGuid().ToString("N");
            _clock = Stopwatch.StartNew();
            if (!_enabled) return;
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException(
                    "An evaluation output directory is required.",
                    nameof(outputDirectory));
            Directory.CreateDirectory(outputDirectory);
            string filename = "desk42-m6-" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",
                    CultureInfo.InvariantCulture) + "-" +
                _sessionId + ".jsonl";
            FilePath = Path.Combine(outputDirectory, filename);
            _writer = new StreamWriter(new FileStream(
                FilePath, FileMode.CreateNew, FileAccess.Write,
                FileShare.Read), new UTF8Encoding(false), 4096)
            {
                AutoFlush = false,
            };
            Record("session_start", 0L, 1, _buildIdentifier);
            Flush();
        }

        public bool Enabled => _enabled;
        public bool Closed => _closed;
        public string FilePath { get; } = string.Empty;
        public string BuildIdentifier => _buildIdentifier;
        public IReadOnlyList<OfficeM6TelemetryEvent> Events => _events;

        public void Record(
            string eventName,
            long tick,
            int shift,
            string value = "")
        {
            if (!_enabled || _closed) return;
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Event name is required.",
                    nameof(eventName));
            var entry = new OfficeM6TelemetryEvent
            {
                SchemaVersion = CurrentSchemaVersion,
                EventName = eventName,
                MonotonicSeconds = _clock.Elapsed.TotalSeconds,
                Tick = tick,
                Shift = shift,
                Value = value ?? string.Empty,
            };
            _events.Add(entry);
            _pendingLines.Add(ToJson(entry));
        }

        public void Flush()
        {
            if (!_enabled || _closed || _writer == null) return;
            for (int i = 0; i < _pendingLines.Count; i++)
                _writer.WriteLine(_pendingLines[i]);
            _pendingLines.Clear();
            _writer.Flush();
        }

        public void CloseNormal(long tick, int shift)
        {
            if (!_enabled || _closed) return;
            Record("session_end", tick, shift,
                _clock.Elapsed.TotalSeconds.ToString("F3",
                    CultureInfo.InvariantCulture));
            Flush();
            _closed = true;
            _writer.Dispose();
            _clock.Stop();
        }

        public void Dispose()
        {
            CloseNormal(0L, 0);
        }

        private string ToJson(OfficeM6TelemetryEvent entry)
        {
            return "{\"schema_version\":" + entry.SchemaVersion +
                ",\"event\":\"" + Escape(entry.EventName) +
                "\",\"monotonic_seconds\":" +
                entry.MonotonicSeconds.ToString("F6",
                    CultureInfo.InvariantCulture) +
                ",\"session_id\":\"" + _sessionId +
                "\",\"build_id\":\"" + Escape(_buildIdentifier) +
                "\",\"tick\":" + entry.Tick +
                ",\"shift\":" + entry.Shift +
                ",\"value\":\"" + Escape(entry.Value) + "\"}";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
