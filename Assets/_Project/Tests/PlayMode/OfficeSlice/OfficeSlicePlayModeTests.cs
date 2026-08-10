using System.Collections;
using System.Collections.Generic;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode
{
    public sealed class OfficeSlicePlayModeTests
    {
        [UnityTest]
        public IEnumerator FirstCustomerStartsWithoutDebugInput()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSliceBootstrap bootstrap =
                Object.FindObjectOfType<OfficeSliceBootstrap>();

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.SimulationState.M2Enabled, Is.True);
            Assert.That(bootstrap.SimulationState.Customers.ActiveDeskCustomer,
                Is.Not.Null);
            Assert.That(bootstrap.SimulationState.Customers.ActiveDeskCustomer.DisplayName,
                Is.EqualTo("NIA BELL"));
            Assert.That(bootstrap.SimulationState.Customers.HasAtMostOneActiveDeskCustomer(),
                Is.True);
        }

        [UnityTest]
        public IEnumerator ManualCaseCompletesThroughPaperMoneyAndDecision()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSliceBootstrap bootstrap =
                Object.FindObjectOfType<OfficeSliceBootstrap>();
            OfficeSimulationState state = bootstrap.SimulationState;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;

            NavigateTo(state, intent, input, "front-desk.interact");
            PressPrimary(state, intent, input); // TAKE
            Assert.That(state.Carry.CarriedFolderId, Is.EqualTo(caseId));
            PressPrimary(state, intent, input); // SEND TO PAPERS
            NavigateTo(state, intent, input, "paper-room.interact");
            state.AdvanceTicks(OfficeQueueService.DefaultTransferDurationTicks);
            PressPrimary(state, intent, input); // TAKE
            PressPrimary(state, intent, input); // CHECK PAPERS
            PressChoice(state, intent, input, 2); // PAYMENT DATE
            PressPrimary(state, intent, input); // SEND TO MONEY
            NavigateTo(state, intent, input, "money-room.interact");
            state.AdvanceTicks(OfficeQueueService.DefaultTransferDurationTicks);
            PressPrimary(state, intent, input); // TAKE
            PressPrimary(state, intent, input); // TRACE MONEY
            PressChoice(state, intent, input, 2); // HOLDING ACCOUNT
            PressPrimary(state, intent, input); // SEND TO FRONT
            NavigateTo(state, intent, input, "front-desk.interact");
            state.AdvanceTicks(OfficeQueueService.DefaultTransferDurationTicks);
            PressChoice(state, intent, input, 1); // HELP CUSTOMER

            OfficeDecisionRecord decision = state.Decisions.RecordFor(caseId);
            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.Stamp, Is.EqualTo("HELP CUSTOMER"));
            Assert.That(state.Decisions.CommitCount, Is.EqualTo(1));
            Assert.That(state.Customers.Customers[0].QueueState,
                Is.EqualTo(OfficeCustomerQueueState.Complete));
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);

            OfficeSimulationState replay =
                OfficeSimulationState.CreateM2Replay(state.CommandLog);
            replay.AdvanceTicks((int)state.CurrentTick);
            Assert.That(replay.Checksum, Is.EqualTo(state.Checksum));
            Assert.That(replay.OrderedStateSnapshot, Is.EqualTo(state.OrderedStateSnapshot));
        }

        [UnityTest]
        public IEnumerator StaffAndWardenCanShareWorkWithoutDuplicateOwnership()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSimulationState state = Object.FindObjectOfType<OfficeSliceBootstrap>()
                .SimulationState;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;

            PressChoice(state, intent, input, 3); // ASSIGN RUNNER
            NavigateTo(state, intent, input, "front-desk.interact");
            PressPrimary(state, intent, input); // WARDEN TAKES FIRST
            state.AdvanceTicks(250);

            Assert.That(state.Carry.CarriedFolderId, Is.EqualTo(caseId));
            Assert.That(state.Staff.Get(OfficeStaffSystem.RunnerId).TaskState,
                Is.EqualTo(OfficeStaffTaskState.Blocked));
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);

            intent.BufferDrop(state.CurrentTick);
            input.AdvanceOneTick();
            state.AdvanceTicks(101);

            OfficeFolderState folder = state.Queues.GetFolder(caseId);
            Assert.That(folder.OwnerKind, Is.EqualTo(OfficeFolderOwnerKind.RoomQueue));
            Assert.That(folder.CurrentRoom, Is.EqualTo(OfficeRoomId.PaperRoom));
            Assert.That(state.Staff.Get(OfficeStaffSystem.RunnerId).TaskState,
                Is.EqualTo(OfficeStaffTaskState.Idle));
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
        }

        [UnityTest]
        public IEnumerator OfficeSliceSceneBootsAsOneRootWithSixCases()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;

            Scene scene = SceneManager.GetActiveScene();
            Assert.That(scene.name, Is.EqualTo("OfficeSlice"));
            GameObject[] roots = scene.GetRootGameObjects();
            Assert.That(roots, Has.Length.EqualTo(1));
            Assert.That(roots[0].name, Is.EqualTo("Office Slice Bootstrap"));

            OfficeSliceBootstrap bootstrap = roots[0].GetComponent<OfficeSliceBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Ready, Is.True);
            Assert.That(bootstrap.CaseRepository.Cases, Has.Count.EqualTo(6));
            Assert.That(bootstrap.VisibleFolderCount, Is.EqualTo(6));
            Assert.That(bootstrap.CriticalRoutesValid, Is.True);
        }

        [UnityTest]
        public IEnumerator OfficeSliceRoutesFoldersWithoutDuplicateOwnership()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSliceBootstrap bootstrap =
                Object.FindObjectOfType<OfficeSliceBootstrap>();

            bootstrap.ForceAllFoldersThroughM1Route();

            Assert.That(bootstrap.SimulationState.Queues.AllFoldersAtFrontDesk(), Is.True);
            Assert.That(bootstrap.SimulationState.Queues.HasSingleLogicalOwnerForEveryFolder(),
                Is.True);
            Assert.That(bootstrap.QueueSummary(), Does.Contain("FrontDesk:"));
        }

        private static void NavigateTo(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input,
            string interactionPointId)
        {
            OfficeInteractionPoint point = state.Grid.GetInteractionPoint(interactionPointId);
            Assert.That(point, Is.Not.Null);
            Assert.That(state.Grid.TryFindPath(
                state.Warden.Cell(state.Grid), point.Cell, out List<OfficeCell> path), Is.True);
            int ticksPerCell = OfficeGrid.LogicalSubunitsPerCell /
                OfficeWardenState.MovementSubunitsPerTick;
            for (int cell = 1; cell < path.Count; cell++)
            {
                int x = path[cell].X - path[cell - 1].X;
                int z = path[cell].Z - path[cell - 1].Z;
                OfficeInputDirection direction = x < 0
                    ? OfficeInputDirection.Left
                    : x > 0
                        ? OfficeInputDirection.Right
                        : z < 0
                            ? OfficeInputDirection.Down
                            : OfficeInputDirection.Up;
                intent.SetMovement(direction);
                for (int tick = 0; tick < ticksPerCell; tick++)
                    input.AdvanceOneTick();
            }
            intent.SetMovement(OfficeInputDirection.None);
            Assert.That(state.Warden.Cell(state.Grid), Is.EqualTo(point.Cell));
        }

        private static void PressPrimary(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            intent.BufferInteraction(state.CurrentTick);
            input.AdvanceOneTick();
        }

        private static void PressChoice(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input,
            int oneBasedChoice)
        {
            intent.BufferChoice(oneBasedChoice, state.CurrentTick);
            input.AdvanceOneTick();
        }
    }
}
