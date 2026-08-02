using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Opens a declared related case only when a matching autonomous action is
    /// present in both the assessor trace and the public observation record. The
    /// service owns the causal envelope and projection; it does not adjudicate.
    /// </summary>
    internal static class InstitutionalActionCausedDescendantCaseService
    {
        internal static InstitutionalServiceResult<DescendantCase> Open(
            InstitutionalConsequenceRun run,
            ScenarioActionCausedDescendantCaseDefinition definition,
            ScenarioCaseDefinition referencedCase,
            IReadOnlyDictionary<string, string> roleAgentIds,
            long currentCycle)
        {
            if (run == null || run.Report == null)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.missing-run");
            }
            if (definition == null)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.missing-definition");
            }
            if (referencedCase == null)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.missing-case-definition");
            }
            if (roleAgentIds == null)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.missing-role-bindings");
            }
            if (!HasRequiredCollections(run))
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.invalid-run-collections");
            }
            if (!DeclarationIsValid(definition, referencedCase))
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.invalid-declaration");
            }

            if (!TryResolveRole(roleAgentIds, definition.TriggerRoleId,
                    out string triggerAgentId) ||
                !TryResolveRole(roleAgentIds, referencedCase.ClaimantRoleId,
                    out string claimantAgentId) ||
                !TryResolveRole(roleAgentIds, referencedCase.RespondentRoleId,
                    out string respondentAgentId) ||
                !TryResolveConnectedAgents(
                    roleAgentIds,
                    definition.ConnectedRoleIds,
                    out List<string> connectedAgentIds))
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.invalid-role-binding");
            }

            CaseFactSet detachedFacts;
            try
            {
                detachedFacts = referencedCase.Facts.Copy();
            }
            catch (Exception exception) when (
                exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.invalid-case-facts");
            }

            DescendantCase existing = FindUniqueCase(
                run.Report,
                definition.CaseId,
                out int existingCount);
            if (existingCount > 1)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.ambiguous-existing-case");
            }
            if (currentCycle < definition.OpenCycle)
            {
                return existing == null
                    ? InstitutionalServiceResult<DescendantCase>.NoChange(
                        "descendant.not-due", null)
                    : InstitutionalServiceResult<DescendantCase>.Rejected(
                        "descendant.opened-before-declared-cycle");
            }
            if (currentCycle > definition.OpenCycle && existing == null)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.declared-cycle-missed");
            }

            SourceMatch sourceMatch = FindSource(
                run,
                definition,
                triggerAgentId);
            if (sourceMatch.Outcome == SourceMatchOutcome.Ambiguous)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.ambiguous-trigger");
            }
            if (sourceMatch.Outcome == SourceMatchOutcome.Malformed)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.invalid-trigger-projection");
            }
            if (sourceMatch.Outcome == SourceMatchOutcome.None)
            {
                return existing == null
                    ? InstitutionalServiceResult<DescendantCase>.NoChange(
                        "descendant.trigger-not-observed", null)
                    : InstitutionalServiceResult<DescendantCase>.Rejected(
                        "descendant.existing-case-has-no-trigger");
            }

            // Optional descendants are materialised by an observed autonomous
            // trigger. Absent triggers are therefore a clean no-op even when their
            // conditional parent ruling never existed. Once a trigger is present,
            // however, its declared ancestry must be exact.
            Ruling originatingRuling = FindUniqueRuling(
                run.Report,
                definition.OriginatingRulingId,
                out int rulingCount);
            if (rulingCount != 1 || originatingRuling == null)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    rulingCount > 1
                        ? "descendant.ambiguous-originating-ruling"
                        : "descendant.missing-originating-ruling");
            }
            if (!OrdinalEquals(originatingRuling.CaseId, definition.ParentCaseId) ||
                originatingRuling.Cycle > definition.OpenCycle)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.invalid-originating-ruling");
            }

            ObservedAgentAction sourceAction = sourceMatch.Action;
            var expected = new DescendantCase
            {
                CaseId = definition.CaseId,
                ParentCaseId = definition.ParentCaseId,
                OpenedCycle = definition.OpenCycle,
                Kind = DescendantCaseKind.RelatedClaim,
                Status = DescendantCaseStatus.Open,
                ParentCauseId = sourceAction.ActionEventId,
                OriginatingEventId = sourceAction.ActionEventId,
                OriginatingRulingId = definition.OriginatingRulingId,
                CausalAgentActionId = sourceAction.ActionEventId,
                ClaimantAgentId = claimantAgentId,
                RespondentId = respondentAgentId,
                OfficialIssueId = referencedCase.IssueId,
                Facts = detachedFacts,
                ConnectedAgentIds = connectedAgentIds,
                SourceActionEventIds = new List<string> { sourceAction.ActionEventId },
            };

            int resultLinkCount = CountResultLinks(
                run.Report,
                sourceAction,
                expected.CaseId,
                out int sourceResultLinkCount);
            int timelineCount = CountOpeningTimeline(run.Report, expected);
            int anyOpeningTimelineCount = CountCaseOpeningTimeline(
                run.Report,
                expected.CaseId);
            if (existing != null)
            {
                if (!EquivalentCausalEnvelope(existing, expected) ||
                    resultLinkCount != 1 ||
                    sourceResultLinkCount != 1 ||
                    timelineCount != 1 ||
                    anyOpeningTimelineCount != 1)
                {
                    return InstitutionalServiceResult<DescendantCase>.Rejected(
                        "descendant.existing-projection-conflict");
                }
                return InstitutionalServiceResult<DescendantCase>.NoChange(
                    "descendant.already-open", existing);
            }

            if (resultLinkCount != 0 ||
                sourceResultLinkCount != 0 ||
                timelineCount != 0 ||
                anyOpeningTimelineCount != 0)
            {
                return InstitutionalServiceResult<DescendantCase>.Rejected(
                    "descendant.orphaned-projection");
            }

            run.Report.DescendantCases.Add(expected);
            sourceAction.ResultDescendantCaseIds.Add(expected.CaseId);
            InstitutionalTimeline.Add(
                run.Report,
                definition.OpenCycle,
                InstitutionalTimelineKind.DescendantCaseOpened,
                sourceAction.ActionEventId,
                expected.CaseId,
                expected.Kind.ToString());
            return InstitutionalServiceResult<DescendantCase>.Applied(expected);
        }

        /// <summary>
        /// Identifies the one action event that is allowed to seed evidence for an
        /// action-caused case before that case materialises. The match deliberately
        /// uses the private decision trace and the public action projection so a
        /// coincidentally similar event cannot pre-populate a conditional docket.
        /// </summary>
        internal static bool IsExactDeclaredTriggerEvent(
            InstitutionalConsequenceRun run,
            ScenarioActionCausedDescendantCaseDefinition definition,
            IReadOnlyDictionary<string, string> roleAgentIds,
            SocietyEvent societyEvent)
        {
            if (run == null || run.Report == null || definition == null ||
                roleAgentIds == null || societyEvent == null)
            {
                return false;
            }
            if (run.AssessorActionTraces == null ||
                run.Report.ObservedAgentActions == null)
            {
                throw new InvalidOperationException(
                    "Descendant trigger matching requires action projections.");
            }
            if (!TryResolveRole(
                    roleAgentIds,
                    definition.TriggerRoleId,
                    out string triggerAgentId) ||
                societyEvent.Tick != definition.TriggerCycle ||
                !OrdinalEquals(societyEvent.ActorId, triggerAgentId) ||
                !OrdinalEquals(
                    societyEvent.OpportunityId,
                    definition.TriggerOpportunityId) ||
                !OrdinalEquals(
                    societyEvent.EvidencePropositionId,
                    definition.TriggerPropositionId) ||
                InstitutionalActionProjector.ActivityFor(societyEvent.Kind) !=
                    ActivityFor(definition.TriggerActionKind))
            {
                return false;
            }

            return HasExactTriggerProjection(
                run,
                definition,
                triggerAgentId,
                societyEvent.EventId,
                societyEvent.CauseDecisionId);
        }

        internal static bool IsExactDeclaredTriggerEvidence(
            InstitutionalConsequenceRun run,
            ScenarioActionCausedDescendantCaseDefinition definition,
            IReadOnlyDictionary<string, string> roleAgentIds,
            EvidenceArtifact artifact)
        {
            if (run == null || run.Report == null || definition == null ||
                roleAgentIds == null || artifact?.Provenance == null)
            {
                return false;
            }
            if (!TryResolveRole(
                    roleAgentIds,
                    definition.TriggerRoleId,
                    out string triggerAgentId) ||
                artifact.EnteredCycle != definition.TriggerCycle ||
                !OrdinalEquals(artifact.Provenance.SourceAgentId, triggerAgentId) ||
                !OrdinalEquals(artifact.PropositionId, definition.TriggerPropositionId))
            {
                return false;
            }
            return HasExactTriggerProjection(
                run,
                definition,
                triggerAgentId,
                artifact.Provenance.SourceSocietyEventId,
                artifact.Provenance.SourceDecisionId);
        }

        private static bool HasExactTriggerProjection(
            InstitutionalConsequenceRun run,
            ScenarioActionCausedDescendantCaseDefinition definition,
            string triggerAgentId,
            string sourceEventId,
            string sourceDecisionId)
        {
            int traceMatches = 0;
            for (int i = 0; i < run.AssessorActionTraces.Count; i++)
            {
                AgentActionTrace trace = run.AssessorActionTraces[i];
                if (trace == null ||
                    trace.Cycle != definition.TriggerCycle ||
                    trace.Action != definition.TriggerActionKind ||
                    !OrdinalEquals(trace.ActorId, triggerAgentId) ||
                    !OrdinalEquals(trace.OpportunityId, definition.TriggerOpportunityId) ||
                    !OrdinalEquals(trace.DecisionId, sourceDecisionId) ||
                    CountOrdinal(trace.ResultEventIds, sourceEventId) != 1)
                {
                    continue;
                }
                traceMatches++;
            }
            if (traceMatches > 1)
            {
                throw new InvalidOperationException(
                    $"Event '{sourceEventId}' has ambiguous descendant trigger traces.");
            }
            if (traceMatches == 0) return false;

            ObservedAgentAction matchingAction = null;
            int actionMatches = 0;
            for (int i = 0; i < run.Report.ObservedAgentActions.Count; i++)
            {
                ObservedAgentAction action = run.Report.ObservedAgentActions[i];
                if (action == null ||
                    !OrdinalEquals(action.ActionEventId, sourceEventId))
                {
                    continue;
                }
                matchingAction = action;
                actionMatches++;
            }
            if (actionMatches > 1)
            {
                throw new InvalidOperationException(
                    $"Event '{sourceEventId}' has ambiguous public action projections.");
            }
            return matchingAction != null &&
                   matchingAction.Cycle == definition.TriggerCycle &&
                   OrdinalEquals(matchingAction.ActorId, triggerAgentId) &&
                   matchingAction.Activity == ActivityFor(definition.TriggerActionKind);
        }

        private static bool HasRequiredCollections(InstitutionalConsequenceRun run)
        {
            return run.AssessorActionTraces != null &&
                   run.Report.ObservedAgentActions != null &&
                   run.Report.EvidenceArtifacts != null &&
                   run.Report.Rulings != null &&
                   run.Report.DescendantCases != null &&
                   run.Report.Timeline != null;
        }

        private static bool DeclarationIsValid(
            ScenarioActionCausedDescendantCaseDefinition definition,
            ScenarioCaseDefinition referencedCase)
        {
            return !IsBlank(definition.DescendantDefinitionId) &&
                   !IsBlank(definition.CaseId) &&
                   !IsBlank(definition.ParentCaseId) &&
                   !OrdinalEquals(definition.CaseId, definition.ParentCaseId) &&
                   definition.OpenCycle >= 0 &&
                   definition.TriggerCycle >= 0 &&
                   definition.TriggerCycle < definition.OpenCycle &&
                   !IsBlank(definition.TriggerRoleId) &&
                   Enum.IsDefined(
                       typeof(SocietyActionKind),
                       definition.TriggerActionKind) &&
                   definition.TriggerActionKind != SocietyActionKind.Idle &&
                   OpportunityDeclarationIsValid(definition) &&
                   (definition.TriggerActionKind != SocietyActionKind.Disclose ||
                    !IsBlank(definition.TriggerPropositionId)) &&
                   !IsBlank(definition.OriginatingRulingId) &&
                   definition.ConnectedRoleIds != null &&
                   OrdinalEquals(referencedCase.CaseId, definition.CaseId) &&
                   referencedCase.OpenCycle == definition.OpenCycle &&
                   !IsBlank(referencedCase.IssueId) &&
                   !IsBlank(referencedCase.ClaimantRoleId) &&
                   !IsBlank(referencedCase.RespondentRoleId) &&
                   referencedCase.Facts != null;
        }

        private static bool OpportunityDeclarationIsValid(
            ScenarioActionCausedDescendantCaseDefinition definition)
        {
            switch (definition.TriggerActionKind)
            {
                case SocietyActionKind.Work:
                case SocietyActionKind.SeekAid:
                case SocietyActionKind.Help:
                case SocietyActionKind.Appeal:
                    return !IsBlank(definition.TriggerOpportunityId);
                default:
                    return true;
            }
        }

        private static SourceMatch FindSource(
            InstitutionalConsequenceRun run,
            ScenarioActionCausedDescendantCaseDefinition definition,
            string triggerAgentId)
        {
            SourceMatch found = null;
            int matches = 0;
            bool malformedMatchingTrace = false;
            for (int i = 0; i < run.AssessorActionTraces.Count; i++)
            {
                AgentActionTrace trace = run.AssessorActionTraces[i];
                if (trace == null ||
                    trace.Cycle != definition.TriggerCycle ||
                    !OrdinalEquals(trace.ActorId, triggerAgentId) ||
                    trace.Action != definition.TriggerActionKind ||
                    !OrdinalEquals(trace.OpportunityId, definition.TriggerOpportunityId))
                {
                    continue;
                }

                ObservedAgentAction observed = FindObservedResult(
                    run.Report,
                    trace,
                    out int observedCount,
                    out bool malformed);
                if (malformed)
                {
                    if (TraceMatchesProposition(
                            run.Report,
                            trace,
                            observed,
                            definition.TriggerPropositionId))
                    {
                        malformedMatchingTrace = true;
                    }
                    continue;
                }
                if (observedCount != 1 || observed == null)
                {
                    if (TraceMatchesProposition(
                            run.Report,
                            trace,
                            null,
                            definition.TriggerPropositionId))
                    {
                        malformedMatchingTrace = true;
                    }
                    continue;
                }
                if (!TraceMatchesProposition(
                        run.Report,
                        trace,
                        observed,
                        definition.TriggerPropositionId))
                {
                    continue;
                }
                if (observed.Cycle != trace.Cycle ||
                    !OrdinalEquals(observed.ActorId, trace.ActorId) ||
                    observed.Activity != ActivityFor(trace.Action))
                {
                    malformedMatchingTrace = true;
                    continue;
                }

                found = new SourceMatch
                {
                    Outcome = SourceMatchOutcome.Match,
                    Action = observed,
                };
                matches++;
            }

            if (matches > 1)
                return new SourceMatch { Outcome = SourceMatchOutcome.Ambiguous };
            if (matches == 1 && malformedMatchingTrace)
                return new SourceMatch { Outcome = SourceMatchOutcome.Ambiguous };
            if (matches == 1) return found;
            return new SourceMatch
            {
                Outcome = malformedMatchingTrace
                    ? SourceMatchOutcome.Malformed
                    : SourceMatchOutcome.None,
            };
        }

        private static ObservedAgentAction FindObservedResult(
            InstitutionalConsequenceReport report,
            AgentActionTrace trace,
            out int count,
            out bool malformed)
        {
            ObservedAgentAction found = null;
            count = 0;
            malformed = trace.ResultEventIds == null;
            if (trace.ResultEventIds == null) return null;

            for (int eventIndex = 0; eventIndex < trace.ResultEventIds.Count; eventIndex++)
            {
                string eventId = trace.ResultEventIds[eventIndex];
                if (IsBlank(eventId) || CountOrdinal(trace.ResultEventIds, eventId) != 1)
                {
                    malformed = true;
                    continue;
                }
                for (int actionIndex = 0;
                     actionIndex < report.ObservedAgentActions.Count;
                     actionIndex++)
                {
                    ObservedAgentAction action = report.ObservedAgentActions[actionIndex];
                    if (action == null || !OrdinalEquals(action.ActionEventId, eventId))
                        continue;
                    found = action;
                    count++;
                }
            }

            if (count > 1) malformed = true;
            return found;
        }

        private static bool TraceMatchesProposition(
            InstitutionalConsequenceReport report,
            AgentActionTrace trace,
            ObservedAgentAction observed,
            string propositionId)
        {
            if (IsBlank(propositionId)) return true;
            if (!IsBlank(trace.SubjectBeliefId) &&
                trace.PerceptionSnapshot?.Beliefs != null)
            {
                BeliefState belief = trace.PerceptionSnapshot.GetBelief(trace.SubjectBeliefId);
                if (belief != null && OrdinalEquals(belief.PropositionId, propositionId))
                    return true;
            }
            if (observed == null) return false;

            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                EvidenceArtifact artifact = report.EvidenceArtifacts[i];
                if (artifact?.Provenance != null &&
                    OrdinalEquals(
                        artifact.Provenance.SourceSocietyEventId,
                        observed.ActionEventId) &&
                    OrdinalEquals(artifact.PropositionId, propositionId))
                {
                    return true;
                }
            }
            return false;
        }

        private static ObservedActivityKind ActivityFor(SocietyActionKind action)
        {
            switch (action)
            {
                case SocietyActionKind.Work:
                    return ObservedActivityKind.WorkPerformed;
                case SocietyActionKind.SeekAid:
                    return ObservedActivityKind.AidRequested;
                case SocietyActionKind.Help:
                    return ObservedActivityKind.AssistanceGiven;
                case SocietyActionKind.Disclose:
                    return ObservedActivityKind.EvidenceSubmitted;
                case SocietyActionKind.Appeal:
                    return ObservedActivityKind.AppealFiled;
                default:
                    return ObservedActivityKind.NoVisibleAction;
            }
        }

        private static bool TryResolveRole(
            IReadOnlyDictionary<string, string> roleAgentIds,
            string roleId,
            out string agentId)
        {
            agentId = null;
            return !IsBlank(roleId) &&
                   roleAgentIds.TryGetValue(roleId, out agentId) &&
                   !IsBlank(agentId);
        }

        private static bool TryResolveConnectedAgents(
            IReadOnlyDictionary<string, string> roleAgentIds,
            IReadOnlyList<string> connectedRoleIds,
            out List<string> connectedAgentIds)
        {
            connectedAgentIds = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (connectedRoleIds == null) return false;
            for (int i = 0; i < connectedRoleIds.Count; i++)
            {
                if (!TryResolveRole(roleAgentIds, connectedRoleIds[i], out string agentId))
                    return false;
                if (seen.Add(agentId)) connectedAgentIds.Add(agentId);
            }
            return true;
        }

        private static DescendantCase FindUniqueCase(
            InstitutionalConsequenceReport report,
            string caseId,
            out int count)
        {
            DescendantCase found = null;
            count = 0;
            for (int i = 0; i < report.DescendantCases.Count; i++)
            {
                DescendantCase candidate = report.DescendantCases[i];
                if (candidate == null || !OrdinalEquals(candidate.CaseId, caseId)) continue;
                found = candidate;
                count++;
            }
            return found;
        }

        private static Ruling FindUniqueRuling(
            InstitutionalConsequenceReport report,
            string rulingId,
            out int count)
        {
            Ruling found = null;
            count = 0;
            for (int i = 0; i < report.Rulings.Count; i++)
            {
                Ruling candidate = report.Rulings[i];
                if (candidate == null || !OrdinalEquals(candidate.RulingId, rulingId)) continue;
                found = candidate;
                count++;
            }
            return found;
        }

        private static int CountOpeningTimeline(
            InstitutionalConsequenceReport report,
            DescendantCase expected)
        {
            int count = 0;
            for (int i = 0; i < report.Timeline.Count; i++)
            {
                InstitutionalTimelineEntry entry = report.Timeline[i];
                if (entry != null &&
                    entry.Cycle == expected.OpenedCycle &&
                    entry.Kind == InstitutionalTimelineKind.DescendantCaseOpened &&
                    OrdinalEquals(entry.CauseId, expected.CausalAgentActionId) &&
                    OrdinalEquals(entry.SubjectId, expected.CaseId) &&
                    OrdinalEquals(entry.DetailId, expected.Kind.ToString()))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountCaseOpeningTimeline(
            InstitutionalConsequenceReport report,
            string caseId)
        {
            int count = 0;
            for (int i = 0; i < report.Timeline.Count; i++)
            {
                InstitutionalTimelineEntry entry = report.Timeline[i];
                if (entry != null &&
                    entry.Kind == InstitutionalTimelineKind.DescendantCaseOpened &&
                    OrdinalEquals(entry.SubjectId, caseId))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountResultLinks(
            InstitutionalConsequenceReport report,
            ObservedAgentAction sourceAction,
            string caseId,
            out int sourceCount)
        {
            int total = 0;
            sourceCount = -1;
            for (int i = 0; i < report.ObservedAgentActions.Count; i++)
            {
                ObservedAgentAction action = report.ObservedAgentActions[i];
                if (action == null || action.ResultDescendantCaseIds == null) continue;
                int count = CountOrdinal(action.ResultDescendantCaseIds, caseId);
                total += count;
                if (ReferenceEquals(action, sourceAction)) sourceCount = count;
            }
            return total;
        }

        private static bool EquivalentCausalEnvelope(
            DescendantCase existing,
            DescendantCase expected)
        {
            return OrdinalEquals(existing.CaseId, expected.CaseId) &&
                   OrdinalEquals(existing.ParentCaseId, expected.ParentCaseId) &&
                   existing.OpenedCycle == expected.OpenedCycle &&
                   existing.Kind == expected.Kind &&
                   OrdinalEquals(existing.ParentCauseId, expected.ParentCauseId) &&
                   OrdinalEquals(existing.OriginatingEventId, expected.OriginatingEventId) &&
                   OrdinalEquals(existing.OriginatingRulingId, expected.OriginatingRulingId) &&
                   OrdinalEquals(existing.CausalAgentActionId, expected.CausalAgentActionId) &&
                   OrdinalEquals(existing.ClaimantAgentId, expected.ClaimantAgentId) &&
                   OrdinalEquals(existing.RespondentId, expected.RespondentId) &&
                   OrdinalEquals(existing.OfficialIssueId, expected.OfficialIssueId) &&
                   FactsEqual(existing.Facts, expected.Facts) &&
                   ListsEqual(existing.ConnectedAgentIds, expected.ConnectedAgentIds) &&
                   ListsEqual(existing.SourceActionEventIds, expected.SourceActionEventIds);
        }

        private static bool FactsEqual(CaseFactSet left, CaseFactSet right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            try
            {
                left.Validate();
                right.Validate();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            for (int i = 0; i < left.Facts.Count; i++)
            {
                if (!left.Facts[i].Equals(right.Facts[i])) return false;
            }
            return true;
        }

        private static bool ListsEqual(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (!OrdinalEquals(left[i], right[i])) return false;
            }
            return true;
        }

        private static int CountOrdinal(IReadOnlyList<string> values, string expected)
        {
            if (values == null) return -1;
            int count = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (OrdinalEquals(values[i], expected)) count++;
            }
            return count;
        }

        private static bool IsBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        private static bool OrdinalEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private enum SourceMatchOutcome
        {
            None,
            Match,
            Ambiguous,
            Malformed,
        }

        private sealed class SourceMatch
        {
            internal SourceMatchOutcome Outcome;
            internal ObservedAgentAction Action;
        }
    }
}
