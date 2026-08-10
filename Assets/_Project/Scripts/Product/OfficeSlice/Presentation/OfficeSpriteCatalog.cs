using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    [CreateAssetMenu(menuName = "Desk42/Office Slice M4/Sprite Catalog")]
    public sealed class OfficeSpriteCatalog : ScriptableObject
    {
        public const string ResourcePath = "OfficeSliceM4/Config/OfficeSpriteCatalog";

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private Sprite sprite;
            [SerializeField] private Vector2 anchor = new(0.5f, 0f);

            public string Id => id;
            public Sprite Sprite => sprite;
            public Vector2 Anchor => anchor;

            public Entry(string id, Sprite sprite, Vector2 anchor)
            {
                this.id = id;
                this.sprite = sprite;
                this.anchor = anchor;
            }
        }

        [SerializeField] private OfficeVisualTheme theme;
        [SerializeField] private List<Entry> entries = new();
        private Dictionary<string, Entry> _lookup;

        public OfficeVisualTheme Theme => theme;
        public IReadOnlyList<Entry> Entries => entries;

        public static OfficeSpriteCatalog LoadRequired()
        {
            return Resources.Load<OfficeSpriteCatalog>(ResourcePath);
        }

        public bool TryResolve(string id, out Sprite sprite)
        {
            EnsureLookup();
            if (_lookup.TryGetValue(id, out Entry entry) && entry.Sprite != null)
            {
                sprite = entry.Sprite;
                return true;
            }
            sprite = null;
            return false;
        }

        public Sprite ResolveOrFallback(string id, out bool usedFallback)
        {
            if (TryResolve(id, out Sprite sprite))
            {
                usedFallback = false;
                return sprite;
            }
            usedFallback = true;
            Debug.LogError("OFFICE_M4_VISUAL_MISSING_ID " + id, this);
            return TryResolve("fallback.explicit", out Sprite fallback) ? fallback : null;
        }

        public bool HasDuplicateIds()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] == null || string.IsNullOrWhiteSpace(entries[i].Id) ||
                    !seen.Add(entries[i].Id)) return true;
            return false;
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, Entry>(entries.Count, StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Id) &&
                    !_lookup.ContainsKey(entry.Id)) _lookup.Add(entry.Id, entry);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(OfficeVisualTheme value, List<Entry> values)
        {
            theme = value;
            entries = values ?? throw new ArgumentNullException(nameof(values));
            _lookup = null;
        }
#endif
    }
}
