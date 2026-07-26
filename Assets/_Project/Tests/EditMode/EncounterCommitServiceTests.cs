using Desk42.Core;
using Desk42.Encounter;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Locks the parts of the handoff §3.3 transaction that are testable
    /// without a live GameManager: encounter identity, presentation
    /// idempotency, and the baseline capture.
    ///
    /// CommitEncounterResult itself requires a RunStateController
    /// MonoBehaviour and is exercised in PlayMode.
    /// </summary>
    public sealed class EncounterCommitServiceTests
    {
        private RunData          _run;
        private MetaProgressData _meta;

        [SetUp]
        public void SetUp()
        {
            _run  = new RunData { SeedCode = "SEED01", ShiftNumber = 1 };
            _meta = new MetaProgressData();
        }

        private static ActiveClaimData Claim(string claimId = "CLM-42424",
            string variant = "moth_accountant_412", string appearanceKey = null)
            => new()
            {
                ClaimId               = claimId,
                ClientVariantId       = variant,
                ClientSpeciesId       = "moth_accountant",
                AuthoredAppearanceKey = appearanceKey,
            };

        // ── Encounter identity (§3.2) ────────────────────────

        [Test]
        public void EnsureEncounterId_AssignsOnce_AndIsStable()
        {
            var claim = Claim();

            string first  = EncounterCommitService.EnsureEncounterId(claim, _run);
            string second = EncounterCommitService.EnsureEncounterId(claim, _run);

            Assert.IsNotNull(first);
            Assert.AreEqual(first, second, "EncounterId must not be regenerated.");
            Assert.AreEqual(first, claim.EncounterId, "Id must persist on the claim.");
        }

        [Test]
        public void EnsureEncounterId_IsUniquePerClaim_EvenWhenClaimIdCollides()
        {
            // ClaimId is a seeded 5-digit number with no uniqueness check,
            // so collisions are possible and must not collapse identity.
            var a = Claim("CLM-11111");
            var b = Claim("CLM-11111");

            string idA = EncounterCommitService.EnsureEncounterId(a, _run);
            string idB = EncounterCommitService.EnsureEncounterId(b, _run);

            Assert.AreNotEqual(idA, idB);
        }

        [Test]
        public void EnsureEncounterId_SequencePersistsOnRunData()
        {
            EncounterCommitService.EnsureEncounterId(Claim("CLM-1"), _run);
            EncounterCommitService.EnsureEncounterId(Claim("CLM-2"), _run);

            Assert.AreEqual(2, _run.EncounterSequence,
                "Sequence must be serialized so ids stay unique across resume.");
        }

        [Test]
        public void EnsureEncounterId_SurvivesNullRunData()
        {
            var claim = Claim();
            string id = EncounterCommitService.EnsureEncounterId(claim, null);

            Assert.IsNotNull(id);
            Assert.IsTrue(id.Contains("NOSEED"));
        }

        [Test]
        public void EnsureEncounterId_ReturnsNullForNullClaim()
            => Assert.IsNull(EncounterCommitService.EnsureEncounterId(null, _run));

        // ── Presentation + baseline (§3.1, §3.4) ─────────────

        [Test]
        public void BeginEncounter_RecordsPresentation_ButNoVisit()
        {
            var claim = Claim();

            var baseline = EncounterCommitService.BeginEncounter(claim, _run, _meta);

            Assert.IsTrue(baseline.IsValid);
            Assert.AreEqual(1, _meta.GetTotalPresentations(claim.ClientVariantId));
            Assert.AreEqual(0, _meta.GetTotalVisits(claim.ClientVariantId),
                "Presenting a claimant must never complete a visit.");
        }

        [Test]
        public void BeginEncounter_IsIdempotent_NoPhantomOnReload()
        {
            var claim = Claim();

            EncounterCommitService.BeginEncounter(claim, _run, _meta);
            // Simulates ShiftManager re-publishing ClaimQueuedEvent on resume.
            EncounterCommitService.BeginEncounter(claim, _run, _meta);
            EncounterCommitService.BeginEncounter(claim, _run, _meta);

            Assert.AreEqual(1, _meta.GetTotalPresentations(claim.ClientVariantId),
                "Mid-encounter resume must not create a phantom presentation.");
        }

        [Test]
        public void Baseline_ReportsPriorVisitsFromHistory()
        {
            var first = Claim("CLM-1");
            EncounterCommitService.BeginEncounter(first, _run, _meta);
            _meta.Encounters.MarkCompleted(first.EncounterId,
                ClaimResolutionKind.Approve, 1L);

            _run.ShiftNumber = 2;
            var second = Claim("CLM-2");
            var baseline = EncounterCommitService.BeginEncounter(second, _run, _meta);

            Assert.AreEqual(1, baseline.PriorVisits);
            Assert.AreEqual(2, baseline.CurrentVisitNumber);
            Assert.AreEqual(1, baseline.PriorPresentations,
                "Presentations before this one, excluding this one.");
        }

        [Test]
        public void Baseline_IsImmutableSnapshot_UnaffectedByLaterCommits()
        {
            var claim = Claim();
            var baseline = EncounterCommitService.BeginEncounter(claim, _run, _meta);

            int capturedPriorVisits = baseline.PriorVisits;

            // The encounter completes — history changes underneath.
            _meta.Encounters.MarkCompleted(claim.EncounterId,
                ClaimResolutionKind.Approve, 1L);

            Assert.AreEqual(capturedPriorVisits, baseline.PriorVisits,
                "Baseline describes entry state and must not drift.");
            Assert.AreEqual(0, baseline.PriorVisits);
            Assert.AreEqual(1, _meta.GetTotalVisits(claim.ClientVariantId),
                "Live history DOES advance — only the baseline is frozen.");
        }

        [Test]
        public void Baseline_FlagsAnAlreadyCommittedEncounter()
        {
            var claim = Claim();
            EncounterCommitService.BeginEncounter(claim, _run, _meta);
            _meta.Encounters.MarkCompleted(claim.EncounterId,
                ClaimResolutionKind.Approve, 1L);

            var replay = EncounterCommitService.BeginEncounter(claim, _run, _meta);

            Assert.IsTrue(replay.AlreadyCommitted,
                "A re-presented committed encounter must be detectable.");
        }

        [Test]
        public void BeginEncounter_ReturnsNoneWithoutMeta()
        {
            var baseline = EncounterCommitService.BeginEncounter(Claim(), _run, null);
            Assert.IsFalse(baseline.IsValid);
        }

        // ── §3.5 no second serialized count ──────────────────

        [Test]
        public void LegacyTotalVisitsField_IsNotUsedForDerivation()
        {
            var claim = Claim();
            EncounterCommitService.BeginEncounter(claim, _run, _meta);
            _meta.Encounters.MarkCompleted(claim.EncounterId,
                ClaimResolutionKind.Approve, 1L);

            // Poison the legacy counter; derivation must ignore it entirely.
            _meta.GetOrCreateProfile(claim.ClientVariantId).TotalVisits = 99;

            Assert.AreEqual(1, _meta.GetTotalVisits(claim.ClientVariantId),
                "Visits derive from encounter history, never from the legacy field.");
        }
    }
}
