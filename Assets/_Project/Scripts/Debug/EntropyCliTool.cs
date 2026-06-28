// ============================================================
// DESK 42 — Entropy CLI Tool
//
// desk42 entropy pin --sanity <int>   — pin EntropyManager.SanityOverride
// desk42 entropy unpin                — clear the pin, back to live value
// ============================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using Desk42.UI;

namespace Desk42.Debugging
{
    public static class EntropyCliTool
    {
        public static string Run(string[] args)
        {
            if (args.Length == 0)
                return "Usage: entropy pin --sanity <int> | unpin";

            switch (args[0].ToLowerInvariant())
            {
                case "pin":
                    if (args.Length < 3 || args[1] != "--sanity" || !int.TryParse(args[2], out var sanity))
                        return "Usage: entropy pin --sanity <int>";
                    EntropyManager.SanityOverride = sanity;
                    return $"Sanity pinned -> {sanity}";

                case "unpin":
                    EntropyManager.SanityOverride = null;
                    return "Sanity pin cleared, using live value.";

                default:
                    return $"Unknown entropy subcommand '{args[0]}'.";
            }
        }
    }
}

#endif
