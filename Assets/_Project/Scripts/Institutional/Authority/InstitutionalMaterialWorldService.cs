using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal static class InstitutionalMaterialWorldService
    {
        /// <summary>
        /// Transfers one complete material resource entity. The operation changes
        /// physical possession only; the official ownership record is never inferred
        /// from, or rewritten by, this transition.
        /// </summary>
        internal static MaterialWorldEvent TransferPossession(
            InstitutionalMaterialWorld world,
            SocietyState society,
            PossessionTransferRequest request)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (request == null) throw new ArgumentNullException(nameof(request));
            InstitutionalMaterialWorldValidator.Validate(world, society);

            MaterialWorldEvent replay = world.GetEvent(request.EventId);
            if (replay != null)
            {
                AssertReplayMatches(replay, request);
                return replay;
            }

            ValidateRequest(world, society, request);
            MaterialResourceState resource = world.GetResource(request.ResourceId);
            if (!string.Equals(
                    resource.PhysicalHolderId,
                    request.ExpectedPhysicalHolderId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Resource {resource.ResourceId} is held by {resource.PhysicalHolderId}, " +
                    $"not expected holder {request.ExpectedPhysicalHolderId}.");
            }
            if (string.Equals(
                    request.ExpectedPhysicalHolderId,
                    request.NewPhysicalHolderId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A possession transfer requires distinct previous and new holders.");
            }
            if (world.EventLedger.Count > 0 &&
                world.EventLedger[world.EventLedger.Count - 1].Tick > request.Tick)
            {
                throw new InvalidOperationException(
                    "Material events must be appended in nondecreasing tick order.");
            }

            var materialEvent = new MaterialWorldEvent
            {
                EventId = request.EventId,
                CauseDecisionId = request.CauseDecisionId,
                Tick = request.Tick,
                Kind = MaterialWorldEventKind.PossessionTransferred,
                ActorAgentId = request.ActorAgentId,
                ResourceId = request.ResourceId,
                Quantity = resource.Quantity,
                PreviousPhysicalHolderId = request.ExpectedPhysicalHolderId,
                NewPhysicalHolderId = request.NewPhysicalHolderId,
                ContextId = request.NewLocationContextId,
                Visibility = request.Visibility,
                Secrecy = request.Secrecy,
                DirectWitnessAgentIds = Clone(request.DirectWitnessAgentIds),
                PotentialRecordSourceIds = Clone(request.PotentialRecordSourceIds),
                CauseEventIds = Clone(request.CauseEventIds),
            };

            string previousHolder = resource.PhysicalHolderId;
            string previousLocation = resource.LocationContextId;
            resource.PhysicalHolderId = request.NewPhysicalHolderId;
            resource.LocationContextId = request.NewLocationContextId;
            world.EventLedger.Add(materialEvent);
            try
            {
                InstitutionalMaterialWorldValidator.Validate(world, society);
            }
            catch
            {
                world.EventLedger.RemoveAt(world.EventLedger.Count - 1);
                resource.PhysicalHolderId = previousHolder;
                resource.LocationContextId = previousLocation;
                throw;
            }

            return materialEvent;
        }

        internal static bool HasActiveAccess(
            InstitutionalMaterialWorld world,
            string agentId,
            string accessKindId,
            string targetId,
            long tick)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            for (int i = 0; i < world.AccessGrants.Count; i++)
            {
                MaterialAccessGrantState grant = world.AccessGrants[i];
                if (grant.Active && ActiveAt(grant.ValidFromTick, grant.ValidUntilTick, tick) &&
                    string.Equals(grant.AgentId, agentId, StringComparison.Ordinal) &&
                    string.Equals(grant.AccessKindId, accessKindId, StringComparison.Ordinal) &&
                    string.Equals(grant.TargetId, targetId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasActiveAuthority(
            InstitutionalMaterialWorld world,
            string agentId,
            MaterialAuthorityKind kind,
            string targetId,
            long tick)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            for (int i = 0; i < world.AuthorityGrants.Count; i++)
            {
                MaterialAuthorityGrantState grant = world.AuthorityGrants[i];
                if (grant.Active && grant.Kind == kind &&
                    ActiveAt(grant.ValidFromTick, grant.ValidUntilTick, tick) &&
                    string.Equals(grant.AgentId, agentId, StringComparison.Ordinal) &&
                    string.Equals(grant.TargetId, targetId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateRequest(
            InstitutionalMaterialWorld world,
            SocietyState society,
            PossessionTransferRequest request)
        {
            if (!Stable(request.EventId) || !Stable(request.CauseDecisionId) ||
                request.Tick < 0 || society.GetAgent(request.ActorAgentId) == null ||
                world.GetResource(request.ResourceId) == null ||
                !Stable(request.ExpectedPhysicalHolderId) ||
                !Stable(request.NewPhysicalHolderId) ||
                !Stable(request.NewLocationContextId) ||
                !Enum.IsDefined(typeof(MaterialEventVisibility), request.Visibility) ||
                request.Secrecy < 0 || request.Secrecy > 100 ||
                request.DirectWitnessAgentIds == null ||
                request.PotentialRecordSourceIds == null || request.CauseEventIds == null)
            {
                throw new InvalidOperationException("Possession transfer request is incomplete.");
            }

            var witnesses = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < request.DirectWitnessAgentIds.Count; i++)
            {
                string witnessId = request.DirectWitnessAgentIds[i];
                if (society.GetAgent(witnessId) == null || !witnesses.Add(witnessId))
                    throw new InvalidOperationException(
                        "Possession transfer has an unknown or duplicate direct witness.");
            }

            UniqueStable(request.PotentialRecordSourceIds, "record source");
            UniqueStable(request.CauseEventIds, "cause event");
            for (int i = 0; i < request.CauseEventIds.Count; i++)
            {
                string causeId = request.CauseEventIds[i];
                if (world.GetEvent(causeId) == null &&
                    !HasSocietyEvent(society, causeId))
                {
                    throw new InvalidOperationException(
                        $"Possession transfer references unavailable cause {causeId}.");
                }
            }
        }

        private static void AssertReplayMatches(
            MaterialWorldEvent existing,
            PossessionTransferRequest request)
        {
            if (existing.Kind != MaterialWorldEventKind.PossessionTransferred ||
                !string.Equals(existing.CauseDecisionId, request.CauseDecisionId, StringComparison.Ordinal) ||
                existing.Tick != request.Tick ||
                !string.Equals(existing.ActorAgentId, request.ActorAgentId, StringComparison.Ordinal) ||
                !string.Equals(existing.ResourceId, request.ResourceId, StringComparison.Ordinal) ||
                !string.Equals(
                    existing.PreviousPhysicalHolderId,
                    request.ExpectedPhysicalHolderId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    existing.NewPhysicalHolderId,
                    request.NewPhysicalHolderId,
                    StringComparison.Ordinal) ||
                !string.Equals(existing.ContextId, request.NewLocationContextId, StringComparison.Ordinal) ||
                existing.Visibility != request.Visibility || existing.Secrecy != request.Secrecy ||
                !SequenceEqual(existing.DirectWitnessAgentIds, request.DirectWitnessAgentIds) ||
                !SequenceEqual(existing.PotentialRecordSourceIds, request.PotentialRecordSourceIds) ||
                !SequenceEqual(existing.CauseEventIds, request.CauseEventIds))
            {
                throw new InvalidOperationException(
                    $"Material event id {request.EventId} was already committed with another payload.");
            }
        }

        private static bool ActiveAt(long from, long until, long tick)
            => tick >= from && (until == -1 || tick <= until);

        private static bool HasSocietyEvent(SocietyState society, string eventId)
        {
            for (int i = 0; i < society.EventLedger.Count; i++)
            {
                if (string.Equals(society.EventLedger[i].EventId, eventId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static List<string> Clone(IReadOnlyList<string> source)
        {
            var result = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            return result;
        }

        private static bool SequenceEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
            }

            return true;
        }

        private static void UniqueStable(IReadOnlyList<string> values, string description)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                if (!Stable(values[i]) || !ids.Add(values[i]))
                    throw new InvalidOperationException(
                        $"Possession transfer has an invalid or duplicate {description}.");
            }
        }

        private static bool Stable(string value) => !string.IsNullOrWhiteSpace(value);
    }
}
