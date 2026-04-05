// ============================================================
// DESK 42 — Behaviour Tree Execution Context
//
// Passed to every BTNode.Tick() call. Carries everything a
// node could need to make a decision, plus a blackboard for
// inter-node communication within a single Tick traversal.
//
// Blackboard is cleared at the START of each full-tree Tick.
// Use it to pass data between sibling/parent/child nodes
// within one frame — e.g. "which card type was last injected".
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Desk42.BehaviourTrees
{
    public sealed class BTContext
    {
        // ── Core References ───────────────────────────────────

        // Set by ClientStateMachine before each Tick. Avoid storing
        // long-lived references to these in node state.
        public object   Client;     // ClientStateMachine (typed at BSM layer)
        public float    DeltaTime;

        // ── World State ───────────────────────────────────────

        public float  CurrentSanity;          // 0-100
        public float  CurrentSoulIntegrity;   // 0-100
        public float  CurrentImpatience;      // 0-100, how full the Impatience bar is
        public string LastCardTypeSlammed;    // PunchCardType enum name, "" if none this tick

        // ── Blackboard ────────────────────────────────────────
        // Cleared before every full-tree traversal.

        private readonly Dictionary<string, object> _blackboard = new(16);

        public void Set<T>(string key, T value)    => _blackboard[key] = value;
        public bool Has(string key)                => _blackboard.ContainsKey(key);
        public void Clear()                        => _blackboard.Clear();

        public T Get<T>(string key, T defaultValue = default)
        {
            if (_blackboard.TryGetValue(key, out var v) && v is T typed)
                return typed;
            return defaultValue;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_blackboard.TryGetValue(key, out var v) && v is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }
    }
}
