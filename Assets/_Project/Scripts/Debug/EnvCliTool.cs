#if UNITY_EDITOR || DEVELOPMENT_BUILD

using Desk42.Core;

namespace Desk42.Debugging
{
    public static class EnvCliTool
    {
        public static string Run(string[] args)
        {
            if (args.Length == 0)
                return "Usage: env set-temp <float> | set-noise <float> | dump";

            switch (args[0].ToLowerInvariant())
            {
                case "set-temp":
                    if (args.Length < 2 || !float.TryParse(args[1], out var temp))
                        return "Usage: env set-temp <float>";
                    OfficeEnvironmentState.ModifyTemperature(temp - OfficeEnvironmentState.Temperature);
                    return $"Temperature -> {OfficeEnvironmentState.Temperature:F1}";

                case "set-noise":
                    if (args.Length < 2 || !float.TryParse(args[1], out var noise))
                        return "Usage: env set-noise <float>";
                    OfficeEnvironmentState.ModifyNoise(noise - OfficeEnvironmentState.NoiseLevel);
                    return $"NoiseLevel -> {OfficeEnvironmentState.NoiseLevel:F1}";

                case "dump":
                    return $"Temperature: {OfficeEnvironmentState.Temperature:F1} " +
                           $"({OfficeEnvironmentState.GetTemperatureState()})\n" +
                           $"NoiseLevel:  {OfficeEnvironmentState.NoiseLevel:F1}\n" +
                           $"TellMult:    {OfficeEnvironmentState.GetTellLeadTimeMultiplier():F2}\n" +
                           $"DurMult:     {OfficeEnvironmentState.GetInjectionDurationMultiplier():F1}";

                default:
                    return $"Unknown env subcommand '{args[0]}'.";
            }
        }
    }
}

#endif
