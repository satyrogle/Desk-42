using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    public enum InstitutionalPolicyKind
    {
        RecordsFirst,
        ProvisionalTrust,
        PrecedentMachine,
    }

    public enum EvidenceArtifactKind
    {
        ClaimantStatement,
        ClinicalRecord,
        PatternTestimony,
        WitnessRecord,
        ManagementRecord,
        ActionRecord,
    }

    public enum EvidenceEffect
    {
        Neutral,
        SupportsFinding,
        OpposesFinding,
    }

    public enum FindingDisposition
    {
        NotEstablished,
        ProvisionallyEstablished,
        Established,
    }

    public enum RulingDisposition
    {
        Denied,
        ProvisionallyRecognised,
        Recognised,
        Affirmed,
        ReversedAndDenied,
        ReversedAndRecognised,
    }

    public enum AppealDisposition
    {
        Pending,
        Affirmed,
        Reversed,
    }

    public enum DescendantCaseKind
    {
        Reliance,
        Retaliation,
        Appeal,
        RelatedClaim,
    }

    public enum DescendantCaseStatus
    {
        Open,
        Resolved,
        Denied,
        Recognised,
    }

    public enum PrecedentReach
    {
        Individual,
        Employer,
        Jurisdiction,
    }

    public enum MaterialConsequenceKind
    {
        ReliefPaid,
        RelianceSpent,
        WagesLost,
        BackpayAwarded,
    }

    public enum InstitutionalTimelineKind
    {
        Incident,
        EvidenceEntered,
        RulingIssued,
        StatusMutated,
        RelianceCreated,
        EmployerResponded,
        AppealFiled,
        AppealHeard,
        HoldingEstablished,
        PrecedentApplied,
        DescendantCaseOpened,
        ComparisonClosed,
    }

    public enum ObservedActivityKind
    {
        NoVisibleAction,
        WorkPerformed,
        AidRequested,
        AssistanceGiven,
        EvidenceSubmitted,
        AppealFiled,
    }

    /// <summary>
    /// Chain-of-custody information for an evidence artifact. It describes how the
    /// institution obtained a claim, not whether that claim is metaphysically true.
    /// </summary>
    [Serializable]
    public sealed class EvidenceProvenance
    {
        public string ProvenanceId;
        public long CreatedCycle;
        public string SourceAgentId;
        public string SourceDecisionId;
        public string SourceSocietyEventId;
        public string SourceRecordId;
        public EvidenceVisibility Visibility;
        public bool CreatedByAgentAction;
        public List<string> ChainOfCustodyIds = new();
    }

    [Serializable]
    public sealed class EvidenceArtifact
    {
        public string ArtifactId;
        public string CaseId;
        public long EnteredCycle;
        public EvidenceArtifactKind Kind;
        public string IssueId;
        public string PropositionId;
        public string OfficialEmployerId;
        public string OfficialIdentityConditionId;
        public string OfficialResourceId;
        public EvidenceEffect Effect;
        public int BaseWeight;
        public int Reliability;
        public bool OfficiallySubmitted;
        public string SuppressedByAgentId;
        public List<string> KnownByAgentIds = new();
        public bool EnteredAfterInitialRuling;
        public EvidenceProvenance Provenance = new();
    }

    [Serializable]
    public sealed class OfficialFinding
    {
        public string FindingId;
        public string CaseId;
        public long Cycle;
        public string IssueId;
        public FindingDisposition Disposition;
        public int WeightedEvidenceScore;
        public int PrecedentWeightApplied;
        public int RequiredScore;
        public List<string> EvidenceArtifactIds = new();
    }

    [Serializable]
    public sealed class OfficialStatusMutation
    {
        public string MutationId;
        public long Cycle;
        public string CauseId;
        public string AffectedAgentId;
        public string StatusId;
        public bool BeforeRecognised;
        public bool AfterRecognised;
        public int ResourceDelta;
    }

    [Serializable]
    public sealed class Ruling
    {
        public string RulingId;
        public string CaseId;
        public long Cycle;
        public string PolicyConfigurationId;
        public string PolicyVersion;
        public RulingDisposition Disposition;
        public string FindingId;
        public int ConfidenceMinimum;
        public int ConfidenceMaximum;
        public List<string> EvidenceArtifactIds = new();
        public List<string> AppliedPolicyIds = new();
        public List<string> SkippedProcedureIds = new();
        public List<string> OfficialStatusMutationIds = new();
        public List<string> CitedHoldingIds = new();
        public List<string> CitedScopeIds = new();
    }

    [Serializable]
    public sealed class DescendantCase
    {
        public string CaseId;
        public string ParentCaseId;
        public long OpenedCycle;
        public DescendantCaseKind Kind;
        public DescendantCaseStatus Status;
        public string ParentCauseId;
        public string OriginatingEventId;
        public string OriginatingRulingId;
        public string CausalAgentActionId;
        public string ClaimantAgentId;
        public string RespondentId;
        public string OfficialIssueId;
        public string OfficialIdentityConditionId;
        public string OfficialEmployerId;
        public List<string> ConnectedAgentIds = new();
        public List<string> SourceActionEventIds = new();
        public List<string> CitedHoldingIds = new();
    }

    [Serializable]
    public sealed class Appeal
    {
        public string AppealId;
        public string CaseId;
        public long FiledCycle;
        public long HearingCycle;
        public string AppellantAgentId;
        public string FilingActionEventId;
        public string ChallengedRulingId;
        public AppealDisposition Disposition;
        public string ResultingRulingId;
        public List<string> GroundsEvidenceArtifactIds = new();
    }

    [Serializable]
    public sealed class PrecedentScope
    {
        public string ScopeId;
        public PrecedentReach Reach;
        public string BoundAgentId;
        public string BoundEmployerId;
        public string IdentityConditionId;
        public bool Retrospective;

        public bool AppliesTo(string agentId, string employerId, string identityConditionId)
        {
            if (!string.Equals(IdentityConditionId, identityConditionId, StringComparison.Ordinal))
                return false;

            switch (Reach)
            {
                case PrecedentReach.Individual:
                    return string.Equals(BoundAgentId, agentId, StringComparison.Ordinal);
                case PrecedentReach.Employer:
                    return string.Equals(BoundEmployerId, employerId, StringComparison.Ordinal);
                case PrecedentReach.Jurisdiction:
                    return true;
                default:
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class Holding
    {
        public string HoldingId;
        public long EstablishedCycle;
        public string SourceAppealId;
        public string SourceRulingId;
        public string RuleId;
        public string IssueId;
        public List<string> SupportingEvidenceArtifactIds = new();
        public PrecedentScope Scope = new();
        public List<string> AppliedCaseIds = new();
    }

    [Serializable]
    public sealed class ObservedAgentAction
    {
        public long Cycle;
        public string ActionEventId;
        public string ActorId;
        public ObservedActivityKind Activity;
        public string TargetId;
        public List<string> ResultEvidenceArtifactIds = new();
        public List<string> ResultDescendantCaseIds = new();
    }

    [Serializable]
    public sealed class MaterialConsequence
    {
        public string ConsequenceId;
        public long Cycle;
        public string CauseId;
        public string AgentId;
        public MaterialConsequenceKind Kind;
        public int ResourceDelta;
    }

    /// <summary>
    /// The officially knowable portion of reliance. Private alternatives, motives,
    /// and the assessor's survived-reversal conclusion remain in Authority.
    /// </summary>
    [Serializable]
    public sealed class RelianceObservation
    {
        public string ObservationId;
        public long Cycle;
        public string AgentId;
        public string EnablingRulingId;
        public string EnablingMutationId;
        public string SourceActionEventId;
        public string RecordedChoiceId;
        public int RecordedResourceDelta;
    }

    [Serializable]
    public sealed class ConnectedOutcomePair
    {
        public string PairId;
        public string CauseRuleId;
        public string ConnectionId;
        public string WinnerAgentId;
        public string WinnerDisplayName;
        public int WinnerResourceDelta;
        public string LoserAgentId;
        public string LoserDisplayName;
        public int LoserResourceDelta;
    }

    [Serializable]
    public sealed class WorkAllocationObservation
    {
        public string AllocationId;
        public string EmployerId;
        public string OriginalWorkerId;
        public string PaidHolderAgentId;
        public string IdentityConditionId;
        public int CommittedWage;
        public string LastMutationCauseId;
    }

    [Serializable]
    public sealed class InstitutionalTimelineEntry
    {
        public string EntryId;
        public long Cycle;
        public InstitutionalTimelineKind Kind;
        public string CauseId;
        public string SubjectId;
        public string DetailId;
    }

    /// <summary>
    /// The complete player/debug-inspector surface for the proof. It deliberately has
    /// no lived-event field and no authoritative eligibility/cause flag.
    /// </summary>
    [Serializable]
    public sealed class InstitutionalConsequenceReport
    {
        public const string RulesetVersion = "institutional-consequence-loop-v0.1";

        public string Ruleset = RulesetVersion;
        public int MasterSeed;
        public string PolicyConfigurationId;
        public string PrimaryCaseId;
        public long FinalCycle;
        public List<ObservedAgentAction> ObservedAgentActions = new();
        public List<EvidenceArtifact> EvidenceArtifacts = new();
        public List<OfficialFinding> OfficialFindings = new();
        public List<Ruling> Rulings = new();
        public List<OfficialStatusMutation> OfficialStatusMutations = new();
        public List<DescendantCase> DescendantCases = new();
        public List<Appeal> Appeals = new();
        public List<Holding> Holdings = new();
        public List<RelianceObservation> RelianceObservations = new();
        public List<MaterialConsequence> MaterialConsequences = new();
        public List<ConnectedOutcomePair> ConnectedOutcomes = new();
        public List<WorkAllocationObservation> WorkAllocations = new();
        public List<InstitutionalTimelineEntry> Timeline = new();
    }

}
