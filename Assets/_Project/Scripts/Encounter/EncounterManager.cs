// ============================================================
// DESK 42 — Encounter Manager (MonoBehaviour)
//
// Orchestrates a single client encounter from start to finish.
//
// Flow:
//   1. ShiftManager dequeues a claim → publishes ClaimQueuedEvent.
//   2. EncounterManager receives it → spawns a ClientStateMachine
//      child, wires PunchCardMachine, fills the hand, updates views.
//   3. Player slams cards, interacts with the client.
//   4. Player presses Approve or Deny.
//   5. EncounterManager publishes ClaimResolvedEvent → ShiftManager
//      and RunStateController both react.
//   6. Cleanup: destroy client GO, clear views, await next claim.
//
// Credit formula (placeholder until ClaimTemplateData SOs exist):
//   Approve → BaseCredits + ShiftNumber × 2
//   Deny    → 0
//
// Soul cost for unethical decisions is published separately via
// MoralChoiceEvent by the moral dilemma system — not here.
// ============================================================

using UnityEngine;
using Desk42.Core;
using Desk42.BSM;

namespace Desk42.Encounter
{
    [DisallowMultipleComponent]
    public sealed class EncounterManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Dependencies")]
        [Tooltip("The PunchCardMachine in the Shift scene.")]
        [SerializeField] private RedTape.PunchCardMachine _punchCardMachine;

        [Tooltip("UI view showing the active client's identity and mood.")]
        [SerializeField] private UI.ClientView            _clientView;

        [Tooltip("UI view showing the current claim document.")]
        [SerializeField] private UI.ClaimPanelView        _claimPanel;

        [Tooltip("UI view showing the player's card hand.")]
        [SerializeField] private UI.CardHandView          _cardHandView;

        [Tooltip("Parent transform where the client child GO will be spawned.")]
        [SerializeField] private Transform                _clientAnchor;

        [Header("Config")]
        [Tooltip("Credits earned for an approved claim (before shift scaling).")]
        [SerializeField] private int _baseCreditsApprove = 10;

        // ── State ─────────────────────────────────────────────

        private ClientStateMachine _activeCSM;
        private ActiveClaimData    _activeClaim;
        private bool               _encounterActive;

        // ── RumorMill Subscriptions ───────────────────────────

        private void OnEnable()
        {
            RumorMill.OnClaimQueued += HandleClaimQueued;
        }

        private void OnDisable()
        {
            RumorMill.OnClaimQueued -= HandleClaimQueued;
        }

        // ── Claim Queued ──────────────────────────────────────

        private void HandleClaimQueued(ClaimQueuedEvent e)
        {
            _activeClaim = e.Claim;
            BeginEncounter(e.Claim);
        }

        private void BeginEncounter(ActiveClaimData claim)
        {
            _encounterActive = true;

            // Spawn a fresh ClientStateMachine on a child GameObject.
            // The CSM drives itself via its own Update() each frame.
            var clientGO = new GameObject($"Client_{claim.ClientVariantId}");
            clientGO.transform.SetParent(_clientAnchor, worldPositionStays: false);
            _activeCSM = clientGO.AddComponent<ClientStateMachine>();
            _activeCSM.Initialize(
                claim.ClientVariantId,
                claim.ClientSpeciesId,
                visitCount: 0,       // TODO: query RepeatOffenderDB from MetaProgressData
                counterTraits: null);

            // Wire the machine so card slams reach this client
            _punchCardMachine?.SetActiveClient(_activeCSM);

            // Fill hand from deck for this encounter
            var run = GameManager.Instance?.Run;
            if (run != null)
                run.Hand.FillFromDeck(run.Deck);

            // Notify views
            _claimPanel?.SetClaim(claim);
            _clientView?.SetClient(_activeCSM, claim.ClientSpeciesId, claim.ClientVariantId);
            _cardHandView?.Refresh();

            Debug.Log($"[EncounterManager] Encounter started: {claim.ClaimId} " +
                      $"({claim.ClientSpeciesId}). " +
                      $"Hand: {run?.Hand?.Count ?? 0} cards.");
        }

        // ── Resolution Buttons ────────────────────────────────

        /// <summary>Called by the Approve button in the Shift scene.</summary>
        public void Approve() => ResolveEncounter(resolvedCorrectly: true);

        /// <summary>Called by the Deny button in the Shift scene.</summary>
        public void Deny() => ResolveEncounter(resolvedCorrectly: false);

        private void ResolveEncounter(bool resolvedCorrectly)
        {
            if (!_encounterActive || _activeClaim == null) return;
            _encounterActive = false;

            var run     = GameManager.Instance?.Run;
            int credits = resolvedCorrectly
                ? _baseCreditsApprove + (run?.ShiftNumber ?? 1) * 2
                : 0;

            RumorMill.PublishDeferred(new ClaimResolvedEvent(
                _activeClaim.ClaimId,
                resolvedCorrectly,
                credits,
                soulCost: 0f,   // unethical soul cost via MoralChoiceEvent only
                _activeClaim.ClientVariantId,
                _activeClaim.ClientSpeciesId));

            Debug.Log($"[EncounterManager] Resolved '{_activeClaim.ClaimId}' — " +
                      $"{(resolvedCorrectly ? "APPROVE" : "DENY")}. Credits: +{credits}.");

            CleanupEncounter();
        }

        private void CleanupEncounter()
        {
            _punchCardMachine?.ClearActiveClient();
            _clientView?.Clear();
            _claimPanel?.Clear();

            if (_activeCSM != null)
            {
                Destroy(_activeCSM.gameObject);
                _activeCSM = null;
            }

            _activeClaim = null;
        }

        // ── Editor Helpers ────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [ContextMenu("Debug: Force Approve")]
        private void DebugForceApprove() => Approve();

        [ContextMenu("Debug: Force Deny")]
        private void DebugForceDeny() => Deny();
#endif
    }
}
