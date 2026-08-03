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
        internal const int CurrentSchemaVersion = 2;
        internal const string CurrentRulesetVersion = "endogenous-run-snapshot-v2";

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
                Society = SocietyStateDeepCopy.Copy(society),
                MaterialWorld = InstitutionalMaterialWorldDeepCopy.Copy(materialWorld),
                Docket = EndogenousDocketStateDeepCopy.Copy(docket),
            };
            RefreshInPlace(snapshot, snapshotId, phase);
            return snapshot;
        }

        internal static void RefreshInPlace(
            EndogenousRunSnapshot snapshot,
            string snapshotId,
            EndogenousCommitPhase phase)
        {
            RefreshMetadataInPlace(snapshot, snapshotId, phase);
            EndogenousRunSnapshotValidator.Validate(snapshot);
        }

        internal static void RefreshMetadataInPlace(
            EndogenousRunSnapshot snapshot,
            string snapshotId,
            EndogenousCommitPhase phase)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(snapshotId))
                throw new ArgumentException(
                    "A stable snapshot id is required.", nameof(snapshotId));
            if (snapshot.Society == null || snapshot.MaterialWorld == null ||
                snapshot.Docket == null)
                throw new InvalidOperationException(
                    "A live snapshot requires society, material and docket state.");
            snapshot.SnapshotId = snapshotId;
            snapshot.Phase = phase;
            snapshot.CurrentTick = snapshot.Society.CurrentTick;
            snapshot.SocietyEventLedgerCursor =
                snapshot.Society.EventLedger.Count;
            snapshot.MaterialEventLedgerCursor =
                snapshot.MaterialWorld.EventLedger.Count;
            snapshot.AppliedCommandIds = AppliedCommandIds(snapshot.Docket);
            snapshot.AppliedTransitionIds = AppliedTransitionIds(
                snapshot.Society, snapshot.MaterialWorld, snapshot.Docket);
            snapshot.PendingAppealIds = PendingAppealIds(snapshot.Docket);
            snapshot.RelianceEventIds ??= new List<string>();
            snapshot.PendingPublicObservationIds ??= new List<string>();
            snapshot.ExclusiveEntitlementIds ??= new List<string>();
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
            for (int i = 0; i < docket.RemedyApplicationTraces.Count; i++)
                result.Add($"remedy:{docket.RemedyApplicationTraces[i].TraceId}");
            for (int i = 0; i < docket.AccessRemedyApplicationTraces.Count; i++)
                result.Add(
                    $"access-remedy:{docket.AccessRemedyApplicationTraces[i].TraceId}");
            for (int i = 0;
                 i < docket.CollectiveRemedyApplicationTraces.Count;
                 i++)
                result.Add(
                    $"collective-remedy:{docket.CollectiveRemedyApplicationTraces[i].TraceId}");
            for (int i = 0; i < docket.ScopeApplicationTraces.Count; i++)
                result.Add($"scope:{docket.ScopeApplicationTraces[i].TraceId}");
            for (int i = 0; i < docket.Appeals.Count; i++)
                result.Add($"appeal:{docket.Appeals[i].AppealId}");
            for (int i = 0; i < docket.Holdings.Count; i++)
                result.Add($"holding:{docket.Holdings[i].HoldingId}");
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        internal static List<string> PendingAppealIds(EndogenousDocketState docket)
        {
            var result = new List<string>();
            for (int i = 0; i < docket.Appeals.Count; i++)
                if (!docket.Appeals[i].Resolved)
                    result.Add(docket.Appeals[i].AppealId);
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
            ValidateRemedyMaterialTransitions(snapshot);
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
            RequireExact(
                snapshot.PendingAppealIds,
                EndogenousRunSnapshotService.PendingAppealIds(snapshot.Docket),
                "pending appeal");
            UniqueStable(snapshot.RelianceEventIds, "reliance event");
            UniqueStable(
                snapshot.PendingPublicObservationIds,
                "pending public observation");
            UniqueStable(snapshot.ExclusiveEntitlementIds, "exclusive entitlement");
        }

        private static void ValidateRemedyMaterialTransitions(
            EndogenousRunSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Docket.RemedyApplicationTraces.Count; i++)
            {
                EndogenousRemedyApplicationTrace trace =
                    snapshot.Docket.RemedyApplicationTraces[i];
                OfficialOwnershipState ownership =
                    snapshot.MaterialWorld.GetOfficialOwnership(trace.ResourceId);
                if (ownership == null || !string.Equals(
                        ownership.RegisteredOwnerId,
                        trace.NewPhysicalHolderId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Remedy {trace.TraceId} did not resolve to its registered owner.");
                }
                if (!trace.MaterialStateChanged) continue;
                MaterialWorldEvent materialEvent = snapshot.MaterialWorld.GetEvent(
                    trace.MaterialEventId);
                if (materialEvent == null ||
                    materialEvent.Kind != MaterialWorldEventKind.PossessionTransferred ||
                    materialEvent.Tick != trace.AppliedTick ||
                    !string.Equals(
                        materialEvent.CauseDecisionId,
                        trace.RulingId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        materialEvent.ResourceId,
                        trace.ResourceId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        materialEvent.PreviousPhysicalHolderId,
                        trace.PreviousPhysicalHolderId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        materialEvent.NewPhysicalHolderId,
                        trace.NewPhysicalHolderId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        materialEvent.ContextId,
                        trace.NewLocationContextId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Remedy {trace.TraceId} lacks its exact material transition.");
                }
            }
            for (int i = 0;
                 i < snapshot.Docket.AccessRemedyApplicationTraces.Count;
                 i++)
            {
                EndogenousAccessRemedyApplicationTrace trace =
                    snapshot.Docket.AccessRemedyApplicationTraces[i];
                MaterialAccessGrantState grant =
                    snapshot.MaterialWorld.GetAccessGrant(trace.AccessGrantId);
                if (grant == null || !string.Equals(
                        grant.AgentId,
                        trace.BeneficiaryAgentId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Access remedy {trace.TraceId} lost its beneficiary grant.");
                }
                if (!trace.MaterialStateChanged) continue;
                MaterialWorldEvent materialEvent = snapshot.MaterialWorld.GetEvent(
                    trace.MaterialEventId);
                if (materialEvent == null ||
                    materialEvent.Kind != MaterialWorldEventKind.AccessChanged ||
                    materialEvent.Tick != trace.AppliedTick ||
                    !string.Equals(materialEvent.CauseDecisionId, trace.RulingId,
                        StringComparison.Ordinal) ||
                    !string.Equals(materialEvent.StateRecordId, trace.AccessGrantId,
                        StringComparison.Ordinal) ||
                    materialEvent.StateBefore != trace.StateBefore ||
                    materialEvent.StateAfter != trace.StateAfter)
                {
                    throw new InvalidOperationException(
                        $"Access remedy {trace.TraceId} lacks its exact material transition.");
                }
            }
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
