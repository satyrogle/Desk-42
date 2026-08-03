using System;
using System.Collections.Generic;

namespace Desk42.Institutional.Player
{
    /// <summary>
    /// Public façade for the first playable institutional loop. It owns one active
    /// causal run and exposes only immutable player projections and validated commands.
    /// </summary>
    public sealed class CausalLegibilitySliceSession
    {
        private const string OriginSuffix = ".pre-ruling";

        private EndogenousRunSnapshot _canonical;
        private EndogenousRunSnapshot _current;

        private CausalLegibilitySliceSession(
            EndogenousRunSnapshot canonical,
            EndogenousRunSnapshot current)
        {
            _canonical = CopySnapshot(canonical, "causal-legibility.canonical");
            _current = CopySnapshot(current, "causal-legibility.current");
            ValidateSessionState();
            RefreshView();
        }

        public PlayerInstitutionView View { get; private set; }

        public static CausalLegibilitySliceSession Create()
        {
            EndogenousRunSnapshot canonical =
                CausalLegibilitySliceSeed.CreatePreRulingSnapshot();
            return new CausalLegibilitySliceSession(canonical, canonical);
        }

        public static CausalLegibilitySliceSession Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A session path is required.", nameof(path));
            EndogenousRunSnapshot current = EndogenousRunSnapshotStore.Load(path);
            EndogenousRunSnapshot canonical = EndogenousRunSnapshotStore.Load(
                path + OriginSuffix);
            return new CausalLegibilitySliceSession(canonical, current);
        }

        public PlayerRulingDraft CreateDraft(
            PlayerScopeChoice scopeChoice,
            RulingDisposition disposition)
        {
            if (_current.Docket.Rulings.Count != 0)
                throw new InvalidOperationException(
                    "Replay from the pre-ruling snapshot before issuing another ruling.");
            EndogenousInstitutionalCase opened = CurrentCase();
            return new PlayerRulingDraft(
                $"slice-command:{opened.CaseId}:{scopeChoice}:{disposition}",
                opened.CaseId,
                opened.CaseVersion,
                opened.EvidenceEnvelopeHash,
                opened.AvailableFactIds,
                opened.ObservationIds,
                disposition,
                scopeChoice);
        }

        public PlayerRulingPreview Preview(PlayerRulingDraft draft)
        {
            ValidateDraftEnvelope(draft);
            ScopeMatchPreview scope = PlayerInstitutionProjector.FindScopePreview(
                View, draft.ScopeChoice);
            string remedy = RemedyFor(draft.Disposition);
            string dispositionLabel = PlayerInstitutionProjector.Humanise(
                draft.Disposition.ToString());
            var direct = new List<string>
            {
                $"Recognise {draft.RecognisedFactIds.Count} official finding(s)",
                $"Record disposition: {dispositionLabel}",
                $"Establish: Possession requires authorised transfer",
                $"Apply: {scope.Description}",
                $"Authorise: {PlayerInstitutionProjector.Humanise(remedy)}",
            };
            return new PlayerRulingPreview(
                $"Recognise {draft.RecognisedFactIds.Count} available proposition(s)",
                dispositionLabel,
                "Possession requires authorised transfer",
                scope,
                "Prospective",
                PlayerInstitutionProjector.Humanise(remedy),
                direct);
        }

        public PlayerInstitutionView Commit(PlayerRulingDraft draft)
        {
            ValidateDraftEnvelope(draft);
            EndogenousInstitutionalCase opened = CurrentCase();
            var command = new PlayerRulingCommand
            {
                CommandId = draft.CommandId,
                CaseId = draft.CaseId,
                ExpectedCaseVersion = draft.ExpectedCaseRevision,
                EvidenceEnvelopeHash = draft.EvidenceEnvelopeHash,
                RecognisedFactIds = Copy(draft.RecognisedFactIds),
                CitedEvidenceArtifactIds = Copy(draft.CitedEvidenceIds),
                Disposition = draft.Disposition,
                HoldingRuleId = EndogenousPlayerRulingService.PossessionHoldingRule,
                Scope = CausalLegibilityScopeFactory.Create(
                    opened, draft.ScopeChoice),
                TemporalReach = TemporalReach.Prospective,
                RemedyDefinitionIds = new List<string>
                {
                    RemedyFor(draft.Disposition),
                },
            };
            EndogenousPlayerRulingService.Commit(
                _current.Society, _current.Docket, command);

            SimulationInput input = CausalLegibilitySliceSeed.QuietInput();
            EndogenousActionOpportunityBuilder.Populate(
                _current.Society, _current.MaterialWorld, input);
            EndogenousScopeEffectService.Apply(
                _current.Society, _current.Docket, input);
            new EndogenousSocietyStepService().Advance(
                _current.Society, _current.MaterialWorld, input);
            EndogenousIncidentDocketPipeline.Process(
                _current.MaterialWorld, _current.Society, _current.Docket);

            _current = EndogenousRunSnapshotService.Capture(
                "causal-legibility.after-ruling",
                EndogenousCommitPhase.ScopeEffectsCommitted,
                _current.Society,
                _current.MaterialWorld,
                _current.Docket);
            RefreshView();
            return View;
        }

