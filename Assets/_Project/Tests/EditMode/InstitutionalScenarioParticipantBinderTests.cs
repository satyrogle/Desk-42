using System;
using System.Collections.Generic;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalScenarioParticipantBinderTests
    {
        [Test]
        public void Bind_AllSemanticPredicates_ReturnsReadOnlyMappingAndDetachedDiagnostics()
        {
            InstitutionalScenarioDefinition definition = Definition(
                Agent(
                    "agent.decoy", 2, "species.cloud", "employer.canal",
                    true, false, true, false),
                Agent(
                    "agent.match", 7, "species.cloud", "employer.canal",
                    true, false, true, true));
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.claimant",
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.cloud",
                    RequiredEmployerId = "employer.canal",
                    RequiredRecognisedStatusIds = new List<string> { "status.recognised" },
                    RequiredUnrecognisedStatusIds = new List<string> { "status.unrecognised" },
                    RequiredAnomalyTraitIds = new List<string> { "trait.resonant" },
                    RequiredCommitmentKinds = new List<string> { "commitment.care" },
                },
            });

            InstitutionalScenarioParticipantBindings bindings =
                InstitutionalScenarioParticipantBinder.Bind(definition);

            AgentState bound = bindings.GetAgent("role.claimant");
            Assert.That(bound.StableId, Is.EqualTo("agent.match"));
            Assert.That(bindings.GetAgentBySimulationOrdinal(7), Is.SameAs(bound));
            Assert.That(bindings.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(bindings.Diagnostics[0].RoleId, Is.EqualTo("role.claimant"));
            Assert.That(bindings.Diagnostics[0].BoundAgentStableId, Is.EqualTo("agent.match"));
            Assert.That(bindings.Diagnostics[0].BoundAgentSimulationOrdinal, Is.EqualTo(7));
            Assert.That(bindings.Diagnostics[0].SemanticCandidateCount, Is.EqualTo(1));

            var mutableMapping = (IDictionary<string, AgentState>)bindings.AgentsByRole;
            Assert.Throws<NotSupportedException>(
                () => mutableMapping.Add("role.illegal", definition.InitialSociety.Agents[0]));

            definition.ParticipantRoles[0].RoleId = "role.mutated";
            definition.InitialSociety.Agents[1].StableId = "agent.mutated";
            Assert.That(bindings.Diagnostics[0].RoleId, Is.EqualTo("role.claimant"));
            Assert.That(bindings.Diagnostics[0].BoundAgentStableId, Is.EqualTo("agent.match"));
            Assert.That(bindings.GetAgent("role.claimant"), Is.SameAs(bound));
        }

        [Test]
        public void Bind_InputCollectionOrderDoesNotChangeBindingOrDiagnosticOrder()
        {
            AgentState alpha = Agent(
                "agent.alpha", 10, "species.alpha", "employer.alpha",
                true, false, false, false);
            AgentState beta = Agent(
                "agent.beta", 3, "species.beta", "employer.beta",
                true, false, false, false);
            InstitutionalScenarioDefinition forward = Definition(alpha, beta);
            forward.ParticipantRoles.Add(Role("role.alpha", species: "species.alpha"));
            forward.ParticipantRoles.Add(Role("role.beta", species: "species.beta"));

            InstitutionalScenarioDefinition reversed = Definition(Clone(beta), Clone(alpha));
            reversed.ParticipantRoles.Add(Role("role.beta", species: "species.beta"));
            reversed.ParticipantRoles.Add(Role("role.alpha", species: "species.alpha"));

            InstitutionalScenarioParticipantBindings first =
                InstitutionalScenarioParticipantBinder.Bind(forward);
            InstitutionalScenarioParticipantBindings second =
                InstitutionalScenarioParticipantBinder.Bind(reversed);

            Assert.That(first.GetAgent("role.alpha").StableId,
                Is.EqualTo(second.GetAgent("role.alpha").StableId));
            Assert.That(first.GetAgent("role.beta").StableId,
                Is.EqualTo(second.GetAgent("role.beta").StableId));
            Assert.That(first.Diagnostics[0].RoleId, Is.EqualTo("role.alpha"));
            Assert.That(second.Diagnostics[0].RoleId, Is.EqualTo("role.alpha"));
            Assert.That(first.Diagnostics[1].RoleId, Is.EqualTo("role.beta"));
            Assert.That(second.Diagnostics[1].RoleId, Is.EqualTo("role.beta"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Bind_DistinctConstraintFindsUniqueGlobalAssignment(bool symmetric)
        {
            InstitutionalScenarioDefinition definition = Definition(
                Agent(
                    "agent.preferred", 0, "species.shared", "employer.preferred",
                    true, false, false, false),
                Agent(
                    "agent.other", 1, "species.shared", "employer.other",
                    true, false, false, false));
            ScenarioParticipantRoleDefinition broad = Role("role.broad", species: "species.shared");
            broad.DistinctFromRoleIds.Add("role.narrow");
            ScenarioParticipantRoleDefinition narrow = Role(
                "role.narrow", employer: "employer.preferred");
            if (symmetric) narrow.DistinctFromRoleIds.Add("role.broad");
            definition.ParticipantRoles.Add(broad);
            definition.ParticipantRoles.Add(narrow);

            InstitutionalScenarioParticipantBindings bindings =
                InstitutionalScenarioParticipantBinder.Bind(definition);

            Assert.That(bindings.GetAgent("role.narrow").StableId,
                Is.EqualTo("agent.preferred"));
            Assert.That(bindings.GetAgent("role.broad").StableId,
                Is.EqualTo("agent.other"));
        }

        [Test]
        public void Bind_RejectsRoleWithNoSemanticMatch()
        {
            InstitutionalScenarioDefinition definition = Definition(
                Agent(
                    "agent.alpha", 0, "species.alpha", "employer.alpha",
                    true, false, false, false));
            definition.ParticipantRoles.Add(Role("role.beta", species: "species.beta"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioParticipantBinder.Bind(definition));

            Assert.That(exception.Message, Does.Contain("no semantic query match"));
        }

        [Test]
        public void Bind_RejectsAmbiguousCompleteBinding()
        {
            InstitutionalScenarioDefinition definition = Definition(
                Agent(
                    "agent.alpha", 0, "species.shared", "employer.alpha",
                    true, false, false, false),
                Agent(
                    "agent.beta", 1, "species.shared", "employer.beta",
                    true, false, false, false));
            definition.ParticipantRoles.Add(Role("role.shared", species: "species.shared"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioParticipantBinder.Bind(definition));

            Assert.That(exception.Message, Does.Contain("ambiguous"));
        }

        [Test]
        public void Bind_RejectsDuplicateAgentWhereDistinctConstraintForbidsIt()
        {
            InstitutionalScenarioDefinition definition = Definition(
                Agent(
                    "agent.only", 0, "species.shared", "employer.only",
                    true, false, false, false));
            ScenarioParticipantRoleDefinition first = Role(
                "role.first", employer: "employer.only");
            first.DistinctFromRoleIds.Add("role.second");
            definition.ParticipantRoles.Add(first);
            definition.ParticipantRoles.Add(Role("role.second", employer: "employer.only"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioParticipantBinder.Bind(definition));

            Assert.That(exception.Message, Does.Contain("no valid binding"));
        }

        [Test]
        public void Bind_AllowsSharedAgentWhenNoDistinctConstraintExists()
        {
            InstitutionalScenarioDefinition definition = Definition(
                Agent(
                    "agent.only", 5, "species.shared", "employer.only",
                    true, false, false, false));
            definition.ParticipantRoles.Add(Role("role.first", employer: "employer.only"));
            definition.ParticipantRoles.Add(Role("role.second", species: "species.shared"));

            InstitutionalScenarioParticipantBindings bindings =
                InstitutionalScenarioParticipantBinder.Bind(definition);

            Assert.That(bindings.GetAgent("role.first"),
                Is.SameAs(bindings.GetAgent("role.second")));
            Assert.That(bindings.GetAgentBySimulationOrdinal(5).StableId,
                Is.EqualTo("agent.only"));
        }

        [Test]
        public void Bind_RejectsInvalidRoleDefinition()
        {
            InstitutionalScenarioDefinition definition = Definition(
                Agent(
                    "agent.alpha", 0, "species.alpha", "employer.alpha",
                    true, false, false, false));
            definition.ParticipantRoles.Add(Role("role.duplicate", species: "species.alpha"));
            definition.ParticipantRoles.Add(Role("role.duplicate", employer: "employer.alpha"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioParticipantBinder.Bind(definition));

            Assert.That(exception.Message, Does.Contain("Duplicate participant role id"));
        }

        [Test]
        public void Bind_RejectsDirectAgentIdUsedByOperation()
        {
            InstitutionalScenarioDefinition definition = Definition(
                Agent(
                    "agent.alpha", 0, "species.alpha", "employer.alpha",
                    true, false, false, false));
            definition.ParticipantRoles.Add(Role("role.alpha", species: "species.alpha"));
            definition.OfficialStatusEffectRequests.Add(new ScenarioOfficialStatusEffectRequest
            {
                TargetRoleId = "agent.alpha",
            });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioParticipantBinder.Bind(definition));

            Assert.That(exception.Message, Does.Contain("forbidden direct agent id"));
        }

        private static InstitutionalScenarioDefinition Definition(params AgentState[] agents)
        {
            var definition = new InstitutionalScenarioDefinition
            {
                ScenarioId = "scenario.binding-tests",
                InitialSociety = new SocietyState
                {
                    MasterSeed = 42,
                    CurrentTick = 0,
                    Regime = new InstitutionalRegimeState(),
                },
            };
            definition.InitialSociety.Agents.AddRange(agents);
            return definition;
        }

        private static ScenarioParticipantRoleDefinition Role(
            string roleId,
            string species = null,
            string employer = null)
        {
            return new ScenarioParticipantRoleDefinition
            {
                RoleId = roleId,
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = species,
                    RequiredEmployerId = employer,
                },
            };
        }

        private static AgentState Agent(
            string id,
            int ordinal,
            string species,
            string employer,
            bool recognised,
            bool unrecognised,
            bool anomaly,
            bool commitment)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = "presentation." + id,
                DisplayName = id,
                SpeciesId = species,
                HouseholdId = "household." + id,
                EmployerId = employer,
                InstitutionalTrust = 50,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = 50,
                    Candour = 50,
                    Solidarity = 50,
                    Duty = 50,
                    InstitutionalReliance = 50,
                },
            };
            foreach (NeedKind need in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = need, Pressure = 20 });
            agent.Standing.SetRecognised("status.recognised", recognised);
            agent.Standing.SetRecognised("status.unrecognised", unrecognised);
            if (anomaly)
            {
                agent.AnomalyRules.Add(new AnomalyStatusRule
                {
                    TraitId = "trait.resonant",
                    RequiredOfficialStatusId = "status.recognised",
                    AffectedNeed = NeedKind.Safety,
                    RecognisedPressureDelta = -1,
                    UnrecognisedPressureDelta = 2,
                    MinimumTicksBetweenActivations = 3,
                    LastAppliedTick = -1,
                    ObservableEffectId = "effect." + id,
                });
            }
            if (commitment)
            {
                agent.Commitments.Add(new CommitmentState
                {
                    CommitmentId = "commitment." + id,
                    Kind = "commitment.care",
                    TargetId = employer,
                    Strength = 70,
                });
            }
            return agent;
        }

        private static AgentState Clone(AgentState source)
        {
            return Agent(
                source.StableId,
                source.SimulationOrdinal,
                source.SpeciesId,
                source.EmployerId,
                source.Standing.IsRecognised("status.recognised"),
                source.Standing.IsRecognised("status.unrecognised"),
                source.AnomalyRules.Count > 0,
                source.Commitments.Count > 0);
        }
    }
}
