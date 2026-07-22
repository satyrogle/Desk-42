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
            RumorMill.OnStateTransition += HandleProjectionChanged;
            RumorMill.OnCardSlammed += HandleProjectionChanged;
            RumorMill.OnCounterTraitGenerated += HandleProjectionChanged;
            RumorMill.OnSupplySignal += HandleProjectionChanged;
            RumorMill.OnOfficeHazard += HandleProjectionChanged;
        }

        private void OnDisable()
        {
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;
            RumorMill.OnClaimQueued   -= HandleClaimQueued;
            RumorMill.OnStateTransition -= HandleProjectionChanged;
            RumorMill.OnCardSlammed -= HandleProjectionChanged;
            RumorMill.OnCounterTraitGenerated -= HandleProjectionChanged;
            RumorMill.OnSupplySignal -= HandleProjectionChanged;
            RumorMill.OnOfficeHazard -= HandleProjectionChanged;
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

        private void HandleProjectionChanged(StateTransitionEvent e) => RefreshFaces();
        private void HandleProjectionChanged(CardSlammedEvent e) => RefreshFaces();
        private void HandleProjectionChanged(CounterTraitGeneratedEvent e) => RefreshFaces();
        private void HandleProjectionChanged(SupplySignalEvent e) => RefreshFaces();
        private void HandleProjectionChanged(OfficeHazardEvent e) => RefreshFaces();

        private void RefreshFaces()
        {
            foreach (var button in _activeButtons)
                button?.RefreshFace();
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
            var run = GameManager.Instance?.Run;
            int cardCount = run?.Hand?.Cards?.Count ?? -1;

            if (_cardButtonPrefab == null)
            {
                Debug.LogError("[CardHandView] _cardButtonPrefab is NULL — cannot rebuild. " +
                               "Run Tools → Desk 42 → Fix Shift Scene Issues.");
                return;
            }
            if (_cardContainer == null)
            {
                Debug.LogError("[CardHandView] _cardContainer is NULL — cannot rebuild.");
                return;
            }

            // Destroy previous buttons
            foreach (var btn in _activeButtons)
                if (btn != null) Destroy(btn.gameObject);
            _activeButtons.Clear();

            if (run == null || run.Hand == null) return;

            foreach (var card in run.Hand.Cards)
            {
                var btn = Instantiate(_cardButtonPrefab, _cardContainer);
                btn.Initialize(card, _machine);
                _activeButtons.Add(btn);
            }

            Debug.Log($"[CardHandView] Rebuilt: {_activeButtons.Count} buttons " +
                      $"(hand had {cardCount} cards).");
        }

        // ── Safety poll: catches missed OnHandChanged events ──

        private float _pollTimer;

        private void Update()
        {
            _pollTimer += Time.deltaTime;
            if (_pollTimer < 0.5f) return;
            _pollTimer = 0f;

            var run = GameManager.Instance?.Run;
            if (run?.Hand == null) return;

            int handCount   = run.Hand.Cards.Count;
            int buttonCount = _activeButtons.Count;

            // Drift detected — count mismatch means we missed a change event
            if (handCount != buttonCount)
            {
                Debug.LogWarning($"[CardHandView] Drift detected: hand={handCount}, buttons={buttonCount}. Forcing rebuild.");
                TryBindHand();
                Rebuild();
            }
            else
            {
                // Safety refresh for fatigue expiry and any modifier whose
                // source does not publish a dedicated change event yet.
                RefreshFaces();
            }
        }
    }
}
