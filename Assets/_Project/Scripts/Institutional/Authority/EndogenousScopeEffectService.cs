using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    [Serializable]
    internal sealed class EndogenousScopeApplicationTrace
    {
        internal string TraceId;
        internal string RulingId;
        internal string HoldingRuleId;
        internal string ActorId;
        internal string OpportunityId;
        internal string IssueId;
        internal string JurisdictionId;
        internal long AppliedTick;
        internal bool ScopeMatched;
        internal string AffectedOfficialStatusId;
        internal bool StatusBefore;
        internal bool StatusAfter;
    }

    /// <summary>
    /// Projects committed holdings into official status visible to later agents. The
    /// matcher reads only the committed public-safe ruling and projected action context.
    /// </summary>
    internal static class EndogenousScopeEffectService
    {
        internal const string ProtectedPossessionStatusId =
            "status.holding-protected-possession";
        internal const string ProtectedIdentityStatusId =
            "status.holding-protected-identity-continuity";
        internal const string ProtectedDependencyStatusId =
            "status.holding-protected-dependency-support";

        internal static List<EndogenousScopeApplicationTrace> Apply(
            SocietyState society,
            EndogenousDocketState state,
            SimulationInput input,
            string jurisdictionId = "branch-42")
        {
            return ApplyCore(
                society, state, input, jurisdictionId, validateBoundary: true);
        }

        internal static List<EndogenousScopeApplicationTrace>
            ApplyWithinValidatedTransaction(
                SocietyState society,
                EndogenousDocketState state,
                SimulationInput input,
                string jurisdictionId = "branch-42")
        {
            return ApplyCore(
                society, state, input, jurisdictionId, validateBoundary: false);
        }

        private static List<EndogenousScopeApplicationTrace> ApplyCore(
            SocietyState society,
            EndogenousDocketState state,
            SimulationInput input,
            string jurisdictionId,
            bool validateBoundary)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (validateBoundary)
                EndogenousDocketValidator.Validate(state, society);
            var traces = new List<EndogenousScopeApplicationTrace>();
            if (input.StealOpportunities == null) return traces;
            var traceById = new Dictionary<string, EndogenousScopeApplicationTrace>(
                state.ScopeApplicationTraces.Count,
                StringComparer.Ordinal);
            for (int i = 0; i < state.ScopeApplicationTraces.Count; i++)
            {
                EndogenousScopeApplicationTrace existing =
                    state.ScopeApplicationTraces[i];
                if (existing != null && !string.IsNullOrWhiteSpace(existing.TraceId))
                    traceById[existing.TraceId] = existing;
            }
            var materialRulings = new List<CommittedPlayerRuling>();
            for (int i = 0; i < state.Rulings.Count; i++)
            {
                CommittedPlayerRuling ruling = state.Rulings[i];
                if (IsMaterialHolding(ruling.HoldingRuleId) &&
                    EstablishesHolding(ruling.Disposition))
                    materialRulings.Add(ruling);
            }

            for (int opportunityIndex = 0;
                 opportunityIndex < input.StealOpportunities.Count;
                 opportunityIndex++)
            {
                StealOpportunity opportunity = input.StealOpportunities[opportunityIndex];
                if (opportunity == null) continue;
                string issueId = string.IsNullOrWhiteSpace(opportunity.IssueId)
                    ? EndogenousIssueKindIds.PossessionDispute
                    : opportunity.IssueId;
                string protectionStatusId = ProtectionStatusIdForIssue(issueId);
                opportunity.ProtectionStatusId = protectionStatusId;
                if (opportunity.RecognisedProtectionUtilityBonus <= 0)
                    opportunity.RecognisedProtectionUtilityBonus = 80;
                if (opportunity.UnrecognisedExposureUtilityPenalty <= 0)
                    opportunity.UnrecognisedExposureUtilityPenalty = 20;

                for (int actorIndex = 0;
                     actorIndex < opportunity.EligibleActorIds.Count;
                     actorIndex++)
                {
                    AgentState actor = society.GetAgent(
                        opportunity.EligibleActorIds[actorIndex]);
                    if (actor == null) continue;
                    for (int rulingIndex = 0;
                     rulingIndex < materialRulings.Count;
                         rulingIndex++)
                    {
                        CommittedPlayerRuling ruling =
                            materialRulings[rulingIndex];
                        if (!HoldingMatchesIssue(ruling.HoldingRuleId, issueId))
                            continue;

                        var context = new ScopeMatchContext
                        {
                            AgentId = actor.StableId,
                            IssueId = issueId,
                            JurisdictionId = jurisdictionId,
                            ActivityId = "physical-possession-transfer",
                        };
                        string traceId =
                            $"scope-match:{ruling.RulingId}:{opportunity.OpportunityId}:" +
                            actor.SimulationOrdinal;
                        if (traceById.TryGetValue(
                                traceId,
                                out EndogenousScopeApplicationTrace replay))
                        {
                            if (replay.ScopeMatched)
                            {
                                opportunity.EnablingRulingId = ruling.RulingId;
                                opportunity.ParentCaseId = ruling.CaseId;
                            }
                            traces.Add(replay);
                            continue;
                        }
                        bool before = actor.Standing.IsRecognised(
                            protectionStatusId);
                        bool matched = ScopeExpressionEvaluator.Matches(
                            ruling.Scope, context);
                        if (matched)
                        {
                            actor.Standing.SetRecognised(
                                protectionStatusId, true);
                            opportunity.EnablingRulingId = ruling.RulingId;
                            opportunity.ParentCaseId = ruling.CaseId;
                        }
                        bool after = actor.Standing.IsRecognised(
                            protectionStatusId);
                        var trace = new EndogenousScopeApplicationTrace
                        {
                            TraceId = traceId,
                            RulingId = ruling.RulingId,
                            HoldingRuleId = ruling.HoldingRuleId,
                            ActorId = actor.StableId,
                            OpportunityId = opportunity.OpportunityId,
                            IssueId = issueId,
                            JurisdictionId = jurisdictionId,
                            AppliedTick = society.CurrentTick,
                            ScopeMatched = matched,
                            AffectedOfficialStatusId = protectionStatusId,
                            StatusBefore = before,
                            StatusAfter = after,
                        };
                        state.ScopeApplicationTraces.Add(trace);
                        traceById.Add(trace.TraceId, trace);
                        traces.Add(trace);
                    }
                }
            }
            if (validateBoundary)
            {
                SocietyStateValidator.Validate(society);
                EndogenousDocketValidator.Validate(state, society);
            }
            return traces;
        }

        private static bool EstablishesHolding(RulingDisposition disposition)
        {
            return disposition == RulingDisposition.ProvisionallyRecognised ||
                   disposition == RulingDisposition.Recognised ||
                   disposition == RulingDisposition.Affirmed ||
                   disposition == RulingDisposition.ReversedAndRecognised;
        }

        private static bool IsMaterialHolding(string holdingRuleId)
        {
            return string.Equals(holdingRuleId,
                       EndogenousPlayerRulingService.PossessionHoldingRule,
                       StringComparison.Ordinal) ||
                   string.Equals(holdingRuleId,
                       EndogenousPlayerRulingService.IdentityHoldingRule,
                       StringComparison.Ordinal) ||
                   string.Equals(holdingRuleId,
                       EndogenousPlayerRulingService.DependencyHoldingRule,
                       StringComparison.Ordinal);
        }

        private static bool HoldingMatchesIssue(string holdingRuleId, string issueId)
        {
            return (string.Equals(issueId, EndogenousIssueKindIds.PossessionDispute,
                        StringComparison.Ordinal) && string.Equals(holdingRuleId,
                        EndogenousPlayerRulingService.PossessionHoldingRule,
                        StringComparison.Ordinal)) ||
                   (string.Equals(issueId, EndogenousIssueKindIds.IdentityContinuity,
                        StringComparison.Ordinal) && string.Equals(holdingRuleId,
                        EndogenousPlayerRulingService.IdentityHoldingRule,
                        StringComparison.Ordinal)) ||
                   (string.Equals(issueId,
                        EndogenousIssueKindIds.DependencyEmergencySupport,
                        StringComparison.Ordinal) && string.Equals(holdingRuleId,
                        EndogenousPlayerRulingService.DependencyHoldingRule,
                        StringComparison.Ordinal));
        }

        internal static string ProtectionStatusIdForIssue(string issueId)
        {
            if (string.Equals(issueId, EndogenousIssueKindIds.IdentityContinuity,
                    StringComparison.Ordinal))
                return ProtectedIdentityStatusId;
            if (string.Equals(issueId,
                    EndogenousIssueKindIds.DependencyEmergencySupport,
                    StringComparison.Ordinal))
                return ProtectedDependencyStatusId;
            return ProtectedPossessionStatusId;
        }

    }
}
