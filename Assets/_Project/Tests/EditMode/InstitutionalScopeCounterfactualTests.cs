using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalScopeCounterfactualTests
    {
        [Test]
        public void NarrowAndBroadScope_FromSameSnapshot_TraceFirstDivergenceToDescendantCase()
        {
            ProofSnapshot canonical = BuildCanonicalPreRulingSnapshot();
            ForkOutcome narrow = RunFork(canonical, broad: false);
            ForkOutcome broad = RunFork(canonical, broad: true);

            Assert.AreEqual(narrow.PreScopeInputSignature, broad.PreScopeInputSignature,
                "Future action opportunities must be identical before scope matching.");
            Assert.IsFalse(narrow.ScopeTrace.StatusBefore);
            Assert.IsFalse(broad.ScopeTrace.StatusBefore);
            Assert.AreEqual(false, narrow.ScopeTrace.ScopeMatched,
                "The narrow holding must not bind the connected agent.");
            Assert.AreEqual(true, broad.ScopeTrace.ScopeMatched,
                "The broad holding must bind the same connected agent and opportunity.");
            Assert.AreEqual(
                narrow.ScopeTrace.OpportunityId,
                broad.ScopeTrace.OpportunityId);
            Assert.AreEqual(
                narrow.ScopeTrace.ActorId,
                broad.ScopeTrace.ActorId);

            Assert.IsFalse(narrow.Decision.PerceptionSnapshot.Standing.IsRecognised(
                EndogenousScopeEffectService.ProtectedPossessionStatusId));
            Assert.IsTrue(broad.Decision.PerceptionSnapshot.Standing.IsRecognised(
                EndogenousScopeEffectService.ProtectedPossessionStatusId));

            CandidateEvaluation narrowCandidate = narrow.Decision.CandidateEvaluations.Single(
                value => value.Action == SocietyActionKind.Steal);
            CandidateEvaluation broadCandidate = broad.Decision.CandidateEvaluations.Single(
                value => value.Action == SocietyActionKind.Steal);
            Assert.AreEqual(100, broadCandidate.Score - narrowCandidate.Score,
                "Protection replaces a 20-point exposure cost with an 80-point bonus.");
            Assert.IsTrue(broadCandidate.Reasons.Any(value =>
                value.ReasonId == "standing.holding-protection" &&
                value.ScoreDelta == 80));
            Assert.IsTrue(narrowCandidate.Reasons.Any(value =>
                value.ReasonId == "standing.unprotected-exposure" &&
                value.ScoreDelta == -20));

            Assert.AreEqual(SocietyActionKind.Idle, narrow.Decision.Action);
            Assert.AreEqual(SocietyActionKind.Steal, broad.Decision.Action);
            Assert.IsTrue(narrow.Step.Events.Any(value =>
                value.ActorId == "agent.connected" &&
                value.Kind == SocietyEventKind.NoActionObserved));
            Assert.IsTrue(broad.Step.Events.Any(value =>
                value.ActorId == "agent.connected" &&
                value.Kind == SocietyEventKind.PossessionTransferRequested));

            Assert.IsNull(narrow.Pulse.AdmittedCase);
            Assert.IsNotNull(broad.Pulse.AdmittedCase);
            Assert.AreEqual(canonical.InitialCaseId, broad.Pulse.AdmittedCase.ParentCaseId);
            Assert.AreEqual(broad.Ruling.RulingId,
                broad.Pulse.AdmittedCase.OriginatingRulingId);
            Assert.AreEqual(broad.Decision.DecisionId,
                broad.Pulse.AdmittedCase.CausalAgentActionId);
            Assert.AreEqual("agent.connected",
                broad.World.GetResource("resource.later").PhysicalHolderId);
            Assert.AreEqual("clinic",
                broad.World.GetOfficialOwnership("resource.later").RegisteredOwnerId);
        }

        [Test]
        public void CounterfactualCommands_HaveIdenticalSubstanceExceptScope()
        {
            ProofSnapshot canonical = BuildCanonicalPreRulingSnapshot();
            PlayerRulingCommand narrow = RulingCommand(canonical, broad: false);
            PlayerRulingCommand broad = RulingCommand(canonical, broad: true);

            Assert.AreEqual(narrow.CaseId, broad.CaseId);
            Assert.AreEqual(narrow.ExpectedCaseVersion, broad.ExpectedCaseVersion);
            Assert.AreEqual(narrow.EvidenceEnvelopeHash, broad.EvidenceEnvelopeHash);
            CollectionAssert.AreEqual(narrow.RecognisedFactIds, broad.RecognisedFactIds);
            CollectionAssert.AreEqual(
                narrow.CitedEvidenceArtifactIds,
                broad.CitedEvidenceArtifactIds);
            Assert.AreEqual(narrow.Disposition, broad.Disposition);
            Assert.AreEqual(narrow.HoldingRuleId, broad.HoldingRuleId);
            Assert.AreEqual(narrow.TemporalReach, broad.TemporalReach);
            CollectionAssert.AreEqual(
                narrow.RemedyDefinitionIds,
                broad.RemedyDefinitionIds);
            Assert.AreNotEqual(
                CanonicalScope(narrow.Scope),
                CanonicalScope(broad.Scope));
        }

        [Test]
        public void RemovingOriginalMaterialOpportunity_RemovesActionAndCaseChain()
        {
            ProofSnapshot canonical = BuildCanonicalPreRulingSnapshot();
            SocietyState society = SocietyStateDeepCopy.Copy(canonical.Society);
            InstitutionalMaterialWorld world = InstitutionalMaterialWorldDeepCopy.Copy(
                canonical.World);
            EndogenousDocketState docket = EndogenousDocketStateDeepCopy.Copy(
                canonical.Docket);
            EndogenousPlayerRulingService.Commit(
                society, docket, RulingCommand(canonical, broad: true));
            world.GetAccessGrant("access:agent.connected:resource.later").Active = false;
            SimulationInput input = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(society, world, input);

            List<EndogenousScopeApplicationTrace> traces =
                EndogenousScopeEffectService.Apply(society, docket, input);
            SimulationStepResult step = new EndogenousSocietyStepService().Advance(
                society, world, input);
            EndogenousDocketPulse pulse = EndogenousIncidentDocketPipeline.Process(
                world, society, docket);

            Assert.IsEmpty(input.StealOpportunities);
            Assert.IsEmpty(traces);
            Assert.AreEqual(SocietyActionKind.Idle,
                step.Decisions.Single(value => value.ActorId == "agent.connected").Action);
            Assert.IsNull(pulse.AdmittedCase);
        }

        [Test]
        public void HoldingProtectionStatus_IsLoadBearingForLaterDecision()
        {
            ProofSnapshot canonical = BuildCanonicalPreRulingSnapshot();
            SocietyState protectedSociety = SocietyStateDeepCopy.Copy(canonical.Society);
            SocietyState perturbedSociety = SocietyStateDeepCopy.Copy(canonical.Society);
            InstitutionalMaterialWorld protectedWorld =
                InstitutionalMaterialWorldDeepCopy.Copy(canonical.World);
            InstitutionalMaterialWorld perturbedWorld =
                InstitutionalMaterialWorldDeepCopy.Copy(canonical.World);
            EndogenousDocketState protectedDocket =
                EndogenousDocketStateDeepCopy.Copy(canonical.Docket);
            EndogenousDocketState perturbedDocket =
                EndogenousDocketStateDeepCopy.Copy(canonical.Docket);
            EndogenousPlayerRulingService.Commit(
                protectedSociety,
                protectedDocket,
                RulingCommand(canonical, broad: true));
            EndogenousPlayerRulingService.Commit(
                perturbedSociety,
                perturbedDocket,
                RulingCommand(canonical, broad: true));
            SimulationInput protectedInput = QuietInput();
            SimulationInput perturbedInput = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(
                protectedSociety, protectedWorld, protectedInput);
            EndogenousActionOpportunityBuilder.Populate(
                perturbedSociety, perturbedWorld, perturbedInput);
            EndogenousScopeEffectService.Apply(
                protectedSociety, protectedDocket, protectedInput);
            EndogenousScopeEffectService.Apply(
                perturbedSociety, perturbedDocket, perturbedInput);
            perturbedSociety.GetAgent("agent.connected").Standing.SetRecognised(
                EndogenousScopeEffectService.ProtectedPossessionStatusId, false);

            AgentDecision protectedDecision = new EndogenousSocietyStepService()
                .Advance(protectedSociety, protectedWorld, protectedInput)
                .Decisions.Single(value => value.ActorId == "agent.connected");
            AgentDecision perturbedDecision = new EndogenousSocietyStepService()
                .Advance(perturbedSociety, perturbedWorld, perturbedInput)
                .Decisions.Single(value => value.ActorId == "agent.connected");

            Assert.AreEqual(SocietyActionKind.Steal, protectedDecision.Action);
            Assert.AreEqual(SocietyActionKind.Idle, perturbedDecision.Action);
        }

        [Test]
        public void CanonicalForkCopies_AreDetachedAndInitiallyByteEquivalent()
        {
            ProofSnapshot canonical = BuildCanonicalPreRulingSnapshot();
            SocietyState firstSociety = SocietyStateDeepCopy.Copy(canonical.Society);
            SocietyState secondSociety = SocietyStateDeepCopy.Copy(canonical.Society);
            InstitutionalMaterialWorld firstWorld =
                InstitutionalMaterialWorldDeepCopy.Copy(canonical.World);
            InstitutionalMaterialWorld secondWorld =
                InstitutionalMaterialWorldDeepCopy.Copy(canonical.World);
            EndogenousDocketState firstDocket =
                EndogenousDocketStateDeepCopy.Copy(canonical.Docket);
            EndogenousDocketState secondDocket =
                EndogenousDocketStateDeepCopy.Copy(canonical.Docket);

            Assert.AreEqual(JsonConvert.SerializeObject(firstSociety),
                JsonConvert.SerializeObject(secondSociety));
            Assert.AreEqual(CanonicalWorld(firstWorld), CanonicalWorld(secondWorld));
            Assert.AreEqual(CanonicalDocket(firstDocket), CanonicalDocket(secondDocket));

            firstSociety.GetAgent("agent.connected").InstitutionalTrust = -100;
            firstWorld.GetResource("resource.later").LocationContextId = "mutated";
            firstDocket.OpenCases[0].IssueId = "mutated";
            Assert.AreNotEqual(firstSociety.GetAgent("agent.connected").InstitutionalTrust,
                secondSociety.GetAgent("agent.connected").InstitutionalTrust);
            Assert.AreNotEqual(firstWorld.GetResource("resource.later").LocationContextId,
                secondWorld.GetResource("resource.later").LocationContextId);
            Assert.AreNotEqual(firstDocket.OpenCases[0].IssueId,
                secondDocket.OpenCases[0].IssueId);
        }

        private static ForkOutcome RunFork(ProofSnapshot canonical, bool broad)
        {
            SocietyState society = SocietyStateDeepCopy.Copy(canonical.Society);
            InstitutionalMaterialWorld world = InstitutionalMaterialWorldDeepCopy.Copy(
                canonical.World);
            EndogenousDocketState docket = EndogenousDocketStateDeepCopy.Copy(
                canonical.Docket);
            CommittedPlayerRuling ruling = EndogenousPlayerRulingService.Commit(
                society, docket, RulingCommand(canonical, broad));
            SimulationInput input = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(society, world, input);
            string preScopeInputSignature = JsonConvert.SerializeObject(input);
            List<EndogenousScopeApplicationTrace> traces =
                EndogenousScopeEffectService.Apply(society, docket, input);
            Assert.AreEqual(1, traces.Count);
            SimulationStepResult step = new EndogenousSocietyStepService().Advance(
                society, world, input);
            AgentDecision decision = step.Decisions.Single(value =>
                value.ActorId == "agent.connected");
            EndogenousDocketPulse pulse = EndogenousIncidentDocketPipeline.Process(
                world, society, docket);
            return new ForkOutcome
            {
                Society = society,
                World = world,
                Docket = docket,
                Ruling = ruling,
                PreScopeInputSignature = preScopeInputSignature,
                ScopeTrace = traces[0],
                Step = step,
                Decision = decision,
                Pulse = pulse,
            };
        }

        private static ProofSnapshot BuildCanonicalPreRulingSnapshot()
        {
            AgentState origin = Agent("agent.origin", 0);
            origin.GetNeed(NeedKind.Health).Pressure = 100;
            origin.Disposition.RiskTolerance = 100;
            AgentState connected = Agent("agent.connected", 1);
            connected.GetNeed(NeedKind.Health).Pressure = 70;
            connected.Disposition.Duty = 100;
            connected.InstitutionalTrust = 100;
            AgentState recorder = Agent("agent.recorder", 2);
            SocietyState society = Society(origin, connected, recorder);
            InstitutionalMaterialWorld world = InitialAndLaterWorld();
            SimulationInput input = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(society, world, input);

            SimulationStepResult firstStep = new EndogenousSocietyStepService().Advance(
                society, world, input);
            Assert.AreEqual(SocietyActionKind.Steal,
                firstStep.Decisions.Single(value => value.ActorId == origin.StableId).Action);
            Assert.AreEqual(SocietyActionKind.Idle,
                firstStep.Decisions.Single(value => value.ActorId == connected.StableId).Action);
            var docket = new EndogenousDocketState();
            EndogenousDocketPulse pulse = EndogenousIncidentDocketPipeline.Process(
                world, society, docket);
            Assert.IsNotNull(pulse.AdmittedCase);
            Assert.AreEqual("agent.origin", pulse.AdmittedCase.PartyIds.Single());
            EndogenousDocketValidator.Validate(docket, society);
            InstitutionalMaterialWorldValidator.Validate(world, society);
            return new ProofSnapshot
            {
                Society = society,
                World = world,
                Docket = docket,
                InitialCaseId = pulse.AdmittedCase.CaseId,
            };
        }

        private static InstitutionalMaterialWorld InitialAndLaterWorld()
        {
            var world = new InstitutionalMaterialWorld();
            AddResource(world, "resource.initial", "agent.origin", "record.camera.initial");
            AddResource(world, "resource.later", "agent.connected", "record.camera.later");
            return world;
        }

        private static void AddResource(
            InstitutionalMaterialWorld world,
            string resourceId,
            string actorId,
            string recordSourceId)
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
                GrantId = $"access:{actorId}:{resourceId}",
                AgentId = actorId,
                AccessKindId = EndogenousActionOpportunityBuilder.MaterialPossessionAccessKind,
                TargetId = resourceId,
                SourceRecordId = "record.shift",
                ValidFromTick = 0,
            });
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = $"recording:{resourceId}",
                AgentId = "agent.recorder",
                AccessKindId = EndogenousActionOpportunityBuilder.RecordingAccessKind,
                TargetId = resourceId,
                SourceRecordId = recordSourceId,
                ValidFromTick = 0,
            });
        }

        private static PlayerRulingCommand RulingCommand(
            ProofSnapshot snapshot,
            bool broad)
        {
            EndogenousInstitutionalCase opened = snapshot.Docket.GetCase(
                snapshot.InitialCaseId);
            ScopeExpression issue = Predicate(
                ScopePredicateKind.IssueEquals,
                EndogenousIssueKindIds.PossessionDispute);
            ScopeExpression scope = broad
                ? issue
                : new ScopeExpression
                {
                    Kind = ScopeExpressionKind.All,
                    Children = new List<ScopeExpression>
                    {
                        issue,
                        Predicate(ScopePredicateKind.AgentEquals, "agent.origin"),
                    },
                };
            return new PlayerRulingCommand
            {
                CommandId = broad ? "command.scope.broad" : "command.scope.narrow",
                CaseId = opened.CaseId,
                ExpectedCaseVersion = opened.CaseVersion,
                EvidenceEnvelopeHash = opened.EvidenceEnvelopeHash,
                RecognisedFactIds = new List<string> { opened.AvailableFactIds[0] },
                CitedEvidenceArtifactIds = new List<string> { opened.ObservationIds[0] },
                Disposition = RulingDisposition.Recognised,
                HoldingRuleId = EndogenousPlayerRulingService.PossessionHoldingRule,
                Scope = scope,
                TemporalReach = TemporalReach.Prospective,
                RemedyDefinitionIds = new List<string>
                {
                    EndogenousPlayerRulingService.RestorePossessionRemedy,
                },
            };
        }

        private static ScopeExpression Predicate(ScopePredicateKind kind, string value)
        {
            return new ScopeExpression
            {
                Kind = ScopeExpressionKind.Predicate,
                PredicateKind = kind,
                Value = value,
            };
        }

        private static string CanonicalScope(ScopeExpression scope)
            => JsonConvert.SerializeObject(scope);

        private static string CanonicalWorld(InstitutionalMaterialWorld world)
        {
            return string.Join("|", world.Resources
                .OrderBy(value => value.ResourceId, StringComparer.Ordinal)
                .Select(value =>
                    $"{value.ResourceId}:{value.PhysicalHolderId}:{value.LocationContextId}")) +
                "#" + string.Join("|", world.EventLedger
                    .OrderBy(value => value.EventId, StringComparer.Ordinal)
                    .Select(value => value.EventId));
        }

        private static string CanonicalDocket(EndogenousDocketState docket)
        {
            return string.Join("|", docket.IncidentCandidates
                       .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                       .Select(value => value.CandidateId)) +
                   "#" + string.Join("|", docket.OpenCases
                       .OrderBy(value => value.CaseId, StringComparer.Ordinal)
                       .Select(value => value.CaseId)) +
                   "#" + string.Join("|", docket.Rulings
                       .OrderBy(value => value.RulingId, StringComparer.Ordinal)
                       .Select(value => value.RulingId));
        }

        private static SocietyState Society(params AgentState[] agents)
        {
            return new SocietyState
            {
                MasterSeed = 424242,
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
                IncidentId = "endogenous-scope-pulse",
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
            };
        }

        private sealed class ProofSnapshot
        {
            internal SocietyState Society;
            internal InstitutionalMaterialWorld World;
            internal EndogenousDocketState Docket;
            internal string InitialCaseId;
        }

        private sealed class ForkOutcome
        {
            internal SocietyState Society;
            internal InstitutionalMaterialWorld World;
            internal EndogenousDocketState Docket;
            internal CommittedPlayerRuling Ruling;
            internal string PreScopeInputSignature;
            internal EndogenousScopeApplicationTrace ScopeTrace;
            internal SimulationStepResult Step;
            internal AgentDecision Decision;
            internal EndogenousDocketPulse Pulse;
        }
    }
}
