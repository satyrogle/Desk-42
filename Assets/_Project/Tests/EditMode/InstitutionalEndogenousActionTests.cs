using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalEndogenousActionTests
    {
        [Test]
        public void Lie_RequiresAssertionInconsistentWithActorsStrongBelief()
        {
            AgentState actor = Agent("agent.actor", 0);
            actor.Beliefs.Add(Belief(
                "belief.observed",
                "resource-was-taken",
                "agent.subject",
                confidence: 90));
            LieOpportunity opportunity = LieOpportunity(actor, "resource-was-taken");

            AgentDecision decision = Decide(actor, new SimulationInput
            {
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
                LieOpportunities = new List<LieOpportunity> { opportunity },
            });

            Assert.AreEqual(SocietyActionKind.Idle, decision.Action,
                "Repeating a believed proposition is not a lie candidate.");

            opportunity.AssertionPropositionId = "resource-was-not-taken";
            opportunity.UtilityBonus = 100;
            decision = Decide(actor, new SimulationInput
            {
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
                LieOpportunities = new List<LieOpportunity> { opportunity },
            });
            Assert.AreEqual(SocietyActionKind.Lie, decision.Action);
            Assert.AreEqual("belief.observed", decision.SubjectBeliefId);
            Assert.IsTrue(decision.Reasons.Any(reason =>
                reason.ReasonId == "belief.confidence" && reason.ScoreDelta > 0));
        }

        [Test]
        public void Lie_UpdatesListenersThroughSourceEvaluation_NotDirectAssignment()
        {
            SocietyState trusted = Society(
                Agent("agent.speaker", 0),
                Agent("agent.listener", 1));
            SocietyState distrusted = SocietyStateDeepCopy.Copy(trusted);
            ConfigureLiePair(trusted, listenerTrust: 90);
            ConfigureLiePair(distrusted, listenerTrust: 10);
            SimulationInput trustedInput = LieInput(trusted.GetAgent("agent.speaker"));
            SimulationInput distrustedInput = LieInput(distrusted.GetAgent("agent.speaker"));

            SimulationStepResult trustedStep = new SocietySimulation().Advance(
                trusted, trustedInput);
            SimulationStepResult distrustedStep = new SocietySimulation().Advance(
                distrusted, distrustedInput);

            SocietyEvent trustedAssertion = trustedStep.Events.Single(value =>
                value.Kind == SocietyEventKind.AssertionMade);
            SocietyEvent distrustedAssertion = distrustedStep.Events.Single(value =>
                value.Kind == SocietyEventKind.AssertionMade);
            BeliefState trustedBelief = trusted.GetAgent("agent.listener").Beliefs.Single(value =>
                value.SourceId == "agent.speaker" &&
                value.PropositionId == "resource-was-not-taken");
            BeliefState distrustedBelief = distrusted.GetAgent("agent.listener").Beliefs.Single(value =>
                value.SourceId == "agent.speaker" &&
                value.PropositionId == "resource-was-not-taken");

            Assert.Greater(trustedBelief.Confidence, distrustedBelief.Confidence);
            Assert.AreNotEqual(90, trustedBelief.Confidence,
                "The source actor's belief confidence must not be assigned directly.");
            Assert.IsTrue(trustedAssertion.Deltas.Any(value =>
                value.FieldId.Contains("source-evaluated-confidence")));
            Assert.IsTrue(distrustedAssertion.Deltas.Any(value =>
                value.FieldId.Contains("source-evaluated-confidence")));
        }

        [Test]
        public void Steal_FromGeneratedOpportunity_ChangesPossessionButNotOwnership()
        {
            AgentState actor = Agent("agent.need", 0);
            actor.GetNeed(NeedKind.Health).Pressure = 100;
            actor.Disposition.RiskTolerance = 100;
            SocietyState society = Society(actor);
            InstitutionalMaterialWorld world = WorldWithMedicine(actor.StableId);
            var input = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(society, world, input);

            Assert.AreEqual(1, input.StealOpportunities.Count);
            Assert.AreEqual("resource.medicine", input.StealOpportunities[0].ResourceId);
            SimulationStepResult step = new EndogenousSocietyStepService().Advance(
                society, world, input);

            Assert.AreEqual(SocietyActionKind.Steal, step.Decisions.Single().Action);
            Assert.AreEqual(actor.StableId,
                world.GetResource("resource.medicine").PhysicalHolderId);
            Assert.AreEqual("clinic",
                world.GetOfficialOwnership("resource.medicine").RegisteredOwnerId);
            Assert.AreEqual(MaterialWorldEventKind.PossessionTransferred,
                world.EventLedger.Single().Kind);
        }

        [Test]
        public void StealOpportunity_DisappearsWhenMaterialAccessIsRemoved()
        {
            AgentState actor = Agent("agent.need", 0);
            SocietyState society = Society(actor);
            InstitutionalMaterialWorld world = WorldWithMedicine(actor.StableId);
            world.AccessGrants[0].Active = false;
            var input = QuietInput();

            EndogenousActionOpportunityBuilder.Populate(society, world, input);

            Assert.IsEmpty(input.StealOpportunities);
            Assert.AreEqual("clinic",
                world.GetResource("resource.medicine").PhysicalHolderId);
        }

        [Test]
        public void Retaliate_RequiresPerceivedPriorActionAndActivePowerRelationship()
        {
            AgentState supervisor = Agent("agent.supervisor", 0);
            AgentState worker = Agent("agent.worker", 1);
            supervisor.Relationships.Add(Relationship("agent.worker", fear: 60));
            worker.Relationships.Add(Relationship("agent.supervisor"));
            SocietyState society = Society(supervisor, worker);
            InstitutionalMaterialWorld world = RetaliationWorld();
            var input = QuietInput();

            EndogenousActionOpportunityBuilder.Populate(society, world, input);
            Assert.IsEmpty(input.RetaliationOpportunities,
                "Power without a perceived prior action must not create retaliation.");

            supervisor.Beliefs.Add(Belief(
                "belief.leak",
                EndogenousActionOpportunityBuilder.PerceivedAdverseActionProposition,
                worker.StableId,
                confidence: 90,
                emotionalWeight: 100));
            EndogenousActionOpportunityBuilder.Populate(society, world, input);
            Assert.AreEqual(1, input.RetaliationOpportunities.Count);

            world.AuthorityGrants[0].Active = false;
            EndogenousActionOpportunityBuilder.Populate(society, world, input);
            Assert.IsEmpty(input.RetaliationOpportunities,
                "A belief without current power must not create retaliation.");
        }

        [Test]
        public void Retaliate_SelectedFromBelief_RevokesAccessThroughAuthority()
        {
            AgentState supervisor = Agent("agent.supervisor", 0);
            AgentState worker = Agent("agent.worker", 1);
            supervisor.Disposition.Duty = 100;
            supervisor.Relationships.Add(Relationship(worker.StableId, fear: 60));
            worker.Relationships.Add(Relationship(supervisor.StableId, trust: 70));
            supervisor.Beliefs.Add(Belief(
                "belief.leak",
                EndogenousActionOpportunityBuilder.PerceivedAdverseActionProposition,
                worker.StableId,
                confidence: 100,
                emotionalWeight: 100));
            SocietyState society = Society(supervisor, worker);
            InstitutionalMaterialWorld world = RetaliationWorld();
            var input = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(society, world, input);
            input.RetaliationOpportunities[0].UtilityBonus = 100;

            SimulationStepResult step = new EndogenousSocietyStepService().Advance(
                society, world, input);

            AgentDecision selected = step.Decisions.Single(value =>
                value.ActorId == supervisor.StableId);
            Assert.AreEqual(SocietyActionKind.Retaliate, selected.Action);
            Assert.IsFalse(world.GetAccessGrant("access.worker.floor").Active);
            MaterialWorldEvent authorityEvent = world.EventLedger.Single();
            Assert.AreEqual(MaterialWorldEventKind.AccessChanged, authorityEvent.Kind);
            Assert.AreEqual("belief.leak", selected.SubjectBeliefId);
        }

        [Test]
        public void Organise_RequiresMultipleCompatibleAutonomousActions()
        {
            AgentState first = Organiser("agent.first", 0);
            AgentState second = Organiser("agent.second", 1);
            SocietyState oneActorSociety = Society(first, Agent("agent.second", 1));
            InstitutionalMaterialWorld oneActorWorld = OrganisationWorld(
                "agent.first", "agent.second");
            var oneActorInput = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(
                oneActorSociety, oneActorWorld, oneActorInput);
            new EndogenousSocietyStepService().Advance(
                oneActorSociety, oneActorWorld, oneActorInput);
            Assert.IsEmpty(oneActorWorld.CollectiveCommitments,
                "One organising action is a proposal, not an instant faction.");

            SocietyState twoActorSociety = Society(first, second);
            InstitutionalMaterialWorld twoActorWorld = OrganisationWorld(
                "agent.first", "agent.second");
            var twoActorInput = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(
                twoActorSociety, twoActorWorld, twoActorInput);
            SimulationStepResult step = new EndogenousSocietyStepService().Advance(
                twoActorSociety, twoActorWorld, twoActorInput);

            Assert.AreEqual(2, step.Decisions.Count(value =>
                value.Action == SocietyActionKind.Organise));
            CollectiveCommitmentState collective =
                twoActorWorld.CollectiveCommitments.Single();
            CollectionAssert.AreEquivalent(
                new[] { "agent.first", "agent.second" },
                collective.MemberAgentIds);
            Assert.AreEqual(2, collective.FormationCauseEventIds.Count);
        }

        [Test]
        public void VariationKeyed_CannotOverturnMeaningfulUtilityDifference()
        {
            AgentState actor = Agent("agent.margin", 0);
            actor.GetNeed(NeedKind.Health).Pressure = 100;
            actor.Disposition.RiskTolerance = 100;
            var input = QuietInput();
            input.StealOpportunities.Add(new StealOpportunity
            {
                OpportunityId = "opportunity.high-margin",
                ResourceId = "resource.test",
                ExpectedPhysicalHolderId = "holder",
                NewLocationContextId = "actor-controlled",
                AccessGrantId = "access.projected",
                ReliefNeed = NeedKind.Health,
                UtilityBonus = 100,
                EligibleActorIds = new List<string> { actor.StableId },
            });

            for (int ordinal = 0; ordinal < 128; ordinal++)
            {
                actor.SimulationOrdinal = ordinal;
                AgentDecision decision = Decide(actor, input, variationAmplitude: 10);
                Assert.AreEqual(SocietyActionKind.Steal, decision.Action,
                    $"Tie-scale variation overturned a meaningful margin at ordinal {ordinal}.");
            }
        }

        [Test]
        public void DecisionSnapshot_DeepCopiesAllEndogenousOpportunities()
        {
            AgentState actor = Agent("agent.snapshot", 0);
            actor.Beliefs.Add(Belief(
                "belief.snapshot", "believed", "agent.subject", confidence: 90));
            var input = QuietInput();
            input.LieOpportunities.Add(LieOpportunity(actor, "asserted"));
            input.StealOpportunities.Add(new StealOpportunity
            {
                OpportunityId = "steal.snapshot",
                ResourceId = "resource.snapshot",
                ExpectedPhysicalHolderId = "holder",
                NewLocationContextId = "location",
                AccessGrantId = "access.snapshot",
                EligibleActorIds = new List<string> { actor.StableId },
            });
            input.RetaliationOpportunities.Add(new RetaliationOpportunity
            {
                OpportunityId = "retaliate.snapshot",
                TargetAgentId = "agent.target",
                PerceivedPriorActionBeliefId = "belief.snapshot",
                AuthorityGrantId = "authority.snapshot",
                AffectedAccessGrantId = "access.target",
                EligibleActorIds = new List<string> { actor.StableId },
            });
            input.OrganiseOpportunities.Add(new OrganiseOpportunity
            {
                OpportunityId = "organise.snapshot",
                CollectiveCommitmentId = "collective.snapshot",
                IssueId = "issue.snapshot",
                IntentionId = "intention.snapshot",
                CommunicationContextId = "room.snapshot",
                EligibleActorIds = new List<string> { actor.StableId },
            });

            AgentDecision decision = Decide(actor, input);
            input.LieOpportunities[0].AssertionPropositionId = "mutated";
            input.StealOpportunities[0].EligibleActorIds[0] = "mutated";
            input.RetaliationOpportunities[0].AuthorityGrantId = "mutated";
            input.OrganiseOpportunities[0].IssueId = "mutated";

            Assert.AreEqual("asserted",
                decision.InputSnapshot.LieOpportunities[0].AssertionPropositionId);
            Assert.AreEqual(actor.StableId,
                decision.InputSnapshot.StealOpportunities[0].EligibleActorIds[0]);
            Assert.AreEqual("authority.snapshot",
                decision.InputSnapshot.RetaliationOpportunities[0].AuthorityGrantId);
            Assert.AreEqual("issue.snapshot",
                decision.InputSnapshot.OrganiseOpportunities[0].IssueId);
        }

        private static void ConfigureLiePair(SocietyState society, int listenerTrust)
        {
            AgentState speaker = society.GetAgent("agent.speaker");
            AgentState listener = society.GetAgent("agent.listener");
            speaker.Disposition.Candour = 0;
            speaker.GetNeed(NeedKind.Safety).Pressure = 100;
            speaker.Beliefs.Add(Belief(
                "belief.observed",
                "resource-was-taken",
                "agent.subject",
                confidence: 90));
            speaker.Relationships.Add(Relationship(listener.StableId));
            listener.Relationships.Add(Relationship(speaker.StableId, trust: listenerTrust));
        }

        private static SimulationInput LieInput(AgentState speaker)
        {
            var input = QuietInput();
            LieOpportunity opportunity = LieOpportunity(speaker, "resource-was-not-taken");
            opportunity.UtilityBonus = 100;
            opportunity.AudienceAgentIds.Add("agent.listener");
            input.LieOpportunities.Add(opportunity);
            return input;
        }

        private static LieOpportunity LieOpportunity(
            AgentState actor,
            string assertionPropositionId)
        {
            return new LieOpportunity
            {
                OpportunityId = "lie.test",
                BeliefId = actor.Beliefs[0].BeliefId,
                AssertionPropositionId = assertionPropositionId,
                AssertionSubjectId = actor.Beliefs[0].SubjectId,
                AssertionObjectId = actor.Beliefs[0].ObjectId,
                ContextId = "room.shared",
                EligibleActorIds = new List<string> { actor.StableId },
            };
        }

        private static InstitutionalMaterialWorld WorldWithMedicine(string actorId)
        {
            var world = new InstitutionalMaterialWorld();
            world.Resources.Add(new MaterialResourceState
            {
                ResourceId = "resource.medicine",
                ResourceKindId = "medicine",
                Quantity = 1,
                PhysicalHolderId = "clinic",
                LocationContextId = "clinic.store",
            });
            world.OfficialOwnerships.Add(new OfficialOwnershipState
            {
                OwnershipRecordId = "ownership.medicine",
                ResourceId = "resource.medicine",
                RegisteredOwnerId = "clinic",
                OwnershipSourceId = "record.inventory",
                RecognitionTick = 0,
            });
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = "access.medicine",
                AgentId = actorId,
                AccessKindId = EndogenousActionOpportunityBuilder.MaterialPossessionAccessKind,
                TargetId = "resource.medicine",
                SourceRecordId = "record.shift",
                ValidFromTick = 0,
            });
            return world;
        }

        private static InstitutionalMaterialWorld RetaliationWorld()
        {
            var world = new InstitutionalMaterialWorld();
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = "access.worker.floor",
                AgentId = "agent.worker",
                AccessKindId = "workplace",
                TargetId = "floor.1",
                SourceRecordId = "record.employment",
                ValidFromTick = 0,
            });
            world.AuthorityGrants.Add(new MaterialAuthorityGrantState
            {
                GrantId = "authority.supervisor.worker",
                AgentId = "agent.supervisor",
                Kind = MaterialAuthorityKind.RemoveAccess,
                TargetId = "agent.worker",
                SourceRecordId = "record.supervisory-authority",
                ValidFromTick = 0,
            });
            return world;
        }

        private static InstitutionalMaterialWorld OrganisationWorld(params string[] agentIds)
        {
            var world = new InstitutionalMaterialWorld();
            for (int i = 0; i < agentIds.Length; i++)
            {
                world.AccessGrants.Add(new MaterialAccessGrantState
                {
                    GrantId = $"access.communication.{agentIds[i]}",
                    AgentId = agentIds[i],
                    AccessKindId = EndogenousActionOpportunityBuilder.CommunicationAccessKind,
                    TargetId = "room.break",
                    SourceRecordId = "record.site-access",
                    ValidFromTick = 0,
                });
            }
            return world;
        }

        private static AgentState Organiser(string id, int ordinal)
        {
            AgentState actor = Agent(id, ordinal);
            actor.Disposition.Solidarity = 100;
            actor.Disposition.RiskTolerance = 100;
            actor.GetNeed(NeedKind.Belonging).Pressure = 80;
            actor.Commitments.Add(new CommitmentState
            {
                CommitmentId = $"commitment.grievance.{id}",
                Kind = "grievance",
                TargetId = "unsafe-shift",
                Strength = 100,
            });
            return actor;
        }

        private static SocietyState Society(params AgentState[] agents)
        {
            return new SocietyState
            {
                MasterSeed = 424242,
                Regime = new InstitutionalRegimeState
                {
                    DecisionVariationAmplitude = 0,
                    WorkReward = 0,
                    AidEffectiveness = 0,
                    DisclosureProtection = 0,
                    RetaliationRisk = 50,
                    AppealAccessibility = 0,
                },
                Agents = agents.ToList(),
            };
        }

        private static AgentState Agent(string id, int ordinal)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = "portrait.generic",
                DisplayName = id,
                SpeciesId = "species.generic",
                HouseholdId = $"household.{id}",
                EmployerId = "employer.generic",
                Disposition = new AgentDispositionState(),
                Standing = new InstitutionalStandingState(),
            };
            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = kind, Pressure = 0 });
            return agent;
        }

        private static RelationshipState Relationship(
            string targetId,
            int trust = 0,
            int fear = 0)
        {
            return new RelationshipState
            {
                TargetAgentId = targetId,
                Trust = trust,
                Fear = fear,
            };
        }

        private static BeliefState Belief(
            string id,
            string proposition,
            string subject,
            int confidence,
            int emotionalWeight = 0)
        {
            return new BeliefState
            {
                BeliefId = id,
                PropositionId = proposition,
                SubjectId = subject,
                ObjectId = "resource.medicine",
                SourceId = "observation.personal",
                Confidence = confidence,
                Secrecy = 60,
                EmotionalWeight = emotionalWeight,
            };
        }

        private static SimulationInput QuietInput()
        {
            return new SimulationInput
            {
                IncidentId = "endogenous-pulse",
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
            };
        }

        private static AgentDecision Decide(
            AgentState actor,
            SimulationInput input,
            int variationAmplitude = 0)
        {
            return new AgentDecisionEngine().Decide(new AgentDecisionContext
            {
                MasterSeed = 424242,
                Tick = 1,
                Actor = AgentPerception.Capture(actor),
                PerceivedAgentIds = actor.Relationships
                    .Select(value => value.TargetAgentId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList(),
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 0,
                    AidEffectiveness = 0,
                    DisclosureProtection = 0,
                    RetaliationRisk = 50,
                    AppealAccessibility = 0,
                    DecisionVariationAmplitude = variationAmplitude,
                },
                Input = input,
            });
        }
    }
}
