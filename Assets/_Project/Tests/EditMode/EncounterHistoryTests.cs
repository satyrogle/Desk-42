using Desk42.Core;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Locks the handoff §3.1 / §3.2 / §3.5 contract on the authoritative
    /// encounter ledger. These are the rules a phantom visit would break.
    /// </summary>
    public sealed class EncounterHistoryTests
    {
        private const string Variant = "moth_accountant_412";
        private const string Enc1    = "ENC-SEED01-S1-001";
        private const string Enc2    = "ENC-SEED01-S2-002";

        private EncounterHistory _history;

        [SetUp]
        public void SetUp() => _history = new EncounterHistory();

        private void Present(string encounterId, string variant = Variant, int shift = 1)
            => _history.BeginPresentation(encounterId, "CLM-11111", variant,
                "moth_accountant", null, shift);

        // ── §3.1 presentation is not a visit ─────────────────

        [Test]
        public void Presentation_DoesNotCountAsVisit()
        {
            Present(Enc1);

            Assert.AreEqual(1, _history.TotalPresentations(Variant));
            Assert.AreEqual(0, _history.TotalVisits(Variant),
                "Spawning a claimant must never increment completed visits.");
        }

        [Test]
        public void CompletedEncounter_CountsAsExactlyOneVisit()
        {
            Present(Enc1);
            _history.MarkCompleted(Enc1, ClaimResolutionKind.Approve, 123L);

            Assert.AreEqual(1, _history.TotalPresentations(Variant));
            Assert.AreEqual(1, _history.TotalVisits(Variant));
        }

        [Test]
        public void InterruptedEncounter_IsPresentedButNeverVisited()
        {
            Present(Enc1);
            // no MarkCompleted — shift ended, player quit, timer expired
            Present(Enc2, shift: 2);
            _history.MarkCompleted(Enc2, ClaimResolutionKind.Deny, 456L);

            Assert.AreEqual(2, _history.TotalPresentations(Variant));
            Assert.AreEqual(1, _history.TotalVisits(Variant),
                "An abandoned encounter must not become a visit.");
        }

        // ── §3.2 idempotency ─────────────────────────────────

        [Test]
        public void RePresentingSameEncounterId_DoesNotCreatePhantom()
        {
            Present(Enc1);
            Present(Enc1);   // scene reconstruction / mid-encounter resume
            Present(Enc1);

            Assert.AreEqual(1, _history.TotalPresentations(Variant),
                "Reload must not create a phantom presentation.");
        }

        [Test]
        public void SecondCompletion_IsRejected_AndDoesNotDoubleCount()
        {
            Present(Enc1);

            Assert.IsTrue(_history.MarkCompleted(Enc1, ClaimResolutionKind.Approve, 1L));
            Assert.IsFalse(_history.MarkCompleted(Enc1, ClaimResolutionKind.Approve, 2L),
                "A completed EncounterId cannot commit twice.");

            Assert.AreEqual(1, _history.TotalVisits(Variant));
        }

        [Test]
        public void SecondCompletion_DoesNotOverwriteOriginalOutcome()
        {
            Present(Enc1);
            _history.MarkCompleted(Enc1, ClaimResolutionKind.Approve, 111L);
            _history.MarkCompleted(Enc1, ClaimResolutionKind.Deny, 222L);

            var record = _history.Find(Enc1);
            Assert.AreEqual(ClaimResolutionKind.Approve, record.Outcome);
            Assert.AreEqual(111L, record.CommittedAtUtcTicks);
        }

        [Test]
        public void MarkCompleted_OnUnknownEncounter_ReturnsFalse()
        {
            Assert.IsFalse(_history.MarkCompleted("ENC-NOPE", ClaimResolutionKind.Approve, 1L));
            Assert.AreEqual(0, _history.TotalVisits(Variant));
        }

        [Test]
        public void IsCompleted_IsTheIdempotencyPredicate()
        {
            Present(Enc1);
            Assert.IsFalse(_history.IsCompleted(Enc1));

            _history.MarkCompleted(Enc1, ClaimResolutionKind.Approve, 1L);
            Assert.IsTrue(_history.IsCompleted(Enc1));
        }

        // ── §3.5 derivation ──────────────────────────────────

        [Test]
        public void PriorVisits_ExcludesTheEncounterItself()
        {
            Present(Enc1);
            _history.MarkCompleted(Enc1, ClaimResolutionKind.Approve, 1L);
            Present(Enc2, shift: 2);

            // Stable whether asked before or after this encounter commits.
            Assert.AreEqual(1, _history.PriorVisits(Variant, Enc2));
            _history.MarkCompleted(Enc2, ClaimResolutionKind.Approve, 2L);
            Assert.AreEqual(1, _history.PriorVisits(Variant, Enc2));
        }

        [Test]
        public void PriorVisits_IsZeroForAFirstTimeClaimant()
        {
            Present(Enc1);
            Assert.AreEqual(0, _history.PriorVisits(Variant, Enc1));
        }

        [Test]
        public void CountsAreScopedPerClaimant()
        {
            Present(Enc1, "species_a_100");
            _history.MarkCompleted(Enc1, ClaimResolutionKind.Approve, 1L);
            Present(Enc2, "species_b_200");
            _history.MarkCompleted(Enc2, ClaimResolutionKind.Approve, 2L);

            Assert.AreEqual(1, _history.TotalVisits("species_a_100"));
            Assert.AreEqual(1, _history.TotalVisits("species_b_200"));
            Assert.AreEqual(0, _history.TotalVisits("species_c_300"));
        }

        [Test]
        public void UnknownClaimant_DerivesZero_NotAnException()
        {
            Assert.AreEqual(0, _history.TotalVisits(null));
            Assert.AreEqual(0, _history.TotalPresentations("never_seen"));
            Assert.AreEqual(0, _history.PriorVisits("", Enc1));
        }

        [Test]
        public void HasCompletedAppearance_TracksAuthoredKeys()
        {
            _history.BeginPresentation(Enc1, "CLM-1", "elias_venn", "human",
                "elias_shift1", 1);

            Assert.IsFalse(_history.HasCompletedAppearance("elias_shift1"));

            _history.MarkCompleted(Enc1, ClaimResolutionKind.Approve, 1L);
            Assert.IsTrue(_history.HasCompletedAppearance("elias_shift1"));
            Assert.IsFalse(_history.HasCompletedAppearance("elias_shift2"));
        }

        [Test]
        public void BeginPresentation_RequiresAnEncounterId()
        {
            Assert.Throws<System.ArgumentException>(
                () => _history.BeginPresentation(null, "CLM-1", Variant, "sp", null, 1));
            Assert.Throws<System.ArgumentException>(
                () => _history.BeginPresentation("  ", "CLM-1", Variant, "sp", null, 1));
        }
    }
}
