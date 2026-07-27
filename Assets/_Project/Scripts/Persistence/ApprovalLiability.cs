// ============================================================
// DESK 42 — Approval liability (Bucket C Δ2)
//
// MEANING: a committed approval created a persistent consequence that
// something later may act on. Encounter history already records THAT an
// encounter was approved; liability records that the approval left an
// unresolved consequence behind.
//
//   EncounterHistory   "Encounter X was approved"
//   this ledger        "Encounter X created an unresolved approval consequence"
//
// The record therefore REFERENCES the source encounter rather than
// reproducing it. Claimant name, disposition, visit count, timestamps and
// completion status are all resolved from EncounterHistory when needed.
//
// IDENTITY: the source EncounterId is the primary key, NOT the claimant.
// Procedural claimants receive a freshly generated {species}_{100..999}
// per claim (proved unstable and collision-capable in CΔ1), so keying
// liability by claimant would attribute one claim's consequence to an
// unrelated later claimant. ClientVariantId is kept as provenance and a
// filtering aid for AUTHORED claimants only.
//
// LIFECYCLE: creation, persistence and query only. No consumer exists
// yet, so an active liability simply remains active. Resolution is
// deliberately not invented here — see BUCKET-C-DELTA-2.md.
// ============================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Desk42.Core
{
    /// <summary>
    /// One persistent consequence created by one committed approval.
    /// Additive and minimal: everything derivable from EncounterHistory is
    /// deliberately absent.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ApprovalLiabilityRecord
    {
        /// <summary>Primary key. The committed approval that created this.</summary>
        [JsonProperty] public string SourceEncounterId;

        /// <summary>
        /// Provenance only. Safe for authored claimants (elias_venn,
        /// control_mara_kest); NOT a stable identity for procedural ones.
        /// </summary>
        [JsonProperty] public string SourceClientVariantId;

        /// <summary>
        /// False until some later system discharges it. Nothing consumes
        /// liability yet, so this stays false for now.
        /// </summary>
        [JsonProperty] public bool Resolved;
    }

    /// <summary>
    /// Append-only ledger of approval liabilities, keyed by source encounter.
    /// Not a second encounter history: it stores only what history cannot
    /// express, and every record points back at a real encounter.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ApprovalLiabilityLedger
    {
        [JsonProperty] private List<ApprovalLiabilityRecord> _records = new();

        [JsonIgnore] public IReadOnlyList<ApprovalLiabilityRecord> Records
            => (IReadOnlyList<ApprovalLiabilityRecord>)_records ?? Array.Empty<ApprovalLiabilityRecord>();

        [JsonIgnore] public int Count => _records?.Count ?? 0;

        /// <summary>The liability created by this encounter, or null.</summary>
        public ApprovalLiabilityRecord Find(string sourceEncounterId)
        {
            if (string.IsNullOrWhiteSpace(sourceEncounterId) || _records == null)
                return null;

            for (int i = 0; i < _records.Count; i++)
            {
                if (string.Equals(_records[i]?.SourceEncounterId, sourceEncounterId,
                        StringComparison.Ordinal))
                    return _records[i];
            }
            return null;
        }

        /// <summary>Exact-once predicate. Persisted, not runtime.</summary>
        public bool Has(string sourceEncounterId) => Find(sourceEncounterId) != null;

        /// <summary>
        /// Records a liability for a committed approval. Idempotent by source
        /// encounter: a duplicate commit, a replayed callback or a reload
        /// followed by a retry all return the existing record.
        /// </summary>
        public ApprovalLiabilityRecord Create(
            string sourceEncounterId, string sourceClientVariantId)
        {
            if (string.IsNullOrWhiteSpace(sourceEncounterId))
                throw new ArgumentException(
                    "Approval liability requires a source EncounterId.",
                    nameof(sourceEncounterId));

            _records ??= new List<ApprovalLiabilityRecord>();

            var existing = Find(sourceEncounterId);
            if (existing != null) return existing;

            var record = new ApprovalLiabilityRecord
            {
                SourceEncounterId     = sourceEncounterId,
                SourceClientVariantId = sourceClientVariantId,
                Resolved              = false,
            };
            _records.Add(record);
            return record;
        }
    }

    /// <summary>
    /// Read-only seam over the ledger, validated against encounter history.
    ///
    /// A record whose source encounter is missing, incomplete, or not an
    /// Approve is an ORPHAN: it is ignored by these queries rather than
    /// deleted or thrown on, so a malformed or hand-edited save still loads.
    /// Production creation makes orphans impossible; this only stops a bad
    /// file being treated as a legitimate active liability.
    /// </summary>
    public static class ApprovalLiabilityPolicy
    {
        /// <summary>Dispositions that create approval liability.</summary>
        public static bool IsLiabilityCreating(ClaimResolutionKind kind)
            => kind == ClaimResolutionKind.Approve;

        /// <summary>
        /// True when the record is backed by a completed, approved encounter.
        /// </summary>
        public static bool IsValid(ApprovalLiabilityRecord record, EncounterHistory history)
        {
            if (record == null || history == null) return false;
            if (string.IsNullOrWhiteSpace(record.SourceEncounterId)) return false;

            var source = history.Find(record.SourceEncounterId);
            return source != null
                && source.Completed
                && IsLiabilityCreating(source.Outcome);
        }

        /// <summary>
        /// True when this encounter has a valid approval liability. Safe for
        /// unknown encounters — returns false rather than throwing.
        /// </summary>
        public static bool HasApprovalLiability(
            MetaProgressData meta, string sourceEncounterId)
            => TryGet(meta, sourceEncounterId) != null;

        /// <summary>The valid liability for this encounter, or null.</summary>
        public static ApprovalLiabilityRecord TryGet(
            MetaProgressData meta, string sourceEncounterId)
        {
            var record = meta?.ApprovalLiabilities?.Find(sourceEncounterId);
            return IsValid(record, meta?.Encounters) ? record : null;
        }

        /// <summary>
        /// Unresolved liabilities backed by a valid source encounter, in
        /// creation order. Orphans are skipped.
        /// </summary>
        public static List<ApprovalLiabilityRecord> ActiveLiabilities(MetaProgressData meta)
        {
            var result = new List<ApprovalLiabilityRecord>();
            var ledger = meta?.ApprovalLiabilities;
            if (ledger == null) return result;

            foreach (var r in ledger.Records)
            {
                if (r == null || r.Resolved) continue;
                if (!IsValid(r, meta.Encounters)) continue;
                result.Add(r);
            }
            return result;
        }

        /// <summary>
        /// Valid liabilities whose provenance names this claimant. A query
        /// convenience for AUTHORED claimants — never liability identity.
        /// </summary>
        public static List<ApprovalLiabilityRecord> ForClaimant(
            MetaProgressData meta, string clientVariantId)
        {
            var result = new List<ApprovalLiabilityRecord>();
            if (string.IsNullOrWhiteSpace(clientVariantId)) return result;

            var ledger = meta?.ApprovalLiabilities;
            if (ledger == null) return result;

            foreach (var r in ledger.Records)
            {
                if (r == null) continue;
                if (!string.Equals(r.SourceClientVariantId, clientVariantId,
                        StringComparison.Ordinal)) continue;
                if (!IsValid(r, meta.Encounters)) continue;
                result.Add(r);
            }
            return result;
        }
    }
}
