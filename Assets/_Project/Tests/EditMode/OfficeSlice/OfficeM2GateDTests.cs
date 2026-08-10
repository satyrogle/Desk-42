using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class OfficeM2GateDTests
    {
        [Test]
        public void CausalRecap_ContainsOnlyObservableEvents()
        {
            OfficeSimulationState state = OfficeM2GateCTests.DriveToBreak();
            OfficeM2GateCTests.Recover(state, fixMachineFirst: true);

            Assert.That(state.CausalEvents.ContainsOnlyObservableEvents(), Is.True);
            Assert.That(state.CausalEvents.Events, Has.Count.GreaterThanOrEqualTo(6));
            Assert.That(Contains(state, OfficeCausalEventKind.RuleTaught), Is.True);
            Assert.That(Contains(state, OfficeCausalEventKind.CopiedFolderMatched), Is.True);
            Assert.That(Contains(state, OfficeCausalEventKind.CopySentToMoney), Is.True);
            Assert.That(Contains(state, OfficeCausalEventKind.MoneyFilled), Is.True);
            Assert.That(Contains(state, OfficeCausalEventKind.MachineStopped), Is.True);
            Assert.That(Contains(state, OfficeCausalEventKind.OriginalFound), Is.True);
        }

        [Test]
        public void ShiftPhases_AreDeterministicAndOrdered()
        {
            OfficeSimulationState first = OfficeM2GateCTests.DriveToBreak();
            OfficeSimulationState second = OfficeM2GateCTests.DriveToBreak();

            Assert.That(first.Shift.Phase, Is.EqualTo(OfficeShiftPhase.Break));
            Assert.That(second.Shift.Phase, Is.EqualTo(first.Shift.Phase));
            Assert.That(second.OrderedStateSnapshot,
                Is.EqualTo(first.OrderedStateSnapshot));
            OfficeM2GateCTests.Recover(first, fixMachineFirst: false);
            Assert.That(first.Shift.Phase, Is.EqualTo(OfficeShiftPhase.Closing));
        }

        [Test]
        public void RestartBeforeRush_ReturnsCleanCheckpoint()
        {
            OfficeSimulationState changed = OfficeSimulationState.CreateM2();
            Assert.That(changed.Carry.TryTake(
                changed.Customers.ActiveDeskCustomer.LinkedAutomationClaimId,
                OfficeRoomId.FrontDesk), Is.True);
            Assert.That(changed.Shift.TryRequestRestart(), Is.True);

            OfficeSimulationState restarted = OfficeSimulationState.CreateM2();

            Assert.That(restarted.CurrentTick, Is.Zero);
            Assert.That(restarted.Shift.Phase, Is.EqualTo(OfficeShiftPhase.Briefing));
            Assert.That(restarted.Decisions.CommitCount, Is.Zero);
            Assert.That(restarted.Queues.ActiveCopyCount, Is.Zero);
            Assert.That(restarted.Carry.IsCarrying, Is.False);
            Assert.That(restarted.Queues.GetQueue(OfficeRoomId.FrontDesk).Count,
                Is.EqualTo(6));
        }

        private static bool Contains(
            OfficeSimulationState state,
            OfficeCausalEventKind kind)
        {
            for (int i = 0; i < state.CausalEvents.Events.Count; i++)
                if (state.CausalEvents.Events[i].Kind == kind) return true;
            return false;
        }
    }
}
