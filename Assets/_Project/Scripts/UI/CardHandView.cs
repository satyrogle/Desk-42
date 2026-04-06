// ============================================================
// DESK 42 — Card Hand View (MonoBehaviour)
//
// Renders the player's current hand as a row of clickable
// CardButtonViews. Rebuilds whenever Hand.OnHandChanged fires.
//
// Setup: assign _cardContainer (the layout group that holds
// the buttons), _cardButtonPrefab (a prefab with CardButtonView),
// and _machine (the PunchCardMachine in the scene).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;
using Desk42.Cards;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class CardHandView : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Tooltip("The PunchCardMachine to route slams to.")]
        [SerializeField] private RedTape.PunchCardMachine _machine;

        [Tooltip("Layout group that holds the card buttons.")]
        [SerializeField] private Transform _cardContainer;

        [Tooltip("Prefab with a CardButtonView component.")]
        [SerializeField] private CardButtonView _cardButtonPrefab;

        // ── State ─────────────────────────────────────────────

        private readonly List<CardButtonView> _activeButtons = new();
        private Hand _subscribedHand;

        // ── RumorMill subscriptions ───────────────────────────

        private void OnEnable()
        {
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;
            RumorMill.OnClaimQueued   += HandleClaimQueued;
        }

        private void OnDisable()
        {
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;
            RumorMill.OnClaimQueued   -= HandleClaimQueued;
            UnsubscribeHand();
        }

        // ── Hand subscription helpers ─────────────────────────

        private void SubscribeHand(Hand hand)
        {
            if (_subscribedHand == hand) return;
            UnsubscribeHand();
            _subscribedHand = hand;
            if (_subscribedHand != null)
                _subscribedHand.OnHandChanged += Rebuild;
        }

        private void UnsubscribeHand()
        {
            if (_subscribedHand != null)
                _subscribedHand.OnHandChanged -= Rebuild;
            _subscribedHand = null;
        }

        // ── Event handlers ────────────────────────────────────

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
            => TryBindHand();

        private void HandleClaimQueued(ClaimQueuedEvent e)
        {
            TryBindHand();
            Rebuild();
        }

        private void TryBindHand()
        {
            var hand = GameManager.Instance?.Run?.Hand;
            if (hand != null) SubscribeHand(hand);
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>Called by EncounterManager after filling the hand.</summary>
        public void Refresh()
        {
            TryBindHand();
            Rebuild();
        }

        // ── Build ─────────────────────────────────────────────

        private void Rebuild()
        {
            // Destroy previous buttons
            foreach (var btn in _activeButtons)
                if (btn != null) Destroy(btn.gameObject);
            _activeButtons.Clear();

            var run = GameManager.Instance?.Run;
            if (run == null || _cardButtonPrefab == null || _cardContainer == null) return;

            foreach (var card in run.Hand.Cards)
            {
                var btn = Instantiate(_cardButtonPrefab, _cardContainer);
                btn.Initialize(card, _machine);
                _activeButtons.Add(btn);
            }
        }
    }
}
