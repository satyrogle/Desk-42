using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional
{
    internal sealed class ScenarioRunStateInitializationResult
    {
        internal ScenarioRunStateInitializationResult(
            bool applied,
            IDictionary<string, EconomicAccountState> accountsById,
            IDictionary<string, AlternativeOptionState> alternativesByKey,
            IDictionary<string, int> alternativeResourceValuesByKey)
        {
            Applied = applied;
            AccountsById = new ReadOnlyDictionary<string, EconomicAccountState>(
                new Dictionary<string, EconomicAccountState>(accountsById, StringComparer.Ordinal));
            AlternativesByKey = new ReadOnlyDictionary<string, AlternativeOptionState>(
                new Dictionary<string, AlternativeOptionState>(alternativesByKey, StringComparer.Ordinal));
            AlternativeResourceValuesByKey = new ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(alternativeResourceValuesByKey, StringComparer.Ordinal));
        }

        internal bool Applied { get; }
        internal IReadOnlyDictionary<string, EconomicAccountState> AccountsById { get; }
        internal IReadOnlyDictionary<string, AlternativeOptionState> AlternativesByKey { get; }
        internal IReadOnlyDictionary<string, int> AlternativeResourceValuesByKey { get; }
    }

    internal sealed class ScenarioLivedIncidentSeedApplication
    {
        internal ScenarioLivedIncidentSeedApplication(
            string incidentSeedId,
            string livedEventId,
            bool applied,
            int needPressureBefore,
            int needPressureAfter,
            IReadOnlyList<string> linkedBeliefIds)
        {
            IncidentSeedId = incidentSeedId;
            LivedEventId = livedEventId;
            Applied = applied;
            NeedPressureBefore = needPressureBefore;
            NeedPressureAfter = needPressureAfter;
            LinkedBeliefIds = new ReadOnlyCollection<string>(new List<string>(linkedBeliefIds));
        }

        internal string IncidentSeedId { get; }
        internal string LivedEventId { get; }
        internal bool Applied { get; }
        internal int NeedPressureBefore { get; }
        internal int NeedPressureAfter { get; }
        internal IReadOnlyList<string> LinkedBeliefIds { get; }
    }

    internal sealed class ScenarioLivedIncidentSeedBatchResult
    {
        internal ScenarioLivedIncidentSeedBatchResult(
            long cycle,
            IReadOnlyList<ScenarioLivedIncidentSeedApplication> applications)
        {
            Cycle = cycle;
            Applications = new ReadOnlyCollection<ScenarioLivedIncidentSeedApplication>(
                new List<ScenarioLivedIncidentSeedApplication>(applications));
            for (int i = 0; i < Applications.Count; i++) Applied |= Applications[i].Applied;
        }

        internal long Cycle { get; }
        internal bool Applied { get; }
        internal IReadOnlyList<ScenarioLivedIncidentSeedApplication> Applications { get; }
    }

    /// <summary>
    /// Creates only scenario-declared economic state, then applies authoritative lived
    /// incident seeds at their declared cycle. It never creates evidence, rulings,
    /// entitlements or truth-bearing public report fields.
    /// </summary>
    internal static class InstitutionalScenarioStateInitializer
    {
        private sealed class PendingIncident
        {
            internal ScenarioLivedIncidentSeedDefinition Definition;
            internal AgentState Subject;
            internal NeedState Need;
            internal string LivedEventId;
            internal LivedEvent ExistingEvent;
            internal List<string> MatchingBeliefIds;
        }

        internal static ScenarioRunStateInitializationResult InitialiseDeclaredState(
            InstitutionalScenarioDefinition definition,
            SocietyState clonedSociety,
            InstitutionalConsequenceRun run,
            IReadOnlyDictionary<string, string> agentIdByRole)
        {
            ValidateCommon(definition, clonedSociety, run, agentIdByRole);
            if (clonedSociety.CurrentTick != definition.StartCycle)
            {
                throw new InvalidOperationException(
                    "Scenario run-state initialization must occur at the declared start cycle.");
            }

            Dictionary<string, string> bindings = CaptureAndValidateBindings(
                definition, clonedSociety, agentIdByRole);
            ValidateStateCollections(run);

            var accountsById = new Dictionary<string, EconomicAccountState>(StringComparer.Ordinal);
            var alternativesByKey = new Dictionary<string, AlternativeOptionState>(StringComparer.Ordinal);
            var alternativeResourceValuesByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Alternatives.Count; i++)
            {
                alternativeResourceValuesByKey.Add(
                    definition.Alternatives[i].AlternativeKey,
                    definition.Alternatives[i].ResourceValue);
            }
            bool hasExistingState = run.EconomicAccounts.Count != 0 || run.AlternativeOptions.Count != 0;

            if (hasExistingState)
            {
                CaptureEquivalentExistingState(
                    definition, run, bindings, accountsById, alternativesByKey);
                bool attachedExistingSociety = AttachSociety(run, clonedSociety);
                return new ScenarioRunStateInitializationResult(
                    attachedExistingSociety,
                    accountsById,
                    alternativesByKey,
                    alternativeResourceValuesByKey);
            }

            bool attachedSociety = AttachSociety(run, clonedSociety);

            for (int i = 0; i < definition.InitialEconomicAccounts.Count; i++)
            {
                ScenarioInitialEconomicAccountDefinition declared =
                    definition.InitialEconomicAccounts[i];
                var account = new EconomicAccountState
                {
                    AgentId = bindings[declared.OwnerRoleId],
                    AvailableCredits = declared.InitialCredits,
                    CommittedIncome = declared.CycleIncome,
                };
                run.EconomicAccounts.Add(account);
                accountsById.Add(declared.AccountId, account);
            }

            for (int i = 0; i < definition.Alternatives.Count; i++)
            {
                ScenarioAlternativeDefinition declared = definition.Alternatives[i];
                var alternative = new AlternativeOptionState
                {
                    OptionId = declared.AlternativeKey,
                    AgentId = bindings[declared.OwnerRoleId],
                    Available = declared.InitiallyAvailable,
                    ChangedByActionEventId = null,
                };
                run.AlternativeOptions.Add(alternative);
                alternativesByKey.Add(declared.AlternativeKey, alternative);
            }

            return new ScenarioRunStateInitializationResult(
                attachedSociety || definition.InitialEconomicAccounts.Count != 0 ||
                definition.Alternatives.Count != 0,
                accountsById,
                alternativesByKey,
                alternativeResourceValuesByKey);
        }

        internal static ScenarioLivedIncidentSeedBatchResult ApplyLivedIncidentSeeds(
            InstitutionalScenarioDefinition definition,
            SocietyState clonedSociety,
            InstitutionalConsequenceRun run,
            IReadOnlyDictionary<string, string> agentIdByRole,
            long cycle)
        {
            ValidateCommon(definition, clonedSociety, run, agentIdByRole);
            if (cycle < definition.StartCycle || cycle > definition.EndCycle)
                throw new ArgumentOutOfRangeException(nameof(cycle));
            bool atDeclaredCycle = clonedSociety.CurrentTick == cycle;
            bool immediatelyBeforeDeclaredCycle =
                cycle > definition.StartCycle && clonedSociety.CurrentTick == cycle - 1;
            if (!atDeclaredCycle && !immediatelyBeforeDeclaredCycle)
            {
                throw new InvalidOperationException(
                    $"Requested incident cycle {cycle} must be applied either immediately " +
                    $"before that cycle's decisions or while society is at that cycle; " +
                    $"society tick is {clonedSociety.CurrentTick}.");
            }
            if (!ReferenceEquals(run.FinalSocietyState, clonedSociety))
            {
                throw new InvalidOperationException(
                    "Incident seeding requires the society attached during run-state initialization.");
            }

            Dictionary<string, string> bindings = CaptureAndValidateBindings(
                definition, clonedSociety, agentIdByRole);
            ValidateStateCollections(run);
            ValidateNoDuplicateAuthoritativeIds(run);

            var pending = new List<PendingIncident>();
            for (int i = 0; i < definition.LivedIncidentSeeds.Count; i++)
            {
                ScenarioLivedIncidentSeedDefinition seed = definition.LivedIncidentSeeds[i];
                if (seed.Cycle != cycle) continue;
                string subjectAgentId = bindings[seed.SubjectRoleId];
                AgentState subject = clonedSociety.GetAgent(subjectAgentId) ??
                    throw new InvalidOperationException(
                        $"Incident seed '{seed.IncidentSeedId}' references missing agent " +
                        $"'{subjectAgentId}'.");
                NeedState need = subject.GetNeed(seed.AffectedNeed) ??
                    throw new InvalidOperationException(
                        $"Incident seed '{seed.IncidentSeedId}' requires missing need " +
                        $"'{seed.AffectedNeed}' on agent '{subjectAgentId}'.");
                string livedEventId = $"lived:{seed.IncidentSeedId}";
                LivedEvent existing = FindAuthoritativeEvent(run, livedEventId);
                List<string> beliefIds = MatchingBeliefIds(subject, seed.PropositionId);
                if (existing != null)
                    ValidateEquivalentAppliedSeed(run, seed, subjectAgentId, existing, beliefIds);
                else
                    ValidateUnusedAuthorityKeys(run, livedEventId);

                pending.Add(new PendingIncident
                {
                    Definition = seed,
                    Subject = subject,
                    Need = need,
                    LivedEventId = livedEventId,
                    ExistingEvent = existing,
                    MatchingBeliefIds = beliefIds,
                });
            }

            if (atDeclaredCycle && cycle > definition.StartCycle)
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    if (pending[i].ExistingEvent == null)
                    {
                        throw new InvalidOperationException(
                            $"Incident seed '{pending[i].Definition.IncidentSeedId}' " +
                            "was not applied before its cycle's decisions.");
                    }
                }
            }

            var results = new List<ScenarioLivedIncidentSeedApplication>(pending.Count);
            for (int i = 0; i < pending.Count; i++)
            {
                PendingIncident item = pending[i];
                int before = item.Need.Pressure;
                if (item.ExistingEvent != null)
                {
                    results.Add(new ScenarioLivedIncidentSeedApplication(
                        item.Definition.IncidentSeedId,
                        item.LivedEventId,
                        false,
                        before,
                        before,
                        item.MatchingBeliefIds));
                    continue;
                }

                var lived = new LivedEvent
                {
                    LivedEventId = item.LivedEventId,
                    Cycle = item.Definition.Cycle,
                    EventKindId = item.Definition.IncidentId,
                    SubjectAgentId = item.Subject.StableId,
                    CauseEntityId = item.Definition.CauseEntityId,
                    AffectedNeed = item.Definition.AffectedNeed,
                    NeedPressureDelta = item.Definition.NeedPressureDelta,
                };
                run.AuthoritativeEvents.Add(lived);
                item.Need.Pressure = InstitutionalMath.Clamp(
                    before + item.Definition.NeedPressureDelta, 0, 100);

                for (int j = 0; j < item.MatchingBeliefIds.Count; j++)
                {
                    run.AuthoritativeBeliefLinks.Add(new AuthoritativeBeliefLink
                    {
                        LivedEventId = item.LivedEventId,
                        AgentId = item.Subject.StableId,
                        BeliefId = item.MatchingBeliefIds[j],
                    });
                }

                results.Add(new ScenarioLivedIncidentSeedApplication(
                    item.Definition.IncidentSeedId,
                    item.LivedEventId,
                    true,
                    before,
                    item.Need.Pressure,
                    item.MatchingBeliefIds));
            }

            return new ScenarioLivedIncidentSeedBatchResult(cycle, results);
        }

        private static void ValidateCommon(
            InstitutionalScenarioDefinition definition,
            SocietyState society,
            InstitutionalConsequenceRun run,
            IReadOnlyDictionary<string, string> bindings)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            InstitutionalScenarioDefinitionValidator.Validate(definition);
            SocietyStateValidator.Validate(society);
            if (ReferenceEquals(definition.InitialSociety, society))
                throw new InvalidOperationException("Scenario execution requires a detached society clone.");
            if (run.Report == null)
                throw new InvalidOperationException("Scenario run requires a public report.");
        }

        private static Dictionary<string, string> CaptureAndValidateBindings(
            InstitutionalScenarioDefinition definition,
            SocietyState society,
            IReadOnlyDictionary<string, string> source)
        {
            if (source.Count != definition.ParticipantRoles.Count)
                throw new InvalidOperationException("Role bindings must contain exactly the declared roles.");

            InstitutionalScenarioParticipantBindings expected =
                InstitutionalScenarioParticipantBinder.Bind(definition);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.ParticipantRoles.Count; i++)
            {
                string roleId = definition.ParticipantRoles[i].RoleId;
                if (!source.TryGetValue(roleId, out string agentId) ||
                    string.IsNullOrWhiteSpace(agentId))
                {
                    throw new InvalidOperationException($"Missing binding for role '{roleId}'.");
                }
                string expectedAgentId = expected.GetAgent(roleId).StableId;
                if (!string.Equals(expectedAgentId, agentId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Role '{roleId}' is bound to wrong agent '{agentId}'.");
                }
                if (society.GetAgent(agentId) == null)
                    throw new InvalidOperationException($"Bound agent '{agentId}' is missing from society.");
                result.Add(roleId, agentId);
            }
            return result;
        }

        private static bool AttachSociety(InstitutionalConsequenceRun run, SocietyState society)
        {
            if (run.FinalSocietyState == null)
            {
                run.FinalSocietyState = society;
                return true;
            }
            if (!ReferenceEquals(run.FinalSocietyState, society))
                throw new InvalidOperationException("Run is already attached to a different society state.");
            return false;
        }

        private static void ValidateStateCollections(InstitutionalConsequenceRun run)
        {
            if (run.EconomicAccounts == null)
                throw new InvalidOperationException("Scenario run is missing its economic-account collection.");
            if (run.AlternativeOptions == null)
                throw new InvalidOperationException("Scenario run is missing its alternative collection.");
            if (run.AuthoritativeEvents == null || run.AuthoritativeBeliefLinks == null ||
                run.AuthoritativeEvidenceLinks == null)
            {
                throw new InvalidOperationException("Scenario run is missing authoritative collections.");
            }
            if (run.Report.Timeline == null || run.Report.EvidenceArtifacts == null)
                throw new InvalidOperationException("Scenario report is missing required collections.");
        }

        private static void CaptureEquivalentExistingState(
            InstitutionalScenarioDefinition definition,
            InstitutionalConsequenceRun run,
            IReadOnlyDictionary<string, string> bindings,
            IDictionary<string, EconomicAccountState> accountsById,
            IDictionary<string, AlternativeOptionState> alternativesByKey)
        {
            if (run.EconomicAccounts.Count != definition.InitialEconomicAccounts.Count ||
                run.AlternativeOptions.Count != definition.Alternatives.Count)
            {
                throw new InvalidOperationException(
                    "Run contains partial, duplicate, or undeclared initial economic state.");
            }

            var accountOwners = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < run.EconomicAccounts.Count; i++)
            {
                EconomicAccountState row = run.EconomicAccounts[i] ??
                    throw new InvalidOperationException("Economic-account rows cannot be null.");
                if (!accountOwners.Add(row.AgentId))
                    throw new InvalidOperationException("Duplicate economic-account owner.");
            }
            for (int i = 0; i < definition.InitialEconomicAccounts.Count; i++)
            {
                ScenarioInitialEconomicAccountDefinition declared =
                    definition.InitialEconomicAccounts[i];
                string owner = bindings[declared.OwnerRoleId];
                EconomicAccountState row = FindAccount(run, owner);
                if (row == null || row.AvailableCredits != declared.InitialCredits ||
                    row.CommittedIncome != declared.CycleIncome)
                {
                    throw new InvalidOperationException(
                        $"Initial economic account '{declared.AccountId}' is missing or conflicting.");
                }
                accountsById.Add(declared.AccountId, row);
            }

            var alternativeKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < run.AlternativeOptions.Count; i++)
            {
                AlternativeOptionState row = run.AlternativeOptions[i] ??
                    throw new InvalidOperationException("Alternative rows cannot be null.");
                if (!alternativeKeys.Add(row.OptionId))
                    throw new InvalidOperationException("Duplicate alternative key.");
            }
            for (int i = 0; i < definition.Alternatives.Count; i++)
            {
                ScenarioAlternativeDefinition declared = definition.Alternatives[i];
                AlternativeOptionState row = FindAlternative(run, declared.AlternativeKey);
                if (row == null ||
                    !string.Equals(row.AgentId, bindings[declared.OwnerRoleId], StringComparison.Ordinal) ||
                    row.Available != declared.InitiallyAvailable ||
                    !string.IsNullOrEmpty(row.ChangedByActionEventId))
                {
                    throw new InvalidOperationException(
                        $"Initial alternative '{declared.AlternativeKey}' is missing or conflicting.");
                }
                alternativesByKey.Add(declared.AlternativeKey, row);
            }
        }

        private static void ValidateNoDuplicateAuthoritativeIds(InstitutionalConsequenceRun run)
        {
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < run.AuthoritativeEvents.Count; i++)
            {
                LivedEvent lived = run.AuthoritativeEvents[i] ??
                    throw new InvalidOperationException("Authoritative lived events cannot be null.");
                if (!eventIds.Add(lived.LivedEventId))
                    throw new InvalidOperationException("Duplicate authoritative lived-event id.");
            }
        }

        private static void ValidateEquivalentAppliedSeed(
            InstitutionalConsequenceRun run,
            ScenarioLivedIncidentSeedDefinition seed,
            string subjectAgentId,
            LivedEvent existing,
            IReadOnlyList<string> expectedBeliefIds)
        {
            if (existing.Cycle != seed.Cycle ||
                !string.Equals(existing.EventKindId, seed.IncidentId, StringComparison.Ordinal) ||
                !string.Equals(existing.SubjectAgentId, subjectAgentId, StringComparison.Ordinal) ||
                !string.Equals(existing.CauseEntityId, seed.CauseEntityId, StringComparison.Ordinal) ||
                existing.AffectedNeed != seed.AffectedNeed ||
                existing.NeedPressureDelta != seed.NeedPressureDelta)
            {
                throw new InvalidOperationException(
                    $"Incident seed '{seed.IncidentSeedId}' collides with a different lived event.");
            }

            var linkedBeliefs = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < run.AuthoritativeBeliefLinks.Count; i++)
            {
                AuthoritativeBeliefLink link = run.AuthoritativeBeliefLinks[i];
                if (!string.Equals(link.LivedEventId, existing.LivedEventId,
                    StringComparison.Ordinal)) continue;
                if (!string.Equals(link.AgentId, subjectAgentId, StringComparison.Ordinal) ||
                    !linkedBeliefs.Add(link.BeliefId))
                    throw new InvalidOperationException("Incident seed has conflicting belief links.");
            }
            if (linkedBeliefs.Count != expectedBeliefIds.Count)
                throw new InvalidOperationException("Incident seed belief links are incomplete.");
            for (int i = 0; i < expectedBeliefIds.Count; i++)
            {
                if (!linkedBeliefs.Contains(expectedBeliefIds[i]))
                    throw new InvalidOperationException("Incident seed belief links are conflicting.");
            }

        }

        private static void ValidateUnusedAuthorityKeys(
            InstitutionalConsequenceRun run,
            string livedEventId)
        {
            for (int i = 0; i < run.AuthoritativeBeliefLinks.Count; i++)
            {
                if (string.Equals(run.AuthoritativeBeliefLinks[i].LivedEventId,
                    livedEventId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Belief link references a missing lived event.");
            }
            for (int i = 0; i < run.AuthoritativeEvidenceLinks.Count; i++)
            {
                if (string.Equals(run.AuthoritativeEvidenceLinks[i].LivedEventId,
                    livedEventId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Evidence link references a missing lived event.");
            }
        }

        private static List<string> MatchingBeliefIds(AgentState subject, string propositionId)
        {
            var result = new List<string>();
            for (int i = 0; i < subject.Beliefs.Count; i++)
            {
                BeliefState belief = subject.Beliefs[i];
                if (string.Equals(belief.PropositionId, propositionId, StringComparison.Ordinal) &&
                    string.Equals(belief.SubjectId, subject.StableId, StringComparison.Ordinal))
                    result.Add(belief.BeliefId);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static LivedEvent FindAuthoritativeEvent(
            InstitutionalConsequenceRun run,
            string livedEventId)
        {
            for (int i = 0; i < run.AuthoritativeEvents.Count; i++)
            {
                if (string.Equals(run.AuthoritativeEvents[i].LivedEventId,
                    livedEventId, StringComparison.Ordinal)) return run.AuthoritativeEvents[i];
            }
            return null;
        }

        private static EconomicAccountState FindAccount(
            InstitutionalConsequenceRun run,
            string agentId)
        {
            for (int i = 0; i < run.EconomicAccounts.Count; i++)
            {
                if (string.Equals(run.EconomicAccounts[i].AgentId,
                    agentId, StringComparison.Ordinal)) return run.EconomicAccounts[i];
            }
            return null;
        }

        private static AlternativeOptionState FindAlternative(
            InstitutionalConsequenceRun run,
            string optionId)
        {
            for (int i = 0; i < run.AlternativeOptions.Count; i++)
            {
                if (string.Equals(run.AlternativeOptions[i].OptionId,
                    optionId, StringComparison.Ordinal)) return run.AlternativeOptions[i];
            }
            return null;
        }
    }
}
