// ============================================================
// DESK 42 — Moral Dilemma System
//
// Generates dilemma prompts during claim processing and
// resolves the player's choice into soul/credit/time changes.
//
// Trigger model:
//   A dilemma may surface at the moment a claim is stamped.
//   The system rolls against a base probability that scales
//   with (1 - soul/100) — the more corrupted the player,
//   the more frequently the dilemmas come. You can't outrun
//   your conscience; you can only exhaust it.
//
//   Base probability: 20% per claim.
//   At soul ≤ 50%: +30% (max 85%).
//   At soul ≤ 25%: an additional +15% (max 85%).
//
// Resolution fires:
//   - MoralChoiceEvent (always)
//   - SoulIntegrityChangedEvent (via RunStateController)
//   - MoralInjurySystem.RecordAct() if bureaucratic choice
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.MoralInjury
{
    // ── Active Dilemma (runtime state for a pending prompt) ───

    public sealed class ActiveDilemma
    {
        public readonly MoralDilemmaData Data;
        public readonly string           ClaimId;
        public readonly string           ClientName;
        public readonly int              ClaimAmount;

        // Built display text (tokens replaced)
        public string BuiltPrompt    { get; private set; }
        public bool   IsResolved     { get; private set; }
        public bool   ChoseEthical   { get; private set; }

        public ActiveDilemma(MoralDilemmaData data, string claimId,
            string clientName, int claimAmount)
        {
            Data        = data;
            ClaimId     = claimId;
            ClientName  = clientName;
            ClaimAmount = claimAmount;
            BuiltPrompt = data.PromptTemplate
                .Replace("{clientName}",  clientName)
                .Replace("{claimAmount}", $"${claimAmount:N0}");
        }

        public void Resolve(bool choseEthical)
        {
            IsResolved   = true;
            ChoseEthical = choseEthical;
        }
    }

    // ── Moral Dilemma System ──────────────────────────────────

    public sealed class MoralDilemmaSystem
    {
        private readonly List<MoralDilemmaData> _allDilemmas;
        private readonly MoralInjurySystem      _injurySystem;

        private const float BASE_DILEMMA_CHANCE   = 0.20f;
        private const float LOW_SOUL_BONUS        = 0.30f;  // soul ≤ 50
        private const float CRITICAL_SOUL_BONUS   = 0.15f;  // soul ≤ 25
        private const float MAX_DILEMMA_CHANCE    = 0.85f;

        // ── Init ──────────────────────────────────────────────

        public MoralDilemmaSystem(
            IEnumerable<MoralDilemmaData> allDilemmas,
            MoralInjurySystem injurySystem)
        {
            _allDilemmas  = new List<MoralDilemmaData>(allDilemmas);
            _injurySystem = injurySystem;
        }

        // ── Generate ─────────────────────────────────────────

        /// <summary>
        /// Roll to see if a dilemma fires for this claim.
        /// Returns null if no dilemma this time.
        /// </summary>
        public ActiveDilemma TryGenerateDilemma(
            string claimId,
            string clientName,
            int    claimAmount,
            float  currentSoul,
            int    shiftNumber)
        {
            float chance = BASE_DILEMMA_CHANCE;
            if (currentSoul <= 50f) chance += LOW_SOUL_BONUS;
            if (currentSoul <= 25f) chance += CRITICAL_SOUL_BONUS;
            chance = Mathf.Min(chance, MAX_DILEMMA_CHANCE);

            if (!SeedEngine.NextBool(SeedStream.MoralDilemma, chance))
                return null;

            var eligible = BuildEligiblePool(currentSoul, shiftNumber);
            if (eligible.Count == 0) return null;

            // Weighted pick
            var weights = new float[eligible.Count];
            for (int i = 0; i < eligible.Count; i++)
                weights[i] = eligible[i].SpawnWeight;

            int idx = SeedEngine.WeightedRandom(SeedStream.MoralDilemma, weights);
            return new ActiveDilemma(eligible[idx], claimId, clientName, claimAmount);
        }

        private List<MoralDilemmaData> BuildEligiblePool(float soul, int shift)
        {
            var pool = new List<MoralDilemmaData>(_allDilemmas.Count);
            foreach (var d in _allDilemmas)
            {
                if (soul < d.MinSoulForEligibility) continue;
                if (soul > d.MaxSoulForEligibility) continue;
                if (shift < d.MinShiftNumber)       continue;
                pool.Add(d);
            }
            return pool;
        }

        // ── Resolve ───────────────────────────────────────────

        /// <summary>
        /// Apply the consequences of the player's choice.
        /// Must be called after the UI collects the input.
        /// </summary>
        public DilemmaResolutionResult Resolve(
            ActiveDilemma dilemma,
            bool          choseEthical)
        {
            dilemma.Resolve(choseEthical);
            var data = dilemma.Data;

            float soulDelta   = choseEthical ? data.EthicalSoulDelta   : data.BureaucraticSoulDelta;
            int   creditDelta = choseEthical ? data.EthicalCreditDelta : data.BureaucraticCreditDelta;
            float timeDelta   = choseEthical ? data.EthicalTimeDelta   : data.BureaucraticTimeDelta;

            float injuryCost = 0f;

            if (!choseEthical)
            {
                // Record in the moral injury system (escalation tracking)
                injuryCost = _injurySystem.RecordAct(data.ActionType, dilemma.ClaimId);

                // Override the soul delta with the escalated cost if it's worse
                if (-injuryCost < soulDelta)
                    soulDelta = -injuryCost;
            }

            // Publish the choice event (RunStateController handles soul/credit mutation)
            RumorMill.PublishDeferred(new MoralChoiceEvent(
                dilemma.ClaimId,
                data.ActionType,
                -soulDelta,          // MoralInjuryDelta is positive when soul is lost
                !choseEthical));

            Debug.Log($"[DilemmaSystem] {dilemma.Data.DilemmaId}: " +
                      $"{(choseEthical ? "ETHICAL" : "BUREAUCRATIC")} choice. " +
                      $"Soul Δ={soulDelta:+0.0;-0.0}, Credit Δ={creditDelta:+0;-0}.");

            return new DilemmaResolutionResult
            {
                ChoseEthical  = choseEthical,
                SoulDelta     = soulDelta,
                CreditDelta   = creditDelta,
                TimeDelta     = timeDelta,
                InjuryCost    = injuryCost,
                NarratorKey   = string.IsNullOrEmpty(data.NarratorOverrideKey)
                    ? (choseEthical ? "dilemma_ethical" : "dilemma_bureaucratic")
                    : data.NarratorOverrideKey,
            };
        }
    }

    // ── Resolution Result ─────────────────────────────────────

    public sealed class DilemmaResolutionResult
    {
        public bool   ChoseEthical;
        public float  SoulDelta;
        public int    CreditDelta;
        public float  TimeDelta;
        public float  InjuryCost;    // escalated cost from MoralInjurySystem
        public string NarratorKey;  // key for NarratorSystem line lookup
    }
}
