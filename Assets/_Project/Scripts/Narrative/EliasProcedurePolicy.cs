using System;

namespace Desk42.Core
{
    public enum EliasProcedureActionId
    {
        Unspecified = 0,
        AmendRecord = 1,
        RetainLegacyUnit = 2,
        ReferForReview = 3,
        RequestClarification = 4,
    }

    public enum EliasProcedureFailureReason
    {
        None = 0,
        NoActiveProofSession = 1,
        NoActiveRun = 2,
        InvalidClaimantIdentity = 3,
        UnknownAppearance = 4,
        AppearanceNotRecorded = 5,
        ActionUnavailableForAppearance = 6,
        ProcedureAlreadyApplied = 7,
        BranchAlreadyWritten = 8,
        BranchNotEstablished = 9,
        ToolLockedByBranch = 10,
    }

    /// <summary>
    /// One proof-specific validator for procedure preview and execution.
    /// This is intentionally not a generic authored-action framework.
    /// </summary>
    public static class EliasProcedurePolicy
    {
        public const string OriginalAddress = "18B Calder House";
        public const string AmendedAddress = "18A Calder House";
        public const string MiriamRegisteredAt18A =
            "M. VENN - REGISTERED 18A";

        public static ProjectedEliasProcedure Preview(
            EliasProofSessionState state,
            RunStateController run,
            string stableClaimantId,
            string appearanceKey,
            EliasProcedureActionId actionId)
        {
            EliasProcedureFailureReason failure = ValidateContext(
                state, run, stableClaimantId, appearanceKey, actionId,
                out int priorVisits);
            if (failure != EliasProcedureFailureReason.None)
            {
                return Unavailable(state, stableClaimantId, appearanceKey,
                    actionId, priorVisits, failure);
            }

            EliasShift2Branch branchToWrite = EliasShift2Branch.None;
            EliasShift2Branch resultingBranch = state.Shift2Branch;
            int requestedCreditsDelta = 0;
            float requestedSanityDelta = 0f;
            float requestedSoulDelta = 0f;
            float requestedStreakDelta = 0f;
            string addressBefore = AddressForBranch(state.Shift2Branch);
            string addressAfter = addressBefore;
            string miriamReference = null;
            string receiptId;

            if (string.Equals(appearanceKey,
                    EliasProofContent.Shift2AppearanceKey,
                    StringComparison.Ordinal))
            {
                switch (actionId)
                {
                    case EliasProcedureActionId.AmendRecord:
                        branchToWrite =
                            EliasShift2Branch.NormalisedAddress;
                        requestedStreakDelta = 1f;
                        addressBefore = OriginalAddress;
                        addressAfter = AmendedAddress;
                        miriamReference = MiriamRegisteredAt18A;
                        receiptId = "elias_shift2_amend_record";
                        break;
                    case EliasProcedureActionId.RetainLegacyUnit:
                        branchToWrite =
                            EliasShift2Branch.LegacyException;
                        addressBefore = OriginalAddress;
                        addressAfter = OriginalAddress;
                        receiptId = "elias_shift2_retain_legacy_unit";
                        break;
                    case EliasProcedureActionId.ReferForReview:
                        branchToWrite =
                            EliasShift2Branch.PhysicalVerification;
                        addressBefore = OriginalAddress;
                        addressAfter = OriginalAddress;
                        receiptId = "elias_shift2_refer_for_review";
                        break;
                    default:
                        return Unavailable(state, stableClaimantId,
                            appearanceKey, actionId, priorVisits,
                            EliasProcedureFailureReason
                                .ActionUnavailableForAppearance);
                }

                resultingBranch = branchToWrite;
            }
            else if (string.Equals(appearanceKey,
                         EliasProofContent.Shift5AppearanceKey,
                         StringComparison.Ordinal))
            {
                if (state.Shift2Branch == EliasShift2Branch.None)
                {
                    return Unavailable(state, stableClaimantId,
                        appearanceKey, actionId, priorVisits,
                        EliasProcedureFailureReason.BranchNotEstablished);
                }

                // These are the two authored tools whose availability is
                // branch-sensitive in Shift 5. They never rewrite Shift 2.
                if (actionId == EliasProcedureActionId.RequestClarification)
                {
                    if (state.Shift2Branch
                        == EliasShift2Branch.LegacyException)
                    {
                        return Unavailable(state, stableClaimantId,
                            appearanceKey, actionId, priorVisits,
                            EliasProcedureFailureReason.ToolLockedByBranch);
                    }
                    receiptId = "elias_shift5_request_clarification";
                }
                else if (actionId == EliasProcedureActionId.ReferForReview)
                {
                    if (state.Shift2Branch
                        == EliasShift2Branch.PhysicalVerification)
                    {
                        return Unavailable(state, stableClaimantId,
                            appearanceKey, actionId, priorVisits,
                            EliasProcedureFailureReason.ToolLockedByBranch);
                    }
                    receiptId = "elias_shift5_refer_for_review";
                }
                else
                {
                    return Unavailable(state, stableClaimantId,
                        appearanceKey, actionId, priorVisits,
                        EliasProcedureFailureReason
                            .ActionUnavailableForAppearance);
                }
            }
            else
            {
                return Unavailable(state, stableClaimantId, appearanceKey,
                    actionId, priorVisits,
                    EliasProcedureFailureReason
                        .ActionUnavailableForAppearance);
            }

            return new ProjectedEliasProcedure(
                isAvailable: true,
                EliasProcedureFailureReason.None,
                state.ProofSessionId,
                appearanceKey,
                stableClaimantId,
                actionId,
                branchToWrite,
                resultingBranch,
                priorVisits,
                priorVisits + 1,
                run.ProjectCreditsDelta(requestedCreditsDelta),
                run.ProjectSanityDelta(requestedSanityDelta),
                run.ProjectSoulIntegrityDelta(requestedSoulDelta),
                run.ProjectComplianceStreakDelta(requestedStreakDelta),
                addressBefore,
                addressAfter,
                miriamReference,
                receiptId,
                requestedCreditsDelta,
                requestedSanityDelta,
                requestedSoulDelta,
                requestedStreakDelta);
        }

