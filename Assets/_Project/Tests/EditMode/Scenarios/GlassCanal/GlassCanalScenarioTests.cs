using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using Desk42.Institutional.Scenarios.GlassCanal;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.Scenarios.GlassCanal
{
    public sealed class GlassCanalScenarioTests
    {
        [Test]
        public void Definition_IsValidAndDeclaresIndependentScenarioContract()
        {
            InstitutionalScenarioDefinition definition =
                GlassCanalScenario.CreateDefinition();

            Assert.DoesNotThrow(() =>
                InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(definition.ScenarioId,
                Is.EqualTo(GlassCanalScenario.ScenarioId));
            Assert.That(definition.CycleSchedule.Select(row => row.Cycle),
                Is.EqualTo(Enumerable.Range(1, 18).Select(value => (long)value)));
            Assert.That(definition.ParticipantRoles, Has.Count.EqualTo(8));
            Assert.That(definition.InitialSociety.Agents, Has.Count.EqualTo(9));
            ScenarioCycleScheduleEntry observation = definition.CycleSchedule
                .Single(value => value.Cycle == 8);
            Assert.That(observation.Visibility,
                Is.EqualTo(ScenarioVisibilityMode.AllBoundRoles));
            Assert.That(observation.ActiveOpportunityIds, Is.Empty);
            Assert.That(observation.WorkAvailable, Is.False);
            Assert.That(observation.AidAvailable, Is.False);
            Assert.That(observation.DisclosureRequested, Is.False);
            Assert.That(observation.AppealWindowOpen, Is.False);
            ScenarioCycleScheduleEntry reliancePulse = definition.CycleSchedule
                .Single(value => value.Cycle == 7);
            Assert.That(reliancePulse.ActiveOpportunityIds, Is.EqualTo(new[]
            {
                GlassCanalScenario.ComplianceOpportunityId,
                GlassCanalScenario.ReliefOpportunityId,
            }));
            Assert.That(reliancePulse.WorkAvailable, Is.True);
            Assert.That(reliancePulse.AidAvailable, Is.True);

            InstitutionalScenarioRunResult bindingProbe =
                InstitutionalScenarioEngine.Run(
                    GlassCanalScenario.CreateDefinition(),
                    GlassCanalScenario
                        .CreateLicensedOutputAccountabilityPolicy());
            Assert.That(bindingProbe.BindingDiagnostics, Has.Count.EqualTo(8));
            Assert.That(bindingProbe.BindingDiagnostics.All(value =>
                    value.SemanticCandidateCount == 1),
                Is.True,
                "Every role must bind semantically to one profile, not an authored id.");

            string[] declaredClasses = definition.EvidenceTemplates
                .Select(value => value.EvidenceClassId)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            InstitutionalPolicyConfiguration[] policies =
            {
                GlassCanalScenario.CreateBoundaryLiteralismPolicy(),
                GlassCanalScenario.CreatePrecautionaryAccessPolicy(),
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy(),
            };
            foreach (InstitutionalPolicyConfiguration policy in policies)
            {
                Assert.That(policy.EvidenceClassWeights, Has.Count.EqualTo(6));
                Assert.That(policy.EvidenceClassWeights
                        .Select(value => value.EvidenceClassId)
                        .OrderBy(value => value),
                    Is.EqualTo(declaredClasses));
                Assert.That(policy.EvidenceClassWeights.All(value =>
                        value.WeightPercent >= 0 &&
                        value.PolicyReliabilityPercent >= 0),
                    Is.True);
            }
            Assert.That(policies[0].EvidenceClassWeights.Single(value =>
                    value.EvidenceClassId ==
                        GlassCanalScenario.PermitMapEvidenceClassId)
                    .PolicyReliabilityPercent,
                Is.EqualTo(100));
            Assert.That(policies[1].EvidenceClassWeights.Single(value =>
                    value.EvidenceClassId ==
                        GlassCanalScenario.PermitMapEvidenceClassId)
                    .PolicyReliabilityPercent,
                Is.EqualTo(50));
            Assert.That(policies[2].EvidenceClassWeights.Single(value =>
                    value.EvidenceClassId ==
                        GlassCanalScenario.PermitMapEvidenceClassId)
                    .PolicyReliabilityPercent,
                Is.EqualTo(50));
            ScenarioEvidenceTemplateDefinition resonance = definition
                .EvidenceTemplates.Single(value =>
                    value.EvidenceTemplateId ==
                        GlassCanalScenario.ResonanceEvidenceTemplateId);
            Assert.That(resonance.SourceEventKind,
                Is.EqualTo(SocietyEventKind.WorkPerformed));
            Assert.That(resonance.SourceOpportunityId,
                Is.EqualTo(GlassCanalScenario.ResonanceObservationOpportunityId));
            ScenarioOpportunityDefinition resonanceObservation = definition.Opportunities
                .Single(value => value.OpportunityId ==
                    GlassCanalScenario.ResonanceObservationOpportunityId);
            Assert.That(resonanceObservation.RequiredOfficialStatusId,
                Is.EqualTo(GlassCanalScenario.UndissipatedOutputStatusId));
            Assert.That(resonanceObservation.RequiredOfficialStatusRecognised, Is.True);
            ScenarioOpportunityDefinition laterReport = definition.Opportunities
                .Single(value =>
                    value.OpportunityId ==
                        GlassCanalScenario.LaterReportOpportunityId);
            Assert.That(laterReport.RequiredOfficialStatusId,
                Is.EqualTo(GlassCanalScenario.PrimaryDispositionRecordedStatusId));
            Assert.That(laterReport.RequiredOfficialStatusRecognised, Is.True);
            Assert.That(definition.DescendantCases.Single().OriginatingRulingId,
                Is.EqualTo(GlassCanalScenario.PrimaryInitialRulingId));

            ScenarioExclusiveEntitlementTransferDefinition transfer =
                definition.EntitlementTransfers.Single();
            Assert.That(transfer.Cycle, Is.EqualTo(17));
            Assert.That(transfer.CauseCaseId,
                Is.EqualTo(GlassCanalScenario.LaterCaseId));
            Assert.That(transfer.CauseRulingId,
                Is.EqualTo(GlassCanalScenario.LaterAppealRulingId));
            Assert.That(transfer.CauseHoldingId,
                Is.EqualTo(GlassCanalScenario.HoldingId));
            Assert.That(transfer.RequiredRulingDisposition,
                Is.EqualTo(RulingDisposition.ReversedAndRecognised));
            Assert.That(transfer.GainKind,
                Is.EqualTo(MaterialConsequenceKind.ResourceGranted));
            Assert.That(transfer.LossKind,
                Is.EqualTo(MaterialConsequenceKind.ResourceRevoked));
        }

        [Test]
        public void CycleTwoSampleContention_HasStableWinnerAndNonIdleFallback()
        {
            InstitutionalScenarioRunResult result = Run(
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            AgentActionTrace winner = Trace(
                result,
                2,
                GlassCanalScenario.KhetAgentId);
            AgentActionTrace fallback = Trace(
                result,
                2,
                GlassCanalScenario.OrinAgentId);
            Assert.That(winner.Action, Is.EqualTo(SocietyActionKind.Work));
            Assert.That(winner.OpportunityId,
                Is.EqualTo(GlassCanalScenario.SampleOpportunityId));
            Assert.That(winner.SelectedCandidateRank, Is.Zero);
            Assert.That(winner.CandidateEvaluations[0].OpportunityId,
                Is.EqualTo(GlassCanalScenario.SampleOpportunityId));
            Assert.That(fallback.Action, Is.EqualTo(SocietyActionKind.Work));
            Assert.That(fallback.OpportunityId, Is.Null);
            Assert.That(fallback.SelectedCandidateRank, Is.GreaterThan(0));
            Assert.That(fallback.CandidateEvaluations[0].OpportunityId,
                Is.EqualTo(GlassCanalScenario.SampleOpportunityId),
                "Both frozen plans must rank the scarce sample first.");
            Assert.That(fallback.CandidateEvaluations[1].Action,
                Is.EqualTo(SocietyActionKind.Work));
            Assert.That(fallback.CandidateEvaluations[1].OpportunityId, Is.Null);
            CapacityReservationTrace rejected = fallback.CapacityReservations.Single(
                value => value.OpportunityId == GlassCanalScenario.SampleOpportunityId);
            Assert.That(rejected.Awarded, Is.False);
            Assert.That(rejected.HolderActorId,
                Is.EqualTo(GlassCanalScenario.KhetAgentId));

            EvidenceArtifact sample = result.Report.EvidenceArtifacts.Single(value =>
                value.SourceTemplateId == GlassCanalScenario.SampleEvidenceTemplateId);
            Assert.That(sample.Provenance.SourceAgentId,
                Is.EqualTo(GlassCanalScenario.KhetAgentId));
            InstitutionalCaseOpening opening = result.Report.CaseOpenings.Single();
            Assert.That(opening.CaseId,
                Is.EqualTo(GlassCanalScenario.PrimaryCaseId));
            Assert.That(opening.TriggerEvidenceArtifactId,
                Is.EqualTo(sample.ArtifactId));
        }

        [Test]
        public void BoundaryLiteralism_StopsAtDenialWithoutRelianceOrTransfer()
        {
            InstitutionalScenarioRunResult result = Run(
                GlassCanalScenario.CreateBoundaryLiteralismPolicy());

            Assert.That(Ruling(result, GlassCanalScenario.PrimaryInitialRulingId)
                    .Disposition,
                Is.EqualTo(RulingDisposition.Denied));
            Assert.That(result.Report.RelianceObservations, Is.Empty);
            Assert.That(result.Report.Appeals, Is.Empty);
            Assert.That(result.Report.Holdings, Is.Empty);
            Assert.That(result.Report.ConnectedOutcomes, Is.Empty);
            Assert.That(Trace(result, 10, GlassCanalScenario.TomaAgentId).Action,
                Is.EqualTo(SocietyActionKind.Idle));
            Assert.That(Trace(result, 14, GlassCanalScenario.SeraAgentId)
                    .OpportunityId,
                Is.EqualTo(GlassCanalScenario.LaterReportOpportunityId));
            Assert.That(result.Report.DescendantCases.Any(value =>
                    value.CaseId == GlassCanalScenario.LaterCaseId),
                Is.True);
            Assert.That(Ruling(result, GlassCanalScenario.LaterInitialRulingId)
                    .Disposition,
                Is.EqualTo(RulingDisposition.Denied));
            Assert.That(result.Report.Rulings.Any(value =>
                    value.RulingId == GlassCanalScenario.LaterAppealRulingId),
                Is.False);
            AssertCurrentHolder(result, GlassCanalScenario.VeyAgentId);
        }

        [Test]
        public void PrecautionaryAccess_GrantsRelianceThenOperatorReversalAndRecovery()
        {
            InstitutionalScenarioRunResult result = Run(
                GlassCanalScenario.CreatePrecautionaryAccessPolicy());

            Assert.That(Ruling(result, GlassCanalScenario.PrimaryInitialRulingId)
                    .Disposition,
                Is.EqualTo(RulingDisposition.ProvisionallyRecognised));
            RelianceEvent reliance = result.AssessorRun.RelianceLedger.Single();
            Assert.That(reliance.RelianceEventId,
                Is.EqualTo(GlassCanalScenario.GrantRelianceId));
            Assert.That(reliance.AlternativeAvailableBefore, Is.True);
            Assert.That(reliance.AlternativeAvailableAfter, Is.False);
            Assert.That(reliance.SurvivedReversal, Is.True);

            Appeal appeal = result.Report.Appeals.Single(value =>
                value.AppellantAgentId == GlassCanalScenario.NaraAgentId);
            Assert.That(appeal.AppellantAgentId,
                Is.EqualTo(GlassCanalScenario.NaraAgentId));
            Assert.That(appeal.ChallengedRulingId,
                Is.EqualTo(GlassCanalScenario.PrimaryInitialRulingId));
            Assert.That(appeal.Disposition, Is.EqualTo(AppealDisposition.Reversed));
            Assert.That(Ruling(result, GlassCanalScenario.PrimaryAppealRulingId)
                    .Disposition,
                Is.EqualTo(RulingDisposition.ReversedAndDenied));
            Assert.That(result.Report.DescendantCases.Any(value =>
                    value.Kind == DescendantCaseKind.Reliance),
                Is.True);
            Assert.That(result.Report.Holdings, Is.Empty);
            Assert.That(result.Report.ConnectedOutcomes, Is.Empty);
            Assert.That(Trace(result, 14, GlassCanalScenario.SeraAgentId)
                    .OpportunityId,
                Is.EqualTo(GlassCanalScenario.LaterReportOpportunityId));
            Assert.That(result.Report.DescendantCases.Any(value =>
                    value.CaseId == GlassCanalScenario.LaterCaseId),
                Is.True);
            Assert.That(Ruling(result, GlassCanalScenario.LaterInitialRulingId)
                    .Disposition,
                Is.EqualTo(RulingDisposition.Denied));
            AssertCurrentHolder(result, GlassCanalScenario.VeyAgentId);
        }

        [Test]
        public void LicensedOutputAccountability_ProducesCompleteCausalChain()
        {
            InstitutionalScenarioRunResult result = Run(
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            Assert.That(Ruling(result, GlassCanalScenario.PrimaryInitialRulingId)
                    .Disposition,
                Is.EqualTo(RulingDisposition.Denied));
            RelianceEvent reliance = result.AssessorRun.RelianceLedger.Single();
            Assert.That(reliance.RelianceEventId,
                Is.EqualTo(GlassCanalScenario.DenialRelianceId));
            Assert.That(reliance.AgentId,
                Is.EqualTo(GlassCanalScenario.MaraAgentId));
            Assert.That(reliance.Cycle, Is.EqualTo(7));
            Assert.That(result.Report.RelianceObservations.Single().Cycle,
                Is.EqualTo(8));
            AgentActionTrace relianceAction = Trace(
                result,
                7,
                GlassCanalScenario.MaraAgentId);
            Assert.That(relianceAction.Action,
                Is.EqualTo(SocietyActionKind.SeekAid));
            Assert.That(relianceAction.OpportunityId,
                Is.EqualTo(GlassCanalScenario.ComplianceOpportunityId));
            Assert.That(Trace(result, 8, GlassCanalScenario.MaraAgentId)
                    .InputSnapshot.VisibleAgentIds,
                Has.Count.EqualTo(8));
            Assert.That(result.AssessorRun.PendingReliancePublicProjections,
                Is.Empty);
            Assert.That(result.Report.MaterialConsequences.Where(value =>
                    value.ResourceId == "resource.glass.private-condenser")
                    .Select(value => value.Cycle),
                Is.All.EqualTo(8));
            Assert.That(result.Report.MaterialConsequences.Any(value =>
                    value.ResourceId == "resource.glass.private-condenser" &&
                    value.Cycle == 7),
                Is.False);
            Assert.That(result.Report.Timeline.Single(value =>
                    value.Kind == InstitutionalTimelineKind.RelianceCreated).Cycle,
                Is.EqualTo(8));
            Assert.That(reliance.SurvivedReversal, Is.True);

            AgentActionTrace docketNotice = Trace(
                result,
                10,
                GlassCanalScenario.TomaAgentId);
            Assert.That(docketNotice.Action, Is.EqualTo(SocietyActionKind.Appeal));
            Assert.That(docketNotice.OpportunityId,
                Is.EqualTo(GlassCanalScenario.PrimaryDocketNoticeOpportunityId));

            Appeal primaryAppeal = result.Report.Appeals.Single(value =>
                value.AppellantAgentId == GlassCanalScenario.MaraAgentId);
            Assert.That(primaryAppeal.AppellantAgentId,
                Is.EqualTo(GlassCanalScenario.MaraAgentId));
            Assert.That(primaryAppeal.Disposition,
                Is.EqualTo(AppealDisposition.Reversed));
            Assert.That(Ruling(result, GlassCanalScenario.PrimaryAppealRulingId)
                    .Disposition,
                Is.EqualTo(RulingDisposition.ReversedAndRecognised));

            Holding holding = result.Report.Holdings.Single();
            Assert.That(holding.HoldingId,
                Is.EqualTo(GlassCanalScenario.HoldingId));
            Assert.That(holding.Scope.RequiredFacts.Contains(
                "permit-class", "bound-weather"), Is.True);
            Assert.That(holding.Scope.RequiredFacts.Contains(
                "output-state", "undissipated"), Is.True);
            Assert.That(holding.Scope.RequiredFacts.Contains(
                "watershed", "glass-canal"), Is.True);
            Assert.That(holding.Scope.AppliesTo(new CaseFactSet(new[]
            {
                new CaseFact("permit-class", "bound-weather"),
                new CaseFact("output-state", "dissipated"),
                new CaseFact("watershed", "glass-canal"),
            })), Is.False,
                "The physical-dissipation fact must gate precedent scope.");

            AgentActionTrace reportAction = Trace(
                result,
                14,
                GlassCanalScenario.SeraAgentId);
            Assert.That(reportAction.Action, Is.EqualTo(SocietyActionKind.Work));
            Assert.That(reportAction.OpportunityId,
                Is.EqualTo(GlassCanalScenario.LaterReportOpportunityId));
            DescendantCase later = result.Report.DescendantCases.Single(value =>
                value.CaseId == GlassCanalScenario.LaterCaseId);
            Assert.That(later.OpenedCycle, Is.EqualTo(15));
            Assert.That(later.CausalAgentActionId,
                Is.EqualTo(reportAction.ResultEventIds.Single()));

            Assert.That(Ruling(result, GlassCanalScenario.LaterInitialRulingId)
                    .Disposition,
                Is.EqualTo(RulingDisposition.Denied));
            Appeal laterAppeal = result.Report.Appeals.Single(value =>
                value.AppellantAgentId == GlassCanalScenario.SeraAgentId);
            Assert.That(laterAppeal.AppellantAgentId,
                Is.EqualTo(GlassCanalScenario.SeraAgentId));
            Ruling laterAppellate = Ruling(
                result,
                GlassCanalScenario.LaterAppealRulingId);
            Assert.That(laterAppellate.Cycle, Is.EqualTo(17));
            Assert.That(laterAppellate.Disposition,
                Is.EqualTo(RulingDisposition.ReversedAndRecognised));
            Assert.That(laterAppellate.CitedHoldingIds,
                Is.EqualTo(new[] { GlassCanalScenario.HoldingId }));
            Assert.That(holding.AppliedCaseIds,
                Is.EqualTo(new[] { GlassCanalScenario.LaterCaseId }));

            ExclusiveEntitlementObservation entitlement =
                result.Report.ExclusiveEntitlements.Single();
            Assert.That(entitlement.ResourceId,
                Is.EqualTo(GlassCanalScenario.FilterResourceId));
            Assert.That(entitlement.CurrentHolderAgentId,
                Is.EqualTo(GlassCanalScenario.SeraAgentId));
            Assert.That(entitlement.LastMutationCauseId,
                Is.EqualTo(GlassCanalScenario.LaterAppealRulingId));
            OfficialStatusMutation[] transferMutations = result.Report
                .OfficialStatusMutations
                .Where(value =>
                    value.CauseId == GlassCanalScenario.LaterAppealRulingId &&
                    value.StatusId == GlassCanalScenario.FilterEntitlementStatusId)
                .ToArray();
            Assert.That(transferMutations, Has.Length.EqualTo(2));
            Assert.That(transferMutations.Single(value =>
                    value.AffectedAgentId == GlassCanalScenario.VeyAgentId)
                    .AfterRecognised,
                Is.False);
            Assert.That(transferMutations.Single(value =>
                    value.AffectedAgentId == GlassCanalScenario.SeraAgentId)
                    .AfterRecognised,
                Is.True);

            MaterialConsequence[] transferMaterials = result.Report
                .MaterialConsequences
                .Where(value =>
                    value.CauseId == GlassCanalScenario.LaterAppealRulingId &&
                    value.ResourceId == GlassCanalScenario.FilterResourceId)
                .ToArray();
            Assert.That(transferMaterials, Has.Length.EqualTo(2));
            MaterialConsequence gained = transferMaterials.Single(value =>
                value.ResourceDelta > 0);
            MaterialConsequence lost = transferMaterials.Single(value =>
                value.ResourceDelta < 0);
            Assert.That(gained.AgentId,
                Is.EqualTo(GlassCanalScenario.SeraAgentId));
            Assert.That(gained.Kind,
                Is.EqualTo(MaterialConsequenceKind.ResourceGranted));
            Assert.That(gained.KindId,
                Is.EqualTo("material-kind.glass.filter-awarded"));
            Assert.That(gained.HasNeedEffect, Is.False);
            Assert.That(lost.AgentId,
                Is.EqualTo(GlassCanalScenario.VeyAgentId));
            Assert.That(lost.Kind,
                Is.EqualTo(MaterialConsequenceKind.ResourceRevoked));
            Assert.That(lost.KindId,
                Is.EqualTo("material-kind.glass.filter-displaced"));
            Assert.That(lost.HasNeedEffect, Is.False);

            ConnectedOutcomePair connection = result.Report.ConnectedOutcomes.Single();
            Assert.That(connection.WinnerAgentId,
                Is.EqualTo(GlassCanalScenario.SeraAgentId));
            Assert.That(connection.WinnerDisplayName, Is.EqualTo("Sera Vale"));
            Assert.That(connection.WinnerResourceDelta, Is.EqualTo(1));
            Assert.That(connection.LoserAgentId,
                Is.EqualTo(GlassCanalScenario.VeyAgentId));
            Assert.That(connection.LoserDisplayName, Is.EqualTo("Vey Ankar"));
            Assert.That(connection.LoserResourceDelta, Is.EqualTo(-1));
            Assert.That(connection.CauseRuleId,
                Is.EqualTo(GlassCanalScenario.HoldingRuleId));
            Assert.That(connection.ConnectionId,
                Is.EqualTo(GlassCanalScenario.FilterResourceId));
            Assert.That(result.Report.MaterialConsequences.Any(value =>
                    value.Kind == MaterialConsequenceKind.WagesLost ||
                    value.Kind == MaterialConsequenceKind.BackpayAwarded),
                Is.False);
            Assert.That(result.Report.WorkAllocations, Is.Empty);
            Assert.That(result.AssessorRun.WorkAllocations, Is.Empty);
        }

        [Test]
        public void LicensedOutputAccountability_ReplaysDeterministically()
        {
            InstitutionalScenarioRunResult first = Run(
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());
            InstitutionalScenarioRunResult replay = Run(
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            Assert.That(Signature(first), Is.EqualTo(Signature(replay)));
        }

        [Test]
        public void EquivalentInspectorProfileRemap_PreservesCausalPattern()
        {
            InstitutionalScenarioRunResult baseline = Run(
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());
            InstitutionalScenarioDefinition remapped =
                GlassCanalScenario.CreateDefinition();
            AgentState inspector = remapped.InitialSociety.GetAgent(
                GlassCanalScenario.KhetAgentId);
            const string remappedInspectorId = "agent.glass.remapped-inspector";
            inspector.StableId = remappedInspectorId;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                remapped,
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            ScenarioParticipantBindingDiagnostic binding =
                result.BindingDiagnostics.Single(value =>
                    value.RoleId == GlassCanalScenario.InspectorRoleId);
            Assert.That(binding.BoundAgentStableId,
                Is.EqualTo(remappedInspectorId));
            Assert.That(binding.SemanticCandidateCount, Is.EqualTo(1));
            Assert.That(Trace(result, 2, remappedInspectorId).OpportunityId,
                Is.EqualTo(GlassCanalScenario.SampleOpportunityId));
            Assert.That(result.Report.EvidenceArtifacts.Single(value =>
                    value.SourceTemplateId ==
                        GlassCanalScenario.SampleEvidenceTemplateId)
                    .Provenance.SourceAgentId,
                Is.EqualTo(remappedInspectorId));
            Assert.That(CausalPatternSignature(result),
                Is.EqualTo(CausalPatternSignature(baseline)));
        }

        [Test]
        public void LoadBearingEmploymentCommitment_ChangesFirstRelevantActionAndWinner()
        {
            InstitutionalScenarioRunResult baseline = Run(
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());
            InstitutionalScenarioDefinition perturbed =
                GlassCanalScenario.CreateDefinition();
            perturbed.InitialSociety.GetAgent(GlassCanalScenario.KhetAgentId)
                .Commitments.Single(value =>
                    value.Kind == "employment" &&
                    value.TargetId == "institution.glass-canal-authority")
                .Strength = 0;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                perturbed,
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            AgentActionTrace before = Trace(
                baseline,
                2,
                GlassCanalScenario.KhetAgentId);
            AgentActionTrace after = Trace(
                result,
                2,
                GlassCanalScenario.KhetAgentId);
            Assert.That(before.Action, Is.EqualTo(SocietyActionKind.Work));
            Assert.That(before.OpportunityId,
                Is.EqualTo(GlassCanalScenario.SampleOpportunityId));
            Assert.That(after.Action, Is.EqualTo(SocietyActionKind.Idle));
            Assert.That(after.OpportunityId, Is.Null);

            CandidateEvaluation beforeSample = before.CandidateEvaluations.Single(value =>
                value.OpportunityId == GlassCanalScenario.SampleOpportunityId);
            CandidateEvaluation afterSample = after.CandidateEvaluations.Single(value =>
                value.OpportunityId == GlassCanalScenario.SampleOpportunityId);
            DecisionReason beforeCommitment = beforeSample.Reasons.Single(value =>
                value.ReasonId == "commitment.employment");
            DecisionReason afterCommitment = afterSample.Reasons.Single(value =>
                value.ReasonId == "commitment.employment");
            Assert.That(beforeCommitment.SourceId,
                Is.EqualTo("institution.glass-canal-authority"));
            Assert.That(beforeCommitment.ScoreDelta, Is.EqualTo(33));
            Assert.That(afterCommitment.ScoreDelta, Is.Zero);
            Assert.That(afterSample.Score,
                Is.EqualTo(beforeSample.Score - 33));
            Assert.That(beforeSample.Score, Is.GreaterThan(0));
            Assert.That(afterSample.Score, Is.LessThan(0));

            AgentActionTrace replacement = Trace(
                result,
                2,
                GlassCanalScenario.OrinAgentId);
            Assert.That(replacement.Action, Is.EqualTo(SocietyActionKind.Work));
            Assert.That(replacement.OpportunityId,
                Is.EqualTo(GlassCanalScenario.SampleOpportunityId));
            Assert.That(result.Report.EvidenceArtifacts.Single(value =>
                    value.SourceTemplateId ==
                        GlassCanalScenario.SampleEvidenceTemplateId)
                    .Provenance.SourceAgentId,
                Is.EqualTo(GlassCanalScenario.OrinAgentId));
        }

        [Test]
        public void UndissipatedControllerExposure_IsNecessaryForResonanceAppealAndHolding()
        {
            InstitutionalScenarioRunResult baseline = Run(
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            AgentActionTrace exposed = Trace(
                baseline,
                4,
                GlassCanalScenario.MaraAgentId);
            Assert.That(exposed.Action, Is.EqualTo(SocietyActionKind.Work));
            Assert.That(exposed.OpportunityId,
                Is.EqualTo(GlassCanalScenario.ResonanceObservationOpportunityId));
            Assert.That(exposed.PerceptionSnapshot.Standing.IsRecognised(
                    GlassCanalScenario.UndissipatedOutputStatusId),
                Is.True);
            WorkOpportunity frozenObservation = exposed.InputSnapshot.WorkOpportunities
                .Single(value => value.OpportunityId ==
                    GlassCanalScenario.ResonanceObservationOpportunityId);
            Assert.That(frozenObservation.RequiredOfficialStatusId,
                Is.EqualTo(GlassCanalScenario.UndissipatedOutputStatusId));
            Assert.That(frozenObservation.RequiredOfficialStatusRecognised, Is.True);

            SocietyEvent anomalyResponse = baseline.AssessorRun.FinalSocietyState
                .EventLedger.Single(value =>
                    value.Tick == 3 &&
                    value.ActorId == GlassCanalScenario.MaraAgentId &&
                    value.Kind == SocietyEventKind.AnomalyStatusResponse);
            Assert.That(anomalyResponse.EvidenceId,
                Is.EqualTo("effect.glass.undissipated-resonance-autonomy-pressure"));
            StateDelta autonomyDelta = anomalyResponse.Deltas.Single();
            Assert.That(autonomyDelta.FieldId, Is.EqualTo("need:Autonomy"));
            Assert.That(autonomyDelta.Before, Is.EqualTo(47));
            Assert.That(autonomyDelta.After, Is.EqualTo(57));
            EvidenceArtifact resonance = baseline.Report.EvidenceArtifacts.Single(value =>
                value.SourceTemplateId ==
                    GlassCanalScenario.ResonanceEvidenceTemplateId);
            Assert.That(resonance.Provenance.SourceAgentId,
                Is.EqualTo(GlassCanalScenario.MaraAgentId));
            Assert.That(resonance.Provenance.SourceSocietyEventId,
                Is.EqualTo(exposed.ResultEventIds.Single()));

            InstitutionalScenarioDefinition dissipated =
                GlassCanalScenario.CreateDefinition();
            dissipated.InitialSociety.GetAgent(GlassCanalScenario.MaraAgentId)
                .Standing.SetRecognised(
                    GlassCanalScenario.UndissipatedOutputStatusId,
                    false);
            dissipated.InitialSociety.GetAgent(GlassCanalScenario.NaraAgentId)
                .Standing.SetRecognised(
                    GlassCanalScenario.UndissipatedOutputStatusId,
                    false);
            AgentState candidWitness = dissipated.InitialSociety.GetAgent(
                GlassCanalScenario.MaraAgentId);
            candidWitness.Disposition.Candour = 100;
            candidWitness.Standing.CanWork = false;
            BeliefState resonanceBelief = candidWitness.GetBelief(
                "belief.glass.mara.resonance");
            resonanceBelief.Confidence = 100;
            resonanceBelief.Secrecy = 0;
            resonanceBelief.EmotionalWeight = 100;

            InstitutionalScenarioRunResult ablated = InstitutionalScenarioEngine.Run(
                dissipated,
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            ScenarioParticipantBindingDiagnostic claimantBinding = ablated
                .BindingDiagnostics.Single(value =>
                    value.RoleId == GlassCanalScenario.PrimaryClaimantRoleId);
            Assert.That(claimantBinding.BoundAgentStableId,
                Is.EqualTo(GlassCanalScenario.MaraAgentId));
            Assert.That(claimantBinding.SemanticCandidateCount, Is.EqualTo(1));

            AgentActionTrace unexposed = Trace(
                ablated,
                3,
                GlassCanalScenario.MaraAgentId);
            Assert.That(unexposed.Action, Is.EqualTo(SocietyActionKind.Disclose));
            Assert.That(unexposed.SubjectBeliefId,
                Is.EqualTo("belief.glass.mara.resonance"));
            Assert.That(unexposed.PerceptionSnapshot.Standing.IsRecognised(
                    GlassCanalScenario.UndissipatedOutputStatusId),
                Is.False);
            Assert.That(ablated.AssessorRun.FinalSocietyState.EventLedger.Any(value =>
                    value.ActorId == GlassCanalScenario.MaraAgentId &&
                    value.Kind == SocietyEventKind.AnomalyStatusResponse),
                Is.False);
            Assert.That(ablated.AssessorRun.FinalSocietyState.EventLedger.Any(value =>
                    value.ActorId == GlassCanalScenario.MaraAgentId &&
                    value.Kind == SocietyEventKind.EvidenceDisclosed &&
                    value.EvidencePropositionId ==
                        GlassCanalScenario.ResonancePropositionId),
                Is.True,
                "Dissipation must make the observation non-probative even when a " +
                "high-candour witness still voices the same proposition.");
            Assert.That(ablated.Report.EvidenceArtifacts.Any(value =>
                    value.SourceTemplateId ==
                        GlassCanalScenario.ResonanceEvidenceTemplateId),
                Is.False);
            Assert.That(ablated.Report.Appeals.Any(value =>
                    value.AppellantAgentId == GlassCanalScenario.MaraAgentId),
                Is.False);
            Assert.That(ablated.Report.Rulings.Any(value =>
                    value.RulingId == GlassCanalScenario.PrimaryAppealRulingId),
                Is.False);
            Assert.That(ablated.Report.Holdings, Is.Empty);
            Assert.That(ablated.Report.DescendantCases.Any(value =>
                    value.CaseId == GlassCanalScenario.LaterCaseId),
                Is.True,
                "Physical dissipation changes institutional treatment, not incident existence.");
            Assert.That(ablated.Report.ConnectedOutcomes, Is.Empty);
            AssertCurrentHolder(ablated, GlassCanalScenario.VeyAgentId);
        }

        [Test]
        public void CycleTenDocketNotice_IsAnActionableConsequenceOfInitialAdverseStanding()
        {
            InstitutionalScenarioRunResult result = Run(
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            OfficialStatusMutation standing = result.Report.OfficialStatusMutations
                .Single(value =>
                    value.Cycle == 6 &&
                    value.CauseId == GlassCanalScenario.PrimaryInitialRulingId &&
                    value.AffectedAgentId == GlassCanalScenario.TomaAgentId &&
                    value.StatusId == InstitutionalStatusIds.AdverseDecision);
            Assert.That(standing.BeforeRecognised, Is.False);
            Assert.That(standing.AfterRecognised, Is.True);

            AgentActionTrace notice = Trace(
                result,
                10,
                GlassCanalScenario.TomaAgentId);
            Assert.That(notice.Action, Is.EqualTo(SocietyActionKind.Appeal));
            Assert.That(notice.OpportunityId,
                Is.EqualTo(GlassCanalScenario.PrimaryDocketNoticeOpportunityId));
            Assert.That(notice.PerceptionSnapshot.Standing.IsRecognised(
                    InstitutionalStatusIds.AdverseDecision),
                Is.True);
            Assert.That(notice.Reasons.Any(value =>
                    value.ReasonId == "procedure.appeal-eligibility"),
                Is.True);
            Assert.That(result.Report.Appeals.Any(value =>
                    value.AppellantAgentId == GlassCanalScenario.TomaAgentId),
                Is.False,
                "The cycle-ten docket action is a notice response, not the cycle-eleven merits appeal.");
        }

        [Test]
        public void WithoutSampleAction_PrimaryCaseAndDownstreamChainDoNotExist()
        {
            InstitutionalScenarioDefinition definition =
                GlassCanalScenario.CreateDefinition();
            definition.InitialSociety
                .GetAgent(GlassCanalScenario.KhetAgentId)
                .Standing.CanWork = false;
            definition.InitialSociety
                .GetAgent(GlassCanalScenario.OrinAgentId)
                .Standing.CanWork = false;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            Assert.That(result.AssessorRun.AssessorActionTraces.Any(value =>
                    value.Cycle == 2 &&
                    value.OpportunityId == GlassCanalScenario.SampleOpportunityId),
                Is.False);
            Assert.That(result.Report.EvidenceArtifacts.Any(value =>
                    value.SourceTemplateId ==
                        GlassCanalScenario.SampleEvidenceTemplateId),
                Is.False);
            Assert.That(result.Report.CaseOpenings.Any(value =>
                    value.CaseId == GlassCanalScenario.PrimaryCaseId),
                Is.False);
            Assert.That(result.Report.Rulings.Any(value =>
                    value.CaseId == GlassCanalScenario.PrimaryCaseId),
                Is.False);
            Assert.That(result.Report.Appeals, Is.Empty);
            Assert.That(result.Report.Holdings, Is.Empty);
            Assert.That(result.Report.DescendantCases.Any(value =>
                    value.CaseId == GlassCanalScenario.LaterCaseId),
                Is.False);
            Assert.That(result.Report.ConnectedOutcomes, Is.Empty);
            AssertCurrentHolder(result, GlassCanalScenario.VeyAgentId);
        }

        [Test]
        public void WithoutRelianceTriggeringAction_NoRelianceButPrecedentStillTransfers()
        {
            InstitutionalScenarioDefinition definition =
                GlassCanalScenario.CreateDefinition();
            definition.InitialSociety
                .GetAgent(GlassCanalScenario.MaraAgentId)
                .Standing.CanSeekAid = false;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            AgentActionTrace cycleSeven = Trace(
                result,
                7,
                GlassCanalScenario.MaraAgentId);
            Assert.That(cycleSeven.Action, Is.Not.EqualTo(SocietyActionKind.SeekAid));
            Assert.That(result.AssessorRun.RelianceLedger, Is.Empty);
            Assert.That(result.Report.RelianceObservations, Is.Empty);
            Assert.That(result.Report.Holdings, Has.Count.EqualTo(1));
            Assert.That(Trace(result, 14, GlassCanalScenario.SeraAgentId)
                    .OpportunityId,
                Is.EqualTo(GlassCanalScenario.LaterReportOpportunityId));
            Assert.That(Ruling(result, GlassCanalScenario.LaterAppealRulingId)
                    .CitedHoldingIds,
                Is.EqualTo(new[] { GlassCanalScenario.HoldingId }));
            Assert.That(result.Report.ConnectedOutcomes, Has.Count.EqualTo(1));
            AssertCurrentHolder(result, GlassCanalScenario.SeraAgentId);
        }

        [Test]
        public void WithoutRelevantAppeals_NoHoldingCitationOrTransferMaterialises()
        {
            InstitutionalScenarioDefinition noPrimaryAppeal =
                GlassCanalScenario.CreateDefinition();
            noPrimaryAppeal.InitialSociety
                .GetAgent(GlassCanalScenario.MaraAgentId)
                .Standing.CanAppeal = false;

            InstitutionalScenarioRunResult primaryBlocked =
                InstitutionalScenarioEngine.Run(
                    noPrimaryAppeal,
                    GlassCanalScenario
                        .CreateLicensedOutputAccountabilityPolicy());

            Assert.That(Trace(primaryBlocked, 11, GlassCanalScenario.MaraAgentId)
                    .Action,
                Is.Not.EqualTo(SocietyActionKind.Appeal));
            Assert.That(primaryBlocked.Report.RelianceObservations,
                Has.Count.EqualTo(1));
            Assert.That(primaryBlocked.Report.Appeals.Any(value =>
                    value.AppellantAgentId == GlassCanalScenario.MaraAgentId),
                Is.False);
            Assert.That(primaryBlocked.Report.Holdings, Is.Empty);
            Assert.That(primaryBlocked.Report.DescendantCases.Any(value =>
                    value.CaseId == GlassCanalScenario.LaterCaseId),
                Is.True);
            Ruling uncitedLaterRuling = Ruling(
                primaryBlocked,
                GlassCanalScenario.LaterAppealRulingId);
            Assert.That(uncitedLaterRuling.CitedHoldingIds, Is.Empty);
            Assert.That(uncitedLaterRuling.Disposition,
                Is.Not.EqualTo(RulingDisposition.ReversedAndRecognised));
            Assert.That(primaryBlocked.Report.ConnectedOutcomes, Is.Empty);
            AssertCurrentHolder(primaryBlocked, GlassCanalScenario.VeyAgentId);

            InstitutionalScenarioDefinition noLaterAppeal =
                GlassCanalScenario.CreateDefinition();
            noLaterAppeal.InitialSociety
                .GetAgent(GlassCanalScenario.SeraAgentId)
                .Standing.CanAppeal = false;
            InstitutionalScenarioRunResult laterBlocked =
                InstitutionalScenarioEngine.Run(
                    noLaterAppeal,
                    GlassCanalScenario
                        .CreateLicensedOutputAccountabilityPolicy());

            Assert.That(laterBlocked.Report.Holdings, Has.Count.EqualTo(1));
            Assert.That(laterBlocked.Report.DescendantCases.Any(value =>
                    value.CaseId == GlassCanalScenario.LaterCaseId),
                Is.True);
            Assert.That(Trace(laterBlocked, 16, GlassCanalScenario.SeraAgentId)
                    .Action,
                Is.Not.EqualTo(SocietyActionKind.Appeal));
            Assert.That(laterBlocked.Report.Rulings.Any(value =>
                    value.RulingId == GlassCanalScenario.LaterAppealRulingId),
                Is.False);
            Assert.That(laterBlocked.Report.Holdings.Single().AppliedCaseIds,
                Is.Empty);
            Assert.That(laterBlocked.Report.ConnectedOutcomes, Is.Empty);
            AssertCurrentHolder(laterBlocked, GlassCanalScenario.VeyAgentId);
        }

        [Test]
        public void WithoutLaterReportAction_LaterCaseCitationAndTransferRemainAbsent()
        {
            InstitutionalScenarioDefinition definition =
                GlassCanalScenario.CreateDefinition();
            definition.InitialSociety
                .GetAgent(GlassCanalScenario.SeraAgentId)
                .Standing.CanWork = false;

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                GlassCanalScenario.CreateLicensedOutputAccountabilityPolicy());

            Assert.That(result.Report.Holdings, Has.Count.EqualTo(1));
            Assert.That(Trace(result, 14, GlassCanalScenario.SeraAgentId).Action,
                Is.Not.EqualTo(SocietyActionKind.Work));
            Assert.That(result.Report.DescendantCases.Any(value =>
                    value.CaseId == GlassCanalScenario.LaterCaseId),
                Is.False);
            Assert.That(result.Report.Rulings.Any(value =>
                    value.CaseId == GlassCanalScenario.LaterCaseId),
                Is.False);
            Assert.That(result.Report.Holdings.Single().AppliedCaseIds, Is.Empty);
            Assert.That(result.Report.ConnectedOutcomes, Is.Empty);
            AssertCurrentHolder(result, GlassCanalScenario.VeyAgentId);
        }

        private static InstitutionalScenarioRunResult Run(
            InstitutionalPolicyConfiguration policy)
        {
            return InstitutionalScenarioEngine.Run(
                GlassCanalScenario.CreateDefinition(),
                policy);
        }

        private static AgentActionTrace Trace(
            InstitutionalScenarioRunResult result,
            long cycle,
            string actorId)
        {
            return result.AssessorRun.AssessorActionTraces.Single(value =>
                value.Cycle == cycle && value.ActorId == actorId);
        }

        private static Ruling Ruling(
            InstitutionalScenarioRunResult result,
            string rulingId)
        {
            return result.Report.Rulings.Single(value =>
                value.RulingId == rulingId);
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
            foreach (AgentActionTrace trace in result.AssessorRun.AssessorActionTraces)
            {
                rows.Add($"trace:{trace.Cycle}:{trace.DecisionId}:{trace.CandidateId}:" +
                         $"{trace.ActorId}:{trace.Action}:{trace.TargetId}:" +
                         $"{trace.OpportunityId}:{trace.SubjectBeliefId}:" +
                         $"{trace.UtilityScore}:{trace.SelectedCandidateRank}:" +
                         string.Join(",", trace.ResultEventIds));
                rows.AddRange(trace.Reasons.Select(reason =>
                    $"trace-reason:{trace.DecisionId}:{reason.ReasonId}:" +
                    $"{reason.SourceId}:{reason.ScoreDelta}"));
                rows.AddRange(trace.CandidateEvaluations.SelectMany((candidate, rank) =>
                    new[]
                    {
                        $"candidate:{trace.DecisionId}:{rank}:{candidate.CandidateId}:" +
                        $"{candidate.Action}:{candidate.TargetId}:{candidate.OpportunityId}:" +
                        $"{candidate.SubjectBeliefId}:{candidate.IntendedNeed}:" +
                        candidate.Score,
                    }.Concat(candidate.Reasons.Select(reason =>
                        $"candidate-reason:{trace.DecisionId}:{rank}:{reason.ReasonId}:" +
                        $"{reason.SourceId}:{reason.ScoreDelta}"))));
                rows.Add($"regime:{trace.DecisionId}:{trace.RegimeSnapshot.WorkReward}:" +
                         $"{trace.RegimeSnapshot.AidEffectiveness}:" +
                         $"{trace.RegimeSnapshot.DisclosureProtection}:" +
                         $"{trace.RegimeSnapshot.RetaliationRisk}:" +
                         $"{trace.RegimeSnapshot.AppealAccessibility}:" +
                         trace.RegimeSnapshot.DecisionVariationAmplitude);
                rows.Add($"input:{trace.DecisionId}:{trace.InputSnapshot.IncidentId}:" +
                         $"{trace.InputSnapshot.WorkAvailable}:{trace.InputSnapshot.AidAvailable}:" +
                         $"{trace.InputSnapshot.DisclosureRequested}:" +
                         $"{trace.InputSnapshot.AppealWindowOpen}:" +
                         $"{trace.InputSnapshot.OpenDocketId}:" +
                         $"{trace.InputSnapshot.AidRequiredOfficialStatusId}:" +
                         $"{trace.InputSnapshot.RestrictAidToOpportunities}:" +
                         $"{trace.InputSnapshot.RestrictAppealToOpportunities}:" +
                         $"{Join(trace.InputSnapshot.AppealEligibleAgentIds)}:" +
                         $"{Join(trace.InputSnapshot.VisibleAgentIds)}:" +
                         $"{Join(trace.InputSnapshot.WorkOpportunities.Select(value => value.OpportunityId))}:" +
                         $"{Join(trace.InputSnapshot.AidOpportunities.Select(value => value.OpportunityId))}:" +
                         Join(trace.InputSnapshot.AppealOpportunities.Select(value => value.OpportunityId)));
                AddPerceptionSnapshot(rows, trace.DecisionId, trace.PerceptionSnapshot);
            }
            rows.AddRange(result.AssessorRun.AssessorActionTraces.SelectMany(trace =>
                trace.CapacityReservations.Select(reservation =>
                    $"capacity:{trace.DecisionId}:{reservation.CandidateRank}:" +
                    $"{reservation.CandidateId}:{reservation.OpportunityId}:" +
                    $"{reservation.Awarded}:" +
                    reservation.HolderActorId)));
            rows.AddRange(result.AssessorRun.AuthoritativeEvents.Select(value =>
                $"lived:{value.LivedEventId}:{value.Cycle}:{value.EventKindId}:" +
                $"{value.SubjectAgentId}:{value.CauseEntityId}:{value.AffectedNeed}:" +
                value.NeedPressureDelta));
            rows.AddRange(result.AssessorRun.AuthoritativeEvidenceLinks.Select(value =>
                $"authority-evidence:{value.LivedEventId}:{value.EvidenceArtifactId}:" +
                value.ObservationKindId));
            rows.AddRange(result.AssessorRun.AuthoritativeBeliefLinks.Select(value =>
                $"authority-belief:{value.LivedEventId}:{value.AgentId}:{value.BeliefId}"));
            rows.AddRange(result.Report.ObservedAgentActions.Select(action =>
                $"observed:{action.Cycle}:{action.ActionEventId}:{action.ActorId}:" +
                $"{action.Activity}:{action.TargetId}:" +
                $"{Join(action.ResultEvidenceArtifactIds)}:" +
                Join(action.ResultDescendantCaseIds)));
            rows.AddRange(result.Report.EvidenceArtifacts.Select(evidence =>
                $"evidence:{evidence.ArtifactId}:{evidence.SourceTemplateId}:" +
                $"{evidence.CaseId}:{evidence.EnteredCycle}:{evidence.Kind}:" +
                $"{evidence.EvidenceClassId}:{evidence.IssueId}:" +
                $"{evidence.PropositionId}:{evidence.OfficialEmployerId}:" +
                $"{evidence.OfficialIdentityConditionId}:{evidence.OfficialResourceId}:" +
                $"{evidence.Effect}:{evidence.BaseWeight}:{evidence.Reliability}:" +
                $"{evidence.OfficiallySubmitted}:{evidence.SuppressedByAgentId}:" +
                $"{evidence.EnteredAfterInitialRuling}:{Join(evidence.KnownByAgentIds)}:" +
                $"{evidence.Provenance.ProvenanceId}:{evidence.Provenance.CreatedCycle}:" +
                $"{evidence.Provenance.SourceAgentId}:{evidence.Provenance.SourceDecisionId}:" +
                $"{evidence.Provenance.SourceSocietyEventId}:" +
                $"{evidence.Provenance.SourceRecordId}:{evidence.Provenance.Visibility}:" +
                $"{evidence.Provenance.CreatedByAgentAction}:" +
                Join(evidence.Provenance.ChainOfCustodyIds)));
            rows.AddRange(result.Report.OfficialFindings.Select(finding =>
                $"finding:{finding.FindingId}:{finding.CaseId}:{finding.Cycle}:" +
                $"{finding.IssueId}:{finding.Disposition}:" +
                $"{finding.WeightedEvidenceScore}:{finding.PrecedentWeightApplied}:" +
                $"{finding.RequiredScore}:{Join(finding.EvidenceArtifactIds)}"));
            rows.AddRange(result.Report.CaseOpenings.Select(opening =>
                $"opening:{opening.ActivationId}:{opening.CaseId}:" +
                $"{opening.OpenedCycle}:{opening.TriggerEvidenceArtifactId}:" +
                opening.CausalAgentActionId));
            rows.AddRange(result.Report.Rulings.Select(ruling =>
                $"ruling:{ruling.RulingId}:{ruling.CaseId}:{ruling.Cycle}:" +
                $"{ruling.PolicyConfigurationId}:{ruling.PolicyVersion}:" +
                $"{ruling.Disposition}:{ruling.FindingId}:" +
                $"{ruling.ConfidenceMinimum}:{ruling.ConfidenceMaximum}:" +
                $"{Join(ruling.EvidenceArtifactIds)}:{Join(ruling.AppliedPolicyIds)}:" +
                $"{Join(ruling.SkippedProcedureIds)}:" +
                $"{Join(ruling.OfficialStatusMutationIds)}:" +
                $"{Join(ruling.CitedHoldingIds)}:{Join(ruling.CitedScopeIds)}"));
            rows.AddRange(result.Report.OfficialStatusMutations.Select(mutation =>
                $"mutation:{mutation.MutationId}:{mutation.CauseId}:" +
                $"{mutation.Cycle}:{mutation.AffectedAgentId}:{mutation.StatusId}:" +
                $"{mutation.BeforeRecognised}:" +
                $"{mutation.AfterRecognised}:{mutation.ResourceDelta}"));
            rows.AddRange(result.Report.Appeals.Select(appeal =>
                $"appeal:{appeal.AppealId}:{appeal.CaseId}:{appeal.FiledCycle}:" +
                $"{appeal.HearingCycle}:{appeal.AppellantAgentId}:" +
                $"{appeal.FilingActionEventId}:{appeal.ChallengedRulingId}:" +
                $"{appeal.Disposition}:{appeal.ResultingRulingId}:" +
                Join(appeal.GroundsEvidenceArtifactIds)));
            rows.AddRange(result.Report.Holdings.Select(holding =>
                $"holding:{holding.HoldingId}:{holding.EstablishedCycle}:" +
                $"{holding.SourceAppealId}:{holding.SourceRulingId}:" +
                $"{holding.RuleId}:{holding.IssueId}:" +
                $"{Join(holding.SupportingEvidenceArtifactIds)}:" +
                $"{holding.Scope.ScopeId}:{holding.Scope.Reach}:" +
                $"{holding.Scope.BoundAgentId}:{holding.Scope.BoundEmployerId}:" +
                $"{holding.Scope.IdentityConditionId}:{holding.Scope.Retrospective}:" +
                $"{Facts(holding.Scope.RequiredFacts)}:{Join(holding.AppliedCaseIds)}"));
            rows.AddRange(result.Report.DescendantCases.Select(candidate =>
                $"descendant:{candidate.CaseId}:{candidate.ParentCaseId}:" +
                $"{candidate.OpenedCycle}:{candidate.Kind}:{candidate.Status}:" +
                $"{candidate.ParentCauseId}:{candidate.OriginatingEventId}:" +
                $"{candidate.OriginatingRulingId}:{candidate.CausalAgentActionId}:" +
                $"{candidate.ClaimantAgentId}:{candidate.RespondentId}:" +
                $"{candidate.OfficialIssueId}:{candidate.OfficialIdentityConditionId}:" +
                $"{candidate.OfficialEmployerId}:{Facts(candidate.Facts)}:" +
                $"{Join(candidate.ConnectedAgentIds)}:{Join(candidate.SourceActionEventIds)}:" +
                Join(candidate.CitedHoldingIds)));
            rows.AddRange(result.AssessorRun.RelianceLedger.Select(reliance =>
                $"reliance:{reliance.RelianceEventId}:{reliance.Cycle}:{reliance.AgentId}:" +
                $"{reliance.BeneficiaryAgentId}:{reliance.ReliedOnRulingId}:" +
                $"{reliance.ReliedOnMutationId}:{reliance.SourceActionEventId}:" +
                $"{reliance.ChoiceId}:{reliance.AbandonedAlternativeId}:" +
                $"{reliance.ResourceSpent}:{reliance.SurvivedReversal}:" +
                $"{reliance.HealthPressureAfterAction}:" +
                $"{reliance.AlternativeAvailableBefore}:{reliance.AlternativeAvailableAfter}:" +
                $"{reliance.CreditsBefore}:{reliance.CreditsAfter}:" +
                $"{reliance.AgentSubsistenceBefore}:{reliance.AgentSubsistenceAfter}:" +
                $"{reliance.HouseholdAgentId}:{reliance.HouseholdSubsistenceBefore}:" +
                reliance.HouseholdSubsistenceAfter));
            rows.AddRange(result.AssessorRun.RelianceLedger.SelectMany(reliance =>
                reliance.AppliedEffects.Select(effect =>
                    $"reliance-effect:{reliance.RelianceEventId}:{effect.EffectId}:" +
                    $"{effect.AgentId}:{effect.ResourceBefore}:{effect.ResourceAfter}:" +
                    $"{effect.HasNeedEffect}:{effect.Need}:{effect.NeedPressureBefore}:" +
                    $"{effect.NeedPressureAfter}:{effect.MaterialConsequenceId}")));
            rows.AddRange(result.Report.RelianceObservations.Select(observation =>
                $"reliance-observation:{observation.ObservationId}:{observation.Cycle}:" +
                $"{observation.AgentId}:{observation.EnablingRulingId}:" +
                $"{observation.EnablingMutationId}:{observation.SourceActionEventId}:" +
                $"{observation.RecordedChoiceId}:{observation.AbandonedAlternativeId}:" +
                $"{observation.ResourceId}:{observation.RecordedResourceDelta}"));
            rows.AddRange(result.AssessorRun.EconomicAccounts.Select(account =>
                $"account:{account.AgentId}:{account.AvailableCredits}:" +
                account.CommittedIncome));
            rows.AddRange(result.AssessorRun.AlternativeOptions.Select(option =>
                $"alternative:{option.OptionId}:{option.AgentId}:{option.Available}:" +
                option.ChangedByActionEventId));
            rows.AddRange(result.Report.MaterialConsequences.Select(material =>
                $"material:{material.ConsequenceId}:{material.Cycle}:{material.CauseId}:" +
                $"{material.AgentId}:{material.Kind}:{material.KindId}:" +
                $"{material.ResourceId}:{material.ResourceDelta}:{material.HasNeedEffect}:" +
                $"{material.Need}:{material.NeedPressureBefore}:{material.NeedPressureAfter}"));
            rows.AddRange(result.Report.ConnectedOutcomes.Select(connection =>
                $"connection:{connection.PairId}:{connection.CauseRuleId}:" +
                $"{connection.ConnectionId}:{connection.WinnerAgentId}:" +
                $"{connection.WinnerResourceDelta}:{connection.LoserAgentId}:" +
                connection.LoserResourceDelta));
            rows.AddRange(result.Report.ExclusiveEntitlements.Select(entitlement =>
                $"entitlement:{entitlement.EntitlementId}:" +
                $"{entitlement.ResourceId}:{entitlement.HolderStatusId}:" +
                $"{entitlement.ConservedAmount}:{entitlement.CurrentHolderAgentId}:" +
                entitlement.LastMutationCauseId));
            rows.AddRange(result.Report.Timeline.Select(entry =>
                $"timeline:{entry.EntryId}:{entry.Cycle}:{entry.Kind}:" +
                $"{entry.CauseId}:{entry.SubjectId}:{entry.DetailId}"));
            AddFinalSocietyRows(rows, result.AssessorRun.FinalSocietyState);
            return string.Join("\n", rows);
        }

        private static void AddPerceptionSnapshot(
            List<string> rows,
            string decisionId,
            AgentPerception perception)
        {
            rows.Add($"perception:{decisionId}:{perception.StableId}:" +
                     $"{perception.SimulationOrdinal}:{perception.EmployerId}:" +
                     $"{perception.InstitutionalTrust}:" +
                     $"{perception.Disposition.RiskTolerance}:" +
                     $"{perception.Disposition.Candour}:" +
                     $"{perception.Disposition.Solidarity}:" +
                     $"{perception.Disposition.Duty}:" +
                     $"{perception.Disposition.InstitutionalReliance}:" +
                     $"{perception.Standing.CanWork}:{perception.Standing.CanSeekAid}:" +
                     $"{perception.Standing.CanAppeal}:{perception.Standing.CanGiveEvidence}");
            rows.AddRange(perception.Needs.Select(value =>
                $"perception-need:{decisionId}:{value.Kind}:{value.Pressure}"));
            rows.AddRange(perception.Standing.OfficialStatuses.Select(value =>
                $"perception-status:{decisionId}:{value.StatusId}:{value.Recognised}"));
            rows.AddRange(perception.Commitments.Select(value =>
                $"perception-commitment:{decisionId}:{value.CommitmentId}:" +
                $"{value.Kind}:{value.TargetId}:{value.Strength}"));
            rows.AddRange(perception.Relationships.Select(value =>
                $"perception-relationship:{decisionId}:{value.TargetAgentId}:" +
                $"{value.Trust}:{value.Fear}:{value.Obligation}:{value.Authority}:" +
                $"{value.Attachment}:{value.PerceivedNeed}:" +
                $"{value.PerceivedNeedPressure}:{value.PerceivedNeedObservedTick}"));
            rows.AddRange(perception.Beliefs.Select(value =>
                $"perception-belief:{decisionId}:{value.BeliefId}:" +
                $"{value.PropositionId}:{value.SubjectId}:{value.ObjectId}:" +
                $"{value.SourceId}:{value.Confidence}:{value.Secrecy}:" +
                $"{value.EmotionalWeight}:{value.AcquiredTick}:" +
                $"{value.EnteredOfficialRecord}:{value.Disclosed}:" +
                $"{value.LastWithheldTick}:{value.LastWithheldIncidentId}"));
        }

        private static void AddFinalSocietyRows(List<string> rows, SocietyState society)
        {
            rows.Add($"society:{society.SchemaVersion}:{society.RulesetVersion}:" +
                     $"{society.MasterSeed}:{society.CurrentTick}");
            foreach (AgentState agent in society.Agents)
            {
                rows.Add($"agent:{agent.StableId}:{agent.SimulationOrdinal}:" +
                         $"{agent.SpeciesId}:{agent.HouseholdId}:{agent.EmployerId}:" +
                         $"{agent.InstitutionalTrust}:{agent.Standing.CanWork}:" +
                         $"{agent.Standing.CanSeekAid}:{agent.Standing.CanAppeal}:" +
                         agent.Standing.CanGiveEvidence);
                rows.AddRange(agent.Needs.Select(value =>
                    $"agent-need:{agent.StableId}:{value.Kind}:{value.Pressure}"));
                rows.AddRange(agent.Standing.OfficialStatuses.Select(value =>
                    $"agent-status:{agent.StableId}:{value.StatusId}:{value.Recognised}"));
                rows.AddRange(agent.Beliefs.Select(value =>
                    $"agent-belief:{agent.StableId}:{value.BeliefId}:" +
                    $"{value.Confidence}:{value.Secrecy}:{value.EmotionalWeight}:" +
                    $"{value.EnteredOfficialRecord}:{value.Disclosed}:" +
                    $"{value.LastWithheldTick}:{value.LastWithheldIncidentId}"));
                rows.AddRange(agent.AnomalyRules.Select(value =>
                    $"agent-anomaly:{agent.StableId}:{value.TraitId}:" +
                    $"{value.RequiredOfficialStatusId}:{value.AffectedNeed}:" +
                    $"{value.RecognisedPressureDelta}:{value.UnrecognisedPressureDelta}:" +
                    $"{value.MinimumTicksBetweenActivations}:{value.LastAppliedTick}:" +
                    value.ObservableEffectId));
            }
            rows.AddRange(society.EventLedger.Select(value =>
                $"society-event:{value.EventId}:{value.CauseDecisionId}:" +
                $"{value.IncidentId}:{value.Tick}:{value.Kind}:{value.ActorId}:" +
                $"{value.TargetId}:{value.OpportunityId}:{value.EvidenceId}:" +
                $"{value.EvidencePropositionId}:{value.EvidenceSubjectId}:" +
                $"{value.EvidenceObjectId}:{value.EvidenceSourceId}:" +
                $"{value.EvidenceBeliefId}:{value.EvidenceSuppressedByAgentId}:" +
                $"{value.EvidenceReliability}:{value.Visibility}:" +
                Join(value.Deltas.Select(delta =>
                    $"{delta.EntityId}>{delta.FieldId}>{delta.Before}>{delta.After}"))));
        }

        private static string Facts(CaseFactSet facts)
        {
            return facts == null
                ? string.Empty
                : Join(facts.Facts.Select(value => $"{value.Key}={value.Value}"));
        }

        private static string Join(IEnumerable<string> values)
        {
            return values == null ? string.Empty : string.Join(",", values);
        }

        private static string CausalPatternSignature(
            InstitutionalScenarioRunResult result)
        {
            var rows = new List<string>();
            foreach (KeyValuePair<string, string> binding in result.AgentIdByRole
                         .OrderBy(value => value.Key))
            {
                rows.AddRange(result.AssessorRun.AssessorActionTraces
                    .Where(value => value.ActorId == binding.Value)
                    .Select(value =>
                        $"role:{binding.Key}:{value.Cycle}:{value.Action}:" +
                        $"{value.OpportunityId}:{value.SelectedCandidateRank}"));
            }
            rows.AddRange(result.Report.Rulings.Select(value =>
                $"ruling:{value.RulingId}:{value.Disposition}:" +
                string.Join(",", value.CitedHoldingIds)));
            rows.AddRange(result.Report.Holdings.Select(value =>
                $"holding:{value.HoldingId}:" +
                string.Join(",", value.AppliedCaseIds)));
            rows.AddRange(result.Report.DescendantCases.Select(value =>
                $"descendant:{value.CaseId}:{value.Kind}:{value.OpenedCycle}"));
            rows.AddRange(result.Report.ConnectedOutcomes.Select(value =>
                $"connection:{value.CauseRuleId}:" +
                $"{value.WinnerResourceDelta}:{value.LoserResourceDelta}"));
            return string.Join("\n", rows);
        }
    }
}
