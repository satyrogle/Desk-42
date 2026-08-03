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
        internal const int CurrentSchemaVersion = 4;
        internal const string CurrentRulesetVersion = "endogenous-docket-v4";

        internal int SchemaVersion = CurrentSchemaVersion;
        internal string RulesetVersion = CurrentRulesetVersion;
        internal bool DirectorEnabled;
        internal List<IncidentCandidate> IncidentCandidates = new();
        internal List<DocketObservation> Observations = new();
        internal List<DocketCandidate> DocketCandidates = new();
        internal List<EndogenousInstitutionalCase> OpenCases = new();
        internal List<CommittedPlayerRuling> Rulings = new();
        internal List<EndogenousRemedyApplicationTrace> RemedyApplicationTraces = new();
        internal List<EndogenousAccessRemedyApplicationTrace>
            AccessRemedyApplicationTraces = new();
        internal List<EndogenousCollectiveRemedyApplicationTrace>
            CollectiveRemedyApplicationTraces = new();
        internal List<EndogenousScopeApplicationTrace> ScopeApplicationTraces = new();
        internal List<EndogenousAppealRecord> Appeals = new();
        internal List<EndogenousHoldingRecord> Holdings = new();

        internal IncidentCandidate GetIncident(string candidateId)
            => Find(IncidentCandidates, value => value.CandidateId, candidateId);

        internal DocketObservation GetObservation(string observationId)
            => Find(Observations, value => value.ObservationId, observationId);

        internal DocketCandidate GetDocketCandidate(string docketCandidateId)
            => Find(DocketCandidates, value => value.DocketCandidateId, docketCandidateId);

        internal EndogenousInstitutionalCase GetCase(string caseId)
            => Find(OpenCases, value => value.CaseId, caseId);

        internal EndogenousAppealRecord GetAppeal(string appealId)
            => Find(Appeals, value => value.AppealId, appealId);

        internal EndogenousHoldingRecord GetHolding(string holdingId)
            => Find(Holdings, value => value.HoldingId, holdingId);

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

    [Serializable]
    internal sealed class EndogenousAccessRemedyApplicationTrace
    {
        internal string TraceId;
        internal string RulingId;
        internal string CaseId;
        internal long AppliedTick;
        internal string AccessGrantId;
        internal string BeneficiaryAgentId;
        internal bool StateBefore;
        internal bool StateAfter;
        internal string MaterialEventId;
        internal bool MaterialStateChanged;
    }

    [Serializable]
    internal sealed class EndogenousCollectiveRemedyApplicationTrace
    {
        internal string TraceId;
        internal string RulingId;
        internal string CaseId;
        internal long AppliedTick;
        internal string CollectiveCommitmentId;
        internal string RecognisedStatusId;
        internal List<string> MemberAgentIds = new();
        internal List<string> ChangedAgentIds = new();
    }

    [Serializable]
    internal sealed class EndogenousAppealRecord
    {
        internal string AppealId;
        internal string CaseId;
        internal string ChallengedRulingId;
        internal long FiledTick;
        internal string ProcedureId;
        internal List<string> GroundsEvidenceIds = new();
        internal bool Resolved;
        internal long ResolvedTick = -1;
        internal string ResultingRulingId;
        internal string ResultingHoldingId;
    }

    [Serializable]
    internal sealed class EndogenousHoldingRecord
    {
        internal string HoldingId;
        internal string SourceAppealId;
        internal string SourceRulingId;
        internal string RuleId;
        internal string IssueId;
        internal long EstablishedTick;
        internal ScopeExpression Scope;
        internal List<string> SupportingEvidenceIds = new();
        internal List<string> AppliedCaseIds = new();
    }
}
