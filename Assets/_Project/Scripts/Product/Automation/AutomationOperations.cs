using System;
using Desk42.Institutional.Player;
using UnityEngine;

namespace Desk42.Product.Automation
{
    internal enum AutomationUrgency
    {
        Routine,
        Urgent,
    }

    [Flags]
    internal enum AutomationEvidenceNeed
    {
        None = 0,
        Identity = 1 << 0,
        OfficialRecord = 1 << 1,
        Witness = 1 << 2,
        ChainOfCustody = 1 << 3,
    }

    internal enum AutomationRoutePriority
    {
        Balanced = 1,
        UrgentFirst = 2,
        DeadlineFirst = 3,
    }

    internal enum AutomationAppealMode
    {
        FullRehearing = 1,
        FastTrack = 2,
        Settlement = 3,
    }

    internal enum AutomationUpgradeKind
    {
        Throughput,
        Capacity,
        Reliability,
    }

    internal enum AutomationProcedureKind
    {
        MandatorySecondaryVerification = 1,
        PresumptionOfValidity = 2,
        AutomaticAdverseReview = 3,
        ProtectedEvidenceChannel = 4,
        AppealFastTrack = 5,
        PrecedentReuse = 6,
    }

    internal static class AutomationProcedureNames
    {
        internal static string ShortName(AutomationProcedureKind kind)
        {
            return kind switch
            {
                AutomationProcedureKind.MandatorySecondaryVerification => "SECOND CHECK",
                AutomationProcedureKind.PresumptionOfValidity => "PRESUME VALID",
                AutomationProcedureKind.AutomaticAdverseReview => "ADVERSE REVIEW",
                AutomationProcedureKind.ProtectedEvidenceChannel => "PROTECTED LANE",
                AutomationProcedureKind.AppealFastTrack => "APPEAL FAST TRACK",
                AutomationProcedureKind.PrecedentReuse => "PRECEDENT REUSE",
                _ => "UNKNOWN",
            };
        }

        internal static string Effect(AutomationProcedureKind kind)
        {
            return kind switch
            {
                AutomationProcedureKind.MandatorySecondaryVerification =>
                    "Two physical verification passes: slower, safer, more machine load.",
                AutomationProcedureKind.PresumptionOfValidity =>
                    "Verification work -34%; machine fault risk +65%.",
                AutomationProcedureKind.AutomaticAdverseReview =>
                    "Overdue dossiers automatically detour through Legal before ruling.",
                AutomationProcedureKind.ProtectedEvidenceChannel =>
                    "Chain-of-custody packets reserve the auxiliary lane; fault risk -48%.",
                AutomationProcedureKind.AppealFastTrack =>
                    "Appeals skip full re-verification and bind the fast-track route.",
                AutomationProcedureKind.PrecedentReuse =>
                    "Each installed precedent accelerates verification of later claims.",
                _ => string.Empty,
            };
        }
    }

    /// <summary>
    /// Factory-facing work characteristics derived only from the public claim projection.
    /// These values never infer or expose the authoritative lived event.
    /// </summary>
    internal sealed class AutomationClaimProfile
    {
        private AutomationClaimProfile(
            AutomationUrgency urgency,
            AutomationEvidenceNeed evidenceNeeds,
            float deadlineSeconds,
            float verificationWork)
        {
            Urgency = urgency;
            EvidenceNeeds = evidenceNeeds;
            DeadlineSeconds = deadlineSeconds;
            VerificationWork = verificationWork;
        }

        internal AutomationUrgency Urgency { get; }
        internal AutomationEvidenceNeed EvidenceNeeds { get; }
        internal float DeadlineSeconds { get; }
        internal float VerificationWork { get; }
        internal int EvidenceNeedCount => CountFlags(EvidenceNeeds);

        internal static AutomationClaimProfile Create(
            AutomationPublicClaim claim,
            int shiftOrdinal)
        {
            if (claim == null) throw new ArgumentNullException(nameof(claim));

            int pressureKey = claim.BatchOrdinal + shiftOrdinal * 3;
            AutomationEvidenceNeed needs = AutomationEvidenceNeed.Identity;
            if ((claim.OfficialFactCount > 0 || claim.EvidencePacketCount > 1) &&
                pressureKey % 2 == 0)
                needs |= AutomationEvidenceNeed.OfficialRecord;
            if (claim.AllegationCount > 0 && pressureKey % 3 != 0)
                needs |= AutomationEvidenceNeed.Witness;
            if ((claim.MissingEvidenceCount > 0 && pressureKey % 4 != 1) ||
                claim.CitableEvidenceCount == 0)
                needs |= AutomationEvidenceNeed.ChainOfCustody;

            bool urgent = claim.MissingEvidenceCount > 1 || pressureKey % 5 == 0;
            float deadline = urgent
                ? 42f + pressureKey % 4 * 4f
                : 88f + pressureKey % 5 * 7f;
            float verificationWork = 0.72f + CountFlags(needs) * 0.28f +
                Mathf.Clamp(claim.EvidencePacketCount - 1, 0, 4) * 0.09f;

            return new AutomationClaimProfile(
                urgent ? AutomationUrgency.Urgent : AutomationUrgency.Routine,
                needs,
                deadline,
                verificationWork);
        }

        internal static AutomationClaimProfile ForAppeal(
            AutomationAppealPacket appeal)
        {
            if (appeal == null) throw new ArgumentNullException(nameof(appeal));
            AutomationEvidenceNeed needs = AutomationEvidenceNeed.OfficialRecord |
                AutomationEvidenceNeed.ChainOfCustody;
            if (appeal.MissingEvidenceCount > 0)
                needs |= AutomationEvidenceNeed.Witness;
            return new AutomationClaimProfile(
                AutomationUrgency.Urgent,
                needs,
                48f,
                1.05f + appeal.EvidencePacketCount * 0.08f);
        }

        private static int CountFlags(AutomationEvidenceNeed value)
        {
            int count = 0;
            int bits = (int)value;
            while (bits != 0)
            {
                count += bits & 1;
                bits >>= 1;
            }
            return count;
        }
    }
}
