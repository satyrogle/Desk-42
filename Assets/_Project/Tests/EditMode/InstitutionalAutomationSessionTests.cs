using System;
using System.Collections.Generic;
using Desk42.Institutional.Player;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalAutomationSessionTests
    {
        [Test]
        public void BatchProjectsDistinctPublicEnvelopesWithoutTruthAccess()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(5);

            Assert.AreEqual(5, session.Claims.Count);
            for (int index = 0; index < session.Claims.Count; index++)
            {
                AutomationPublicClaim claim = session.Claims[index];
                Assert.AreEqual(index + 1, claim.BatchOrdinal);
                Assert.IsNotEmpty(claim.AutomationClaimId);
                Assert.IsNotEmpty(claim.DisplayId);
                Assert.IsNotEmpty(claim.SourceCaseId);
                Assert.Greater(claim.EvidencePacketCount, 0);
                Assert.Greater(claim.AllegationCount, 0);
                Assert.IsNotNull(claim.UnknownsSummary);
                for (int earlier = 0; earlier < index; earlier++)
                    Assert.AreNotEqual(
                        session.Claims[earlier].AutomationClaimId,
                        claim.AutomationClaimId);
            }
        }

        [Test]
        public void NarrowRecognisedRulingCreatesNoReturnPacket()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(8);
            AutomationPublicClaim claim = FirstPossession(session.Claims);

            AutomationRulingResult result = session.Commit(
                claim.AutomationClaimId,
                PlayerScopeChoice.Narrow,
                PlayerRulingDisposition.Recognised);

            Assert.IsNotEmpty(result.RulingId);
            Assert.IsNull(result.Appeal);
            Assert.That(result.Disposition, Does.Contain("Recogn"));
        }

        [Test]
        public void BroadRecognisedRulingCreatesLinkedReturnPacket()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(8);
            AutomationPublicClaim claim = FirstPossession(session.Claims);

            AutomationRulingResult result = session.Commit(
                claim.AutomationClaimId,
                PlayerScopeChoice.Broad,
                PlayerRulingDisposition.Recognised);

            Assert.IsNotNull(result.Appeal);
            Assert.AreEqual(
                claim.AutomationClaimId,
                result.Appeal.ParentAutomationClaimId);
            Assert.AreEqual(result.RulingId, result.Appeal.OriginatingRulingId);
            Assert.IsNotEmpty(result.Appeal.SourceCaseId);
            Assert.IsNotEmpty(result.Appeal.PublicBasis);
        }

        [Test]
        public void CommittingTheSameEnvelopeTwiceIsRejected()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(1);
            string claimId = session.Claims[0].AutomationClaimId;
            session.Commit(
                claimId,
                PlayerScopeChoice.Broad,
                PlayerRulingDisposition.Recognised);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                session.Commit(
                    claimId,
                    PlayerScopeChoice.Narrow,
                    PlayerRulingDisposition.Denied));
            Assert.That(error.Message, Does.Contain("already has a ruling"));
        }

        [Test]
        public void ShiftReleaseRetainsOneSocietyAndAdvancesGlobalClaimIdentity()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(12);
            long tickBefore = session.SocietyTick;
            string firstCase = session.Claims[0].SourceCaseId;

            IReadOnlyList<AutomationPublicClaim> second =
                session.ReleaseNextShift(12);

            Assert.AreEqual(12, second.Count);
            Assert.AreEqual(13, second[0].BatchOrdinal);
            Assert.AreNotEqual(firstCase, second[0].SourceCaseId);
            Assert.Greater(session.SocietyTick, tickBefore);
        }

        [Test]
        public void PublicEvidenceEnvelopesCarryDeterministicSupportVariation()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(12);
            var support = new HashSet<int>();
            for (int i = 0; i < session.Claims.Count; i++)
                support.Add(session.Claims[i].EvidenceSupportMaximum);

            Assert.GreaterOrEqual(support.Count, 3,
                "Camera, access-log and damaged-sensor records should not be equivalent.");
        }

        [Test]
        public void ReturnedAppealCommitsAppellateRulingAndRealHolding()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(8);
            AutomationRulingResult initial = session.Commit(
                FirstPossession(session.Claims).AutomationClaimId,
                PlayerScopeChoice.Broad,
                PlayerRulingDisposition.Recognised);

            AutomationAppealResolutionResult appellate = session.ResolveAppeal(
                initial.Appeal,
                AutomationAppealProcedure.FastTrack,
                establishHolding: true);

            Assert.IsNotEmpty(appellate.RulingId);
            Assert.That(appellate.Disposition, Does.Contain("Affirmed"));
            Assert.That(appellate.EstablishedHolding, Is.True);
            Assert.AreEqual(1, session.HoldingCount);
            Assert.AreEqual(2, session.CommittedRulingCount);
        }

        [Test]
        public void PrecedentReuseCitesMatchingInstalledHoldingOnLaterCase()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(8);
            AutomationRulingResult initial = session.Commit(
                FirstPossession(session.Claims).AutomationClaimId,
                PlayerScopeChoice.Broad,
                PlayerRulingDisposition.Recognised);
            session.ResolveAppeal(
                initial.Appeal,
                AutomationAppealProcedure.FastTrack,
                establishHolding: true);
            AutomationPublicClaim later = FirstPossession(
                session.ReleaseNextShift(8));

            AutomationRulingResult laterResult = session.Commit(
                later.AutomationClaimId,
                PlayerScopeChoice.Broad,
                PlayerRulingDisposition.Recognised,
                citeMatchingHoldings: true);

            Assert.GreaterOrEqual(laterResult.CitedHoldingCount, 1);
        }

        [Test]
        public void ShiftOneBroadRulingProducesTraceableLaterDocketWork()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(8);
            AutomationRulingResult first = session.Commit(
                FirstPossession(session.Claims).AutomationClaimId,
                PlayerScopeChoice.Broad,
                PlayerRulingDisposition.Recognised);

            IReadOnlyList<AutomationPublicClaim> later =
                session.ReleaseNextShift(12);
            bool foundDescendant = false;
            for (int i = 0; i < later.Count; i++)
                if (later[i].OriginatingRulingId == first.RulingId)
                    foundDescendant = true;

            Assert.That(foundDescendant, Is.True,
                "A later operating batch should contain work caused by the retained ruling.");
        }

        [Test]
        public void BindingProcedureIsRecordedOnCommittedInstitutionalRuling()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(1);
            AutomationRulingResult result = session.Commit(
                session.Claims[0].AutomationClaimId,
                PlayerScopeChoice.Narrow,
                PlayerRulingDisposition.Recognised,
                new[]
                {
                    AutomationInstitutionalProcedure.MandatorySecondaryVerification,
                    AutomationInstitutionalProcedure.ProtectedEvidenceChannel,
                });

            Assert.That(result.DirectInstitutionalChanges,
                Has.Some.Contains("Secondary verification"));
            Assert.That(result.DirectInstitutionalChanges,
                Has.Some.Contains("Protected evidence channel"));
        }

        [Test]
        public void AccessWithdrawalRulingExecutesRestorationInSameSociety()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(8);
            AutomationPublicClaim access = null;
            for (int i = 0; i < session.Claims.Count; i++)
                if (session.Claims[i].Issue.Contains("Access"))
                    access = session.Claims[i];
            Assert.IsNotNull(access);

            AutomationRulingResult result = session.Commit(
                access.AutomationClaimId,
                PlayerScopeChoice.Narrow,
                PlayerRulingDisposition.Recognised);

            Assert.That(result.Remedies, Has.Some.Contains("Restore access"));
            Assert.That(result.DirectInstitutionalChanges,
                Has.Some.Contains("restore access"));
        }

        [Test]
        public void CollectiveGrievanceExecutesGroupStandingRemedy()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(12);
            AutomationPublicClaim collective = null;
            for (int i = 0; i < session.Claims.Count; i++)
                if (session.Claims[i].Issue.Contains("Collective"))
                    collective = session.Claims[i];

            Assert.IsNotNull(collective,
                "The third active family should originate from generic organisation.");
            Assert.GreaterOrEqual(collective.Parties.Count, 2);
            AutomationRulingResult result = session.Commit(
                collective.AutomationClaimId,
                PlayerScopeChoice.Broad,
                PlayerRulingDisposition.Recognised);

            Assert.That(result.Remedies, Has.Some.Contains("Recognise collective"));
            Assert.That(result.DirectInstitutionalChanges,
                Has.Some.Contains("collective standing"));
            Assert.GreaterOrEqual(
                session.SocietyMetrics.RecognisedCollectiveMembers, 2);
        }

        [Test]
        public void CheckpointRestoresContinuingDocketAndPrecedentModes()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(8);
            AutomationRulingResult initial = session.Commit(
                FirstPossession(session.Claims).AutomationClaimId,
                PlayerScopeChoice.Broad,
                PlayerRulingDisposition.Recognised);
            session.ResolveAppeal(
                initial.Appeal,
                AutomationAppealProcedure.FastTrack,
                establishHolding: true);
            string holdingId = session.Precedents[0].HoldingId;
            session.SetPrecedentMode(
                holdingId, AutomationPrecedentMode.HumanReviewRequired);

            InstitutionalAutomationCheckpoint checkpoint =
                session.CreateCheckpoint();
            InstitutionalAutomationSession restored =
                InstitutionalAutomationSession.Restore(checkpoint);

            Assert.AreEqual(session.SocietyTick, restored.SocietyTick);
            Assert.AreEqual(session.CommittedRulingCount,
                restored.CommittedRulingCount);
            Assert.AreEqual(1, restored.Precedents.Count);
            Assert.AreEqual(AutomationPrecedentMode.HumanReviewRequired,
                restored.Precedents[0].Mode);
            Assert.AreEqual(session.Claims[0].SourceCaseId,
                restored.Claims[0].SourceCaseId);
        }

        [Test]
        [Category("LongRunningProduct")]
        public void EightProofFortressBatchesRetainOneSocietyThroughNinetySixRulings()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(12);
            for (int shift = 1; shift <= 8; shift++)
            {
                var batch = new List<AutomationPublicClaim>(session.Claims);
                for (int i = 0; i < batch.Count; i++)
                {
                    AutomationPublicClaim claim = batch[i];
                    PlayerRulingDisposition disposition =
                        claim.CitableEvidenceCount > 0 &&
                        claim.EvidenceSupportMinimum >= 52
                            ? PlayerRulingDisposition.Recognised
                            : PlayerRulingDisposition.Denied;
                    session.Commit(
                        claim.AutomationClaimId,
                        PlayerScopeChoice.Narrow,
                        disposition);
                }
                if (shift < 8) session.ReleaseNextShift(12);
            }

            session.ValidateCurrentState();
            Assert.That(session.CommittedRulingCount,
                Is.GreaterThanOrEqualTo(96));
            Assert.That(session.SocietyTick, Is.GreaterThan(8));
        }

        private static AutomationPublicClaim FirstPossession(
            IReadOnlyList<AutomationPublicClaim> claims)
        {
            for (int i = 0; i < claims.Count; i++)
                if (claims[i].Issue.Contains("Possession")) return claims[i];
            throw new AssertionException("The persistent feed has no possession case.");
        }
    }
}
