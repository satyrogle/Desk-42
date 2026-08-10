using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    [CreateAssetMenu(menuName = "Desk42/Office Slice M4/Visual Theme")]
    public sealed class OfficeVisualTheme : ScriptableObject
    {
        [Serializable]
        public sealed class SemanticColour
        {
            [SerializeField] private string id;
            [SerializeField] private Color colour = Color.white;

            public string Id => id;
            public Color Colour => colour;

            public SemanticColour(string id, Color colour)
            {
                this.id = id;
                this.colour = colour;
            }
        }

        [SerializeField] private List<SemanticColour> colours = new();
        [SerializeField] private bool reducedFlash;

        public IReadOnlyList<SemanticColour> Colours => colours;
        public bool ReducedFlash => reducedFlash;

        public bool TryGetColour(string id, out Color colour)
        {
            for (int i = 0; i < colours.Count; i++)
            {
                if (!string.Equals(colours[i].Id, id, StringComparison.Ordinal)) continue;
                colour = colours[i].Colour;
                return true;
            }
            colour = Color.magenta;
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetColours(List<SemanticColour> values)
        {
            colours = values ?? throw new ArgumentNullException(nameof(values));
        }
#endif
    }
}
