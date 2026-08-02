using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalScenarioInputBuilderTests
    {
        [Test]
        public void Build_ProjectsOnlyNamedActiveOpportunitiesAndDetachesResolvedLists()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            Dictionary<string, string> bindings = ValidBindingsInReverseOrder();

            SimulationInput input = InstitutionalScenarioInputBuilder.Build(
                definition,
                cycle: 4,
                bindings);

            Assert.AreEqual("incident.fixture", input.IncidentId);
            Assert.IsTrue(input.WorkAvailable);
            Assert.IsTrue(input.AidAvailable);
            Assert.IsTrue(input.DisclosureRequested);
            Assert.IsTrue(input.AppealWindowOpen);
            Assert.AreEqual("docket.fixture", input.OpenDocketId);
            Assert.IsTrue(input.RestrictAidToOpportunities);
            Assert.IsTrue(input.RestrictAppealToOpportunities);
            CollectionAssert.AreEqual(new[] { "agent.alpha" }, input.VisibleAgentIds);

            WorkOpportunity work = input.WorkOpportunities.Single();
            Assert.AreEqual("op.work", work.OpportunityId);
            Assert.AreEqual("purpose.work", work.PurposeId);
            Assert.AreEqual("cause.work", work.SourceCauseId);
            Assert.AreEqual("employer.alpha", work.RequiredEmployerId);
            Assert.AreEqual("status.worker", work.RequiredOfficialStatusId);
            Assert.AreEqual(4, work.EarliestCycle);
            Assert.AreEqual(30, work.UtilityBonus);
            CollectionAssert.AreEqual(
                new[] { "agent.alpha", "agent.beta" },
                work.ParticipantAgentIds);

            AidOpportunity aid = input.AidOpportunities.Single();
            Assert.AreEqual("op.aid", aid.OpportunityId);
            Assert.AreEqual("purpose.aid", aid.PurposeId);
            Assert.AreEqual("cause.aid", aid.SourceCauseId);
            Assert.AreEqual("status.aid", aid.RequiredOfficialStatusId);
            Assert.AreEqual(20, aid.UtilityBonus);
            CollectionAssert.AreEqual(new[] { "agent.alpha" }, aid.EligibleAgentIds);

            AppealOpportunity appeal = input.AppealOpportunities.Single();
            Assert.AreEqual("op.appeal", appeal.OpportunityId);
            Assert.AreEqual("docket.fixture", appeal.DocketId);
            Assert.AreEqual("case.fixture", appeal.CaseId);
            Assert.AreEqual("ruling:case.fixture:initial:4", appeal.ChallengedRulingId);
            Assert.AreEqual("cause.appeal", appeal.SourceCauseId);
            Assert.AreEqual(8, appeal.HearingCycle);
            Assert.AreEqual(40, appeal.UtilityBonus);
            CollectionAssert.AreEqual(new[] { "agent.beta" }, appeal.PartyAgentIds);

            Assert.IsFalse(input.WorkOpportunities.Any(value =>
                value.OpportunityId == "op.work-later"),
                "An opportunity named by another cycle must not leak into this input.");

            definition.CycleSchedule.Single(value => value.Cycle == 4)
                .VisibleRoleIds[0] = "role.beta";
            definition.Opportunities.Single(value => value.OpportunityId == "op.work")
                .EligibleRoleIds[0] = "role.beta";
            bindings["role.alpha"] = "agent.beta";

            CollectionAssert.AreEqual(new[] { "agent.alpha" }, input.VisibleAgentIds);
            CollectionAssert.AreEqual(
                new[] { "agent.alpha", "agent.beta" },
                work.ParticipantAgentIds);
            Assert.AreNotSame(input.VisibleAgentIds, work.ParticipantAgentIds);
            Assert.AreNotSame(work.ParticipantAgentIds, aid.EligibleAgentIds);
            Assert.AreNotSame(aid.EligibleAgentIds, appeal.PartyAgentIds);
        }

        [Test]
        public void Build_AllBoundVisibilityUsesDeclaredRoleOrder_NotDictionaryOrder()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            ScenarioCycleScheduleEntry cycleFour = definition.CycleSchedule.Single(
                value => value.Cycle == 4);
            cycleFour.Visibility = ScenarioVisibilityMode.AllBoundRoles;
            cycleFour.VisibleRoleIds.Clear();

            SimulationInput input = InstitutionalScenarioInputBuilder.Build(
                definition,
                cycle: 4,
                ValidBindingsInReverseOrder());

            CollectionAssert.AreEqual(
                new[] { "agent.alpha", "agent.beta" },
                input.VisibleAgentIds);
        }

        [Test]
        public void Build_NoBoundVisibilityProducesAnExplicitEmptyDetachedList()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            ScenarioCycleScheduleEntry cycleFive = definition.CycleSchedule.Single(
                value => value.Cycle == 5);
            cycleFive.Visibility = ScenarioVisibilityMode.NoBoundRoles;
            cycleFive.VisibleRoleIds.Clear();

            SimulationInput input = InstitutionalScenarioInputBuilder.Build(
                definition,
                cycle: 5,
                ValidBindingsInReverseOrder());

            Assert.IsNotNull(input.VisibleAgentIds);
            Assert.IsEmpty(input.VisibleAgentIds);
            Assert.IsFalse(input.RestrictAidToOpportunities);
            Assert.IsFalse(input.RestrictAppealToOpportunities);
            Assert.AreEqual("op.work-later", input.WorkOpportunities.Single().OpportunityId);
        }

        [Test]
        public void Build_RequiresExactlyOneScheduleEntryForRequestedCycle()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.CycleSchedule.RemoveAll(value => value.Cycle == 6);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioInputBuilder.Build(
                    definition,
                    cycle: 6,
                    ValidBindingsInReverseOrder()));

            Assert.That(exception.Message, Does.Contain("every cycle"));
        }

        [Test]
        public void Build_RejectsMissingUnknownOrNonDistinctRoleBindings()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            Dictionary<string, string> missing = ValidBindingsInReverseOrder();
            missing.Remove("role.alpha");
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioInputBuilder.Build(definition, 4, missing));

            Dictionary<string, string> unknownRole = ValidBindingsInReverseOrder();
            unknownRole.Add("role.unknown", "agent.alpha");
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioInputBuilder.Build(definition, 4, unknownRole));

            Dictionary<string, string> unknownAgent = ValidBindingsInReverseOrder();
            unknownAgent["role.alpha"] = "agent.unknown";
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioInputBuilder.Build(definition, 4, unknownAgent));

            Dictionary<string, string> sameAgent = ValidBindingsInReverseOrder();
            sameAgent["role.beta"] = "agent.alpha";
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioInputBuilder.Build(definition, 4, sameAgent));
        }

        [Test]
        public void Build_RejectsOpportunityAvailabilityAndDecisionWindowMismatches()
        {
            InstitutionalScenarioDefinition outsideWindow = ValidDefinition();
            outsideWindow.Opportunities.Single(value => value.OpportunityId == "op.aid")
                .AvailabilityEndCycle = 3;
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioInputBuilder.Build(
                    outsideWindow,
                    4,
                    ValidBindingsInReverseOrder()));

            InstitutionalScenarioDefinition closedKindWindow = ValidDefinition();
            closedKindWindow.CycleSchedule.Single(value => value.Cycle == 4)
                .AidAvailable = false;
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioInputBuilder.Build(
                    closedKindWindow,
                    4,
                    ValidBindingsInReverseOrder()));
        }

        [Test]
        public void Build_RejectsKindRestrictionsTheRuntimeInputCannotRepresent()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.Opportunities.Single(value => value.OpportunityId == "op.aid")
                .RequiredEmployerId = "employer.alpha";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioInputBuilder.Build(
                    definition,
                    4,
                    ValidBindingsInReverseOrder()));

            Assert.That(exception.Message, Does.Contain("cannot declare"));
        }

        [Test]
        public void Build_GatesDescendantAppealUntilTheCaseHasMaterialised()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.Cases.Add(new ScenarioCaseDefinition
            {
                CaseId = "case.zeta",
                IssueId = "issue.zeta",
                ClaimantRoleId = "role.alpha",
                RespondentRoleId = "role.beta",
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("fact.jurisdiction", "zeta"),
                }),
                OpenCycle = 5,
                InitialEvidenceCutoffCycle = 6,
                InitialRulingCycle = 6,
                AdjudicationEvidenceCutoffCycle = 8,
                AdjudicationCycle = 8,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "adjudication",
                InitialRulingId = "ruling:case.zeta:initial:6",
                AdjudicationRulingId = "ruling:case.zeta:adjudication:8",
                InitialScoreThreshold = 50,
                ProvisionalScoreThreshold = 25,
                ProvisionalRecognitionPermitted = true,
                AdjudicationScoreThreshold = 60,
            });
            definition.DescendantCases.Add(
                new ScenarioActionCausedDescendantCaseDefinition
                {
                    DescendantDefinitionId = "descendant.case.zeta",
                    CaseId = "case.zeta",
                    ParentCaseId = "case.fixture",
                    OpenCycle = 5,
                    TriggerCycle = 4,
                    TriggerRoleId = "role.alpha",
                    TriggerActionKind = SocietyActionKind.Work,
                    TriggerOpportunityId = "op.work",
                    OriginatingRulingId = "ruling:case.fixture:initial:4",
                    ConnectedRoleIds = new List<string>
                    {
                        "role.alpha",
                        "role.beta",
                    },
                });
            definition.Opportunities.Insert(2, new ScenarioOpportunityDefinition
            {
                OpportunityId = "op.appeal-descendant",
                Kind = ScenarioOpportunityKind.Appeal,
                PurposeId = "purpose.appeal-descendant",
                SourceCauseId = "cause.appeal-descendant",
                AvailabilityStartCycle = 4,
                AvailabilityEndCycle = 5,
                UtilityBonus = 40,
                CaseId = "case.zeta",
                ChallengedRulingId = "ruling:case.zeta:initial:6",
                HearingCycle = 8,
                EligibleRoleIds = new List<string> { "role.beta" },
            });
            definition.CycleSchedule.Single(value => value.Cycle == 4)
                .ActiveOpportunityIds.Insert(2, "op.appeal-descendant");
            ScenarioCycleScheduleEntry cycleFive = definition.CycleSchedule.Single(
                value => value.Cycle == 5);
            cycleFive.AppealWindowOpen = true;
            cycleFive.OpenDocketId = "docket.fixture";
            cycleFive.ActiveOpportunityIds.Insert(0, "op.appeal-descendant");

            var report = new InstitutionalConsequenceReport
            {
                PrimaryCaseId = definition.PrimaryCaseId,
            };
            SimulationInput beforeOpening = InstitutionalScenarioInputBuilder.Build(
                definition,
                4,
                ValidBindingsInReverseOrder(),
                report);

            Assert.That(beforeOpening.AppealOpportunities.Select(value => value.OpportunityId),
                Does.Not.Contain("op.appeal-descendant"));
            Assert.That(beforeOpening.RestrictAppealToOpportunities, Is.True);

            report.DescendantCases.Add(new DescendantCase
            {
                CaseId = "case.zeta",
                OpenedCycle = 5,
                Status = DescendantCaseStatus.Open,
            });
            SimulationInput afterOpening = InstitutionalScenarioInputBuilder.Build(
                definition,
                5,
                ValidBindingsInReverseOrder(),
                report);

            Assert.That(afterOpening.AppealOpportunities.Single().OpportunityId,
                Is.EqualTo("op.appeal-descendant"));
        }

        private static Dictionary<string, string> ValidBindingsInReverseOrder()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role.beta"] = "agent.beta",
                ["role.alpha"] = "agent.alpha",
            };
        }

        private static InstitutionalScenarioDefinition ValidDefinition()
        {
            var definition = new InstitutionalScenarioDefinition
            {
                ScenarioId = "scenario.fixture",
                IncidentId = "incident.fixture",
                PrimaryCaseId = "case.fixture",
                StartCycle = 0,
                EndCycle = 10,
                InitialSociety = CreateSociety(),
            };

            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.alpha",
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.alpha",
                },
                DistinctFromRoleIds = new List<string> { "role.beta" },
            });
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.beta",
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.beta",
                },
                DistinctFromRoleIds = new List<string> { "role.alpha" },
            });

            definition.Cases.Add(new ScenarioCaseDefinition
            {
                CaseId = "case.fixture",
                IssueId = "issue.fixture",
                ClaimantRoleId = "role.alpha",
                RespondentRoleId = "role.beta",
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("fact.jurisdiction", "fixture"),
                }),
                OpenCycle = 0,
                InitialEvidenceCutoffCycle = 4,
                InitialRulingCycle = 4,
                AdjudicationEvidenceCutoffCycle = 8,
                AdjudicationCycle = 8,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "adjudication",
                InitialRulingId = "ruling:case.fixture:initial:4",
                AdjudicationRulingId = "ruling:case.fixture:adjudication:8",
                InitialScoreThreshold = 50,
                ProvisionalScoreThreshold = 25,
                ProvisionalRecognitionPermitted = true,
                AdjudicationScoreThreshold = 60,
            });

            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = "op.aid",
                Kind = ScenarioOpportunityKind.Aid,
                PurposeId = "purpose.aid",
                SourceCauseId = "cause.aid",
                AvailabilityStartCycle = 4,
                AvailabilityEndCycle = 4,
                UtilityBonus = 20,
                RequiredOfficialStatusId = "status.aid",
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { "role.alpha" },
            });
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = "op.appeal",
                Kind = ScenarioOpportunityKind.Appeal,
                PurposeId = "purpose.appeal",
                SourceCauseId = "cause.appeal",
                AvailabilityStartCycle = 4,
                AvailabilityEndCycle = 4,
                UtilityBonus = 40,
                CaseId = "case.fixture",
                ChallengedRulingId = "ruling:case.fixture:initial:4",
                HearingCycle = 8,
                EligibleRoleIds = new List<string> { "role.beta" },
            });
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = "op.work",
                Kind = ScenarioOpportunityKind.Work,
                PurposeId = "purpose.work",
                SourceCauseId = "cause.work",
                AvailabilityStartCycle = 4,
                AvailabilityEndCycle = 4,
                UtilityBonus = 30,
                RequiredEmployerId = "employer.alpha",
                RequiredOfficialStatusId = "status.worker",
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { "role.alpha", "role.beta" },
            });
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = "op.work-later",
                Kind = ScenarioOpportunityKind.Work,
                PurposeId = "purpose.work-later",
                SourceCauseId = "cause.work-later",
                AvailabilityStartCycle = 5,
                AvailabilityEndCycle = 5,
                UtilityBonus = 10,
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { "role.beta" },
            });

            for (long cycle = 1; cycle <= 3; cycle++)
            {
                definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
                {
                    ScheduleEntryId = $"schedule.{cycle:000}",
                    IncidentId = "incident.fixture",
                    Cycle = cycle,
                    Visibility = ScenarioVisibilityMode.NoBoundRoles,
                });
            }
            definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
            {
                ScheduleEntryId = "schedule.004",
                IncidentId = "incident.fixture",
                Cycle = 4,
                WorkAvailable = true,
                AidAvailable = true,
                DisclosureRequested = true,
                AppealWindowOpen = true,
                OpenDocketId = "docket.fixture",
                Visibility = ScenarioVisibilityMode.ListedRoles,
                VisibleRoleIds = new List<string> { "role.alpha" },
                ActiveOpportunityIds = new List<string>
                {
                    "op.aid",
                    "op.appeal",
                    "op.work",
                },
            });
            definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
            {
                ScheduleEntryId = "schedule.005",
                IncidentId = "incident.fixture",
                Cycle = 5,
                WorkAvailable = true,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
                Visibility = ScenarioVisibilityMode.ListedRoles,
                VisibleRoleIds = new List<string> { "role.beta" },
                ActiveOpportunityIds = new List<string> { "op.work-later" },
            });
            for (long cycle = 6; cycle <= 10; cycle++)
            {
                definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
                {
                    ScheduleEntryId = $"schedule.{cycle:000}",
                    IncidentId = "incident.fixture",
                    Cycle = cycle,
                    Visibility = ScenarioVisibilityMode.NoBoundRoles,
                });
            }

            definition.EvidenceTemplates.Add(new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = "evidence.fixture",
                SourceEventKind = SocietyEventKind.WorkPerformed,
                SourceOpportunityId = "op.work",
                RequiredPropositionId = null,
                CaseId = "case.fixture",
                IssueId = "issue.fixture",
                EvidenceClassId = "evidence-class.fixture",
                Effect = EvidenceEffect.SupportsFinding,
                Weight = 25,
                Visibility = EvidenceVisibility.OfficialRecord,
            });

            InstitutionalScenarioDefinitionValidator.Validate(definition);
            return definition;
        }

        private static SocietyState CreateSociety()
        {
            var society = new SocietyState
            {
                MasterSeed = 42,
                CurrentTick = 0,
                Regime = new InstitutionalRegimeState(),
            };
            society.Agents.Add(CreateAgent("agent.alpha", 0, "species.alpha", "employer.alpha"));
            society.Agents.Add(CreateAgent("agent.beta", 1, "species.beta", "employer.beta"));
            return society;
        }

        private static AgentState CreateAgent(
            string stableId,
            int ordinal,
            string speciesId,
            string employerId)
        {
            var agent = new AgentState
            {
                StableId = stableId,
                SimulationOrdinal = ordinal,
                PresentationId = "presentation." + stableId,
                DisplayName = stableId,
                SpeciesId = speciesId,
                HouseholdId = "household." + stableId,
                EmployerId = employerId,
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
            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
            {
                agent.Needs.Add(new NeedState
                {
                    Kind = kind,
                    Pressure = 20,
                });
            }
            return agent;
        }
    }
}
