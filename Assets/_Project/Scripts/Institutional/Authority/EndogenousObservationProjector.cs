using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Converts only recorded or formally submitted material into player-safe docket
    /// observations. Witness presence alone is intentionally insufficient.
    /// </summary>
    internal static class EndogenousObservationProjector
    {
        internal static List<DocketObservation> Project(
            InstitutionalMaterialWorld world,
            SocietyState society,
            EndogenousDocketState state)
        {
            return ProjectCore(world, society, state, validateBoundary: true);
        }

        internal static List<DocketObservation> ProjectWithinValidatedTransaction(
            InstitutionalMaterialWorld world,
            SocietyState society,
            EndogenousDocketState state)
        {
            return ProjectCore(world, society, state, validateBoundary: false);
        }

        private static List<DocketObservation> ProjectCore(
            InstitutionalMaterialWorld world,
            SocietyState society,
            EndogenousDocketState state,
            bool validateBoundary)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            var added = new List<DocketObservation>();

            for (int i = 0; i < world.EventLedger.Count; i++)
            {
                MaterialWorldEvent materialEvent = world.EventLedger[i];
                IncidentCandidate incident = IncidentForMaterialEvent(
                    state, materialEvent.EventId);
                if (incident == null) continue;
                for (int sourceIndex = 0;
                     sourceIndex < materialEvent.PotentialRecordSourceIds.Count;
                     sourceIndex++)
                {
                    string sourceRecordId =
                        materialEvent.PotentialRecordSourceIds[sourceIndex];
                    string observationId =
                        $"observation:record:{materialEvent.EventId}:{sourceRecordId}";
                    if (state.GetObservation(observationId) != null) continue;
                    DocketObservation observation = FromMaterialRecord(
                        incident, materialEvent, observationId, sourceRecordId);
                    state.Observations.Add(observation);
                    added.Add(observation);
                }
            }

            for (int i = 0; i < society.EventLedger.Count; i++)
            {
                SocietyEvent societyEvent = society.EventLedger[i];
                if (societyEvent.Visibility != EvidenceVisibility.OfficialRecord ||
                    string.IsNullOrWhiteSpace(societyEvent.EvidenceId))
                {
                    continue;
                }
                IncidentCandidate incident = IncidentForSocietyEvent(state, societyEvent);
                if (incident == null) continue;
                string observationId = $"observation:submission:{societyEvent.EventId}";
                if (state.GetObservation(observationId) != null) continue;
                var observation = new DocketObservation
                {
                    ObservationId = observationId,
                    RecordedTick = societyEvent.Tick,
                    ObservationKindId = "submitted-allegation",
                    IssueId = incident.ConflictKindId,
                    PropositionId = societyEvent.EvidencePropositionId,
                    SourceAgentId = societyEvent.ActorId,
                    AllegedSubjectAgentId = societyEvent.EvidenceSubjectId,
                    OfficialResourceId = societyEvent.EvidenceObjectId,
                    SourceRecordId = societyEvent.EvidenceId,
                    Reliability = societyEvent.EvidenceReliability > 0
                        ? societyEvent.EvidenceReliability
                        : 50,
                    ObservedMaterialHarm = ObservedHarm(incident.ConflictKindId),
                    OfficiallySubmitted = true,
                    AuthorityIncidentCandidateId = incident.CandidateId,
                    ParentCaseId = societyEvent.ParentCaseId,
                    OriginatingRulingId = societyEvent.EnablingRulingId,
                    CausalAgentActionId = societyEvent.CauseDecisionId,
                };
                state.Observations.Add(observation);
                added.Add(observation);
            }

            if (validateBoundary)
                EndogenousDocketValidator.Validate(state, society);
            return added;
        }

        private static DocketObservation FromMaterialRecord(
            IncidentCandidate incident,
            MaterialWorldEvent materialEvent,
            string observationId,
            string sourceRecordId)
        {
            string kind;
            string proposition;
            string subject;
            switch (materialEvent.Kind)
            {
                case MaterialWorldEventKind.PossessionTransferred:
                    kind = "recorded-possession-change";
                    proposition = "registered-asset-possession-changed";
                    subject = materialEvent.ActorAgentId;
                    break;
                case MaterialWorldEventKind.AccessChanged:
                    kind = "recorded-access-withdrawal";
                    proposition = "access-was-withdrawn";
                    subject = materialEvent.TargetAgentId;
                    break;
                case MaterialWorldEventKind.CollectiveCommitmentChanged:
                    kind = "recorded-collective-grievance";
                    proposition = "collective-action-was-registered";
                    subject = null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return new DocketObservation
            {
                ObservationId = observationId,
                RecordedTick = materialEvent.Tick,
                ObservationKindId = kind,
                IssueId = incident.ConflictKindId,
                PropositionId = proposition,
                AllegedSubjectAgentId = subject,
                OfficialResourceId = incident.SubjectResourceId,
                SourceRecordId = sourceRecordId,
                Reliability = DocumentaryReliability(sourceRecordId),
                ObservedMaterialHarm = ObservedHarm(incident.ConflictKindId),
                OfficiallySubmitted = true,
                AuthorityIncidentCandidateId = incident.CandidateId,
                ParentCaseId = incident.ParentCaseId,
                OriginatingRulingId = incident.OriginatingRulingId,
                CausalAgentActionId = incident.CausalAgentActionId,
            };
        }

        private static int DocumentaryReliability(string sourceRecordId)
        {
            if (string.IsNullOrWhiteSpace(sourceRecordId)) return 50;
            if (sourceRecordId.StartsWith(
                    "record.camera", StringComparison.Ordinal)) return 90;
            if (sourceRecordId.StartsWith(
                    "record.access-log", StringComparison.Ordinal)) return 72;
            if (sourceRecordId.StartsWith(
                    "record.damaged-sensor", StringComparison.Ordinal)) return 54;
            return 80;
        }

        private static IncidentCandidate IncidentForMaterialEvent(
            EndogenousDocketState state,
            string materialEventId)
        {
            for (int i = 0; i < state.IncidentCandidates.Count; i++)
            {
                IncidentCandidate incident = state.IncidentCandidates[i];
                if (Contains(incident.CauseEventIds, materialEventId)) return incident;
            }
            return null;
        }

        private static IncidentCandidate IncidentForSocietyEvent(
            EndogenousDocketState state,
            SocietyEvent societyEvent)
        {
            if (!string.IsNullOrWhiteSpace(societyEvent.RelatedEventId))
            {
                IncidentCandidate direct = IncidentForMaterialEvent(
                    state, societyEvent.RelatedEventId);
                if (direct != null) return direct;
            }
            if (!string.IsNullOrWhiteSpace(societyEvent.EvidenceObjectId))
            {
                IncidentCandidate latest = null;
                for (int i = 0; i < state.IncidentCandidates.Count; i++)
                {
                    IncidentCandidate candidate = state.IncidentCandidates[i];
                    if (candidate.DetectedTick <= societyEvent.Tick && string.Equals(
                            candidate.SubjectResourceId,
                            societyEvent.EvidenceObjectId,
                            StringComparison.Ordinal) &&
                        (latest == null || candidate.DetectedTick > latest.DetectedTick))
                    {
                        latest = candidate;
                    }
                }
                return latest;
            }
            return null;
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int ObservedHarm(string issueId)
        {
            switch (issueId)
            {
                case EndogenousIssueKindIds.PossessionDispute:
                    return 60;
                case EndogenousIssueKindIds.AccessWithdrawal:
                    return 75;
                case EndogenousIssueKindIds.CollectiveGrievance:
                    return 40;
                default:
                    return 0;
            }
        }
    }
}
