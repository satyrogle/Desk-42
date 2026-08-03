using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    public static class EndogenousIssueKindIds
    {
        public const string PossessionDispute = "possession-dispute";
        public const string AccessWithdrawal = "access-withdrawal";
        public const string CollectiveGrievance = "collective-grievance";
    }

    /// <summary>
    /// Player-safe institutional material. It records what was submitted or captured,
    /// never whether the proposition is authoritative lived truth.
    /// </summary>
    [Serializable]
    public sealed class DocketObservation
    {
        public string ObservationId;
        public long RecordedTick;
        public string ObservationKindId;
        public string IssueId;
        public string PropositionId;
        public string SourceAgentId;
        public string AllegedSubjectAgentId;
        public string OfficialResourceId;
        public string SourceRecordId;
        public int Reliability;
        public int ObservedMaterialHarm;
        public bool OfficiallySubmitted;

        internal string AuthorityIncidentCandidateId;
    }

    /// <summary>
    /// Public-safe candidate for deterministic case admission. It contains only
    /// institutionally observable evidence and allegations.
    /// </summary>
    [Serializable]
    public sealed class DocketCandidate
    {
        public string DocketCandidateId;
        public string EligibilityRuleId;
        public string IssueId;
        public long EligibleTick;
        public int UnresolvedMaterialHarm;
        public List<string> ObservableEvidenceIds = new();
        public List<string> AllegingAgentIds = new();
        public List<string> PotentialPartyIds = new();
        public bool Admitted;
        public string AdmittedCaseId;

        internal string AuthorityIncidentCandidateId;
    }

    [Serializable]
    public sealed class EndogenousInstitutionalCase
    {
        public string CaseId;
        public string DocketCandidateId;
        public string IssueId;
        public long OpenedTick;
        public List<string> PartyIds = new();
        public List<string> ObservationIds = new();
    }
}
