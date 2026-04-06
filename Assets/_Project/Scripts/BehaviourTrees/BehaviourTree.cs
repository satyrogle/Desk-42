// ============================================================
// DESK 42 — Behaviour Tree
//
// Wrapper around a root BTNode. Owns the BTContext and manages
// the tick budget so the tree never takes more than its
// allotted CPU time per frame.
//
// Key method: Tick(deltaTime) — call every frame.
// The tree holds execution state internally, so partial
// traversals resume correctly next frame.
//
// Mutation API:
//   InsertBlockerForCard(cardType, counterTraitId)
//     — MutationEngine calls this to insert counter-nodes
//   HasBlockerForCard(cardType) → bool
//     — StateInjector calls this before accepting a card slam
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Desk42.BehaviourTrees
{
    public sealed class BehaviourTree
    {
        // ── State ─────────────────────────────────────────────

        private BTNode    _root;
        private BTContext _context;

        // All BTBlockerNodes currently in the tree, keyed by card type
        private readonly Dictionary<string, BTBlockerNode> _blockers
            = new(4);

        // ── Init ──────────────────────────────────────────────

        public BehaviourTree(BTNode root, object clientRef)
        {
            _root    = root;
            _context = new BTContext { Client = clientRef };
        }

        // ── Tick ──────────────────────────────────────────────

        /// <summary>
        /// Advance the tree by one frame.
        /// Returns the status of the root node.
        /// If the tree completes (Success/Failure), it auto-resets
        /// for the next frame — client behaviours loop naturally.
        /// </summary>
        public BTStatus Tick(float deltaTime, float sanity,
            float soulIntegrity, float impatience,
            string lastCardTypeSlammed = "")
        {
            if (_paused) return BTStatus.Running;

            _context.DeltaTime           = deltaTime;
            _context.CurrentSanity       = sanity;
            _context.CurrentSoulIntegrity = soulIntegrity;
            _context.CurrentImpatience   = impatience;
            _context.LastCardTypeSlammed = lastCardTypeSlammed;
            _context.Clear();

            var status = _root.Tick(_context);

            // Auto-reset so the loop continues naturally
            if (status != BTStatus.Running)
                _root.Reset();

            return status;
        }

        // ── Pause / Resume ────────────────────────────────────

        private bool _paused;

        /// <summary>
        /// Pause the tree — Tick() returns Running without executing.
        /// Called by ClientStateStack when an injected state is active.
        /// </summary>
        public void Pause()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
            // Reset the root so behaviour restarts cleanly after pause
            _root.Reset();
        }

        public bool IsPaused => _paused;

        // ── Mutation: Blocker Injection ───────────────────────

        /// <summary>
        /// Insert a BTBlockerNode at the START of the root Selector/Sequence.
        /// The blocker is checked by the tree on the next tick.
        /// StateInjector also calls HasBlockerForCard() before accepting
        /// the card — if a blocker exists, the injection fails with
        /// a "Pre-Filed Exemption" notification.
        /// </summary>
        public void InsertBlockerForCard(string cardType, string counterTraitId)
        {
            if (_blockers.ContainsKey(cardType))
            {
                Debug.Log($"[BehaviourTree] Blocker for {cardType} already exists — skipping.");
                return;
            }

            var blocker = new BTBlockerNode(cardType, counterTraitId);
            _blockers[cardType] = blocker;

            // Walk to first composite node in root and prepend
            if (_root is BTCompositeNode composite)
            {
                composite.InsertChild(0, blocker);
                Debug.Log($"[BehaviourTree] Blocker inserted for {cardType} ({counterTraitId}).");
            }
            else
            {
                // Root is not a composite — wrap it
                var wrapper = new BTSelector();
                wrapper.AddChild(blocker);  // blocker checked first, fails fast
                wrapper.AddChild(_root);    // original root
                _root = wrapper;
                Debug.Log($"[BehaviourTree] Root wrapped to insert blocker for {cardType}.");
            }
        }

        public bool HasBlockerForCard(string cardType)
            => _blockers.ContainsKey(cardType);

        public IReadOnlyDictionary<string, BTBlockerNode> AllBlockers => _blockers;

        // ── Debug ─────────────────────────────────────────────

        public BTNode Root => _root;

        public string DumpTree()
        {
            var sb = new System.Text.StringBuilder();
            DumpNode(_root, sb, 0);
            return sb.ToString();
        }

        private static void DumpNode(BTNode node, System.Text.StringBuilder sb, int depth)
        {
            sb.Append(new string(' ', depth * 2));
            sb.AppendLine($"[{node.LastStatus}] {node}");

            if (node is BTCompositeNode c)
                foreach (var child in c.Children)
                    DumpNode(child, sb, depth + 1);
        }
    }
}
