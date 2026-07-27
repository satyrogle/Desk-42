// ============================================================
// DESK 42 — Encounter History (authoritative)
//
// The single source of truth for "how many times has this
// claimant been here, and how many of those completed".
//
// Handoff §3.5: EncounterHistory is authoritative.
//   TotalPresentations = count(encounter records)
//   TotalVisits        = count(completed encounter records)
//
// Nothing else may serialize a second visit or presentation
// count. ClientTacticProfile.TotalVisits is retained only so
// legacy meta.json deserializes; it is never read or written
// by gameplay any more (see MetaProgressData).
//
// Lives in MetaProgressData rather than RunData because visits
// are inherently cross-run: the BSM's repeat-offender tells and
// the five-shift proof both need recurrence to survive a run
// boundary AND an application restart.
// ============================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Desk42.Core
{
    /// <summary>
    /// One presentation of one claim to the player. Appended when the
    /// encounter begins; marked complete only by CommitEncounterResult.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EncounterRecord
    {
        [JsonProperty] public string EncounterId;
        [JsonProperty] public string ClaimId;
        [JsonProperty] public string ClientVariantId;
        [JsonProperty] public string ClientSpeciesId;

        /// <summary>Authored proof appearance key. Null for procedural claims.</summary>
        [JsonProperty] public string AuthoredAppearanceKey;

        [JsonProperty] public int ShiftNumber;

        /// <summary>
        /// False until CommitEncounterResult succeeds. An interrupted,
        /// timed-out, or abandoned encounter stays false forever — it counts
        /// as a presentation but never as a visit.
        /// </summary>
        [JsonProperty] public bool Completed;

        [JsonProperty] public ClaimResolutionKind Outcome = ClaimResolutionKind.Unspecified;

        /// <summary>UTC ticks at commit. 0 while incomplete.</summary>
        [JsonProperty] public long CommittedAtUtcTicks;
    }

    /// <summary>
    /// Lifecycle status of an encounter record. DERIVED from the record's
    /// Completed flag plus the currently active encounter id — deliberately
    /// not stored, so it cannot drift from the ledger (Bucket 2).
    /// </summary>
    public enum EncounterStatus
    {
        /// <summary>No record with this id.</summary>
        Unknown = 0,

        /// <summary>Presented, not committed, and still the encounter at the desk.</summary>
        Active = 1,

        /// <summary>Presented, not committed, and no longer active — shift ended
        /// or the player quit. Counts as a presentation, never as a visit.</summary>
        Interrupted = 2,

        /// <summary>Committed. Counts as exactly one visit.</summary>
        Completed = 3,
    }

    /// <summary>
    /// Append-only ledger of encounter records. All visit/presentation
    /// numbers are DERIVED from this list — never stored alongside it.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EncounterHistory
    {
        [JsonProperty] private List<EncounterRecord> _records = new();

        [JsonIgnore] public IReadOnlyList<EncounterRecord> Records => _records;

        [JsonIgnore] public int Count => _records?.Count ?? 0;

        // ── Lookup ───────────────────────────────────────────

        public EncounterRecord Find(string encounterId)
        {
            if (string.IsNullOrWhiteSpace(encounterId) || _records == null)
                return null;

            for (int i = 0; i < _records.Count; i++)
            {
                if (string.Equals(_records[i]?.EncounterId, encounterId,
                        StringComparison.Ordinal))
                    return _records[i];
            }
            return null;
        }

        /// <summary>
        /// Handoff §3.2: a completed EncounterId cannot commit twice.
        /// This is the idempotency predicate — there is no separate
        /// committed-ID collection.
        /// </summary>
        public bool IsCompleted(string encounterId)
            => Find(encounterId)?.Completed == true;

        public bool Contains(string encounterId) => Find(encounterId) != null;

        // ── Append / complete ────────────────────────────────

        /// <summary>
        /// Records that an encounter was presented. Idempotent: re-presenting
        /// the same EncounterId (scene reconstruction, mid-encounter resume)
        /// returns the existing record instead of creating a phantom.
        /// </summary>
        public EncounterRecord BeginPresentation(
            string encounterId,
            string claimId,
            string clientVariantId,
            string clientSpeciesId,
            string authoredAppearanceKey,
            int shiftNumber)
        {
            if (string.IsNullOrWhiteSpace(encounterId))
                throw new ArgumentException("EncounterId is required.", nameof(encounterId));

            _records ??= new List<EncounterRecord>();

            var existing = Find(encounterId);
            if (existing != null) return existing;

            var record = new EncounterRecord
            {
                EncounterId           = encounterId,
                ClaimId               = claimId,
                ClientVariantId       = clientVariantId,
                ClientSpeciesId       = clientSpeciesId,
                AuthoredAppearanceKey = authoredAppearanceKey,
                ShiftNumber           = shiftNumber,
                Completed             = false,
            };
            _records.Add(record);
            return record;
        }

        /// <summary>
        /// Marks a presented encounter complete. Returns false when the
        /// encounter is unknown or already complete — callers treat false
        /// as "this commit is a duplicate, do nothing".
        /// </summary>
        public bool MarkCompleted(string encounterId, ClaimResolutionKind outcome, long utcTicks)
        {
            var record = Find(encounterId);
            if (record == null || record.Completed) return false;

            record.Completed           = true;
            record.Outcome             = outcome;
            record.CommittedAtUtcTicks = utcTicks;
            return true;
        }

        // ── Derived counts (handoff §3.5) ────────────────────

        public int TotalPresentations(string clientVariantId)
            => CountWhere(clientVariantId, completedOnly: false);

        public int TotalVisits(string clientVariantId)
            => CountWhere(clientVariantId, completedOnly: true);

        /// <summary>
        /// Completed visits BEFORE the given encounter — the value the BSM
        /// consumes as "have I seen this claimant before". Excludes the
        /// encounter itself so it is stable whether queried at presentation
        /// or after commit.
        /// </summary>
        public int PriorVisits(string clientVariantId, string excludingEncounterId)
        {
            if (string.IsNullOrWhiteSpace(clientVariantId) || _records == null)
                return 0;

            int n = 0;
            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r == null || !r.Completed) continue;
                if (!string.Equals(r.ClientVariantId, clientVariantId, StringComparison.Ordinal))
                    continue;
                if (string.Equals(r.EncounterId, excludingEncounterId, StringComparison.Ordinal))
                    continue;
                n++;
            }
            return n;
        }

        private int CountWhere(string clientVariantId, bool completedOnly)
        {
            if (string.IsNullOrWhiteSpace(clientVariantId) || _records == null)
                return 0;

            int n = 0;
            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r == null) continue;
                if (completedOnly && !r.Completed) continue;
                if (string.Equals(r.ClientVariantId, clientVariantId, StringComparison.Ordinal))
                    n++;
            }
            return n;
        }

        // ── Historical disposition query (CΔ1) ───────────────

        /// <summary>
        /// Committed outcomes for one claimant, in history (encounter) order.
        ///
        /// This is a VIEW over the authoritative ledger, not a second
        /// disposition store: there is deliberately no
        /// Dictionary&lt;ClaimantId, LastDisposition&gt; that could diverge from
        /// history. Every prior encounter is preserved rather than collapsed to
        /// the claimant's most recent outcome.
        ///
        /// Only COMPLETED records appear. An interrupted or in-progress
        /// encounter is a presentation, never a final disposition, so it can
        /// never masquerade as one merely because the claimant identity exists.
        ///
        /// An unknown claimant returns an empty list rather than throwing.
        /// </summary>
        public IReadOnlyList<EncounterRecord> CommittedDispositionsFor(string clientVariantId)
        {
            var result = new List<EncounterRecord>();
            if (string.IsNullOrWhiteSpace(clientVariantId) || _records == null)
                return result;

            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r == null || !r.Completed) continue;
                if (!string.Equals(r.ClientVariantId, clientVariantId,
                        StringComparison.Ordinal))
                    continue;
                result.Add(r);
            }
            return result;
        }

        /// <summary>
        /// The claimant's most recent committed outcome, or Unspecified when
        /// they have none. Convenience over the full list — never a substitute
        /// for it, and never separately stored.
        /// </summary>
        public ClaimResolutionKind LatestDispositionFor(string clientVariantId)
        {
            var all = CommittedDispositionsFor(clientVariantId);
            return all.Count == 0
                ? ClaimResolutionKind.Unspecified
                : all[all.Count - 1].Outcome;
        }

        /// <summary>True when this claimant has any committed history.</summary>
        public bool HasCommittedHistory(string clientVariantId)
            => CommittedDispositionsFor(clientVariantId).Count > 0;

        // ── Derived lifecycle status (Bucket 2) ──────────────

        /// <summary>
        /// Status of one encounter, derived from the ledger plus the id of the
        /// encounter currently at the desk (RunData.ActiveClaim.EncounterId).
        /// Nothing about lifecycle is stored.
        /// </summary>
        public EncounterStatus StatusOf(string encounterId, string activeEncounterId)
        {
            var record = Find(encounterId);
            if (record == null) return EncounterStatus.Unknown;
            if (record.Completed) return EncounterStatus.Completed;

            return string.Equals(encounterId, activeEncounterId, StringComparison.Ordinal)
                ? EncounterStatus.Active
                : EncounterStatus.Interrupted;
        }

        /// <summary>
        /// Encounters presented but never committed, excluding the one at the
        /// desk. These are carried-forward/interrupted encounters.
        /// </summary>
        public List<EncounterRecord> Interrupted(string activeEncounterId)
        {
            var result = new List<EncounterRecord>();
            if (_records == null) return result;

            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r == null || r.Completed) continue;
                if (string.Equals(r.EncounterId, activeEncounterId, StringComparison.Ordinal))
                    continue;
                result.Add(r);
            }
            return result;
        }

        /// <summary>Completed visits for an authored appearance key.</summary>
        public bool HasCompletedAppearance(string appearanceKey)
        {
            if (string.IsNullOrWhiteSpace(appearanceKey) || _records == null)
                return false;

            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r != null && r.Completed
                    && string.Equals(r.AuthoredAppearanceKey, appearanceKey,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
