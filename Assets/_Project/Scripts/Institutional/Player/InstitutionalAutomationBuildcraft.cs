using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional.Player
{
    public enum AutomationPrecedentMode
    {
        MandatoryCitation = 1,
        PermittedCitation = 2,
        HumanReviewRequired = 3,
        DoNotAutomate = 4,
    }

    [Serializable]
    public sealed class InstitutionalAutomationCheckpoint
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string AuthorityPayload;
        public int NextClaimOrdinal;
        public int PulseOrdinal;
        public List<string> ReleasedCaseIds = new();
        public List<string> ReservedAppealCaseIds = new();
        public List<AutomationClaimCheckpoint> CurrentClaims = new();
        public List<AutomationPrecedentModeCheckpoint> PrecedentModes = new();
    }

    [Serializable]
    public sealed class AutomationClaimCheckpoint
    {
        public string AutomationClaimId;
        public int BatchOrdinal;
        public string SourceCaseId;
    }

    [Serializable]
    public sealed class AutomationPrecedentModeCheckpoint
    {
        public string HoldingId;
        public AutomationPrecedentMode Mode;
    }

    public sealed class AutomationPrecedentRecord
    {
        internal AutomationPrecedentRecord(
            string holdingId,
            string issue,
            string scope,
            string sourceAppealId,
            string sourceRulingId,
            int currentMatchingCases,
            int appliedCaseCount,
            int liabilityExposure,
            int conflictingHoldingCount,
            AutomationPrecedentMode mode)
        {
            HoldingId = holdingId ?? string.Empty;
            Issue = issue ?? string.Empty;
            Scope = scope ?? string.Empty;
            SourceAppealId = sourceAppealId ?? string.Empty;
            SourceRulingId = sourceRulingId ?? string.Empty;
            CurrentMatchingCases = currentMatchingCases;
            AppliedCaseCount = appliedCaseCount;
            LiabilityExposure = liabilityExposure;
            ConflictingHoldingCount = conflictingHoldingCount;
            Mode = mode;
        }

        public string HoldingId { get; }
        public string Issue { get; }
        public string Scope { get; }
        public string SourceAppealId { get; }
        public string SourceRulingId { get; }
        public int CurrentMatchingCases { get; }
        public int AppliedCaseCount { get; }
        public int LiabilityExposure { get; }
        public int ConflictingHoldingCount { get; }
        public AutomationPrecedentMode Mode { get; }
    }

    public sealed class AutomationSocietyMetrics
    {
        internal AutomationSocietyMetrics(
            int agentCount,
            int averageInstitutionalTrust,
            int activeCollectives,
            int recognisedCollectiveMembers,
            int totalRelationshipFear)
        {
            AgentCount = agentCount;
            AverageInstitutionalTrust = averageInstitutionalTrust;
            ActiveCollectives = activeCollectives;
            RecognisedCollectiveMembers = recognisedCollectiveMembers;
            TotalRelationshipFear = totalRelationshipFear;
        }

        public int AgentCount { get; }
        public int AverageInstitutionalTrust { get; }
        public int ActiveCollectives { get; }
        public int RecognisedCollectiveMembers { get; }
        public int TotalRelationshipFear { get; }
    }

    public sealed partial class InstitutionalAutomationSession
    {
        private readonly Dictionary<string, AutomationPrecedentMode>
            _precedentModes = new(StringComparer.Ordinal);

        private InstitutionalAutomationSession()
        {
        }

        public IReadOnlyList<AutomationPrecedentRecord> Precedents =>
            ProjectPrecedents();

        public AutomationSocietyMetrics SocietyMetrics =>
            ProjectSocietyMetrics();

        public int AppealReversalCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _current.Docket.Appeals.Count; i++)
                {
                    EndogenousAppealRecord appeal = _current.Docket.Appeals[i];
                    if (!appeal.Resolved) continue;
                    CommittedPlayerRuling ruling = FindRulingById(
                        appeal.ResultingRulingId);
                    if (ruling?.Disposition == RulingDisposition.ReversedAndDenied ||
                        ruling?.Disposition ==
                        RulingDisposition.ReversedAndRecognised) count++;
                }
                return count;
            }
        }

        public void ValidateCurrentState()
        {
            EndogenousRunSnapshotService.RefreshInPlace(
                _current,
                "persistent-automation.validated",
                EndogenousCommitPhase.ScopeEffectsCommitted);
        }

        public InstitutionalAutomationCheckpoint CreateCheckpoint()
        {
            var checkpoint = new InstitutionalAutomationCheckpoint
            {
                AuthorityPayload = EndogenousRunSnapshotStore.SerializePayload(
                    _current),
                NextClaimOrdinal = _nextClaimOrdinal,
                PulseOrdinal = _pulseOrdinal,
            };
            CopyStable(_releasedCaseIds, checkpoint.ReleasedCaseIds);
            CopyStable(_reservedAppealCaseIds,
                checkpoint.ReservedAppealCaseIds);
            for (int i = 0; i < _claims.Count; i++)
            {
                AutomationPublicClaim claim = _claims[i];
                checkpoint.CurrentClaims.Add(new AutomationClaimCheckpoint
                {
                    AutomationClaimId = claim.AutomationClaimId,
                    BatchOrdinal = claim.BatchOrdinal,
                    SourceCaseId = claim.SourceCaseId,
                });
            }
            var holdingIds = new List<string>(_precedentModes.Keys);
            holdingIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < holdingIds.Count; i++)
                checkpoint.PrecedentModes.Add(
                    new AutomationPrecedentModeCheckpoint
                    {
                        HoldingId = holdingIds[i],
                        Mode = _precedentModes[holdingIds[i]],
                    });
            return checkpoint;
        }

        public static InstitutionalAutomationSession Restore(
            InstitutionalAutomationCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (checkpoint.SchemaVersion !=
                    InstitutionalAutomationCheckpoint.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(checkpoint.AuthorityPayload) ||
                checkpoint.NextClaimOrdinal < 0 || checkpoint.PulseOrdinal < 1 ||
                checkpoint.ReleasedCaseIds == null ||
                checkpoint.ReservedAppealCaseIds == null ||
                checkpoint.CurrentClaims == null ||
                checkpoint.PrecedentModes == null)
            {
                throw new InvalidOperationException(
                    "Institutional automation checkpoint is incomplete or unsupported.");
            }

            var session = new InstitutionalAutomationSession
            {
                _current = EndogenousRunSnapshotStore.DeserializePayload(
                    checkpoint.AuthorityPayload),
                _nextClaimOrdinal = checkpoint.NextClaimOrdinal,
                _pulseOrdinal = checkpoint.PulseOrdinal,
            };
            AddStable(checkpoint.ReleasedCaseIds, session._releasedCaseIds);
            AddStable(checkpoint.ReservedAppealCaseIds,
                session._reservedAppealCaseIds);
            PlayerInstitutionView view = session.CurrentView();
            var claims = new List<AutomationPublicClaim>(
                checkpoint.CurrentClaims.Count);
            for (int i = 0; i < checkpoint.CurrentClaims.Count; i++)
            {
                AutomationClaimCheckpoint saved = checkpoint.CurrentClaims[i];
                if (saved == null || string.IsNullOrWhiteSpace(
                        saved.AutomationClaimId) || saved.BatchOrdinal < 1 ||
                    string.IsNullOrWhiteSpace(saved.SourceCaseId) ||
                    session._byAutomationId.ContainsKey(saved.AutomationClaimId))
                {
                    throw new InvalidOperationException(
                        "Checkpoint contains an invalid current claim identity.");
                }
                PublicCaseRecord record = FindCase(view, saved.SourceCaseId);
                AutomationPublicClaim claim = ProjectClaim(
                    saved.AutomationClaimId,
                    saved.BatchOrdinal,
                    record,
                    view);
                var envelope = new ClaimEnvelope(saved.SourceCaseId, claim);
                CommittedPlayerRuling committed = session.FindRuling(
                    saved.SourceCaseId);
                if (committed != null)
                    envelope.Result = session.RebuildResult(
                        claim, committed, view);
                session._byAutomationId.Add(
                    saved.AutomationClaimId, envelope);
                claims.Add(claim);
            }
            session._claims = new ReadOnlyCollection<AutomationPublicClaim>(claims);
            for (int i = 0; i < checkpoint.PrecedentModes.Count; i++)
            {
                AutomationPrecedentModeCheckpoint saved =
                    checkpoint.PrecedentModes[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.HoldingId) ||
                    !Enum.IsDefined(typeof(AutomationPrecedentMode), saved.Mode) ||
                    session._current.Docket.GetHolding(saved.HoldingId) == null)
                {
                    throw new InvalidOperationException(
                        "Checkpoint contains an invalid precedent mode.");
                }
                session._precedentModes.Add(saved.HoldingId, saved.Mode);
            }
            return session;
        }

        public AutomationPublicClaim FindClaim(string automationClaimId)
        {
            return !string.IsNullOrWhiteSpace(automationClaimId) &&
                   _byAutomationId.TryGetValue(
                       automationClaimId, out ClaimEnvelope envelope)
                ? envelope.Claim
                : null;
        }

        public AutomationRulingResult GetRulingResult(string automationClaimId)
        {
            return !string.IsNullOrWhiteSpace(automationClaimId) &&
                   _byAutomationId.TryGetValue(
                       automationClaimId, out ClaimEnvelope envelope)
                ? envelope.Result
                : null;
        }

        public AutomationAppealPacket GetAppealPacket(string appealId)
        {
            if (string.IsNullOrWhiteSpace(appealId)) return null;
            EndogenousAppealRecord appeal = _current.Docket.GetAppeal(appealId);
            if (appeal == null) return null;
            EndogenousInstitutionalCase appellateCase =
                _current.Docket.GetCase(appeal.CaseId);
            if (appellateCase == null) return null;
            string parentAutomationId = AutomationIdForRuling(
                appeal.ChallengedRulingId);
            return new AutomationAppealPacket(
                appeal.AppealId,
                parentAutomationId,
                appeal.CaseId,
                appeal.ChallengedRulingId,
                "A later autonomous action contests how the holding was applied.",
                appellateCase.ObservationIds.Count,
                MissingEvidenceCount(CurrentView(), appeal.CaseId));
        }

        public AutomationAppealResolutionResult GetAppealResolutionResult(
            string appealId)
        {
            EndogenousAppealRecord appeal = string.IsNullOrWhiteSpace(appealId)
                ? null
                : _current.Docket.GetAppeal(appealId);
            if (appeal == null || !appeal.Resolved) return null;
            CommittedPlayerRuling ruling = FindRulingById(
                appeal.ResultingRulingId);
            if (ruling == null) return null;
            AutomationAppealProcedure procedure = appeal.ProcedureId switch
            {
                "procedure.fast-track" => AutomationAppealProcedure.FastTrack,
                "procedure.settlement" => AutomationAppealProcedure.Settlement,
                _ => AutomationAppealProcedure.FullRehearing,
            };
            return new AutomationAppealResolutionResult(
                appeal.AppealId,
                ruling.RulingId,
                PlayerInstitutionProjector.Humanise(
                    ruling.Disposition.ToString()),
                appeal.ResultingHoldingId ?? string.Empty,
                procedure,
                ruling.RemedyDefinitionIds);
        }

        public void SetPrecedentMode(
            string holdingId,
            AutomationPrecedentMode mode)
        {
            if (string.IsNullOrWhiteSpace(holdingId) ||
                _current.Docket.GetHolding(holdingId) == null)
                throw new InvalidOperationException(
                    "Only an installed holding can receive an automation mode.");
            if (!Enum.IsDefined(typeof(AutomationPrecedentMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            _precedentModes[holdingId] = mode;
        }

        public bool RequiresHumanPrecedentReview(string automationClaimId)
        {
            if (!_byAutomationId.TryGetValue(
                    automationClaimId, out ClaimEnvelope envelope)) return false;
            EndogenousInstitutionalCase opened =
                _current.Docket.GetCase(envelope.CaseId);
            if (opened == null) return false;
            for (int i = 0; i < _current.Docket.Holdings.Count; i++)
            {
                EndogenousHoldingRecord holding = _current.Docket.Holdings[i];
                if (ModeFor(holding.HoldingId) ==
                        AutomationPrecedentMode.HumanReviewRequired &&
                    HoldingMatches(holding, opened)) return true;
            }
            return false;
        }

        private List<string> ApplyConfiguredHoldings(
            EndogenousInstitutionalCase opened,
            IReadOnlyList<AutomationInstitutionalProcedure> procedures,
            bool humanReviewCompleted)
        {
            bool reusePermitted = ContainsProcedure(
                procedures, AutomationInstitutionalProcedure.PrecedentReuse);
            var allowed = new List<string>();
            for (int i = 0; i < _current.Docket.Holdings.Count; i++)
            {
                EndogenousHoldingRecord holding = _current.Docket.Holdings[i];
                AutomationPrecedentMode mode = ModeFor(holding.HoldingId);
                bool allow = mode == AutomationPrecedentMode.MandatoryCitation ||
                    mode == AutomationPrecedentMode.PermittedCitation &&
                    reusePermitted ||
                    mode == AutomationPrecedentMode.HumanReviewRequired &&
                    humanReviewCompleted;
                if (allow) allowed.Add(holding.HoldingId);
            }
            return allowed.Count == 0
                ? new List<string>()
                : EndogenousAppellateService.ApplyMatchingHoldings(
                    _current.Society,
                    _current.Docket,
                    opened.CaseId,
                    allowed);
        }

        private IReadOnlyList<AutomationPrecedentRecord> ProjectPrecedents()
        {
            var result = new List<AutomationPrecedentRecord>(
                _current.Docket.Holdings.Count);
            for (int i = 0; i < _current.Docket.Holdings.Count; i++)
            {
                EndogenousHoldingRecord holding = _current.Docket.Holdings[i];
                int matching = 0;
                for (int caseIndex = 0;
                     caseIndex < _current.Docket.OpenCases.Count;
                     caseIndex++)
                {
                    EndogenousInstitutionalCase opened =
                        _current.Docket.OpenCases[caseIndex];
                    if (FindRuling(opened.CaseId) == null &&
                        HoldingMatches(holding, opened)) matching++;
                }
                int conflicts = 0;
                for (int otherIndex = 0;
                     otherIndex < _current.Docket.Holdings.Count;
                     otherIndex++)
                {
                    if (otherIndex == i) continue;
                    EndogenousHoldingRecord other =
                        _current.Docket.Holdings[otherIndex];
                    if (string.Equals(holding.IssueId, other.IssueId,
                            StringComparison.Ordinal) &&
                        !string.Equals(holding.RuleId, other.RuleId,
                            StringComparison.Ordinal)) conflicts++;
                }
                bool narrow = ContainsPredicate(
                    holding.Scope, ScopePredicateKind.AgentEquals);
                int liability = holding.AppliedCaseIds.Count * 2 +
                    (narrow ? 2 : 8) + conflicts * 5;
                result.Add(new AutomationPrecedentRecord(
                    holding.HoldingId,
                    PlayerInstitutionProjector.Humanise(holding.IssueId),
                    narrow ? "Present claimant only" :
                        "Issue-wide in Branch 42",
                    holding.SourceAppealId,
                    holding.SourceRulingId,
                    matching,
                    holding.AppliedCaseIds.Count,
                    liability,
                    conflicts,
                    ModeFor(holding.HoldingId)));
            }
            return new ReadOnlyCollection<AutomationPrecedentRecord>(result);
        }

        private AutomationSocietyMetrics ProjectSocietyMetrics()
        {
            int trust = 0;
            int fear = 0;
            int recognisedCollectiveMembers = 0;
            for (int i = 0; i < _current.Society.Agents.Count; i++)
            {
                AgentState agent = _current.Society.Agents[i];
                trust += agent.InstitutionalTrust;
                for (int relationshipIndex = 0;
                     relationshipIndex < agent.Relationships.Count;
                     relationshipIndex++)
                    fear += agent.Relationships[relationshipIndex].Fear;
                for (int statusIndex = 0;
                     statusIndex < agent.Standing.OfficialStatuses.Count;
                     statusIndex++)
                {
                    OfficialStatusState status =
                        agent.Standing.OfficialStatuses[statusIndex];
                    if (status.Recognised && status.StatusId.StartsWith(
                            EndogenousCollectiveRemedyEffectService.
                                CollectiveRecognitionStatusPrefix,
                            StringComparison.Ordinal))
                        recognisedCollectiveMembers++;
                }
            }
            int agents = _current.Society.Agents.Count;
            return new AutomationSocietyMetrics(
                agents,
                agents == 0 ? 0 : trust / agents,
                _current.MaterialWorld.CollectiveCommitments.Count,
                recognisedCollectiveMembers,
                fear);
        }

        private AutomationRulingResult RebuildResult(
            AutomationPublicClaim claim,
            CommittedPlayerRuling committed,
            PlayerInstitutionView view)
        {
            PublicRulingRecord publicRuling = FindPublicRuling(
                view, committed.RulingId);
            AutomationAppealPacket appeal = null;
            for (int i = 0; i < _current.Docket.Appeals.Count; i++)
            {
                EndogenousAppealRecord candidate = _current.Docket.Appeals[i];
                if (!string.Equals(candidate.ChallengedRulingId,
                        committed.RulingId, StringComparison.Ordinal)) continue;
                AutomationAppealPacket restored = GetAppealPacket(
                    candidate.AppealId);
                if (restored != null)
                    appeal = new AutomationAppealPacket(
                        restored.AppealId,
                        claim.AutomationClaimId,
                        restored.SourceCaseId,
                        restored.OriginatingRulingId,
                        restored.PublicBasis,
                        restored.EvidencePacketCount,
                        restored.MissingEvidenceCount);
                break;
            }
            int cited = 0;
            for (int i = 0; i < _current.Docket.Holdings.Count; i++)
                if (Contains(
                        _current.Docket.Holdings[i].AppliedCaseIds,
                        committed.CaseId)) cited++;
            return new AutomationRulingResult(
                claim.AutomationClaimId,
                publicRuling.RulingId,
                publicRuling.Disposition,
                publicRuling.Scope,
                publicRuling.TemporalReach,
                publicRuling.Remedies,
                publicRuling.DirectInstitutionalChanges,
                appeal,
                cited);
        }

        private CommittedPlayerRuling FindRulingById(string rulingId)
        {
            for (int i = 0; i < _current.Docket.Rulings.Count; i++)
                if (string.Equals(_current.Docket.Rulings[i].RulingId,
                        rulingId, StringComparison.Ordinal))
                    return _current.Docket.Rulings[i];
            return null;
        }

        private AutomationPrecedentMode ModeFor(string holdingId)
        {
            return _precedentModes.TryGetValue(
                holdingId, out AutomationPrecedentMode mode)
                ? mode
                : AutomationPrecedentMode.PermittedCitation;
        }

        private static bool HoldingMatches(
            EndogenousHoldingRecord holding,
            EndogenousInstitutionalCase opened)
        {
            if (!string.Equals(holding.IssueId, opened.IssueId,
                    StringComparison.Ordinal)) return false;
            if (opened.PartyIds.Count == 0)
                return ScopeExpressionEvaluator.Matches(
                    holding.Scope,
                    new ScopeMatchContext
                    {
                        IssueId = opened.IssueId,
                        JurisdictionId = "branch-42",
                    });
            for (int i = 0; i < opened.PartyIds.Count; i++)
                if (ScopeExpressionEvaluator.Matches(
                        holding.Scope,
                        new ScopeMatchContext
                        {
                            AgentId = opened.PartyIds[i],
                            IssueId = opened.IssueId,
                            JurisdictionId = "branch-42",
                        })) return true;
            return false;
        }

        private string AutomationIdForRuling(string rulingId)
        {
            for (int i = 0; i < _claims.Count; i++)
            {
                AutomationRulingResult result = GetRulingResult(
                    _claims[i].AutomationClaimId);
                if (result != null && string.Equals(
                        result.RulingId, rulingId, StringComparison.Ordinal))
                    return result.AutomationClaimId;
            }
            return string.Empty;
        }

        private static int MissingEvidenceCount(
            PlayerInstitutionView view,
            string caseId)
        {
            return FindCase(view, caseId).MissingEvidence.Count;
        }

        private static bool ContainsPredicate(
            ScopeExpression expression,
            ScopePredicateKind kind)
        {
            if (expression.Kind == ScopeExpressionKind.Predicate &&
                expression.PredicateKind == kind) return true;
            for (int i = 0; i < expression.Children.Count; i++)
                if (ContainsPredicate(expression.Children[i], kind)) return true;
            return false;
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string expected)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static void CopyStable(
            IEnumerable<string> source,
            List<string> destination)
        {
            destination.AddRange(source);
            destination.Sort(StringComparer.Ordinal);
        }

        private static void AddStable(
            IReadOnlyList<string> source,
            HashSet<string> destination)
        {
            for (int i = 0; i < source.Count; i++)
                if (string.IsNullOrWhiteSpace(source[i]) ||
                    !destination.Add(source[i]))
                    throw new InvalidOperationException(
                        "Checkpoint contains duplicate or unstable identities.");
        }
    }
}
