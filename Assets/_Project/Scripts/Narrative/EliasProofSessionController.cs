using System;
using UnityEngine;

namespace Desk42.Core
{
    /// <summary>
    /// Owns the validation spine across five fresh RunData instances. The
    /// controller is a child of the DontDestroyOnLoad GameManager and has no
    /// disk-save hook: validation proof state is intentionally session-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EliasProofSessionController : MonoBehaviour
    {
        [SerializeField]
        private EliasProofSessionState _state = new();

        public EliasProofSessionState State
            => _state ??= new EliasProofSessionState();

        public bool HasActiveSession => State.IsActive;

        /// <summary>
        /// Captures prior visits, records the authored appearance once, then
        /// exposes both the BSM input and the human-facing visit number.
        /// Replaying the same appearance after reconstruction returns the same
        /// transaction without advancing the session.
        /// </summary>
        public EliasVisitTransaction RecordAppearance(
            string stableClaimantId, string appearanceKey)
        {
            if (!HasActiveSession)
                throw new InvalidOperationException(
                    "An active Elias proof session is required.");
            if (!string.Equals(stableClaimantId,
                    EliasProofContent.CanonicalClaimantId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Elias must use stable claimant ID " +
                    $"'{EliasProofContent.CanonicalClaimantId}', received " +
                    $"'{stableClaimantId}'.");
            }
            if (!EliasProofContent.TryGetExpectedPriorVisits(
                    appearanceKey, out int expectedPriorVisits))
            {
                throw new ArgumentException(
                    $"Unknown Elias appearance key '{appearanceKey}'.",
                    nameof(appearanceKey));
            }

            bool alreadyRecorded =
                State.RecordedAppearanceKeys.Contains(appearanceKey);
            if (!alreadyRecorded)
            {
                AssertPrecedingAppearances(expectedPriorVisits);
                State.RecordedAppearanceKeys.Add(appearanceKey);
            }

            var transaction = new EliasVisitTransaction(
                State.ProofSessionId,
                appearanceKey,
                stableClaimantId,
                expectedPriorVisits,
                !alreadyRecorded);
            Debug.Log(
                $"[EliasProof] appearance={appearanceKey} " +
                $"priorVisits={transaction.PriorVisits} " +
                $"currentVisit={transaction.CurrentVisitNumber} " +
                $"recordedNow={transaction.WasNewlyRecorded}.");
            return transaction;
        }

        /// <summary>
        /// Starts from a clean record even if a previous proof was active.
        /// Supplying an ID keeps automated routes deterministic; runtime proof
        /// sessions receive a new opaque ID.
        /// </summary>
        public EliasProofSessionState BeginProofSession(
            string proofSessionId = null)
        {
            string id = string.IsNullOrWhiteSpace(proofSessionId)
                ? Guid.NewGuid().ToString("N")
                : proofSessionId.Trim();
            _state = EliasProofSessionState.Create(id);
            Debug.Log($"[EliasProof] Session started: {id}.");
            return _state;
        }

        /// <summary>
        /// Explicitly removes all proof state. No aftermath or appearance key
        /// is allowed to survive this boundary.
        /// </summary>
        public void EndProofSession()
        {
            if (HasActiveSession)
                Debug.Log($"[EliasProof] Session ended: {State.ProofSessionId}.");
            _state = new EliasProofSessionState();
        }

        private void Awake()
        {
            _state ??= new EliasProofSessionState();
        }

        private void AssertPrecedingAppearances(int expectedPriorVisits)
        {
            bool shift1Recorded = State.RecordedAppearanceKeys.Contains(
                EliasProofContent.Shift1AppearanceKey);
            bool shift2Recorded = State.RecordedAppearanceKeys.Contains(
                EliasProofContent.Shift2AppearanceKey);
            bool valid = expectedPriorVisits switch
            {
                0 => !shift1Recorded && !shift2Recorded,
                1 => shift1Recorded && !shift2Recorded,
                2 => shift1Recorded && shift2Recorded,
                _ => false,
            };
            if (!valid)
            {
                throw new InvalidOperationException(
                    $"Elias appearance order is invalid for priorVisits=" +
                    $"{expectedPriorVisits}. Recorded keys: " +
                    $"{string.Join(", ", State.RecordedAppearanceKeys)}.");
            }
        }
    }
}
