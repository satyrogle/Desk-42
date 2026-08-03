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
                policy.PolicyReliabilityPercent(artifact));
            Assert.Throws<System.InvalidOperationException>(() =>
                policy.ValidateEvidenceClassCoverage(new[]
                {
                    artifact.EvidenceClassId,
                }));
        }

        [Test]
        public void PolicyReliabilityComposesAfterSourceReliabilityAndPreservesBounds()
        {
            EvidenceEvaluation support = EvaluateSingle(
                EvidenceEffect.SupportsFinding,
                baseWeight: 200,
                classWeight: 80,
                sourceReliability: 50,
                policyReliability: 25);
            EvidenceEvaluation oppose = EvaluateSingle(
                EvidenceEffect.OpposesFinding,
                baseWeight: 200,
                classWeight: 80,
                sourceReliability: 50,
                policyReliability: 25);

            Assert.AreEqual(20, support.Score);
            Assert.AreEqual(20, support.MinimumScore);
            Assert.AreEqual(160, support.MaximumScore);
            Assert.AreEqual(-20, oppose.Score);
            Assert.AreEqual(-160, oppose.MinimumScore);
            Assert.AreEqual(-20, oppose.MaximumScore);
        }

        [TestCase(10, 30, 68, 99, 1)]
        [TestCase(10, 30, 67, 50, 1)]
        public void PolicyReliabilityUsesFrozenSequentialTruncationOrder(
            int baseWeight,
            int classWeight,
            int sourceReliability,
            int policyReliability,
            int expected)
        {
            EvidenceEvaluation result = EvaluateSingle(
                EvidenceEffect.SupportsFinding,
                baseWeight,
                classWeight,
                sourceReliability,
                policyReliability);

            Assert.AreEqual(expected, result.Score);
        }

        [Test]
        public void OmittedPolicyReliabilityDefaultsToNeutralHundred()
        {
            var artifact = new EvidenceArtifact
            {
                ArtifactId = "artifact.default-reliability",
                CaseId = "case.fixture",
                EnteredCycle = 1,
                Kind = EvidenceArtifactKind.ActionRecord,
                EvidenceClassId = "evidence-class.fixture",
                Effect = EvidenceEffect.SupportsFinding,
                BaseWeight = 200,
                Reliability = 50,
            };
            var report = new InstitutionalConsequenceReport();
            report.EvidenceArtifacts.Add(artifact);
            var policy = new InstitutionalPolicyConfiguration
            {
                EvidenceClassWeights = new List<EvidenceClassWeight>
                {
                    new()
                    {
                        EvidenceClassId = artifact.EvidenceClassId,
                        WeightPercent = 80,
                    },
                },
            };

            EvidenceEvaluation result = InstitutionalEvidencePipeline.Evaluate(
                report,
                artifact.CaseId,
                1,
                policy);

            Assert.AreEqual(100, policy.PolicyReliabilityPercent(artifact));
            Assert.AreEqual(80, result.Score);
            Assert.AreEqual(80, result.MinimumScore);
            Assert.AreEqual(160, result.MaximumScore);
        }

        [Test]
        public void PolicyReliabilityRulesValidateAndCloneAsDetachedData()
        {
            var source = new InstitutionalPolicyConfiguration
            {
                EvidenceClassWeights = new List<EvidenceClassWeight>
                {
                    new()
                    {
                        EvidenceClassId = "evidence-class.fixture",
                        WeightPercent = 70,
                        PolicyReliabilityPercent = 40,
                    },
                },
            };
            EvidenceArtifact artifact = new()
            {
                EvidenceClassId = "evidence-class.fixture",
            };

            InstitutionalPolicyConfiguration clone = source.CloneWithIdentity(
                "configuration.clone",
                "policy.clone.v1");
            source.EvidenceClassWeights[0].WeightPercent = 1;
            source.EvidenceClassWeights[0].PolicyReliabilityPercent = 2;
            source.EvidenceClassWeights.Add(new EvidenceClassWeight
            {
                EvidenceClassId = "evidence-class.source-only",
                WeightPercent = 3,
                PolicyReliabilityPercent = 4,
            });

            Assert.AreNotSame(source.EvidenceClassWeights, clone.EvidenceClassWeights);
            Assert.AreNotSame(source.EvidenceClassWeights[0], clone.EvidenceClassWeights[0]);
            Assert.AreEqual(70, clone.WeightPercent(artifact));
            Assert.AreEqual(40, clone.PolicyReliabilityPercent(artifact));
            Assert.AreEqual(1, clone.EvidenceClassWeights.Count);

            clone.EvidenceClassWeights[0].PolicyReliabilityPercent = 55;
            Assert.AreEqual(2, source.PolicyReliabilityPercent(artifact));
            Assert.AreEqual(55, clone.PolicyReliabilityPercent(artifact));
        }

        [TestCase(-1)]
        [TestCase(101)]
        public void OutOfRangePolicyReliabilityIsRejected(int policyReliability)
        {
            InstitutionalPolicyConfiguration policy = PolicyWithRule(
                classWeight: 100,
                policyReliability);

            Assert.Throws<System.InvalidOperationException>(() =>
                policy.ValidateEvidenceClassCoverage(new[]
                {
                    "evidence-class.fixture",
                }));
            Assert.Throws<System.InvalidOperationException>(() =>
                EvaluateSingle(
                    EvidenceEffect.SupportsFinding,
                    baseWeight: 100,
                    classWeight: 100,
                    sourceReliability: 100,
                    policyReliability: policyReliability));
        }

        [Test]
        public void ZeroPolicyReliabilityIsValidAndSuppressesEffectiveScore()
        {
            InstitutionalPolicyConfiguration policy = PolicyWithRule(
                classWeight: 100,
                policyReliability: 0);
            policy.ValidateEvidenceClassCoverage(new[] { "evidence-class.fixture" });

            EvidenceEvaluation result = EvaluateSingle(
                EvidenceEffect.SupportsFinding,
                baseWeight: 100,
                classWeight: 100,
                sourceReliability: 100,
                policyReliability: 0);

            Assert.AreEqual(0, result.Score);
            Assert.AreEqual(0, result.MinimumScore);
            Assert.AreEqual(100, result.MaximumScore);
        }

        [Test]
        public void InvalidPolicyReliabilityRowsFailClosed()
        {
            var nullRow = new InstitutionalPolicyConfiguration
            {
                EvidenceClassWeights = new List<EvidenceClassWeight> { null },
            };
            var duplicate = new InstitutionalPolicyConfiguration
            {
                EvidenceClassWeights = new List<EvidenceClassWeight>
                {
                    new()
                    {
                        EvidenceClassId = "evidence-class.fixture",
                        WeightPercent = 100,
                    },
                    new()
                    {
                        EvidenceClassId = "evidence-class.fixture",
                        WeightPercent = 100,
                    },
                },
            };

            Assert.Throws<System.InvalidOperationException>(() =>
                nullRow.ValidateEvidenceClassCoverage(new[] { "evidence-class.fixture" }));
            Assert.Throws<System.InvalidOperationException>(() =>
                duplicate.ValidateEvidenceClassCoverage(new[] { "evidence-class.fixture" }));
        }

        [Test]
        public void ScoreAccumulationThrowsInsteadOfOverflowing()
        {
            var report = new InstitutionalConsequenceReport();
            report.EvidenceArtifacts.Add(OverflowArtifact("artifact.overflow.a"));
            report.EvidenceArtifacts.Add(OverflowArtifact("artifact.overflow.b"));
            InstitutionalPolicyConfiguration policy = PolicyWithRule(100, 100);

            Assert.Throws<System.OverflowException>(() =>
                InstitutionalEvidencePipeline.Evaluate(
                    report,
                    "case.fixture",
                    1,
                    policy));
        }

        [Test]
        public void LegacyEvidenceKindUsesNeutralPolicyReliability()
        {
            var artifact = new EvidenceArtifact
            {
                ArtifactId = "artifact.legacy",
                CaseId = "case.fixture",
                EnteredCycle = 1,
                Kind = EvidenceArtifactKind.ActionRecord,
                Effect = EvidenceEffect.SupportsFinding,
                BaseWeight = 100,
                Reliability = 50,
            };
            var report = new InstitutionalConsequenceReport();
            report.EvidenceArtifacts.Add(artifact);
            var policy = new InstitutionalPolicyConfiguration
            {
                ActionRecordWeightPercent = 50,
            };

            EvidenceEvaluation result = InstitutionalEvidencePipeline.Evaluate(
                report,
                artifact.CaseId,
                1,
                policy);

            Assert.AreEqual(100, policy.PolicyReliabilityPercent(artifact));
            Assert.AreEqual(25, result.Score);
        }

        private static EvidenceEvaluation EvaluateSingle(
            EvidenceEffect effect,
            int baseWeight,
            int classWeight,
            int sourceReliability,
            int policyReliability)
        {
            var report = new InstitutionalConsequenceReport();
            report.EvidenceArtifacts.Add(new EvidenceArtifact
            {
                ArtifactId = "artifact.fixture",
                CaseId = "case.fixture",
                EnteredCycle = 1,
                Kind = EvidenceArtifactKind.ActionRecord,
                EvidenceClassId = "evidence-class.fixture",
                Effect = effect,
                BaseWeight = baseWeight,
                Reliability = sourceReliability,
            });
            return InstitutionalEvidencePipeline.Evaluate(
                report,
                "case.fixture",
                1,
                PolicyWithRule(classWeight, policyReliability));
        }

        private static InstitutionalPolicyConfiguration PolicyWithRule(
            int classWeight,
            int policyReliability)
        {
            return new InstitutionalPolicyConfiguration
            {
                EvidenceClassWeights = new List<EvidenceClassWeight>
                {
                    new()
                    {
                        EvidenceClassId = "evidence-class.fixture",
                        WeightPercent = classWeight,
                        PolicyReliabilityPercent = policyReliability,
                    },
                },
            };
        }

        private static EvidenceArtifact OverflowArtifact(string artifactId)
        {
            return new EvidenceArtifact
            {
                ArtifactId = artifactId,
                CaseId = "case.fixture",
                EnteredCycle = 1,
                Kind = EvidenceArtifactKind.ActionRecord,
                EvidenceClassId = "evidence-class.fixture",
                Effect = EvidenceEffect.SupportsFinding,
                BaseWeight = int.MaxValue,
                Reliability = 100,
            };
        }
    }
}