        private static EliasProcedureFailureReason ValidateContext(
            EliasProofSessionState state,
            RunStateController run,
            string stableClaimantId,
            string appearanceKey,
            EliasProcedureActionId actionId,
            out int priorVisits)
        {
            priorVisits = 0;
            if (state?.IsActive != true)
                return EliasProcedureFailureReason.NoActiveProofSession;
            if (run == null)
                return EliasProcedureFailureReason.NoActiveRun;
            if (!string.Equals(stableClaimantId,
                    EliasProofContent.CanonicalClaimantId,
                    StringComparison.Ordinal))
            {
                return EliasProcedureFailureReason
                    .InvalidClaimantIdentity;
            }
            if (!EliasProofContent.TryGetExpectedPriorVisits(
                    appearanceKey, out priorVisits))
            {
                return EliasProcedureFailureReason.UnknownAppearance;
            }
            if (!state.RecordedAppearanceKeys.Contains(appearanceKey))
                return EliasProcedureFailureReason.AppearanceNotRecorded;
            if (actionId == EliasProcedureActionId.Unspecified)
            {
                return EliasProcedureFailureReason
                    .ActionUnavailableForAppearance;
            }
            if (state.AppliedProcedureAppearanceKeys.Contains(appearanceKey))
                return EliasProcedureFailureReason.ProcedureAlreadyApplied;
            if (string.Equals(appearanceKey,
                    EliasProofContent.Shift2AppearanceKey,
                    StringComparison.Ordinal)
                && state.Shift2Branch != EliasShift2Branch.None)
            {
                return EliasProcedureFailureReason.BranchAlreadyWritten;
            }
            return EliasProcedureFailureReason.None;
        }

        private static ProjectedEliasProcedure Unavailable(
            EliasProofSessionState state,
            string stableClaimantId,
            string appearanceKey,
            EliasProcedureActionId actionId,
            int priorVisits,
            EliasProcedureFailureReason failureReason)
        {
            EliasShift2Branch branch =
                state?.Shift2Branch ?? EliasShift2Branch.None;
            string address = AddressForBranch(branch);
            return new ProjectedEliasProcedure(
                isAvailable: false,
                failureReason,
                state?.ProofSessionId,
                appearanceKey,
                stableClaimantId,
                actionId,
                EliasShift2Branch.None,
                branch,
                priorVisits,
                priorVisits + 1,
                0,
                0f,
                0f,
                0f,
                address,
                address,
                branch == EliasShift2Branch.NormalisedAddress
                    ? MiriamRegisteredAt18A
                    : null,
                null,
                0,
                0f,
                0f,
                0f);
        }

        private static string AddressForBranch(EliasShift2Branch branch)
            => branch == EliasShift2Branch.NormalisedAddress
                ? AmendedAddress
                : OriginalAddress;
    }
}
