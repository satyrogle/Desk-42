// ============================================================
// DESK 42 — No-op Analytics Backend
//
// Default backend. Discards every event. Used when no real
// telemetry is wired up so AnalyticsService can be called
// unconditionally without null-checks at every call site.
// ============================================================

using System.Collections.Generic;

namespace Desk42.Meta.Analytics
{
    public sealed class NoOpAnalyticsBackend : IAnalyticsBackend
    {
        public void Initialize() { }
        public void Track(string eventName, IReadOnlyDictionary<string, object> props) { }
        public void SetUserProperty(string key, object value) { }
        public void Flush() { }
        public void Shutdown() { }
    }
}
