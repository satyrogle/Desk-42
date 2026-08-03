using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Exact-once material record of an executable player remedy. The trace stores the
    /// explicit destination rule and before/after state so public projection never has
    /// to infer whether a remedy merely existed on paper.
    /// </summary>
    [Serializable]
    internal sealed class EndogenousRemedyApplicationTrace
    {
        internal string TraceId;
        internal string RulingId;
        internal string CaseId;
        internal string RemedyDefinitionId;
        internal long AppliedTick;
        internal string ResourceId;
        internal string DestinationRuleId;
        internal string PreviousPhysicalHolderId;
        internal string NewPhysicalHolderId;
        internal string PreviousLocationContextId;
        internal string NewLocationContextId;
        internal string MaterialEventId;
        internal bool MaterialStateChanged;
    }

    /// <summary>
    /// Executes the bounded v0.1 possession remedy. Restoration always means return to
    /// the registered owner in the official ownership record; that destination is
    /// resolved and frozen before the material transition is appended.
    /// </summary>
    internal static class EndogenousRemedyEffectService
    {
        internal const string RegisteredOwnerDestinationRule =
            "destination.registered-owner";

        internal static EndogenousRemedyApplicationTrace Execute(
            SocietyState society,
            InstitutionalMaterialWorld world,
            EndogenousDocketState state,
            CommittedPlayerRuling ruling)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (ruling == null) throw new ArgumentNullException(nameof(ruling));
            InstitutionalMaterialWorldValidator.Validate(world, society);
            EndogenousDocketValidator.Validate(state, society);

            string materialRemedy = MaterialRemedy(ruling.RemedyDefinitionIds);
            if (ruling.Disposition == RulingDisposition.Denied ||
                ruling.Disposition == RulingDisposition.ReversedAndDenied ||
                materialRemedy == null)
            {
                return null;
            }

            EndogenousInstitutionalCase opened = state.GetCase(ruling.CaseId);
            if (opened == null || !SupportsMaterialRemedy(opened.IssueId, materialRemedy))
            {
                throw new InvalidOperationException(
                    "The material remedy does not match the committed issue family.");
            }

            string resourceId = ResolveSingleResourceId(state, opened);
            string traceId = $"remedy-effect:{ruling.RulingId}:{resourceId}";
            EndogenousRemedyApplicationTrace replay = FindTrace(state, traceId);
            if (replay != null)
            {
                if (!string.Equals(replay.RulingId, ruling.RulingId, StringComparison.Ordinal) ||
                    !string.Equals(replay.ResourceId, resourceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Remedy transition {traceId} already records another payload.");
                }
                return replay;
            }

            MaterialResourceState resource = world.GetResource(resourceId);
            OfficialOwnershipState ownership = world.GetOfficialOwnership(resourceId);
            if (resource == null || ownership == null ||
                string.IsNullOrWhiteSpace(ownership.RegisteredOwnerId))
            {
                throw new InvalidOperationException(
                    "Restore-possession requires a material resource and registered owner.");
            }

            string previousHolder = resource.PhysicalHolderId;
            string registeredOwner = ownership.RegisteredOwnerId;
            string eventId = $"material:{traceId}";
            string destinationContext = $"institutional-custody:{registeredOwner}";
            bool changed = !string.Equals(
                previousHolder, registeredOwner, StringComparison.Ordinal);
            if (changed && society.GetAgent(previousHolder) == null)
            {
                throw new InvalidOperationException(
                    "Restore-possession requires the current physical holder to be a known actor.");
            }

            var trace = new EndogenousRemedyApplicationTrace
            {
                TraceId = traceId,
                RulingId = ruling.RulingId,
                CaseId = ruling.CaseId,
                RemedyDefinitionId = materialRemedy,
                AppliedTick = society.CurrentTick,
                ResourceId = resourceId,
                DestinationRuleId = RegisteredOwnerDestinationRule,
                PreviousPhysicalHolderId = previousHolder,
                NewPhysicalHolderId = registeredOwner,
                PreviousLocationContextId = resource.LocationContextId,
                NewLocationContextId = changed
                    ? destinationContext
                    : resource.LocationContextId,
                MaterialEventId = changed ? eventId : string.Empty,
                MaterialStateChanged = changed,
            };
            state.RemedyApplicationTraces.Add(trace);

            try
            {
                if (changed)
                {
                    InstitutionalMaterialWorldService.TransferPossession(
                        world,
                        society,
                        new PossessionTransferRequest
                        {
                            EventId = eventId,
                            IssueId = opened.IssueId,
                            CauseDecisionId = ruling.RulingId,
                            Tick = society.CurrentTick,
                            ActorAgentId = previousHolder,
                            ResourceId = resourceId,
                            ExpectedPhysicalHolderId = previousHolder,
                            NewPhysicalHolderId = registeredOwner,
                            NewLocationContextId = destinationContext,
                            Visibility = MaterialEventVisibility.PublicContext,
                            Secrecy = 0,
                            PotentialRecordSourceIds = new List<string>
                            {
                                $"record:remedy-execution:{ruling.RulingId}",
                            },
                        });
                }
                EndogenousDocketValidator.Validate(state, society);
                InstitutionalMaterialWorldValidator.Validate(world, society);
            }
            catch
            {
                state.RemedyApplicationTraces.RemoveAt(
                    state.RemedyApplicationTraces.Count - 1);
                if (changed && world.EventLedger.Count > 0 && string.Equals(
                        world.EventLedger[world.EventLedger.Count - 1].EventId,
                        eventId,
                        StringComparison.Ordinal))
                {
                    world.EventLedger.RemoveAt(world.EventLedger.Count - 1);
                    resource.PhysicalHolderId = previousHolder;
                    resource.LocationContextId = trace.PreviousLocationContextId;
                }
                throw;
            }

            return trace;
        }

        private static string MaterialRemedy(IReadOnlyList<string> remedies)
        {
            if (Contains(remedies, EndogenousPlayerRulingService.RestorePossessionRemedy))
                return EndogenousPlayerRulingService.RestorePossessionRemedy;
            if (Contains(remedies,
                    EndogenousPlayerRulingService.RestoreIdentityContinuityRemedy))
                return EndogenousPlayerRulingService.RestoreIdentityContinuityRemedy;
            if (Contains(remedies,
                    EndogenousPlayerRulingService.GrantEmergencySupportRemedy))
                return EndogenousPlayerRulingService.GrantEmergencySupportRemedy;
            return null;
        }

        private static bool SupportsMaterialRemedy(string issueId, string remedyId)
        {
            return (string.Equals(issueId, EndogenousIssueKindIds.PossessionDispute,
                        StringComparison.Ordinal) && string.Equals(remedyId,
                        EndogenousPlayerRulingService.RestorePossessionRemedy,
                        StringComparison.Ordinal)) ||
                   (string.Equals(issueId, EndogenousIssueKindIds.IdentityContinuity,
                        StringComparison.Ordinal) && string.Equals(remedyId,
                        EndogenousPlayerRulingService.RestoreIdentityContinuityRemedy,
                        StringComparison.Ordinal)) ||
                   (string.Equals(issueId,
                        EndogenousIssueKindIds.DependencyEmergencySupport,
                        StringComparison.Ordinal) && string.Equals(remedyId,
                        EndogenousPlayerRulingService.GrantEmergencySupportRemedy,
                        StringComparison.Ordinal));
        }

        private static string ResolveSingleResourceId(
            EndogenousDocketState state,
            EndogenousInstitutionalCase opened)
        {
            string result = null;
            for (int i = 0; i < opened.ObservationIds.Count; i++)
            {
                DocketObservation observation = state.GetObservation(
                    opened.ObservationIds[i]);
                if (observation == null ||
                    string.IsNullOrWhiteSpace(observation.OfficialResourceId))
                {
                    continue;
                }
                if (result != null && !string.Equals(
                        result,
                        observation.OfficialResourceId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Restore-possession cannot choose between multiple case resources.");
                }
                result = observation.OfficialResourceId;
            }
            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidOperationException(
                    "Restore-possession requires one officially identified case resource.");
            return result;
        }

        private static EndogenousRemedyApplicationTrace FindTrace(
            EndogenousDocketState state,
            string traceId)
        {
            for (int i = 0; i < state.RemedyApplicationTraces.Count; i++)
            {
                if (string.Equals(
                        state.RemedyApplicationTraces[i].TraceId,
                        traceId,
                        StringComparison.Ordinal))
                {
                    return state.RemedyApplicationTraces[i];
                }
            }
            return null;
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
