using System;
using UnityEngine;

namespace Desk42.Core
{
    [Serializable]
    public sealed class EliasAuthoredAppearance
    {
        [SerializeField] private string _appearanceKey;
        [SerializeField] private int _shiftNumber;
        [SerializeField] private int _queuePosition;
        [SerializeField] private string[] _authoredClaimIds;

        public string AppearanceKey => _appearanceKey;
        public int ShiftNumber => _shiftNumber;
        public int QueuePosition => _queuePosition;
        public string[] AuthoredClaimIds
            => _authoredClaimIds ?? Array.Empty<string>();

        public EliasAuthoredAppearance(string appearanceKey,
            int shiftNumber, int queuePosition, params string[] claimIds)
        {
            _appearanceKey = appearanceKey;
            _shiftNumber = shiftNumber;
            _queuePosition = queuePosition;
            _authoredClaimIds = claimIds ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Authored identity and schedule manifest for the one-character proof.
    /// It is deliberately specific to Elias; it is not a generic recurrence
    /// catalogue.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Desk42/Narrative/Elias Proof Content",
        fileName = "EliasProofContent")]
    public sealed class EliasProofContent : ScriptableObject
    {
        public const string CanonicalClaimantId = "elias_venn";
        public const string Shift1AppearanceKey = "elias_shift_1";
        public const string Shift2AppearanceKey = "elias_shift_2";
        public const string Shift5AppearanceKey = "elias_shift_5";

        [Header("Authored Identity")]
        [SerializeField] private string _displayName = "Elias Venn";
        [SerializeField] private string _clientSpeciesId = "moth_accountant";
        [SerializeField] private string _visualProfileId = "moth_accountant";
        [SerializeField] private string _proofSpineRole =
            "Five-shift causal attribution proof";

        [Header("Authored Appearances")]
        [SerializeField] private EliasAuthoredAppearance[] _appearances =
        {
            new(Shift1AppearanceKey, 1, 2, "elias_shift_1_claim"),
            new(Shift2AppearanceKey, 2, 2, "elias_shift_2_claim"),
            new(Shift5AppearanceKey, 5, 3,
                "elias_shift_5a_claim",
                "elias_shift_5b_claim",
                "elias_shift_5c_claim"),
        };

        [Header("Authored Follow-up Claims")]
        [SerializeField] private string[] _authoredFollowUpClaimIds =
        {
            "elias_aftermath_5a_1",
            "elias_aftermath_5a_2",
            "elias_aftermath_5b_1",
            "elias_aftermath_5c_1",
            "elias_aftermath_5c_2",
        };

        public string StableClaimantId => CanonicalClaimantId;
        public string DisplayName => _displayName;
        public string ClientSpeciesId => _clientSpeciesId;
        public string VisualProfileId => _visualProfileId;
        public string ProofSpineRole => _proofSpineRole;
        public EliasAuthoredAppearance[] Appearances
            => _appearances ?? Array.Empty<EliasAuthoredAppearance>();
        public string[] AuthoredFollowUpClaimIds
            => _authoredFollowUpClaimIds ?? Array.Empty<string>();

        public bool TryGetAppearance(string appearanceKey,
            out EliasAuthoredAppearance appearance)
        {
            foreach (EliasAuthoredAppearance candidate in Appearances)
            {
                if (candidate != null
                    && string.Equals(candidate.AppearanceKey, appearanceKey,
                        StringComparison.Ordinal))
                {
                    appearance = candidate;
                    return true;
                }
            }

            appearance = null;
            return false;
        }

        public static bool TryGetExpectedPriorVisits(
            string appearanceKey, out int priorVisits)
        {
            switch (appearanceKey)
            {
                case Shift1AppearanceKey:
                    priorVisits = 0;
                    return true;
                case Shift2AppearanceKey:
                    priorVisits = 1;
                    return true;
                case Shift5AppearanceKey:
                    priorVisits = 2;
                    return true;
                default:
                    priorVisits = 0;
                    return false;
            }
        }
    }
}
