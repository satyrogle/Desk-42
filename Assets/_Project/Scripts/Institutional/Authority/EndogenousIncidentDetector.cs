using System;
using System.Collections.Generic;
using System.Linq;

namespace Desk42.Institutional
{
    /// <summary>
    /// Recognises generic causal conflict patterns from authority-owned material events.
    /// Detection does not make those conflicts institutionally visible.
    /// </summary>
    internal static class EndogenousIncidentDetector
    {
        internal static List<IncidentCandidate> Detect(
            InstitutionalMaterialWorld world,
            SocietyState society,
            EndogenousDocketState state)
        {
            return DetectCore(world, society, state, validateBoundary: true);
        }

        internal static List<IncidentCandidate> DetectWithinValidatedTransaction(
            InstitutionalMaterialWorld world,
            SocietyState society,
            EndogenousDocketState state)
        {
            return DetectCore(world, society, state, validateBoundary: false);
        }

        private static List<IncidentCandidate> DetectCore(
            InstitutionalMaterialWorld world,
            SocietyState society,
            EndogenousDocketState state,
            bool validateBoundary)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (validateBoundary)
            {
                InstitutionalMaterialWorldValidator.Validate(world, society);
                EndogenousDocketValidator.Validate(state, society);
            }

            var added = new List<IncidentCandidate>();
            for (int i = 0; i < world.EventLedger.Count; i++)
            {
                MaterialWorldEvent materialEvent = world.EventLedger[i];
                IncidentCandidate candidate = FromMaterialEvent(world, society, materialEvent);
                if (candidate == null || ContainsDedupe(state, candidate.DedupeKey)) continue;
                state.IncidentCandidates.Add(candidate);
                added.Add(candidate);
            }

            if (validateBoundary)
                EndogenousDocketValidator.Validate(state, society);
            return added;
        }

        private static IncidentCandidate FromMaterialEvent(
            InstitutionalMaterialWorld world,
            SocietyState society,
            MaterialWorldEvent materialEvent)
        {
            SocietyEvent causalAction = FindCausalAction(society, materialEvent);
            switch (materialEvent.Kind)
            {
                case MaterialWorldEventKind.PossessionTransferred:
                    OfficialOwnershipState ownership = world.GetOfficialOwnership(
                        materialEvent.ResourceId);
                    if (ownership == null || string.Equals(
                            ownership.RegisteredOwnerId,
                            materialEvent.NewPhysicalHolderId,
                            StringComparison.Ordinal))
                    {
                        return null;
                    }
                    return NewCandidate(
                        materialEvent,
                        EndogenousIssueKindIds.PossessionDispute,
                        60,
                        materialEvent.ResourceId,
                        causalAction,
                        materialEvent.ActorAgentId);
                case MaterialWorldEventKind.AccessChanged:
                    if (!materialEvent.StateBefore || materialEvent.StateAfter) return null;
                    return NewCandidate(
                        materialEvent,
                        EndogenousIssueKindIds.AccessWithdrawal,
                        75,
                        null,
                        causalAction,
                        materialEvent.ActorAgentId,
                        materialEvent.TargetAgentId);
                case MaterialWorldEventKind.CollectiveCommitmentChanged:
                    if (!materialEvent.StateAfter) return null;
                    CollectiveCommitmentState collective = world.GetCollectiveCommitment(
                        materialEvent.StateRecordId);
                    if (collective == null) return null;
                    return NewCandidate(
                        materialEvent,
                        EndogenousIssueKindIds.CollectiveGrievance,
                        40,
                        null,
                        causalAction,
                        collective.MemberAgentIds.ToArray());
                default:
                    return null;
            }
        }

        private static IncidentCandidate NewCandidate(
            MaterialWorldEvent materialEvent,
            string conflictKindId,
            int harm,
            string resourceId,
            SocietyEvent causalAction,
            params string[] affectedAgentIds)
        {
            var causes = new List<string>(materialEvent.CauseEventIds.Count + 1)
            {
                materialEvent.EventId,
            };
            for (int i = 0; i < materialEvent.CauseEventIds.Count; i++)
                AddUnique(causes, materialEvent.CauseEventIds[i]);
            var affected = new List<string>();
            for (int i = 0; i < affectedAgentIds.Length; i++)
                if (!string.IsNullOrWhiteSpace(affectedAgentIds[i]))
                    AddUnique(affected, affectedAgentIds[i]);
            affected.Sort(StringComparer.Ordinal);

            return new IncidentCandidate
            {
                CandidateId = $"incident:{conflictKindId}:{materialEvent.EventId}",
                CauseEventIds = causes,
                AffectedAgentIds = affected,
                ConflictKindId = conflictKindId,
                DetectedTick = materialEvent.Tick,
                UnresolvedMaterialHarm = harm,
                SubjectResourceId = resourceId,
                DedupeKey = $"{conflictKindId}:{materialEvent.EventId}",
                ParentCaseId = causalAction?.ParentCaseId,
                OriginatingRulingId = causalAction?.EnablingRulingId,
                CausalAgentActionId = causalAction?.CauseDecisionId,
            };
        }

        private static SocietyEvent FindCausalAction(
            SocietyState society,
            MaterialWorldEvent materialEvent)
        {
            for (int causeIndex = 0;
                 causeIndex < materialEvent.CauseEventIds.Count;
                 causeIndex++)
            {
                string causeId = materialEvent.CauseEventIds[causeIndex];
                for (int eventIndex = 0; eventIndex < society.EventLedger.Count; eventIndex++)
                {
                    SocietyEvent candidate = society.EventLedger[eventIndex];
                    if (string.Equals(candidate.EventId, causeId, StringComparison.Ordinal))
                        return candidate;
                }
            }
            return null;
        }

        private static bool ContainsDedupe(EndogenousDocketState state, string dedupeKey)
        {
            for (int i = 0; i < state.IncidentCandidates.Count; i++)
            {
                if (string.Equals(
                        state.IncidentCandidates[i].DedupeKey,
                        dedupeKey,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddUnique(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return;
            values.Add(value);
        }
    }
}
