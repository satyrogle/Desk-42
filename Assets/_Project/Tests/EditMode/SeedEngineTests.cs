// ============================================================
// DESK 42 — SeedEngine Unit Tests (Edit Mode)
// ============================================================

using NUnit.Framework;
using Desk42.Core;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class SeedEngineTests
    {
        [SetUp]
        public void SetUp() => SeedEngine.Init(12345);

        // ── Determinism ───────────────────────────────────────

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            SeedEngine.Init(99999);
            int a1 = SeedEngine.Next(SeedStream.ClaimQueue, 100);
            float b1 = SeedEngine.NextFloat(SeedStream.CardDraft);

            SeedEngine.Init(99999);
            int a2 = SeedEngine.Next(SeedStream.ClaimQueue, 100);
            float b2 = SeedEngine.NextFloat(SeedStream.CardDraft);

            Assert.AreEqual(a1, a2, "Same seed must produce same int sequence.");
            Assert.AreEqual(b1, b2, "Same seed must produce same float sequence.");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            SeedEngine.Init(1);
            int a = SeedEngine.Next(SeedStream.ClaimQueue, 10000);

            SeedEngine.Init(2);
            int b = SeedEngine.Next(SeedStream.ClaimQueue, 10000);

            Assert.AreNotEqual(a, b, "Different seeds should (almost always) diverge.");
        }

        // ── Stream Independence ───────────────────────────────

        [Test]
        public void Streams_AreIndependent()
        {
            SeedEngine.Init(42);
            // Drain the ClaimQueue stream heavily
            for (int i = 0; i < 100; i++)
                SeedEngine.Next(SeedStream.ClaimQueue, 1000);

            float after = SeedEngine.NextFloat(SeedStream.CardDraft);

            SeedEngine.Init(42);
            // Don't touch ClaimQueue stream this time
            float fresh = SeedEngine.NextFloat(SeedStream.CardDraft);

            Assert.AreEqual(after, fresh,
                "Draining one stream must not affect another stream.");
        }

        // ── Range Validation ──────────────────────────────────

        [Test]
        public void Next_RespectsRange()
        {
            SeedEngine.Init(0);
            for (int i = 0; i < 1000; i++)
            {
                int v = SeedEngine.Next(SeedStream.ClaimQueue, 5, 15);
                Assert.IsTrue(v >= 5 && v < 15,
                    $"Next({v}) out of range [5, 15)");
            }
        }

        [Test]
        public void NextFloat_RespectsRange()
        {
            SeedEngine.Init(0);
            for (int i = 0; i < 1000; i++)
            {
                float v = SeedEngine.NextFloat(SeedStream.ShopInventory, 0.1f, 0.9f);
                Assert.IsTrue(v >= 0.1f && v < 0.9f,
                    $"NextFloat({v}) out of range [0.1, 0.9)");
            }
        }

        // ── NextBool ──────────────────────────────────────────

        [Test]
        public void NextBool_ZeroProbability_AlwaysFalse()
        {
            SeedEngine.Init(0);
            for (int i = 0; i < 100; i++)
                Assert.IsFalse(SeedEngine.NextBool(SeedStream.MutationGeneration, 0f));
        }

        [Test]
        public void NextBool_OneProbability_AlwaysTrue()
        {
            SeedEngine.Init(0);
            for (int i = 0; i < 100; i++)
                Assert.IsTrue(SeedEngine.NextBool(SeedStream.MutationGeneration, 1f));
        }

        // ── Share Codes ───────────────────────────────────────

        [Test]
        public void ShareCode_RoundTrips()
        {
            SeedEngine.Init(987654321);
            string code = SeedEngine.CurrentSeedCode;

            Assert.AreEqual(6, code.Length, "Share code must be 6 characters.");

            bool ok = SeedEngine.TryParseSeedCode(code, out int parsed);
            Assert.IsTrue(ok, "Parsing own share code must succeed.");

            SeedEngine.Init(parsed);
            Assert.AreEqual(code, SeedEngine.CurrentSeedCode,
                "Re-initialising with parsed seed must reproduce the same code.");
        }

        [Test]
        public void ShareCode_InvalidInput_ReturnsFalse()
        {
            Assert.IsFalse(SeedEngine.TryParseSeedCode("", out _));
            Assert.IsFalse(SeedEngine.TryParseSeedCode("ABC", out _));   // too short
            Assert.IsFalse(SeedEngine.TryParseSeedCode("AAAAAAA", out _)); // too long
            Assert.IsFalse(SeedEngine.TryParseSeedCode("A1B!C2", out _)); // invalid char
        }

        // ── Shuffle ───────────────────────────────────────────

        [Test]
        public void Shuffle_ProducesFullPermutation()
        {
            SeedEngine.Init(777);
            var list = new System.Collections.Generic.List<int> { 1, 2, 3, 4, 5 };
            SeedEngine.Shuffle(SeedStream.ClaimQueue, list);

            Assert.AreEqual(5, list.Count);
            for (int i = 1; i <= 5; i++)
                Assert.Contains(i, list, $"Element {i} missing after shuffle.");
        }

        [Test]
        public void Shuffle_SameSeed_ProducesSameOrder()
        {
            var list1 = new System.Collections.Generic.List<int> { 1, 2, 3, 4, 5 };
            var list2 = new System.Collections.Generic.List<int> { 1, 2, 3, 4, 5 };

            SeedEngine.Init(42);
            SeedEngine.Shuffle(SeedStream.ClaimQueue, list1);

            SeedEngine.Init(42);
            SeedEngine.Shuffle(SeedStream.ClaimQueue, list2);

            CollectionAssert.AreEqual(list1, list2,
                "Same seed must produce same shuffle order.");
        }
    }
}
