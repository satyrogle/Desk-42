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
    public sealed class EncounterManager : RumorMillListener
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

        /// <summary>The client BSM for the in-progress encounter, if any.</summary>
        public ClientStateMachine ActiveClient => _activeCSM;

        // ── Lifecycle ─────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_punchCardMachine == null)
                Debug.LogError("[EncounterManager] _punchCardMachine not assigned.", this);
            if (_clientView == null)
                Debug.LogError("[EncounterManager] _clientView not assigned.", this);
            if (_claimPanel == null)
                Debug.LogError("[EncounterManager] _claimPanel not assigned.", this);
            if (_cardHandView == null)
                Debug.LogError("[EncounterManager] _cardHandView not assigned.", this);
            if (_clientAnchor == null)
                Debug.LogError("[EncounterManager] _clientAnchor not assigned.", this);
        }
#endif

        private void Awake()
        {
            Desk42Services.Register(this);
        }

        private void OnDestroy()
        {
            Desk42Services.Unregister(this);
        }

        protected override void Subscribe()   => RumorMill.OnClaimQueued += HandleClaimQueued;
        protected override void Unsubscribe() => RumorMill.OnClaimQueued -= HandleClaimQueued;

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

            // Query RepeatOffenderDB for visit history and counter-traits
            var meta = GameManager.Instance?.Meta;
            int visitCount = 0;
            System.Collections.Generic.List<string> counterTraits = null;
            if (meta != null)
            {
                var profile = meta.GetOrCreateProfile(claim.ClientVariantId);
                visitCount    = profile.TotalVisits;
                counterTraits = profile.CounterTraitIds;
            }

            _activeCSM.Initialize(
                claim.ClientVariantId,
                claim.ClientSpeciesId,
                visitCount,
                counterTraits);

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
        public void Approve() => ResolveEncounter(ClaimResolutionKind.Approve);

        /// <summary>Called by the Deny button in the Shift scene.</summary>
        public void Deny() => ResolveEncounter(ClaimResolutionKind.Deny);

        /// <summary>
        /// Layer 3 Dark Economy — drag the client into the Pneumatic
        /// Tube. Bypasses the claim with no payout, no soul cost, no
        /// dilemma. Grants Dark Intelligence. ~30% chance of an OSHA
        /// maintenance fee popup (handled by PneumaticTube itself).
        /// </summary>
        public void Liquify()
        {
            if (!_encounterActive || _activeClaim == null) return;

            var run = GameManager.Instance?.Run;
            if (run == null)
            {
                Debug.LogError("[EncounterManager] Cannot liquify without an active run.");
                return;
            }

            _encounterActive = false;

            // Liquify is an explicit policy outcome: it advances the shift
            // while preserving its existing zero-resource-cost behaviour. Its
            // Dark Intelligence gain is applied and reported through one result.
            var outcome = ClaimResolutionConsequencePolicy.Liquify();
            var applied = run.ApplyClaimResolution(
                outcome,
                _activeClaim.ClaimId,
                _activeClaim.ClientVariantId,
                _activeClaim.ClientSpeciesId);
            RumorMill.PublishDeferred(new ClaimResolvedEvent(applied));

            Debug.Log($"[EncounterManager] LIQUIFIED '{_activeClaim.ClaimId}'. +3 Dark Intel.");

            // Same race-free cleanup as ResolveEncounter — detach
            // state now, destroy GameObject after the animation delay.
            var resolvedCSM = _activeCSM;
            _activeCSM   = null;
            _activeClaim = null;
            _punchCardMachine?.ClearActiveClient();
            StartCoroutine(DestroyClientAfter(resolvedCSM, 0.8f));
        }

        private void ResolveEncounter(ClaimResolutionKind kind)
        {
            if (!_encounterActive || _activeClaim == null) return;

            // Resolving clears the machine and stops its coroutine. Reject the
            // click until OnSlamResolved so a same-frame decision cannot cancel
            // the card effect at its first yield.
            if (_punchCardMachine != null && _punchCardMachine.IsProcessing)
            {
                Debug.LogWarning("[EncounterManager] Finish processing the punch card before resolving.");
                return;
            }

            var run = GameManager.Instance?.Run;
            if (run == null)
            {
                Debug.LogError("[EncounterManager] Cannot resolve a claim without an active run.");
                return;
            }

            _encounterActive = false;

            var outcome = ClaimResolutionConsequencePolicy.Resolve(
                kind,
                _activeClaim,
                run?.ShiftNumber ?? 1,
                _baseCreditsApprove,
                ComplianceVowSystem.GetBasePayoutMultiplier());

            // Apply synchronously at the publish site. The deferred event below
            // is notification-only and cannot re-apply resources during flush.
            var applied = run.ApplyClaimResolution(
                outcome,
                _activeClaim.ClaimId,
                _activeClaim.ClientVariantId,
                _activeClaim.ClientSpeciesId);

            RumorMill.PublishDeferred(new ClaimResolvedEvent(applied));

            Debug.Log($"[EncounterManager] Resolved '{_activeClaim.ClaimId}' — " +
                      $"{applied.Kind}. Credits: {applied.CreditsDelta}, " +
                      $"Sanity: {applied.SanityDelta}, Soul: {applied.SoulIntegrityDelta}, " +
                      $"Dark Intel: {applied.DarkIntelligenceDelta}.");

            TryTriggerDilemma(run);

            var resolvedCSM = _activeCSM;
            _activeCSM   = null;
            _activeClaim = null;
            _punchCardMachine?.ClearActiveClient();
            StartCoroutine(DestroyClientAfter(resolvedCSM, 0.8f));
        }

        private static System.Collections.IEnumerator DestroyClientAfter(
            BSM.ClientStateMachine csm, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (csm != null) Destroy(csm.gameObject);
        }

        private void TryTriggerDilemma(RunStateController run)
        {
            if (!(GameManager.Instance?.IsPhaseUnlocked(3) ?? true)) return;

            if (run == null || run.MoralDilemmas == null || _activeClaim == null) return;

            var dilemma = run.MoralDilemmas.TryGenerateDilemma(
                _activeClaim.ClaimId,
                _activeClaim.ClaimantName ?? "Unknown",
                _activeClaim.ClaimAmount,
                run.SoulIntegrity,
                run.ShiftNumber);

            if (dilemma == null) return;

            var data = dilemma.Data;
            RumorMill.Publish(new DilemmaTriggeredEvent(
                prompt:           dilemma.BuiltPrompt,
                ethical:          data.EthicalChoiceLabel,
                bureaucratic:     data.BureaucraticChoiceLabel,
                onEthical:        () => ApplyDilemmaResolution(run,
                    run.MoralDilemmas.Resolve(dilemma, choseEthical: true)),
                onBureaucratic:   (inverted) => ApplyDilemmaResolution(run,
                    run.MoralDilemmas.Resolve(dilemma, choseEthical: false, inverted))));
        }

        private static void ApplyDilemmaResolution(RunStateController run,
            Desk42.MoralInjury.DilemmaResolutionResult result)
        {
            if (run == null || result == null) return;
            run.ModifyCredits(result.CreditDelta);
            run.ExtendTimer(result.TimeDelta);
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
