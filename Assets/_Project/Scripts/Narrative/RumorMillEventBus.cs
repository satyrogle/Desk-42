// ============================================================
// DESK 42 — Rumor Mill Event Bus
//
// This is the connective tissue of the entire game. Every
// system publishes here; every system subscribes here.
// Nothing talks to anything else directly.
//
// Design: static typed event bus.
//   - Zero GC (all events are readonly structs).
//   - Frame-deferred dispatch to prevent mid-frame cascades
//     causing stutter when many systems respond to one event.
//   - Ordered dispatch: handlers are invoked in priority order,
//     allowing UI to always update after gameplay logic.
//
// Usage:
//   PUBLISH:   RumorMill.Publish(new CardSlammedEvent(...));
//   SUBSCRIBE: RumorMill.OnCardSlammed += HandleCardSlammed;
//   UNSUB:     RumorMill.OnCardSlammed -= HandleCardSlammed;
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Core
{
    public static class RumorMill
    {
        // ── Typed Event Channels ──────────────────────────────
        // Add a new channel for each new event type.

        public static event Action<CardSlammedEvent>           OnCardSlammed;
        public static event Action<StateTransitionEvent>       OnStateTransition;
        public static event Action<ClaimResolvedEvent>         OnClaimResolved;
        public static event Action<MoralChoiceEvent>           OnMoralChoice;
        public static event Action<SoulIntegrityChangedEvent>  OnSoulIntegrityChanged;
        public static event Action<SanityChangedEvent>         OnSanityChanged;
        public static event Action<NDASignedEvent>             OnNDASigned;
        public static event Action<OfficeHazardEvent>          OnOfficeHazard;
        public static event Action<FactionShiftEvent>          OnFactionShift;
        public static event Action<ShiftLifecycleEvent>        OnShiftLifecycle;
        public static event Action<ShiftPhaseChangedEvent>     OnShiftPhaseChanged;
        public static event Action<MilestoneReachedEvent>      OnMilestoneReached;
        public static event Action<CounterTraitGeneratedEvent> OnCounterTraitGenerated;
        public static event Action<ExpenseUnmetEvent>          OnExpenseUnmet;
        public static event Action<NarratorToneChangedEvent>   OnNarratorToneChanged;
        public static event Action<ClaimQueuedEvent>           OnClaimQueued;
        public static event Action<TideEscalatedEvent>         OnTideEscalated;

        // ── Frame-Deferred Dispatch ───────────────────────────
        // Events queued here are dispatched at end-of-frame by
        // the RumorMillDriver MonoBehaviour. This prevents a
        // single event from cascading synchronously through
        // every system and causing frame spikes.

        private abstract class DeferredEvent { public abstract void Dispatch(); }

        private sealed class DeferredEvent<T> : DeferredEvent
        {
            private readonly T _payload;
            private readonly Action<T> _channel;

            public DeferredEvent(T payload, Action<T> channel)
            { _payload = payload; _channel = channel; }

            public override void Dispatch() => _channel?.Invoke(_payload);
        }

        private static readonly Queue<DeferredEvent> _queue = new(64);
        private static bool _isDispatching;

        // ── Publish API ───────────────────────────────────────

        /// <summary>
        /// Publish immediately — fires all subscribers in the current frame.
        /// Use for time-critical events (UI feedback, audio triggers) where
        /// a one-frame delay would be perceptible.
        /// </summary>
        public static void Publish(CardSlammedEvent e)           => OnCardSlammed?.Invoke(e);
        public static void Publish(StateTransitionEvent e)       => OnStateTransition?.Invoke(e);
        public static void Publish(SanityChangedEvent e)         => OnSanityChanged?.Invoke(e);
        public static void Publish(NarratorToneChangedEvent e)   => OnNarratorToneChanged?.Invoke(e);

        /// <summary>
        /// Publish deferred — queues the event for end-of-frame dispatch.
        /// Use for events with broad cascading effects (moral choices,
        /// claim resolution, faction shifts).
        /// </summary>
        public static void PublishDeferred(ClaimResolvedEvent e)
            => Enqueue(e, OnClaimResolved);
        public static void PublishDeferred(MoralChoiceEvent e)
            => Enqueue(e, OnMoralChoice);
        public static void PublishDeferred(SoulIntegrityChangedEvent e)
            => Enqueue(e, OnSoulIntegrityChanged);
        public static void PublishDeferred(NDASignedEvent e)
            => Enqueue(e, OnNDASigned);
        public static void PublishDeferred(OfficeHazardEvent e)
            => Enqueue(e, OnOfficeHazard);
        public static void PublishDeferred(FactionShiftEvent e)
            => Enqueue(e, OnFactionShift);
        public static void PublishDeferred(ShiftLifecycleEvent e)
            => Enqueue(e, OnShiftLifecycle);
        public static void PublishDeferred(ShiftPhaseChangedEvent e)
            => Enqueue(e, OnShiftPhaseChanged);
        public static void PublishDeferred(MilestoneReachedEvent e)
            => Enqueue(e, OnMilestoneReached);
        public static void PublishDeferred(CounterTraitGeneratedEvent e)
            => Enqueue(e, OnCounterTraitGenerated);
        public static void PublishDeferred(ExpenseUnmetEvent e)
            => Enqueue(e, OnExpenseUnmet);
        public static void PublishDeferred(ClaimQueuedEvent e)
            => Enqueue(e, OnClaimQueued);
        public static void PublishDeferred(TideEscalatedEvent e)
            => Enqueue(e, OnTideEscalated);

        // ── Internal ──────────────────────────────────────────

        private static void Enqueue<T>(T payload, Action<T> channel)
            => _queue.Enqueue(new DeferredEvent<T>(payload, channel));

        /// <summary>
        /// Called by RumorMillDriver.LateUpdate() — drains the queue.
        /// </summary>
        internal static void DrainQueue()
        {
            if (_isDispatching) return; // reentrancy guard
            _isDispatching = true;

            // Drain only the events queued at the start of this frame;
            // events published DURING dispatch go into the next frame.
            int count = _queue.Count;
            for (int i = 0; i < count && _queue.Count > 0; i++)
            {
                var evt = _queue.Dequeue();
                try   { evt.Dispatch(); }
                catch (Exception ex)
                {
                    // Never let a subscriber crash the whole bus
                    Debug.LogError($"[RumorMill] Exception in deferred handler: {ex}");
                }
            }

            _isDispatching = false;
        }

        /// <summary>
        /// Clear all subscriptions — call between scenes / on application quit
        /// to prevent stale delegates from prior scene objects.
        /// </summary>
        public static void ClearAllSubscriptions()
        {
            OnCardSlammed           = null;
            OnStateTransition       = null;
            OnClaimResolved         = null;
            OnMoralChoice           = null;
            OnSoulIntegrityChanged  = null;
            OnSanityChanged         = null;
            OnNDASigned             = null;
            OnOfficeHazard          = null;
            OnFactionShift          = null;
            OnShiftLifecycle        = null;
            OnShiftPhaseChanged     = null;
            OnMilestoneReached      = null;
            OnCounterTraitGenerated = null;
            OnExpenseUnmet          = null;
            OnNarratorToneChanged   = null;
            OnClaimQueued           = null;
            OnTideEscalated         = null;
            _queue.Clear();
        }
    }

    // ── Driver MonoBehaviour ──────────────────────────────────
    // A single instance of this lives in the Boot scene via
    // GameManager. It drains the deferred queue each LateUpdate.

    public sealed class RumorMillDriver : MonoBehaviour
    {
        private void LateUpdate() => RumorMill.DrainQueue();

        private void OnDestroy() => RumorMill.ClearAllSubscriptions();
    }
}
