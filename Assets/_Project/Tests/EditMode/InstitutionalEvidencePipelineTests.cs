using System.Collections.Generic;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalEvidencePipelineTests
    {
        [Test]
        public void OpaqueEvidenceClassesDriveScoreAndEvidenceBounds()
        {
            var report = new InstitutionalConsequenceReport();
            report.EvidenceArtifacts.Add(new EvidenceArtifact
            {
                ArtifactId = "artifact.support",
                CaseId = "case.fixture",
                EnteredCycle = 2,
                Kind = EvidenceArtifactKind.ClaimantStatement,
                EvidenceClassId = "evidence-class.sample",
                Effect = EvidenceEffect.SupportsFinding,
                BaseWeight = 100,
                Reliability = 50,
            });
            report.EvidenceArtifacts.Add(new EvidenceArtifact
            {
                ArtifactId = "artifact.oppose",
                CaseId = "case.fixture",
                EnteredCycle = 3,
                Kind = EvidenceArtifactKind.ClaimantStatement,
                EvidenceClassId = "evidence-class.telemetry",
                Effect = EvidenceEffect.OpposesFinding,
                BaseWeight = 60,
                Reliability = 50,
            });
            var policy = new InstitutionalPolicyConfiguration
            {
                ClaimantEvidenceWeightPercent = 0,
                EvidenceClassWeights = new List<EvidenceClassWeight>
                {
                    new()
                    {
                        EvidenceClassId = "evidence-class.sample",
                        WeightPercent = 80,
                    },
                    new()
                    {
                        EvidenceClassId = "evidence-class.telemetry",
                        WeightPercent = 50,
                    },
                },
            };

            EvidenceEvaluation result = InstitutionalEvidencePipeline.Evaluate(
                report,
                "case.fixture",
                3,
                policy);

            Assert.AreEqual(25, result.Score);
            Assert.AreEqual(10, result.MinimumScore);
            Assert.AreEqual(65, result.MaximumScore);
            CollectionAssert.AreEqual(
                new[] { "artifact.oppose", "artifact.support" },
                result.Evidence.ConvertAll(value => value.ArtifactId));
        }

        [Test]
        public void MissingOpaqueWeightIsRejectedInsteadOfFallingBackToLegacyKind()
        {
            var artifact = new EvidenceArtifact
            {
                Kind = EvidenceArtifactKind.WitnessRecord,
                EvidenceClassId = "evidence-class.unconfigured",
            };
            var policy = new InstitutionalPolicyConfiguration
            {
                WitnessEvidenceWeightPercent = 73,
            };

            Assert.Throws<System.InvalidOperationException>(() =>
                policy.WeightPercent(artifact));
            Assert.Throws<System.InvalidOperationException>(() =>
                policy.ValidateEvidenceClassCoverage(new[]
                {
                    artifact.EvidenceClassId,
                }));
        }
    }
}
