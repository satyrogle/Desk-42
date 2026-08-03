using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using Desk42.Institutional.Scenarios.WorkplaceIdentity;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalScenarioEngineTests
    {
        [Test]
        public void Run_IssuesDeclaredInitialRulingAndMutatesOnlyAfterFrozenDecision()
        {
            InstitutionalScenarioDefinition definition = Definition("alpha");
            string claimantRoleId = "role.alpha.claimant";
            string claimantAgentId = "agent.alpha.claimant";
            string statusId = "status.alpha.after-ruling";

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                Policy("alpha"));

            Assert.That(result.Report.Rulings, Has.Count.EqualTo(1));
            Assert.That(result.Report.Rulings[0].RulingId,
                Is.EqualTo("ruling:case.alpha:initial:1"));
            Assert.That(result.Report.Rulings[0].Disposition,
                Is.EqualTo(RulingDisposition.Denied));
            Assert.That(result.Report.FinalCycle, Is.EqualTo(2));
            Assert.That(result.AgentIdByRole[claimantRoleId], Is.EqualTo(claimantAgentId));
            Assert.That(result.AssessorRun, Is.Not.Null);
            Assert.That(result.EntitlementRegistry.Count, Is.Zero);

            AgentActionTrace firstClaimantDecision = result.AssessorRun.AssessorActionTraces
                .Single(trace => trace.Cycle == 1 && trace.ActorId == claimantAgentId);
            Assert.That(
                firstClaimantDecision.PerceptionSnapshot.Standing.IsRecognised(statusId),
                Is.False,
                "The cycle-one decision must not observe its later deadline mutation.");
            Assert.That(
                result.AssessorRun.FinalSocietyState.GetAgent(claimantAgentId)
                    .Standing.IsRecognised(statusId),
                Is.True);
            Assert.That(result.Report.OfficialStatusMutations, Has.Count.EqualTo(1));

            Assert.That(definition.InitialSociety.CurrentTick, Is.Zero);
            Assert.That(
                definition.InitialSociety.GetAgent(claimantAgentId)
                    .Standing.IsRecognised(statusId),
                Is.False,
                "Execution must not mutate authored scenario state.");
        }

        [Test]
        public void Run_RenamedEquivalentScenarioPreservesShapeWithoutIdentifierLeakage()
        {
            InstitutionalScenarioRunResult first = InstitutionalScenarioEngine.Run(
                Definition("first"),
                Policy("first"));
            InstitutionalScenarioRunResult second = InstitutionalScenarioEngine.Run(
                Definition("second"),
                Policy("second"));

            Assert.That(first.Report.Rulings[0].RulingId,
                Is.EqualTo("ruling:case.first:initial:1"));
            Assert.That(second.Report.Rulings[0].RulingId,
                Is.EqualTo("ruling:case.second:initial:1"));
            Assert.That(second.Report.PrimaryCaseId, Is.EqualTo("case.second"));
            Assert.That(second.Report.Rulings[0].CaseId, Is.EqualTo("case.second"));
            Assert.That(second.Report.Rulings[0].RulingId, Does.Not.Contain("first"));

            CollectionAssert.AreEqual(
                first.Report.Rulings.Select(ruling => ruling.Disposition),
                second.Report.Rulings.Select(ruling => ruling.Disposition));
            CollectionAssert.AreEqual(
                first.Report.Timeline.Select(entry => entry.Kind),
                second.Report.Timeline.Select(entry => entry.Kind));
            Assert.That(first.Report.ObservedAgentActions.Count,
                Is.EqualTo(second.Report.ObservedAgentActions.Count));
        }

        [Test]
        public void Run_ExactTriggerEvidenceMayPrecedeOnlyItsMaterialisedDescendant()
        {
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                DescendantTriggerDefinition(),
                Policy("trigger"));

            DescendantCase descendant = result.Report.DescendantCases.Single();
            Assert.That(descendant.CaseId, Is.EqualTo("case.trigger-descendant"));
            Assert.That(descendant.OpenedCycle, Is.EqualTo(3));
            EvidenceArtifact triggerEvidence = result.Report.EvidenceArtifacts.Single(value =>
                value.CaseId == descendant.CaseId);
            Assert.That(triggerEvidence.EnteredCycle, Is.EqualTo(2));
            Assert.That(triggerEvidence.EnteredCycle, Is.LessThan(descendant.OpenedCycle));
            Assert.That(triggerEvidence.Provenance.SourceSocietyEventId,
                Is.EqualTo(descendant.CausalAgentActionId));
            Assert.That(result.Report.Rulings.Any(value =>
                value.CaseId == descendant.CaseId), Is.True);
        }

        [Test]
        public void Run_SameCycleEvidenceActionOpensPrimaryBeforeItsRuling()
        {
            InstitutionalScenarioDefinition definition =
                EvidenceActivatedPrimaryDefinition(canWork: true);

            InstitutionalScenarioRunResult first = InstitutionalScenarioEngine.Run(
                definition,
                Policy("activation"));
            InstitutionalScenarioRunResult replay = InstitutionalScenarioEngine.Run(
                EvidenceActivatedPrimaryDefinition(canWork: true),
                Policy("activation"));

            InstitutionalCaseOpening opening = first.Report.CaseOpenings.Single();
            EvidenceArtifact trigger = first.Report.EvidenceArtifacts.Single();
            Ruling ruling = first.Report.Rulings.Single();
            Assert.That(opening.OpenedCycle, Is.EqualTo(1));
            Assert.That(opening.TriggerEvidenceArtifactId,
                Is.EqualTo(trigger.ArtifactId));
            Assert.That(opening.CausalAgentActionId,
                Is.EqualTo(trigger.Provenance.SourceSocietyEventId));
            Assert.That(ruling.Cycle, Is.EqualTo(opening.OpenedCycle));
            Assert.That(first.Report.Timeline.FindIndex(entry =>
                    entry.Kind == InstitutionalTimelineKind.EvidenceEntered),
                Is.LessThan(first.Report.Timeline.FindIndex(entry =>
                    entry.Kind == InstitutionalTimelineKind.CaseOpened)));
            Assert.That(first.Report.Timeline.FindIndex(entry =>
                    entry.Kind == InstitutionalTimelineKind.CaseOpened),
                Is.LessThan(first.Report.Timeline.FindIndex(entry =>
                    entry.Kind == InstitutionalTimelineKind.RulingIssued)));

            Assert.That(replay.Report.CaseOpenings.Select(value =>
                    $"{value.ActivationId}|{value.CaseId}|{value.OpenedCycle}|" +
                    $"{value.TriggerEvidenceArtifactId}|{value.CausalAgentActionId}"),
                Is.EqualTo(first.Report.CaseOpenings.Select(value =>
                    $"{value.ActivationId}|{value.CaseId}|{value.OpenedCycle}|" +
                    $"{value.TriggerEvidenceArtifactId}|{value.CausalAgentActionId}")));
        }

        [Test]
        public void Run_WithoutTriggerAction_PrimaryNeverOpensOrRules()
        {
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                EvidenceActivatedPrimaryDefinition(canWork: false),
                Policy("activation"));

            Assert.That(result.Report.EvidenceArtifacts, Is.Empty);
            Assert.That(result.Report.CaseOpenings, Is.Empty);
            Assert.That(result.Report.Rulings, Is.Empty);
            Assert.That(result.Report.OfficialStatusMutations, Is.Empty);
        }

        [Test]
        public void RunValidator_RejectsUndeclaredCaseOpening()
        {
            InstitutionalScenarioDefinition definition = Definition("undeclared-opening");
            definition.InitialSociety.GetAgent("agent.undeclared-opening.claimant")
                .Standing.CanWork = true;
            InstitutionalPolicyConfiguration policy = Policy("undeclared-opening");
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                policy);
            EvidenceArtifact trigger = result.Report.EvidenceArtifacts.Single();
            result.Report.CaseOpenings.Add(new InstitutionalCaseOpening
            {
                ActivationId = "activation.undeclared",
                CaseId = definition.PrimaryCaseId,
                OpenedCycle = definition.Cases[0].OpenCycle,
                TriggerEvidenceArtifactId = trigger.ArtifactId,
                CausalAgentActionId = trigger.Provenance.SourceSocietyEventId,
            });

            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(definition, policy, result));
        }

        [Test]
        public void RunValidator_RejectsDuplicateAndForgedCaseOpenings()
        {
            InstitutionalScenarioDefinition duplicateDefinition =
                EvidenceActivatedPrimaryDefinition(canWork: true);
            InstitutionalPolicyConfiguration duplicatePolicy = Policy("activation");
            InstitutionalScenarioRunResult duplicate = InstitutionalScenarioEngine.Run(
                duplicateDefinition,
                duplicatePolicy);
            duplicate.Report.CaseOpenings.Add(duplicate.Report.CaseOpenings.Single());
            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(duplicateDefinition, duplicatePolicy, duplicate));

            InstitutionalScenarioDefinition forgedDefinition =
                EvidenceActivatedPrimaryDefinition(canWork: true);
            InstitutionalPolicyConfiguration forgedPolicy = Policy("activation");
            InstitutionalScenarioRunResult forged = InstitutionalScenarioEngine.Run(
                forgedDefinition,
                forgedPolicy);
            forged.Report.CaseOpenings.Single().CausalAgentActionId =
                "action.forged";
            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(forgedDefinition, forgedPolicy, forged));
        }

        [Test]
        public void EvidenceActivation_EquivalentLiveRunReentryIsIdempotent()
        {
            InstitutionalScenarioDefinition definition =
                EvidenceActivatedPrimaryDefinition(canWork: true);
            InstitutionalPolicyConfiguration policy = Policy("activation");
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                policy);
            var context = new InstitutionalScenarioExecutionContext(
                definition,
                policy,
                result.AssessorRun,
                result.AgentIdByRole);
            int timelineCount = result.Report.Timeline.Count;

            InstitutionalEvidenceActivatedCaseService.OpenDueCases(context, 1);

            Assert.That(result.Report.CaseOpenings, Has.Count.EqualTo(1));
            Assert.That(result.Report.Timeline, Has.Count.EqualTo(timelineCount));
        }

        [TestCase("class")]
        [TestCase("effect")]
        [TestCase("weight")]
        [TestCase("visibility")]
        [TestCase("kind")]
        [TestCase("submission")]
        public void RunValidator_RejectsAlteredTriggerTemplateSemantics(
            string alteration)
        {
            InstitutionalScenarioDefinition definition =
                EvidenceActivatedPrimaryDefinition(canWork: true);
            InstitutionalPolicyConfiguration policy = Policy("activation");
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                policy);
            EvidenceArtifact trigger = result.Report.EvidenceArtifacts.Single();

            switch (alteration)
            {
                case "class":
                    trigger.EvidenceClassId = "evidence-class.forged";
                    break;
                case "effect":
                    trigger.Effect = EvidenceEffect.OpposesFinding;
                    break;
                case "weight":
                    trigger.BaseWeight++;
                    break;
                case "visibility":
                    trigger.Provenance.Visibility = EvidenceVisibility.Private;
                    break;
                case "kind":
                    trigger.Kind = EvidenceArtifactKind.ClaimantStatement;
                    break;
                case "submission":
                    trigger.OfficiallySubmitted = false;
                    break;
                default:
                    Assert.Fail($"Unknown trigger alteration '{alteration}'.");
                    break;
            }

            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(definition, policy, result));
        }

        [Test]
        public void RunValidator_RejectsNonTriggerEvidenceAtCaseOpening()
        {
            InstitutionalScenarioDefinition definition =
                EvidenceActivatedPrimaryDefinition(canWork: true);
            InstitutionalPolicyConfiguration policy = Policy("activation");
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                policy);
            EvidenceArtifact trigger = result.Report.EvidenceArtifacts.Single();
            result.Report.EvidenceArtifacts.Add(CopyArtifact(
                trigger,
                "artifact.activation.unrelated"));

            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(definition, policy, result));
        }

        [Test]
        public void CausalValidator_RejectsMissingOrPostRulingCaseOpeningTimeline()
        {
            InstitutionalScenarioDefinition missingDefinition =
                EvidenceActivatedPrimaryDefinition(canWork: true);
            InstitutionalPolicyConfiguration missingPolicy = Policy("activation");
            InstitutionalScenarioRunResult missing = InstitutionalScenarioEngine.Run(
                missingDefinition,
                missingPolicy);
            missing.Report.Timeline.RemoveAll(entry =>
                entry.Kind == InstitutionalTimelineKind.CaseOpened);
            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(missingDefinition, missingPolicy, missing));

            InstitutionalScenarioDefinition reorderedDefinition =
                EvidenceActivatedPrimaryDefinition(canWork: true);
            InstitutionalPolicyConfiguration reorderedPolicy = Policy("activation");
            InstitutionalScenarioRunResult reordered = InstitutionalScenarioEngine.Run(
                reorderedDefinition,
                reorderedPolicy);
            InstitutionalTimelineEntry opening = reordered.Report.Timeline.Single(entry =>
                entry.Kind == InstitutionalTimelineKind.CaseOpened);
            reordered.Report.Timeline.Remove(opening);
            int rulingIndex = reordered.Report.Timeline.FindIndex(entry =>
                entry.Kind == InstitutionalTimelineKind.RulingIssued);
            reordered.Report.Timeline.Insert(rulingIndex + 1, opening);

            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(reorderedDefinition, reorderedPolicy, reordered));
        }

        [Test]
        public void Validators_RejectFindingOrRulingBeforeCaseOpening()
        {
            InstitutionalScenarioDefinition findingDefinition =
                EvidenceActivatedPrimaryDefinition(canWork: true);
            InstitutionalPolicyConfiguration findingPolicy = Policy("activation");
            InstitutionalScenarioRunResult finding = InstitutionalScenarioEngine.Run(
                findingDefinition,
                findingPolicy);
            finding.Report.OfficialFindings.Single().Cycle = 0;
            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(findingDefinition, findingPolicy, finding));

            InstitutionalScenarioDefinition rulingDefinition =
                EvidenceActivatedPrimaryDefinition(canWork: true);
            InstitutionalPolicyConfiguration rulingPolicy = Policy("activation");
            InstitutionalScenarioRunResult ruling = InstitutionalScenarioEngine.Run(
                rulingDefinition,
                rulingPolicy);
            ruling.Report.Rulings.Single().Cycle = 0;
            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(rulingDefinition, rulingPolicy, ruling));
        }

        [Test]
        public void Run_AdjudicationOnlyCitationLeavesInitialDeniedThenChangesAppeal()
        {
            InstitutionalScenarioDefinition definition =
                AdjudicationOnlyCitationDefinition();
            InstitutionalPolicyConfiguration policy =
                WorkplaceIdentityScenario.CreatePrecedentPolicy();
            policy.EvidenceClassWeights.Add(new EvidenceClassWeight
            {
                EvidenceClassId = "evidence-class.workplace.successor-action",
                WeightPercent = 100,
            });

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                policy);

            ScenarioCaseDefinition targetCase = definition.Cases.Single(value =>
                value.CaseId == WorkplaceIdentityScenario.SuccessorCaseId);
            Ruling initial = result.Report.Rulings.Single(value =>
                value.RulingId == targetCase.InitialRulingId);
            Ruling adjudication = result.Report.Rulings.Single(value =>
                value.RulingId == targetCase.AdjudicationRulingId);
            Assert.That(initial.Disposition, Is.EqualTo(RulingDisposition.Denied));
            Assert.That(initial.CitedHoldingIds, Is.Empty);
            Assert.That(adjudication.Disposition,
                Is.EqualTo(RulingDisposition.ReversedAndRecognised));
            Assert.That(adjudication.CitedHoldingIds,
                Is.EqualTo(new[] { WorkplaceIdentityScenario.HoldingId }));
            Assert.That(result.Report.Holdings.Single().AppliedCaseIds,
                Is.EqualTo(new[] { WorkplaceIdentityScenario.SuccessorCaseId }));

            ScenarioExclusiveEntitlementTransferDefinition transfer =
                definition.EntitlementTransfers.Single();
            ExclusiveEntitlementObservation entitlement =
                result.Report.ExclusiveEntitlements.Single();
            Assert.That(entitlement.CurrentHolderAgentId,
                Is.EqualTo(WorkplaceIdentityScenario.LaterClaimantAgentId));
            Assert.That(entitlement.LastMutationCauseId,
                Is.EqualTo(adjudication.RulingId));
            OfficialStatusMutation[] transferMutations = result.Report
                .OfficialStatusMutations
                .Where(value =>
                    value.CauseId == adjudication.RulingId &&
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
                    value.CauseId == adjudication.RulingId &&
                    value.ResourceId == entitlement.ResourceId)
                .ToArray();
            Assert.That(transferMaterials, Has.Length.EqualTo(2));
            Assert.That(transferMaterials.All(value =>
                value.CauseId == adjudication.RulingId), Is.True);
            ConnectedOutcomePair connected = result.Report.ConnectedOutcomes.Single();
            Assert.That(connected.PairId,
                Is.EqualTo($"connected:{transfer.TransferId}"));
            Assert.That(connected.WinnerAgentId,
                Is.EqualTo(WorkplaceIdentityScenario.LaterClaimantAgentId));
            Assert.That(connected.LoserAgentId,
                Is.EqualTo(WorkplaceIdentityScenario.ContingentHolderAgentId));
            Assert.That(result.Report.OfficialStatusMutations.Any(value =>
                    value.CauseId == initial.RulingId &&
                    value.StatusId == entitlement.HolderStatusId),
                Is.False,
                "The denied initial ruling must not transfer the entitlement.");
            Assert.That(result.Report.MaterialConsequences.Any(value =>
                    value.CauseId == initial.RulingId &&
                    value.ResourceId == entitlement.ResourceId),
                Is.False);
        }

        [Test]
        public void RunValidator_RejectsHoldingMovedToUndeclaredAppellateRuling()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            InstitutionalPolicyConfiguration policy =
                WorkplaceIdentityScenario.CreatePrecedentPolicy();
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                policy);
            Holding holding = result.Report.Holdings.Single();
            Ruling appellate = result.Report.Rulings.Single(value =>
                value.RulingId == WorkplaceIdentityScenario.PrimaryAppealRulingId);
            appellate.CitedHoldingIds.Add(holding.HoldingId);
            appellate.CitedScopeIds.Add(holding.Scope.ScopeId);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    ValidateMutatedRun(definition, policy, result));
            Assert.That(exception.Message, Does.Contain("undeclared holding"));
        }

        [Test]
        public void Run_DelaysDeclaredReliancePublicRowsWithoutDelayingAuthorityEffects()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            ScenarioIrreversibleRelianceDefinition declaration =
                definition.RelianceDefinitions.Single();
            declaration.PublicObservationCycle = declaration.Cycle + 1;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                WorkplaceIdentityScenario.CreateReliancePolicy());

            RelianceEvent authority = result.AssessorRun.RelianceLedger.Single();
            RelianceObservation observation =
                result.Report.RelianceObservations.Single();
            Assert.AreEqual(declaration.Cycle, authority.Cycle);
            Assert.AreEqual(declaration.PublicObservationCycle, observation.Cycle);
            Assert.IsEmpty(result.AssessorRun.PendingReliancePublicProjections);
            Assert.That(authority.AppliedEffects, Is.Not.Empty);
            Assert.That(authority.AppliedEffects.All(effect =>
                result.Report.MaterialConsequences.Single(material =>
                    material.ConsequenceId == effect.MaterialConsequenceId).Cycle ==
                declaration.PublicObservationCycle));
            Assert.That(result.Report.Timeline.Single(entry =>
                entry.Kind == InstitutionalTimelineKind.RelianceCreated).Cycle,
                Is.EqualTo(declaration.PublicObservationCycle));

            observation.Cycle = declaration.Cycle;
            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    InstitutionalScenarioRunValidator.Validate(result));
            Assert.That(exception.Message,
                Does.Contain("action, authority state or public observation"));
        }

        [TestCase("clear-effects")]
        [TestCase("recorded-choice")]
        [TestCase("effect-agent")]
        [TestCase("material-kind")]
        [TestCase("material-resource")]
        [TestCase("material-need")]
        [TestCase("trace-status")]
        public void RunValidator_RejectsRelianceProjectionThatDiffersFromDeclaration(
            string corruption)
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            InstitutionalPolicyConfiguration policy =
                WorkplaceIdentityScenario.CreatePrecedentPolicy();
            InstitutionalScenarioRunResult result =
                InstitutionalScenarioEngine.Run(definition, policy);
            RelianceEvent reliance = result.AssessorRun.RelianceLedger.Single();
            RelianceAppliedEffect applied = reliance.AppliedEffects[0];
            MaterialConsequence material = result.Report.MaterialConsequences.Single(
                value => value.ConsequenceId == applied.MaterialConsequenceId);

            switch (corruption)
            {
                case "clear-effects":
                    reliance.AppliedEffects.Clear();
                    break;
                case "recorded-choice":
                    result.Report.RelianceObservations.Single().RecordedChoiceId =
                        "choice.forged";
                    break;
                case "effect-agent":
                    applied.AgentId = WorkplaceIdentityScenario.EmployerAgentId;
                    break;
                case "material-kind":
                    material.Kind = material.Kind == MaterialConsequenceKind.ReliefPaid
                        ? MaterialConsequenceKind.RelianceSpent
                        : MaterialConsequenceKind.ReliefPaid;
                    break;
                case "material-resource":
                    material.ResourceId = "resource.forged";
                    break;
                case "material-need":
                    material.NeedPressureAfter++;
                    break;
                case "trace-status":
                    AgentActionTrace sourceTrace = result.AssessorRun.AssessorActionTraces
                        .Single(trace => trace.ResultEventIds.Contains(
                            reliance.SourceActionEventId));
                    if (sourceTrace.Action == SocietyActionKind.Work)
                    {
                        sourceTrace.InputSnapshot.WorkOpportunities.Single(value =>
                                value.OpportunityId == sourceTrace.OpportunityId)
                            .RequiredOfficialStatusId = "status.forged";
                    }
                    else
                    {
                        sourceTrace.InputSnapshot.AidOpportunities.Single(value =>
                                value.OpportunityId == sourceTrace.OpportunityId)
                            .RequiredOfficialStatusId = "status.forged";
                    }
                    break;
                default:
                    Assert.Fail($"Unknown corruption '{corruption}'.");
                    break;
            }

            Assert.Throws<InvalidOperationException>(() =>
                ValidateMutatedRun(definition, policy, result));
        }

        [Test]
        public void RunValidator_RejectsAnOmittedDeclaredRelianceEnvelope()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            InstitutionalPolicyConfiguration policy =
                WorkplaceIdentityScenario.CreatePrecedentPolicy();
            InstitutionalScenarioRunResult result =
                InstitutionalScenarioEngine.Run(definition, policy);
            RelianceEvent reliance = result.AssessorRun.RelianceLedger.Single();
            RelianceObservation observation =
                result.Report.RelianceObservations.Single();

            result.AssessorRun.RelianceLedger.Clear();
            result.Report.RelianceObservations.Clear();
            result.Report.MaterialConsequences.RemoveAll(value =>
                value.CauseId == reliance.SourceActionEventId);
            result.Report.Timeline.RemoveAll(value =>
                value.Kind == InstitutionalTimelineKind.RelianceCreated &&
                value.DetailId == observation.ObservationId);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    ValidateMutatedRun(definition, policy, result));
            Assert.That(exception.Message,
                Does.Contain("omits or invents a conditionally activated"));
        }

        [TestCase("omission")]
        [TestCase("case-id")]
        [TestCase("trigger")]
        [TestCase("respondent")]
        [TestCase("issue")]
        [TestCase("facts")]
        public void RunValidator_RejectsAnOmittedOrReattributedDeclaredRecovery(
            string corruption)
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            InstitutionalPolicyConfiguration policy =
                WorkplaceIdentityScenario.CreatePrecedentPolicy();
            InstitutionalScenarioRunResult result =
                InstitutionalScenarioEngine.Run(definition, policy);
            DescendantCase recovery = result.Report.DescendantCases.Single(value =>
                value.Kind == DescendantCaseKind.Reliance);

            switch (corruption)
            {
                case "omission":
                    result.Report.DescendantCases.Remove(recovery);
                    result.Report.ObservedAgentActions.Single(value =>
                            value.ActionEventId == recovery.CausalAgentActionId)
                        .ResultDescendantCaseIds.Remove(recovery.CaseId);
                    result.Report.Timeline.RemoveAll(value =>
                        value.Kind == InstitutionalTimelineKind.DescendantCaseOpened &&
                        value.SubjectId == recovery.CaseId);
                    result.AssessorRun.RelianceLedger.Single().SurvivedReversal = false;
                    break;
                case "case-id":
                    recovery.CaseId = "case.workplace.reliance-recovery:forged";
                    break;
                case "trigger":
                    recovery.ParentCauseId =
                        WorkplaceIdentityScenario.PrimaryInitialRulingId;
                    break;
                case "respondent":
                    recovery.RespondentId =
                        WorkplaceIdentityScenario.DependentAgentId;
                    break;
                case "issue":
                    recovery.OfficialIssueId = "issue.workplace.forged";
                    break;
                case "facts":
                    recovery.Facts.Facts[0].Value = "choice.workplace.forged";
                    break;
                default:
                    Assert.Fail($"Unknown corruption '{corruption}'.");
                    break;
            }

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    ValidateMutatedRun(definition, policy, result));
            Assert.That(exception.Message, Does.Contain("Reliance recovery"));
        }

        [Test]
        public void RunValidator_AcceptsADeclaredRelianceWhoseConditionsNeverActivate()
        {
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                WorkplaceIdentityScenario.CreateDefinition(),
                WorkplaceIdentityScenario.CreateFinalDenialPolicy());

            Assert.IsEmpty(result.AssessorRun.RelianceLedger);
            Assert.IsEmpty(result.Report.RelianceObservations);
            Assert.DoesNotThrow(() =>
                InstitutionalScenarioRunValidator.Validate(result));
        }

        [Test]
        public void RunValidator_RejectsRemovedOrReattributedTransferProjection()
        {
            InstitutionalScenarioDefinition removedDefinition =
                WorkplaceIdentityScenario.CreateDefinition();
            InstitutionalPolicyConfiguration removedPolicy =
                WorkplaceIdentityScenario.CreatePrecedentPolicy();
            InstitutionalScenarioRunResult removed = InstitutionalScenarioEngine.Run(
                removedDefinition,
                removedPolicy);
            MaterialConsequence removedGain = removed.Report.MaterialConsequences.Single(
                value =>
                    value.CauseId == WorkplaceIdentityScenario.SuccessorInitialRulingId &&
                    value.ResourceId == WorkplaceIdentityScenario.PaidShiftResourceId &&
                    value.ResourceDelta > 0);
            removed.Report.MaterialConsequences.Remove(removedGain);
            InvalidOperationException removedException =
                Assert.Throws<InvalidOperationException>(() =>
                    ValidateMutatedRun(removedDefinition, removedPolicy, removed));
            Assert.That(removedException.Message,
                Does.Contain("lacks its exact conserved material pair"));

            InstitutionalScenarioDefinition reattributedDefinition =
                WorkplaceIdentityScenario.CreateDefinition();
            InstitutionalPolicyConfiguration reattributedPolicy =
                WorkplaceIdentityScenario.CreatePrecedentPolicy();
            InstitutionalScenarioRunResult reattributed = InstitutionalScenarioEngine.Run(
                reattributedDefinition,
                reattributedPolicy);
            OfficialStatusMutation reattributedGain = reattributed.Report
                .OfficialStatusMutations.Single(value =>
                    value.CauseId ==
                        WorkplaceIdentityScenario.SuccessorInitialRulingId &&
                    value.StatusId == WorkplaceIdentityScenario.PaidShiftHolderStatusId &&
                    value.AfterRecognised);
            reattributedGain.CauseId = WorkplaceIdentityScenario.PrimaryAppealRulingId;
            InvalidOperationException reattributedException =
                Assert.Throws<InvalidOperationException>(() =>
                    ValidateMutatedRun(
                        reattributedDefinition,
                        reattributedPolicy,
                        reattributed));
            Assert.That(reattributedException.Message,
                Does.Contain("lacks its exact paired status mutations"));
        }

        [Test]
        public void RunValidator_RejectsReattributedConnectedOutcome()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            InstitutionalPolicyConfiguration policy =
                WorkplaceIdentityScenario.CreatePrecedentPolicy();
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                policy);
            result.Report.ConnectedOutcomes.Single().CauseRuleId = "rule.forged";

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    ValidateMutatedRun(definition, policy, result));

            Assert.That(exception.Message,
                Does.Contain("connected outcome was reattributed"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RunValidator_RejectsPublicEntitlementChainTamper(bool mutateCause)
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            InstitutionalPolicyConfiguration policy =
                WorkplaceIdentityScenario.CreatePrecedentPolicy();
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                policy);
            ExclusiveEntitlementObservation observation =
                result.Report.ExclusiveEntitlements.Single();
            if (mutateCause)
            {
                observation.LastMutationCauseId =
                    WorkplaceIdentityScenario.PrimaryAppealRulingId;
            }
            else
            {
                observation.CurrentHolderAgentId =
                    WorkplaceIdentityScenario.ContingentHolderAgentId;
            }

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    ValidateMutatedRun(definition, policy, result));

            Assert.That(exception.Message,
                Does.Contain("Public entitlement").And
                    .Contain("eligible transfer chain"));
        }

        private static InstitutionalScenarioDefinition
            EvidenceActivatedPrimaryDefinition(bool canWork)
        {
            InstitutionalScenarioDefinition definition = Definition("activation");
            definition.Cases[0].OpenCycle = 1;
            definition.EvidenceActivatedCases.Add(
                new ScenarioEvidenceActivatedCaseDefinition
                {
                    ActivationId = "activation.primary.work",
                    CaseId = "case.activation",
                    EvidenceTemplateId = "evidence.activation.work",
                    TriggerCycle = 1,
                });
            definition.InitialSociety.GetAgent("agent.activation.claimant")
                .Standing.CanWork = canWork;
            return definition;
        }

        private static void ValidateMutatedRun(
            InstitutionalScenarioDefinition definition,
            InstitutionalPolicyConfiguration policy,
            InstitutionalScenarioRunResult result)
        {
            InstitutionalScenarioRunValidator.Validate(result);
        }

        private static EvidenceArtifact CopyArtifact(
            EvidenceArtifact source,
            string artifactId)
        {
            return new EvidenceArtifact
            {
                ArtifactId = artifactId,
                CaseId = source.CaseId,
                EnteredCycle = source.EnteredCycle,
                Kind = source.Kind,
                EvidenceClassId = source.EvidenceClassId,
                SourceTemplateId = source.SourceTemplateId,
                IssueId = source.IssueId,
                PropositionId = source.PropositionId,
                OfficialEmployerId = source.OfficialEmployerId,
                OfficialIdentityConditionId = source.OfficialIdentityConditionId,
                OfficialResourceId = source.OfficialResourceId,
                Effect = source.Effect,
                BaseWeight = source.BaseWeight,
                Reliability = source.Reliability,
                OfficiallySubmitted = source.OfficiallySubmitted,
                SuppressedByAgentId = source.SuppressedByAgentId,
                KnownByAgentIds = new List<string>(source.KnownByAgentIds),
                EnteredAfterInitialRuling = source.EnteredAfterInitialRuling,
                Provenance = new EvidenceProvenance
                {
                    ProvenanceId = "provenance.activation.unrelated",
                    CreatedCycle = source.Provenance.CreatedCycle,
                    SourceAgentId = source.Provenance.SourceAgentId,
                    SourceDecisionId = source.Provenance.SourceDecisionId,
                    SourceSocietyEventId = source.Provenance.SourceSocietyEventId,
                    SourceRecordId = source.Provenance.SourceRecordId,
                    Visibility = source.Provenance.Visibility,
                    CreatedByAgentAction = source.Provenance.CreatedByAgentAction,
                    ChainOfCustodyIds = new List<string>(
                        source.Provenance.ChainOfCustodyIds),
                },
            };
        }

        private static InstitutionalScenarioDefinition
            AdjudicationOnlyCitationDefinition()
        {
            const string appealId = "appeal.workplace.successor-runtime";
            const string appealOpportunityId = "opportunity.workplace.appeal-successor";
            const string evidenceTemplateId =
                "evidence-template.workplace.successor-work-runtime";
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            ScenarioCaseDefinition targetCase = definition.Cases.Single(value =>
                value.CaseId == WorkplaceIdentityScenario.SuccessorCaseId);
            targetCase.InitialRulingCycle = 8;
            targetCase.InitialRulingId =
                "ruling:case.workplace.successor-shift:initial:8";

            definition.InitialSociety
                .GetAgent(WorkplaceIdentityScenario.LaterClaimantAgentId)
                .Standing.CanAppeal = true;
            definition.Opportunities.Insert(1, new ScenarioOpportunityDefinition
            {
                OpportunityId = appealOpportunityId,
                Kind = ScenarioOpportunityKind.Appeal,
                PurposeId = "purpose.runtime.challenge-successor-denial",
                SourceCauseId = "cause.runtime.successor-appeal-window",
                AvailabilityStartCycle = 9,
                AvailabilityEndCycle = 9,
                UtilityBonus = 1000,
                CaseId = targetCase.CaseId,
                ChallengedRulingId = targetCase.InitialRulingId,
                HearingCycle = 10,
                EligibleRoleIds = new List<string>
                {
                    WorkplaceIdentityScenario.LaterClaimantRoleId,
                },
            });
            ScenarioCycleScheduleEntry filingSchedule =
                definition.CycleSchedule.Single(value => value.Cycle == 9);
            filingSchedule.AppealWindowOpen = true;
            filingSchedule.OpenDocketId = "docket.runtime.successor";
            filingSchedule.ActiveOpportunityIds.Add(appealOpportunityId);
            definition.EvidenceTemplates.Add(new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = evidenceTemplateId,
                SourceEventKind = SocietyEventKind.WorkPerformed,
                SourceOpportunityId = WorkplaceIdentityScenario.WorkOpportunityId,
                CaseId = targetCase.CaseId,
                IssueId = targetCase.IssueId,
                EvidenceClassId = "evidence-class.workplace.successor-action",
                Effect = EvidenceEffect.SupportsFinding,
                Weight = 10,
                Visibility = EvidenceVisibility.OfficialRecord,
            });
            definition.Appeals.Add(new ScenarioAppealDefinition
            {
                AppealId = appealId,
                CaseId = targetCase.CaseId,
                OpportunityId = appealOpportunityId,
                AppellantRoleId = WorkplaceIdentityScenario.LaterClaimantRoleId,
                FilingCycle = 9,
                HearingCycle = 10,
                ChallengedRulingId = targetCase.InitialRulingId,
                ResultingRulingId = targetCase.AdjudicationRulingId,
                GroundsEvidenceTemplateIds = new List<string>
                {
                    evidenceTemplateId,
                },
            });
            definition.OfficialStatusEffectRequests.Add(
                new ScenarioOfficialStatusEffectRequest
                {
                    EffectRequestId =
                        "effect.workplace.successor-adverse-decision",
                    Cycle = 8,
                    CauseCaseId = targetCase.CaseId,
                    CauseRulingId = targetCase.InitialRulingId,
                    RequiredRulingDisposition = RulingDisposition.Denied,
                    TargetRoleId =
                        WorkplaceIdentityScenario.LaterClaimantRoleId,
                    StatusId = InstitutionalStatusIds.AdverseDecision,
                    RequestedRecognisedState = true,
                });
            definition.HoldingCitations[0].CitationId =
                "citation.runtime.successor-adjudication";
            definition.HoldingCitations[0].TargetRulingId =
                targetCase.AdjudicationRulingId;
            definition.EntitlementTransfers[0].CauseRulingId =
                targetCase.AdjudicationRulingId;
            definition.EntitlementTransfers[0].Cycle =
                targetCase.AdjudicationCycle;
            definition.EntitlementTransfers[0].RequiredRulingDisposition =
                RulingDisposition.ReversedAndRecognised;
            return definition;
        }

        private static InstitutionalScenarioDefinition DescendantTriggerDefinition()
        {
            const string claimantRoleId = "role.trigger.claimant";
            const string respondentRoleId = "role.trigger.respondent";
            const string claimantAgentId = "agent.trigger.claimant";
            const string respondentAgentId = "agent.trigger.respondent";
            const string primaryCaseId = "case.trigger";
            const string descendantCaseId = "case.trigger-descendant";
            const string issueId = "issue.trigger";
            const string opportunityId = "opportunity.trigger.aid";
            const string propositionId = "proposition.trigger.disclosure";

            AgentState claimant = Agent(
                claimantAgentId,
                0,
                "species.trigger.claimant",
                null);
            claimant.Standing.CanWork = true;
            claimant.Standing.CanGiveEvidence = true;
            claimant.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.trigger",
                PropositionId = propositionId,
                SubjectId = claimantAgentId,
                ObjectId = "object.trigger",
                SourceId = "record.trigger",
                Confidence = 100,
                Secrecy = 0,
                EmotionalWeight = 100,
                AcquiredTick = 0,
            });
            AgentState respondent = Agent(
                respondentAgentId,
                1,
                "species.trigger.respondent",
                null);
            var definition = new InstitutionalScenarioDefinition
            {
                ScenarioId = "scenario.trigger",
                IncidentId = "incident.trigger",
                PrimaryCaseId = primaryCaseId,
                StartCycle = 0,
                EndCycle = 4,
                InitialSociety = new SocietyState
                {
                    MasterSeed = 2718,
                    CurrentTick = 0,
                    Regime = new InstitutionalRegimeState(),
                    Agents = new List<AgentState> { claimant, respondent },
                },
            };
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = claimantRoleId,
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.trigger.claimant",
                },
                DistinctFromRoleIds = new List<string> { respondentRoleId },
            });
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = respondentRoleId,
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.trigger.respondent",
                },
                DistinctFromRoleIds = new List<string> { claimantRoleId },
            });
            definition.Cases.Add(new ScenarioCaseDefinition
            {
                CaseId = primaryCaseId,
                IssueId = issueId,
                ClaimantRoleId = claimantRoleId,
                RespondentRoleId = respondentRoleId,
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("fact.trigger.jurisdiction", "fixture"),
                }),
                OpenCycle = 0,
                InitialEvidenceCutoffCycle = 1,
                InitialRulingCycle = 1,
                AdjudicationEvidenceCutoffCycle = 4,
                AdjudicationCycle = 4,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "adjudication",
                InitialRulingId = "ruling:case.trigger:initial:1",
                AdjudicationRulingId = "ruling:case.trigger:adjudication:4",
                InitialScoreThreshold = 40,
                ProvisionalScoreThreshold = 20,
                AdjudicationScoreThreshold = 40,
            });
            definition.Cases.Add(new ScenarioCaseDefinition
            {
                CaseId = descendantCaseId,
                IssueId = issueId,
                ClaimantRoleId = claimantRoleId,
                RespondentRoleId = respondentRoleId,
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("fact.trigger.jurisdiction", "fixture"),
                }),
                OpenCycle = 3,
                InitialEvidenceCutoffCycle = 3,
                InitialRulingCycle = 3,
                AdjudicationEvidenceCutoffCycle = 4,
                AdjudicationCycle = 4,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "adjudication",
                InitialRulingId = "ruling:case.trigger-descendant:initial:3",
                AdjudicationRulingId = "ruling:case.trigger-descendant:adjudication:4",
                InitialScoreThreshold = 40,
                ProvisionalScoreThreshold = 20,
                AdjudicationScoreThreshold = 40,
            });
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = opportunityId,
                Kind = ScenarioOpportunityKind.Aid,
                PurposeId = "purpose.trigger.aid",
                SourceCauseId = "cause.trigger.aid",
                AvailabilityStartCycle = 4,
                AvailabilityEndCycle = 4,
                UtilityBonus = 10,
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { respondentRoleId },
            });
            for (long cycle = 1; cycle <= 4; cycle++)
            {
                var schedule = new ScenarioCycleScheduleEntry
                {
                    ScheduleEntryId = $"schedule.trigger.{cycle:000}",
                    IncidentId = "incident.trigger",
                    Cycle = cycle,
                    Visibility = ScenarioVisibilityMode.NoBoundRoles,
                };
                if (cycle == 2)
                {
                    schedule.DisclosureRequested = true;
                }
                if (cycle == 4)
                {
                    schedule.AidAvailable = true;
                    schedule.ActiveOpportunityIds.Add(opportunityId);
                }
                definition.CycleSchedule.Add(schedule);
            }
            definition.EvidenceTemplates.Add(new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = "evidence.trigger.primary",
                SourceEventKind = SocietyEventKind.EvidenceDisclosed,
                SourceOpportunityId = null,
                RequiredPropositionId = propositionId,
                CaseId = primaryCaseId,
                IssueId = issueId,
                EvidenceClassId = "evidence-class.trigger.work",
                Effect = EvidenceEffect.SupportsFinding,
                Weight = 50,
                Visibility = EvidenceVisibility.OfficialRecord,
            });
            definition.EvidenceTemplates.Add(new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = "evidence.trigger.zeta",
                SourceEventKind = SocietyEventKind.EvidenceDisclosed,
                SourceOpportunityId = null,
                RequiredPropositionId = propositionId,
                CaseId = descendantCaseId,
                IssueId = issueId,
                EvidenceClassId = "evidence-class.trigger.work",
                Effect = EvidenceEffect.SupportsFinding,
                Weight = 50,
                Visibility = EvidenceVisibility.OfficialRecord,
            });
            definition.DescendantCases.Add(
                new ScenarioActionCausedDescendantCaseDefinition
                {
                    DescendantDefinitionId = "descendant.trigger",
                    CaseId = descendantCaseId,
                    ParentCaseId = primaryCaseId,
                    OpenCycle = 3,
                    TriggerCycle = 2,
                    TriggerRoleId = claimantRoleId,
                    TriggerActionKind = SocietyActionKind.Disclose,
                    TriggerOpportunityId = null,
                    TriggerPropositionId = propositionId,
                    OriginatingRulingId = "ruling:case.trigger:initial:1",
                    ConnectedRoleIds = new List<string>
                    {
                        claimantRoleId,
                        respondentRoleId,
                    },
                });
            return definition;
        }

        private static InstitutionalScenarioDefinition Definition(string key)
        {
            string claimantRoleId = $"role.{key}.claimant";
            string respondentRoleId = $"role.{key}.respondent";
            string claimantAgentId = $"agent.{key}.claimant";
            string respondentAgentId = $"agent.{key}.respondent";
            string caseId = $"case.{key}";
            string issueId = $"issue.{key}";
            string incidentId = $"incident.{key}";
            string opportunityId = $"opportunity.{key}.work";
            string statusId = $"status.{key}.after-ruling";

            var definition = new InstitutionalScenarioDefinition
            {
                ScenarioId = $"scenario.{key}",
                IncidentId = incidentId,
                PrimaryCaseId = caseId,
                StartCycle = 0,
                EndCycle = 2,
                InitialSociety = new SocietyState
                {
                    MasterSeed = 31415,
                    CurrentTick = 0,
                    Regime = new InstitutionalRegimeState(),
                    Agents = new List<AgentState>
                    {
                        Agent(
                            claimantAgentId,
                            0,
                            $"species.{key}.claimant",
                            statusId),
                        Agent(
                            respondentAgentId,
                            1,
                            $"species.{key}.respondent",
                            null),
                    },
                },
            };
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = claimantRoleId,
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = $"species.{key}.claimant",
                },
                DistinctFromRoleIds = new List<string> { respondentRoleId },
            });
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = respondentRoleId,
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = $"species.{key}.respondent",
                },
                DistinctFromRoleIds = new List<string> { claimantRoleId },
            });
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = opportunityId,
                Kind = ScenarioOpportunityKind.Work,
                PurposeId = $"purpose.{key}.work",
                SourceCauseId = $"cause.{key}.work",
                AvailabilityStartCycle = 1,
                AvailabilityEndCycle = 1,
                UtilityBonus = 100,
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { claimantRoleId },
            });
            definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
            {
                ScheduleEntryId = $"schedule.{key}.001",
                IncidentId = incidentId,
                Cycle = 1,
                WorkAvailable = true,
                Visibility = ScenarioVisibilityMode.NoBoundRoles,
                ActiveOpportunityIds = new List<string> { opportunityId },
            });
            definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
            {
                ScheduleEntryId = $"schedule.{key}.002",
                IncidentId = incidentId,
                Cycle = 2,
                Visibility = ScenarioVisibilityMode.NoBoundRoles,
            });
            definition.EvidenceTemplates.Add(new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = $"evidence.{key}.work",
                SourceEventKind = SocietyEventKind.WorkPerformed,
                SourceOpportunityId = opportunityId,
                CaseId = caseId,
                IssueId = issueId,
                EvidenceClassId = $"evidence-class.{key}.work",
                Effect = EvidenceEffect.SupportsFinding,
                Weight = 50,
                Visibility = EvidenceVisibility.OfficialRecord,
            });
            definition.Cases.Add(new ScenarioCaseDefinition
            {
                CaseId = caseId,
                IssueId = issueId,
                ClaimantRoleId = claimantRoleId,
                RespondentRoleId = respondentRoleId,
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact($"fact.{key}.jurisdiction", "fixture"),
                }),
                OpenCycle = 0,
                InitialEvidenceCutoffCycle = 1,
                InitialRulingCycle = 1,
                AdjudicationEvidenceCutoffCycle = 2,
                AdjudicationCycle = 2,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "appeal",
                InitialRulingId = $"ruling:{caseId}:initial:1",
                AdjudicationRulingId = $"ruling:{caseId}:appeal:2",
                InitialScoreThreshold = 40,
                ProvisionalScoreThreshold = 20,
                ProvisionalRecognitionPermitted = false,
                AdjudicationScoreThreshold = 40,
            });
            definition.OfficialStatusEffectRequests.Add(
                new ScenarioOfficialStatusEffectRequest
                {
                    EffectRequestId = $"effect.{key}.after-ruling",
                    Cycle = 1,
                    CauseCaseId = caseId,
                    CauseRulingId = $"ruling:{caseId}:initial:1",
                    RequiredRulingDisposition = RulingDisposition.Denied,
                    TargetRoleId = claimantRoleId,
                    StatusId = statusId,
                    RequestedRecognisedState = true,
                });
            return definition;
        }

        private static AgentState Agent(
            string id,
            int ordinal,
            string speciesId,
            string unrecognisedStatusId)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = $"presentation.{id}",
                DisplayName = $"Participant {ordinal}",
                SpeciesId = speciesId,
                HouseholdId = $"household.{id}",
                EmployerId = $"organisation.{id}",
                InstitutionalTrust = 50,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = 50,
                    Candour = 50,
                    Solidarity = 50,
                    Duty = 50,
                    InstitutionalReliance = 50,
                },
                Standing = new InstitutionalStandingState
                {
                    CanWork = false,
                    CanSeekAid = false,
                    CanAppeal = false,
                    CanGiveEvidence = false,
                },
            };
            foreach (NeedKind need in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = need, Pressure = 20 });
            if (!string.IsNullOrEmpty(unrecognisedStatusId))
                agent.Standing.SetRecognised(unrecognisedStatusId, false);
            return agent;
        }

        private static InstitutionalPolicyConfiguration Policy(string key)
        {
            return new InstitutionalPolicyConfiguration
            {
                PolicyConfigurationId = $"configuration.{key}",
                PolicyVersion = $"configuration.{key}.v1",
                WorkReward = 50,
                AidEffectiveness = 50,
                DisclosureProtection = 50,
                RetaliationRisk = 50,
                AppealAccessibility = 50,
                DecisionVariationAmplitude = 0,
                InitialRecognitionThreshold = 40,
                ProvisionalRecognitionThreshold = 20,
                AppealRecognitionThreshold = 40,
                LaterRecognitionThreshold = 40,
                CitedHoldingWeight = 0,
                PermitProvisionalRecognition = false,
                ProvisionalReliefAmount = 0,
                EstablishAppellateHolding = false,
                AutoCiteMatchingHoldings = false,
                HoldingReach = PrecedentReach.Individual,
                HoldingIsRetrospective = false,
                EvidenceClassWeights = new List<EvidenceClassWeight>
                {
                    new EvidenceClassWeight
                    {
                        EvidenceClassId = $"evidence-class.{key}.work",
                        WeightPercent = 100,
                    },
                },
            };
        }
    }
}
