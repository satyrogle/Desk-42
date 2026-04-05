// ============================================================
// DESK 42 — Card Library (ScriptableObject)
//
// Holds references to every PunchCardData SO in the project.
// Provides O(1) lookup by SO asset name so RunStateController
// and Deck.FromSaveState can resolve card IDs without
// Addressables or Resources at this stage.
//
// Key convention:
//   The lookup key is the SO asset name (card.name).
//   This must match the value written to CardInstanceData.CardId
//   by Deck.SerializePile (which uses c.Data.name).
//   Archetype BuildStartingDeckIds() must also use asset names
//   (e.g. "Card_Analyse", "Card_PendingReview").
//
// Designer workflow:
//   Create > Desk42 > Cards > Card Library
//   Drag all PunchCardData SOs into the _cards list.
//   Assign the single library asset to GameManager in the Boot scene.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Cards
{
    [CreateAssetMenu(
        menuName = "Desk42/Cards/Card Library",
        fileName = "CardLibrary")]
    public sealed class CardLibrary : ScriptableObject
    {
        [SerializeField]
        private List<PunchCardData> _cards = new();

        private Dictionary<string, PunchCardData> _lookup;

        private void OnEnable() => RebuildLookup();

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, PunchCardData>(
                _cards.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var card in _cards)
            {
                if (card == null) continue;

                // SO asset name is the stable ID (same value written to CardInstanceData.CardId)
                if (!_lookup.TryAdd(card.name, card))
                    Debug.LogWarning($"[CardLibrary] Duplicate asset name: '{card.name}' — " +
                                     $"second entry ignored.");
            }
        }

        /// <summary>
        /// Resolve a card by its asset name.
        /// Returns null and logs a warning if not found.
        /// </summary>
        public PunchCardData Resolve(string cardId)
        {
            if (_lookup == null) RebuildLookup();

            if (_lookup.TryGetValue(cardId, out var card)) return card;

            Debug.LogWarning($"[CardLibrary] Card not found: '{cardId}'. " +
                             $"Check that the SO is added to the library and named correctly.");
            return null;
        }

        public IReadOnlyList<PunchCardData> AllCards => _cards;
    }
}
