using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeWardenState
    {
        public const int MovementSubunitsPerTick = 4;

        public OfficeWardenState(OfficeCell spawn)
        {
            XSubunits = spawn.X * OfficeGrid.LogicalSubunitsPerCell;
            ZSubunits = spawn.Z * OfficeGrid.LogicalSubunitsPerCell;
        }

        public int XSubunits { get; private set; }
        public int ZSubunits { get; private set; }

        public OfficeCell Cell(OfficeGrid grid)
        {
            return grid.CellForLogicalPosition(XSubunits, ZSubunits);
        }

        public bool TryMove(int x, int z, OfficeGrid grid)
        {
            x = Math.Sign(x);
            z = Math.Sign(z);
            if (x == 0 && z == 0) return false;

            // M1 diagonal input is resolved horizontally first. This keeps keyboard
            // and controller streams identical and avoids diagonal speed advantage.
            if (x != 0 && z != 0) z = 0;

            int candidateX = XSubunits + x * MovementSubunitsPerTick;
            int candidateZ = ZSubunits + z * MovementSubunitsPerTick;
            if (!grid.IsWalkable(grid.CellForLogicalPosition(candidateX, candidateZ)))
                return false;

            XSubunits = candidateX;
            ZSubunits = candidateZ;
            return true;
        }
    }

    public sealed class OfficeSimulationState
    {
        private readonly List<OfficeCommandFailure> _failures = new();
        private int _nextSequence = 1;

        private OfficeSimulationState(
            OfficeCaseRepository cases,
            OfficeCommandLog commandLog,
            bool replayMode)
        {
            Cases = cases ?? throw new ArgumentNullException(nameof(cases));
            Grid = OfficeGrid.CreateM1();
            Warden = new OfficeWardenState(Grid.SpawnCell);
            Queues = new OfficeQueueService(Cases);
            CommandLog = commandLog ?? new OfficeCommandLog();
            ReplayMode = replayMode;
        }

        public OfficeGrid Grid { get; }
        public OfficeCaseRepository Cases { get; }
        public OfficeWardenState Warden { get; }
        public OfficeQueueService Queues { get; }
        public OfficeCommandLog CommandLog { get; }
        public bool ReplayMode { get; }
        public long CurrentTick { get; private set; }
        public int AppliedCommandCount { get; private set; }
        public int DecisionStubCount { get; private set; }
        public IReadOnlyList<OfficeCommandFailure> Failures => _failures.AsReadOnly();
        public string Checksum => OfficeStateChecksum.Compute(this);
        public string OrderedStateSnapshot => OfficeStateChecksum.Snapshot(this);

        public static OfficeSimulationState Create(OfficeCaseRepository cases)
        {
            return new OfficeSimulationState(cases, new OfficeCommandLog(), false);
        }

        public static OfficeSimulationState CreateReplay(
            OfficeCaseRepository cases,
            OfficeCommandLog sourceLog)
        {
            if (sourceLog == null) throw new ArgumentNullException(nameof(sourceLog));
            return new OfficeSimulationState(cases, sourceLog.CloneForReplay(), true);
        }

        public OfficeCommand CreateMoveCommand(int x, int z)
        {
            return OfficeCommand.Move(CurrentTick + 1, _nextSequence++, x, z);
        }

        public OfficeCommand CreateInteractCommand(string targetId = "")
        {
            return OfficeCommand.Interact(CurrentTick + 1, _nextSequence++, targetId);
        }

        public OfficeCommand CreateSendCommand(string caseId)
        {
            return OfficeCommand.Send(CurrentTick + 1, _nextSequence++, caseId);
        }

        public OfficeCommand CreateDecideCommand(string caseId)
        {
            return OfficeCommand.Decide(CurrentTick + 1, _nextSequence++, caseId);
        }

        public bool TryQueueCommand(OfficeCommand command, out OfficeCommandFailure failure)
        {
            if (command == null)
            {
                failure = AddFailure(
                    CurrentTick,
                    0,
                    "MALFORMED_COMMAND",
                    "Command is null.");
                return false;
            }
            if (ReplayMode)
            {
                failure = AddFailure(
                    CurrentTick,
                    command.Sequence,
                    "LIVE_INPUT_DISABLED",
                    "Replay owns the command stream.");
                return false;
            }
            if (command.Tick <= CurrentTick)
            {
                failure = AddFailure(
                    CurrentTick,
                    command.Sequence,
                    "PAST_COMMAND",
                    "Command tick must be in the future.");
                return false;
            }
            if (!CommandLog.TryRecord(command, out string message))
            {
                failure = AddFailure(
                    CurrentTick,
                    command.Sequence,
                    "INVALID_COMMAND",
                    message);
                return false;
            }
            failure = null;
            return true;
        }

        public void AdvanceOneTick()
        {
            CurrentTick++;
            Queues.AdvanceToTick(CurrentTick);
            IReadOnlyList<OfficeCommand> commands = CommandLog.Commands;
            for (int i = 0; i < commands.Count; i++)
            {
                OfficeCommand command = commands[i];
                if (command.Tick < CurrentTick) continue;
                if (command.Tick > CurrentTick) break;
                Execute(command);
            }
        }

        public void AdvanceTicks(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++) AdvanceOneTick();
        }

        public void ForceAllFoldersThroughM1Route()
        {
            IReadOnlyList<string> folderIds = Queues.FolderIds;
            for (int folderIndex = 0; folderIndex < folderIds.Count; folderIndex++)
            {
                string caseId = folderIds[folderIndex];
                for (int stage = 0; stage < 4; stage++)
                {
                    OfficeCommand command = CreateSendCommand(caseId);
                    if (!TryQueueCommand(command, out OfficeCommandFailure failure))
                        throw new InvalidOperationException(failure.ToString());
                    AdvanceOneTick();
                    AdvanceTicks(OfficeQueueService.DefaultTransferDurationTicks);
                }
            }
        }

        private void Execute(OfficeCommand command)
        {
            AppliedCommandCount++;
            switch (command.Kind)
            {
                case OfficeCommandKind.Move:
                    Warden.TryMove(command.Arg0, command.Arg1, Grid);
                    break;
                case OfficeCommandKind.Interact:
                    ExecuteInteract(command);
                    break;
                case OfficeCommandKind.Send:
                    if (string.IsNullOrWhiteSpace(command.TargetId) ||
                        !Queues.TrySendCase(command.TargetId, CurrentTick))
                        AddFailure(CurrentTick, command.Sequence, "MISSING_TARGET",
                            "Send target does not identify a queued folder.");
                    break;
                case OfficeCommandKind.Decide:
                    DecisionStubCount++;
                    if (string.IsNullOrWhiteSpace(command.TargetId))
                        AddFailure(CurrentTick, command.Sequence, "MISSING_TARGET",
                            "Decide target is empty.");
                    else
                        AddFailure(CurrentTick, command.Sequence, "DECIDE_STUB",
                            "M1 decision receiver is intentionally deferred.");
                    break;
                default:
                    AddFailure(CurrentTick, command.Sequence, "UNKNOWN_COMMAND",
                        "Command kind is not supported.");
                    break;
            }
        }

        private void ExecuteInteract(OfficeCommand command)
        {
            OfficeCell wardenCell = Warden.Cell(Grid);
            OfficeInteractionPoint point = string.IsNullOrWhiteSpace(command.TargetId)
                ? Grid.ChooseClosestInteractionPoint(wardenCell)
                : Grid.GetInteractionPoint(command.TargetId);
            if (point == null)
            {
                AddFailure(CurrentTick, command.Sequence, "MISSING_TARGET",
                    "No stable interaction point is available.");
                return;
            }
            int distance = Math.Abs(point.Cell.X - wardenCell.X) +
                Math.Abs(point.Cell.Z - wardenCell.Z);
            if (distance > 2)
            {
                AddFailure(CurrentTick, command.Sequence, "OUT_OF_RANGE",
                    "Interaction point is outside the six-tick interaction buffer range.");
                return;
            }
            if (!Queues.TrySendFromRoom(point.Room, CurrentTick))
                AddFailure(CurrentTick, command.Sequence, "EMPTY_QUEUE",
                    "The selected room has no folder ready to send.");
        }

        private OfficeCommandFailure AddFailure(
            long tick,
            int sequence,
            string code,
            string message)
        {
            var failure = new OfficeCommandFailure(tick, sequence, code, message);
            _failures.Add(failure);
            return failure;
        }
    }

    public sealed class OfficeSimulationClock
    {
        public const int TicksPerSecond = 30;
        public const int DefaultMaximumCatchUpTicks = 4;
        public const double TickDurationSeconds = 1d / TicksPerSecond;

        private double _accumulator;

        public OfficeSimulationClock(int maximumCatchUpTicks = DefaultMaximumCatchUpTicks)
        {
            if (maximumCatchUpTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumCatchUpTicks));
            MaximumCatchUpTicks = maximumCatchUpTicks;
        }

        public int MaximumCatchUpTicks { get; }
        public long CurrentTick { get; private set; }
        public bool Paused { get; private set; }

        public int Advance(double unscaledDeltaTime, Action tick)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            if (Paused) return 0;

            _accumulator += Math.Max(0d, unscaledDeltaTime);
            int executed = 0;
            while (_accumulator >= TickDurationSeconds && executed < MaximumCatchUpTicks)
            {
                tick();
                CurrentTick++;
                _accumulator -= TickDurationSeconds;
                executed++;
            }
            if (executed == MaximumCatchUpTicks && _accumulator > TickDurationSeconds)
                _accumulator = TickDurationSeconds;
            return executed;
        }

        public bool Step(Action tick)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            tick();
            CurrentTick++;
            _accumulator = 0d;
            return true;
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (paused) _accumulator = 0d;
        }
    }

    public static class OfficeStateChecksum
    {
        public static string Compute(OfficeSimulationState state)
        {
            string snapshot = Snapshot(state);
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < snapshot.Length; i++)
            {
                hash ^= snapshot[i];
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        public static string Snapshot(OfficeSimulationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var builder = new StringBuilder();
            builder.Append("tick=").Append(state.CurrentTick);
            builder.Append("|warden=").Append(state.Warden.XSubunits).Append(',')
                .Append(state.Warden.ZSubunits);
            builder.Append("|commands=").Append(state.CommandLog.Commands.Count)
                .Append('|').Append(state.AppliedCommandCount);
            foreach (OfficeRoomId room in Enum.GetValues(typeof(OfficeRoomId)))
            {
                builder.Append("|queue=").Append(room).Append(':');
                IReadOnlyList<string> ids = state.Queues.GetQueue(room).CaseIds;
                for (int i = 0; i < ids.Count; i++)
                {
                    if (i > 0) builder.Append(',');
                    builder.Append(ids[i]);
                }
            }
            IReadOnlyList<string> folderIds = state.Queues.FolderIds;
            for (int i = 0; i < folderIds.Count; i++)
            {
                OfficeFolderState folder = state.Queues.GetFolder(folderIds[i]);
                builder.Append("|folder=").Append(folder.CaseId).Append(':')
                    .Append(folder.CurrentRoom).Append(':').Append(folder.IsMoving)
                    .Append(':').Append(folder.SourceRoom).Append(':')
                    .Append(folder.DestinationRoom).Append(':')
                    .Append(folder.TransferStartTick).Append(':')
                    .Append(folder.TransferEndTick);
            }
            for (int i = 0; i < state.Failures.Count; i++)
                builder.Append("|failure=").Append(state.Failures[i]);
            return builder.ToString();
        }
    }
}
