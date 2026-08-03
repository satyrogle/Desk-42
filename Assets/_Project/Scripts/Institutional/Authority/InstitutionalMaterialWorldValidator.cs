using System;
using System.Collections.Generic;
using System.Globalization;

namespace Desk42.Institutional
{
    internal static class InstitutionalMaterialWorldValidator
    {
        internal static void Validate(InstitutionalMaterialWorld world, SocietyState society)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (society == null) throw new ArgumentNullException(nameof(society));
            SocietyStateValidator.Validate(society);

            if (world.SchemaVersion != InstitutionalMaterialWorld.CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Unsupported material-world schema version {world.SchemaVersion}.");
            if (!string.Equals(
                    world.RulesetVersion,
                    InstitutionalMaterialWorld.CurrentRulesetVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unsupported material-world ruleset {world.RulesetVersion}.");
            }
            if (world.Resources == null || world.OfficialOwnerships == null ||
                world.AccessGrants == null || world.AuthorityGrants == null ||
                world.CollectiveCommitments == null || world.EventLedger == null)
            {
                throw new InvalidOperationException(
                    "Material world requires every authoritative state collection.");
            }

            HashSet<string> agentIds = AgentIds(society);
            HashSet<string> resourceIds = ValidateResources(world.Resources);
            ValidateOwnerships(world.OfficialOwnerships, resourceIds);
            ValidateAccessGrants(world.AccessGrants, agentIds);
            ValidateAuthorityGrants(world.AuthorityGrants, agentIds);
            ValidateCollectiveCommitments(world.CollectiveCommitments, agentIds);
            ValidateEvents(world, society, agentIds, resourceIds);
            ValidateCollectiveCommitmentCauses(world, society);
        }

        private static HashSet<string> AgentIds(SocietyState society)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < society.Agents.Count; i++)
                result.Add(society.Agents[i].StableId);
            return result;
        }

