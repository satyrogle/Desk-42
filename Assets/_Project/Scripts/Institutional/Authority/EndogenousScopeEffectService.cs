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

        internal static List<EndogenousScopeApplicationTrace> Apply(
            SocietyState society,
            EndogenousDocketState state,
            SimulationInput input,
            string jurisdictionId = "branch-42")
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (input == null) throw new ArgumentNullException(nameof(input));
            EndogenousDocketValidator.Validate(state, society);
            var traces = new List<EndogenousScopeApplicationTrace>();
            if (input.StealOpportunities == null) return traces;

            for (int opportunityIndex = 0;
                 opportunityIndex < input.StealOpportunities.Count;
                 opportunityIndex++)
            {
                StealOpportunity opportunity = input.StealOpportunities[opportunityIndex];
                if (opportunity == null) continue;
                opportunity.ProtectionStatusId = ProtectedPossessionStatusId;
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
                    for (int rulingIndex = 0; rulingIndex < state.Rulings.Count; rulingIndex++)
                    {
                        CommittedPlayerRuling ruling = state.Rulings[rulingIndex];
                        if (!string.Equals(
                                ruling.HoldingRuleId,
                                EndogenousPlayerRulingService.PossessionHoldingRule,
                                StringComparison.Ordinal) ||
                            ruling.Disposition == RulingDisposition.Denied)
                        {
                            continue;
                        }

                        var context = new ScopeMatchContext
                        {
                            AgentId = actor.StableId,
                            IssueId = EndogenousIssueKindIds.PossessionDispute,
                            JurisdictionId = jurisdictionId,
                            ActivityId = "physical-possession-transfer",
                        };
                        bool before = actor.Standing.IsRecognised(
                            ProtectedPossessionStatusId);
                        bool matched = ScopeExpressionEvaluator.Matches(
                            ruling.Scope, context);
                        if (matched)
                        {
                            actor.Standing.SetRecognised(
                                ProtectedPossessionStatusId, true);
                            opportunity.EnablingRulingId = ruling.RulingId;
                            opportunity.ParentCaseId = ruling.CaseId;
                        }
                        bool after = actor.Standing.IsRecognised(
                            ProtectedPossessionStatusId);
                        traces.Add(new EndogenousScopeApplicationTrace
                        {
                            TraceId =
                                $"scope-match:{ruling.RulingId}:{opportunity.OpportunityId}:" +
                                actor.SimulationOrdinal,
                            RulingId = ruling.RulingId,
                            HoldingRuleId = ruling.HoldingRuleId,
                            ActorId = actor.StableId,
                            OpportunityId = opportunity.OpportunityId,
                            IssueId = EndogenousIssueKindIds.PossessionDispute,
                            JurisdictionId = jurisdictionId,
                            ScopeMatched = matched,
                            AffectedOfficialStatusId = ProtectedPossessionStatusId,
                            StatusBefore = before,
                            StatusAfter = after,
                        });
                    }
                }
            }
            SocietyStateValidator.Validate(society);
            return traces;
        }
    }
}
