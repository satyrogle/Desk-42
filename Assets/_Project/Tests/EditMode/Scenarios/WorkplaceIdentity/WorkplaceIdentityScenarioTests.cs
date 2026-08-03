using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using Desk42.Institutional.Scenarios.WorkplaceIdentity;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.Scenarios.WorkplaceIdentity
{
    public sealed class WorkplaceIdentityScenarioTests
    {
        [Test]
        public void Definition_IsValidAndDeclaresExactConservedTransferCause()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();

            Assert.DoesNotThrow(() =>
                InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(definition.ScenarioId,
                Is.EqualTo(WorkplaceIdentityScenario.ScenarioId));
            Assert.That(definition.CycleSchedule.Select(row => row.Cycle),
                Is.EqualTo(Enumerable.Range(1, 11).Select(value => (long)value)));
            Assert.That(definition.ParticipantRoles, Has.Count.EqualTo(5));
            Assert.That(definition.InitialSociety.Agents, Has.Count.EqualTo(5));

            ScenarioExclusiveEntitlementDefinition entitlement =
                definition.ExclusiveEntitlements.Single();
            ScenarioExclusiveEntitlementTransferDefinition transfer =
                definition.EntitlementTransfers.Single();
            Assert.That(entitlement.ResourceId,
                Is.EqualTo(WorkplaceIdentityScenario.PaidShiftResourceId));
            Assert.That(entitlement.InitialHolderRoleId,
                Is.EqualTo(WorkplaceIdentityScenario.ContingentHolderRoleId));
            Assert.That(transfer.CauseRulingId,
                Is.EqualTo(WorkplaceIdentityScenario.SuccessorInitialRulingId));
            Assert.That(transfer.CauseHoldingId,
                Is.EqualTo(WorkplaceIdentityScenario.HoldingId));
            Assert.That(transfer.RequiredRulingDisposition,
                Is.EqualTo(RulingDisposition.Recognised));
            ScenarioHoldingCitationDefinition citation =
                definition.HoldingCitations.Single();
            Assert.That(citation.CitationId,
                Is.EqualTo(WorkplaceIdentityScenario.HoldingCitationId));
            Assert.That(citation.TargetCaseId,
                Is.EqualTo(WorkplaceIdentityScenario.SuccessorCaseId));
            Assert.That(citation.TargetRulingId,
                Is.EqualTo(WorkplaceIdentityScenario.SuccessorInitialRulingId));
        }

        [Test]
        public void PrecedentPolicy_ProducesCompleteTraceableInstitutionalChain()
        {
            InstitutionalScenarioRunResult result = Run(
                WorkplaceIdentityScenario.CreatePrecedentPolicy());

            Assert.That(result.AssessorRun.AuthoritativeEvents, Has.Count.EqualTo(1));
            LivedEvent lived = result.AssessorRun.AuthoritativeEvents.Single();
            Assert.That(lived.LivedEventId,
                Is.EqualTo("lived:incident-seed.workplace.identity-injury"));
            Assert.That(lived.SubjectAgentId,
                Is.EqualTo(WorkplaceIdentityScenario.PrimaryClaimantAgentId));

            AgentActionTrace disclosure = Trace(
                result,
                1,
                WorkplaceIdentityScenario.PrimaryClaimantAgentId,
                SocietyActionKind.Disclose);
            EvidenceArtifact evidence = result.Report.EvidenceArtifacts.Single();
            Assert.That(disclosure.SubjectBeliefId,
                Is.EqualTo("belief.workplace.identity-continuity"));
            Assert.That(evidence.SourceTemplateId,
                Is.EqualTo(WorkplaceIdentityScenario.EvidenceTemplateId));
            Assert.That(evidence.Provenance.CreatedByAgentAction, Is.True);
            Assert.That(evidence.Provenance.SourceDecisionId,
                Is.EqualTo(disclosure.DecisionId));
            Assert.That(result.AssessorRun.AuthoritativeEvidenceLinks,
                Has.Count.EqualTo(1));

            Ruling initial = Ruling(
                result,
                WorkplaceIdentityScenario.PrimaryInitialRulingId);
            OfficialFinding initialFinding = result.Report.OfficialFindings.Single(
                finding => finding.FindingId == initial.FindingId);
            Assert.That(initial.Disposition, Is.EqualTo(RulingDisposition.Denied));
            Assert.That(initialFinding.WeightedEvidenceScore, Is.EqualTo(54));

            OfficialStatusMutation adverse = result.Report.OfficialStatusMutations
                .Single(mutation =>
                    mutation.CauseId ==
                        WorkplaceIdentityScenario.PrimaryInitialRulingId &&
                    mutation.StatusId == InstitutionalStatusIds.AdverseDecision);
            Assert.That(adverse.AfterRecognised, Is.True);
            Assert.That(adverse.ResourceDelta, Is.EqualTo(-5));

            AgentActionTrace aid = Trace(
                result,
                4,
                WorkplaceIdentityScenario.PrimaryClaimantAgentId,
                SocietyActionKind.SeekAid);
            Assert.That(aid.OpportunityId,
                Is.EqualTo(WorkplaceIdentityScenario.AidOpportunityId));
            RelianceEvent reliance = result.AssessorRun.RelianceLedger.Single();
            Assert.That(reliance.RelianceEventId,
                Is.EqualTo(WorkplaceIdentityScenario.RelianceId));
            Assert.That(reliance.AlternativeAvailableBefore, Is.True);
            Assert.That(reliance.AlternativeAvailableAfter, Is.False);
            Assert.That(reliance.CreditsAfter, Is.LessThan(reliance.CreditsBefore));
            Assert.That(reliance.SurvivedReversal, Is.True);
            Assert.That(result.Report.RelianceObservations, Has.Count.EqualTo(1));
            Assert.That(result.Report.DescendantCases.Any(candidate =>
                    candidate.Kind == DescendantCaseKind.Reliance),
                Is.True);

            AgentActionTrace appealAction = Trace(
                result,
                5,
                WorkplaceIdentityScenario.PrimaryClaimantAgentId,
                SocietyActionKind.Appeal);
            Assert.That(appealAction.OpportunityId,
                Is.EqualTo(WorkplaceIdentityScenario.AppealOpportunityId));
            Appeal appeal = result.Report.Appeals.Single();
            Assert.That(appeal.CaseId,
                Is.EqualTo(WorkplaceIdentityScenario.PrimaryCaseId));
            Assert.That(appeal.FilingActionEventId,
                Is.EqualTo(appealAction.ResultEventIds.Single()));
            Assert.That(appeal.ChallengedRulingId,
                Is.EqualTo(WorkplaceIdentityScenario.PrimaryInitialRulingId));
            Assert.That(appeal.Disposition, Is.EqualTo(AppealDisposition.Reversed));

            Ruling appellate = Ruling(
                result,
                WorkplaceIdentityScenario.PrimaryAppealRulingId);
            Assert.That(appellate.Disposition,
                Is.EqualTo(RulingDisposition.ReversedAndRecognised));
            Assert.That(appellate.CitedHoldingIds, Is.Empty,
                "A holding cannot leak from its later-ruling declaration into the source appeal.");
            Holding holding = result.Report.Holdings.Single();
            Assert.That(holding.HoldingId,
                Is.EqualTo(WorkplaceIdentityScenario.HoldingId));
            Assert.That(holding.SourceAppealId, Is.EqualTo(appeal.AppealId));
            Assert.That(holding.Scope.Reach, Is.EqualTo(PrecedentReach.Employer));
            Assert.That(holding.Scope.BoundEmployerId,
                Is.EqualTo(WorkplaceIdentityScenario.EmployerId));
            Assert.That(holding.Scope.RequiredFacts.Contains(
                    "identity-condition",
                    "identity.superseded-continuity"),
                Is.True);

            AgentActionTrace laterWork = Trace(
                result,
                7,
                WorkplaceIdentityScenario.ContingentHolderAgentId,
                SocietyActionKind.Work);
            Assert.That(laterWork.OpportunityId,
                Is.EqualTo(WorkplaceIdentityScenario.WorkOpportunityId));
            DescendantCase successor = result.Report.DescendantCases.Single(
                candidate =>
                    candidate.CaseId == WorkplaceIdentityScenario.SuccessorCaseId);
            Assert.That(successor.OpenedCycle, Is.EqualTo(8));
            Assert.That(successor.CausalAgentActionId,
                Is.EqualTo(laterWork.ResultEventIds.Single()));
            Assert.That(successor.OriginatingRulingId,
                Is.EqualTo(WorkplaceIdentityScenario.PrimaryInitialRulingId));

            Ruling successorRuling = Ruling(
                result,
                WorkplaceIdentityScenario.SuccessorInitialRulingId);
            Assert.That(successorRuling.Disposition,
                Is.EqualTo(RulingDisposition.Recognised));
            Assert.That(successorRuling.CitedHoldingIds,
                Is.EqualTo(new[] { WorkplaceIdentityScenario.HoldingId }));
            Assert.That(holding.AppliedCaseIds,
                Is.EqualTo(new[] { WorkplaceIdentityScenario.SuccessorCaseId }));

            ExclusiveEntitlementObservation entitlement =
                result.Report.ExclusiveEntitlements.Single();
            Assert.That(entitlement.CurrentHolderAgentId,
                Is.EqualTo(WorkplaceIdentityScenario.LaterClaimantAgentId));
            Assert.That(entitlement.LastMutationCauseId,
                Is.EqualTo(successorRuling.RulingId));
            OfficialStatusMutation[] transferMutations = result.Report
                .OfficialStatusMutations
                .Where(value =>
                    value.CauseId == successorRuling.RulingId &&
                    value.StatusId == entitlement.HolderStatusId)
                .ToArray();
            Assert.That(transferMutations, Has.Length.EqualTo(2));
            Assert.That(transferMutations.Single(value =>
                    value.AffectedAgentId ==
                        WorkplaceIdentityScenario.ContingentHolderAgentId)
                    .AfterRecognised,
                Is.False);
            Assert.That(transferMutations.Single(value =>
                    value.AffectedAgentId ==
                        WorkplaceIdentityScenario.LaterClaimantAgentId)
                    .AfterRecognised,
                Is.True);
            MaterialConsequence[] transferMaterials = result.Report
                .MaterialConsequences
                .Where(value =>
                    value.CauseId == successorRuling.RulingId &&
                    value.ResourceId == entitlement.ResourceId)
                .ToArray();
            Assert.That(transferMaterials, Has.Length.EqualTo(2));
            Assert.That(transferMaterials.Single(value => value.ResourceDelta > 0).Kind,
                Is.EqualTo(MaterialConsequenceKind.BackpayAwarded));
            Assert.That(transferMaterials.Single(value => value.ResourceDelta < 0).Kind,
                Is.EqualTo(MaterialConsequenceKind.WagesLost));
            ConnectedOutcomePair connection = result.Report.ConnectedOutcomes.Single();
            Assert.That(connection.CauseRuleId,
                Is.EqualTo(WorkplaceIdentityScenario.HoldingRuleId));
            Assert.That(connection.ConnectionId,
                Is.EqualTo(WorkplaceIdentityScenario.PaidShiftResourceId));
            Assert.That(connection.WinnerAgentId,
                Is.EqualTo(WorkplaceIdentityScenario.LaterClaimantAgentId));
            Assert.That(connection.WinnerDisplayName, Is.EqualTo("Ivo Reed"));
            Assert.That(connection.WinnerResourceDelta, Is.EqualTo(1));
            Assert.That(connection.LoserAgentId,
                Is.EqualTo(WorkplaceIdentityScenario.ContingentHolderAgentId));
            Assert.That(connection.LoserDisplayName, Is.EqualTo("Mara Quill"));
            Assert.That(connection.LoserResourceDelta, Is.EqualTo(-1));

            Assert.That(result.Report.WorkAllocations, Is.Empty);
            Assert.That(result.AssessorRun.WorkAllocations, Is.Empty);
        }

        [Test]
        public void Policies_AreStructurallyOptionalRatherThanForcedSequence()
        {
            InstitutionalScenarioRunResult reliance = Run(
                WorkplaceIdentityScenario.CreateReliancePolicy());
            InstitutionalScenarioRunResult finalDenial = Run(
                WorkplaceIdentityScenario.CreateFinalDenialPolicy());
            InstitutionalScenarioRunResult precedent = Run(
                WorkplaceIdentityScenario.CreatePrecedentPolicy());

            Assert.That(reliance.Report.RelianceObservations,
                Has.Count.EqualTo(1));
            Assert.That(reliance.Report.Appeals.Single().Disposition,
                Is.EqualTo(AppealDisposition.Reversed));
            Assert.That(reliance.Report.Holdings, Is.Empty);
            Assert.That(reliance.Report.ConnectedOutcomes, Is.Empty);
            AssertCurrentHolder(
                reliance,
                WorkplaceIdentityScenario.ContingentHolderAgentId);

            Assert.That(finalDenial.Report.RelianceObservations, Is.Empty);
            Assert.That(finalDenial.Report.Appeals.Single().Disposition,
                Is.EqualTo(AppealDisposition.Affirmed));
            Ruling denialOnAppeal = Ruling(
                finalDenial,
                WorkplaceIdentityScenario.PrimaryAppealRulingId);
            Assert.That(denialOnAppeal.Disposition,
                Is.EqualTo(RulingDisposition.Affirmed));
            Assert.That(finalDenial.Report.OfficialFindings.Single(
                    finding => finding.FindingId == denialOnAppeal.FindingId)
                    .Disposition,
                Is.EqualTo(FindingDisposition.NotEstablished));
            Assert.That(finalDenial.Report.Holdings, Is.Empty);
            Assert.That(finalDenial.Report.ConnectedOutcomes, Is.Empty);

            Assert.That(precedent.Report.RelianceObservations,
                Has.Count.EqualTo(1));
            Assert.That(precedent.Report.Holdings, Has.Count.EqualTo(1));
            Assert.That(precedent.Report.ConnectedOutcomes,
                Has.Count.EqualTo(1));
            AssertCurrentHolder(
                precedent,
                WorkplaceIdentityScenario.LaterClaimantAgentId);
        }

        [Test]
        public void PrecedentPolicy_ReplaysDeterministically()
        {
            InstitutionalScenarioRunResult first = Run(
                WorkplaceIdentityScenario.CreatePrecedentPolicy());
            InstitutionalScenarioRunResult second = Run(
                WorkplaceIdentityScenario.CreatePrecedentPolicy());

            Assert.That(Signature(first), Is.EqualTo(Signature(second)));
        }

        [Test]
        public void WithoutDisclosure_NoGroundedAppealHoldingOrTransferMaterialises()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            definition.InitialSociety
                .GetAgent(WorkplaceIdentityScenario.PrimaryClaimantAgentId)
                .Standing.CanGiveEvidence = false;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                WorkplaceIdentityScenario.CreatePrecedentPolicy());

            Assert.That(result.Report.EvidenceArtifacts, Is.Empty);
            Assert.That(result.Report.ObservedAgentActions.Any(action =>
                    action.Activity == ObservedActivityKind.EvidenceSubmitted),
                Is.False);
            Assert.That(Ruling(
                    result,
                    WorkplaceIdentityScenario.PrimaryInitialRulingId)
                    .Disposition,
                Is.EqualTo(RulingDisposition.Denied));
            Assert.That(result.Report.RelianceObservations,
                Has.Count.EqualTo(1));
            Assert.That(result.AssessorRun.AssessorActionTraces.Any(trace =>
                    trace.Action == SocietyActionKind.Appeal),
                Is.True,
                "The autonomous attempt remains visible even without legal grounds.");
            Assert.That(result.Report.Appeals, Is.Empty);
            Assert.That(result.Report.Holdings, Is.Empty);
            Assert.That(result.Report.ConnectedOutcomes, Is.Empty);
            Assert.That(Ruling(
                    result,
                    WorkplaceIdentityScenario.SuccessorInitialRulingId)
                    .CitedHoldingIds,
                Is.Empty);
            AssertCurrentHolder(
                result,
                WorkplaceIdentityScenario.ContingentHolderAgentId);
        }

        [Test]
        public void WithoutRelianceChoice_PrecedentStillReachesLaterCase()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            definition.InitialSociety
                .GetAgent(WorkplaceIdentityScenario.PrimaryClaimantAgentId)
                .Standing.CanSeekAid = false;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                WorkplaceIdentityScenario.CreatePrecedentPolicy());

            Assert.That(result.AssessorRun.RelianceLedger, Is.Empty);
            Assert.That(result.Report.RelianceObservations, Is.Empty);
            Assert.That(result.Report.DescendantCases.Any(candidate =>
                    candidate.Kind == DescendantCaseKind.Reliance),
                Is.False);
            Assert.That(result.Report.Appeals.Single().Disposition,
                Is.EqualTo(AppealDisposition.Reversed));
            Assert.That(result.Report.Holdings, Has.Count.EqualTo(1));
            Assert.That(Ruling(
                    result,
                    WorkplaceIdentityScenario.SuccessorInitialRulingId)
                    .CitedHoldingIds,
                Is.EqualTo(new[] { WorkplaceIdentityScenario.HoldingId }));
            Assert.That(result.Report.ConnectedOutcomes, Has.Count.EqualTo(1));
        }

        [Test]
        public void WithoutAppealAction_RelianceSurvivesButNoHoldingOrTransferExists()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            definition.InitialSociety
                .GetAgent(WorkplaceIdentityScenario.PrimaryClaimantAgentId)
                .Standing.CanAppeal = false;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                WorkplaceIdentityScenario.CreatePrecedentPolicy());

            Assert.That(result.Report.RelianceObservations,
                Has.Count.EqualTo(1));
            Assert.That(result.AssessorRun.RelianceLedger.Single().SurvivedReversal,
                Is.False);
            Assert.That(result.Report.Appeals, Is.Empty);
            Assert.That(result.Report.Holdings, Is.Empty);
            Assert.That(result.Report.ConnectedOutcomes, Is.Empty);
            Assert.That(Ruling(
                    result,
                    WorkplaceIdentityScenario.SuccessorInitialRulingId)
                    .CitedHoldingIds,
                Is.Empty);
            AssertCurrentHolder(
                result,
                WorkplaceIdentityScenario.ContingentHolderAgentId);
        }

        [Test]
        public void WithoutDescendantWorkAction_LaterCaseAndTransferRemainAbsent()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            definition.InitialSociety
                .GetAgent(WorkplaceIdentityScenario.ContingentHolderAgentId)
                .Standing.CanWork = false;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                WorkplaceIdentityScenario.CreatePrecedentPolicy());

            Assert.That(result.Report.Holdings, Has.Count.EqualTo(1));
            Assert.That(result.Report.DescendantCases.Any(candidate =>
                    candidate.CaseId == WorkplaceIdentityScenario.SuccessorCaseId),
                Is.False);
            Assert.That(result.Report.Rulings.Any(ruling =>
                    ruling.RulingId ==
                        WorkplaceIdentityScenario.SuccessorInitialRulingId),
                Is.False);
            Assert.That(result.Report.Holdings.Single().AppliedCaseIds, Is.Empty);
            Assert.That(result.Report.ConnectedOutcomes, Is.Empty);
            AssertCurrentHolder(
                result,
                WorkplaceIdentityScenario.ContingentHolderAgentId);
        }

        private static InstitutionalScenarioRunResult Run(
            InstitutionalPolicyConfiguration policy)
        {
            return InstitutionalScenarioEngine.Run(
                WorkplaceIdentityScenario.CreateDefinition(),
                policy);
        }

        private static AgentActionTrace Trace(
            InstitutionalScenarioRunResult result,
            long cycle,
            string actorId,
            SocietyActionKind action)
        {
            return result.AssessorRun.AssessorActionTraces.Single(trace =>
                trace.Cycle == cycle &&
                trace.ActorId == actorId &&
                trace.Action == action);
        }

        private static Ruling Ruling(
            InstitutionalScenarioRunResult result,
            string rulingId)
        {
            return result.Report.Rulings.Single(candidate =>
                candidate.RulingId == rulingId);
        }

        private static void AssertCurrentHolder(
            InstitutionalScenarioRunResult result,
            string expectedAgentId)
        {
            Assert.That(result.Report.ExclusiveEntitlements.Single()
                    .CurrentHolderAgentId,
                Is.EqualTo(expectedAgentId));
        }

        private static string Signature(InstitutionalScenarioRunResult result)
        {
            var rows = new List<string>
            {
                $"top:{result.Report.MasterSeed}:{result.Report.FinalCycle}:" +
                result.Report.PolicyConfigurationId,
            };
            rows.AddRange(result.AssessorRun.AssessorActionTraces.Select(trace =>
                $"trace:{trace.Cycle}:{trace.DecisionId}:{trace.ActorId}:" +
                $"{trace.Action}:{trace.OpportunityId}:{trace.UtilityScore}:" +
                string.Join(",", trace.ResultEventIds)));
            rows.AddRange(result.Report.EvidenceArtifacts.Select(evidence =>
                $"evidence:{evidence.ArtifactId}:{evidence.SourceTemplateId}:" +
                $"{evidence.Reliability}"));
            rows.AddRange(result.Report.Rulings.Select(ruling =>
                $"ruling:{ruling.RulingId}:{ruling.Disposition}:" +
                string.Join(",", ruling.EvidenceArtifactIds) + ":" +
                string.Join(",", ruling.CitedHoldingIds)));
            rows.AddRange(result.Report.OfficialStatusMutations.Select(mutation =>
                $"mutation:{mutation.MutationId}:{mutation.CauseId}:" +
                $"{mutation.AffectedAgentId}:{mutation.StatusId}:" +
                $"{mutation.AfterRecognised}:{mutation.ResourceDelta}"));
            rows.AddRange(result.Report.RelianceObservations.Select(observation =>
                $"reliance:{observation.ObservationId}:" +
                $"{observation.SourceActionEventId}:" +
                $"{observation.RecordedResourceDelta}"));
            rows.AddRange(result.Report.Appeals.Select(appeal =>
                $"appeal:{appeal.AppealId}:{appeal.Disposition}:" +
                appeal.ResultingRulingId));
            rows.AddRange(result.Report.Holdings.Select(holding =>
                $"holding:{holding.HoldingId}:{holding.RuleId}:" +
                string.Join(",", holding.AppliedCaseIds)));
            rows.AddRange(result.Report.DescendantCases.Select(candidate =>
                $"descendant:{candidate.CaseId}:{candidate.Kind}:" +
                $"{candidate.CausalAgentActionId}:{candidate.OriginatingRulingId}"));
            rows.AddRange(result.Report.MaterialConsequences.Select(material =>
                $"material:{material.ConsequenceId}:{material.CauseId}:" +
                $"{material.AgentId}:{material.ResourceDelta}"));
            rows.AddRange(result.Report.ConnectedOutcomes.Select(connection =>
                $"connection:{connection.PairId}:{connection.CauseRuleId}:" +
                $"{connection.WinnerAgentId}:{connection.LoserAgentId}"));
            rows.AddRange(result.Report.ExclusiveEntitlements.Select(entitlement =>
                $"entitlement:{entitlement.EntitlementId}:" +
                $"{entitlement.CurrentHolderAgentId}:" +
                entitlement.LastMutationCauseId));
            return string.Join("\n", rows);
        }
    }
}
