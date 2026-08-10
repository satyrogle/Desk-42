using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class OfficeM2GateATests
    {
        [Test]
        public void CustomerSchedule_IsStableAndReplayable()
        {
            OfficeSimulationState live = OfficeSimulationState.CreateM2();
            live.AdvanceTicks(1600);
            OfficeSimulationState replay =
                OfficeSimulationState.CreateM2Replay(live.CommandLog);
            replay.AdvanceTicks(1600);

            Assert.That(live.Customers.Customers, Has.Count.EqualTo(6));
            Assert.That(replay.OrderedStateSnapshot, Is.EqualTo(live.OrderedStateSnapshot));
            for (int i = 0; i < live.Customers.Customers.Count; i++)
            {
                OfficeCustomerState first = live.Customers.Customers[i];
                OfficeCustomerState second = replay.Customers.Customers[i];
                Assert.That(second.CustomerId, Is.EqualTo(first.CustomerId));
                Assert.That(second.ArrivalTick, Is.EqualTo(first.ArrivalTick));
                Assert.That(second.LinkedAutomationClaimId,
                    Is.EqualTo(first.LinkedAutomationClaimId));
                Assert.That(second.AuthoredOfficeTraitId,
                    Is.EqualTo(first.AuthoredOfficeTraitId));
            }
        }

        [Test]
        public void FrontDesk_AllowsOnlyOneActiveCustomer()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            for (int tick = 0; tick < 1800; tick++)
            {
                Assert.That(state.Customers.HasAtMostOneActiveDeskCustomer(), Is.True);
                state.AdvanceOneTick();
            }
            Assert.That(state.Customers.ActiveDeskCustomer, Is.Not.Null);
            Assert.That(state.Customers.ActiveDeskCustomer.DisplayName, Is.EqualTo("NIA BELL"));
        }

        [Test]
        public void CarryState_HasOneLogicalOwner()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;

            Assert.That(state.Carry.TryTake(caseId, OfficeRoomId.FrontDesk), Is.True);
            Assert.That(state.Carry.CarriedFolderId, Is.EqualTo(caseId));
            Assert.That(state.Queues.GetFolder(caseId).OwnerKind,
                Is.EqualTo(OfficeFolderOwnerKind.Warden));
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
            Assert.That(state.Carry.TryTake(state.Cases.Cases[1].AutomationClaimId,
                OfficeRoomId.FrontDesk), Is.False);
            Assert.That(state.Carry.TryDrop(OfficeRoomId.FrontDesk), Is.True);
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
        }

        [Test]
        public void InvalidSend_DoesNotLoseFolder()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            Assert.That(state.Carry.TryTake(caseId, OfficeRoomId.FrontDesk), Is.True);
            string before = state.OrderedStateSnapshot;

            OfficeCommand invalid = state.CreateSendCommand(
                caseId, OfficeRoomId.FrontDesk);
            Assert.That(state.TryQueueCommand(invalid, out _), Is.True);
            state.AdvanceOneTick();

            Assert.That(state.Carry.CarriedFolderId, Is.EqualTo(caseId));
            Assert.That(state.Queues.GetFolder(caseId).OwnerKind,
                Is.EqualTo(OfficeFolderOwnerKind.Warden));
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
            Assert.That(state.Failures[state.Failures.Count - 1].Code,
                Is.EqualTo("INVALID_SEND"));
            Assert.That(before, Does.Contain("Warden:warden"));
        }

        [Test]
        public void CompareTask_ProducesStableReason()
        {
            OfficeSimulationState first = OfficeSimulationState.CreateM2();
            OfficeSimulationState second = OfficeSimulationState.CreateM2();
            string firstId = first.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            string secondId = second.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;

            Assert.That(first.ManualTasks.TryStart(OfficeManualTaskKind.Compare,
                firstId, 10, out _), Is.True);
            Assert.That(second.ManualTasks.TryStart(OfficeManualTaskKind.Compare,
                secondId, 10, out _), Is.True);
            Assert.That(first.ManualTasks.TrySubmit((int)OfficePaperEntry.PaymentDate,
                out bool firstComplete, out string firstReason), Is.True);
            Assert.That(second.ManualTasks.TrySubmit((int)OfficePaperEntry.PaymentDate,
                out bool secondComplete, out string secondReason), Is.True);

            Assert.That(firstComplete, Is.True);
            Assert.That(secondComplete, Is.True);
            Assert.That(firstReason, Is.EqualTo("THE PAPERS DON'T MATCH"));
            Assert.That(secondReason, Is.EqualTo(firstReason));
        }

        [Test]
        public void TraceTask_ProducesStablePathResult()
        {
            OfficeSimulationState first = OfficeSimulationState.CreateM2();
            OfficeSimulationState second = OfficeSimulationState.CreateM2();
            CompleteTutorialCompare(first);
            CompleteTutorialCompare(second);
            string firstId = first.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            string secondId = second.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;

            Assert.That(first.ManualTasks.TryStart(OfficeManualTaskKind.Trace,
                firstId, 20, out _), Is.True);
            Assert.That(second.ManualTasks.TryStart(OfficeManualTaskKind.Trace,
                secondId, 20, out _), Is.True);
            Assert.That(first.ManualTasks.TrySubmit(1,
                out bool firstComplete, out string firstResult), Is.True);
            Assert.That(second.ManualTasks.TrySubmit(1,
                out bool secondComplete, out string secondResult), Is.True);

            Assert.That(firstComplete, Is.True);
            Assert.That(secondComplete, Is.True);
            Assert.That(firstResult, Is.EqualTo("MONEY MOVED"));
            Assert.That(secondResult, Is.EqualTo(firstResult));
            Assert.That(first.ManualTasks.RecordFor(firstId).TracePathSummary,
                Is.EqualTo("COMPANY > PAYMENT RECORD > HOLDING ACCOUNT"));
        }

        [Test]
        public void Decide_CommitsExactlyOnce()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;

            Assert.That(state.Decisions.DefaultScope.ToString(), Is.EqualTo("Narrow"));
            Assert.That(state.Decisions.DefaultProcedures, Is.Empty);
            Assert.That(state.Decisions.TryCommit(caseId,
                OfficeDecisionChoice.HelpCustomer,
                out OfficeDecisionRecord first,
                out string firstFailure), Is.True, firstFailure);
            Assert.That(state.Decisions.TryCommit(caseId,
                OfficeDecisionChoice.RejectCase,
                out OfficeDecisionRecord duplicate,
                out string duplicateFailure), Is.False);

            Assert.That(first, Is.Not.Null);
            Assert.That(first.Stamp, Is.EqualTo("HELP CUSTOMER"));
            Assert.That(first.RulingId, Is.Not.Empty);
            Assert.That(first.DirectChanges, Is.Not.Null);
            Assert.That(duplicate, Is.SameAs(first));
            Assert.That(duplicateFailure, Is.EqualTo("DECISION_ALREADY_MADE"));
            Assert.That(state.Decisions.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void M1CommandSchema_RemainsAcceptedByM2Log()
        {
            var log = new OfficeCommandLog();
            var legacy = new OfficeCommand(1, 1, 1, OfficeCommandKind.Move,
                "warden", string.Empty, 1, 0, string.Empty);

            Assert.That(log.TryRecord(legacy, out string failure), Is.True, failure);
            OfficeSimulationState replay = OfficeSimulationState.CreateM2Replay(log);
            replay.AdvanceOneTick();
            Assert.That(replay.AppliedCommandCount, Is.EqualTo(1));
        }

        private static void CompleteTutorialCompare(OfficeSimulationState state)
        {
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            Assert.That(state.ManualTasks.TryStart(OfficeManualTaskKind.Compare,
                caseId, 10, out _), Is.True);
            Assert.That(state.ManualTasks.TrySubmit((int)OfficePaperEntry.PaymentDate,
                out bool complete, out _), Is.True);
            Assert.That(complete, Is.True);
        }
    }
}
