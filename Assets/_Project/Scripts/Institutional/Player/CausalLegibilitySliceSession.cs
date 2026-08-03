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
        private readonly string _sessionId;
        private long _generation;
        private EndogenousRunSnapshot _canonical;
        private EndogenousRunSnapshot _current;

        private CausalLegibilitySliceSession(
            string sessionId,
            long generation,
            EndogenousRunSnapshot canonical,
            EndogenousRunSnapshot current)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A stable session id is required.", nameof(sessionId));
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            _sessionId = sessionId;
            _generation = generation;
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
            return new CausalLegibilitySliceSession(
                "causal-legibility-session:" + Guid.NewGuid().ToString("N"),
                0,
                canonical,
                canonical);
        }

        public static CausalLegibilitySliceSession Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A session path is required.", nameof(path));
            EndogenousRunSessionSnapshot saved = EndogenousRunSnapshotStore.LoadSession(path);
            return new CausalLegibilitySliceSession(
                saved.SessionId,
                saved.Generation,
                saved.Origin,
                saved.Current);
        }

        public PlayerRulingDraft CreateDraft(
            PlayerScopeChoice scopeChoice,
            PlayerRulingDisposition disposition)
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
                $"System derives {draft.RecognisedFactIds.Count} official finding(s)",
                $"Player selects disposition: {dispositionLabel}",
            };
            if (draft.Disposition == PlayerRulingDisposition.Denied)
            {
                direct.Add("Do not establish the system-derived proposed holding");
                direct.Add("Do not apply the proposed scope or material remedy");
            }
            else
            {
                direct.Add(
                    "System-derived holding: Possession requires authorised transfer");
                direct.Add($"Player selects scope: {scope.Description}");
                direct.Add(
                    $"Required execution: {PlayerInstitutionProjector.Humanise(remedy)} " +
                    "to the registered owner");
            }
            return new PlayerRulingPreview(
                $"System-derived: recognise {draft.RecognisedFactIds.Count} " +
                "available proposition(s)",
                dispositionLabel,
                "System-derived proposal: Possession requires authorised transfer",
                scope,
                "Fixed: Prospective",
                "Disposition-required: " + PlayerInstitutionProjector.Humanise(remedy),
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
                Disposition = ToDomainDisposition(draft.Disposition),
                HoldingRuleId = EndogenousPlayerRulingService.PossessionHoldingRule,
                Scope = CausalLegibilityScopeFactory.Create(
                    opened, draft.ScopeChoice),
                TemporalReach = TemporalReach.Prospective,
                RemedyDefinitionIds = new List<string>
                {
                    RemedyFor(draft.Disposition),
                },
            };
            CommittedPlayerRuling committed = EndogenousPlayerRulingService.Commit(
                _current.Society, _current.Docket, command);

            SimulationInput input = CausalLegibilitySliceSeed.QuietInput();
            EndogenousActionOpportunityBuilder.Populate(
                _current.Society, _current.MaterialWorld, input);
            EndogenousRemedyEffectService.Execute(
                _current.Society,
                _current.MaterialWorld,
                _current.Docket,
                committed);
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
            long nextGeneration = checked(_generation + 1);
            EndogenousRunSessionSnapshot saved =
                EndogenousRunSessionSnapshotService.Capture(
                    _sessionId,
                    nextGeneration,
                    _canonical,
                    _current);
            EndogenousRunSnapshotStore.SaveSession(path, saved);
            _generation = nextGeneration;
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

        private static string RemedyFor(PlayerRulingDisposition disposition)
        {
            return disposition == PlayerRulingDisposition.Denied
                ? EndogenousPlayerRulingService.NoChangeRemedy
                : EndogenousPlayerRulingService.RestorePossessionRemedy;
        }

        private static RulingDisposition ToDomainDisposition(
            PlayerRulingDisposition disposition)
        {
            switch (disposition)
            {
                case PlayerRulingDisposition.Recognised:
                    return RulingDisposition.Recognised;
                case PlayerRulingDisposition.Denied:
                    return RulingDisposition.Denied;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(disposition), disposition, "Unsupported player disposition.");
            }
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
