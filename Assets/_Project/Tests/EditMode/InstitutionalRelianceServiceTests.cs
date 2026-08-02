using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalRelianceServiceTests
    {
        private const string RelianceOpportunityId = "opportunity.test.reliance";
        private const string ActorId = "agent.test.actor";
        private const string BeneficiaryId = "agent.test.beneficiary";
        private const string RelatedId = "agent.test.related";
        private const string StatusId = "status.test.access";
        private const string RulingId = "ruling.test.access";
        private const string MutationId = "mutation.test.access";

        [Test]
        public void Create_VerifiesCausalStatusAndAppliesThreeRoleEffects()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.rely", 3, true);
            run.AlternativeOptions.Add(new AlternativeOptionState
            {
                OptionId = "alternative.test.unrelated",
                AgentId = RelatedId,
                Available = true,
            });
            run.AlternativeOptions.Add(new AlternativeOptionState
            {
                OptionId = "alternative.test.abandoned",
                AgentId = ActorId,
                Available = true,
            });

            RelianceCreationResult result = InstitutionalRelianceService.TryCreate(
                run,
                CreateRequest(
                    "reliance.test.one",
                    "observation.test.one",
                    "action.test.rely",
                    "alternative.test.abandoned"));

            Assert.IsTrue(result.Created);
            Assert.AreEqual(RelianceFailureReason.None, result.FailureReason);
            Assert.NotNull(result.Reliance);
            Assert.NotNull(result.Observation);
            Assert.AreEqual(3, result.MaterialConsequences.Count);
            Assert.AreEqual(-20, result.Observation.RecordedResourceDelta);
            Assert.AreEqual(RulingId, result.Observation.EnablingRulingId);
            Assert.AreEqual(MutationId, result.Observation.EnablingMutationId);
            Assert.AreEqual("alternative.test.abandoned",
                result.Reliance.AbandonedAlternativeId);

            Assert.AreEqual(80, FindAccount(run, ActorId).AvailableCredits);
            Assert.AreEqual(105, FindAccount(run, BeneficiaryId).AvailableCredits);
            Assert.AreEqual(97, FindAccount(run, RelatedId).AvailableCredits);
            Assert.AreEqual(50,
                run.FinalSocietyState.GetAgent(ActorId)
                    .GetNeed(NeedKind.Subsistence).Pressure);
            Assert.AreEqual(23,
                run.FinalSocietyState.GetAgent(BeneficiaryId)
                    .GetNeed(NeedKind.Health).Pressure);
            Assert.AreEqual(34,
                run.FinalSocietyState.GetAgent(RelatedId)
                    .GetNeed(NeedKind.Safety).Pressure);

            Assert.IsFalse(run.AlternativeOptions.Single(value =>
                value.OptionId == "alternative.test.abandoned").Available);
            Assert.IsTrue(run.AlternativeOptions.Single(value =>
                value.OptionId == "alternative.test.unrelated").Available);
            Assert.AreEqual("action.test.rely", run.AlternativeOptions.Single(value =>
                value.OptionId == "alternative.test.abandoned").ChangedByActionEventId);
            Assert.That(run.Report.Timeline.Any(entry =>
                entry.Kind == InstitutionalTimelineKind.RelianceCreated &&
                entry.CauseId == "action.test.rely" &&
                entry.DetailId == "observation.test.one"));
        }

        [Test]
        public void Create_WhenTraceDidNotReadStatus_IsExplicitFailureAndAtomic()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.unrelated", 3, false);
            run.AlternativeOptions.Add(new AlternativeOptionState
            {
                OptionId = "alternative.test.keep",
                AgentId = RelatedId,
                Available = true,
            });
            int actorCredits = FindAccount(run, ActorId).AvailableCredits;
            int timelineCount = run.Report.Timeline.Count;

            RelianceCreationResult result = InstitutionalRelianceService.TryCreate(
                run,
                CreateRequest(
                    "reliance.test.rejected",
                    "observation.test.rejected",
                    "action.test.unrelated",
                    "alternative.test.keep"));

            Assert.IsFalse(result.Created);
            Assert.AreEqual(
                RelianceFailureReason.ActionDidNotReadRequiredStatus,
                result.FailureReason);
            Assert.IsNull(result.Reliance);
            Assert.IsNull(result.Observation);
            Assert.IsEmpty(run.RelianceLedger);
            Assert.IsEmpty(run.Report.RelianceObservations);
            Assert.IsEmpty(run.Report.MaterialConsequences);
            Assert.AreEqual(actorCredits, FindAccount(run, ActorId).AvailableCredits);
            Assert.IsTrue(run.AlternativeOptions.Single().Available);
            Assert.AreEqual(timelineCount, run.Report.Timeline.Count);
        }

        [Test]
        public void Create_MissingAlternativeKey_DoesNotConsumeFirstAlternative()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.missing-option", 3, true);
            run.AlternativeOptions.Add(new AlternativeOptionState
            {
                OptionId = "alternative.test.first",
                AgentId = RelatedId,
                Available = true,
            });

            RelianceCreationResult result = InstitutionalRelianceService.TryCreate(
                run,
                CreateRequest(
                    "reliance.test.missing-option",
                    "observation.test.missing-option",
                    "action.test.missing-option",
                    "alternative.test.not-present"));

            Assert.IsFalse(result.Created);
            Assert.AreEqual(
                RelianceFailureReason.AlternativeNotFound,
                result.FailureReason);
            Assert.IsTrue(run.AlternativeOptions.Single().Available);
            Assert.IsNull(run.AlternativeOptions.Single().ChangedByActionEventId);
            Assert.IsEmpty(run.RelianceLedger);
        }

        [Test]
        public void Create_LateTimelineIdCollision_IsRejectedWithoutAnyStateMutation()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.atomic-late-failure", 3, true);
            AddAlternative(run, "alternative.test.atomic-late-failure");

            int collidingIndex = run.Report.Timeline.Count + 1;
            run.Report.Timeline.Add(new InstitutionalTimelineEntry
            {
                EntryId =
                    $"timeline:3:{collidingIndex}:" +
                    $"{InstitutionalTimelineKind.RelianceCreated}",
                Cycle = 1,
                Kind = InstitutionalTimelineKind.Incident,
                CauseId = "fixture.timeline-collision",
            });

            int actorCredits = FindAccount(run, ActorId).AvailableCredits;
            int beneficiaryCredits = FindAccount(run, BeneficiaryId).AvailableCredits;
            int relatedCredits = FindAccount(run, RelatedId).AvailableCredits;
            int actorSubsistence = run.FinalSocietyState.GetAgent(ActorId)
                .GetNeed(NeedKind.Subsistence).Pressure;
            int beneficiaryHealth = run.FinalSocietyState.GetAgent(BeneficiaryId)
                .GetNeed(NeedKind.Health).Pressure;
            int relatedSafety = run.FinalSocietyState.GetAgent(RelatedId)
                .GetNeed(NeedKind.Safety).Pressure;
            AlternativeOptionState alternative = run.AlternativeOptions.Single();
            int timelineCount = run.Report.Timeline.Count;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    InstitutionalRelianceService.TryCreate(
                        run,
                        CreateRequest(
                            "reliance.test.atomic-late-failure",
                            "observation.test.atomic-late-failure",
                            "action.test.atomic-late-failure",
                            alternative.OptionId)));

            StringAssert.Contains("timeline id", exception.Message);
            Assert.AreEqual(actorCredits, FindAccount(run, ActorId).AvailableCredits);
            Assert.AreEqual(beneficiaryCredits,
                FindAccount(run, BeneficiaryId).AvailableCredits);
            Assert.AreEqual(relatedCredits, FindAccount(run, RelatedId).AvailableCredits);
            Assert.AreEqual(actorSubsistence, run.FinalSocietyState.GetAgent(ActorId)
                .GetNeed(NeedKind.Subsistence).Pressure);
            Assert.AreEqual(beneficiaryHealth,
                run.FinalSocietyState.GetAgent(BeneficiaryId)
                    .GetNeed(NeedKind.Health).Pressure);
            Assert.AreEqual(relatedSafety, run.FinalSocietyState.GetAgent(RelatedId)
                .GetNeed(NeedKind.Safety).Pressure);
            Assert.IsTrue(alternative.Available);
            Assert.IsNull(alternative.ChangedByActionEventId);
            Assert.IsEmpty(run.RelianceLedger);
            Assert.IsEmpty(run.Report.RelianceObservations);
            Assert.IsEmpty(run.Report.MaterialConsequences);
            Assert.AreEqual(timelineCount, run.Report.Timeline.Count);
        }

        [Test]
        public void Recovery_UsesRelianceKeyForUniqueCaseIdsAndRejectsDuplicates()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.first", 3, true);
            AddAutonomousAction(run, "action.test.second", 4, true);
            AddAlternative(run, "alternative.test.first");
            AddAlternative(run, "alternative.test.second");
            Assert.IsTrue(InstitutionalRelianceService.TryCreate(
                run,
                CreateRequest(
                    "reliance.test.first",
                    "observation.test.first",
                    "action.test.first",
                    "alternative.test.first")).Created);
            Assert.IsTrue(InstitutionalRelianceService.TryCreate(
                run,
                CreateRequest(
                    "reliance.test.second",
                    "observation.test.second",
                    "action.test.second",
                    "alternative.test.second")).Created);
            Ruling reversal = new Ruling
            {
                RulingId = "ruling.test.reversal",
                CaseId = "case.test.primary",
                Cycle = 5,
                PolicyConfigurationId = "configuration.test",
                PolicyVersion = "configuration.test.v1",
                Disposition = RulingDisposition.ReversedAndDenied,
                FindingId = "finding.test.reversal",
            };
            run.Report.Rulings.Add(reversal);

            RelianceRecoveryRequest firstRequest =
                CreateRecoveryRequest("reliance.test.first");
            RelianceRecoveryResult first =
                InstitutionalRelianceService.TryCreateRecoveryAfterReversal(
                    run,
                    reversal,
                    firstRequest);
            RelianceRecoveryResult second =
                InstitutionalRelianceService.TryCreateRecoveryAfterReversal(
                    run,
                    reversal,
                    CreateRecoveryRequest("reliance.test.second"));
            RelianceRecoveryResult duplicate =
                InstitutionalRelianceService.TryCreateRecoveryAfterReversal(
                    run,
                    reversal,
                    CreateRecoveryRequest("reliance.test.first"));

            Assert.IsTrue(first.Created);
            Assert.IsTrue(second.Created);
            Assert.AreNotEqual(first.RecoveryCase.CaseId, second.RecoveryCase.CaseId);
            StringAssert.EndsWith("reliance.test.first", first.RecoveryCase.CaseId);
            StringAssert.EndsWith("reliance.test.second", second.RecoveryCase.CaseId);
            Assert.AreEqual("action.test.first",
                first.RecoveryCase.CausalAgentActionId);
            Assert.AreEqual(reversal.RulingId,
                first.RecoveryCase.ParentCauseId);
            Assert.AreEqual(
                "glass-canal",
                first.RecoveryCase.Facts.Facts.Single(value =>
                    value.Key == "watershed").Value);
            firstRequest.Facts.Facts.Single(value =>
                value.Key == "watershed").Value = "mutated-after-recovery";
            Assert.AreEqual(
                "glass-canal",
                first.RecoveryCase.Facts.Facts.Single(value =>
                    value.Key == "watershed").Value);
            Assert.IsTrue(run.RelianceLedger.Single(value =>
                value.RelianceEventId == "reliance.test.first").SurvivedReversal);
            Assert.That(run.Report.ObservedAgentActions.Single(value =>
                value.ActionEventId == "action.test.first").ResultDescendantCaseIds,
                Does.Contain(first.RecoveryCase.CaseId));

            Assert.IsFalse(duplicate.Created);
            Assert.AreEqual(
                RelianceRecoveryFailureReason.DuplicateRecoveryCase,
                duplicate.FailureReason);
            Assert.AreEqual(2, run.Report.DescendantCases.Count);
        }

        private static InstitutionalConsequenceRun CreateRun()
        {
            var society = new SocietyState();
            society.Agents.Add(CreateAgent(ActorId, 0));
            society.Agents.Add(CreateAgent(BeneficiaryId, 1));
            society.Agents.Add(CreateAgent(RelatedId, 2));
            var run = new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport(),
                FinalSocietyState = society,
            };
            run.EconomicAccounts.Add(CreateAccount(ActorId));
            run.EconomicAccounts.Add(CreateAccount(BeneficiaryId));
            run.EconomicAccounts.Add(CreateAccount(RelatedId));

            var ruling = new Ruling
            {
                RulingId = RulingId,
                CaseId = "case.test.primary",
                Cycle = 2,
                PolicyConfigurationId = "configuration.test",
                PolicyVersion = "configuration.test.v1",
                Disposition = RulingDisposition.Recognised,
                FindingId = "finding.test.access",
            };
            ruling.OfficialStatusMutationIds.Add(MutationId);
            run.Report.Rulings.Add(ruling);
            run.Report.OfficialStatusMutations.Add(new OfficialStatusMutation
            {
                MutationId = MutationId,
                Cycle = 2,
                CauseId = RulingId,
                AffectedAgentId = ActorId,
                StatusId = StatusId,
                BeforeRecognised = false,
                AfterRecognised = true,
            });
            society.GetAgent(ActorId).Standing.SetRecognised(StatusId, true);
            return run;
        }

        private static AgentState CreateAgent(string agentId, int ordinal)
        {
            var agent = new AgentState
            {
                StableId = agentId,
                SimulationOrdinal = ordinal,
                PresentationId = agentId,
                DisplayName = agentId,
                SpeciesId = "species.test",
                HouseholdId = "household.test",
                EmployerId = "institution.test",
            };
            foreach (NeedKind kind in System.Enum.GetValues(typeof(NeedKind)))
            {
                agent.Needs.Add(new NeedState
                {
                    Kind = kind,
                    Pressure = kind switch
                    {
                        NeedKind.Health => 30,
                        NeedKind.Subsistence => 40,
                        NeedKind.Safety => 30,
                        _ => 20,
                    },
                });
            }
            return agent;
        }

        private static EconomicAccountState CreateAccount(string agentId)
        {
            return new EconomicAccountState
            {
                AgentId = agentId,
                AvailableCredits = 100,
            };
        }

        private static void AddAutonomousAction(
            InstitutionalConsequenceRun run,
            string actionEventId,
            long cycle,
            bool readsRequiredStatus)
        {
            run.Report.ObservedAgentActions.Add(new ObservedAgentAction
            {
                Cycle = cycle,
                ActionEventId = actionEventId,
                ActorId = ActorId,
                Activity = ObservedActivityKind.AidRequested,
                TargetId = BeneficiaryId,
            });
            var trace = new AgentActionTrace
            {
                Cycle = cycle,
                DecisionId = $"decision:{actionEventId}",
                CandidateId = $"candidate:{actionEventId}",
                ActorId = ActorId,
                Action = SocietyActionKind.SeekAid,
                OpportunityId = RelianceOpportunityId,
                PerceptionSnapshot = AgentPerception.Capture(
                    run.FinalSocietyState.GetAgent(ActorId)),
                InputSnapshot = new SimulationInput
                {
                    AidOpportunities = new List<AidOpportunity>
                    {
                        new()
                        {
                            OpportunityId = RelianceOpportunityId,
                            RequiredOfficialStatusId = readsRequiredStatus
                                ? StatusId
                                : "status.test.other",
                            RequiredOfficialStatusRecognised = true,
                            EligibleAgentIds = new List<string> { ActorId },
                        },
                    },
                },
            };
            trace.ResultEventIds.Add(actionEventId);
            trace.Reasons.Add(new DecisionReason
            {
                ReasonId = "standing.required-status",
                SourceId = readsRequiredStatus ? StatusId : "status.test.other",
            });
            run.AssessorActionTraces.Add(trace);
        }

        private static void AddAlternative(
            InstitutionalConsequenceRun run,
            string optionId)
        {
            run.AlternativeOptions.Add(new AlternativeOptionState
            {
                OptionId = optionId,
                AgentId = ActorId,
                Available = true,
            });
        }

        private static RelianceCreationRequest CreateRequest(
            string relianceId,
            string observationId,
            string actionId,
            string alternativeId)
        {
            return new RelianceCreationRequest
            {
                RelianceEventId = relianceId,
                ObservationId = observationId,
                SourceActionEventId = actionId,
                ActorAgentId = ActorId,
                ExpectedActionKind = SocietyActionKind.SeekAid,
                ExpectedOpportunityId = RelianceOpportunityId,
                BeneficiaryAgentId = BeneficiaryId,
                RelatedAgentId = RelatedId,
                EnablingRulingId = RulingId,
                EnablingMutationId = MutationId,
                RequiredStatusId = StatusId,
                ExpectedRecognisedState = true,
                ChoiceId = $"choice:{relianceId}",
                RecordedChoiceId = $"recorded-choice:{relianceId}",
                AbandonedAlternativeId = alternativeId,
                ResourceId = "resource.reliance-fixture",
                Effects = new List<RelianceEffectDelta>
                {
                    new()
                    {
                        EffectId = "effect.actor",
                        Recipient = RelianceEffectRecipient.Actor,
                        ResourceDelta = -20,
                        MaterialKind = MaterialConsequenceKind.RelianceSpent,
                        ResourceId = "resource.reliance-fixture",
                        Need = NeedKind.Subsistence,
                        NeedPressureDelta = 10,
                    },
                    new()
                    {
                        EffectId = "effect.beneficiary",
                        Recipient = RelianceEffectRecipient.Beneficiary,
                        ResourceDelta = 5,
                        MaterialKind = MaterialConsequenceKind.ReliefPaid,
                        ResourceId = "resource.reliance-fixture",
                        Need = NeedKind.Health,
                        NeedPressureDelta = -7,
                    },
                    new()
                    {
                        EffectId = "effect.related",
                        Recipient = RelianceEffectRecipient.RelatedAgent,
                        ResourceDelta = -3,
                        MaterialKind = MaterialConsequenceKind.RelianceSpent,
                        ResourceId = "resource.reliance-fixture",
                        Need = NeedKind.Safety,
                        NeedPressureDelta = 4,
                    },
                },
            };
        }

        private static RelianceRecoveryRequest CreateRecoveryRequest(
            string relianceId)
        {
            return new RelianceRecoveryRequest
            {
                RelianceEventId = relianceId,
                CaseIdPrefix = "case.test.recovery",
                ParentCaseId = "case.test.primary",
                RespondentId = "institution.test",
                OfficialIssueId = "issue.test.reliance",
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("watershed", "glass-canal"),
                }),
            };
        }

        private static EconomicAccountState FindAccount(
            InstitutionalConsequenceRun run,
            string agentId)
        {
            return run.EconomicAccounts.Single(value => value.AgentId == agentId);
        }
    }
}
