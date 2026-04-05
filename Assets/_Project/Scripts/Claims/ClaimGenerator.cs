// ============================================================
// DESK 42 — Claim Generator
//
// Pure C# static utility. Assembles a complete ActiveClaimData
// from a weighted draw of ClaimTemplateData + AnomalyTagData SOs.
//
// All randomness routes through SeedEngine (ClaimQueue stream)
// so the same master seed always produces the same queue.
//
// Generation steps per claim:
//   1. Weighted-pick a template (ShiftNumber gate + SpawnWeight).
//   2. Generate a bureaucratic ClaimId ("CLM-XXXXX").
//   3. Pick species → derive ClientVariantId (deterministic per seed).
//   4. Pick claimant name + department + incident text (token replace).
//   5. Pick claim amount in range.
//   6. Assign 0..AnomalyTagSlots anomaly tags (weighted, filtered).
//   7. Pick hidden trait from highest-corruption tag.
//   8. Compute final corruption: base + Σ tag contributions (clamped 0-1).
//   9. Roll NDA requirement.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Claims
{
    public static class ClaimGenerator
    {
        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Generate a single claim. Uses SeedEngine.ClaimQueue stream.
        /// Pass null for meta if no RepeatOffenderDB integration is needed.
        /// </summary>
        public static ActiveClaimData Generate(
            int shiftNumber,
            IReadOnlyList<ClaimTemplateData> templates,
            IReadOnlyList<AnomalyTagData>    allTags,
            MetaProgressData                 meta)
        {
            // ── Step 1: Pick template ─────────────────────────
            var template = PickTemplate(shiftNumber, templates);
            if (template == null)
            {
                Debug.LogWarning("[ClaimGenerator] No eligible templates for shift " +
                                 $"{shiftNumber}. Returning stub claim.");
                return MakeStubClaim(shiftNumber);
            }

            // ── Step 2: Claim ID (5-digit bureaucratic number) ─
            int  claimNumber = SeedEngine.Next(SeedStream.ClaimQueue, 10000, 99999);
            string claimId   = $"CLM-{claimNumber}";

            // ── Step 3: Species + ClientVariantId ─────────────
            string speciesId = PickFrom(template.SpeciesPool, SeedStream.ClaimQueue)
                               ?? "human_standard";
            int variantSuffix = SeedEngine.Next(SeedStream.ClaimQueue, 100, 999);
            string variantId  = $"{speciesId}_{variantSuffix}";

            // ── Step 4: Text ──────────────────────────────────
            string claimantName = PickFrom(template.ClaimantNamePool, SeedStream.ClaimQueue)
                                  ?? "B. Person";
            string deptName     = PickFrom(template.DeptNamePool, SeedStream.ClaimQueue)
                                  ?? "Claims Processing";
            int    claimAmount  = SeedEngine.Next(SeedStream.ClaimQueue,
                                      template.ClaimAmountMin, template.ClaimAmountMax + 1);
            string incidentText = BuildIncidentText(template, claimantName,
                                      claimAmount, deptName);

            // ── Step 5-7: Anomaly tags + hidden trait ─────────
            var  tags       = PickAnomalyTags(template, allTags, shiftNumber);
            string hiddenTrait = SelectHiddenTrait(tags);
            float corruption   = ComputeCorruption(template.BaseCorruption, tags);
            int   corruptSeed  = SeedEngine.Next(SeedStream.ClaimQueue, 0, int.MaxValue);

            // ── Step 8: NDA ───────────────────────────────────
            bool ndaRequired = tags.Exists(t => t.ForcesNDA)
                               || SeedEngine.NextBool(SeedStream.ClaimQueue,
                                      template.NDARequiredChance);

            // ── Assemble ──────────────────────────────────────
            var tagIds = new string[tags.Count];
            for (int i = 0; i < tags.Count; i++)
                tagIds[i] = tags[i].TagId;

            return new ActiveClaimData
            {
                ClaimId         = claimId,
                ClientVariantId = variantId,
                ClientSpeciesId = speciesId,
                TemplateId      = template.TemplateId,
                AnomalyTagIds   = tagIds,
                HiddenTraitId   = hiddenTrait,
                TraitRevealed   = false,
                CorruptionLevel = corruption,
                CorruptionSeed  = corruptSeed,
                NDARequired     = ndaRequired,
                NDASigned       = false,
                IsResolved      = false,
                WasHumane       = false,
                IncidentText    = incidentText,
                ClaimantName    = claimantName,
                ClaimAmount     = claimAmount,
            };
        }

        /// <summary>
        /// Generate an ordered queue of <paramref name="count"/> claims for the shift.
        /// Each successive call advances the ClaimQueue stream, giving variety
        /// while remaining deterministic under the same master seed.
        /// </summary>
        public static List<ActiveClaimData> GenerateQueue(
            int count,
            int shiftNumber,
            IReadOnlyList<ClaimTemplateData> templates,
            IReadOnlyList<AnomalyTagData>    allTags,
            MetaProgressData                 meta)
        {
            var queue = new List<ActiveClaimData>(count);
            for (int i = 0; i < count; i++)
                queue.Add(Generate(shiftNumber, templates, allTags, meta));
            return queue;
        }

        // ── Template Selection ────────────────────────────────

        private static ClaimTemplateData PickTemplate(
            int shiftNumber, IReadOnlyList<ClaimTemplateData> templates)
        {
            if (templates == null || templates.Count == 0) return null;

            // Build a weight array for eligible templates
            var  eligible = new List<ClaimTemplateData>(templates.Count);
            var  weights  = new List<float>(templates.Count);

            foreach (var t in templates)
            {
                if (t == null || t.MinShiftNumber > shiftNumber || t.SpawnWeight <= 0f)
                    continue;
                eligible.Add(t);
                weights.Add(t.SpawnWeight);
            }

            if (eligible.Count == 0) return null;

            int idx = SeedEngine.WeightedRandom(SeedStream.ClaimQueue, weights.ToArray());
            return eligible[idx];
        }

        // ── Anomaly Tag Selection ─────────────────────────────

        private static List<AnomalyTagData> PickAnomalyTags(
            ClaimTemplateData                template,
            IReadOnlyList<AnomalyTagData>    allTags,
            int                              shiftNumber)
        {
            var result = new List<AnomalyTagData>(template.AnomalyTagSlots);

            if (allTags == null || allTags.Count == 0 || template.AnomalyTagSlots == 0)
                return result;

            // Build eligible pool
            var  eligible = new List<AnomalyTagData>(allTags.Count);
            var  weights  = new List<float>(allTags.Count);
            bool hasFilter = template.AnomalyTagFilter != null &&
                             template.AnomalyTagFilter.Length > 0;

            foreach (var tag in allTags)
            {
                if (tag == null || tag.SpawnWeight <= 0f) continue;
                if (tag.MinShiftNumber > shiftNumber)     continue;
                if (hasFilter && Array.IndexOf(template.AnomalyTagFilter, tag.TagId) < 0)
                    continue;

                eligible.Add(tag);
                weights.Add(tag.SpawnWeight);
            }

            if (eligible.Count == 0) return result;

            // How many slots to fill? For low-slot templates there's a 30% chance of 0.
            int slotCount = template.AnomalyTagSlots;
            if (slotCount == 1 && !SeedEngine.NextBool(SeedStream.ClaimQueue, 0.70f))
                return result; // 30% chance: no anomaly tag

            slotCount = Mathf.Min(slotCount, eligible.Count);

            // Pick without replacement
            var usedIndices = new HashSet<int>(slotCount);
            float[] w = weights.ToArray();

            for (int i = 0; i < slotCount; i++)
            {
                // Zero out already-picked weights for this draw
                int idx = PickUnique(w, usedIndices);
                if (idx < 0) break;
                result.Add(eligible[idx]);
                usedIndices.Add(idx);
            }

            return result;
        }

        private static int PickUnique(float[] weights, HashSet<int> used)
        {
            // Build a masked weight total
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                if (!used.Contains(i)) total += weights[i];

            if (total <= 0f) return -1;

            float roll       = SeedEngine.NextFloat(SeedStream.ClaimQueue) * total;
            float cumulative = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                if (used.Contains(i)) continue;
                cumulative += weights[i];
                if (roll < cumulative) return i;
            }
            return -1;
        }

        // ── Hidden Trait Selection ────────────────────────────

        private static string SelectHiddenTrait(List<AnomalyTagData> tags)
        {
            if (tags.Count == 0) return null;

            // The tag with the highest corruption contribution is the "primary" tag
            AnomalyTagData primary = null;
            float maxCorruption = -1f;

            foreach (var tag in tags)
            {
                if (!string.IsNullOrEmpty(tag.HiddenTraitId) &&
                    tag.CorruptionContribution > maxCorruption)
                {
                    maxCorruption = tag.CorruptionContribution;
                    primary       = tag;
                }
            }

            return primary?.HiddenTraitId;
        }

        // ── Corruption Computation ────────────────────────────

        private static float ComputeCorruption(float baseCorruption,
            List<AnomalyTagData> tags)
        {
            float total = baseCorruption;
            foreach (var tag in tags)
                total += tag.CorruptionContribution;
            return Mathf.Clamp01(total);
        }

        // ── Text Assembly ─────────────────────────────────────

        private static string BuildIncidentText(ClaimTemplateData template,
            string claimantName, int claimAmount, string deptName)
        {
            if (template.IncidentTextVariants == null ||
                template.IncidentTextVariants.Length == 0)
                return $"Claim filed by {claimantName}. Amount: ¢{claimAmount}.";

            int idx = SeedEngine.Next(SeedStream.ClaimQueue,
                                      template.IncidentTextVariants.Length);
            string text = template.IncidentTextVariants[idx];

            return text
                .Replace("{claimant}", claimantName)
                .Replace("{amount}",   $"¢{claimAmount:N0}")
                .Replace("{dept}",     deptName);
        }

        // ── Utilities ─────────────────────────────────────────

        private static string PickFrom(string[] pool, SeedStream stream)
        {
            if (pool == null || pool.Length == 0) return null;
            return pool[SeedEngine.Next(stream, pool.Length)];
        }

        private static ActiveClaimData MakeStubClaim(int shiftNumber) => new()
        {
            ClaimId         = $"CLM-00000",
            ClientVariantId = "human_standard_001",
            ClientSpeciesId = "human_standard",
            TemplateId      = "stub",
            AnomalyTagIds   = Array.Empty<string>(),
            HiddenTraitId   = null,
            CorruptionLevel = 0.1f,
            CorruptionSeed  = 0,
            NDARequired     = false,
            IncidentText    = "Standard claim. Please process as filed.",
            ClaimantName    = "B. Person",
            ClaimAmount     = 1000,
        };
    }
}
