// ============================================================
// DESK 42 — Deck
//
// The player's full card collection for a run.
// Owns three piles: DrawPile, Discard, Archive.
// Hand is managed separately (Hand.cs).
//
// Card identity uses a stable CardInstanceId (GUID) so that
// fatigue, jam state, and per-card history survive reshuffles.
//
// Shuffle uses SeedEngine stream CardDraft for determinism.
//
// Draw order: DrawPile → (reshuffle Discard when empty).
// Archive: cards permanently removed from circulation this run
//          (crumpled, consumed by ability, or traded away).
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;      // CardInstanceData, SeedEngine, SeedStream

namespace Desk42.Cards
{
    public sealed class Deck
    {
        // ── Piles ─────────────────────────────────────────────

        private readonly List<CardInstance> _drawPile    = new(40);
        private readonly List<CardInstance> _discardPile = new(40);
        private readonly List<CardInstance> _archive     = new(8);

        // ── Queries ───────────────────────────────────────────

        public IReadOnlyList<CardInstance> DrawPile    => _drawPile;
        public IReadOnlyList<CardInstance> DiscardPile => _discardPile;
        public IReadOnlyList<CardInstance> Archive     => _archive;

        public int DrawCount    => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;
        public int ArchiveCount => _archive.Count;
        public int TotalCards   => DrawCount + DiscardCount;

        // ── Construction ──────────────────────────────────────

        /// <summary>
        /// Build a Deck from a list of card data assets.
        /// Each entry produces one CardInstance with a unique ID.
        /// </summary>
        public static Deck FromDataList(IEnumerable<PunchCardData> cards)
        {
            var deck = new Deck();
            foreach (var data in cards)
                deck._drawPile.Add(new CardInstance(data));
            return deck;
        }

        /// <summary>
        /// Restore a Deck from saved run state (preserves fatigue/jam/crumple).
        /// </summary>
        public static Deck FromSaveState(
            List<CardInstanceData> drawData,
            List<CardInstanceData> discardData,
            List<CardInstanceData> archiveData,
            Func<string, PunchCardData> cardLookup)
        {
            var deck = new Deck();
            Restore(drawData,   deck._drawPile,    cardLookup);
            Restore(discardData, deck._discardPile, cardLookup);
            Restore(archiveData, deck._archive,     cardLookup);
            return deck;
        }

        private static void Restore(
            List<CardInstanceData> source,
            List<CardInstance> target,
            Func<string, PunchCardData> lookup)
        {
            if (source == null) return;
            foreach (var d in source)
            {
                var data = lookup(d.CardId);
                if (data == null)
                {
                    Debug.LogWarning($"[Deck] Card not found for id '{d.CardId}' — skipped.");
                    continue;
                }
                target.Add(new CardInstance(data, d.InstanceId)
                {
                    Fatigue    = d.Fatigue,
                    IsJammed   = d.IsJammed,
                    IsCrumpled = d.IsCrumpled,
                });
            }
        }

        // ── Draw ──────────────────────────────────────────────

        /// <summary>
        /// Draw the top card. Returns null only if both piles are empty.
        /// Reshuffles discard into draw when draw pile empties.
        /// </summary>
        public CardInstance Draw()
        {
            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0) return null;
                ReshuffleDiscardIntoDraw();
            }

            var card = _drawPile[_drawPile.Count - 1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            return card;
        }

        /// <summary>Draw up to <paramref name="count"/> cards.</summary>
        public List<CardInstance> DrawMultiple(int count)
        {
            var drawn = new List<CardInstance>(count);
            for (int i = 0; i < count; i++)
            {
                var card = Draw();
                if (card == null) break;
                drawn.Add(card);
            }
            return drawn;
        }

        // ── Discard ───────────────────────────────────────────

        public void Discard(CardInstance card)
        {
            if (card == null) return;
            _discardPile.Add(card);
        }

        public void DiscardAll(IEnumerable<CardInstance> cards)
        {
            foreach (var c in cards) Discard(c);
        }

        // ── Archive ───────────────────────────────────────────

