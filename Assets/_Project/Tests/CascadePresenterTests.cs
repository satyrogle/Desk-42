using NUnit.Framework;

namespace Desk42.Tests
{
    public class CascadePresenterTests
    {
        // [TODO: wire to CascadePresenter once Claude reviews it]

        [Test]
        public void TimingTest_EachModifierYieldsBaseDelay()
        {
            // Timing test: each modifier step yields BaseDelay by default.
        }

        [Test]
        public void FastForwardTest_AutoFastForwardCountYieldsSkipDelay()
        {
            // Fast-forward test: SeenComboCount >= AutoFastForwardCount causes the step to yield SkipDelay.
        }

        [Test]
        public void ManualSkipTest_HoldToFastForwardRoutesToSkipDelay()
        {
            // Manual skip test: hold-to-fast-forward routes to SkipDelay path.
        }

        [Test]
        public void StepCountTest_PlaySequenceRunsExactlyAsManyStepsAsModifiers()
        {
            // Step count test: PlaySequence runs exactly as many steps as the packet has modifiers.
        }

        [Test]
        public void StampTierTest_SanityMapsToCorrectTier()
        {
            // Stamp tier test: Sanity 90 -> Tier 1, Sanity 60 -> Tier 2, Sanity 35 -> Tier 3, Sanity 10 -> Tier 4.
        }
    }
}
