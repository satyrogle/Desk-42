using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalScenarioEvidenceProjectorTests
    {
        [Test]
        public void Project_MapsOneEventToMultipleCasesWithUniqueIdsAndExactOnceCausality()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            InstitutionalConsequenceRun run = RunWithObservedActionAndAuthoritativeBelief();
            SimulationStepResult step = Step(Event(
                "event.006",
                SocietyEventKind.EvidenceDisclosed,
                null,
                "proposition.shared"));

            ScenarioEvidenceProjectionResult first =
                InstitutionalScenarioEvidenceProjector.Project(
                    run,
                    definition,
                    step,
                    Bindings());

            CollectionAssert.AreEqual(
                new[] { "template.alpha", "template.beta" },
                first.Records.Select(value => value.EvidenceTemplateId));
            CollectionAssert.AreEqual(
                new[]
                {
                    "artifact:event.006:template.alpha",
                    "artifact:event.006:template.beta",
                },
                first.Records.Select(value => value.ArtifactId));
            Assert.That(first.Records.All(value => value.Added), Is.True);

            EvidenceArtifact alpha = run.Report.EvidenceArtifacts.Single(value =>
                value.ArtifactId == "artifact:event.006:template.alpha");
            Assert.That(alpha.CaseId, Is.EqualTo("case.alpha"));
                Assert.That(alpha.IssueId, Is.EqualTo("issue.alpha"));
                Assert.That(alpha.EvidenceClassId, Is.EqualTo("evidence-class.alpha"));
                Assert.That(alpha.Kind, Is.EqualTo(EvidenceArtifactKind.ActionRecord));
                Assert.That(alpha.PropositionId, Is.EqualTo("proposition.shared"));
                Assert.That(alpha.Effect, Is.EqualTo(EvidenceEffect.SupportsFinding));
                Assert.That(alpha.BaseWeight, Is.EqualTo(25));
                Assert.That(alpha.EnteredAfterInitialRuling, Is.True);
                Assert.That(alpha.Provenance.ProvenanceId,
                    Is.EqualTo("provenance:event.006:template.alpha"));
            Assert.That(alpha.Provenance.Visibility,
                Is.EqualTo(EvidenceVisibility.OfficialRecord));

            EvidenceArtifact beta = run.Report.EvidenceArtifacts.Single(value =>
                value.ArtifactId == "artifact:event.006:template.beta");
            Assert.That(beta.CaseId, Is.EqualTo("case.beta"));
                Assert.That(beta.IssueId, Is.EqualTo("issue.beta"));
                Assert.That(beta.EvidenceClassId, Is.EqualTo("evidence-class.beta"));
                Assert.That(beta.Effect, Is.EqualTo(EvidenceEffect.OpposesFinding));
                Assert.That(beta.BaseWeight, Is.EqualTo(40));
                Assert.That(beta.EnteredAfterInitialRuling, Is.False);
            Assert.That(beta.Provenance.Visibility, Is.EqualTo(EvidenceVisibility.Private));

            CollectionAssert.AreEqual(
                first.Records.Select(value => value.ArtifactId),
                run.Report.ObservedAgentActions.Single().ResultEvidenceArtifactIds);
            Assert.That(run.Report.Timeline, Has.Count.EqualTo(2));
            Assert.That(run.Report.Timeline.All(value =>
                value.Kind == InstitutionalTimelineKind.EvidenceEntered), Is.True);
            Assert.That(run.AuthoritativeEvidenceLinks, Has.Count.EqualTo(2));
            Assert.That(run.AuthoritativeEvidenceLinks.All(value =>
                value.LivedEventId == "lived.fixture"), Is.True);
            Assert.That(run.Report.OfficialFindings, Is.Empty);
            Assert.That(run.Report.Rulings, Is.Empty);
            Assert.That(run.Report.OfficialStatusMutations, Is.Empty);

            ScenarioEvidenceProjectionResult second =
                InstitutionalScenarioEvidenceProjector.Project(
                    run,
                    definition,
                    step,
                    Bindings());

            Assert.That(second.Records.All(value => !value.Added), Is.True);
            Assert.That(run.Report.EvidenceArtifacts, Has.Count.EqualTo(2));
            Assert.That(run.Report.Timeline, Has.Count.EqualTo(2));
            Assert.That(run.AuthoritativeEvidenceLinks, Has.Count.EqualTo(2));
            Assert.That(run.Report.ObservedAgentActions.Single()
                .ResultEvidenceArtifactIds, Has.Count.EqualTo(2));
        }

        [Test]
        public void Project_FiltersTemplateSignaturesAndOrdersEventsDeterministically()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            InstitutionalConsequenceRun run = EmptyRun();
            var step = new SimulationStepResult { Tick = 6 };
            step.Events.Add(Event(
                "event.zulu",
                SocietyEventKind.EvidenceDisclosed,
                "opportunity.disclose",
                "proposition.shared"));
            step.Events.Add(Event(
                "event.mismatch-proposition",
                SocietyEventKind.EvidenceDisclosed,
                "opportunity.disclose",
                "proposition.other"));
            step.Events.Add(Event(
                "event.mismatch-kind",
                SocietyEventKind.WorkPerformed,
                "opportunity.disclose",
                "proposition.shared"));
            step.Events.Add(Event(
                "event.alpha",
                SocietyEventKind.EvidenceDisclosed,
                "opportunity.disclose",
                "proposition.shared"));

            ScenarioEvidenceProjectionResult result =
                InstitutionalScenarioEvidenceProjector.Project(run, definition, step);

            CollectionAssert.AreEqual(
                new[]
                {
                    "event.alpha/template.alpha",
                    "event.zulu/template.alpha",
                },
                result.Records.Select(value =>
                    $"{value.SourceEventId}/{value.EvidenceTemplateId}"));
            Assert.That(run.Report.EvidenceArtifacts, Has.Count.EqualTo(2));
        }

        [Test]
        public void Project_RejectsAmbiguousDuplicateEventTemplateMatchesBeforeMutation()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            InstitutionalConsequenceRun run = EmptyRun();
            SocietyEvent duplicate = Event(
                "event.duplicate",
                SocietyEventKind.EvidenceDisclosed,
                "opportunity.disclose",
                "proposition.shared");
            var step = new SimulationStepResult { Tick = 6 };
            step.Events.Add(duplicate);
            step.Events.Add(Event(
                "event.duplicate",
                SocietyEventKind.EvidenceDisclosed,
                "opportunity.disclose",
                "proposition.shared"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioEvidenceProjector.Project(run, definition, step));

            Assert.That(exception.Message, Does.Contain("Ambiguous"));
            Assert.That(exception.Message, Does.Contain("template.alpha"));
            Assert.That(run.Report.EvidenceArtifacts, Is.Empty);
            Assert.That(run.Report.Timeline, Is.Empty);
            Assert.That(run.AuthoritativeEvidenceLinks, Is.Empty);
        }

        [Test]
        public void ReplayRejectsChangedReliabilityProvenanceAndDuplicateReportIds()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            InstitutionalConsequenceRun run = EmptyRun();
            SocietyEvent source = Event(
                "event.replay",
                SocietyEventKind.EvidenceDisclosed,
                "opportunity.disclose",
                "proposition.shared");
            source.EvidenceReliability = 70;
            SimulationStepResult step = Step(source);
            InstitutionalScenarioEvidenceProjector.Project(run, definition, step);

            source.EvidenceReliability = 71;
            source.CauseDecisionId = "decision.changed";
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioEvidenceProjector.Project(run, definition, step));
            Assert.That(run.Report.EvidenceArtifacts, Has.Count.EqualTo(1));

            run.Report.EvidenceArtifacts.Add(run.Report.EvidenceArtifacts[0]);
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioEvidenceProjector.Project(run, definition, step));
        }

        [Test]
        public void Project_SuppressesOrdinaryEvidenceUntilDescendantHasOpened()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            InstitutionalConsequenceRun run = EmptyRun();
            SocietyEvent before = Event(
                "event.before-open",
                SocietyEventKind.EvidenceDisclosed,
                "opportunity.disclose",
                "proposition.shared");

            InstitutionalScenarioEvidenceProjector.Project(
                run,
                definition,
                Step(before),
                Bindings());

            Assert.That(run.Report.EvidenceArtifacts.Any(value =>
                value.CaseId == "case.beta"), Is.False);

            run.Report.DescendantCases.Add(new DescendantCase
            {
                CaseId = "case.beta",
                OpenedCycle = 7,
                Status = DescendantCaseStatus.Open,
            });
            SocietyEvent after = Event(
                "event.after-open",
                SocietyEventKind.EvidenceDisclosed,
                "opportunity.disclose",
                "proposition.shared");
            after.Tick = 8;
            SimulationStepResult afterStep = Step(after);
            afterStep.Tick = 8;

            InstitutionalScenarioEvidenceProjector.Project(
                run,
                definition,
                afterStep,
                Bindings());

            Assert.That(run.Report.EvidenceArtifacts.Count(value =>
                value.CaseId == "case.beta"), Is.EqualTo(1));
        }

        [Test]
        public void DeclaredEvidenceResolution_ZeroIsUnavailableAndMultipleRemainExact()
        {
            var report = new InstitutionalConsequenceReport();
            report.EvidenceArtifacts.Add(ResolvableArtifact(
                "event.zulu", "template.shared", "case.alpha", 4));
            report.EvidenceArtifacts.Add(ResolvableArtifact(
                "event.alpha", "template.shared", "case.alpha", 3));
            report.EvidenceArtifacts.Add(ResolvableArtifact(
                "event.other-case", "template.shared", "case.other", 2));
            report.EvidenceArtifacts.Add(ResolvableArtifact(
                "event.future", "template.shared", "case.alpha", 8));

            bool available = InstitutionalScenarioLookup.TryResolveEvidenceArtifactIds(
                report,
                new[] { "template.shared" },
                "case.alpha",
                5,
                "test evidence envelope",
                out List<string> artifactIds);
            bool missing = InstitutionalScenarioLookup.TryResolveEvidenceArtifactIds(
                report,
                new[] { "template.missing" },
                "case.alpha",
                5,
                "test missing evidence envelope",
                out List<string> missingIds);

            Assert.That(available, Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "artifact:event.alpha:template.shared",
                    "artifact:event.zulu:template.shared",
                },
                artifactIds);
            Assert.That(missing, Is.False);
            Assert.That(missingIds, Is.Empty);
        }

        [Test]
        public void DeclaredEvidenceResolution_RejectsInexactPresentProvenance()
        {
            var report = new InstitutionalConsequenceReport();
            EvidenceArtifact artifact = ResolvableArtifact(
                "event.source", "template.shared", "case.alpha", 3);
            artifact.Provenance.SourceSocietyEventId = "event.tampered";
            report.EvidenceArtifacts.Add(artifact);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioLookup.TryResolveEvidenceArtifactIds(
                    report,
                    new[] { "template.shared" },
                    "case.alpha",
                    5,
                    "test evidence envelope",
                    out _));

            Assert.That(exception.Message, Does.Contain("exact event-template provenance"));
        }

        private static InstitutionalConsequenceRun RunWithObservedActionAndAuthoritativeBelief()
        {
            InstitutionalConsequenceRun run = EmptyRun();
            run.Report.ObservedAgentActions.Add(new ObservedAgentAction
            {
                Cycle = 6,
                ActionEventId = "event.006",
                ActorId = "agent.claimant",
                Activity = ObservedActivityKind.EvidenceSubmitted,
            });
            run.AssessorActionTraces.Add(new AgentActionTrace
            {
                Cycle = 6,
                DecisionId = "decision.fixture",
                ActorId = "agent.claimant",
                Action = SocietyActionKind.Disclose,
                ResultEventIds = new List<string> { "event.006" },
            });
            run.AuthoritativeEvents.Add(new LivedEvent
            {
                LivedEventId = "lived.fixture",
                Cycle = 1,
                EventKindId = "event-kind.fixture",
                SubjectAgentId = "agent.claimant",
                CauseEntityId = "cause.fixture",
                AffectedNeed = NeedKind.Health,
                NeedPressureDelta = 10,
            });
            run.AuthoritativeBeliefLinks.Add(new AuthoritativeBeliefLink
            {
                LivedEventId = "lived.fixture",
                AgentId = "agent.claimant",
                BeliefId = "belief.fixture",
            });
            return run;
        }

        private static Dictionary<string, string> Bindings()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role.claimant"] = "agent.claimant",
                ["role.respondent"] = "agent.respondent",
            };
        }

        private static InstitutionalConsequenceRun EmptyRun()
        {
            return new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport(),
            };
        }

        private static EvidenceArtifact ResolvableArtifact(
            string sourceEventId,
            string templateId,
            string caseId,
            long cycle)
        {
            string decisionId = $"decision:{sourceEventId}";
            return new EvidenceArtifact
            {
                ArtifactId = $"artifact:{sourceEventId}:{templateId}",
                CaseId = caseId,
                EnteredCycle = cycle,
                SourceTemplateId = templateId,
                Provenance = new EvidenceProvenance
                {
                    ProvenanceId = $"provenance:{sourceEventId}:{templateId}",
                    CreatedCycle = cycle,
                    SourceSocietyEventId = sourceEventId,
                    SourceDecisionId = decisionId,
                    CreatedByAgentAction = true,
                    ChainOfCustodyIds = new List<string>
                    {
                        decisionId,
                        sourceEventId,
                    },
                },
            };
        }

        private static SimulationStepResult Step(params SocietyEvent[] events)
        {
            var result = new SimulationStepResult { Tick = 6 };
            result.Events.AddRange(events);
            return result;
        }

        private static SocietyEvent Event(
            string eventId,
            SocietyEventKind kind,
            string opportunityId,
            string propositionId)
        {
            return new SocietyEvent
            {
                EventId = eventId,
                CauseDecisionId = "decision.fixture",
                IncidentId = "incident.fixture",
                Tick = 6,
                Kind = kind,
                ActorId = "agent.claimant",
                TargetId = "agent.respondent",
                OpportunityId = opportunityId,
                EvidenceId = "evidence.fixture",
                EvidencePropositionId = propositionId,
                EvidenceSourceId = "record.fixture",
                EvidenceBeliefId = "belief.fixture",
                EvidenceReliability = 80,
                Visibility = EvidenceVisibility.Observable,
            };
        }

        private static InstitutionalScenarioDefinition ValidDefinition()
        {
            var definition = new InstitutionalScenarioDefinition
            {
                ScenarioId = "scenario.fixture",
                IncidentId = "incident.fixture",
                PrimaryCaseId = "case.alpha",
                StartCycle = 0,
                EndCycle = 10,
                InitialSociety = Society(),
            };

            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.claimant",
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.claimant",
                },
                DistinctFromRoleIds = new List<string> { "role.respondent" },
            });
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.respondent",
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.respondent",
                },
                DistinctFromRoleIds = new List<string> { "role.claimant" },
            });

            definition.Cases.Add(Case(
                "case.alpha",
                "issue.alpha",
                initialRulingCycle: 4));
            ScenarioCaseDefinition betaCase = Case(
                "case.beta",
                "issue.beta",
                initialRulingCycle: 8);
            betaCase.OpenCycle = 7;
            definition.Cases.Add(betaCase);

            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = "opportunity.disclose",
                Kind = ScenarioOpportunityKind.Work,
                PurposeId = "purpose.disclose",
                SourceCauseId = "cause.fixture",
                AvailabilityStartCycle = 6,
                AvailabilityEndCycle = 6,
                UtilityBonus = 10,
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { "role.claimant" },
            });
            for (long cycle = 1; cycle <= 5; cycle++)
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
                ScheduleEntryId = "schedule.006",
                IncidentId = "incident.fixture",
                Cycle = 6,
                WorkAvailable = true,
                DisclosureRequested = true,
                Visibility = ScenarioVisibilityMode.ListedRoles,
                VisibleRoleIds = new List<string> { "role.claimant" },
                ActiveOpportunityIds = new List<string> { "opportunity.disclose" },
            });
            for (long cycle = 7; cycle <= 10; cycle++)
            {
                definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
                {
                    ScheduleEntryId = $"schedule.{cycle:000}",
                    IncidentId = "incident.fixture",
                    Cycle = cycle,
                    Visibility = ScenarioVisibilityMode.NoBoundRoles,
                });
            }

            definition.DescendantCases.Add(
                new ScenarioActionCausedDescendantCaseDefinition
                {
                    DescendantDefinitionId = "descendant.case.beta",
                    CaseId = "case.beta",
                    ParentCaseId = "case.alpha",
                    OpenCycle = 7,
                    TriggerCycle = 6,
                    TriggerRoleId = "role.claimant",
                    TriggerActionKind = SocietyActionKind.Disclose,
                    TriggerOpportunityId = null,
                    TriggerPropositionId = "proposition.shared",
                    OriginatingRulingId = "ruling:case.alpha:initial:4",
                    ConnectedRoleIds = new List<string>
                    {
                        "role.claimant",
                        "role.respondent",
                    },
                });

            definition.EvidenceTemplates.Add(Template(
                "template.alpha",
                "case.alpha",
                "issue.alpha",
                "evidence-class.alpha",
                EvidenceEffect.SupportsFinding,
                25,
                EvidenceVisibility.OfficialRecord));
            definition.EvidenceTemplates.Add(Template(
                "template.beta",
                "case.beta",
                "issue.beta",
                "evidence-class.beta",
                EvidenceEffect.OpposesFinding,
                40,
                EvidenceVisibility.Private));

            InstitutionalScenarioDefinitionValidator.Validate(definition);
            return definition;
        }

        private static ScenarioCaseDefinition Case(
            string caseId,
            string issueId,
            long initialRulingCycle)
        {
            return new ScenarioCaseDefinition
            {
                CaseId = caseId,
                IssueId = issueId,
                ClaimantRoleId = "role.claimant",
                RespondentRoleId = "role.respondent",
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("fact.jurisdiction", caseId),
                }),
                OpenCycle = 0,
                InitialEvidenceCutoffCycle = initialRulingCycle,
                InitialRulingCycle = initialRulingCycle,
                AdjudicationEvidenceCutoffCycle = 10,
                AdjudicationCycle = 10,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "adjudication",
                InitialRulingId = $"ruling:{caseId}:initial:{initialRulingCycle}",
                AdjudicationRulingId = $"ruling:{caseId}:adjudication:10",
                InitialScoreThreshold = 50,
                ProvisionalScoreThreshold = 25,
                ProvisionalRecognitionPermitted = true,
                AdjudicationScoreThreshold = 60,
            };
        }

        private static ScenarioEvidenceTemplateDefinition Template(
            string templateId,
            string caseId,
            string issueId,
            string evidenceClassId,
            EvidenceEffect effect,
            int weight,
            EvidenceVisibility visibility)
        {
            return new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = templateId,
                SourceEventKind = SocietyEventKind.EvidenceDisclosed,
                SourceOpportunityId = null,
                RequiredPropositionId = "proposition.shared",
                CaseId = caseId,
                IssueId = issueId,
                EvidenceClassId = evidenceClassId,
                Effect = effect,
                Weight = weight,
                Visibility = visibility,
            };
        }

        private static SocietyState Society()
        {
            var society = new SocietyState
            {
                MasterSeed = 42,
                CurrentTick = 0,
                Regime = new InstitutionalRegimeState(),
            };
            society.Agents.Add(Agent(
                "agent.claimant", 0, "species.claimant"));
            society.Agents.Add(Agent(
                "agent.respondent", 1, "species.respondent"));
            return society;
        }

        private static AgentState Agent(string id, int ordinal, string speciesId)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = "presentation." + id,
                DisplayName = id,
                SpeciesId = speciesId,
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
            };
            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = kind, Pressure = 20 });
            return agent;
        }
    }
}
