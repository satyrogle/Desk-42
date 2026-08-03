using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Structural boundary for authored scenario data. Validation checks identity,
    /// ordering, bounds and references; it does not score evidence or execute effects.
    /// </summary>
    public static class InstitutionalScenarioDefinitionValidator
    {
        public const int MaximumIdLength = 128;
        public const int MaximumRoles = 32;
        public const int MaximumLivedIncidentSeeds = 128;
        public const int MaximumEconomicAccounts = 64;
        public const int MaximumAlternatives = 128;
        public const int MaximumOpportunities = 128;
        public const int MaximumScheduleEntries = 512;
        public const int MaximumEvidenceTemplates = 256;
        public const int MaximumCases = 64;
        public const int MaximumEvidenceActivatedCases = 64;
        public const int MaximumStatusEffects = 256;
        public const int MaximumRelianceDefinitions = 128;
        public const int MaximumRelianceRecoveries = 128;
        public const int MaximumAppeals = 64;
        public const int MaximumHoldings = 64;
        public const int MaximumHoldingCitations = 128;
        public const int MaximumDescendantCases = 64;
        public const int MaximumEntitlements = 64;
        public const int MaximumTransfers = 128;
        public const int MaximumReferencesPerDefinition = 32;
        public const long MaximumCycle = 1_000_000;

        public static void Validate(InstitutionalScenarioDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.SchemaVersion != InstitutionalScenarioDefinition.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported scenario schema version {definition.SchemaVersion}.");
            }

            ValidateStableId(definition.ScenarioId, "scenario id");
            ValidateStableId(definition.IncidentId, "incident id");
            ValidateStableId(definition.PrimaryCaseId, "primary case id");
            ValidateCycleRange(definition.StartCycle, definition.EndCycle, "scenario");

            SocietyStateValidator.Validate(definition.InitialSociety);
            if (definition.InitialSociety.CurrentTick != definition.StartCycle)
            {
                throw new InvalidOperationException(
                    "Initial society tick must equal the scenario start cycle.");
            }
            ValidateInitialAgentOrder(definition.InitialSociety.Agents);

            RequireCollection(definition.ParticipantRoles, 1, MaximumRoles, "participant roles");
            RequireCollection(definition.LivedIncidentSeeds, 0, MaximumLivedIncidentSeeds,
                "lived incident seeds");
            RequireCollection(definition.InitialEconomicAccounts, 0, MaximumEconomicAccounts,
                "initial economic accounts");
            RequireCollection(definition.Alternatives, 0, MaximumAlternatives, "alternatives");
            RequireCollection(definition.Opportunities, 1, MaximumOpportunities, "opportunities");
            RequireCollection(definition.CycleSchedule, 1, MaximumScheduleEntries, "cycle schedule");
            RequireCollection(definition.EvidenceTemplates, 1, MaximumEvidenceTemplates, "evidence templates");
            RequireCollection(definition.Cases, 1, MaximumCases, "cases");
            RequireCollection(definition.EvidenceActivatedCases, 0,
                MaximumEvidenceActivatedCases, "evidence-activated cases");
            RequireCollection(definition.OfficialStatusEffectRequests, 0, MaximumStatusEffects,
                "official-status effect requests");
            RequireCollection(definition.RelianceDefinitions, 0, MaximumRelianceDefinitions,
                "reliance definitions");
            RequireCollection(definition.RelianceRecoveries, 0, MaximumRelianceRecoveries,
                "reliance recoveries");
            RequireCollection(definition.Appeals, 0, MaximumAppeals, "appeals");
            RequireCollection(definition.Holdings, 0, MaximumHoldings, "holdings");
            RequireCollection(definition.HoldingCitations, 0,
                MaximumHoldingCitations, "holding citations");
            RequireCollection(definition.DescendantCases, 0, MaximumDescendantCases,
                "descendant cases");
            RequireCollection(definition.ExclusiveEntitlements, 0, MaximumEntitlements,
                "exclusive entitlements");
            RequireCollection(definition.EntitlementTransfers, 0, MaximumTransfers,
                "entitlement transfers");

            var agentIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.InitialSociety.Agents.Count; i++)
                agentIds.Add(definition.InitialSociety.Agents[i].StableId);

            Dictionary<string, ScenarioParticipantRoleDefinition> roles =
                ValidateRoles(definition.ParticipantRoles, definition.InitialSociety, agentIds);
            ValidateLivedIncidentSeeds(definition, roles, agentIds);
            ValidateInitialEconomicAccounts(definition, roles, agentIds);
            Dictionary<string, ScenarioAlternativeDefinition> alternatives =
                ValidateAlternatives(definition, roles, agentIds);
            Dictionary<string, ScenarioCaseDefinition> cases =
                ValidateCases(definition, roles, agentIds);
            if (!cases.ContainsKey(definition.PrimaryCaseId))
                throw new InvalidOperationException("Primary case id does not reference a declared case.");

            var rulingToCase = BuildRulingIndex(definition.Cases);
            Dictionary<string, ScenarioOpportunityDefinition> opportunities =
                ValidateOpportunities(definition, roles, cases, rulingToCase, agentIds);
            HashSet<string> activeOpportunityCycles =
                ValidateSchedule(definition, roles, opportunities, agentIds);
            Dictionary<string, ScenarioEvidenceTemplateDefinition> evidence =
                ValidateEvidenceTemplates(definition, opportunities, cases);
            HashSet<string> evidenceActivatedCaseIds =
                ValidateEvidenceActivatedCases(
                    definition,
                    cases,
                    evidence,
                    activeOpportunityCycles);
            Dictionary<string, ScenarioOfficialStatusEffectRequest> effects =
                ValidateStatusEffects(definition, roles, cases, rulingToCase, agentIds);
            Dictionary<string, ScenarioIrreversibleRelianceDefinition> reliance =
                ValidateRelianceDefinitions(
                definition, roles, opportunities, effects, rulingToCase,
                alternatives, activeOpportunityCycles, agentIds);
            Dictionary<string, ScenarioAppealDefinition> appeals =
                ValidateAppeals(
                    definition, roles, opportunities, cases, rulingToCase,
                    evidence, activeOpportunityCycles, agentIds);
            ValidateRelianceRecoveries(
                definition, roles, reliance, appeals, cases, rulingToCase, agentIds);
            Dictionary<string, ScenarioHoldingDefinition> holdings =
                ValidateHoldings(
                    definition, appeals, cases, rulingToCase, evidence, agentIds);
            ValidateAppealHoldingLinks(appeals, holdings);
            ValidateHoldingCitations(
                definition,
                cases,
                rulingToCase,
                holdings,
                appeals);
            ValidateDescendantCases(
                definition, roles, opportunities, cases, rulingToCase,
                activeOpportunityCycles, evidenceActivatedCaseIds, agentIds);
            Dictionary<string, ScenarioExclusiveEntitlementDefinition> entitlements =
                ValidateEntitlements(definition, roles, agentIds);
            ValidateTransfers(
                definition, roles, cases, rulingToCase, holdings, entitlements,
                agentIds);
        }

        private static Dictionary<string, ScenarioParticipantRoleDefinition> ValidateRoles(
            List<ScenarioParticipantRoleDefinition> definitions,
            SocietyState society,
            HashSet<string> agentIds)
        {
            var result = new Dictionary<string, ScenarioParticipantRoleDefinition>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definitions.Count; i++)
            {
                ScenarioParticipantRoleDefinition role = definitions[i] ??
                    throw new InvalidOperationException("Participant roles cannot contain null entries.");
                ValidateStableId(role.RoleId, "participant role id");
                ValidateStrictObjectOrder(previousId, role.RoleId, "participant roles");
                previousId = role.RoleId;
                if (agentIds.Contains(role.RoleId))
                    throw new InvalidOperationException($"Role '{role.RoleId}' is a direct agent id.");
                if (!result.TryAdd(role.RoleId, role))
                    throw new InvalidOperationException($"Duplicate participant role id '{role.RoleId}'.");
                ValidateParticipantQuery(role.RoleId, role.Query);
                ValidateOrderedIds(role.DistinctFromRoleIds, "distinct role references", false);
                if (!HasQueryCandidate(role.Query, society.Agents))
                    throw new InvalidOperationException($"Role '{role.RoleId}' has no semantic query candidate.");
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                ScenarioParticipantRoleDefinition role = definitions[i];
                for (int j = 0; j < role.DistinctFromRoleIds.Count; j++)
                {
                    string otherId = role.DistinctFromRoleIds[j];
                    RequireRoleReference(otherId, $"role '{role.RoleId}' distinct binding", result, agentIds);
                    if (string.Equals(role.RoleId, otherId, StringComparison.Ordinal))
                        throw new InvalidOperationException("A role cannot require itself to be distinct.");
                    if (!result[otherId].DistinctFromRoleIds.Contains(role.RoleId))
                    {
                        throw new InvalidOperationException(
                            $"Distinct binding between '{role.RoleId}' and '{otherId}' must be symmetric.");
                    }
                }
            }

            return result;
        }

        private static void ValidateLivedIncidentSeeds(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            HashSet<string> agentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.LivedIncidentSeeds.Count; i++)
            {
                ScenarioLivedIncidentSeedDefinition item = definition.LivedIncidentSeeds[i] ??
                    throw new InvalidOperationException("Lived incident seeds cannot contain null entries.");
                ValidateStableId(item.IncidentSeedId, "lived incident seed id");
                ValidateStrictObjectOrder(previousId, item.IncidentSeedId, "lived incident seeds");
                previousId = item.IncidentSeedId;
                if (!ids.Add(item.IncidentSeedId))
                    throw new InvalidOperationException("Duplicate lived incident seed id.");
                if (!string.Equals(item.IncidentId, definition.IncidentId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Lived incident seed references an unknown incident.");
                ValidateCycle(item.Cycle, definition, "lived incident seed cycle");
                RequireRoleReference(item.SubjectRoleId, "lived incident subject", roles, agentIds);
                ValidateStableId(item.CauseEntityId, "lived incident cause entity id");
                RejectDirectAgentId(item.CauseEntityId, "lived incident cause entity", agentIds);
                ValidateStableId(item.PropositionId, "lived incident proposition id");
                if (!Enum.IsDefined(typeof(NeedKind), item.AffectedNeed))
                    throw new InvalidOperationException("Lived incident seed has an invalid affected need.");
                ValidateRange(item.NeedPressureDelta, -100, 100, "lived incident need delta");
                if (item.NeedPressureDelta == 0)
                    throw new InvalidOperationException("Lived incident need delta must be non-zero.");
            }
        }

        private static void ValidateInitialEconomicAccounts(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            HashSet<string> agentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var owners = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.InitialEconomicAccounts.Count; i++)
            {
                ScenarioInitialEconomicAccountDefinition item = definition.InitialEconomicAccounts[i] ??
                    throw new InvalidOperationException("Initial economic accounts cannot contain null entries.");
                ValidateStableId(item.AccountId, "initial economic account id");
                ValidateStrictObjectOrder(previousId, item.AccountId, "initial economic accounts");
                previousId = item.AccountId;
                if (!ids.Add(item.AccountId))
                    throw new InvalidOperationException("Duplicate initial economic account id.");
                RequireRoleReference(item.OwnerRoleId, "economic account owner", roles, agentIds);
                if (!owners.Add(item.OwnerRoleId))
                    throw new InvalidOperationException("A role may own only one initial economic account.");
                ValidateRange(item.InitialCredits, -1_000_000, 1_000_000, "initial account credits");
                ValidateRange(item.CycleIncome, -1_000_000, 1_000_000, "account cycle income");
            }
        }

        private static Dictionary<string, ScenarioAlternativeDefinition> ValidateAlternatives(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            HashSet<string> agentIds)
        {
            var result = new Dictionary<string, ScenarioAlternativeDefinition>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.Alternatives.Count; i++)
            {
                ScenarioAlternativeDefinition item = definition.Alternatives[i] ??
                    throw new InvalidOperationException("Alternatives cannot contain null entries.");
                ValidateStableId(item.AlternativeKey, "alternative key");
                ValidateStrictObjectOrder(previousId, item.AlternativeKey, "alternatives");
                previousId = item.AlternativeKey;
                if (!result.TryAdd(item.AlternativeKey, item))
                    throw new InvalidOperationException("Duplicate alternative key.");
                RequireRoleReference(item.OwnerRoleId, "alternative owner", roles, agentIds);
                ValidateRange(item.ResourceValue, -1_000_000, 1_000_000, "alternative resource value");
            }
            return result;
        }

        private static void ValidateParticipantQuery(string roleId, ScenarioParticipantQuery query)
        {
            if (query == null)
                throw new InvalidOperationException($"Role '{roleId}' requires a participant query.");

            ValidateOptionalStableId(query.RequiredSpeciesId, $"role '{roleId}' species predicate");
            ValidateOptionalStableId(query.RequiredEmployerId, $"role '{roleId}' employer predicate");
            ValidateOrderedIds(query.RequiredRecognisedStatusIds, "recognised status predicates", false);
            ValidateOrderedIds(query.RequiredUnrecognisedStatusIds, "unrecognised status predicates", false);
            ValidateOrderedIds(query.RequiredAnomalyTraitIds, "anomaly predicates", false);
            ValidateOrderedIds(query.RequiredCommitmentKinds, "commitment predicates", false);

            for (int i = 0; i < query.RequiredRecognisedStatusIds.Count; i++)
            {
                if (query.RequiredUnrecognisedStatusIds.Contains(query.RequiredRecognisedStatusIds[i]))
                    throw new InvalidOperationException($"Role '{roleId}' requires a status in both states.");
            }

            bool hasPredicate = !string.IsNullOrEmpty(query.RequiredSpeciesId) ||
                                !string.IsNullOrEmpty(query.RequiredEmployerId) ||
                                query.RequiredRecognisedStatusIds.Count > 0 ||
                                query.RequiredUnrecognisedStatusIds.Count > 0 ||
                                query.RequiredAnomalyTraitIds.Count > 0 ||
                                query.RequiredCommitmentKinds.Count > 0;
            if (!hasPredicate)
                throw new InvalidOperationException($"Role '{roleId}' requires at least one semantic predicate.");
        }

        private static bool HasQueryCandidate(ScenarioParticipantQuery query, List<AgentState> agents)
        {
            for (int i = 0; i < agents.Count; i++)
            {
                if (MatchesQuery(query, agents[i])) return true;
            }
            return false;
        }

        private static bool MatchesQuery(ScenarioParticipantQuery query, AgentState agent)
        {
            if (!string.IsNullOrEmpty(query.RequiredSpeciesId) &&
                !string.Equals(query.RequiredSpeciesId, agent.SpeciesId, StringComparison.Ordinal)) return false;
            if (!string.IsNullOrEmpty(query.RequiredEmployerId) &&
                !string.Equals(query.RequiredEmployerId, agent.EmployerId, StringComparison.Ordinal)) return false;

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
                bool found = false;
                for (int j = 0; j < agent.AnomalyRules.Count; j++)
                    found |= string.Equals(agent.AnomalyRules[j].TraitId,
                        query.RequiredAnomalyTraitIds[i], StringComparison.Ordinal);
                if (!found) return false;
            }
            for (int i = 0; i < query.RequiredCommitmentKinds.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < agent.Commitments.Count; j++)
                    found |= string.Equals(agent.Commitments[j].Kind,
                        query.RequiredCommitmentKinds[i], StringComparison.Ordinal);
                if (!found) return false;
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

        private static Dictionary<string, ScenarioCaseDefinition> ValidateCases(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            HashSet<string> agentIds)
        {
            var result = new Dictionary<string, ScenarioCaseDefinition>(StringComparer.Ordinal);
            var rulingIds = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.Cases.Count; i++)
            {
                ScenarioCaseDefinition item = definition.Cases[i] ??
                    throw new InvalidOperationException("Cases cannot contain null entries.");
                ValidateStableId(item.CaseId, "case id");
                ValidateStrictObjectOrder(previousId, item.CaseId, "cases");
                previousId = item.CaseId;
                if (!result.TryAdd(item.CaseId, item))
                    throw new InvalidOperationException($"Duplicate case id '{item.CaseId}'.");
                ValidateStableId(item.IssueId, $"case '{item.CaseId}' issue id");
                RequireRoleReference(item.ClaimantRoleId, $"case '{item.CaseId}' claimant", roles, agentIds);
                RequireRoleReference(item.RespondentRoleId, $"case '{item.CaseId}' respondent", roles, agentIds);
                if (string.Equals(item.ClaimantRoleId, item.RespondentRoleId, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Case '{item.CaseId}' requires distinct parties.");
                RequireRolesDeclaredDistinct(
                    item.ClaimantRoleId,
                    item.RespondentRoleId,
                    roles,
                    $"case '{item.CaseId}' parties");
                ValidateFacts(item.Facts, $"case '{item.CaseId}' facts", true);
                RejectDirectAgentIdsInFacts(
                    item.Facts, $"case '{item.CaseId}' facts", agentIds);
                ValidateCycle(item.OpenCycle, definition, $"case '{item.CaseId}' open cycle");
                ValidateCycle(
                    item.InitialEvidenceCutoffCycle,
                    definition,
                    $"case '{item.CaseId}' initial evidence cutoff");
                ValidateCycle(item.InitialRulingCycle, definition, $"case '{item.CaseId}' initial ruling cycle");
                ValidateCycle(
                    item.AdjudicationEvidenceCutoffCycle,
                    definition,
                    $"case '{item.CaseId}' adjudication evidence cutoff");
                ValidateCycle(item.AdjudicationCycle, definition, $"case '{item.CaseId}' adjudication cycle");
                if (item.OpenCycle > item.InitialEvidenceCutoffCycle ||
                    item.InitialEvidenceCutoffCycle > item.InitialRulingCycle ||
                    item.InitialRulingCycle > item.AdjudicationEvidenceCutoffCycle ||
                    item.AdjudicationEvidenceCutoffCycle > item.AdjudicationCycle)
                {
                    throw new InvalidOperationException($"Case '{item.CaseId}' has invalid cycle ordering.");
                }
                ValidateStableId(item.InitialPhaseId, $"case '{item.CaseId}' initial phase id");
                ValidateStableId(item.AdjudicationPhaseId,
                    $"case '{item.CaseId}' adjudication phase id");
                ValidateStableId(item.InitialRulingId, $"case '{item.CaseId}' initial ruling id");
                ValidateStableId(item.AdjudicationRulingId, $"case '{item.CaseId}' adjudication ruling id");
                string expectedInitialRulingId =
                    $"ruling:{item.CaseId}:{item.InitialPhaseId}:{item.InitialRulingCycle}";
                string expectedAdjudicationRulingId =
                    $"ruling:{item.CaseId}:{item.AdjudicationPhaseId}:{item.AdjudicationCycle}";
                if (!string.Equals(item.InitialRulingId, expectedInitialRulingId,
                        StringComparison.Ordinal) ||
                    !string.Equals(item.AdjudicationRulingId,
                        expectedAdjudicationRulingId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Case '{item.CaseId}' ruling ids must match the generic " +
                        "adjudication id convention.");
                }
                if (!rulingIds.Add(item.InitialRulingId) || !rulingIds.Add(item.AdjudicationRulingId))
                    throw new InvalidOperationException("Ruling ids must be unique across all cases.");
                ValidateRange(item.InitialScoreThreshold, 1, 10_000,
                    $"case '{item.CaseId}' initial score threshold");
                ValidateRange(item.ProvisionalScoreThreshold, 1, 10_000,
                    $"case '{item.CaseId}' provisional score threshold");
                if (item.ProvisionalScoreThreshold > item.InitialScoreThreshold)
                {
                    throw new InvalidOperationException(
                        $"Case '{item.CaseId}' provisional threshold cannot exceed its initial threshold.");
                }
                ValidateRange(item.AdjudicationScoreThreshold, 1, 10_000,
                    $"case '{item.CaseId}' adjudication score threshold");
            }
            return result;
        }

        private static Dictionary<string, string> BuildRulingIndex(List<ScenarioCaseDefinition> cases)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < cases.Count; i++)
            {
                result.Add(cases[i].InitialRulingId, cases[i].CaseId);
                result.Add(cases[i].AdjudicationRulingId, cases[i].CaseId);
            }
            return result;
        }

        private static Dictionary<string, ScenarioOpportunityDefinition> ValidateOpportunities(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            Dictionary<string, ScenarioCaseDefinition> cases,
            Dictionary<string, string> rulingToCase,
            HashSet<string> agentIds)
        {
            var result = new Dictionary<string, ScenarioOpportunityDefinition>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.Opportunities.Count; i++)
            {
                ScenarioOpportunityDefinition item = definition.Opportunities[i] ??
                    throw new InvalidOperationException("Opportunities cannot contain null entries.");
                ValidateStableId(item.OpportunityId, "opportunity id");
                ValidateStrictObjectOrder(previousId, item.OpportunityId, "opportunities");
                previousId = item.OpportunityId;
                if (!result.TryAdd(item.OpportunityId, item))
                    throw new InvalidOperationException($"Duplicate opportunity id '{item.OpportunityId}'.");
                if (!Enum.IsDefined(typeof(ScenarioOpportunityKind), item.Kind))
                    throw new InvalidOperationException($"Opportunity '{item.OpportunityId}' has an invalid kind.");
                ValidateStableId(item.PurposeId, $"opportunity '{item.OpportunityId}' purpose id");
                ValidateStableId(item.SourceCauseId, $"opportunity '{item.OpportunityId}' source cause id");
                RejectDirectAgentId(item.SourceCauseId, "opportunity source cause", agentIds);
                ValidateCycleRange(item.AvailabilityStartCycle, item.AvailabilityEndCycle,
                    $"opportunity '{item.OpportunityId}' availability");
                ValidateCycle(item.AvailabilityStartCycle, definition, "opportunity start cycle");
                ValidateCycle(item.AvailabilityEndCycle, definition, "opportunity end cycle");
                ValidateRange(item.UtilityBonus, -1_000, 1_000,
                    $"opportunity '{item.OpportunityId}' utility bonus");
                ValidateOptionalStableId(item.RequiredEmployerId, "required employer id");
                ValidateOptionalStableId(item.RequiredOfficialStatusId, "required official status id");
                if (item.Kind == ScenarioOpportunityKind.Aid &&
                    !string.IsNullOrEmpty(item.RequiredEmployerId))
                {
                    throw new InvalidOperationException(
                        "Aid opportunities cannot declare an employer restriction.");
                }
                if (item.Kind == ScenarioOpportunityKind.Appeal &&
                    (!string.IsNullOrEmpty(item.RequiredEmployerId) ||
                     !string.IsNullOrEmpty(item.RequiredOfficialStatusId)))
                {
                    throw new InvalidOperationException(
                        "Appeal opportunities cannot declare employer or status restrictions.");
                }
                ValidateOrderedIds(item.EligibleRoleIds, "opportunity eligible roles", true);
                for (int j = 0; j < item.EligibleRoleIds.Count; j++)
                    RequireRoleReference(item.EligibleRoleIds[j], "opportunity eligible role", roles, agentIds);

                if (item.Kind == ScenarioOpportunityKind.Appeal)
                {
                    ValidateStableId(item.CaseId, "appeal opportunity case id");
                    ValidateStableId(item.ChallengedRulingId, "appeal opportunity challenged ruling id");
                    if (!cases.ContainsKey(item.CaseId))
                        throw new InvalidOperationException("Appeal opportunity references a missing case.");
                    if (!rulingToCase.TryGetValue(item.ChallengedRulingId, out string rulingCaseId) ||
                        !string.Equals(rulingCaseId, item.CaseId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Appeal opportunity ruling does not belong to its case.");
                    }
                    ValidateCycle(item.HearingCycle, definition, "appeal opportunity hearing cycle");
                    if (item.HearingCycle < item.AvailabilityStartCycle)
                        throw new InvalidOperationException("Appeal hearing precedes opportunity availability.");
                }
                else
                {
                    if (!string.IsNullOrEmpty(item.CaseId) ||
                        !string.IsNullOrEmpty(item.ChallengedRulingId) || item.HearingCycle != -1)
                    {
                        throw new InvalidOperationException(
                            "Only appeal opportunities may declare case, challenged ruling or hearing fields.");
                    }
                }
            }
            return result;
        }

        private static HashSet<string> ValidateSchedule(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            Dictionary<string, ScenarioOpportunityDefinition> opportunities,
            HashSet<string> agentIds)
        {
            var entryIds = new HashSet<string>(StringComparer.Ordinal);
            var scheduledCycles = new HashSet<long>();
            var activatedOpportunityIds = new HashSet<string>(StringComparer.Ordinal);
            var activeOpportunityCycles = new HashSet<string>(StringComparer.Ordinal);
            long previousCycle = -1;
            string previousId = null;
            for (int i = 0; i < definition.CycleSchedule.Count; i++)
            {
                ScenarioCycleScheduleEntry item = definition.CycleSchedule[i] ??
                    throw new InvalidOperationException("Cycle schedule cannot contain null entries.");
                ValidateStableId(item.ScheduleEntryId, "schedule entry id");
                if (!entryIds.Add(item.ScheduleEntryId))
                    throw new InvalidOperationException($"Duplicate schedule entry id '{item.ScheduleEntryId}'.");
                if (item.Cycle < previousCycle ||
                    (item.Cycle == previousCycle &&
                     StringComparer.Ordinal.Compare(previousId, item.ScheduleEntryId) >= 0))
                {
                    throw new InvalidOperationException("Cycle schedule must be ordered by cycle then id.");
                }
                previousCycle = item.Cycle;
                previousId = item.ScheduleEntryId;
                if (!scheduledCycles.Add(item.Cycle))
                    throw new InvalidOperationException("Only one schedule entry is allowed per cycle.");
                ValidateCycle(item.Cycle, definition, "schedule cycle");
                if (item.Cycle == definition.StartCycle)
                {
                    throw new InvalidOperationException(
                        "The start cycle is initialization-only and cannot have a decision schedule row.");
                }
                if (!string.Equals(item.IncidentId, definition.IncidentId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Schedule entry references an unknown incident id.");
                if (!Enum.IsDefined(typeof(ScenarioVisibilityMode), item.Visibility))
                    throw new InvalidOperationException("Schedule entry has an invalid visibility mode.");
                ValidateOptionalStableId(item.OpenDocketId, "schedule open docket id");
                if (item.AppealWindowOpen != !string.IsNullOrEmpty(item.OpenDocketId))
                {
                    throw new InvalidOperationException(
                        "Appeal-window schedule state and open docket id must be declared together.");
                }
                ValidateOrderedIds(item.VisibleRoleIds, "visible roles", false);
                ValidateOrderedIds(item.ActiveOpportunityIds, "active opportunities", false);
                if (item.Visibility == ScenarioVisibilityMode.ListedRoles && item.VisibleRoleIds.Count == 0)
                    throw new InvalidOperationException("Listed-role visibility requires at least one role.");
                if (item.Visibility != ScenarioVisibilityMode.ListedRoles && item.VisibleRoleIds.Count != 0)
                    throw new InvalidOperationException("Only listed-role visibility may include role ids.");
                for (int j = 0; j < item.VisibleRoleIds.Count; j++)
                    RequireRoleReference(item.VisibleRoleIds[j], "schedule visibility", roles, agentIds);
                for (int j = 0; j < item.ActiveOpportunityIds.Count; j++)
                {
                    string opportunityId = item.ActiveOpportunityIds[j];
                    if (!opportunities.TryGetValue(opportunityId, out ScenarioOpportunityDefinition opportunity))
                        throw new InvalidOperationException($"Schedule references missing opportunity '{opportunityId}'.");
                    if (item.Cycle < opportunity.AvailabilityStartCycle ||
                        item.Cycle > opportunity.AvailabilityEndCycle)
                    {
                        throw new InvalidOperationException(
                            $"Opportunity '{opportunityId}' is active outside its declared availability.");
                    }
                    if ((opportunity.Kind == ScenarioOpportunityKind.Work && !item.WorkAvailable) ||
                        (opportunity.Kind == ScenarioOpportunityKind.Aid && !item.AidAvailable) ||
                        (opportunity.Kind == ScenarioOpportunityKind.Appeal && !item.AppealWindowOpen))
                    {
                        throw new InvalidOperationException(
                            $"Opportunity '{opportunityId}' is active while its base decision window is closed.");
                    }
                    activatedOpportunityIds.Add(opportunityId);
                    activeOpportunityCycles.Add(OpportunityCycleKey(opportunityId, item.Cycle));
                }
            }
            long expectedScheduleCount = definition.EndCycle - definition.StartCycle;
            if (scheduledCycles.Count != expectedScheduleCount)
            {
                throw new InvalidOperationException(
                    "Cycle schedule must contain exactly one executable row for every cycle " +
                    "after the start cycle through the end cycle.");
            }
            for (long cycle = definition.StartCycle + 1;
                 cycle <= definition.EndCycle;
                 cycle++)
            {
                if (!scheduledCycles.Contains(cycle))
                {
                    throw new InvalidOperationException(
                        $"Cycle schedule is missing executable cycle {cycle}.");
                }
            }
            foreach (string opportunityId in opportunities.Keys)
            {
                if (!activatedOpportunityIds.Contains(opportunityId))
                    throw new InvalidOperationException($"Opportunity '{opportunityId}' is never activated.");
            }
            return activeOpportunityCycles;
        }

        private static Dictionary<string, ScenarioEvidenceTemplateDefinition> ValidateEvidenceTemplates(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioOpportunityDefinition> opportunities,
            Dictionary<string, ScenarioCaseDefinition> cases)
        {
            var result = new Dictionary<string, ScenarioEvidenceTemplateDefinition>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.EvidenceTemplates.Count; i++)
            {
                ScenarioEvidenceTemplateDefinition item = definition.EvidenceTemplates[i] ??
                    throw new InvalidOperationException("Evidence templates cannot contain null entries.");
                ValidateStableId(item.EvidenceTemplateId, "evidence template id");
                ValidateStrictObjectOrder(previousId, item.EvidenceTemplateId, "evidence templates");
                previousId = item.EvidenceTemplateId;
                if (!result.TryAdd(item.EvidenceTemplateId, item))
                    throw new InvalidOperationException($"Duplicate evidence template id '{item.EvidenceTemplateId}'.");
                if (!Enum.IsDefined(typeof(SocietyEventKind), item.SourceEventKind))
                    throw new InvalidOperationException("Evidence template has an invalid source event kind.");
                ValidateOptionalStableId(item.SourceOpportunityId,
                    "evidence source opportunity id");
                ScenarioOpportunityDefinition sourceOpportunity = null;
                if (!string.IsNullOrEmpty(item.SourceOpportunityId) &&
                    !opportunities.TryGetValue(item.SourceOpportunityId,
                        out sourceOpportunity))
                {
                    throw new InvalidOperationException(
                        "Evidence template references a missing opportunity.");
                }
                if (sourceOpportunity != null)
                {
                    SocietyActionKind sourceAction = item.SourceEventKind switch
                    {
                        SocietyEventKind.WorkPerformed => SocietyActionKind.Work,
                        SocietyEventKind.AidRequested => SocietyActionKind.SeekAid,
                        SocietyEventKind.AppealFiled => SocietyActionKind.Appeal,
                        _ => SocietyActionKind.Idle,
                    };
                    if (sourceAction != SocietyActionKind.Idle)
                    {
                        ValidateActionOpportunityCompatibility(
                            sourceAction,
                            sourceOpportunity.Kind,
                            "evidence template");
                    }
                }
                ValidateOptionalStableId(item.RequiredPropositionId,
                    "evidence proposition id");
                if (item.SourceEventKind == SocietyEventKind.EvidenceDisclosed &&
                    string.IsNullOrEmpty(item.RequiredPropositionId))
                {
                    throw new InvalidOperationException(
                        "Disclosure evidence requires a proposition filter.");
                }
                if (item.SourceEventKind != SocietyEventKind.EvidenceDisclosed &&
                    !string.IsNullOrEmpty(item.RequiredPropositionId))
                {
                    throw new InvalidOperationException(
                        "Only disclosure events expose a proposition that an evidence " +
                        "template can filter.");
                }
                if ((item.SourceEventKind == SocietyEventKind.WorkPerformed ||
                     item.SourceEventKind == SocietyEventKind.AidRequested ||
                     item.SourceEventKind == SocietyEventKind.AppealFiled) &&
                    string.IsNullOrEmpty(item.SourceOpportunityId))
                {
                    throw new InvalidOperationException(
                        "Opportunity-backed evidence requires an opportunity filter.");
                }
                if (item.SourceEventKind != SocietyEventKind.WorkPerformed &&
                    item.SourceEventKind != SocietyEventKind.AidRequested &&
                    item.SourceEventKind != SocietyEventKind.AppealFiled &&
                    !string.IsNullOrEmpty(item.SourceOpportunityId))
                {
                    throw new InvalidOperationException(
                        "Only opportunity-backed events expose an opportunity id for " +
                        "evidence-template matching.");
                }
                ValidateStableId(item.CaseId, "evidence case id");
                if (!cases.TryGetValue(item.CaseId, out ScenarioCaseDefinition caseDefinition))
                    throw new InvalidOperationException("Evidence template references a missing case.");
                ValidateStableId(item.IssueId, "evidence issue id");
                if (!string.Equals(item.IssueId, caseDefinition.IssueId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Evidence issue does not match its case issue.");
                ValidateStableId(item.EvidenceClassId, "evidence class id");
                if (!Enum.IsDefined(typeof(EvidenceEffect), item.Effect))
                    throw new InvalidOperationException("Evidence template has an invalid effect.");
                if (!Enum.IsDefined(typeof(EvidenceVisibility), item.Visibility))
                    throw new InvalidOperationException("Evidence template has an invalid visibility.");
                ValidateRange(item.Weight, 1, 1_000, "evidence weight");
            }
            return result;
        }

        private static HashSet<string> ValidateEvidenceActivatedCases(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioCaseDefinition> cases,
            Dictionary<string, ScenarioEvidenceTemplateDefinition> evidence,
            HashSet<string> activeOpportunityCycles)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var activatedCaseIds = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.EvidenceActivatedCases.Count; i++)
            {
                ScenarioEvidenceActivatedCaseDefinition item =
                    definition.EvidenceActivatedCases[i] ??
                    throw new InvalidOperationException(
                        "Evidence-activated cases cannot contain null entries.");
                ValidateStableId(item.ActivationId, "case activation id");
                ValidateStrictObjectOrder(
                    previousId,
                    item.ActivationId,
                    "evidence-activated cases");
                previousId = item.ActivationId;
                if (!ids.Add(item.ActivationId))
                {
                    throw new InvalidOperationException(
                        $"Duplicate case activation id '{item.ActivationId}'.");
                }
                ValidateStableId(item.CaseId, "case activation target case id");
                ValidateStableId(
                    item.EvidenceTemplateId,
                    "case activation evidence template id");
                if (!cases.TryGetValue(item.CaseId, out ScenarioCaseDefinition target) ||
                    !activatedCaseIds.Add(item.CaseId))
                {
                    throw new InvalidOperationException(
                        "Case activation references a missing or duplicate case.");
                }
                if (!evidence.TryGetValue(
                        item.EvidenceTemplateId,
                        out ScenarioEvidenceTemplateDefinition template) ||
                    !string.Equals(template.CaseId, item.CaseId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Case activation evidence must belong to its exact target case.");
                }
                ValidateCycle(item.TriggerCycle, definition, "case activation trigger cycle");
                if (item.TriggerCycle <= definition.StartCycle)
                {
                    throw new InvalidOperationException(
                        "Case activation trigger cycle must follow the " +
                        "initialization-only start cycle.");
                }
                if (item.TriggerCycle > target.OpenCycle)
                {
                    throw new InvalidOperationException(
                        "Case activation trigger cycle cannot follow its case open cycle.");
                }
                bool opportunityBacked =
                    template.SourceEventKind == SocietyEventKind.WorkPerformed ||
                    template.SourceEventKind == SocietyEventKind.AidRequested ||
                    template.SourceEventKind == SocietyEventKind.AppealFiled;
                if (!opportunityBacked ||
                    string.IsNullOrWhiteSpace(template.SourceOpportunityId))
                {
                    throw new InvalidOperationException(
                        "Case activation evidence must originate from one " +
                        "capacity-reserved opportunity action.");
                }
                if (!activeOpportunityCycles.Contains(
                        OpportunityCycleKey(
                            template.SourceOpportunityId,
                            item.TriggerCycle)))
                {
                    throw new InvalidOperationException(
                        "Case activation evidence opportunity is not active on its " +
                        "exact trigger cycle.");
                }
            }
            return activatedCaseIds;
        }

        private static Dictionary<string, ScenarioOfficialStatusEffectRequest> ValidateStatusEffects(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            Dictionary<string, ScenarioCaseDefinition> cases,
            Dictionary<string, string> rulingToCase,
            HashSet<string> agentIds)
        {
            var result = new Dictionary<string, ScenarioOfficialStatusEffectRequest>(StringComparer.Ordinal);
            var effectSignatures = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.OfficialStatusEffectRequests.Count; i++)
            {
                ScenarioOfficialStatusEffectRequest item = definition.OfficialStatusEffectRequests[i] ??
                    throw new InvalidOperationException("Official-status effects cannot contain null entries.");
                ValidateStableId(item.EffectRequestId, "effect request id");
                ValidateStrictObjectOrder(previousId, item.EffectRequestId, "official-status effects");
                previousId = item.EffectRequestId;
                if (!result.TryAdd(item.EffectRequestId, item))
                    throw new InvalidOperationException($"Duplicate effect request id '{item.EffectRequestId}'.");
                ValidateCycle(item.Cycle, definition, "effect request cycle");
                ValidateStableId(item.CauseCaseId, "effect cause case id");
                if (!cases.ContainsKey(item.CauseCaseId))
                    throw new InvalidOperationException("Effect request references a missing cause case.");
                ValidateStableId(item.CauseRulingId, "effect cause ruling id");
                if (!rulingToCase.TryGetValue(item.CauseRulingId, out string causeCaseId) ||
                    !string.Equals(causeCaseId, item.CauseCaseId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Effect request ruling does not belong to its case.");
                }
                if (item.Cycle != RulingCycle(cases[item.CauseCaseId], item.CauseRulingId))
                    throw new InvalidOperationException("Effect request cycle must equal its cause ruling cycle.");
                if (!Enum.IsDefined(typeof(RulingDisposition), item.RequiredRulingDisposition))
                    throw new InvalidOperationException("Effect request has an invalid ruling-disposition condition.");
                RequireRoleReference(item.TargetRoleId, "effect target", roles, agentIds);
                ValidateStableId(item.StatusId, "effect status id");
                ValidateRange(item.RequestedResourceDelta, -1_000_000, 1_000_000,
                    "effect requested resource delta");
                if (item.RequestedResourceDelta != 0 &&
                    !RoleHasEconomicAccount(definition, item.TargetRoleId))
                {
                    throw new InvalidOperationException(
                        "A resource-changing official effect requires an initialized account " +
                        "for its target role.");
                }
                string signature = $"{item.CauseRulingId}\u001f" +
                                   $"{item.RequiredRulingDisposition}\u001f" +
                                   $"{item.TargetRoleId}\u001f{item.StatusId}";
                if (!effectSignatures.Add(signature))
                {
                    throw new InvalidOperationException(
                        "Official status effects must be unique for ruling, disposition, " +
                        "target role and status.");
                }
            }
            return result;
        }

        private static Dictionary<string, ScenarioIrreversibleRelianceDefinition>
            ValidateRelianceDefinitions(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            Dictionary<string, ScenarioOpportunityDefinition> opportunities,
            Dictionary<string, ScenarioOfficialStatusEffectRequest> effects,
            Dictionary<string, string> rulingToCase,
            Dictionary<string, ScenarioAlternativeDefinition> alternatives,
            HashSet<string> activeOpportunityCycles,
            HashSet<string> agentIds)
        {
            var result = new Dictionary<string, ScenarioIrreversibleRelianceDefinition>(
                StringComparer.Ordinal);
            var choiceKeys = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.RelianceDefinitions.Count; i++)
            {
                ScenarioIrreversibleRelianceDefinition item = definition.RelianceDefinitions[i] ??
                    throw new InvalidOperationException("Reliance definitions cannot contain null entries.");
                ValidateStableId(item.RelianceId, "reliance id");
                ValidateStrictObjectOrder(previousId, item.RelianceId, "reliance definitions");
                previousId = item.RelianceId;
                if (!result.TryAdd(item.RelianceId, item))
                    throw new InvalidOperationException($"Duplicate reliance id '{item.RelianceId}'.");
                ValidateCycle(item.Cycle, definition, "reliance cycle");
                RequireRoleReference(item.RelyingRoleId, "relying role", roles, agentIds);
                RequireRoleReference(item.BeneficiaryRoleId, "reliance beneficiary", roles, agentIds);
                if (!string.IsNullOrEmpty(item.RelatedRoleId))
                    RequireRoleReference(item.RelatedRoleId, "reliance related role", roles, agentIds);
                if (!opportunities.TryGetValue(item.SourceOpportunityId, out ScenarioOpportunityDefinition opportunity))
                    throw new InvalidOperationException("Reliance references a missing source opportunity.");
                if (item.SourceActionKind != SocietyActionKind.Work &&
                    item.SourceActionKind != SocietyActionKind.SeekAid &&
                    item.SourceActionKind != SocietyActionKind.Appeal)
                {
                    throw new InvalidOperationException(
                        "Reliance requires an opportunity-backed action with a stable opportunity id.");
                }
                ValidateActionOpportunityCompatibility(item.SourceActionKind, opportunity.Kind, "reliance");
                if (!opportunity.EligibleRoleIds.Contains(item.RelyingRoleId))
                    throw new InvalidOperationException("Relying role is not eligible for its source opportunity.");
                if (!activeOpportunityCycles.Contains(OpportunityCycleKey(item.SourceOpportunityId, item.Cycle)))
                    throw new InvalidOperationException("Reliance source opportunity is not active on its cycle.");
                if (!effects.TryGetValue(item.EnablingEffectRequestId,
                    out ScenarioOfficialStatusEffectRequest effect))
                {
                    throw new InvalidOperationException("Reliance references a missing enabling effect.");
                }
                if (!string.Equals(effect.TargetRoleId, item.RelyingRoleId, StringComparison.Ordinal) ||
                    effect.Cycle >= item.Cycle)
                {
                    throw new InvalidOperationException(
                        "Reliance requires a status effect established for its role on an earlier cycle.");
                }
                if (!rulingToCase.ContainsKey(item.EnablingRulingId) ||
                    !string.Equals(item.EnablingRulingId, effect.CauseRulingId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Reliance enabling ruling does not match its effect.");
                }
                ValidateStableId(item.IrreversibleChoiceKey, "irreversible choice key");
                if (!choiceKeys.Add(item.IrreversibleChoiceKey))
                    throw new InvalidOperationException("Irreversible choice keys must be unique.");
                ValidateStableId(item.AbandonedAlternativeKey, "abandoned alternative key");
                if (string.Equals(item.IrreversibleChoiceKey, item.AbandonedAlternativeKey,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Reliance choice and abandoned alternative must differ.");
                }
                if (!alternatives.TryGetValue(item.AbandonedAlternativeKey,
                        out ScenarioAlternativeDefinition alternative) ||
                    !string.Equals(alternative.OwnerRoleId, item.RelyingRoleId, StringComparison.Ordinal) ||
                    !alternative.InitiallyAvailable)
                {
                    throw new InvalidOperationException(
                        "Reliance requires an initially available alternative owned by the relying role.");
                }
                ValidateStableId(item.ExpectedStatusId, "reliance expected status id");
                if (!string.Equals(item.ExpectedStatusId, effect.StatusId, StringComparison.Ordinal) ||
                    item.ExpectedRecognisedState != effect.RequestedRecognisedState)
                {
                    throw new InvalidOperationException("Reliance expected state does not match its enabling effect.");
                }
                if (!string.Equals(
                        opportunity.RequiredOfficialStatusId,
                        item.ExpectedStatusId,
                        StringComparison.Ordinal) ||
                    opportunity.RequiredOfficialStatusRecognised !=
                        item.ExpectedRecognisedState)
                {
                    throw new InvalidOperationException(
                        "Reliance source opportunity must explicitly read the official " +
                        "state established by its enabling effect.");
                }
                ValidateStableId(item.ResourceId, "reliance resource id");
                RequireCollection(item.Effects, 1, 3, "reliance effects");
                var recipients = new HashSet<ScenarioRelianceEffectRecipient>();
                var effectRecipientRoles = new List<string>();
                string previousEffectId = null;
                int actorResourceDelta = 0;
                for (int j = 0; j < item.Effects.Count; j++)
                {
                    ScenarioRelianceEffectDefinition relianceEffect = item.Effects[j] ??
                        throw new InvalidOperationException(
                            "Reliance effects cannot contain null entries.");
                    ValidateStableId(relianceEffect.EffectId, "reliance effect id");
                    ValidateStrictObjectOrder(
                        previousEffectId, relianceEffect.EffectId, "reliance effects");
                    previousEffectId = relianceEffect.EffectId;
                    if (!Enum.IsDefined(
                            typeof(ScenarioRelianceEffectRecipient),
                            relianceEffect.Recipient) ||
                        !recipients.Add(relianceEffect.Recipient))
                    {
                        throw new InvalidOperationException(
                            "Reliance effects require unique, valid recipients.");
                    }
                    if (!Enum.IsDefined(
                        typeof(MaterialConsequenceKind), relianceEffect.MaterialKind))
                    {
                        throw new InvalidOperationException(
                            "Reliance effect has an invalid material consequence kind.");
                    }
                    ValidateOptionalStableId(
                        relianceEffect.MaterialKindId, "reliance material kind id");
                    ValidateOptionalStableId(
                        relianceEffect.ResourceId, "reliance effect resource id");
                    ValidateRange(
                        relianceEffect.ResourceDelta,
                        -1_000_000,
                        1_000_000,
                        "reliance effect resource delta");
                    ValidateRange(
                        relianceEffect.NeedPressureDelta,
                        -100,
                        100,
                        "reliance effect need delta");
                    if (!relianceEffect.HasNeedEffect &&
                        relianceEffect.NeedPressureDelta != 0)
                    {
                        throw new InvalidOperationException(
                            "A reliance need delta requires an explicit need effect.");
                    }
                    if (relianceEffect.HasNeedEffect &&
                        !Enum.IsDefined(typeof(NeedKind), relianceEffect.Need))
                    {
                        throw new InvalidOperationException(
                            "Reliance effect has an invalid need kind.");
                    }
                    if (relianceEffect.ResourceDelta == 0 &&
                        relianceEffect.NeedPressureDelta == 0)
                    {
                        throw new InvalidOperationException(
                            "Reliance effects must change a resource or a need.");
                    }

                    string recipientRole = relianceEffect.Recipient switch
                    {
                        ScenarioRelianceEffectRecipient.RelyingRole => item.RelyingRoleId,
                        ScenarioRelianceEffectRecipient.BeneficiaryRole => item.BeneficiaryRoleId,
                        ScenarioRelianceEffectRecipient.RelatedRole => item.RelatedRoleId,
                        _ => null,
                    };
                    if (string.IsNullOrEmpty(recipientRole))
                    {
                        throw new InvalidOperationException(
                            "Reliance effect references an undeclared recipient role.");
                    }
                    effectRecipientRoles.Add(recipientRole);
                    if (relianceEffect.ResourceDelta != 0 &&
                        !RoleHasEconomicAccount(definition, recipientRole))
                    {
                        throw new InvalidOperationException(
                            "Every resource-changing reliance recipient requires an " +
                            "initialized economic account.");
                    }
                    if (relianceEffect.Recipient ==
                        ScenarioRelianceEffectRecipient.RelyingRole)
                    {
                        actorResourceDelta += relianceEffect.ResourceDelta;
                    }
                }
                for (int left = 0; left < effectRecipientRoles.Count; left++)
                {
                    for (int right = left + 1; right < effectRecipientRoles.Count; right++)
                    {
                        if (string.Equals(
                                effectRecipientRoles[left],
                                effectRecipientRoles[right],
                                StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "Distinct reliance recipient slots cannot resolve through the " +
                                "same semantic role.");
                        }
                        RequireRolesDeclaredDistinct(
                            effectRecipientRoles[left],
                            effectRecipientRoles[right],
                            roles,
                            "reliance effect recipients");
                    }
                }
                if (actorResourceDelta >= 0)
                {
                    throw new InvalidOperationException(
                        "Irreversible reliance must record a net resource cost for the relying role.");
                }
            }
            return result;
        }

        private static Dictionary<string, ScenarioAppealDefinition> ValidateAppeals(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            Dictionary<string, ScenarioOpportunityDefinition> opportunities,
            Dictionary<string, ScenarioCaseDefinition> cases,
            Dictionary<string, string> rulingToCase,
            Dictionary<string, ScenarioEvidenceTemplateDefinition> evidence,
            HashSet<string> activeOpportunityCycles,
            HashSet<string> agentIds)
        {
            var result = new Dictionary<string, ScenarioAppealDefinition>(StringComparer.Ordinal);
            var usedOpportunities = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.Appeals.Count; i++)
            {
                ScenarioAppealDefinition item = definition.Appeals[i] ??
                    throw new InvalidOperationException("Appeals cannot contain null entries.");
                ValidateStableId(item.AppealId, "appeal id");
                ValidateStrictObjectOrder(previousId, item.AppealId, "appeals");
                previousId = item.AppealId;
                if (!result.TryAdd(item.AppealId, item))
                    throw new InvalidOperationException($"Duplicate appeal id '{item.AppealId}'.");
                if (!cases.TryGetValue(item.CaseId, out ScenarioCaseDefinition caseDefinition))
                    throw new InvalidOperationException("Appeal references a missing case.");
                if (!opportunities.TryGetValue(item.OpportunityId, out ScenarioOpportunityDefinition opportunity) ||
                    opportunity.Kind != ScenarioOpportunityKind.Appeal)
                {
                    throw new InvalidOperationException("Appeal references a missing or non-appeal opportunity.");
                }
                if (!usedOpportunities.Add(item.OpportunityId))
                    throw new InvalidOperationException("An appeal opportunity may define only one appeal.");
                RequireRoleReference(item.AppellantRoleId, "appeal appellant", roles, agentIds);
                if (!opportunity.EligibleRoleIds.Contains(item.AppellantRoleId))
                    throw new InvalidOperationException("Appeal appellant is not eligible for its opportunity.");
                ValidateCycle(item.FilingCycle, definition, "appeal filing cycle");
                ValidateCycle(item.HearingCycle, definition, "appeal hearing cycle");
                if (item.FilingCycle >= item.HearingCycle ||
                    item.HearingCycle != opportunity.HearingCycle ||
                    !activeOpportunityCycles.Contains(OpportunityCycleKey(item.OpportunityId, item.FilingCycle)))
                {
                    throw new InvalidOperationException("Appeal filing/hearing schedule is inconsistent.");
                }
                if (!string.Equals(opportunity.CaseId, item.CaseId, StringComparison.Ordinal) ||
                    !string.Equals(opportunity.ChallengedRulingId, item.ChallengedRulingId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Appeal does not match its opportunity.");
                }
                if (!rulingToCase.TryGetValue(item.ChallengedRulingId, out string challengedCase) ||
                    !string.Equals(challengedCase, item.CaseId, StringComparison.Ordinal) ||
                    !rulingToCase.TryGetValue(item.ResultingRulingId, out string resultingCase) ||
                    !string.Equals(resultingCase, item.CaseId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Appeal rulings do not belong to its case.");
                }
                if (!string.Equals(item.ResultingRulingId, caseDefinition.AdjudicationRulingId,
                    StringComparison.Ordinal))
                    throw new InvalidOperationException("Appeal resulting ruling must be the case adjudication ruling.");
                if (RulingCycle(caseDefinition, item.ChallengedRulingId) >= item.FilingCycle ||
                    item.HearingCycle != caseDefinition.AdjudicationCycle)
                {
                    throw new InvalidOperationException(
                        "Appeal must be filed after the challenged ruling and heard on the " +
                        "resulting adjudication cycle.");
                }
                bool appellantCanObserveAdverseDecision = false;
                for (int effectIndex = 0;
                     effectIndex < definition.OfficialStatusEffectRequests.Count;
                     effectIndex++)
                {
                    ScenarioOfficialStatusEffectRequest statusEffect =
                        definition.OfficialStatusEffectRequests[effectIndex];
                    if (string.Equals(
                            statusEffect.TargetRoleId,
                            item.AppellantRoleId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            statusEffect.StatusId,
                            InstitutionalStatusIds.AdverseDecision,
                            StringComparison.Ordinal) &&
                        statusEffect.RequestedRecognisedState &&
                        statusEffect.Cycle < item.FilingCycle &&
                        string.Equals(
                            statusEffect.CauseRulingId,
                            item.ChallengedRulingId,
                            StringComparison.Ordinal))
                    {
                        appellantCanObserveAdverseDecision = true;
                        break;
                    }
                }
                if (!appellantCanObserveAdverseDecision)
                {
                    throw new InvalidOperationException(
                        "Appeal appellant requires an earlier declared adverse-decision " +
                        "status effect from the challenged ruling.");
                }
                ValidateOptionalStableId(item.ResultingHoldingId, "appeal resulting holding id");
                ValidateOrderedIds(item.GroundsEvidenceTemplateIds, "appeal grounds evidence", true);
                for (int j = 0; j < item.GroundsEvidenceTemplateIds.Count; j++)
                {
                    string evidenceId = item.GroundsEvidenceTemplateIds[j];
                    if (!evidence.TryGetValue(evidenceId, out ScenarioEvidenceTemplateDefinition template) ||
                        !string.Equals(template.CaseId, item.CaseId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Appeal grounds reference missing or unrelated evidence.");
                    }
                }
            }
            return result;
        }

        private static void ValidateRelianceRecoveries(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            Dictionary<string, ScenarioIrreversibleRelianceDefinition> reliance,
            Dictionary<string, ScenarioAppealDefinition> appeals,
            Dictionary<string, ScenarioCaseDefinition> cases,
            Dictionary<string, string> rulingToCase,
            HashSet<string> agentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var relianceIds = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.RelianceRecoveries.Count; i++)
            {
                ScenarioRelianceRecoveryDefinition item =
                    definition.RelianceRecoveries[i] ??
                    throw new InvalidOperationException(
                        "Reliance recoveries cannot contain null entries.");
                ValidateStableId(item.RecoveryDefinitionId, "reliance recovery id");
                ValidateStrictObjectOrder(
                    previousId, item.RecoveryDefinitionId, "reliance recoveries");
                previousId = item.RecoveryDefinitionId;
                if (!ids.Add(item.RecoveryDefinitionId))
                    throw new InvalidOperationException("Duplicate reliance recovery id.");
                if (!reliance.TryGetValue(
                    item.RelianceId,
                    out ScenarioIrreversibleRelianceDefinition reliedOn) ||
                    !relianceIds.Add(item.RelianceId))
                {
                    throw new InvalidOperationException(
                        "A reliance event may define exactly one declared recovery.");
                }
                ValidateCycle(item.Cycle, definition, "reliance recovery cycle");
                if (!rulingToCase.TryGetValue(
                        item.TriggerReversalRulingId,
                        out string rulingCaseId) ||
                    !string.Equals(rulingCaseId, item.ParentCaseId,
                        StringComparison.Ordinal) ||
                    !cases.TryGetValue(item.ParentCaseId, out ScenarioCaseDefinition parentCase) ||
                    item.Cycle != RulingCycle(parentCase, item.TriggerReversalRulingId) ||
                    item.Cycle <= reliedOn.Cycle)
                {
                    throw new InvalidOperationException(
                        "Reliance recovery must be caused by a later ruling in its parent case.");
                }
                bool appealProducesRuling = false;
                foreach (ScenarioAppealDefinition appeal in appeals.Values)
                {
                    if (string.Equals(
                        appeal.ResultingRulingId,
                        item.TriggerReversalRulingId,
                        StringComparison.Ordinal))
                    {
                        appealProducesRuling = true;
                        break;
                    }
                }
                if (!appealProducesRuling)
                {
                    throw new InvalidOperationException(
                        "Reliance recovery trigger must be an appeal's resulting ruling.");
                }
                ValidateStableId(item.CaseIdPrefix, "reliance recovery case prefix");
                RequireRoleReference(item.ClaimantRoleId, "recovery claimant", roles, agentIds);
                RequireRoleReference(item.RespondentRoleId, "recovery respondent", roles, agentIds);
                if (!string.Equals(item.ClaimantRoleId, reliedOn.RelyingRoleId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Reliance recovery claimant must be the relying role.");
                }
                RequireRolesDeclaredDistinct(
                    item.ClaimantRoleId,
                    item.RespondentRoleId,
                    roles,
                    "reliance recovery parties");
                ValidateStableId(item.IssueId, "reliance recovery issue id");
                ValidateFacts(item.Facts, "reliance recovery facts", true);
                RejectDirectAgentIdsInFacts(
                    item.Facts, "reliance recovery facts", agentIds);
            }
        }

        private static Dictionary<string, ScenarioHoldingDefinition> ValidateHoldings(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioAppealDefinition> appeals,
            Dictionary<string, ScenarioCaseDefinition> cases,
            Dictionary<string, string> rulingToCase,
            Dictionary<string, ScenarioEvidenceTemplateDefinition> evidence,
            HashSet<string> agentIds)
        {
            var result = new Dictionary<string, ScenarioHoldingDefinition>(StringComparer.Ordinal);
            var scopeIds = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.Holdings.Count; i++)
            {
                ScenarioHoldingDefinition item = definition.Holdings[i] ??
                    throw new InvalidOperationException("Holdings cannot contain null entries.");
                ValidateStableId(item.HoldingId, "holding id");
                ValidateStrictObjectOrder(previousId, item.HoldingId, "holdings");
                previousId = item.HoldingId;
                if (!result.TryAdd(item.HoldingId, item))
                    throw new InvalidOperationException($"Duplicate holding id '{item.HoldingId}'.");
                ValidateStableId(item.ScopeId, "holding scope id");
                if (!scopeIds.Add(item.ScopeId))
                    throw new InvalidOperationException("Holding scope ids must be unique.");
                if (!appeals.TryGetValue(item.SourceAppealId, out ScenarioAppealDefinition appeal))
                    throw new InvalidOperationException("Holding references a missing source appeal.");
                if (!rulingToCase.TryGetValue(item.SourceRulingId, out string sourceCaseId) ||
                    !string.Equals(sourceCaseId, appeal.CaseId, StringComparison.Ordinal) ||
                    !string.Equals(item.SourceRulingId, appeal.ResultingRulingId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Holding source ruling does not match its appeal.");
                }
                ValidateStableId(item.RuleId, "holding rule id");
                ValidateStableId(item.IssueId, "holding issue id");
                if (!string.Equals(item.IssueId, cases[appeal.CaseId].IssueId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Holding issue does not match its source case.");
                ValidateCycle(item.EstablishedCycle, definition, "holding established cycle");
                if (item.EstablishedCycle != appeal.HearingCycle ||
                    item.EstablishedCycle !=
                        RulingCycle(cases[appeal.CaseId], item.SourceRulingId))
                {
                    throw new InvalidOperationException(
                        "Holding must be established on its resulting appeal-ruling cycle.");
                }
                ValidateFacts(item.RequiredScopeFacts, "holding required scope facts", true);
                RejectDirectAgentIdsInFacts(
                    item.RequiredScopeFacts,
                    "holding required scope facts",
                    agentIds);
                ValidateOrderedIds(item.SupportingEvidenceTemplateIds,
                    "holding supporting evidence", true);
                for (int j = 0; j < item.SupportingEvidenceTemplateIds.Count; j++)
                {
                    string evidenceId = item.SupportingEvidenceTemplateIds[j];
                    if (!evidence.TryGetValue(evidenceId, out ScenarioEvidenceTemplateDefinition template) ||
                        !string.Equals(template.CaseId, appeal.CaseId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Holding support references missing or unrelated evidence.");
                    }
                }
            }
            return result;
        }

        private static void ValidateAppealHoldingLinks(
            Dictionary<string, ScenarioAppealDefinition> appeals,
            Dictionary<string, ScenarioHoldingDefinition> holdings)
        {
            foreach (ScenarioAppealDefinition appeal in appeals.Values)
            {
                if (string.IsNullOrEmpty(appeal.ResultingHoldingId)) continue;
                if (!holdings.TryGetValue(appeal.ResultingHoldingId, out ScenarioHoldingDefinition holding) ||
                    !string.Equals(holding.SourceAppealId, appeal.AppealId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Appeal resulting holding link is missing or inconsistent.");
                }
            }
            foreach (ScenarioHoldingDefinition holding in holdings.Values)
            {
                if (!string.Equals(appeals[holding.SourceAppealId].ResultingHoldingId,
                    holding.HoldingId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Every holding must be named by its source appeal.");
                }
            }
        }

        private static void ValidateHoldingCitations(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioCaseDefinition> cases,
            Dictionary<string, string> rulingToCase,
            Dictionary<string, ScenarioHoldingDefinition> holdings,
            Dictionary<string, ScenarioAppealDefinition> appeals)
        {
            var citationIds = new HashSet<string>(StringComparer.Ordinal);
            var holdingRulingPairs = new HashSet<string>(StringComparer.Ordinal);
            var holdingCasePairs = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.HoldingCitations.Count; i++)
            {
                ScenarioHoldingCitationDefinition item =
                    definition.HoldingCitations[i] ??
                    throw new InvalidOperationException(
                        "Holding citations cannot contain null entries.");
                ValidateStableId(item.CitationId, "holding citation id");
                ValidateStrictObjectOrder(
                    previousId,
                    item.CitationId,
                    "holding citations");
                previousId = item.CitationId;
                if (!citationIds.Add(item.CitationId))
                {
                    throw new InvalidOperationException(
                        $"Duplicate holding citation id '{item.CitationId}'.");
                }
                if (!holdings.TryGetValue(
                        item.HoldingId,
                        out ScenarioHoldingDefinition holding))
                    throw new InvalidOperationException("Citation references a missing holding.");
                if (!cases.TryGetValue(
                        item.TargetCaseId,
                        out ScenarioCaseDefinition targetCase))
                    throw new InvalidOperationException("Citation references a missing target case.");
                if (!rulingToCase.TryGetValue(
                        item.TargetRulingId,
                        out string rulingCaseId) ||
                    !string.Equals(
                        rulingCaseId,
                        item.TargetCaseId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Citation target ruling must belong to its exact target case.");
                }
                if (string.Equals(
                        item.TargetRulingId,
                        holding.SourceRulingId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A holding cannot cite itself in its source ruling.");
                }
                long targetCycle = RulingCycle(targetCase, item.TargetRulingId);
                bool targetsInitialRuling = string.Equals(
                    item.TargetRulingId,
                    targetCase.InitialRulingId,
                    StringComparison.Ordinal);
                if (!targetsInitialRuling)
                {
                    int exactAppealRoutes = 0;
                    foreach (ScenarioAppealDefinition appeal in appeals.Values)
                    {
                        if (string.Equals(
                                appeal.CaseId,
                                item.TargetCaseId,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                appeal.ResultingRulingId,
                                item.TargetRulingId,
                                StringComparison.Ordinal) &&
                            appeal.HearingCycle == targetCase.AdjudicationCycle)
                        {
                            exactAppealRoutes++;
                        }
                    }
                    if (exactAppealRoutes != 1)
                    {
                        throw new InvalidOperationException(
                            "Citation target adjudication ruling requires one exact " +
                            "declared appeal route.");
                    }
                }
                if (targetCycle < holding.EstablishedCycle ||
                    (targetsInitialRuling &&
                     targetCycle == holding.EstablishedCycle))
                {
                    throw new InvalidOperationException(
                        "Citation target ruling precedes the holding in execution order.");
                }
                if (!string.Equals(
                        targetCase.IssueId,
                        holding.IssueId,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Citation target issue does not match its holding.");
                if (!targetCase.Facts.ContainsAll(holding.RequiredScopeFacts))
                    throw new InvalidOperationException(
                        "Citation holding scope does not match target case facts.");

                string rulingPair = $"{item.HoldingId}\u001f{item.TargetRulingId}";
                if (!holdingRulingPairs.Add(rulingPair))
                {
                    throw new InvalidOperationException(
                        "A holding may be declared only once for an exact target ruling.");
                }
                string casePair = $"{item.HoldingId}\u001f{item.TargetCaseId}";
                if (!holdingCasePairs.Add(casePair))
                {
                    throw new InvalidOperationException(
                        "A holding may be applied only once per target case.");
                }
            }
        }

        private static void ValidateDescendantCases(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            Dictionary<string, ScenarioOpportunityDefinition> opportunities,
            Dictionary<string, ScenarioCaseDefinition> cases,
            Dictionary<string, string> rulingToCase,
            HashSet<string> activeOpportunityCycles,
            HashSet<string> evidenceActivatedCaseIds,
            HashSet<string> agentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var descendantCaseIds = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.DescendantCases.Count; i++)
            {
                ScenarioActionCausedDescendantCaseDefinition item = definition.DescendantCases[i] ??
                    throw new InvalidOperationException("Descendant cases cannot contain null entries.");
                ValidateStableId(item.DescendantDefinitionId, "descendant definition id");
                ValidateStrictObjectOrder(previousId, item.DescendantDefinitionId, "descendant cases");
                previousId = item.DescendantDefinitionId;
                if (!ids.Add(item.DescendantDefinitionId))
                    throw new InvalidOperationException("Duplicate descendant definition id.");
                if (!cases.TryGetValue(item.CaseId, out ScenarioCaseDefinition descendantCase) ||
                    !descendantCaseIds.Add(item.CaseId))
                    throw new InvalidOperationException("Descendant definition references a missing or duplicate case.");
                if (evidenceActivatedCaseIds.Contains(item.CaseId))
                {
                    throw new InvalidOperationException(
                        "A case cannot be both evidence-activated and action-caused.");
                }
                if (!cases.TryGetValue(item.ParentCaseId, out ScenarioCaseDefinition parentCase) ||
                    string.Equals(item.CaseId, item.ParentCaseId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Descendant definition has an invalid parent case.");
                ValidateCycle(item.OpenCycle, definition, "descendant open cycle");
                ValidateCycle(item.TriggerCycle, definition, "descendant trigger cycle");
                if (item.OpenCycle != descendantCase.OpenCycle || item.OpenCycle <= parentCase.OpenCycle)
                    throw new InvalidOperationException("Descendant open cycle is inconsistent with its case.");
                if (item.TriggerCycle >= item.OpenCycle)
                    throw new InvalidOperationException(
                        "Descendant trigger action must occur before the case opens.");
                RequireRoleReference(item.TriggerRoleId, "descendant trigger role", roles, agentIds);
                if (!Enum.IsDefined(typeof(SocietyActionKind), item.TriggerActionKind) ||
                    item.TriggerActionKind == SocietyActionKind.Idle)
                    throw new InvalidOperationException("Descendant case requires a non-idle society action.");
                ValidateOptionalStableId(item.TriggerPropositionId,
                    "descendant trigger proposition id");
                if (item.TriggerActionKind == SocietyActionKind.Disclose &&
                    string.IsNullOrEmpty(item.TriggerPropositionId))
                {
                    throw new InvalidOperationException(
                        "Disclosure-caused descendant cases require a proposition filter.");
                }
                if (item.TriggerActionKind != SocietyActionKind.Disclose &&
                    !string.IsNullOrEmpty(item.TriggerPropositionId))
                {
                    throw new InvalidOperationException(
                        "Only disclosure actions expose a proposition for descendant matching.");
                }
                if (!string.IsNullOrEmpty(item.TriggerOpportunityId))
                {
                    if (!opportunities.TryGetValue(item.TriggerOpportunityId,
                        out ScenarioOpportunityDefinition opportunity))
                        throw new InvalidOperationException("Descendant case references a missing trigger opportunity.");
                    ValidateActionOpportunityCompatibility(item.TriggerActionKind, opportunity.Kind,
                        "descendant case");
                    if (!opportunity.EligibleRoleIds.Contains(item.TriggerRoleId))
                        throw new InvalidOperationException("Descendant trigger role is not opportunity-eligible.");
                    if (!activeOpportunityCycles.Contains(
                        OpportunityCycleKey(item.TriggerOpportunityId, item.TriggerCycle)))
                    {
                        throw new InvalidOperationException(
                            "Descendant trigger opportunity is not active on its exact trigger cycle.");
                    }
                }
                else if (item.TriggerActionKind == SocietyActionKind.Work ||
                         item.TriggerActionKind == SocietyActionKind.SeekAid ||
                         item.TriggerActionKind == SocietyActionKind.Help ||
                         item.TriggerActionKind == SocietyActionKind.Appeal)
                {
                    throw new InvalidOperationException("Opportunity-backed action requires a trigger opportunity id.");
                }
                if (!rulingToCase.TryGetValue(item.OriginatingRulingId, out string rulingCaseId) ||
                    !string.Equals(rulingCaseId, item.ParentCaseId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Descendant originating ruling must belong to its parent case.");
                if (RulingCycle(parentCase, item.OriginatingRulingId) >= item.OpenCycle)
                    throw new InvalidOperationException(
                        "Descendant originating ruling must precede the descendant case.");
                ValidateOrderedIds(item.ConnectedRoleIds, "descendant connected roles", true);
                for (int j = 0; j < item.ConnectedRoleIds.Count; j++)
                    RequireRoleReference(item.ConnectedRoleIds[j], "descendant connected role", roles, agentIds);
            }

            for (int i = 0; i < definition.Cases.Count; i++)
            {
                string caseId = definition.Cases[i].CaseId;
                if (string.Equals(caseId, definition.PrimaryCaseId, StringComparison.Ordinal))
                {
                    if (descendantCaseIds.Contains(caseId))
                        throw new InvalidOperationException("Primary case cannot also be a descendant case.");
                }
                else if (!descendantCaseIds.Contains(caseId) &&
                         !evidenceActivatedCaseIds.Contains(caseId))
                {
                    throw new InvalidOperationException(
                        $"Non-primary case '{caseId}' lacks a conditional activation definition.");
                }
            }
        }

        private static Dictionary<string, ScenarioExclusiveEntitlementDefinition> ValidateEntitlements(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            HashSet<string> agentIds)
        {
            var result = new Dictionary<string, ScenarioExclusiveEntitlementDefinition>(StringComparer.Ordinal);
            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            var holderStatusIds = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.ExclusiveEntitlements.Count; i++)
            {
                ScenarioExclusiveEntitlementDefinition item = definition.ExclusiveEntitlements[i] ??
                    throw new InvalidOperationException("Exclusive entitlements cannot contain null entries.");
                ValidateStableId(item.EntitlementId, "exclusive entitlement id");
                ValidateStrictObjectOrder(previousId, item.EntitlementId, "exclusive entitlements");
                previousId = item.EntitlementId;
                if (!result.TryAdd(item.EntitlementId, item))
                    throw new InvalidOperationException("Duplicate exclusive entitlement id.");
                ValidateStableId(item.ResourceId, "exclusive resource id");
                if (!resourceIds.Add(item.ResourceId))
                    throw new InvalidOperationException("A resource may have only one exclusive entitlement.");
                ValidateStableId(item.OfficialStatusId, "exclusive entitlement status id");
                if (!holderStatusIds.Add(item.OfficialStatusId))
                    throw new InvalidOperationException(
                        "An official holder status may belong to only one entitlement.");
                RequireRoleReference(item.InitialHolderRoleId, "initial entitlement holder", roles, agentIds);
                AgentState recognisedHolder = null;
                int recognisedCount = 0;
                for (int j = 0; j < definition.InitialSociety.Agents.Count; j++)
                {
                    AgentState agent = definition.InitialSociety.Agents[j];
                    if (!agent.Standing.IsRecognised(item.OfficialStatusId)) continue;
                    recognisedHolder = agent;
                    recognisedCount++;
                }
                if (recognisedCount != 1 ||
                    !MatchesQuery(roles[item.InitialHolderRoleId].Query, recognisedHolder))
                {
                    throw new InvalidOperationException(
                        "Initial entitlement state must contain exactly one recognised " +
                        "holder matching its declared semantic role.");
                }
                ValidateRange(item.Units, 1, 1_000_000, "exclusive entitlement units");
            }
            return result;
        }

        private static void ValidateTransfers(
            InstitutionalScenarioDefinition definition,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            Dictionary<string, ScenarioCaseDefinition> cases,
            Dictionary<string, string> rulingToCase,
            Dictionary<string, ScenarioHoldingDefinition> holdings,
            Dictionary<string, ScenarioExclusiveEntitlementDefinition> entitlements,
            HashSet<string> agentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var causeResourcePairs = new HashSet<string>(StringComparer.Ordinal);
            string previousId = null;
            for (int i = 0; i < definition.EntitlementTransfers.Count; i++)
            {
                ScenarioExclusiveEntitlementTransferDefinition item = definition.EntitlementTransfers[i] ??
                    throw new InvalidOperationException("Entitlement transfers cannot contain null entries.");
                ValidateStableId(item.TransferId, "entitlement transfer id");
                ValidateStableId(
                    InstitutionalScenarioDerivedIds.ConnectedOutcomePair(item.TransferId),
                    "entitlement transfer connected-outcome pair id");
                ValidateStrictObjectOrder(previousId, item.TransferId, "entitlement transfers");
                previousId = item.TransferId;
                if (!ids.Add(item.TransferId))
                    throw new InvalidOperationException("Duplicate entitlement transfer id.");
                ValidateCycle(item.Cycle, definition, "entitlement transfer cycle");
                if (!entitlements.TryGetValue(
                        item.EntitlementId,
                        out ScenarioExclusiveEntitlementDefinition entitlement))
                    throw new InvalidOperationException("Transfer references a missing entitlement.");
                RequireRoleReference(item.FromRoleId, "transfer source role", roles, agentIds);
                RequireRoleReference(item.ToRoleId, "transfer destination role", roles, agentIds);
                if (string.Equals(item.FromRoleId, item.ToRoleId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Exclusive entitlement transfer requires distinct holders.");
                RequireRolesDeclaredDistinct(
                    item.FromRoleId,
                    item.ToRoleId,
                    roles,
                    "exclusive entitlement transfer holders");
                if (!Enum.IsDefined(
                        typeof(RulingDisposition),
                        item.RequiredRulingDisposition))
                {
                    throw new InvalidOperationException(
                        "Entitlement transfer requires a valid ruling disposition.");
                }
                if (!Enum.IsDefined(typeof(MaterialConsequenceKind), item.GainKind) ||
                    !Enum.IsDefined(typeof(MaterialConsequenceKind), item.LossKind))
                {
                    throw new InvalidOperationException(
                        "Entitlement transfer consequence kinds are invalid.");
                }
                ValidateOptionalStableId(item.GainKindId, "entitlement transfer gain kind id");
                ValidateOptionalStableId(item.LossKindId, "entitlement transfer loss kind id");
                ValidateStableId(item.CauseCaseId, "entitlement transfer cause case id");
                if (!cases.ContainsKey(item.CauseCaseId))
                    throw new InvalidOperationException("Transfer references a missing cause case.");
                ValidateStableId(item.CauseRulingId, "entitlement transfer cause ruling id");
                if (!rulingToCase.TryGetValue(
                        item.CauseRulingId,
                        out string rulingCaseId) ||
                    !string.Equals(rulingCaseId, item.CauseCaseId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Transfer cause ruling must belong to its exact cause case.");
                }
                string causeResourcePair =
                    $"{item.CauseRulingId}\u001f{entitlement.ResourceId}";
                if (!causeResourcePairs.Add(causeResourcePair))
                {
                    throw new InvalidOperationException(
                        "A cause ruling may transfer an exclusive resource only once.");
                }
                ValidateStableId(item.CauseHoldingId, "entitlement transfer cause holding id");
                if (!holdings.TryGetValue(item.CauseHoldingId, out ScenarioHoldingDefinition holding))
                    throw new InvalidOperationException("Transfer references a missing cause holding.");
                ScenarioCaseDefinition targetCase = cases[item.CauseCaseId];
                if (!RulingDispositionCanMaterialise(
                        targetCase,
                        item.CauseRulingId,
                        item.RequiredRulingDisposition))
                {
                    throw new InvalidOperationException(
                        "Entitlement transfer ruling disposition cannot materialise " +
                        "in its cause-ruling phase.");
                }
                bool exactCitationDeclared = false;
                for (int citationIndex = 0;
                     citationIndex < definition.HoldingCitations.Count;
                     citationIndex++)
                {
                    ScenarioHoldingCitationDefinition citation =
                        definition.HoldingCitations[citationIndex];
                    if (string.Equals(
                            citation.HoldingId,
                            item.CauseHoldingId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            citation.TargetCaseId,
                            item.CauseCaseId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            citation.TargetRulingId,
                            item.CauseRulingId,
                            StringComparison.Ordinal))
                    {
                        exactCitationDeclared = true;
                        break;
                    }
                }
                if (!exactCitationDeclared ||
                    !targetCase.Facts.ContainsAll(holding.RequiredScopeFacts) ||
                    RulingCycle(targetCase, item.CauseRulingId) != item.Cycle ||
                    item.Cycle < holding.EstablishedCycle)
                {
                    throw new InvalidOperationException(
                        "Transfer requires its exact cause holding and ruling citation " +
                        "in the same cycle.");
                }
            }

            var chronological = new List<ScenarioExclusiveEntitlementTransferDefinition>(
                definition.EntitlementTransfers);
            chronological.Sort((left, right) =>
            {
                int cycle = left.Cycle.CompareTo(right.Cycle);
                return cycle != 0 ? cycle : StringComparer.Ordinal.Compare(left.TransferId, right.TransferId);
            });
            var holders = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ScenarioExclusiveEntitlementDefinition entitlement in entitlements.Values)
                holders.Add(entitlement.EntitlementId, entitlement.InitialHolderRoleId);
            for (int i = 0; i < chronological.Count; i++)
            {
                ScenarioExclusiveEntitlementTransferDefinition transfer = chronological[i];
                if (!string.Equals(holders[transfer.EntitlementId], transfer.FromRoleId,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Transfer '{transfer.TransferId}' does not name the current exclusive holder.");
                }
                holders[transfer.EntitlementId] = transfer.ToRoleId;
            }
        }

        private static void ValidateActionOpportunityCompatibility(
            SocietyActionKind action,
            ScenarioOpportunityKind opportunity,
            string context)
        {
            bool compatible = action switch
            {
                SocietyActionKind.Work => opportunity == ScenarioOpportunityKind.Work,
                SocietyActionKind.SeekAid => opportunity == ScenarioOpportunityKind.Aid,
                SocietyActionKind.Help => opportunity == ScenarioOpportunityKind.Aid,
                SocietyActionKind.Appeal => opportunity == ScenarioOpportunityKind.Appeal,
                SocietyActionKind.Disclose => true,
                SocietyActionKind.Withhold => true,
                _ => false,
            };
            if (!compatible)
                throw new InvalidOperationException($"{context} action is incompatible with its opportunity.");
        }

        private static void RequireRolesDeclaredDistinct(
            string leftRoleId,
            string rightRoleId,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            string context)
        {
            if (!roles.TryGetValue(leftRoleId, out ScenarioParticipantRoleDefinition left) ||
                !roles.TryGetValue(rightRoleId, out ScenarioParticipantRoleDefinition right) ||
                (!left.DistinctFromRoleIds.Contains(rightRoleId) &&
                 !right.DistinctFromRoleIds.Contains(leftRoleId)))
            {
                throw new InvalidOperationException(
                    $"{context} must declare a semantic distinct-role constraint.");
            }
        }

        private static bool RoleHasEconomicAccount(
            InstitutionalScenarioDefinition definition,
            string roleId)
        {
            for (int i = 0; i < definition.InitialEconomicAccounts.Count; i++)
            {
                if (string.Equals(
                    definition.InitialEconomicAccounts[i].OwnerRoleId,
                    roleId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void RejectDirectAgentId(
            string value,
            string context,
            HashSet<string> agentIds)
        {
            if (agentIds.Contains(value))
            {
                throw new InvalidOperationException(
                    $"{context} uses forbidden direct agent id '{value}'.");
            }
        }

        private static void RejectDirectAgentIdsInFacts(
            CaseFactSet facts,
            string context,
            HashSet<string> agentIds)
        {
            for (int i = 0; i < facts.Facts.Count; i++)
            {
                CaseFact fact = facts.Facts[i];
                RejectDirectAgentId(fact.Key, context, agentIds);
                RejectDirectAgentId(fact.Value, context, agentIds);
            }
        }

        private static long RulingCycle(ScenarioCaseDefinition caseDefinition, string rulingId)
        {
            return string.Equals(caseDefinition.InitialRulingId, rulingId, StringComparison.Ordinal)
                ? caseDefinition.InitialRulingCycle
                : caseDefinition.AdjudicationCycle;
        }

        private static bool RulingDispositionCanMaterialise(
            ScenarioCaseDefinition caseDefinition,
            string rulingId,
            RulingDisposition disposition)
        {
            bool initial = string.Equals(
                caseDefinition.InitialRulingId,
                rulingId,
                StringComparison.Ordinal);
            return initial
                ? disposition == RulingDisposition.Denied ||
                  disposition == RulingDisposition.ProvisionallyRecognised ||
                  disposition == RulingDisposition.Recognised
                : disposition == RulingDisposition.Affirmed ||
                  disposition == RulingDisposition.ReversedAndDenied ||
                  disposition == RulingDisposition.ReversedAndRecognised;
        }

        private static void RequireRoleReference(
            string roleId,
            string context,
            Dictionary<string, ScenarioParticipantRoleDefinition> roles,
            HashSet<string> agentIds)
        {
            ValidateStableId(roleId, context);
            if (agentIds.Contains(roleId))
                throw new InvalidOperationException($"{context} uses forbidden direct agent id '{roleId}'.");
            if (!roles.ContainsKey(roleId))
                throw new InvalidOperationException($"{context} references missing role '{roleId}'.");
        }

        private static void ValidateInitialAgentOrder(List<AgentState> agents)
        {
            int previousOrdinal = -1;
            string previousId = null;
            for (int i = 0; i < agents.Count; i++)
            {
                AgentState agent = agents[i];
                if (agent.SimulationOrdinal < previousOrdinal ||
                    (agent.SimulationOrdinal == previousOrdinal &&
                     StringComparer.Ordinal.Compare(previousId, agent.StableId) >= 0))
                {
                    throw new InvalidOperationException(
                        "Initial society agents must be ordered by simulation ordinal then stable id.");
                }
                previousOrdinal = agent.SimulationOrdinal;
                previousId = agent.StableId;
            }
        }

        private static void ValidateFacts(CaseFactSet facts, string context, bool requireAny)
        {
            if (facts == null) throw new InvalidOperationException($"{context} requires a fact set.");
            facts.Validate();
            if (requireAny && facts.Count == 0)
                throw new InvalidOperationException($"{context} requires at least one fact.");
            if (facts.Count > MaximumReferencesPerDefinition)
                throw new InvalidOperationException($"{context} exceeds the bounded fact count.");
            CaseFact previous = null;
            for (int i = 0; i < facts.Facts.Count; i++)
            {
                CaseFact current = facts.Facts[i];
                if (previous != null && previous.CompareTo(current) >= 0)
                    throw new InvalidOperationException($"{context} must use deterministic fact order.");
                previous = current;
            }
        }

        private static string OpportunityCycleKey(string opportunityId, long cycle)
        {
            return opportunityId + "\u001f" + cycle;
        }

        private static void ValidateCycle(long cycle, InstitutionalScenarioDefinition definition, string context)
        {
            if (cycle < definition.StartCycle || cycle > definition.EndCycle)
                throw new InvalidOperationException($"{context} is outside the scenario cycle range.");
        }

        private static void ValidateCycleRange(long start, long end, string context)
        {
            if (start < 0 || end < start || end > MaximumCycle)
                throw new InvalidOperationException($"{context} has invalid cycle bounds.");
        }

        private static void ValidateRange(int value, int minimum, int maximum, string context)
        {
            if (value < minimum || value > maximum)
                throw new InvalidOperationException($"{context} must be in [{minimum}, {maximum}].");
        }

        private static void ValidateStrictObjectOrder(string previousId, string currentId, string context)
        {
            if (previousId != null && StringComparer.Ordinal.Compare(previousId, currentId) >= 0)
                throw new InvalidOperationException($"{context} must be strictly ordered by stable id.");
        }

        private static void ValidateOrderedIds(List<string> ids, string context, bool requireAny)
        {
            if (ids == null) throw new InvalidOperationException($"{context} requires a collection.");
            if (ids.Count > MaximumReferencesPerDefinition)
                throw new InvalidOperationException($"{context} exceeds the bounded reference count.");
            if (requireAny && ids.Count == 0)
                throw new InvalidOperationException($"{context} requires at least one id.");
            string previousId = null;
            for (int i = 0; i < ids.Count; i++)
            {
                ValidateStableId(ids[i], context);
                if (previousId != null && StringComparer.Ordinal.Compare(previousId, ids[i]) >= 0)
                    throw new InvalidOperationException($"{context} must be strictly ordered and unique.");
                previousId = ids[i];
            }
        }

        private static void ValidateOptionalStableId(string value, string context)
        {
            if (!string.IsNullOrEmpty(value)) ValidateStableId(value, context);
        }

        private static void ValidateStableId(string value, string context)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdLength ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException($"{context} requires a bounded, non-blank stable id.");
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i]))
                    throw new InvalidOperationException($"{context} cannot contain control characters.");
            }
        }

        private static void RequireCollection<T>(List<T> values, int minimum, int maximum, string context)
        {
            if (values == null)
                throw new InvalidOperationException($"Scenario requires a {context} collection.");
            if (values.Count < minimum || values.Count > maximum)
                throw new InvalidOperationException($"{context} count must be in [{minimum}, {maximum}].");
        }
    }

    /// <summary>
    /// Deterministic ordinal lookup over a validated scenario definition. It exposes
    /// declarations only and cannot execute scenario transitions.
    /// </summary>
    public sealed class InstitutionalScenarioDefinitionIndex
    {
        private readonly SortedDictionary<string, ScenarioParticipantRoleDefinition> _roles = new();
        private readonly SortedDictionary<string, ScenarioLivedIncidentSeedDefinition> _incidents = new();
        private readonly SortedDictionary<string, ScenarioInitialEconomicAccountDefinition> _accounts = new();
        private readonly SortedDictionary<string, ScenarioAlternativeDefinition> _alternatives = new();
        private readonly SortedDictionary<string, ScenarioOpportunityDefinition> _opportunities = new();
        private readonly SortedDictionary<string, ScenarioCycleScheduleEntry> _schedule = new();
        private readonly SortedDictionary<string, ScenarioEvidenceTemplateDefinition> _evidence = new();
        private readonly SortedDictionary<string, ScenarioCaseDefinition> _cases = new();
        private readonly SortedDictionary<string, ScenarioEvidenceActivatedCaseDefinition>
            _evidenceActivatedCases = new();
        private readonly SortedDictionary<string, ScenarioOfficialStatusEffectRequest> _effects = new();
        private readonly SortedDictionary<string, ScenarioIrreversibleRelianceDefinition> _reliance = new();
        private readonly SortedDictionary<string, ScenarioRelianceRecoveryDefinition> _recoveries = new();
        private readonly SortedDictionary<string, ScenarioAppealDefinition> _appeals = new();
        private readonly SortedDictionary<string, ScenarioHoldingDefinition> _holdings = new();
        private readonly SortedDictionary<string, ScenarioHoldingCitationDefinition>
            _holdingCitations = new();
        private readonly SortedDictionary<string, ScenarioActionCausedDescendantCaseDefinition> _descendants = new();
        private readonly SortedDictionary<string, ScenarioExclusiveEntitlementDefinition> _entitlements = new();
        private readonly SortedDictionary<string, ScenarioExclusiveEntitlementTransferDefinition> _transfers = new();

        public InstitutionalScenarioDefinitionIndex(InstitutionalScenarioDefinition definition)
        {
            InstitutionalScenarioDefinitionValidator.Validate(definition);
            for (int i = 0; i < definition.ParticipantRoles.Count; i++)
                _roles.Add(definition.ParticipantRoles[i].RoleId, definition.ParticipantRoles[i]);
            for (int i = 0; i < definition.LivedIncidentSeeds.Count; i++)
                _incidents.Add(definition.LivedIncidentSeeds[i].IncidentSeedId,
                    definition.LivedIncidentSeeds[i]);
            for (int i = 0; i < definition.InitialEconomicAccounts.Count; i++)
                _accounts.Add(definition.InitialEconomicAccounts[i].AccountId,
                    definition.InitialEconomicAccounts[i]);
            for (int i = 0; i < definition.Alternatives.Count; i++)
                _alternatives.Add(definition.Alternatives[i].AlternativeKey,
                    definition.Alternatives[i]);
            for (int i = 0; i < definition.Opportunities.Count; i++)
                _opportunities.Add(definition.Opportunities[i].OpportunityId, definition.Opportunities[i]);
            for (int i = 0; i < definition.CycleSchedule.Count; i++)
                _schedule.Add(definition.CycleSchedule[i].ScheduleEntryId, definition.CycleSchedule[i]);
            for (int i = 0; i < definition.EvidenceTemplates.Count; i++)
                _evidence.Add(definition.EvidenceTemplates[i].EvidenceTemplateId, definition.EvidenceTemplates[i]);
            for (int i = 0; i < definition.Cases.Count; i++)
                _cases.Add(definition.Cases[i].CaseId, definition.Cases[i]);
            for (int i = 0; i < definition.EvidenceActivatedCases.Count; i++)
                _evidenceActivatedCases.Add(
                    definition.EvidenceActivatedCases[i].ActivationId,
                    definition.EvidenceActivatedCases[i]);
            for (int i = 0; i < definition.OfficialStatusEffectRequests.Count; i++)
                _effects.Add(definition.OfficialStatusEffectRequests[i].EffectRequestId,
                    definition.OfficialStatusEffectRequests[i]);
            for (int i = 0; i < definition.RelianceDefinitions.Count; i++)
                _reliance.Add(definition.RelianceDefinitions[i].RelianceId, definition.RelianceDefinitions[i]);
            for (int i = 0; i < definition.RelianceRecoveries.Count; i++)
                _recoveries.Add(
                    definition.RelianceRecoveries[i].RecoveryDefinitionId,
                    definition.RelianceRecoveries[i]);
            for (int i = 0; i < definition.Appeals.Count; i++)
                _appeals.Add(definition.Appeals[i].AppealId, definition.Appeals[i]);
            for (int i = 0; i < definition.Holdings.Count; i++)
                _holdings.Add(definition.Holdings[i].HoldingId, definition.Holdings[i]);
            for (int i = 0; i < definition.HoldingCitations.Count; i++)
                _holdingCitations.Add(
                    definition.HoldingCitations[i].CitationId,
                    definition.HoldingCitations[i]);
            for (int i = 0; i < definition.DescendantCases.Count; i++)
                _descendants.Add(definition.DescendantCases[i].DescendantDefinitionId,
                    definition.DescendantCases[i]);
            for (int i = 0; i < definition.ExclusiveEntitlements.Count; i++)
                _entitlements.Add(definition.ExclusiveEntitlements[i].EntitlementId,
                    definition.ExclusiveEntitlements[i]);
            for (int i = 0; i < definition.EntitlementTransfers.Count; i++)
                _transfers.Add(definition.EntitlementTransfers[i].TransferId,
                    definition.EntitlementTransfers[i]);
        }

        public ScenarioParticipantRoleDefinition GetRole(string id) => Get(_roles, id, "role");
        public ScenarioLivedIncidentSeedDefinition GetLivedIncidentSeed(string id) =>
            Get(_incidents, id, "lived incident seed");
        public ScenarioInitialEconomicAccountDefinition GetInitialEconomicAccount(string id) =>
            Get(_accounts, id, "initial economic account");
        public ScenarioAlternativeDefinition GetAlternative(string id) =>
            Get(_alternatives, id, "alternative");
        public ScenarioOpportunityDefinition GetOpportunity(string id) => Get(_opportunities, id, "opportunity");
        public ScenarioCycleScheduleEntry GetScheduleEntry(string id) => Get(_schedule, id, "schedule entry");
        public ScenarioEvidenceTemplateDefinition GetEvidenceTemplate(string id) => Get(_evidence, id, "evidence template");
        public ScenarioCaseDefinition GetCase(string id) => Get(_cases, id, "case");
        public ScenarioEvidenceActivatedCaseDefinition GetEvidenceActivatedCase(string id) =>
            Get(_evidenceActivatedCases, id, "evidence-activated case");
        public ScenarioOfficialStatusEffectRequest GetStatusEffect(string id) => Get(_effects, id, "status effect");
        public ScenarioIrreversibleRelianceDefinition GetReliance(string id) => Get(_reliance, id, "reliance");
        public ScenarioRelianceRecoveryDefinition GetRelianceRecovery(string id) =>
            Get(_recoveries, id, "reliance recovery");
        public ScenarioAppealDefinition GetAppeal(string id) => Get(_appeals, id, "appeal");
        public ScenarioHoldingDefinition GetHolding(string id) => Get(_holdings, id, "holding");
        public ScenarioHoldingCitationDefinition GetHoldingCitation(string id) =>
            Get(_holdingCitations, id, "holding citation");
        public ScenarioActionCausedDescendantCaseDefinition GetDescendant(string id) => Get(_descendants, id, "descendant");
        public ScenarioExclusiveEntitlementDefinition GetEntitlement(string id) => Get(_entitlements, id, "entitlement");
        public ScenarioExclusiveEntitlementTransferDefinition GetTransfer(string id) => Get(_transfers, id, "transfer");

        private static T Get<T>(SortedDictionary<string, T> values, string id, string kind)
        {
            if (string.IsNullOrWhiteSpace(id) || !values.TryGetValue(id, out T value))
                throw new KeyNotFoundException($"Unknown scenario {kind} id '{id}'.");
            return value;
        }

    }
}
