// ============================================================
// DESK 42 — Card Fatigue Tracker
//
// Tracks how many times each card has been played this shift.
// At JamFatigue: card jams (brief lockout, then clears).
// At MaxFatigue:  card is crumpled and removed for the rest
//                 of the shift.
//
// This mechanic enforces the "read and adapt" design philosophy
// — optimal strategies can't be repeated indefinitely.
//
// Lives on the PunchCardMachine (one per shift).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Cards;

namespace Desk42.RedTape
{
    public sealed class CardFatigueTracker
    {
        private readonly Dictionary<string, int>   _fatigueMap = new();
        private readonly Dictionary<string, float> _jamTimers  = new();

        private const float JAM_LOCKOUT_DURATION = 2.5f; // seconds

        // ── Query ─────────────────────────────────────────────

        public int GetFatigue(string cardId)
        {
            _fatigueMap.TryGetValue(cardId, out int v);
            return v;
        }

        public bool IsJammed(string cardId)
            => _jamTimers.ContainsKey(cardId);

        public bool IsCrumpled(string cardId, PunchCardData data)
            => data.MaxFatigue >= 0 && GetFatigue(cardId) >= data.MaxFatigue;

        /// <summary>Can this card be played right now?</summary>
        public bool CanPlay(string cardId, PunchCardData data, out string reason)
        {
            if (IsJammed(cardId))
            {
                reason = $"JAMMED ({_jamTimers[cardId]:F1}s remaining)";
                return false;
            }

            if (IsCrumpled(cardId, data))
            {
                reason = "CRUMPLED — removed from deck";
                return false;
            }

            reason = "";
            return true;
        }

        // ── Record Play ───────────────────────────────────────

        /// <summary>
        /// Record one play of a card. Returns the fatigue outcome.
        /// </summary>
        public FatigueOutcome RecordPlay(string cardId, PunchCardData data)
        {
            _fatigueMap.TryGetValue(cardId, out int current);
            int newFatigue = current + 1;
            _fatigueMap[cardId] = newFatigue;

            // Check jam threshold
            if (data.JamFatigue >= 0 && newFatigue == data.JamFatigue)
            {
                _jamTimers[cardId] = JAM_LOCKOUT_DURATION;
                Debug.Log($"[FatigueTracker] Card {data.DisplayName} JAMMED (fatigue {newFatigue})");
                return FatigueOutcome.Jammed;
            }

            // Check crumple threshold
            if (data.MaxFatigue >= 0 && newFatigue >= data.MaxFatigue)
            {
                Debug.Log($"[FatigueTracker] Card {data.DisplayName} CRUMPLED (fatigue {newFatigue})");
                return FatigueOutcome.Crumpled;
            }

            return FatigueOutcome.Normal;
        }

        // ── Tick (jam timers) ─────────────────────────────────

        public void Tick(float deltaTime)
        {
            if (_jamTimers.Count == 0) return;

            var toRemove = new List<string>(2);
            foreach (var kv in _jamTimers)
            {
                float remaining = kv.Value - deltaTime;
                if (remaining <= 0f)
                    toRemove.Add(kv.Key);
                else
                    _jamTimers[kv.Key] = remaining;
            }

            foreach (var id in toRemove)
            {
                _jamTimers.Remove(id);
                Debug.Log($"[FatigueTracker] Card {id} jam cleared.");
            }
        }

        // ── Reset ─────────────────────────────────────────────

        /// <summary>
        /// Reset all fatigue between shifts.
        /// Some Employee Handbook benefits can reduce per-shift fatigue accumulation.
        /// </summary>
        public void ResetForNewShift()
        {
            _fatigueMap.Clear();
            _jamTimers.Clear();
        }

        // ── Archetype Interaction ─────────────────────────────

        /// <summary>
        /// IT Person archetype: spend a Debug token to reset fatigue
        /// on a specific card.
        /// </summary>
        public void ResetCardFatigue(string cardId)
        {
            _fatigueMap.Remove(cardId);
            _jamTimers.Remove(cardId);
        }

        // ── Enum ─────────────────────────────────────────────

        public enum FatigueOutcome
        {
            Normal,   // played successfully, fatigue incremented
            Jammed,   // hit jam threshold — brief lockout
            Crumpled, // hit max threshold — removed for this shift
        }
    }
}
