// ============================================================
// DESK 42 — Analytics Backend Interface
//
// Pluggable backend so the rest of the game only sees one API
// (AnalyticsService.Track) regardless of where events go:
// no-op (default), local log file (dev/QA), Steam, PlayFab,
// Unity Analytics, etc.
//
// Implementations live alongside this file. Default backend is
// chosen by AnalyticsBootstrap based on platform / scripting
// defines.
// ============================================================

using System.Collections.Generic;

namespace Desk42.Meta.Analytics
{
    public interface IAnalyticsBackend
    {
        /// <summary>Called once after construction. Backend may
        /// open files, init SDKs, etc. Called from the main thread.</summary>
        void Initialize();

        /// <summary>Fire-and-forget event. Implementations must
        /// not throw — log and swallow errors instead.</summary>
        void Track(string eventName, IReadOnlyDictionary<string, object> props);

        /// <summary>Set a sticky user property (archetype, build,
        /// platform). Applied to subsequent Track calls when the
        /// backend supports it.</summary>
        void SetUserProperty(string key, object value);

        /// <summary>Persist any buffered events. Called on quit
        /// and on shift boundary.</summary>
        void Flush();

        /// <summary>Called once on shutdown.</summary>
        void Shutdown();
    }
}