        private static HashSet<string> ValidateResources(
            IReadOnlyList<MaterialResourceState> resources)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < resources.Count; i++)
            {
                MaterialResourceState resource = resources[i];
                if (resource == null || !Stable(resource.ResourceId) ||
                    !ids.Add(resource.ResourceId))
                {
                    throw new InvalidOperationException(
                        "Every material resource requires a unique stable id.");
                }
                if (!Stable(resource.ResourceKindId) || !Stable(resource.LocationContextId))
                {
                    throw new InvalidOperationException(
                        $"Resource {resource.ResourceId} requires a kind and location context.");
                }
                if (resource.Quantity <= 0)
                    throw new InvalidOperationException(
                        $"Resource {resource.ResourceId} requires a positive quantity.");
                if (resource.PhysicalHolderId != null && !Stable(resource.PhysicalHolderId))
                    throw new InvalidOperationException(
                        $"Resource {resource.ResourceId} has an invalid physical holder.");
            }

            return ids;
        }

        private static void ValidateOwnerships(
            IReadOnlyList<OfficialOwnershipState> ownerships,
            HashSet<string> resourceIds)
        {
            var recordIds = new HashSet<string>(StringComparer.Ordinal);
            var ownedResources = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ownerships.Count; i++)
            {
                OfficialOwnershipState ownership = ownerships[i];
                if (ownership == null || !Stable(ownership.OwnershipRecordId) ||
                    !recordIds.Add(ownership.OwnershipRecordId))
                {
                    throw new InvalidOperationException(
                        "Every official ownership record requires a unique stable id.");
                }
                if (!resourceIds.Contains(ownership.ResourceId) ||
                    !ownedResources.Add(ownership.ResourceId))
                {
                    throw new InvalidOperationException(
                        "Official ownership must reference one unique material resource.");
                }
                if (!Stable(ownership.RegisteredOwnerId) ||
                    !Stable(ownership.OwnershipSourceId) || ownership.RecognitionTick < 0)
                {
                    throw new InvalidOperationException(
                        $"Ownership record {ownership.OwnershipRecordId} is incomplete.");
                }
            }
        }

        private static void ValidateAccessGrants(
            IReadOnlyList<MaterialAccessGrantState> grants,
            HashSet<string> agentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < grants.Count; i++)
            {
                MaterialAccessGrantState grant = grants[i];
                if (grant == null || !Stable(grant.GrantId) || !ids.Add(grant.GrantId) ||
                    !agentIds.Contains(grant.AgentId) || !Stable(grant.AccessKindId) ||
                    !Stable(grant.TargetId) || !Stable(grant.SourceRecordId))
                {
                    throw new InvalidOperationException(
                        "Every material access grant requires a unique id, known agent, " +
                        "target, kind and source record.");
                }
                ValidateInterval(grant.ValidFromTick, grant.ValidUntilTick, grant.GrantId);
            }
        }

        private static void ValidateAuthorityGrants(
            IReadOnlyList<MaterialAuthorityGrantState> grants,
            HashSet<string> agentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < grants.Count; i++)
            {
                MaterialAuthorityGrantState grant = grants[i];
                if (grant == null || !Stable(grant.GrantId) || !ids.Add(grant.GrantId) ||
                    !agentIds.Contains(grant.AgentId) || !Stable(grant.TargetId) ||
                    !Stable(grant.SourceRecordId) ||
                    !Enum.IsDefined(typeof(MaterialAuthorityKind), grant.Kind))
                {
                    throw new InvalidOperationException(
                        "Every material authority grant requires a unique id, known agent, " +
                        "defined authority, target and source record.");
                }
                ValidateInterval(grant.ValidFromTick, grant.ValidUntilTick, grant.GrantId);
            }
        }

        private static void ValidateCollectiveCommitments(
            IReadOnlyList<CollectiveCommitmentState> commitments,
            HashSet<string> agentIds)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < commitments.Count; i++)
            {
                CollectiveCommitmentState commitment = commitments[i];
                if (commitment == null || !Stable(commitment.CommitmentId) ||
                    !ids.Add(commitment.CommitmentId) || !Stable(commitment.IssueId) ||
                    !Stable(commitment.CurrentIntentionId) || commitment.FormedTick < 0)
                {
                    throw new InvalidOperationException(
                        "Every collective commitment requires a unique id, issue, intention " +
                        "and formation tick.");
                }
                Range(commitment.Strength, 0, 100, $"{commitment.CommitmentId}.strength");
                if (commitment.MemberAgentIds == null ||
                    commitment.FormationCauseEventIds == null ||
                    commitment.MemberAgentIds.Count < 2 ||
                    commitment.FormationCauseEventIds.Count < 2)
                {
                    throw new InvalidOperationException(
                        $"Collective {commitment.CommitmentId} requires multiple members and causes.");
                }
                UniqueKnown(
                    commitment.MemberAgentIds,
                    agentIds,
                    $"{commitment.CommitmentId}.member");
                UniqueStable(
                    commitment.FormationCauseEventIds,
                    $"{commitment.CommitmentId}.formation-cause");
            }
        }

        private static void ValidateEvents(
            InstitutionalMaterialWorld world,
            SocietyState society,
            HashSet<string> agentIds,
            HashSet<string> resourceIds)
        {
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var availableCauseIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < society.EventLedger.Count; i++)
                availableCauseIds.Add(society.EventLedger[i].EventId);
            long oldestRetainedSocietyTick = society.EventLedger.Count == 0
                ? long.MaxValue
                : society.EventLedger[0].Tick;

            long previousTick = -1;
            for (int i = 0; i < world.EventLedger.Count; i++)
            {
                MaterialWorldEvent materialEvent = world.EventLedger[i];
                if (materialEvent == null || !Stable(materialEvent.EventId) ||
                    !eventIds.Add(materialEvent.EventId) || materialEvent.Tick < previousTick ||
                    !agentIds.Contains(materialEvent.ActorAgentId) ||
                    !Stable(materialEvent.ContextId) ||
                    !Enum.IsDefined(typeof(MaterialWorldEventKind), materialEvent.Kind) ||
                    !Enum.IsDefined(typeof(MaterialEventVisibility), materialEvent.Visibility))
                {
                    throw new InvalidOperationException(
                        "Every material event requires a unique id, monotonic tick, known actor, " +
                        "context and defined kind/visibility.");
                }
                previousTick = materialEvent.Tick;
                Range(materialEvent.Secrecy, 0, 100, $"{materialEvent.EventId}.secrecy");

                if (materialEvent.TargetAgentId != null &&
                    !agentIds.Contains(materialEvent.TargetAgentId))
                {
                    throw new InvalidOperationException(
                        $"Material event {materialEvent.EventId} has an unknown target agent.");
                }
                if (materialEvent.ResourceId != null &&
                    !resourceIds.Contains(materialEvent.ResourceId))
                {
                    throw new InvalidOperationException(
                        $"Material event {materialEvent.EventId} references an unknown resource.");
                }
                if (materialEvent.DirectWitnessAgentIds == null ||
                    materialEvent.PotentialRecordSourceIds == null ||
                    materialEvent.CauseEventIds == null)
                {
                    throw new InvalidOperationException(
                        $"Material event {materialEvent.EventId} has incomplete observability state.");
                }
                UniqueKnown(
                    materialEvent.DirectWitnessAgentIds,
                    agentIds,
                    $"{materialEvent.EventId}.witness");
                UniqueStable(
                    materialEvent.PotentialRecordSourceIds,
                    $"{materialEvent.EventId}.record-source");
                UniqueKnownCauses(
                    materialEvent,
                    availableCauseIds,
                    oldestRetainedSocietyTick);
                ValidateKindSpecificState(world, materialEvent);
                availableCauseIds.Add(materialEvent.EventId);
            }
        }

        private static void ValidateCollectiveCommitmentCauses(
            InstitutionalMaterialWorld world,
            SocietyState society)
        {
            var causeTicks = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int i = 0; i < society.EventLedger.Count; i++)
                causeTicks.Add(society.EventLedger[i].EventId, society.EventLedger[i].Tick);
            for (int i = 0; i < world.EventLedger.Count; i++)
                causeTicks.Add(world.EventLedger[i].EventId, world.EventLedger[i].Tick);
            long oldestRetainedSocietyTick = society.EventLedger.Count == 0
                ? long.MaxValue
                : society.EventLedger[0].Tick;

            for (int commitmentIndex = 0;
                 commitmentIndex < world.CollectiveCommitments.Count;
                 commitmentIndex++)
            {
                CollectiveCommitmentState commitment =
                    world.CollectiveCommitments[commitmentIndex];
                for (int causeIndex = 0;
                     causeIndex < commitment.FormationCauseEventIds.Count;
                     causeIndex++)
                {
                    string causeId = commitment.FormationCauseEventIds[causeIndex];
                    bool retained = causeTicks.TryGetValue(
                        causeId, out long causeTick);
                    bool prunedHistoricalSocietyCause = !retained &&
                        IsPrunedHistoricalSocietyCause(
                            causeId,
                            commitment.FormedTick,
                            oldestRetainedSocietyTick);
                    if ((!retained && !prunedHistoricalSocietyCause) ||
                        retained && causeTick > commitment.FormedTick)
                    {
                        throw new InvalidOperationException(
                            $"Collective {commitment.CommitmentId} references a missing or " +
                            $"future formation cause {causeId}.");
                    }
                }
            }
        }

        private static void ValidateKindSpecificState(
            InstitutionalMaterialWorld world,
            MaterialWorldEvent materialEvent)
        {
            switch (materialEvent.Kind)
            {
                case MaterialWorldEventKind.PossessionTransferred:
                    if (!Stable(materialEvent.ResourceId) || materialEvent.Quantity <= 0 ||
                        !Stable(materialEvent.PreviousPhysicalHolderId) ||
                        !Stable(materialEvent.NewPhysicalHolderId) ||
                        string.Equals(
                            materialEvent.PreviousPhysicalHolderId,
                            materialEvent.NewPhysicalHolderId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Possession event {materialEvent.EventId} is incomplete.");
                    }
                    break;
                case MaterialWorldEventKind.AccessChanged:
                    if (world.GetAccessGrant(materialEvent.StateRecordId) == null ||
                        materialEvent.StateBefore == materialEvent.StateAfter)
                    {
                        throw new InvalidOperationException(
                            $"Access event {materialEvent.EventId} is incomplete.");
                    }
                    break;
                case MaterialWorldEventKind.AuthorityChanged:
                    if (world.GetAuthorityGrant(materialEvent.StateRecordId) == null ||
                        materialEvent.StateBefore == materialEvent.StateAfter)
                    {
                        throw new InvalidOperationException(
                            $"Authority event {materialEvent.EventId} is incomplete.");
                    }
                    break;
                case MaterialWorldEventKind.CollectiveCommitmentChanged:
                    if (world.GetCollectiveCommitment(materialEvent.StateRecordId) == null)
                    {
                        throw new InvalidOperationException(
                            $"Collective event {materialEvent.EventId} is incomplete.");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void UniqueKnownCauses(
            MaterialWorldEvent materialEvent,
            HashSet<string> availableCauseIds,
            long oldestRetainedSocietyTick)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < materialEvent.CauseEventIds.Count; i++)
            {
                string causeId = materialEvent.CauseEventIds[i];
                bool retained = availableCauseIds.Contains(causeId);
                bool prunedHistoricalSocietyCause = !retained &&
                    IsPrunedHistoricalSocietyCause(
                        causeId,
                        materialEvent.Tick,
                        oldestRetainedSocietyTick);
                if (!Stable(causeId) || !seen.Add(causeId) ||
                    (!retained && !prunedHistoricalSocietyCause))
                {
                    throw new InvalidOperationException(
                        $"Material event {materialEvent.EventId} has an invalid or future cause.");
                }
            }
        }

        internal static bool IsPrunedHistoricalSocietyCause(
            string causeId,
            long ownerTick,
            long oldestRetainedSocietyTick)
        {
            if (oldestRetainedSocietyTick == long.MaxValue ||
                string.IsNullOrWhiteSpace(causeId) ||
                !causeId.StartsWith("event:", StringComparison.Ordinal))
                return false;
            int tickStart = "event:".Length;
            int tickEnd = causeId.IndexOf(':', tickStart);
            if (tickEnd <= tickStart || !long.TryParse(
                    causeId.Substring(tickStart, tickEnd - tickStart),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long causeTick)) return false;
            return causeTick <= ownerTick &&
                   causeTick <= oldestRetainedSocietyTick;
        }

        private static void UniqueKnown(
            IReadOnlyList<string> values,
            HashSet<string> known,
            string field)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i];
                if (!Stable(value) || !known.Contains(value) || !seen.Add(value))
                    throw new InvalidOperationException($"{field} contains an invalid id.");
            }
        }

        private static void UniqueStable(IReadOnlyList<string> values, string field)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                if (!Stable(values[i]) || !seen.Add(values[i]))
                    throw new InvalidOperationException($"{field} contains an invalid id.");
            }
        }

        private static void ValidateInterval(long from, long until, string id)
        {
            if (from < 0 || (until != -1 && until < from))
                throw new InvalidOperationException($"Grant {id} has an invalid active interval.");
        }

        private static bool Stable(string value) => !string.IsNullOrWhiteSpace(value);

        private static void Range(int value, int minimum, int maximum, string field)
        {
            if (value < minimum || value > maximum)
                throw new InvalidOperationException(
                    $"{field} must be in [{minimum}, {maximum}], got {value}.");
        }
    }
}
