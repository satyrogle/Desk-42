using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Creates a deterministic, detached copy of the complete persisted society graph.
    /// The copy retains collection order and does not normalise null collections or
    /// null elements, allowing callers to validate the copied graph at their boundary.
    /// </summary>
    internal static class SocietyStateDeepCopy
    {
        internal static SocietyState Copy(SocietyState source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return new SocietyState
            {
                SchemaVersion = source.SchemaVersion,
                RulesetVersion = source.RulesetVersion,
                MasterSeed = source.MasterSeed,
                CurrentTick = source.CurrentTick,
                Regime = CopyRegime(source.Regime),
                Agents = CopyList(source.Agents, CopyAgent),
                EventLedger = CopyList(source.EventLedger, CopyEvent),
            };
        }

        private static InstitutionalRegimeState CopyRegime(InstitutionalRegimeState source)
        {
            if (source == null) return null;

            return new InstitutionalRegimeState
            {
                WorkReward = source.WorkReward,
                AidEffectiveness = source.AidEffectiveness,
                DisclosureProtection = source.DisclosureProtection,
                RetaliationRisk = source.RetaliationRisk,
                AppealAccessibility = source.AppealAccessibility,
                DecisionVariationAmplitude = source.DecisionVariationAmplitude,
            };
        }

        private static AgentState CopyAgent(AgentState source)
        {
            if (source == null) return null;

            return new AgentState
            {
                StableId = source.StableId,
                SimulationOrdinal = source.SimulationOrdinal,
                PresentationId = source.PresentationId,
                DisplayName = source.DisplayName,
                SpeciesId = source.SpeciesId,
                HouseholdId = source.HouseholdId,
                EmployerId = source.EmployerId,
                InstitutionalTrust = source.InstitutionalTrust,
                Disposition = CopyDisposition(source.Disposition),
                Standing = CopyStanding(source.Standing),
                Needs = CopyList(source.Needs, CopyNeed),
                Commitments = CopyList(source.Commitments, CopyCommitment),
                Relationships = CopyList(source.Relationships, CopyRelationship),
                Beliefs = CopyList(source.Beliefs, CopyBelief),
                AnomalyRules = CopyList(source.AnomalyRules, CopyAnomalyRule),
            };
        }

        private static AgentDispositionState CopyDisposition(AgentDispositionState source)
        {
            if (source == null) return null;

            return new AgentDispositionState
            {
                RiskTolerance = source.RiskTolerance,
                Candour = source.Candour,
                Solidarity = source.Solidarity,
                Duty = source.Duty,
                InstitutionalReliance = source.InstitutionalReliance,
            };
        }

        private static InstitutionalStandingState CopyStanding(InstitutionalStandingState source)
        {
            if (source == null) return null;

            return new InstitutionalStandingState
            {
                CanWork = source.CanWork,
                CanSeekAid = source.CanSeekAid,
                CanAppeal = source.CanAppeal,
                CanGiveEvidence = source.CanGiveEvidence,
                OfficialStatuses = CopyList(source.OfficialStatuses, CopyOfficialStatus),
            };
        }

        private static OfficialStatusState CopyOfficialStatus(OfficialStatusState source)
        {
            if (source == null) return null;

            return new OfficialStatusState
            {
                StatusId = source.StatusId,
                Recognised = source.Recognised,
            };
        }

        private static NeedState CopyNeed(NeedState source)
        {
            if (source == null) return null;

            return new NeedState
            {
                Kind = source.Kind,
                Pressure = source.Pressure,
            };
        }

        private static CommitmentState CopyCommitment(CommitmentState source)
        {
            if (source == null) return null;

            return new CommitmentState
            {
                CommitmentId = source.CommitmentId,
                Kind = source.Kind,
                TargetId = source.TargetId,
                Strength = source.Strength,
            };
        }

        private static RelationshipState CopyRelationship(RelationshipState source)
        {
            if (source == null) return null;

            return new RelationshipState
            {
                TargetAgentId = source.TargetAgentId,
                Trust = source.Trust,
                Fear = source.Fear,
                Obligation = source.Obligation,
                Authority = source.Authority,
                Attachment = source.Attachment,
                PerceivedNeed = source.PerceivedNeed,
                PerceivedNeedPressure = source.PerceivedNeedPressure,
                PerceivedNeedObservedTick = source.PerceivedNeedObservedTick,
            };
        }

        private static BeliefState CopyBelief(BeliefState source)
        {
            if (source == null) return null;

            return new BeliefState
            {
                BeliefId = source.BeliefId,
                PropositionId = source.PropositionId,
                SubjectId = source.SubjectId,
                ObjectId = source.ObjectId,
                SourceId = source.SourceId,
                Confidence = source.Confidence,
                Secrecy = source.Secrecy,
                EmotionalWeight = source.EmotionalWeight,
                AcquiredTick = source.AcquiredTick,
                EnteredOfficialRecord = source.EnteredOfficialRecord,
                Disclosed = source.Disclosed,
                LastWithheldTick = source.LastWithheldTick,
                LastWithheldIncidentId = source.LastWithheldIncidentId,
            };
        }

        private static AnomalyStatusRule CopyAnomalyRule(AnomalyStatusRule source)
        {
            if (source == null) return null;

            return new AnomalyStatusRule
            {
                TraitId = source.TraitId,
                RequiredOfficialStatusId = source.RequiredOfficialStatusId,
                AffectedNeed = source.AffectedNeed,
                RecognisedPressureDelta = source.RecognisedPressureDelta,
                UnrecognisedPressureDelta = source.UnrecognisedPressureDelta,
                MinimumTicksBetweenActivations = source.MinimumTicksBetweenActivations,
                LastAppliedTick = source.LastAppliedTick,
                ObservableEffectId = source.ObservableEffectId,
            };
        }

        private static SocietyEvent CopyEvent(SocietyEvent source)
        {
            if (source == null) return null;

            return new SocietyEvent
            {
                EventId = source.EventId,
                CauseDecisionId = source.CauseDecisionId,
                IncidentId = source.IncidentId,
                Tick = source.Tick,
                Kind = source.Kind,
                ActorId = source.ActorId,
                TargetId = source.TargetId,
                OpportunityId = source.OpportunityId,
                EvidenceId = source.EvidenceId,
                EvidencePropositionId = source.EvidencePropositionId,
                EvidenceSubjectId = source.EvidenceSubjectId,
                EvidenceObjectId = source.EvidenceObjectId,
                EvidenceSourceId = source.EvidenceSourceId,
                EvidenceBeliefId = source.EvidenceBeliefId,
                EvidenceSuppressedByAgentId = source.EvidenceSuppressedByAgentId,
                EvidenceReliability = source.EvidenceReliability,
                Visibility = source.Visibility,
                ActionResourceId = source.ActionResourceId,
                ActionContextId = source.ActionContextId,
                RelatedEventId = source.RelatedEventId,
                AuthorityGrantId = source.AuthorityGrantId,
                AffectedStateRecordId = source.AffectedStateRecordId,
                CollectiveCommitmentId = source.CollectiveCommitmentId,
                CollectiveIssueId = source.CollectiveIssueId,
                CollectiveIntentionId = source.CollectiveIntentionId,
                RequiredParticipantCount = source.RequiredParticipantCount,
                ActionSecrecy = source.ActionSecrecy,
                DirectWitnessAgentIds = CopyStrings(source.DirectWitnessAgentIds),
                PotentialRecordSourceIds = CopyStrings(source.PotentialRecordSourceIds),
                PerceivedCauseEventIds = CopyStrings(source.PerceivedCauseEventIds),
                Deltas = CopyList(source.Deltas, CopyDelta),
            };
        }

        private static List<string> CopyStrings(List<string> source)
        {
            if (source == null) return null;
            return new List<string>(source);
        }

        private static StateDelta CopyDelta(StateDelta source)
        {
            if (source == null) return null;

            return new StateDelta
            {
                EntityId = source.EntityId,
                FieldId = source.FieldId,
                Before = source.Before,
                After = source.After,
            };
        }

        private static List<TTarget> CopyList<TSource, TTarget>(
            List<TSource> source,
            Func<TSource, TTarget> copyElement)
        {
            if (source == null) return null;

            var copy = new List<TTarget>(source.Count);
            for (int i = 0; i < source.Count; i++)
                copy.Add(copyElement(source[i]));
            return copy;
        }
    }
}
