// ============================================================
// DESK 42 — BT Leaf Nodes
//
// All single-purpose leaf nodes live here:
//   BTActionNode   — delegate-based action
//   BTConditionNode — delegate-based guard (Success/Failure only)
//   BTWaitNode     — waits for duration, then succeeds
//   BTAlwaysNode   — always returns a fixed status (testing utility)
// ============================================================

using System;
using UnityEngine;

namespace Desk42.BehaviourTrees
{
    // ── Action ────────────────────────────────────────────────
    // The primary leaf type. Executes a C# delegate each tick.
    // The delegate returns BTStatus — Return Running to span frames.
    //
    // Usage:
    //   new BTActionNode("DrumOnDesk", ctx => {
    //       ... play animation ...
    //       return done ? BTStatus.Success : BTStatus.Running;
    //   });

    public sealed class BTActionNode : BTNode
    {
        private readonly Func<BTContext, BTStatus> _action;

        public BTActionNode(string label, Func<BTContext, BTStatus> action)
        {
            DebugLabel = label;
            _action    = action ?? throw new ArgumentNullException(nameof(action));
        }

        protected override BTStatus Execute(BTContext ctx) => _action(ctx);
    }

    // ── Condition ─────────────────────────────────────────────
    // Instant check — never Running. Used as guards in Sequences.
    //
    // Usage:
    //   new BTConditionNode("IsAgitated", ctx =>
    //       ctx.Get<ClientStateID>("MoodState") == ClientStateID.Agitated)

    public sealed class BTConditionNode : BTNode
    {
        private readonly Func<BTContext, bool> _predicate;

        public BTConditionNode(string label, Func<BTContext, bool> predicate)
        {
            DebugLabel = label;
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        protected override BTStatus Execute(BTContext ctx)
            => _predicate(ctx) ? BTStatus.Success : BTStatus.Failure;
    }

    // ── Wait ──────────────────────────────────────────────────
    // Runs for `duration` seconds then returns Success.
    // Useful for "idle for 2 seconds before trying next behaviour."

    public sealed class BTWaitNode : BTNode
    {
        private readonly float _duration;
        private float          _elapsed;

        public BTWaitNode(float duration, string label = "")
        {
            _duration  = duration;
            DebugLabel = string.IsNullOrEmpty(label) ? $"Wait({duration:F1}s)" : label;
        }

        protected override void OnStart(BTContext ctx) => _elapsed = 0f;

        protected override BTStatus Execute(BTContext ctx)
        {
            _elapsed += ctx.DeltaTime;
            return _elapsed >= _duration ? BTStatus.Success : BTStatus.Running;
        }

        public override void Reset()
        {
            base.Reset();
            _elapsed = 0f;
        }
    }

    // ── Always ────────────────────────────────────────────────
    // Returns a fixed status. Useful for testing and as
    // placeholder stubs during development.

    public sealed class BTAlwaysNode : BTNode
    {
        private readonly BTStatus _status;

        public BTAlwaysNode(BTStatus status)
        {
            _status    = status;
            DebugLabel = $"Always{status}";
        }

        protected override BTStatus Execute(BTContext ctx) => _status;
    }
}
