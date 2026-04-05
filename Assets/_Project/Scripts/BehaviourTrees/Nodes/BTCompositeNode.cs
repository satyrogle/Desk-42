// ============================================================
// DESK 42 — Composite Node Base
// Shared base for Selector and Sequence.
// Children can be added, removed, and reordered at runtime
// — this is what the MutationEngine exploits to inject
// counter-nodes into a running tree.
// ============================================================

using System.Collections.Generic;

namespace Desk42.BehaviourTrees
{
    public abstract class BTCompositeNode : BTNode
    {
        protected readonly List<BTNode> _children = new(8);
        protected int _runningIndex;   // which child is mid-run

        public IReadOnlyList<BTNode> Children => _children;

        public void AddChild(BTNode child)    => _children.Add(child);
        public void RemoveChild(BTNode child) => _children.Remove(child);

        /// <summary>
        /// Insert a child at a specific index. Used by MutationEngine to
        /// prepend high-priority counter-nodes before existing children.
        /// </summary>
        public void InsertChild(int index, BTNode child)
            => _children.Insert(index, child);

        public override void Reset()
        {
            base.Reset();
            _runningIndex = 0;
            foreach (var c in _children) c.Reset();
        }
    }

    // ── Selector ──────────────────────────────────────────────
    // Tries children in order. Returns Success as soon as one
    // succeeds. Returns Failure only if ALL children fail.
    // Resumes from the running child on subsequent ticks.

    public sealed class BTSelector : BTCompositeNode
    {
        protected override BTStatus Execute(BTContext ctx)
        {
            for (int i = _runningIndex; i < _children.Count; i++)
            {
                var status = _children[i].Tick(ctx);

                switch (status)
                {
                    case BTStatus.Success:
                        _runningIndex = 0;
                        ResetFrom(i + 1);
                        return BTStatus.Success;

                    case BTStatus.Running:
                        _runningIndex = i;
                        return BTStatus.Running;

                    case BTStatus.Failure:
                        // Try next child
                        break;
                }
            }

            _runningIndex = 0;
            return BTStatus.Failure;
        }

        private void ResetFrom(int startIndex)
        {
            for (int i = startIndex; i < _children.Count; i++)
                _children[i].Reset();
        }
    }

    // ── Sequence ──────────────────────────────────────────────
    // Runs children in order. Returns Failure as soon as one
    // fails. Returns Success only when ALL children succeed.
    // Resumes from the running child on subsequent ticks.

    public sealed class BTSequence : BTCompositeNode
    {
        protected override BTStatus Execute(BTContext ctx)
        {
            for (int i = _runningIndex; i < _children.Count; i++)
            {
                var status = _children[i].Tick(ctx);

                switch (status)
                {
                    case BTStatus.Failure:
                        _runningIndex = 0;
                        ResetFrom(i + 1);
                        return BTStatus.Failure;

                    case BTStatus.Running:
                        _runningIndex = i;
                        return BTStatus.Running;

                    case BTStatus.Success:
                        // Continue to next child
                        break;
                }
            }

            _runningIndex = 0;
            return BTStatus.Success;
        }

        private void ResetFrom(int startIndex)
        {
            for (int i = startIndex; i < _children.Count; i++)
                _children[i].Reset();
        }
    }

    // ── Parallel ──────────────────────────────────────────────
    // Ticks ALL children every frame regardless of status.
    // Succeeds when successThreshold children succeed.
    // Fails when (children.Count - successThreshold + 1) fail.
    // Default: succeed when any one child succeeds.

    public sealed class BTParallel : BTCompositeNode
    {
        private readonly int _successThreshold;

        public BTParallel(int successThreshold = 1)
            => _successThreshold = successThreshold;

        protected override BTStatus Execute(BTContext ctx)
        {
            int successes = 0, failures = 0;

            foreach (var child in _children)
            {
                var s = child.Tick(ctx);
                if (s == BTStatus.Success) successes++;
                else if (s == BTStatus.Failure) failures++;
            }

            if (successes >= _successThreshold) return BTStatus.Success;
            if (failures  > _children.Count - _successThreshold) return BTStatus.Failure;
            return BTStatus.Running;
        }
    }
}
