using System;
using System.Collections.Generic;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalAdjudicationServiceTests
    {
        [Test]
        public void InitialAdjudicationReturnsDetachedEvidenceAndEvidenceScoreBounds()
        {
            InstitutionalConsequenceReport report = CreateReportWithSupportEvidence(
                baseWeight: 200,
                reliability: 50);
            EvidenceArtifact liveArtifact = report.EvidenceArtifacts[0];
            InstitutionalAdjudicationRequest request = CreateRequest(
                cycle: 4,
                maximumEvidenceCycle: 2,
                requiredEvidenceScore: 70);
            request.AppliedPolicyIds.Add("rule.burden");
            request.SkippedProcedureIds.Add("procedure.external-verification");

            InstitutionalAdjudicationResult result =
                InstitutionalAdjudicationService.IssueInitial(report, request);

            Assert.AreEqual(RulingDisposition.Recognised, result.Ruling.Disposition);
            Assert.AreEqual(FindingDisposition.Established, result.Finding.Disposition);
            Assert.AreEqual(80, result.EvidenceScore);
            Assert.AreEqual(80, result.EvidenceScoreMinimum);
            Assert.AreEqual(160, result.EvidenceScoreMaximum);
            Assert.AreEqual(80, result.Ruling.ConfidenceMinimum);
            Assert.AreEqual(160, result.Ruling.ConfidenceMaximum,
                "Legacy confidence fields contain score bounds and are not percentages.");
            Assert.AreEqual(1, report.OfficialFindings.Count);
            Assert.AreSame(result.Finding, report.OfficialFindings[0]);
            Assert.AreEqual(1, report.Rulings.Count);
            Assert.AreSame(result.Ruling, report.Rulings[0]);
            Assert.AreEqual(1, report.Timeline.Count);
            Assert.AreEqual(InstitutionalTimelineKind.RulingIssued,
                report.Timeline[0].Kind);

            liveArtifact.BaseWeight = 1;
            liveArtifact.Provenance.SourceDecisionId = "decision.changed-later";
            request.AppliedPolicyIds.Add("rule.added-later");

            Assert.AreEqual(200, result.FrozenEvidence[0].BaseWeight);
            Assert.AreEqual("decision.source", result.FrozenEvidence[0].SourceDecisionId);
            CollectionAssert.AreEqual(
                new[] { "rule.burden" },
                result.Ruling.AppliedPolicyIds);
        }

        [TestCase(40, false, 0, RulingDisposition.Recognised,
            FindingDisposition.Established)]
        [TestCase(100, true, 40, RulingDisposition.ProvisionallyRecognised,
            FindingDisposition.ProvisionallyEstablished)]
        [TestCase(100, false, 0, RulingDisposition.Denied,
            FindingDisposition.NotEstablished)]
        public void InitialDispositionComesOnlyFromConfiguredScoreThresholds(
            int requiredScore,
            bool permitProvisional,
            int provisionalScore,
            RulingDisposition expectedRuling,
            FindingDisposition expectedFinding)
        {
            InstitutionalConsequenceReport report = CreateReportWithSupportEvidence(
                baseWeight: 50,
                reliability: 100);
            InstitutionalAdjudicationRequest request = CreateRequest(
                cycle: 3,
                maximumEvidenceCycle: 2,
                requiredEvidenceScore: requiredScore);
            request.PermitProvisionalRecognition = permitProvisional;
            request.ProvisionalEvidenceScore = permitProvisional
                ? provisionalScore
                : (int?)null;

            InstitutionalAdjudicationResult result =
                InstitutionalAdjudicationService.IssueInitial(report, request);

            Assert.AreEqual(expectedRuling, result.Ruling.Disposition);
            Assert.AreEqual(expectedFinding, result.Finding.Disposition);
        }

        [Test]
        public void PolicyClassReliabilityAloneCanChangeFindingAndRuling()
        {
            InstitutionalConsequenceReport trustedReport =
                CreateReportWithSupportEvidence(baseWeight: 100, reliability: 100);
            InstitutionalAdjudicationRequest trustedRequest = CreateRequest(
                cycle: 3,
                maximumEvidenceCycle: 2,
                requiredEvidenceScore: 60);
            trustedRequest.PolicyConfiguration.EvidenceClassWeights[0]
                .PolicyReliabilityPercent = 100;

            InstitutionalConsequenceReport discountedReport =
                CreateReportWithSupportEvidence(baseWeight: 100, reliability: 100);
            InstitutionalAdjudicationRequest discountedRequest = CreateRequest(
                cycle: 3,
                maximumEvidenceCycle: 2,
                requiredEvidenceScore: 60);
            discountedRequest.PolicyConfiguration.EvidenceClassWeights[0]
                .PolicyReliabilityPercent = 50;

            InstitutionalAdjudicationResult trusted =
                InstitutionalAdjudicationService.IssueInitial(
                    trustedReport,
                    trustedRequest);
            InstitutionalAdjudicationResult discounted =
                InstitutionalAdjudicationService.IssueInitial(
                    discountedReport,
                    discountedRequest);

            Assert.AreEqual(80, trusted.EvidenceScore);
            Assert.AreEqual(RulingDisposition.Recognised, trusted.Ruling.Disposition);
            Assert.AreEqual(FindingDisposition.Established, trusted.Finding.Disposition);
            Assert.AreEqual(40, discounted.EvidenceScore);
            Assert.AreEqual(RulingDisposition.Denied, discounted.Ruling.Disposition);
            Assert.AreEqual(FindingDisposition.NotEstablished,
                discounted.Finding.Disposition);
            Assert.AreEqual(40, discounted.Ruling.ConfidenceMinimum);
            Assert.AreEqual(80, discounted.Ruling.ConfidenceMaximum);
        }

        [TestCase(false, false, RulingDisposition.Affirmed, AppealDisposition.Affirmed)]
        [TestCase(true, true, RulingDisposition.Affirmed, AppealDisposition.Affirmed)]
        [TestCase(false, true, RulingDisposition.ReversedAndRecognised,
            AppealDisposition.Reversed)]
        [TestCase(true, false, RulingDisposition.ReversedAndDenied,
            AppealDisposition.Reversed)]
        public void AppealDispositionComparesSubstantiveResults(
            bool challengedRecognised,
            bool finalRecognised,
            RulingDisposition expectedRuling,
            AppealDisposition expectedAppeal)
        {
            InstitutionalConsequenceReport report = new();
            OfficialFinding challengedFinding = AddChallengedRuling(
                report,
                challengedRecognised);
            Ruling challengedRuling = report.Rulings[0];
            Appeal appeal = AddPendingAppeal(report, challengedRuling);
            if (finalRecognised)
                report.EvidenceArtifacts.Add(CreateSupportArtifact(100, 100));
            InstitutionalAdjudicationRequest request = CreateRequest(
                cycle: 4,
                maximumEvidenceCycle: 3,
                requiredEvidenceScore: 50);

            InstitutionalAdjudicationResult result =
                InstitutionalAdjudicationService.ResolveAppeal(
                    report,
                    request,
                    challengedRuling,
                    appeal);

            Assert.AreEqual(expectedRuling, result.Ruling.Disposition);
            Assert.AreEqual(expectedAppeal, appeal.Disposition);
            Assert.AreEqual(result.Ruling.RulingId, appeal.ResultingRulingId);
            Assert.AreEqual(finalRecognised, result.SubstantivelyRecognised);
            Assert.AreEqual(2, report.OfficialFindings.Count);
            Assert.AreSame(challengedFinding, report.OfficialFindings[0]);
            Assert.AreEqual(2, report.Rulings.Count);
            Assert.AreEqual(2, report.Timeline.Count);
            Assert.AreEqual(InstitutionalTimelineKind.AppealHeard,
                report.Timeline[0].Kind);
            Assert.AreEqual(result.Ruling.RulingId, report.Timeline[0].DetailId);
            Assert.AreEqual(InstitutionalTimelineKind.RulingIssued,
                report.Timeline[1].Kind);
            Assert.IsEmpty(report.Holdings,
                "Adjudication resolves an appeal but never creates precedent.");
        }

        [Test]
        public void CitedHoldingContributesWeightAndCopiesItsValidatedScope()
        {
            InstitutionalConsequenceReport report = new();
            report.Holdings.Add(new Holding
            {
                HoldingId = "holding.fixture",
                EstablishedCycle = 1,
                IssueId = "issue.fixture",
                Scope = new PrecedentScope
                {
                    ScopeId = "scope.fixture",
                },
            });
            InstitutionalAdjudicationRequest request = CreateRequest(
                cycle: 2,
                maximumEvidenceCycle: 1,
                requiredEvidenceScore: 50);
            request.CitedHoldingWeight = 60;
            request.CitedHoldingIds.Add("holding.fixture");

            InstitutionalAdjudicationResult result =
                InstitutionalAdjudicationService.IssueInitial(report, request);

            Assert.AreEqual(60, result.EvidenceScore);
            Assert.AreEqual(60, result.Finding.PrecedentWeightApplied);
            Assert.AreEqual(RulingDisposition.Recognised, result.Ruling.Disposition);
            CollectionAssert.AreEqual(
                new[] { "holding.fixture" },
                result.Ruling.CitedHoldingIds);
            CollectionAssert.AreEqual(
                new[] { "scope.fixture" },
                result.Ruling.CitedScopeIds);
            Assert.AreEqual(1, report.Holdings.Count,
                "Citing precedent must not establish another holding.");
        }

        [Test]
        public void CitedHoldingWithMismatchedFactsCannotInfluenceDisposition()
        {
            InstitutionalConsequenceReport report = new();
            report.Holdings.Add(new Holding
            {
                HoldingId = "holding.fixture",
                EstablishedCycle = 1,
                IssueId = "issue.fixture",
                Scope = new PrecedentScope
                {
                    ScopeId = "scope.fixture",
                    RequiredFacts = new CaseFactSet(new[]
                    {
                        new CaseFact("watershed", "glass-canal"),
                    }),
                },
            });
            InstitutionalAdjudicationRequest request = CreateRequest(
                cycle: 2,
                maximumEvidenceCycle: 1,
                requiredEvidenceScore: 50);
            request.CaseFacts = new CaseFactSet(new[]
            {
                new CaseFact("watershed", "other-canal"),
            });
            request.CitedHoldingWeight = 60;
            request.CitedHoldingIds.Add("holding.fixture");

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalAdjudicationService.IssueInitial(report, request));
            Assert.That(report.OfficialFindings, Is.Empty);
            Assert.That(report.Rulings, Is.Empty);
            Assert.That(report.Timeline, Is.Empty);
        }

        [Test]
        public void DuplicateAdjudicationIsRejectedWithoutDuplicateRows()
        {
            InstitutionalConsequenceReport report = CreateReportWithSupportEvidence(
                baseWeight: 100,
                reliability: 100);
            InstitutionalAdjudicationRequest request = CreateRequest(
                cycle: 3,
                maximumEvidenceCycle: 2,
                requiredEvidenceScore: 50);

            InstitutionalAdjudicationService.IssueInitial(report, request);

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalAdjudicationService.IssueInitial(report, request));
            Assert.AreEqual(1, report.OfficialFindings.Count);
            Assert.AreEqual(1, report.Rulings.Count);
            Assert.AreEqual(1, report.Timeline.Count);
        }

        [Test]
        public void InvalidAppealChronologyIsRejectedBeforeReportOrAppealMutation()
        {
            InstitutionalConsequenceReport report = new();
            AddChallengedRuling(report, challengedRecognised: false);
            Ruling challengedRuling = report.Rulings[0];
            Appeal appeal = AddPendingAppeal(report, challengedRuling);
            appeal.HearingCycle = 6;
            InstitutionalAdjudicationRequest request = CreateRequest(
                cycle: 4,
                maximumEvidenceCycle: 4,
                requiredEvidenceScore: 50);

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalAdjudicationService.ResolveAppeal(
                    report,
                    request,
                    challengedRuling,
                    appeal));

            Assert.AreEqual(AppealDisposition.Pending, appeal.Disposition);
            Assert.IsNull(appeal.ResultingRulingId);
            Assert.AreEqual(1, report.OfficialFindings.Count);
            Assert.AreEqual(1, report.Rulings.Count);
            Assert.IsEmpty(report.Timeline);
        }

        [Test]
        public void EvidenceEnvelopeCannotReachPastTheRulingCycle()
        {
            InstitutionalConsequenceReport report = new();
            InstitutionalAdjudicationRequest request = CreateRequest(
                cycle: 4,
                maximumEvidenceCycle: 5,
                requiredEvidenceScore: 50);

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalAdjudicationService.IssueInitial(report, request));
            Assert.IsEmpty(report.OfficialFindings);
            Assert.IsEmpty(report.Rulings);
            Assert.IsEmpty(report.Timeline);
        }

        private static InstitutionalConsequenceReport CreateReportWithSupportEvidence(
            int baseWeight,
            int reliability)
        {
            var report = new InstitutionalConsequenceReport();
            report.EvidenceArtifacts.Add(CreateSupportArtifact(baseWeight, reliability));
            return report;
        }

        private static EvidenceArtifact CreateSupportArtifact(
            int baseWeight,
            int reliability)
        {
            return new EvidenceArtifact
            {
                ArtifactId = "artifact.support",
                CaseId = "case.fixture",
                EnteredCycle = 2,
                Kind = EvidenceArtifactKind.ActionRecord,
                EvidenceClassId = "evidence-class.fixture",
                IssueId = "issue.fixture",
                PropositionId = "proposition.fixture",
                Effect = EvidenceEffect.SupportsFinding,
                BaseWeight = baseWeight,
                Reliability = reliability,
                OfficiallySubmitted = true,
                Provenance = new EvidenceProvenance
                {
                    ProvenanceId = "provenance.support",
                    SourceDecisionId = "decision.source",
                    SourceSocietyEventId = "event.source",
                },
            };
        }

        private static InstitutionalAdjudicationRequest CreateRequest(
            long cycle,
            long maximumEvidenceCycle,
            int requiredEvidenceScore)
        {
            return new InstitutionalAdjudicationRequest
            {
                CaseId = "case.fixture",
                IssueId = "issue.fixture",
                PhaseId = "phase.fixture",
                Cycle = cycle,
                MaximumEvidenceCycle = maximumEvidenceCycle,
                RequiredEvidenceScore = requiredEvidenceScore,
                PolicyConfiguration = new InstitutionalPolicyConfiguration
                {
                    PolicyConfigurationId = "configuration.fixture",
                    PolicyVersion = "policy.fixture.v1",
                    EvidenceClassWeights = new List<EvidenceClassWeight>
                    {
                        new()
                        {
                            EvidenceClassId = "evidence-class.fixture",
                            WeightPercent = 80,
                        },
                    },
                },
            };
        }

        private static OfficialFinding AddChallengedRuling(
            InstitutionalConsequenceReport report,
            bool challengedRecognised)
        {
            var finding = new OfficialFinding
            {
                FindingId = "finding.challenged",
                CaseId = "case.fixture",
                Cycle = 1,
                IssueId = "issue.fixture",
                Disposition = challengedRecognised
                    ? FindingDisposition.Established
                    : FindingDisposition.NotEstablished,
                RequiredScore = 50,
            };
            report.OfficialFindings.Add(finding);
            report.Rulings.Add(new Ruling
            {
                RulingId = "ruling.challenged",
                CaseId = "case.fixture",
                Cycle = 1,
                PolicyConfigurationId = "configuration.prior",
                PolicyVersion = "policy.prior.v1",
                Disposition = challengedRecognised
                    ? RulingDisposition.Recognised
                    : RulingDisposition.Denied,
                FindingId = finding.FindingId,
            });
            return finding;
        }

        private static Appeal AddPendingAppeal(
            InstitutionalConsequenceReport report,
            Ruling challengedRuling)
        {
            var appeal = new Appeal
            {
                AppealId = "appeal.fixture",
                CaseId = "case.fixture",
                FiledCycle = 2,
                HearingCycle = 4,
                AppellantAgentId = "agent.fixture",
                ChallengedRulingId = challengedRuling.RulingId,
                Disposition = AppealDisposition.Pending,
            };
            report.Appeals.Add(appeal);
            return appeal;
        }
    }
}
