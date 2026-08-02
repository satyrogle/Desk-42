using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Converts autonomous society actions into declared appeal, reliance and
    /// descendant-case consequences. No action is synthesized by this phase.
    /// </summary>
    internal static class InstitutionalScenarioActionPhase
    {
        internal static void FileObservedAppeals(
            InstitutionalScenarioExecutionContext context,
            SimulationInput input,
            SimulationStepResult step)
        {
            for (int i = 0; i < step.Events.Count; i++)
            {
                SocietyEvent societyEvent = step.Events[i];
                if (societyEvent.Kind != SocietyEventKind.AppealFiled) continue;

                ScenarioAppealDefinition declaration = FindAppealDeclaration(
                    context,
                    societyEvent);
                // An opportunity can remain visible outside the one filing cycle
                // declared for institutional acceptance. The autonomous attempt is
                // still observed, but it does not silently become another appeal.
                if (declaration == null) continue;
                ScenarioCaseDefinition appealCase = InstitutionalScenarioLookup.Case(
                    context.Definition,
                    declaration.CaseId);
                if (!InstitutionalScenarioLookup.CaseIsActive(
                        context.Definition,
                        context.Run.Report,
                        appealCase,
                        societyEvent.Tick))
                {
                    // Optional descendant cases own no docket until their causal
                    // trigger has materialised them. A matching autonomous attempt
                    // remains observable but cannot create an institutional appeal.
                    continue;
                }
                if (!InstitutionalScenarioLookup.TryResolveEvidenceArtifactIds(
                        context.Run.Report,
                        declaration.GroundsEvidenceTemplateIds,
                        declaration.CaseId,
                        societyEvent.Tick,
                        $"Appeal '{declaration.AppealId}' grounds",
                        out List<string> groundsEvidenceArtifactIds))
                {
                    // The action remains observable, but an optional institutional
                    // branch cannot materialise without every declared ground.
                    continue;
                }
                InstitutionalServiceResult<Appeal> result =
                    InstitutionalAppealPrecedentService.FileAppeal(
                        context.Run,
                        societyEvent,
                        input.AppealOpportunities,
                        groundsEvidenceArtifactIds);
                InstitutionalScenarioLookup.RequireAccepted(result, "appeal filing");
                RememberAppeal(context, declaration.AppealId, result.Value);
            }
        }

        internal static void CreateDueReliance(
            InstitutionalScenarioExecutionContext context,
            SimulationStepResult step,
            long cycle)
        {
            for (int i = 0; i < context.Definition.RelianceDefinitions.Count; i++)
            {
                ScenarioIrreversibleRelianceDefinition declaration =
                    context.Definition.RelianceDefinitions[i];
                if (declaration.Cycle != cycle) continue;
                if (!context.StatusEffectsByDeclarationId.TryGetValue(
                        declaration.EnablingEffectRequestId,
                        out ScenarioOfficialStatusEffectExecutionResult enabling) ||
                    !enabling.RequiredDispositionMatched ||
                    enabling.StatusMutationResult.RecordedMutation == null ||
                    enabling.StatusMutationResult.CurrentRecognisedState !=
                        declaration.ExpectedRecognisedState)
                {
                    continue;
                }

                string actorAgentId = context.AgentIdByRole[declaration.RelyingRoleId];
                SocietyEvent sourceEvent = FindRelianceSourceEvent(
                    step,
                    declaration,
                    actorAgentId);
                if (sourceEvent == null) continue;

                RelianceCreationRequest request = CreateRelianceRequest(
                    context,
                    declaration,
                    enabling,
                    sourceEvent,
                    actorAgentId);
                RelianceCreationResult result = InstitutionalRelianceService.TryCreate(
                    context.Run,
                    request);
                if (!result.Created)
                {
                    throw new InvalidOperationException(
                        $"Reliance '{declaration.RelianceId}' was rejected: " +
                        result.FailureReason);
                }
            }
        }

        internal static void OpenDueDescendantCases(
            InstitutionalScenarioExecutionContext context,
            long cycle)
        {
            for (int i = 0; i < context.Definition.DescendantCases.Count; i++)
            {
                ScenarioActionCausedDescendantCaseDefinition declaration =
                    context.Definition.DescendantCases[i];
                if (declaration.OpenCycle != cycle) continue;
                ScenarioCaseDefinition referencedCase = InstitutionalScenarioLookup.Case(
                    context.Definition,
                    declaration.CaseId);
                InstitutionalServiceResult<DescendantCase> result =
                    InstitutionalActionCausedDescendantCaseService.Open(
                        context.Run,
                        declaration,
                        referencedCase,
                        context.AgentIdByRole,
                        cycle);
                InstitutionalScenarioLookup.RequireAccepted(
                    result,
                    "descendant-case opening");
            }
        }

        internal static void CreateDueRelianceRecoveries(
            InstitutionalScenarioExecutionContext context,
            long cycle)
        {
            for (int i = 0; i < context.Definition.RelianceRecoveries.Count; i++)
            {
                ScenarioRelianceRecoveryDefinition declaration =
                    context.Definition.RelianceRecoveries[i];
                if (declaration.Cycle != cycle) continue;
                Ruling reversal = InstitutionalScenarioLookup.Ruling(
                    context.Run.Report,
                    declaration.TriggerReversalRulingId);
                if (reversal == null ||
                    (reversal.Disposition != RulingDisposition.ReversedAndDenied &&
                     reversal.Disposition != RulingDisposition.ReversedAndRecognised))
                {
                    continue;
                }

                RelianceEvent reliedOn = FindReliance(
                    context.Run,
                    declaration.RelianceId);
                if (reliedOn != null &&
                    !InstitutionalScenarioLookup.Equal(
                        reliedOn.AgentId,
                        context.AgentIdByRole[declaration.ClaimantRoleId]))
                {
                    throw new InvalidOperationException(
                        $"Reliance recovery '{declaration.RecoveryDefinitionId}' has a " +
                        "claimant different from the relying agent.");
                }

                RelianceRecoveryResult result =
                    InstitutionalRelianceService.TryCreateRecoveryAfterReversal(
                        context.Run,
                        reversal,
                        new RelianceRecoveryRequest
                        {
                            RelianceEventId = declaration.RelianceId,
                            CaseIdPrefix = declaration.CaseIdPrefix,
                            ParentCaseId = declaration.ParentCaseId,
                            RespondentId = context.AgentIdByRole[
                                declaration.RespondentRoleId],
                            OfficialIssueId = declaration.IssueId,
                            Facts = declaration.Facts.Copy(),
                        });
                if (!result.Created &&
                    result.FailureReason !=
                        RelianceRecoveryFailureReason.RelianceNotFound)
                {
                    throw new InvalidOperationException(
                        $"Reliance recovery '{declaration.RecoveryDefinitionId}' was " +
                        $"rejected: {result.FailureReason}.");
                }
            }
        }

        private static RelianceCreationRequest CreateRelianceRequest(
            InstitutionalScenarioExecutionContext context,
            ScenarioIrreversibleRelianceDefinition declaration,
            ScenarioOfficialStatusEffectExecutionResult enabling,
            SocietyEvent sourceEvent,
            string actorAgentId)
        {
            var request = new RelianceCreationRequest
            {
                RelianceEventId = declaration.RelianceId,
                ObservationId = $"observation:{declaration.RelianceId}",
                SourceActionEventId = sourceEvent.EventId,
                ActorAgentId = actorAgentId,
                BeneficiaryAgentId = context.AgentIdByRole[declaration.BeneficiaryRoleId],
                RelatedAgentId = string.IsNullOrEmpty(declaration.RelatedRoleId)
                    ? null
                    : context.AgentIdByRole[declaration.RelatedRoleId],
                ExpectedActionKind = declaration.SourceActionKind,
                ExpectedOpportunityId = declaration.SourceOpportunityId,
                EnablingRulingId = declaration.EnablingRulingId,
                EnablingMutationId = enabling.StatusMutationResult.RecordedMutation.MutationId,
                RequiredStatusId = declaration.ExpectedStatusId,
                ExpectedRecognisedState = declaration.ExpectedRecognisedState,
                ChoiceId = declaration.IrreversibleChoiceKey,
                RecordedChoiceId = declaration.IrreversibleChoiceKey,
                AbandonedAlternativeId = declaration.AbandonedAlternativeKey,
                ResourceId = declaration.ResourceId,
            };
            for (int i = 0; i < declaration.Effects.Count; i++)
            {
                ScenarioRelianceEffectDefinition effect = declaration.Effects[i];
                request.Effects.Add(new RelianceEffectDelta
                {
                    EffectId = effect.EffectId,
                    Recipient = effect.Recipient switch
                    {
                        ScenarioRelianceEffectRecipient.RelyingRole =>
                            RelianceEffectRecipient.Actor,
                        ScenarioRelianceEffectRecipient.BeneficiaryRole =>
                            RelianceEffectRecipient.Beneficiary,
                        ScenarioRelianceEffectRecipient.RelatedRole =>
                            RelianceEffectRecipient.RelatedAgent,
                        _ => throw new InvalidOperationException(
                            $"Reliance effect '{effect.EffectId}' has an invalid recipient."),
                    },
                    ResourceDelta = effect.ResourceDelta,
                    MaterialKind = effect.MaterialKind,
                    MaterialKindId = effect.MaterialKindId,
                    ResourceId = effect.ResourceId,
                    Need = effect.HasNeedEffect ? effect.Need : null,
                    NeedPressureDelta = effect.NeedPressureDelta,
                });
            }
            return request;
        }

        private static RelianceEvent FindReliance(
            InstitutionalConsequenceRun run,
            string relianceId)
        {
            RelianceEvent match = null;
            int count = 0;
            for (int i = 0; i < run.RelianceLedger.Count; i++)
            {
                if (!InstitutionalScenarioLookup.Equal(
                        run.RelianceLedger[i].RelianceEventId,
                        relianceId)) continue;
                match = run.RelianceLedger[i];
                count++;
            }
            if (count > 1)
                throw new InvalidOperationException($"Duplicate reliance id '{relianceId}'.");
            return match;
        }

        private static ScenarioAppealDefinition FindAppealDeclaration(
            InstitutionalScenarioExecutionContext context,
            SocietyEvent filingEvent)
        {
            ScenarioAppealDefinition match = null;
            int count = 0;
            for (int i = 0; i < context.Definition.Appeals.Count; i++)
            {
                ScenarioAppealDefinition candidate = context.Definition.Appeals[i];
                if (candidate.FilingCycle != filingEvent.Tick ||
                    !InstitutionalScenarioLookup.Equal(
                        candidate.OpportunityId,
                        filingEvent.OpportunityId) ||
                    !InstitutionalScenarioLookup.Equal(
                        context.AgentIdByRole[candidate.AppellantRoleId],
                        filingEvent.ActorId))
                {
                    continue;
                }
                match = candidate;
                count++;
            }
            if (count > 1)
            {
                throw new InvalidOperationException(
                    $"Observed appeal event '{filingEvent.EventId}' matched {count} " +
                    "declarative appeal definitions.");
            }
            return match;
        }

        private static void RememberAppeal(
            InstitutionalScenarioExecutionContext context,
            string declaredAppealId,
            Appeal appeal)
        {
            if (appeal == null)
                throw new InvalidOperationException("An accepted appeal filing has no appeal value.");
            if (context.ActualAppealsByDeclarationId.TryGetValue(
                    declaredAppealId,
                    out Appeal existing))
            {
                if (!ReferenceEquals(existing, appeal))
                {
                    throw new InvalidOperationException(
                        $"Declared appeal '{declaredAppealId}' mapped to conflicting filings.");
                }
                return;
            }
            context.ActualAppealsByDeclarationId.Add(declaredAppealId, appeal);
        }

        private static SocietyEvent FindRelianceSourceEvent(
            SimulationStepResult step,
            ScenarioIrreversibleRelianceDefinition declaration,
            string actorAgentId)
        {
            SocietyEvent match = null;
            int count = 0;
            SocietyEventKind expectedKind = EventKindFor(declaration.SourceActionKind);
            for (int i = 0; i < step.Events.Count; i++)
            {
                SocietyEvent candidate = step.Events[i];
                if (candidate.Kind != expectedKind ||
                    !InstitutionalScenarioLookup.Equal(candidate.ActorId, actorAgentId) ||
                    !InstitutionalScenarioLookup.Equal(
                        candidate.OpportunityId,
                        declaration.SourceOpportunityId))
                {
                    continue;
                }
                match = candidate;
                count++;
            }
            if (count > 1)
            {
                throw new InvalidOperationException(
                    $"Reliance '{declaration.RelianceId}' has an ambiguous source action.");
            }
            return match;
        }

        private static SocietyEventKind EventKindFor(SocietyActionKind action)
        {
            return action switch
            {
                SocietyActionKind.Work => SocietyEventKind.WorkPerformed,
                SocietyActionKind.SeekAid => SocietyEventKind.AidRequested,
                SocietyActionKind.Appeal => SocietyEventKind.AppealFiled,
                _ => throw new InvalidOperationException(
                    $"Action '{action}' cannot source a declared reliance opportunity."),
            };
        }
    }
}
