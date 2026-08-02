using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Desk42.Core;
using Desk42.Institutional;
using Desk42.Institutional.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalAgentSimulationTests
    {
        [Test]
        public void Population_EightProfiles_MaintainsInvariantsAcrossTicks()
        {
            SocietyState state = PrototypePopulationFactory.Create(420042);
            var expectedIds = new HashSet<string>(state.Agents.Select(agent => agent.StableId));
            var simulation = new SocietySimulation();

            Assert.AreEqual(PrototypePopulationFactory.PrototypePopulationSize, state.Agents.Count);
            Assert.AreEqual(PrototypePopulationFactory.PrototypePopulationSize, expectedIds.Count);

            for (int tick = 1; tick <= 32; tick++)
            {
                SimulationStepResult result = simulation.Advance(state, new SimulationInput());

                Assert.AreEqual(tick, result.Tick);
                Assert.AreEqual(PrototypePopulationFactory.PrototypePopulationSize, state.Agents.Count);
                Assert.AreEqual(PrototypePopulationFactory.PrototypePopulationSize, result.Decisions.Count);
                CollectionAssert.AreEquivalent(expectedIds, state.Agents.Select(agent => agent.StableId));
                CollectionAssert.AreEquivalent(expectedIds, result.Decisions.Select(decision => decision.ActorId));
                AssertPopulationInvariants(state);

                foreach (AgentDecision decision in result.Decisions)
                {
                    Assert.IsTrue(
                        result.Events.Any(societyEvent => societyEvent.CauseDecisionId == decision.DecisionId),
                        $"{decision.DecisionId} must leave a material record, evidence, or observable absence.");
                }
            }
        }

        [Test]
        public void Simulation_SameSeedAndInputs_ProducesIdenticalDecisionTrace()
        {
            SocietyState first = PrototypePopulationFactory.Create(10101);
            SocietyState second = PrototypePopulationFactory.Create(10101);
            var firstSimulation = new SocietySimulation();
            var secondSimulation = new SocietySimulation();

            SeedEngine.Init(9090);
            for (int tick = 0; tick < 48; tick++)
            {
                SimulationStepResult firstStep = firstSimulation.Advance(first, new SimulationInput());

                // The institutional simulation must be independent from legacy mutable RNG streams.
                for (int draw = 0; draw < tick + 1; draw++)
                    SeedEngine.Next(SeedStream.ClaimQueue, 100000);

                SimulationStepResult secondStep = secondSimulation.Advance(second, new SimulationInput());
                Assert.AreEqual(CanonicalStep(firstStep), CanonicalStep(secondStep));
            }

            Assert.AreEqual(
                JsonConvert.SerializeObject(first),
                JsonConvert.SerializeObject(second));
        }

        [Test]
        public void Population_OrderPermutation_ProducesIsomorphicOutcome()
        {
            SocietyState ordered = PrototypePopulationFactory.Create(77331);
            SocietyState reversed = PrototypePopulationFactory.Create(77331);
            reversed.Agents.Reverse();
            foreach (AgentState agent in reversed.Agents)
            {
                agent.Needs.Reverse();
                agent.Commitments.Reverse();
                agent.Relationships.Reverse();
                agent.Beliefs.Reverse();
                agent.Standing.OfficialStatuses.Reverse();
                agent.AnomalyRules.Reverse();
            }
            var firstSimulation = new SocietySimulation();
            var secondSimulation = new SocietySimulation();

            for (int tick = 0; tick < 24; tick++)
            {
                SimulationStepResult first = firstSimulation.Advance(ordered, new SimulationInput());
                SimulationStepResult second = secondSimulation.Advance(reversed, new SimulationInput());
                Assert.AreEqual(CanonicalStep(first), CanonicalStep(second));
            }

            CanonicalizeCollectionOrder(ordered);
            CanonicalizeCollectionOrder(reversed);
            Assert.AreEqual(
                JsonConvert.SerializeObject(ordered),
                JsonConvert.SerializeObject(reversed));
        }

        [Test]
        public void EquivalentProfileSubstitution_PreservesBehaviour()
        {
            SocietyState original = PrototypePopulationFactory.Create(87654);
            SocietyState substituted = PrototypePopulationFactory.Create(87654);
            const string originalId = "agent.mara-kest";
            const string substituteId = "agent.tovin-ash";
            AgentState replacement = substituted.GetAgent(originalId);
            RemapAgentId(substituted, originalId, substituteId);
            replacement.DisplayName = "Tovin Ash";
            replacement.PresentationId = "portrait.prototype-substitute";

            var firstSimulation = new SocietySimulation();
            var secondSimulation = new SocietySimulation();
            for (int tick = 0; tick < 12; tick++)
            {
                SimulationStepResult first = firstSimulation.Advance(original, new SimulationInput());
                SimulationStepResult second = secondSimulation.Advance(substituted, new SimulationInput());
                Assert.AreEqual(
                    CanonicalCausalStep(first, null, null),
                    CanonicalCausalStep(second, substituteId, originalId));
            }

            RemapAgentId(substituted, substituteId, originalId);
            AgentState normalizedReplacement = substituted.GetAgent(originalId);
            AgentState originalAgent = original.GetAgent(originalId);
            normalizedReplacement.DisplayName = originalAgent.DisplayName;
            normalizedReplacement.PresentationId = originalAgent.PresentationId;
            CanonicalizeCollectionOrder(original);
            CanonicalizeCollectionOrder(substituted);
            Assert.AreEqual(
                JsonConvert.SerializeObject(original),
                JsonConvert.SerializeObject(substituted));
        }

        [Test]
        public void InstitutionalRegime_SamePersonAndSeed_ChangesDecisionForTraceableReasons()
        {
            AgentState protectedActor = DecisionFixtureAgent("agent.regime-test");
            protectedActor.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.regime-sensitive",
                PropositionId = "claim.regime-sensitive",
                SubjectId = "agent.subject",
                SourceId = "memory.personal",
                Confidence = 50,
                Secrecy = 50,
            });
            AgentState exposedActor = DecisionFixtureAgent("agent.regime-test");
            exposedActor.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.regime-sensitive",
                PropositionId = "claim.regime-sensitive",
                SubjectId = "agent.subject",
                SourceId = "memory.personal",
                Confidence = 50,
                Secrecy = 50,
            });

            AgentDecisionContext protectedContext = DecisionContext(
                protectedActor, 31337, disclosureRequested: true);
            protectedContext.Regime.DisclosureProtection = 100;
            protectedContext.Regime.RetaliationRisk = 0;
            AgentDecisionContext exposedContext = DecisionContext(
                exposedActor, 31337, disclosureRequested: true);
            exposedContext.Regime.DisclosureProtection = 0;
            exposedContext.Regime.RetaliationRisk = 100;

            var engine = new AgentDecisionEngine();
            AgentDecision protectedDecision = engine.Decide(protectedContext);
            AgentDecision exposedDecision = engine.Decide(exposedContext);

            Assert.AreEqual(SocietyActionKind.Disclose, protectedDecision.Action);
            Assert.AreEqual(SocietyActionKind.Withhold, exposedDecision.Action);
            Assert.IsTrue(protectedDecision.Reasons.Any(reason =>
                reason.ReasonId == "regime.disclosure-protection" && reason.ScoreDelta > 0));
            Assert.IsTrue(exposedDecision.Reasons.Any(reason =>
                reason.ReasonId == "regime.retaliation-risk" && reason.ScoreDelta > 0));
        }

        [Test]
        public void RelevantBeliefPerturbation_ChangesDecisionAndExplainsWhy()
        {
            AgentState actor = DecisionFixtureAgent("agent.belief-test");
            actor.Disposition.Candour = 50;
            actor.Disposition.RiskTolerance = 50;
            actor.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.load-bearing",
                PropositionId = "claim.test",
                SubjectId = "agent.subject",
                ObjectId = "object.baseline",
                SourceId = "source.baseline",
                Confidence = 0,
                Secrecy = 50,
                EmotionalWeight = 0,
            });

            var engine = new AgentDecisionEngine();
            AgentDecisionContext context = DecisionContext(actor, 5150, disclosureRequested: true);
            AgentDecision baseline = engine.Decide(context);
            Assert.AreEqual(SocietyActionKind.Withhold, baseline.Action);

            actor.Beliefs[0].Confidence = 100;
            context = DecisionContext(actor, 5150, disclosureRequested: true);
            AgentDecision changed = engine.Decide(context);
            Assert.AreEqual(SocietyActionKind.Disclose, changed.Action);
            Assert.IsTrue(changed.Reasons.Any(reason =>
                reason.ReasonId == "belief.confidence" &&
                reason.SourceId == "belief.load-bearing" &&
                reason.ScoreDelta == 33));
            AssertTraceAddsUp(baseline);
            AssertTraceAddsUp(changed);

            actor.Beliefs[0].Confidence = 0;
            actor.Beliefs[0].SourceId = "source.unrelated-presentation-change";
            context = DecisionContext(actor, 5150, disclosureRequested: true);
            AgentDecision unrelated = engine.Decide(context);
            Assert.AreEqual(CanonicalDecision(baseline), CanonicalDecision(unrelated),
                "A field not read by this decision rule must not create a false divergence.");
        }

        [Test]
        public void DecisionContext_CapturesPerceptionBeforeWorldStateMutates()
        {
            AgentState actor = DecisionFixtureAgent("agent.perception-snapshot");
            actor.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.snapshot",
                PropositionId = "claim.snapshot",
                SubjectId = actor.StableId,
                Confidence = 0,
                Secrecy = 50,
            });
            AgentDecisionContext captured = DecisionContext(actor, 6161, disclosureRequested: true);

            actor.Beliefs[0].Confidence = 100;
            var engine = new AgentDecisionEngine();
            AgentDecision fromCapturedPerception = engine.Decide(captured);
            AgentDecision fromFreshPerception = engine.Decide(
                DecisionContext(actor, 6161, disclosureRequested: true));

            Assert.AreEqual(SocietyActionKind.Withhold, fromCapturedPerception.Action);
            Assert.AreEqual(SocietyActionKind.Disclose, fromFreshPerception.Action);
        }

        [Test]
        public void RelevantRelationshipPerturbation_ChangesActionTargetAndExplainsWhy()
        {
            AgentState actor = DecisionFixtureAgent("agent.relationship-test");
            actor.Disposition.Solidarity = 40;
            actor.Relationships.Add(new RelationshipState
            {
                TargetAgentId = "agent.target-a",
                Trust = 50,
                Obligation = 70,
                Attachment = 30,
                PerceivedNeed = NeedKind.Safety,
                PerceivedNeedPressure = 30,
            });
            actor.Relationships.Add(new RelationshipState
            {
                TargetAgentId = "agent.target-b",
                Trust = 50,
                Obligation = 5,
                Attachment = 30,
                PerceivedNeed = NeedKind.Safety,
                PerceivedNeedPressure = 30,
            });

            var engine = new AgentDecisionEngine();
            AgentDecisionContext context = DecisionContext(
                actor,
                8181,
                disclosureRequested: false,
                "agent.target-a",
                "agent.target-b");
            AgentDecision baseline = engine.Decide(context);
            Assert.AreEqual(SocietyActionKind.Help, baseline.Action);
            Assert.AreEqual("agent.target-a", baseline.TargetId);

            actor.Relationships[1].Obligation = 100;
            context = DecisionContext(
                actor,
                8181,
                disclosureRequested: false,
                "agent.target-a",
                "agent.target-b");
            AgentDecision changed = engine.Decide(context);
            Assert.AreEqual(SocietyActionKind.Help, changed.Action);
            Assert.AreEqual("agent.target-b", changed.TargetId);
            Assert.IsTrue(changed.Reasons.Any(reason =>
                reason.ReasonId == "relationship.obligation" &&
                reason.SourceId == "agent.target-b" &&
                reason.ScoreDelta == 50));
            AssertTraceAddsUp(baseline);
            AssertTraceAddsUp(changed);
        }

        [Test]
        public void UnseenValidProfile_UsesGenericDecisionPipeline()
        {
            AgentState registered = DecisionFixtureAgent("agent.registered-profile");
            AgentState unseen = DecisionFixtureAgent("agent.never-registered");
            unseen.DisplayName = "A Person Not In The Factory";
            unseen.PresentationId = "portrait.unknown-to-code";
            registered.Standing.SetRecognised("adverse-decision", true);
            unseen.Standing.SetRecognised("adverse-decision", true);
            registered.GetNeed(NeedKind.Autonomy).Pressure = 90;
            unseen.GetNeed(NeedKind.Autonomy).Pressure = 90;

            var engine = new AgentDecisionEngine();
            AgentDecision expected = engine.Decide(
                DecisionContext(registered, 9191, disclosureRequested: false));
            AgentDecision decision = engine.Decide(
                DecisionContext(unseen, 9191, disclosureRequested: false));

            Assert.AreEqual(SocietyActionKind.Appeal, expected.Action);
            Assert.AreEqual(expected.Action, decision.Action);
            Assert.AreEqual(expected.TargetId, decision.TargetId);
            CollectionAssert.AreEqual(
                expected.Reasons.Where(reason => reason.ReasonId != "variation.keyed")
                    .Select(reason => reason.ReasonId),
                decision.Reasons.Where(reason => reason.ReasonId != "variation.keyed")
                    .Select(reason => reason.ReasonId));
            Assert.AreEqual("agent.never-registered", decision.ActorId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(decision.CandidateId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(decision.DecisionId));
            Assert.IsNotNull(decision.Reasons);
            Assert.Greater(decision.Reasons.Count, 0);
        }

        [Test]
        public void PopulationSaveRoundTrip_PreservesDeterministicContinuation()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "desk42-institutional-tests",
                Guid.NewGuid().ToString("N"));

            try
            {
                SocietyState uninterrupted = PrototypePopulationFactory.Create(24680);
                SocietyState toSave = PrototypePopulationFactory.Create(24680);
                var firstSimulation = new SocietySimulation();
                var secondSimulation = new SocietySimulation();

                for (int tick = 0; tick < 12; tick++)
                {
                    firstSimulation.Advance(uninterrupted, new SimulationInput());
                    secondSimulation.Advance(toSave, new SimulationInput());
                }

                var store = new InstitutionalSocietyStore(directory);
                Assert.IsTrue(store.Save(toSave));

                // Cold restart: no store or simulation instance survives the save boundary.
                var restartedStore = new InstitutionalSocietyStore(directory);
                SocietyState loaded = restartedStore.Load();
                Assert.IsNotNull(loaded);
                Assert.AreEqual(
                    JsonConvert.SerializeObject(toSave),
                    JsonConvert.SerializeObject(loaded),
                    "Every persisted simulation field must survive, not only the dominant decisions.");

                var restartedControlSimulation = new SocietySimulation();
                var restartedLoadedSimulation = new SocietySimulation();

                for (int tick = 0; tick < 20; tick++)
                {
                    SimulationStepResult expected = restartedControlSimulation.Advance(
                        uninterrupted, new SimulationInput());
                    SimulationStepResult actual = restartedLoadedSimulation.Advance(
                        loaded, new SimulationInput());
                    Assert.AreEqual(CanonicalStep(expected), CanonicalStep(actual));
                }

                Assert.AreEqual(
                    JsonConvert.SerializeObject(uninterrupted),
                    JsonConvert.SerializeObject(loaded));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void OfficialStatus_ChangesBoundedAnomalyEffectWithoutGlobalMeter()
        {
            SocietyState unrecognised = PrototypePopulationFactory.Create(333);
            SocietyState recognised = PrototypePopulationFactory.Create(333);
            recognised.GetAgent("agent.elias-venn").Standing.SetRecognised("identity-continuity", true);

            int unrecognisedBefore = unrecognised.GetAgent("agent.elias-venn").GetNeed(NeedKind.Health).Pressure;
            int recognisedBefore = recognised.GetAgent("agent.elias-venn").GetNeed(NeedKind.Health).Pressure;
            SimulationStepResult first = new SocietySimulation().Advance(unrecognised, new SimulationInput());
            SimulationStepResult second = new SocietySimulation().Advance(recognised, new SimulationInput());
            int unrecognisedAfter = unrecognised.GetAgent("agent.elias-venn").GetNeed(NeedKind.Health).Pressure;
            int recognisedAfter = recognised.GetAgent("agent.elias-venn").GetNeed(NeedKind.Health).Pressure;

            Assert.AreEqual(4, unrecognisedAfter - unrecognisedBefore);
            Assert.AreEqual(-1, recognisedAfter - recognisedBefore);
            Assert.IsTrue(first.Events.Any(societyEvent =>
                societyEvent.Kind == SocietyEventKind.AnomalyStatusResponse &&
                societyEvent.ActorId == "agent.elias-venn" &&
                societyEvent.EvidenceId == "observable.elias-phase-instability"));
            Assert.IsTrue(second.Events.Any(societyEvent =>
                societyEvent.Kind == SocietyEventKind.AnomalyStatusResponse &&
                societyEvent.ActorId == "agent.elias-venn"));
        }

        [Test]
        public void DuplicateAnomalyTrait_IsRejectedBeforeItCanCollide()
        {
            SocietyState state = PrototypePopulationFactory.Create(7331);
            AgentState elias = state.GetAgent("agent.elias-venn");
            elias.AnomalyRules.Add(new AnomalyStatusRule
            {
                TraitId = "anomaly.superseded-body",
                RequiredOfficialStatusId = "identity-continuity",
                AffectedNeed = NeedKind.Health,
                RecognisedPressureDelta = -1,
                UnrecognisedPressureDelta = 1,
                MinimumTicksBetweenActivations = 3,
                ObservableEffectId = "observable.duplicate",
            });

            Assert.Throws<InvalidOperationException>(() =>
                new SocietySimulation().Advance(state, new SimulationInput()));
        }

        [Test]
        public void IncidentScopedWithholding_CannotRepeatUntilTheIncidentChanges()
        {
            AgentState actor = DecisionFixtureAgent("agent.withhold-once");
            actor.Disposition.Candour = 0;
            actor.Disposition.RiskTolerance = 0;
            actor.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.withhold-once",
                PropositionId = "claim.withhold-once",
                SubjectId = actor.StableId,
                SourceId = "memory.private",
                Confidence = 0,
                Secrecy = 100,
            });
            var state = new SocietyState { MasterSeed = 8080 };
            state.Agents.Add(actor);
            var simulation = new SocietySimulation();
            var firstIncident = new SimulationInput
            {
                IncidentId = "incident.first",
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = true,
                AppealWindowOpen = false,
            };

            SimulationStepResult first = simulation.Advance(state, firstIncident);
            SimulationStepResult repeated = simulation.Advance(state, firstIncident);
            var nextIncident = new SimulationInput
            {
                IncidentId = "incident.second",
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = true,
                AppealWindowOpen = false,
            };
            SimulationStepResult reopened = simulation.Advance(state, nextIncident);

            Assert.AreEqual(SocietyActionKind.Withhold, first.Decisions.Single().Action);
            Assert.AreNotEqual(SocietyActionKind.Withhold, repeated.Decisions.Single().Action);
            Assert.AreEqual(SocietyActionKind.Withhold, reopened.Decisions.Single().Action);
        }

        [Test]
        public void Help_UsesTheActorsPerceivedNeedRatherThanTargetPrivateState()
        {
            AgentState helper = DecisionFixtureAgent("agent.helper");
            AgentState target = DecisionFixtureAgent("agent.target");
            helper.SimulationOrdinal = 0;
            target.SimulationOrdinal = 1;
            helper.Disposition.Solidarity = 100;
            helper.Relationships.Add(new RelationshipState
            {
                TargetAgentId = target.StableId,
                Trust = 80,
                Obligation = 100,
                Attachment = 80,
                PerceivedNeed = NeedKind.Autonomy,
                PerceivedNeedPressure = 100,
            });
            target.GetNeed(NeedKind.Health).Pressure = 100;
            target.GetNeed(NeedKind.Autonomy).Pressure = 50;
            var state = new SocietyState { MasterSeed = 4545 };
            state.Agents.Add(helper);
            state.Agents.Add(target);
            int healthBefore = target.GetNeed(NeedKind.Health).Pressure;
            int autonomyBefore = target.GetNeed(NeedKind.Autonomy).Pressure;

            SimulationStepResult result = new SocietySimulation().Advance(state, new SimulationInput
            {
                IncidentId = "incident.help",
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
            });

            AgentDecision decision = result.Decisions.Single(value => value.ActorId == helper.StableId);
            Assert.AreEqual(SocietyActionKind.Help, decision.Action);
            Assert.AreEqual(NeedKind.Autonomy, decision.IntendedNeed);
            Assert.AreEqual(healthBefore, target.GetNeed(NeedKind.Health).Pressure);
            Assert.AreEqual(autonomyBefore - 6, target.GetNeed(NeedKind.Autonomy).Pressure);
        }

        private static void AssertPopulationInvariants(SocietyState state)
        {
            Assert.DoesNotThrow(() => SocietyStateValidator.Validate(state));
            var ids = new HashSet<string>(state.Agents.Select(agent => agent.StableId), StringComparer.Ordinal);
            var eventIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (AgentState agent in state.Agents)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(agent.StableId));
                Assert.IsFalse(string.IsNullOrWhiteSpace(agent.DisplayName));
                Assert.AreEqual(Enum.GetValues(typeof(NeedKind)).Length, agent.Needs.Count);
                Assert.AreEqual(agent.Needs.Count, agent.Needs.Select(need => need.Kind).Distinct().Count());
                Assert.IsTrue(agent.Needs.All(need => need.Pressure >= 0 && need.Pressure <= 100));
                Assert.IsTrue(agent.Beliefs.All(belief =>
                    belief.Confidence >= 0 && belief.Confidence <= 100 &&
                    belief.Secrecy >= 0 && belief.Secrecy <= 100 &&
                    belief.EmotionalWeight >= 0 && belief.EmotionalWeight <= 100));

                var targets = new HashSet<string>(StringComparer.Ordinal);
                foreach (RelationshipState relationship in agent.Relationships)
                {
                    Assert.AreNotEqual(agent.StableId, relationship.TargetAgentId);
                    Assert.IsTrue(ids.Contains(relationship.TargetAgentId));
                    Assert.IsTrue(targets.Add(relationship.TargetAgentId),
                        $"Duplicate relationship {agent.StableId} -> {relationship.TargetAgentId}");
                }
            }

            foreach (SocietyEvent societyEvent in state.EventLedger)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(societyEvent.EventId));
                Assert.IsTrue(eventIds.Add(societyEvent.EventId), $"Duplicate event id {societyEvent.EventId}");
            }
            Assert.LessOrEqual(state.EventLedger.Count, SocietyState.MaximumEventHistory);
        }

        private static AgentState DecisionFixtureAgent(string id)
        {
            var actor = new AgentState
            {
                StableId = id,
                PresentationId = "portrait.fixture",
                DisplayName = "Fixture Person",
                SpeciesId = "species.fixture",
                InstitutionalTrust = 0,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = 50,
                    Candour = 50,
                    Solidarity = 0,
                    Duty = 0,
                    InstitutionalReliance = 0,
                },
                Standing = new InstitutionalStandingState
                {
                    CanWork = false,
                    CanSeekAid = false,
                    CanAppeal = true,
                    CanGiveEvidence = true,
                },
            };

            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
                actor.Needs.Add(new NeedState { Kind = kind, Pressure = 0 });
            return actor;
        }

        private static AgentDecisionContext DecisionContext(
            AgentState actor,
            int seed,
            bool disclosureRequested,
            params string[] perceivedAgentIds)
        {
            return new AgentDecisionContext
            {
                MasterSeed = seed,
                Tick = 1,
                Actor = AgentPerception.Capture(actor),
                PerceivedAgentIds = perceivedAgentIds ?? Array.Empty<string>(),
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 50,
                    AidEffectiveness = 50,
                    DisclosureProtection = 50,
                    RetaliationRisk = 50,
                    AppealAccessibility = 50,
                },
                Input = new SimulationInput
                {
                    WorkAvailable = false,
                    AidAvailable = false,
                    DisclosureRequested = disclosureRequested,
                    AppealWindowOpen = true,
                },
            };
        }

        private static string CanonicalStep(SimulationStepResult result)
        {
            var builder = new StringBuilder();
            builder.Append(result.Tick).Append('|');
            foreach (AgentDecision decision in result.Decisions.OrderBy(value => value.ActorId, StringComparer.Ordinal))
                builder.Append(CanonicalDecision(decision)).Append('|');
            foreach (SocietyEvent societyEvent in result.Events.OrderBy(value => value.EventId, StringComparer.Ordinal))
            {
                builder.Append(societyEvent.EventId).Append(':')
                    .Append(societyEvent.CauseDecisionId).Append(':')
                    .Append(societyEvent.IncidentId).Append(':')
                    .Append(societyEvent.Kind).Append(':')
                    .Append(societyEvent.ActorId).Append(':')
                    .Append(societyEvent.TargetId).Append(':')
                    .Append(societyEvent.EvidenceId).Append(':')
                    .Append(societyEvent.EvidencePropositionId).Append(':')
                    .Append(societyEvent.EvidenceSubjectId).Append(':')
                    .Append(societyEvent.EvidenceObjectId).Append(':')
                    .Append(societyEvent.EvidenceSourceId).Append(':')
                    .Append(societyEvent.Visibility);
                foreach (StateDelta delta in societyEvent.Deltas
                    .OrderBy(value => value.EntityId, StringComparer.Ordinal)
                    .ThenBy(value => value.FieldId, StringComparer.Ordinal))
                {
                    builder.Append('[').Append(delta.EntityId).Append(':')
                        .Append(delta.FieldId).Append(':')
                        .Append(delta.Before).Append('>').Append(delta.After).Append(']');
                }
                builder.Append('|');
            }
            return builder.ToString();
        }

        private static string CanonicalDecision(AgentDecision decision)
        {
            var builder = new StringBuilder();
            builder.Append(decision.Tick).Append(':')
                .Append(decision.ApplicationOrdinal).Append(':')
                .Append(decision.ActorId).Append(':')
                .Append(decision.Action).Append(':')
                .Append(decision.TargetId).Append(':')
                .Append(decision.SubjectBeliefId).Append(':')
                .Append(decision.IntendedNeed).Append(':')
                .Append(decision.CandidateId).Append(':')
                .Append(decision.Score);
            foreach (DecisionReason reason in decision.Reasons)
            {
                builder.Append('[').Append(reason.ReasonId).Append(':')
                    .Append(reason.SourceId).Append(':')
                    .Append(reason.ScoreDelta).Append(']');
            }
            return builder.ToString();
        }

        private static void AssertTraceAddsUp(AgentDecision decision)
        {
            Assert.AreEqual(
                decision.Score,
                decision.Reasons.Sum(reason => reason.ScoreDelta),
                $"Trace arithmetic must explain {decision.DecisionId} exactly.");
        }

        private static string CanonicalCausalStep(
            SimulationStepResult result,
            string substituteId,
            string originalId)
        {
            var builder = new StringBuilder();
            builder.Append(result.Tick).Append('|');
            foreach (AgentDecision decision in result.Decisions.OrderBy(
                value => Normalize(value.ActorId, substituteId, originalId),
                StringComparer.Ordinal))
            {
                builder.Append(Normalize(decision.ActorId, substituteId, originalId)).Append(':')
                    .Append(decision.Action).Append(':')
                    .Append(Normalize(decision.TargetId, substituteId, originalId)).Append(':')
                    .Append(decision.SubjectBeliefId).Append(':')
                    .Append(decision.IntendedNeed).Append(':')
                    .Append(Normalize(decision.CandidateId, substituteId, originalId)).Append('|');
            }
            foreach (SocietyEvent societyEvent in result.Events.OrderBy(
                value => Normalize(value.EventId, substituteId, originalId),
                StringComparer.Ordinal))
            {
                builder.Append(Normalize(societyEvent.EventId, substituteId, originalId)).Append(':')
                    .Append(societyEvent.Kind).Append(':')
                    .Append(Normalize(societyEvent.ActorId, substituteId, originalId)).Append(':')
                    .Append(Normalize(societyEvent.TargetId, substituteId, originalId)).Append(':')
                    .Append(Normalize(societyEvent.EvidenceId, substituteId, originalId)).Append(':')
                    .Append(Normalize(societyEvent.EvidenceSubjectId, substituteId, originalId)).Append(':')
                    .Append(Normalize(societyEvent.EvidenceObjectId, substituteId, originalId));
                foreach (StateDelta delta in societyEvent.Deltas
                    .OrderBy(value => Normalize(value.EntityId, substituteId, originalId), StringComparer.Ordinal)
                    .ThenBy(value => Normalize(value.FieldId, substituteId, originalId), StringComparer.Ordinal))
                {
                    builder.Append('[')
                        .Append(Normalize(delta.EntityId, substituteId, originalId)).Append(':')
                        .Append(Normalize(delta.FieldId, substituteId, originalId)).Append(':')
                        .Append(delta.Before).Append('>').Append(delta.After).Append(']');
                }
                builder.Append('|');
            }
            return builder.ToString();
        }

        private static string Normalize(string value, string substituteId, string originalId)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(substituteId)) return value;
            return value.Replace(substituteId, originalId);
        }

        private static void RemapAgentId(SocietyState state, string fromId, string toId)
        {
            AgentState renamed = state.GetAgent(fromId);
            Assert.IsNotNull(renamed, $"Expected agent {fromId} before remapping.");
            renamed.StableId = toId;

            foreach (AgentState agent in state.Agents)
            {
                foreach (RelationshipState relationship in agent.Relationships)
                    relationship.TargetAgentId = Normalize(relationship.TargetAgentId, fromId, toId);
                foreach (CommitmentState commitment in agent.Commitments)
                    commitment.TargetId = Normalize(commitment.TargetId, fromId, toId);
                foreach (BeliefState belief in agent.Beliefs)
                {
                    belief.SubjectId = Normalize(belief.SubjectId, fromId, toId);
                    belief.ObjectId = Normalize(belief.ObjectId, fromId, toId);
                }
            }

            foreach (SocietyEvent societyEvent in state.EventLedger)
            {
                societyEvent.EventId = Normalize(societyEvent.EventId, fromId, toId);
                societyEvent.CauseDecisionId = Normalize(societyEvent.CauseDecisionId, fromId, toId);
                societyEvent.ActorId = Normalize(societyEvent.ActorId, fromId, toId);
                societyEvent.TargetId = Normalize(societyEvent.TargetId, fromId, toId);
                societyEvent.EvidenceId = Normalize(societyEvent.EvidenceId, fromId, toId);
                societyEvent.EvidenceSubjectId = Normalize(societyEvent.EvidenceSubjectId, fromId, toId);
                societyEvent.EvidenceObjectId = Normalize(societyEvent.EvidenceObjectId, fromId, toId);
                foreach (StateDelta delta in societyEvent.Deltas)
                {
                    delta.EntityId = Normalize(delta.EntityId, fromId, toId);
                    delta.FieldId = Normalize(delta.FieldId, fromId, toId);
                }
            }
        }

        private static void CanonicalizeCollectionOrder(SocietyState state)
        {
            state.Agents.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
            foreach (AgentState agent in state.Agents)
            {
                agent.Needs.Sort((left, right) => left.Kind.CompareTo(right.Kind));
                agent.Commitments.Sort((left, right) =>
                    string.CompareOrdinal(left.CommitmentId, right.CommitmentId));
                agent.Relationships.Sort((left, right) =>
                    string.CompareOrdinal(left.TargetAgentId, right.TargetAgentId));
                agent.Beliefs.Sort((left, right) => string.CompareOrdinal(left.BeliefId, right.BeliefId));
                agent.Standing.OfficialStatuses.Sort((left, right) =>
                    string.CompareOrdinal(left.StatusId, right.StatusId));
                agent.AnomalyRules.Sort((left, right) => string.CompareOrdinal(left.TraitId, right.TraitId));
            }
            foreach (SocietyEvent societyEvent in state.EventLedger)
            {
                societyEvent.Deltas.Sort((left, right) =>
                {
                    int entity = string.CompareOrdinal(left.EntityId, right.EntityId);
                    return entity != 0 ? entity : string.CompareOrdinal(left.FieldId, right.FieldId);
                });
            }
            state.EventLedger.Sort((left, right) => string.CompareOrdinal(left.EventId, right.EventId));
        }
    }
}
