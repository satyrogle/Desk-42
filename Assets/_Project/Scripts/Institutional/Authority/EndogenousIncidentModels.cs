using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Assessor-only recognition that a causally relevant event happened. This type
    /// must never appear in player-facing reports or docket DTOs.
    /// </summary>
    [Serializable]
    internal sealed class IncidentCandidate
    {
        internal string CandidateId;
        internal List<string> CauseEventIds = new();
        internal List<string> AffectedAgentIds = new();
        internal string ConflictKindId;
        internal long DetectedTick;
        internal int UnresolvedMaterialHarm;
        internal string SubjectResourceId;
        internal string DedupeKey;
        internal string ParentCaseId;
        internal string OriginatingRulingId;
        internal string CausalAgentActionId;
    }

    [Serializable]
    internal sealed class EndogenousDocketState
    {
        internal const int CurrentSchemaVersion = 1;
        internal const string CurrentRulesetVersion = "endogenous-docket-v1";

        internal int SchemaVersion = CurrentSchemaVersion;
        internal string RulesetVersion = CurrentRulesetVersion;
        internal bool DirectorEnabled;
        internal List<IncidentCandidate> IncidentCandidates = new();
        internal List<DocketObservation> Observations = new();
        internal List<DocketCandidate> DocketCandidates = new();
        internal List<EndogenousInstitutionalCase> OpenCases = new();
        internal List<CommittedPlayerRuling> Rulings = new();
        internal List<EndogenousScopeApplicationTrace> ScopeApplicationTraces = new();

        internal IncidentCandidate GetIncident(string candidateId)
            => Find(IncidentCandidates, value => value.CandidateId, candidateId);

        internal DocketObservation GetObservation(string observationId)
            => Find(Observations, value => value.ObservationId, observationId);

        internal DocketCandidate GetDocketCandidate(string docketCandidateId)
            => Find(DocketCandidates, value => value.DocketCandidateId, docketCandidateId);

        internal EndogenousInstitutionalCase GetCase(string caseId)
            => Find(OpenCases, value => value.CaseId, caseId);

        private static T Find<T>(IReadOnlyList<T> values, Func<T, string> id, string expected)
            where T : class
        {
            if (string.IsNullOrEmpty(expected)) return null;
            for (int i = 0; i < values.Count; i++)
            {
                T value = values[i];
                if (value != null && string.Equals(id(value), expected, StringComparison.Ordinal))
                    return value;
            }
            return null;
        }
    }
}