        public PlayerInstitutionView ReplayFromPreRuling()
        {
            _current = CopySnapshot(_canonical, "causal-legibility.replay");
            RefreshView();
            return View;
        }

        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A session path is required.", nameof(path));
            EndogenousRunSnapshotStore.Save(path + OriginSuffix, _canonical);
            EndogenousRunSnapshotStore.Save(path, _current);
        }

        private void RefreshView()
        {
            View = PlayerInstitutionProjector.Project(
                _current.Society, _current.MaterialWorld, _current.Docket);
        }

        private void ValidateSessionState()
        {
            EndogenousRunSnapshotValidator.Validate(_canonical);
            EndogenousRunSnapshotValidator.Validate(_current);
            if (_canonical.Docket.Rulings.Count != 0 ||
                _canonical.Docket.OpenCases.Count != 1)
            {
                throw new InvalidOperationException(
                    "The replay origin must contain one unrated case and no ruling.");
            }
        }

        private EndogenousInstitutionalCase CurrentCase()
        {
            if (_current.Docket.OpenCases.Count == 0)
                throw new InvalidOperationException("No case is available for ruling.");
            return _current.Docket.OpenCases[0];
        }

        private void ValidateDraftEnvelope(PlayerRulingDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            EndogenousInstitutionalCase opened = CurrentCase();
            if (!string.Equals(
                    draft.CaseId,
                    opened.CaseId,
                    StringComparison.Ordinal) ||
                draft.ExpectedCaseRevision != opened.CaseVersion ||
                !string.Equals(
                    draft.EvidenceEnvelopeHash,
                    opened.EvidenceEnvelopeHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The case changed while the ruling was composed. Review the latest record.");
            }
            if (_current.Docket.Rulings.Count != 0)
                throw new InvalidOperationException("This case already has a committed ruling.");
        }

        private static string RemedyFor(RulingDisposition disposition)
        {
            return disposition == RulingDisposition.Denied
                ? EndogenousPlayerRulingService.NoChangeRemedy
                : EndogenousPlayerRulingService.RestorePossessionRemedy;
        }

        private static List<string> Copy(IReadOnlyList<string> source)
        {
            var result = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            return result;
        }

        private static EndogenousRunSnapshot CopySnapshot(
            EndogenousRunSnapshot source,
            string snapshotId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return EndogenousRunSnapshotService.Capture(
                snapshotId,
                source.Phase,
                source.Society,
                source.MaterialWorld,
                source.Docket);
        }
    }

    internal static class CausalLegibilityScopeFactory
    {
        internal static ScopeExpression Create(
            EndogenousInstitutionalCase opened,
            PlayerScopeChoice choice)
        {
            if (opened == null) throw new ArgumentNullException(nameof(opened));
            var issue = new ScopeExpression
            {
                Kind = ScopeExpressionKind.Predicate,
                PredicateKind = ScopePredicateKind.IssueEquals,
                Value = opened.IssueId,
            };
            if (choice == PlayerScopeChoice.Broad) return issue;
            if (opened.PartyIds.Count == 0)
                throw new InvalidOperationException(
                    "A claimant-limited scope requires an official party.");
            return new ScopeExpression
            {
                Kind = ScopeExpressionKind.All,
                Children = new List<ScopeExpression>
                {
                    issue,
                    new ScopeExpression
                    {
                        Kind = ScopeExpressionKind.Predicate,
                        PredicateKind = ScopePredicateKind.AgentEquals,
                        Value = opened.PartyIds[0],
                    },
                },
            };
        }
    }
}
