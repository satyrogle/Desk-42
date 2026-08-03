using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional.Player
{
    /// <summary>
    /// Product-facing batch adapter for the automation floor. Each envelope owns an
    /// isolated deterministic institutional session, exposes only its public projection,
    /// and commits through the same validated ruling command used by the playable slice.
    /// It never exposes authoritative material state, private belief or utility traces.
    /// </summary>
    public sealed class InstitutionalAutomationSession
    {
        private readonly Dictionary<string, ClaimEnvelope> _byAutomationId;
        private readonly IReadOnlyList<AutomationPublicClaim> _claims;

        private InstitutionalAutomationSession(int claimCount)
        {
            if (claimCount < 1 || claimCount > 32)
                throw new ArgumentOutOfRangeException(nameof(claimCount));
            _byAutomationId = new Dictionary<string, ClaimEnvelope>(
                StringComparer.Ordinal);
            var claims = new List<AutomationPublicClaim>(claimCount);
            for (int index = 0; index < claimCount; index++)
            {
                CausalLegibilitySliceSession session =
                    CausalLegibilitySliceSession.Create();
                string automationId = "claim.branch42." + (index + 1).ToString("D3");
                AutomationPublicClaim claim = ProjectClaim(
                    automationId, index + 1, session.View);
                _byAutomationId.Add(
                    automationId, new ClaimEnvelope(session, claim));
                claims.Add(claim);
            }
            _claims = new ReadOnlyCollection<AutomationPublicClaim>(claims);
        }

        public IReadOnlyList<AutomationPublicClaim> Claims => _claims;

        public static InstitutionalAutomationSession Create(int claimCount)
        {
            return new InstitutionalAutomationSession(claimCount);
        }

        public AutomationRulingResult Commit(
            string automationClaimId,
            PlayerScopeChoice scope,
            PlayerRulingDisposition disposition)
        {
            if (string.IsNullOrWhiteSpace(automationClaimId))
                throw new ArgumentException(
                    "An automation claim id is required.", nameof(automationClaimId));
            if (!_byAutomationId.TryGetValue(
                    automationClaimId, out ClaimEnvelope envelope))
                throw new InvalidOperationException(
                    $"Automation claim '{automationClaimId}' is not in this batch.");
            if (envelope.Result != null)
                throw new InvalidOperationException(
                    $"Automation claim '{automationClaimId}' already has a ruling.");

            PlayerRulingDraft draft = envelope.Session.CreateDraft(scope, disposition);
            PlayerInstitutionView ruled = envelope.Session.Commit(draft);
            PublicRulingRecord ruling = LatestRuling(ruled);
            PublicCaseRecord descendant = FindDescendant(
                ruled, envelope.Claim.SourceCaseId, ruling.RulingId);
            AutomationAppealPacket appeal = descendant == null
                ? null
                : new AutomationAppealPacket(
                    "appeal.branch42." + envelope.Claim.BatchOrdinal.ToString("D3"),
                    automationClaimId,
                    descendant.CaseId,
                    ruling.RulingId,
                    "A connected case now contests how the holding was applied.",
                    descendant.EvidenceIds.Count,
                    descendant.MissingEvidence.Count);
            envelope.Result = new AutomationRulingResult(
                automationClaimId,
                ruling.RulingId,
                ruling.Disposition,
                ruling.Scope,
                ruling.TemporalReach,
                ruling.Remedies,
                ruling.DirectInstitutionalChanges,
                appeal);
            return envelope.Result;
        }

        private static AutomationPublicClaim ProjectClaim(
            string automationId,
            int ordinal,
            PlayerInstitutionView view)
        {
            PublicCaseRecord record = FindCurrentCase(view);
            int citable = 0;
            for (int i = 0; i < view.Evidence.Count; i++)
                if (view.Evidence[i].CaseId == record.CaseId && view.Evidence[i].Citable)
                    citable++;
            return new AutomationPublicClaim(
                automationId,
                ordinal,
                "CLAIM 42-" + ordinal.ToString("D2"),
                record.CaseId,
                record.Issue,
                record.Parties,
                record.EvidenceIds.Count,
                citable,
                record.RecognisedFacts.Count,
                record.Allegations.Count + record.ContestedPropositions.Count,
                record.MissingEvidence.Count,
                record.EvidenceSupportMinimum,
                record.EvidenceSupportMaximum,
                view.UnknownsSummary);
        }

        private static PublicCaseRecord FindCurrentCase(PlayerInstitutionView view)
        {
            for (int i = 0; i < view.Cases.Count; i++)
                if (string.Equals(view.Cases[i].CaseId, view.CurrentCaseId,
                        StringComparison.Ordinal)) return view.Cases[i];
            throw new InvalidOperationException(
                "The public institutional view has no current case projection.");
        }

        private static PublicRulingRecord LatestRuling(PlayerInstitutionView view)
        {
            if (view.Rulings.Count == 0)
                throw new InvalidOperationException(
                    "The validated ruling command produced no public ruling projection.");
            return view.Rulings[view.Rulings.Count - 1];
        }

        private static PublicCaseRecord FindDescendant(
            PlayerInstitutionView view,
            string parentCaseId,
            string rulingId)
        {
            for (int i = 0; i < view.Cases.Count; i++)
            {
                PublicCaseRecord candidate = view.Cases[i];
                if (string.Equals(candidate.ParentCaseId, parentCaseId,
                        StringComparison.Ordinal) &&
                    string.Equals(candidate.OriginatingRulingId, rulingId,
                        StringComparison.Ordinal)) return candidate;
            }
            return null;
        }

        private sealed class ClaimEnvelope
        {
            internal ClaimEnvelope(
                CausalLegibilitySliceSession session, AutomationPublicClaim claim)
            {
                Session = session;
                Claim = claim;
            }

            internal CausalLegibilitySliceSession Session { get; }
            internal AutomationPublicClaim Claim { get; }
            internal AutomationRulingResult Result { get; set; }
        }
    }

    public sealed class AutomationPublicClaim
    {
        internal AutomationPublicClaim(
            string automationClaimId,
            int batchOrdinal,
            string displayId,
            string sourceCaseId,
            string issue,
            IEnumerable<string> parties,
            int evidencePacketCount,
            int citableEvidenceCount,
            int officialFactCount,
            int allegationCount,
            int missingEvidenceCount,
            int supportMinimum,
            int supportMaximum,
            string unknownsSummary)
        {
            AutomationClaimId = automationClaimId ?? string.Empty;
            BatchOrdinal = batchOrdinal;
            DisplayId = displayId ?? string.Empty;
            SourceCaseId = sourceCaseId ?? string.Empty;
            Issue = issue ?? string.Empty;
            Parties = Freeze(parties);
            EvidencePacketCount = evidencePacketCount;
            CitableEvidenceCount = citableEvidenceCount;
            OfficialFactCount = officialFactCount;
            AllegationCount = allegationCount;
            MissingEvidenceCount = missingEvidenceCount;
            EvidenceSupportMinimum = supportMinimum;
            EvidenceSupportMaximum = supportMaximum;
            UnknownsSummary = unknownsSummary ?? string.Empty;
        }

        public string AutomationClaimId { get; }
        public int BatchOrdinal { get; }
        public string DisplayId { get; }
        public string SourceCaseId { get; }
        public string Issue { get; }
        public IReadOnlyList<string> Parties { get; }
        public int EvidencePacketCount { get; }
        public int CitableEvidenceCount { get; }
        public int OfficialFactCount { get; }
        public int AllegationCount { get; }
        public int MissingEvidenceCount { get; }
        public int EvidenceSupportMinimum { get; }
        public int EvidenceSupportMaximum { get; }
        public string UnknownsSummary { get; }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values)
        {
            return new ReadOnlyCollection<string>(
                values == null ? new List<string>() : new List<string>(values));
        }
    }

    public sealed class AutomationRulingResult
    {
        internal AutomationRulingResult(
            string automationClaimId,
            string rulingId,
            string disposition,
            string scope,
            string temporalReach,
            IEnumerable<string> remedies,
            IEnumerable<string> directChanges,
            AutomationAppealPacket appeal)
        {
            AutomationClaimId = automationClaimId ?? string.Empty;
            RulingId = rulingId ?? string.Empty;
            Disposition = disposition ?? string.Empty;
            Scope = scope ?? string.Empty;
            TemporalReach = temporalReach ?? string.Empty;
            Remedies = Freeze(remedies);
            DirectInstitutionalChanges = Freeze(directChanges);
            Appeal = appeal;
        }

        public string AutomationClaimId { get; }
        public string RulingId { get; }
        public string Disposition { get; }
        public string Scope { get; }
        public string TemporalReach { get; }
        public IReadOnlyList<string> Remedies { get; }
        public IReadOnlyList<string> DirectInstitutionalChanges { get; }
        public AutomationAppealPacket Appeal { get; }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values)
        {
            return new ReadOnlyCollection<string>(
                values == null ? new List<string>() : new List<string>(values));
        }
    }

    public sealed class AutomationAppealPacket
    {
        internal AutomationAppealPacket(
            string appealId,
            string parentAutomationClaimId,
            string sourceCaseId,
            string originatingRulingId,
            string publicBasis,
            int evidencePacketCount,
            int missingEvidenceCount)
        {
            AppealId = appealId ?? string.Empty;
            ParentAutomationClaimId = parentAutomationClaimId ?? string.Empty;
            SourceCaseId = sourceCaseId ?? string.Empty;
            OriginatingRulingId = originatingRulingId ?? string.Empty;
            PublicBasis = publicBasis ?? string.Empty;
            EvidencePacketCount = evidencePacketCount;
            MissingEvidenceCount = missingEvidenceCount;
        }

        public string AppealId { get; }
        public string ParentAutomationClaimId { get; }
        public string SourceCaseId { get; }
        public string OriginatingRulingId { get; }
        public string PublicBasis { get; }
        public int EvidencePacketCount { get; }
        public int MissingEvidenceCount { get; }
    }
}
