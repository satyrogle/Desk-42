// ============================================================
// DESK 42 — Hand
//
// The cards currently held by the player.
// Filled at shift start and after each draw (from Deck).
//
// Responsibilities:
//   - Cap at MaxHandSize (default 5; archetype can modify).
//   - Know which cards are playable (not jammed, not crumpled).
//   - Notify subscribers when hand composition changes
//     (for UI rebuild).
//
// The Hand does not own fatigue state — that lives in
// CardFatigueTracker. It does mirror IsJammed/IsCrumpled
// back onto CardInstance at the end of each slam via Sync().
//
// Design: one Hand per run (not per shift). Cards move between
// Deck piles and Hand. The Hand is never serialized directly —
// the Deck state captures everything.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Desk42.RedTape;

namespace Desk42.Cards
{
    public sealed class Hand
    {
        // ── Config ────────────────────────────────────────────

        public const int DEFAULT_MAX_SIZE = 5;

        // ── State ─────────────────────────────────────────────

        private readonly List<CardInstance> _cards = new(DEFAULT_MAX_SIZE + 2);
        private int _maxSize;

        // ── Events ────────────────────────────────────────────

        /// <summary>Fires whenever a card is added or removed (UI rebuild trigger).</summary>
        public event Action OnHandChanged;

        // ── Init ──────────────────────────────────────────────

        public Hand(int maxSize = DEFAULT_MAX_SIZE)
        {
            _maxSize = maxSize;
        }

        // ── Queries ───────────────────────────────────────────

        public IReadOnlyList<CardInstance> Cards     => _cards;
        public int  Count                            => _cards.Count;
        public bool IsFull                           => _cards.Count >= _maxSize;
        public int  MaxSize                          => _maxSize;
        public int  EmptySlots                       => Mathf.Max(0, _maxSize - _cards.Count);

        public bool Contains(string instanceId)
        {
            foreach (var c in _cards)
                if (c.InstanceId == instanceId) return true;
            return false;
        }

        public CardInstance FindById(string instanceId)
        {
            foreach (var c in _cards)
                if (c.InstanceId == instanceId) return c;
            return null;
        }

        // ── Draw Into Hand ────────────────────────────────────

        /// <summary>
        /// Fill hand to MaxSize by drawing from the deck.
        /// Returns the number of cards actually drawn.
        /// </summary>
        public int FillFromDeck(Deck deck)
        {
            int drawn = 0;
            while (!IsFull)
            {
                var card = deck.Draw();
                if (card == null) break;
                AddCard(card);
                drawn++;
            }
            return drawn;
        }

        /// <summary>Add a single card to hand (up to MaxSize).</summary>
        public bool AddCard(CardInstance card)
        {
            if (card == null) return false;
            if (_cards.Count >= _maxSize)
            {
                Debug.LogWarning($"[Hand] Tried to add {card} but hand is full.");
                return false;
            }
            _cards.Add(card);
            OnHandChanged?.Invoke();
            return true;
        }

        // ── Play / Discard ────────────────────────────────────

        /// <summary>
        /// Remove a card from hand after a slam, placing it in the appropriate pile.
        /// Crumpled cards go to Archive; all others go to Discard.
        /// </summary>
        public void OnCardPlayed(CardInstance card, CardFatigueTracker fatigue, Deck deck)
        {
            if (!_cards.Remove(card)) return;

            // Sync fatigue state onto the instance before routing
            SyncFatigueOnto(card, fatigue);

            if (card.IsCrumpled)
                deck.Archive(card);
            else
                deck.Discard(card);

            OnHandChanged?.Invoke();
        }

        /// <summary>Discard the entire hand (e.g. end of shift).</summary>
        public void DiscardAll(Deck deck)
        {
            for (int i = _cards.Count - 1; i >= 0; i--)
                deck.Discard(_cards[i]);

            _cards.Clear();
            OnHandChanged?.Invoke();
        }

        // ── Fatigue Sync ──────────────────────────────────────

        /// <summary>
        /// Mirror CardFatigueTracker state back onto a CardInstance.
        /// Called after each slam so IsJammed/IsCrumpled are accurate.
        /// </summary>
        public void SyncFatigueOnto(CardInstance card, CardFatigueTracker fatigue)
        {
            card.Fatigue    = fatigue.GetFatigue(card.InstanceId);
            card.IsJammed   = fatigue.IsJammed(card.InstanceId);
            card.IsCrumpled = fatigue.IsCrumpled(card.InstanceId, card.Data);
        }

        /// <summary>Sync all cards in hand.</summary>
        public void SyncAllFatigue(CardFatigueTracker fatigue)
        {
            bool changed = false;
            foreach (var card in _cards)
            {
                bool wasCrumpled = card.IsCrumpled;
                SyncFatigueOnto(card, fatigue);
                if (!wasCrumpled && card.IsCrumpled) changed = true;
            }
            if (changed) OnHandChanged?.Invoke();
        }

        // ── Archetype Modifiers ───────────────────────────────

        /// <summary>
        /// Adjust max hand size (Employee Handbook / archetype perk).
        /// Clamps to [1, 10].
        /// </summary>
        public void SetMaxSize(int newMax)
        {
            _maxSize = Mathf.Clamp(newMax, 1, 10);
        }

        public void ModifyMaxSize(int delta) => SetMaxSize(_maxSize + delta);

        // ── Playability Query ─────────────────────────────────

        /// <summary>
        /// Returns all cards that can legally be played right now.
        /// (Not jammed, not crumpled — credit/context checks are in StateInjector.)
        /// </summary>
        public List<CardInstance> GetPlayableCards(CardFatigueTracker fatigue)
        {
            var playable = new List<CardInstance>(_cards.Count);
            foreach (var card in _cards)
            {
                if (!fatigue.IsJammed(card.InstanceId) &&
                    !fatigue.IsCrumpled(card.InstanceId, card.Data))
                    playable.Add(card);
            }
            return playable;
        }

        // ── IT Person: Reset specific card fatigue ────────────

        public void ResetCardFatigue(string instanceId, CardFatigueTracker fatigue)
        {
            var card = FindById(instanceId);
            if (card == null) return;
            fatigue.ResetCardFatigue(instanceId);
            SyncFatigueOnto(card, fatigue);
            OnHandChanged?.Invoke();
        }

        // ── Debug ─────────────────────────────────────────────

        public string Dump()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Hand ({Count}/{MaxSize}) ===");
            foreach (var c in _cards)
                sb.AppendLine($"  {c.Data.DisplayName} [{c.InstanceId[..6]}]" +
                              $" f={c.Fatigue}" +
                              $"{(c.IsJammed   ? " [JAMMED]"   : "")}" +
                              $"{(c.IsCrumpled ? " [CRUMPLED]" : "")}");
            return sb.ToString();
        }
    }
}
