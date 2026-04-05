// ============================================================
// DESK 42 — Transition Rule
//
// A single conditional edge in the BSM transition graph.
// The TransitionTable holds a list of these and resolves
// which one fires given the current TransitionContext.
//
// Each rule has:
//   TriggerAction    — which player action activates this rule
//   Conditions       — ALL must pass for the rule to fire
//   TargetState      — the BSM state to transition to
//   Priority         — higher-priority rules evaluated first
//
// The result: the same card can produce 5+ different outcomes
// depending on species, counter-traits, soul integrity, etc.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.BSM.Transitions
{
    // ── Transition Context ────────────────────────────────────

    public sealed class TransitionContext
    {
        public string         TriggerAction;       // PunchCardType name or "Organic_<event>"
        public string         ClientSpeciesId;
        public List<string>   ActiveCounterTraits;
        public ClientStateID  CurrentState;
        public float          SoulIntegrity;       // 0-100
        public float          ImpatienceRatio;     // 0-1
        public float          LegalReputation;
        public float          ManagementReputation;
        public int            VisitCount;
        public bool           HiddenTraitRevealed;
        public float          ClaimCorruption;
        public string         OfficeTemp;
    }

    // ── Condition Interface ───────────────────────────────────

    public interface ITransitionCondition
    {
        bool Evaluate(TransitionContext ctx);
        string Description { get; } // for debug inspector
    }

    // ── Built-in Conditions ───────────────────────────────────

    [Serializable]
    public sealed class IsRepeatOffenderCondition : ITransitionCondition
    {
        public bool Evaluate(TransitionContext ctx) => ctx.VisitCount > 0;
        public string Description => "Is Repeat Offender";
    }

    [Serializable]
    public sealed class SoulIntegrityBelowCondition : ITransitionCondition
    {
        public float Threshold;
        public bool Evaluate(TransitionContext ctx) => ctx.SoulIntegrity < Threshold;
        public string Description => $"Soul < {Threshold}";
    }

    [Serializable]
    public sealed class SoulIntegrityAboveCondition : ITransitionCondition
    {
        public float Threshold;
        public bool Evaluate(TransitionContext ctx) => ctx.SoulIntegrity >= Threshold;
        public string Description => $"Soul >= {Threshold}";
    }

    [Serializable]
    public sealed class ImpatienceAboveCondition : ITransitionCondition
    {
        public float Threshold; // 0-1
        public bool Evaluate(TransitionContext ctx) => ctx.ImpatienceRatio > Threshold;
        public string Description => $"Impatience > {Threshold:P0}";
    }

    [Serializable]
    public sealed class HasCounterTraitCondition : ITransitionCondition
    {
        public string TraitId;
        public bool Evaluate(TransitionContext ctx)
            => ctx.ActiveCounterTraits != null && ctx.ActiveCounterTraits.Contains(TraitId);
        public string Description => $"Has Trait: {TraitId}";
    }

    [Serializable]
    public sealed class CurrentStateIsCondition : ITransitionCondition
    {
        public ClientStateID RequiredState;
        public bool Evaluate(TransitionContext ctx) => ctx.CurrentState == RequiredState;
        public string Description => $"State == {RequiredState}";
    }

    [Serializable]
    public sealed class SpeciesIsCondition : ITransitionCondition
    {
        public string SpeciesId;
        public bool Evaluate(TransitionContext ctx)
            => string.Equals(ctx.ClientSpeciesId, SpeciesId,
                StringComparison.OrdinalIgnoreCase);
        public string Description => $"Species == {SpeciesId}";
    }

    [Serializable]
    public sealed class OfficeTempIsCondition : ITransitionCondition
    {
        public string RequiredTemp; // "PEAK_EFFICIENCY", "LUNCH_BREAK", "SYSTEM_CRASH"
        public bool Evaluate(TransitionContext ctx)
            => string.Equals(ctx.OfficeTemp, RequiredTemp, StringComparison.OrdinalIgnoreCase);
        public string Description => $"OfficeTemp == {RequiredTemp}";
    }

    [Serializable]
    public sealed class FactionReputationCondition : ITransitionCondition
    {
        public FactionID Faction;
        public float     Threshold;
        public bool      RequireAbove;

        public bool Evaluate(TransitionContext ctx)
        {
            float rep = Faction switch
            {
                FactionID.Legal      => ctx.LegalReputation,
                FactionID.Management => ctx.ManagementReputation,
                _                    => 0f,
            };
            return RequireAbove ? rep >= Threshold : rep < Threshold;
        }

        public string Description
            => $"{Faction} rep {(RequireAbove ? ">=" : "<")} {Threshold}";
    }

    // ── Transition Rule ───────────────────────────────────────

    [Serializable]
    public sealed class TransitionRule
    {
        [SerializeField] public string         TriggerAction;   // PunchCardType name
        [SerializeField] public ClientStateID  TargetState;
        [SerializeField] public int            Priority;        // higher = evaluated first
        [SerializeField] public string         DebugName;       // editor label

        // Conditions stored as typed objects — not inspector-friendly yet.
        // TODO: Replace with ScriptableObject per-condition for Inspector editing.
        public List<ITransitionCondition> Conditions = new();

        /// <summary>
        /// Returns true if this rule should fire given the context.
        /// All conditions must pass.
        /// </summary>
        public bool Evaluate(TransitionContext ctx)
        {
            // Check action matches
            if (!string.IsNullOrEmpty(TriggerAction) &&
                !string.Equals(ctx.TriggerAction, TriggerAction,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            // All conditions must pass
            foreach (var condition in Conditions)
                if (!condition.Evaluate(ctx)) return false;

            return true;
        }

        public override string ToString()
            => string.IsNullOrEmpty(DebugName)
                ? $"{TriggerAction} -> {TargetState} (P:{Priority})"
                : DebugName;
    }
}
