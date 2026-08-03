using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Desk42.Institutional
{
    /// <summary>
    /// Scenario-neutral integrity boundary for a completed institutional run. It
    /// validates the public causal graph first, then the assessor-only projection
    /// and exclusive-entitlement state when those surfaces are available.
    /// </summary>
    internal static class InstitutionalCausalGraphValidator
    {
        internal static void Validate(InstitutionalScenarioRunResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            Require(ReferenceEquals(result.Report, result.AssessorRun?.Report),
                "Scenario result and assessor run expose different public reports.");
            Validate(result.AssessorRun, result.EntitlementRegistry);
        }

        internal static void Validate(InstitutionalConsequenceRun run)
        {
            Validate(run, null);
        }

        internal static void Validate(
            InstitutionalConsequenceRun run,
            ExclusiveEntitlementRegistry entitlementRegistry)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.Report == null)
                throw new InvalidOperationException("Scenario run has no public report.");
            if (run.FinalSocietyState == null)
                throw new InvalidOperationException("Scenario run has no final society state.");

            SocietyStateValidator.Validate(run.FinalSocietyState);
            Require(run.FinalSocietyState.CurrentTick == run.Report.FinalCycle,
                "Final society tick does not match the report final cycle.");
            Require(run.FinalSocietyState.MasterSeed == run.Report.MasterSeed,
                "Final society seed does not match the report seed.");

            ReportIndex index = ValidateReport(run.Report);
            ValidateAgents(run, index);
            ValidateAuthorityProjection(run, index);
            ValidateEntitlements(run, index, entitlementRegistry);
            ValidateWorkAllocationProjection(run, index);
            ValidateNoLivedTruthIdentifierLeak(run, index);
        }

        internal static void Validate(InstitutionalConsequenceReport report)
        {
            ValidateReport(report);
        }

        private static ReportIndex ValidateReport(InstitutionalConsequenceReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            Require(string.Equals(
                    report.Ruleset,
                    InstitutionalConsequenceReport.RulesetVersion,
                    StringComparison.Ordinal),
                "Unexpected institutional consequence ruleset.");
            RequireId(report.PolicyConfigurationId, "report policy configuration");
            RequireId(report.PrimaryCaseId, "primary case");
            Require(report.FinalCycle >= 0, "Report final cycle cannot be negative.");
            AssertPublicSurfaceContainsNoAuthorityTypes();

            var index = new ReportIndex(report);
            ValidateActions(index);
            ValidateCaseOpenings(index);
            ValidateEvidence(index);
            ValidateFindings(index);
            ValidateRulings(index);
            ValidateMutations(index);
            ValidateDescendantCases(index);
            ValidateAppeals(index);
            ValidateHoldings(index);
            ValidateRelianceObservations(index);
            ValidateMaterialConsequences(index);
            ValidateConnectedOutcomes(index);
            ValidatePublicEntitlementRecords(index);
            ValidateWorkAllocationRecords(index);
            ValidateTimeline(index);
            return index;
        }

        private static void ValidateActions(ReportIndex index)
        {
            foreach (ObservedAgentAction action in index.Actions.Values)
            {
                ValidateCycle(action.Cycle, index.Report.FinalCycle,
                    $"Observed action {action.ActionEventId}");
                RequireId(action.ActorId,
                    $"observed action {action.ActionEventId} actor");
                Require(Enum.IsDefined(typeof(ObservedActivityKind), action.Activity),
                    $"Observed action {action.ActionEventId} has an invalid activity.");
                ValidateReferenceList(
                    action.ResultEvidenceArtifactIds,
                    index.Evidence,
                    $"Observed action {action.ActionEventId} evidence result");
                ValidateReferenceList(
                    action.ResultDescendantCaseIds,
                    index.Descendants,
                    $"Observed action {action.ActionEventId} descendant result");

                for (int i = 0; i < action.ResultEvidenceArtifactIds.Count; i++)
                {
                    EvidenceArtifact artifact = index.Evidence[
                        action.ResultEvidenceArtifactIds[i]];
                    Require(artifact.Provenance != null &&
                            artifact.Provenance.CreatedByAgentAction &&
                            OrdinalEquals(
                                artifact.Provenance.SourceSocietyEventId,
                                action.ActionEventId),
                        $"Action {action.ActionEventId} points to evidence that does not point back.");
                }

                for (int i = 0; i < action.ResultDescendantCaseIds.Count; i++)
                {
                    DescendantCase descendant = index.Descendants[
                        action.ResultDescendantCaseIds[i]];
                    Require(descendant.SourceActionEventIds != null &&
                            descendant.SourceActionEventIds.Contains(action.ActionEventId),
                        $"Action {action.ActionEventId} points to a descendant case that does not point back.");
                }
            }
        }

        private static void ValidateEvidence(ReportIndex index)
        {
            foreach (EvidenceArtifact artifact in index.Evidence.Values)
            {
                Require(index.Cases.Contains(artifact.CaseId),
                    $"Evidence {artifact.ArtifactId} references unknown case {artifact.CaseId}.");
                ValidateCycle(artifact.EnteredCycle, index.Report.FinalCycle,
                    $"Evidence {artifact.ArtifactId}");
                RequireId(artifact.IssueId, $"evidence {artifact.ArtifactId} issue");
                RequireId(artifact.PropositionId,
                    $"evidence {artifact.ArtifactId} proposition");
                Require(artifact.BaseWeight >= 0,
                    $"Evidence {artifact.ArtifactId} has a negative base weight.");
                Require(artifact.Reliability >= 0 && artifact.Reliability <= 100,
                    $"Evidence {artifact.ArtifactId} has invalid reliability.");
                Require(artifact.OfficiallySubmitted,
                    $"Reported evidence {artifact.ArtifactId} was not officially submitted.");
                Require(artifact.Provenance != null,
                    $"Evidence {artifact.ArtifactId} has no provenance.");
                RequireId(artifact.Provenance.ProvenanceId,
                    $"evidence {artifact.ArtifactId} provenance");
                Require(index.ProvenanceIds.Add(artifact.Provenance.ProvenanceId),
                    $"Duplicate evidence provenance id {artifact.Provenance.ProvenanceId}.");
                index.RegisterGlobal(
                    artifact.Provenance.ProvenanceId,
                    "evidence provenance");
                Require(artifact.Provenance.CreatedCycle >= 0 &&
                        artifact.Provenance.CreatedCycle <= artifact.EnteredCycle,
                    $"Evidence {artifact.ArtifactId} predates its provenance.");
                ValidateIdentifierList(
                    artifact.KnownByAgentIds,
                    $"evidence {artifact.ArtifactId} known-by agent");
                Require(artifact.KnownByAgentIds.Count > 0,
                    $"Evidence {artifact.ArtifactId} records no knower.");
                ValidateIdentifierList(
                    artifact.Provenance.ChainOfCustodyIds,
                    $"evidence {artifact.ArtifactId} chain-of-custody entry");

                if (index.Descendants.TryGetValue(
                        artifact.CaseId,
                        out DescendantCase evidenceCase))
                {
                    Require(
                        evidenceCase.OpenedCycle <= artifact.EnteredCycle ||
                        IsCaseOpeningTriggerEvidence(index, evidenceCase, artifact),
                        $"Evidence {artifact.ArtifactId} predates its case.");
                }
                if (index.CaseOpeningsByCase.TryGetValue(
                        artifact.CaseId,
                        out InstitutionalCaseOpening caseOpening))
                {
                    Require(
                        artifact.EnteredCycle > caseOpening.OpenedCycle ||
                        IsCaseOpeningTriggerEvidence(
                            index,
                            caseOpening,
                            artifact),
                        $"Evidence {artifact.ArtifactId} predates its evidence-activated case.");
                }

                if (artifact.Provenance.CreatedByAgentAction)
                {
                    Require(index.Actions.TryGetValue(
                            artifact.Provenance.SourceSocietyEventId,
                            out ObservedAgentAction source),
                        $"Evidence {artifact.ArtifactId} has no observed source action.");
                    Require(source.Cycle <= artifact.Provenance.CreatedCycle &&
                            artifact.Provenance.CreatedCycle <= artifact.EnteredCycle,
                        $"Evidence {artifact.ArtifactId} has invalid action chronology.");
                    Require(OrdinalEquals(
                            source.ActorId,
                            artifact.Provenance.SourceAgentId),
                        $"Evidence {artifact.ArtifactId} names the wrong source actor.");
                    Require(source.ResultEvidenceArtifactIds.Contains(artifact.ArtifactId),
                        $"Evidence {artifact.ArtifactId} is absent from its source action results.");
                    RequireId(artifact.Provenance.SourceDecisionId,
                        $"evidence {artifact.ArtifactId} source decision");
                }
            }
        }

        private static void ValidateCaseOpenings(ReportIndex index)
        {
            foreach (InstitutionalCaseOpening opening in index.CaseOpenings.Values)
            {
                RequireId(opening.CaseId,
                    $"case opening {opening.ActivationId} case");
                RequireId(opening.TriggerEvidenceArtifactId,
                    $"case opening {opening.ActivationId} trigger evidence");
                RequireId(opening.CausalAgentActionId,
                    $"case opening {opening.ActivationId} causal action");
                ValidateCycle(
                    opening.OpenedCycle,
                    index.Report.FinalCycle,
                    $"Case opening {opening.ActivationId}");
                Require(index.Evidence.TryGetValue(
                            opening.TriggerEvidenceArtifactId,
                            out EvidenceArtifact trigger),
                    $"Case opening {opening.ActivationId} has no trigger evidence.");
                Require(index.Actions.TryGetValue(
                            opening.CausalAgentActionId,
                            out ObservedAgentAction action),
                    $"Case opening {opening.ActivationId} has no causal action.");
                Require(OrdinalEquals(trigger.CaseId, opening.CaseId) &&
                        trigger.EnteredCycle <= opening.OpenedCycle &&
                        trigger.Provenance != null &&
                        trigger.Provenance.CreatedByAgentAction &&
                        OrdinalEquals(
                            trigger.Provenance.SourceSocietyEventId,
                            opening.CausalAgentActionId) &&
                        action.Cycle == trigger.EnteredCycle &&
                        CountOrdinal(
                            action.ResultEvidenceArtifactIds,
                            trigger.ArtifactId) == 1,
                    $"Case opening {opening.ActivationId} has an invalid " +
                    "evidence/action provenance envelope.");
                Require(!index.Descendants.ContainsKey(opening.CaseId),
                    $"Case {opening.CaseId} is both evidence-activated and descendant.");
            }
        }

        private static bool IsCaseOpeningTriggerEvidence(
            ReportIndex index,
            DescendantCase descendant,
            EvidenceArtifact artifact)
        {
            if (artifact?.Provenance == null || descendant == null ||
                !artifact.Provenance.CreatedByAgentAction ||
                !OrdinalEquals(
                    artifact.Provenance.SourceSocietyEventId,
                    descendant.CausalAgentActionId) ||
                CountOrdinal(
                    descendant.SourceActionEventIds,
                    artifact.Provenance.SourceSocietyEventId) != 1 ||
                !index.Actions.TryGetValue(
                    artifact.Provenance.SourceSocietyEventId,
                    out ObservedAgentAction sourceAction))
            {
                return false;
            }
            return sourceAction.Cycle == artifact.EnteredCycle &&
                   CountOrdinal(
                       sourceAction.ResultDescendantCaseIds,
                       descendant.CaseId) == 1 &&
                   CountOrdinal(
                       sourceAction.ResultEvidenceArtifactIds,
                       artifact.ArtifactId) == 1;
        }

        private static bool IsCaseOpeningTriggerEvidence(
            ReportIndex index,
            InstitutionalCaseOpening opening,
            EvidenceArtifact artifact)
        {
            if (artifact?.Provenance == null || opening == null ||
                !OrdinalEquals(
                    artifact.ArtifactId,
                    opening.TriggerEvidenceArtifactId) ||
                !OrdinalEquals(
                    artifact.Provenance.SourceSocietyEventId,
                    opening.CausalAgentActionId) ||
                !index.Actions.TryGetValue(
                    opening.CausalAgentActionId,
                    out ObservedAgentAction sourceAction))
            {
                return false;
            }
            return sourceAction.Cycle == artifact.EnteredCycle &&
                   CountOrdinal(
                       sourceAction.ResultEvidenceArtifactIds,
                       artifact.ArtifactId) == 1;
        }

        private static void ValidateFindings(ReportIndex index)
        {
            foreach (OfficialFinding finding in index.Findings.Values)
            {
                Require(index.Cases.Contains(finding.CaseId),
                    $"Finding {finding.FindingId} references unknown case {finding.CaseId}.");
                ValidateCaseCycle(index, finding.CaseId, finding.Cycle,
                    $"Finding {finding.FindingId}");
                RequireId(finding.IssueId, $"finding {finding.FindingId} issue");
                Require(Enum.IsDefined(typeof(FindingDisposition), finding.Disposition),
                    $"Finding {finding.FindingId} has an invalid disposition.");
                ValidateReferenceList(
                    finding.EvidenceArtifactIds,
                    index.Evidence,
                    $"Finding {finding.FindingId} evidence");
                for (int i = 0; i < finding.EvidenceArtifactIds.Count; i++)
                {
                    EvidenceArtifact artifact = index.Evidence[
                        finding.EvidenceArtifactIds[i]];
                    Require(OrdinalEquals(artifact.CaseId, finding.CaseId) &&
                            artifact.EnteredCycle <= finding.Cycle,
                        $"Finding {finding.FindingId} uses evidence outside its case or chronology.");
                }
            }
        }

        private static void ValidateRulings(ReportIndex index)
        {
            foreach (Ruling ruling in index.Rulings.Values)
            {
                Require(index.Cases.Contains(ruling.CaseId),
                    $"Ruling {ruling.RulingId} references unknown case {ruling.CaseId}.");
                ValidateCaseCycle(index, ruling.CaseId, ruling.Cycle,
                    $"Ruling {ruling.RulingId}");
                Require(Enum.IsDefined(typeof(RulingDisposition), ruling.Disposition),
                    $"Ruling {ruling.RulingId} has an invalid disposition.");
                Require(index.Findings.TryGetValue(
                        ruling.FindingId,
                        out OfficialFinding finding),
                    $"Ruling {ruling.RulingId} has no finding.");
                Require(OrdinalEquals(finding.CaseId, ruling.CaseId) &&
                        finding.Cycle == ruling.Cycle,
                    $"Ruling {ruling.RulingId} and its finding disagree on case or cycle.");
                Require(OrdinalEquals(
                        ruling.PolicyConfigurationId,
                        index.Report.PolicyConfigurationId),
                    $"Ruling {ruling.RulingId} records the wrong policy configuration.");
                RequireId(ruling.PolicyVersion,
                    $"ruling {ruling.RulingId} policy version");
                Require(ruling.ConfidenceMinimum <= ruling.ConfidenceMaximum &&
                        finding.WeightedEvidenceScore >= ruling.ConfidenceMinimum &&
                        finding.WeightedEvidenceScore <= ruling.ConfidenceMaximum,
                    $"Ruling {ruling.RulingId} has invalid evidence-score bounds.");

                ValidateReferenceList(
                    ruling.EvidenceArtifactIds,
                    index.Evidence,
                    $"Ruling {ruling.RulingId} evidence");
                Require(SameSet(
                        ruling.EvidenceArtifactIds,
                        finding.EvidenceArtifactIds),
                    $"Ruling {ruling.RulingId} and its finding have different evidence envelopes.");
                ValidateIdentifierList(
                    ruling.AppliedPolicyIds,
                    $"ruling {ruling.RulingId} applied policy");
                Require(ruling.AppliedPolicyIds.Count > 0 &&
                        ruling.AppliedPolicyIds.Contains(ruling.PolicyVersion),
                    $"Ruling {ruling.RulingId} did not freeze its policy version.");
                ValidateIdentifierList(
                    ruling.SkippedProcedureIds,
                    $"ruling {ruling.RulingId} skipped procedure");
                ValidateReferenceList(
                    ruling.OfficialStatusMutationIds,
                    index.Mutations,
                    $"Ruling {ruling.RulingId} mutation");
                ValidateReferenceList(
                    ruling.CitedHoldingIds,
                    index.Holdings,
                    $"Ruling {ruling.RulingId} cited holding");
                ValidateIdentifierList(
                    ruling.CitedScopeIds,
                    $"ruling {ruling.RulingId} cited scope");
                Require(ruling.CitedHoldingIds.Count == ruling.CitedScopeIds.Count,
                    $"Ruling {ruling.RulingId} did not freeze holding and scope together.");

                for (int i = 0; i < ruling.OfficialStatusMutationIds.Count; i++)
                {
                    OfficialStatusMutation mutation = index.Mutations[
                        ruling.OfficialStatusMutationIds[i]];
                    Require(OrdinalEquals(mutation.CauseId, ruling.RulingId),
                        $"Ruling {ruling.RulingId} points to a mutation that does not point back.");
                }

                for (int i = 0; i < ruling.CitedHoldingIds.Count; i++)
                {
                    Holding holding = index.Holdings[ruling.CitedHoldingIds[i]];
                    Require(holding.Scope != null &&
                            OrdinalEquals(
                                holding.Scope.ScopeId,
                                ruling.CitedScopeIds[i]) &&
                            OrdinalEquals(holding.IssueId, finding.IssueId),
                        $"Ruling {ruling.RulingId} has a mismatched holding citation.");
                    Require(holding.EstablishedCycle <= ruling.Cycle &&
                            !OrdinalEquals(holding.SourceRulingId, ruling.RulingId),
                        $"Ruling {ruling.RulingId} cites a future or self-authored holding.");
                    Require(CountOrdinal(holding.AppliedCaseIds, ruling.CaseId) == 1,
                        $"Ruling {ruling.RulingId} citation lacks one holding application backlink.");
                }
            }
        }

        private static void ValidateMutations(ReportIndex index)
        {
            foreach (OfficialStatusMutation mutation in index.Mutations.Values)
            {
                ValidateCycle(mutation.Cycle, index.Report.FinalCycle,
                    $"Official mutation {mutation.MutationId}");
                RequireId(mutation.AffectedAgentId,
                    $"official mutation {mutation.MutationId} agent");
                RequireId(mutation.StatusId,
                    $"official mutation {mutation.MutationId} status");
                Require(mutation.BeforeRecognised != mutation.AfterRecognised,
                    $"Official mutation {mutation.MutationId} records no state change.");
                Require(index.Rulings.TryGetValue(
                        mutation.CauseId,
                        out Ruling ruling),
                    $"Official mutation {mutation.MutationId} has no causal ruling.");
                Require(ruling.Cycle == mutation.Cycle,
                    $"Official mutation {mutation.MutationId} is not contemporaneous with its ruling.");
                Require(CountOrdinal(
                        ruling.OfficialStatusMutationIds,
                        mutation.MutationId) == 1,
                    $"Official mutation {mutation.MutationId} lacks one ruling backlink.");
            }
        }

        private static void ValidateDescendantCases(ReportIndex index)
        {
            foreach (DescendantCase descendant in index.Descendants.Values)
            {
                Require(!OrdinalEquals(descendant.CaseId, index.Report.PrimaryCaseId),
                    $"Descendant case {descendant.CaseId} reuses the primary case id.");
                Require(index.Cases.Contains(descendant.ParentCaseId) &&
                        !OrdinalEquals(descendant.CaseId, descendant.ParentCaseId),
                    $"Descendant case {descendant.CaseId} has an invalid parent case.");
                ValidateCycle(descendant.OpenedCycle, index.Report.FinalCycle,
                    $"Descendant case {descendant.CaseId}");
                if (index.Descendants.TryGetValue(
                        descendant.ParentCaseId,
                        out DescendantCase parent))
                {
                    Require(parent.OpenedCycle <= descendant.OpenedCycle,
                        $"Descendant case {descendant.CaseId} predates its parent case.");
                }
                Require(Enum.IsDefined(typeof(DescendantCaseKind), descendant.Kind) &&
                        Enum.IsDefined(typeof(DescendantCaseStatus), descendant.Status),
                    $"Descendant case {descendant.CaseId} has invalid state.");
                RequireId(descendant.ParentCauseId,
                    $"descendant case {descendant.CaseId} parent cause");
                RequireId(descendant.OriginatingEventId,
                    $"descendant case {descendant.CaseId} originating event");
                RequireId(descendant.OriginatingRulingId,
                    $"descendant case {descendant.CaseId} originating ruling");
                RequireId(descendant.CausalAgentActionId,
                    $"descendant case {descendant.CaseId} causal action");
                RequireId(descendant.ClaimantAgentId,
                    $"descendant case {descendant.CaseId} claimant");
                RequireId(descendant.RespondentId,
                    $"descendant case {descendant.CaseId} respondent");
                Require(!OrdinalEquals(
                        descendant.ClaimantAgentId,
                        descendant.RespondentId),
                    $"Descendant case {descendant.CaseId} binds one participant to both sides.");
                RequireId(descendant.OfficialIssueId,
                    $"descendant case {descendant.CaseId} issue");
                Require(descendant.Facts != null,
                    $"Descendant case {descendant.CaseId} has no case facts.");
                descendant.Facts.Validate();

                ValidateReferenceList(
                    descendant.SourceActionEventIds,
                    index.Actions,
                    $"Descendant case {descendant.CaseId} source action");
                Require(descendant.SourceActionEventIds.Count > 0,
                    $"Descendant case {descendant.CaseId} has no source action.");
                ValidateIdentifierList(
                    descendant.ConnectedAgentIds,
                    $"descendant case {descendant.CaseId} connected agent");
                ValidateReferenceList(
                    descendant.CitedHoldingIds,
                    index.Holdings,
                    $"Descendant case {descendant.CaseId} cited holding");

                Require(index.Actions.ContainsKey(descendant.OriginatingEventId),
                    $"Descendant case {descendant.CaseId} has no originating event.");
                Require(index.Actions.TryGetValue(
                        descendant.CausalAgentActionId,
                        out ObservedAgentAction causalAction),
                    $"Descendant case {descendant.CaseId} has no causal agent action.");
                Require(descendant.SourceActionEventIds.Contains(
                            descendant.OriginatingEventId) &&
                        descendant.SourceActionEventIds.Contains(
                            descendant.CausalAgentActionId),
                    $"Descendant case {descendant.CaseId} has an incomplete action envelope.");
                Require(causalAction.Cycle <= descendant.OpenedCycle &&
                        CountOrdinal(
                            causalAction.ResultDescendantCaseIds,
                            descendant.CaseId) == 1,
                    $"Descendant case {descendant.CaseId} has an invalid causal action backlink.");
                for (int i = 0; i < descendant.SourceActionEventIds.Count; i++)
                {
                    Require(index.Actions[descendant.SourceActionEventIds[i]].Cycle <=
                            descendant.OpenedCycle,
                        $"Descendant case {descendant.CaseId} predates a source action.");
                }

                Require(index.Rulings.TryGetValue(
                        descendant.OriginatingRulingId,
                        out Ruling originatingRuling) &&
                        OrdinalEquals(
                            originatingRuling.CaseId,
                            descendant.ParentCaseId) &&
                        originatingRuling.Cycle <= descendant.OpenedCycle,
                    $"Descendant case {descendant.CaseId} has an invalid originating ruling.");
                if (descendant.Kind == DescendantCaseKind.Reliance)
                {
                    Require(OrdinalEquals(
                                descendant.ParentCauseId,
                                descendant.OriginatingRulingId) &&
                            OrdinalEquals(
                                descendant.OriginatingEventId,
                                descendant.CausalAgentActionId) &&
                            descendant.SourceActionEventIds.Count == 1 &&
                            OrdinalEquals(
                                descendant.SourceActionEventIds[0],
                                descendant.CausalAgentActionId) &&
                            originatingRuling.Cycle == descendant.OpenedCycle &&
                            (originatingRuling.Disposition ==
                                 RulingDisposition.ReversedAndDenied ||
                             originatingRuling.Disposition ==
                                 RulingDisposition.ReversedAndRecognised),
                        $"Reliance descendant {descendant.CaseId} lacks its exact " +
                        "reversal and source-action envelope.");
                }
                Require(TryResolveCauseCycle(
                            index,
                            descendant.ParentCauseId,
                            out long parentCauseCycle) &&
                        parentCauseCycle <= descendant.OpenedCycle,
                    $"Descendant case {descendant.CaseId} has an unresolved or future parent cause.");

                for (int i = 0; i < descendant.CitedHoldingIds.Count; i++)
                {
                    Holding holding = index.Holdings[descendant.CitedHoldingIds[i]];
                    Require(CountOrdinal(holding.AppliedCaseIds, descendant.CaseId) == 1,
                        $"Descendant case {descendant.CaseId} citation lacks a holding backlink.");
                }
            }

            foreach (DescendantCase descendant in index.Descendants.Values)
                AssertAcyclicCaseParentage(index, descendant);
        }

        private static void ValidateAppeals(ReportIndex index)
        {
            foreach (Appeal appeal in index.Appeals.Values)
            {
                Require(index.Cases.Contains(appeal.CaseId),
                    $"Appeal {appeal.AppealId} references unknown case {appeal.CaseId}.");
                ValidateCaseCycle(index, appeal.CaseId, appeal.FiledCycle,
                    $"Appeal {appeal.AppealId}");
                Require(appeal.HearingCycle >= appeal.FiledCycle,
                    $"Appeal {appeal.AppealId} has a hearing before filing.");
                RequireId(appeal.AppellantAgentId,
                    $"appeal {appeal.AppealId} appellant");
                Require(index.Actions.TryGetValue(
                        appeal.FilingActionEventId,
                        out ObservedAgentAction filingAction) &&
                        filingAction.Activity == ObservedActivityKind.AppealFiled &&
                        OrdinalEquals(
                            filingAction.ActorId,
                            appeal.AppellantAgentId) &&
                        filingAction.Cycle == appeal.FiledCycle,
                    $"Appeal {appeal.AppealId} has an invalid filing action.");
                Require(index.Rulings.TryGetValue(
                        appeal.ChallengedRulingId,
                        out Ruling challenged) &&
                        OrdinalEquals(challenged.CaseId, appeal.CaseId) &&
                        challenged.Cycle < appeal.FiledCycle,
                    $"Appeal {appeal.AppealId} has an invalid challenged ruling.");
                Require(Enum.IsDefined(typeof(AppealDisposition), appeal.Disposition),
                    $"Appeal {appeal.AppealId} has an invalid disposition.");

                ValidateReferenceList(
                    appeal.GroundsEvidenceArtifactIds,
                    index.Evidence,
                    $"Appeal {appeal.AppealId} grounds");
                Require(appeal.GroundsEvidenceArtifactIds.Count > 0,
                    $"Appeal {appeal.AppealId} has no declared grounds evidence.");
                for (int groundIndex = 0;
                     groundIndex < appeal.GroundsEvidenceArtifactIds.Count;
                     groundIndex++)
                {
                    EvidenceArtifact ground = index.Evidence[
                        appeal.GroundsEvidenceArtifactIds[groundIndex]];
                    Require(OrdinalEquals(ground.CaseId, appeal.CaseId) &&
                            ground.EnteredCycle <= appeal.FiledCycle,
                        $"Appeal {appeal.AppealId} contains evidence outside its " +
                        "filing-cycle case envelope.");
                }

                if (appeal.Disposition == AppealDisposition.Pending)
                {
                    Require(string.IsNullOrWhiteSpace(appeal.ResultingRulingId),
                        $"Pending appeal {appeal.AppealId} already names a resulting ruling.");
                    Require(CountTimeline(
                            index.Report,
                            InstitutionalTimelineKind.AppealHeard,
                            null,
                            appeal.AppealId,
                            null,
                            null) == 0,
                        $"Pending appeal {appeal.AppealId} already has a hearing timeline entry.");
                    continue;
                }

                Require(index.Rulings.TryGetValue(
                        appeal.ResultingRulingId,
                        out Ruling resulting) &&
                        OrdinalEquals(resulting.CaseId, appeal.CaseId) &&
                        resulting.Cycle >= appeal.HearingCycle &&
                        resulting.Cycle > appeal.FiledCycle &&
                        resulting.Cycle > challenged.Cycle,
                    $"Appeal {appeal.AppealId} has an invalid resulting ruling.");
                if (appeal.Disposition == AppealDisposition.Affirmed)
                {
                    Require(resulting.Disposition == RulingDisposition.Affirmed,
                        $"Appeal {appeal.AppealId} and its ruling disagree on affirmation.");
                }
                else
                {
                    Require(resulting.Disposition == RulingDisposition.ReversedAndDenied ||
                            resulting.Disposition == RulingDisposition.ReversedAndRecognised,
                        $"Appeal {appeal.AppealId} and its ruling disagree on reversal.");
                }
            }
        }

        private static void ValidateHoldings(ReportIndex index)
        {
            foreach (Holding holding in index.Holdings.Values)
            {
                RequireId(holding.RuleId, $"holding {holding.HoldingId} rule");
                RequireId(holding.IssueId, $"holding {holding.HoldingId} issue");
                Require(index.Appeals.TryGetValue(
                        holding.SourceAppealId,
                        out Appeal appeal) &&
                        appeal.Disposition == AppealDisposition.Reversed,
                    $"Holding {holding.HoldingId} has no reversed source appeal.");
                Require(index.Rulings.TryGetValue(
                        holding.SourceRulingId,
                        out Ruling ruling) &&
                        OrdinalEquals(
                            appeal.ResultingRulingId,
                            holding.SourceRulingId) &&
                        ruling.Disposition == RulingDisposition.ReversedAndRecognised &&
                        ruling.Cycle == holding.EstablishedCycle,
                    $"Holding {holding.HoldingId} has an invalid source ruling.");
                OfficialFinding sourceFinding = index.Findings[ruling.FindingId];
                Require(OrdinalEquals(sourceFinding.IssueId, holding.IssueId),
                    $"Holding {holding.HoldingId} has the wrong issue.");
                ValidateReferenceList(
                    holding.SupportingEvidenceArtifactIds,
                    index.Evidence,
                    $"Holding {holding.HoldingId} supporting evidence");
                Require(holding.SupportingEvidenceArtifactIds.Count > 0,
                    $"Holding {holding.HoldingId} has no supporting evidence.");
                for (int supportIndex = 0;
                     supportIndex < holding.SupportingEvidenceArtifactIds.Count;
                     supportIndex++)
                {
                    Require(ruling.EvidenceArtifactIds.Contains(
                            holding.SupportingEvidenceArtifactIds[supportIndex]),
                        $"Holding {holding.HoldingId} cites evidence not used by its " +
                        "source ruling.");
                }
                ValidateScope(index, holding);
                ValidateReferenceList(
                    holding.AppliedCaseIds,
                    index.Cases,
                    $"Holding {holding.HoldingId} applied case");

                for (int i = 0; i < holding.AppliedCaseIds.Count; i++)
                {
                    string caseId = holding.AppliedCaseIds[i];
                    int citingRulingCount = 0;
                    foreach (Ruling candidate in index.Rulings.Values)
                    {
                        if (OrdinalEquals(candidate.CaseId, caseId) &&
                            candidate.CitedHoldingIds.Contains(holding.HoldingId))
                        {
                            citingRulingCount++;
                            Require(candidate.Cycle >= holding.EstablishedCycle,
                                $"Holding {holding.HoldingId} was applied before it existed.");
                        }
                    }
                    Require(citingRulingCount == 1,
                        $"Holding {holding.HoldingId} applied case {caseId} lacks one citing ruling.");

                    if (index.Descendants.TryGetValue(caseId, out DescendantCase descendant))
                    {
                        Require(descendant.CitedHoldingIds.Contains(holding.HoldingId),
                            $"Holding {holding.HoldingId} applied descendant case {caseId} without a case backlink.");
                        Require(holding.Scope.AppliesTo(descendant.Facts),
                            $"Holding {holding.HoldingId} was applied outside its fact scope.");
                    }
                }
            }
        }

        private static void ValidateRelianceObservations(ReportIndex index)
        {
            foreach (RelianceObservation reliance in index.Reliance.Values)
            {
                ValidateCycle(reliance.Cycle, index.Report.FinalCycle,
                    $"Reliance observation {reliance.ObservationId}");
                RequireId(reliance.AgentId,
                    $"reliance observation {reliance.ObservationId} agent");
                RequireId(reliance.RecordedChoiceId,
                    $"reliance observation {reliance.ObservationId} choice");
                RequireId(reliance.AbandonedAlternativeId,
                    $"reliance observation {reliance.ObservationId} alternative");
                if (reliance.RecordedResourceDelta != 0)
                    RequireId(reliance.ResourceId,
                        $"reliance observation {reliance.ObservationId} resource");
                Require(reliance.RecordedResourceDelta < 0,
                    $"Reliance observation {reliance.ObservationId} records no irreversible actor cost.");
                Require(index.Actions.TryGetValue(
                        reliance.SourceActionEventId,
                        out ObservedAgentAction action) &&
                        OrdinalEquals(action.ActorId, reliance.AgentId) &&
                        action.Cycle <= reliance.Cycle,
                    $"Reliance observation {reliance.ObservationId} has an invalid source action.");
                Require(index.Rulings.TryGetValue(
                        reliance.EnablingRulingId,
                        out Ruling ruling) &&
                        ruling.Cycle < action.Cycle,
                    $"Reliance observation {reliance.ObservationId} has an invalid enabling ruling.");
                Require(index.Mutations.TryGetValue(
                        reliance.EnablingMutationId,
                        out OfficialStatusMutation mutation) &&
                        OrdinalEquals(mutation.CauseId, ruling.RulingId) &&
                        OrdinalEquals(mutation.AffectedAgentId, reliance.AgentId) &&
                        mutation.Cycle == ruling.Cycle,
                    $"Reliance observation {reliance.ObservationId} has an invalid enabling mutation.");
            }
        }

        private static void ValidateMaterialConsequences(ReportIndex index)
        {
            foreach (MaterialConsequence consequence in index.Material.Values)
            {
                ValidateCycle(consequence.Cycle, index.Report.FinalCycle,
                    $"Material consequence {consequence.ConsequenceId}");
                RequireId(consequence.CauseId,
                    $"material consequence {consequence.ConsequenceId} cause");
                RequireId(consequence.AgentId,
                    $"material consequence {consequence.ConsequenceId} agent");
                Require(Enum.IsDefined(typeof(MaterialConsequenceKind), consequence.Kind),
                    $"Material consequence {consequence.ConsequenceId} has an invalid kind.");
                RequireId(consequence.KindId,
                    $"material consequence {consequence.ConsequenceId} kind");
                if (consequence.ResourceDelta != 0)
                    RequireId(consequence.ResourceId,
                        $"material consequence {consequence.ConsequenceId} resource");
                Require(consequence.HasNeedEffect
                        ? Enum.IsDefined(typeof(NeedKind), consequence.Need) &&
                          consequence.NeedPressureBefore >= 0 &&
                          consequence.NeedPressureBefore <= 100 &&
                          consequence.NeedPressureAfter >= 0 &&
                          consequence.NeedPressureAfter <= 100
                        : consequence.NeedPressureBefore == 0 &&
                          consequence.NeedPressureAfter == 0,
                    $"Material consequence {consequence.ConsequenceId} has an " +
                    "invalid need projection.");
                Require(TryResolveCauseCycle(
                            index,
                            consequence.CauseId,
                            out long causeCycle) &&
                        causeCycle <= consequence.Cycle,
                    $"Material consequence {consequence.ConsequenceId} has an unresolved or future cause.");
            }
        }

        private static void ValidateConnectedOutcomes(ReportIndex index)
        {
            foreach (ConnectedOutcomePair pair in index.ConnectedOutcomes.Values)
            {
                RequireId(pair.CauseRuleId,
                    $"connected outcome {pair.PairId} cause rule");
                RequireId(pair.ConnectionId,
                    $"connected outcome {pair.PairId} connection");
                RequireId(pair.WinnerAgentId,
                    $"connected outcome {pair.PairId} winner");
                RequireId(pair.LoserAgentId,
                    $"connected outcome {pair.PairId} loser");
                Require(!OrdinalEquals(pair.WinnerAgentId, pair.LoserAgentId) &&
                        pair.WinnerResourceDelta > 0 &&
                        pair.LoserResourceDelta < 0 &&
                        pair.WinnerResourceDelta + pair.LoserResourceDelta == 0,
                    $"Connected outcome {pair.PairId} is not a conserved two-party transfer.");
                Require(HasKnownConnection(index, pair.ConnectionId),
                    $"Connected outcome {pair.PairId} has no known connection.");
                Require(index.Holdings.Values.Exists(value =>
                        OrdinalEquals(value.RuleId, pair.CauseRuleId)),
                    $"Connected outcome {pair.PairId} has no holding for its cause rule.");

                MaterialConsequence gain = FindUniqueMaterial(
                    index,
                    pair.WinnerAgentId,
                    pair.WinnerResourceDelta,
                    out int gainCount);
                MaterialConsequence loss = FindUniqueMaterial(
                    index,
                    pair.LoserAgentId,
                    pair.LoserResourceDelta,
                    out int lossCount);
                Require(gainCount == 1 && lossCount == 1 &&
                        OrdinalEquals(gain.CauseId, loss.CauseId),
                    $"Connected outcome {pair.PairId} lacks one paired material transfer.");
            }
        }

        private static bool HasKnownConnection(
            ReportIndex index,
            string connectionId)
        {
            if (index.WorkAllocations.ContainsKey(connectionId) ||
                index.Entitlements.ContainsKey(connectionId))
            {
                return true;
            }
            foreach (ExclusiveEntitlementObservation entitlement in
                     index.Entitlements.Values)
            {
                if (OrdinalEquals(entitlement.ResourceId, connectionId))
                    return true;
            }
            return false;
        }

        private static void ValidatePublicEntitlementRecords(ReportIndex index)
        {
            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            var holderStatusIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExclusiveEntitlementObservation observation in
                     index.Entitlements.Values)
            {
                RequireId(observation.ResourceId,
                    $"entitlement {observation.EntitlementId} resource");
                RequireId(observation.HolderStatusId,
                    $"entitlement {observation.EntitlementId} holder status");
                Require(resourceIds.Add(observation.ResourceId),
                    $"Duplicate exclusive entitlement resource {observation.ResourceId}.");
                Require(holderStatusIds.Add(observation.HolderStatusId),
                    $"Duplicate exclusive entitlement holder status {observation.HolderStatusId}.");
                Require(observation.ConservedAmount > 0 &&
                        observation.ConservedAmount <=
                            ExclusiveEntitlementService.MaximumConservedAmount,
                    $"Entitlement {observation.EntitlementId} has an invalid conserved amount.");
                if (!string.IsNullOrWhiteSpace(observation.CurrentHolderAgentId))
                    RequireId(observation.CurrentHolderAgentId,
                        $"entitlement {observation.EntitlementId} holder");
                if (!string.IsNullOrWhiteSpace(observation.LastMutationCauseId))
                {
                    Require(TryResolveCauseCycle(
                            index,
                            observation.LastMutationCauseId,
                            out _),
                        $"Entitlement {observation.EntitlementId} has an unresolved last mutation cause.");
                }
            }
        }

        private static void ValidateWorkAllocationRecords(ReportIndex index)
        {
            foreach (WorkAllocationObservation allocation in index.WorkAllocations.Values)
            {
                RequireId(allocation.EmployerId,
                    $"work allocation {allocation.AllocationId} employer");
                RequireId(allocation.OriginalWorkerId,
                    $"work allocation {allocation.AllocationId} original worker");
                RequireId(allocation.PaidHolderAgentId,
                    $"work allocation {allocation.AllocationId} paid holder");
                RequireId(allocation.IdentityConditionId,
                    $"work allocation {allocation.AllocationId} identity condition");
                Require(allocation.CommittedWage > 0,
                    $"Work allocation {allocation.AllocationId} has no committed resource.");
                RequireId(allocation.LastMutationCauseId,
                    $"work allocation {allocation.AllocationId} mutation cause");
                Require(TryResolveCauseCycle(
                        index,
                        allocation.LastMutationCauseId,
                        out _),
                    $"Work allocation {allocation.AllocationId} has an unresolved mutation cause.");
            }
        }

        private static void ValidateTimeline(ReportIndex index)
        {
            long previousCycle = -1;
            for (int i = 0; i < index.Report.Timeline.Count; i++)
            {
                InstitutionalTimelineEntry entry = index.Report.Timeline[i];
                ValidateCycle(entry.Cycle, index.Report.FinalCycle,
                    $"Timeline entry {entry.EntryId}");
                Require(entry.Cycle >= previousCycle,
                    $"Timeline regressed from cycle {previousCycle} to {entry.Cycle}.");
                previousCycle = entry.Cycle;
                Require(Enum.IsDefined(typeof(InstitutionalTimelineKind), entry.Kind),
                    $"Timeline entry {entry.EntryId} has an invalid kind.");
                RequireId(entry.CauseId, $"timeline entry {entry.EntryId} cause");
                RequireId(entry.SubjectId, $"timeline entry {entry.EntryId} subject");
                RequireId(entry.DetailId, $"timeline entry {entry.EntryId} detail");
                ValidateTimelineReference(index, entry);
            }

            foreach (EvidenceArtifact artifact in index.Evidence.Values)
            {
                Require(CountTimeline(
                        index.Report,
                        InstitutionalTimelineKind.EvidenceEntered,
                        artifact.EnteredCycle,
                        artifact.Provenance.SourceSocietyEventId,
                        artifact.Provenance.SourceAgentId,
                        artifact.ArtifactId) == 1,
                    $"Evidence {artifact.ArtifactId} lacks one timeline projection.");
            }
            foreach (Ruling ruling in index.Rulings.Values)
            {
                Require(CountTimeline(
                        index.Report,
                        InstitutionalTimelineKind.RulingIssued,
                        ruling.Cycle,
                        ruling.RulingId,
                        ruling.CaseId,
                        ruling.Disposition.ToString()) == 1,
                    $"Ruling {ruling.RulingId} lacks one timeline projection.");
            }
            foreach (OfficialStatusMutation mutation in index.Mutations.Values)
            {
                Require(CountTimeline(
                        index.Report,
                        InstitutionalTimelineKind.StatusMutated,
                        mutation.Cycle,
                        mutation.CauseId,
                        mutation.AffectedAgentId,
                        mutation.StatusId) == 1,
                    $"Mutation {mutation.MutationId} lacks one timeline projection.");
            }
            foreach (RelianceObservation reliance in index.Reliance.Values)
            {
                Require(CountTimeline(
                        index.Report,
                        InstitutionalTimelineKind.RelianceCreated,
                        reliance.Cycle,
                        reliance.SourceActionEventId,
                        reliance.AgentId,
                        reliance.ObservationId) == 1,
                    $"Reliance {reliance.ObservationId} lacks one timeline projection.");
            }
            foreach (Appeal appeal in index.Appeals.Values)
            {
                Require(CountTimeline(
                        index.Report,
                        InstitutionalTimelineKind.AppealFiled,
                        appeal.FiledCycle,
                        appeal.FilingActionEventId,
                        appeal.AppellantAgentId,
                        appeal.AppealId) == 1,
                    $"Appeal {appeal.AppealId} lacks one filing timeline projection.");
                if (appeal.Disposition != AppealDisposition.Pending)
                {
                    Ruling result = index.Rulings[appeal.ResultingRulingId];
                    Require(CountTimeline(
                            index.Report,
                            InstitutionalTimelineKind.AppealHeard,
                            result.Cycle,
                            appeal.AppealId,
                            appeal.CaseId,
                            result.RulingId) == 1,
                        $"Appeal {appeal.AppealId} lacks one hearing timeline projection.");
                }
            }
            foreach (Holding holding in index.Holdings.Values)
            {
                Require(CountTimeline(
                        index.Report,
                        InstitutionalTimelineKind.HoldingEstablished,
                        holding.EstablishedCycle,
                        holding.SourceRulingId,
                        holding.HoldingId,
                        holding.RuleId) == 1,
                    $"Holding {holding.HoldingId} lacks one establishment timeline projection.");
                for (int i = 0; i < holding.AppliedCaseIds.Count; i++)
                {
                    string caseId = holding.AppliedCaseIds[i];
                    Ruling target = FindUniqueCitingRuling(
                        index,
                        holding.HoldingId,
                        caseId);
                    Require(CountTimeline(
                            index.Report,
                            InstitutionalTimelineKind.PrecedentApplied,
                            target.Cycle,
                            holding.HoldingId,
                            caseId,
                            target.RulingId) == 1,
                        $"Holding {holding.HoldingId} application to {caseId} lacks one timeline projection.");
                }
            }
            foreach (DescendantCase descendant in index.Descendants.Values)
            {
                Require(CountTimeline(
                        index.Report,
                        InstitutionalTimelineKind.DescendantCaseOpened,
                        descendant.OpenedCycle,
                        descendant.ParentCauseId,
                        descendant.CaseId,
                        descendant.Kind.ToString()) == 1,
                    $"Descendant case {descendant.CaseId} lacks one opening timeline projection.");
            }
            foreach (InstitutionalCaseOpening opening in index.CaseOpenings.Values)
            {
                Require(CountTimeline(
                        index.Report,
                        InstitutionalTimelineKind.CaseOpened,
                        opening.OpenedCycle,
                        opening.TriggerEvidenceArtifactId,
                        opening.CaseId,
                        opening.ActivationId) == 1,
                    $"Case opening {opening.ActivationId} lacks one timeline projection.");
                int evidenceTimelineIndex = -1;
                int openingTimelineIndex = -1;
                for (int timelineIndex = 0;
                     timelineIndex < index.Report.Timeline.Count;
                     timelineIndex++)
                {
                    InstitutionalTimelineEntry entry =
                        index.Report.Timeline[timelineIndex];
                    if (entry.Kind == InstitutionalTimelineKind.EvidenceEntered &&
                        OrdinalEquals(
                            entry.DetailId,
                            opening.TriggerEvidenceArtifactId))
                    {
                        evidenceTimelineIndex = timelineIndex;
                    }
                    if (entry.Kind == InstitutionalTimelineKind.CaseOpened &&
                        OrdinalEquals(entry.DetailId, opening.ActivationId))
                    {
                        openingTimelineIndex = timelineIndex;
                    }
                }
                Require(evidenceTimelineIndex >= 0 &&
                        openingTimelineIndex > evidenceTimelineIndex,
                    $"Case opening {opening.ActivationId} does not follow its trigger " +
                    "evidence in the public timeline.");
                foreach (Ruling ruling in index.Rulings.Values)
                {
                    if (ruling.Cycle != opening.OpenedCycle ||
                        !OrdinalEquals(ruling.CaseId, opening.CaseId))
                    {
                        continue;
                    }

                    int rulingTimelineIndex = -1;
                    for (int timelineIndex = 0;
                         timelineIndex < index.Report.Timeline.Count;
                         timelineIndex++)
                    {
                        InstitutionalTimelineEntry entry =
                            index.Report.Timeline[timelineIndex];
                        if (entry.Kind == InstitutionalTimelineKind.RulingIssued &&
                            OrdinalEquals(entry.CauseId, ruling.RulingId))
                        {
                            rulingTimelineIndex = timelineIndex;
                            break;
                        }
                    }
                    Require(rulingTimelineIndex > openingTimelineIndex,
                        $"Ruling {ruling.RulingId} does not follow case opening " +
                        $"{opening.ActivationId} in the public timeline.");
                }
            }
        }

        private static void ValidateTimelineReference(
            ReportIndex index,
            InstitutionalTimelineEntry entry)
        {
            switch (entry.Kind)
            {
                case InstitutionalTimelineKind.Incident:
                    return;
                case InstitutionalTimelineKind.EvidenceEntered:
                    Require(index.Evidence.TryGetValue(entry.DetailId, out EvidenceArtifact evidence) &&
                            evidence.EnteredCycle == entry.Cycle &&
                            OrdinalEquals(
                                evidence.Provenance.SourceSocietyEventId,
                                entry.CauseId) &&
                            OrdinalEquals(
                                evidence.Provenance.SourceAgentId,
                                entry.SubjectId),
                        $"Timeline entry {entry.EntryId} has an invalid evidence reference.");
                    return;
                case InstitutionalTimelineKind.RulingIssued:
                    Require(index.Rulings.TryGetValue(entry.CauseId, out Ruling ruling) &&
                            ruling.Cycle == entry.Cycle &&
                            OrdinalEquals(ruling.CaseId, entry.SubjectId) &&
                            OrdinalEquals(ruling.Disposition.ToString(), entry.DetailId),
                        $"Timeline entry {entry.EntryId} has an invalid ruling reference.");
                    return;
                case InstitutionalTimelineKind.StatusMutated:
                    int mutationMatches = 0;
                    foreach (OfficialStatusMutation mutation in index.Mutations.Values)
                    {
                        if (mutation.Cycle == entry.Cycle &&
                            OrdinalEquals(mutation.CauseId, entry.CauseId) &&
                            OrdinalEquals(mutation.AffectedAgentId, entry.SubjectId) &&
                            OrdinalEquals(mutation.StatusId, entry.DetailId))
                            mutationMatches++;
                    }
                    Require(mutationMatches == 1,
                        $"Timeline entry {entry.EntryId} has an invalid mutation reference.");
                    return;
                case InstitutionalTimelineKind.RelianceCreated:
                    Require(index.Reliance.TryGetValue(
                                entry.DetailId,
                                out RelianceObservation reliance) &&
                            reliance.Cycle == entry.Cycle &&
                            OrdinalEquals(
                                reliance.SourceActionEventId,
                                entry.CauseId) &&
                            OrdinalEquals(reliance.AgentId, entry.SubjectId),
                        $"Timeline entry {entry.EntryId} has an invalid reliance reference.");
                    return;
                case InstitutionalTimelineKind.EmployerResponded:
                    Require(index.Actions.TryGetValue(
                                entry.CauseId,
                                out ObservedAgentAction response) &&
                            response.Cycle == entry.Cycle,
                        $"Timeline entry {entry.EntryId} has an invalid response action reference.");
                    return;
                case InstitutionalTimelineKind.AppealFiled:
                    Require(index.Appeals.TryGetValue(entry.DetailId, out Appeal filed) &&
                            filed.FiledCycle == entry.Cycle &&
                            OrdinalEquals(filed.FilingActionEventId, entry.CauseId) &&
                            OrdinalEquals(filed.AppellantAgentId, entry.SubjectId),
                        $"Timeline entry {entry.EntryId} has an invalid appeal filing reference.");
                    return;
                case InstitutionalTimelineKind.AppealHeard:
                    Require(index.Appeals.TryGetValue(entry.CauseId, out Appeal heard) &&
                            index.Rulings.TryGetValue(entry.DetailId, out Ruling result) &&
                            OrdinalEquals(heard.ResultingRulingId, result.RulingId) &&
                            result.Cycle == entry.Cycle &&
                            OrdinalEquals(heard.CaseId, entry.SubjectId),
                        $"Timeline entry {entry.EntryId} has an invalid appeal hearing reference.");
                    return;
                case InstitutionalTimelineKind.HoldingEstablished:
                    Require(index.Holdings.TryGetValue(entry.SubjectId, out Holding holding) &&
                            holding.EstablishedCycle == entry.Cycle &&
                            OrdinalEquals(holding.SourceRulingId, entry.CauseId) &&
                            OrdinalEquals(holding.RuleId, entry.DetailId),
                        $"Timeline entry {entry.EntryId} has an invalid holding reference.");
                    return;
                case InstitutionalTimelineKind.PrecedentApplied:
                    Require(index.Holdings.TryGetValue(entry.CauseId, out Holding precedent) &&
                            index.Rulings.TryGetValue(entry.DetailId, out Ruling target) &&
                            target.Cycle == entry.Cycle &&
                            OrdinalEquals(target.CaseId, entry.SubjectId) &&
                            target.CitedHoldingIds.Contains(precedent.HoldingId) &&
                            precedent.AppliedCaseIds.Contains(entry.SubjectId),
                        $"Timeline entry {entry.EntryId} has an invalid precedent reference.");
                    return;
                case InstitutionalTimelineKind.CaseOpened:
                    Require(index.CaseOpenings.TryGetValue(
                                entry.DetailId,
                                out InstitutionalCaseOpening opening) &&
                            opening.OpenedCycle == entry.Cycle &&
                            OrdinalEquals(
                                opening.TriggerEvidenceArtifactId,
                                entry.CauseId) &&
                            OrdinalEquals(opening.CaseId, entry.SubjectId),
                        $"Timeline entry {entry.EntryId} has an invalid case-opening reference.");
                    return;
                case InstitutionalTimelineKind.DescendantCaseOpened:
                    Require(index.Descendants.TryGetValue(
                                entry.SubjectId,
                                out DescendantCase descendant) &&
                            descendant.OpenedCycle == entry.Cycle &&
                            OrdinalEquals(descendant.ParentCauseId, entry.CauseId) &&
                            OrdinalEquals(descendant.Kind.ToString(), entry.DetailId),
                        $"Timeline entry {entry.EntryId} has an invalid descendant-case reference.");
                    return;
                case InstitutionalTimelineKind.ComparisonClosed:
                    Require(index.Cases.Contains(entry.SubjectId),
                        $"Timeline entry {entry.EntryId} closes an unknown case comparison.");
                    return;
                default:
                    throw new InvalidOperationException(
                        $"Timeline entry {entry.EntryId} uses unsupported kind {entry.Kind}.");
            }
        }

        private static void ValidateAgents(
            InstitutionalConsequenceRun run,
            ReportIndex index)
        {
            var agentIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < run.FinalSocietyState.Agents.Count; i++)
                agentIds.Add(run.FinalSocietyState.Agents[i].StableId);

            foreach (ObservedAgentAction action in index.Actions.Values)
                RequireAgent(agentIds, action.ActorId, $"action {action.ActionEventId}");
            foreach (EvidenceArtifact artifact in index.Evidence.Values)
            {
                for (int i = 0; i < artifact.KnownByAgentIds.Count; i++)
                    RequireAgent(agentIds, artifact.KnownByAgentIds[i],
                        $"evidence {artifact.ArtifactId} knower");
                if (!string.IsNullOrWhiteSpace(artifact.SuppressedByAgentId))
                    RequireAgent(agentIds, artifact.SuppressedByAgentId,
                        $"evidence {artifact.ArtifactId} suppressor");
                if (!string.IsNullOrWhiteSpace(artifact.Provenance.SourceAgentId))
                    RequireAgent(agentIds, artifact.Provenance.SourceAgentId,
                        $"evidence {artifact.ArtifactId} source");
            }
            foreach (OfficialStatusMutation mutation in index.Mutations.Values)
                RequireAgent(agentIds, mutation.AffectedAgentId,
                    $"mutation {mutation.MutationId}");
            foreach (DescendantCase descendant in index.Descendants.Values)
            {
                RequireAgent(agentIds, descendant.ClaimantAgentId,
                    $"case {descendant.CaseId} claimant");
                for (int i = 0; i < descendant.ConnectedAgentIds.Count; i++)
                    RequireAgent(agentIds, descendant.ConnectedAgentIds[i],
                        $"case {descendant.CaseId} connected agent");
            }
            foreach (Appeal appeal in index.Appeals.Values)
                RequireAgent(agentIds, appeal.AppellantAgentId,
                    $"appeal {appeal.AppealId}");
            foreach (RelianceObservation reliance in index.Reliance.Values)
                RequireAgent(agentIds, reliance.AgentId,
                    $"reliance {reliance.ObservationId}");
            foreach (MaterialConsequence material in index.Material.Values)
                RequireAgent(agentIds, material.AgentId,
                    $"material consequence {material.ConsequenceId}");
            foreach (ExclusiveEntitlementObservation entitlement in
                     index.Entitlements.Values)
            {
                if (!string.IsNullOrWhiteSpace(entitlement.CurrentHolderAgentId))
                    RequireAgent(agentIds, entitlement.CurrentHolderAgentId,
                        $"entitlement {entitlement.EntitlementId} holder");
            }
            foreach (ConnectedOutcomePair pair in index.ConnectedOutcomes.Values)
            {
                RequireAgent(agentIds, pair.WinnerAgentId,
                    $"connected outcome {pair.PairId} winner");
                RequireAgent(agentIds, pair.LoserAgentId,
                    $"connected outcome {pair.PairId} loser");
            }
            foreach (WorkAllocationObservation allocation in
                     index.WorkAllocations.Values)
            {
                RequireAgent(agentIds, allocation.OriginalWorkerId,
                    $"work allocation {allocation.AllocationId} original worker");
                RequireAgent(agentIds, allocation.PaidHolderAgentId,
                    $"work allocation {allocation.AllocationId} paid holder");
            }
        }

        private static void ValidateAuthorityProjection(
            InstitutionalConsequenceRun run,
            ReportIndex index)
        {
            Require(run.AuthoritativeEvents != null &&
                    run.AuthoritativeEvidenceLinks != null &&
                    run.AuthoritativeBeliefLinks != null &&
                    run.AssessorActionTraces != null &&
                    run.RelianceLedger != null &&
                    run.PendingReliancePublicProjections != null &&
                    run.EconomicAccounts != null &&
                    run.AlternativeOptions != null &&
                    run.WorkAllocations != null,
                "Scenario run has an uninitialised authority collection.");
            Require(run.PendingReliancePublicProjections.Count == 0,
                "A completed scenario run retains unpublished reliance projections.");

            Dictionary<string, LivedEvent> livedEvents = UniqueMap(
                run.AuthoritativeEvents,
                value => value.LivedEventId,
                "authoritative lived event");
            foreach (LivedEvent lived in livedEvents.Values)
            {
                ValidateCycle(lived.Cycle, index.Report.FinalCycle,
                    $"Lived event {lived.LivedEventId}");
                RequireId(lived.EventKindId,
                    $"lived event {lived.LivedEventId} kind");
                RequireId(lived.SubjectAgentId,
                    $"lived event {lived.LivedEventId} subject");
                Require(run.FinalSocietyState.GetAgent(lived.SubjectAgentId) != null,
                    $"Lived event {lived.LivedEventId} references an unknown subject.");
                RequireId(lived.CauseEntityId,
                    $"lived event {lived.LivedEventId} cause entity");
            }

            var authorityEvidenceLinks = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < run.AuthoritativeEvidenceLinks.Count; i++)
            {
                AuthoritativeEvidenceLink link = run.AuthoritativeEvidenceLinks[i];
                Require(link != null, "Authority evidence links contain a null entry.");
                Require(livedEvents.ContainsKey(link.LivedEventId),
                    $"Authority evidence link references unknown lived event {link.LivedEventId}.");
                Require(index.Evidence.ContainsKey(link.EvidenceArtifactId),
                    $"Authority evidence link references unknown artifact {link.EvidenceArtifactId}.");
                RequireId(link.ObservationKindId,
                    "authority evidence link observation kind");
                Require(authorityEvidenceLinks.Add(
                        $"{link.LivedEventId}\u001f{link.EvidenceArtifactId}\u001f{link.ObservationKindId}"),
                    "Duplicate authority evidence link.");
            }

            var authorityBeliefLinks = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < run.AuthoritativeBeliefLinks.Count; i++)
            {
                AuthoritativeBeliefLink link = run.AuthoritativeBeliefLinks[i];
                Require(link != null, "Authority belief links contain a null entry.");
                Require(livedEvents.ContainsKey(link.LivedEventId),
                    $"Authority belief link references unknown lived event {link.LivedEventId}.");
                AgentState agent = run.FinalSocietyState.GetAgent(link.AgentId);
                Require(agent != null && agent.GetBelief(link.BeliefId) != null,
                    $"Authority belief link references an unknown agent or belief.");
                Require(authorityBeliefLinks.Add(
                        $"{link.LivedEventId}\u001f{link.AgentId}\u001f{link.BeliefId}"),
                    "Duplicate authority belief link.");
            }

            ValidateActionTraces(run, index);
            ValidateRelianceLedger(run, index);
            ValidateEconomicState(run, index);
        }

        private static void ValidateActionTraces(
            InstitutionalConsequenceRun run,
            ReportIndex index)
        {
            Dictionary<string, AgentActionTrace> traces = UniqueMap(
                run.AssessorActionTraces,
                value => value.DecisionId,
                "assessor action trace");
            var observedTraceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (AgentActionTrace trace in traces.Values)
            {
                ValidateCycle(trace.Cycle, index.Report.FinalCycle,
                    $"Action trace {trace.DecisionId}");
                RequireId(trace.CandidateId,
                    $"action trace {trace.DecisionId} candidate");
                RequireId(trace.ActorId,
                    $"action trace {trace.DecisionId} actor");
                Require(run.FinalSocietyState.GetAgent(trace.ActorId) != null,
                    $"Action trace {trace.DecisionId} references an unknown actor.");
                Require(Enum.IsDefined(typeof(SocietyActionKind), trace.Action),
                    $"Action trace {trace.DecisionId} has an invalid action.");
                ValidateIdentifierList(
                    trace.ResultEventIds,
                    $"action trace {trace.DecisionId} result event");
                Require(trace.Reasons != null &&
                        trace.CandidateEvaluations != null &&
                        trace.CapacityReservations != null &&
                        trace.PerceptionSnapshot != null &&
                        trace.RegimeSnapshot != null &&
                        trace.InputSnapshot != null,
                    $"Action trace {trace.DecisionId} lacks a frozen diagnostic envelope.");

                for (int i = 0; i < trace.ResultEventIds.Count; i++)
                {
                    string resultId = trace.ResultEventIds[i];
                    SocietyEvent resultEvent = FindSocietyEvent(
                        run.FinalSocietyState,
                        resultId,
                        out int resultEventCount);
                    Require(resultEventCount == 1 && resultEvent != null &&
                            OrdinalEquals(
                                resultEvent.CauseDecisionId,
                                trace.DecisionId) &&
                            resultEvent.Tick == trace.Cycle &&
                            OrdinalEquals(resultEvent.ActorId, trace.ActorId) &&
                            resultEvent.Kind ==
                                InstitutionalActionProjector.EventKindFor(
                                    trace.Action) &&
                            OrdinalEquals(
                                resultEvent.OpportunityId,
                                trace.OpportunityId),
                        $"Action trace {trace.DecisionId} lacks its exact final " +
                        $"society event {resultId}.");
                    if (!index.Actions.TryGetValue(resultId, out ObservedAgentAction action))
                        continue;
                    observedTraceCounts[resultId] = observedTraceCounts.TryGetValue(
                        resultId,
                        out int count) ? count + 1 : 1;
                    Require(action.Cycle == trace.Cycle &&
                            OrdinalEquals(action.ActorId, trace.ActorId) &&
                            action.Activity == ActivityFor(trace.Action),
                        $"Action trace {trace.DecisionId} disagrees with observed action {resultId}.");
                }
            }

            foreach (ObservedAgentAction action in index.Actions.Values)
            {
                Require(observedTraceCounts.TryGetValue(
                            action.ActionEventId,
                            out int count) &&
                        count == 1,
                    $"Observed action {action.ActionEventId} lacks one assessor trace.");
                SocietyEvent societyEvent = FindSocietyEvent(
                    run.FinalSocietyState,
                    action.ActionEventId,
                    out int eventCount);
                Require(eventCount == 1 && societyEvent.Tick == action.Cycle &&
                        OrdinalEquals(societyEvent.ActorId, action.ActorId) &&
                        InstitutionalActionProjector.ActivityFor(
                            societyEvent.Kind) == action.Activity,
                    $"Observed action {action.ActionEventId} lacks one final society event.");
            }
        }

        private static void ValidateRelianceLedger(
            InstitutionalConsequenceRun run,
            ReportIndex index)
        {
            Dictionary<string, RelianceEvent> ledger = UniqueMap(
                run.RelianceLedger,
                value => value.RelianceEventId,
                "authority reliance event");
            var matchedObservations = new HashSet<string>(StringComparer.Ordinal);
            var relianceSourceActionIds = new HashSet<string>(StringComparer.Ordinal);
            var materialIdsByRelianceAction = new Dictionary<
                string,
                HashSet<string>>(StringComparer.Ordinal);

            foreach (RelianceEvent reliance in ledger.Values)
            {
                ValidateCycle(reliance.Cycle, index.Report.FinalCycle,
                    $"Authority reliance {reliance.RelianceEventId}");
                RequireId(reliance.AgentId,
                    $"authority reliance {reliance.RelianceEventId} actor");
                RequireId(reliance.BeneficiaryAgentId,
                    $"authority reliance {reliance.RelianceEventId} beneficiary");
                RequireId(reliance.ChoiceId,
                    $"authority reliance {reliance.RelianceEventId} private choice");
                RequireId(reliance.PublicObservationId,
                    $"authority reliance {reliance.RelianceEventId} public observation");
                RequireId(reliance.RecordedChoiceId,
                    $"authority reliance {reliance.RelianceEventId} recorded choice");
                Require((reliance.SourceActionKind == SocietyActionKind.Work ||
                         reliance.SourceActionKind == SocietyActionKind.SeekAid) &&
                        !string.IsNullOrWhiteSpace(reliance.SourceOpportunityId) &&
                        !string.IsNullOrWhiteSpace(reliance.RequiredStatusId),
                    $"Authority reliance {reliance.RelianceEventId} lacks a valid " +
                    "status-bearing source action contract.");
                ValidateCycle(reliance.PublicObservationCycle, index.Report.FinalCycle,
                    $"Authority reliance {reliance.RelianceEventId} public observation");
                Require(reliance.PublicObservationCycle >= reliance.Cycle,
                    $"Authority reliance {reliance.RelianceEventId} becomes public " +
                    "before its action.");
                if (reliance.ResourceSpent != 0)
                    RequireId(reliance.ResourceId,
                        $"authority reliance {reliance.RelianceEventId} resource");
                Require(relianceSourceActionIds.Add(reliance.SourceActionEventId),
                    $"Authority reliance source action {reliance.SourceActionEventId} " +
                    "is reused by multiple reliance events.");
                Require(run.FinalSocietyState.GetAgent(reliance.AgentId) != null &&
                        run.FinalSocietyState.GetAgent(reliance.BeneficiaryAgentId) != null,
                    $"Authority reliance {reliance.RelianceEventId} references an unknown agent.");
                Require(index.Rulings.ContainsKey(reliance.ReliedOnRulingId) &&
                        index.Mutations.ContainsKey(reliance.ReliedOnMutationId) &&
                        index.Actions.ContainsKey(reliance.SourceActionEventId),
                    $"Authority reliance {reliance.RelianceEventId} has a broken causal link.");
                ObservedAgentAction sourceAction =
                    index.Actions[reliance.SourceActionEventId];
                Require(sourceAction.Cycle == reliance.Cycle &&
                        OrdinalEquals(sourceAction.ActorId, reliance.AgentId),
                    $"Authority reliance {reliance.RelianceEventId} does not occur " +
                    "with its source action.");
                OfficialStatusMutation enablingMutation =
                    index.Mutations[reliance.ReliedOnMutationId];
                Require(OrdinalEquals(
                            enablingMutation.AffectedAgentId,
                            reliance.AgentId) &&
                        OrdinalEquals(
                            enablingMutation.StatusId,
                            reliance.RequiredStatusId) &&
                        enablingMutation.AfterRecognised ==
                            reliance.ExpectedRecognisedState &&
                        enablingMutation.Cycle < sourceAction.Cycle,
                    $"Authority reliance {reliance.RelianceEventId} has an invalid " +
                    "enabling status mutation.");
                foreach (OfficialStatusMutation candidate in index.Mutations.Values)
                {
                    if (ReferenceEquals(candidate, enablingMutation) ||
                        !OrdinalEquals(
                            candidate.AffectedAgentId,
                            enablingMutation.AffectedAgentId) ||
                        !OrdinalEquals(
                            candidate.StatusId,
                            enablingMutation.StatusId))
                    {
                        continue;
                    }
                    Require(candidate.Cycle < enablingMutation.Cycle ||
                            candidate.Cycle >= sourceAction.Cycle,
                        $"Authority reliance {reliance.RelianceEventId} cites a " +
                        "status mutation superseded before its action.");
                }
                AgentActionTrace sourceTrace = null;
                int sourceTraceCount = 0;
                for (int traceIndex = 0;
                     traceIndex < run.AssessorActionTraces.Count;
                     traceIndex++)
                {
                    AgentActionTrace candidate = run.AssessorActionTraces[traceIndex];
                    if (candidate?.ResultEventIds == null ||
                        !candidate.ResultEventIds.Contains(
                            reliance.SourceActionEventId))
                    {
                        continue;
                    }
                    sourceTrace = candidate;
                    sourceTraceCount++;
                }
                Require(sourceTraceCount == 1 &&
                        sourceTrace.Cycle == reliance.Cycle &&
                        OrdinalEquals(sourceTrace.ActorId, reliance.AgentId) &&
                        sourceTrace.Action == reliance.SourceActionKind &&
                        OrdinalEquals(
                            sourceTrace.OpportunityId,
                            reliance.SourceOpportunityId) &&
                        InstitutionalRelianceService.TraceReadsStatus(
                            sourceTrace,
                            reliance.RequiredStatusId,
                            reliance.ExpectedRecognisedState),
                    $"Authority reliance {reliance.RelianceEventId} lacks its exact " +
                    "status-reading autonomous action trace.");
                Require(reliance.ResourceSpent > 0 &&
                        reliance.AlternativeAvailableBefore &&
                        !reliance.AlternativeAvailableAfter,
                    $"Authority reliance {reliance.RelianceEventId} records no irreversible choice.");

                RelianceObservation observation = FindRelianceObservation(
                    index,
                    reliance,
                    out int observationCount);
                Require(observationCount == 1,
                    $"Authority reliance {reliance.RelianceEventId} lacks one public observation.");
                Require(observation.Cycle == reliance.PublicObservationCycle &&
                        OrdinalEquals(observation.AgentId, reliance.AgentId) &&
                        OrdinalEquals(
                            observation.EnablingRulingId,
                            reliance.ReliedOnRulingId) &&
                        OrdinalEquals(
                            observation.EnablingMutationId,
                            reliance.ReliedOnMutationId) &&
                        OrdinalEquals(
                            observation.SourceActionEventId,
                            reliance.SourceActionEventId) &&
                        OrdinalEquals(
                            observation.RecordedChoiceId,
                            reliance.RecordedChoiceId) &&
                        OrdinalEquals(
                            observation.AbandonedAlternativeId,
                            reliance.AbandonedAlternativeId) &&
                        OrdinalEquals(observation.ResourceId, reliance.ResourceId) &&
                        observation.RecordedResourceDelta ==
                            -reliance.ResourceSpent,
                    $"Authority reliance {reliance.RelianceEventId} disagrees with " +
                    "its frozen public observation envelope.");
                Require(matchedObservations.Add(observation.ObservationId),
                    $"Public reliance {observation.ObservationId} maps to multiple authority events.");

                if (!materialIdsByRelianceAction.TryGetValue(
                        reliance.SourceActionEventId,
                        out HashSet<string> actionMaterialIds))
                {
                    actionMaterialIds = new HashSet<string>(StringComparer.Ordinal);
                    materialIdsByRelianceAction.Add(
                        reliance.SourceActionEventId,
                        actionMaterialIds);
                }
                Require(reliance.AppliedEffects != null &&
                        reliance.AppliedEffects.Count >= 1 &&
                        reliance.AppliedEffects.Count <=
                            InstitutionalRelianceService.MaximumEffects,
                    $"Authority reliance {reliance.RelianceEventId} has an invalid " +
                    "applied-effect count.");
                var appliedEffectIds = new HashSet<string>(StringComparer.Ordinal);
                var appliedMaterialIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < reliance.AppliedEffects.Count; i++)
                {
                    RelianceAppliedEffect effect = reliance.AppliedEffects[i];
                    Require(effect != null &&
                            !string.IsNullOrWhiteSpace(effect.EffectId) &&
                            appliedEffectIds.Add(effect.EffectId) &&
                            !string.IsNullOrWhiteSpace(
                                effect.MaterialConsequenceId) &&
                            effect.ResourceBefore >= 0 &&
                            effect.ResourceAfter >= 0 &&
                            (effect.HasNeedEffect
                                ? Enum.IsDefined(typeof(NeedKind), effect.Need) &&
                                  effect.NeedPressureBefore >= 0 &&
                                  effect.NeedPressureBefore <= 100 &&
                                  effect.NeedPressureAfter >= 0 &&
                                  effect.NeedPressureAfter <= 100
                                : effect.NeedPressureBefore == 0 &&
                                  effect.NeedPressureAfter == 0) &&
                            appliedMaterialIds.Add(effect.MaterialConsequenceId) &&
                            actionMaterialIds.Add(effect.MaterialConsequenceId) &&
                            index.Material.TryGetValue(
                                effect.MaterialConsequenceId,
                                out MaterialConsequence projected) &&
                            OrdinalEquals(projected.CauseId, reliance.SourceActionEventId) &&
                            OrdinalEquals(projected.AgentId, effect.AgentId) &&
                            projected.Cycle == observation.Cycle &&
                            projected.ResourceDelta ==
                                effect.ResourceAfter - effect.ResourceBefore &&
                            projected.Kind == effect.MaterialKind &&
                            OrdinalEquals(projected.KindId, effect.MaterialKindId) &&
                            OrdinalEquals(projected.ResourceId, effect.ResourceId) &&
                            projected.HasNeedEffect == effect.HasNeedEffect &&
                            (!effect.HasNeedEffect ||
                             (projected.Need == effect.Need &&
                              projected.NeedPressureBefore == effect.NeedPressureBefore &&
                              projected.NeedPressureAfter == effect.NeedPressureAfter)),
                        $"Authority reliance {reliance.RelianceEventId} has an invalid " +
                        "material-effect projection.");
                }

                int actorMaterialDelta = 0;
                foreach (MaterialConsequence material in index.Material.Values)
                {
                    if (OrdinalEquals(material.CauseId, reliance.SourceActionEventId) &&
                        OrdinalEquals(material.AgentId, reliance.AgentId) &&
                        (string.IsNullOrWhiteSpace(observation.ResourceId) ||
                         OrdinalEquals(material.ResourceId, observation.ResourceId)))
                    {
                        actorMaterialDelta = checked(
                            actorMaterialDelta + material.ResourceDelta);
                    }
                }
                Require(actorMaterialDelta == observation.RecordedResourceDelta &&
                        reliance.ResourceSpent == -observation.RecordedResourceDelta,
                    $"Authority reliance {reliance.RelianceEventId} disagrees with its material projection.");

                int recoveryCount = 0;
                DescendantCase recovery = null;
                foreach (DescendantCase descendant in index.Descendants.Values)
                {
                    if (descendant.Kind == DescendantCaseKind.Reliance &&
                        OrdinalEquals(
                            descendant.CausalAgentActionId,
                            reliance.SourceActionEventId) &&
                        OrdinalEquals(descendant.ClaimantAgentId, reliance.AgentId))
                    {
                        recovery = descendant;
                        recoveryCount++;
                        Require(descendant.OpenedCycle >
                                reliance.PublicObservationCycle,
                            $"Authority reliance {reliance.RelianceEventId} has a " +
                            "recovery before its public observation.");
                    }
                }
                Require(recoveryCount <= 1,
                    $"Authority reliance {reliance.RelianceEventId} has multiple " +
                    "recovery cases.");
                if (recoveryCount == 1)
                {
                    int resultingAppealCount = 0;
                    int exactReversalAppealCount = 0;
                    foreach (Appeal appeal in index.Appeals.Values)
                    {
                        if (!OrdinalEquals(
                                appeal.ResultingRulingId,
                                recovery.OriginatingRulingId))
                        {
                            continue;
                        }
                        resultingAppealCount++;
                        if (appeal.Disposition == AppealDisposition.Reversed &&
                            OrdinalEquals(
                                appeal.CaseId,
                                recovery.ParentCaseId) &&
                            OrdinalEquals(
                                appeal.ChallengedRulingId,
                                reliance.ReliedOnRulingId))
                        {
                            exactReversalAppealCount++;
                        }
                    }
                    Require(resultingAppealCount == 1 &&
                            exactReversalAppealCount == 1,
                        $"Authority reliance {reliance.RelianceEventId} recovery " +
                        "does not reverse the exact ruling relied on.");
                }
                Require(reliance.SurvivedReversal == (recoveryCount == 1),
                    $"Authority reliance {reliance.RelianceEventId} disagrees with its recovery case.");
            }

            foreach (MaterialConsequence material in index.Material.Values)
            {
                if (materialIdsByRelianceAction.TryGetValue(
                        material.CauseId,
                        out HashSet<string> allowedMaterialIds))
                {
                    Require(allowedMaterialIds.Contains(material.ConsequenceId),
                        $"Material consequence {material.ConsequenceId} caused by a " +
                        "reliance action is not linked to an authoritative applied effect.");
                }
            }

            Require(matchedObservations.Count == index.Reliance.Count,
                "A public reliance observation has no authority ledger event.");
        }

        private static void ValidateEconomicState(
            InstitutionalConsequenceRun run,
            ReportIndex index)
        {
            Dictionary<string, EconomicAccountState> accounts = UniqueMap(
                run.EconomicAccounts,
                value => value.AgentId,
                "economic account");
            foreach (EconomicAccountState account in accounts.Values)
            {
                Require(run.FinalSocietyState.GetAgent(account.AgentId) != null,
                    $"Economic account references unknown agent {account.AgentId}.");
                Require(account.AvailableCredits >= 0,
                    $"Economic account {account.AgentId} has negative available credits.");
            }

            Dictionary<string, AlternativeOptionState> alternatives = UniqueMap(
                run.AlternativeOptions,
                value => value.OptionId,
                "alternative option");
            foreach (AlternativeOptionState alternative in alternatives.Values)
            {
                Require(run.FinalSocietyState.GetAgent(alternative.AgentId) != null,
                    $"Alternative {alternative.OptionId} references an unknown agent.");
                if (!alternative.Available)
                {
                    Require(index.Actions.ContainsKey(alternative.ChangedByActionEventId),
                        $"Abandoned alternative {alternative.OptionId} has no causal action.");
                }
            }

            foreach (RelianceObservation reliance in index.Reliance.Values)
            {
                Require(alternatives.TryGetValue(
                            reliance.AbandonedAlternativeId,
                            out AlternativeOptionState alternative) &&
                        !alternative.Available &&
                        OrdinalEquals(alternative.AgentId, reliance.AgentId) &&
                        OrdinalEquals(
                            alternative.ChangedByActionEventId,
                            reliance.SourceActionEventId),
                    $"Reliance {reliance.ObservationId} has no matching abandoned alternative.");
                Require(accounts.ContainsKey(reliance.AgentId),
                    $"Reliance {reliance.ObservationId} has no actor economic account.");
            }
        }

        private static void ValidateEntitlements(
            InstitutionalConsequenceRun run,
            ReportIndex index,
            ExclusiveEntitlementRegistry registry)
        {
            if (registry != null)
            {
                Require(registry.Count == index.Entitlements.Count,
                    "Exclusive entitlement registry and public report have different counts.");
            }

            foreach (ExclusiveEntitlementObservation observation in
                     index.Entitlements.Values)
            {
                int recognisedCount = 0;
                string recognisedHolderId = null;
                for (int i = 0; i < run.FinalSocietyState.Agents.Count; i++)
                {
                    AgentState agent = run.FinalSocietyState.Agents[i];
                    if (!agent.Standing.IsRecognised(observation.HolderStatusId))
                        continue;
                    recognisedCount++;
                    recognisedHolderId = agent.StableId;
                }
                int expectedCount = string.IsNullOrWhiteSpace(
                    observation.CurrentHolderAgentId) ? 0 : 1;
                Require(recognisedCount == expectedCount &&
                        (expectedCount == 0 || OrdinalEquals(
                            recognisedHolderId,
                            observation.CurrentHolderAgentId)),
                    $"Entitlement {observation.EntitlementId} violates its final holder invariant.");

                if (registry != null)
                {
                    ExclusiveEntitlementState state = registry.Find(
                        observation.EntitlementId,
                        observation.ResourceId);
                    Require(state != null &&
                            OrdinalEquals(
                                state.HolderStatusId,
                                observation.HolderStatusId) &&
                            state.ConservedAmount == observation.ConservedAmount &&
                            OrdinalEquals(
                                state.CurrentHolderAgentId,
                                observation.CurrentHolderAgentId) &&
                            OrdinalEquals(
                                state.LastMutationCauseId,
                                observation.LastMutationCauseId),
                        $"Entitlement {observation.EntitlementId} public and authority states disagree.");
                }

                ValidateEntitlementTransfer(index, observation);
            }
        }

        private static void ValidateEntitlementTransfer(
            ReportIndex index,
            ExclusiveEntitlementObservation observation)
        {
            if (string.IsNullOrWhiteSpace(observation.LastMutationCauseId)) return;

            var mutations = new List<OfficialStatusMutation>();
            foreach (OfficialStatusMutation mutation in index.Mutations.Values)
            {
                if (OrdinalEquals(mutation.CauseId, observation.LastMutationCauseId) &&
                    OrdinalEquals(mutation.StatusId, observation.HolderStatusId))
                    mutations.Add(mutation);
            }
            Require(mutations.Count <= 2,
                $"Entitlement {observation.EntitlementId} has too many holder mutations for one cause.");

            var materials = new List<MaterialConsequence>();
            foreach (MaterialConsequence material in index.Material.Values)
            {
                if (OrdinalEquals(material.CauseId, observation.LastMutationCauseId) &&
                    OrdinalEquals(material.ResourceId, observation.ResourceId))
                    materials.Add(material);
            }

            if (mutations.Count < 2)
            {
                Require(materials.Count == 0,
                    $"Entitlement {observation.EntitlementId} has transfer material without two holder mutations.");
                return;
            }

            OfficialStatusMutation lossMutation = null;
            OfficialStatusMutation gainMutation = null;
            for (int i = 0; i < mutations.Count; i++)
            {
                if (mutations[i].BeforeRecognised && !mutations[i].AfterRecognised)
                    lossMutation = mutations[i];
                if (!mutations[i].BeforeRecognised && mutations[i].AfterRecognised)
                    gainMutation = mutations[i];
            }
            Require(lossMutation != null && gainMutation != null &&
                    !OrdinalEquals(
                        lossMutation.AffectedAgentId,
                        gainMutation.AffectedAgentId) &&
                    OrdinalEquals(
                        observation.CurrentHolderAgentId,
                        gainMutation.AffectedAgentId),
                $"Entitlement {observation.EntitlementId} lacks one displaced and one recognised holder.");
            Require(materials.Count == 2,
                $"Entitlement {observation.EntitlementId} lacks one gain/loss material pair.");

            MaterialConsequence gain = null;
            MaterialConsequence loss = null;
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i].ResourceDelta == observation.ConservedAmount &&
                    OrdinalEquals(
                        materials[i].AgentId,
                        gainMutation.AffectedAgentId))
                    gain = materials[i];
                if (materials[i].ResourceDelta == -observation.ConservedAmount &&
                    OrdinalEquals(
                        materials[i].AgentId,
                        lossMutation.AffectedAgentId))
                    loss = materials[i];
            }
            Require(gain != null && loss != null &&
                    gain.ResourceDelta + loss.ResourceDelta == 0,
                $"Entitlement {observation.EntitlementId} transfer is not conserved.");
        }

        private static void ValidateWorkAllocationProjection(
            InstitutionalConsequenceRun run,
            ReportIndex index)
        {
            Dictionary<string, WorkAllocationState> authority = UniqueMap(
                run.WorkAllocations,
                value => value.AllocationId,
                "authority work allocation");
            Require(authority.Count == index.WorkAllocations.Count,
                "Authority and public work-allocation counts differ.");
            foreach (WorkAllocationState state in authority.Values)
            {
                Require(index.WorkAllocations.TryGetValue(
                            state.AllocationId,
                            out WorkAllocationObservation observation) &&
                        OrdinalEquals(state.EmployerId, observation.EmployerId) &&
                        OrdinalEquals(
                            state.OriginalWorkerId,
                            observation.OriginalWorkerId) &&
                        OrdinalEquals(
                            state.PaidHolderAgentId,
                            observation.PaidHolderAgentId) &&
                        OrdinalEquals(
                            state.IdentityConditionId,
                            observation.IdentityConditionId) &&
                        state.CommittedWage == observation.CommittedWage &&
                        OrdinalEquals(
                            state.LastMutationCauseId,
                            observation.LastMutationCauseId),
                    $"Work allocation {state.AllocationId} public and authority projections disagree.");
            }
        }

        private static void ValidateNoLivedTruthIdentifierLeak(
            InstitutionalConsequenceRun run,
            ReportIndex index)
        {
            HashSet<string> publicStrings = CollectPublicStringValues(index.Report);
            for (int i = 0; i < run.AuthoritativeEvents.Count; i++)
            {
                LivedEvent lived = run.AuthoritativeEvents[i];
                Require(!publicStrings.Contains(lived.LivedEventId),
                    $"Authority-only lived event id {lived.LivedEventId} leaked into the public report.");
            }
        }

        private static void ValidateScope(ReportIndex index, Holding holding)
        {
            Require(holding.Scope != null,
                $"Holding {holding.HoldingId} has no scope.");
            RequireId(holding.Scope.ScopeId,
                $"holding {holding.HoldingId} scope");
            Require(index.ScopeIds.Add(holding.Scope.ScopeId),
                $"Duplicate precedent scope id {holding.Scope.ScopeId}.");
            index.RegisterGlobal(holding.Scope.ScopeId, "precedent scope");
            Require(Enum.IsDefined(typeof(PrecedentReach), holding.Scope.Reach),
                $"Holding {holding.HoldingId} has an invalid scope reach.");
            if (holding.Scope.Reach == PrecedentReach.Individual)
                RequireId(holding.Scope.BoundAgentId,
                    $"holding {holding.HoldingId} bound agent");
            if (holding.Scope.Reach == PrecedentReach.Employer)
                RequireId(holding.Scope.BoundEmployerId,
                    $"holding {holding.HoldingId} bound employer");
            Require(holding.Scope.RequiredFacts != null,
                $"Holding {holding.HoldingId} has no required-fact set.");
            holding.Scope.RequiredFacts.Validate();
        }

        private static void AssertAcyclicCaseParentage(
            ReportIndex index,
            DescendantCase start)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            DescendantCase current = start;
            while (current != null)
            {
                Require(visited.Add(current.CaseId),
                    $"Descendant case parent cycle includes {current.CaseId}.");
                if (OrdinalEquals(current.ParentCaseId, index.Report.PrimaryCaseId))
                    return;
                Require(index.Descendants.TryGetValue(
                        current.ParentCaseId,
                        out current),
                    $"Case {start.CaseId} has an unresolved parent chain.");
            }
        }

        private static void ValidateCaseCycle(
            ReportIndex index,
            string caseId,
            long cycle,
            string label)
        {
            ValidateCycle(cycle, index.Report.FinalCycle, label);
            if (index.CaseOpeningsByCase.TryGetValue(
                    caseId,
                    out InstitutionalCaseOpening opening))
            {
                Require(cycle >= opening.OpenedCycle,
                    $"{label} predates case {caseId}.");
            }
            if (index.Descendants.TryGetValue(caseId, out DescendantCase descendant))
                Require(cycle >= descendant.OpenedCycle,
                    $"{label} predates case {caseId}.");
        }

        private static bool TryResolveCauseCycle(
            ReportIndex index,
            string causeId,
            out long cycle)
        {
            if (index.Actions.TryGetValue(causeId, out ObservedAgentAction action))
            {
                cycle = action.Cycle;
                return true;
            }
            if (index.Rulings.TryGetValue(causeId, out Ruling ruling))
            {
                cycle = ruling.Cycle;
                return true;
            }
            if (index.Mutations.TryGetValue(causeId, out OfficialStatusMutation mutation))
            {
                cycle = mutation.Cycle;
                return true;
            }
            if (index.Appeals.TryGetValue(causeId, out Appeal appeal))
            {
                cycle = appeal.FiledCycle;
                return true;
            }
            if (index.Holdings.TryGetValue(causeId, out Holding holding))
            {
                cycle = holding.EstablishedCycle;
                return true;
            }
            if (index.Reliance.TryGetValue(causeId, out RelianceObservation reliance))
            {
                cycle = reliance.Cycle;
                return true;
            }
            if (index.Descendants.TryGetValue(causeId, out DescendantCase descendant))
            {
                cycle = descendant.OpenedCycle;
                return true;
            }
            if (index.Evidence.TryGetValue(causeId, out EvidenceArtifact evidence))
            {
                cycle = evidence.EnteredCycle;
                return true;
            }
            if (index.Findings.TryGetValue(causeId, out OfficialFinding finding))
            {
                cycle = finding.Cycle;
                return true;
            }
            cycle = 0;
            return false;
        }

        private static List<string> EvidenceForCaseAt(
            ReportIndex index,
            string caseId,
            long cycle)
        {
            var result = new List<string>();
            foreach (EvidenceArtifact artifact in index.Evidence.Values)
            {
                if (OrdinalEquals(artifact.CaseId, caseId) &&
                    artifact.EnteredCycle <= cycle)
                    result.Add(artifact.ArtifactId);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static Ruling FindUniqueCitingRuling(
            ReportIndex index,
            string holdingId,
            string caseId)
        {
            Ruling found = null;
            int count = 0;
            foreach (Ruling ruling in index.Rulings.Values)
            {
                if (OrdinalEquals(ruling.CaseId, caseId) &&
                    ruling.CitedHoldingIds.Contains(holdingId))
                {
                    found = ruling;
                    count++;
                }
            }
            Require(count == 1,
                $"Holding {holdingId} applied to {caseId} has {count} citing rulings.");
            return found;
        }

        private static RelianceObservation FindRelianceObservation(
            ReportIndex index,
            RelianceEvent reliance,
            out int count)
        {
            RelianceObservation found = null;
            count = 0;
            foreach (RelianceObservation observation in index.Reliance.Values)
            {
                if (OrdinalEquals(
                        observation.ObservationId,
                        reliance.PublicObservationId))
                {
                    found = observation;
                    count++;
                }
            }
            return found;
        }

        private static MaterialConsequence FindUniqueMaterial(
            ReportIndex index,
            string agentId,
            int resourceDelta,
            out int count)
        {
            MaterialConsequence found = null;
            count = 0;
            foreach (MaterialConsequence material in index.Material.Values)
            {
                if (OrdinalEquals(material.AgentId, agentId) &&
                    material.ResourceDelta == resourceDelta)
                {
                    found = material;
                    count++;
                }
            }
            return found;
        }

        private static SocietyEvent FindSocietyEvent(
            SocietyState state,
            string eventId,
            out int count)
        {
            SocietyEvent found = null;
            count = 0;
            for (int i = 0; i < state.EventLedger.Count; i++)
            {
                SocietyEvent candidate = state.EventLedger[i];
                if (OrdinalEquals(candidate.EventId, eventId))
                {
                    found = candidate;
                    count++;
                }
            }
            return found;
        }

        private static ObservedActivityKind ActivityFor(SocietyActionKind action)
        {
            return action switch
            {
                SocietyActionKind.Work => ObservedActivityKind.WorkPerformed,
                SocietyActionKind.SeekAid => ObservedActivityKind.AidRequested,
                SocietyActionKind.Help => ObservedActivityKind.AssistanceGiven,
                SocietyActionKind.Disclose => ObservedActivityKind.EvidenceSubmitted,
                SocietyActionKind.Appeal => ObservedActivityKind.AppealFiled,
                _ => ObservedActivityKind.NoVisibleAction,
            };
        }

        private static int CountTimeline(
            InstitutionalConsequenceReport report,
            InstitutionalTimelineKind kind,
            long? cycle,
            string causeId,
            string subjectId,
            string detailId)
        {
            int count = 0;
            for (int i = 0; i < report.Timeline.Count; i++)
            {
                InstitutionalTimelineEntry entry = report.Timeline[i];
                if (entry != null && entry.Kind == kind &&
                    (!cycle.HasValue || entry.Cycle == cycle.Value) &&
                    (causeId == null || OrdinalEquals(entry.CauseId, causeId)) &&
                    (subjectId == null || OrdinalEquals(entry.SubjectId, subjectId)) &&
                    (detailId == null || OrdinalEquals(entry.DetailId, detailId)))
                    count++;
            }
            return count;
        }

        private static void AssertPublicSurfaceContainsNoAuthorityTypes()
        {
            Assembly authorityAssembly = typeof(InstitutionalCausalGraphValidator).Assembly;
            var pending = new Queue<Type>();
            var visited = new HashSet<Type>();
            pending.Enqueue(typeof(InstitutionalConsequenceReport));

            while (pending.Count > 0)
            {
                Type current = UnwrapCollectionType(pending.Dequeue());
                if (current == null || current == typeof(string) ||
                    current.IsPrimitive || current.IsEnum || !visited.Add(current))
                    continue;
                Require(current.Assembly != authorityAssembly,
                    $"Public report surface exposes authority-only type {current.FullName}.");

                FieldInfo[] fields = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public);
                for (int i = 0; i < fields.Length; i++)
                    pending.Enqueue(fields[i].FieldType);
                PropertyInfo[] properties = current.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public);
                for (int i = 0; i < properties.Length; i++)
                {
                    if (properties[i].GetMethod != null &&
                        properties[i].GetMethod.IsPublic &&
                        properties[i].GetIndexParameters().Length == 0)
                        pending.Enqueue(properties[i].PropertyType);
                }
            }
        }

        private static Type UnwrapCollectionType(Type type)
        {
            if (type == null) return null;
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType)
            {
                Type[] arguments = type.GetGenericArguments();
                if (arguments.Length == 1 &&
                    typeof(IEnumerable).IsAssignableFrom(type))
                    return arguments[0];
            }
            return type;
        }

        private static HashSet<string> CollectPublicStringValues(object root)
        {
            var values = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<object>(ReferenceComparer.Instance);
            CollectPublicStringValues(root, values, visited);
            return values;
        }

        private static void CollectPublicStringValues(
            object value,
            HashSet<string> values,
            HashSet<object> visited)
        {
            if (value == null) return;
            if (value is string text)
            {
                if (!string.IsNullOrEmpty(text)) values.Add(text);
                return;
            }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type.IsValueType) return;
            if (!visited.Add(value)) return;
            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                    CollectPublicStringValues(item, values, visited);
                return;
            }
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
                CollectPublicStringValues(fields[i].GetValue(value), values, visited);
            PropertyInfo[] properties = type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < properties.Length; i++)
            {
                if (properties[i].GetMethod == null ||
                    !properties[i].GetMethod.IsPublic ||
                    properties[i].GetIndexParameters().Length != 0)
                    continue;
                CollectPublicStringValues(
                    properties[i].GetValue(value),
                    values,
                    visited);
            }
        }

        private static Dictionary<string, T> UniqueMap<T>(
            List<T> rows,
            Func<T, string> id,
            string label)
            where T : class
        {
            Require(rows != null, $"The {label} collection is null.");
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                T row = rows[i];
                Require(row != null, $"The {label} collection contains a null row.");
                string rowId = id(row);
                RequireId(rowId, label);
                Require(result.TryAdd(rowId, row),
                    $"Duplicate {label} id {rowId}.");
            }
            return result;
        }

        private static void ValidateReferenceList<T>(
            List<string> references,
            Dictionary<string, T> available,
            string label)
            where T : class
        {
            ValidateIdentifierList(references, label);
            for (int i = 0; i < references.Count; i++)
                Require(available.ContainsKey(references[i]),
                    $"{label} references missing id {references[i]}.");
        }

        private static void ValidateReferenceList(
            List<string> references,
            HashSet<string> available,
            string label)
        {
            ValidateIdentifierList(references, label);
            for (int i = 0; i < references.Count; i++)
            {
                Require(available.Contains(references[i]),
                    $"{label} references missing id {references[i]}.");
            }
        }

        private static void ValidateIdentifierList(List<string> values, string label)
        {
            Require(values != null, $"The {label} list is null.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                RequireId(values[i], label);
                Require(seen.Add(values[i]),
                    $"Duplicate {label} id {values[i]}.");
            }
        }

        private static bool SameSet(List<string> left, List<string> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            return new HashSet<string>(left, StringComparer.Ordinal).SetEquals(right);
        }

        private static int CountOrdinal(List<string> values, string expected)
        {
            if (values == null) return 0;
            int count = 0;
            for (int i = 0; i < values.Count; i++)
                if (OrdinalEquals(values[i], expected)) count++;
            return count;
        }

        private static void ValidateCycle(long cycle, long finalCycle, string label)
        {
            Require(cycle >= 0 && cycle <= finalCycle,
                $"{label} has invalid cycle {cycle} for final cycle {finalCycle}.");
        }

        private static void RequireAgent(
            HashSet<string> agentIds,
            string agentId,
            string label)
        {
            Require(agentIds.Contains(agentId),
                $"{label} references unknown agent {agentId}.");
        }

        private static void RequireId(string value, string label)
        {
            Require(!string.IsNullOrWhiteSpace(value), $"A {label} lacks an id.");
        }

        private static bool OrdinalEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class ReportIndex
        {
            internal readonly InstitutionalConsequenceReport Report;
            internal readonly Dictionary<string, ObservedAgentAction> Actions;
            internal readonly Dictionary<string, EvidenceArtifact> Evidence;
            internal readonly Dictionary<string, OfficialFinding> Findings;
            internal readonly Dictionary<string, Ruling> Rulings;
            internal readonly Dictionary<string, OfficialStatusMutation> Mutations;
            internal readonly Dictionary<string, InstitutionalCaseOpening> CaseOpenings;
            internal readonly Dictionary<string, InstitutionalCaseOpening> CaseOpeningsByCase;
            internal readonly Dictionary<string, DescendantCase> Descendants;
            internal readonly Dictionary<string, Appeal> Appeals;
            internal readonly Dictionary<string, Holding> Holdings;
            internal readonly Dictionary<string, RelianceObservation> Reliance;
            internal readonly Dictionary<string, MaterialConsequence> Material;
            internal readonly Dictionary<string, ConnectedOutcomePair> ConnectedOutcomes;
            internal readonly Dictionary<string, ExclusiveEntitlementObservation> Entitlements;
            internal readonly Dictionary<string, WorkAllocationObservation> WorkAllocations;
            internal readonly Dictionary<string, InstitutionalTimelineEntry> Timeline;
            internal readonly HashSet<string> Cases = new(StringComparer.Ordinal);
            internal readonly HashSet<string> GlobalIds = new(StringComparer.Ordinal);
            internal readonly HashSet<string> ProvenanceIds = new(StringComparer.Ordinal);
            internal readonly HashSet<string> ScopeIds = new(StringComparer.Ordinal);

            internal ReportIndex(InstitutionalConsequenceReport report)
            {
                Report = report;
                Actions = Map(report.ObservedAgentActions,
                    value => value.ActionEventId, "observed action");
                Evidence = Map(report.EvidenceArtifacts,
                    value => value.ArtifactId, "evidence artifact");
                Findings = Map(report.OfficialFindings,
                    value => value.FindingId, "official finding");
                Rulings = Map(report.Rulings,
                    value => value.RulingId, "ruling");
                Mutations = Map(report.OfficialStatusMutations,
                    value => value.MutationId, "official mutation");
                CaseOpenings = Map(report.CaseOpenings,
                    value => value.ActivationId, "case opening");
                CaseOpeningsByCase = UniqueMap(
                    report.CaseOpenings,
                    value => value.CaseId,
                    "case opening case");
                Descendants = Map(report.DescendantCases,
                    value => value.CaseId, "descendant case");
                Appeals = Map(report.Appeals,
                    value => value.AppealId, "appeal");
                Holdings = Map(report.Holdings,
                    value => value.HoldingId, "holding");
                Reliance = Map(report.RelianceObservations,
                    value => value.ObservationId, "reliance observation");
                Material = Map(report.MaterialConsequences,
                    value => value.ConsequenceId, "material consequence");
                ConnectedOutcomes = Map(report.ConnectedOutcomes,
                    value => value.PairId, "connected outcome");
                Entitlements = Map(report.ExclusiveEntitlements,
                    value => value.EntitlementId, "exclusive entitlement");
                WorkAllocations = Map(report.WorkAllocations,
                    value => value.AllocationId, "work allocation");
                Timeline = Map(report.Timeline,
                    value => value.EntryId, "timeline entry");

                Require(Cases.Add(report.PrimaryCaseId),
                    "Primary case id is duplicated.");
                foreach (InstitutionalCaseOpening opening in CaseOpenings.Values)
                {
                    if (!OrdinalEquals(opening.CaseId, report.PrimaryCaseId))
                        Require(Cases.Add(opening.CaseId),
                            $"Duplicate case id {opening.CaseId}.");
                }
                foreach (string caseId in Descendants.Keys)
                    Require(Cases.Add(caseId), $"Duplicate case id {caseId}.");
            }

            internal void RegisterGlobal(string id, string label)
            {
                Require(GlobalIds.Add(id),
                    $"Identifier {id} is reused by {label} and another report node.");
            }

            private Dictionary<string, T> Map<T>(
                List<T> rows,
                Func<T, string> id,
                string label)
                where T : class
            {
                Dictionary<string, T> result = UniqueMap(rows, id, label);
                foreach (string rowId in result.Keys) RegisterGlobal(rowId, label);
                return result;
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }
    }

    internal static class InstitutionalCausalGraphDictionaryExtensions
    {
        internal static bool Exists<TKey, TValue>(
            this Dictionary<TKey, TValue>.ValueCollection values,
            Predicate<TValue> predicate)
        {
            foreach (TValue value in values)
                if (predicate(value)) return true;
            return false;
        }
    }
}
