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
            Assert.IsFalse(result.PublicProjectionDeferred);
            Assert.IsEmpty(run.PendingReliancePublicProjections);
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
        public void Create_DelayedPublicProjection_AppliesAuthorityNowAndPublishesFrozenRowsWhenDue()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.delayed", 3, true);
            AddAlternative(run, "alternative.test.delayed");
            RelianceCreationRequest request = CreateRequest(
                "reliance.test.delayed",
                "observation.test.delayed",
                "action.test.delayed",
                "alternative.test.delayed");
            request.PublicObservationCycle = 4;

            RelianceCreationResult result = InstitutionalRelianceService.TryCreate(
                run,
                request);

            Assert.IsTrue(result.Created);
            Assert.IsTrue(result.PublicProjectionDeferred);
            Assert.AreEqual(3, result.Reliance.Cycle);
            Assert.AreEqual(4, result.Observation.Cycle);
            Assert.AreEqual(80, FindAccount(run, ActorId).AvailableCredits);
            Assert.IsFalse(run.AlternativeOptions.Single().Available);
            Assert.That(run.RelianceLedger, Has.Count.EqualTo(1));
            Assert.That(run.PendingReliancePublicProjections, Has.Count.EqualTo(1));
            Assert.IsEmpty(run.Report.RelianceObservations);
            Assert.IsEmpty(run.Report.MaterialConsequences);
            Assert.IsFalse(run.Report.Timeline.Any(entry =>
                entry.Kind == InstitutionalTimelineKind.RelianceCreated));

            // The result is diagnostic output; mutating it cannot rewrite the frozen
            // projection retained by the assessor-owned run.
            result.Observation.Cycle = 99;
            result.MaterialConsequences[0].Cycle = 99;
            InstitutionalPublicObservationProjector.ProjectDueReliance(run, 3);
            Assert.IsEmpty(run.Report.RelianceObservations);

            InstitutionalPublicObservationProjector.ProjectDueReliance(run, 4);

            Assert.IsEmpty(run.PendingReliancePublicProjections);
            Assert.That(run.Report.RelianceObservations, Has.Count.EqualTo(1));
            Assert.AreEqual(4, run.Report.RelianceObservations.Single().Cycle);
            Assert.That(run.Report.MaterialConsequences, Has.Count.EqualTo(3));
            Assert.That(run.Report.MaterialConsequences.All(value => value.Cycle == 4));
            Assert.That(run.Report.Timeline.Single(entry =>
                entry.Kind == InstitutionalTimelineKind.RelianceCreated).Cycle,
                Is.EqualTo(4));
        }

        [Test]
        public void Create_DeferredSemanticIdsRemainDistinctForColonBearingKeys()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.collision.one", 3, true);
            AddAutonomousAction(run, "action.collision.two", 3, true);
            AddAlternative(run, "alternative.collision.one");
            AddAlternative(run, "alternative.collision.two");

            RelianceCreationRequest first = CreateRequest(
                "a",
                "observation.collision.one",
                "action.collision.one",
                "alternative.collision.one");
            first.PublicObservationCycle = 4;
            first.Effects.RemoveRange(1, first.Effects.Count - 1);
            first.Effects[0].EffectId = "b:c";

            RelianceCreationRequest second = CreateRequest(
                "a:b",
                "observation.collision.two",
                "action.collision.two",
                "alternative.collision.two");
            second.PublicObservationCycle = 4;
            second.Effects.RemoveRange(1, second.Effects.Count - 1);
            second.Effects[0].EffectId = "c";

            RelianceCreationResult firstResult =
                InstitutionalRelianceService.TryCreate(run, first);
            RelianceCreationResult secondResult =
                InstitutionalRelianceService.TryCreate(run, second);

            Assert.IsTrue(firstResult.Created);
            Assert.IsTrue(secondResult.Created);
            Assert.AreNotEqual(
                firstResult.MaterialConsequences.Single().ConsequenceId,
                secondResult.MaterialConsequences.Single().ConsequenceId);
            Assert.That(run.PendingReliancePublicProjections, Has.Count.EqualTo(2));

            InstitutionalPublicObservationProjector.ProjectDueReliance(run, 4);

            Assert.That(run.Report.MaterialConsequences.Select(value =>
                value.ConsequenceId).Distinct().Count(), Is.EqualTo(2));
            Assert.IsEmpty(run.PendingReliancePublicProjections);
        }

        [Test]
        public void Create_RejectsASecondRelianceOnTheSameObservedAction()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.single-reliance", 3, true);
            AddAlternative(run, "alternative.test.first-reliance");
            AddAlternative(run, "alternative.test.second-reliance");

            RelianceCreationRequest first = CreateRequest(
                "reliance.test.first",
                "observation.test.first",
                "action.test.single-reliance",
                "alternative.test.first-reliance");
            first.PublicObservationCycle = 4;
            Assert.IsTrue(InstitutionalRelianceService.TryCreate(run, first).Created);

            RelianceCreationRequest second = CreateRequest(
                "reliance.test.second",
                "observation.test.second",
                "action.test.single-reliance",
                "alternative.test.second-reliance");
            second.PublicObservationCycle = 4;
            RelianceCreationResult result =
                InstitutionalRelianceService.TryCreate(run, second);

            Assert.IsFalse(result.Created);
            Assert.AreEqual(
                RelianceFailureReason.SourceActionAlreadyUsed,
                result.FailureReason);
            Assert.That(run.RelianceLedger, Has.Count.EqualTo(1));
            Assert.That(run.PendingReliancePublicProjections, Has.Count.EqualTo(1));
        }

        [TestCase("duplicate-trace")]
        [TestCase("wrong-cycle")]
        [TestCase("aliased-recipient")]
        [TestCase("wrong-cause-decision")]
        [TestCase("wrong-event-kind")]
        [TestCase("wrong-event-opportunity")]
        public void Create_RejectsAnAmbiguousActionOrRecipientEnvelope(
            string corruption)
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.ambiguous", 3, true);
            AddAlternative(run, "alternative.test.ambiguous");
            RelianceCreationRequest request = CreateRequest(
                "reliance.test.ambiguous",
                "observation.test.ambiguous",
                "action.test.ambiguous",
                "alternative.test.ambiguous");
            switch (corruption)
            {
                case "duplicate-trace":
                    run.AssessorActionTraces.Add(
                        run.AssessorActionTraces.Single());
                    break;
                case "wrong-cycle":
                    run.AssessorActionTraces.Single().Cycle++;
                    break;
                case "aliased-recipient":
                    request.BeneficiaryAgentId = ActorId;
                    break;
                case "wrong-cause-decision":
                    run.FinalSocietyState.EventLedger.Single().CauseDecisionId =
                        "decision.test.foreign";
                    break;
                case "wrong-event-kind":
                    run.FinalSocietyState.EventLedger.Single().Kind =
                        SocietyEventKind.WorkPerformed;
                    break;
                case "wrong-event-opportunity":
                    run.FinalSocietyState.EventLedger.Single().OpportunityId =
                        "opportunity.test.foreign";
                    break;
                default:
                    Assert.Fail($"Unknown corruption '{corruption}'.");
                    break;
            }

            RelianceCreationResult result =
                InstitutionalRelianceService.TryCreate(run, request);

            Assert.IsFalse(result.Created);
            Assert.IsEmpty(run.RelianceLedger);
            Assert.IsTrue(run.AlternativeOptions.Single().Available);
        }

        [Test]
        public void Create_RejectsAnEnablingMutationSupersededBeforeTheAction()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.superseded-status", 4, true);
            AddAlternative(run, "alternative.test.superseded-status");
            var supersedingRuling = new Ruling
            {
                RulingId = "ruling.test.superseding-status",
                CaseId = "case.test.primary",
                Cycle = 3,
                PolicyConfigurationId = "configuration.test",
                PolicyVersion = "configuration.test.v1",
                Disposition = RulingDisposition.Denied,
                FindingId = "finding.test.superseding-status",
            };
            var supersedingMutation = new OfficialStatusMutation
            {
                MutationId = "mutation.test.superseding-status",
                Cycle = 3,
                CauseId = supersedingRuling.RulingId,
                AffectedAgentId = ActorId,
                StatusId = StatusId,
                BeforeRecognised = true,
                AfterRecognised = false,
            };
            supersedingRuling.OfficialStatusMutationIds.Add(
                supersedingMutation.MutationId);
            run.Report.Rulings.Add(supersedingRuling);
            run.Report.OfficialStatusMutations.Add(supersedingMutation);

            RelianceCreationResult result =
                InstitutionalRelianceService.TryCreate(
                    run,
                    CreateRequest(
                        "reliance.test.superseded-status",
                        "observation.test.superseded-status",
                        "action.test.superseded-status",
                        "alternative.test.superseded-status"));

            Assert.IsFalse(result.Created);
            Assert.AreEqual(
                RelianceFailureReason.EnablingMutationMismatch,
                result.FailureReason);
            Assert.IsEmpty(run.RelianceLedger);
        }

        [Test]
        public void Create_RejectsObservationIdReusedByAnotherPublicNode()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.global-observation", 3, true);
            AddAlternative(run, "alternative.test.global-observation");
            RelianceCreationRequest request = CreateRequest(
                "reliance.test.global-observation",
                RulingId,
                "action.test.global-observation",
                "alternative.test.global-observation");

            RelianceCreationResult result =
                InstitutionalRelianceService.TryCreate(run, request);

            Assert.IsFalse(result.Created);
            Assert.AreEqual(
                RelianceFailureReason.DuplicateObservation,
                result.FailureReason);
            Assert.IsEmpty(run.RelianceLedger);
        }

        [Test]
        public void Create_DeferredMaterialIdCollisionIsAtomicAcrossPublicNodeKinds()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.global-material", 3, true);
            AddAlternative(run, "alternative.test.global-material");
            RelianceCreationRequest request = CreateRequest(
                "reliance.test.global-material",
                "observation.test.global-material",
                "action.test.global-material",
                "alternative.test.global-material");
            request.PublicObservationCycle = 4;
            string effectId = request.Effects[0].EffectId;
            string collidingId =
                $"material:4:reliance:{request.RelianceEventId.Length}:" +
                $"{request.RelianceEventId}:{effectId.Length}:{effectId}";
            run.Report.EvidenceArtifacts.Add(new EvidenceArtifact
            {
                ArtifactId = collidingId,
            });
            int creditsBefore = FindAccount(run, ActorId).AvailableCredits;

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalRelianceService.TryCreate(run, request));

            Assert.AreEqual(creditsBefore,
                FindAccount(run, ActorId).AvailableCredits);
            Assert.IsEmpty(run.RelianceLedger);
            Assert.IsEmpty(run.PendingReliancePublicProjections);
            Assert.IsTrue(run.AlternativeOptions.Single().Available);
        }

        [TestCase("wrong-reliance")]
        [TestCase("orphan-material")]
        [TestCase("wrong-kind")]
        [TestCase("wrong-resource")]
        [TestCase("blank-material-id")]
        [TestCase("wrong-observation-id")]
        [TestCase("wrong-observation-cycle")]
        [TestCase("wrong-recorded-choice")]
        [TestCase("cross-node-id")]
        public void ProjectDue_CorruptPendingProjection_FailsBeforePublicMutation(
            string corruption)
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.pending-corruption", 3, true);
            AddAlternative(run, "alternative.test.pending-corruption");
            RelianceCreationRequest request = CreateRequest(
                "reliance.test.pending-corruption",
                "observation.test.pending-corruption",
                "action.test.pending-corruption",
                "alternative.test.pending-corruption");
            request.PublicObservationCycle = 4;
            Assert.IsTrue(InstitutionalRelianceService.TryCreate(run, request).Created);
            PendingReliancePublicProjection pending =
                run.PendingReliancePublicProjections.Single();

            switch (corruption)
            {
                case "wrong-reliance":
                    pending.RelianceEventId = "reliance.test.missing";
                    break;
                case "orphan-material":
                    pending.MaterialConsequences.Add(new MaterialConsequence
                    {
                        ConsequenceId = "material.test.pending-orphan",
                        Cycle = 4,
                        CauseId = "action.test.pending-corruption",
                        AgentId = RelatedId,
                        Kind = MaterialConsequenceKind.ReliefPaid,
                        KindId = "material-kind.test.pending-orphan",
                    });
                    break;
                case "wrong-kind":
                    pending.MaterialConsequences[0].Kind =
                        MaterialConsequenceKind.ReliefPaid;
                    break;
                case "wrong-resource":
                    pending.MaterialConsequences[0].ResourceId =
                        "resource.test.reattributed";
                    break;
                case "blank-material-id":
                    pending.MaterialConsequences[0].ConsequenceId = null;
                    break;
                case "wrong-observation-id":
                    pending.Observation.ObservationId = "observation.test.forged";
                    break;
                case "wrong-observation-cycle":
                    pending.Observation.Cycle++;
                    break;
                case "wrong-recorded-choice":
                    pending.Observation.RecordedChoiceId = "recorded-choice.forged";
                    break;
                case "cross-node-id":
                    run.Report.EvidenceArtifacts.Add(new EvidenceArtifact
                    {
                        ArtifactId =
                            pending.MaterialConsequences[0].ConsequenceId,
                    });
                    break;
                default:
                    Assert.Fail($"Unknown corruption '{corruption}'.");
                    break;
            }

            int creditsBeforeProjection = FindAccount(run, ActorId).AvailableCredits;
            int pendingCount = run.PendingReliancePublicProjections.Count;
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalPublicObservationProjector.ProjectDueReliance(run, 4));

            Assert.AreEqual(creditsBeforeProjection,
                FindAccount(run, ActorId).AvailableCredits);
            Assert.AreEqual(pendingCount, run.PendingReliancePublicProjections.Count);
            Assert.IsEmpty(run.Report.RelianceObservations);
            Assert.IsEmpty(run.Report.MaterialConsequences);
            Assert.IsFalse(run.Report.Timeline.Any(value =>
                value.Kind == InstitutionalTimelineKind.RelianceCreated));
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
            AddReversalAppeal(run, reversal, RulingId);

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
            RelianceRecoveryRequest duplicateRequest =
                CreateRecoveryRequest("reliance.test.first");
            duplicateRequest.CaseIdPrefix = "case.test.alternate-recovery";
            RelianceRecoveryResult duplicate =
                InstitutionalRelianceService.TryCreateRecoveryAfterReversal(
                    run,
                    reversal,
                    duplicateRequest);

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

        [Test]
        public void Recovery_CannotBecomePublicBeforeDelayedRelianceObservation()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.delayed-recovery", 3, true);
            AddAlternative(run, "alternative.test.delayed-recovery");
            RelianceCreationRequest creation = CreateRequest(
                "reliance.test.delayed-recovery",
                "observation.test.delayed-recovery",
                "action.test.delayed-recovery",
                "alternative.test.delayed-recovery");
            creation.PublicObservationCycle = 5;
            Assert.IsTrue(
                InstitutionalRelianceService.TryCreate(run, creation).Created);

            var reversal = new Ruling
            {
                RulingId = "ruling.test.early-reversal",
                CaseId = "case.test.primary",
                Cycle = 6,
                PolicyConfigurationId = "configuration.test",
                PolicyVersion = "configuration.test.v1",
                Disposition = RulingDisposition.ReversedAndDenied,
                FindingId = "finding.test.early-reversal",
            };
            run.Report.Rulings.Add(reversal);
            AddReversalAppeal(run, reversal, RulingId);

            RelianceRecoveryResult result =
                InstitutionalRelianceService.TryCreateRecoveryAfterReversal(
                    run,
                    reversal,
                    CreateRecoveryRequest("reliance.test.delayed-recovery"));

            Assert.IsFalse(result.Created);
            Assert.AreEqual(
                RelianceRecoveryFailureReason.InvalidChronology,
                result.FailureReason);
            Assert.IsEmpty(run.Report.DescendantCases);
            Assert.IsFalse(run.RelianceLedger.Single().SurvivedReversal);
        }

        [Test]
        public void Recovery_RejectsAnAppealThatReversedAnotherRuling()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.wrong-reversal", 3, true);
            AddAlternative(run, "alternative.test.wrong-reversal");
            Assert.IsTrue(InstitutionalRelianceService.TryCreate(
                run,
                CreateRequest(
                    "reliance.test.wrong-reversal",
                    "observation.test.wrong-reversal",
                    "action.test.wrong-reversal",
                    "alternative.test.wrong-reversal")).Created);
            var reversal = new Ruling
            {
                RulingId = "ruling.test.wrong-reversal",
                CaseId = "case.test.primary",
                Cycle = 5,
                PolicyConfigurationId = "configuration.test",
                PolicyVersion = "configuration.test.v1",
                Disposition = RulingDisposition.ReversedAndDenied,
                FindingId = "finding.test.wrong-reversal",
            };
            run.Report.Rulings.Add(reversal);
            AddReversalAppeal(run, reversal, "ruling.test.unrelated");

            RelianceRecoveryResult result =
                InstitutionalRelianceService.TryCreateRecoveryAfterReversal(
                    run,
                    reversal,
                    CreateRecoveryRequest("reliance.test.wrong-reversal"));

            Assert.IsFalse(result.Created);
            Assert.AreEqual(
                RelianceRecoveryFailureReason.ReversalRulingNotFound,
                result.FailureReason);
            Assert.IsEmpty(run.Report.DescendantCases);
        }

        [TestCase("observation")]
        [TestCase("missing-material")]
        [TestCase("reattributed-material")]
        [TestCase("missing-timeline")]
        public void Recovery_RequiresTheCompletePublishedRelianceEnvelope(
            string corruption)
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddAutonomousAction(run, "action.test.public-envelope", 3, true);
            AddAlternative(run, "alternative.test.public-envelope");
            Assert.IsTrue(InstitutionalRelianceService.TryCreate(
                run,
                CreateRequest(
                    "reliance.test.public-envelope",
                    "observation.test.public-envelope",
                    "action.test.public-envelope",
                    "alternative.test.public-envelope")).Created);
            var reversal = new Ruling
            {
                RulingId = "ruling.test.public-envelope-reversal",
                CaseId = "case.test.primary",
                Cycle = 5,
                PolicyConfigurationId = "configuration.test",
                PolicyVersion = "configuration.test.v1",
                Disposition = RulingDisposition.ReversedAndDenied,
                FindingId = "finding.test.public-envelope-reversal",
            };
            run.Report.Rulings.Add(reversal);
            AddReversalAppeal(run, reversal, RulingId);
            switch (corruption)
            {
                case "observation":
                    run.Report.RelianceObservations.Single().RecordedChoiceId =
                        "recorded-choice.test.forged";
                    break;
                case "missing-material":
                    run.Report.MaterialConsequences.RemoveAt(0);
                    break;
                case "reattributed-material":
                    run.Report.MaterialConsequences[0].AgentId = BeneficiaryId;
                    break;
                case "missing-timeline":
                    run.Report.Timeline.RemoveAll(entry =>
                        entry.Kind == InstitutionalTimelineKind.RelianceCreated);
                    break;
                default:
                    Assert.Fail($"Unknown corruption '{corruption}'.");
                    break;
            }

            RelianceRecoveryResult result =
                InstitutionalRelianceService.TryCreateRecoveryAfterReversal(
                    run,
                    reversal,
                    CreateRecoveryRequest("reliance.test.public-envelope"));

            Assert.IsFalse(result.Created);
            Assert.AreEqual(
                RelianceRecoveryFailureReason.InvalidChronology,
                result.FailureReason);
            Assert.IsEmpty(run.Report.DescendantCases);
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
            run.FinalSocietyState.EventLedger.Add(new SocietyEvent
            {
                EventId = actionEventId,
                CauseDecisionId = trace.DecisionId,
                IncidentId = "incident.test.reliance",
                Tick = cycle,
                Kind = SocietyEventKind.AidRequested,
                ActorId = ActorId,
                TargetId = BeneficiaryId,
                OpportunityId = RelianceOpportunityId,
                Visibility = EvidenceVisibility.OfficialRecord,
            });
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

        private static void AddReversalAppeal(
            InstitutionalConsequenceRun run,
            Ruling reversal,
            string challengedRulingId)
        {
            run.Report.Appeals.Add(new Appeal
            {
                AppealId = $"appeal:{reversal.RulingId}",
                CaseId = reversal.CaseId,
                FiledCycle = reversal.Cycle - 1,
                HearingCycle = reversal.Cycle,
                AppellantAgentId = ActorId,
                FilingActionEventId = "action.test.appeal-fixture",
                ChallengedRulingId = challengedRulingId,
                Disposition = AppealDisposition.Reversed,
                ResultingRulingId = reversal.RulingId,
            });
        }

        private static EconomicAccountState FindAccount(
            InstitutionalConsequenceRun run,
            string agentId)
        {
            return run.EconomicAccounts.Single(value => value.AgentId == agentId);
        }
    }
}
