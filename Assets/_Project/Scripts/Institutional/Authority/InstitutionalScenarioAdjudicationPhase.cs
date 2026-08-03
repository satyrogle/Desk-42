using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Coordinates deadline adjudication and the exact-once appeal/holding/citation
    /// graph. Evidence scoring and every graph mutation remain service-owned.
    /// </summary>
    internal static class InstitutionalScenarioAdjudicationPhase
    {
        internal static void IssueDueInitialRulings(
            InstitutionalScenarioExecutionContext context,
            long cycle)
        {
            for (int i = 0; i < context.Definition.Cases.Count; i++)
            {
                ScenarioCaseDefinition declaration = context.Definition.Cases[i];
                if (declaration.InitialRulingCycle != cycle ||
                    !InstitutionalScenarioLookup.CaseHasOpened(context, declaration))
                {
                    continue;
                }

                List<string> citedHoldingIds = ResolveCitations(
                    context,
                    declaration,
                    declaration.InitialRulingId);
                InstitutionalAdjudicationResult result =
                    InstitutionalAdjudicationService.IssueInitial(
                        context.Run.Report,
                        CreateRequest(
                            context,
                            declaration,
                            declaration.InitialPhaseId,
                            cycle,
                            declaration.InitialEvidenceCutoffCycle,
                            declaration.InitialScoreThreshold,
                            declaration.ProvisionalRecognitionPermitted &&
                                context.Policy.PermitProvisionalRecognition,
                            declaration.ProvisionalScoreThreshold,
                            citedHoldingIds));
                InstitutionalScenarioLookup.RequireDeclaredRulingId(
                    declaration.InitialRulingId,
                    result.Ruling);
                ApplyCitations(context, declaration, result.Ruling, citedHoldingIds);
            }
        }

        internal static void ResolveDueAppeals(
            InstitutionalScenarioExecutionContext context,
            long cycle)
        {
            List<ScenarioCaseDefinition> dueCases = OrderedDueAppealCases(context, cycle);
            for (int i = 0; i < dueCases.Count; i++)
                ResolveAppealForCase(context, dueCases[i], cycle);
        }

        internal static void ExecuteDueStatusEffects(
            InstitutionalScenarioExecutionContext context,
            long cycle)
        {
            for (int i = 0;
                 i < context.Definition.OfficialStatusEffectRequests.Count;
                 i++)
            {
                ScenarioOfficialStatusEffectRequest declaration =
                    context.Definition.OfficialStatusEffectRequests[i];
                if (declaration.Cycle != cycle ||
                    InstitutionalScenarioLookup.Ruling(
                        context.Run.Report,
                        declaration.CauseRulingId) == null)
                {
                    continue;
                }
                ScenarioOfficialStatusEffectExecutionResult result =
                    InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                        context.Run,
                        declaration,
                        context.AgentIdByRole);
                context.StatusEffectsByDeclarationId.Add(
                    declaration.EffectRequestId,
                    result);
            }
        }

        private static List<ScenarioCaseDefinition> OrderedDueAppealCases(
            InstitutionalScenarioExecutionContext context,
            long cycle)
        {
            var pending = new List<ScenarioCaseDefinition>();
            for (int i = 0; i < context.Definition.Cases.Count; i++)
            {
                ScenarioCaseDefinition candidate = context.Definition.Cases[i];
                if (candidate.AdjudicationCycle == cycle &&
                    InstitutionalScenarioLookup.CaseHasOpened(context, candidate))
                {
                    pending.Add(candidate);
                }
            }

            var ordered = new List<ScenarioCaseDefinition>(pending.Count);
            var completedCaseIds = new HashSet<string>(StringComparer.Ordinal);
            while (pending.Count > 0)
            {
                int selectedIndex = -1;
                for (int i = 0; i < pending.Count; i++)
                {
                    if (!DependsOnPendingHoldingSource(
                            context,
                            pending[i],
                            pending,
                            completedCaseIds))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                if (selectedIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Adjudications at cycle {cycle} contain a holding dependency cycle.");
                }
                ScenarioCaseDefinition selected = pending[selectedIndex];
                pending.RemoveAt(selectedIndex);
                ordered.Add(selected);
                completedCaseIds.Add(selected.CaseId);
            }
            return ordered;
        }

        private static bool DependsOnPendingHoldingSource(
            InstitutionalScenarioExecutionContext context,
            ScenarioCaseDefinition target,
            IReadOnlyList<ScenarioCaseDefinition> pending,
            HashSet<string> completedCaseIds)
        {
            if (!context.Policy.AutoCiteMatchingHoldings) return false;
            for (int i = 0; i < context.Definition.HoldingCitations.Count; i++)
            {
                ScenarioHoldingCitationDefinition citation =
                    context.Definition.HoldingCitations[i];
                if (!InstitutionalScenarioLookup.Equal(
                        citation.TargetCaseId,
                        target.CaseId) ||
                    !InstitutionalScenarioLookup.Equal(
                        citation.TargetRulingId,
                        target.AdjudicationRulingId))
                {
                    continue;
                }
                ScenarioHoldingDefinition holding = InstitutionalScenarioLookup.Holding(
                    context.Definition,
                    citation.HoldingId);
                ScenarioAppealDefinition sourceAppeal = InstitutionalScenarioLookup.Appeal(
                    context.Definition,
                    holding.SourceAppealId);
                if (InstitutionalScenarioLookup.Equal(
                        sourceAppeal.CaseId,
                        target.CaseId) ||
                    completedCaseIds.Contains(sourceAppeal.CaseId))
                {
                    continue;
                }
                for (int j = 0; j < pending.Count; j++)
                {
                    if (InstitutionalScenarioLookup.Equal(
                            pending[j].CaseId,
                            sourceAppeal.CaseId)) return true;
                }
            }
            return false;
        }

        private static void ResolveAppealForCase(
            InstitutionalScenarioExecutionContext context,
            ScenarioCaseDefinition caseDefinition,
            long cycle)
        {
            ScenarioAppealDefinition appealDefinition = null;
            Appeal filedAppeal = null;
            int count = 0;
            for (int i = 0; i < context.Definition.Appeals.Count; i++)
            {
                ScenarioAppealDefinition candidate = context.Definition.Appeals[i];
                if (!InstitutionalScenarioLookup.Equal(
                        candidate.CaseId,
                        caseDefinition.CaseId) ||
                    candidate.HearingCycle != cycle ||
                    !context.ActualAppealsByDeclarationId.TryGetValue(
                        candidate.AppealId,
                        out Appeal actual))
                {
                    continue;
                }
                appealDefinition = candidate;
                filedAppeal = actual;
                count++;
            }
            if (count == 0) return;
            if (count != 1)
            {
                throw new InvalidOperationException(
                    $"Case '{caseDefinition.CaseId}' has {count} filed appeals at one deadline.");
            }

            Ruling challenged = InstitutionalScenarioLookup.Ruling(
                context.Run.Report,
                appealDefinition.ChallengedRulingId);
            if (challenged == null)
            {
                throw new InvalidOperationException(
                    $"Appeal '{appealDefinition.AppealId}' has no challenged ruling.");
            }

            List<string> citedHoldingIds = ResolveCitations(
                context,
                caseDefinition,
                caseDefinition.AdjudicationRulingId);
            InstitutionalAdjudicationResult result =
                InstitutionalAdjudicationService.ResolveAppeal(
                    context.Run.Report,
                    CreateRequest(
                        context,
                        caseDefinition,
                        caseDefinition.AdjudicationPhaseId,
                        cycle,
                        caseDefinition.AdjudicationEvidenceCutoffCycle,
                        caseDefinition.AdjudicationScoreThreshold,
                        false,
                        null,
                        citedHoldingIds),
                    challenged,
                    filedAppeal);
            InstitutionalScenarioLookup.RequireDeclaredRulingId(
                caseDefinition.AdjudicationRulingId,
                result.Ruling);
            if (!InstitutionalScenarioLookup.Equal(
                    appealDefinition.ResultingRulingId,
                    result.Ruling.RulingId))
            {
                throw new InvalidOperationException(
                    $"Appeal '{appealDefinition.AppealId}' produced ruling " +
                    $"'{result.Ruling.RulingId}' instead of its declared result.");
            }
            ApplyCitations(context, caseDefinition, result.Ruling, citedHoldingIds);
            EstablishResultingHolding(context, appealDefinition, filedAppeal, result.Ruling);
        }

        private static InstitutionalAdjudicationRequest CreateRequest(
            InstitutionalScenarioExecutionContext context,
            ScenarioCaseDefinition caseDefinition,
            string phaseId,
            long cycle,
            long maximumEvidenceCycle,
            int requiredScore,
            bool permitProvisional,
            int? provisionalScore,
            List<string> citedHoldingIds)
        {
            return new InstitutionalAdjudicationRequest
            {
                CaseId = caseDefinition.CaseId,
                IssueId = caseDefinition.IssueId,
                PhaseId = phaseId,
                Cycle = cycle,
                MaximumEvidenceCycle = maximumEvidenceCycle,
                PolicyConfiguration = context.Policy,
                RequiredEvidenceScore = requiredScore,
                PermitProvisionalRecognition = permitProvisional,
                ProvisionalEvidenceScore = permitProvisional ? provisionalScore : null,
                CitedHoldingWeight = checked(
                    context.Policy.CitedHoldingWeight * citedHoldingIds.Count),
                CitedHoldingIds = new List<string>(citedHoldingIds),
                CaseFacts = caseDefinition.Facts.Copy(),
                DeferCitationProjection = true,
                AppliedPolicyIds = new List<string>
                {
                    context.Policy.PolicyVersion,
                    context.Policy.PolicyConfigurationId,
                },
            };
        }

        private static List<string> ResolveCitations(
            InstitutionalScenarioExecutionContext context,
            ScenarioCaseDefinition caseDefinition,
            string targetRulingId)
        {
            var result = new List<string>();
            if (!context.Policy.AutoCiteMatchingHoldings) return result;
            ResolveCitationTarget(
                context,
                caseDefinition,
                out string targetAgentId,
                out string targetEmployerId,
                out string targetIdentityConditionId);

            InstitutionalServiceResult<List<Holding>> matches =
                InstitutionalAppealPrecedentService.FindMatchingHoldings(
                    context.Run.Report,
                    caseDefinition.IssueId,
                    targetAgentId,
                    targetEmployerId,
                    targetIdentityConditionId,
                    caseDefinition.Facts);
            InstitutionalScenarioLookup.RequireAccepted(matches, "precedent matching");
            if (matches.Value == null) return result;

            for (int i = 0; i < context.Definition.HoldingCitations.Count; i++)
            {
                ScenarioHoldingCitationDefinition declaration =
                    context.Definition.HoldingCitations[i];
                if (!InstitutionalScenarioLookup.Equal(
                        declaration.TargetCaseId,
                        caseDefinition.CaseId) ||
                    !InstitutionalScenarioLookup.Equal(
                        declaration.TargetRulingId,
                        targetRulingId))
                {
                    continue;
                }
                string declaredId = declaration.HoldingId;
                for (int j = 0; j < matches.Value.Count; j++)
                {
                    if (InstitutionalScenarioLookup.Equal(
                            matches.Value[j].HoldingId,
                            declaredId))
                    {
                        result.Add(declaredId);
                        break;
                    }
                }
            }
            return result;
        }

        private static void ApplyCitations(
            InstitutionalScenarioExecutionContext context,
            ScenarioCaseDefinition caseDefinition,
            Ruling ruling,
            IReadOnlyList<string> citedHoldingIds)
        {
            ResolveCitationTarget(
                context,
                caseDefinition,
                out string targetAgentId,
                out string targetEmployerId,
                out string targetIdentityConditionId);
            for (int i = 0; i < citedHoldingIds.Count; i++)
            {
                InstitutionalServiceResult<Holding> result =
                    InstitutionalAppealPrecedentService.ApplyHolding(
                        context.Run.Report,
                        citedHoldingIds[i],
                        ruling.RulingId,
                        caseDefinition.CaseId,
                        caseDefinition.IssueId,
                        targetAgentId,
                        targetEmployerId,
                        targetIdentityConditionId,
                        caseDefinition.Facts);
                InstitutionalScenarioLookup.RequireAccepted(
                    result,
                    "precedent application");
            }
        }

        private static void ResolveCitationTarget(
            InstitutionalScenarioExecutionContext context,
            ScenarioCaseDefinition caseDefinition,
            out string targetAgentId,
            out string targetEmployerId,
            out string targetIdentityConditionId)
        {
            if (!context.AgentIdByRole.TryGetValue(
                    caseDefinition.ClaimantRoleId,
                    out targetAgentId) ||
                string.IsNullOrWhiteSpace(targetAgentId))
            {
                throw new InvalidOperationException(
                    $"Case '{caseDefinition.CaseId}' has no bound precedent target.");
            }

            AgentState target = context.Run.FinalSocietyState.GetAgent(targetAgentId);
            if (target == null)
            {
                throw new InvalidOperationException(
                    $"Case '{caseDefinition.CaseId}' precedent target is absent from society.");
            }
            targetEmployerId = target.EmployerId;
            targetIdentityConditionId = null;

            DescendantCase projectedCase = null;
            int projectedCount = 0;
            for (int i = 0; i < context.Run.Report.DescendantCases.Count; i++)
            {
                DescendantCase candidate = context.Run.Report.DescendantCases[i];
                if (candidate == null || !InstitutionalScenarioLookup.Equal(
                        candidate.CaseId,
                        caseDefinition.CaseId)) continue;
                projectedCase = candidate;
                projectedCount++;
            }
            if (projectedCount > 1)
            {
                throw new InvalidOperationException(
                    $"Case '{caseDefinition.CaseId}' has ambiguous precedent target context.");
            }
            if (projectedCase == null) return;
            if (!string.IsNullOrWhiteSpace(projectedCase.ClaimantAgentId) &&
                !InstitutionalScenarioLookup.Equal(
                    projectedCase.ClaimantAgentId,
                    targetAgentId))
            {
                throw new InvalidOperationException(
                    $"Case '{caseDefinition.CaseId}' claimant conflicts with its role binding.");
            }
            if (!string.IsNullOrWhiteSpace(projectedCase.OfficialEmployerId))
                targetEmployerId = projectedCase.OfficialEmployerId;
            targetIdentityConditionId = projectedCase.OfficialIdentityConditionId;
        }

        private static void EstablishResultingHolding(
            InstitutionalScenarioExecutionContext context,
            ScenarioAppealDefinition appealDefinition,
            Appeal filedAppeal,
            Ruling resultingRuling)
        {
            if (!context.Policy.EstablishAppellateHolding ||
                resultingRuling.Disposition != RulingDisposition.ReversedAndRecognised ||
                string.IsNullOrWhiteSpace(appealDefinition.ResultingHoldingId))
            {
                return;
            }

            ScenarioHoldingDefinition declaration = InstitutionalScenarioLookup.Holding(
                context.Definition,
                appealDefinition.ResultingHoldingId);
            string claimantAgentId = context.AgentIdByRole[
                InstitutionalScenarioLookup.Case(
                    context.Definition,
                    appealDefinition.CaseId).ClaimantRoleId];
            AgentState claimant = context.Run.FinalSocietyState.GetAgent(claimantAgentId);
            var scope = new PrecedentScope
            {
                ScopeId = declaration.ScopeId,
                Reach = context.Policy.HoldingReach,
                BoundAgentId = context.Policy.HoldingReach == PrecedentReach.Individual
                    ? claimantAgentId
                    : null,
                BoundEmployerId = context.Policy.HoldingReach == PrecedentReach.Employer
                    ? claimant?.EmployerId
                    : null,
                RequiredFacts = declaration.RequiredScopeFacts.Copy(),
                Retrospective = declaration.Retrospective &&
                    context.Policy.HoldingIsRetrospective,
            };
            if (!InstitutionalScenarioLookup.TryResolveEvidenceArtifactIds(
                    context.Run.Report,
                    declaration.SupportingEvidenceTemplateIds,
                    appealDefinition.CaseId,
                    resultingRuling.Cycle,
                    $"Holding '{declaration.HoldingId}' support",
                    out List<string> supportingEvidenceArtifactIds))
            {
                return;
            }
            InstitutionalServiceResult<Holding> result =
                InstitutionalAppealPrecedentService.EstablishHolding(
                    context.Run.Report,
                    filedAppeal.AppealId,
                    declaration.HoldingId,
                    declaration.RuleId,
                    declaration.IssueId,
                    scope,
                    supportingEvidenceArtifactIds);
            InstitutionalScenarioLookup.RequireAccepted(
                result,
                "holding establishment");
        }
    }
}
