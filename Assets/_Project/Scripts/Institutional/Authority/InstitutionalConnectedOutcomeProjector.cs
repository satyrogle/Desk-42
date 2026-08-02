using System;

namespace Desk42.Institutional
{
    /// <summary>
    /// Owns the player-safe paired projection of one conserved transfer. Scenario
    /// orchestration supplies opaque causal identifiers; this projector resolves
    /// display data, enforces exact-once publication, and owns the report mutation.
    /// </summary>
    internal static class InstitutionalConnectedOutcomeProjector
    {
        internal static InstitutionalServiceResult<ConnectedOutcomePair> Project(
            InstitutionalConsequenceRun run,
            string pairId,
            string causeRuleId,
            string connectionId,
            string winnerAgentId,
            string loserAgentId,
            int conservedAmount)
        {
            if (run?.Report?.ConnectedOutcomes == null || run.FinalSocietyState == null)
            {
                return InstitutionalServiceResult<ConnectedOutcomePair>.Rejected(
                    "connected-outcome.missing-run");
            }
            if (!ValidId(pairId) || !ValidId(causeRuleId) || !ValidId(connectionId) ||
                !ValidId(winnerAgentId) || !ValidId(loserAgentId) ||
                string.Equals(winnerAgentId, loserAgentId, StringComparison.Ordinal) ||
                conservedAmount <= 0)
            {
                return InstitutionalServiceResult<ConnectedOutcomePair>.Rejected(
                    "connected-outcome.invalid-request");
            }

            AgentState winner = run.FinalSocietyState.GetAgent(winnerAgentId);
            AgentState loser = run.FinalSocietyState.GetAgent(loserAgentId);
            if (winner == null || loser == null)
            {
                return InstitutionalServiceResult<ConnectedOutcomePair>.Rejected(
                    "connected-outcome.missing-participant");
            }

            var expected = new ConnectedOutcomePair
            {
                PairId = pairId,
                CauseRuleId = causeRuleId,
                ConnectionId = connectionId,
                WinnerAgentId = winner.StableId,
                WinnerDisplayName = winner.DisplayName,
                WinnerResourceDelta = conservedAmount,
                LoserAgentId = loser.StableId,
                LoserDisplayName = loser.DisplayName,
                LoserResourceDelta = -conservedAmount,
            };

            ConnectedOutcomePair existing = null;
            int matches = 0;
            for (int i = 0; i < run.Report.ConnectedOutcomes.Count; i++)
            {
                ConnectedOutcomePair candidate = run.Report.ConnectedOutcomes[i];
                if (candidate == null || !string.Equals(
                        candidate.PairId,
                        pairId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                existing = candidate;
                matches++;
            }
            if (matches > 1)
            {
                return InstitutionalServiceResult<ConnectedOutcomePair>.Rejected(
                    "connected-outcome.ambiguous-existing-pair");
            }
            if (existing != null)
            {
                return Equivalent(existing, expected)
                    ? InstitutionalServiceResult<ConnectedOutcomePair>.NoChange(
                        "connected-outcome.already-projected",
                        existing)
                    : InstitutionalServiceResult<ConnectedOutcomePair>.Rejected(
                        "connected-outcome.conflicting-existing-pair");
            }

            run.Report.ConnectedOutcomes.Add(expected);
            return InstitutionalServiceResult<ConnectedOutcomePair>.Applied(expected);
        }

        private static bool Equivalent(
            ConnectedOutcomePair left,
            ConnectedOutcomePair right)
        {
            return string.Equals(left.PairId, right.PairId, StringComparison.Ordinal) &&
                   string.Equals(left.CauseRuleId, right.CauseRuleId, StringComparison.Ordinal) &&
                   string.Equals(left.ConnectionId, right.ConnectionId, StringComparison.Ordinal) &&
                   string.Equals(left.WinnerAgentId, right.WinnerAgentId, StringComparison.Ordinal) &&
                   string.Equals(
                       left.WinnerDisplayName,
                       right.WinnerDisplayName,
                       StringComparison.Ordinal) &&
                   left.WinnerResourceDelta == right.WinnerResourceDelta &&
                   string.Equals(left.LoserAgentId, right.LoserAgentId, StringComparison.Ordinal) &&
                   string.Equals(
                       left.LoserDisplayName,
                       right.LoserDisplayName,
                       StringComparison.Ordinal) &&
                   left.LoserResourceDelta == right.LoserResourceDelta;
        }

        private static bool ValidId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > ExclusiveEntitlementService.MaximumIdentifierLength)
            {
                return false;
            }
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i])) return false;
            }
            return true;
        }
    }
}
