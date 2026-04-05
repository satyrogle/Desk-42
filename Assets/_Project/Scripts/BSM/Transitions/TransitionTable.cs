// ============================================================
// DESK 42 — Transition Table (ScriptableObject)
//
// A ScriptableObject asset that holds the complete set of
// TransitionRules for a client template.
//
// How it works:
//   1. Sort all rules by Priority (descending).
//   2. Evaluate each rule against the current TransitionContext.
//   3. Return the TargetState of the FIRST matching rule.
//   4. If no rule matches, return the fallback state.
//
// One TransitionTable per client TEMPLATE (not per instance).
// Rare/boss clients can have unique tables.
// Default table covers most standard clients.
//
// Designer workflow:
//   Create > Desk42 > BSM > TransitionTable
//   Add rules in the Inspector, set conditions via code/editor.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.BSM.Transitions
{
    [CreateAssetMenu(
        menuName = "Desk42/BSM/Transition Table",
        fileName = "TransitionTable_Default")]
    public sealed class TransitionTable : ScriptableObject
    {
        [SerializeField]
        private ClientStateID _fallbackState = ClientStateID.Pending;

        [SerializeField]
        private List<TransitionRule> _rules = new();

        // ── Sorted cache ──────────────────────────────────────

        private List<TransitionRule> _sortedRules;
        private bool                 _isDirty = true;

        private void OnValidate() => _isDirty = true;

        // ── Resolution ────────────────────────────────────────

        /// <summary>
        /// Evaluates all rules against the context and returns the
        /// target state of the highest-priority matching rule.
        /// Returns _fallbackState if no rule matches.
        /// </summary>
        public ClientStateID Resolve(TransitionContext ctx)
        {
            EnsureSorted();

            foreach (var rule in _sortedRules)
            {
                if (rule.Evaluate(ctx))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[TransitionTable] Rule matched: {rule} -> {rule.TargetState}");
#endif
                    return rule.TargetState;
                }
            }

            return _fallbackState;
        }

        // ── Runtime Rule Injection ────────────────────────────
        // Used by MutationEngine to add counter-trait rules at runtime
        // without modifying the shared SO asset.

        private List<TransitionRule> _runtimeRules;

        /// <summary>
        /// Add a runtime-only rule for this encounter instance.
        /// Does not modify the SO asset — cleared when client exits.
        /// </summary>
        public void AddRuntimeRule(TransitionRule rule)
        {
            _runtimeRules ??= new List<TransitionRule>();
            _runtimeRules.Add(rule);
            _isDirty = true;
        }

        public void ClearRuntimeRules()
        {
            _runtimeRules?.Clear();
            _isDirty = true;
        }

        // ── Factory: Default Table ────────────────────────────
        // Creates a basic transition table in code for prototyping.
        // Replace with designed SO assets for production.

        public static TransitionTable CreateDefault()
        {
            var table = CreateInstance<TransitionTable>();
            table.name = "TransitionTable_Default_Runtime";

            // THREATEN_AUDIT → AGITATED unless repeat offender (then LITIGIOUS)
            table._rules.Add(new TransitionRule
            {
                TriggerAction = nameof(PunchCardType.ThreatAudit),
                TargetState   = ClientStateID.Agitated,
                Priority      = 10,
                DebugName     = "Threaten -> Agitated (default)",
                Conditions    = new List<ITransitionCondition>
                {
                    new IsRepeatOffenderCondition { }  // inverted — only if NOT repeat
                }
            });

            table._rules.Add(new TransitionRule
            {
                TriggerAction = nameof(PunchCardType.ThreatAudit),
                TargetState   = ClientStateID.Litigious,
                Priority      = 20, // higher priority
                DebugName     = "Threaten -> Litigious (repeat offender)",
                Conditions    = new List<ITransitionCondition>
                {
                    new IsRepeatOffenderCondition()
                }
            });

            // Low soul integrity makes REDACT more likely to backfire → PARANOID
            table._rules.Add(new TransitionRule
            {
                TriggerAction = nameof(PunchCardType.Redact),
                TargetState   = ClientStateID.Paranoid,
                Priority      = 30,
                DebugName     = "Redact -> Paranoid (low soul)",
                Conditions    = new List<ITransitionCondition>
                {
                    new SoulIntegrityBelowCondition { Threshold = 40f }
                }
            });

            table._rules.Add(new TransitionRule
            {
                TriggerAction = nameof(PunchCardType.Redact),
                TargetState   = ClientStateID.Suspicious,
                Priority      = 10,
                DebugName     = "Redact -> Suspicious (default)",
            });

            table._fallbackState = ClientStateID.Pending;
            return table;
        }

        // ── Private ───────────────────────────────────────────

        private void EnsureSorted()
        {
            if (!_isDirty && _sortedRules != null) return;

            _sortedRules = new List<TransitionRule>(_rules);
            if (_runtimeRules != null)
                _sortedRules.AddRange(_runtimeRules);

            // Descending priority — highest fires first
            _sortedRules.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            _isDirty = false;
        }
    }
}
