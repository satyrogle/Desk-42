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
        public IEnumerator AutomationClearsTwoKnownCases()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSimulationState state = Object.FindObjectOfType<OfficeSliceBootstrap>()
                .SimulationState;
            UnlockAutomation(state);
            string first = state.Cases.Cases[1].AutomationClaimId;
            string second = state.Cases.Cases[5].AutomationClaimId;

            Assert.That(state.Queues.TryTransferCase(
                first, OfficeRoomId.WeirdRoom, state.CurrentTick, 1), Is.True);
            Assert.That(state.Queues.TryTransferCase(
                second, OfficeRoomId.WeirdRoom, state.CurrentTick, 1), Is.True);
            state.AdvanceOneTick();
            state.AdvanceTicks(OfficeAutomationRuleState.TransferDurationTicks);

            Assert.That(state.AutomationRule.Matches, Has.Count.EqualTo(2));
            Assert.That(state.AutomationRule.Matches[0].Matched, Is.True);
            Assert.That(state.AutomationRule.Matches[1].Matched, Is.True);
            Assert.That(state.Queues.GetFolder(first).CurrentRoom,
                Is.EqualTo(OfficeRoomId.MoneyRoom));
            Assert.That(state.Queues.GetFolder(second).CurrentRoom,
                Is.EqualTo(OfficeRoomId.MoneyRoom));
        }

        [UnityTest]
        public IEnumerator CopyEchoEdgeCaseTriggersDeterministicBreak()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSimulationState state = Object.FindObjectOfType<OfficeSliceBootstrap>()
                .SimulationState;

            DriveToBreak(state);

            Assert.That(state.BreakState.Active, Is.True);
            Assert.That(state.BreakState.OriginalMarkedHard, Is.True);
            Assert.That(state.AutomationRule.LastAcceptedCopyId, Is.Not.Empty);
            Assert.That(state.Queues.ActiveCopyCount, Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator TwoRecoveryOrdersReachValidClosingState()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSimulationState calmFirst = Object.FindObjectOfType<OfficeSliceBootstrap>()
                .SimulationState;
            DriveToBreak(calmFirst);
            OfficeSimulationState fixFirst = OfficeSimulationState.CreateM2();
            DriveToBreak(fixFirst);

            RecoverBreak(calmFirst, fixMachineFirst: false);
            RecoverBreak(fixFirst, fixMachineFirst: true);

            Assert.That(calmFirst.BreakState.Recovered, Is.True);
            Assert.That(fixFirst.BreakState.Recovered, Is.True);
            Assert.That(calmFirst.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
            Assert.That(fixFirst.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
        }

        [UnityTest]
        public IEnumerator FullShiftReachesResultWithoutDebugControls()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSimulationState state = Object.FindObjectOfType<OfficeSliceBootstrap>()
                .SimulationState;
            DriveToBreak(state);
            RecoverBreak(state, fixMachineFirst: true);
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);

            while (state.Decisions.CommitCount < 6)
                CompleteActiveCase(state, intent, input);
            state.AdvanceOneTick();

            Assert.That(state.Shift.Phase, Is.EqualTo(OfficeShiftPhase.Result));
            Assert.That(state.Shift.Success, Is.True);
            Assert.That(state.CausalEvents.ContainsOnlyObservableEvents(), Is.True);
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
            OfficeSimulationState replay =
                OfficeSimulationState.CreateM2Replay(state.CommandLog);
            replay.AdvanceTicks((int)state.CurrentTick);
            Assert.That(replay.Checksum, Is.EqualTo(state.Checksum));
            Assert.That(replay.OrderedStateSnapshot,
                Is.EqualTo(state.OrderedStateSnapshot));
            Debug.Log("M2_FULL_SHIFT_CHECKSUM " + state.Checksum);
        }

        [UnityTest]
        public IEnumerator ShiftOneCompletesIntoUpgradeChoiceWithoutDebugControls()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSliceBootstrap bootstrap =
                Object.FindObjectOfType<OfficeSliceBootstrap>();
            OfficeCampaignState campaign = bootstrap.CampaignState;
            OfficeSimulationState state = campaign.CurrentSimulation;
            DriveToBreak(state);
            RecoverBreak(state, fixMachineFirst: true);
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            while (state.Decisions.CommitCount < 6)
                CompleteActiveCase(state, intent, input);
            state.AdvanceOneTick();

            Assert.That(campaign.Phase,
                Is.EqualTo(OfficeCampaignPhase.ChooseUpgrade));
            intent.BufferChoice(1, state.CurrentTick);
            input.AdvanceOneTick();
            Assert.That(campaign.Upgrades.FastTraysTier, Is.EqualTo(1));
            Assert.That(campaign.Phase,
                Is.EqualTo(OfficeCampaignPhase.ReadyForNextShift));
            intent.BufferInteraction(state.CurrentTick);
            input.AdvanceOneTick();
            Assert.That(campaign.CurrentShiftOrdinal, Is.EqualTo(2));
            Assert.That(campaign.CurrentSimulation, Is.Not.SameAs(state));
            Assert.That(campaign.CurrentSimulation.Customers.Customers[0].CustomerId,
                Is.EqualTo(state.Customers.Customers[0].CustomerId));
            yield return null;
            Assert.That(bootstrap.SimulationState,
                Is.SameAs(campaign.CurrentSimulation));
        }

        [UnityTest]
        public IEnumerator FailureRestartReturnsToCleanCheckpoint()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSliceBootstrap bootstrap =
                Object.FindObjectOfType<OfficeSliceBootstrap>();
            OfficeSimulationState changed = bootstrap.SimulationState;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            changed.Shift.ForceFailureForDevelopment();
