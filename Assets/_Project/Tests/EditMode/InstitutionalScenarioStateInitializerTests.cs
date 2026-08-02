using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalScenarioStateInitializerTests
    {
        [Test]
        public void InitialiseDeclaredState_CreatesOnlyDeclaredRows_AndIsIdempotent()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            SocietyState society = SocietyStateDeepCopy.Copy(definition.InitialSociety);
            InstitutionalConsequenceRun run = EmptyRun();
            IReadOnlyDictionary<string, string> bindings = Bindings();

            ScenarioRunStateInitializationResult first =
                InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                    definition, society, run, bindings);

            Assert.That(first.Applied, Is.True);
            Assert.That(run.FinalSocietyState, Is.SameAs(society));
            Assert.That(run.EconomicAccounts, Has.Count.EqualTo(1));
            Assert.That(run.EconomicAccounts[0].AgentId, Is.EqualTo("agent.alpha"));
            Assert.That(run.EconomicAccounts[0].AvailableCredits, Is.EqualTo(73));
            Assert.That(run.EconomicAccounts[0].CommittedIncome, Is.EqualTo(9));
            Assert.That(first.AccountsById["account.alpha"],
                Is.SameAs(run.EconomicAccounts[0]));
            Assert.That(run.AlternativeOptions, Has.Count.EqualTo(1));
            Assert.That(run.AlternativeOptions[0].OptionId, Is.EqualTo("alternative.alpha"));
            Assert.That(run.AlternativeOptions[0].AgentId, Is.EqualTo("agent.alpha"));
            Assert.That(run.AlternativeOptions[0].Available, Is.True);
            Assert.That(run.AlternativeOptions[0].ChangedByActionEventId, Is.Null);
            Assert.That(first.AlternativesByKey["alternative.alpha"],
                Is.SameAs(run.AlternativeOptions[0]));
            Assert.That(first.AlternativeResourceValuesByKey["alternative.alpha"],
                Is.EqualTo(18));

            ScenarioRunStateInitializationResult replay =
                InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                    definition, society, run, bindings);

            Assert.That(replay.Applied, Is.False);
            Assert.That(run.EconomicAccounts, Has.Count.EqualTo(1));
            Assert.That(run.AlternativeOptions, Has.Count.EqualTo(1));
        }

        [Test]
        public void ApplyLivedIncidentSeeds_ClampsAndLinksAuthority_WithoutLeakingTruth()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            SocietyState society = SocietyStateDeepCopy.Copy(definition.InitialSociety);
            InstitutionalConsequenceRun run = EmptyRun();
            IReadOnlyDictionary<string, string> bindings = Bindings();
            InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                definition, society, run, bindings);

            ScenarioLivedIncidentSeedBatchResult first =
                InstitutionalScenarioStateInitializer.ApplyLivedIncidentSeeds(
                    definition, society, run, bindings, 1);

            Assert.That(first.Applied, Is.True);
            Assert.That(society.CurrentTick, Is.EqualTo(0),
                "Pre-decision incident seeding must not advance the simulation clock.");
            Assert.That(first.Applications, Has.Count.EqualTo(1));
            Assert.That(first.Applications[0].NeedPressureBefore, Is.EqualTo(96));
            Assert.That(first.Applications[0].NeedPressureAfter, Is.EqualTo(100));
            Assert.That(society.GetAgent("agent.alpha").GetNeed(NeedKind.Safety).Pressure,
                Is.EqualTo(100));
            Assert.That(run.AuthoritativeEvents, Has.Count.EqualTo(1));
            Assert.That(run.AuthoritativeEvents[0].LivedEventId,
                Is.EqualTo("lived:seed.alpha"));
            Assert.That(run.AuthoritativeEvents[0].CauseEntityId,
                Is.EqualTo("entity.hidden-cause"));
            Assert.That(run.AuthoritativeBeliefLinks.Select(value => value.BeliefId),
                Is.EquivalentTo(new[] { "belief.match" }));
            Assert.That(run.AuthoritativeEvidenceLinks, Is.Empty);
            Assert.That(run.Report.EvidenceArtifacts, Is.Empty);
            Assert.That(run.Report.Timeline, Is.Empty,
                "An authority-only lived seed cannot manufacture a public incident report.");

            ScenarioLivedIncidentSeedBatchResult replay =
                InstitutionalScenarioStateInitializer.ApplyLivedIncidentSeeds(
                    definition, society, run, bindings, 1);

            Assert.That(replay.Applied, Is.False);
            Assert.That(run.AuthoritativeEvents, Has.Count.EqualTo(1));
            Assert.That(run.AuthoritativeBeliefLinks, Has.Count.EqualTo(1));
            Assert.That(run.Report.Timeline, Is.Empty);
            Assert.That(society.GetAgent("agent.alpha").GetNeed(NeedKind.Safety).Pressure,
                Is.EqualTo(100));
        }

        [Test]
        public void Boundary_RejectsWrongRoleWrongTickAndMissingNeed()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            SocietyState society = SocietyStateDeepCopy.Copy(definition.InitialSociety);
            InstitutionalConsequenceRun run = EmptyRun();
            var wrongBindings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role.alpha"] = "agent.beta",
                ["role.beta"] = "agent.beta",
            };

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                    definition, society, run, wrongBindings));

            run = EmptyRun();
            InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                definition, society, run, Bindings());
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioStateInitializer.ApplyLivedIncidentSeeds(
                    definition, society, run, Bindings(), 2));

            society.CurrentTick = 1;
            society.GetAgent("agent.alpha").Needs.RemoveAll(value =>
                value.Kind == NeedKind.Safety);
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioStateInitializer.ApplyLivedIncidentSeeds(
                    definition, society, run, Bindings(), 1));
        }

        [Test]
        public void NewIncidentCannotBeBackfilledAfterItsDecisionPulse()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            SocietyState society = SocietyStateDeepCopy.Copy(definition.InitialSociety);
            InstitutionalConsequenceRun run = EmptyRun();
            InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                definition, society, run, Bindings());
            society.CurrentTick = 1;

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioStateInitializer.ApplyLivedIncidentSeeds(
                    definition, society, run, Bindings(), 1));
            Assert.That(run.AuthoritativeEvents, Is.Empty);
            Assert.That(run.Report.Timeline, Is.Empty);
        }

        [Test]
        public void Boundary_RejectsMissingCollectionsPartialStateAndDuplicateLivedIds()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            SocietyState society = SocietyStateDeepCopy.Copy(definition.InitialSociety);
            InstitutionalConsequenceRun missing = EmptyRun();
            missing.EconomicAccounts = null;
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                    definition, society, missing, Bindings()));

            InstitutionalConsequenceRun partial = EmptyRun();
            partial.AlternativeOptions.Add(new AlternativeOptionState
            {
                OptionId = "alternative.alpha",
                AgentId = "agent.alpha",
                Available = true,
            });
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                    definition, society, partial, Bindings()));

            InstitutionalConsequenceRun duplicate = EmptyRun();
            InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                definition, society, duplicate, Bindings());
            society.CurrentTick = 1;
            duplicate.AuthoritativeEvents.Add(Lived("lived:duplicate"));
            duplicate.AuthoritativeEvents.Add(Lived("lived:duplicate"));
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioStateInitializer.ApplyLivedIncidentSeeds(
                    definition, society, duplicate, Bindings(), 1));
        }

        private static LivedEvent Lived(string id)
        {
            return new LivedEvent
            {
                LivedEventId = id,
                Cycle = 0,
                EventKindId = "incident.other",
                SubjectAgentId = "agent.alpha",
                CauseEntityId = "entity.other",
                AffectedNeed = NeedKind.Health,
                NeedPressureDelta = 1,
            };
        }

        private static IReadOnlyDictionary<string, string> Bindings()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role.alpha"] = "agent.alpha",
                ["role.beta"] = "agent.beta",
            };
        }

        private static InstitutionalConsequenceRun EmptyRun()
        {
            return new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport
                {
                    MasterSeed = 42,
                    PolicyConfigurationId = "policy.fixture",
                    PrimaryCaseId = "case.primary",
                },
            };
        }

        private static InstitutionalScenarioDefinition ValidDefinition()
        {
            var definition = new InstitutionalScenarioDefinition
            {
                ScenarioId = "scenario.state-initializer",
                IncidentId = "incident.primary",
                PrimaryCaseId = "case.primary",
                StartCycle = 0,
                EndCycle = 4,
                InitialSociety = new SocietyState
                {
                    MasterSeed = 42,
                    CurrentTick = 0,
                    Regime = new InstitutionalRegimeState(),
                },
            };
            definition.InitialSociety.Agents.Add(AgentAlpha());
            definition.InitialSociety.Agents.Add(AgentBeta());
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.alpha",
                Query = new ScenarioParticipantQuery { RequiredSpeciesId = "species.alpha" },
                DistinctFromRoleIds = new List<string> { "role.beta" },
            });
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.beta",
                Query = new ScenarioParticipantQuery { RequiredSpeciesId = "species.beta" },
                DistinctFromRoleIds = new List<string> { "role.alpha" },
            });
            definition.LivedIncidentSeeds.Add(new ScenarioLivedIncidentSeedDefinition
            {
                IncidentSeedId = "seed.alpha",
                IncidentId = "incident.primary",
                Cycle = 1,
                SubjectRoleId = "role.alpha",
                CauseEntityId = "entity.hidden-cause",
                PropositionId = "proposition.hidden-truth",
                AffectedNeed = NeedKind.Safety,
                NeedPressureDelta = 10,
            });
            definition.InitialEconomicAccounts.Add(new ScenarioInitialEconomicAccountDefinition
            {
                AccountId = "account.alpha",
                OwnerRoleId = "role.alpha",
                InitialCredits = 73,
                CycleIncome = 9,
            });
            definition.Alternatives.Add(new ScenarioAlternativeDefinition
            {
                AlternativeKey = "alternative.alpha",
                OwnerRoleId = "role.alpha",
                InitiallyAvailable = true,
                ResourceValue = 18,
            });
            var facts = new CaseFactSet();
            facts.Add("fact.jurisdiction", "fixture");
            definition.Cases.Add(new ScenarioCaseDefinition
            {
                CaseId = "case.primary",
                IssueId = "issue.primary",
                ClaimantRoleId = "role.alpha",
                RespondentRoleId = "role.beta",
                Facts = facts,
                OpenCycle = 0,
                InitialEvidenceCutoffCycle = 2,
                InitialRulingCycle = 2,
                AdjudicationEvidenceCutoffCycle = 4,
                AdjudicationCycle = 4,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "adjudication",
                InitialRulingId = "ruling:case.primary:initial:2",
                AdjudicationRulingId = "ruling:case.primary:adjudication:4",
                InitialScoreThreshold = 20,
                ProvisionalScoreThreshold = 10,
                ProvisionalRecognitionPermitted = true,
                AdjudicationScoreThreshold = 20,
            });
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = "opportunity.work",
                Kind = ScenarioOpportunityKind.Work,
                PurposeId = "purpose.work",
                SourceCauseId = "cause.schedule",
                AvailabilityStartCycle = 1,
                AvailabilityEndCycle = 1,
                UtilityBonus = 0,
                EligibleRoleIds = new List<string> { "role.beta" },
            });
            definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
            {
                ScheduleEntryId = "schedule.1",
                IncidentId = "incident.primary",
                Cycle = 1,
                WorkAvailable = true,
                Visibility = ScenarioVisibilityMode.AllBoundRoles,
                ActiveOpportunityIds = new List<string> { "opportunity.work" },
            });
            for (long cycle = 2; cycle <= 4; cycle++)
            {
                definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
                {
                    ScheduleEntryId = $"schedule.{cycle}",
                    IncidentId = "incident.primary",
                    Cycle = cycle,
                    Visibility = ScenarioVisibilityMode.NoBoundRoles,
                });
            }
            definition.EvidenceTemplates.Add(new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = "evidence.work",
                SourceEventKind = SocietyEventKind.WorkPerformed,
                SourceOpportunityId = "opportunity.work",
                CaseId = "case.primary",
                IssueId = "issue.primary",
                EvidenceClassId = "class.work",
                Effect = EvidenceEffect.Neutral,
                Weight = 1,
                Visibility = EvidenceVisibility.Observable,
            });
            InstitutionalScenarioDefinitionValidator.Validate(definition);
            return definition;
        }

        private static AgentState AgentAlpha()
        {
            AgentState agent = Agent("agent.alpha", 0, "species.alpha");
            agent.GetNeed(NeedKind.Safety).Pressure = 96;
            agent.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.match",
                PropositionId = "proposition.hidden-truth",
                SubjectId = "agent.alpha",
                ObjectId = "entity.hidden-cause",
                SourceId = "source.embodied",
                Confidence = 90,
                Secrecy = 10,
                EmotionalWeight = 80,
                AcquiredTick = 0,
            });
            agent.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.wrong-subject",
                PropositionId = "proposition.hidden-truth",
                SubjectId = "agent.beta",
                ObjectId = "entity.hidden-cause",
                SourceId = "source.hearsay",
                Confidence = 60,
                Secrecy = 20,
                EmotionalWeight = 20,
                AcquiredTick = 0,
            });
            return agent;
        }

        private static AgentState AgentBeta()
        {
            return Agent("agent.beta", 1, "species.beta");
        }

        private static AgentState Agent(string id, int ordinal, string species)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = "presentation." + id,
                DisplayName = id,
                SpeciesId = species,
                HouseholdId = "household." + id,
                EmployerId = "employer.fixture",
                InstitutionalTrust = 50,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = 50,
                    Candour = 50,
                    Solidarity = 50,
                    Duty = 50,
                    InstitutionalReliance = 50,
                },
                Standing = new InstitutionalStandingState(),
            };
            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = kind, Pressure = 20 });
            return agent;
        }
    }
}
