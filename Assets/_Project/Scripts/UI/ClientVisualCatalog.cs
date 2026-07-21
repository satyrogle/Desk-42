using System;
using Desk42.Core;
using UnityEngine;

namespace Desk42.UI
{
    /// <summary>
    /// Maps claim species IDs and behavioral states to claimant portrait sprites.
    /// Aliases keep old saves and temporary prototype IDs visually compatible.
    /// </summary>
    [CreateAssetMenu(fileName = "ClientVisualCatalog", menuName = "Desk 42/Visuals/Client Visual Catalog")]
    public sealed class ClientVisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Profile
        {
            [Tooltip("Canonical species ID followed by any legacy/prototype aliases.")]
            public string[] SpeciesIds = Array.Empty<string>();

            public Sprite Pending;
            public Sprite Agitated;
            public Sprite Litigious;
            public Sprite Cooperative;
            public Sprite Suspicious;
            public Sprite Resigned;
            public Sprite Paranoid;
            public Sprite Dissociating;
            public Sprite Smug;

            public Sprite Resolve(ClientStateID state)
            {
                Sprite stateSprite = state switch
                {
                    ClientStateID.Pending => Pending,
                    ClientStateID.Agitated => Agitated,
                    ClientStateID.Litigious => Litigious,
                    ClientStateID.Cooperative => Cooperative,
                    ClientStateID.Suspicious => Suspicious,
                    ClientStateID.Resigned => Resigned,
                    ClientStateID.Paranoid => Paranoid,
                    ClientStateID.Dissociating => Dissociating,
                    ClientStateID.Smug => Smug,
                    _ => null
                };

                return stateSprite != null ? stateSprite : Pending;
            }

            public bool Matches(string speciesId)
            {
                if (string.IsNullOrWhiteSpace(speciesId) || SpeciesIds == null)
                    return false;

                foreach (string candidate in SpeciesIds)
                {
                    if (string.Equals(candidate, speciesId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
        }

        [SerializeField] private Profile[] _profiles = Array.Empty<Profile>();
        [SerializeField] private int _fallbackProfileIndex;

        public Profile[] Profiles
        {
            get => _profiles;
            set => _profiles = value ?? Array.Empty<Profile>();
        }

        public int FallbackProfileIndex
        {
            get => _fallbackProfileIndex;
            set => _fallbackProfileIndex = value;
        }

        public Sprite ResolveSprite(string speciesId, ClientStateID state)
        {
            if (_profiles == null || _profiles.Length == 0)
                return null;

            foreach (Profile profile in _profiles)
            {
                if (profile != null && profile.Matches(speciesId))
                    return profile.Resolve(state);
            }

            int fallback = Mathf.Clamp(_fallbackProfileIndex, 0, _profiles.Length - 1);
            return _profiles[fallback]?.Resolve(state);
        }
    }
}
