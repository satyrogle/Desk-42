using NUnit.Framework;

namespace Desk42.Tests
{
    [TestFixture]
    public class ATBEdgeCaseTests
    {
        [Test, Explicit]
        public void CardSlamOnFullImpatience_ResolveEnemyFirst()
        {
            // Expected sequence:
            // 1. OnCardSlammed fires with Impatience >= Max.
            // 2. Card input cached and paused.
            // 3. ClientStateMachine forces transition (PENDING to AGITATED).
            // 4. Sanity hit applied.
            // 5. Card resolves against new state.
        }
    }
}
