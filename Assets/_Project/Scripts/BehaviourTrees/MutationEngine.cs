// ============================================================
// DESK 42 — Mutation Engine
//
// Watches card usage patterns and procedurally generates
// counter-traits when a tactic is overused.
//
// This is the Repeat Offender Database's live component:
//   - Per-encounter: checks if a slam triggers a mutation.
//   - Persistent: the generated counter-trait is saved to
//     MetaProgressData and survives across runs.
//
// Counter-trait generation:
//   1. Each successful slam records usage in the encounter tally.
//   2. If usage of a card type crosses MUTATION_THRESHOLD in
//      this encounter, roll SeedEngine mutation chance.
//   3. On roll success, pick a counter-trait from the map
//      for that card type.
//   4. Insert a BTBlockerNode into the client's base BT.
//   5. Also add a runtime TransitionRule that gives the client
//      an organic response to that card type.
//   6. Persist the trait via MetaProgressData.
//   7. Fire CounterTraitGeneratedEvent for the Rumor Mill.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;
using Desk42.BSM;
using Desk42.BSM.Transitions;

namespace Desk42.RedTape
{
    public sealed class MutationEngine
    {
        // ── Config ────────────────────────────────────────────

        // How many times a card must be used in ONE encounter
        // before mutation can trigger.
        private const int MUTATION_THRESHOLD = 2;

        // Base probability of mutation triggering at threshold.
        // Increases by 15% for each use above the threshold.
        private const float BASE_MUTATION_CHANCE = 0.05f;
        private const float MUTATION_CHANCE_STEP = 0.15f;

        // ── State ─────────────────────────────────────────────

        // Per-encounter card usage tally (reset between encounters)
        private readonly Dictionary<string, int> _encounterTally = new(8);

        // Counter-traits already generated for the current client
        // (both persistent from DB and generated this encounter)
        private readonly HashSet<string> _activeTraits = new(4);

        // ── Counter-Trait Map ─────────────────────────────────
        // Maps card type to a pool of possible counter-traits.
        // Each trait has an id, a description, and a transition
        // rule / BT node it injects.

        private static readonly Dictionary<string, string[]> CounterTraitPool
            = new()
        {
            [nameof(PunchCardType.ThreatAudit)]   = new[] { "retained_counsel", "filed_first", "loud_chewer" },
            [nameof(PunchCardType.PendingReview)] = new[] { "pre_filed_exemption", "form_predelegation" },
            [nameof(PunchCardType.Redact)]         = new[] { "photographic_memory", "notarized_copies" },
            [nameof(PunchCardType.LegalHold)]      = new[] { "injunction_immunity", "counter_hold" },
            [nameof(PunchCardType.Expedite)]       = new[] { "deliberate_delay", "union_regulation" },
            [nameof(PunchCardType.Analyse)]        = new[] { "nda_preemptive", "trait_masking" },
            [nameof(PunchCardType.CooperationRoute)] = new[] { "professional_distance", "scripted_responses" },
            [nameof(PunchCardType.Forget)]         = new[] { "meticulous_notes", "memory_implant" },
        };

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Called by StateInjector after every successful slam.
        /// Checks if the usage pattern warrants a mutation.
        /// </summary>
        public void CheckAndMutate(
            ClientStateMachine client,
            PunchCardType cardType,
            MetaProgressData meta)
        {
            string key = cardType.ToString();

            // Tally this encounter's usage
            _encounterTally.TryGetValue(key, out int count);
            _encounterTally[key] = count + 1;

            int newCount = _encounterTally[key];

            if (newCount < MUTATION_THRESHOLD) return;

            // Compute probability — increases with each overuse
            float chance = BASE_MUTATION_CHANCE
                           + (newCount - MUTATION_THRESHOLD) * MUTATION_CHANCE_STEP;
            chance = Mathf.Min(chance, 0.85f); // cap at 85%

            if (!SeedEngine.NextBool(SeedStream.MutationGeneration, chance)) return;

            // Generate a counter-trait
            GenerateCounterTrait(client, key, meta);
        }

        /// <summary>
        /// Load existing counter-traits from the Repeat Offender DB
        /// into the current encounter's BT and transition table.
        /// Call this when a client is initialized for an encounter.
        /// </summary>
        public void LoadExistingCounterTraits(
            ClientStateMachine client,
            MetaProgressData meta)
        {
            if (meta == null) return;

            var profile = meta.GetOrCreateProfile(client.ClientVariantId);

            foreach (string traitId in profile.CounterTraitIds)
            {
                if (_activeTraits.Contains(traitId)) continue;

                ApplyCounterTrait(client, traitId);
                _activeTraits.Add(traitId);
            }
        }