        /// <summary>
        /// Permanently remove a card from this run's circulation.
        /// Used for crumpled cards and consumed-by-ability cards.
        /// </summary>
        public void Archive(CardInstance card)
        {
            if (card == null) return;

            // Remove from wherever it currently lives
            _drawPile.Remove(card);
            _discardPile.Remove(card);

            _archive.Add(card);
            Debug.Log($"[Deck] Archived {card.Data.DisplayName} ({card.InstanceId}).");
        }

        // ── Add Card (from shop / draft) ──────────────────────

        /// <summary>Add a newly acquired card to the top of the discard pile.</summary>
        public void AddCard(PunchCardData data)
        {
            _discardPile.Add(new CardInstance(data));
        }

        /// <summary>Add an existing CardInstance (e.g. upgraded copy) to discard.</summary>
        public void AddCard(CardInstance instance)
        {
            _discardPile.Add(instance);
        }

        // ── Remove Card ───────────────────────────────────────

        /// <summary>Remove a card by instance ID (used by IT Person debug token).</summary>
        public bool RemoveById(string instanceId)
        {
            var card = FindById(instanceId);
            if (card == null) return false;
            _drawPile.Remove(card);
            _discardPile.Remove(card);
            return true;
        }

        public CardInstance FindById(string instanceId)
        {
            foreach (var c in _drawPile)    if (c.InstanceId == instanceId) return c;
            foreach (var c in _discardPile) if (c.InstanceId == instanceId) return c;
            return null;
        }

        // ── Shuffle ───────────────────────────────────────────

        /// <summary>
        /// Shuffle the draw pile in place using SeedEngine (CardDraft stream).
        /// Call at run start after building the deck.
        /// </summary>
        public void Shuffle()
        {
            SeedEngine.Shuffle(SeedStream.CardDraft, _drawPile);
        }

        /// <summary>
        /// Move all discard cards to draw pile and shuffle.
        /// Called automatically when draw pile empties during draw.
        /// </summary>
        public void ReshuffleDiscardIntoDraw()
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle();
            Debug.Log($"[Deck] Reshuffled {_drawPile.Count} cards.");
        }

        // ── Serialisation ─────────────────────────────────────

        public List<CardInstanceData> SerializeDrawPile()
            => SerializePile(_drawPile);

        public List<CardInstanceData> SerializeDiscardPile()
            => SerializePile(_discardPile);

        public List<CardInstanceData> SerializeArchive()
            => SerializePile(_archive);

        private static List<CardInstanceData> SerializePile(List<CardInstance> pile)
        {
            var result = new List<CardInstanceData>(pile.Count);
            foreach (var c in pile)
                result.Add(new CardInstanceData
                {
                    CardId     = c.Data.name, // SO asset name used as stable ID
                    InstanceId = c.InstanceId,
                    Fatigue    = c.Fatigue,
                    IsJammed   = c.IsJammed,
                    IsCrumpled = c.IsCrumpled,
                });
            return result;
        }

        // ── Debug ─────────────────────────────────────────────

        public string Dump()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Deck ({TotalCards} active, {ArchiveCount} archived) ===");
            sb.AppendLine($"DrawPile ({DrawCount}):");
            foreach (var c in _drawPile)
                sb.AppendLine($"  {c.Data.DisplayName} [{c.InstanceId[..6]}] f={c.Fatigue}");
            sb.AppendLine($"Discard ({DiscardCount}):");
            foreach (var c in _discardPile)
                sb.AppendLine($"  {c.Data.DisplayName} [{c.InstanceId[..6]}]");
            return sb.ToString();
        }
    }

    // ── Card Instance ─────────────────────────────────────────
    // Runtime representation of one copy of a card.
    // Data is the SO asset; mutable fields track per-run state.

    public sealed class CardInstance
    {
        public readonly PunchCardData Data;
        public readonly string        InstanceId;

        // Per-run mutable state
        public int  Fatigue;
        public bool IsJammed;
        public bool IsCrumpled;

        public CardInstance(PunchCardData data)
        {
            Data       = data;
            InstanceId = Guid.NewGuid().ToString();
        }

        public CardInstance(PunchCardData data, string instanceId)
        {
            Data       = data;
            InstanceId = instanceId;
        }

        public override string ToString()
            => $"{Data.DisplayName} [{InstanceId[..6]}]";
    }
}
