using NUnit.Framework;

namespace Desk42.Tests
{
    public class ATBEdgeCaseTests
    {
        // [TODO: wire to StateInjector.TrySlam() once Claude builds it]

        [Test]
        public void CardSlamOnFullImpatience_ResolveEnemyFirst()
        {
            /*
             * Expected sequence:
             * 1. OnCardSlammed fires with Impatience >= Max.
             * 2. Card input is cached and paused.
             * 3. ClientStateMachine forces transition (PENDING to AGITATED).
             * 4. Sanity hit applied.
             * 5. Card resolves against the new state.
             */
        }
    }
}
