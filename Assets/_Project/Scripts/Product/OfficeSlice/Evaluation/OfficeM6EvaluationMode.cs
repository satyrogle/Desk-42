using System;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeM6EvaluationMode
    {
        public const string LaunchArgument = "--desk42-evaluation";
        public const string BuildIdentifier =
            "Desk42 Office Slice M6 Evaluation Candidate";

        public OfficeM6EvaluationMode(string[] arguments)
        {
            Enabled = HasArgument(arguments, LaunchArgument);
        }

        public bool Enabled { get; }
        public bool TelemetryEnabled => Enabled;
        public bool DeveloperShortcutsAllowed => !Enabled;
        public bool ForceFreshOnboarding => Enabled;
        public int StartingShiftOrdinal => 1;
        public bool UsesPlayerHud => true;
        public bool UsesM4VisualTarget => true;
        public bool UsesM5AudioFeedback => true;

        private static bool HasArgument(string[] arguments, string expected)
        {
            if (arguments == null) return false;
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], expected,
                        StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
