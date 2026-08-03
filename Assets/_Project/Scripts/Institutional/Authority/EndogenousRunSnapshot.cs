using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal enum EndogenousCommitPhase
    {
        TickCommitted,
        IncidentCandidatesCommitted,
        PublicObservationsCommitted,
        DocketCommitted,
        CaseOpened,
        RulingCommitted,
        ScopeEffectsCommitted,
    }

    [Serializable]
    internal sealed class EndogenousRunSnapshot
    {
        internal const int CurrentSchemaVersion = 1;
        internal const string CurrentRulesetVersion = "endogenous-run-snapshot-v1";

        internal int SchemaVersion = CurrentSchemaVersion;
        internal string RulesetVersion = CurrentRulesetVersion;
        internal string SnapshotId;
        internal EndogenousCommitPhase Phase;
        internal long CurrentTick;
        internal int SocietyEventLedgerCursor;
        internal int MaterialEventLedgerCursor;
        internal SocietyState Society;
        internal InstitutionalMaterialWorld MaterialWorld;
        internal EndogenousDocketState Docket;
        internal List<string> AppliedCommandIds = new();
        internal List<string> AppliedTransitionIds = new();
        internal List<string> PendingAppealIds = new();
        internal List<string> RelianceEventIds = new();
        internal List<string> PendingPublicObservationIds = new();
        internal List<string> ExclusiveEntitlementIds = new();
    }

    internal static class EndogenousRunSnapshotService
    {
        internal static EndogenousRunSnapshot Capture(
            string snapshotId,
            EndogenousCommitPhase phase,
            SocietyState society,
            InstitutionalMaterialWorld materialWorld,
            EndogenousDocketState docket)
        {
            if (string.IsNullOrWhiteSpace(snapshotId))
                throw new ArgumentException("A stable snapshot id is required.", nameof(snapshotId));
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (materialWorld == null) throw new ArgumentNullException(nameof(materialWorld));
            if (docket == null) throw new ArgumentNullException(nameof(docket));
            var snapshot = new EndogenousRunSnapshot
            {
                SnapshotId = snapshotId,
                Phase = phase,
                CurrentTick = society.CurrentTick,
                SocietyEventLedgerCursor = society.EventLedger.Count,
                MaterialEventLedgerCursor = materialWorld.EventLedger.Count,
                Society = SocietyStateDeepCopy.Copy(society),
                MaterialWorld = InstitutionalMaterialWorldDeepCopy.Copy(materialWorld),
                Docket = EndogenousDocketStateDeepCopy.Copy(docket),
            };
            snapshot.AppliedCommandIds = AppliedCommandIds(snapshot.Docket);
            snapshot.AppliedTransitionIds = AppliedTransitionIds(
                snapshot.Society, snapshot.MaterialWorld, snapshot.Docket);
            EndogenousRunSnapshotValidator.Validate(snapshot);
            return snapshot;
        }

        internal static List<string> AppliedCommandIds(EndogenousDocketState docket)
        {
            var result = new List<string>(docket.Rulings.Count);
            for (int i = 0; i < docket.Rulings.Count; i++)
                result.Add(docket.Rulings[i].PlayerCommandId);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        internal static List<string> AppliedTransitionIds(
            SocietyState society,
            InstitutionalMaterialWorld materialWorld,
            EndogenousDocketState docket)
        {
            var result = new List<string>();
            for (int i = 0; i < society.EventLedger.Count; i++)
                result.Add($"society:{society.EventLedger[i].EventId}");
            for (int i = 0; i < materialWorld.EventLedger.Count; i++)
                result.Add($"material:{materialWorld.EventLedger[i].EventId}");
            for (int i = 0; i < docket.IncidentCandidates.Count; i++)
                result.Add($"incident:{docket.IncidentCandidates[i].CandidateId}");
            for (int i = 0; i < docket.Observations.Count; i++)
                result.Add($"observation:{docket.Observations[i].ObservationId}");
            for (int i = 0; i < docket.DocketCandidates.Count; i++)
                result.Add($"docket:{docket.DocketCandidates[i].DocketCandidateId}");
            for (int i = 0; i < docket.OpenCases.Count; i++)
                result.Add($"case:{docket.OpenCases[i].CaseId}");
            for (int i = 0; i < docket.Rulings.Count; i++)
                result.Add($"ruling:{docket.Rulings[i].RulingId}");
            for (int i = 0; i < docket.ScopeApplicationTraces.Count; i++)
                result.Add($"scope:{docket.ScopeApplicationTraces[i].TraceId}");
            result.Sort(StringComparer.Ordinal);
            return result;
        }
    }

    internal static class EndogenousRunSnapshotValidator
    {
        internal static void Validate(EndogenousRunSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.SchemaVersion != EndogenousRunSnapshot.CurrentSchemaVersion ||
                !string.Equals(
                    snapshot.RulesetVersion,
                    EndogenousRunSnapshot.CurrentRulesetVersion,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(snapshot.SnapshotId) ||
                !Enum.IsDefined(typeof(EndogenousCommitPhase), snapshot.Phase) ||
                snapshot.Society == null || snapshot.MaterialWorld == null ||
                snapshot.Docket == null || snapshot.AppliedCommandIds == null ||
                snapshot.AppliedTransitionIds == null ||
                snapshot.PendingAppealIds == null || snapshot.RelianceEventIds == null ||
                snapshot.PendingPublicObservationIds == null ||
                snapshot.ExclusiveEntitlementIds == null)
            {
                throw new InvalidOperationException(
                    "Endogenous run snapshot is incomplete or unsupported.");
            }
            SocietyStateValidator.Validate(snapshot.Society);
            InstitutionalMaterialWorldValidator.Validate(
                snapshot.MaterialWorld, snapshot.Society);
            EndogenousDocketValidator.Validate(snapshot.Docket, snapshot.Society);
            if (snapshot.CurrentTick != snapshot.Society.CurrentTick ||
                snapshot.SocietyEventLedgerCursor != snapshot.Society.EventLedger.Count ||
                snapshot.MaterialEventLedgerCursor !=
                snapshot.MaterialWorld.EventLedger.Count)
            {
                throw new InvalidOperationException(
                    "Snapshot cursors do not match their committed ledgers.");
            }
            RequireExact(
                snapshot.AppliedCommandIds,
                EndogenousRunSnapshotService.AppliedCommandIds(snapshot.Docket),
                "applied command");
            RequireExact(
                snapshot.AppliedTransitionIds,
                EndogenousRunSnapshotService.AppliedTransitionIds(
                    snapshot.Society, snapshot.MaterialWorld, snapshot.Docket),
                "applied transition");
            UniqueStable(snapshot.PendingAppealIds, "pending appeal");
            UniqueStable(snapshot.RelianceEventIds, "reliance event");
            UniqueStable(
                snapshot.PendingPublicObservationIds,
                "pending public observation");
            UniqueStable(snapshot.ExclusiveEntitlementIds, "exclusive entitlement");
        }

        private static void RequireExact(
            IReadOnlyList<string> actual,
            IReadOnlyList<string> expected,
            string description)
        {
            if (actual.Count != expected.Count)
                throw new InvalidOperationException(
                    $"Snapshot {description} identities are incomplete.");
            for (int i = 0; i < actual.Count; i++)
            {
                if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Snapshot {description} identities are not canonical.");
            }
        }

        private static void UniqueStable(IReadOnlyList<string> values, string description)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i]) || !ids.Add(values[i]))
                    throw new InvalidOperationException(
                        $"Snapshot contains an invalid {description} identity.");
            }
        }
    }
}
