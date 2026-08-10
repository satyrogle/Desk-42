using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            return new OfficeCommand(1, tick, sequence, OfficeCommandKind.Move,
                "warden", string.Empty, x, z, string.Empty);
        }

        public static OfficeCommand Interact(
            long tick,
            int sequence,
            string targetId = "")
        {
            return new OfficeCommand(1, tick, sequence, OfficeCommandKind.Interact,
                "warden", targetId, 0, 0, string.Empty);
        }

        public static OfficeCommand Send(long tick, int sequence, string targetId)
        {
            return new OfficeCommand(1, tick, sequence, OfficeCommandKind.Send,
                "warden", targetId, 0, 0, string.Empty);
        }

        public static OfficeCommand Decide(long tick, int sequence, string targetId)
        {
            return new OfficeCommand(1, tick, sequence, OfficeCommandKind.Decide,
                "warden", targetId, 0, 0, string.Empty);
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
        public const int CurrentSchemaVersion = 1;

        private readonly List<OfficeCommand> _commands = new();
        private readonly HashSet<int> _sequences = new();

        public bool RecordingEnabled { get; private set; } = true;
        public IReadOnlyList<OfficeCommand> Commands =>
            new ReadOnlyCollection<OfficeCommand>(_commands);

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

        public OfficeCommandLog CloneForReplay()
        {
            var clone = new OfficeCommandLog();
            for (int i = 0; i < _commands.Count; i++)
            {
                if (!clone.TryRecord(_commands[i], out string failure))
                    throw new InvalidOperationException(failure);
            }
            clone.RecordingEnabled = false;
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
