using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional
{
    /// <summary>
    /// Assessor result for one declarative scenario execution. The public report is
    /// the player-safe projection; authority state remains internal to this assembly.
    /// </summary>
    internal sealed class InstitutionalScenarioRunResult
    {
        private readonly InstitutionalScenarioExecutionContext _validationContext;

        internal InstitutionalScenarioRunResult(
            InstitutionalScenarioExecutionContext validationContext,
            IReadOnlyList<ScenarioParticipantBindingDiagnostic> bindingDiagnostics,
            ScenarioRunStateInitializationResult initialization)
        {
            _validationContext = validationContext ??
                throw new ArgumentNullException(nameof(validationContext));
            AssessorRun = _validationContext.Run;
            AgentIdByRole = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(
                    _validationContext.AgentIdByRole,
                    StringComparer.Ordinal));
            BindingDiagnostics = new ReadOnlyCollection<ScenarioParticipantBindingDiagnostic>(
                new List<ScenarioParticipantBindingDiagnostic>(
                    bindingDiagnostics ??
                    throw new ArgumentNullException(nameof(bindingDiagnostics))));
            EntitlementRegistry = _validationContext.EntitlementRegistry;
            Initialization = initialization ??
                throw new ArgumentNullException(nameof(initialization));
        }

        internal InstitutionalConsequenceReport Report => AssessorRun.Report;
        internal InstitutionalConsequenceRun AssessorRun { get; }
        internal IReadOnlyDictionary<string, string> AgentIdByRole { get; }
        internal IReadOnlyList<ScenarioParticipantBindingDiagnostic> BindingDiagnostics { get; }
        internal ExclusiveEntitlementRegistry EntitlementRegistry { get; }
        internal ScenarioRunStateInitializationResult Initialization { get; }
        internal void ValidateAgainstOrigin()
        {
            InstitutionalScenarioRunValidator.Validate(_validationContext);
        }
    }

    internal sealed class InstitutionalScenarioExecutionContext
    {
        internal InstitutionalScenarioExecutionContext(
            InstitutionalScenarioDefinition definition,
            InstitutionalPolicyConfiguration policy,
            InstitutionalConsequenceRun run,
            IReadOnlyDictionary<string, string> agentIdByRole)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Run = run ?? throw new ArgumentNullException(nameof(run));
            AgentIdByRole = agentIdByRole ??
                throw new ArgumentNullException(nameof(agentIdByRole));
        }

        internal InstitutionalScenarioDefinition Definition { get; }
        internal InstitutionalPolicyConfiguration Policy { get; }
        internal InstitutionalConsequenceRun Run { get; }
        internal IReadOnlyDictionary<string, string> AgentIdByRole { get; }
        internal ExclusiveEntitlementRegistry EntitlementRegistry { get; } = new();
        internal Dictionary<string, Appeal> ActualAppealsByDeclarationId { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, ScenarioOfficialStatusEffectExecutionResult>
            StatusEffectsByDeclarationId { get; } = new(StringComparer.Ordinal);
    }
}
