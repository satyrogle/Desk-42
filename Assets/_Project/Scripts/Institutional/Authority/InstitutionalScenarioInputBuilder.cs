using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Projects one declarative scenario schedule row into the detached input read by
    /// the generic agent simulation. It resolves roles, but owns no scenario effects.
    /// </summary>
    internal static class InstitutionalScenarioInputBuilder
    {
        internal static SimulationInput Build(
            InstitutionalScenarioDefinition definition,
            long cycle,
            IReadOnlyDictionary<string, string> roleAgentIds)
        {
            return Build(definition, cycle, roleAgentIds, report: null);
        }

        internal static SimulationInput Build(
            InstitutionalScenarioDefinition definition,
            long cycle,
            IReadOnlyDictionary<string, string> roleAgentIds,
            InstitutionalConsequenceReport report)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (roleAgentIds == null) throw new ArgumentNullException(nameof(roleAgentIds));

            // Scenario records are mutable serializable data. Revalidate at this
            // execution boundary so a post-load or post-binding edit cannot bypass
            // the definition contract.
            InstitutionalScenarioDefinitionValidator.Validate(definition);

            Dictionary<string, string> bindings = CaptureBindings(definition, roleAgentIds);
            ScenarioCycleScheduleEntry schedule = SelectScheduleEntry(definition, cycle);
            Dictionary<string, ScenarioOpportunityDefinition> opportunities =
                IndexOpportunities(definition.Opportunities);

            var input = new SimulationInput
            {
                IncidentId = schedule.IncidentId,
                WorkAvailable = schedule.WorkAvailable,
                AidAvailable = schedule.AidAvailable,
                DisclosureRequested = schedule.DisclosureRequested,
                AppealWindowOpen = schedule.AppealWindowOpen,
                OpenDocketId = schedule.OpenDocketId,
                WorkOpportunities = new List<WorkOpportunity>(),
                AidOpportunities = new List<AidOpportunity>(),
                AppealOpportunities = new List<AppealOpportunity>(),
                VisibleAgentIds = ResolveVisibleAgents(definition, schedule, bindings),
            };

            bool scheduledAppealWasGated = false;
            for (int i = 0; i < schedule.ActiveOpportunityIds.Count; i++)
            {
                string opportunityId = schedule.ActiveOpportunityIds[i];
                if (!opportunities.TryGetValue(opportunityId,
                    out ScenarioOpportunityDefinition opportunity))
                {
                    throw new InvalidOperationException(
                        $"Schedule entry '{schedule.ScheduleEntryId}' references missing " +
                        $"opportunity '{opportunityId}'.");
                }

                ValidateActivation(schedule, opportunity, cycle);
                if (opportunity.Kind == ScenarioOpportunityKind.Appeal &&
                    !InstitutionalScenarioLookup.CaseIsActive(
                        definition,
                        report,
                        InstitutionalScenarioLookup.Case(definition, opportunity.CaseId),
                        cycle))
                {
                    // Preserve the restrictive input contract even though this
                    // conditional case has not materialised. Otherwise an empty
                    // filtered list would reopen the decision engine's unrestricted
                    // legacy appeal path.
                    scheduledAppealWasGated = true;
                    continue;
                }
                List<string> eligibleAgentIds = ResolveRoles(
                    opportunity.EligibleRoleIds,
                    bindings,
                    $"opportunity '{opportunity.OpportunityId}' eligibility");

                switch (opportunity.Kind)
                {
                    case ScenarioOpportunityKind.Work:
                        input.WorkOpportunities.Add(ToWork(opportunity, eligibleAgentIds));
                        break;
                    case ScenarioOpportunityKind.Aid:
                        input.AidOpportunities.Add(ToAid(opportunity, eligibleAgentIds));
                        break;
                    case ScenarioOpportunityKind.Appeal:
                        input.AppealOpportunities.Add(
                            ToAppeal(opportunity, schedule.OpenDocketId, eligibleAgentIds));
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Opportunity '{opportunity.OpportunityId}' has an unsupported kind.");
                }
            }

            input.RestrictAidToOpportunities = input.AidOpportunities.Count > 0;
            input.RestrictAppealToOpportunities =
                scheduledAppealWasGated || input.AppealOpportunities.Count > 0;
            return input;
        }

        private static Dictionary<string, string> CaptureBindings(
            InstitutionalScenarioDefinition definition,
            IReadOnlyDictionary<string, string> source)
        {
            var roleIds = new HashSet<string>(StringComparer.Ordinal);
            var agentIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.InitialSociety.Agents.Count; i++)
                agentIds.Add(definition.InitialSociety.Agents[i].StableId);

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.ParticipantRoles.Count; i++)
            {
                ScenarioParticipantRoleDefinition role = definition.ParticipantRoles[i];
                roleIds.Add(role.RoleId);
                if (!source.TryGetValue(role.RoleId, out string agentId) ||
                    string.IsNullOrWhiteSpace(agentId))
                {
                    throw new InvalidOperationException(
                        $"Participant role '{role.RoleId}' has no bound agent.");
                }
                if (!agentIds.Contains(agentId))
                {
                    throw new InvalidOperationException(
                        $"Participant role '{role.RoleId}' is bound to unknown agent '{agentId}'.");
                }
                result.Add(role.RoleId, agentId);
            }

            var suppliedRoleIds = new List<string>(source.Keys);
            suppliedRoleIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < suppliedRoleIds.Count; i++)
            {
                string roleId = suppliedRoleIds[i];
                if (!roleIds.Contains(roleId))
                {
                    throw new InvalidOperationException(
                        $"Role binding references undeclared role '{roleId}'.");
                }
            }

            for (int i = 0; i < definition.ParticipantRoles.Count; i++)
            {
                ScenarioParticipantRoleDefinition role = definition.ParticipantRoles[i];
                for (int j = 0; j < role.DistinctFromRoleIds.Count; j++)
                {
                    string otherRoleId = role.DistinctFromRoleIds[j];
                    if (!result.TryGetValue(otherRoleId, out string otherAgentId))
                    {
                        throw new InvalidOperationException(
                            $"Role '{role.RoleId}' references unbound distinct role '{otherRoleId}'.");
                    }
                    if (string.Equals(result[role.RoleId], otherAgentId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Distinct roles '{role.RoleId}' and '{otherRoleId}' cannot bind " +
                            "to the same agent.");
                    }
                }
            }

            return result;
        }

        private static ScenarioCycleScheduleEntry SelectScheduleEntry(
            InstitutionalScenarioDefinition definition,
            long cycle)
        {
            ScenarioCycleScheduleEntry selected = null;
            int matches = 0;
            for (int i = 0; i < definition.CycleSchedule.Count; i++)
            {
                ScenarioCycleScheduleEntry candidate = definition.CycleSchedule[i];
                if (candidate.Cycle != cycle) continue;
                selected = candidate;
                matches++;
            }

            if (matches != 1)
            {
                throw new InvalidOperationException(
                    $"Scenario cycle {cycle} requires exactly one schedule entry; found {matches}.");
            }
            return selected;
        }

        private static Dictionary<string, ScenarioOpportunityDefinition> IndexOpportunities(
            IReadOnlyList<ScenarioOpportunityDefinition> definitions)
        {
            var result = new Dictionary<string, ScenarioOpportunityDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                ScenarioOpportunityDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.OpportunityId) ||
                    !result.TryAdd(definition.OpportunityId, definition))
                {
                    throw new InvalidOperationException(
                        "Scenario opportunities require unique, non-blank identifiers.");
                }
            }
            return result;
        }

        private static List<string> ResolveVisibleAgents(
            InstitutionalScenarioDefinition definition,
            ScenarioCycleScheduleEntry schedule,
            IReadOnlyDictionary<string, string> bindings)
        {
            switch (schedule.Visibility)
            {
                case ScenarioVisibilityMode.AllBoundRoles:
                {
                    var roleIds = new List<string>(definition.ParticipantRoles.Count);
                    for (int i = 0; i < definition.ParticipantRoles.Count; i++)
                        roleIds.Add(definition.ParticipantRoles[i].RoleId);
                    return ResolveRoles(roleIds, bindings, "all-bound-role visibility");
                }
                case ScenarioVisibilityMode.ListedRoles:
                    return ResolveRoles(
                        schedule.VisibleRoleIds,
                        bindings,
                        $"schedule entry '{schedule.ScheduleEntryId}' visibility");
                case ScenarioVisibilityMode.NoBoundRoles:
                    return new List<string>();
                default:
                    throw new InvalidOperationException(
                        $"Schedule entry '{schedule.ScheduleEntryId}' has an unsupported visibility mode.");
            }
        }

        private static List<string> ResolveRoles(
            IReadOnlyList<string> roleIds,
            IReadOnlyDictionary<string, string> bindings,
            string context)
        {
            if (roleIds == null)
                throw new InvalidOperationException($"{context} requires a role collection.");

            var result = new List<string>(roleIds.Count);
            var seenAgents = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < roleIds.Count; i++)
            {
                string roleId = roleIds[i];
                if (string.IsNullOrWhiteSpace(roleId) ||
                    !bindings.TryGetValue(roleId, out string agentId))
                {
                    throw new InvalidOperationException(
                        $"{context} references missing or unbound role '{roleId}'.");
                }
                if (seenAgents.Add(agentId)) result.Add(agentId);
            }
            return result;
        }

        private static void ValidateActivation(
            ScenarioCycleScheduleEntry schedule,
            ScenarioOpportunityDefinition opportunity,
            long cycle)
        {
            if (cycle < opportunity.AvailabilityStartCycle ||
                cycle > opportunity.AvailabilityEndCycle)
            {
                throw new InvalidOperationException(
                    $"Opportunity '{opportunity.OpportunityId}' is active outside its " +
                    "declared availability window.");
            }

            bool windowOpen = opportunity.Kind switch
            {
                ScenarioOpportunityKind.Work => schedule.WorkAvailable,
                ScenarioOpportunityKind.Aid => schedule.AidAvailable,
                ScenarioOpportunityKind.Appeal => schedule.AppealWindowOpen,
                _ => false,
            };
            if (!windowOpen)
            {
                throw new InvalidOperationException(
                    $"Opportunity '{opportunity.OpportunityId}' kind does not match an " +
                    "open schedule window.");
            }
            if (opportunity.Kind == ScenarioOpportunityKind.Appeal &&
                string.IsNullOrWhiteSpace(schedule.OpenDocketId))
            {
                throw new InvalidOperationException(
                    $"Appeal opportunity '{opportunity.OpportunityId}' requires an open docket.");
            }
        }

        private static WorkOpportunity ToWork(
            ScenarioOpportunityDefinition source,
            List<string> eligibleAgentIds)
        {
            return new WorkOpportunity
            {
                OpportunityId = source.OpportunityId,
                PurposeId = source.PurposeId,
                SourceCauseId = source.SourceCauseId,
                RequiredEmployerId = source.RequiredEmployerId,
                RequiredOfficialStatusId = source.RequiredOfficialStatusId,
                RequiredOfficialStatusRecognised =
                    source.RequiredOfficialStatusRecognised,
                EarliestCycle = source.AvailabilityStartCycle,
                UtilityBonus = source.UtilityBonus,
                ParticipantAgentIds = new List<string>(eligibleAgentIds),
            };
        }

        private static AidOpportunity ToAid(
            ScenarioOpportunityDefinition source,
            List<string> eligibleAgentIds)
        {
            if (!string.IsNullOrEmpty(source.RequiredEmployerId))
            {
                throw new InvalidOperationException(
                    $"Aid opportunity '{source.OpportunityId}' cannot express a required employer.");
            }
            return new AidOpportunity
            {
                OpportunityId = source.OpportunityId,
                PurposeId = source.PurposeId,
                SourceCauseId = source.SourceCauseId,
                RequiredOfficialStatusId = source.RequiredOfficialStatusId,
                RequiredOfficialStatusRecognised =
                    source.RequiredOfficialStatusRecognised,
                UtilityBonus = source.UtilityBonus,
                EligibleAgentIds = new List<string>(eligibleAgentIds),
            };
        }

        private static AppealOpportunity ToAppeal(
            ScenarioOpportunityDefinition source,
            string docketId,
            List<string> eligibleAgentIds)
        {
            if (!string.IsNullOrEmpty(source.RequiredEmployerId) ||
                !string.IsNullOrEmpty(source.RequiredOfficialStatusId))
            {
                throw new InvalidOperationException(
                    $"Appeal opportunity '{source.OpportunityId}' declares restrictions " +
                    "that the appeal input model cannot express.");
            }
            return new AppealOpportunity
            {
                OpportunityId = source.OpportunityId,
                DocketId = docketId,
                CaseId = source.CaseId,
                ChallengedRulingId = source.ChallengedRulingId,
                SourceCauseId = source.SourceCauseId,
                HearingCycle = source.HearingCycle,
                UtilityBonus = source.UtilityBonus,
                PartyAgentIds = new List<string>(eligibleAgentIds),
            };
        }
    }
}
