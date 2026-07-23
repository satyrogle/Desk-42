using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Desk42.Core
{
    public enum EliasShift1Disposition
    {
        None = 0,
        Approved = 1,
        Denied = 2,
    }

    public enum EliasShift2Branch
    {
        None = 0,
        NormalisedAddress = 1,
        LegacyException = 2,
        PhysicalVerification = 3,
    }

    public readonly struct EliasVisitTransaction
    {
        public readonly string ProofSessionId;
        public readonly string AppearanceKey;
        public readonly string StableClaimantId;
        public readonly int PriorVisits;
        public readonly int CurrentVisitNumber;
        public readonly bool WasNewlyRecorded;

        public EliasVisitTransaction(string proofSessionId,
            string appearanceKey, string stableClaimantId,
            int priorVisits, bool wasNewlyRecorded)
        {
            ProofSessionId = proofSessionId;
            AppearanceKey = appearanceKey;
            StableClaimantId = stableClaimantId;
            PriorVisits = priorVisits;
            CurrentVisitNumber = priorVisits + 1;
            WasNewlyRecorded = wasNewlyRecorded;
        }
    }

    /// <summary>
    /// Exact-once aftermath routing for authored proof claims. This is session
    /// data, not a generic condition or recurring-character system.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EliasAftermathModifierState
    {
        [JsonProperty]
        public string ModifierId { get; internal set; }

        [JsonProperty]
        public HashSet<string> PendingClaimIds { get; private set; } = new();

        [JsonProperty]
        public HashSet<string> AppliedClaimIds { get; private set; } = new();

        [JsonIgnore]
        public bool IsActive => !string.IsNullOrWhiteSpace(ModifierId);
    }

    /// <summary>
    /// Canonical state for one five-shift Elias proof session. It deliberately
    /// lives outside RunData and MetaProgressData: a normal shift remains one
    /// fresh RunData, while this record survives those run boundaries only for
    /// the lifetime of the persistent GameManager.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EliasProofSessionState
    {
        [JsonProperty]
        public string ProofSessionId { get; internal set; }

        [JsonProperty]
        public EliasShift1Disposition Shift1Disposition
            { get; internal set; } = EliasShift1Disposition.None;

        [JsonProperty]
        public EliasShift2Branch Shift2Branch
            { get; internal set; } = EliasShift2Branch.None;

        [JsonProperty]
        public ClaimResolutionKind Shift2FinalDisposition
            { get; internal set; } = ClaimResolutionKind.Unspecified;

        [JsonProperty]
        public EliasProcedureActionId Shift2ProcedureAction
            { get; internal set; } = EliasProcedureActionId.Unspecified;

        [JsonProperty]
        public string Shift2ProcedureReceiptId
            { get; internal set; }

        [JsonProperty]
        public EliasProcedureActionId Shift5ProcedureAction
            { get; internal set; } = EliasProcedureActionId.Unspecified;

        [JsonProperty]
        public string Shift5ProcedureReceiptId
            { get; internal set; }

        [JsonProperty]
        public HashSet<string> RecordedAppearanceKeys
            { get; private set; } = new();

        [JsonProperty]
        public HashSet<string> AppliedProcedureAppearanceKeys
            { get; private set; } = new();

        [JsonProperty]
        public EliasAftermathModifierState ActiveAftermathModifier
            { get; internal set; } = new();

        [JsonIgnore]
        public bool IsActive => !string.IsNullOrWhiteSpace(ProofSessionId);

        internal static EliasProofSessionState Create(string proofSessionId)
            => new()
            {
                ProofSessionId = proofSessionId,
            };
    }
}
