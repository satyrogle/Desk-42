using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Verifies that a completed generic run still conforms to the declarative
    /// scenario that produced it. Generic graph integrity remains owned by
    /// <see cref="InstitutionalCausalGraphValidator"/>.
    /// </summary>
    internal static class InstitutionalScenarioRunValidator
    {
        internal static void Validate(InstitutionalScenarioRunResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.ValidateAgainstOrigin();
        }

        internal static void Validate(InstitutionalScenarioExecutionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            InstitutionalScenarioDefinitionValidator.Validate(context.Definition);
            context.Policy.Validate();
            ValidateTopLevel(context);
            ValidateBindings(context);
            ValidateDeclaredCaseOpenings(context);
            ValidateDeclaredEvidence(context);
            ValidateDeclaredRulings(context);
            ValidateDeclaredAppeals(context);
            ValidateDeclaredHoldings(context);
            ValidateDeclaredEntitlements(context);
            ValidateDeclaredTransfers(context);
            InstitutionalCausalGraphValidator.Validate(
                context.Run,
                context.EntitlementRegistry);
        }

        private static void ValidateDeclaredEvidence(
            InstitutionalScenarioExecutionContext context)
        {
            InstitutionalConsequenceReport report = context.Run.Report;
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                EvidenceArtifact artifact = report.EvidenceArtifacts[i];
                Require(artifact != null,
                    "Scenario evidence projection contains a null artifact.");
                ScenarioEvidenceTemplateDefinition template = FindEvidenceTemplate(
                    context.Definition,
                    artifact.SourceTemplateId);
                Require(template != null &&
                        Equal(template.CaseId, artifact.CaseId) &&
                        Equal(template.IssueId, artifact.IssueId) &&
                        Equal(template.EvidenceClassId, artifact.EvidenceClassId) &&
                        artifact.Effect == template.Effect &&
                        artifact.BaseWeight == template.Weight &&
                        artifact.Kind == EvidenceArtifactKind.ActionRecord &&
                        artifact.Provenance != null &&
                        artifact.Provenance.Visibility == template.Visibility &&
                        artifact.OfficiallySubmitted,
                    $"Evidence '{artifact.ArtifactId}' differs from its declared template.");

                ScenarioCaseDefinition caseDefinition = InstitutionalScenarioLookup.Case(
                    context.Definition,
                    artifact.CaseId);
                ScenarioEvidenceActivatedCaseDefinition activation =
                    FindEvidenceActivation(context.Definition, artifact.CaseId);
                if (activation != null)
                {
                    InstitutionalCaseOpening opening = FindCaseOpening(
                        report,
                        artifact.CaseId);
                    Require(opening != null,
                        $"Evidence '{artifact.ArtifactId}' entered an activated case " +
                        "that never opened.");
                    if (artifact.EnteredCycle <= opening.OpenedCycle)
                    {
                        ScenarioEvidenceTemplateDefinition activationTemplate =
                            FindEvidenceTemplate(
                                context.Definition,
                                activation.EvidenceTemplateId);
                        Require(Equal(
                                    artifact.ArtifactId,
                                    opening.TriggerEvidenceArtifactId) &&
                                InstitutionalEvidenceActivatedCaseService
                                    .IsExactDeclaredTriggerEvidence(
                                        context.Run,
                                        activation,
                                        activationTemplate,
                                        artifact),
                            $"Evidence '{artifact.ArtifactId}' entered at or before " +
                            "case opening without being its exact trigger.");
                        continue;
                    }
                }
                if (InstitutionalScenarioLookup.CaseIsActive(
                        context.Definition,
                        report,
                        caseDefinition,
                        artifact.EnteredCycle))
                {
                    continue;
                }

                ScenarioActionCausedDescendantCaseDefinition descendant =
                    FindDescendant(context.Definition, artifact.CaseId);
                bool exactOpeningTrigger =
                    InstitutionalScenarioLookup.CaseHasOpened(
                        context,
                        caseDefinition) &&
                    descendant != null &&
                    InstitutionalActionCausedDescendantCaseService
                        .IsExactDeclaredTriggerEvidence(
                            context.Run,
                            descendant,
                            context.AgentIdByRole,
                            artifact);
                Require(exactOpeningTrigger,
                    $"Evidence '{artifact.ArtifactId}' entered before its case without " +
                    "being the exact declared opening trigger.");
            }
        }

        private static void ValidateDeclaredCaseOpenings(
            InstitutionalScenarioExecutionContext context)
        {
            InstitutionalConsequenceReport report = context.Run.Report;
            Require(report.CaseOpenings != null,
                "Scenario report has no case-opening collection.");
            var activationIds = new HashSet<string>(StringComparer.Ordinal);
            var caseIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < report.CaseOpenings.Count; i++)
            {
                InstitutionalCaseOpening opening = report.CaseOpenings[i];
                Require(opening != null,
                    "Scenario case-opening projection contains a null row.");
                Require(activationIds.Add(opening.ActivationId) &&
                        caseIds.Add(opening.CaseId),
                    "Scenario case-opening projections must be unique by activation and case.");
                ScenarioEvidenceActivatedCaseDefinition activation =
                    FindEvidenceActivationById(
                        context.Definition,
                        opening.ActivationId);
                Require(activation != null && Equal(
                        activation.CaseId,
                        opening.CaseId),
                    $"Case opening '{opening.ActivationId}' is undeclared or changed case.");
                ScenarioCaseDefinition target = InstitutionalScenarioLookup.Case(
                    context.Definition,
                    opening.CaseId);
                Require(opening.OpenedCycle == target.OpenCycle,
                    $"Case opening '{opening.ActivationId}' occurred outside its declared cycle.");
                ScenarioEvidenceTemplateDefinition template = FindEvidenceTemplate(
                    context.Definition,
                    activation.EvidenceTemplateId);
                EvidenceArtifact trigger = FindEvidence(
                    report,
                    opening.TriggerEvidenceArtifactId);
                Require(trigger != null &&
                        Equal(trigger.SourceTemplateId, activation.EvidenceTemplateId) &&
                        Equal(trigger.CaseId, activation.CaseId) &&
                        trigger.EnteredCycle == activation.TriggerCycle &&
                        Equal(
                            opening.CausalAgentActionId,
                            trigger.Provenance?.SourceSocietyEventId) &&
                        InstitutionalEvidenceActivatedCaseService
                            .IsExactDeclaredTriggerEvidence(
                                context.Run,
                                activation,
                                template,
                                trigger),
                    $"Case opening '{opening.ActivationId}' lacks its exact evidence/action cause.");
            }
        }

        private static void ValidateTopLevel(
            InstitutionalScenarioExecutionContext context)
        {
            InstitutionalConsequenceRun run = context.Run;
            InstitutionalConsequenceReport report = run.Report;
            if (report == null || run.FinalSocietyState == null)
                throw new InvalidOperationException(
                    "Scenario execution did not produce both report and final society state.");
            Require(report.MasterSeed == context.Definition.InitialSociety.MasterSeed,
                "Report seed differs from the declared initial society seed.");
            Require(Equal(report.PrimaryCaseId, context.Definition.PrimaryCaseId),
                "Report primary case differs from the scenario declaration.");
            Require(Equal(
                    report.PolicyConfigurationId,
                    context.Policy.PolicyConfigurationId),
                "Report policy identity differs from the frozen run policy.");
            Require(report.FinalCycle == context.Definition.EndCycle,
                "Report did not finish on the declared end cycle.");
            Require(run.FinalSocietyState.CurrentTick == context.Definition.EndCycle,
                "Society did not finish on the declared end cycle.");
        }

        private static void ValidateBindings(
            InstitutionalScenarioExecutionContext context)
        {
            Require(
                context.AgentIdByRole.Count ==
                    context.Definition.ParticipantRoles.Count,
                "Runtime role-binding count differs from the declaration.");

            var seenAgentsByRole = new Dictionary<string, string>(
                StringComparer.Ordinal);
            for (int i = 0; i < context.Definition.ParticipantRoles.Count; i++)
            {
                ScenarioParticipantRoleDefinition declaration =
                    context.Definition.ParticipantRoles[i];
                Require(context.AgentIdByRole.TryGetValue(
                        declaration.RoleId,
                        out string agentId),
                    $"Declared role '{declaration.RoleId}' was not bound.");
                Require(context.Run.FinalSocietyState.GetAgent(agentId) != null,
                    $"Role '{declaration.RoleId}' is bound to a missing final agent.");
                seenAgentsByRole.Add(declaration.RoleId, agentId);
            }

            for (int i = 0; i < context.Definition.ParticipantRoles.Count; i++)
            {
                ScenarioParticipantRoleDefinition declaration =
                    context.Definition.ParticipantRoles[i];
                for (int j = 0; j < declaration.DistinctFromRoleIds.Count; j++)
                {
                    string otherRoleId = declaration.DistinctFromRoleIds[j];
                    Require(seenAgentsByRole.TryGetValue(
                            otherRoleId,
                            out string otherAgentId),
                        $"Distinct role '{otherRoleId}' has no runtime binding.");
                    Require(!Equal(
                            seenAgentsByRole[declaration.RoleId],
                            otherAgentId),
                        $"Roles '{declaration.RoleId}' and '{otherRoleId}' " +
                        "violated their distinct-participant declaration.");
                }
            }
        }

        private static void ValidateDeclaredRulings(
            InstitutionalScenarioExecutionContext context)
        {
            InstitutionalConsequenceReport report = context.Run.Report;
            for (int i = 0; i < report.Rulings.Count; i++)
            {
                Ruling ruling = report.Rulings[i];
                ScenarioCaseDefinition declaration = FindCaseByRulingId(
                    context.Definition,
                    ruling.RulingId);
                Require(declaration != null,
                    $"Ruling '{ruling.RulingId}' is not declared by any scenario case.");
                Require(Equal(ruling.CaseId, declaration.CaseId),
                    $"Ruling '{ruling.RulingId}' escaped its declared case.");
                Require(InstitutionalScenarioLookup.CaseIsActive(
                            context.Definition,
                            report,
                            declaration,
                            ruling.Cycle),
                    $"Ruling '{ruling.RulingId}' predates its case activation.");

                bool isInitial = Equal(
                    ruling.RulingId,
                    declaration.InitialRulingId);
                long expectedCycle = isInitial
                    ? declaration.InitialRulingCycle
                    : declaration.AdjudicationCycle;
                long evidenceCutoff = isInitial
                    ? declaration.InitialEvidenceCutoffCycle
                    : declaration.AdjudicationEvidenceCutoffCycle;
                Require(ruling.Cycle == expectedCycle,
                    $"Ruling '{ruling.RulingId}' occurred outside its declared cycle.");
                Require(Equal(
                        ruling.PolicyConfigurationId,
                        context.Policy.PolicyConfigurationId),
                    $"Ruling '{ruling.RulingId}' used another policy identity.");

                for (int evidenceIndex = 0;
                     evidenceIndex < ruling.EvidenceArtifactIds.Count;
                     evidenceIndex++)
                {
                    EvidenceArtifact artifact = FindEvidence(
                        report,
                        ruling.EvidenceArtifactIds[evidenceIndex]);
                    Require(artifact != null && artifact.EnteredCycle <= evidenceCutoff,
                        $"Ruling '{ruling.RulingId}' used evidence outside its " +
                        "declared phase cutoff.");
                }

                for (int citationIndex = 0;
                     citationIndex < ruling.CitedHoldingIds.Count;
                     citationIndex++)
                {
                    Require(ContainsExactCitation(
                            context.Definition,
                            ruling.CitedHoldingIds[citationIndex],
                            ruling.CaseId,
                            ruling.RulingId),
                        $"Ruling '{ruling.RulingId}' cited an undeclared holding.");
                }
            }
        }

        private static void ValidateDeclaredAppeals(
            InstitutionalScenarioExecutionContext context)
        {
            Require(
                context.Run.Report.Appeals.Count ==
                    context.ActualAppealsByDeclarationId.Count,
                "Filed appeal projection count differs from the declaration map.");
            foreach (KeyValuePair<string, Appeal> pair in
                     context.ActualAppealsByDeclarationId)
            {
                Appeal appeal = pair.Value;
                ScenarioAppealDefinition declaration = FindAppeal(
                    context.Definition,
                    pair.Key);
                Require(declaration != null,
                    $"Appeal declaration '{pair.Key}' is missing from the scenario.");
                Require(ContainsAppeal(context.Run.Report.Appeals, appeal.AppealId),
                    $"Appeal declaration '{pair.Key}' has no public projection.");
                Require(Equal(appeal.CaseId, declaration.CaseId) &&
                        appeal.FiledCycle == declaration.FilingCycle &&
                        appeal.HearingCycle == declaration.HearingCycle &&
                        Equal(
                            appeal.ChallengedRulingId,
                            declaration.ChallengedRulingId),
                    $"Appeal '{appeal.AppealId}' differs from its declaration.");
                if (appeal.Disposition != AppealDisposition.Pending)
                {
                    Require(Equal(
                            appeal.ResultingRulingId,
                            declaration.ResultingRulingId),
                        $"Appeal '{appeal.AppealId}' resolved through an " +
                        "undeclared ruling.");
                }
            }
        }

        private static void ValidateDeclaredHoldings(
            InstitutionalScenarioExecutionContext context)
        {
            for (int i = 0; i < context.Run.Report.Holdings.Count; i++)
            {
                Holding holding = context.Run.Report.Holdings[i];
                ScenarioHoldingDefinition declaration = FindHolding(
                    context.Definition,
                    holding.HoldingId);
                Require(declaration != null,
                    $"Holding '{holding.HoldingId}' is not declared by the scenario.");
                Require(context.ActualAppealsByDeclarationId.TryGetValue(
                        declaration.SourceAppealId,
                        out Appeal sourceAppeal),
                    $"Holding '{holding.HoldingId}' has no filed source appeal.");
                Require(holding.Scope != null,
                    $"Holding '{holding.HoldingId}' has no scope projection.");
                Require(Equal(holding.RuleId, declaration.RuleId) &&
                        Equal(holding.IssueId, declaration.IssueId) &&
                        Equal(holding.SourceAppealId, sourceAppeal.AppealId) &&
                        Equal(holding.SourceRulingId, declaration.SourceRulingId) &&
                        Equal(holding.Scope.ScopeId, declaration.ScopeId),
                    $"Holding '{holding.HoldingId}' differs from its declaration.");
            }
        }

        private static void ValidateDeclaredEntitlements(
            InstitutionalScenarioExecutionContext context)
        {
            Require(
                context.EntitlementRegistry.Count ==
                    context.Definition.ExclusiveEntitlements.Count,
                "Entitlement registry count differs from the scenario declaration.");
            Require(
                context.Run.Report.ExclusiveEntitlements.Count ==
                    context.Definition.ExclusiveEntitlements.Count,
                "Public entitlement projection count differs from the declaration.");

            for (int i = 0;
                 i < context.Definition.ExclusiveEntitlements.Count;
                 i++)
            {
                ScenarioExclusiveEntitlementDefinition declaration =
                    context.Definition.ExclusiveEntitlements[i];
                ExclusiveEntitlementState state = context.EntitlementRegistry.Find(
                    declaration.EntitlementId,
                    declaration.ResourceId);
                Require(state != null,
                    $"Entitlement '{declaration.EntitlementId}' was not registered.");
                Require(Equal(state.HolderStatusId, declaration.OfficialStatusId) &&
                        state.ConservedAmount == declaration.Units,
                    $"Entitlement '{declaration.EntitlementId}' differs from its declaration.");
                ExclusiveEntitlementService.AssertHolderInvariant(context.Run, state);
            }
        }

        private static void ValidateDeclaredTransfers(
            InstitutionalScenarioExecutionContext context)
        {
            var expectedHolderByEntitlement = new Dictionary<string, string>(
                StringComparer.Ordinal);
            var expectedLastCauseByEntitlement = new Dictionary<string, string>(
                StringComparer.Ordinal);
            for (int i = 0;
                 i < context.Definition.ExclusiveEntitlements.Count;
                 i++)
            {
                ScenarioExclusiveEntitlementDefinition entitlement =
                    context.Definition.ExclusiveEntitlements[i];
                expectedHolderByEntitlement.Add(
                    entitlement.EntitlementId,
                    context.AgentIdByRole[entitlement.InitialHolderRoleId]);
                expectedLastCauseByEntitlement.Add(
                    entitlement.EntitlementId,
                    null);
            }

            var expectedConnectedPairIds = new HashSet<string>(
                StringComparer.Ordinal);
            var expectedTransferMutationIds = new HashSet<string>(
                StringComparer.Ordinal);
            var expectedTransferMaterialIds = new HashSet<string>(
                StringComparer.Ordinal);
            var entitlementHolderStatusIds = new HashSet<string>(
                StringComparer.Ordinal);
            var entitlementResourceIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int i = 0;
                 i < context.Definition.ExclusiveEntitlements.Count;
                 i++)
            {
                entitlementHolderStatusIds.Add(
                    context.Definition.ExclusiveEntitlements[i].OfficialStatusId);
                entitlementResourceIds.Add(
                    context.Definition.ExclusiveEntitlements[i].ResourceId);
            }
            var chronological = new List<ScenarioExclusiveEntitlementTransferDefinition>(
                context.Definition.EntitlementTransfers);
            chronological.Sort((left, right) =>
            {
                int cycleOrder = left.Cycle.CompareTo(right.Cycle);
                return cycleOrder != 0
                    ? cycleOrder
                    : StringComparer.Ordinal.Compare(left.TransferId, right.TransferId);
            });

            for (int i = 0; i < chronological.Count; i++)
            {
                ScenarioExclusiveEntitlementTransferDefinition transfer =
                    chronological[i];
                ScenarioExclusiveEntitlementDefinition entitlement =
                    InstitutionalScenarioLookup.Entitlement(
                        context.Definition,
                        transfer.EntitlementId);
                ExclusiveEntitlementState state = context.EntitlementRegistry.Find(
                    entitlement.EntitlementId,
                    entitlement.ResourceId);
                Require(state != null,
                    $"Transfer '{transfer.TransferId}' has no registered entitlement.");

                string fromAgentId = context.AgentIdByRole[transfer.FromRoleId];
                string toAgentId = context.AgentIdByRole[transfer.ToRoleId];
                Ruling causeRuling = InstitutionalScenarioLookup.Ruling(
                    context.Run.Report,
                    transfer.CauseRulingId);
                bool eligible = causeRuling != null &&
                    Equal(causeRuling.CaseId, transfer.CauseCaseId) &&
                    causeRuling.Cycle <= transfer.Cycle &&
                    Contains(causeRuling.CitedHoldingIds, transfer.CauseHoldingId) &&
                    causeRuling.Disposition == transfer.RequiredRulingDisposition;

                if (!eligible)
                {
                    RequireNoTransferProjections(
                        context,
                        transfer,
                        entitlement);
                    continue;
                }

                Require(Equal(
                        expectedHolderByEntitlement[transfer.EntitlementId],
                        fromAgentId),
                    $"Transfer '{transfer.TransferId}' did not follow the last eligible " +
                    "exclusive holder.");
                RequireExactTransferMutations(
                    context,
                    transfer,
                    entitlement,
                    causeRuling,
                    fromAgentId,
                    toAgentId,
                    expectedTransferMutationIds);
                RequireExactTransferMaterials(
                    context,
                    transfer,
                    entitlement,
                    state,
                    causeRuling,
                    fromAgentId,
                    toAgentId,
                    expectedTransferMaterialIds);
                string pairId = InstitutionalScenarioEntitlementPhase
                    .BuildConnectedOutcomePairId(transfer.TransferId);
                RequireExactConnectedOutcome(
                    context,
                    transfer,
                    entitlement,
                    causeRuling,
                    fromAgentId,
                    toAgentId,
                    pairId);
                Require(expectedConnectedPairIds.Add(pairId),
                    $"Transfer '{transfer.TransferId}' reused a connected outcome id.");

                expectedHolderByEntitlement[transfer.EntitlementId] = toAgentId;
                expectedLastCauseByEntitlement[transfer.EntitlementId] =
                    causeRuling.RulingId;
            }

            for (int i = 0;
                 i < context.Definition.ExclusiveEntitlements.Count;
                 i++)
            {
                ScenarioExclusiveEntitlementDefinition entitlement =
                    context.Definition.ExclusiveEntitlements[i];
                ExclusiveEntitlementState state = context.EntitlementRegistry.Find(
                    entitlement.EntitlementId,
                    entitlement.ResourceId);
                ExclusiveEntitlementObservation observation =
                    FindExclusiveEntitlementObservation(
                        context.Run.Report,
                        entitlement.EntitlementId,
                        entitlement.ResourceId);
                string expectedHolder =
                    expectedHolderByEntitlement[entitlement.EntitlementId];
                string expectedLastCause =
                    expectedLastCauseByEntitlement[entitlement.EntitlementId];
                Require(Equal(state.CurrentHolderAgentId, expectedHolder) &&
                        Equal(state.LastMutationCauseId, expectedLastCause),
                    $"Authoritative entitlement '{entitlement.EntitlementId}' does " +
                    "not match its eligible transfer chain.");
                Require(Equal(observation.CurrentHolderAgentId, expectedHolder) &&
                        Equal(observation.LastMutationCauseId, expectedLastCause) &&
                        Equal(observation.HolderStatusId, entitlement.OfficialStatusId) &&
                        observation.ConservedAmount == entitlement.Units,
                    $"Public entitlement '{entitlement.EntitlementId}' does not " +
                    "match its eligible transfer chain.");
            }

            for (int i = 0; i < context.Run.Report.ConnectedOutcomes.Count; i++)
            {
                ConnectedOutcomePair pair = context.Run.Report.ConnectedOutcomes[i];
                Require(pair != null && expectedConnectedPairIds.Contains(pair.PairId),
                    "Scenario run contains a foreign or ineligible connected outcome.");
            }
            for (int i = 0;
                 i < context.Run.Report.OfficialStatusMutations.Count;
                 i++)
            {
                OfficialStatusMutation mutation =
                    context.Run.Report.OfficialStatusMutations[i];
                if (mutation != null &&
                    entitlementHolderStatusIds.Contains(mutation.StatusId))
                {
                    Require(expectedTransferMutationIds.Contains(mutation.MutationId),
                        "Scenario run contains a foreign entitlement-holder mutation.");
                }
            }
            for (int i = 0;
                 i < context.Run.Report.MaterialConsequences.Count;
                 i++)
            {
                MaterialConsequence material =
                    context.Run.Report.MaterialConsequences[i];
                if (material != null &&
                    entitlementResourceIds.Contains(material.ResourceId))
                {
                    Require(expectedTransferMaterialIds.Contains(material.ConsequenceId),
                        "Scenario run contains a foreign entitlement material consequence.");
                }
            }
        }

        private static void RequireNoTransferProjections(
            InstitutionalScenarioExecutionContext context,
            ScenarioExclusiveEntitlementTransferDefinition transfer,
            ScenarioExclusiveEntitlementDefinition entitlement)
        {
            string pairId = InstitutionalScenarioEntitlementPhase
                .BuildConnectedOutcomePairId(transfer.TransferId);
            int pairCount = 0;
            for (int i = 0; i < context.Run.Report.ConnectedOutcomes.Count; i++)
            {
                if (Equal(context.Run.Report.ConnectedOutcomes[i]?.PairId, pairId))
                    pairCount++;
            }

            int mutationCount = 0;
            for (int i = 0;
                 i < context.Run.Report.OfficialStatusMutations.Count;
                 i++)
            {
                OfficialStatusMutation mutation =
                    context.Run.Report.OfficialStatusMutations[i];
                if (mutation != null &&
                    Equal(mutation.CauseId, transfer.CauseRulingId) &&
                    Equal(mutation.StatusId, entitlement.OfficialStatusId))
                {
                    mutationCount++;
                }
            }

            int materialCount = 0;
            for (int i = 0;
                 i < context.Run.Report.MaterialConsequences.Count;
                 i++)
            {
                MaterialConsequence material =
                    context.Run.Report.MaterialConsequences[i];
                if (material != null &&
                    Equal(material.CauseId, transfer.CauseRulingId) &&
                    Equal(material.ResourceId, entitlement.ResourceId))
                {
                    materialCount++;
                }
            }

            Require(pairCount == 0 && mutationCount == 0 && materialCount == 0,
                $"Ineligible transfer '{transfer.TransferId}' projected a transfer effect.");
        }

        private static void RequireExactTransferMutations(
            InstitutionalScenarioExecutionContext context,
            ScenarioExclusiveEntitlementTransferDefinition transfer,
            ScenarioExclusiveEntitlementDefinition entitlement,
            Ruling causeRuling,
            string fromAgentId,
            string toAgentId,
            HashSet<string> expectedMutationIds)
        {
            OfficialStatusMutation loss = null;
            OfficialStatusMutation gain = null;
            int lossIndex = -1;
            int gainIndex = -1;
            int attributableCount = 0;
            for (int i = 0;
                 i < context.Run.Report.OfficialStatusMutations.Count;
                 i++)
            {
                OfficialStatusMutation mutation =
                    context.Run.Report.OfficialStatusMutations[i];
                if (mutation == null ||
                    !Equal(mutation.CauseId, causeRuling.RulingId) ||
                    !Equal(mutation.StatusId, entitlement.OfficialStatusId))
                {
                    continue;
                }

                attributableCount++;
                if (Equal(mutation.AffectedAgentId, fromAgentId) &&
                    mutation.BeforeRecognised &&
                    !mutation.AfterRecognised &&
                    mutation.ResourceDelta == 0 &&
                    mutation.Cycle == causeRuling.Cycle)
                {
                    Require(loss == null,
                        $"Transfer '{transfer.TransferId}' duplicated its loss mutation.");
                    loss = mutation;
                    lossIndex = i;
                }
                else if (Equal(mutation.AffectedAgentId, toAgentId) &&
                         !mutation.BeforeRecognised &&
                         mutation.AfterRecognised &&
                         mutation.ResourceDelta == 0 &&
                         mutation.Cycle == causeRuling.Cycle)
                {
                    Require(gain == null,
                        $"Transfer '{transfer.TransferId}' duplicated its gain mutation.");
                    gain = mutation;
                    gainIndex = i;
                }
            }

            Require(attributableCount == 2 && loss != null && gain != null,
                $"Transfer '{transfer.TransferId}' lacks its exact paired status mutations.");
            Require(Equal(
                        loss.MutationId,
                        InstitutionalStatusMutationService.BuildMutationId(
                            causeRuling,
                            lossIndex,
                            fromAgentId,
                            entitlement.OfficialStatusId)) &&
                    Equal(
                        gain.MutationId,
                        InstitutionalStatusMutationService.BuildMutationId(
                            causeRuling,
                            gainIndex,
                            toAgentId,
                            entitlement.OfficialStatusId)),
                $"Transfer '{transfer.TransferId}' status mutation ids are not deterministic.");
            Require(Count(causeRuling.OfficialStatusMutationIds, loss.MutationId) == 1 &&
                    Count(causeRuling.OfficialStatusMutationIds, gain.MutationId) == 1,
                $"Transfer '{transfer.TransferId}' mutations are not owned by its cause ruling.");
            Require(expectedMutationIds.Add(loss.MutationId) &&
                    expectedMutationIds.Add(gain.MutationId),
                $"Transfer '{transfer.TransferId}' reused a status mutation projection.");
        }

        private static void RequireExactTransferMaterials(
            InstitutionalScenarioExecutionContext context,
            ScenarioExclusiveEntitlementTransferDefinition transfer,
            ScenarioExclusiveEntitlementDefinition entitlement,
            ExclusiveEntitlementState state,
            Ruling causeRuling,
            string fromAgentId,
            string toAgentId,
            HashSet<string> expectedMaterialIds)
        {
            string gainKindId = string.IsNullOrWhiteSpace(transfer.GainKindId)
                ? transfer.GainKind.ToString()
                : transfer.GainKindId;
            string lossKindId = string.IsNullOrWhiteSpace(transfer.LossKindId)
                ? transfer.LossKind.ToString()
                : transfer.LossKindId;
            MaterialConsequence gain = null;
            MaterialConsequence loss = null;
            int gainIndex = -1;
            int lossIndex = -1;
            int attributableCount = 0;
            for (int i = 0;
                 i < context.Run.Report.MaterialConsequences.Count;
                 i++)
            {
                MaterialConsequence material =
                    context.Run.Report.MaterialConsequences[i];
                if (material == null ||
                    !Equal(material.CauseId, causeRuling.RulingId) ||
                    !Equal(material.ResourceId, entitlement.ResourceId))
                {
                    continue;
                }

                attributableCount++;
                if (Equal(material.AgentId, toAgentId) &&
                    material.Kind == transfer.GainKind &&
                    Equal(material.KindId, gainKindId) &&
                    material.ResourceDelta == entitlement.Units &&
                    material.Cycle == causeRuling.Cycle &&
                    !material.HasNeedEffect)
                {
                    Require(gain == null,
                        $"Transfer '{transfer.TransferId}' duplicated its gain consequence.");
                    gain = material;
                    gainIndex = i;
                }
                else if (Equal(material.AgentId, fromAgentId) &&
                         material.Kind == transfer.LossKind &&
                         Equal(material.KindId, lossKindId) &&
                         material.ResourceDelta == -entitlement.Units &&
                         material.Cycle == causeRuling.Cycle &&
                         !material.HasNeedEffect)
                {
                    Require(loss == null,
                        $"Transfer '{transfer.TransferId}' duplicated its loss consequence.");
                    loss = material;
                    lossIndex = i;
                }
            }

            Require(attributableCount == 2 && gain != null && loss != null,
                $"Transfer '{transfer.TransferId}' lacks its exact conserved material pair.");
            Require(Equal(
                        gain.ConsequenceId,
                        ExclusiveEntitlementService.BuildMaterialConsequenceId(
                            context.Run.Report,
                            causeRuling,
                            state,
                            toAgentId,
                            transfer.GainKind,
                            gainIndex)) &&
                    Equal(
                        loss.ConsequenceId,
                        ExclusiveEntitlementService.BuildMaterialConsequenceId(
                            context.Run.Report,
                            causeRuling,
                            state,
                            fromAgentId,
                            transfer.LossKind,
                            lossIndex)),
                $"Transfer '{transfer.TransferId}' material ids are not deterministic.");
            Require(expectedMaterialIds.Add(gain.ConsequenceId) &&
                    expectedMaterialIds.Add(loss.ConsequenceId),
                $"Transfer '{transfer.TransferId}' reused a material projection.");
        }

        private static void RequireExactConnectedOutcome(
            InstitutionalScenarioExecutionContext context,
            ScenarioExclusiveEntitlementTransferDefinition transfer,
            ScenarioExclusiveEntitlementDefinition entitlement,
            Ruling causeRuling,
            string fromAgentId,
            string toAgentId,
            string pairId)
        {
            ConnectedOutcomePair matched = null;
            int matches = 0;
            for (int i = 0; i < context.Run.Report.ConnectedOutcomes.Count; i++)
            {
                ConnectedOutcomePair candidate =
                    context.Run.Report.ConnectedOutcomes[i];
                if (candidate == null || !Equal(candidate.PairId, pairId)) continue;
                matched = candidate;
                matches++;
            }
            Require(matches == 1,
                $"Transfer '{transfer.TransferId}' requires one exact connected outcome.");

            ScenarioHoldingDefinition holding = InstitutionalScenarioLookup.Holding(
                context.Definition,
                transfer.CauseHoldingId);
            AgentState winner = context.Run.FinalSocietyState.GetAgent(toAgentId);
            AgentState loser = context.Run.FinalSocietyState.GetAgent(fromAgentId);
            Require(matched != null &&
                    Equal(matched.CauseRuleId, holding.RuleId) &&
                    Equal(matched.ConnectionId, entitlement.ResourceId) &&
                    Equal(matched.WinnerAgentId, toAgentId) &&
                    Equal(matched.WinnerDisplayName, winner?.DisplayName) &&
                    matched.WinnerResourceDelta == entitlement.Units &&
                    Equal(matched.LoserAgentId, fromAgentId) &&
                    Equal(matched.LoserDisplayName, loser?.DisplayName) &&
                    matched.LoserResourceDelta == -entitlement.Units &&
                    causeRuling.Disposition == transfer.RequiredRulingDisposition,
                $"Transfer '{transfer.TransferId}' connected outcome was reattributed.");
        }

        private static ExclusiveEntitlementObservation
            FindExclusiveEntitlementObservation(
                InstitutionalConsequenceReport report,
                string entitlementId,
                string resourceId)
        {
            ExclusiveEntitlementObservation result = null;
            int matches = 0;
            for (int i = 0; i < report.ExclusiveEntitlements.Count; i++)
            {
                ExclusiveEntitlementObservation candidate =
                    report.ExclusiveEntitlements[i];
                if (candidate == null ||
                    !Equal(candidate.EntitlementId, entitlementId) ||
                    !Equal(candidate.ResourceId, resourceId))
                {
                    continue;
                }
                result = candidate;
                matches++;
            }
            Require(matches == 1,
                $"Entitlement '{entitlementId}' requires one public observation.");
            return result;
        }

        private static ScenarioCaseDefinition FindCaseByRulingId(
            InstitutionalScenarioDefinition definition,
            string rulingId)
        {
            ScenarioCaseDefinition result = null;
            for (int i = 0; i < definition.Cases.Count; i++)
            {
                ScenarioCaseDefinition candidate = definition.Cases[i];
                if (!Equal(candidate.InitialRulingId, rulingId) &&
                    !Equal(candidate.AdjudicationRulingId, rulingId)) continue;
                if (result != null)
                    throw new InvalidOperationException(
                        $"Ruling '{rulingId}' has more than one case declaration.");
                result = candidate;
            }
            return result;
        }

        private static ScenarioAppealDefinition FindAppeal(
            InstitutionalScenarioDefinition definition,
            string appealId)
        {
            for (int i = 0; i < definition.Appeals.Count; i++)
            {
                if (Equal(definition.Appeals[i].AppealId, appealId))
                    return definition.Appeals[i];
            }
            return null;
        }

        private static ScenarioHoldingDefinition FindHolding(
            InstitutionalScenarioDefinition definition,
            string holdingId)
        {
            for (int i = 0; i < definition.Holdings.Count; i++)
            {
                if (Equal(definition.Holdings[i].HoldingId, holdingId))
                    return definition.Holdings[i];
            }
            return null;
        }

        private static ScenarioEvidenceTemplateDefinition FindEvidenceTemplate(
            InstitutionalScenarioDefinition definition,
            string templateId)
        {
            ScenarioEvidenceTemplateDefinition result = null;
            for (int i = 0; i < definition.EvidenceTemplates.Count; i++)
            {
                ScenarioEvidenceTemplateDefinition candidate =
                    definition.EvidenceTemplates[i];
                if (!Equal(candidate.EvidenceTemplateId, templateId)) continue;
                if (result != null)
                {
                    throw new InvalidOperationException(
                        $"Evidence template '{templateId}' is duplicated.");
                }
                result = candidate;
            }
            return result;
        }

        private static ScenarioActionCausedDescendantCaseDefinition FindDescendant(
            InstitutionalScenarioDefinition definition,
            string caseId)
        {
            ScenarioActionCausedDescendantCaseDefinition result = null;
            for (int i = 0; i < definition.DescendantCases.Count; i++)
            {
                ScenarioActionCausedDescendantCaseDefinition candidate =
                    definition.DescendantCases[i];
                if (!Equal(candidate.CaseId, caseId)) continue;
                if (result != null)
                {
                    throw new InvalidOperationException(
                        $"Descendant case '{caseId}' is declared more than once.");
                }
                result = candidate;
            }
            return result;
        }

        private static ScenarioEvidenceActivatedCaseDefinition FindEvidenceActivation(
            InstitutionalScenarioDefinition definition,
            string caseId)
        {
            ScenarioEvidenceActivatedCaseDefinition result = null;
            for (int i = 0; i < definition.EvidenceActivatedCases.Count; i++)
            {
                ScenarioEvidenceActivatedCaseDefinition candidate =
                    definition.EvidenceActivatedCases[i];
                if (!Equal(candidate.CaseId, caseId)) continue;
                if (result != null)
                {
                    throw new InvalidOperationException(
                        $"Evidence-activated case '{caseId}' is declared more than once.");
                }
                result = candidate;
            }
            return result;
        }

        private static ScenarioEvidenceActivatedCaseDefinition
            FindEvidenceActivationById(
                InstitutionalScenarioDefinition definition,
                string activationId)
        {
            ScenarioEvidenceActivatedCaseDefinition result = null;
            for (int i = 0; i < definition.EvidenceActivatedCases.Count; i++)
            {
                ScenarioEvidenceActivatedCaseDefinition candidate =
                    definition.EvidenceActivatedCases[i];
                if (!Equal(candidate.ActivationId, activationId)) continue;
                if (result != null)
                {
                    throw new InvalidOperationException(
                        $"Case activation '{activationId}' is declared more than once.");
                }
                result = candidate;
            }
            return result;
        }

        private static EvidenceArtifact FindEvidence(
            InstitutionalConsequenceReport report,
            string artifactId)
        {
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                if (Equal(report.EvidenceArtifacts[i].ArtifactId, artifactId))
                    return report.EvidenceArtifacts[i];
            }
            return null;
        }

        private static InstitutionalCaseOpening FindCaseOpening(
            InstitutionalConsequenceReport report,
            string caseId)
        {
            InstitutionalCaseOpening result = null;
            int count = 0;
            for (int i = 0; i < report.CaseOpenings.Count; i++)
            {
                InstitutionalCaseOpening candidate = report.CaseOpenings[i];
                if (candidate == null || !Equal(candidate.CaseId, caseId)) continue;
                result = candidate;
                count++;
            }
            if (count > 1)
            {
                throw new InvalidOperationException(
                    $"Case '{caseId}' has more than one opening projection.");
            }
            return result;
        }

        private static bool ContainsAppeal(
            IReadOnlyList<Appeal> appeals,
            string appealId)
        {
            int count = 0;
            for (int i = 0; i < appeals.Count; i++)
            {
                if (Equal(appeals[i].AppealId, appealId)) count++;
            }
            return count == 1;
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (Equal(values[i], expected)) return true;
            }
            return false;
        }

        private static int Count(IReadOnlyList<string> values, string expected)
        {
            int count = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (Equal(values[i], expected)) count++;
            }
            return count;
        }

        private static bool ContainsExactCitation(
            InstitutionalScenarioDefinition definition,
            string holdingId,
            string caseId,
            string rulingId)
        {
            int count = 0;
            for (int i = 0; i < definition.HoldingCitations.Count; i++)
            {
                ScenarioHoldingCitationDefinition citation =
                    definition.HoldingCitations[i];
                if (Equal(citation.HoldingId, holdingId) &&
                    Equal(citation.TargetCaseId, caseId) &&
                    Equal(citation.TargetRulingId, rulingId))
                {
                    count++;
                }
            }
            return count == 1;
        }

        private static bool Equal(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
