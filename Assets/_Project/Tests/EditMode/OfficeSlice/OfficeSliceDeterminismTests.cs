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
    }
}
