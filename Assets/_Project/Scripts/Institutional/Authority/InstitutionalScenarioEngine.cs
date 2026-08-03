using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional
{
    /// <summary>
    /// Deterministic pulse coordinator for validated declarative scenarios. It owns
    /// ordering only; each causal transition remains in its focused generic service
    /// or phase coordinator.
    /// </summary>
    public static class InstitutionalScenarioEngine
    {
        public static InstitutionalConsequenceReport RunScenario(
            InstitutionalScenarioDefinition definition,
            InstitutionalPolicyConfiguration policy)
        {
            return Run(definition, policy).Report;
        }

        internal static InstitutionalScenarioRunResult Run(
            InstitutionalScenarioDefinition definition,
            InstitutionalPolicyConfiguration policy)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            InstitutionalScenarioDefinitionValidator.Validate(definition);
            policy.Validate();

            InstitutionalPolicyConfiguration frozenPolicy = policy.CloneWithIdentity(
                policy.PolicyConfigurationId,
                policy.PolicyVersion);
            frozenPolicy.Validate();
            var declaredEvidenceClassIds = new List<string>(
                definition.EvidenceTemplates.Count);
            for (int i = 0; i < definition.EvidenceTemplates.Count; i++)
            {
                declaredEvidenceClassIds.Add(
                    definition.EvidenceTemplates[i].EvidenceClassId);
            }
            frozenPolicy.ValidateEvidenceClassCoverage(declaredEvidenceClassIds);

            InstitutionalScenarioParticipantBindings sourceBindings =
                InstitutionalScenarioParticipantBinder.Bind(definition);
            IReadOnlyDictionary<string, string> agentIdByRole =
                CaptureAgentIds(sourceBindings);
            SocietyState society = SocietyStateDeepCopy.Copy(definition.InitialSociety);
            society.Regime = frozenPolicy.CreateRegime();
            SocietyStateValidator.Validate(society);

            var run = new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport
                {
                    MasterSeed = society.MasterSeed,
                    PolicyConfigurationId = frozenPolicy.PolicyConfigurationId,
                    PrimaryCaseId = definition.PrimaryCaseId,
                },
            };
            ScenarioRunStateInitializationResult initialization =
                InstitutionalScenarioStateInitializer.InitialiseDeclaredState(
                    definition,
                    society,
                    run,
                    agentIdByRole);
            var context = new InstitutionalScenarioExecutionContext(
                definition,
                frozenPolicy,
                run,
                agentIdByRole);
            InstitutionalScenarioEntitlementPhase.RegisterInitial(context);

            var simulation = new SocietySimulation();
            for (long cycle = definition.StartCycle + 1;
                 cycle <= definition.EndCycle;
                 cycle++)
            {
                InstitutionalScenarioStateInitializer.ApplyLivedIncidentSeeds(
                    definition,
                    society,
                    run,
                    agentIdByRole,
                    cycle);
                SimulationInput input = InstitutionalScenarioInputBuilder.Build(
                    definition,
                    cycle,
                    agentIdByRole,
                    run.Report);
                SimulationStepResult step = simulation.Advance(society, input);

                // These phases observe the same already-frozen decision pulse. No
                // ruling or status mutation can feed back into this cycle's choices.
                InstitutionalActionProjector.Capture(run, step);
                InstitutionalScenarioEvidenceProjector.Project(
                    run,
                    definition,
                    step,
                    agentIdByRole);
                InstitutionalEvidenceActivatedCaseService.OpenDueCases(
                    context,
                    cycle);
                InstitutionalScenarioActionPhase.FileObservedAppeals(context, input, step);
                InstitutionalScenarioActionPhase.CreateDueReliance(context, step, cycle);
                InstitutionalScenarioActionPhase.OpenDueDescendantCases(context, cycle);
                InstitutionalScenarioAdjudicationPhase.IssueDueInitialRulings(context, cycle);
                InstitutionalScenarioAdjudicationPhase.ResolveDueAppeals(context, cycle);
                InstitutionalScenarioActionPhase.CreateDueRelianceRecoveries(context, cycle);
                InstitutionalScenarioAdjudicationPhase.ExecuteDueStatusEffects(context, cycle);
                InstitutionalScenarioEntitlementPhase.TransferDue(context, cycle);
            }

            run.Report.FinalCycle = society.CurrentTick;
            InstitutionalScenarioRunValidator.Validate(context);
            return new InstitutionalScenarioRunResult(
                context,
                sourceBindings.Diagnostics,
                initialization);
        }

        private static IReadOnlyDictionary<string, string> CaptureAgentIds(
            InstitutionalScenarioParticipantBindings bindings)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, AgentState> pair in bindings.AgentsByRole)
                result.Add(pair.Key, pair.Value.StableId);
            return new ReadOnlyDictionary<string, string>(result);
        }
    }
}
