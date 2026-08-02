using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// A detached snapshot of the information an actor may use for one decision.
    /// It deliberately contains no references to other agents or authoritative lived state.
    /// </summary>
    internal sealed class AgentPerception
    {
        public string StableId;
        public int SimulationOrdinal;
        public string EmployerId;
        public int InstitutionalTrust;
        public AgentDispositionState Disposition;
        public InstitutionalStandingState Standing;
        public List<NeedState> Needs;
        public List<CommitmentState> Commitments;
        public List<RelationshipState> Relationships;
        public List<BeliefState> Beliefs;

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
                if (string.Equals(Beliefs[i].BeliefId, beliefId, StringComparison.Ordinal))
                    return Beliefs[i];
            return null;
        }

        public static AgentPerception Capture(AgentState actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));

            var perception = new AgentPerception
            {
                StableId = actor.StableId,
                SimulationOrdinal = actor.SimulationOrdinal,
                EmployerId = actor.EmployerId,
                InstitutionalTrust = actor.InstitutionalTrust,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = actor.Disposition.RiskTolerance,
                    Candour = actor.Disposition.Candour,
                    Solidarity = actor.Disposition.Solidarity,
                    Duty = actor.Disposition.Duty,
                    InstitutionalReliance = actor.Disposition.InstitutionalReliance,
                },
                Standing = new InstitutionalStandingState
                {
                    CanWork = actor.Standing.CanWork,
                    CanSeekAid = actor.Standing.CanSeekAid,
                    CanAppeal = actor.Standing.CanAppeal,
                    CanGiveEvidence = actor.Standing.CanGiveEvidence,
                },
                Needs = new List<NeedState>(actor.Needs.Count),
                Commitments = new List<CommitmentState>(actor.Commitments.Count),
                Relationships = new List<RelationshipState>(actor.Relationships.Count),
                Beliefs = new List<BeliefState>(actor.Beliefs.Count),
            };

            for (int i = 0; i < actor.Standing.OfficialStatuses.Count; i++)
            {
                OfficialStatusState status = actor.Standing.OfficialStatuses[i];
                perception.Standing.OfficialStatuses.Add(new OfficialStatusState
                {
                    StatusId = status.StatusId,
                    Recognised = status.Recognised,
                });
            }
            for (int i = 0; i < actor.Needs.Count; i++)
            {
                NeedState need = actor.Needs[i];
                perception.Needs.Add(new NeedState { Kind = need.Kind, Pressure = need.Pressure });
            }
            for (int i = 0; i < actor.Commitments.Count; i++)
            {
                CommitmentState commitment = actor.Commitments[i];
                perception.Commitments.Add(new CommitmentState
                {
                    CommitmentId = commitment.CommitmentId,
                    Kind = commitment.Kind,
                    TargetId = commitment.TargetId,
                    Strength = commitment.Strength,
                });
            }
            for (int i = 0; i < actor.Relationships.Count; i++)
            {
                RelationshipState relationship = actor.Relationships[i];
                perception.Relationships.Add(new RelationshipState
                {
                    TargetAgentId = relationship.TargetAgentId,
                    Trust = relationship.Trust,
                    Fear = relationship.Fear,
                    Obligation = relationship.Obligation,
                    Authority = relationship.Authority,
                    Attachment = relationship.Attachment,
                    PerceivedNeed = relationship.PerceivedNeed,
                    PerceivedNeedPressure = relationship.PerceivedNeedPressure,
                    PerceivedNeedObservedTick = relationship.PerceivedNeedObservedTick,
                });
            }
            for (int i = 0; i < actor.Beliefs.Count; i++)
            {
                BeliefState belief = actor.Beliefs[i];
                perception.Beliefs.Add(new BeliefState
                {
                    BeliefId = belief.BeliefId,
                    PropositionId = belief.PropositionId,
                    SubjectId = belief.SubjectId,
                    ObjectId = belief.ObjectId,
                    SourceId = belief.SourceId,
                    Confidence = belief.Confidence,
                    Secrecy = belief.Secrecy,
                    EmotionalWeight = belief.EmotionalWeight,
                    AcquiredTick = belief.AcquiredTick,
                    EnteredOfficialRecord = belief.EnteredOfficialRecord,
                    Disclosed = belief.Disclosed,
                    LastWithheldTick = belief.LastWithheldTick,
                    LastWithheldIncidentId = belief.LastWithheldIncidentId,
                });
            }

            return perception;
        }

        internal static AgentPerception Copy(AgentPerception source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Capture(new AgentState
            {
                StableId = source.StableId,
                SimulationOrdinal = source.SimulationOrdinal,
                EmployerId = source.EmployerId,
                InstitutionalTrust = source.InstitutionalTrust,
                Disposition = source.Disposition,
                Standing = source.Standing,
                Needs = source.Needs,
                Commitments = source.Commitments,
                Relationships = source.Relationships,
                Beliefs = source.Beliefs,
            });
        }
    }
}
