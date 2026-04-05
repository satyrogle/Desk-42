// ============================================================
// DESK 42 — BT Decorator Nodes
//
// Decorators wrap a single child and modify its result or
// execution conditions.
//
//   BTInverter          — flips Success/Failure
//   BTConditionalGate   — only runs child if condition passes
//   BTCooldownDecorator — enforces minimum time between runs
//   BTRepeatDecorator   — repeats child N times or until failure
//   BTBlockerNode       — always fails; blocks card type (mutation)
// ============================================================

using System;
using UnityEngine;

namespace Desk42.BehaviourTrees
{
    // ── Abstract Decorator Base ───────────────────────────────

    public abstract class BTDecorator : BTNode
    {
        protected BTNode Child;

        protected BTDecorator(BTNode child)
        {
            Child = child ?? throw new ArgumentNullException(nameof(child));
        }

        public override void Reset()
        {
            base.Reset();
            Child.Reset();
        }
    }

    // ── Inverter ──────────────────────────────────────────────
    // Swaps Success ↔ Failure. Running passes through unchanged.

    public sealed class BTInverter : BTDecorator
    {
        public BTInverter(BTNode child) : base(child)
            => DebugLabel = $"NOT({child.DebugLabel})";

        protected override BTStatus Execute(BTContext ctx)
        {
            return Child.Tick(ctx) switch
            {
                BTStatus.Success => BTStatus.Failure,
                BTStatus.Failure => BTStatus.Success,
                _                => BTStatus.Running,
            };
        }
    }

    // ── Conditional Gate ─────────────────────────────────────
    // Evaluates the predicate ONCE per run (on OnStart).
    // If predicate fails, returns Failure immediately without
    // ticking the child. If predicate passes, child runs normally.

    public sealed class BTConditionalGate : BTDecorator
    {
        private readonly Func<BTContext, bool> _condition;
        private bool _conditionPassed;

        public BTConditionalGate(string label, Func<BTContext, bool> condition, BTNode child)
            : base(child)
        {
            DebugLabel = label;
            _condition = condition;
        }

        protected override void OnStart(BTContext ctx)
            => _conditionPassed = _condition(ctx);

        protected override BTStatus Execute(BTContext ctx)
        {
            if (!_conditionPassed) return BTStatus.Failure;
            return Child.Tick(ctx);
        }

        public override void Reset()
        {
            base.Reset();
            _conditionPassed = false;
        }
    }

    // ── Cooldown ──────────────────────────────────────────────
    // After child completes (Success or Failure), blocks this
    // node for `cooldownSeconds`. Returns Failure while cooling.
    // Tracks elapsed time via context DeltaTime.

    public sealed class BTCooldownDecorator : BTDecorator
    {
        private readonly float _cooldownSeconds;
        private float _cooldownRemaining;

        public BTCooldownDecorator(float cooldownSeconds, BTNode child) : base(child)
        {
            _cooldownSeconds = cooldownSeconds;
            DebugLabel       = $"Cooldown({cooldownSeconds:F1}s, {child.DebugLabel})";
        }

        protected override BTStatus Execute(BTContext ctx)
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= ctx.DeltaTime;
                return BTStatus.Failure;
            }

            var status = Child.Tick(ctx);
            if (status != BTStatus.Running)
                _cooldownRemaining = _cooldownSeconds;

            return status;
        }

        public override void Reset()
        {
            base.Reset();
            _cooldownRemaining = 0f;
        }
    }

    // ── Repeat ────────────────────────────────────────────────
    // Repeats child up to `maxRepeats` times (-1 = infinite).
    // Stops and returns Failure if child returns Failure.
    // Returns Success after maxRepeats successful completions.

    public sealed class BTRepeatDecorator : BTDecorator
    {
        private readonly int _maxRepeats;
        private int _completedCount;

        public BTRepeatDecorator(int maxRepeats, BTNode child) : base(child)
        {
            _maxRepeats = maxRepeats;
            DebugLabel  = maxRepeats < 0
                ? $"RepeatForever({child.DebugLabel})"
                : $"Repeat({maxRepeats}, {child.DebugLabel})";
        }

        protected override void OnStart(BTContext ctx) => _completedCount = 0;

        protected override BTStatus Execute(BTContext ctx)
        {
            var status = Child.Tick(ctx);

            if (status == BTStatus.Failure) return BTStatus.Failure;
            if (status == BTStatus.Running) return BTStatus.Running;

            // Success — increment and check
            _completedCount++;
            Child.Reset();

            if (_maxRepeats >= 0 && _completedCount >= _maxRepeats)
                return BTStatus.Success;

            return BTStatus.Running; // keep repeating
        }

        public override void Reset()
        {
            base.Reset();
            _completedCount = 0;
        }
    }

    // ── Blocker Node (Mutation counter-node) ──────────────────
    // Inserted by MutationEngine when a card type is overused.
    // Immediately fails, preventing the parent Sequence from
    // completing — this is the "Pre-Filed Exemption" in the GDD.
    //
    // The blockerCardType is metadata for the debug visualizer
    // and for the StateInjector to check before slamming a card.

    public sealed class BTBlockerNode : BTNode
    {
        public readonly string BlockedCardType; // PunchCardType name
        public readonly string CounterTraitId;  // which trait generated this

        public BTBlockerNode(string blockedCardType, string counterTraitId)
        {
            BlockedCardType = blockedCardType;
            CounterTraitId  = counterTraitId;
            DebugLabel      = $"BLOCKED[{blockedCardType}]({counterTraitId})";
        }

        // Always fails immediately — blocks parent Sequence
        protected override BTStatus Execute(BTContext ctx) => BTStatus.Failure;
    }
}
