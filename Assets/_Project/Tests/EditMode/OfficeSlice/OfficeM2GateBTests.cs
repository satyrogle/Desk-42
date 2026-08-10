using System;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class OfficeM2GateBTests
    {
        [Test]
        public void CustomerPressure_HasNoRandomTransition()
        {
            OfficeSimulationState first = OfficeSimulationState.CreateM2();
            OfficeSimulationState second = OfficeSimulationState.CreateM2();

            first.AdvanceTicks(2000);
            second.AdvanceTicks(2000);

            Assert.That(second.OrderedStateSnapshot,
                Is.EqualTo(first.OrderedStateSnapshot));
            Assert.That(second.Customers.Customers[0].VisibleMoodState,
                Is.EqualTo(first.Customers.Customers[0].VisibleMoodState));
            Assert.That(first.CustomerPressure.RecordFor(
                first.Customers.Customers[0].CustomerId).LastAuthoredCause,
                Is.EqualTo("WAITING"));
        }

        [Test]
        public void Calm_UsesTickDurationAndCooldown()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            state.AdvanceTicks(500);
            OfficeCustomerState customer = state.Customers.ActiveDeskCustomer;
            OfficeCustomerPressureRecord pressure =
                state.CustomerPressure.RecordFor(customer.CustomerId);
            int before = pressure.PressureTicks;

            Assert.That(state.CustomerPressure.TryStartCalm(
                customer.CustomerId, state.Warden.Cell(state.Grid)), Is.True);
            state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks - 1);
            Assert.That(state.CustomerPressure.CalmActive, Is.True);
            Assert.That(state.CustomerPressure.CalmRemainingTicks, Is.EqualTo(1));
            state.AdvanceOneTick();

            Assert.That(state.CustomerPressure.CalmActive, Is.False);
            Assert.That(state.CustomerPressure.CalmCooldownRemainingTicks,
                Is.EqualTo(OfficeCustomerPressureState.CalmCooldownTicks));
            Assert.That(pressure.PressureTicks, Is.LessThan(before));
            Assert.That(state.CustomerPressure.TryStartCalm(
                customer.CustomerId, state.Warden.Cell(state.Grid)), Is.False);
            state.AdvanceTicks(OfficeCustomerPressureState.CalmCooldownTicks);
            Assert.That(state.CustomerPressure.CalmCooldownRemainingTicks, Is.Zero);
        }

        [Test]
        public void StaffAssignment_IsStable()
        {
            OfficeSimulationState first = OfficeSimulationState.CreateM2();
            OfficeSimulationState second = OfficeSimulationState.CreateM2();
            AssignBoth(first);
            AssignBoth(second);

            int runnerStartX = first.Staff.Get(OfficeStaffSystem.RunnerId).XSubunits;
            int runnerStartZ = first.Staff.Get(OfficeStaffSystem.RunnerId).ZSubunits;
            first.AdvanceOneTick();
            second.AdvanceOneTick();
            OfficeStaffState runnerAfterOne = first.Staff.Get(OfficeStaffSystem.RunnerId);
            Assert.That(Math.Abs(runnerAfterOne.XSubunits - runnerStartX) +
                Math.Abs(runnerAfterOne.ZSubunits - runnerStartZ),
                Is.LessThanOrEqualTo(OfficeStaffSystem.MovementSubunitsPerTick));
            first.AdvanceTicks(399);
            second.AdvanceTicks(399);

            Assert.That(first.Staff.Staff, Has.Count.EqualTo(2));
            Assert.That(first.Staff.Get(OfficeStaffSystem.RunnerId).TaskState,
                Is.EqualTo(OfficeStaffTaskState.Idle));
            Assert.That(first.Staff.Get(OfficeStaffSystem.TalkerId).TaskState,
                Is.EqualTo(OfficeStaffTaskState.AttendingCustomer));
            Assert.That(first.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
            Assert.That(second.OrderedStateSnapshot,
                Is.EqualTo(first.OrderedStateSnapshot));
        }

        [Test]
        public void Help_IsTickBasedAcrossRenderRates()
        {
            OfficeRoomWorkState atThirty = SimulateHelp(30);
            OfficeRoomWorkState atSixty = SimulateHelp(60);
            OfficeRoomWorkState atOneFortyFour = SimulateHelp(144);

            Assert.That(atThirty.Job("job.help").RemainingTicks, Is.Zero);
            Assert.That(atSixty.Job("job.help").RemainingTicks,
                Is.EqualTo(atThirty.Job("job.help").RemainingTicks));
            Assert.That(atOneFortyFour.Job("job.help").RemainingTicks,
                Is.EqualTo(atThirty.Job("job.help").RemainingTicks));
        }

        [Test]
        public void Help_MovementCancelsWithoutCompletingWork()
        {
            var work = new OfficeRoomWorkState();
            var start = new OfficeCell(0, 5);
            Assert.That(work.TryStartJob(
                "job.cancel", "case.cancel", OfficeRoomId.PaperRoom), Is.True);
            Assert.That(work.TryStartHelp(OfficeRoomId.PaperRoom, start), Is.True);

            work.AdvanceOneTick(new OfficeCell(1, 5));

            Assert.That(work.HelpActive, Is.False);
            Assert.That(work.Job("job.cancel").RemainingTicks,
                Is.EqualTo(OfficeRoomWorkState.DefaultDurationTicks - 1));
        }

        private static void AssignBoth(OfficeSimulationState state)
        {
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            string customerId = state.Customers.ActiveDeskCustomer.CustomerId;
            Assert.That(state.Staff.TryAssign(
                OfficeStaffSystem.RunnerId,
                caseId,
                OfficeRoomId.PaperRoom,
                state.CurrentTick,
                out string runnerFailure), Is.True, runnerFailure);
            Assert.That(state.Staff.TryAssign(
                OfficeStaffSystem.TalkerId,
                customerId,
                OfficeRoomId.FrontDesk,
                state.CurrentTick,
                out string talkerFailure), Is.True, talkerFailure);
        }

        private static OfficeRoomWorkState SimulateHelp(int renderFramesPerSecond)
        {
            var work = new OfficeRoomWorkState();
            var cell = new OfficeCell(0, 5);
            Assert.That(work.TryStartJob(
                "job.help", "case.help", OfficeRoomId.PaperRoom), Is.True);
            Assert.That(work.TryStartHelp(OfficeRoomId.PaperRoom, cell), Is.True);
            var clock = new OfficeSimulationClock();
            int frames = renderFramesPerSecond;
            for (int frame = 0; frame < frames; frame++)
                clock.Advance(1d / renderFramesPerSecond,
                    () => work.AdvanceOneTick(cell));
            Assert.That(clock.CurrentTick, Is.EqualTo(30));
            return work;
        }
    }
}
