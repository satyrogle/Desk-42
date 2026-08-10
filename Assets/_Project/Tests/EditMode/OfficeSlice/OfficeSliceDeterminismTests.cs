using System;
using System.Collections.Generic;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class OfficeSliceDeterminismTests
    {
        [Test]
        public void SixCaseProjectionPreservesStablePublicIdentity()
        {
            OfficeCaseRepository repository = OfficeCaseProjector.CreateSixCaseRepository();

            Assert.That(repository.Cases, Has.Count.EqualTo(6));
            Assert.That(repository.Cases, Is.All.Not.Null);
            Assert.That(repository.Cases, Is.Unique);
            for (int i = 0; i < repository.Cases.Count; i++)
            {
                OfficeCase officeCase = repository.Cases[i];
                Assert.That(officeCase.AutomationClaimId, Is.Not.Empty);
                Assert.That(officeCase.SourceCaseId, Is.Not.Empty);
                Assert.That(officeCase.IssueId, Is.Not.Empty);
                Assert.That(officeCase.DisplayId, Is.Not.Empty);
                Assert.That(officeCase.PublicEvidenceNeeds, Is.Not.Empty);
            }
        }

        [Test]
        public void RepositoryRejectsStableSourceCollision()
        {
            OfficeCase first = CreateCase("claim-a", "source-a", "issue-a");
            OfficeCase second = CreateCase("claim-b", "source-a", "issue-b");

            Assert.Throws<InvalidOperationException>(() =>
                new OfficeCaseRepository(new[] { first, second }));
        }

        [Test]
        public void CommandLogRejectsDuplicateAndFutureSchema()
        {
            var log = new OfficeCommandLog();
            Assert.That(log.TryRecord(OfficeCommand.Move(4, 1, 1, 0), out _), Is.True);
            Assert.That(log.TryRecord(OfficeCommand.Move(5, 1, 0, 1), out string duplicate),
                Is.False);
            Assert.That(duplicate, Does.Contain("Duplicate"));
            Assert.That(log.TryRecord(new OfficeCommand(2, 6, 2,
                OfficeCommandKind.Move, "warden", string.Empty, 1, 0, string.Empty),
                out string futureSchema), Is.False);
            Assert.That(futureSchema, Does.Contain("Unsupported"));
            Assert.That(log.Commands[0].Tick, Is.EqualTo(4));
        }

        [Test]
        public void GridProvidesDeterministicRoutesAndBlockedCollision()
        {
            OfficeGrid grid = OfficeGrid.CreateM1();
            for (int i = 0; i < grid.InteractionPoints.Count; i++)
            {
                Assert.That(grid.TryFindPath(grid.SpawnCell, grid.InteractionPoints[i].Cell,
                    out List<OfficeCell> path), Is.True);
                Assert.That(path[0], Is.EqualTo(grid.SpawnCell));
                Assert.That(path[path.Count - 1], Is.EqualTo(grid.InteractionPoints[i].Cell));
            }

            var warden = new OfficeWardenState(grid.SpawnCell);
            Assert.That(warden.TryMove(1, 1, grid), Is.True);
            Assert.That(warden.XSubunits, Is.EqualTo(grid.SpawnCell.X * 32 + 4));
            Assert.That(warden.ZSubunits, Is.EqualTo(grid.SpawnCell.Z * 32));
            Assert.That(grid.IsWalkable(new OfficeCell(-14, 0)), Is.False);
        }

        [Test]
        public void QueueTransferPausesAndRestoresSingleOwnership()
        {
            OfficeCaseRepository repository = OfficeCaseProjector.CreateSixCaseRepository();
            var queues = new OfficeQueueService(repository);
            string caseId = repository.Cases[0].AutomationClaimId;

            Assert.That(queues.TryTransferCase(caseId, OfficeRoomId.PaperRoom, 10), Is.True);
            Assert.That(queues.GetFolder(caseId).IsMoving, Is.True);
            queues.AdvanceToTick(24);
            Assert.That(queues.GetFolder(caseId).IsMoving, Is.True);
            queues.AdvanceToTick(25);
            Assert.That(queues.GetFolder(caseId).CurrentRoom, Is.EqualTo(OfficeRoomId.PaperRoom));
            Assert.That(queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
            Assert.That(queues.TryEnqueue(caseId, OfficeRoomId.MoneyRoom), Is.False);
        }

        [Test]
        public void MissingTargetFailureIsStable()
        {
            OfficeSimulationState first = OfficeSimulationState.Create(
                OfficeCaseProjector.CreateSixCaseRepository());
            OfficeSimulationState second = OfficeSimulationState.Create(
                OfficeCaseProjector.CreateSixCaseRepository());

            Assert.That(first.TryQueueCommand(OfficeCommand.Send(1, 1, "missing"), out _), Is.True);
            Assert.That(second.TryQueueCommand(OfficeCommand.Send(1, 1, "missing"), out _), Is.True);
            first.AdvanceOneTick();
            second.AdvanceOneTick();

            Assert.That(first.Failures, Has.Count.EqualTo(1));
            Assert.That(first.Failures[0].Code, Is.EqualTo("MISSING_TARGET"));
            Assert.That(first.Failures[0].ToString(), Is.EqualTo(second.Failures[0].ToString()));
        }

        [Test]
        public void FixedClockPausesStepsAndLimitsCatchUp()
        {
            var clock = new OfficeSimulationClock();
            int ticks = 0;
            Assert.That(clock.Advance(1d, () => ticks++), Is.EqualTo(4));
            Assert.That(ticks, Is.EqualTo(4));
            clock.SetPaused(true);
            Assert.That(clock.Advance(1d, () => ticks++), Is.EqualTo(0));
            Assert.That(clock.Step(() => ticks++), Is.True);
            Assert.That(ticks, Is.EqualTo(5));
            Assert.That(clock.CurrentTick, Is.EqualTo(5));
        }

        [Test]
        public void HeldInputAtThirtySixtyAndOneFortyFourFpsIsIdentical()
        {
            OfficeSimulationState atThirty = SimulateHeldInput(30, 4);
            OfficeSimulationState atSixty = SimulateHeldInput(60, 4);
            OfficeSimulationState atOneFortyFour = SimulateHeldInput(144, 4);

            Assert.That(atSixty.CommandLog.ToJson(),
                Is.EqualTo(atThirty.CommandLog.ToJson()));
            Assert.That(atOneFortyFour.CommandLog.ToJson(),
                Is.EqualTo(atThirty.CommandLog.ToJson()));
            Assert.That(atSixty.Warden.XSubunits, Is.EqualTo(atThirty.Warden.XSubunits));
            Assert.That(atSixty.Warden.ZSubunits, Is.EqualTo(atThirty.Warden.ZSubunits));
            Assert.That(atOneFortyFour.Warden.XSubunits,
                Is.EqualTo(atThirty.Warden.XSubunits));
            Assert.That(atOneFortyFour.Warden.ZSubunits,
                Is.EqualTo(atThirty.Warden.ZSubunits));
            Assert.That(atSixty.Checksum, Is.EqualTo(atThirty.Checksum));
            Assert.That(atOneFortyFour.Checksum, Is.EqualTo(atThirty.Checksum));
            Assert.That(atSixty.OrderedStateSnapshot,
                Is.EqualTo(atThirty.OrderedStateSnapshot));
            Assert.That(atOneFortyFour.OrderedStateSnapshot,
                Is.EqualTo(atThirty.OrderedStateSnapshot));
        }

        [Test]
        public void InputGeneratorQueuesAtMostOneWardenMovePerTick()
        {
            OfficeSimulationState state = SimulateHeldInput(144, 4);
            var moveTicks = new HashSet<long>();
            int moveCount = 0;
            for (int i = 0; i < state.CommandLog.Commands.Count; i++)
            {
                OfficeCommand command = state.CommandLog.Commands[i];
                if (command.Kind != OfficeCommandKind.Move || command.ActorId != "warden")
                    continue;

                moveCount++;
                Assert.That(moveTicks.Add(command.Tick), Is.True,
                    "More than one Warden Move was generated for tick " + command.Tick + ".");
            }

            Assert.That(moveCount, Is.EqualTo(120));
            Assert.That(moveTicks.Count, Is.EqualTo(120));
        }

        [Test]
        public void KeyboardAndGamepadDirectionsGenerateEquivalentCommands()
        {
            OfficeInputDirection[] directions =
            {
                OfficeInputDirection.Left,
                OfficeInputDirection.Right,
                OfficeInputDirection.Down,
                OfficeInputDirection.Up,
            };

            for (int i = 0; i < directions.Length; i++)
            {
                OfficeInputDirection keyboardDirection = KeyboardDirection(directions[i]);
                OfficeInputDirection gamepadDirection = GamepadDirection(directions[i]);
                Assert.That(gamepadDirection, Is.EqualTo(keyboardDirection));

                string keyboardLog = SingleMoveLog(keyboardDirection);
                string gamepadLog = SingleMoveLog(gamepadDirection);
                Assert.That(gamepadLog, Is.EqualTo(keyboardLog));
            }
        }

        [Test]
        public void BufferedInteractionFiresOnceAndReplayLocksOutLiveIntent()
        {
            OfficeCaseRepository repository = OfficeCaseProjector.CreateSixCaseRepository();
            OfficeSimulationState live = OfficeSimulationState.Create(repository);
            var liveIntent = new OfficeInputIntent();
            var liveGenerator = new OfficeInputCommandGenerator(live, liveIntent);

            liveIntent.BufferInteraction(live.CurrentTick);
            liveIntent.BufferInteraction(live.CurrentTick);
            Assert.That(liveIntent.InteractionExpiresAfterTick,
                Is.EqualTo(OfficeInputIntent.InteractionBufferTicks));
            for (int tick = 0; tick < 12; tick++) liveGenerator.AdvanceOneTick();

            Assert.That(CountCommands(live, OfficeCommandKind.Interact), Is.EqualTo(1));
            Assert.That(liveIntent.HasBufferedInteraction, Is.False);

            var replayLog = new OfficeCommandLog();
            Assert.That(replayLog.TryRecord(OfficeCommand.Move(1, 1, 1, 0), out _), Is.True);
            OfficeSimulationState replay = OfficeSimulationState.CreateReplay(repository, replayLog);
            var replayIntent = new OfficeInputIntent();
            replayIntent.SetMovement(OfficeInputDirection.Left);
            replayIntent.BufferInteraction(replay.CurrentTick);
            var replayGenerator = new OfficeInputCommandGenerator(replay, replayIntent);

            replayGenerator.AdvanceOneTick();

            Assert.That(replay.CommandLog.Commands, Has.Count.EqualTo(1));
            Assert.That(replay.Warden.XSubunits,
                Is.EqualTo(replay.Grid.SpawnCell.X * OfficeGrid.LogicalSubunitsPerCell +
                    OfficeWardenState.MovementSubunitsPerTick));
            Assert.That(replayIntent.Movement, Is.EqualTo(OfficeInputDirection.None));
            Assert.That(replayIntent.HasBufferedInteraction, Is.False);
        }

        [Test]
        public void TenThousandTickReplayMatchesAcrossThreeRuns()
        {
            for (int run = 0; run < 3; run++)
            {
                OfficeCaseRepository repository = OfficeCaseProjector.CreateSixCaseRepository();
                var sourceLog = new OfficeCommandLog();
                for (int i = 0; i < 96; i++)
                {
                    long tick = i + 1;
                    int sequence = i + 1;
                    OfficeCommand command = (i % 4) switch
                    {
                        0 => OfficeCommand.Move(tick, sequence, i % 3 - 1, 1),
                        1 => OfficeCommand.Interact(tick, sequence),
                        2 => OfficeCommand.Send(tick, sequence,
                            repository.Cases[i % repository.Cases.Count].AutomationClaimId),
                        _ => OfficeCommand.Decide(tick, sequence,
                            repository.Cases[i % repository.Cases.Count].AutomationClaimId),
                    };
                    Assert.That(sourceLog.TryRecord(command, out string failure), Is.True, failure);
                }

                var live = OfficeSimulationState.Create(repository);
                for (int i = 0; i < sourceLog.Commands.Count; i++)
                    Assert.That(live.TryQueueCommand(sourceLog.Commands[i],
                            out OfficeCommandFailure failure),
                        Is.True, failure == null ? string.Empty : failure.ToString());
                live.AdvanceTicks(10000);

                var replay = OfficeSimulationState.CreateReplay(repository, sourceLog);
                replay.AdvanceTicks(10000);

                Assert.That(replay.Checksum, Is.EqualTo(live.Checksum));
                Assert.That(replay.OrderedStateSnapshot, Is.EqualTo(live.OrderedStateSnapshot));
                Assert.That(replay.CurrentTick, Is.EqualTo(10000));
            }
        }

        [Test]
        public void FullM1RouteReturnsAllFoldersToFrontDesk()
        {
            var state = OfficeSimulationState.Create(OfficeCaseProjector.CreateSixCaseRepository());

            state.ForceAllFoldersThroughM1Route();

            Assert.That(state.Queues.AllFoldersAtFrontDesk(), Is.True);
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
            Assert.That(state.Queues.GetQueue(OfficeRoomId.FrontDesk).Count, Is.EqualTo(6));
        }

        private static OfficeCase CreateCase(string claimId, string sourceId, string issueId)
        {
            return new OfficeCase(
                claimId,
                sourceId,
                issueId,
                "DISPLAY-" + claimId,
                "Issue " + claimId,
                OfficeCaseUrgency.Routine,
                new OfficeCaseSchedule(0, 100),
                new[] { "public evidence" },
                string.Empty,
                string.Empty);
        }

        private static OfficeSimulationState SimulateHeldInput(
            int renderFramesPerSecond,
            int seconds)
        {
            OfficeSimulationState state = OfficeSimulationState.Create(
                OfficeCaseProjector.CreateSixCaseRepository());
            var intent = new OfficeInputIntent();
            var generator = new OfficeInputCommandGenerator(state, intent);
            var clock = new OfficeSimulationClock();
            OfficeInputDirection heldRight = OfficeInputCanonicalizer.FromDigital(
                false, true, false, false);

            int frameCount = renderFramesPerSecond * seconds;
            double frameDuration = 1d / renderFramesPerSecond;
            for (int frame = 0; frame < frameCount; frame++)
            {
                intent.SetMovement(heldRight);
                clock.Advance(frameDuration, generator.AdvanceOneTick);
            }

            int expectedTicks = OfficeSimulationClock.TicksPerSecond * seconds;
            Assert.That(clock.CurrentTick, Is.EqualTo(expectedTicks));
            Assert.That(state.CurrentTick, Is.EqualTo(expectedTicks));
            return state;
        }

        private static string SingleMoveLog(OfficeInputDirection direction)
        {
            OfficeSimulationState state = OfficeSimulationState.Create(
                OfficeCaseProjector.CreateSixCaseRepository());
            var intent = new OfficeInputIntent();
            intent.SetMovement(direction);
            var generator = new OfficeInputCommandGenerator(state, intent);
            generator.AdvanceOneTick();
            return state.CommandLog.ToJson();
        }

        private static OfficeInputDirection KeyboardDirection(OfficeInputDirection direction)
        {
            return direction switch
            {
                OfficeInputDirection.Left =>
                    OfficeInputCanonicalizer.FromDigital(true, false, false, false),
                OfficeInputDirection.Right =>
                    OfficeInputCanonicalizer.FromDigital(false, true, false, false),
                OfficeInputDirection.Down =>
                    OfficeInputCanonicalizer.FromDigital(false, false, true, false),
                OfficeInputDirection.Up =>
                    OfficeInputCanonicalizer.FromDigital(false, false, false, true),
                _ => OfficeInputDirection.None,
            };
        }

        private static OfficeInputDirection GamepadDirection(OfficeInputDirection direction)
        {
            return direction switch
            {
                OfficeInputDirection.Left => OfficeInputCanonicalizer.FromAnalog(-1f, 0f),
                OfficeInputDirection.Right => OfficeInputCanonicalizer.FromAnalog(1f, 0f),
                OfficeInputDirection.Down => OfficeInputCanonicalizer.FromAnalog(0f, -1f),
                OfficeInputDirection.Up => OfficeInputCanonicalizer.FromAnalog(0f, 1f),
                _ => OfficeInputDirection.None,
            };
        }

        private static int CountCommands(
            OfficeSimulationState state,
            OfficeCommandKind kind)
        {
            int count = 0;
            for (int i = 0; i < state.CommandLog.Commands.Count; i++)
                if (state.CommandLog.Commands[i].Kind == kind) count++;
            return count;
        }
    }
}
