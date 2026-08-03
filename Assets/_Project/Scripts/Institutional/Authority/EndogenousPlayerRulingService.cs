using System;
using System.Collections.Generic;
using System.Text;

namespace Desk42.Institutional
{
    internal static class EndogenousPlayerRulingService
    {
        internal const string CurrentRulesetVersion = "endogenous-player-ruling-v1";
        internal const string PossessionHoldingRule =
            "holding.possession-requires-authorised-transfer";
        internal const string AccessHoldingRule =
            "holding.adverse-access-action-requires-protection";
        internal const string CollectiveHoldingRule =
            "holding.collective-action-protected";
        internal const string NoChangeRemedy = "remedy.no-change";
        internal const string RestorePossessionRemedy = "remedy.restore-possession";
        internal const string RestoreAccessRemedy = "remedy.restore-access";
        internal const string RecogniseCollectiveRemedy = "remedy.recognise-collective";

        internal static CommittedPlayerRuling Commit(
            SocietyState society,
            EndogenousDocketState state,
            PlayerRulingCommand command)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (command == null) throw new ArgumentNullException(nameof(command));
            EndogenousDocketValidator.Validate(state, society);

            CommittedPlayerRuling replay = FindByCommandId(state, command.CommandId);
            if (replay != null)
            {
                if (!PayloadMatches(replay, command))
                    throw new InvalidOperationException(
                        $"Player command {command.CommandId} already committed another payload.");
                return replay;
            }

            EndogenousInstitutionalCase opened = state.GetCase(command.CaseId);
            ValidateCommand(society, state, opened, command);
            var committed = new CommittedPlayerRuling
            {
                RulingId = $"ruling:{command.CommandId}",
                PlayerCommandId = command.CommandId,
                CaseId = command.CaseId,
                CaseVersion = command.ExpectedCaseVersion,
                CommittedTick = society.CurrentTick,
                EvidenceEnvelopeHash = command.EvidenceEnvelopeHash,
                RecognisedFactIds = SortedCopy(command.RecognisedFactIds),
                CitedEvidenceArtifactIds = SortedCopy(
                    command.CitedEvidenceArtifactIds),
                Disposition = command.Disposition,
                HoldingRuleId = command.HoldingRuleId,
                Scope = ScopeExpressionEvaluator.Copy(command.Scope),
                TemporalReach = command.TemporalReach,
                RemedyDefinitionIds = SortedCopy(command.RemedyDefinitionIds),
                AppliedProcedureIds = SortedCopy(command.AppliedProcedureIds),
                RulesetVersion = CurrentRulesetVersion,
            };
            state.Rulings.Add(committed);
            try
            {
                EndogenousDocketValidator.Validate(state, society);
            }
            catch
            {
                state.Rulings.RemoveAt(state.Rulings.Count - 1);
                throw;
            }
            return committed;
        }

