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
        BiometricContinuity = 1 << 4,
        DependencyProof = 1 << 5,
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
        BurdenShift = 7,
        AnonymousDisclosure = 8,
        EmergencyRelief = 9,
        EmployerSelfCertification = 10,
        IndependentVerification = 11,
        NarrowPrecedent = 12,
        RetrospectiveReview = 13,
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
                AutomationProcedureKind.BurdenShift => "BURDEN SHIFT",
                AutomationProcedureKind.AnonymousDisclosure => "ANON DISCLOSURE",
                AutomationProcedureKind.EmergencyRelief => "EMERGENCY RELIEF",
                AutomationProcedureKind.EmployerSelfCertification => "SELF CERTIFY",
                AutomationProcedureKind.IndependentVerification => "INDEPENDENT CHECK",
                AutomationProcedureKind.NarrowPrecedent => "NARROW PRECEDENT",
                AutomationProcedureKind.RetrospectiveReview => "RETRO REVIEW",
                _ => "UNKNOWN",
            };
        }

        internal static string Effect(AutomationProcedureKind kind)
        {
            return Effect(kind, 1);
        }

        internal static string Effect(AutomationProcedureKind kind, int tier)
        {
            tier = Mathf.Clamp(tier, 1, 3);
            return kind switch
            {
                AutomationProcedureKind.MandatorySecondaryVerification =>
                    tier == 1
                        ? "Two physical verification passes: slower, safer, more machine load."
                        : tier == 2
                            ? "Urgent second checks gain deadline grace and reserve the faster verifier."
                            : "Verified issue families create reusable same-issue scan patterns.",
                AutomationProcedureKind.PresumptionOfValidity =>
                    tier == 1
                        ? "Verification work -34%; machine fault risk +65%."
                        : tier == 2
                            ? "Routine claims bypass one evidence need; weak recognition exposure rises."
                            : "Recognised claimants gain trust; accelerated verification runs hotter.",
                AutomationProcedureKind.AutomaticAdverseReview =>
                    tier == 1
                        ? "Overdue dossiers automatically detour through Legal before ruling."
                        : tier == 2
                            ? "Legal review pauses the deadline but adds Legal heat."
                            : "Repeated reviews accelerate later adverse files and increase liability scrutiny.",
                AutomationProcedureKind.ProtectedEvidenceChannel =>
                    tier == 1
                        ? "Chain-of-custody packets reserve the auxiliary lane; fault risk -48%."
                        : tier == 2
                            ? "Protected evidence suppresses retaliation pulses and slows intake release."
                            : "Protected access files trigger restoration review and consume Legal capacity.",
                AutomationProcedureKind.AppealFastTrack =>
                    tier == 1
                        ? "Appeals skip full re-verification and bind the fast-track route."
                        : tier == 2
                            ? "Fast-track appeals produce less Legal heat but weaker holdings."
                            : "Upheld fast-track appeals grant credit and increase precedent exposure.",
                AutomationProcedureKind.PrecedentReuse =>
                    tier == 1
                        ? "Permitted real holdings accelerate matching later claims."
                        : tier == 2
                            ? "Matching holdings also reduce evidence work and increase citation exposure."
                            : "Compatible holdings synthesise broader automation with conflict risk.",
                AutomationProcedureKind.BurdenShift =>
                    "Lower claimant evidence burden; more weak files reach Legal and appeal.",
                AutomationProcedureKind.AnonymousDisclosure =>
                    "Witness packets gain protection and speed; source confidence and review load fall apart.",
                AutomationProcedureKind.EmergencyRelief =>
                    "Urgent dependency files receive provisional relief before final review; creates reliance exposure.",
                AutomationProcedureKind.EmployerSelfCertification =>
                    "Official-record work is bypassed; throughput rises with rework and appeal risk.",
                AutomationProcedureKind.IndependentVerification =>
                    "A separate physical check reduces fault risk while adding heat and queue load.",
                AutomationProcedureKind.NarrowPrecedent =>
                    "Holdings bind the claimant only; conflicts fall and reuse opportunities shrink.",
                AutomationProcedureKind.RetrospectiveReview =>
                    "Weak completed rulings return through Legal; liability falls while the floor floods with rework.",
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
            float verificationWork,
            string issueId,
            int linkedDossierCount,
            bool descendant)
        {
            Urgency = urgency;
            EvidenceNeeds = evidenceNeeds;
            DeadlineSeconds = deadlineSeconds;
            VerificationWork = verificationWork;
            IssueId = issueId ?? string.Empty;
            LinkedDossierCount = Mathf.Max(1, linkedDossierCount);
            Descendant = descendant;
        }

        internal AutomationUrgency Urgency { get; }
        internal AutomationEvidenceNeed EvidenceNeeds { get; }
        internal float DeadlineSeconds { get; }
        internal float VerificationWork { get; }
        internal string IssueId { get; }
        internal int LinkedDossierCount { get; }
        internal bool Descendant { get; }
        internal int EvidenceNeedCount => CountFlags(EvidenceNeeds);

        internal static AutomationClaimProfile Create(
            AutomationPublicClaim claim,
            int shiftOrdinal)
        {
            if (claim == null) throw new ArgumentNullException(nameof(claim));

            int pressureKey = claim.BatchOrdinal + shiftOrdinal * 3;
            bool collective = string.Equals(
                claim.IssueId, AutomationIssueIds.Collective,
                StringComparison.Ordinal);
            bool access = string.Equals(
                claim.IssueId, AutomationIssueIds.Access,
                StringComparison.Ordinal);
            bool identity = string.Equals(
                claim.IssueId, AutomationIssueIds.Identity,
                StringComparison.Ordinal);
            bool dependency = string.Equals(
                claim.IssueId, AutomationIssueIds.Dependency,
                StringComparison.Ordinal);
            AutomationEvidenceNeed needs = AutomationEvidenceNeed.Identity;
            if ((claim.OfficialFactCount > 0 || claim.EvidencePacketCount > 1) &&
                pressureKey % 2 == 0)
                needs |= AutomationEvidenceNeed.OfficialRecord;
            if (claim.AllegationCount > 0 && pressureKey % 3 != 0)
                needs |= AutomationEvidenceNeed.Witness;
            if ((claim.MissingEvidenceCount > 0 && pressureKey % 4 != 1) ||
                claim.CitableEvidenceCount == 0)
                needs |= AutomationEvidenceNeed.ChainOfCustody;
            if (collective)
                needs |= AutomationEvidenceNeed.OfficialRecord |
                    AutomationEvidenceNeed.Witness |
                    AutomationEvidenceNeed.ChainOfCustody;
            if (access)
                needs |= AutomationEvidenceNeed.OfficialRecord |
                    AutomationEvidenceNeed.Witness;
            if (identity)
                needs |= AutomationEvidenceNeed.OfficialRecord |
                    AutomationEvidenceNeed.ChainOfCustody |
                    AutomationEvidenceNeed.BiometricContinuity;
            if (dependency)
                needs |= AutomationEvidenceNeed.Witness |
                    AutomationEvidenceNeed.ChainOfCustody |
                    AutomationEvidenceNeed.DependencyProof;

            bool urgent = dependency || claim.EvidenceSupportMinimum < 25 ||
                pressureKey % 5 == 0;
            float deadline = urgent
                ? 42f + pressureKey % 4 * 4f
                : 88f + pressureKey % 5 * 7f;
            float verificationWork = 0.72f + CountFlags(needs) * 0.28f +
                Mathf.Clamp(claim.EvidencePacketCount - 1, 0, 4) * 0.09f;
            if (collective) verificationWork += 0.55f;
            if (identity) verificationWork += 0.38f;
            if (dependency) verificationWork += 0.46f;

            return new AutomationClaimProfile(
                urgent ? AutomationUrgency.Urgent : AutomationUrgency.Routine,
                needs,
                deadline,
                verificationWork,
                claim.IssueId,
                collective ? Mathf.Max(2, claim.Parties.Count) : 1,
                !string.IsNullOrWhiteSpace(claim.ParentCaseId));
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
                1.05f + appeal.EvidencePacketCount * 0.08f,
                "appeal",
                1,
                true);
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
