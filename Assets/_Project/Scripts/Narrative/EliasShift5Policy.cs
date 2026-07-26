// ============================================================
// DESK 42 — Shift 5 branch state (the no-clean-out mechanic)
//
// Derives the Shift 5 record state from the PERSISTED Shift 2 branch.
// Never from claimant name, display text, or the Shift 5 claim id.
//
// The Bureau is functioning correctly in all three branches. None of
// these states is a bug or corruption: the harm in 5A happens precisely
// because a clean, internally consistent classification is machine-
// actionable and the system acts on it.
//
//   5A NormalisedAddress   clean authoritative record
//                          -> Bureau CAN act -> dependent action ACTIVE
//   5B LegacyException     legacy 18B retained
//                          -> Bureau cannot execute -> NOT AUTHORISED
//   5C PhysicalVerification classification unresolved
//                          -> consequence HELD
//
// The player's Shift 5 disposition CANNOT reverse the Shift 2
// registration. Approve processes the current claim under a valid 18A
// and the dependent action remains active; Deny refuses the current
// claim and the dependent action still remains active, because denial
// is not a reversal of the earlier record procedure. There is no third
// "undo Shift 2" option, and this type deliberately exposes no API that
// could become one.
// ============================================================

using System;

namespace Desk42.Core
{
    /// <summary>Downstream Bureau action state at Shift 5.</summary>
    public enum EliasDependentAction
    {
        /// <summary>No branch established — Shift 5 state is undefined.</summary>
        None = 0,

        /// <summary>5A — the record was actionable and the Bureau acted.</summary>
        Active = 1,

        /// <summary>5B — the legacy exception blocks automated execution.</summary>
        NotAuthorised = 2,

        /// <summary>5C — verification unresolved, consequence withheld.</summary>
        Held = 3,
    }

    /// <summary>Record classification presented at Shift 5.</summary>
    public enum EliasRecordClassification
    {
        None = 0,
        /// <summary>5A — amended, authoritative.</summary>
        Registered18A = 1,
        /// <summary>5B — legacy retained under exception.</summary>
        Legacy18B = 2,
        /// <summary>5C — referred, not final.</summary>
        NotFinal = 3,
    }

    /// <summary>
    /// The facts the Shift 5 desk shows, resolved from persisted proof state.
    /// Immutable: the Shift 5 disposition does not and cannot change it.
    /// </summary>
    public readonly struct EliasShift5State
    {
        public readonly EliasShift2Branch Branch;
        public readonly EliasRecordClassification Classification;
        public readonly EliasDependentAction DependentAction;

        /// <summary>The record the classification is attributed to.</summary>
        public readonly string SourceRecord;

        /// <summary>True only when the classification is final and valid.</summary>
        public readonly bool RecordIsValid;

        /// <summary>
        /// Always false. Reversal authority does not exist at this desk in any
        /// branch — this is the mechanical statement of "no clean out".
        /// </summary>
        public bool ReversalAvailableAtThisDesk => false;

        public bool IsResolved => Branch != EliasShift2Branch.None;

        internal EliasShift5State(
            EliasShift2Branch branch,
            EliasRecordClassification classification,
            EliasDependentAction dependentAction,
            string sourceRecord,
            bool recordIsValid)
        {
            Branch          = branch;
            Classification  = classification;
            DependentAction = dependentAction;
            SourceRecord    = sourceRecord;
            RecordIsValid   = recordIsValid;
        }
    }

    /// <summary>
    /// Resolves Shift 5 state from the persisted Shift 2 branch. Pure.
    /// </summary>
    public static class EliasShift5Policy
    {
        public const string SourceRecordLabel = "M. VENN";

        /// <summary>
        /// Derives Shift 5 state from persisted proof state.
        /// Throws on a null state rather than inventing a default, so a lost
        /// causal chain surfaces loudly instead of silently presenting 5B.
        /// </summary>
        public static EliasShift5State Resolve(EliasProofSessionState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return ForBranch(state.Shift2Branch);
        }

        /// <summary>Branch-only overload for fixtures and presentation.</summary>
        public static EliasShift5State ForBranch(EliasShift2Branch branch)
            => branch switch
            {
                EliasShift2Branch.NormalisedAddress => new EliasShift5State(
                    branch,
                    EliasRecordClassification.Registered18A,
                    EliasDependentAction.Active,
                    SourceRecordLabel,
                    recordIsValid: true),

                EliasShift2Branch.LegacyException => new EliasShift5State(
                    branch,
                    EliasRecordClassification.Legacy18B,
                    EliasDependentAction.NotAuthorised,
                    SourceRecordLabel,
                    recordIsValid: false),

                EliasShift2Branch.PhysicalVerification => new EliasShift5State(
                    branch,
                    EliasRecordClassification.NotFinal,
                    EliasDependentAction.Held,
                    SourceRecordLabel,
                    recordIsValid: false),

                _ => new EliasShift5State(
                    EliasShift2Branch.None,
                    EliasRecordClassification.None,
                    EliasDependentAction.None,
                    null,
                    recordIsValid: false),
            };

        /// <summary>
        /// The dependent-action state AFTER a Shift 5 disposition.
        ///
        /// Deliberately returns the state unchanged for every disposition.
        /// Approving processes the current claim under a valid classification;
        /// denying refuses the current claim. Neither reverses the Shift 2
        /// record procedure, so neither clears the downstream consequence.
        /// </summary>
        public static EliasDependentAction DependentActionAfter(
            EliasShift5State state, ClaimResolutionKind disposition)
        {
            _ = disposition; // no disposition reverses the earlier registration
            return state.DependentAction;
        }
    }
}
