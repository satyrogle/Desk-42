using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeM3
{
    public sealed class OfficeM3GateDTests
    {
        [Test]
        public void CampaignResultContainsObservableFactsAndNextDayTease()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();

            OfficeM3TestDriver.DriveCampaignToResult(campaign);

            OfficeCampaignResult result = campaign.Result;
            Assert.That(result.CustomersHelped, Is.EqualTo(18));
            Assert.That(result.CustomersRejected, Is.Zero);
            Assert.That(result.RulesTaught, Is.EqualTo(2));
            Assert.That(result.RuleMatches, Is.GreaterThanOrEqualTo(3));
            Assert.That(result.CopiesCleared, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.OfficeFailuresRecovered,
                Is.GreaterThanOrEqualTo(4));
            Assert.That(result.UpgradesChosen, Is.EqualTo(2));
            Assert.That(result.AverageWaitTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.MisroutedFiles, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.KnownCustomerFollowUps, Is.EqualTo(2));
            Assert.That(OfficeCampaignResult.NextDayTease, Is.EqualTo(
                "TOMORROW'S FIRST CASE: " +
                "THE COMPLAINT BOX HAS FILED A COMPLAINT."));
        }

        [Test]
        public void ThreeShiftWhatHappenedIncludesEveryShift()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();

            OfficeM3TestDriver.DriveCampaignToResult(campaign);

            Assert.That(campaign.CompletedShiftSummaries,
                Has.Count.EqualTo(3));
            for (int shift = 0; shift < 3; shift++)
            {
                Assert.That(campaign.CompletedShiftSummaries[shift].ShiftOrdinal,
                    Is.EqualTo(shift + 1));
                Assert.That(campaign.CompletedShiftSummaries[shift]
                    .ObservableRecapLines, Is.Not.Empty);
            }
            Assert.That(campaign.Result.ObservableRecapLines.Count,
                Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void ThreeShiftReplayProducesIdenticalChecksum()
        {
            OfficeCampaignState live = OfficeCampaignState.Create();
            OfficeM3TestDriver.DriveCampaignToResult(live);
            OfficeCampaignReplayTape tape = live.CreateReplayTape();

            OfficeCampaignState replay =
                OfficeCampaignReplayRunner.ReplayToResult(tape);

            TestContext.WriteLine("M3_CAMPAIGN_CHECKSUM=" + live.Checksum);
            string difference = FirstDifference(
                live.OrderedStateSnapshot, replay.OrderedStateSnapshot);
            Assert.That(replay.OrderedStateSnapshot,
                Is.EqualTo(live.OrderedStateSnapshot), difference);
            Assert.That(replay.Checksum, Is.EqualTo(live.Checksum));
            Assert.That(replay.Result.CustomersHelped,
                Is.EqualTo(live.Result.CustomersHelped));
            Assert.That(replay.CompletedShiftSummaries,
                Has.Count.EqualTo(3));
        }

        [TestCase(1, "opening")]
        [TestCase(1, "rush")]
        [TestCase(2, "rush")]
        [TestCase(2, "result")]
        [TestCase(3, "promotion-cascade")]
        [TestCase(3, "result")]
        public void CampaignCaptureDriverReachesNamedState(
            int shiftOrdinal,
            string stateName)
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();

            OfficeCampaignCaptureDriver.Prepare(
                campaign,
                shiftOrdinal,
                stateName);

            Assert.That(campaign.CurrentShiftOrdinal,
                Is.EqualTo(shiftOrdinal));
            if (shiftOrdinal == 1 && stateName == "opening")
                Assert.That(campaign.CurrentSimulation.CurrentTick, Is.Zero);
            else if (shiftOrdinal == 1)
                Assert.That(campaign.CurrentSimulation.BreakState.Active, Is.True);
            else if (shiftOrdinal == 2 && stateName == "rush")
                Assert.That(campaign.CurrentSimulation.GhostClock.Active, Is.True);
            else if (shiftOrdinal == 2)
                Assert.That(campaign.Phase,
                    Is.EqualTo(OfficeCampaignPhase.ChooseUpgrade));
            else if (stateName == "promotion-cascade")
                Assert.That(campaign.CurrentSimulation.PromotionCascade.Active,
                    Is.True);
            else
                Assert.That(campaign.IsComplete, Is.True);
        }

        [Test]
        public void CampaignReplayLocksOutLiveInput()
        {
            OfficeCampaignState live = OfficeCampaignState.Create();
            OfficeM3TestDriver.DriveCampaignToResult(live);
            OfficeCampaignState replay = OfficeCampaignState.CreateReplay(
                live.CreateReplayTape());
            OfficeSimulationState state = replay.CurrentSimulation;
            int commandCount = state.CommandLog.Commands.Count;
            var intent = new OfficeInputIntent();
            intent.SetMovement(OfficeInputDirection.Left);
            intent.BufferInteraction(state.CurrentTick);
            var input = new OfficeInputCommandGenerator(state, intent);

            input.AdvanceOneTick();

            Assert.That(state.ReplayMode, Is.True);
            Assert.That(state.CommandLog.Commands.Count, Is.EqualTo(commandCount));
            Assert.That(intent.Movement, Is.EqualTo(OfficeInputDirection.None));
            Assert.That(intent.HasBufferedInteraction, Is.False);
        }

        private static string FirstDifference(string expected, string actual)
        {
            int length = System.Math.Min(expected.Length, actual.Length);
            int index = 0;
            while (index < length && expected[index] == actual[index]) index++;
            int start = System.Math.Max(0, index - 120);
            int expectedLength = System.Math.Min(300, expected.Length - start);
            int actualLength = System.Math.Min(300, actual.Length - start);
            return "FIRST DIFFERENCE AT " + index +
                "\nLIVE=" + expected.Substring(start, expectedLength) +
                "\nREPLAY=" + actual.Substring(start, actualLength);
        }
    }
}
