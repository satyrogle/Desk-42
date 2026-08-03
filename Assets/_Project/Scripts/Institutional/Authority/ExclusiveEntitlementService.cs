using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// A bounded registry of exclusive, conserved entitlements. The identifiers are
    /// deliberately opaque: this model can represent a cartridge, permit, berth,
    /// ration, or any other resource that has at most one recognised holder.
    /// </summary>
    internal sealed class ExclusiveEntitlementRegistry
    {
        internal const int MaximumEntries = 256;

        private readonly List<ExclusiveEntitlementState> entries = new();

        internal int Count => entries.Count;

        internal ExclusiveEntitlementState Find(
            string entitlementId,
            string resourceId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ExclusiveEntitlementState entry = entries[i];
                if (string.Equals(entry.EntitlementId, entitlementId,
                        StringComparison.Ordinal) &&
                    string.Equals(entry.ResourceId, resourceId,
                        StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        internal bool ContainsHolderStatus(string holderStatusId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].HolderStatusId, holderStatusId,
                    StringComparison.Ordinal)) return true;
            }

            return false;
        }

        internal void Add(ExclusiveEntitlementState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (entries.Count >= MaximumEntries)
                throw new InvalidOperationException(
                    $"Exclusive entitlement registry is limited to {MaximumEntries} entries.");
            entries.Add(state);
        }
    }

    internal sealed class ExclusiveEntitlementState
    {
        internal ExclusiveEntitlementState(
            string entitlementId,
            string resourceId,
            string holderStatusId,
            int conservedAmount,
            string currentHolderAgentId,
            string lastMutationCauseId)
        {
            EntitlementId = entitlementId;
            ResourceId = resourceId;
            HolderStatusId = holderStatusId;
            ConservedAmount = conservedAmount;
            CurrentHolderAgentId = currentHolderAgentId;
            LastMutationCauseId = lastMutationCauseId;
        }

        internal string EntitlementId { get; }
        internal string ResourceId { get; }
        internal string HolderStatusId { get; }
        internal int ConservedAmount { get; }
        internal string CurrentHolderAgentId { get; private set; }
        internal string LastMutationCauseId { get; private set; }

        internal void RecordHolderChange(string holderAgentId, string causeId)
        {
            CurrentHolderAgentId = holderAgentId;
            LastMutationCauseId = causeId;
        }
    }

    /// <summary>
    /// Explicit result for assignment, release, transfer, and no-op requests.
    /// Paired material consequences are present only when one real agent displaces
    /// another; assignment from or release to the unheld state invents no counterparty.
    /// </summary>
    internal sealed class ExclusiveEntitlementTransferResult
    {
        internal bool Changed;
        internal string EntitlementId;
        internal string ResourceId;
        internal string PreviousHolderAgentId;
        internal string CurrentHolderAgentId;
        internal int ConservedAmount;
        internal StatusMutationResult PreviousHolderMutation;
        internal StatusMutationResult CurrentHolderMutation;
        internal MaterialConsequence GainConsequence;
        internal MaterialConsequence LossConsequence;
    }

    /// <summary>
    /// Performs deterministic changes to an exclusive entitlement without knowing
    /// the scenario's participants, payment type, or resource meaning.
    /// </summary>
    internal static class ExclusiveEntitlementService
    {
        internal const int MaximumIdentifierLength = 128;
        internal const int MaximumConservedAmount = 1_000_000;

        /// <summary>
        /// Attaches a registry to entitlement state already authored in the initial
        /// society. No ruling or mutation is invented for state that predates cycle
        /// one; exactly one declared holder must already carry the holder status.
        /// </summary>
        internal static ExclusiveEntitlementState RegisterInitialState(
            ExclusiveEntitlementRegistry registry,
            InstitutionalConsequenceRun run,
            string entitlementId,
            string resourceId,
            string holderStatusId,
            int conservedAmount,
            string initialHolderAgentId,
            string initialCauseId = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (run == null || run.Report == null || run.FinalSocietyState == null)
                throw new InvalidOperationException(
                    "Initial entitlement registration requires report and society state.");
            ValidateIdentifier(entitlementId, nameof(entitlementId));
            ValidateIdentifier(resourceId, nameof(resourceId));
            ValidateIdentifier(holderStatusId, nameof(holderStatusId));
            ValidateAmount(conservedAmount);
            AgentState holder = FindOptionalAgent(run, initialHolderAgentId);
            if (holder == null)
                throw new InvalidOperationException(
                    "An authored initial entitlement requires one declared holder.");
            if (registry.Find(entitlementId, resourceId) != null ||
                registry.ContainsHolderStatus(holderStatusId))
            {
                throw new InvalidOperationException(
                    $"Exclusive entitlement {entitlementId}/{resourceId} is already registered.");
            }
            if (registry.Count >= ExclusiveEntitlementRegistry.MaximumEntries)
                throw new InvalidOperationException(
                    $"Exclusive entitlement registry is limited to " +
                    $"{ExclusiveEntitlementRegistry.MaximumEntries} entries.");
            EnsureObservationKeyAvailable(run.Report, entitlementId, resourceId);

            var state = new ExclusiveEntitlementState(
                entitlementId,
                resourceId,
                holderStatusId,
                conservedAmount,
                holder.StableId,
                initialCauseId);
            AssertHolderInvariant(run, state);
            var observation = new ExclusiveEntitlementObservation
            {
                EntitlementId = entitlementId,
                ResourceId = resourceId,
                HolderStatusId = holderStatusId,
                ConservedAmount = conservedAmount,
                CurrentHolderAgentId = holder.StableId,
                LastMutationCauseId = initialCauseId,
            };

            registry.Add(state);
            run.Report.ExclusiveEntitlements.Add(observation);
            return state;
        }

        internal static ExclusiveEntitlementState Register(
            ExclusiveEntitlementRegistry registry,
            InstitutionalConsequenceRun run,
            Ruling ruling,
            string entitlementId,
            string resourceId,
            string holderStatusId,
            int conservedAmount,
            string initialHolderAgentId)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            ValidateRunAndRuling(run, ruling);
            ValidateIdentifier(entitlementId, nameof(entitlementId));
            ValidateIdentifier(resourceId, nameof(resourceId));
            ValidateIdentifier(holderStatusId, nameof(holderStatusId));
            ValidateAmount(conservedAmount);

            if (registry.Find(entitlementId, resourceId) != null)
                throw new InvalidOperationException(
                    $"Exclusive entitlement {entitlementId}/{resourceId} is already registered.");
            if (registry.ContainsHolderStatus(holderStatusId))
                throw new InvalidOperationException(
                    $"Holder status {holderStatusId} is already bound to another entitlement.");
            if (registry.Count >= ExclusiveEntitlementRegistry.MaximumEntries)
                throw new InvalidOperationException(
                    $"Exclusive entitlement registry is limited to " +
                    $"{ExclusiveEntitlementRegistry.MaximumEntries} entries.");

            AgentState initialHolder = FindOptionalAgent(run, initialHolderAgentId);
            EnsureStatusIsUnrecognisedByEveryone(run, holderStatusId);

            StatusMutationResult initialMutation = null;
            if (initialHolder != null)
            {
                initialMutation = InstitutionalStatusMutationService.Apply(
                    run,
                    ruling,
                    initialHolder.StableId,
                    holderStatusId,
                    true,
                    0);
                if (!initialMutation.Changed)
                    throw new InvalidOperationException(
                        $"Initial holder status {holderStatusId} did not change.");
            }

            var state = new ExclusiveEntitlementState(
                entitlementId,
                resourceId,
                holderStatusId,
                conservedAmount,
                initialHolder?.StableId,
                initialMutation == null ? null : ruling.RulingId);
            registry.Add(state);
            run.Report.ExclusiveEntitlements.Add(new ExclusiveEntitlementObservation
            {
                EntitlementId = entitlementId,
                ResourceId = resourceId,
                HolderStatusId = holderStatusId,
                ConservedAmount = conservedAmount,
                CurrentHolderAgentId = initialHolder?.StableId,
                LastMutationCauseId = initialMutation == null ? null : ruling.RulingId,
            });
            AssertHolderInvariant(run, state);
            return state;
        }

        internal static ExclusiveEntitlementTransferResult ChangeHolder(
            ExclusiveEntitlementRegistry registry,
            InstitutionalConsequenceRun run,
            Ruling ruling,
            string entitlementId,
            string resourceId,
            string expectedCurrentHolderAgentId,
            string newHolderAgentId,
            MaterialConsequenceKind gainKind,
            MaterialConsequenceKind lossKind,
            string gainKindId = null,
            string lossKindId = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            ValidateRunAndRuling(run, ruling);
            ValidateIdentifier(entitlementId, nameof(entitlementId));
            ValidateIdentifier(resourceId, nameof(resourceId));

            ExclusiveEntitlementState state = registry.Find(entitlementId, resourceId);
            if (state == null)
                throw new InvalidOperationException(
                    $"Unknown exclusive entitlement {entitlementId}/{resourceId}.");

            AssertHolderInvariant(run, state);
            ExclusiveEntitlementObservation observation = FindObservation(
                run.Report,
                state.EntitlementId,
                state.ResourceId);
            ValidateObservationMatchesState(observation, state);
            if (!string.Equals(state.CurrentHolderAgentId,
                    expectedCurrentHolderAgentId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Stale entitlement transfer for {entitlementId}/{resourceId}: " +
                    $"expected holder {Describe(expectedCurrentHolderAgentId)}, " +
                    $"actual holder {Describe(state.CurrentHolderAgentId)}.");
            }

            AgentState newHolder = FindOptionalAgent(run, newHolderAgentId);
            string previousHolderId = state.CurrentHolderAgentId;
            if (string.Equals(previousHolderId, newHolder?.StableId,
                StringComparison.Ordinal))
            {
                return NoOp(state);
            }

            int nextMutationIndex = run.Report.OfficialStatusMutations.Count;
            if (!string.IsNullOrEmpty(previousHolderId))
            {
                InstitutionalStatusMutationService.EnsureMutationIdAvailable(
                    run.Report,
                    InstitutionalStatusMutationService.BuildMutationId(
                        ruling,
                        nextMutationIndex,
                        previousHolderId,
                        state.HolderStatusId));
                nextMutationIndex++;
            }
            if (newHolder != null)
            {
                InstitutionalStatusMutationService.EnsureMutationIdAvailable(
                    run.Report,
                    InstitutionalStatusMutationService.BuildMutationId(
                        ruling,
                        nextMutationIndex,
                        newHolder.StableId,
                        state.HolderStatusId));
            }

            if (!string.IsNullOrEmpty(previousHolderId) && newHolder != null)
            {
                EnsureMaterialIdAvailable(
                    run.Report,
                    BuildMaterialConsequenceId(
                        run.Report,
                        ruling,
                        state,
                        newHolder.StableId,
                        gainKind,
                        run.Report.MaterialConsequences.Count));
                EnsureMaterialIdAvailable(
                    run.Report,
                    BuildMaterialConsequenceId(
                        run.Report,
                        ruling,
                        state,
                        previousHolderId,
                        lossKind,
                        run.Report.MaterialConsequences.Count + 1));
            }

            StatusMutationResult previousMutation = null;
            if (!string.IsNullOrEmpty(previousHolderId))
            {
                previousMutation = InstitutionalStatusMutationService.Apply(
                    run,
                    ruling,
                    previousHolderId,
                    state.HolderStatusId,
                    false,
                    0);
                if (!previousMutation.Changed)
                    throw new InvalidOperationException(
                        $"Previous holder {previousHolderId} was not displaced.");
            }

            StatusMutationResult currentMutation = null;
            if (newHolder != null)
            {
                currentMutation = InstitutionalStatusMutationService.Apply(
                    run,
                    ruling,
                    newHolder.StableId,
                    state.HolderStatusId,
                    true,
                    0);
                if (!currentMutation.Changed)
                    throw new InvalidOperationException(
                        $"New holder {newHolder.StableId} was not recognised.");
            }

            MaterialConsequence gain = null;
            MaterialConsequence loss = null;
            if (!string.IsNullOrEmpty(previousHolderId) && newHolder != null)
            {
                gain = AddMaterialConsequence(
                    run.Report,
                    ruling,
                    state,
                    newHolder.StableId,
                    gainKind,
                    gainKindId,
                    state.ConservedAmount);
                loss = AddMaterialConsequence(
                    run.Report,
                    ruling,
                    state,
                    previousHolderId,
                    lossKind,
                    lossKindId,
                    -state.ConservedAmount);
                if (gain.ResourceDelta + loss.ResourceDelta != 0)
                    throw new InvalidOperationException(
                        $"Transfer of {entitlementId}/{resourceId} was not conserved.");
            }

            state.RecordHolderChange(newHolder?.StableId, ruling.RulingId);
            observation.CurrentHolderAgentId = state.CurrentHolderAgentId;
            observation.LastMutationCauseId = ruling.RulingId;
            AssertHolderInvariant(run, state);

            return new ExclusiveEntitlementTransferResult
            {
                Changed = true,
                EntitlementId = state.EntitlementId,
                ResourceId = state.ResourceId,
                PreviousHolderAgentId = previousHolderId,
                CurrentHolderAgentId = state.CurrentHolderAgentId,
                ConservedAmount = state.ConservedAmount,
                PreviousHolderMutation = previousMutation,
                CurrentHolderMutation = currentMutation,
                GainConsequence = gain,
                LossConsequence = loss,
            };
        }

        internal static void AssertHolderInvariant(
            InstitutionalConsequenceRun run,
            ExclusiveEntitlementState state)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (run.FinalSocietyState == null)
                throw new InvalidOperationException("Entitlement run has no society state.");

            int recognisedCount = 0;
            string recognisedHolderId = null;
            for (int i = 0; i < run.FinalSocietyState.Agents.Count; i++)
            {
                AgentState agent = run.FinalSocietyState.Agents[i];
                if (!agent.Standing.IsRecognised(state.HolderStatusId)) continue;
                recognisedCount++;
                recognisedHolderId = agent.StableId;
            }

            int expectedCount = string.IsNullOrEmpty(state.CurrentHolderAgentId) ? 0 : 1;
            if (recognisedCount != expectedCount ||
                (expectedCount == 1 && !string.Equals(
                    recognisedHolderId,
                    state.CurrentHolderAgentId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Exclusive entitlement {state.EntitlementId}/{state.ResourceId} " +
                    $"has holder {Describe(state.CurrentHolderAgentId)} but " +
                    $"{recognisedCount} recognised holder status record(s).");
            }
        }

        private static ExclusiveEntitlementTransferResult NoOp(
            ExclusiveEntitlementState state)
        {
            return new ExclusiveEntitlementTransferResult
            {
                Changed = false,
                EntitlementId = state.EntitlementId,
                ResourceId = state.ResourceId,
                PreviousHolderAgentId = state.CurrentHolderAgentId,
                CurrentHolderAgentId = state.CurrentHolderAgentId,
                ConservedAmount = state.ConservedAmount,
            };
        }

        private static MaterialConsequence AddMaterialConsequence(
            InstitutionalConsequenceReport report,
            Ruling ruling,
            ExclusiveEntitlementState state,
            string agentId,
            MaterialConsequenceKind kind,
            string kindId,
            int delta)
        {
            var consequence = new MaterialConsequence
            {
                ConsequenceId = BuildMaterialConsequenceId(
                    report,
                    ruling,
                    state,
                    agentId,
                    kind,
                    report.MaterialConsequences.Count),
                Cycle = ruling.Cycle,
                CauseId = ruling.RulingId,
                AgentId = agentId,
                Kind = kind,
                KindId = string.IsNullOrWhiteSpace(kindId) ? kind.ToString() : kindId,
                ResourceId = state.ResourceId,
                ResourceDelta = delta,
            };
            report.MaterialConsequences.Add(consequence);
            return consequence;
        }

        private static ExclusiveEntitlementObservation FindObservation(
            InstitutionalConsequenceReport report,
            string entitlementId,
            string resourceId)
        {
            ExclusiveEntitlementObservation matched = null;
            int matches = 0;
            for (int i = 0; i < report.ExclusiveEntitlements.Count; i++)
            {
                ExclusiveEntitlementObservation observation =
                    report.ExclusiveEntitlements[i];
                if (string.Equals(observation.EntitlementId, entitlementId,
                        StringComparison.Ordinal) &&
                    string.Equals(observation.ResourceId, resourceId,
                        StringComparison.Ordinal))
                {
                    matched = observation;
                    matches++;
                }
            }
            if (matches != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one public entitlement observation " +
                    $"{entitlementId}/{resourceId}, found {matches}.");
            }
            return matched;
        }

        private static void ValidateObservationMatchesState(
            ExclusiveEntitlementObservation observation,
            ExclusiveEntitlementState state)
        {
            if (!string.Equals(observation.HolderStatusId, state.HolderStatusId,
                    StringComparison.Ordinal) ||
                observation.ConservedAmount != state.ConservedAmount ||
                !string.Equals(observation.CurrentHolderAgentId,
                    state.CurrentHolderAgentId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Public entitlement observation {state.EntitlementId}/" +
                    $"{state.ResourceId} does not match authoritative state.");
            }
        }

        internal static string BuildMaterialConsequenceId(
            InstitutionalConsequenceReport report,
            Ruling ruling,
            ExclusiveEntitlementState state,
            string agentId,
            MaterialConsequenceKind kind,
            int index)
        {
            return $"material:{ruling.Cycle}:{index}:" +
                   $"{agentId}:{kind}:{state.EntitlementId}:{state.ResourceId}";
        }

        private static void EnsureMaterialIdAvailable(
            InstitutionalConsequenceReport report,
            string consequenceId)
        {
            for (int i = 0; i < report.MaterialConsequences.Count; i++)
            {
                if (string.Equals(report.MaterialConsequences[i]?.ConsequenceId,
                        consequenceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Material consequence id '{consequenceId}' already exists.");
                }
            }
        }

        private static void EnsureObservationKeyAvailable(
            InstitutionalConsequenceReport report,
            string entitlementId,
            string resourceId)
        {
            if (report.ExclusiveEntitlements == null)
                throw new InvalidOperationException(
                    "Initial entitlement registration requires an initialized public observation collection.");
            for (int i = 0; i < report.ExclusiveEntitlements.Count; i++)
            {
                ExclusiveEntitlementObservation observation =
                    report.ExclusiveEntitlements[i];
                if (observation != null &&
                    string.Equals(observation.EntitlementId, entitlementId,
                        StringComparison.Ordinal) &&
                    string.Equals(observation.ResourceId, resourceId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Public entitlement observation {entitlementId}/{resourceId} " +
                        "is already registered.");
                }
            }
        }

        private static void ValidateRunAndRuling(
            InstitutionalConsequenceRun run,
            Ruling ruling)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.Report == null)
                throw new InvalidOperationException("Entitlement run has no public report.");
            if (run.FinalSocietyState == null)
                throw new InvalidOperationException("Entitlement run has no society state.");
            if (ruling == null) throw new ArgumentNullException(nameof(ruling));
            ValidateIdentifier(ruling.RulingId, "ruling.RulingId");
            if (run.Report.Rulings == null ||
                run.Report.OfficialStatusMutations == null ||
                run.Report.MaterialConsequences == null ||
                run.Report.ExclusiveEntitlements == null ||
                run.Report.Timeline == null ||
                ruling.OfficialStatusMutationIds == null)
            {
                throw new InvalidOperationException(
                    "Entitlement transfer requires initialized report and ruling collections.");
            }
            int rulingMatches = 0;
            bool exactRuling = false;
            for (int i = 0; i < run.Report.Rulings.Count; i++)
            {
                Ruling registered = run.Report.Rulings[i];
                if (registered == null || !string.Equals(
                        registered.RulingId, ruling.RulingId, StringComparison.Ordinal))
                    continue;
                rulingMatches++;
                exactRuling |= ReferenceEquals(registered, ruling);
            }
            if (rulingMatches != 1 || !exactRuling)
                throw new InvalidOperationException(
                    "Entitlement cause must be the unique ruling registered in the report.");
        }

        private static AgentState FindOptionalAgent(
            InstitutionalConsequenceRun run,
            string agentId)
        {
            if (string.IsNullOrEmpty(agentId)) return null;
            ValidateIdentifier(agentId, nameof(agentId));
            AgentState agent = run.FinalSocietyState.GetAgent(agentId);
            if (agent == null)
                throw new InvalidOperationException($"Unknown entitlement holder {agentId}.");
            return agent;
        }

        private static void EnsureStatusIsUnrecognisedByEveryone(
            InstitutionalConsequenceRun run,
            string holderStatusId)
        {
            for (int i = 0; i < run.FinalSocietyState.Agents.Count; i++)
            {
                AgentState agent = run.FinalSocietyState.Agents[i];
                if (agent.Standing.IsRecognised(holderStatusId))
                {
                    throw new InvalidOperationException(
                        $"Holder status {holderStatusId} is already recognised for " +
                        $"{agent.StableId}.");
                }
            }
        }

        private static void ValidateIdentifier(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Identifier cannot be empty.", name);
            if (value.Length > MaximumIdentifierLength)
                throw new ArgumentOutOfRangeException(
                    name,
                    $"Identifier cannot exceed {MaximumIdentifierLength} characters.");
        }

        private static void ValidateAmount(int conservedAmount)
        {
            if (conservedAmount <= 0 || conservedAmount > MaximumConservedAmount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(conservedAmount),
                    $"Conserved amount must be between 1 and {MaximumConservedAmount}.");
            }
        }

        private static string Describe(string holderId)
        {
            return string.IsNullOrEmpty(holderId) ? "<none>" : holderId;
        }
    }
}
