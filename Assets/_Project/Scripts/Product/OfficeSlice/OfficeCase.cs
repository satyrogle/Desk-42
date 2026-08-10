using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Desk42.Institutional.Player;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeCaseUrgency
    {
        Routine,
        Elevated,
        Urgent,
        Critical,
    }

    public sealed class OfficeCaseSchedule
    {
        public OfficeCaseSchedule(long openedTick, long absoluteDeadlineTick)
        {
            if (absoluteDeadlineTick < openedTick)
                throw new ArgumentOutOfRangeException(nameof(absoluteDeadlineTick));

            OpenedTick = openedTick;
            AbsoluteDeadlineTick = absoluteDeadlineTick;
        }

        public long OpenedTick { get; }
        public long AbsoluteDeadlineTick { get; }

        public long RemainingDeadlineTicks(long currentTick)
        {
            return Math.Max(0L, AbsoluteDeadlineTick - currentTick);
        }
    }

    /// <summary>
    /// Product-safe spatial projection of one public automation claim. The office
    /// owns scheduling and location; it does not create or mutate institutional truth.
    /// </summary>
    public sealed class OfficeCase
    {
        public OfficeCase(
            string automationClaimId,
            string sourceCaseId,
            string issueId,
            string displayId,
            string issueLabel,
            OfficeCaseUrgency urgency,
            OfficeCaseSchedule schedule,
            IEnumerable<string> publicEvidenceNeeds,
            string parentCaseId,
            string originatingRulingId)
        {
            AutomationClaimId = RequireId(automationClaimId, nameof(automationClaimId));
            SourceCaseId = RequireId(sourceCaseId, nameof(sourceCaseId));
            IssueId = RequireId(issueId, nameof(issueId));
            DisplayId = RequireId(displayId, nameof(displayId));
            IssueLabel = issueLabel ?? string.Empty;
            Urgency = urgency;
            Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            PublicEvidenceNeeds = Freeze(publicEvidenceNeeds);
            ParentCaseId = parentCaseId ?? string.Empty;
            OriginatingRulingId = originatingRulingId ?? string.Empty;
        }

        public string AutomationClaimId { get; }
        public string SourceCaseId { get; }
        public string IssueId { get; }
        public string DisplayId { get; }
        public string IssueLabel { get; }
        public OfficeCaseUrgency Urgency { get; }
        public OfficeCaseSchedule Schedule { get; }
        public IReadOnlyList<string> PublicEvidenceNeeds { get; }
        public string ParentCaseId { get; }
        public string OriginatingRulingId { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable public ID is required.", parameterName);
            return value;
        }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values)
        {
            return new ReadOnlyCollection<string>(
                values == null ? new List<string>() : new List<string>(values));
        }
    }

    public sealed class OfficeCaseRepository
    {
        private readonly List<OfficeCase> _cases;
        private readonly IReadOnlyList<OfficeCase> _readOnlyCases;
        private readonly Dictionary<string, OfficeCase> _byAutomationId;

        public OfficeCaseRepository(IEnumerable<OfficeCase> cases)
        {
            if (cases == null) throw new ArgumentNullException(nameof(cases));

            _cases = new List<OfficeCase>();
            _readOnlyCases = _cases.AsReadOnly();
            _byAutomationId = new Dictionary<string, OfficeCase>(StringComparer.Ordinal);
            foreach (OfficeCase officeCase in cases)
            {
                if (officeCase == null) throw new ArgumentException("Null office case.", nameof(cases));
                if (_byAutomationId.ContainsKey(officeCase.AutomationClaimId))
                    throw new InvalidOperationException(
                        "Stable office case ID collision: " + officeCase.AutomationClaimId);
                if (_cases.Exists(value => string.Equals(
                        value.SourceCaseId, officeCase.SourceCaseId, StringComparison.Ordinal)))
                    throw new InvalidOperationException(
                        "Stable source case ID collision: " + officeCase.SourceCaseId);

                _cases.Add(officeCase);
                _byAutomationId.Add(officeCase.AutomationClaimId, officeCase);
            }
        }

        public IReadOnlyList<OfficeCase> Cases => _readOnlyCases;

        public OfficeCase Get(string automationClaimId)
        {
            if (string.IsNullOrWhiteSpace(automationClaimId)) return null;
            _byAutomationId.TryGetValue(automationClaimId, out OfficeCase value);
            return value;
        }
    }

    public static class OfficeCaseProjector
    {
        public static OfficeCaseRepository CreateSixCaseRepository()
        {
            InstitutionalAutomationSession session = InstitutionalAutomationSession.Create(6);
            return FromClaims(session.Claims);
        }

        public static OfficeCaseRepository FromClaims(
            IReadOnlyList<AutomationPublicClaim> claims)
        {
            if (claims == null) throw new ArgumentNullException(nameof(claims));

            var projected = new List<OfficeCase>(claims.Count);
            for (int i = 0; i < claims.Count; i++)
                projected.Add(Project(claims[i]));
            return new OfficeCaseRepository(projected);
        }

        public static OfficeCase Project(AutomationPublicClaim claim)
        {
            if (claim == null) throw new ArgumentNullException(nameof(claim));

            int evidencePressure = claim.MissingEvidenceCount * 2 +
                Math.Max(0, claim.EvidenceSupportMaximum - claim.EvidenceSupportMinimum);
            OfficeCaseUrgency urgency = evidencePressure >= 7
                ? OfficeCaseUrgency.Critical
                : evidencePressure >= 4
                    ? OfficeCaseUrgency.Urgent
                    : evidencePressure >= 1
                        ? OfficeCaseUrgency.Elevated
                        : OfficeCaseUrgency.Routine;

            var evidenceNeeds = new List<string>
            {
                "PUBLIC EVIDENCE PACKETS: " + claim.EvidencePacketCount,
                "CITABLE PUBLIC EVIDENCE: " + claim.CitableEvidenceCount,
                "MISSING PUBLIC EVIDENCE: " + claim.MissingEvidenceCount,
            };

            // This is a product-owned routing schedule derived only from the public
            // claim. It is not an institutional deadline or hidden truth value.
            long deadline = 180L + (claim.BatchOrdinal * 30L) -
                (claim.MissingEvidenceCount * 10L);
            return new OfficeCase(
                claim.AutomationClaimId,
                claim.SourceCaseId,
                claim.IssueId,
                claim.DisplayId,
                claim.Issue,
                urgency,
                new OfficeCaseSchedule(0L, Math.Max(60L, deadline)),
                evidenceNeeds,
                claim.ParentCaseId,
                claim.OriginatingRulingId);
        }
    }
}
