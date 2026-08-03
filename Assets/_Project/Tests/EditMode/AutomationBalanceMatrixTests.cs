using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Deterministic pre-playtest tuning sweep. This is deliberately labelled a
    /// forecast: it catches dead or dominant builds before human telemetry, but it
    /// does not make a claim about player behaviour or commercial balance.
    /// </summary>
    public sealed class AutomationBalanceMatrixTests
    {
        private enum RepresentativeBuild
        {
            EvidenceFortress,
            ThroughputGamble,
            ReliefAndReview,
        }

        private enum ForecastDoctrine
        {
            ProofFortress = 1,
            RubberStampMill = 2,
            AppealRefinery = 3,
            ProvisionalWelfareOffice = 4,
        }

        private sealed class Forecast
        {
            internal int Completion;
            internal int Overdue;
            internal int Jams;
            internal int Appeals;
            internal int Holdings;
            internal int Stability;
            internal string Outcome;
        }

        [Test]
        public void FourDoctrinesThreeBuildsTenSeedsProduceInspectableTradeoffs()
        {
            var rows = new List<string>
            {
                "doctrine,build,seed,completion,overdue,jams,appeals,holdings,stability,outcome",
            };
            var outcomes = new HashSet<string>(StringComparer.Ordinal);
            var buildWins = new Dictionary<RepresentativeBuild, int>();
            foreach (RepresentativeBuild build in
                     Enum.GetValues(typeof(RepresentativeBuild)))
                buildWins.Add(build, 0);

            for (int doctrine = 1; doctrine <= 4; doctrine++)
            for (int build = 0; build < 3; build++)
            for (int seed = 1; seed <= 10; seed++)
            {
                Forecast result = Evaluate(
                    (ForecastDoctrine)doctrine,
                    (RepresentativeBuild)build,
                    seed * 7919 + doctrine * 101 + build * 17);
                outcomes.Add(result.Outcome);
                if (result.Completion >= 70 && result.Stability >= 38)
                    buildWins[(RepresentativeBuild)build]++;
                rows.Add(string.Join(",",
                    ((ForecastDoctrine)doctrine).ToString(),
                    ((RepresentativeBuild)build).ToString(),
                    seed,
                    result.Completion,
                    result.Overdue,
                    result.Jams,
                    result.Appeals,
                    result.Holdings,
                    result.Stability,
                    result.Outcome));
            }

            string directory = Path.Combine("tmp", "v0.5");
            Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(
                directory, "balance-forecast-4x3x10.csv"), rows);

            Assert.That(rows.Count, Is.EqualTo(121));
            Assert.That(outcomes.Count, Is.GreaterThanOrEqualTo(3));
            foreach (RepresentativeBuild build in
                     Enum.GetValues(typeof(RepresentativeBuild)))
                Assert.That(buildWins[build], Is.GreaterThan(0),
                    build + " has no viable forecast in the required matrix.");
        }

        private static Forecast Evaluate(
            ForecastDoctrine doctrine,
            RepresentativeBuild build,
            int seed)
        {
            int capacity = doctrine switch
            {
                ForecastDoctrine.ProofFortress => 77,
                ForecastDoctrine.RubberStampMill => 101,
                ForecastDoctrine.AppealRefinery => 83,
                ForecastDoctrine.ProvisionalWelfareOffice => 88,
                _ => 80,
            };
            int reliability = doctrine switch
            {
                ForecastDoctrine.ProofFortress => 88,
                ForecastDoctrine.RubberStampMill => 58,
                ForecastDoctrine.AppealRefinery => 76,
                ForecastDoctrine.ProvisionalWelfareOffice => 72,
                _ => 70,
            };
            int stability = doctrine switch
            {
                ForecastDoctrine.ProofFortress => 64,
                ForecastDoctrine.RubberStampMill => 38,
                ForecastDoctrine.AppealRefinery => 50,
                ForecastDoctrine.ProvisionalWelfareOffice => 72,
                _ => 50,
            };
            int appealBias = doctrine switch
            {
                ForecastDoctrine.ProofFortress => 7,
                ForecastDoctrine.RubberStampMill => 25,
                ForecastDoctrine.AppealRefinery => 19,
                ForecastDoctrine.ProvisionalWelfareOffice => 17,
                _ => 12,
            };

            switch (build)
            {
                case RepresentativeBuild.EvidenceFortress:
                    capacity -= 13;
                    reliability += 17;
                    appealBias -= 5;
                    stability += 7;
                    break;
                case RepresentativeBuild.ThroughputGamble:
                    capacity += 18;
                    reliability -= 18;
                    appealBias += 8;
                    stability -= 8;
                    break;
                case RepresentativeBuild.ReliefAndReview:
                    capacity -= 5;
                    reliability += 4;
                    appealBias += 4;
                    stability += doctrine ==
                        ForecastDoctrine.ProvisionalWelfareOffice ? 15 : 9;
                    break;
            }

            int noise = Stable(seed, 1, 13) - 6;
            int completion = Math.Max(48, Math.Min(96, capacity + noise));
            int overdue = Math.Max(0, 96 - completion + Stable(seed, 2, 8));
            int jams = Math.Max(0,
                (100 - reliability) / 8 + Stable(seed, 3, 5));
            int appeals = Math.Max(0,
                appealBias + Stable(seed, 4, 9) - 4);
            int holdings = doctrine == ForecastDoctrine.AppealRefinery
                ? Math.Max(1, appeals / 4)
                : Math.Max(0, appeals / 7);
            stability = Math.Max(0, Math.Min(100,
                stability - overdue / 3 - jams + Stable(seed, 5, 7) - 3));

            string outcome;
            if (completion >= 84 && stability < 44)
                outcome = "EfficientButHarmful";
            else if (stability >= 72 && completion < 76)
                outcome = "HumaneButInsolvent";
            else if (holdings >= 6 && appeals >= 24)
                outcome = "PrecedentPressure";
            else if (completion >= 72 && stability >= 45)
                outcome = "Certified";
            else
                outcome = "AdministrativeBlindness";

            return new Forecast
            {
                Completion = completion,
                Overdue = overdue,
                Jams = jams,
                Appeals = appeals,
                Holdings = holdings,
                Stability = stability,
                Outcome = outcome,
            };
        }

        private static int Stable(int seed, int salt, int range)
        {
            unchecked
            {
                uint value = (uint)(seed * 1103515245 + salt * 12345);
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                return (int)(value % (uint)Math.Max(1, range));
            }
        }
    }
}
