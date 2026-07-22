// ============================================================
// DESK 42 — Analytics Bootstrap (MonoBehaviour)
//
// Self-bootstrapping. GameManager.EnsureChildSystems() adds one
// of these as a child. On Awake it picks a backend and
// subscribes to the RumorMill events that form the gameplay
// funnel.
//
// Backend selection:
//   Editor / Development build → LocalLogAnalyticsBackend
//   Release build              → NoOpAnalyticsBackend (until
//     a real provider is wired)
//
// Override at runtime: call Analytics.SetBackend(new YourBackend()).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Meta.Analytics
{
    [DisallowMultipleComponent]
    public sealed class AnalyticsBootstrap : MonoBehaviour
    {
        private bool _subscribed;

        private void Awake()
        {
            // Pick default backend
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Analytics.SetBackend(new LocalLogAnalyticsBackend());
#else
            Analytics.SetBackend(new NoOpAnalyticsBackend());
#endif

            Analytics.SetUserProperty("build",      Application.version);
            Analytics.SetUserProperty("platform",   Application.platform.ToString());
            Analytics.SetUserProperty("is_editor",  Application.isEditor);

            Analytics.Track("session_start");
        }

        private void OnEnable()
        {
            if (_subscribed) return;
            _subscribed = true;

            RumorMill.OnShiftLifecycle    += HandleShiftLifecycle;
            RumorMill.OnClaimResolved     += HandleClaimResolved;
            RumorMill.OnMoralChoice       += HandleMoralChoice;
            RumorMill.OnNDASigned         += HandleNDASigned;
            RumorMill.OnSanityChanged     += HandleSanityChanged;
            RumorMill.OnMilestoneReached  += HandleMilestone;
            RumorMill.OnRunCompleted      += HandleRunCompleted;
            RumorMill.OnTideEscalated     += HandleTideEscalated;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;

            RumorMill.OnShiftLifecycle    -= HandleShiftLifecycle;
            RumorMill.OnClaimResolved     -= HandleClaimResolved;
            RumorMill.OnMoralChoice       -= HandleMoralChoice;
            RumorMill.OnNDASigned         -= HandleNDASigned;
            RumorMill.OnSanityChanged     -= HandleSanityChanged;
            RumorMill.OnMilestoneReached  -= HandleMilestone;
            RumorMill.OnRunCompleted      -= HandleRunCompleted;
            RumorMill.OnTideEscalated     -= HandleTideEscalated;
        }

        private void OnApplicationQuit()
        {
            Analytics.Track("session_end");
            Analytics.Flush();
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            Analytics.Track(e.IsStart ? "shift_started" : "shift_ended",
                new Dictionary<string, object>
                {
                    ["shift"] = e.ShiftNumber,
                    ["phase"] = e.Phase.ToString(),
                    ["seed"]  = e.Seed,
                });
        }

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            Analytics.Track("claim_resolved", new Dictionary<string, object>
            {
                ["claim_id"]     = e.ClaimId,
                ["resolution_kind"] = e.Kind.ToString().ToLowerInvariant(),
                ["credits"]      = e.CreditsEarned,
                ["soul_cost"]    = e.SoulCost,
                ["client"]       = e.ClientVariantId,
                ["species"]      = e.ClientSpeciesId,
            });
        }

        private void HandleMoralChoice(MoralChoiceEvent e)
        {
            Analytics.Track("moral_choice", new Dictionary<string, object>
            {
                ["claim_id"]   = e.ClaimId,
                ["action"]     = e.ActionType.ToString(),
                ["unethical"]  = e.WasUnethical,
                ["injury_d"]   = e.MoralInjuryDelta,
                ["inverted"]   = e.WasInverted,
            });
        }

        private void HandleNDASigned(NDASignedEvent e)
        {
            Analytics.Track("nda_signed", new Dictionary<string, object>
            {
                ["claim_id"] = e.ClaimId,
                ["total"]    = e.TotalNDACount,
            });
        }

        // Cache last-sanity to avoid spamming the funnel; only
        // log fugue triggers and large drops.
        private float _lastSanity = 100f;
        private void HandleSanityChanged(SanityChangedEvent e)
        {
            if (e.TriggeredFugue)
            {
                Analytics.Track("fugue_triggered", new Dictionary<string, object>
                {
                    ["sanity_before"] = e.Previous,
                    ["sanity_after"]  = e.Current,
                });
            }
            // Track threshold crossings only (50, 25, 10) to keep volume sane.
            int prevBucket = SanityBucket(_lastSanity);
            int newBucket  = SanityBucket(e.Current);
            if (newBucket < prevBucket)
            {
                Analytics.Track("sanity_threshold_crossed", new Dictionary<string, object>
                {
                    ["bucket"] = newBucket, // 50, 25, 10, 0
                });
            }
            _lastSanity = e.Current;
        }

        private static int SanityBucket(float s)
        {
            if (s >= 50f) return 100;
            if (s >= 25f) return 50;
            if (s >= 10f) return 25;
            if (s > 0f)   return 10;
            return 0;
        }

        private void HandleMilestone(MilestoneReachedEvent e)
        {
            Analytics.Track("milestone_reached", new Dictionary<string, object>
            {
                ["id"]    = e.MilestoneId.ToString(),
                ["shift"] = e.ShiftNumber,
                ["tag"]   = e.Tag,
            });
        }

        private void HandleRunCompleted(RunCompletedEvent e)
        {
            Analytics.Track("run_completed", new Dictionary<string, object>
            {
                ["shift"]            = e.ShiftNumber,
                ["claims_processed"] = e.ClaimsProcessed,
                ["credits"]          = e.CreditsEarned,
                ["efficiency"]       = e.EfficiencyRating,
                ["soul"]             = e.SoulIntegrity,
                ["sanity"]           = e.Sanity,
                ["debt"]             = e.PersonalExpenseDebt,
                ["combo"]            = e.ComboMultiplier,
                ["archetype"]        = e.ArchetypeId,
                ["seed"]             = e.SeedCode,
                ["fugue"]            = e.FugueTriggered,
            });
            Analytics.Flush();
        }

        private void HandleTideEscalated(TideEscalatedEvent e)
        {
            // Only log overperformance escalations (the interesting signal)
            if (!e.IsOverperformance) return;
            Analytics.Track("tide_escalated", new Dictionary<string, object>
            {
                ["from"] = e.OldLevel,
                ["to"]   = e.NewLevel,
            });
        }
    }
}
