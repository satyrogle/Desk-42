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
    }
}
