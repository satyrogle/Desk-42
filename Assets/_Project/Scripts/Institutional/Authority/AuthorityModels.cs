using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Assessor-only causal state. This assembly is not auto-referenced, so gameplay
    /// code receives only InstitutionalConsequenceReport and cannot query lived truth.
    /// </summary>
    [Serializable]
    internal sealed class LivedEvent
    {
        internal string LivedEventId;
        internal long Cycle;
        internal string EventKindId;
        internal string SubjectAgentId;
        internal string CauseEntityId;
        internal NeedKind AffectedNeed;
        internal int NeedPressureDelta;
    }

    internal sealed class AuthoritativeEvidenceLink
    {
        internal string LivedEventId;
        internal string EvidenceArtifactId;
        internal string ObservationKindId;
    }

    internal sealed class AuthoritativeBeliefLink
    {
        internal string LivedEventId;
        internal string AgentId;
        internal string BeliefId;
    }

    [Serializable]
    internal sealed class AgentActionTrace
    {
        internal long Cycle;
        internal string DecisionId;
        internal string CandidateId;
        internal string ActorId;
        internal SocietyActionKind Action;
        internal string TargetId;
        internal string OpportunityId;
        internal string SubjectBeliefId;
        internal int UtilityScore;
        internal int SelectedCandidateRank;
        internal List<DecisionReason> Reasons = new();
        internal List<CandidateEvaluation> CandidateEvaluations = new();
        internal List<CapacityReservationTrace> CapacityReservations = new();
        internal List<string> ResultEventIds = new();
        internal AgentPerception PerceptionSnapshot;
        internal InstitutionalRegimeState RegimeSnapshot;
        internal SimulationInput InputSnapshot;
    }

    [Serializable]
    internal sealed class RelianceEvent
    {
        internal string RelianceEventId;
        internal long Cycle;
        internal string AgentId;
        internal string BeneficiaryAgentId;
        internal string ReliedOnRulingId;
        internal string ReliedOnMutationId;
        internal string SourceActionEventId;
        internal string ChoiceId;
        internal string AbandonedAlternativeId;
        internal int ResourceSpent;
        internal bool SurvivedReversal;
        internal int HealthPressureAfterAction;
        internal bool AlternativeAvailableBefore;
        internal bool AlternativeAvailableAfter;
        internal int CreditsBefore;
        internal int CreditsAfter;
        internal int AgentSubsistenceBefore;
        internal int AgentSubsistenceAfter;
        internal string HouseholdAgentId;
        internal int HouseholdSubsistenceBefore;
        internal int HouseholdSubsistenceAfter;
        internal List<RelianceAppliedEffect> AppliedEffects = new();
    }

    [Serializable]
    internal sealed class RelianceAppliedEffect
    {
        internal string EffectId;
        internal string AgentId;
        internal int ResourceBefore;
        internal int ResourceAfter;
        internal bool HasNeedEffect;
        internal NeedKind Need;
        internal int NeedPressureBefore;
        internal int NeedPressureAfter;
        internal string MaterialConsequenceId;
    }

    internal sealed class EconomicAccountState
    {
        internal string AgentId;
        internal int AvailableCredits;
        internal int CommittedIncome;
    }

    internal sealed class AlternativeOptionState
    {
        internal string OptionId;
        internal string AgentId;
        internal bool Available;
        internal string ChangedByActionEventId;
    }

    internal sealed class WorkAllocationState
    {
        internal string AllocationId;
        internal string EmployerId;
        internal string OriginalWorkerId;
        internal string PaidHolderAgentId;
        internal string IdentityConditionId;
        internal int CommittedWage;
        internal string SourceRulingId;
        internal string LastMutationCauseId;
    }

    internal sealed class CaseOpportunityState
    {
        internal AppealOpportunity Opportunity;
        internal DescendantCaseKind DescendantKind;
        internal string ParentCaseId;
        internal string IssueId;
        internal string EmployerId;
        internal string IdentityConditionId;
        internal string ResourceId;
        internal string OriginatingActionEventId;
        internal long EarliestFilingCycle;
        internal bool Filed;
        internal string FiledAppealId;
    }

    internal sealed class InstitutionalIncidentRoles
    {
        internal string PrimaryClaimantId;
        internal string PrimaryWitnessId;
        internal string EmployerRepresentativeId;
        internal string HouseholdMemberId;
        internal string WorkerRepresentativeId;
        internal string ClinicalAssessorId;
        internal string LaterClaimantId;
        internal string ContingentWorkerId;
        internal string EmployerId;
    }

    internal sealed class InstitutionalConsequenceRun
    {
        internal InstitutionalConsequenceReport Report;
        internal List<LivedEvent> AuthoritativeEvents = new();
        internal List<AuthoritativeEvidenceLink> AuthoritativeEvidenceLinks = new();
        internal List<AuthoritativeBeliefLink> AuthoritativeBeliefLinks = new();
        internal List<AgentActionTrace> AssessorActionTraces = new();
        internal List<RelianceEvent> RelianceLedger = new();
        internal List<EconomicAccountState> EconomicAccounts = new();
        internal List<AlternativeOptionState> AlternativeOptions = new();
        internal List<WorkAllocationState> WorkAllocations = new();
        internal SocietyState FinalSocietyState;
    }
}
