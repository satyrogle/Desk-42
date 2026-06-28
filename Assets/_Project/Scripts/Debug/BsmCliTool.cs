// ============================================================
// DESK 42 — BSM CLI Tool
//
// desk42 bsm set-state <STATE_ENUM>     — force ClientStateMachine.ForceState
// desk42 bsm set-impatience <float>     — pin Impatience for ATB testing
// desk42 bsm clear-impatience           — release the pin, back to live value
//
// Talks to ClientStateMachine directly. Registered with Desk42CLI
// by Desk42CliBootstrap.
// ============================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Globalization;
using Desk42.BSM;
using Desk42.Core;
using Desk42.Encounter;

namespace Desk42.Debugging
{
    public static class BsmCliTool
    {
        public static string Run(string[] args)
        {
            if (args.Length == 0)
                return "Usage: bsm set-state <STATE> | set-impatience <float> | clear-impatience";

            var client = Desk42Services.Get<EncounterManager>()?.ActiveClient;
            if (client == null)
                return "No active client encounter.";

            switch (args[0].ToLowerInvariant())
            {
                case "set-state":
                    if (args.Length < 2)
                        return "Usage: bsm set-state <STATE>";
                    if (!Enum.TryParse<ClientStateID>(args[1], true, out var state))
                        return $"Unknown state '{args[1]}'. Valid: {string.Join(", ", Enum.GetNames(typeof(ClientStateID)))}";
                    client.ForceState(state);
                    return $"Forced state -> {state}";

                case "set-impatience":
                    if (args.Length < 2 ||
                        !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        return "Usage: bsm set-impatience <float>";
                    client.ImpatienceOverride = value;
                    return $"Impatience pinned -> {value} (Max: {ClientStateMachine.MaxImpatience})";

                case "clear-impatience":
                    client.ImpatienceOverride = null;
                    return "Impatience pin cleared, using live value.";

                default:
                    return $"Unknown bsm subcommand '{args[0]}'.";
            }
        }
    }
}

#endif
