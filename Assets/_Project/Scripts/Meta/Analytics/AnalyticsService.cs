// ============================================================
// DESK 42 — Analytics Service
//
// Static facade. Game systems call:
//   Analytics.Track("claim_resolved", new() { ["card"] = "Redact" });
//
// One backend can be installed at boot (LocalLog by default in
// Editor + dev builds, NoOp in shipping builds unless overridden).
// Replacing the backend is a one-line change in AnalyticsBootstrap.
//
// Thread-safety: assumes Track() is called from the main thread
// (Unity coroutines / event handlers). Backend impls handle their
// own internal locking if they buffer.
// ============================================================

using System.Collections.Generic;
namespace Desk42.Meta.Analytics
{
    public static class TelemetryEvents
    {
        public const string OnboardingPhaseAdvanced = "onboarding_phase_advanced";
        public const string DarkIntelligenceUnlocked = "dark_intelligence_unlocked";
        public const string ExeFragmentCollected = "exe_fragment_collected";
        public const string ParadoxRecipeExecuted = "paradox_recipe_executed";
        public const string ParadoxRecipeFailed = "paradox_recipe_failed";
        public const string BossFightStarted = "boss_fight_started";
        public const string BossFightWon = "boss_fight_won";
        public const string CrashToWinCompleted = "crash_to_win_completed";
    }

    public static class Analytics
    {
        private static IAnalyticsBackend _backend = new NoOpAnalyticsBackend();

        public static IAnalyticsBackend Backend => _backend;

        /// <summary>Install a backend. Calls Initialize on it and
        /// Shutdown on the previous one.</summary>
        public static void SetBackend(IAnalyticsBackend backend)
        {
            if (backend == null) backend = new NoOpAnalyticsBackend();
            _backend?.Shutdown();
            _backend = backend;
            _backend.Initialize();
        }

        public static void Track(string eventName,
            IReadOnlyDictionary<string, object> props = null)
            => _backend.Track(eventName, props);

        public static void SetUserProperty(string key, object value)
            => _backend.SetUserProperty(key, value);

        public static void Flush() => _backend.Flush();
    }
}
