// ============================================================
// DESK 42 — Achievement Bootstrap (MonoBehaviour)
//
// Self-bootstrapping. GameManager.EnsureChildSystems adds one
// of these as a child. On Awake it picks a backend and
// subscribes to the RumorMill events that grant achievements.
//
// Backend selection (default — override at runtime via
// Achievements.SetBackend(...)):
//   DESK42_STEAM defined → SteamAchievementBackend
//   Editor / dev build   → LocalAchievementBackend
//   Otherwise            → NoOpAchievementBackend
//
// Achievement triggers:
//   Milestones (RumorMill.OnMilestoneReached) → endings
//   First-time events                          → first-X achievements
//   OnRunCompleted                              → per-shift mastery
//   Lifetime totals (Meta.LifetimeStats)       → career achievements
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Meta.Achievements
{
    [DisallowMultipleComponent]
    public sealed class AchievementBootstrap : MonoBehaviour
    {
        private bool _subscribed;

        private void Awake()
        {
#if DESK42_STEAM
            Achievements.SetBackend(new SteamAchievementBackend());
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            Achievements.SetBackend(new LocalAchievementBackend());
#else
            Achievements.SetBackend(new NoOpAchievementBackend());
#endif
        }

        private void OnEnable()
        {
            if (_subscribed) return;
            _subscribed = true;

            RumorMill.OnMilestoneReached += HandleMilestone;
            RumorMill.OnClaimResolved    += HandleClaimResolved;
            RumorMill.OnNDASigned        += HandleNDASigned;
            RumorMill.OnSanityChanged    += HandleSanityChanged;
            RumorMill.OnShiftLifecycle   += HandleShiftLifecycle;
            RumorMill.OnTideEscalated    += HandleTideEscalated;
            RumorMill.OnRunCompleted     += HandleRunCompleted;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;

            RumorMill.OnMilestoneReached -= HandleMilestone;
            RumorMill.OnClaimResolved    -= HandleClaimResolved;
            RumorMill.OnNDASigned        -= HandleNDASigned;
            RumorMill.OnSanityChanged    -= HandleSanityChanged;
            RumorMill.OnShiftLifecycle   -= HandleShiftLifecycle;
            RumorMill.OnTideEscalated    -= HandleTideEscalated;
            RumorMill.OnRunCompleted     -= HandleRunCompleted;
        }

        // ── Per-shift counters (reset on shift start) ─────────

        private int _shiftClaimCount;
        private int _shiftNdaCount;

        // ── Event Handlers ────────────────────────────────────

        private void HandleMilestone(MilestoneReachedEvent e)
        {
            switch (e.MilestoneId)
            {
                case MilestoneID.TheCompanyMan:
                    Achievements.Unlock(AchievementCatalog.EndingTheCompanyMan);
                    break;
                case MilestoneID.TheWhistleblower:
                    Achievements.Unlock(AchievementCatalog.EndingTheWhistleblower);
                    break;
                case MilestoneID.TheGreatAudit:
                    Achievements.Unlock(AchievementCatalog.EndingTheGreatAudit);
                    break;
                case MilestoneID.RedactedTruth:
                    Achievements.Unlock(AchievementCatalog.EndingRedactedTruth);
                    break;
                case MilestoneID.TheBreach:
                    Achievements.Unlock(AchievementCatalog.EndingTheBreach);
                    break;
            }
        }

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            _shiftClaimCount++;
            Achievements.Unlock(AchievementCatalog.FirstClaimResolved);
        }

        private void HandleNDASigned(NDASignedEvent e)
        {
            _shiftNdaCount++;
            Achievements.Unlock(AchievementCatalog.FirstNDASigned);
        }

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            if (e.TriggeredFugue)
                Achievements.Unlock(AchievementCatalog.FirstFugueSurvived);
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart)
            {
                _shiftClaimCount = 0;
                _shiftNdaCount   = 0;
                return;
            }

            // Shift ended (not start). Per-shift mastery checks.
            Achievements.Unlock(AchievementCatalog.FirstShiftCompleted);
            if (_shiftNdaCount == 0)
                Achievements.Unlock(AchievementCatalog.ShiftZeroNDA);
            if (_shiftClaimCount >= 25)
                Achievements.Unlock(AchievementCatalog.Shift25Claims);
            else if (_shiftClaimCount >= 10)
                Achievements.Unlock(AchievementCatalog.Shift10Claims);

            // Career totals from lifetime stats (already updated
            // by the time the shift-end milestone fires).
            var meta = GameManager.Instance?.Meta;
            if (meta != null)
            {
                int total = meta.LifetimeStats.TotalShiftsCompleted;
                if (total >= 100) Achievements.Unlock(AchievementCatalog.Career100Shifts);
                else if (total >= 50)  Achievements.Unlock(AchievementCatalog.Career50Shifts);
                else if (total >= 10)  Achievements.Unlock(AchievementCatalog.Career10Shifts);
            }
        }

        private void HandleTideEscalated(TideEscalatedEvent e)
        {
            if (e.NewLevel >= 3) Achievements.Unlock(AchievementCatalog.TidePeak);
        }

        private void HandleRunCompleted(RunCompletedEvent e)
        {
            // Run-level mastery (not per-shift)
            if (e.SoulIntegrity >= 99.5f)
                Achievements.Unlock(AchievementCatalog.ShiftPerfectSoul);
        }
    }
}
