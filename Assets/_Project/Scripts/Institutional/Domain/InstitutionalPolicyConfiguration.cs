using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    [Serializable]
    public sealed class EvidenceClassWeight
    {
        public string EvidenceClassId;
        public int WeightPercent;
    }

    /// <summary>
    /// Immutable-in-practice executable institutional rule bundle. Preserved proof
    /// and declarative scenario runners consume values, never configuration labels.
    /// </summary>
    [Serializable]
    public sealed class InstitutionalPolicyConfiguration
    {
        public string PolicyConfigurationId;
        public string PolicyVersion;
        public InstitutionalPolicyKind Kind;

        public int WorkReward;
        public int AidEffectiveness;
        public int DisclosureProtection;
        public int RetaliationRisk;
        public int AppealAccessibility;
        public int DecisionVariationAmplitude;

        public int ClaimantEvidenceWeightPercent;
        public int ClinicalEvidenceWeightPercent;
        public int PatternEvidenceWeightPercent;
        public int WitnessEvidenceWeightPercent;
        public int ManagementEvidenceWeightPercent;
        public int ActionRecordWeightPercent;
        public List<EvidenceClassWeight> EvidenceClassWeights = new();

        public int InitialRecognitionThreshold;
        public int ProvisionalRecognitionThreshold;
        public int AppealRecognitionThreshold;
        public int LaterRecognitionThreshold;
        public int CitedHoldingWeight;
        public bool PermitProvisionalRecognition;
        public int ProvisionalReliefAmount;
        public bool EstablishAppellateHolding;
        public bool AutoCiteMatchingHoldings;
        public PrecedentReach HoldingReach;
        public bool HoldingIsRetrospective;

        public int WeightPercent(EvidenceArtifactKind kind)
        {
            switch (kind)
            {
                case EvidenceArtifactKind.ClaimantStatement:
                    return ClaimantEvidenceWeightPercent;
                case EvidenceArtifactKind.ClinicalRecord:
                    return ClinicalEvidenceWeightPercent;
                case EvidenceArtifactKind.PatternTestimony:
                    return PatternEvidenceWeightPercent;
                case EvidenceArtifactKind.WitnessRecord:
                    return WitnessEvidenceWeightPercent;
                case EvidenceArtifactKind.ManagementRecord:
                    return ManagementEvidenceWeightPercent;
                case EvidenceArtifactKind.ActionRecord:
                    return ActionRecordWeightPercent;
                default:
                    return 0;
            }
        }

        public int WeightPercent(EvidenceArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (!string.IsNullOrWhiteSpace(artifact.EvidenceClassId))
            {
                if (EvidenceClassWeights == null)
                    throw new InvalidOperationException(
                        "Opaque evidence classes require configured class weights.");
                for (int i = 0; i < EvidenceClassWeights.Count; i++)
                {
                    EvidenceClassWeight configured = EvidenceClassWeights[i];
                    if (string.Equals(configured.EvidenceClassId,
                        artifact.EvidenceClassId, StringComparison.Ordinal))
                    {
                        return configured.WeightPercent;
                    }
                }
                throw new InvalidOperationException(
                    $"No policy weight is configured for opaque evidence class " +
                    $"'{artifact.EvidenceClassId}'.");
            }
            return WeightPercent(artifact.Kind);
        }

        public void ValidateEvidenceClassCoverage(IEnumerable<string> evidenceClassIds)
        {
            if (evidenceClassIds == null) throw new ArgumentNullException(nameof(evidenceClassIds));
            ValidateEvidenceClassWeights();
            var configured = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < EvidenceClassWeights.Count; i++)
                configured.Add(EvidenceClassWeights[i].EvidenceClassId);

            foreach (string evidenceClassId in evidenceClassIds)
            {
                if (string.IsNullOrWhiteSpace(evidenceClassId))
                    throw new InvalidOperationException(
                        "Scenario evidence classes require non-blank identifiers.");
                if (!configured.Contains(evidenceClassId))
                {
                    throw new InvalidOperationException(
                        $"Policy '{PolicyConfigurationId}' does not cover evidence class " +
                        $"'{evidenceClassId}'.");
                }
            }
        }

        public InstitutionalRegimeState CreateRegime()
        {
            return new InstitutionalRegimeState
            {
                WorkReward = WorkReward,
                AidEffectiveness = AidEffectiveness,
                DisclosureProtection = DisclosureProtection,
                RetaliationRisk = RetaliationRisk,
                AppealAccessibility = AppealAccessibility,
                DecisionVariationAmplitude = DecisionVariationAmplitude,
            };
        }

        public InstitutionalPolicyConfiguration CloneWithIdentity(
            string configurationId,
            string policyVersion)
        {
            return new InstitutionalPolicyConfiguration
            {
                PolicyConfigurationId = configurationId,
                PolicyVersion = policyVersion,
                Kind = Kind,
                WorkReward = WorkReward,
                AidEffectiveness = AidEffectiveness,
                DisclosureProtection = DisclosureProtection,
                RetaliationRisk = RetaliationRisk,
                AppealAccessibility = AppealAccessibility,
                DecisionVariationAmplitude = DecisionVariationAmplitude,
                ClaimantEvidenceWeightPercent = ClaimantEvidenceWeightPercent,
                ClinicalEvidenceWeightPercent = ClinicalEvidenceWeightPercent,
                PatternEvidenceWeightPercent = PatternEvidenceWeightPercent,
                WitnessEvidenceWeightPercent = WitnessEvidenceWeightPercent,
                ManagementEvidenceWeightPercent = ManagementEvidenceWeightPercent,
                ActionRecordWeightPercent = ActionRecordWeightPercent,
                EvidenceClassWeights = CloneEvidenceClassWeights(EvidenceClassWeights),
                InitialRecognitionThreshold = InitialRecognitionThreshold,
                ProvisionalRecognitionThreshold = ProvisionalRecognitionThreshold,
                AppealRecognitionThreshold = AppealRecognitionThreshold,
                LaterRecognitionThreshold = LaterRecognitionThreshold,
                CitedHoldingWeight = CitedHoldingWeight,
                PermitProvisionalRecognition = PermitProvisionalRecognition,
                ProvisionalReliefAmount = ProvisionalReliefAmount,
                EstablishAppellateHolding = EstablishAppellateHolding,
                AutoCiteMatchingHoldings = AutoCiteMatchingHoldings,
                HoldingReach = HoldingReach,
                HoldingIsRetrospective = HoldingIsRetrospective,
            };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PolicyConfigurationId))
                throw new InvalidOperationException("Policy configuration id is required.");
            if (string.IsNullOrWhiteSpace(PolicyVersion))
                throw new InvalidOperationException("Policy version is required.");
            ValidatePercent(WorkReward, nameof(WorkReward));
            ValidatePercent(AidEffectiveness, nameof(AidEffectiveness));
            ValidatePercent(DisclosureProtection, nameof(DisclosureProtection));
            ValidatePercent(RetaliationRisk, nameof(RetaliationRisk));
            ValidatePercent(AppealAccessibility, nameof(AppealAccessibility));
            ValidatePercent(ClaimantEvidenceWeightPercent, nameof(ClaimantEvidenceWeightPercent));
            ValidatePercent(ClinicalEvidenceWeightPercent, nameof(ClinicalEvidenceWeightPercent));
            ValidatePercent(PatternEvidenceWeightPercent, nameof(PatternEvidenceWeightPercent));
            ValidatePercent(WitnessEvidenceWeightPercent, nameof(WitnessEvidenceWeightPercent));
            ValidatePercent(ManagementEvidenceWeightPercent, nameof(ManagementEvidenceWeightPercent));
            ValidatePercent(ActionRecordWeightPercent, nameof(ActionRecordWeightPercent));
            ValidateEvidenceClassWeights();
            if (DecisionVariationAmplitude < 0 || DecisionVariationAmplitude > 10)
                throw new InvalidOperationException("Decision variation amplitude must be in [0, 10].");
            if (InitialRecognitionThreshold < 1 || AppealRecognitionThreshold < 1 ||
                LaterRecognitionThreshold < 1)
                throw new InvalidOperationException("Recognition thresholds must be positive.");
            if (CitedHoldingWeight < 0)
                throw new InvalidOperationException("Cited holding weight cannot be negative.");
            if (PermitProvisionalRecognition && ProvisionalRecognitionThreshold < 1)
                throw new InvalidOperationException("A provisional policy requires a positive threshold.");
            if (ProvisionalReliefAmount < 0)
                throw new InvalidOperationException("Configured material amounts cannot be negative.");
        }

        private static void ValidatePercent(int value, string field)
        {
            if (value < 0 || value > 100)
                throw new InvalidOperationException($"{field} must be in [0, 100].");
        }

        private void ValidateEvidenceClassWeights()
        {
            if (EvidenceClassWeights == null)
                throw new InvalidOperationException("Evidence class weights cannot be null.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < EvidenceClassWeights.Count; i++)
            {
                EvidenceClassWeight configured = EvidenceClassWeights[i];
                if (configured == null || string.IsNullOrWhiteSpace(configured.EvidenceClassId))
                    throw new InvalidOperationException("Evidence class weight requires an ID.");
                if (!ids.Add(configured.EvidenceClassId))
                    throw new InvalidOperationException(
                        $"Duplicate evidence class weight '{configured.EvidenceClassId}'.");
                ValidatePercent(configured.WeightPercent,
                    $"EvidenceClassWeights[{configured.EvidenceClassId}]");
            }
        }

        private static List<EvidenceClassWeight> CloneEvidenceClassWeights(
            List<EvidenceClassWeight> source)
        {
            var clone = new List<EvidenceClassWeight>();
            if (source == null) return clone;
            for (int i = 0; i < source.Count; i++)
            {
                EvidenceClassWeight configured = source[i];
                if (configured == null) continue;
                clone.Add(new EvidenceClassWeight
                {
                    EvidenceClassId = configured.EvidenceClassId,
                    WeightPercent = configured.WeightPercent,
                });
            }
            return clone;
        }
    }

    public static class InstitutionalPolicyConfigurations
    {
        public static InstitutionalPolicyConfiguration RecordsFirst()
        {
            return new InstitutionalPolicyConfiguration
            {
                PolicyConfigurationId = "configuration.records-first",
                PolicyVersion = "records-first.v1",
                Kind = InstitutionalPolicyKind.RecordsFirst,
                WorkReward = 54,
                AidEffectiveness = 48,
                DisclosureProtection = 20,
                RetaliationRisk = 80,
                AppealAccessibility = 35,
                DecisionVariationAmplitude = 0,
                ClaimantEvidenceWeightPercent = 0,
                ClinicalEvidenceWeightPercent = 50,
                PatternEvidenceWeightPercent = 0,
                WitnessEvidenceWeightPercent = 100,
                ManagementEvidenceWeightPercent = 100,
                ActionRecordWeightPercent = 100,
                InitialRecognitionThreshold = 70,
                ProvisionalRecognitionThreshold = 0,
                AppealRecognitionThreshold = 70,
                LaterRecognitionThreshold = 80,
                CitedHoldingWeight = 0,
                PermitProvisionalRecognition = false,
                ProvisionalReliefAmount = 0,
                EstablishAppellateHolding = false,
                AutoCiteMatchingHoldings = false,
                HoldingReach = PrecedentReach.Individual,
                HoldingIsRetrospective = false,
            };
        }

        public static InstitutionalPolicyConfiguration ProvisionalTrust()
        {
            return new InstitutionalPolicyConfiguration
            {
                PolicyConfigurationId = "configuration.provisional-trust",
                PolicyVersion = "provisional-trust.v1",
                Kind = InstitutionalPolicyKind.ProvisionalTrust,
                WorkReward = 54,
                AidEffectiveness = 80,
                DisclosureProtection = 90,
                RetaliationRisk = 15,
                AppealAccessibility = 85,
                DecisionVariationAmplitude = 0,
                ClaimantEvidenceWeightPercent = 100,
                ClinicalEvidenceWeightPercent = 100,
                PatternEvidenceWeightPercent = 100,
                WitnessEvidenceWeightPercent = 100,
                ManagementEvidenceWeightPercent = 100,
                ActionRecordWeightPercent = 100,
                InitialRecognitionThreshold = 120,
                ProvisionalRecognitionThreshold = 35,
                AppealRecognitionThreshold = 80,
                LaterRecognitionThreshold = 80,
                CitedHoldingWeight = 0,
                PermitProvisionalRecognition = true,
                ProvisionalReliefAmount = 60,
                EstablishAppellateHolding = false,
                AutoCiteMatchingHoldings = false,
                HoldingReach = PrecedentReach.Individual,
                HoldingIsRetrospective = false,
            };
        }

        public static InstitutionalPolicyConfiguration PrecedentMachine()
        {
            return new InstitutionalPolicyConfiguration
            {
                PolicyConfigurationId = "configuration.precedent-machine",
                PolicyVersion = "precedent-machine.v1",
                Kind = InstitutionalPolicyKind.PrecedentMachine,
                WorkReward = 54,
                AidEffectiveness = 48,
                DisclosureProtection = 82,
                RetaliationRisk = 18,
                AppealAccessibility = 90,
                DecisionVariationAmplitude = 0,
                ClaimantEvidenceWeightPercent = 100,
                ClinicalEvidenceWeightPercent = 100,
                PatternEvidenceWeightPercent = 100,
                WitnessEvidenceWeightPercent = 100,
                ManagementEvidenceWeightPercent = 25,
                ActionRecordWeightPercent = 100,
                InitialRecognitionThreshold = 120,
                ProvisionalRecognitionThreshold = 0,
                AppealRecognitionThreshold = 65,
                LaterRecognitionThreshold = 80,
                CitedHoldingWeight = 60,
                PermitProvisionalRecognition = false,
                ProvisionalReliefAmount = 0,
                EstablishAppellateHolding = true,
                AutoCiteMatchingHoldings = true,
                HoldingReach = PrecedentReach.Employer,
                HoldingIsRetrospective = true,
            };
        }
    }
}
