// ============================================================
// DESK 42 — Carried-forward encounters (Bucket C Δ3A)
//
// THE GAP THIS CLOSES: EncounterHistory already DERIVES an Interrupted
// status, but derivation is not carry-forward. The claim payload lives
// only in per-run RunData, which a new run replaces, so an interrupted
// encounter was unrecoverable across a run boundary — history knew it
// happened and nothing could reconstruct it.
//
// A carried encounter is THE SAME ENCOUNTER, not a new one. It retains
// its original EncounterId and ClientVariantId across interruption,
// save, restart, run/shift transition, re-presentation and final
// resolution. Nothing here mints a new identity.
//
// This is NOT scheduled recurrence. Carry-forward is unresolved work
// continuing; recurrence is a completed claimant returning as a new
// encounter. CΔ3B owns the latter, if it is ever needed.
//
// Interruption is explicitly NOT a disposition: no Approve/Deny/Liquify,
// no approval liability, no final consequences, no completed history.
// ============================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Desk42.Core
{
    /// <summary>
    /// One unresolved encounter preserved for continuation. Stores the claim
    /// payload because RunData does not survive the run boundary.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CarriedEncounterRecord
    {
        /// <summary>Primary key — the ORIGINAL encounter identity.</summary>
        [JsonProperty] public string EncounterId;

        /// <summary>Claimant provenance, preserved rather than regenerated.</summary>
        [JsonProperty] public string ClientVariantId;

        /// <summary>
        /// The claim needed to reconstruct the same unresolved case. Persistent
        /// content only — no scene or UI state.
        /// </summary>
        [JsonProperty] public ActiveClaimData Claim;

        /// <summary>Shift on which it was most recently interrupted.</summary>
        [JsonProperty] public int InterruptedOnShift;

        /// <summary>
        /// How many times this encounter has been interrupted. Interruption is
        /// not assumed to happen only once.
        /// </summary>
        [JsonProperty] public int InterruptCount;
    }

    /// <summary>
    /// Carried encounters keyed by original EncounterId. Not a second
    /// EncounterHistory: history owns what happened, this owns only the
    /// unresolved work still outstanding.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CarriedEncounterLedger
    {
        [JsonProperty] private List<CarriedEncounterRecord> _records = new();

        [JsonIgnore] public IReadOnlyList<CarriedEncounterRecord> Records
            => (IReadOnlyList<CarriedEncounterRecord>)_records
               ?? Array.Empty<CarriedEncounterRecord>();

        [JsonIgnore] public int Count => _records?.Count ?? 0;

        /// <summary>First/canonical record for this encounter, or null.</summary>
        public CarriedEncounterRecord Find(string encounterId)
        {
            if (string.IsNullOrWhiteSpace(encounterId) || _records == null) return null;

            for (int i = 0; i < _records.Count; i++)
            {
                if (string.Equals(_records[i]?.EncounterId, encounterId,
                        StringComparison.Ordinal))
                    return _records[i];
            }
            return null;
        }

        public bool Has(string encounterId) => Find(encounterId) != null;

        /// <summary>
        /// Marks an encounter as carried forward. Idempotent by EncounterId:
        /// interrupting the same encounter again updates the existing record
        /// and bumps the counter rather than adding a second one.
        /// </summary>
        public CarriedEncounterRecord Carry(ActiveClaimData claim, int shiftNumber)
        {
            if (claim == null || string.IsNullOrWhiteSpace(claim.EncounterId))
                throw new ArgumentException(
                    "Carry-forward requires a claim with an EncounterId.", nameof(claim));

            if (claim.IsResolved)
                throw new InvalidOperationException(
                    $"Encounter '{claim.EncounterId}' is resolved and cannot be carried.");

            _records ??= new List<CarriedEncounterRecord>();

            var existing = Find(claim.EncounterId);
            if (existing != null)
            {
                existing.Claim              = claim;
                existing.InterruptedOnShift = shiftNumber;
                existing.InterruptCount++;
                return existing;
            }

            var record = new CarriedEncounterRecord
            {
                EncounterId        = claim.EncounterId,
                ClientVariantId    = claim.ClientVariantId,
                Claim              = claim,
                InterruptedOnShift = shiftNumber,
                InterruptCount     = 1,
            };
            _records.Add(record);
            return record;
        }

        /// <summary>
        /// Removes carried work for this encounter. Called when it terminally
        /// commits, so a resolved encounter never returns as outstanding work.
        /// Removes every row for the id, so malformed duplicates cannot leave a
        /// stale copy behind. Safe for unknown ids.
        /// </summary>
        public bool Release(string encounterId)
        {
            if (string.IsNullOrWhiteSpace(encounterId) || _records == null) return false;

            int removed = _records.RemoveAll(r =>
                r != null && string.Equals(r.EncounterId, encounterId, StringComparison.Ordinal));
            return removed > 0;
        }

        /// <summary>
        /// One canonical carried encounter per EncounterId, in persisted order,
        /// skipping malformed rows with no reconstructable claim.
        ///
        /// Read-only: malformed duplicates are not deleted and nothing is
        /// written back, so loading a bad save stays non-destructive.
        /// </summary>
        public List<CarriedEncounterRecord> Canonical()
        {
            var result = new List<CarriedEncounterRecord>();
            if (_records == null) return result;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in _records)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.EncounterId)) continue;
                if (r.Claim == null) continue;              // unreconstructable: ignored
                if (!seen.Add(r.EncounterId)) continue;     // later duplicate: ignored
                result.Add(r);
            }
            return result;
        }
    }
}
