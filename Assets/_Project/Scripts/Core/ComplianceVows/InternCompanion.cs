// ============================================================
// DESK 42 — Intern Companion (MonoBehaviour)
//
// Vow 8: Mandatory Mentorship. An invulnerable "Intern" file
// on the desk auto-plays a random basic card after each of the
// player's plays.
//
// Rank 1: plays from a mini-deck of basic cards.
// Rank 2: plays from the player's actual deck.
// Rank 3: plays 2 cards from the player's deck.
// ============================================================

using UnityEngine;
using Desk42.Cards;

namespace Desk42.Core
{
    [DisallowMultipleComponent]
    public sealed class InternCompanion : MonoBehaviour
    {
        private static readonly string[] _basicCardIds =
        {
            "Card_PendingReview",
            "Card_Analyse",
            "Card_CooperationRoute",
        };

        private RedTape.PunchCardMachine _machine;
        private bool _internIsPlaying; // guard against recursive slam loop

        private void OnEnable()
        {
            RumorMill.OnCardSlammed += HandleCardSlammed;
        }

        private void OnDisable()
        {
            RumorMill.OnCardSlammed -= HandleCardSlammed;
        }

        public void SetMachine(RedTape.PunchCardMachine machine)
        {
            _machine = machine;
        }

        private void HandleCardSlammed(CardSlammedEvent e)
        {
            // Guard: don't react to our own auto-slams
            if (_internIsPlaying) return;

            var source = ComplianceVowSystem.GetMentorshipSource();
            if (source == MentorshipSource.None) return;

            _internIsPlaying = true;
            int plays = source == MentorshipSource.PlayerDeckDouble ? 2 : 1;

            for (int i = 0; i < plays; i++)
            {
                string cardId = PickCard(source);
                if (string.IsNullOrEmpty(cardId)) continue;

                // Resolve the card data from the library
                var cardData = GameManager.Instance?.Cards?.Resolve(cardId);
                if (cardData == null) continue;

                // Auto-slam via the machine
                string instanceId = System.Guid.NewGuid().ToString("N");
                _machine?.SlamCard(cardData, instanceId);

                Debug.Log($"[InternCompanion] Auto-played: {cardData.DisplayName}.");
            }

            _internIsPlaying = false;
        }

        private string PickCard(MentorshipSource source)
        {
            if (source == MentorshipSource.BasicCards)
            {
                int idx = SeedEngine.Next(SeedStream.CardDraft, _basicCardIds.Length);
                return _basicCardIds[idx];
            }

            // Player deck — pick a random card from the draw pile
            var deck = GameManager.Instance?.Run?.RawData?.Deck;
            if (deck == null || deck.DrawPile.Count == 0) return null;

            int drawIdx = SeedEngine.Next(SeedStream.CardDraft, deck.DrawPile.Count);
            return deck.DrawPile[drawIdx].CardId;
        }
    }
}
