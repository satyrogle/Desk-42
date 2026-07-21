using NUnit.Framework;

namespace Desk42.Tests
{
    [TestFixture]
    public class CascadePresenterTests
    {
        [Test, Explicit]
        public void StepYieldsBaseDelay_ByDefault() {}

        [Test, Explicit]
        public void StepYieldsSkipDelay_WhenSeenCountExceedsThreshold() {}

        [Test, Explicit]
        public void StepYieldsSkipDelay_WhenManualFastForwardHeld() {}

        [Test, Explicit]
        public void StepCount_MatchesPacketModifierCount() {}

        [Test, Explicit]
        public void StampTier_MapsCorrectlyFromSanity() {}
    }
}
