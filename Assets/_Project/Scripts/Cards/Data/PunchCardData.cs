// ============================================================
// DESK 42 — Punch Card Data (ScriptableObject)
//
// One SO asset per punch card type. Defines everything the
// StateInjector and PunchCardMachine need to know about a card.
//
// The card itself is a physical object in the scene (a prefab).
// PunchCardData is the blueprint; the prefab holds the art/collider.
//
// Designer workflow:
//   Create > Desk42 > Cards > Punch Card
//   Fill in fields, assign to card prefab's CardView component.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Cards
{
    [CreateAssetMenu(
        menuName = "Desk42/Cards/Punch Card",
        fileName = "Card_PendingReview")]
    public sealed class PunchCardData : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Must match a PunchCardType enum value exactly.")]
        public PunchCardType CardType;

        [Tooltip("Display name shown on the physical card texture.")]
        public string DisplayName = "PENDING REVIEW";

        [Tooltip("Department code printed in upper corner. E.g. FORM-27B.")]
        public string FormCode = "FORM-01A";

        [TextArea(2, 4)]
        [Tooltip("Bureaucratic flavour text on the card body.")]
        public string FlavourText =
            "The undersigned hereby requests a formal review of the above-referenced claim.";

        // ── Effect ────────────────────────────────────────────

        [Header("Effect")]
        [Tooltip("Duration of the injected state in seconds. 0 = organic transition only.")]
        public float InjectionDuration = 10f;

        [Tooltip("The BSM state this card injects into. Used as fallback if TransitionTable has no matching rule.")]
        public ClientStateID DefaultTargetState = ClientStateID.Pending;

        [Tooltip("Override duration multiplier from this card's archetype (applied by SynergyResolver).")]
        public float ArchetypeMultiplier = 1f;

        // ── Costs ─────────────────────────────────────────────

        [Header("Costs")]
        [Tooltip("Credits spent when this card is played. 0 = free.")]
        public int CreditCost = 0;

        [Tooltip("Soul integrity cost. Playing repeatedly accumulates Moral Injury.")]
        [Range(0f, 15f)]
        public float SoulCost = 0f;

        // ── Card Type Metadata ────────────────────────────────

        [Header("Card Type")]
        [Tooltip("Rarity tier — determines draft pool weighting and shop price.")]
        public CardRarity Rarity = CardRarity.Common;

        [Tooltip("Archetype this card belongs to. Used for synergy resolution.")]
        public string ArchetypeId = "core";

        [Tooltip("Tag used by office supply synergies. E.g. 'LEGAL', 'OCCULT', 'FILING'.")]
        public string[] TypeTags = { "FILING" };

        // ── Fatigue ───────────────────────────────────────────

        [Header("Fatigue")]
        [Tooltip("Max times this card can be played before jamming. -1 = no limit.")]
        public int MaxFatigue = 5;

        [Tooltip("Fatigue level at which the card jams (brief lockout but not removed).")]
        public int JamFatigue = 3;

        // ── Visual / Audio ────────────────────────────────────

        [Header("Presentation")]
        public Sprite CardArtwork;
        public Color  CardBorderColor = Color.white;

        [Tooltip("Sound played when card is slammed into the machine.")]
        public string SlamSoundKey = "sfx_card_slam_default";

        [Tooltip("Particle effect key triggered on slam.")]
        public string SlamParticleKey = "vfx_card_process";

        // ── Validation ────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
                DisplayName = CardType.ToString().Replace("_", " ").ToUpperInvariant();
        }
#endif
    }
}
