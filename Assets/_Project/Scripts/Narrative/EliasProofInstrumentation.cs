using System;

namespace Desk42.Core
{
    public enum EliasCompromisedToolState
    {
        None = 0,
        RequestClarificationLocked = 1,
        ReferForReviewLocked = 2,
    }

    /// <summary>
    /// Read-only factual record for one five-shift proof run. The test ID is
    /// opaque, the claimant identity is stable, and every later field comes
    /// from the same state that drove scheduling and execution.
    /// </summary>
    [Serializable]
    public sealed class EliasProofRunRecord
    {
        public string AnonymisedTestId { get; private set; }
        public string EliasStableId { get; private set; }
        public EliasShift1Disposition Shift1Disposition { get; private set; }
        public int Shift1VisitCount { get; private set; }
        public int Shift2VisitCount { get; private set; }
        public EliasShift2Branch Shift2Branch { get; private set; }
        public string Shift2ReceiptId { get; private set; }
        public float ComplianceStreakDelta { get; private set; }
        public float AuditRiskDelta { get; private set; }
        public ClaimResolutionKind Shift2Resolution { get; private set; }
        public int Shift5VisitCount { get; private set; }
        public EliasShift2Branch Shift5BranchLoaded { get; private set; }
        public string Shift5LoadedClaimId { get; private set; }
        public EliasCompromisedToolState CompromisedToolState
            { get; private set; }
        public string Shift5ReceiptId { get; private set; }
        public ClaimResolutionKind Shift5Resolution { get; private set; }
        public string TemporaryModifierApplied { get; private set; }

        internal static EliasProofRunRecord Capture(
            EliasProofSessionState state)
        {
            if (state?.IsActive != true)
            {
                throw new InvalidOperationException(
                    "An active Elias proof session is required.");
            }

            return new EliasProofRunRecord
            {
                AnonymisedTestId = state.ProofSessionId,
                EliasStableId = EliasProofContent.CanonicalClaimantId,
                Shift1Disposition = state.Shift1Disposition,
                Shift1VisitCount = VisitCount(
                    state, EliasProofContent.Shift1AppearanceKey, 1),
                Shift2VisitCount = VisitCount(
                    state, EliasProofContent.Shift2AppearanceKey, 2),
                Shift2Branch = state.Shift2Branch,
                Shift2ReceiptId = state.Shift2ProcedureReceiptId,
                ComplianceStreakDelta =
                    state.Shift2ComplianceStreakDelta,
                AuditRiskDelta = state.Shift2AuditRiskDelta,
                Shift2Resolution = state.Shift2FinalDisposition,
                Shift5VisitCount = VisitCount(
                    state, EliasProofContent.Shift5AppearanceKey, 3),
                Shift5BranchLoaded = state.Shift2Branch,
                Shift5LoadedClaimId = state.Shift5LoadedClaimId,
                CompromisedToolState = CompromisedToolFor(
                    state.Shift2Branch),
                Shift5ReceiptId = state.Shift5ProcedureReceiptId,
                Shift5Resolution = state.Shift5FinalDisposition,
                TemporaryModifierApplied =
                    state.ActiveAftermathModifier?.ModifierId,
            };
        }

        public override string ToString()
            => "[EliasProofRun] " +
               $"testId={AnonymisedTestId} " +
               $"stableId={EliasStableId} " +
               $"shift1={Shift1Disposition}/visit{Shift1VisitCount} " +
               $"shift2=visit{Shift2VisitCount}/{Shift2Branch}/" +
               $"{Shift2ReceiptId}/{Shift2Resolution} " +
               $"streakDelta={ComplianceStreakDelta:+0.##;-0.##;0} " +
               $"auditRiskDelta={AuditRiskDelta:+0.##;-0.##;0} " +
               $"shift5=visit{Shift5VisitCount}/{Shift5BranchLoaded}/" +
               $"{Shift5LoadedClaimId}/{CompromisedToolState}/" +
               $"{Shift5ReceiptId}/{Shift5Resolution} " +
               $"modifier={TemporaryModifierApplied}";

        private static int VisitCount(
            EliasProofSessionState state,
            string appearanceKey,
            int visitNumber)
            => state.RecordedAppearanceKeys.Contains(appearanceKey)
                ? visitNumber
                : 0;

        private static EliasCompromisedToolState CompromisedToolFor(
            EliasShift2Branch branch)
            => branch switch
            {
                EliasShift2Branch.LegacyException =>
                    EliasCompromisedToolState
                        .RequestClarificationLocked,
                EliasShift2Branch.PhysicalVerification =>
                    EliasCompromisedToolState.ReferForReviewLocked,
                _ => EliasCompromisedToolState.None,
            };
    }
}
