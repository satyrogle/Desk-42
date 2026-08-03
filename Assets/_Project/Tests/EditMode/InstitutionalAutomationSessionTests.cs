using System;
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
                InstitutionalAutomationSession.Create(1);
            AutomationPublicClaim claim = session.Claims[0];

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
                InstitutionalAutomationSession.Create(1);
            AutomationPublicClaim claim = session.Claims[0];

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
    }
}
