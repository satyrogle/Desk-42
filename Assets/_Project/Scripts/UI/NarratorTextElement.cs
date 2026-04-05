// ============================================================
// DESK 42 — Narrator Text Element (MonoBehaviour)
//
// Attach to any UI Text / TextMeshPro component.
// Registers a context key; PassiveAggressiveUIController
// finds all instances in the scene and refreshes them when
// the narrator tone changes.
//
// Usage:
//   1. Add this component to a UI Text GameObject.
//   2. Set ContextKey to e.g. "btn.submit_claim".
//   3. PassiveAggressiveUIController auto-discovers all
//      NarratorTextElement instances on tone change.
// ============================================================

using UnityEngine;
using TMPro;
using Desk42.Core;

namespace Desk42.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class NarratorTextElement : MonoBehaviour
    {
        [Tooltip("Key into NarratorSystem line bank (e.g. 'btn.submit_claim').")]
        public string ContextKey;

        [Tooltip("If set, {n} and {total} are filled with these values.")]
        public int StatusN;
        public int StatusTotal;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            // Apply current tone immediately on spawn
            var tone = GameManager.Instance?.Run?.NarratorTone
                ?? NarratorReliability.Professional;
            Refresh(tone);
        }

        public void Refresh(NarratorReliability tone)
        {
            if (_text == null || string.IsNullOrEmpty(ContextKey)) return;

            _text.text = StatusTotal > 0
                ? NarratorSystem.GetStatusLine(ContextKey, tone, StatusN, StatusTotal)
                : NarratorSystem.GetLine(ContextKey, tone);
        }
    }
}
