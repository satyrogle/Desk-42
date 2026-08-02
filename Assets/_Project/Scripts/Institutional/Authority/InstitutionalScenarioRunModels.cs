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
        internal InstitutionalScenarioRunResult(
            InstitutionalConsequenceRun assessorRun,
            IReadOnlyDictionary<string, string> agentIdByRole,
            IReadOnlyList<ScenarioParticipantBindingDiagnostic> bindingDiagnostics,
            ExclusiveEntitlementRegistry entitlementRegistry,
            ScenarioRunStateInitializationResult initialization)
        {
            AssessorRun = assessorRun ?? throw new ArgumentNullException(nameof(assessorRun));
            AgentIdByRole = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(
                    agentIdByRole ?? throw new ArgumentNullException(nameof(agentIdByRole)),
                    StringComparer.Ordinal));
            BindingDiagnostics = new ReadOnlyCollection<ScenarioParticipantBindingDiagnostic>(
                new List<ScenarioParticipantBindingDiagnostic>(
                    bindingDiagnostics ??
                    throw new ArgumentNullException(nameof(bindingDiagnostics))));
            EntitlementRegistry = entitlementRegistry ??
                throw new ArgumentNullException(nameof(entitlementRegistry));
            Initialization = initialization ??
                throw new ArgumentNullException(nameof(initialization));
        }

        internal InstitutionalConsequenceReport Report => AssessorRun.Report;
        internal InstitutionalConsequenceRun AssessorRun { get; }
        internal IReadOnlyDictionary<string, string> AgentIdByRole { get; }
        internal IReadOnlyList<ScenarioParticipantBindingDiagnostic> BindingDiagnostics { get; }
        internal ExclusiveEntitlementRegistry EntitlementRegistry { get; }
        internal ScenarioRunStateInitializationResult Initialization { get; }
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
