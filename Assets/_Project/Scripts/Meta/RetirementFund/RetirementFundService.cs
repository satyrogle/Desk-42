// ============================================================
// DESK 42 — Retirement Fund Service (MonoBehaviour)
//
// Awards AuditPoints (the cosmetic-progression currency) when
// a run completes. AuditPoints persist in
// MetaProgressData.Cosmetics.AuditPoints and are spent in the
// RetirementLoungePanel on the InternalAudit hub.
//
// Formula:
//   1 point per claim resolved this run
//   +5 if SoulIntegrity at run end >= 80
//   +3 per shift completed this run
//   +10 if fugue was triggered and player still finished
//
// Self-bootstrapping. GameManager.EnsureChildSystems spawns one.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Meta.RetirementFund
{
    [DisallowMultipleComponent]
    public sealed class RetirementFundService : MonoBehaviour
    {
        private bool _subscribed;

        private void OnEnable()
        {
            if (_subscribed) return;
            _subscribed = true;
            RumorMill.OnRunCompleted += HandleRunCompleted;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            RumorMill.OnRunCompleted -= HandleRunCompleted;
        }

        private void HandleRunCompleted(RunCompletedEvent e)
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null || meta.Cosmetics == null) return;

            int award = e.ClaimsProcessed;
            if (e.SoulIntegrity >= 80f)   award += 5;
            if (e.FugueTriggered)         award += 10;
            award += e.ShiftNumber * 3;

            meta.Cosmetics.AuditPoints += award;
            SaveSystem.SaveMeta(meta);

            Debug.Log($"[RetirementFund] +{award} AuditPoints " +
                      $"(claims:{e.ClaimsProcessed} soul:{e.SoulIntegrity:F0} " +
                      $"shifts:{e.ShiftNumber} fugue:{e.FugueTriggered}). " +
                      $"Balance: {meta.Cosmetics.AuditPoints}");
        }
    }
}
