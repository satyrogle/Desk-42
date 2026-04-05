// ============================================================
// DESK 42 — Client State Stack
//
// Manages the injection stack on top of the base behaviour
// tree. Injected states (from punch cards) push here.
// The stack always executes the TOP entry.
// When a timer expires, that entry is popped and the next
// one below (or the base BT) takes over.
//
// This is the core mechanic of the Red Tape Engine:
//   Card slammed → InjectedState pushed → Client forced to
//   comply with the injected state for its Duration → Pop →
//   Base behaviour resumes.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.BSM
{
    public sealed class ClientStateStack
    {
        private readonly struct StackEntry
        {
            public readonly IClientState State;
            public readonly float        MaxDuration; // 0 = no timer
            public float                 Elapsed;

            public StackEntry(IClientState state)
            {
                State       = state;
                MaxDuration = state.Duration;
                Elapsed     = 0f;
            }

            public StackEntry WithElapsed(float e)
                => new StackEntry(State, MaxDuration, e);

            private StackEntry(IClientState s, float max, float elapsed)
            { State = s; MaxDuration = max; Elapsed = elapsed; }

            public bool HasTimer  => MaxDuration > 0f;
            public bool IsExpired => HasTimer && Elapsed >= MaxDuration;
        }

        private readonly Stack<StackEntry> _stack = new(4);

        // ── Queries ───────────────────────────────────────────

        public bool         IsEmpty       => _stack.Count == 0;
        public IClientState ActiveState   => IsEmpty ? null : _stack.Peek().State;
        public int          Depth         => _stack.Count;

        /// <summary>How many seconds remain on the current injection timer.</summary>
        public float TimeRemaining
        {
            get
            {
                if (IsEmpty) return 0f;
                var top = _stack.Peek();
                return top.HasTimer
                    ? Mathf.Max(0f, top.MaxDuration - top.Elapsed)
                    : float.PositiveInfinity;
            }
        }

        // ── Push ──────────────────────────────────────────────

        /// <summary>
        /// Push an injected state onto the stack.
        /// The previous active state is NOT exited — it resumes when this
        /// entry is eventually popped.
        /// </summary>
        public void Push(IClientState state, ClientContext ctx)
        {
            _stack.Push(new StackEntry(state));
            state.Enter(ctx);

            Debug.Log($"[StateStack] Pushed {state.StateID} " +
                      $"(duration: {(state.Duration > 0 ? state.Duration + "s" : "∞")}, " +
                      $"depth: {_stack.Count})");
        }

        // ── Tick ──────────────────────────────────────────────

        /// <summary>
        /// Tick the top state. Pops expired entries automatically.
        /// Returns false when the stack empties — the caller (ClientStateMachine)
        /// should then tick the base BT instead.
        /// </summary>
        public bool Tick(ClientContext ctx, float deltaTime)
        {
            while (!IsEmpty)
            {
                // Rebuild top entry with advanced elapsed time
                var entry = _stack.Pop();
                float newElapsed = entry.Elapsed + deltaTime;

                if (entry.IsExpired)
                {
                    // Timer ran out — exit the state and continue popping
                    entry.State.Exit(ctx);
                    Debug.Log($"[StateStack] Popped {entry.State.StateID} (timer expired).");
                    continue;
                }

                // Push back the updated entry (struct copy with new elapsed)
                _stack.Push(entry.WithElapsed(newElapsed));

                // Tick the state; if it requests early exit, pop it
                bool continueRunning = _stack.Peek().State.Tick(ctx, deltaTime);
                if (!continueRunning)
                {
                    var top = _stack.Pop();
                    top.State.Exit(ctx);
                    Debug.Log($"[StateStack] Popped {top.State.StateID} (self-terminated).");
                    continue;
                }

                return true; // Stack has an active state, base BT is paused
            }

            return false; // Stack empty — base BT should run
        }

        // ── Force Pop ─────────────────────────────────────────

        /// <summary>
        /// Pop the top state immediately (e.g. if a new high-priority
        /// card overrides the current injection).
        /// </summary>
        public void ForcePopTop(ClientContext ctx)
        {
            if (IsEmpty) return;
            var top = _stack.Pop();
            top.State.Exit(ctx);
            Debug.Log($"[StateStack] Force-popped {top.State.StateID}.");
        }

        /// <summary>Clear all injected states. Used on client exit / Fugue State.</summary>
        public void ClearAll(ClientContext ctx)
        {
            while (!IsEmpty)
            {
                var top = _stack.Pop();
                top.State.Exit(ctx);
            }
        }
    }
}
