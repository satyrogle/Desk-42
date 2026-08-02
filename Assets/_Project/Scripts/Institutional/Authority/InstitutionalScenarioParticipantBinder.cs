using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional
{
    /// <summary>
    /// Detached explanation of one semantic role binding. It records stable values,
    /// not references to mutable scenario or society state.
    /// </summary>
    internal sealed class ScenarioParticipantBindingDiagnostic
    {
        internal ScenarioParticipantBindingDiagnostic(
            string roleId,
            string boundAgentStableId,
            int boundAgentSimulationOrdinal,
            int semanticCandidateCount,
            IReadOnlyList<string> distinctFromRoleIds)
        {
            RoleId = roleId;
            BoundAgentStableId = boundAgentStableId;
            BoundAgentSimulationOrdinal = boundAgentSimulationOrdinal;
            SemanticCandidateCount = semanticCandidateCount;
            DistinctFromRoleIds = new ReadOnlyCollection<string>(
                new List<string>(distinctFromRoleIds));
        }

        internal string RoleId { get; }
        internal string BoundAgentStableId { get; }
        internal int BoundAgentSimulationOrdinal { get; }
        internal int SemanticCandidateCount { get; }
        internal IReadOnlyList<string> DistinctFromRoleIds { get; }
    }

    /// <summary>
    /// Structurally read-only role-to-agent bindings. Agent objects remain owned by
    /// the supplied society; the dictionaries themselves cannot be rewritten.
    /// </summary>
    internal sealed class InstitutionalScenarioParticipantBindings
    {
        private readonly IReadOnlyDictionary<string, AgentState> _agentsByRole;
        private readonly IReadOnlyDictionary<int, AgentState> _agentsBySimulationOrdinal;

        internal InstitutionalScenarioParticipantBindings(
            IDictionary<string, AgentState> agentsByRole,
            IReadOnlyList<ScenarioParticipantBindingDiagnostic> diagnostics)
        {
            var roleCopy = new Dictionary<string, AgentState>(
                agentsByRole, StringComparer.Ordinal);
            var ordinalCopy = new Dictionary<int, AgentState>();
            foreach (KeyValuePair<string, AgentState> pair in roleCopy)
                ordinalCopy[pair.Value.SimulationOrdinal] = pair.Value;

            _agentsByRole = new ReadOnlyDictionary<string, AgentState>(roleCopy);
            _agentsBySimulationOrdinal = new ReadOnlyDictionary<int, AgentState>(ordinalCopy);
            Diagnostics = new ReadOnlyCollection<ScenarioParticipantBindingDiagnostic>(
                new List<ScenarioParticipantBindingDiagnostic>(diagnostics));
        }

        internal IReadOnlyDictionary<string, AgentState> AgentsByRole => _agentsByRole;
        internal IReadOnlyList<ScenarioParticipantBindingDiagnostic> Diagnostics { get; }

        internal AgentState GetAgent(string roleId)
        {
            if (string.IsNullOrWhiteSpace(roleId))
                throw new ArgumentException("A semantic role id is required.", nameof(roleId));
            if (!_agentsByRole.TryGetValue(roleId, out AgentState agent))
                throw new KeyNotFoundException($"No participant is bound to role '{roleId}'.");
            return agent;
        }

        internal AgentState GetAgentBySimulationOrdinal(int simulationOrdinal)
        {
            if (!_agentsBySimulationOrdinal.TryGetValue(simulationOrdinal, out AgentState agent))
            {
                throw new KeyNotFoundException(
                    $"No bound participant has simulation ordinal {simulationOrdinal}.");
            }
            return agent;
        }
    }

    /// <summary>
    /// Resolves semantic participant roles without scenario-specific code or direct
    /// agent identifiers. A binding is accepted only when the complete constraint
    /// problem has exactly one solution.
    /// </summary>
    internal static class InstitutionalScenarioParticipantBinder
    {
        private sealed class RoleCandidates
        {
            internal ScenarioParticipantRoleDefinition Definition;
            internal List<AgentState> Candidates;
        }

        internal static InstitutionalScenarioParticipantBindings Bind(
            InstitutionalScenarioDefinition definition)
        {
            InstitutionalScenarioParticipantBindingValidator.Validate(definition);

            List<RoleCandidates> roles = BuildCandidateSets(definition);
            var selected = new Dictionary<string, AgentState>(StringComparer.Ordinal);
            Dictionary<string, AgentState> uniqueSolution = null;
            int solutionCount = 0;

            Search(roles, 0, selected, ref uniqueSolution, ref solutionCount);
            if (solutionCount == 0)
            {
                throw new InvalidOperationException(
                    "Participant role constraints have no valid binding; a distinct-role constraint " +
                    "requires the same agent to occupy two forbidden roles.");
            }
            if (solutionCount > 1)
            {
                throw new InvalidOperationException(
                    "Participant role binding is ambiguous; more than one complete semantic binding is valid.");
            }

            var diagnosticRoles = new List<RoleCandidates>(roles);
            diagnosticRoles.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.Definition.RoleId, right.Definition.RoleId));
            var diagnostics = new List<ScenarioParticipantBindingDiagnostic>(diagnosticRoles.Count);
            for (int i = 0; i < diagnosticRoles.Count; i++)
            {
                RoleCandidates role = diagnosticRoles[i];
                AgentState agent = uniqueSolution[role.Definition.RoleId];
                List<string> distinctRoleIds = EffectiveDistinctRoleIds(role.Definition, roles);
                diagnostics.Add(new ScenarioParticipantBindingDiagnostic(
                    role.Definition.RoleId,
                    agent.StableId,
                    agent.SimulationOrdinal,
                    role.Candidates.Count,
                    distinctRoleIds));
            }

            return new InstitutionalScenarioParticipantBindings(uniqueSolution, diagnostics);
        }

        private static List<string> EffectiveDistinctRoleIds(
            ScenarioParticipantRoleDefinition role,
            IReadOnlyList<RoleCandidates> roles)
        {
            var result = new List<string>();
            for (int i = 0; i < roles.Count; i++)
            {
                ScenarioParticipantRoleDefinition other = roles[i].Definition;
                if (string.Equals(role.RoleId, other.RoleId, StringComparison.Ordinal)) continue;
                if (RequiresDistinct(role, other)) result.Add(other.RoleId);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static void Search(
            IReadOnlyList<RoleCandidates> roles,
            int roleIndex,
            Dictionary<string, AgentState> selected,
            ref Dictionary<string, AgentState> uniqueSolution,
            ref int solutionCount)
        {
            if (solutionCount > 1) return;
            if (roleIndex == roles.Count)
            {
                solutionCount++;
                if (solutionCount == 1)
                {
                    uniqueSolution = new Dictionary<string, AgentState>(
                        selected, StringComparer.Ordinal);
                }
                return;
            }

            RoleCandidates role = roles[roleIndex];
            for (int i = 0; i < role.Candidates.Count; i++)
            {
                AgentState candidate = role.Candidates[i];
                if (ConflictsWithSelected(role.Definition, candidate, roles, selected)) continue;

                selected.Add(role.Definition.RoleId, candidate);
                Search(roles, roleIndex + 1, selected, ref uniqueSolution, ref solutionCount);
                selected.Remove(role.Definition.RoleId);
                if (solutionCount > 1) return;
            }
        }

        private static bool ConflictsWithSelected(
            ScenarioParticipantRoleDefinition role,
            AgentState candidate,
            IReadOnlyList<RoleCandidates> roles,
            IReadOnlyDictionary<string, AgentState> selected)
        {
            foreach (KeyValuePair<string, AgentState> existing in selected)
            {
                if (!string.Equals(existing.Value.StableId, candidate.StableId,
                        StringComparison.Ordinal)) continue;

                ScenarioParticipantRoleDefinition existingRole = FindRole(roles, existing.Key);
                if (RequiresDistinct(role, existingRole)) return true;
            }
            return false;
        }

        private static bool RequiresDistinct(
            ScenarioParticipantRoleDefinition left,
            ScenarioParticipantRoleDefinition right)
        {
            return ContainsOrdinal(left.DistinctFromRoleIds, right.RoleId) ||
                   ContainsOrdinal(right.DistinctFromRoleIds, left.RoleId);
        }

        private static ScenarioParticipantRoleDefinition FindRole(
            IReadOnlyList<RoleCandidates> roles,
            string roleId)
        {
            for (int i = 0; i < roles.Count; i++)
            {
                if (string.Equals(roles[i].Definition.RoleId, roleId, StringComparison.Ordinal))
                    return roles[i].Definition;
            }
            throw new InvalidOperationException($"Unknown selected role '{roleId}'.");
        }

        private static List<RoleCandidates> BuildCandidateSets(
            InstitutionalScenarioDefinition definition)
        {
            var result = new List<RoleCandidates>(definition.ParticipantRoles.Count);
            for (int i = 0; i < definition.ParticipantRoles.Count; i++)
            {
                ScenarioParticipantRoleDefinition role = definition.ParticipantRoles[i];
                var candidates = new List<AgentState>();
                for (int j = 0; j < definition.InitialSociety.Agents.Count; j++)
                {
                    AgentState agent = definition.InitialSociety.Agents[j];
                    if (MatchesAll(role.Query, agent)) candidates.Add(agent);
                }

                candidates.Sort(CompareAgents);
                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Participant role '{role.RoleId}' has no semantic query match.");
                }
                result.Add(new RoleCandidates { Definition = role, Candidates = candidates });
            }

            result.Sort((left, right) =>
            {
                int candidateCount = left.Candidates.Count.CompareTo(right.Candidates.Count);
                return candidateCount != 0
                    ? candidateCount
                    : StringComparer.Ordinal.Compare(
                        left.Definition.RoleId, right.Definition.RoleId);
            });
            return result;
        }

        private static int CompareAgents(AgentState left, AgentState right)
        {
            int ordinal = left.SimulationOrdinal.CompareTo(right.SimulationOrdinal);
            return ordinal != 0
                ? ordinal
                : StringComparer.Ordinal.Compare(left.StableId, right.StableId);
        }

        private static bool MatchesAll(ScenarioParticipantQuery query, AgentState agent)
        {
            if (!string.IsNullOrEmpty(query.RequiredSpeciesId) &&
                !string.Equals(query.RequiredSpeciesId, agent.SpeciesId, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrEmpty(query.RequiredEmployerId) &&
                !string.Equals(query.RequiredEmployerId, agent.EmployerId, StringComparison.Ordinal))
                return false;

            for (int i = 0; i < query.RequiredRecognisedStatusIds.Count; i++)
            {
                if (!HasExplicitStatus(agent, query.RequiredRecognisedStatusIds[i], true)) return false;
            }
            for (int i = 0; i < query.RequiredUnrecognisedStatusIds.Count; i++)
            {
                if (!HasExplicitStatus(agent, query.RequiredUnrecognisedStatusIds[i], false)) return false;
            }
            for (int i = 0; i < query.RequiredAnomalyTraitIds.Count; i++)
            {
                if (!HasAnomalyTrait(agent, query.RequiredAnomalyTraitIds[i])) return false;
            }
            for (int i = 0; i < query.RequiredCommitmentKinds.Count; i++)
            {
                if (!HasCommitmentKind(agent, query.RequiredCommitmentKinds[i])) return false;
            }
            return true;
        }

        private static bool HasExplicitStatus(AgentState agent, string statusId, bool recognised)
        {
            for (int i = 0; i < agent.Standing.OfficialStatuses.Count; i++)
            {
                OfficialStatusState status = agent.Standing.OfficialStatuses[i];
                if (string.Equals(status.StatusId, statusId, StringComparison.Ordinal))
                    return status.Recognised == recognised;
            }
            return false;
        }

        private static bool HasAnomalyTrait(AgentState agent, string traitId)
        {
            for (int i = 0; i < agent.AnomalyRules.Count; i++)
            {
                if (string.Equals(agent.AnomalyRules[i].TraitId, traitId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool HasCommitmentKind(AgentState agent, string commitmentKind)
        {
            for (int i = 0; i < agent.Commitments.Count; i++)
            {
                if (string.Equals(agent.Commitments[i].Kind, commitmentKind,
                        StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool ContainsOrdinal(IReadOnlyList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], expected, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