        private static void ValidateCommand(
            SocietyState society,
            EndogenousDocketState state,
            EndogenousInstitutionalCase opened,
            PlayerRulingCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.CommandId) ||
                string.IsNullOrWhiteSpace(command.CaseId) || opened == null ||
                command.ExpectedCaseVersion != opened.CaseVersion ||
                !string.Equals(
                    command.EvidenceEnvelopeHash,
                    opened.EvidenceEnvelopeHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Player ruling command has a stale or missing case evidence envelope.");
            }
            if (command.RecognisedFactIds == null ||
                command.RecognisedFactIds.Count == 0 ||
                command.CitedEvidenceArtifactIds == null ||
                command.CitedEvidenceArtifactIds.Count == 0 ||
                command.RemedyDefinitionIds == null ||
                command.RemedyDefinitionIds.Count == 0 ||
                command.AppliedProcedureIds == null)
            {
                throw new InvalidOperationException(
                    "Player ruling command requires facts, cited evidence and a remedy.");
            }
            RequireUniqueSubset(
                command.RecognisedFactIds,
                opened.AvailableFactIds,
                "recognised fact");
            RequireUniqueSubset(
                command.CitedEvidenceArtifactIds,
                opened.ObservationIds,
                "cited evidence");
            ValidateDisposition(command.Disposition);
            ValidateHolding(opened.IssueId, command.HoldingRuleId);
            ValidateRemedies(
                opened.IssueId,
                command.Disposition,
                command.RemedyDefinitionIds);
            ValidateProcedures(command.AppliedProcedureIds);
            if (command.TemporalReach != TemporalReach.Prospective)
                throw new InvalidOperationException(
                    "Retrospective player rulings are rejected until causal replay is implemented.");
            ScopeExpressionEvaluator.Validate(command.Scope);
            RejectHiddenScopeVocabulary(command.Scope);
            if (!MatchesCurrentOfficialCase(opened, command.Scope))
                throw new InvalidOperationException(
                    "The holding scope does not apply to the official case being decided.");
        }

        private static void ValidateProcedures(IReadOnlyList<string> procedureIds)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < procedureIds.Count; i++)
            {
                string procedureId = procedureIds[i];
                if (!IsSupportedProcedure(procedureId) || !seen.Add(procedureId))
                    throw new InvalidOperationException(
                        "A ruling references an unsupported or duplicate procedure.");
            }
        }

        private static bool IsSupportedProcedure(string procedureId)
        {
            return string.Equals(procedureId, "procedure.secondary-verification",
                       StringComparison.Ordinal) ||
                   string.Equals(procedureId, "procedure.presumption-validity",
                       StringComparison.Ordinal) ||
                   string.Equals(procedureId, "procedure.automatic-adverse-review",
                       StringComparison.Ordinal) ||
                   string.Equals(procedureId, "procedure.protected-evidence-channel",
                       StringComparison.Ordinal) ||
                   string.Equals(procedureId, "procedure.appeal-fast-track",
                       StringComparison.Ordinal) ||
                   string.Equals(procedureId, "procedure.precedent-reuse",
                       StringComparison.Ordinal) ||
                   string.Equals(procedureId, "procedure.full-rehearing",
                       StringComparison.Ordinal) ||
                   string.Equals(procedureId, "procedure.fast-track",
                       StringComparison.Ordinal) ||
                   string.Equals(procedureId, "procedure.settlement",
                       StringComparison.Ordinal);
        }

        private static void ValidateDisposition(RulingDisposition disposition)
        {
            if (disposition != RulingDisposition.Denied &&
                disposition != RulingDisposition.ProvisionallyRecognised &&
                disposition != RulingDisposition.Recognised)
            {
                throw new InvalidOperationException(
                    $"Disposition {disposition} is not valid for an initial player ruling.");
            }
        }

        private static void ValidateHolding(string issueId, string holdingRuleId)
        {
            string expected;
            switch (issueId)
            {
                case EndogenousIssueKindIds.PossessionDispute:
                    expected = PossessionHoldingRule;
                    break;
                case EndogenousIssueKindIds.AccessWithdrawal:
                    expected = AccessHoldingRule;
                    break;
                case EndogenousIssueKindIds.CollectiveGrievance:
                    expected = CollectiveHoldingRule;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Issue {issueId} has no supported holding grammar.");
            }
            if (!string.Equals(expected, holdingRuleId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Holding {holdingRuleId} is unsupported for issue {issueId}.");
        }

        private static void ValidateRemedies(
            string issueId,
            RulingDisposition disposition,
            IReadOnlyList<string> remedies)
        {
            string expected = NoChangeRemedy;
            if (disposition != RulingDisposition.Denied)
            {
                switch (issueId)
                {
                    case EndogenousIssueKindIds.PossessionDispute:
                        expected = RestorePossessionRemedy;
                        break;
                    case EndogenousIssueKindIds.AccessWithdrawal:
                        expected = RestoreAccessRemedy;
                        break;
                    case EndogenousIssueKindIds.CollectiveGrievance:
                        expected = RecogniseCollectiveRemedy;
                        break;
                }
            }
            if (remedies.Count != 1 ||
                !string.Equals(remedies[0], expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Disposition {disposition} requires remedy {expected}.");
            }
        }

        private static bool MatchesCurrentOfficialCase(
            EndogenousInstitutionalCase opened,
            ScopeExpression scope)
        {
            if (opened.PartyIds.Count == 0)
            {
                return ScopeExpressionEvaluator.Matches(scope, new ScopeMatchContext
                {
                    IssueId = opened.IssueId,
                    JurisdictionId = "branch-42",
                });
            }
            for (int i = 0; i < opened.PartyIds.Count; i++)
            {
                if (ScopeExpressionEvaluator.Matches(scope, new ScopeMatchContext
                    {
                        AgentId = opened.PartyIds[i],
                        IssueId = opened.IssueId,
                        JurisdictionId = "branch-42",
                    }))
                {
                    return true;
                }
            }
            return false;
        }

        private static void RejectHiddenScopeVocabulary(ScopeExpression scope)
        {
            if (scope.Kind == ScopeExpressionKind.Predicate)
            {
                RejectHidden(scope.Key);
                RejectHidden(scope.Value);
            }
            for (int i = 0; i < scope.Children.Count; i++)
                RejectHiddenScopeVocabulary(scope.Children[i]);
        }

        private static void RejectHidden(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (value.StartsWith("lived.", StringComparison.Ordinal) ||
                value.StartsWith("authority.", StringComparison.Ordinal) ||
                value.StartsWith("incident.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Scope expressions may reference only official public-safe state.");
            }
        }

        private static void RequireUniqueSubset(
            IReadOnlyList<string> values,
            IReadOnlyList<string> allowed,
            string description)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i]) ||
                    !allowedSet.Contains(values[i]) || !seen.Add(values[i]))
                {
                    throw new InvalidOperationException(
                        $"Player ruling contains an unavailable or duplicate {description}.");
                }
            }
        }

        private static CommittedPlayerRuling FindByCommandId(
            EndogenousDocketState state,
            string commandId)
        {
            if (string.IsNullOrEmpty(commandId)) return null;
            for (int i = 0; i < state.Rulings.Count; i++)
            {
                if (string.Equals(
                        state.Rulings[i].PlayerCommandId,
                        commandId,
                        StringComparison.Ordinal))
                {
                    return state.Rulings[i];
                }
            }
            return null;
        }

        private static bool PayloadMatches(
            CommittedPlayerRuling committed,
            PlayerRulingCommand command)
        {
            return string.Equals(committed.CaseId, command.CaseId, StringComparison.Ordinal) &&
                   committed.CaseVersion == command.ExpectedCaseVersion &&
                   string.Equals(
                       committed.EvidenceEnvelopeHash,
                       command.EvidenceEnvelopeHash,
                       StringComparison.Ordinal) &&
                   committed.Disposition == command.Disposition &&
                   string.Equals(
                       committed.HoldingRuleId,
                       command.HoldingRuleId,
                       StringComparison.Ordinal) &&
                   committed.TemporalReach == command.TemporalReach &&
                   SequenceEqual(
                       committed.RecognisedFactIds,
                       SortedCopy(command.RecognisedFactIds)) &&
                   SequenceEqual(
                       committed.CitedEvidenceArtifactIds,
                       SortedCopy(command.CitedEvidenceArtifactIds)) &&
                   SequenceEqual(
                       committed.RemedyDefinitionIds,
                       SortedCopy(command.RemedyDefinitionIds)) &&
                   SequenceEqual(
                       committed.AppliedProcedureIds,
                       SortedCopy(command.AppliedProcedureIds)) &&
                   string.Equals(
                       CanonicalScope(committed.Scope),
                       CanonicalScope(command.Scope),
                       StringComparison.Ordinal);
        }

        private static string CanonicalScope(ScopeExpression scope)
        {
            if (scope == null) return "<null>";
            var result = new StringBuilder();
            AppendScope(result, scope);
            return result.ToString();
        }

        private static void AppendScope(StringBuilder result, ScopeExpression scope)
        {
            result.Append((int)scope.Kind).Append(':')
                .Append((int)scope.PredicateKind).Append(':')
                .Append(scope.Key).Append(':').Append(scope.Value).Append('[');
            if (scope.Children != null)
                for (int i = 0; i < scope.Children.Count; i++)
                    AppendScope(result, scope.Children[i]);
            result.Append(']');
        }

        private static List<string> SortedCopy(IReadOnlyList<string> source)
        {
            if (source == null) return null;
            var result = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static bool SequenceEqual(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
            return true;
        }
    }
}
