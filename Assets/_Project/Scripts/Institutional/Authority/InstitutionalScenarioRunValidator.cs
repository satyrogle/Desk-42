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
        internal static void Validate(InstitutionalScenarioExecutionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            InstitutionalScenarioDefinitionValidator.Validate(context.Definition);
            context.Policy.Validate();
            ValidateTopLevel(context);
            ValidateBindings(context);
            ValidateDeclaredEvidence(context);
            ValidateDeclaredRulings(context);
            ValidateDeclaredAppeals(context);
            ValidateDeclaredHoldings(context);
            ValidateDeclaredEntitlements(context);
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
                Require(template != null && Equal(template.CaseId, artifact.CaseId) &&
                        Equal(template.IssueId, artifact.IssueId),
                    $"Evidence '{artifact.ArtifactId}' escaped its declared template case.");

                ScenarioCaseDefinition caseDefinition = InstitutionalScenarioLookup.Case(
                    context.Definition,
                    artifact.CaseId);
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
                Require(descendant != null &&
                        InstitutionalScenarioLookup.CaseHasOpened(
                            context,
                            caseDefinition) &&
                        InstitutionalActionCausedDescendantCaseService
                            .IsExactDeclaredTriggerEvidence(
                                context.Run,
                                descendant,
                                context.AgentIdByRole,
                                artifact),
                    $"Evidence '{artifact.ArtifactId}' entered before its case without " +
                    "being the exact declared opening trigger.");
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
                    Require(Contains(
                            declaration.CitedHoldingIds,
                            ruling.CitedHoldingIds[citationIndex]),
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
