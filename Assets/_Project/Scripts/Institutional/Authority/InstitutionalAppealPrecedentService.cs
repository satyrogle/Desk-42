using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal enum InstitutionalServiceOutcome
    {
        Applied,
        NoChange,
        Rejected,
    }

    /// <summary>
    /// Explicit mutation result used by the appeal and precedent boundary. Rejections
    /// are data, rather than a partially-applied mutation followed by an exception.
    /// </summary>
    internal sealed class InstitutionalServiceResult<T> where T : class
    {
        internal InstitutionalServiceOutcome Outcome { get; }
        internal string ReasonId { get; }
        internal T Value { get; }

        private InstitutionalServiceResult(
            InstitutionalServiceOutcome outcome,
            string reasonId,
            T value)
        {
            Outcome = outcome;
            ReasonId = reasonId;
            Value = value;
        }

        internal static InstitutionalServiceResult<T> Applied(T value)
        {
            return new InstitutionalServiceResult<T>(
                InstitutionalServiceOutcome.Applied,
                "applied",
                value);
        }

        internal static InstitutionalServiceResult<T> NoChange(string reasonId, T value)
        {
            return new InstitutionalServiceResult<T>(
                InstitutionalServiceOutcome.NoChange,
                reasonId,
                value);
        }

        internal static InstitutionalServiceResult<T> Rejected(string reasonId)
        {
            return new InstitutionalServiceResult<T>(
                InstitutionalServiceOutcome.Rejected,
                reasonId,
                null);
        }
    }

    /// <summary>
    /// Scenario-neutral appeal, holding, and citation transitions. Scenario content
    /// supplies opaque identifiers, opportunities, rulings, and case facts; this
    /// service owns reference checks, chronology, idempotence, and public projection.
    /// </summary>
    internal static class InstitutionalAppealPrecedentService
    {
        internal static InstitutionalServiceResult<Appeal> FileAppeal(
            InstitutionalConsequenceRun run,
            SocietyEvent filingEvent,
            IReadOnlyList<AppealOpportunity> declaredOpportunities)
        {
            return FileAppeal(
                run,
                filingEvent,
                declaredOpportunities,
                declaredGroundsEvidenceArtifactIds: null);
        }

        internal static InstitutionalServiceResult<Appeal> FileAppeal(
            InstitutionalConsequenceRun run,
            SocietyEvent filingEvent,
            IReadOnlyList<AppealOpportunity> declaredOpportunities,
            IReadOnlyList<string> declaredGroundsEvidenceArtifactIds)
        {
            if (run == null || run.Report == null)
                return InstitutionalServiceResult<Appeal>.Rejected("appeal.missing-run");
            if (filingEvent == null)
                return InstitutionalServiceResult<Appeal>.Rejected("appeal.missing-filing-event");
            if (declaredOpportunities == null)
                return InstitutionalServiceResult<Appeal>.Rejected("appeal.missing-opportunities");
            if (filingEvent.Kind != SocietyEventKind.AppealFiled ||
                IsBlank(filingEvent.EventId) ||
                IsBlank(filingEvent.CauseDecisionId) ||
                IsBlank(filingEvent.ActorId) ||
                IsBlank(filingEvent.OpportunityId))
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.invalid-filing-event");
            }

            ObservedAgentAction observed = FindUniqueObservedAction(
                run.Report,
                filingEvent.EventId,
                out int observedCount);
            if (observedCount != 1 || observed.Activity != ObservedActivityKind.AppealFiled ||
                observed.Cycle != filingEvent.Tick ||
                !OrdinalEquals(observed.ActorId, filingEvent.ActorId))
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.filing-not-observed");
            }

            AgentActionTrace trace = FindAutonomousAppealTrace(
                run,
                filingEvent,
                out int traceCount);
            if (traceCount != 1 || trace == null)
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.filing-not-autonomous");
            }

            AppealOpportunity opportunity = FindUniqueOpportunity(
                declaredOpportunities,
                filingEvent.OpportunityId,
                out int opportunityCount);
            if (opportunityCount != 1 || opportunity == null ||
                IsBlank(opportunity.CaseId) ||
                IsBlank(opportunity.ChallengedRulingId) ||
                IsBlank(opportunity.OpportunityId))
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.opportunity-not-declared");
            }
            if (CountOrdinal(opportunity.PartyAgentIds, filingEvent.ActorId) != 1)
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.actor-not-party");
            }

            Ruling challenged = FindUniqueRuling(
                run.Report,
                opportunity.ChallengedRulingId,
                out int challengedCount);
            if (challengedCount != 1 || challenged == null ||
                !OrdinalEquals(challenged.CaseId, opportunity.CaseId))
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.challenged-ruling-not-found");
            }
            if (challenged.Cycle >= filingEvent.Tick ||
                opportunity.HearingCycle < filingEvent.Tick)
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.invalid-filing-chronology");
            }

            Appeal existing = FindAppealByFilingEvent(
                run.Report,
                filingEvent.EventId,
                out int existingCount);
            if (existingCount > 1)
                return InstitutionalServiceResult<Appeal>.Rejected("appeal.duplicate-filing");
            if (existingCount == 1)
            {
                return AppealMatchesFiling(
                    run.Report,
                    existing,
                    filingEvent,
                    opportunity,
                    challenged)
                    ? InstitutionalServiceResult<Appeal>.NoChange(
                        "appeal.already-filed",
                        existing)
                    : InstitutionalServiceResult<Appeal>.Rejected(
                        "appeal.conflicting-existing-filing");
            }

            string appealId = $"appeal:{filingEvent.EventId}";
            if (FindUniqueAppeal(run.Report, appealId, out int idCount) != null || idCount != 0)
                return InstitutionalServiceResult<Appeal>.Rejected("appeal.id-collision");

            List<EvidenceArtifact> grounds;
            try
            {
                grounds = declaredGroundsEvidenceArtifactIds == null
                    ? InstitutionalEvidencePipeline.ForCase(
                        run.Report,
                        opportunity.CaseId,
                        filingEvent.Tick)
                    : SelectDeclaredEvidence(
                        run.Report,
                        opportunity.CaseId,
                        filingEvent.Tick,
                        declaredGroundsEvidenceArtifactIds);
            }
            catch (Exception)
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.invalid-evidence-envelope");
            }
            if (!EvidenceEnvelopeIsValid(
                    run.Report,
                    opportunity.CaseId,
                    filingEvent.Tick,
                    InstitutionalEvidencePipeline.CopyIds(grounds)))
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.invalid-evidence-envelope");
            }

            var appeal = new Appeal
            {
                AppealId = appealId,
                CaseId = opportunity.CaseId,
                FiledCycle = filingEvent.Tick,
                HearingCycle = opportunity.HearingCycle,
                AppellantAgentId = filingEvent.ActorId,
                FilingActionEventId = filingEvent.EventId,
                ChallengedRulingId = challenged.RulingId,
                Disposition = AppealDisposition.Pending,
                GroundsEvidenceArtifactIds = InstitutionalEvidencePipeline.CopyIds(grounds),
            };
            run.Report.Appeals.Add(appeal);
            InstitutionalTimeline.Add(
                run.Report,
                filingEvent.Tick,
                InstitutionalTimelineKind.AppealFiled,
                filingEvent.EventId,
                filingEvent.ActorId,
                appeal.AppealId);
            return InstitutionalServiceResult<Appeal>.Applied(appeal);
        }

        internal static InstitutionalServiceResult<Appeal> ResolveAppeal(
            InstitutionalConsequenceReport report,
            string appealId,
            Ruling suppliedResultingRuling)
        {
            if (report == null)
                return InstitutionalServiceResult<Appeal>.Rejected("appeal.missing-report");
            if (IsBlank(appealId) || suppliedResultingRuling == null ||
                IsBlank(suppliedResultingRuling.RulingId))
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.missing-resolution-reference");
            }

            Appeal appeal = FindUniqueAppeal(report, appealId, out int appealCount);
            if (appealCount != 1 || appeal == null)
                return InstitutionalServiceResult<Appeal>.Rejected("appeal.not-found");

            Ruling resulting = FindUniqueRuling(
                report,
                suppliedResultingRuling.RulingId,
                out int resultCount);
            if (resultCount != 1 || resulting == null)
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.resulting-ruling-not-found");
            }
            Ruling challenged = FindUniqueRuling(
                report,
                appeal.ChallengedRulingId,
                out int challengedCount);
            if (challengedCount != 1 || challenged == null)
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.challenged-ruling-not-found");
            }
            if (!OrdinalEquals(resulting.CaseId, appeal.CaseId) ||
                resulting.Cycle < appeal.HearingCycle ||
                resulting.Cycle <= appeal.FiledCycle ||
                resulting.Cycle <= challenged.Cycle ||
                OrdinalEquals(resulting.RulingId, challenged.RulingId))
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.invalid-resolution-chronology");
            }
            if (!EvidenceEnvelopeIsValid(
                    report,
                    resulting.CaseId,
                    resulting.Cycle,
                    resulting.EvidenceArtifactIds))
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.invalid-resulting-ruling-evidence");
            }

            AppealDisposition disposition;
            switch (resulting.Disposition)
            {
                case RulingDisposition.Affirmed:
                    disposition = AppealDisposition.Affirmed;
                    break;
                case RulingDisposition.ReversedAndDenied:
                case RulingDisposition.ReversedAndRecognised:
                    disposition = AppealDisposition.Reversed;
                    break;
                default:
                    return InstitutionalServiceResult<Appeal>.Rejected(
                        "appeal.non-appellate-ruling-disposition");
            }

            int hearingTimelineCount = CountTimeline(
                report,
                InstitutionalTimelineKind.AppealHeard,
                appeal.AppealId,
                appeal.CaseId,
                resulting.RulingId);
            if (appeal.Disposition != AppealDisposition.Pending ||
                !IsBlank(appeal.ResultingRulingId))
            {
                if (appeal.Disposition == disposition &&
                    OrdinalEquals(appeal.ResultingRulingId, resulting.RulingId) &&
                    hearingTimelineCount == 1)
                {
                    return InstitutionalServiceResult<Appeal>.NoChange(
                        "appeal.already-resolved",
                        appeal);
                }
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.conflicting-resolution");
            }
            if (hearingTimelineCount != 0)
            {
                return InstitutionalServiceResult<Appeal>.Rejected(
                    "appeal.conflicting-hearing-record");
            }

            appeal.Disposition = disposition;
            appeal.ResultingRulingId = resulting.RulingId;
            InstitutionalTimeline.Add(
                report,
                resulting.Cycle,
                InstitutionalTimelineKind.AppealHeard,
                appeal.AppealId,
                appeal.CaseId,
                resulting.RulingId);
            return InstitutionalServiceResult<Appeal>.Applied(appeal);
        }

        internal static InstitutionalServiceResult<Holding> EstablishHolding(
            InstitutionalConsequenceReport report,
            string appealId,
            string holdingId,
            string ruleId,
            string issueId,
            PrecedentScope proposedScope)
        {
            return EstablishHolding(
                report,
                appealId,
                holdingId,
                ruleId,
                issueId,
                proposedScope,
                declaredSupportingEvidenceArtifactIds: null);
        }

        internal static InstitutionalServiceResult<Holding> EstablishHolding(
            InstitutionalConsequenceReport report,
            string appealId,
            string holdingId,
            string ruleId,
            string issueId,
            PrecedentScope proposedScope,
            IReadOnlyList<string> declaredSupportingEvidenceArtifactIds)
        {
            if (report == null)
                return InstitutionalServiceResult<Holding>.Rejected("holding.missing-report");
            if (IsBlank(appealId) || IsBlank(holdingId) || IsBlank(ruleId) ||
                IsBlank(issueId) || !ScopeIsValid(proposedScope, requireFacts: true))
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "holding.invalid-definition");
            }

            Appeal appeal = FindUniqueAppeal(report, appealId, out int appealCount);
            if (appealCount != 1 || appeal == null ||
                appeal.Disposition != AppealDisposition.Reversed ||
                IsBlank(appeal.ResultingRulingId))
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "holding.appeal-not-reversed");
            }
            Ruling ruling = FindUniqueRuling(
                report,
                appeal.ResultingRulingId,
                out int rulingCount);
            if (rulingCount != 1 || ruling == null ||
                !ResolvedAppealSourceIsValid(report, appeal, ruling) ||
                ruling.Disposition != RulingDisposition.ReversedAndRecognised)
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "holding.ruling-not-reversed-and-recognised");
            }

            OfficialFinding finding = FindUniqueFinding(
                report,
                ruling.FindingId,
                out int findingCount);
            if (findingCount != 1 || finding == null ||
                !OrdinalEquals(finding.CaseId, ruling.CaseId) ||
                !OrdinalEquals(finding.IssueId, issueId) ||
                finding.Cycle > ruling.Cycle)
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "holding.issue-not-supported-by-ruling");
            }
            if (!EvidenceEnvelopeIsValid(
                    report,
                    ruling.CaseId,
                    ruling.Cycle,
                    ruling.EvidenceArtifactIds))
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "holding.invalid-supporting-evidence");
            }

            List<EvidenceArtifact> declaredSupportingEvidence = null;
            if (declaredSupportingEvidenceArtifactIds != null)
            {
                try
                {
                    declaredSupportingEvidence = SelectDeclaredEvidence(
                        report,
                        ruling.CaseId,
                        ruling.Cycle,
                        declaredSupportingEvidenceArtifactIds);
                }
                catch (Exception)
                {
                    return InstitutionalServiceResult<Holding>.Rejected(
                        "holding.invalid-declared-supporting-evidence");
                }
                for (int i = 0; i < declaredSupportingEvidence.Count; i++)
                {
                    if (!ruling.EvidenceArtifactIds.Contains(
                        declaredSupportingEvidence[i].ArtifactId))
                    {
                        return InstitutionalServiceResult<Holding>.Rejected(
                            "holding.declared-evidence-not-used-by-ruling");
                    }
                }
            }

            var supportingEvidenceIds = declaredSupportingEvidence == null
                ? new List<string>(ruling.EvidenceArtifactIds)
                : InstitutionalEvidencePipeline.CopyIds(declaredSupportingEvidence);
            supportingEvidenceIds.Sort(StringComparer.Ordinal);
            if (supportingEvidenceIds.Count == 0)
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "holding.invalid-supporting-evidence");
            }

            Holding existing = FindUniqueHolding(report, holdingId, out int existingCount);
            if (existingCount > 1)
                return InstitutionalServiceResult<Holding>.Rejected("holding.duplicate-id");
            if (existingCount == 1)
            {
                if (!HoldingMatchesDefinition(
                        existing,
                        appeal,
                        ruling,
                        ruleId,
                        issueId,
                        proposedScope,
                        supportingEvidenceIds))
                {
                    return InstitutionalServiceResult<Holding>.Rejected(
                        "holding.conflicting-definition");
                }
                int existingTimelineCount = CountTimeline(
                    report,
                    InstitutionalTimelineKind.HoldingEstablished,
                    ruling.RulingId,
                    existing.HoldingId,
                    existing.RuleId);
                return existingTimelineCount == 1
                    ? InstitutionalServiceResult<Holding>.NoChange(
                        "holding.already-established",
                        existing)
                    : InstitutionalServiceResult<Holding>.Rejected(
                        "holding.inconsistent-timeline");
            }

            for (int i = 0; i < report.Holdings.Count; i++)
            {
                Holding other = report.Holdings[i];
                if (other?.Scope != null &&
                    OrdinalEquals(other.Scope.ScopeId, proposedScope.ScopeId))
                {
                    return InstitutionalServiceResult<Holding>.Rejected(
                        "holding.scope-id-collision");
                }
            }

            var holding = new Holding
            {
                HoldingId = holdingId,
                EstablishedCycle = ruling.Cycle,
                SourceAppealId = appeal.AppealId,
                SourceRulingId = ruling.RulingId,
                RuleId = ruleId,
                IssueId = issueId,
                SupportingEvidenceArtifactIds = supportingEvidenceIds,
                Scope = CopyScope(proposedScope),
                AppliedCaseIds = new List<string>(),
            };
            report.Holdings.Add(holding);
            InstitutionalTimeline.Add(
                report,
                ruling.Cycle,
                InstitutionalTimelineKind.HoldingEstablished,
                ruling.RulingId,
                holding.HoldingId,
                holding.RuleId);
            return InstitutionalServiceResult<Holding>.Applied(holding);
        }

        internal static InstitutionalServiceResult<List<Holding>> FindMatchingHoldings(
            InstitutionalConsequenceReport report,
            string issueId,
            CaseFactSet caseFacts)
        {
            // A caller without a target context can only use jurisdiction-wide
            // precedent. Individual and employer reach must never collapse into a
            // facts-only match.
            return FindMatchingHoldings(
                report,
                issueId,
                targetAgentId: null,
                targetEmployerId: null,
                targetIdentityConditionId: null,
                caseFacts: caseFacts);
        }

        internal static InstitutionalServiceResult<List<Holding>> FindMatchingHoldings(
            InstitutionalConsequenceReport report,
            string issueId,
            string targetAgentId,
            string targetEmployerId,
            string targetIdentityConditionId,
            CaseFactSet caseFacts)
        {
            if (report == null || IsBlank(issueId) || caseFacts == null)
            {
                return InstitutionalServiceResult<List<Holding>>.Rejected(
                    "precedent.invalid-match-input");
            }
            try
            {
                caseFacts.Validate();
            }
            catch (InvalidOperationException)
            {
                return InstitutionalServiceResult<List<Holding>>.Rejected(
                    "precedent.invalid-case-facts");
            }

            var seenHoldingIds = new HashSet<string>(StringComparer.Ordinal);
            var seenScopeIds = new HashSet<string>(StringComparer.Ordinal);
            var matches = new List<Holding>();
            for (int i = 0; i < report.Holdings.Count; i++)
            {
                Holding holding = report.Holdings[i];
                if (holding == null || IsBlank(holding.HoldingId) ||
                    !seenHoldingIds.Add(holding.HoldingId) ||
                    holding.Scope == null || IsBlank(holding.Scope.ScopeId) ||
                    !seenScopeIds.Add(holding.Scope.ScopeId) ||
                    !HoldingSourceIsValid(report, holding))
                {
                    return InstitutionalServiceResult<List<Holding>>.Rejected(
                        "precedent.invalid-holding-record");
                }
                if (OrdinalEquals(holding.IssueId, issueId) &&
                    holding.Scope.AppliesTo(
                        targetAgentId,
                        targetEmployerId,
                        targetIdentityConditionId,
                        caseFacts))
                {
                    matches.Add(holding);
                }
            }

            matches.Sort(CompareHoldingMatches);
            return matches.Count == 0
                ? InstitutionalServiceResult<List<Holding>>.NoChange(
                    "precedent.no-match",
                    matches)
                : InstitutionalServiceResult<List<Holding>>.Applied(matches);
        }

        internal static InstitutionalServiceResult<Holding> ApplyHolding(
            InstitutionalConsequenceReport report,
            string holdingId,
            string targetRulingId,
            string targetCaseId,
            string issueId,
            CaseFactSet caseFacts)
        {
            return ApplyHolding(
                report,
                holdingId,
                targetRulingId,
                targetCaseId,
                issueId,
                targetAgentId: null,
                targetEmployerId: null,
                targetIdentityConditionId: null,
                caseFacts: caseFacts);
        }

        internal static InstitutionalServiceResult<Holding> ApplyHolding(
            InstitutionalConsequenceReport report,
            string holdingId,
            string targetRulingId,
            string targetCaseId,
            string issueId,
            string targetAgentId,
            string targetEmployerId,
            string targetIdentityConditionId,
            CaseFactSet caseFacts)
        {
            if (report == null || IsBlank(holdingId) || IsBlank(targetRulingId) ||
                IsBlank(targetCaseId) || IsBlank(issueId))
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "precedent.invalid-application-input");
            }

            InstitutionalServiceResult<List<Holding>> matchResult =
                FindMatchingHoldings(
                    report,
                    issueId,
                    targetAgentId,
                    targetEmployerId,
                    targetIdentityConditionId,
                    caseFacts);
            if (matchResult.Outcome == InstitutionalServiceOutcome.Rejected)
            {
                return InstitutionalServiceResult<Holding>.Rejected(matchResult.ReasonId);
            }
            Holding holding = null;
            for (int i = 0; i < matchResult.Value.Count; i++)
            {
                if (OrdinalEquals(matchResult.Value[i].HoldingId, holdingId))
                {
                    holding = matchResult.Value[i];
                    break;
                }
            }
            if (holding == null)
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "precedent.holding-does-not-match");
            }

            Ruling ruling = FindUniqueRuling(report, targetRulingId, out int rulingCount);
            if (rulingCount != 1 || ruling == null ||
                !OrdinalEquals(ruling.CaseId, targetCaseId) ||
                OrdinalEquals(ruling.RulingId, holding.SourceRulingId) ||
                ruling.Cycle < holding.EstablishedCycle)
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "precedent.invalid-target-ruling");
            }
            OfficialFinding finding = FindUniqueFinding(
                report,
                ruling.FindingId,
                out int findingCount);
            if (findingCount != 1 || finding == null ||
                !OrdinalEquals(finding.CaseId, targetCaseId) ||
                !OrdinalEquals(finding.IssueId, issueId) ||
                finding.Cycle > ruling.Cycle)
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "precedent.target-issue-mismatch");
            }

            DescendantCase descendant = FindUniqueDescendantCase(
                report,
                targetCaseId,
                out int descendantCount);
            if (descendantCount > 1)
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "precedent.duplicate-target-case");
            }

            int citedHoldingCount = CountOrdinal(ruling.CitedHoldingIds, holding.HoldingId);
            int citedScopeCount = CountOrdinal(ruling.CitedScopeIds, holding.Scope.ScopeId);
            int appliedCaseCount = CountOrdinal(holding.AppliedCaseIds, targetCaseId);
            int descendantCitationCount = descendant == null
                ? 0
                : CountOrdinal(descendant.CitedHoldingIds, holding.HoldingId);
            int timelineCount = CountTimeline(
                report,
                InstitutionalTimelineKind.PrecedentApplied,
                holding.HoldingId,
                targetCaseId,
                ruling.RulingId);

            if (citedHoldingCount > 1 || citedScopeCount > 1 || appliedCaseCount > 1 ||
                descendantCitationCount > 1 || timelineCount > 1)
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "precedent.duplicate-application-record");
            }

            bool absent = citedHoldingCount == 0 && citedScopeCount == 0 &&
                          appliedCaseCount == 0 && descendantCitationCount == 0 &&
                          timelineCount == 0;
            bool complete = citedHoldingCount == 1 && citedScopeCount == 1 &&
                            appliedCaseCount == 1 && timelineCount == 1 &&
                            (descendant == null || descendantCitationCount == 1);
            if (complete)
            {
                return InstitutionalServiceResult<Holding>.NoChange(
                    "precedent.already-applied",
                    holding);
            }
            if (!absent)
            {
                return InstitutionalServiceResult<Holding>.Rejected(
                    "precedent.partial-application-record");
            }

            ruling.CitedHoldingIds.Add(holding.HoldingId);
            ruling.CitedScopeIds.Add(holding.Scope.ScopeId);
            holding.AppliedCaseIds.Add(targetCaseId);
            if (descendant != null) descendant.CitedHoldingIds.Add(holding.HoldingId);
            InstitutionalTimeline.Add(
                report,
                ruling.Cycle,
                InstitutionalTimelineKind.PrecedentApplied,
                holding.HoldingId,
                targetCaseId,
                ruling.RulingId);
            return InstitutionalServiceResult<Holding>.Applied(holding);
        }

        private static AgentActionTrace FindAutonomousAppealTrace(
            InstitutionalConsequenceRun run,
            SocietyEvent filingEvent,
            out int count)
        {
            AgentActionTrace found = null;
            count = 0;
            for (int i = 0; i < run.AssessorActionTraces.Count; i++)
            {
                AgentActionTrace trace = run.AssessorActionTraces[i];
                if (trace == null ||
                    !OrdinalEquals(trace.DecisionId, filingEvent.CauseDecisionId) ||
                    trace.Action != SocietyActionKind.Appeal ||
                    !OrdinalEquals(trace.ActorId, filingEvent.ActorId) ||
                    !OrdinalEquals(trace.OpportunityId, filingEvent.OpportunityId) ||
                    CountOrdinal(trace.ResultEventIds, filingEvent.EventId) != 1)
                {
                    continue;
                }
                found = trace;
                count++;
            }
            return found;
        }

        private static AppealOpportunity FindUniqueOpportunity(
            IReadOnlyList<AppealOpportunity> opportunities,
            string opportunityId,
            out int count)
        {
            AppealOpportunity found = null;
            count = 0;
            for (int i = 0; i < opportunities.Count; i++)
            {
                AppealOpportunity opportunity = opportunities[i];
                if (opportunity != null &&
                    OrdinalEquals(opportunity.OpportunityId, opportunityId))
                {
                    found = opportunity;
                    count++;
                }
            }
            return found;
        }

        private static bool AppealMatchesFiling(
            InstitutionalConsequenceReport report,
            Appeal appeal,
            SocietyEvent filingEvent,
            AppealOpportunity opportunity,
            Ruling challenged)
        {
            if (appeal == null) return false;
            if (!OrdinalEquals(appeal.CaseId, opportunity.CaseId) ||
                appeal.FiledCycle != filingEvent.Tick ||
                appeal.HearingCycle != opportunity.HearingCycle ||
                !OrdinalEquals(appeal.AppellantAgentId, filingEvent.ActorId) ||
                !OrdinalEquals(appeal.FilingActionEventId, filingEvent.EventId) ||
                !OrdinalEquals(appeal.ChallengedRulingId, challenged.RulingId))
            {
                return false;
            }
            List<EvidenceArtifact> expected = InstitutionalEvidencePipeline.ForCase(
                report,
                opportunity.CaseId,
                filingEvent.Tick);
            return SameOrdinalSet(
                appeal.GroundsEvidenceArtifactIds,
                InstitutionalEvidencePipeline.CopyIds(expected));
        }

        private static bool HoldingMatchesDefinition(
            Holding holding,
            Appeal appeal,
            Ruling ruling,
            string ruleId,
            string issueId,
            PrecedentScope proposedScope,
            List<string> expectedSupportingEvidenceArtifactIds)
        {
            return holding != null &&
                   holding.EstablishedCycle == ruling.Cycle &&
                   OrdinalEquals(holding.SourceAppealId, appeal.AppealId) &&
                   OrdinalEquals(holding.SourceRulingId, ruling.RulingId) &&
                   OrdinalEquals(holding.RuleId, ruleId) &&
                   OrdinalEquals(holding.IssueId, issueId) &&
                   ScopeEquals(holding.Scope, proposedScope) &&
                   SameOrdinalSet(
                       holding.SupportingEvidenceArtifactIds,
                       expectedSupportingEvidenceArtifactIds);
        }

        private static bool HoldingSourceIsValid(
            InstitutionalConsequenceReport report,
            Holding holding)
        {
            if (holding == null || IsBlank(holding.HoldingId) ||
                IsBlank(holding.SourceAppealId) || IsBlank(holding.SourceRulingId) ||
                IsBlank(holding.RuleId) || IsBlank(holding.IssueId) ||
                !ScopeIsValid(holding.Scope, requireFacts: true))
            {
                return false;
            }
            Appeal appeal = FindUniqueAppeal(
                report,
                holding.SourceAppealId,
                out int appealCount);
            Ruling ruling = FindUniqueRuling(
                report,
                holding.SourceRulingId,
                out int rulingCount);
            OfficialFinding finding = null;
            int findingCount = 0;
            if (ruling != null)
                finding = FindUniqueFinding(report, ruling.FindingId, out findingCount);
            return appealCount == 1 && rulingCount == 1 && findingCount == 1 &&
                   appeal != null && ruling != null && finding != null &&
                   ResolvedAppealSourceIsValid(report, appeal, ruling) &&
                   ruling.Disposition == RulingDisposition.ReversedAndRecognised &&
                   OrdinalEquals(finding.CaseId, ruling.CaseId) &&
                   OrdinalEquals(finding.IssueId, holding.IssueId) &&
                   finding.Cycle <= ruling.Cycle &&
                   holding.EstablishedCycle == ruling.Cycle &&
                   IsNonEmptyOrdinalSubset(
                       holding.SupportingEvidenceArtifactIds,
                       ruling.EvidenceArtifactIds) &&
                   EvidenceEnvelopeIsValid(
                       report,
                       ruling.CaseId,
                       ruling.Cycle,
                       holding.SupportingEvidenceArtifactIds) &&
                   CountTimeline(
                       report,
                       InstitutionalTimelineKind.HoldingEstablished,
                       ruling.RulingId,
                       holding.HoldingId,
                       holding.RuleId) == 1;
        }

        private static bool ResolvedAppealSourceIsValid(
            InstitutionalConsequenceReport report,
            Appeal appeal,
            Ruling ruling)
        {
            if (appeal == null || ruling == null ||
                appeal.Disposition != AppealDisposition.Reversed ||
                IsBlank(appeal.FilingActionEventId) ||
                IsBlank(appeal.AppellantAgentId) ||
                !OrdinalEquals(appeal.ResultingRulingId, ruling.RulingId) ||
                !OrdinalEquals(appeal.CaseId, ruling.CaseId))
            {
                return false;
            }
            Ruling challenged = FindUniqueRuling(
                report,
                appeal.ChallengedRulingId,
                out int challengedCount);
            return challengedCount == 1 && challenged != null &&
                   OrdinalEquals(challenged.CaseId, appeal.CaseId) &&
                   challenged.Cycle < appeal.FiledCycle &&
                   appeal.FiledCycle <= appeal.HearingCycle &&
                   appeal.HearingCycle <= ruling.Cycle &&
                   EvidenceEnvelopeIsValid(
                       report,
                       appeal.CaseId,
                       appeal.FiledCycle,
                       appeal.GroundsEvidenceArtifactIds) &&
                   EvidenceEnvelopeIsValid(
                       report,
                       ruling.CaseId,
                       ruling.Cycle,
                       ruling.EvidenceArtifactIds) &&
                   CountTimeline(
                       report,
                       InstitutionalTimelineKind.AppealFiled,
                       appeal.FilingActionEventId,
                       appeal.AppellantAgentId,
                       appeal.AppealId) == 1 &&
                   CountTimeline(
                       report,
                       InstitutionalTimelineKind.AppealHeard,
                       appeal.AppealId,
                       appeal.CaseId,
                       ruling.RulingId) == 1;
        }

        private static int CompareHoldingMatches(Holding left, Holding right)
        {
            int leftSpecificity = left.Scope.RequiredFacts.Count;
            int rightSpecificity = right.Scope.RequiredFacts.Count;
            int specificity = rightSpecificity.CompareTo(leftSpecificity);
            if (specificity != 0) return specificity;
            int cycle = left.EstablishedCycle.CompareTo(right.EstablishedCycle);
            if (cycle != 0) return cycle;
            return StringComparer.Ordinal.Compare(left.HoldingId, right.HoldingId);
        }

        private static bool EvidenceEnvelopeIsValid(
            InstitutionalConsequenceReport report,
            string caseId,
            long maximumCycle,
            List<string> evidenceIds)
        {
            if (report == null || IsBlank(caseId) || evidenceIds == null) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < evidenceIds.Count; i++)
            {
                string evidenceId = evidenceIds[i];
                if (IsBlank(evidenceId) || !seen.Add(evidenceId)) return false;
                EvidenceArtifact artifact = FindUniqueEvidence(report, evidenceId, out int count);
                if (count != 1 || artifact == null ||
                    !OrdinalEquals(artifact.CaseId, caseId) ||
                    artifact.EnteredCycle > maximumCycle)
                {
                    return false;
                }
            }
            return true;
        }

        private static List<EvidenceArtifact> SelectDeclaredEvidence(
            InstitutionalConsequenceReport report,
            string caseId,
            long maximumCycle,
            IReadOnlyList<string> declaredEvidenceArtifactIds)
        {
            if (declaredEvidenceArtifactIds == null ||
                declaredEvidenceArtifactIds.Count == 0)
            {
                throw new InvalidOperationException(
                    "A declared evidence envelope requires at least one artifact.");
            }

            var result = new List<EvidenceArtifact>(
                declaredEvidenceArtifactIds.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < declaredEvidenceArtifactIds.Count; i++)
            {
                string artifactId = declaredEvidenceArtifactIds[i];
                if (IsBlank(artifactId) || !seen.Add(artifactId))
                {
                    throw new InvalidOperationException(
                        "Declared evidence ids must be non-blank and unique.");
                }
                EvidenceArtifact artifact = FindUniqueEvidence(
                    report,
                    artifactId,
                    out int count);
                if (count != 1 || artifact == null ||
                    !OrdinalEquals(artifact.CaseId, caseId) ||
                    artifact.EnteredCycle > maximumCycle)
                {
                    throw new InvalidOperationException(
                        "Declared evidence is missing, unrelated, or not yet entered.");
                }
                result.Add(artifact);
            }
            result.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ArtifactId,
                right.ArtifactId));
            return result;
        }

        private static bool ScopeIsValid(PrecedentScope scope, bool requireFacts)
        {
            if (scope == null || IsBlank(scope.ScopeId) || scope.RequiredFacts == null)
                return false;
            if (!Enum.IsDefined(typeof(PrecedentReach), scope.Reach)) return false;
            if (scope.Reach == PrecedentReach.Individual && IsBlank(scope.BoundAgentId))
                return false;
            if (scope.Reach == PrecedentReach.Employer && IsBlank(scope.BoundEmployerId))
                return false;
            try
            {
                scope.RequiredFacts.Validate();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            return !requireFacts || scope.RequiredFacts.Count > 0;
        }

        private static PrecedentScope CopyScope(PrecedentScope source)
        {
            return new PrecedentScope
            {
                ScopeId = source.ScopeId,
                Reach = source.Reach,
                BoundAgentId = source.BoundAgentId,
                BoundEmployerId = source.BoundEmployerId,
                IdentityConditionId = source.IdentityConditionId,
                RequiredFacts = source.RequiredFacts.Copy(),
                Retrospective = source.Retrospective,
            };
        }

        private static bool ScopeEquals(PrecedentScope left, PrecedentScope right)
        {
            if (!ScopeIsValid(left, requireFacts: true) ||
                !ScopeIsValid(right, requireFacts: true) ||
                !OrdinalEquals(left.ScopeId, right.ScopeId) ||
                left.Reach != right.Reach ||
                !OrdinalEquals(left.BoundAgentId, right.BoundAgentId) ||
                !OrdinalEquals(left.BoundEmployerId, right.BoundEmployerId) ||
                !OrdinalEquals(left.IdentityConditionId, right.IdentityConditionId) ||
                left.Retrospective != right.Retrospective ||
                left.RequiredFacts.Count != right.RequiredFacts.Count)
            {
                return false;
            }
            for (int i = 0; i < left.RequiredFacts.Facts.Count; i++)
            {
                if (!right.RequiredFacts.Contains(left.RequiredFacts.Facts[i])) return false;
            }
            return true;
        }

        private static bool SameOrdinalSet(List<string> left, List<string> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            var leftSet = new HashSet<string>(left, StringComparer.Ordinal);
            var rightSet = new HashSet<string>(right, StringComparer.Ordinal);
            return leftSet.Count == left.Count && rightSet.Count == right.Count &&
                   leftSet.SetEquals(rightSet);
        }

        private static bool IsNonEmptyOrdinalSubset(
            List<string> subset,
            List<string> superset)
        {
            if (subset == null || subset.Count == 0 || superset == null) return false;
            var subsetIds = new HashSet<string>(subset, StringComparer.Ordinal);
            if (subsetIds.Count != subset.Count) return false;
            var supersetIds = new HashSet<string>(superset, StringComparer.Ordinal);
            return supersetIds.IsSupersetOf(subsetIds);
        }

        private static int CountTimeline(
            InstitutionalConsequenceReport report,
            InstitutionalTimelineKind kind,
            string causeId,
            string subjectId,
            string detailId)
        {
            int count = 0;
            for (int i = 0; i < report.Timeline.Count; i++)
            {
                InstitutionalTimelineEntry entry = report.Timeline[i];
                if (entry != null && entry.Kind == kind &&
                    OrdinalEquals(entry.CauseId, causeId) &&
                    OrdinalEquals(entry.SubjectId, subjectId) &&
                    OrdinalEquals(entry.DetailId, detailId))
                {
                    count++;
                }
            }
            return count;
        }

        private static ObservedAgentAction FindUniqueObservedAction(
            InstitutionalConsequenceReport report,
            string eventId,
            out int count)
        {
            ObservedAgentAction found = null;
            count = 0;
            for (int i = 0; i < report.ObservedAgentActions.Count; i++)
            {
                ObservedAgentAction action = report.ObservedAgentActions[i];
                if (action != null && OrdinalEquals(action.ActionEventId, eventId))
                {
                    found = action;
                    count++;
                }
            }
            return found;
        }

        private static Appeal FindUniqueAppeal(
            InstitutionalConsequenceReport report,
            string appealId,
            out int count)
        {
            Appeal found = null;
            count = 0;
            for (int i = 0; i < report.Appeals.Count; i++)
            {
                Appeal appeal = report.Appeals[i];
                if (appeal != null && OrdinalEquals(appeal.AppealId, appealId))
                {
                    found = appeal;
                    count++;
                }
            }
            return found;
        }

        private static Appeal FindAppealByFilingEvent(
            InstitutionalConsequenceReport report,
            string filingEventId,
            out int count)
        {
            Appeal found = null;
            count = 0;
            for (int i = 0; i < report.Appeals.Count; i++)
            {
                Appeal appeal = report.Appeals[i];
                if (appeal != null &&
                    OrdinalEquals(appeal.FilingActionEventId, filingEventId))
                {
                    found = appeal;
                    count++;
                }
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
                Ruling ruling = report.Rulings[i];
                if (ruling != null && OrdinalEquals(ruling.RulingId, rulingId))
                {
                    found = ruling;
                    count++;
                }
            }
            return found;
        }

        private static OfficialFinding FindUniqueFinding(
            InstitutionalConsequenceReport report,
            string findingId,
            out int count)
        {
            OfficialFinding found = null;
            count = 0;
            for (int i = 0; i < report.OfficialFindings.Count; i++)
            {
                OfficialFinding finding = report.OfficialFindings[i];
                if (finding != null && OrdinalEquals(finding.FindingId, findingId))
                {
                    found = finding;
                    count++;
                }
            }
            return found;
        }

        private static Holding FindUniqueHolding(
            InstitutionalConsequenceReport report,
            string holdingId,
            out int count)
        {
            Holding found = null;
            count = 0;
            for (int i = 0; i < report.Holdings.Count; i++)
            {
                Holding holding = report.Holdings[i];
                if (holding != null && OrdinalEquals(holding.HoldingId, holdingId))
                {
                    found = holding;
                    count++;
                }
            }
            return found;
        }

        private static EvidenceArtifact FindUniqueEvidence(
            InstitutionalConsequenceReport report,
            string evidenceId,
            out int count)
        {
            EvidenceArtifact found = null;
            count = 0;
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                EvidenceArtifact artifact = report.EvidenceArtifacts[i];
                if (artifact != null && OrdinalEquals(artifact.ArtifactId, evidenceId))
                {
                    found = artifact;
                    count++;
                }
            }
            return found;
        }

        private static DescendantCase FindUniqueDescendantCase(
            InstitutionalConsequenceReport report,
            string caseId,
            out int count)
        {
            DescendantCase found = null;
            count = 0;
            for (int i = 0; i < report.DescendantCases.Count; i++)
            {
                DescendantCase descendant = report.DescendantCases[i];
                if (descendant != null && OrdinalEquals(descendant.CaseId, caseId))
                {
                    found = descendant;
                    count++;
                }
            }
            return found;
        }

        private static int CountOrdinal(List<string> values, string expected)
        {
            if (values == null) return -1;
            int count = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (OrdinalEquals(values[i], expected)) count++;
            }
            return count;
        }

        private static bool OrdinalEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static bool IsBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }
    }
}
