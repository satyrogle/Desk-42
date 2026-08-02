using System;
using System.Collections.Generic;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class SocietyStateDeepCopyTests
    {
        [Test]
        public void Copy_CompleteGraphIsDetachedOrderedAndValidAfterSourceMutation()
        {
            SocietyState source = CreateValidState();

            SocietyState copy = SocietyStateDeepCopy.Copy(source);

            AssertDetached(source, copy);
            MutateEverySourceCategory(source);
            AssertOriginalValuesRemain(copy);
            Assert.DoesNotThrow(() => SocietyStateValidator.Validate(copy));
        }

        [Test]
        public void Copy_PreservesNullCollectionsElementsAndNestedObjects()
        {
            var nullRootCollections = new SocietyState
            {
                Regime = null,
                Agents = null,
                EventLedger = null,
            };

            SocietyState nullRootCopy = SocietyStateDeepCopy.Copy(nullRootCollections);

            Assert.IsNull(nullRootCopy.Regime);
            Assert.IsNull(nullRootCopy.Agents);
            Assert.IsNull(nullRootCopy.EventLedger);

            var partialAgent = new AgentState
            {
                Disposition = null,
                Standing = null,
                Needs = null,
                Commitments = null,
                Relationships = null,
                Beliefs = null,
                AnomalyRules = null,
            };
            var partialEvent = new SocietyEvent { Deltas = null };
            var partial = new SocietyState
            {
                Agents = new List<AgentState> { null, partialAgent },
                EventLedger = new List<SocietyEvent> { null, partialEvent },
            };

            SocietyState partialCopy = SocietyStateDeepCopy.Copy(partial);

            Assert.AreEqual(2, partialCopy.Agents.Count);
            Assert.IsNull(partialCopy.Agents[0]);
            Assert.AreNotSame(partialAgent, partialCopy.Agents[1]);
            Assert.IsNull(partialCopy.Agents[1].Disposition);
            Assert.IsNull(partialCopy.Agents[1].Standing);
            Assert.IsNull(partialCopy.Agents[1].Needs);
            Assert.IsNull(partialCopy.Agents[1].Commitments);
            Assert.IsNull(partialCopy.Agents[1].Relationships);
            Assert.IsNull(partialCopy.Agents[1].Beliefs);
            Assert.IsNull(partialCopy.Agents[1].AnomalyRules);
            Assert.AreEqual(2, partialCopy.EventLedger.Count);
            Assert.IsNull(partialCopy.EventLedger[0]);
            Assert.AreNotSame(partialEvent, partialCopy.EventLedger[1]);
            Assert.IsNull(partialCopy.EventLedger[1].Deltas);
        }

        [Test]
        public void Copy_NullSourceThrows()
        {
            Assert.Throws<ArgumentNullException>(() => SocietyStateDeepCopy.Copy(null));
        }

        private static SocietyState CreateValidState()
        {
            var alpha = CreateAgent("agent.alpha", 0, "agent.beta", 10);
            var beta = CreateAgent("agent.beta", 1, "agent.alpha", 20);

            return new SocietyState
            {
                SchemaVersion = SocietyState.CurrentSchemaVersion,
                RulesetVersion = SocietyState.CurrentRulesetVersion,
                MasterSeed = 7391,
                CurrentTick = 42,
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 61,
                    AidEffectiveness = 62,
                    DisclosureProtection = 63,
                    RetaliationRisk = 64,
                    AppealAccessibility = 65,
                    DecisionVariationAmplitude = 6,
                },
                Agents = new List<AgentState> { alpha, beta },
                EventLedger = new List<SocietyEvent>
                {
                    CreateEvent("event.first", "agent.alpha", "agent.beta", 40),
                    CreateEvent("event.second", "agent.beta", "agent.alpha", 41),
                },
            };
        }

        private static AgentState CreateAgent(
            string stableId,
            int ordinal,
            string relationshipTargetId,
            int offset)
        {
            return new AgentState
            {
                StableId = stableId,
                SimulationOrdinal = ordinal,
                PresentationId = $"presentation.{ordinal}",
                DisplayName = $"Agent {ordinal}",
                SpeciesId = $"species.{ordinal}",
                HouseholdId = $"household.{ordinal}",
                EmployerId = $"employer.{ordinal}",
                InstitutionalTrust = 40 + offset,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = 10 + offset,
                    Candour = 20 + offset,
                    Solidarity = 30 + offset,
                    Duty = 40 + offset,
                    InstitutionalReliance = 50 + offset,
                },
                Standing = new InstitutionalStandingState
                {
                    CanWork = ordinal == 0,
                    CanSeekAid = true,
                    CanAppeal = true,
                    CanGiveEvidence = ordinal != 0,
                    OfficialStatuses = new List<OfficialStatusState>
                    {
                        new()
                        {
                            StatusId = $"status.{ordinal}.first",
                            Recognised = true,
                        },
                        new()
                        {
                            StatusId = $"status.{ordinal}.second",
                            Recognised = false,
                        },
                    },
                },
                Needs = new List<NeedState>
                {
                    new() { Kind = NeedKind.Health, Pressure = 11 + offset },
                    new() { Kind = NeedKind.Subsistence, Pressure = 12 + offset },
                    new() { Kind = NeedKind.Safety, Pressure = 13 + offset },
                    new() { Kind = NeedKind.Belonging, Pressure = 14 + offset },
                    new() { Kind = NeedKind.Autonomy, Pressure = 15 + offset },
                },
                Commitments = new List<CommitmentState>
                {
                    new()
                    {
                        CommitmentId = $"commitment.{ordinal}.first",
                        Kind = $"commitment-kind.{ordinal}",
                        TargetId = relationshipTargetId,
                        Strength = 35 + offset,
                    },
                    new()
                    {
                        CommitmentId = $"commitment.{ordinal}.second",
                        Kind = $"commitment-kind.{ordinal}.secondary",
                        TargetId = stableId,
                        Strength = 36 + offset,
                    },
                },
                Relationships = new List<RelationshipState>
                {
                    new()
                    {
                        TargetAgentId = relationshipTargetId,
                        Trust = 21 + offset,
                        Fear = 22 + offset,
                        Obligation = 23 + offset,
                        Authority = 24 + offset,
                        Attachment = 25 + offset,
                        PerceivedNeed = NeedKind.Safety,
                        PerceivedNeedPressure = 26 + offset,
                        PerceivedNeedObservedTick = 27 + offset,
                    },
                },
                Beliefs = new List<BeliefState>
                {
                    new()
                    {
                        BeliefId = $"belief.{ordinal}.first",
                        PropositionId = $"proposition.{ordinal}",
                        SubjectId = stableId,
                        ObjectId = relationshipTargetId,
                        SourceId = $"source.{ordinal}",
                        Confidence = 31 + offset,
                        Secrecy = 32 + offset,
                        EmotionalWeight = 33 + offset,
                        AcquiredTick = 34 + offset,
                        EnteredOfficialRecord = ordinal == 0,
                        Disclosed = ordinal != 0,
                        LastWithheldTick = 35 + offset,
                        LastWithheldIncidentId = $"incident.withheld.{ordinal}",
                    },
                },
                AnomalyRules = new List<AnomalyStatusRule>
                {
                    new()
                    {
                        TraitId = $"trait.{ordinal}.first",
                        RequiredOfficialStatusId = $"status.{ordinal}.first",
                        AffectedNeed = NeedKind.Autonomy,
                        RecognisedPressureDelta = 3,
                        UnrecognisedPressureDelta = -4,
                        MinimumTicksBetweenActivations = 5 + ordinal,
                        LastAppliedTick = 36 + offset,
                        ObservableEffectId = $"effect.{ordinal}",
                    },
                },
            };
        }

        private static SocietyEvent CreateEvent(
            string eventId,
            string actorId,
            string targetId,
            long tick)
        {
            return new SocietyEvent
            {
                EventId = eventId,
                CauseDecisionId = $"decision.{eventId}",
                IncidentId = $"incident.{eventId}",
                Tick = tick,
                Kind = SocietyEventKind.EvidenceDisclosed,
                ActorId = actorId,
                TargetId = targetId,
                OpportunityId = $"opportunity.{eventId}",
                EvidenceId = $"evidence.{eventId}",
                EvidencePropositionId = $"evidence-proposition.{eventId}",
                EvidenceSubjectId = actorId,
                EvidenceObjectId = targetId,
                EvidenceSourceId = $"evidence-source.{eventId}",
                EvidenceBeliefId = $"evidence-belief.{eventId}",
                EvidenceSuppressedByAgentId = $"suppression.{eventId}",
                EvidenceReliability = 81,
                Visibility = EvidenceVisibility.OfficialRecord,
                Deltas = new List<StateDelta>
                {
                    new()
                    {
                        EntityId = actorId,
                        FieldId = $"field.{eventId}.first",
                        Before = 7,
                        After = 8,
                    },
                    new()
                    {
                        EntityId = targetId,
                        FieldId = $"field.{eventId}.second",
                        Before = 9,
                        After = 10,
                    },
                },
            };
        }

        private static void AssertDetached(SocietyState source, SocietyState copy)
        {
            Assert.AreNotSame(source, copy);
            Assert.AreNotSame(source.Regime, copy.Regime);
            Assert.AreNotSame(source.Agents, copy.Agents);
            Assert.AreNotSame(source.EventLedger, copy.EventLedger);

            for (int agentIndex = 0; agentIndex < source.Agents.Count; agentIndex++)
            {
                AgentState sourceAgent = source.Agents[agentIndex];
                AgentState copiedAgent = copy.Agents[agentIndex];
                Assert.AreNotSame(sourceAgent, copiedAgent);
                Assert.AreNotSame(sourceAgent.Disposition, copiedAgent.Disposition);
                Assert.AreNotSame(sourceAgent.Standing, copiedAgent.Standing);
                Assert.AreNotSame(
                    sourceAgent.Standing.OfficialStatuses,
                    copiedAgent.Standing.OfficialStatuses);
                Assert.AreNotSame(
                    sourceAgent.Standing.OfficialStatuses[0],
                    copiedAgent.Standing.OfficialStatuses[0]);
                Assert.AreNotSame(sourceAgent.Needs, copiedAgent.Needs);
                Assert.AreNotSame(sourceAgent.Needs[0], copiedAgent.Needs[0]);
                Assert.AreNotSame(sourceAgent.Commitments, copiedAgent.Commitments);
                Assert.AreNotSame(sourceAgent.Commitments[0], copiedAgent.Commitments[0]);
                Assert.AreNotSame(sourceAgent.Relationships, copiedAgent.Relationships);
                Assert.AreNotSame(sourceAgent.Relationships[0], copiedAgent.Relationships[0]);
                Assert.AreNotSame(sourceAgent.Beliefs, copiedAgent.Beliefs);
                Assert.AreNotSame(sourceAgent.Beliefs[0], copiedAgent.Beliefs[0]);
                Assert.AreNotSame(sourceAgent.AnomalyRules, copiedAgent.AnomalyRules);
                Assert.AreNotSame(sourceAgent.AnomalyRules[0], copiedAgent.AnomalyRules[0]);
            }

            for (int eventIndex = 0; eventIndex < source.EventLedger.Count; eventIndex++)
            {
                SocietyEvent sourceEvent = source.EventLedger[eventIndex];
                SocietyEvent copiedEvent = copy.EventLedger[eventIndex];
                Assert.AreNotSame(sourceEvent, copiedEvent);
                Assert.AreNotSame(sourceEvent.Deltas, copiedEvent.Deltas);
                Assert.AreNotSame(sourceEvent.Deltas[0], copiedEvent.Deltas[0]);
            }
        }

        private static void MutateEverySourceCategory(SocietyState source)
        {
            AgentState agent = source.Agents[0];
            AgentDispositionState disposition = agent.Disposition;
            InstitutionalStandingState standing = agent.Standing;
            OfficialStatusState status = standing.OfficialStatuses[0];
            NeedState need = agent.Needs[0];
            CommitmentState commitment = agent.Commitments[0];
            RelationshipState relationship = agent.Relationships[0];
            BeliefState belief = agent.Beliefs[0];
            AnomalyStatusRule anomaly = agent.AnomalyRules[0];
            SocietyEvent societyEvent = source.EventLedger[0];
            StateDelta delta = societyEvent.Deltas[0];

            source.SchemaVersion = 999;
            source.RulesetVersion = "mutated.ruleset";
            source.MasterSeed = -1;
            source.CurrentTick = -1;

            source.Regime.WorkReward = 1;
            source.Regime.AidEffectiveness = 2;
            source.Regime.DisclosureProtection = 3;
            source.Regime.RetaliationRisk = 4;
            source.Regime.AppealAccessibility = 5;
            source.Regime.DecisionVariationAmplitude = 0;

            agent.StableId = "mutated.agent";
            agent.SimulationOrdinal = 99;
            agent.PresentationId = "mutated.presentation";
            agent.DisplayName = "Mutated";
            agent.SpeciesId = "mutated.species";
            agent.HouseholdId = "mutated.household";
            agent.EmployerId = "mutated.employer";
            agent.InstitutionalTrust = -100;

            disposition.RiskTolerance = 1;
            disposition.Candour = 2;
            disposition.Solidarity = 3;
            disposition.Duty = 4;
            disposition.InstitutionalReliance = 5;

            standing.CanWork = !standing.CanWork;
            standing.CanSeekAid = false;
            standing.CanAppeal = false;
            standing.CanGiveEvidence = !standing.CanGiveEvidence;
            status.StatusId = "mutated.status";
            status.Recognised = !status.Recognised;
            standing.OfficialStatuses.Clear();

            need.Kind = NeedKind.Belonging;
            need.Pressure = 99;
            agent.Needs.Clear();

            commitment.CommitmentId = "mutated.commitment";
            commitment.Kind = "mutated.kind";
            commitment.TargetId = "mutated.target";
            commitment.Strength = 99;
            agent.Commitments.Clear();

            relationship.TargetAgentId = "mutated.relationship-target";
            relationship.Trust = 1;
            relationship.Fear = 2;
            relationship.Obligation = 3;
            relationship.Authority = 4;
            relationship.Attachment = 5;
            relationship.PerceivedNeed = NeedKind.Health;
            relationship.PerceivedNeedPressure = 6;
            relationship.PerceivedNeedObservedTick = 7;
            agent.Relationships.Clear();

            belief.BeliefId = "mutated.belief";
            belief.PropositionId = "mutated.proposition";
            belief.SubjectId = "mutated.subject";
            belief.ObjectId = "mutated.object";
            belief.SourceId = "mutated.source";
            belief.Confidence = 1;
            belief.Secrecy = 2;
            belief.EmotionalWeight = 3;
            belief.AcquiredTick = 4;
            belief.EnteredOfficialRecord = !belief.EnteredOfficialRecord;
            belief.Disclosed = !belief.Disclosed;
            belief.LastWithheldTick = 5;
            belief.LastWithheldIncidentId = "mutated.incident";
            agent.Beliefs.Clear();

            anomaly.TraitId = "mutated.trait";
            anomaly.RequiredOfficialStatusId = "mutated.required-status";
            anomaly.AffectedNeed = NeedKind.Health;
            anomaly.RecognisedPressureDelta = 1;
            anomaly.UnrecognisedPressureDelta = 2;
            anomaly.MinimumTicksBetweenActivations = 3;
            anomaly.LastAppliedTick = 4;
            anomaly.ObservableEffectId = "mutated.effect";
            agent.AnomalyRules.Clear();

            societyEvent.EventId = "mutated.event";
            societyEvent.CauseDecisionId = "mutated.decision";
            societyEvent.IncidentId = "mutated.incident";
            societyEvent.Tick = -1;
            societyEvent.Kind = SocietyEventKind.NoActionObserved;
            societyEvent.ActorId = "mutated.actor";
            societyEvent.TargetId = "mutated.target";
            societyEvent.OpportunityId = "mutated.opportunity";
            societyEvent.EvidenceId = "mutated.evidence";
            societyEvent.EvidencePropositionId = "mutated.evidence-proposition";
            societyEvent.EvidenceSubjectId = "mutated.evidence-subject";
            societyEvent.EvidenceObjectId = "mutated.evidence-object";
            societyEvent.EvidenceSourceId = "mutated.evidence-source";
            societyEvent.EvidenceBeliefId = "mutated.evidence-belief";
            societyEvent.EvidenceSuppressedByAgentId = "mutated.suppressor";
            societyEvent.EvidenceReliability = -1;
            societyEvent.Visibility = EvidenceVisibility.Private;

            delta.EntityId = "mutated.delta-entity";
            delta.FieldId = "mutated.delta-field";
            delta.Before = -1;
            delta.After = -2;
            societyEvent.Deltas.Clear();

            source.Agents.Clear();
            source.EventLedger.Clear();
        }

        private static void AssertOriginalValuesRemain(SocietyState copy)
        {
            Assert.AreEqual(SocietyState.CurrentSchemaVersion, copy.SchemaVersion);
                Assert.AreEqual(SocietyState.CurrentRulesetVersion, copy.RulesetVersion);
                Assert.AreEqual(7391, copy.MasterSeed);
                Assert.AreEqual(42, copy.CurrentTick);

                Assert.AreEqual(61, copy.Regime.WorkReward);
                Assert.AreEqual(62, copy.Regime.AidEffectiveness);
                Assert.AreEqual(63, copy.Regime.DisclosureProtection);
                Assert.AreEqual(64, copy.Regime.RetaliationRisk);
                Assert.AreEqual(65, copy.Regime.AppealAccessibility);
                Assert.AreEqual(6, copy.Regime.DecisionVariationAmplitude);

                Assert.AreEqual(2, copy.Agents.Count);
                Assert.AreEqual("agent.alpha", copy.Agents[0].StableId);
                Assert.AreEqual("agent.beta", copy.Agents[1].StableId);
                Assert.AreEqual(0, copy.Agents[0].SimulationOrdinal);
                Assert.AreEqual("presentation.0", copy.Agents[0].PresentationId);
                Assert.AreEqual("Agent 0", copy.Agents[0].DisplayName);
                Assert.AreEqual("species.0", copy.Agents[0].SpeciesId);
                Assert.AreEqual("household.0", copy.Agents[0].HouseholdId);
                Assert.AreEqual("employer.0", copy.Agents[0].EmployerId);
                Assert.AreEqual(50, copy.Agents[0].InstitutionalTrust);

                Assert.AreEqual(20, copy.Agents[0].Disposition.RiskTolerance);
                Assert.AreEqual(30, copy.Agents[0].Disposition.Candour);
                Assert.AreEqual(40, copy.Agents[0].Disposition.Solidarity);
                Assert.AreEqual(50, copy.Agents[0].Disposition.Duty);
                Assert.AreEqual(60, copy.Agents[0].Disposition.InstitutionalReliance);

                Assert.IsTrue(copy.Agents[0].Standing.CanWork);
                Assert.IsTrue(copy.Agents[0].Standing.CanSeekAid);
                Assert.IsTrue(copy.Agents[0].Standing.CanAppeal);
                Assert.IsFalse(copy.Agents[0].Standing.CanGiveEvidence);
                Assert.AreEqual(2, copy.Agents[0].Standing.OfficialStatuses.Count);
                Assert.AreEqual(
                    "status.0.first",
                    copy.Agents[0].Standing.OfficialStatuses[0].StatusId);
                Assert.IsTrue(copy.Agents[0].Standing.OfficialStatuses[0].Recognised);
                Assert.AreEqual(
                    "status.0.second",
                    copy.Agents[0].Standing.OfficialStatuses[1].StatusId);

                Assert.AreEqual(5, copy.Agents[0].Needs.Count);
                Assert.AreEqual(NeedKind.Health, copy.Agents[0].Needs[0].Kind);
                Assert.AreEqual(21, copy.Agents[0].Needs[0].Pressure);

                Assert.AreEqual(2, copy.Agents[0].Commitments.Count);
                Assert.AreEqual(
                    "commitment.0.first",
                    copy.Agents[0].Commitments[0].CommitmentId);
                Assert.AreEqual("commitment-kind.0", copy.Agents[0].Commitments[0].Kind);
                Assert.AreEqual("agent.beta", copy.Agents[0].Commitments[0].TargetId);
                Assert.AreEqual(45, copy.Agents[0].Commitments[0].Strength);

                Assert.AreEqual("agent.beta", copy.Agents[0].Relationships[0].TargetAgentId);
                Assert.AreEqual(31, copy.Agents[0].Relationships[0].Trust);
                Assert.AreEqual(32, copy.Agents[0].Relationships[0].Fear);
                Assert.AreEqual(33, copy.Agents[0].Relationships[0].Obligation);
                Assert.AreEqual(34, copy.Agents[0].Relationships[0].Authority);
                Assert.AreEqual(35, copy.Agents[0].Relationships[0].Attachment);
                Assert.AreEqual(NeedKind.Safety, copy.Agents[0].Relationships[0].PerceivedNeed);
                Assert.AreEqual(36, copy.Agents[0].Relationships[0].PerceivedNeedPressure);
                Assert.AreEqual(37, copy.Agents[0].Relationships[0].PerceivedNeedObservedTick);

                Assert.AreEqual("belief.0.first", copy.Agents[0].Beliefs[0].BeliefId);
                Assert.AreEqual("proposition.0", copy.Agents[0].Beliefs[0].PropositionId);
                Assert.AreEqual("agent.alpha", copy.Agents[0].Beliefs[0].SubjectId);
                Assert.AreEqual("agent.beta", copy.Agents[0].Beliefs[0].ObjectId);
                Assert.AreEqual("source.0", copy.Agents[0].Beliefs[0].SourceId);
                Assert.AreEqual(41, copy.Agents[0].Beliefs[0].Confidence);
                Assert.AreEqual(42, copy.Agents[0].Beliefs[0].Secrecy);
                Assert.AreEqual(43, copy.Agents[0].Beliefs[0].EmotionalWeight);
                Assert.AreEqual(44, copy.Agents[0].Beliefs[0].AcquiredTick);
                Assert.IsTrue(copy.Agents[0].Beliefs[0].EnteredOfficialRecord);
                Assert.IsFalse(copy.Agents[0].Beliefs[0].Disclosed);
                Assert.AreEqual(45, copy.Agents[0].Beliefs[0].LastWithheldTick);
                Assert.AreEqual(
                    "incident.withheld.0",
                    copy.Agents[0].Beliefs[0].LastWithheldIncidentId);

                Assert.AreEqual("trait.0.first", copy.Agents[0].AnomalyRules[0].TraitId);
                Assert.AreEqual(
                    "status.0.first",
                    copy.Agents[0].AnomalyRules[0].RequiredOfficialStatusId);
                Assert.AreEqual(NeedKind.Autonomy, copy.Agents[0].AnomalyRules[0].AffectedNeed);
                Assert.AreEqual(3, copy.Agents[0].AnomalyRules[0].RecognisedPressureDelta);
                Assert.AreEqual(-4, copy.Agents[0].AnomalyRules[0].UnrecognisedPressureDelta);
                Assert.AreEqual(5, copy.Agents[0].AnomalyRules[0].MinimumTicksBetweenActivations);
                Assert.AreEqual(46, copy.Agents[0].AnomalyRules[0].LastAppliedTick);
                Assert.AreEqual("effect.0", copy.Agents[0].AnomalyRules[0].ObservableEffectId);

                Assert.AreEqual(2, copy.EventLedger.Count);
                Assert.AreEqual("event.first", copy.EventLedger[0].EventId);
                Assert.AreEqual("event.second", copy.EventLedger[1].EventId);
                Assert.AreEqual("decision.event.first", copy.EventLedger[0].CauseDecisionId);
                Assert.AreEqual("incident.event.first", copy.EventLedger[0].IncidentId);
                Assert.AreEqual(40, copy.EventLedger[0].Tick);
                Assert.AreEqual(SocietyEventKind.EvidenceDisclosed, copy.EventLedger[0].Kind);
                Assert.AreEqual("agent.alpha", copy.EventLedger[0].ActorId);
                Assert.AreEqual("agent.beta", copy.EventLedger[0].TargetId);
                Assert.AreEqual("opportunity.event.first", copy.EventLedger[0].OpportunityId);
                Assert.AreEqual("evidence.event.first", copy.EventLedger[0].EvidenceId);
                Assert.AreEqual(
                    "evidence-proposition.event.first",
                    copy.EventLedger[0].EvidencePropositionId);
                Assert.AreEqual("agent.alpha", copy.EventLedger[0].EvidenceSubjectId);
                Assert.AreEqual("agent.beta", copy.EventLedger[0].EvidenceObjectId);
                Assert.AreEqual(
                    "evidence-source.event.first",
                    copy.EventLedger[0].EvidenceSourceId);
                Assert.AreEqual(
                    "evidence-belief.event.first",
                    copy.EventLedger[0].EvidenceBeliefId);
                Assert.AreEqual(
                    "suppression.event.first",
                    copy.EventLedger[0].EvidenceSuppressedByAgentId);
                Assert.AreEqual(81, copy.EventLedger[0].EvidenceReliability);
                Assert.AreEqual(EvidenceVisibility.OfficialRecord, copy.EventLedger[0].Visibility);
                Assert.AreEqual(2, copy.EventLedger[0].Deltas.Count);
                Assert.AreEqual("agent.alpha", copy.EventLedger[0].Deltas[0].EntityId);
                Assert.AreEqual("field.event.first.first", copy.EventLedger[0].Deltas[0].FieldId);
                Assert.AreEqual(7, copy.EventLedger[0].Deltas[0].Before);
            Assert.AreEqual(8, copy.EventLedger[0].Deltas[0].After);
        }
    }
}
