// ============================================================
// DESK 42 — Behaviour Tree Node (Abstract Base)
//
// All BT nodes derive from this. The tree is pure C# — no
// MonoBehaviours — so nodes are heap-allocated objects that
// can be unit tested, pooled, and cloned freely.
//
// Lifecycle:
//   OnStart()   — called once when node first returns Running
//   Tick()      — called every frame while node is active
//   OnEnd()     — called once when node exits (Success/Failure)
//   Reset()     — returns node to pre-start state (for reuse)
// ============================================================

namespace Desk42.BehaviourTrees
{
    public abstract class BTNode
    {
        // Cached status so composites can know if a child is mid-run
        public  BTStatus LastStatus   { get; protected set; } = BTStatus.Failure;
        public  bool     IsRunning    => LastStatus == BTStatus.Running;
        public  string   DebugLabel   { get; set; } = "";

        // ── Core API ──────────────────────────────────────────

        public BTStatus Tick(BTContext ctx)
        {
            if (LastStatus != BTStatus.Running)
                OnStart(ctx);

            LastStatus = Execute(ctx);

            if (LastStatus != BTStatus.Running)
                OnEnd(ctx, LastStatus);

            return LastStatus;
        }

        protected abstract BTStatus Execute(BTContext ctx);

        // ── Lifecycle Hooks ───────────────────────────────────

        protected virtual void OnStart(BTContext ctx) { }
        protected virtual void OnEnd(BTContext ctx, BTStatus finalStatus) { }

        /// <summary>
        /// Reset this node back to its initial state.
        /// Called by composites when the tree is interrupted mid-run.
        /// </summary>
        public virtual void Reset()
        {
            LastStatus = BTStatus.Failure;
        }

        // ── Debug ─────────────────────────────────────────────

        public override string ToString()
            => string.IsNullOrEmpty(DebugLabel)
                ? GetType().Name
                : $"{GetType().Name}({DebugLabel})";
    }
}