#endif
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(changed, intent);
            intent.BufferRestart(changed.CurrentTick);
            input.AdvanceOneTick();
            Assert.That(changed.Shift.RestartRequested, Is.True);

            Assert.That(bootstrap.RestartShift(), Is.True);
            OfficeSimulationState restarted = bootstrap.SimulationState;
            Assert.That(restarted, Is.Not.SameAs(changed));
            Assert.That(restarted.CurrentTick, Is.Zero);
            Assert.That(restarted.Shift.Phase, Is.EqualTo(OfficeShiftPhase.Briefing));
            Assert.That(restarted.Decisions.CommitCount, Is.Zero);
            Assert.That(restarted.Queues.ActiveCopyCount, Is.Zero);
            Assert.That(restarted.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
            yield return null;

            int activeRuntimeRoots = 0;
            for (int i = 0; i < bootstrap.transform.childCount; i++)
                if (bootstrap.transform.GetChild(i).gameObject.activeSelf &&
                    bootstrap.transform.GetChild(i).name == "Office Slice Runtime")
                    activeRuntimeRoots++;
            Assert.That(activeRuntimeRoots, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ControllerCanCompleteCriticalPath()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSimulationState state = Object.FindObjectOfType<OfficeSliceBootstrap>()
                .SimulationState;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;

            CompleteActiveCaseWithAnalogControls(state, intent, input);

            Assert.That(state.Decisions.RecordFor(caseId), Is.Not.Null);
            Assert.That(state.Decisions.RecordFor(caseId).Stamp,
                Is.EqualTo("HELP CUSTOMER"));
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

        private static void UnlockAutomation(OfficeSimulationState state)
        {
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            OfficeCaseWorkDefinition work = state.WorkDefinitionFor(caseId);
            Assert.That(state.ManualTasks.TryStart(
                OfficeManualTaskKind.Compare, caseId, state.CurrentTick, out _), Is.True);
            Assert.That(state.ManualTasks.TrySubmit((int)work.PaperAnswer,
                out bool compared, out _), Is.True);
            Assert.That(compared, Is.True);
            Assert.That(state.ManualTasks.TryStart(
                OfficeManualTaskKind.Trace, caseId, state.CurrentTick, out _), Is.True);
            Assert.That(state.ManualTasks.TrySubmit(work.MoneyPathAnswer,
                out bool traced, out _), Is.True);
            Assert.That(traced, Is.True);
            state.AdvanceOneTick();
            Assert.That(state.AutomationRule.TryToggle(), Is.True);
        }

        private static void DriveToBreak(OfficeSimulationState state)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            CompleteActiveCase(state, intent, input);
            intent.BufferToggleRule(state.CurrentTick);
            input.AdvanceOneTick();
            CompleteActiveCase(state, intent, input);
            for (int tick = 0; tick < 1200 && !state.BreakState.Active; tick++)
                input.AdvanceOneTick();
            Assert.That(state.BreakState.Active, Is.True);
        }

        private static void CompleteActiveCase(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            OfficeCaseWorkDefinition work = state.WorkDefinitionFor(caseId);
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "paper-room.interact");
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            PressChoice(state, intent, input, (int)work.PaperAnswer + 1);
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "money-room.interact");
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            PressChoice(state, intent, input, work.MoneyPathAnswer + 1);
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "front-desk.interact");
            PressChoice(state, intent, input, 1);
            Assert.That(state.Decisions.RecordFor(caseId), Is.Not.Null);
        }

        private static void CompleteActiveCaseWithAnalogControls(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            OfficeCaseWorkDefinition work = state.WorkDefinitionFor(caseId);
            NavigateToAnalog(state, intent, input, "front-desk.interact");
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            NavigateToAnalog(state, intent, input, "paper-room.interact");
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            PressChoice(state, intent, input, (int)work.PaperAnswer + 1);
            PressPrimary(state, intent, input);
            NavigateToAnalog(state, intent, input, "money-room.interact");
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            PressChoice(state, intent, input, work.MoneyPathAnswer + 1);
            PressPrimary(state, intent, input);
            NavigateToAnalog(state, intent, input, "front-desk.interact");
            PressChoice(state, intent, input, 1);
        }

        private static void CalmUntilActionable(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            int safety = 0;
            while (state.PrimaryActionLabel == "CALM" && safety++ < 6)
            {
                PressPrimary(state, intent, input);
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
                if (state.PrimaryActionLabel == "CALM" &&
                    state.CustomerPressure.CalmCooldownRemainingTicks > 0)
                    state.AdvanceTicks(
                        state.CustomerPressure.CalmCooldownRemainingTicks);
            }
        }

        private static void NavigateToAnalog(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input,
            string interactionPointId)
        {
            OfficeInteractionPoint point = state.Grid.GetInteractionPoint(interactionPointId);
            Assert.That(state.Grid.TryFindPath(
                state.Warden.Cell(state.Grid), point.Cell, out List<OfficeCell> path), Is.True);
            int ticksPerCell = OfficeGrid.LogicalSubunitsPerCell /
                OfficeWardenState.MovementSubunitsPerTick;
            for (int cell = 1; cell < path.Count; cell++)
            {
                int x = path[cell].X - path[cell - 1].X;
                int z = path[cell].Z - path[cell - 1].Z;
                OfficeInputDirection direction =
                    OfficeInputCanonicalizer.FromAnalog(x, z);
                intent.SetMovement(direction);
                for (int tick = 0; tick < ticksPerCell; tick++)
                    input.AdvanceOneTick();
            }
            intent.SetMovement(OfficeInputDirection.None);
        }

        private static void RecoverBreak(
            OfficeSimulationState state,
            bool fixMachineFirst)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            if (!fixMachineFirst)
            {
                PressPrimary(state, intent, input);
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
            }
            NavigateTo(state, intent, input, "weird-room.interact");
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "front-desk.interact");
            if (fixMachineFirst)
            {
                PressPrimary(state, intent, input);
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
            }
            NavigateTo(state, intent, input, "money-room.interact");
            int safety = 0;
            while (state.Queues.ActiveCopyCount > 0 && safety++ < 40)
            {
                if (state.PrimaryActionLabel == "CALM")
                {
                    PressPrimary(state, intent, input);
                    state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(
                        state.Queues.FirstActiveCopyAt(OfficeRoomId.MoneyRoom)))
                    state.AdvanceOneTick();
                else
                    PressPrimary(state, intent, input);
            }
            if (state.PrimaryActionLabel == "CALM")
            {
                PressPrimary(state, intent, input);
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
            }
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            PressPrimary(state, intent, input);
            intent.BufferDrop(state.CurrentTick);
            input.AdvanceOneTick();
            state.AdvanceOneTick();
        }
    }
}
