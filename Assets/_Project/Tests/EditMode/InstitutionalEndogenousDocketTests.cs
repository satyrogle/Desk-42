using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalEndogenousDocketTests
    {
        [Test]
        public void AutonomousPossessionActions_CreateIncidents_ButOnlyRecordedOneBecomesCase()
        {
            (SocietyState society, InstitutionalMaterialWorld world, SimulationInput input) =
                TwoPossessionConflicts(recordSecond: true);
            SimulationStepResult actions = new EndogenousSocietyStepService().Advance(
                society, world, input);
            var docket = new EndogenousDocketState();

            EndogenousDocketPulse pulse = EndogenousIncidentDocketPipeline.Process(
                world, society, docket);

            Assert.AreEqual(2, actions.Decisions.Count(value =>
                value.Action == SocietyActionKind.Steal));
            Assert.AreEqual(2, pulse.DetectedIncidents.Count);
            Assert.AreEqual(2, docket.IncidentCandidates.Count);
            Assert.AreEqual(1, docket.Observations.Count);
            Assert.AreEqual(1, docket.DocketCandidates.Count);
            Assert.AreEqual(1, docket.OpenCases.Count);
            Assert.IsNotNull(pulse.AdmittedCase);
            Assert.AreEqual(EndogenousIssueKindIds.PossessionDispute,
                pulse.AdmittedCase.IssueId);
            Assert.IsFalse(docket.DocketCandidates.Single().PotentialPartyIds.Contains(
                "agent.private"),
                "An unobserved lived participant must not leak into the public docket.");
            Assert.IsTrue(docket.DocketCandidates.Single().PotentialPartyIds.Contains(
                "agent.recorded"));
        }

        [Test]
        public void RemovingObservability_PreservesLivedIncidents_ButRemovesOfficialCase()
        {
            (SocietyState visibleSociety, InstitutionalMaterialWorld visibleWorld,
                SimulationInput visibleInput) = TwoPossessionConflicts(recordSecond: true);
            (SocietyState hiddenSociety, InstitutionalMaterialWorld hiddenWorld,
                SimulationInput hiddenInput) = TwoPossessionConflicts(recordSecond: false);
            new EndogenousSocietyStepService().Advance(
                visibleSociety, visibleWorld, visibleInput);
            new EndogenousSocietyStepService().Advance(
                hiddenSociety, hiddenWorld, hiddenInput);
            var visibleDocket = new EndogenousDocketState();
            var hiddenDocket = new EndogenousDocketState();

            EndogenousIncidentDocketPipeline.Process(
                visibleWorld, visibleSociety, visibleDocket);
            EndogenousIncidentDocketPipeline.Process(
                hiddenWorld, hiddenSociety, hiddenDocket);

            Assert.AreEqual(2, visibleDocket.IncidentCandidates.Count);
            Assert.AreEqual(2, hiddenDocket.IncidentCandidates.Count);
            Assert.AreEqual(1, visibleDocket.OpenCases.Count);
            Assert.IsEmpty(hiddenDocket.Observations);
            Assert.IsEmpty(hiddenDocket.DocketCandidates);
            Assert.IsEmpty(hiddenDocket.OpenCases);
        }

        [Test]
        public void DirectWitnessWithoutRecordOrSubmission_DoesNotCreateDocketEvidence()
        {
            AgentState actor = Agent("agent.actor", 0);
            actor.GetNeed(NeedKind.Health).Pressure = 100;
            actor.Disposition.RiskTolerance = 100;
            AgentState witness = Agent("agent.witness", 1);
            SocietyState society = Society(actor, witness);
            InstitutionalMaterialWorld world = OneResourceWorld(
                "resource.private", actor.StableId);
            SimulationInput input = QuietInput();
            input.StealOpportunities.Add(StealOpportunity(
                "private", "resource.private", actor.StableId,
                EvidenceVisibility.Observable,
                new[] { witness.StableId },
                Array.Empty<string>()));
            new EndogenousSocietyStepService().Advance(society, world, input);
            var docket = new EndogenousDocketState();

            EndogenousIncidentDocketPipeline.Process(world, society, docket);

            Assert.AreEqual(1, docket.IncidentCandidates.Count);
            Assert.IsEmpty(docket.Observations,
                "A witness has knowledge, not an automatically submitted official record.");
            Assert.IsEmpty(docket.OpenCases);
        }

        [Test]
        public void Pipeline_ReprocessingSameCausalGraph_IsIdempotent()
        {
            (SocietyState society, InstitutionalMaterialWorld world, SimulationInput input) =
                TwoPossessionConflicts(recordSecond: true);
            new EndogenousSocietyStepService().Advance(society, world, input);
            var docket = new EndogenousDocketState();
            EndogenousDocketPulse first = EndogenousIncidentDocketPipeline.Process(
                world, society, docket);

            EndogenousDocketPulse replay = EndogenousIncidentDocketPipeline.Process(
                world, society, docket);

            Assert.AreEqual(2, first.DetectedIncidents.Count);
            Assert.AreEqual(1, first.ProjectedObservations.Count);
            Assert.AreEqual(1, first.ComposedDocketCandidates.Count);
            Assert.IsNotNull(first.AdmittedCase);
            Assert.IsEmpty(replay.DetectedIncidents);
            Assert.IsEmpty(replay.ProjectedObservations);
            Assert.IsEmpty(replay.ComposedDocketCandidates);
            Assert.IsNull(replay.AdmittedCase);
            Assert.AreEqual(2, docket.IncidentCandidates.Count);
            Assert.AreEqual(1, docket.Observations.Count);
            Assert.AreEqual(1, docket.DocketCandidates.Count);
            Assert.AreEqual(1, docket.OpenCases.Count);
        }

        [Test]
        public void Admission_IsOldestThenHighestHarmThenStableId_WithDirectorDisabled()
        {
            SocietyState society = Society(
                Agent("agent.a", 0), Agent("agent.b", 1));
            society.CurrentTick = 10;
            EndogenousDocketState state = ManualAdmissionState();

            EndogenousInstitutionalCase first = EndogenousDocketService.AdmitNext(
                society, state);
            EndogenousInstitutionalCase second = EndogenousDocketService.AdmitNext(
                society, state);
            EndogenousInstitutionalCase third = EndogenousDocketService.AdmitNext(
                society, state);

            Assert.IsFalse(state.DirectorEnabled);
            Assert.AreEqual("case:docket.oldest", first.CaseId);
            Assert.AreEqual("case:docket.high-harm", second.CaseId);
            Assert.AreEqual("case:docket.low-harm", third.CaseId);
        }

        [Test]
        public void DirectorEnabled_IsRejectedFromProofPath()
        {
            SocietyState society = Society(Agent("agent.a", 0));
            var state = new EndogenousDocketState { DirectorEnabled = true };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => EndogenousDocketValidator.Validate(state, society));
            StringAssert.Contains("Director", error.Message);
        }

        [Test]
        public void AuthorityIncidentTruth_IsAbsentFromPublicDocketTypeGraph()
        {
            Assert.IsFalse(typeof(IncidentCandidate).IsPublic);
            Assert.IsFalse(typeof(IncidentCandidate).IsNestedPublic);
            Type[] publicTypes =
            {
                typeof(DocketObservation),
                typeof(DocketCandidate),
                typeof(EndogenousInstitutionalCase),
            };
            foreach (Type publicType in publicTypes)
            {
                foreach (FieldInfo field in publicType.GetFields(
                             BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.IsFalse(ContainsIncidentType(field.FieldType),
                        $"{publicType.Name}.{field.Name} leaks authority incident truth.");
                }
                foreach (PropertyInfo property in publicType.GetProperties(
                             BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.IsFalse(ContainsIncidentType(property.PropertyType),
                        $"{publicType.Name}.{property.Name} leaks authority incident truth.");
                }
            }
        }

        [Test]
        public void IdentityRemap_PreservesGenericCausalPattern()
        {
            (SocietyState firstSociety, InstitutionalMaterialWorld firstWorld,
                SimulationInput firstInput) = OneRecordedConflict("agent.alpha");
            (SocietyState secondSociety, InstitutionalMaterialWorld secondWorld,
                SimulationInput secondInput) = OneRecordedConflict("agent.unseen-profile");
            new EndogenousSocietyStepService().Advance(
                firstSociety, firstWorld, firstInput);
            new EndogenousSocietyStepService().Advance(
                secondSociety, secondWorld, secondInput);
            var firstDocket = new EndogenousDocketState();
            var secondDocket = new EndogenousDocketState();

            EndogenousIncidentDocketPipeline.Process(
                firstWorld, firstSociety, firstDocket);
            EndogenousIncidentDocketPipeline.Process(
                secondWorld, secondSociety, secondDocket);

            Assert.AreEqual(firstDocket.IncidentCandidates.Count,
                secondDocket.IncidentCandidates.Count);
            Assert.AreEqual(firstDocket.Observations.Count,
                secondDocket.Observations.Count);
            Assert.AreEqual(firstDocket.DocketCandidates.Single().IssueId,
                secondDocket.DocketCandidates.Single().IssueId);
            Assert.AreEqual(firstDocket.OpenCases.Single().ObservationIds.Count,
                secondDocket.OpenCases.Single().ObservationIds.Count);
        }

        [Test]
        public void EndogenousAuthorityAssembly_HasNoScenarioAssemblyDependency()
        {
            string[] references = typeof(EndogenousIncidentDocketPipeline).Assembly
                .GetReferencedAssemblies()
                .Select(value => value.Name)
                .ToArray();
            CollectionAssert.DoesNotContain(
                references, "Desk42.Institutional.Scenarios");
            CollectionAssert.DoesNotContain(
                references, "Desk42.Institutional.Scenarios.WorkplaceIdentity");
            CollectionAssert.DoesNotContain(
                references, "Desk42.Institutional.Scenarios.GlassCanal");
        }

        private static EndogenousDocketState ManualAdmissionState()
        {
            var state = new EndogenousDocketState();
            AddManualCandidate(state, "oldest", tick: 1, harm: 1);
            AddManualCandidate(state, "low-harm", tick: 2, harm: 10);
            AddManualCandidate(state, "high-harm", tick: 2, harm: 90);
            return state;
        }

        private static void AddManualCandidate(
            EndogenousDocketState state,
            string suffix,
            long tick,
            int harm)
        {
            string incidentId = $"incident.{suffix}";
            string observationId = $"observation.{suffix}";
            string docketId = $"docket.{suffix}";
            state.IncidentCandidates.Add(new IncidentCandidate
            {
                CandidateId = incidentId,
                CauseEventIds = new List<string> { $"event.{suffix}" },
                AffectedAgentIds = new List<string> { "agent.a" },
                ConflictKindId = EndogenousIssueKindIds.PossessionDispute,
                DetectedTick = tick,
                UnresolvedMaterialHarm = harm,
                DedupeKey = $"dedupe.{suffix}",
            });
            state.Observations.Add(new DocketObservation
            {
                ObservationId = observationId,
                RecordedTick = tick,
                ObservationKindId = "recorded-possession-change",
                IssueId = EndogenousIssueKindIds.PossessionDispute,
                PropositionId = "registered-asset-possession-changed",
                SourceRecordId = $"record.{suffix}",
                Reliability = 90,
                ObservedMaterialHarm = harm,
                OfficiallySubmitted = true,
                AuthorityIncidentCandidateId = incidentId,
            });
            state.DocketCandidates.Add(new DocketCandidate
            {
                DocketCandidateId = docketId,
                EligibilityRuleId = "observable-possession-conflict-v1",
                IssueId = EndogenousIssueKindIds.PossessionDispute,
                EligibleTick = tick,
                UnresolvedMaterialHarm = harm,
                ObservableEvidenceIds = new List<string> { observationId },
                AuthorityIncidentCandidateId = incidentId,
            });
        }

        private static (SocietyState, InstitutionalMaterialWorld, SimulationInput)
            TwoPossessionConflicts(bool recordSecond)
        {
            AgentState privateActor = Agent("agent.private", 0);
            AgentState recordedActor = Agent("agent.recorded", 1);
            privateActor.GetNeed(NeedKind.Health).Pressure = 100;
            recordedActor.GetNeed(NeedKind.Health).Pressure = 100;
            privateActor.Disposition.RiskTolerance = 100;
            recordedActor.Disposition.RiskTolerance = 100;
            SocietyState society = Society(privateActor, recordedActor);
            var world = new InstitutionalMaterialWorld();
            AddResource(world, "resource.private", privateActor.StableId);
            AddResource(world, "resource.recorded", recordedActor.StableId);
            SimulationInput input = QuietInput();
            input.StealOpportunities.Add(StealOpportunity(
                "private", "resource.private", privateActor.StableId,
                EvidenceVisibility.Private,
                Array.Empty<string>(), Array.Empty<string>()));
            input.StealOpportunities.Add(StealOpportunity(
                "recorded", "resource.recorded", recordedActor.StableId,
                recordSecond ? EvidenceVisibility.Observable : EvidenceVisibility.Private,
                Array.Empty<string>(),
                recordSecond ? new[] { "record.camera.7" } : Array.Empty<string>()));
            return (society, world, input);
        }

        private static (SocietyState, InstitutionalMaterialWorld, SimulationInput)
            OneRecordedConflict(string actorId)
        {
            AgentState actor = Agent(actorId, 0);
            actor.GetNeed(NeedKind.Health).Pressure = 100;
            actor.Disposition.RiskTolerance = 100;
            SocietyState society = Society(actor);
            InstitutionalMaterialWorld world = OneResourceWorld("resource.one", actorId);
            SimulationInput input = QuietInput();
            input.StealOpportunities.Add(StealOpportunity(
                "one", "resource.one", actorId, EvidenceVisibility.Observable,
                Array.Empty<string>(), new[] { "record.camera.one" }));
            return (society, world, input);
        }

        private static InstitutionalMaterialWorld OneResourceWorld(
            string resourceId,
            string actorId)
        {
            var world = new InstitutionalMaterialWorld();
            AddResource(world, resourceId, actorId);
            return world;
        }

        private static void AddResource(
            InstitutionalMaterialWorld world,
            string resourceId,
            string actorId)
        {
            world.Resources.Add(new MaterialResourceState
            {
                ResourceId = resourceId,
                ResourceKindId = "medicine",
                Quantity = 1,
                PhysicalHolderId = "clinic",
                LocationContextId = "clinic.store",
            });
            world.OfficialOwnerships.Add(new OfficialOwnershipState
            {
                OwnershipRecordId = $"ownership:{resourceId}",
                ResourceId = resourceId,
                RegisteredOwnerId = "clinic",
                OwnershipSourceId = "record.inventory",
                RecognitionTick = 0,
            });
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = $"access:{resourceId}:{actorId}",
                AgentId = actorId,
                AccessKindId = EndogenousActionOpportunityBuilder.MaterialPossessionAccessKind,
                TargetId = resourceId,
                SourceRecordId = "record.shift",
                ValidFromTick = 0,
            });
        }

        private static StealOpportunity StealOpportunity(
            string suffix,
            string resourceId,
            string actorId,
            EvidenceVisibility visibility,
            IEnumerable<string> witnesses,
            IEnumerable<string> recordSources)
        {
            return new StealOpportunity
            {
                OpportunityId = $"possession.{suffix}",
                ResourceId = resourceId,
                ExpectedPhysicalHolderId = "clinic",
                NewLocationContextId = "actor-controlled",
                AccessGrantId = $"access:{resourceId}:{actorId}",
                ReliefNeed = NeedKind.Health,
                ReliefAmount = 20,
                UtilityBonus = 100,
                Visibility = visibility,
                Secrecy = visibility == EvidenceVisibility.Private ? 90 : 20,
                EligibleActorIds = new List<string> { actorId },
                DirectWitnessAgentIds = witnesses.ToList(),
                PotentialRecordSourceIds = recordSources.ToList(),
            };
        }

        private static SocietyState Society(params AgentState[] agents)
        {
            return new SocietyState
            {
                MasterSeed = 9001,
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 0,
                    AidEffectiveness = 0,
                    DisclosureProtection = 0,
                    RetaliationRisk = 0,
                    AppealAccessibility = 0,
                    DecisionVariationAmplitude = 0,
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

        private static SimulationInput QuietInput()
        {
            return new SimulationInput
            {
                IncidentId = "endogenous-docket-pulse",
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
            };
        }

        private static bool ContainsIncidentType(Type type)
        {
            if (type == typeof(IncidentCandidate)) return true;
            if (!type.IsGenericType) return false;
            return type.GetGenericArguments().Any(ContainsIncidentType);
        }
    }
}
