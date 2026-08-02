using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    public static class InstitutionalConsequenceValidator
    {
        public static void Validate(InstitutionalConsequenceReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            Require(report.Ruleset == InstitutionalConsequenceReport.RulesetVersion,
                "Unexpected consequence-loop ruleset.");
            Require(report.FinalCycle == InstitutionalConsequenceLoop.FinalCycle,
                "The proof must close at cycle 15.");

            HashSet<string> actionIds = Unique(report.ObservedAgentActions,
                value => value.ActionEventId, "observed action");
            HashSet<string> evidenceIds = Unique(report.EvidenceArtifacts,
                value => value.ArtifactId, "evidence artifact");
            HashSet<string> findingIds = Unique(report.OfficialFindings,
                value => value.FindingId, "official finding");
            HashSet<string> rulingIds = Unique(report.Rulings,
                value => value.RulingId, "ruling");
            HashSet<string> mutationIds = Unique(report.OfficialStatusMutations,
                value => value.MutationId, "official mutation");
            HashSet<string> descendantIds = Unique(report.DescendantCases,
                value => value.CaseId, "descendant case");
            HashSet<string> appealIds = Unique(report.Appeals,
                value => value.AppealId, "appeal");
            HashSet<string> holdingIds = Unique(report.Holdings,
                value => value.HoldingId, "holding");
            Unique(report.RelianceObservations,
                value => value.ObservationId, "reliance observation");
            HashSet<string> allocationIds = Unique(report.WorkAllocations,
                value => value.AllocationId, "work allocation");
            Unique(report.MaterialConsequences, value => value.ConsequenceId, "material consequence");
            Unique(report.ConnectedOutcomes, value => value.PairId, "connected outcome");
            Unique(report.Timeline, value => value.EntryId, "timeline entry");

            var actionCycles = new Dictionary<string, long>(StringComparer.Ordinal);
            var actionsById = new Dictionary<string, ObservedAgentAction>(StringComparer.Ordinal);
            for (int i = 0; i < report.ObservedAgentActions.Count; i++)
            {
                actionCycles[report.ObservedAgentActions[i].ActionEventId] =
                    report.ObservedAgentActions[i].Cycle;
                actionsById[report.ObservedAgentActions[i].ActionEventId] =
                    report.ObservedAgentActions[i];
            }
            var rulingCycles = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int i = 0; i < report.Rulings.Count; i++)
                rulingCycles[report.Rulings[i].RulingId] = report.Rulings[i].Cycle;

            int agentEvidenceCount = 0;
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                EvidenceArtifact artifact = report.EvidenceArtifacts[i];
                Require(artifact.Provenance != null,
                    $"Evidence {artifact.ArtifactId} has no provenance.");
                Require(artifact.EnteredCycle >= 1 && artifact.EnteredCycle <= report.FinalCycle,
                    $"Evidence {artifact.ArtifactId} has an invalid cycle.");
                Require(artifact.Reliability >= 0 && artifact.Reliability <= 100,
                    $"Evidence {artifact.ArtifactId} has invalid reliability.");
                Require(artifact.OfficiallySubmitted,
                    $"Reported evidence {artifact.ArtifactId} was not officially submitted.");
                Require(artifact.KnownByAgentIds != null &&
                        artifact.KnownByAgentIds.Count > 0,
                    $"Evidence {artifact.ArtifactId} records no knower.");
                if (!artifact.Provenance.CreatedByAgentAction) continue;
                agentEvidenceCount++;
                Require(actionIds.Contains(artifact.Provenance.SourceSocietyEventId),
                    $"Evidence {artifact.ArtifactId} has no source action.");
                Require(actionCycles[artifact.Provenance.SourceSocietyEventId] <= artifact.EnteredCycle,
                    $"Evidence {artifact.ArtifactId} predates its source action.");
                Require(string.Equals(
                        actionsById[artifact.Provenance.SourceSocietyEventId].ActorId,
                        artifact.Provenance.SourceAgentId,
                        StringComparison.Ordinal),
                    $"Evidence {artifact.ArtifactId} names the wrong source actor.");
            }
            Require(agentEvidenceCount >= 2,
                "At least two artifacts must descend from generic actions.");

            for (int i = 0; i < report.OfficialFindings.Count; i++)
            {
                OfficialFinding finding = report.OfficialFindings[i];
                RequireAllExist(finding.EvidenceArtifactIds, evidenceIds,
                    $"Finding {finding.FindingId} references missing evidence");
            }

            for (int i = 0; i < report.Rulings.Count; i++)
            {
                Ruling ruling = report.Rulings[i];
                Require(findingIds.Contains(ruling.FindingId),
                    $"Ruling {ruling.RulingId} has no finding.");
                Require(ruling.EvidenceArtifactIds != null && ruling.AppliedPolicyIds != null &&
                        ruling.SkippedProcedureIds != null && ruling.CitedHoldingIds != null &&
                        ruling.CitedScopeIds != null,
                    $"Ruling {ruling.RulingId} lacks a frozen procedural envelope.");
                Require(!string.IsNullOrWhiteSpace(ruling.PolicyVersion),
                    $"Ruling {ruling.RulingId} records no policy version.");
                Require(ruling.ConfidenceMinimum <= ruling.ConfidenceMaximum,
                    $"Ruling {ruling.RulingId} has an inverted confidence range.");
                OfficialFinding rulingFinding = FindFinding(report, ruling.FindingId);
                Require(rulingFinding != null &&
                        rulingFinding.WeightedEvidenceScore >= ruling.ConfidenceMinimum &&
                        rulingFinding.WeightedEvidenceScore <= ruling.ConfidenceMaximum,
                    $"Ruling {ruling.RulingId} score is outside its confidence range.");
                Require(string.Equals(rulingFinding.CaseId, ruling.CaseId,
                            StringComparison.Ordinal) &&
                        rulingFinding.Cycle == ruling.Cycle &&
                        new HashSet<string>(rulingFinding.EvidenceArtifactIds,
                            StringComparer.Ordinal).SetEquals(ruling.EvidenceArtifactIds),
                    $"Ruling {ruling.RulingId} and its finding disagree.");
                Require(ruling.AppliedPolicyIds.Contains(ruling.PolicyVersion),
                    $"Ruling {ruling.RulingId} did not apply its recorded policy version.");
                Require(ruling.AppliedPolicyIds.Count > 0,
                    $"Ruling {ruling.RulingId} records no policy.");
                RequireAllExist(ruling.EvidenceArtifactIds, evidenceIds,
                    $"Ruling {ruling.RulingId} references missing evidence");
                RequireAllExist(ruling.OfficialStatusMutationIds, mutationIds,
                    $"Ruling {ruling.RulingId} references missing mutations");
                RequireAllExist(ruling.CitedHoldingIds, holdingIds,
                    $"Ruling {ruling.RulingId} cites a missing holding");
                Require(ruling.CitedHoldingIds.Count == ruling.CitedScopeIds.Count,
                    $"Ruling {ruling.RulingId} did not freeze holding and scope together.");
                for (int citationIndex = 0;
                     citationIndex < ruling.CitedHoldingIds.Count;
                     citationIndex++)
                {
                    Holding citedHolding = FindHolding(
                        report, ruling.CitedHoldingIds[citationIndex]);
                    Require(citedHolding?.Scope != null &&
                            string.Equals(citedHolding.Scope.ScopeId,
                                ruling.CitedScopeIds[citationIndex],
                                StringComparison.Ordinal) &&
                            string.Equals(citedHolding.IssueId, rulingFinding.IssueId,
                                StringComparison.Ordinal),
                        $"Ruling {ruling.RulingId} mismatches a cited holding, scope, or issue.");
                }

                var expectedEvidence = new HashSet<string>(StringComparer.Ordinal);
                for (int evidenceIndex = 0; evidenceIndex < report.EvidenceArtifacts.Count; evidenceIndex++)
                {
                    EvidenceArtifact artifact = report.EvidenceArtifacts[evidenceIndex];
                    if (artifact.EnteredCycle <= ruling.Cycle &&
                        string.Equals(artifact.CaseId, ruling.CaseId, StringComparison.Ordinal))
                        expectedEvidence.Add(artifact.ArtifactId);
                }
                Require(expectedEvidence.SetEquals(ruling.EvidenceArtifactIds),
                    $"Ruling {ruling.RulingId} did not freeze the exact as-of-cycle evidence set.");
            }

            for (int i = 0; i < report.OfficialStatusMutations.Count; i++)
            {
                OfficialStatusMutation mutation = report.OfficialStatusMutations[i];
                Require(rulingIds.Contains(mutation.CauseId),
                    $"Mutation {mutation.MutationId} has no causal ruling.");
                Require(rulingCycles[mutation.CauseId] == mutation.Cycle,
                    $"Mutation {mutation.MutationId} is not contemporaneous with its ruling.");
            }

            for (int i = 0; i < report.RelianceObservations.Count; i++)
            {
                RelianceObservation reliance = report.RelianceObservations[i];
                Require(actionIds.Contains(reliance.SourceActionEventId),
                    $"Reliance {reliance.ObservationId} has no source action.");
                Require(rulingIds.Contains(reliance.EnablingRulingId),
                    $"Reliance {reliance.ObservationId} has no enabling ruling.");
                Require(mutationIds.Contains(reliance.EnablingMutationId),
                    $"Reliance {reliance.ObservationId} has no enabling mutation.");
                Require(actionCycles[reliance.SourceActionEventId] == reliance.Cycle &&
                        rulingCycles[reliance.EnablingRulingId] < reliance.Cycle,
                    $"Reliance {reliance.ObservationId} has invalid chronology.");
                Require(reliance.RecordedResourceDelta < 0,
                    $"Reliance {reliance.ObservationId} records no irreversible cost.");
            }

            Require(report.DescendantCases.Count > 0,
                "At least one descendant case is required.");
            for (int i = 0; i < report.DescendantCases.Count; i++)
            {
                DescendantCase descendant = report.DescendantCases[i];
                Require(descendant.SourceActionEventIds != null &&
                        descendant.SourceActionEventIds.Count > 0,
                    $"Descendant case {descendant.CaseId} has no source action.");
                RequireAllExist(descendant.SourceActionEventIds, actionIds,
                    $"Descendant case {descendant.CaseId} references a missing action");
                for (int actionIndex = 0; actionIndex < descendant.SourceActionEventIds.Count; actionIndex++)
                    Require(actionCycles[descendant.SourceActionEventIds[actionIndex]] <= descendant.OpenedCycle,
                        $"Descendant case {descendant.CaseId} predates a source action.");
                bool parentIsAction = actionIds.Contains(descendant.ParentCauseId);
                bool parentIsRuling = rulingIds.Contains(descendant.ParentCauseId);
                Require(parentIsAction || parentIsRuling,
                    $"Descendant case {descendant.CaseId} has an unresolved parent cause.");
                long parentCycle = parentIsAction
                    ? actionCycles[descendant.ParentCauseId]
                    : rulingCycles[descendant.ParentCauseId];
                Require(parentCycle <= descendant.OpenedCycle,
                    $"Descendant case {descendant.CaseId} predates its parent cause.");
                Require(!string.IsNullOrWhiteSpace(descendant.ParentCaseId) &&
                        !string.IsNullOrWhiteSpace(descendant.OriginatingEventId) &&
                        !string.IsNullOrWhiteSpace(descendant.OriginatingRulingId) &&
                        !string.IsNullOrWhiteSpace(descendant.CausalAgentActionId),
                    $"Descendant case {descendant.CaseId} lacks an explicit causal envelope.");
                Require(actionIds.Contains(descendant.CausalAgentActionId),
                    $"Descendant case {descendant.CaseId} has no causal agent action.");
                Require(actionIds.Contains(descendant.OriginatingEventId) &&
                        string.Equals(descendant.OriginatingEventId,
                            descendant.CausalAgentActionId, StringComparison.Ordinal) &&
                        descendant.SourceActionEventIds.Contains(descendant.OriginatingEventId) &&
                        descendant.SourceActionEventIds.Contains(descendant.CausalAgentActionId) &&
                        string.Equals(actionsById[descendant.CausalAgentActionId].ActorId,
                            descendant.ClaimantAgentId, StringComparison.Ordinal),
                    $"Descendant case {descendant.CaseId} has a semantically invalid action path.");
                Require(rulingIds.Contains(descendant.OriginatingRulingId),
                    $"Descendant case {descendant.CaseId} has no originating ruling.");
                Ruling originatingRuling = FindRuling(report, descendant.OriginatingRulingId);
                Require(string.Equals(originatingRuling.CaseId, descendant.ParentCaseId,
                        StringComparison.Ordinal),
                    $"Descendant case {descendant.CaseId} names the wrong parent case.");
                bool hasExpectedParentCause = descendant.Kind switch
                {
                    DescendantCaseKind.Appeal => string.Equals(
                        descendant.ParentCauseId, descendant.OriginatingRulingId,
                        StringComparison.Ordinal),
                    DescendantCaseKind.RelatedClaim => string.Equals(
                        descendant.ParentCauseId, descendant.CausalAgentActionId,
                        StringComparison.Ordinal),
                    DescendantCaseKind.Reliance => string.Equals(
                        descendant.ParentCauseId, descendant.OriginatingRulingId,
                        StringComparison.Ordinal),
                    _ => false,
                };
                Require(hasExpectedParentCause,
                    $"Descendant case {descendant.CaseId} has the wrong kind-specific parent cause.");
                RequireAllExist(descendant.CitedHoldingIds, holdingIds,
                    $"Descendant case {descendant.CaseId} cites a missing holding");
                if (descendant.Kind == DescendantCaseKind.RelatedClaim)
                {
                    Require(!string.IsNullOrWhiteSpace(descendant.OfficialIssueId) &&
                            !string.IsNullOrWhiteSpace(descendant.OfficialIdentityConditionId) &&
                            !string.IsNullOrWhiteSpace(descendant.OfficialEmployerId),
                        $"Related case {descendant.CaseId} lacks official scope facts.");
                }
            }

            for (int i = 0; i < report.Appeals.Count; i++)
            {
                Appeal appeal = report.Appeals[i];
                Require(actionIds.Contains(appeal.FilingActionEventId),
                    $"Appeal {appeal.AppealId} was not filed by an action.");
                Require(actionsById[appeal.FilingActionEventId].Activity ==
                        ObservedActivityKind.AppealFiled &&
                        string.Equals(actionsById[appeal.FilingActionEventId].ActorId,
                            appeal.AppellantAgentId, StringComparison.Ordinal) &&
                        actionCycles[appeal.FilingActionEventId] == appeal.FiledCycle,
                    $"Appeal {appeal.AppealId} has an invalid filing action.");
                Require(rulingIds.Contains(appeal.ChallengedRulingId),
                    $"Appeal {appeal.AppealId} challenges no ruling.");
                Require(rulingCycles[appeal.ChallengedRulingId] < appeal.FiledCycle,
                    $"Appeal {appeal.AppealId} predates its challenged ruling.");
                RequireAllExist(appeal.GroundsEvidenceArtifactIds, evidenceIds,
                    $"Appeal {appeal.AppealId} references missing grounds");
                for (int groundsIndex = 0;
                     groundsIndex < appeal.GroundsEvidenceArtifactIds.Count;
                     groundsIndex++)
                {
                    EvidenceArtifact ground = FindEvidence(
                        report, appeal.GroundsEvidenceArtifactIds[groundsIndex]);
                    Require(ground != null && ground.EnteredCycle <= appeal.FiledCycle &&
                            string.Equals(ground.CaseId, appeal.CaseId,
                                StringComparison.Ordinal),
                        $"Appeal {appeal.AppealId} has invalid grounds.");
                }
                if (!string.IsNullOrEmpty(appeal.ResultingRulingId))
                {
                    Require(rulingIds.Contains(appeal.ResultingRulingId),
                        $"Appeal {appeal.AppealId} resolves to a missing ruling.");
                    Require(rulingCycles[appeal.ResultingRulingId] >= appeal.FiledCycle,
                        $"Appeal {appeal.AppealId} resolves before filing.");
                    Ruling result = FindRuling(report, appeal.ResultingRulingId);
                    Require(string.Equals(result.CaseId, appeal.CaseId,
                            StringComparison.Ordinal) && result.Cycle == appeal.HearingCycle,
                        $"Appeal {appeal.AppealId} resolves in the wrong case or hearing.");
                    if (appeal.Disposition == AppealDisposition.Affirmed)
                        Require(result.Disposition == RulingDisposition.Affirmed,
                            $"Appeal {appeal.AppealId} says affirmed but its ruling does not.");
                    if (appeal.Disposition == AppealDisposition.Reversed)
                        Require(result.Disposition == RulingDisposition.ReversedAndDenied ||
                                result.Disposition == RulingDisposition.ReversedAndRecognised,
                            $"Appeal {appeal.AppealId} says reversed but its ruling does not.");
                    DescendantCase appealCase = FindCaseByFilingAction(
                        report, appeal.FilingActionEventId);
                    Ruling challengedRuling = FindRuling(report, appeal.ChallengedRulingId);
                    bool hasExpectedCaseEnvelope = appealCase != null &&
                        challengedRuling != null && (appealCase.Kind switch
                        {
                            DescendantCaseKind.Appeal =>
                                string.Equals(appeal.CaseId, challengedRuling.CaseId,
                                    StringComparison.Ordinal) &&
                                string.Equals(appealCase.ParentCaseId, appeal.CaseId,
                                    StringComparison.Ordinal),
                            DescendantCaseKind.RelatedClaim =>
                                string.Equals(appeal.CaseId, appealCase.CaseId,
                                    StringComparison.Ordinal) &&
                                string.Equals(appealCase.ParentCaseId,
                                    challengedRuling.CaseId, StringComparison.Ordinal),
                            _ => false,
                        });
                    Require(appealCase != null &&
                            hasExpectedCaseEnvelope &&
                            appealCase.OpenedCycle == appeal.FiledCycle &&
                            string.Equals(appealCase.OriginatingRulingId,
                                appeal.ChallengedRulingId, StringComparison.Ordinal) &&
                            appealCase.Status == (appeal.Disposition == AppealDisposition.Reversed
                                ? DescendantCaseStatus.Recognised
                                : DescendantCaseStatus.Denied),
                        $"Appeal {appeal.AppealId} and its descendant case disagree.");
                }
            }

            for (int i = 0; i < report.Holdings.Count; i++)
            {
                Holding holding = report.Holdings[i];
                Require(appealIds.Contains(holding.SourceAppealId),
                    $"Holding {holding.HoldingId} has no source appeal.");
                Require(rulingIds.Contains(holding.SourceRulingId),
                    $"Holding {holding.HoldingId} has no source ruling.");
                Require(holding.SupportingEvidenceArtifactIds != null &&
                        holding.SupportingEvidenceArtifactIds.Count > 0,
                    $"Holding {holding.HoldingId} records no supporting evidence.");
                RequireAllExist(holding.SupportingEvidenceArtifactIds, evidenceIds,
                    $"Holding {holding.HoldingId} references missing evidence");
                Ruling sourceRuling = FindRuling(report, holding.SourceRulingId);
                Require(sourceRuling != null && IsNonEmptyOrdinalSubset(
                        holding.SupportingEvidenceArtifactIds,
                        sourceRuling.EvidenceArtifactIds),
                    $"Holding {holding.HoldingId} was not derived from its source ruling evidence.");
                Appeal sourceAppeal = FindAppeal(report, holding.SourceAppealId);
                OfficialFinding sourceFinding = FindFinding(report, sourceRuling.FindingId);
                Require(sourceAppeal != null &&
                        string.Equals(sourceAppeal.ResultingRulingId, sourceRuling.RulingId,
                            StringComparison.Ordinal) &&
                        sourceAppeal.Disposition == AppealDisposition.Reversed &&
                        sourceRuling.Disposition == RulingDisposition.ReversedAndRecognised &&
                        string.Equals(sourceFinding.IssueId, holding.IssueId,
                            StringComparison.Ordinal),
                    $"Holding {holding.HoldingId} is not supported by a successful appeal.");
                Require(holding.Scope != null && !string.IsNullOrWhiteSpace(holding.Scope.ScopeId),
                    $"Holding {holding.HoldingId} has no scope.");
                for (int caseIndex = 0; caseIndex < holding.AppliedCaseIds.Count; caseIndex++)
                {
                    DescendantCase descendant = FindCase(report, holding.AppliedCaseIds[caseIndex]);
                    Require(descendant != null &&
                            descendant.CitedHoldingIds.Contains(holding.HoldingId) &&
                            string.Equals(descendant.OfficialIssueId, holding.IssueId,
                                StringComparison.Ordinal),
                        $"Holding {holding.HoldingId} affected an uncited case.");
                    Require(holding.Scope.AppliesTo(descendant.ClaimantAgentId,
                            descendant.OfficialEmployerId,
                            descendant.OfficialIdentityConditionId),
                        $"Holding {holding.HoldingId} affected an out-of-scope case.");
                }
            }

            for (int i = 0; i < report.ConnectedOutcomes.Count; i++)
            {
                ConnectedOutcomePair pair = report.ConnectedOutcomes[i];
                Require(pair.WinnerResourceDelta > 0 && pair.LoserResourceDelta < 0,
                    $"Connected outcome {pair.PairId} lacks a winner and loser.");
                Require(allocationIds.Contains(pair.ConnectionId),
                    $"Connected outcome {pair.PairId} has no shared allocation.");
                WorkAllocationObservation allocation = FindAllocation(report, pair.ConnectionId);
                Require(string.Equals(allocation.PaidHolderAgentId, pair.WinnerAgentId,
                        StringComparison.Ordinal),
                    $"Connected outcome {pair.PairId} winner does not hold the allocation.");
                Require(!string.Equals(pair.WinnerAgentId, pair.LoserAgentId, StringComparison.Ordinal),
                    $"Connected outcome {pair.PairId} uses one person twice.");
                Require(!string.IsNullOrWhiteSpace(pair.WinnerDisplayName) &&
                        !string.IsNullOrWhiteSpace(pair.LoserDisplayName),
                    $"Connected outcome {pair.PairId} does not name both people.");
                Require(string.Equals(allocation.OriginalWorkerId, pair.WinnerAgentId,
                            StringComparison.Ordinal) &&
                        pair.WinnerResourceDelta == allocation.CommittedWage &&
                        pair.LoserResourceDelta == -allocation.CommittedWage,
                    $"Connected outcome {pair.PairId} does not transfer its allocation.");
                MaterialConsequence winnerMaterial = FindMaterial(
                    report, pair.WinnerAgentId, pair.WinnerResourceDelta);
                MaterialConsequence loserMaterial = FindMaterial(
                    report, pair.LoserAgentId, pair.LoserResourceDelta);
                Require(winnerMaterial != null && loserMaterial != null &&
                        string.Equals(winnerMaterial.CauseId, loserMaterial.CauseId,
                            StringComparison.Ordinal),
                    $"Connected outcome {pair.PairId} lacks paired material consequences.");
                Ruling outcomeRuling = FindRuling(report, winnerMaterial.CauseId);
                Require(outcomeRuling != null &&
                        outcomeRuling.AppliedPolicyIds.Contains(pair.CauseRuleId) &&
                        report.Holdings.Exists(value =>
                            value.RuleId == pair.CauseRuleId &&
                            outcomeRuling.CitedHoldingIds.Contains(value.HoldingId)),
                    $"Connected outcome {pair.PairId} is not caused by a cited rule.");
                Require(report.OfficialStatusMutations.Exists(value =>
                            value.AffectedAgentId == pair.WinnerAgentId &&
                            value.StatusId == "paid-shift-allocation" &&
                            value.AfterRecognised &&
                            value.CauseId == outcomeRuling.RulingId) &&
                        report.OfficialStatusMutations.Exists(value =>
                            value.AffectedAgentId == pair.LoserAgentId &&
                            value.StatusId == "paid-shift-allocation" &&
                            value.AfterRecognised) &&
                        report.OfficialStatusMutations.Exists(value =>
                            value.AffectedAgentId == pair.LoserAgentId &&
                            value.StatusId == "paid-shift-allocation" &&
                            !value.AfterRecognised &&
                            value.CauseId == outcomeRuling.RulingId),
                    $"Connected outcome {pair.PairId} does not displace its prior holder.");
            }

            long previousCycle = 0;
            for (int i = 0; i < report.Timeline.Count; i++)
            {
                InstitutionalTimelineEntry entry = report.Timeline[i];
                Require(entry.Cycle >= previousCycle,
                    $"Timeline regressed from {previousCycle} to {entry.Cycle}.");
                previousCycle = entry.Cycle;
            }

            var activityKinds = new HashSet<ObservedActivityKind>();
            for (int i = 0; i < report.ObservedAgentActions.Count; i++)
                if (report.ObservedAgentActions[i].Activity != ObservedActivityKind.NoVisibleAction)
                    activityKinds.Add(report.ObservedAgentActions[i].Activity);
            Require(activityKinds.Count >= 3,
                "The proof needs at least three visible consequential activity kinds.");
        }

        private static DescendantCase FindCase(InstitutionalConsequenceReport report, string caseId)
        {
            for (int i = 0; i < report.DescendantCases.Count; i++)
                if (string.Equals(report.DescendantCases[i].CaseId, caseId,
                    StringComparison.Ordinal)) return report.DescendantCases[i];
            return null;
        }

        private static Ruling FindRuling(InstitutionalConsequenceReport report, string rulingId)
        {
            for (int i = 0; i < report.Rulings.Count; i++)
                if (string.Equals(report.Rulings[i].RulingId, rulingId,
                    StringComparison.Ordinal)) return report.Rulings[i];
            return null;
        }

        private static OfficialFinding FindFinding(
            InstitutionalConsequenceReport report,
            string findingId)
        {
            for (int i = 0; i < report.OfficialFindings.Count; i++)
                if (string.Equals(report.OfficialFindings[i].FindingId, findingId,
                    StringComparison.Ordinal)) return report.OfficialFindings[i];
            return null;
        }

        private static EvidenceArtifact FindEvidence(
            InstitutionalConsequenceReport report,
            string artifactId)
        {
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
                if (string.Equals(report.EvidenceArtifacts[i].ArtifactId, artifactId,
                    StringComparison.Ordinal)) return report.EvidenceArtifacts[i];
            return null;
        }

        private static Appeal FindAppeal(InstitutionalConsequenceReport report, string appealId)
        {
            for (int i = 0; i < report.Appeals.Count; i++)
                if (string.Equals(report.Appeals[i].AppealId, appealId,
                    StringComparison.Ordinal)) return report.Appeals[i];
            return null;
        }

        private static Holding FindHolding(
            InstitutionalConsequenceReport report,
            string holdingId)
        {
            for (int i = 0; i < report.Holdings.Count; i++)
                if (string.Equals(report.Holdings[i].HoldingId, holdingId,
                    StringComparison.Ordinal)) return report.Holdings[i];
            return null;
        }

        private static DescendantCase FindCaseByFilingAction(
            InstitutionalConsequenceReport report,
            string filingActionId)
        {
            for (int i = 0; i < report.DescendantCases.Count; i++)
            {
                DescendantCase descendant = report.DescendantCases[i];
                if ((descendant.Kind == DescendantCaseKind.Appeal ||
                     descendant.Kind == DescendantCaseKind.RelatedClaim) &&
                    descendant.SourceActionEventIds.Contains(filingActionId)) return descendant;
            }
            return null;
        }

        private static MaterialConsequence FindMaterial(
            InstitutionalConsequenceReport report,
            string agentId,
            int resourceDelta)
        {
            for (int i = 0; i < report.MaterialConsequences.Count; i++)
            {
                MaterialConsequence material = report.MaterialConsequences[i];
                if (string.Equals(material.AgentId, agentId, StringComparison.Ordinal) &&
                    material.ResourceDelta == resourceDelta) return material;
            }
            return null;
        }

        private static WorkAllocationObservation FindAllocation(
            InstitutionalConsequenceReport report,
            string allocationId)
        {
            for (int i = 0; i < report.WorkAllocations.Count; i++)
                if (string.Equals(report.WorkAllocations[i].AllocationId, allocationId,
                    StringComparison.Ordinal)) return report.WorkAllocations[i];
            return null;
        }

        private static HashSet<string> Unique<T>(
            List<T> values,
            Func<T, string> id,
            string label)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                string value = id(values[i]);
                Require(!string.IsNullOrWhiteSpace(value), $"A {label} lacks an id.");
                Require(ids.Add(value), $"Duplicate {label} id {value}.");
            }
            return ids;
        }

        private static void RequireAllExist(
            List<string> references,
            HashSet<string> available,
            string message)
        {
            if (references == null) throw new InvalidOperationException($"{message}: null list.");
            for (int i = 0; i < references.Count; i++)
                Require(available.Contains(references[i]), $"{message}: {references[i]}.");
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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