        /// <summary>Reset per-encounter state when a new client sits down.</summary>
        public void ResetForNewEncounter()
        {
            _encounterTally.Clear();
            _activeTraits.Clear();
        }

        // ── Private: Generate ─────────────────────────────────

        private void GenerateCounterTrait(
            ClientStateMachine client,
            string cardTypeKey,
            MetaProgressData meta)
        {
            // Pick a trait that isn't already active
            if (!CounterTraitPool.TryGetValue(cardTypeKey, out var pool)) return;

            string traitId = null;
            foreach (var candidate in pool)
            {
                if (!_activeTraits.Contains(candidate))
                {
                    traitId = candidate;
                    break;
                }
            }

            if (traitId == null)
            {
                Debug.Log($"[MutationEngine] All counter-traits for {cardTypeKey} already active.");
                return;
            }

            // Apply to BT + transition table
            ApplyCounterTrait(client, traitId);
            _activeTraits.Add(traitId);

            // Persist to meta (survives future runs)
            meta?.AddCounterTrait(client.ClientVariantId, traitId);

            // Fire Rumor Mill event
            RumorMill.PublishDeferred(new CounterTraitGeneratedEvent(
                client.ClientVariantId, traitId,
                System.Enum.Parse<PunchCardType>(cardTypeKey)));

            Debug.Log($"[MutationEngine] Counter-trait GENERATED: {traitId} " +
                      $"for {client.ClientVariantId} (card: {cardTypeKey})");
        }

        private void ApplyCounterTrait(ClientStateMachine client, string traitId)
        {
            // Map trait ID to BT blocker and/or transition rule
            switch (traitId)
            {
                // ── THREATEN_AUDIT counter-traits ─────────────

                case "retained_counsel":
                    // Blocks THREATEN_AUDIT entirely
                    client.BaseBT.InsertBlockerForCard(
                        nameof(PunchCardType.ThreatAudit), traitId);
                    // Also transition: THREATEN -> LITIGIOUS instead of AGITATED
                    AddBlockingTransitionRule(client,
                        nameof(PunchCardType.ThreatAudit),
                        Core.ClientStateID.Litigious, priority: 99,
                        "retained_counsel_rule");
                    break;

                case "filed_first":
                    // Client starts LITIGIOUS next encounter (not this one —
                    // handled at spawn time via RepeatOffenderDB check)
                    Debug.Log($"[MutationEngine] filed_first: next encounter starts LITIGIOUS.");
                    break;

                case "loud_chewer":
                    // Sanity drain passive — applied via Rumor Mill
                    // TODO: Register passive with RunStateController
                    break;

                // ── PENDING_REVIEW counter-traits ─────────────

                case "pre_filed_exemption":
                    // Hard BT blocker on PENDING_REVIEW
                    client.BaseBT.InsertBlockerForCard(
                        nameof(PunchCardType.PendingReview), traitId);
                    break;

                case "form_predelegation":
                    // PENDING_REVIEW halved in duration — not blocked entirely
                    // Apply via SynergyResolver modifier TODO
                    break;

                // ── REDACT counter-traits ─────────────────────

                case "photographic_memory":
                    client.BaseBT.InsertBlockerForCard(
                        nameof(PunchCardType.Redact), traitId);
                    break;

                case "notarized_copies":
                    // Redact causes PARANOID instead of SUSPICIOUS
                    AddBlockingTransitionRule(client,
                        nameof(PunchCardType.Redact),
                        Core.ClientStateID.Paranoid, priority: 99,
                        "notarized_copies_rule");
                    break;

                // ── Default ───────────────────────────────────

                default:
                    Debug.LogWarning($"[MutationEngine] No application logic for trait: {traitId}");
                    break;
            }
        }

        // ── Transition Rule Helpers ───────────────────────────

        private static void AddBlockingTransitionRule(
            ClientStateMachine client,
            string triggerAction,
            Core.ClientStateID targetState,
            int priority,
            string debugName)
        {
            // Build a runtime-only rule (no conditions = fires for all contexts
            // where TriggerAction matches, overriding lower-priority SO rules).
            var rule = new TransitionRule
            {
                TriggerAction = triggerAction,
                TargetState   = targetState,
                Priority      = priority,
                DebugName     = debugName,
                Conditions    = new System.Collections.Generic.List<ITransitionCondition>(),
            };

            client.AddRuntimeTransitionRule(rule);

            Debug.Log($"[MutationEngine] Runtime rule wired: {debugName} " +
                      $"({triggerAction} → {targetState}, priority {priority})");
        }
    }
}
