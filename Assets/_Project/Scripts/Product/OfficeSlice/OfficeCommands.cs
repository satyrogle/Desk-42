using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeCommandKind
    {
        Move,
        Interact,
        Send,
        Decide,
        Carry,
        Drop,
        StartWork,
        SubmitWorkChoice,
        CancelWork,
        Help,
        Calm,
        Fix,
        ToggleRule,
        AssignStaff,
        Restart,
        ChooseUpgrade,
        ContinueToNextShift,
        ToggleRule2,
        RemoveSupervisorStamp,
        ReassignRunner,
    }

    public sealed class OfficeCommand
    {
        public OfficeCommand(
            int schemaVersion,
            long tick,
            int sequence,
            OfficeCommandKind kind,
            string actorId,
            string targetId,
            int arg0,
            int arg1,
            string textArg)
        {
            SchemaVersion = schemaVersion;
            Tick = tick;
            Sequence = sequence;
            Kind = kind;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Arg0 = arg0;
            Arg1 = arg1;
            TextArg = textArg ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public long Tick { get; }
        public int Sequence { get; }
        public OfficeCommandKind Kind { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public int Arg0 { get; }
        public int Arg1 { get; }
        public string TextArg { get; }

        public static OfficeCommand Move(long tick, int sequence, int x, int z)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Move,
                "warden", string.Empty, x, z, string.Empty);
        }

        public static OfficeCommand Interact(
            long tick,
            int sequence,
            string targetId = "")
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Interact,
                "warden", targetId, 0, 0, string.Empty);
        }

        public static OfficeCommand Send(long tick, int sequence, string targetId)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Send,
                "warden", targetId, -1, 0, string.Empty);
        }

        public static OfficeCommand Send(
            long tick,
            int sequence,
            string targetId,
            OfficeRoomId destination)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Send,
                "warden", targetId, (int)destination, 0, destination.ToString());
        }

        public static OfficeCommand Decide(long tick, int sequence, string targetId)
        {
            return Decide(tick, sequence, targetId, OfficeDecisionChoice.RejectCase);
        }

        public static OfficeCommand Decide(
            long tick,
            int sequence,
            string targetId,
            OfficeDecisionChoice choice)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Decide,
                "warden", targetId, (int)choice, 0, choice.ToString());
        }

        public static OfficeCommand Carry(long tick, int sequence, string targetId)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Carry,
                "warden", targetId, 0, 0, string.Empty);
        }

        public static OfficeCommand Drop(long tick, int sequence)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Drop,
                "warden", string.Empty, 0, 0, string.Empty);
        }

        public static OfficeCommand StartWork(
            long tick,
            int sequence,
            string targetId,
            OfficeManualTaskKind kind)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.StartWork,
                "warden", targetId, (int)kind, 0, kind.ToString());
        }

        public static OfficeCommand SubmitWorkChoice(
            long tick,
            int sequence,
            int choice)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.SubmitWorkChoice,
                "warden", string.Empty, choice, 0, string.Empty);
        }

        public static OfficeCommand CancelWork(long tick, int sequence)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.CancelWork,
                "warden", string.Empty, 0, 0, string.Empty);
        }

        public static OfficeCommand Help(long tick, int sequence, string jobId)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Help,
                "warden", jobId, 0, 0, string.Empty);
        }

        public static OfficeCommand Calm(long tick, int sequence, string customerId)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Calm,
                "warden", customerId, 0, 0, string.Empty);
        }

        public static OfficeCommand AssignStaff(
            long tick,
            int sequence,
            string staffId,
            string targetId,
            OfficeRoomId destination)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.AssignStaff,
                "warden", staffId, (int)destination, 0, targetId);
        }

        public static OfficeCommand ToggleRule(long tick, int sequence)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.ToggleRule,
                "warden", "auto-sorter", 0, 0, string.Empty);
        }

        public static OfficeCommand Fix(long tick, int sequence)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Fix,
                "warden", string.Empty, 0, 0, string.Empty);
        }

        public static OfficeCommand Restart(long tick, int sequence)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.Restart,
                "warden", "shift", 0, 0, string.Empty);
        }

        public static OfficeCommand ChooseUpgrade(
            long tick,
            int sequence,
            OfficeUpgradeFamily family)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.ChooseUpgrade,
                "warden", "office-upgrade", (int)family, 0, family.ToString());
        }

        public static OfficeCommand ContinueToNextShift(long tick, int sequence)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.ContinueToNextShift,
                "warden", "next-shift", 0, 0, string.Empty);
        }

        public static OfficeCommand ToggleRule2(long tick, int sequence)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.ToggleRule2,
                "warden", "pay-sorter", 0, 0, string.Empty);
        }

        public static OfficeCommand RemoveSupervisorStamp(long tick, int sequence)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.RemoveSupervisorStamp,
                "warden", "supervisor-stamp", 0, 0, string.Empty);
        }

        public static OfficeCommand ReassignRunner(long tick, int sequence)
        {
            return new OfficeCommand(OfficeCommandLog.CurrentSchemaVersion,
                tick, sequence, OfficeCommandKind.ReassignRunner,
                "warden", OfficeStaffSystem.RunnerId, 0, 0, "warden");
        }
    }

    public sealed class OfficeCommandFailure
    {
        public OfficeCommandFailure(long tick, int sequence, string code, string message)
        {
            Tick = tick;
            Sequence = sequence;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public long Tick { get; }
        public int Sequence { get; }
        public string Code { get; }
        public string Message { get; }

        public override string ToString()
        {
            return Tick.ToString(CultureInfo.InvariantCulture) + ":" +
                Sequence.ToString(CultureInfo.InvariantCulture) + ":" + Code + ":" +
                Message;
        }
    }

    public sealed class OfficeCommandLog
    {
        public const int CurrentSchemaVersion = 3;

        private readonly List<OfficeCommand> _commands = new();
        private readonly HashSet<int> _sequences = new();
        private readonly IReadOnlyList<OfficeCommand> _readOnlyCommands;

        public OfficeCommandLog()
        {
            _readOnlyCommands = _commands.AsReadOnly();
        }

        public bool RecordingEnabled { get; private set; } = true;
        public IReadOnlyList<OfficeCommand> Commands => _readOnlyCommands;

        public bool TryRecord(OfficeCommand command, out string failure)
        {
            failure = string.Empty;
            if (command == null)
            {
                failure = "Command is null.";
                return false;
            }
            if (!RecordingEnabled)
            {
                failure = "Command recording is disabled.";
                return false;
            }
            if (command.SchemaVersion <= 0 ||
                command.SchemaVersion > CurrentSchemaVersion)
            {
                failure = "Unsupported command schema version " + command.SchemaVersion + ".";
                return false;
            }
            if (command.SchemaVersion < 3 && IsCampaignCommand(command.Kind))
            {
                failure = "Office command schema " + command.SchemaVersion +
                    " cannot contain M3 campaign command " + command.Kind + ".";
                return false;
            }
            if (command.Tick < 0)
            {
                failure = "Command tick cannot be negative.";
                return false;
            }
            if (command.Sequence <= 0)
            {
                failure = "Command sequence must be positive.";
                return false;
            }
            if (!_sequences.Add(command.Sequence))
            {
                failure = "Duplicate command sequence " + command.Sequence + ".";
                return false;
            }

            int insertAt = _commands.Count;
            for (int i = 0; i < _commands.Count; i++)
            {
                OfficeCommand existing = _commands[i];
                if (command.Tick < existing.Tick ||
                    (command.Tick == existing.Tick && command.Sequence < existing.Sequence))
                {
                    insertAt = i;
                    break;
                }
            }
            _commands.Insert(insertAt, command);
            return true;
        }

        private static bool IsCampaignCommand(OfficeCommandKind kind)
        {
            return kind == OfficeCommandKind.ChooseUpgrade ||
                kind == OfficeCommandKind.ContinueToNextShift ||
                kind == OfficeCommandKind.ToggleRule2 ||
                kind == OfficeCommandKind.RemoveSupervisorStamp ||
                kind == OfficeCommandKind.ReassignRunner;
        }

        public OfficeCommandLog CloneForReplay()
        {
            OfficeCommandLog clone = CloneForArchive();
            clone.RecordingEnabled = false;
            return clone;
        }

        public OfficeCommandLog CloneForArchive()
        {
            var clone = new OfficeCommandLog();
            for (int i = 0; i < _commands.Count; i++)
            {
                if (!clone.TryRecord(_commands[i], out string failure))
                    throw new InvalidOperationException(failure);
            }
            return clone;
        }

        public void SetRecordingEnabled(bool enabled)
        {
            RecordingEnabled = enabled;
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"schemaVersion\":");
            builder.Append(CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"commands\":[");
            for (int i = 0; i < _commands.Count; i++)
            {
                if (i > 0) builder.Append(',');
                OfficeCommand command = _commands[i];
                builder.Append("{\"schemaVersion\":");
                builder.Append(command.SchemaVersion.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"tick\":");
                builder.Append(command.Tick.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"sequence\":");
                builder.Append(command.Sequence.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"kind\":\"");
                builder.Append(command.Kind);
                builder.Append("\",\"actorId\":\"");
                builder.Append(Escape(command.ActorId));
                builder.Append("\",\"targetId\":\"");
                builder.Append(Escape(command.TargetId));
                builder.Append("\",\"arg0\":");
                builder.Append(command.Arg0.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"arg1\":");
                builder.Append(command.Arg1.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"textArg\":\"");
                builder.Append(Escape(command.TextArg));
                builder.Append("\"}");
            }
            builder.Append("]}");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
