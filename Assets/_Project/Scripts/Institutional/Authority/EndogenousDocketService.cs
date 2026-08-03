using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Desk42.Institutional
{
    /// <summary>
    /// Composes public-safe docket candidates from observations and admits cases by a
    /// fixed deterministic rule. There is deliberately no Director dependency.
    /// </summary>
    internal static class EndogenousDocketService
    {
        internal static List<DocketCandidate> Compose(
            SocietyState society,
            EndogenousDocketState state)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            EndogenousDocketValidator.Validate(state, society);
            var added = new List<DocketCandidate>();

            var incidentIds = new List<string>();
            for (int i = 0; i < state.Observations.Count; i++)
                AddUnique(incidentIds, state.Observations[i].AuthorityIncidentCandidateId);
            incidentIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < incidentIds.Count; i++)
            {
                string incidentId = incidentIds[i];
                string docketId = $"docket:{incidentId}";
                if (state.GetDocketCandidate(docketId) != null) continue;
                List<DocketObservation> observations = ObservationsForIncident(state, incidentId);
                if (observations.Count == 0) continue;

                var candidate = new DocketCandidate
                {
                    DocketCandidateId = docketId,
                    EligibilityRuleId = EligibilityRule(observations[0].IssueId),
                    IssueId = observations[0].IssueId,
                    EligibleTick = observations[0].RecordedTick,
                    UnresolvedMaterialHarm = observations[0].ObservedMaterialHarm,
                    AuthorityIncidentCandidateId = incidentId,
                    ParentCaseId = observations[0].ParentCaseId,
                    OriginatingRulingId = observations[0].OriginatingRulingId,
                    CausalAgentActionId = observations[0].CausalAgentActionId,
                };
                for (int observationIndex = 0;
                     observationIndex < observations.Count;
                     observationIndex++)
                {
                    DocketObservation observation = observations[observationIndex];
                    candidate.EligibleTick = Math.Min(
                        candidate.EligibleTick, observation.RecordedTick);
                    candidate.UnresolvedMaterialHarm = Math.Max(
                        candidate.UnresolvedMaterialHarm,
                        observation.ObservedMaterialHarm);
                    AddUnique(candidate.ObservableEvidenceIds, observation.ObservationId);
                    if (!string.IsNullOrWhiteSpace(observation.SourceAgentId))
                        AddUnique(candidate.AllegingAgentIds, observation.SourceAgentId);
                    if (!string.IsNullOrWhiteSpace(observation.SourceAgentId))
                        AddUnique(candidate.PotentialPartyIds, observation.SourceAgentId);
                    if (!string.IsNullOrWhiteSpace(observation.AllegedSubjectAgentId))
                        AddUnique(
                            candidate.PotentialPartyIds,
                            observation.AllegedSubjectAgentId);
                }
                candidate.ObservableEvidenceIds.Sort(StringComparer.Ordinal);
                candidate.AllegingAgentIds.Sort(StringComparer.Ordinal);
                candidate.PotentialPartyIds.Sort(StringComparer.Ordinal);
                state.DocketCandidates.Add(candidate);
                added.Add(candidate);
            }

            EndogenousDocketValidator.Validate(state, society);
            return added;
        }

        internal static EndogenousInstitutionalCase AdmitNext(
            SocietyState society,
            EndogenousDocketState state)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            EndogenousDocketValidator.Validate(state, society);
            if (state.DirectorEnabled)
                throw new InvalidOperationException(
                    "The endogenous proof path requires the Director to remain disabled.");

            DocketCandidate selected = null;
            for (int i = 0; i < state.DocketCandidates.Count; i++)
            {
                DocketCandidate candidate = state.DocketCandidates[i];
                if (candidate.Admitted) continue;
                if (selected == null || CompareAdmission(candidate, selected) < 0)
                    selected = candidate;
            }
            if (selected == null) return null;

            string caseId = $"case:{selected.DocketCandidateId}";
            EndogenousInstitutionalCase replay = state.GetCase(caseId);
            if (replay != null)
            {
                selected.Admitted = true;
                selected.AdmittedCaseId = replay.CaseId;
                return replay;
            }

            var opened = new EndogenousInstitutionalCase
            {
                CaseId = caseId,
                DocketCandidateId = selected.DocketCandidateId,
                IssueId = selected.IssueId,
                OpenedTick = Math.Max(society.CurrentTick, selected.EligibleTick),
                EvidenceEnvelopeHash = EvidenceEnvelopeHash(state, selected),
                PartyIds = new List<string>(selected.PotentialPartyIds),
                ObservationIds = new List<string>(selected.ObservableEvidenceIds),
                ParentCaseId = selected.ParentCaseId,
                OriginatingRulingId = selected.OriginatingRulingId,
                CausalAgentActionId = selected.CausalAgentActionId,
            };
            opened.AvailableFactIds.Add($"fact:issue:{selected.IssueId}");
            for (int i = 0; i < selected.ObservableEvidenceIds.Count; i++)
                opened.AvailableFactIds.Add(
                    $"fact:observation:{selected.ObservableEvidenceIds[i]}");
            opened.AvailableFactIds.Sort(StringComparer.Ordinal);
            selected.Admitted = true;
            selected.AdmittedCaseId = opened.CaseId;
            state.OpenCases.Add(opened);
            EndogenousDocketValidator.Validate(state, society);
            return opened;
        }

        private static int CompareAdmission(DocketCandidate left, DocketCandidate right)
        {
            int eligible = left.EligibleTick.CompareTo(right.EligibleTick);
            if (eligible != 0) return eligible;
            int harm = right.UnresolvedMaterialHarm.CompareTo(left.UnresolvedMaterialHarm);
            return harm != 0
                ? harm
                : string.CompareOrdinal(left.DocketCandidateId, right.DocketCandidateId);
        }

        internal static string EvidenceEnvelopeHash(
            EndogenousDocketState state,
            DocketCandidate candidate)
        {
            var canonical = new StringBuilder();
            canonical.Append(candidate.DocketCandidateId).Append('|')
                .Append(candidate.IssueId).Append('|')
                .Append(candidate.EligibleTick).Append('|');
            var evidenceIds = new List<string>(candidate.ObservableEvidenceIds);
            evidenceIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < evidenceIds.Count; i++)
            {
                DocketObservation observation = state.GetObservation(evidenceIds[i]) ??
                    throw new InvalidOperationException(
                        $"Missing docket observation {evidenceIds[i]}.");
                canonical.Append(observation.ObservationId).Append(':')
                    .Append(observation.RecordedTick).Append(':')
                    .Append(observation.IssueId).Append(':')
                    .Append(observation.PropositionId).Append(':')
                    .Append(observation.SourceRecordId).Append(':')
                    .Append(observation.Reliability).Append(';');
            }

            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] bytes = algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(canonical.ToString()));
                var result = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                    result.Append(bytes[i].ToString("x2"));
                return result.ToString();
            }
        }

        private static List<DocketObservation> ObservationsForIncident(
            EndogenousDocketState state,
            string incidentId)
        {
            var result = new List<DocketObservation>();
            for (int i = 0; i < state.Observations.Count; i++)
            {
                DocketObservation observation = state.Observations[i];
                if (string.Equals(
                        observation.AuthorityIncidentCandidateId,
                        incidentId,
                        StringComparison.Ordinal))
                {
                    result.Add(observation);
                }
            }
            result.Sort((left, right) =>
                string.CompareOrdinal(left.ObservationId, right.ObservationId));
            return result;
        }

        private static string EligibilityRule(string conflictKindId)
        {
            switch (conflictKindId)
            {
                case EndogenousIssueKindIds.PossessionDispute:
                    return "observable-possession-conflict-v1";
                case EndogenousIssueKindIds.AccessWithdrawal:
                    return "observable-access-withdrawal-v1";
                case EndogenousIssueKindIds.CollectiveGrievance:
                    return "observable-collective-grievance-v1";
                default:
                    throw new InvalidOperationException(
                        $"No docket grammar exists for conflict kind {conflictKindId}.");
            }
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return;
            values.Add(value);
        }
    }
}
