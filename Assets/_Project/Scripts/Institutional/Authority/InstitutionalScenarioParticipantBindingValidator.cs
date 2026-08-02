using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Validates the participant-binding boundary and every operation's role
    /// references. Asymmetric distinct declarations are deliberately accepted: the
    /// binder treats either direction as a final uniqueness constraint.
    /// </summary>
    internal static class InstitutionalScenarioParticipantBindingValidator
    {
        internal static void Validate(InstitutionalScenarioDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.SchemaVersion != InstitutionalScenarioDefinition.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported scenario schema version {definition.SchemaVersion}.");
            }
            ValidateStableId(definition.ScenarioId, "scenario id");
            SocietyStateValidator.Validate(definition.InitialSociety);

            if (definition.ParticipantRoles == null || definition.ParticipantRoles.Count == 0 ||
                definition.ParticipantRoles.Count > InstitutionalScenarioDefinitionValidator.MaximumRoles)
            {
                throw new InvalidOperationException("Scenario requires a bounded participant role collection.");
            }

            var agentIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.InitialSociety.Agents.Count; i++)
                agentIds.Add(definition.InitialSociety.Agents[i].StableId);

            var roles = new Dictionary<string, ScenarioParticipantRoleDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < definition.ParticipantRoles.Count; i++)
            {
                ScenarioParticipantRoleDefinition role = definition.ParticipantRoles[i] ??
                    throw new InvalidOperationException("Participant roles cannot contain null entries.");
                ValidateStableId(role.RoleId, "participant role id");
                if (agentIds.Contains(role.RoleId))
                    throw new InvalidOperationException($"Role '{role.RoleId}' is a direct agent id.");
                if (!roles.TryAdd(role.RoleId, role))
                    throw new InvalidOperationException($"Duplicate participant role id '{role.RoleId}'.");
                ValidateQuery(role.RoleId, role.Query);
                ValidateIdCollection(role.DistinctFromRoleIds, $"role '{role.RoleId}' distinct references");
            }

            foreach (KeyValuePair<string, ScenarioParticipantRoleDefinition> pair in roles)
            {
                for (int i = 0; i < pair.Value.DistinctFromRoleIds.Count; i++)
                {
                    string otherRoleId = pair.Value.DistinctFromRoleIds[i];
                    if (string.Equals(pair.Key, otherRoleId, StringComparison.Ordinal))
                        throw new InvalidOperationException("A participant role cannot be distinct from itself.");
                    if (agentIds.Contains(otherRoleId))
                    {
                        throw new InvalidOperationException(
                            $"Distinct-role constraint uses forbidden direct agent id '{otherRoleId}'.");
                    }
                    if (!roles.ContainsKey(otherRoleId))
                    {
                        throw new InvalidOperationException(
                            $"Role '{pair.Key}' references missing distinct role '{otherRoleId}'.");
                    }
                }
            }

            RejectDirectAgentIdsInOperations(definition, roles, agentIds);
        }

        private static void ValidateQuery(string roleId, ScenarioParticipantQuery query)
        {
            if (query == null)
                throw new InvalidOperationException($"Role '{roleId}' requires a participant query.");

            ValidateOptionalStableId(query.RequiredSpeciesId, $"role '{roleId}' species predicate");
            ValidateOptionalStableId(query.RequiredEmployerId, $"role '{roleId}' employer predicate");
            ValidateIdCollection(query.RequiredRecognisedStatusIds, "recognised status predicates");
            ValidateIdCollection(query.RequiredUnrecognisedStatusIds, "unrecognised status predicates");
            ValidateIdCollection(query.RequiredAnomalyTraitIds, "anomaly predicates");
            ValidateIdCollection(query.RequiredCommitmentKinds, "commitment predicates");

            for (int i = 0; i < query.RequiredRecognisedStatusIds.Count; i++)
            {
                if (ContainsOrdinal(
                        query.RequiredUnrecognisedStatusIds,
                        query.RequiredRecognisedStatusIds[i]))
                {
                    throw new InvalidOperationException(
                        $"Role '{roleId}' requires a status in both recognised states.");
                }
            }

            bool hasPredicate = !string.IsNullOrEmpty(query.RequiredSpeciesId) ||
                                !string.IsNullOrEmpty(query.RequiredEmployerId) ||
                                query.RequiredRecognisedStatusIds.Count > 0 ||
                                query.RequiredUnrecognisedStatusIds.Count > 0 ||
                                query.RequiredAnomalyTraitIds.Count > 0 ||
                                query.RequiredCommitmentKinds.Count > 0;
            if (!hasPredicate)
                throw new InvalidOperationException($"Role '{roleId}' requires a semantic predicate.");
        }

        private static void RejectDirectAgentIdsInOperations(
            InstitutionalScenarioDefinition definition,
            IReadOnlyDictionary<string, ScenarioParticipantRoleDefinition> roles,
            HashSet<string> agentIds)
        {
            CheckRoleReferences(definition.LivedIncidentSeeds, item => new[] { item.SubjectRoleId },
                "lived incident", roles, agentIds);
            CheckRoleReferences(definition.InitialEconomicAccounts, item => new[] { item.OwnerRoleId },
                "economic account", roles, agentIds);
            CheckRoleReferences(definition.Alternatives, item => new[] { item.OwnerRoleId },
                "alternative", roles, agentIds);
            CheckRoleReferences(definition.Opportunities, item => item.EligibleRoleIds,
                "opportunity", roles, agentIds);
            CheckRoleReferences(definition.CycleSchedule, item => item.VisibleRoleIds,
                "cycle schedule", roles, agentIds);
            CheckRoleReferences(definition.Cases,
                item => new[] { item.ClaimantRoleId, item.RespondentRoleId },
                "case", roles, agentIds);
            CheckRoleReferences(definition.OfficialStatusEffectRequests,
                item => new[] { item.TargetRoleId }, "official status effect", roles, agentIds);
            CheckRoleReferences(definition.RelianceDefinitions,
                item => RelianceRoles(item), "reliance", roles, agentIds);
            CheckRoleReferences(definition.RelianceRecoveries,
                item => new[] { item.ClaimantRoleId, item.RespondentRoleId },
                "reliance recovery", roles, agentIds);
            CheckRoleReferences(definition.Appeals, item => new[] { item.AppellantRoleId },
                "appeal", roles, agentIds);
            CheckRoleReferences(definition.DescendantCases,
                item => Combine(item.TriggerRoleId, item.ConnectedRoleIds),
                "descendant case", roles, agentIds);
            CheckRoleReferences(definition.ExclusiveEntitlements,
                item => new[] { item.InitialHolderRoleId }, "exclusive entitlement", roles, agentIds);
            CheckRoleReferences(definition.EntitlementTransfers,
                item => new[] { item.FromRoleId, item.ToRoleId },
                "entitlement transfer", roles, agentIds);
        }

        private static void CheckRoleReferences<T>(
            IReadOnlyList<T> declarations,
            Func<T, IReadOnlyList<string>> selectRoleIds,
            string context,
            IReadOnlyDictionary<string, ScenarioParticipantRoleDefinition> roles,
            HashSet<string> agentIds)
            where T : class
        {
            if (declarations == null)
                throw new InvalidOperationException($"Scenario requires a {context} collection.");
            for (int i = 0; i < declarations.Count; i++)
            {
                T declaration = declarations[i] ??
                    throw new InvalidOperationException($"{context} collection contains a null entry.");
                IReadOnlyList<string> roleIds = selectRoleIds(declaration) ??
                    throw new InvalidOperationException($"{context} requires a role reference collection.");
                for (int j = 0; j < roleIds.Count; j++)
                {
                    string roleId = roleIds[j];
                    ValidateStableId(roleId, $"{context} role reference");
                    if (agentIds.Contains(roleId))
                    {
                        throw new InvalidOperationException(
                            $"{context} uses forbidden direct agent id '{roleId}'.");
                    }
                    if (!roles.ContainsKey(roleId))
                    {
                        throw new InvalidOperationException(
                            $"{context} references missing role '{roleId}'.");
                    }
                }
            }
        }

        private static IReadOnlyList<string> Combine(
            string first,
            IReadOnlyList<string> remaining)
        {
            if (remaining == null) return null;
            var result = new List<string>(remaining.Count + 1) { first };
            for (int i = 0; i < remaining.Count; i++) result.Add(remaining[i]);
            return result;
        }

        private static IReadOnlyList<string> RelianceRoles(
            ScenarioIrreversibleRelianceDefinition declaration)
        {
            var result = new List<string>
            {
                declaration.RelyingRoleId,
                declaration.BeneficiaryRoleId,
            };
            if (!string.IsNullOrEmpty(declaration.RelatedRoleId))
                result.Add(declaration.RelatedRoleId);
            return result;
        }

        private static void ValidateIdCollection(IReadOnlyList<string> ids, string context)
        {
            if (ids == null) throw new InvalidOperationException($"{context} requires a collection.");
            if (ids.Count > InstitutionalScenarioDefinitionValidator.MaximumReferencesPerDefinition)
                throw new InvalidOperationException($"{context} exceeds the bounded reference count.");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++)
            {
                ValidateStableId(ids[i], context);
                if (!unique.Add(ids[i]))
                    throw new InvalidOperationException($"{context} contains duplicate id '{ids[i]}'.");
            }
        }

        private static void ValidateOptionalStableId(string value, string context)
        {
            if (!string.IsNullOrEmpty(value)) ValidateStableId(value, context);
        }

        private static void ValidateStableId(string value, string context)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > InstitutionalScenarioDefinitionValidator.MaximumIdLength ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{context} requires a bounded, non-blank stable id.");
            }
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i]))
                    throw new InvalidOperationException($"{context} cannot contain control characters.");
            }
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
