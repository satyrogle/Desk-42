using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional.Player
{
    /// <summary>
    /// Product-facing adapter over one continuing endogenous society. Dossiers are
    /// released in operational batches, but every batch shares the same agents,
    /// material world, docket, rulings and later consequences.
    /// </summary>
    public sealed class InstitutionalAutomationSession
    {
        private readonly Dictionary<string, ClaimEnvelope> _byAutomationId =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _releasedCaseIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _reservedAppealCaseIds = new(StringComparer.Ordinal);
        private EndogenousRunSnapshot _current;
        private IReadOnlyList<AutomationPublicClaim> _claims =
            new ReadOnlyCollection<AutomationPublicClaim>(new List<AutomationPublicClaim>());
        private int _nextClaimOrdinal;
        private int _pulseOrdinal = 1;

        private InstitutionalAutomationSession(int initialClaimCount)
        {
            ValidateClaimCount(initialClaimCount);
            _current = CausalLegibilitySliceSeed.CreatePersistentAutomationSnapshot();
            ReleaseNextShift(initialClaimCount);
        }

        public IReadOnlyList<AutomationPublicClaim> Claims => _claims;
        public long SocietyTick => _current.Society.CurrentTick;
        public int CommittedRulingCount => _current.Docket.Rulings.Count;
        public int HoldingCount => _current.Docket.Holdings.Count;

        public static InstitutionalAutomationSession Create(int claimCount)
        {
            return new InstitutionalAutomationSession(claimCount);
        }

        /// <summary>
        /// Releases the next operational batch from the same docket. If the docket is
        /// short, generic society pulses create more material events and observations.
        /// </summary>
        public IReadOnlyList<AutomationPublicClaim> ReleaseNextShift(int claimCount)
        {
            ValidateClaimCount(claimCount);
            EnsureAvailableCases(claimCount);
            PlayerInstitutionView view = CurrentView();
            var selected = new List<PublicCaseRecord>(claimCount);
            for (int i = 0; i < view.Cases.Count && selected.Count < claimCount; i++)
            {
                PublicCaseRecord record = view.Cases[i];
                if (record.RulingCommitted ||
                    _releasedCaseIds.Contains(record.CaseId) ||
                    _reservedAppealCaseIds.Contains(record.CaseId)) continue;
                selected.Add(record);
            }
            if (selected.Count != claimCount)
                throw new InvalidOperationException(
                    "The continuing society could not release the requested docket batch.");

            var claims = new List<AutomationPublicClaim>(claimCount);
            for (int i = 0; i < selected.Count; i++)
            {
                PublicCaseRecord record = selected[i];
                int ordinal = ++_nextClaimOrdinal;
                string automationId = "claim.branch42." + ordinal.ToString("D3");
                AutomationPublicClaim claim = ProjectClaim(
                    automationId, ordinal, record, view);
                _releasedCaseIds.Add(record.CaseId);
                _byAutomationId.Add(
                    automationId, new ClaimEnvelope(record.CaseId, claim));
                claims.Add(claim);
            }
            _claims = new ReadOnlyCollection<AutomationPublicClaim>(claims);
            return _claims;
        }

        public AutomationRulingResult Commit(
            string automationClaimId,
            PlayerScopeChoice scope,
            PlayerRulingDisposition disposition)
        {
            return Commit(
                automationClaimId,
                scope,
                disposition,
                citeMatchingHoldings: false);
        }

        public AutomationRulingResult Commit(
            string automationClaimId,
            PlayerScopeChoice scope,
            PlayerRulingDisposition disposition,
            bool citeMatchingHoldings)
        {
            var procedures = new List<AutomationInstitutionalProcedure>();
            if (citeMatchingHoldings)
                procedures.Add(AutomationInstitutionalProcedure.PrecedentReuse);
            return Commit(automationClaimId, scope, disposition, procedures);
        }

        public AutomationRulingResult Commit(
            string automationClaimId,
            PlayerScopeChoice scope,
            PlayerRulingDisposition disposition,
            IReadOnlyList<AutomationInstitutionalProcedure> procedures)
        {
            if (string.IsNullOrWhiteSpace(automationClaimId))
                throw new ArgumentException(
                    "An automation claim id is required.", nameof(automationClaimId));
            if (!_byAutomationId.TryGetValue(
                    automationClaimId, out ClaimEnvelope envelope))
                throw new InvalidOperationException(
                    $"Automation claim '{automationClaimId}' is not in this society.");
            if (envelope.Result != null)
                throw new InvalidOperationException(
                    $"Automation claim '{automationClaimId}' already has a ruling.");

            EndogenousInstitutionalCase opened = _current.Docket.GetCase(envelope.CaseId) ??
                throw new InvalidOperationException(
                    "The released case no longer exists in the continuing docket.");
            if (FindRuling(opened.CaseId) != null)
                throw new InvalidOperationException("The released case is already ruled.");

            procedures ??= Array.Empty<AutomationInstitutionalProcedure>();
            List<string> citedHoldingIds = ContainsProcedure(
                    procedures, AutomationInstitutionalProcedure.PrecedentReuse)
                ? EndogenousAppellateService.ApplyMatchingHoldings(
                    _current.Society, _current.Docket, opened.CaseId)
                : new List<string>();

            var command = new PlayerRulingCommand
            {
                CommandId =
                    "automation-command:" + automationClaimId + ":" +
                    scope + ":" + disposition,
                CaseId = opened.CaseId,
                ExpectedCaseVersion = opened.CaseVersion,
                EvidenceEnvelopeHash = opened.EvidenceEnvelopeHash,
                RecognisedFactIds = Copy(opened.AvailableFactIds),
                CitedEvidenceArtifactIds = Copy(opened.ObservationIds),
                Disposition = ToDomainDisposition(disposition),
                HoldingRuleId = HoldingFor(opened.IssueId),
                Scope = CausalLegibilityScopeFactory.Create(opened, scope),
                TemporalReach = TemporalReach.Prospective,
                RemedyDefinitionIds = new List<string>
                {
                    RemedyFor(opened.IssueId, disposition),
                },
                AppliedProcedureIds = ProcedureIds(procedures),
            };
            CommittedPlayerRuling committed = EndogenousPlayerRulingService.Commit(
                _current.Society, _current.Docket, command);

            SimulationInput input = CausalLegibilitySliceSeed.QuietInput();
            input.IncidentId = "automation-ruling-pulse:" + committed.RulingId;
            if (string.Equals(
                    opened.IssueId,
                    EndogenousIssueKindIds.PossessionDispute,
                    StringComparison.Ordinal))
            {
                EndogenousRemedyEffectService.Execute(
                    _current.Society,
                    _current.MaterialWorld,
                    _current.Docket,
                    committed);
            }
            else if (string.Equals(
                         opened.IssueId,
                         EndogenousIssueKindIds.AccessWithdrawal,
                         StringComparison.Ordinal))
            {
                EndogenousAccessRemedyEffectService.Execute(
                    _current.Society,
                    _current.MaterialWorld,
                    _current.Docket,
                    committed);
            }
            EndogenousActionOpportunityBuilder.Populate(
                _current.Society, _current.MaterialWorld, input);
            EndogenousScopeEffectService.Apply(
                _current.Society, _current.Docket, input);
            new EndogenousSocietyStepService().Advance(
                _current.Society, _current.MaterialWorld, input);
            EndogenousIncidentDocketPipeline.Process(
                _current.MaterialWorld,
                _current.Society,
                _current.Docket,
                admitOneCase: false);
            CausalLegibilitySliceSeed.AdmitAllCases(
                _current.Society, _current.Docket);
            EndogenousInstitutionalCase internalDescendant =
                scope == PlayerScopeChoice.Broad &&
                disposition == PlayerRulingDisposition.Recognised
                    ? FindDescendant(opened.CaseId, committed.RulingId)
                    : null;
            string appealId = null;
            if (internalDescendant != null)
            {
                appealId =
                    "appeal.branch42." + envelope.Claim.BatchOrdinal.ToString("D3");
                _reservedAppealCaseIds.Add(internalDescendant.CaseId);
                EndogenousAppellateService.File(
                    _current.Society,
                    _current.Docket,
                    appealId,
                    internalDescendant.CaseId,
                    committed.RulingId,
                    internalDescendant.ObservationIds);
            }
            CaptureCurrent("persistent-automation.after-ruling");

            PlayerInstitutionView ruled = CurrentView();
            PublicRulingRecord ruling = FindPublicRuling(ruled, committed.RulingId);
            PublicCaseRecord descendant = internalDescendant == null
                ? null
                : FindCase(ruled, internalDescendant.CaseId);
            AutomationAppealPacket appeal = null;
            if (descendant != null)
            {
                _reservedAppealCaseIds.Add(descendant.CaseId);
                appeal = new AutomationAppealPacket(
                    appealId,
                    automationClaimId,
                    descendant.CaseId,
                    committed.RulingId,
                    "A later autonomous action contests how the holding was applied.",
                    descendant.EvidenceIds.Count,
                    descendant.MissingEvidence.Count);
            }
            envelope.Result = new AutomationRulingResult(
                automationClaimId,
                ruling.RulingId,
                ruling.Disposition,
                ruling.Scope,
                ruling.TemporalReach,
                ruling.Remedies,
                ruling.DirectInstitutionalChanges,
                appeal,
                citedHoldingIds.Count);
            return envelope.Result;
        }

        public AutomationAppealResolutionResult ResolveAppeal(
            AutomationAppealPacket packet,
            AutomationAppealProcedure procedure,
            bool establishHolding)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));
            if (!Enum.IsDefined(typeof(AutomationAppealProcedure), procedure))
                throw new ArgumentOutOfRangeException(nameof(procedure));
            EndogenousAppealRecord appeal = _current.Docket.GetAppeal(packet.AppealId) ??
                throw new InvalidOperationException(
                    "The returned appeal is not filed in the continuing docket.");
            PublicCaseRecord publicCase = FindCase(CurrentView(), appeal.CaseId);
            RulingDisposition outcome = procedure switch
            {
                AutomationAppealProcedure.Settlement => RulingDisposition.Affirmed,
                AutomationAppealProcedure.FastTrack => RulingDisposition.Affirmed,
                AutomationAppealProcedure.FullRehearing
                    when publicCase.EvidenceSupportMinimum >= 40 =>
                        RulingDisposition.Affirmed,
                _ => RulingDisposition.ReversedAndDenied,
            };
            EndogenousAppellateResolution resolution =
                EndogenousAppellateService.Resolve(
                    _current.Society,
                    _current.Docket,
                    appeal.AppealId,
                    ToDomainProcedure(procedure),
                    outcome,
                    establishHolding && procedure != AutomationAppealProcedure.Settlement);
            EndogenousInstitutionalCase opened = _current.Docket.GetCase(appeal.CaseId);
            if (string.Equals(
                    opened.IssueId,
                    EndogenousIssueKindIds.PossessionDispute,
                    StringComparison.Ordinal))
            {
                EndogenousRemedyEffectService.Execute(
                    _current.Society,
                    _current.MaterialWorld,
                    _current.Docket,
                    resolution.Ruling);
            }
            else if (string.Equals(
                         opened.IssueId,
                         EndogenousIssueKindIds.AccessWithdrawal,
                         StringComparison.Ordinal))
            {
                EndogenousAccessRemedyEffectService.Execute(
                    _current.Society,
                    _current.MaterialWorld,
                    _current.Docket,
                    resolution.Ruling);
            }

            SimulationInput input = CausalLegibilitySliceSeed.QuietInput();
            input.IncidentId = "automation-appeal-pulse:" + appeal.AppealId;
            EndogenousActionOpportunityBuilder.Populate(
                _current.Society, _current.MaterialWorld, input);
            EndogenousScopeEffectService.Apply(
                _current.Society, _current.Docket, input);
            new EndogenousSocietyStepService().Advance(
                _current.Society, _current.MaterialWorld, input);
            EndogenousIncidentDocketPipeline.Process(
                _current.MaterialWorld,
                _current.Society,
                _current.Docket,
                admitOneCase: false);
            CausalLegibilitySliceSeed.AdmitAllCases(
                _current.Society, _current.Docket);
            CaptureCurrent("persistent-automation.after-appeal");

            return new AutomationAppealResolutionResult(
                appeal.AppealId,
                resolution.Ruling.RulingId,
                PlayerInstitutionProjector.Humanise(
                    resolution.Ruling.Disposition.ToString()),
                resolution.Holding?.HoldingId ?? string.Empty,
                procedure,
                resolution.Ruling.RemedyDefinitionIds);
        }

        private void EnsureAvailableCases(int required)
        {
            const int maximumPulses = 64;
            for (int attempt = 0; attempt <= maximumPulses; attempt++)
            {
                PlayerInstitutionView view = CurrentView();
                int available = 0;
                for (int i = 0; i < view.Cases.Count; i++)
                {
                    PublicCaseRecord record = view.Cases[i];
                    if (!record.RulingCommitted &&
                        !_releasedCaseIds.Contains(record.CaseId) &&
                        !_reservedAppealCaseIds.Contains(record.CaseId)) available++;
                }
                if (available >= required) return;
                CausalLegibilitySliceSeed.AdvanceAutomationPulse(
                    _current.Society,
                    _current.MaterialWorld,
                    _current.Docket,
                    "automation-feed-pulse:" + (_pulseOrdinal++).ToString("D3"));
                CausalLegibilitySliceSeed.AdmitAllCases(
                    _current.Society, _current.Docket);
                CaptureCurrent("persistent-automation.feed");
            }
            throw new InvalidOperationException(
                "The continuing society exhausted its bounded claim-generation window.");
        }

        private PlayerInstitutionView CurrentView()
        {
            return PlayerInstitutionProjector.Project(
                _current.Society, _current.MaterialWorld, _current.Docket);
        }

        private void CaptureCurrent(string snapshotId)
        {
            _current = EndogenousRunSnapshotService.Capture(
                snapshotId,
                EndogenousCommitPhase.ScopeEffectsCommitted,
                _current.Society,
                _current.MaterialWorld,
                _current.Docket);
        }

        private CommittedPlayerRuling FindRuling(string caseId)
        {
            for (int i = 0; i < _current.Docket.Rulings.Count; i++)
                if (string.Equals(
                        _current.Docket.Rulings[i].CaseId,
                        caseId,
                        StringComparison.Ordinal))
                    return _current.Docket.Rulings[i];
            return null;
        }

        private EndogenousInstitutionalCase FindDescendant(
            string parentCaseId,
            string rulingId)
        {
            for (int i = 0; i < _current.Docket.OpenCases.Count; i++)
            {
                EndogenousInstitutionalCase candidate = _current.Docket.OpenCases[i];
                if (string.Equals(candidate.ParentCaseId, parentCaseId,
                        StringComparison.Ordinal) &&
                    string.Equals(candidate.OriginatingRulingId, rulingId,
                        StringComparison.Ordinal)) return candidate;
            }
            return null;
        }

        private static AutomationPublicClaim ProjectClaim(
            string automationId,
            int ordinal,
            PublicCaseRecord record,
            PlayerInstitutionView view)
        {
            int citable = 0;
            for (int i = 0; i < view.Evidence.Count; i++)
                if (string.Equals(
                        view.Evidence[i].CaseId,
                        record.CaseId,
                        StringComparison.Ordinal) && view.Evidence[i].Citable)
                    citable++;
            return new AutomationPublicClaim(
                automationId,
                ordinal,
                "CLAIM 42-" + ordinal.ToString("D3"),
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
                view.UnknownsSummary,
                record.ParentCaseId,
                record.OriginatingRulingId);
        }

        private static PublicRulingRecord FindPublicRuling(
            PlayerInstitutionView view,
            string rulingId)
        {
            for (int i = 0; i < view.Rulings.Count; i++)
                if (string.Equals(
                        view.Rulings[i].RulingId,
                        rulingId,
                        StringComparison.Ordinal)) return view.Rulings[i];
            throw new InvalidOperationException(
                "The committed ruling has no public projection.");
        }

        private static PublicCaseRecord FindCase(
            PlayerInstitutionView view,
            string caseId)
        {
            for (int i = 0; i < view.Cases.Count; i++)
            {
                PublicCaseRecord candidate = view.Cases[i];
                if (string.Equals(
                        candidate.CaseId,
                        caseId,
                        StringComparison.Ordinal)) return candidate;
            }
            throw new InvalidOperationException(
                "The continuing docket has no public projection for the requested case.");
        }

        private static EndogenousAppellateProcedure ToDomainProcedure(
            AutomationAppealProcedure procedure)
        {
            return procedure switch
            {
                AutomationAppealProcedure.FullRehearing =>
                    EndogenousAppellateProcedure.FullRehearing,
                AutomationAppealProcedure.FastTrack =>
                    EndogenousAppellateProcedure.FastTrack,
                AutomationAppealProcedure.Settlement =>
                    EndogenousAppellateProcedure.Settlement,
                _ => throw new ArgumentOutOfRangeException(nameof(procedure)),
            };
        }

        private static string HoldingFor(string issueId)
        {
            if (string.Equals(issueId, EndogenousIssueKindIds.PossessionDispute,
                    StringComparison.Ordinal))
                return EndogenousPlayerRulingService.PossessionHoldingRule;
            if (string.Equals(issueId, EndogenousIssueKindIds.AccessWithdrawal,
                    StringComparison.Ordinal))
                return EndogenousPlayerRulingService.AccessHoldingRule;
            if (string.Equals(issueId, EndogenousIssueKindIds.CollectiveGrievance,
                    StringComparison.Ordinal))
                return EndogenousPlayerRulingService.CollectiveHoldingRule;
            throw new InvalidOperationException(
                "The automation feed encountered an unsupported institutional issue.");
        }

        private static string RemedyFor(
            string issueId,
            PlayerRulingDisposition disposition)
        {
            if (disposition == PlayerRulingDisposition.Denied)
                return EndogenousPlayerRulingService.NoChangeRemedy;
            if (string.Equals(issueId, EndogenousIssueKindIds.PossessionDispute,
                    StringComparison.Ordinal))
                return EndogenousPlayerRulingService.RestorePossessionRemedy;
            if (string.Equals(issueId, EndogenousIssueKindIds.AccessWithdrawal,
                    StringComparison.Ordinal))
                return EndogenousPlayerRulingService.RestoreAccessRemedy;
            return EndogenousPlayerRulingService.RecogniseCollectiveRemedy;
        }

        private static RulingDisposition ToDomainDisposition(
            PlayerRulingDisposition disposition)
        {
            return disposition switch
            {
                PlayerRulingDisposition.Recognised => RulingDisposition.Recognised,
                PlayerRulingDisposition.Denied => RulingDisposition.Denied,
                _ => throw new ArgumentOutOfRangeException(nameof(disposition)),
            };
        }

        private static List<string> Copy(IReadOnlyList<string> source)
        {
            var result = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            return result;
        }

        private static void ValidateClaimCount(int claimCount)
        {
            if (claimCount < 1 || claimCount > 32)
                throw new ArgumentOutOfRangeException(nameof(claimCount));
        }

        private static bool ContainsProcedure(
            IReadOnlyList<AutomationInstitutionalProcedure> procedures,
            AutomationInstitutionalProcedure expected)
        {
            for (int i = 0; i < procedures.Count; i++)
                if (procedures[i] == expected) return true;
            return false;
        }

        private static List<string> ProcedureIds(
            IReadOnlyList<AutomationInstitutionalProcedure> procedures)
        {
            var result = new List<string>(procedures.Count);
            for (int i = 0; i < procedures.Count; i++)
            {
                string id = procedures[i] switch
                {
                    AutomationInstitutionalProcedure.MandatorySecondaryVerification =>
                        "procedure.secondary-verification",
                    AutomationInstitutionalProcedure.PresumptionOfValidity =>
                        "procedure.presumption-validity",
                    AutomationInstitutionalProcedure.AutomaticAdverseReview =>
                        "procedure.automatic-adverse-review",
                    AutomationInstitutionalProcedure.ProtectedEvidenceChannel =>
                        "procedure.protected-evidence-channel",
                    AutomationInstitutionalProcedure.AppealFastTrack =>
                        "procedure.appeal-fast-track",
                    AutomationInstitutionalProcedure.PrecedentReuse =>
                        "procedure.precedent-reuse",
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(procedures), procedures[i], "Unsupported procedure."),
                };
                if (!result.Contains(id)) result.Add(id);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private sealed class ClaimEnvelope
        {
            internal ClaimEnvelope(string caseId, AutomationPublicClaim claim)
            {
                CaseId = caseId;
                Claim = claim;
            }

            internal string CaseId { get; }
            internal AutomationPublicClaim Claim { get; }
            internal AutomationRulingResult Result { get; set; }
        }
    }

    public enum AutomationInstitutionalProcedure
    {
        MandatorySecondaryVerification,
        PresumptionOfValidity,
        AutomaticAdverseReview,
        ProtectedEvidenceChannel,
        AppealFastTrack,
        PrecedentReuse,
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
            string unknownsSummary,
            string parentCaseId,
            string originatingRulingId)
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
            ParentCaseId = parentCaseId ?? string.Empty;
            OriginatingRulingId = originatingRulingId ?? string.Empty;
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
        public string ParentCaseId { get; }
        public string OriginatingRulingId { get; }

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
            AutomationAppealPacket appeal,
            int citedHoldingCount)
        {
            AutomationClaimId = automationClaimId ?? string.Empty;
            RulingId = rulingId ?? string.Empty;
            Disposition = disposition ?? string.Empty;
            Scope = scope ?? string.Empty;
            TemporalReach = temporalReach ?? string.Empty;
            Remedies = Freeze(remedies);
            DirectInstitutionalChanges = Freeze(directChanges);
            Appeal = appeal;
            CitedHoldingCount = citedHoldingCount;
        }

        public string AutomationClaimId { get; }
        public string RulingId { get; }
        public string Disposition { get; }
        public string Scope { get; }
        public string TemporalReach { get; }
        public IReadOnlyList<string> Remedies { get; }
        public IReadOnlyList<string> DirectInstitutionalChanges { get; }
        public AutomationAppealPacket Appeal { get; }
        public int CitedHoldingCount { get; }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values)
        {
            return new ReadOnlyCollection<string>(
                values == null ? new List<string>() : new List<string>(values));
        }
    }

    public enum AutomationAppealProcedure
    {
        FullRehearing,
        FastTrack,
        Settlement,
    }

    public sealed class AutomationAppealResolutionResult
    {
        internal AutomationAppealResolutionResult(
            string appealId,
            string rulingId,
            string disposition,
            string holdingId,
            AutomationAppealProcedure procedure,
            IEnumerable<string> remedies)
        {
            AppealId = appealId ?? string.Empty;
            RulingId = rulingId ?? string.Empty;
            Disposition = disposition ?? string.Empty;
            HoldingId = holdingId ?? string.Empty;
            Procedure = procedure;
            Remedies = new ReadOnlyCollection<string>(
                remedies == null ? new List<string>() : new List<string>(remedies));
        }

        public string AppealId { get; }
        public string RulingId { get; }
        public string Disposition { get; }
        public string HoldingId { get; }
        public AutomationAppealProcedure Procedure { get; }
        public IReadOnlyList<string> Remedies { get; }
        public bool EstablishedHolding => !string.IsNullOrWhiteSpace(HoldingId);
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
