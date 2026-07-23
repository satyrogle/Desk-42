using System;
using System.Linq;
using Desk42.Core;
using Desk42.UI;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class EliasProcedureReceiptSequenceTests
    {
        [Test]
        public void AmendRecord_RendersLockedMemoryAnchorBeforeAppliedReward()
        {
            EliasProcedureReceiptBeat[] beats =
                EliasProcedureReceiptSequence.Build(BuildResult(
                    EliasProcedureActionId.AmendRecord,
                    EliasProofContent.Shift2AppearanceKey,
                    EliasShift2Branch.NormalisedAddress,
                    "18B Calder House",
                    "18A Calder House",
                    EliasProcedurePolicy.MiriamRegisteredAt18A,
                    complianceStreakDelta: 1f));

            CollectionAssert.AreEqual(
                new[]
                {
                    "RECORD AMENDED",
                    "18B -> 18A",
                    "M. VENN - REGISTERED 18A",
                    "CLAIM ACCEPTED FOR PROCESSING",
                    "COMPLIANCE STREAK +1",
                },
                beats.Select(beat => beat.Text).ToArray());
            Assert.AreEqual(
                EliasProcedureReceiptBeatKind.MemoryAnchor,
                beats[2].Kind);
            Assert.Less(
                Array.FindIndex(beats, beat =>
                    beat.Kind
                        == EliasProcedureReceiptBeatKind.MemoryAnchor),
                Array.FindIndex(beats, beat =>
                    beat.Kind
                        == EliasProcedureReceiptBeatKind.AppliedDelta));
        }

        [TestCase(
            EliasProcedureActionId.RetainLegacyUnit,
            EliasProofContent.Shift2AppearanceKey,
            "LEGACY UNIT RETAINED")]
        [TestCase(
            EliasProcedureActionId.ReferForReview,
            EliasProofContent.Shift2AppearanceKey,
            "PHYSICAL VERIFICATION OPENED")]
        [TestCase(
            EliasProcedureActionId.RequestClarification,
            EliasProofContent.Shift5AppearanceKey,
            "CLARIFICATION REQUESTED")]
        [TestCase(
            EliasProcedureActionId.ReferForReview,
            EliasProofContent.Shift5AppearanceKey,
            "REVIEW REFERRED")]
        public void OtherProcedures_ReportActionWithoutGrading(
            EliasProcedureActionId actionId,
            string appearanceKey,
            string expectedHeading)
        {
            EliasProcedureReceiptBeat[] beats =
                EliasProcedureReceiptSequence.Build(BuildResult(
                    actionId,
                    appearanceKey,
                    EliasShift2Branch.LegacyException,
                    "18B Calder House",
                    "18B Calder House",
                    null,
                    complianceStreakDelta: 0f));

            Assert.AreEqual(expectedHeading, beats[0].Text);
            Assert.AreEqual(
                "CLAIM ACCEPTED FOR PROCESSING",
                beats.Last().Text);
            string allCopy =
                string.Join(" ", beats.Select(beat => beat.Text))
                    .ToLowerInvariant();
            foreach (string banned in new[]
                     {
                         "correct", "valid choice", "better choice",
                         "optimal", "mistake", "wasted", "penalty",
                         "should have",
                     })
            {
                StringAssert.DoesNotContain(banned, allCopy);
            }
        }

        [Test]
        public void Receipt_UsesActualAppliedDelta()
        {
            EliasProcedureReceiptBeat[] beats =
                EliasProcedureReceiptSequence.Build(BuildResult(
                    EliasProcedureActionId.AmendRecord,
                    EliasProofContent.Shift2AppearanceKey,
                    EliasShift2Branch.NormalisedAddress,
                    "18B Calder House",
                    "18A Calder House",
                    EliasProcedurePolicy.MiriamRegisteredAt18A,
                    complianceStreakDelta: 0.5f));

            Assert.AreEqual(
                "COMPLIANCE STREAK +0.5",
                beats.Last().Text);
        }

        [Test]
        public void AmendRecord_WithMissingAnchor_FailsLoudly()
        {
            Assert.Throws<InvalidOperationException>(() =>
                EliasProcedureReceiptSequence.Build(BuildResult(
                    EliasProcedureActionId.AmendRecord,
                    EliasProofContent.Shift2AppearanceKey,
                    EliasShift2Branch.NormalisedAddress,
                    "18B Calder House",
                    "18A Calder House",
                    null,
                    complianceStreakDelta: 1f)));
        }

        private static AppliedEliasProcedure BuildResult(
            EliasProcedureActionId actionId,
            string appearanceKey,
            EliasShift2Branch branch,
            string addressBefore,
            string addressAfter,
            string miriamReference,
            float complianceStreakDelta)
            => new(
                "receipt-test",
                appearanceKey,
                EliasProofContent.CanonicalClaimantId,
                actionId,
                branch,
                priorVisits: 1,
                currentVisitNumber: 2,
                creditsDelta: 0,
                sanityDelta: 0f,
                soulIntegrityDelta: 0f,
                complianceStreakDelta,
                addressBefore,
                addressAfter,
                miriamReference,
                $"test_{actionId}");
    }
}
