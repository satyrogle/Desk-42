// ============================================================
// DESK 42 — Paradox Recipe Detector (MonoBehaviour)
//
// Watches RumorMill.OnCardSlammed and tracks the per-encounter
// sequence of cards. After each slam, checks every ParadoxRecipe
// to see if its sequence appears as a suffix of the current
// sequence. If yes:
//   - increment MetaProgressData.ExeFragmentsCollected
//   - emit ParadoxRecipeExecuted telemetry
//   - spawn an .exe fragment visual on the desk
//   - clear the per-encounter sequence so the same recipe
//     can't be re-triggered by appending one extra card
//
// Self-bootstrapping. Auto-injected via HighImpactInjector into
// UI_HighImpactSystems alongside the other Layer 3/4 components.
//
// Gated on Phase 4 — paradox recipes are Layer 3 content.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;
using Desk42.Meta.Analytics;
// Disambiguate the Analytics *class* from the sibling
// Desk42.Meta.Analytics *namespace* (which wins via C# scope
// resolution from inside Desk42.Meta.DataSmuggling).
using AnalyticsAPI = Desk42.Meta.Analytics.Analytics;

namespace Desk42.Meta.DataSmuggling
{
    [DisallowMultipleComponent]
    public sealed class ParadoxRecipeDetector : MonoBehaviour
    {
        [Tooltip("Phase gate. Recipes are silently ignored below this phase.")]
        [SerializeField] private int _requiredPhase = 4;

        private readonly List<PunchCardType> _sequence = new();
        private bool _subscribed;

        private void OnEnable()
        {
            if (_subscribed) return;
            _subscribed = true;
            RumorMill.OnCardSlammed   += HandleCardSlammed;
            RumorMill.OnClaimQueued   += HandleClaimQueued;
            RumorMill.OnClaimResolved += HandleClaimResolved;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            RumorMill.OnCardSlammed   -= HandleCardSlammed;
            RumorMill.OnClaimQueued   -= HandleClaimQueued;
            RumorMill.OnClaimResolved -= HandleClaimResolved;
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleClaimQueued(ClaimQueuedEvent _)
        {
            _sequence.Clear();
        }

        private void HandleClaimResolved(ClaimResolvedEvent _)
        {
            _sequence.Clear();
        }

        private void HandleCardSlammed(CardSlammedEvent e)
        {
            if (!e.Result.IsSuccess) return;
            if (!(GameManager.Instance?.IsPhaseUnlocked(_requiredPhase) ?? false)) return;

            _sequence.Add(e.CardType);

            // Check every recipe — fire on the first match. Suffix
            // match: the recipe's full sequence must appear as the
            // tail of our running sequence.
            foreach (var recipe in ParadoxRecipes.All)
            {
                if (SequenceEndsWith(_sequence, recipe.Sequence))
                {
                    FireRecipe(recipe);
                    _sequence.Clear();
                    return;
                }
            }
        }

        private void FireRecipe(ParadoxRecipe recipe)
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null) return;

            meta.ExeFragmentsCollected++;
            SaveSystem.SaveMeta(meta);

            AnalyticsAPI.Track(TelemetryEvents.ParadoxRecipeExecuted,
                new Dictionary<string, object>
                {
                    ["recipe"]    = recipe.Id,
                    ["fragments"] = meta.ExeFragmentsCollected,
                });

            Debug.Log($"[Paradox] Recipe '{recipe.Id}' fired. " +
                      $"Fragments: {meta.ExeFragmentsCollected}");

            ExeFragmentDrop.SpawnOnDesk(recipe);
        }

        // ── Suffix match ──────────────────────────────────────

        private static bool SequenceEndsWith(IReadOnlyList<PunchCardType> haystack,
            PunchCardType[] needle)
        {
            if (needle == null || needle.Length == 0) return false;
            if (haystack.Count < needle.Length) return false;

            int offset = haystack.Count - needle.Length;
            for (int i = 0; i < needle.Length; i++)
                if (haystack[offset + i] != needle[i]) return false;

            return true;
        }
    }
}
