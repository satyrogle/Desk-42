using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal static class EndogenousDocketValidator
    {
        internal static void Validate(EndogenousDocketState state, SocietyState society)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (society == null) throw new ArgumentNullException(nameof(society));
            SocietyStateValidator.Validate(society);
            if (state.SchemaVersion != EndogenousDocketState.CurrentSchemaVersion ||
                !string.Equals(
                    state.RulesetVersion,
                    EndogenousDocketState.CurrentRulesetVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unsupported endogenous docket state.");
            }
            if (state.DirectorEnabled)
                throw new InvalidOperationException(
                    "The endogenous proof state must keep the Director disabled.");
            if (state.IncidentCandidates == null || state.Observations == null ||
                state.DocketCandidates == null || state.OpenCases == null ||
                state.Rulings == null)
            {
                throw new InvalidOperationException(
                    "Endogenous docket state requires every committed collection.");
            }

            HashSet<string> agentIds = AgentIds(society);
            HashSet<string> incidentIds = ValidateIncidents(state, agentIds);
            HashSet<string> observationIds = ValidateObservations(
                state, agentIds, incidentIds);
            HashSet<string> docketIds = ValidateDockets(
                state, incidentIds, observationIds);
            HashSet<string> caseIds = ValidateCases(
                state, agentIds, docketIds, observationIds);
            ValidateRulings(state, caseIds);
            ValidateLineage(state);
        }

        private static HashSet<string> AgentIds(SocietyState society)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < society.Agents.Count; i++)
                result.Add(society.Agents[i].StableId);
            return result;
        }

        private static HashSet<string> ValidateIncidents(
            EndogenousDocketState state,
            HashSet<string> agentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var dedupe = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.IncidentCandidates.Count; i++)
            {
                IncidentCandidate incident = state.IncidentCandidates[i];
                if (incident == null || !Stable(incident.CandidateId) ||
                    !ids.Add(incident.CandidateId) || !Stable(incident.DedupeKey) ||
                    !dedupe.Add(incident.DedupeKey) || !Stable(incident.ConflictKindId) ||
                    incident.DetectedTick < 0 || incident.UnresolvedMaterialHarm < 0 ||
                    incident.CauseEventIds == null || incident.CauseEventIds.Count == 0 ||
                    incident.AffectedAgentIds == null)
                {
                    throw new InvalidOperationException(
                        "Every incident candidate requires a unique causal envelope.");
                }
                UniqueStable(incident.CauseEventIds, $"{incident.CandidateId}.cause");
                UniqueKnown(
                    incident.AffectedAgentIds,
                    agentIds,
                    $"{incident.CandidateId}.affected-agent");
            }
            return ids;
        }

        private static HashSet<string> ValidateObservations(
            EndogenousDocketState state,
            HashSet<string> agentIds,
            HashSet<string> incidentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.Observations.Count; i++)
            {
                DocketObservation observation = state.Observations[i];
                if (observation == null || !Stable(observation.ObservationId) ||
                    !ids.Add(observation.ObservationId) || observation.RecordedTick < 0 ||
                    !Stable(observation.ObservationKindId) ||
                    !Stable(observation.IssueId) ||
                    !Stable(observation.PropositionId) ||
                    !Stable(observation.SourceRecordId) ||
                    observation.Reliability < 0 || observation.Reliability > 100 ||
                    observation.ObservedMaterialHarm < 0 ||
                    !observation.OfficiallySubmitted ||
                    !incidentIds.Contains(observation.AuthorityIncidentCandidateId))
                {
                    throw new InvalidOperationException(
                        "Every docket observation requires submitted public-safe provenance.");
                }
                KnownOptional(observation.SourceAgentId, agentIds, observation.ObservationId);
                KnownOptional(
                    observation.AllegedSubjectAgentId,
                    agentIds,
                    observation.ObservationId);
            }
            return ids;
        }

        private static HashSet<string> ValidateDockets(
            EndogenousDocketState state,
            HashSet<string> incidentIds,
            HashSet<string> observationIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.DocketCandidates.Count; i++)
            {
                DocketCandidate docket = state.DocketCandidates[i];
                if (docket == null || !Stable(docket.DocketCandidateId) ||
                    !ids.Add(docket.DocketCandidateId) ||
                    !Stable(docket.EligibilityRuleId) || !Stable(docket.IssueId) ||
                    docket.EligibleTick < 0 || docket.UnresolvedMaterialHarm < 0 ||
                    docket.ObservableEvidenceIds == null ||
                    docket.ObservableEvidenceIds.Count == 0 ||
                    docket.AllegingAgentIds == null || docket.PotentialPartyIds == null ||
                    !incidentIds.Contains(docket.AuthorityIncidentCandidateId) ||
                    docket.Admitted != !string.IsNullOrWhiteSpace(docket.AdmittedCaseId))
                {
                    throw new InvalidOperationException(
                        "Every docket candidate requires observable evidence and coherent admission state.");
                }
                UniqueKnown(
                    docket.ObservableEvidenceIds,
                    observationIds,
                    $"{docket.DocketCandidateId}.evidence");
                UniqueStable(
                    docket.AllegingAgentIds,
                    $"{docket.DocketCandidateId}.alleging-agent");
                UniqueStable(
                    docket.PotentialPartyIds,
                    $"{docket.DocketCandidateId}.potential-party");
                for (int evidenceIndex = 0;
                     evidenceIndex < docket.ObservableEvidenceIds.Count;
                     evidenceIndex++)
                {
                    DocketObservation observation = state.GetObservation(
                        docket.ObservableEvidenceIds[evidenceIndex]);
                    if (!string.Equals(
                            observation.AuthorityIncidentCandidateId,
                            docket.AuthorityIncidentCandidateId,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            observation.IssueId,
                            docket.IssueId,
                            StringComparison.Ordinal) ||
                        observation.ObservedMaterialHarm > docket.UnresolvedMaterialHarm)
                    {
                        throw new InvalidOperationException(
                            "A docket candidate mixed evidence or harm from another issue.");
                    }
                }
            }
            return ids;
        }

        private static HashSet<string> ValidateCases(
            EndogenousDocketState state,
            HashSet<string> agentIds,
            HashSet<string> docketIds,
            HashSet<string> observationIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.OpenCases.Count; i++)
            {
                EndogenousInstitutionalCase opened = state.OpenCases[i];
                if (opened == null || !Stable(opened.CaseId) || !ids.Add(opened.CaseId) ||
                    !docketIds.Contains(opened.DocketCandidateId) ||
                    !Stable(opened.IssueId) || opened.OpenedTick < 0 ||
                    opened.CaseVersion < 1 || !Stable(opened.EvidenceEnvelopeHash) ||
                    opened.PartyIds == null || opened.ObservationIds == null ||
                    opened.AvailableFactIds == null || opened.AvailableFactIds.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Every endogenous case requires a unique admitted docket source.");
                }
                DocketCandidate docket = state.GetDocketCandidate(opened.DocketCandidateId);
                if (!docket.Admitted || !string.Equals(
                        docket.AdmittedCaseId, opened.CaseId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Endogenous case and docket admission disagree.");
                }
                UniqueKnown(opened.PartyIds, agentIds, $"{opened.CaseId}.party");
                UniqueKnown(
                    opened.ObservationIds,
                    observationIds,
                    $"{opened.CaseId}.observation");
                UniqueStable(opened.AvailableFactIds, $"{opened.CaseId}.available-fact");
            }
            return ids;
        }

        private static void ValidateRulings(
            EndogenousDocketState state,
            HashSet<string> caseIds)
        {
            var rulingIds = new HashSet<string>(StringComparer.Ordinal);
            var commandIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.Rulings.Count; i++)
            {
                CommittedPlayerRuling ruling = state.Rulings[i];
                if (ruling == null || !Stable(ruling.RulingId) ||
                    !rulingIds.Add(ruling.RulingId) || !Stable(ruling.PlayerCommandId) ||
                    !commandIds.Add(ruling.PlayerCommandId) ||
                    !caseIds.Contains(ruling.CaseId) || ruling.CaseVersion < 1 ||
                    ruling.CommittedTick < 0 || !Stable(ruling.EvidenceEnvelopeHash) ||
                    ruling.RecognisedFactIds == null ||
                    ruling.CitedEvidenceArtifactIds == null ||
                    !Stable(ruling.HoldingRuleId) || ruling.RemedyDefinitionIds == null ||
                    !Enum.IsDefined(typeof(RulingDisposition), ruling.Disposition) ||
                    !Enum.IsDefined(typeof(TemporalReach), ruling.TemporalReach) ||
                    ruling.TemporalReach != TemporalReach.Prospective ||
                    !string.Equals(
                        ruling.RulesetVersion,
                        EndogenousPlayerRulingService.CurrentRulesetVersion,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Every committed player ruling requires a frozen unique command payload.");
                }
                EndogenousInstitutionalCase opened = state.GetCase(ruling.CaseId);
                if (opened.CaseVersion != ruling.CaseVersion || !string.Equals(
                        opened.EvidenceEnvelopeHash,
                        ruling.EvidenceEnvelopeHash,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Ruling {ruling.RulingId} does not match its frozen case envelope.");
                }
                UniqueSubset(
                    ruling.RecognisedFactIds,
                    opened.AvailableFactIds,
                    $"{ruling.RulingId}.recognised-fact");
                UniqueSubset(
                    ruling.CitedEvidenceArtifactIds,
                    opened.ObservationIds,
                    $"{ruling.RulingId}.cited-evidence");
                UniqueStable(
                    ruling.RemedyDefinitionIds,
                    $"{ruling.RulingId}.remedy");
                ScopeExpressionEvaluator.Validate(ruling.Scope);
            }
        }

        private static void ValidateLineage(EndogenousDocketState state)
        {
            for (int i = 0; i < state.Observations.Count; i++)
                ValidateLineageTuple(
                    state,
                    state.Observations[i].ParentCaseId,
                    state.Observations[i].OriginatingRulingId,
                    state.Observations[i].CausalAgentActionId,
                    state.Observations[i].ObservationId);
            for (int i = 0; i < state.DocketCandidates.Count; i++)
                ValidateLineageTuple(
                    state,
                    state.DocketCandidates[i].ParentCaseId,
                    state.DocketCandidates[i].OriginatingRulingId,
                    state.DocketCandidates[i].CausalAgentActionId,
                    state.DocketCandidates[i].DocketCandidateId);
            for (int i = 0; i < state.OpenCases.Count; i++)
                ValidateLineageTuple(
                    state,
                    state.OpenCases[i].ParentCaseId,
                    state.OpenCases[i].OriginatingRulingId,
                    state.OpenCases[i].CausalAgentActionId,
                    state.OpenCases[i].CaseId);
        }

        private static void ValidateLineageTuple(
            EndogenousDocketState state,
            string parentCaseId,
            string rulingId,
            string actionId,
            string ownerId)
        {
            bool noPrecedentLineage = parentCaseId == null && rulingId == null;
            if (noPrecedentLineage)
            {
                if (actionId != null && !Stable(actionId))
                    throw new InvalidOperationException(
                        $"{ownerId} has an invalid primary causal action id.");
                return;
            }
            if (!Stable(parentCaseId) || !Stable(rulingId) || !Stable(actionId) ||
                state.GetCase(parentCaseId) == null || FindRuling(state, rulingId) == null)
            {
                throw new InvalidOperationException(
                    $"{ownerId} has incomplete or unavailable descendant-case lineage.");
            }
        }

        private static CommittedPlayerRuling FindRuling(
            EndogenousDocketState state,
            string rulingId)
        {
            for (int i = 0; i < state.Rulings.Count; i++)
                if (string.Equals(state.Rulings[i].RulingId, rulingId, StringComparison.Ordinal))
                    return state.Rulings[i];
            return null;
        }

        private static void UniqueSubset(
            IReadOnlyList<string> values,
            IReadOnlyList<string> allowed,
            string field)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                if (!Stable(values[i]) || !allowedSet.Contains(values[i]) ||
                    !seen.Add(values[i]))
                {
                    throw new InvalidOperationException($"{field} contains an invalid id.");
                }
            }
        }

        private static void KnownOptional(
            string value,
            HashSet<string> known,
            string owner)
        {
            if (value != null && (!Stable(value) || !known.Contains(value)))
                throw new InvalidOperationException($"{owner} references an unknown agent.");
        }

        private static void UniqueKnown(
            IReadOnlyList<string> values,
            HashSet<string> known,
            string field)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                if (!Stable(values[i]) || !known.Contains(values[i]) || !seen.Add(values[i]))
                    throw new InvalidOperationException($"{field} contains an invalid id.");
            }
        }

        private static void UniqueStable(IReadOnlyList<string> values, string field)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                if (!Stable(values[i]) || !seen.Add(values[i]))
                    throw new InvalidOperationException($"{field} contains an invalid id.");
            }
        }

        private static bool Stable(string value) => !string.IsNullOrWhiteSpace(value);
    }
}
