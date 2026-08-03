using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    public static class InstitutionalStatusIds
    {
        public const string AdverseDecision = "adverse-decision";
        public const string AppealPending = "appeal-pending";
    }

    public enum NeedKind
    {
        Health,
        Subsistence,
        Safety,
        Belonging,
        Autonomy,
    }

    public enum SocietyActionKind
    {
        Idle,
        Work,
        SeekAid,
        Help,
        Disclose,
        Withhold,
        Appeal,
        Lie,
        Steal,
        Retaliate,
        Organise,
    }

    public enum SocietyEventKind
    {
        NoActionObserved,
        WorkPerformed,
        AidRequested,
        AssistanceGiven,
        EvidenceDisclosed,
        ResponseWithheld,
        AppealFiled,
        AnomalyStatusResponse,
        AssertionMade,
        PossessionTransferRequested,
        RetaliatoryAuthorityExercised,
        OrganisationProposed,
    }

    public enum EvidenceVisibility
    {
        Private,
        Observable,
        OfficialRecord,
    }

    [Serializable]
    public sealed class NeedState
    {
        public NeedKind Kind;
        public int Pressure;
    }

    [Serializable]
    public sealed class CommitmentState
    {
        public string CommitmentId;
        public string Kind;
        public string TargetId;
        public int Strength;
    }

    [Serializable]
    public sealed class RelationshipState
    {
        public string TargetAgentId;
        public int Trust;
        public int Fear;
        public int Obligation;
        public int Authority;
        public int Attachment;
        public NeedKind PerceivedNeed;
        public int PerceivedNeedPressure;
        public long PerceivedNeedObservedTick;
    }

    [Serializable]
    public sealed class BeliefState
    {
        public string BeliefId;
        public string PropositionId;
        public string SubjectId;
        public string ObjectId;
        public string SourceId;
        public int Confidence;
        public int Secrecy;
        public int EmotionalWeight;
        public long AcquiredTick;
        public bool EnteredOfficialRecord;
        public bool Disclosed;
        public long LastWithheldTick = -1;
        public string LastWithheldIncidentId;
    }

    [Serializable]
    public sealed class OfficialStatusState
    {
        public string StatusId;
        public bool Recognised;
    }

    [Serializable]
    public sealed class InstitutionalStandingState
    {
        public bool CanWork = true;
        public bool CanSeekAid = true;
        public bool CanAppeal = true;
        public bool CanGiveEvidence = true;
        public List<OfficialStatusState> OfficialStatuses = new();

        public bool IsRecognised(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return false;
            for (int i = 0; i < OfficialStatuses.Count; i++)
            {
                OfficialStatusState status = OfficialStatuses[i];
                if (string.Equals(status.StatusId, statusId, StringComparison.Ordinal))
                    return status.Recognised;
            }

            return false;
        }

        public void SetRecognised(string statusId, bool recognised)
        {
            if (string.IsNullOrWhiteSpace(statusId))
                throw new ArgumentException("A stable status id is required.", nameof(statusId));

            for (int i = 0; i < OfficialStatuses.Count; i++)
            {
                OfficialStatusState status = OfficialStatuses[i];
                if (!string.Equals(status.StatusId, statusId, StringComparison.Ordinal)) continue;
                status.Recognised = recognised;
                return;
            }

            OfficialStatuses.Add(new OfficialStatusState
            {
                StatusId = statusId,
                Recognised = recognised,
            });
        }
    }

    /// <summary>
    /// A bounded anomalous rule owned by one entity. It reads an official status and
    /// creates a defined need pressure; it contains no global chaos or Fugue meter.
    /// </summary>
    [Serializable]
    public sealed class AnomalyStatusRule
    {
        public string TraitId;
        public string RequiredOfficialStatusId;
        public NeedKind AffectedNeed;
        public int RecognisedPressureDelta;
        public int UnrecognisedPressureDelta;
        public int MinimumTicksBetweenActivations = 3;
        public long LastAppliedTick = -1;
        public string ObservableEffectId;
    }

    [Serializable]
    public sealed class AgentDispositionState
    {
        public int RiskTolerance;
        public int Candour;
        public int Solidarity;
        public int Duty;
        public int InstitutionalReliance;
    }

    [Serializable]
    public sealed class AgentState
    {
        public string StableId;
        public int SimulationOrdinal;
        public string PresentationId;
        public string DisplayName;
        public string SpeciesId;
        public string HouseholdId;
        public string EmployerId;
        public int InstitutionalTrust;
        public AgentDispositionState Disposition = new();
        public InstitutionalStandingState Standing = new();
        public List<NeedState> Needs = new();
        public List<CommitmentState> Commitments = new();
        public List<RelationshipState> Relationships = new();
        public List<BeliefState> Beliefs = new();
        public List<AnomalyStatusRule> AnomalyRules = new();

        public NeedState GetNeed(NeedKind kind)
        {
            for (int i = 0; i < Needs.Count; i++)
                if (Needs[i].Kind == kind) return Needs[i];
            return null;
        }

        public RelationshipState GetRelationship(string targetAgentId)
        {
            for (int i = 0; i < Relationships.Count; i++)
            {
                RelationshipState relationship = Relationships[i];
                if (string.Equals(relationship.TargetAgentId, targetAgentId, StringComparison.Ordinal))
                    return relationship;
            }

            return null;
        }

        public BeliefState GetBelief(string beliefId)
        {
            for (int i = 0; i < Beliefs.Count; i++)
            {
                BeliefState belief = Beliefs[i];
                if (string.Equals(belief.BeliefId, beliefId, StringComparison.Ordinal))
                    return belief;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class InstitutionalRegimeState
    {
        public int WorkReward = 50;
        public int AidEffectiveness = 50;
        public int DisclosureProtection = 50;
        public int RetaliationRisk = 50;
        public int AppealAccessibility = 50;
        public int DecisionVariationAmplitude = 2;
    }

    [Serializable]
    public sealed class SocietyState
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentRulesetVersion = "institutional-agents-v1";
        public const int MaximumEventHistory = 256;
        public const int MaximumAnomalyRulesPerAgent = 2;
        public const int MaximumAnomalyPressurePerActivation = 10;

        public int SchemaVersion = CurrentSchemaVersion;
        public string RulesetVersion = CurrentRulesetVersion;
        public int MasterSeed;
        public long CurrentTick;
        public InstitutionalRegimeState Regime = new();
        public List<AgentState> Agents = new();
        public List<SocietyEvent> EventLedger = new();

        public AgentState GetAgent(string stableId)
        {
            for (int i = 0; i < Agents.Count; i++)
            {
                AgentState agent = Agents[i];
                if (string.Equals(agent.StableId, stableId, StringComparison.Ordinal))
                    return agent;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class WorkOpportunity
    {
        public string OpportunityId;
        public string PurposeId;
        public string SourceCauseId;
        public string RequiredEmployerId;
        public string RequiredOfficialStatusId;
        public bool RequiredOfficialStatusRecognised = true;
        public long EarliestCycle;
        public int UtilityBonus;
        public List<string> ParticipantAgentIds = new();
    }

    [Serializable]
    public sealed class AidOpportunity
    {
        public string OpportunityId;
        public string PurposeId;
        public string SourceCauseId;
        public string RequiredOfficialStatusId;
        public bool RequiredOfficialStatusRecognised = true;
        public int UtilityBonus;
        public List<string> EligibleAgentIds = new();
    }

    [Serializable]
    public sealed class AppealOpportunity
    {
        public string OpportunityId;
        public string DocketId;
        public string CaseId;
        public string ChallengedRulingId;
        public string SourceCauseId;
        public long HearingCycle;
        public int UtilityBonus;
        public List<string> PartyAgentIds = new();
    }

    /// <summary>
    /// A perceived opportunity to make an assertion that conflicts with the actor's
    /// own identified belief. The assertion is information, not a truth mutation.
    /// </summary>
    [Serializable]
    public sealed class LieOpportunity
    {
        public string OpportunityId;
        public string BeliefId;
        public string AssertionPropositionId;
        public string AssertionSubjectId;
        public string AssertionObjectId;
        public string ContextId;
        public int UtilityBonus;
        public EvidenceVisibility Visibility = EvidenceVisibility.Observable;
        public string PotentialRecordSourceId;
        public List<string> EligibleActorIds = new();
        public List<string> AudienceAgentIds = new();
    }

    /// <summary>
    /// A perceived opportunity to take physical possession. Official ownership is
    /// deliberately absent because this action cannot rewrite it.
    /// </summary>
    [Serializable]
    public sealed class StealOpportunity
    {
        public string OpportunityId;
        public string IssueId = EndogenousIssueKindIds.PossessionDispute;
        public string ResourceId;
        public string ExpectedPhysicalHolderId;
        public string NewLocationContextId;
        public string AccessGrantId;
        public string ProtectionStatusId;
        public int RecognisedProtectionUtilityBonus;
        public int UnrecognisedExposureUtilityPenalty;
        public string EnablingRulingId;
        public string ParentCaseId;
        public NeedKind ReliefNeed = NeedKind.Health;
        public int ReliefAmount;
        public int UtilityBonus;
        public EvidenceVisibility Visibility = EvidenceVisibility.Private;
        public int Secrecy;
        public List<string> EligibleActorIds = new();
        public List<string> DirectWitnessAgentIds = new();
        public List<string> PotentialRecordSourceIds = new();
    }

    /// <summary>
    /// A perceived opportunity to use institutional power against an agent believed
    /// to have taken a prior adverse action.
    /// </summary>
    [Serializable]
    public sealed class RetaliationOpportunity
    {
        public string OpportunityId;
        public string TargetAgentId;
        public string PerceivedPriorActionBeliefId;
        public string AuthorityGrantId;
        public string AffectedAccessGrantId;
        public string AdverseActionKindId;
        public int PerceivedPower;
        public int UtilityBonus;
        public EvidenceVisibility Visibility = EvidenceVisibility.Observable;
        public int Secrecy;
        public List<string> EligibleActorIds = new();
        public List<string> DirectWitnessAgentIds = new();
        public List<string> PotentialRecordSourceIds = new();
    }

    /// <summary>
    /// One agent's perceived opportunity to propose or join collective action. Each
    /// actor receives a distinct opportunity id; a shared commitment id is what lets
    /// compatible independent actions accumulate.
    /// </summary>
    [Serializable]
    public sealed class OrganiseOpportunity
    {
        public string OpportunityId;
        public string CollectiveCommitmentId;
        public string IssueId;
        public string IntentionId;
        public string CommunicationContextId;
        public int RequiredParticipantCount = 2;
        public int UtilityBonus;
        public EvidenceVisibility Visibility = EvidenceVisibility.Observable;
        public int Secrecy;
        public List<string> EligibleActorIds = new();
        public List<string> PerceivedCauseEventIds = new();
        public List<string> DirectWitnessAgentIds = new();
        public List<string> PotentialRecordSourceIds = new();
    }

    [Serializable]
    public sealed class SimulationInput
    {
        public string IncidentId = "routine-cycle";
        public bool WorkAvailable = true;
        public bool AidAvailable = true;
        public bool DisclosureRequested = true;
        public bool AppealWindowOpen = true;
        public string OpenDocketId;
        public string AidRequiredOfficialStatusId;
        public List<string> AppealEligibleAgentIds;
        public List<WorkOpportunity> WorkOpportunities = new();
        public List<AidOpportunity> AidOpportunities = new();
        public List<AppealOpportunity> AppealOpportunities = new();
        public List<LieOpportunity> LieOpportunities = new();
        public List<StealOpportunity> StealOpportunities = new();
        public List<RetaliationOpportunity> RetaliationOpportunities = new();
        public List<OrganiseOpportunity> OrganiseOpportunities = new();
        public bool RestrictAidToOpportunities;
        public bool RestrictAppealToOpportunities;

        /// <summary>
        /// Optional attendance for this decision opportunity. Null preserves the
        /// original behaviour (every related agent is perceived); an empty list
        /// represents an individual interview or isolated workstation.
        /// </summary>
        public List<string> VisibleAgentIds;
    }

    [Serializable]
    internal sealed class DecisionReason
    {
        public string ReasonId;
        public string SourceId;
        public int ScoreDelta;
    }

    [Serializable]
    internal sealed class CandidateEvaluation
    {
        public string CandidateId;
        public SocietyActionKind Action;
        public string TargetId;
        public string OpportunityId;
        public string SubjectBeliefId;
        public NeedKind? IntendedNeed;
        public int Score;
        public List<DecisionReason> Reasons = new();
    }

    /// <summary>
    /// Immutable executable portion of one ranked candidate. Diagnostic reasons are
    /// retained separately in CandidateEvaluations; resolution may only select one of
    /// these already-scored entries.
    /// </summary>
    [Serializable]
    internal sealed class RankedCandidatePlanEntry
    {
        public string CandidateId { get; }
        public SocietyActionKind Action { get; }
        public string TargetId { get; }
        public string OpportunityId { get; }
        public string SubjectBeliefId { get; }
        public NeedKind? IntendedNeed { get; }
        public int Score { get; }

        public RankedCandidatePlanEntry(CandidateEvaluation source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            CandidateId = source.CandidateId;
            Action = source.Action;
            TargetId = source.TargetId;
            OpportunityId = source.OpportunityId;
            SubjectBeliefId = source.SubjectBeliefId;
            IntendedNeed = source.IntendedNeed;
            Score = source.Score;
        }
    }

    [Serializable]
    internal sealed class CapacityReservationTrace
    {
        public int CandidateRank;
        public string CandidateId;
        public string OpportunityId;
        public bool Awarded;
        public string HolderActorId;
    }

    /// <summary>
    /// Internal deterministic decision trace. Scores and private reason identifiers are
    /// developer diagnostics, not part of the player-facing observation surface.
    /// </summary>
    [Serializable]
    internal sealed class AgentDecision
    {
        public long Tick;
        public int ApplicationOrdinal;
        public string DecisionId;
        public string CandidateId;
        public string ActorId;
        public SocietyActionKind Action;
        public string TargetId;
        public string OpportunityId;
        public string SubjectBeliefId;
        public NeedKind? IntendedNeed;
        public int Score;
        public List<DecisionReason> Reasons = new();

        // Frozen in score-descending, candidate-id-ascending order by the decision
        // pass. Capacity arbitration may select a later entry, but never re-scores
        // or rebuilds this plan after another action mutates society state.
        public List<CandidateEvaluation> CandidateEvaluations = new();
        private readonly List<RankedCandidatePlanEntry> _rankedCandidatePlan = new();
        [NonSerialized]
        private IReadOnlyList<RankedCandidatePlanEntry> _rankedCandidatePlanView;
        public IReadOnlyList<RankedCandidatePlanEntry> RankedCandidatePlan =>
            _rankedCandidatePlanView ??= _rankedCandidatePlan.AsReadOnly();
        public int SelectedCandidateRank;
        public List<CapacityReservationTrace> CapacityReservations = new();

        // Detached assessor diagnostics captured at decision time. Gameplay consumes
        // the resulting action/event, not these private utility inputs.
        public AgentPerception PerceptionSnapshot;
        public InstitutionalRegimeState RegimeSnapshot;
        public SimulationInput InputSnapshot;

        internal void RetainRankedCandidatePlan(IReadOnlyList<CandidateEvaluation> evaluations)
        {
            if (evaluations == null) throw new ArgumentNullException(nameof(evaluations));
            if (_rankedCandidatePlan.Count != 0)
                throw new InvalidOperationException("A decision plan may only be retained once.");
            for (int i = 0; i < evaluations.Count; i++)
                _rankedCandidatePlan.Add(new RankedCandidatePlanEntry(evaluations[i]));
        }
    }

    [Serializable]
    public sealed class StateDelta
    {
        public string EntityId;
        public string FieldId;
        public int Before;
        public int After;
    }

    [Serializable]
    public sealed class SocietyEvent
    {
        public string EventId;
        public string CauseDecisionId;
        public string IncidentId;
        public long Tick;
        public SocietyEventKind Kind;
        public string ActorId;
        public string TargetId;
        public string OpportunityId;
        public string EvidenceId;
        public string EvidencePropositionId;
        public string EvidenceSubjectId;
        public string EvidenceObjectId;
        public string EvidenceSourceId;
        internal string EvidenceBeliefId;
        internal string ActionResourceId;
        internal string ActionContextId;
        internal string RelatedEventId;
        internal string AuthorityGrantId;
        internal string AffectedStateRecordId;
        internal string CollectiveCommitmentId;
        internal string CollectiveIssueId;
        internal string CollectiveIntentionId;
        internal string EnablingRulingId;
        internal string ParentCaseId;
        internal int RequiredParticipantCount;
        internal int ActionSecrecy;
        internal List<string> DirectWitnessAgentIds = new();
        internal List<string> PotentialRecordSourceIds = new();
        internal List<string> PerceivedCauseEventIds = new();
        public string EvidenceSuppressedByAgentId;
        public int EvidenceReliability;
        public EvidenceVisibility Visibility;
        public List<StateDelta> Deltas = new();
    }

    [Serializable]
    public sealed class SimulationStepResult
    {
        public long Tick;
        internal List<AgentDecision> Decisions = new();
        public List<SocietyEvent> Events = new();
    }

    internal sealed class AgentDecisionContext
    {
        public int MasterSeed;
        public long Tick;
        public AgentPerception Actor;
        public IReadOnlyList<string> PerceivedAgentIds;
        public InstitutionalRegimeState Regime;
        public SimulationInput Input;
    }

    internal static class InstitutionalMath
    {
        public static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
