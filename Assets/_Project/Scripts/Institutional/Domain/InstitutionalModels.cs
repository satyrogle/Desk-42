using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
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
    public sealed class SimulationInput
    {
        public string IncidentId = "routine-cycle";
        public bool WorkAvailable = true;
        public bool AidAvailable = true;
        public bool DisclosureRequested = true;
        public bool AppealWindowOpen = true;
    }

    [Serializable]
    public sealed class DecisionReason
    {
        public string ReasonId;
        public string SourceId;
        public int ScoreDelta;
    }

    /// <summary>
    /// Internal deterministic decision trace. Scores and private reason identifiers are
    /// developer diagnostics, not part of the player-facing observation surface.
    /// </summary>
    [Serializable]
    public sealed class AgentDecision
    {
        public long Tick;
        public int ApplicationOrdinal;
        public string DecisionId;
        public string CandidateId;
        public string ActorId;
        public SocietyActionKind Action;
        public string TargetId;
        public string SubjectBeliefId;
        public NeedKind? IntendedNeed;
        public int Score;
        public List<DecisionReason> Reasons = new();
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
        public string EvidenceId;
        public string EvidencePropositionId;
        public string EvidenceSubjectId;
        public string EvidenceObjectId;
        public string EvidenceSourceId;
        public EvidenceVisibility Visibility;
        public List<StateDelta> Deltas = new();
    }

    [Serializable]
    public sealed class SimulationStepResult
    {
        public long Tick;
        public List<AgentDecision> Decisions = new();
        public List<SocietyEvent> Events = new();
    }

    public sealed class AgentDecisionContext
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
