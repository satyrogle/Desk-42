using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal enum EndogenousAppellateProcedure
    {
        FullRehearing,
        FastTrack,
        Settlement,
    }

    internal sealed class EndogenousAppellateResolution
    {
        internal EndogenousAppealRecord Appeal;
        internal CommittedPlayerRuling Ruling;
        internal EndogenousHoldingRecord Holding;
    }

    /// <summary>
    /// Authority-owned appellate path for the endogenous docket. It records filing,
    /// commits a real appellate ruling and optionally installs a matching holding.
    /// </summary>
    internal static class EndogenousAppellateService
    {
        internal static EndogenousAppealRecord File(
            SocietyState society,
            EndogenousDocketState state,
            string appealId,
            string appellateCaseId,
            string challengedRulingId,
            IReadOnlyList<string> groundsEvidenceIds)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(appealId))
                throw new ArgumentException("An appeal id is required.", nameof(appealId));
            EndogenousDocketValidator.Validate(state, society);
            EndogenousAppealRecord replay = state.GetAppeal(appealId);
            if (replay != null) return replay;

            EndogenousInstitutionalCase opened = state.GetCase(appellateCaseId) ??
                throw new InvalidOperationException("The appellate case does not exist.");
            CommittedPlayerRuling challenged = FindRuling(state, challengedRulingId) ??
                throw new InvalidOperationException("The challenged ruling does not exist.");
            if (!string.Equals(
                    opened.ParentCaseId,
                    challenged.CaseId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    opened.OriginatingRulingId,
                    challenged.RulingId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The appellate case is not a traceable descendant of the challenged ruling.");
            }

            var appeal = new EndogenousAppealRecord
            {
                AppealId = appealId,
                CaseId = opened.CaseId,
                ChallengedRulingId = challenged.RulingId,
                FiledTick = society.CurrentTick,
                ProcedureId = "procedure.pending-selection",
                GroundsEvidenceIds = Copy(groundsEvidenceIds),
            };
            state.Appeals.Add(appeal);
            try
            {
                EndogenousDocketValidator.Validate(state, society);
            }
            catch
            {
                state.Appeals.RemoveAt(state.Appeals.Count - 1);
                throw;
            }
            return appeal;
        }

        internal static EndogenousAppellateResolution Resolve(
            SocietyState society,
            EndogenousDocketState state,
            string appealId,
            EndogenousAppellateProcedure procedure,
            RulingDisposition outcome,
            bool establishHolding)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!Enum.IsDefined(typeof(EndogenousAppellateProcedure), procedure))
                throw new ArgumentOutOfRangeException(nameof(procedure));
            if (outcome != RulingDisposition.Affirmed &&
                outcome != RulingDisposition.ReversedAndDenied &&
                outcome != RulingDisposition.ReversedAndRecognised)
            {
                throw new InvalidOperationException(
                    "An appellate resolution requires an appellate disposition.");
            }
            EndogenousDocketValidator.Validate(state, society);
            EndogenousAppealRecord appeal = state.GetAppeal(appealId) ??
                throw new InvalidOperationException("The appeal does not exist.");
            if (appeal.Resolved)
            {
                return new EndogenousAppellateResolution
                {
                    Appeal = appeal,
                    Ruling = FindRuling(state, appeal.ResultingRulingId),
                    Holding = state.GetHolding(appeal.ResultingHoldingId),
                };
            }

            EndogenousInstitutionalCase opened = state.GetCase(appeal.CaseId) ??
                throw new InvalidOperationException("The appellate case no longer exists.");
            CommittedPlayerRuling challenged = FindRuling(
                state, appeal.ChallengedRulingId) ??
                throw new InvalidOperationException("The challenged ruling no longer exists.");
            string commandId = "appellate-command:" + appeal.AppealId + ":" + procedure;
            var ruling = new CommittedPlayerRuling
            {
                RulingId = "ruling:" + commandId,
                PlayerCommandId = commandId,
                CaseId = opened.CaseId,
                CaseVersion = opened.CaseVersion,
                CommittedTick = society.CurrentTick,
                EvidenceEnvelopeHash = opened.EvidenceEnvelopeHash,
                RecognisedFactIds = Copy(opened.AvailableFactIds),
                CitedEvidenceArtifactIds = Copy(appeal.GroundsEvidenceIds),
                Disposition = outcome,
                HoldingRuleId = challenged.HoldingRuleId,
                Scope = ScopeExpressionEvaluator.Copy(challenged.Scope),
                TemporalReach = TemporalReach.Prospective,
                RemedyDefinitionIds = new List<string>
                {
                    RemedyFor(opened.IssueId, outcome),
                },
                AppliedProcedureIds = new List<string>
                {
                    ProcedureId(procedure),
                },
                RulesetVersion = EndogenousPlayerRulingService.CurrentRulesetVersion,
            };

            state.Rulings.Add(ruling);
            appeal.ProcedureId = ProcedureId(procedure);
            appeal.Resolved = true;
            appeal.ResolvedTick = society.CurrentTick;
            appeal.ResultingRulingId = ruling.RulingId;

            EndogenousHoldingRecord holding = null;
            if (establishHolding && outcome != RulingDisposition.ReversedAndDenied)
            {
                holding = new EndogenousHoldingRecord
                {
                    HoldingId = "holding:appeal:" + appeal.AppealId,
                    SourceAppealId = appeal.AppealId,
                    SourceRulingId = ruling.RulingId,
                    RuleId = ruling.HoldingRuleId,
                    IssueId = opened.IssueId,
                    EstablishedTick = society.CurrentTick,
                    Scope = ScopeExpressionEvaluator.Copy(ruling.Scope),
                    SupportingEvidenceIds = Copy(appeal.GroundsEvidenceIds),
                };
                state.Holdings.Add(holding);
                appeal.ResultingHoldingId = holding.HoldingId;
            }

            try
            {
                EndogenousDocketValidator.Validate(state, society);
            }
            catch
            {
                if (holding != null)
                    state.Holdings.RemoveAt(state.Holdings.Count - 1);
                state.Rulings.RemoveAt(state.Rulings.Count - 1);
                appeal.ProcedureId = "procedure.pending-selection";
                appeal.Resolved = false;
                appeal.ResolvedTick = -1;
                appeal.ResultingRulingId = null;
                appeal.ResultingHoldingId = null;
                throw;
            }

            return new EndogenousAppellateResolution
            {
                Appeal = appeal,
                Ruling = ruling,
                Holding = holding,
            };
        }

        internal static List<string> ApplyMatchingHoldings(
            SocietyState society,
            EndogenousDocketState state,
            string caseId)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            EndogenousDocketValidator.Validate(state, society);
            EndogenousInstitutionalCase opened = state.GetCase(caseId) ??
                throw new InvalidOperationException("The cited case does not exist.");
            var matches = new List<string>();
            for (int i = 0; i < state.Holdings.Count; i++)
            {
                EndogenousHoldingRecord holding = state.Holdings[i];
                if (!string.Equals(
                        holding.IssueId,
                        opened.IssueId,
                        StringComparison.Ordinal) ||
                    !ScopeMatchesAnyParty(holding.Scope, opened)) continue;
                AddUnique(holding.AppliedCaseIds, opened.CaseId);
                matches.Add(holding.HoldingId);
            }
            matches.Sort(StringComparer.Ordinal);
            EndogenousDocketValidator.Validate(state, society);
            return matches;
        }

        private static bool ScopeMatchesAnyParty(
            ScopeExpression scope,
            EndogenousInstitutionalCase opened)
        {
            if (opened.PartyIds.Count == 0)
                return ScopeExpressionEvaluator.Matches(scope, new ScopeMatchContext
                {
                    IssueId = opened.IssueId,
                    JurisdictionId = "branch-42",
                });
            for (int i = 0; i < opened.PartyIds.Count; i++)
                if (ScopeExpressionEvaluator.Matches(scope, new ScopeMatchContext
                    {
                        AgentId = opened.PartyIds[i],
                        IssueId = opened.IssueId,
                        JurisdictionId = "branch-42",
                    })) return true;
            return false;
        }

        private static string RemedyFor(string issueId, RulingDisposition outcome)
        {
            if (outcome == RulingDisposition.ReversedAndDenied)
                return EndogenousPlayerRulingService.NoChangeRemedy;
            if (string.Equals(issueId, EndogenousIssueKindIds.PossessionDispute,
                    StringComparison.Ordinal))
                return EndogenousPlayerRulingService.RestorePossessionRemedy;
            if (string.Equals(issueId, EndogenousIssueKindIds.AccessWithdrawal,
                    StringComparison.Ordinal))
                return EndogenousPlayerRulingService.RestoreAccessRemedy;
            return EndogenousPlayerRulingService.RecogniseCollectiveRemedy;
        }

        private static string ProcedureId(EndogenousAppellateProcedure procedure)
        {
            return procedure switch
            {
                EndogenousAppellateProcedure.FullRehearing => "procedure.full-rehearing",
                EndogenousAppellateProcedure.FastTrack => "procedure.fast-track",
                EndogenousAppellateProcedure.Settlement => "procedure.settlement",
                _ => throw new ArgumentOutOfRangeException(nameof(procedure)),
            };
        }

        private static CommittedPlayerRuling FindRuling(
            EndogenousDocketState state,
            string rulingId)
        {
            if (string.IsNullOrWhiteSpace(rulingId)) return null;
            for (int i = 0; i < state.Rulings.Count; i++)
                if (string.Equals(
                        state.Rulings[i].RulingId,
                        rulingId,
                        StringComparison.Ordinal)) return state.Rulings[i];
            return null;
        }

        private static List<string> Copy(IReadOnlyList<string> source)
        {
            var result = new List<string>(source?.Count ?? 0);
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static void AddUnique(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return;
            values.Add(value);
            values.Sort(StringComparer.Ordinal);
        }
    }
}
