using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalExclusiveEntitlementTests
    {
        private const string EntitlementId = "entitlement.test.filter-access";
        private const string ResourceId = "resource.test.cartridge-cf9";
        private const string HolderStatusId = "status.test.holds-cartridge-cf9";

        [Test]
        public void InitialStateRegistration_ProjectsAuthoredHolderWithoutInventingRuling()
        {
            InstitutionalConsequenceRun run = CreateRun();
            run.FinalSocietyState.GetAgent("agent.old-holder").Standing
                .SetRecognised(HolderStatusId, true);
            var registry = new ExclusiveEntitlementRegistry();

            ExclusiveEntitlementState state =
                ExclusiveEntitlementService.RegisterInitialState(
                    registry,
                    run,
                    EntitlementId,
                    ResourceId,
                    HolderStatusId,
                    1,
                    "agent.old-holder",
                    "initial-state.fixture");

            Assert.AreEqual("agent.old-holder", state.CurrentHolderAgentId);
            Assert.IsEmpty(run.Report.Rulings);
            Assert.IsEmpty(run.Report.OfficialStatusMutations);
            ExclusiveEntitlementObservation observation =
                run.Report.ExclusiveEntitlements.Single();
            Assert.AreEqual(ResourceId, observation.ResourceId);
            Assert.AreEqual("agent.old-holder", observation.CurrentHolderAgentId);
            Assert.AreEqual("initial-state.fixture", observation.LastMutationCauseId);
            Assert.DoesNotThrow(() =>
                ExclusiveEntitlementService.AssertHolderInvariant(run, state));
        }

        [Test]
        public void InitialStateRegistration_InvalidAuthoredHolder_IsAtomic()
        {
            InstitutionalConsequenceRun run = CreateRun();
            var registry = new ExclusiveEntitlementRegistry();

            Assert.Throws<InvalidOperationException>(() =>
                ExclusiveEntitlementService.RegisterInitialState(
                    registry,
                    run,
                    EntitlementId,
                    ResourceId,
                    HolderStatusId,
                    1,
                    "agent.old-holder",
                    "initial-state.invalid"));

            Assert.AreEqual(0, registry.Count);
            Assert.IsEmpty(run.Report.ExclusiveEntitlements);
            Assert.IsFalse(run.FinalSocietyState.GetAgent("agent.old-holder")
                .Standing.IsRecognised(HolderStatusId));
        }

        [Test]
        public void InitialStateRegistration_MissingObservationCollection_IsAtomic()
        {
            InstitutionalConsequenceRun run = CreateRun();
            run.FinalSocietyState.GetAgent("agent.old-holder").Standing
                .SetRecognised(HolderStatusId, true);
            run.Report.ExclusiveEntitlements = null;
            var registry = new ExclusiveEntitlementRegistry();

            Assert.Throws<InvalidOperationException>(() =>
                ExclusiveEntitlementService.RegisterInitialState(
                    registry,
                    run,
                    EntitlementId,
                    ResourceId,
                    HolderStatusId,
                    1,
                    "agent.old-holder",
                    "initial-state.missing-observation-collection"));

            Assert.AreEqual(0, registry.Count);
            Assert.IsTrue(run.FinalSocietyState.GetAgent("agent.old-holder")
                .Standing.IsRecognised(HolderStatusId));
        }

        [Test]
        public void Transfer_MovesOneConservedResourceAndEmitsPairedConsequences()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ExclusiveEntitlementRegistry registry = new();
            Ruling initialRuling = AddRuling(run, "ruling.test.initial", 1);
            ExclusiveEntitlementState state = ExclusiveEntitlementService.Register(
                registry,
                run,
                initialRuling,
                EntitlementId,
                ResourceId,
                HolderStatusId,
                7,
                "agent.old-holder");
            int mutationCount = run.Report.OfficialStatusMutations.Count;
            int timelineCount = run.Report.Timeline.Count;
            Ruling transferRuling = AddRuling(run, "ruling.test.transfer", 2);

            ExclusiveEntitlementTransferResult result =
                ExclusiveEntitlementService.ChangeHolder(
                    registry,
                    run,
                    transferRuling,
                    EntitlementId,
                    ResourceId,
                    "agent.old-holder",
                    "agent.new-holder",
                    MaterialConsequenceKind.ReliefPaid,
                    MaterialConsequenceKind.RelianceSpent);

            Assert.IsTrue(result.Changed);
            Assert.AreSame(state, registry.Find(EntitlementId, ResourceId));
            Assert.AreEqual("agent.old-holder", result.PreviousHolderAgentId);
            Assert.AreEqual("agent.new-holder", result.CurrentHolderAgentId);
            Assert.AreEqual("agent.new-holder", state.CurrentHolderAgentId);
            Assert.AreEqual(7, state.ConservedAmount);
            Assert.AreEqual(7, result.ConservedAmount);
            Assert.AreEqual(transferRuling.RulingId, state.LastMutationCauseId);

            Assert.IsTrue(result.PreviousHolderMutation.Changed);
            Assert.IsFalse(result.PreviousHolderMutation.CurrentRecognisedState);
            Assert.IsTrue(result.CurrentHolderMutation.Changed);
            Assert.IsTrue(result.CurrentHolderMutation.CurrentRecognisedState);
            Assert.AreEqual(mutationCount + 2,
                run.Report.OfficialStatusMutations.Count);
            Assert.AreEqual(timelineCount + 2, run.Report.Timeline.Count);

            Assert.AreEqual(7, result.GainConsequence.ResourceDelta);
            Assert.AreEqual(-7, result.LossConsequence.ResourceDelta);
            Assert.AreEqual(0,
                result.GainConsequence.ResourceDelta +
                result.LossConsequence.ResourceDelta);
            Assert.AreEqual(transferRuling.RulingId,
                result.GainConsequence.CauseId);
            Assert.AreEqual(transferRuling.RulingId,
                result.LossConsequence.CauseId);
            Assert.AreEqual("agent.new-holder",
                result.GainConsequence.AgentId);
            Assert.AreEqual("agent.old-holder",
                result.LossConsequence.AgentId);
            Assert.AreEqual(2, run.Report.MaterialConsequences.Count);

            Assert.IsFalse(run.FinalSocietyState
                .GetAgent("agent.old-holder").Standing
                .IsRecognised(HolderStatusId));
            Assert.IsTrue(run.FinalSocietyState
                .GetAgent("agent.new-holder").Standing
                .IsRecognised(HolderStatusId));
            Assert.AreEqual(1, run.FinalSocietyState.Agents.Count(agent =>
                agent.Standing.IsRecognised(HolderStatusId)));
            Assert.DoesNotThrow(() =>
                ExclusiveEntitlementService.AssertHolderInvariant(run, state));
        }

        [Test]
        public void SameHolderRequest_ReturnsExplicitNoOpWithoutReportChanges()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ExclusiveEntitlementRegistry registry = new();
            ExclusiveEntitlementState state = ExclusiveEntitlementService.Register(
                registry,
                run,
                AddRuling(run, "ruling.test.initial", 1),
                EntitlementId,
                ResourceId,
                HolderStatusId,
                1,
                "agent.old-holder");
            int mutations = run.Report.OfficialStatusMutations.Count;
            int materials = run.Report.MaterialConsequences.Count;
            int timeline = run.Report.Timeline.Count;
            Ruling ruling = AddRuling(run, "ruling.test.no-op", 2);

            ExclusiveEntitlementTransferResult result =
                ExclusiveEntitlementService.ChangeHolder(
                    registry,
                    run,
                    ruling,
                    EntitlementId,
                    ResourceId,
                    "agent.old-holder",
                    "agent.old-holder",
                    MaterialConsequenceKind.ReliefPaid,
                    MaterialConsequenceKind.RelianceSpent);

            Assert.IsFalse(result.Changed);
            Assert.AreEqual("agent.old-holder", result.PreviousHolderAgentId);
            Assert.AreEqual("agent.old-holder", result.CurrentHolderAgentId);
            Assert.AreEqual(1, result.ConservedAmount);
            Assert.IsNull(result.PreviousHolderMutation);
            Assert.IsNull(result.CurrentHolderMutation);
            Assert.IsNull(result.GainConsequence);
            Assert.IsNull(result.LossConsequence);
            Assert.AreEqual("agent.old-holder", state.CurrentHolderAgentId);
            Assert.AreEqual(mutations, run.Report.OfficialStatusMutations.Count);
            Assert.AreEqual(materials, run.Report.MaterialConsequences.Count);
            Assert.AreEqual(timeline, run.Report.Timeline.Count);
            Assert.IsEmpty(ruling.OfficialStatusMutationIds);
        }

        [Test]
        public void UnheldAssignment_PreservesExclusiveStateWithoutInventingALoser()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ExclusiveEntitlementRegistry registry = new();
            ExclusiveEntitlementState state = ExclusiveEntitlementService.Register(
                registry,
                run,
                AddRuling(run, "ruling.test.unheld", 1),
                EntitlementId,
                ResourceId,
                HolderStatusId,
                4,
                null);
            Assert.IsNull(state.CurrentHolderAgentId);
            Assert.AreEqual(0, run.FinalSocietyState.Agents.Count(agent =>
                agent.Standing.IsRecognised(HolderStatusId)));
            Ruling ruling = AddRuling(run, "ruling.test.assignment", 2);

            ExclusiveEntitlementTransferResult result =
                ExclusiveEntitlementService.ChangeHolder(
                    registry,
                    run,
                    ruling,
                    EntitlementId,
                    ResourceId,
                    null,
                    "agent.new-holder",
                    MaterialConsequenceKind.ReliefPaid,
                    MaterialConsequenceKind.RelianceSpent);

            Assert.IsTrue(result.Changed);
            Assert.IsNull(result.PreviousHolderAgentId);
            Assert.AreEqual("agent.new-holder", result.CurrentHolderAgentId);
            Assert.IsNull(result.PreviousHolderMutation);
            Assert.IsTrue(result.CurrentHolderMutation.Changed);
            Assert.IsNull(result.GainConsequence);
            Assert.IsNull(result.LossConsequence);
            Assert.IsEmpty(run.Report.MaterialConsequences);
            Assert.AreEqual(1, run.FinalSocietyState.Agents.Count(agent =>
                agent.Standing.IsRecognised(HolderStatusId)));
            Assert.DoesNotThrow(() =>
                ExclusiveEntitlementService.AssertHolderInvariant(run, state));
        }

        [Test]
        public void StaleExpectedHolder_IsRejectedBeforeAnyMutation()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ExclusiveEntitlementRegistry registry = new();
            ExclusiveEntitlementService.Register(
                registry,
                run,
                AddRuling(run, "ruling.test.initial", 1),
                EntitlementId,
                ResourceId,
                HolderStatusId,
                2,
                "agent.old-holder");
            int mutations = run.Report.OfficialStatusMutations.Count;
            int timeline = run.Report.Timeline.Count;
            Ruling ruling = AddRuling(run, "ruling.test.stale", 2);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                ExclusiveEntitlementService.ChangeHolder(
                    registry,
                    run,
                    ruling,
                    EntitlementId,
                    ResourceId,
                    "agent.someone-else",
                    "agent.new-holder",
                    MaterialConsequenceKind.ReliefPaid,
                    MaterialConsequenceKind.RelianceSpent));

            StringAssert.Contains("Stale entitlement transfer", exception.Message);
            Assert.AreEqual(mutations, run.Report.OfficialStatusMutations.Count);
            Assert.AreEqual(timeline, run.Report.Timeline.Count);
            Assert.IsEmpty(run.Report.MaterialConsequences);
            Assert.IsEmpty(ruling.OfficialStatusMutationIds);
        }

        [Test]
        public void MissingPublicObservation_IsRejectedBeforeAnyTransferMutation()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ExclusiveEntitlementRegistry registry = new();
            ExclusiveEntitlementState state = ExclusiveEntitlementService.Register(
                registry,
                run,
                AddRuling(run, "ruling.test.initial", 1),
                EntitlementId,
                ResourceId,
                HolderStatusId,
                2,
                "agent.old-holder");
            run.Report.ExclusiveEntitlements.Clear();
            int mutations = run.Report.OfficialStatusMutations.Count;
            int timeline = run.Report.Timeline.Count;
            Ruling ruling = AddRuling(run, "ruling.test.missing-observation", 2);

            Assert.Throws<InvalidOperationException>(() =>
                ExclusiveEntitlementService.ChangeHolder(
                    registry,
                    run,
                    ruling,
                    EntitlementId,
                    ResourceId,
                    "agent.old-holder",
                    "agent.new-holder",
                    MaterialConsequenceKind.ReliefPaid,
                    MaterialConsequenceKind.RelianceSpent));

            Assert.AreEqual("agent.old-holder", state.CurrentHolderAgentId);
            Assert.IsTrue(run.FinalSocietyState.GetAgent("agent.old-holder")
                .Standing.IsRecognised(HolderStatusId));
            Assert.IsFalse(run.FinalSocietyState.GetAgent("agent.new-holder")
                .Standing.IsRecognised(HolderStatusId));
            Assert.AreEqual(mutations, run.Report.OfficialStatusMutations.Count);
            Assert.AreEqual(timeline, run.Report.Timeline.Count);
            Assert.That(run.Report.MaterialConsequences, Is.Empty);
            Assert.That(ruling.OfficialStatusMutationIds, Is.Empty);
        }

        [Test]
        public void Transfer_SecondGeneratedMutationIdCollision_IsAtomic()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ExclusiveEntitlementRegistry registry = new();
            Ruling initialRuling = AddRuling(run, "ruling.test.initial", 1);
            ExclusiveEntitlementState state = ExclusiveEntitlementService.Register(
                registry,
                run,
                initialRuling,
                EntitlementId,
                ResourceId,
                HolderStatusId,
                3,
                "agent.old-holder");
            ExclusiveEntitlementObservation observation =
                run.Report.ExclusiveEntitlements.Single();
            Ruling transferRuling = AddRuling(
                run,
                "ruling.test.second-mutation-collision",
                2);

            int collidingSecondIndex =
                run.Report.OfficialStatusMutations.Count + 2;
            run.Report.OfficialStatusMutations.Add(new OfficialStatusMutation
            {
                MutationId =
                    $"mutation:{transferRuling.Cycle}:{collidingSecondIndex}:" +
                    $"agent.new-holder:{HolderStatusId}",
                Cycle = 0,
                CauseId = "fixture.second-mutation-collision",
            });

            int mutationCount = run.Report.OfficialStatusMutations.Count;
            int timelineCount = run.Report.Timeline.Count;
            string lastCause = state.LastMutationCauseId;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    ExclusiveEntitlementService.ChangeHolder(
                        registry,
                        run,
                        transferRuling,
                        EntitlementId,
                        ResourceId,
                        "agent.old-holder",
                        "agent.new-holder",
                        MaterialConsequenceKind.ReliefPaid,
                        MaterialConsequenceKind.RelianceSpent));

            StringAssert.Contains("Official mutation id", exception.Message);
            Assert.AreEqual("agent.old-holder", state.CurrentHolderAgentId);
            Assert.AreEqual(lastCause, state.LastMutationCauseId);
            Assert.AreEqual("agent.old-holder", observation.CurrentHolderAgentId);
            Assert.AreEqual(lastCause, observation.LastMutationCauseId);
            Assert.IsTrue(run.FinalSocietyState.GetAgent("agent.old-holder")
                .Standing.IsRecognised(HolderStatusId));
            Assert.IsFalse(run.FinalSocietyState.GetAgent("agent.new-holder")
                .Standing.IsRecognised(HolderStatusId));
            Assert.AreEqual(mutationCount,
                run.Report.OfficialStatusMutations.Count);
            Assert.AreEqual(timelineCount, run.Report.Timeline.Count);
            Assert.IsEmpty(run.Report.MaterialConsequences);
            Assert.IsEmpty(transferRuling.OfficialStatusMutationIds);
        }

        [Test]
        public void DuplicateKeyAndOutOfRangeAmount_AreRejected()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ExclusiveEntitlementRegistry registry = new();
            Ruling ruling = AddRuling(run, "ruling.test.registration", 1);
            ExclusiveEntitlementService.Register(
                registry,
                run,
                ruling,
                EntitlementId,
                ResourceId,
                HolderStatusId,
                1,
                null);

            Assert.Throws<InvalidOperationException>(() =>
                ExclusiveEntitlementService.Register(
                    registry,
                    run,
                    ruling,
                    EntitlementId,
                    ResourceId,
                    "status.test.other",
                    1,
                    null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ExclusiveEntitlementService.Register(
                    registry,
                    run,
                    ruling,
                    "entitlement.test.other",
                    "resource.test.other",
                    "status.test.other",
                    0,
                null));
        }

        [Test]
        public void ScenarioTransfer_UsesDeclaredExactRulingWhenCaseHasMultipleCitations()
        {
            InstitutionalConsequenceRun run = CreateRun();
            run.FinalSocietyState.GetAgent("agent.old-holder").Standing
                .SetRecognised(HolderStatusId, true);
            var definition = new InstitutionalScenarioDefinition();
            definition.ExclusiveEntitlements.Add(new ScenarioExclusiveEntitlementDefinition
            {
                EntitlementId = EntitlementId,
                ResourceId = ResourceId,
                OfficialStatusId = HolderStatusId,
                InitialHolderRoleId = "role.old-holder",
                Units = 1,
            });
            definition.Holdings.Add(new ScenarioHoldingDefinition
            {
                HoldingId = "holding.test",
                RuleId = "rule.test",
            });
            definition.EntitlementTransfers.Add(
                new ScenarioExclusiveEntitlementTransferDefinition
                {
                    TransferId = "transfer.test",
                    Cycle = 4,
                    EntitlementId = EntitlementId,
                    FromRoleId = "role.old-holder",
                    ToRoleId = "role.new-holder",
                    CauseCaseId = "case.test.entitlement",
                    CauseRulingId = "ruling.test.exact",
                    CauseHoldingId = "holding.test",
                    RequiredRulingDisposition = RulingDisposition.Recognised,
                });
            var context = new InstitutionalScenarioExecutionContext(
                definition,
                new InstitutionalPolicyConfiguration(),
                run,
                new Dictionary<string, string>
                {
                    ["role.old-holder"] = "agent.old-holder",
                    ["role.new-holder"] = "agent.new-holder",
                });
            InstitutionalScenarioEntitlementPhase.RegisterInitial(context);
            Ruling other = AddRuling(run, "ruling.test.other", 2);
            other.CitedHoldingIds.Add("holding.test");
            Ruling exact = AddRuling(run, "ruling.test.exact", 4);
            exact.CitedHoldingIds.Add("holding.test");

            InstitutionalScenarioEntitlementPhase.TransferDue(context, 4);

            ExclusiveEntitlementState state = context.EntitlementRegistry.Find(
                EntitlementId,
                ResourceId);
            Assert.AreEqual("agent.new-holder", state.CurrentHolderAgentId);
            Assert.AreEqual(exact.RulingId, state.LastMutationCauseId);
            Assert.That(run.Report.MaterialConsequences, Has.Count.EqualTo(2));
            Assert.That(run.Report.MaterialConsequences.All(value =>
                value.CauseId == exact.RulingId), Is.True);
        }

        [Test]
        public void ScenarioTransfer_AbsentOrUncitedExactRulingDoesNotMaterialise()
        {
            InstitutionalConsequenceRun absentRun = CreateRun();
            InstitutionalScenarioExecutionContext absentContext =
                CreateScenarioTransferContext(absentRun);
            InstitutionalScenarioEntitlementPhase.RegisterInitial(absentContext);

            Assert.DoesNotThrow(() =>
                InstitutionalScenarioEntitlementPhase.TransferDue(absentContext, 4));
            Assert.AreEqual(
                "agent.old-holder",
                absentContext.EntitlementRegistry.Find(EntitlementId, ResourceId)
                    .CurrentHolderAgentId);

            InstitutionalConsequenceRun uncitedRun = CreateRun();
            InstitutionalScenarioExecutionContext uncitedContext =
                CreateScenarioTransferContext(uncitedRun);
            InstitutionalScenarioEntitlementPhase.RegisterInitial(uncitedContext);
            AddRuling(uncitedRun, "ruling.test.exact", 4);

            Assert.DoesNotThrow(() =>
                InstitutionalScenarioEntitlementPhase.TransferDue(uncitedContext, 4));
            Assert.AreEqual(
                "agent.old-holder",
                uncitedContext.EntitlementRegistry.Find(EntitlementId, ResourceId)
                    .CurrentHolderAgentId);
            Assert.That(uncitedRun.Report.MaterialConsequences, Is.Empty);
            Assert.That(uncitedRun.Report.ConnectedOutcomes, Is.Empty);
        }

        [Test]
        public void ScenarioTransfer_CitedButDispositionIneligibleRulingDoesNotMaterialise()
        {
            InstitutionalConsequenceRun run = CreateRun();
            InstitutionalScenarioExecutionContext context =
                CreateScenarioTransferContext(run);
            InstitutionalScenarioEntitlementPhase.RegisterInitial(context);
            Ruling denied = AddRuling(run, "ruling.test.exact", 4);
            denied.Disposition = RulingDisposition.Denied;
            denied.CitedHoldingIds.Add("holding.test");

            Assert.DoesNotThrow(() =>
                InstitutionalScenarioEntitlementPhase.TransferDue(context, 4));

            ExclusiveEntitlementState state = context.EntitlementRegistry.Find(
                EntitlementId,
                ResourceId);
            Assert.That(state.CurrentHolderAgentId, Is.EqualTo("agent.old-holder"));
            Assert.That(state.LastMutationCauseId, Is.Null);
            Assert.That(run.Report.OfficialStatusMutations, Is.Empty);
            Assert.That(run.Report.MaterialConsequences, Is.Empty);
            Assert.That(run.Report.ConnectedOutcomes, Is.Empty);
        }

        [Test]
        public void ScenarioTransfer_ForeignExactCauseStillRejectsWithoutMutation()
        {
            InstitutionalConsequenceRun run = CreateRun();
            InstitutionalScenarioExecutionContext context =
                CreateScenarioTransferContext(run);
            InstitutionalScenarioEntitlementPhase.RegisterInitial(context);
            Ruling foreign = AddRuling(run, "ruling.test.exact", 4);
            foreign.CaseId = "case.test.foreign";
            foreign.CitedHoldingIds.Add("holding.test");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioEntitlementPhase.TransferDue(context, 4));

            StringAssert.Contains("exact cause ruling is inconsistent", exception.Message);
            Assert.AreEqual(
                "agent.old-holder",
                context.EntitlementRegistry.Find(EntitlementId, ResourceId)
                    .CurrentHolderAgentId);
            Assert.That(run.Report.MaterialConsequences, Is.Empty);
            Assert.That(run.Report.ConnectedOutcomes, Is.Empty);
        }

        private static InstitutionalScenarioExecutionContext CreateScenarioTransferContext(
            InstitutionalConsequenceRun run)
        {
            run.FinalSocietyState.GetAgent("agent.old-holder").Standing
                .SetRecognised(HolderStatusId, true);
            var definition = new InstitutionalScenarioDefinition();
            definition.ExclusiveEntitlements.Add(new ScenarioExclusiveEntitlementDefinition
            {
                EntitlementId = EntitlementId,
                ResourceId = ResourceId,
                OfficialStatusId = HolderStatusId,
                InitialHolderRoleId = "role.old-holder",
                Units = 1,
            });
            definition.Holdings.Add(new ScenarioHoldingDefinition
            {
                HoldingId = "holding.test",
                RuleId = "rule.test",
            });
            definition.EntitlementTransfers.Add(
                new ScenarioExclusiveEntitlementTransferDefinition
                {
                    TransferId = "transfer.test",
                    Cycle = 4,
                    EntitlementId = EntitlementId,
                    FromRoleId = "role.old-holder",
                    ToRoleId = "role.new-holder",
                    CauseCaseId = "case.test.entitlement",
                    CauseRulingId = "ruling.test.exact",
                    CauseHoldingId = "holding.test",
                    RequiredRulingDisposition = RulingDisposition.Recognised,
                });
            return new InstitutionalScenarioExecutionContext(
                definition,
                new InstitutionalPolicyConfiguration(),
                run,
                new Dictionary<string, string>
                {
                    ["role.old-holder"] = "agent.old-holder",
                    ["role.new-holder"] = "agent.new-holder",
                });
        }

        private static InstitutionalConsequenceRun CreateRun()
        {
            var society = new SocietyState();
            society.Agents.Add(CreateAgent("agent.old-holder", 0));
            society.Agents.Add(CreateAgent("agent.new-holder", 1));
            society.Agents.Add(CreateAgent("agent.observer", 2));
            return new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport(),
                FinalSocietyState = society,
            };
        }

        private static AgentState CreateAgent(string id, int ordinal)
        {
            return new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = id,
                DisplayName = id,
                SpeciesId = "species.test",
                HouseholdId = "household.test",
                EmployerId = "institution.test",
            };
        }

        private static Ruling AddRuling(
            InstitutionalConsequenceRun run,
            string rulingId,
            long cycle)
        {
            var ruling = new Ruling
            {
                RulingId = rulingId,
                CaseId = "case.test.entitlement",
                Cycle = cycle,
                PolicyConfigurationId = "configuration.test",
                PolicyVersion = "configuration.test.v1",
                Disposition = RulingDisposition.Recognised,
                FindingId = "finding.test.entitlement",
            };
            run.Report.Rulings.Add(ruling);
            return ruling;
        }
    }
}
