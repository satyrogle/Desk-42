using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Commits one frozen society decision pulse and then applies the selected actions
    /// to authority-owned material state. It does not create cases or official facts.
    /// </summary>
    internal sealed class EndogenousSocietyStepService
    {
        private readonly SocietySimulation _simulation;

        internal EndogenousSocietyStepService()
            : this(new SocietySimulation())
        {
        }

        internal EndogenousSocietyStepService(SocietySimulation simulation)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }

        internal SimulationStepResult Advance(
            SocietyState society,
            InstitutionalMaterialWorld world,
            SimulationInput input)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (world == null) throw new ArgumentNullException(nameof(world));
            input ??= new SimulationInput();
            InstitutionalMaterialWorldValidator.Validate(world, society);
            ValidateProjectedMaterialActions(world, society, input);

            SimulationStepResult result = _simulation.Advance(society, input);
            ApplyPossessionTransfers(society, world, input, result);
            ApplyAuthorityExercises(society, world, input, result);
            FormCollectives(society, world, result);
            SocietyStateValidator.Validate(society);
            InstitutionalMaterialWorldValidator.Validate(world, society);
            return result;
        }

        private static void ValidateProjectedMaterialActions(
            InstitutionalMaterialWorld world,
            SocietyState society,
            SimulationInput input)
        {
            long tick = society.CurrentTick + 1;
            if (input.StealOpportunities != null)
            {
                for (int i = 0; i < input.StealOpportunities.Count; i++)
                {
                    StealOpportunity opportunity = input.StealOpportunities[i];
                    if (opportunity == null) continue;
                    MaterialResourceState resource = world.GetResource(opportunity.ResourceId);
                    if (resource == null || !string.Equals(
                            resource.PhysicalHolderId,
                            opportunity.ExpectedPhysicalHolderId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Steal opportunity {opportunity.OpportunityId} is stale.");
                    }
                    for (int actorIndex = 0;
                         actorIndex < opportunity.EligibleActorIds.Count;
                         actorIndex++)
                    {
                        string actorId = opportunity.EligibleActorIds[actorIndex];
                        if (!InstitutionalMaterialWorldService.HasActiveAccess(
                                world,
                                actorId,
                                EndogenousActionOpportunityBuilder.MaterialPossessionAccessKind,
                                opportunity.ResourceId,
                                tick))
                        {
                            throw new InvalidOperationException(
                                $"Steal opportunity {opportunity.OpportunityId} exposes an actor " +
                                "without current material access.");
                        }
                    }
                }
            }
        }

        private static void ApplyPossessionTransfers(
            SocietyState society,
            InstitutionalMaterialWorld world,
            SimulationInput input,
            SimulationStepResult result)
        {
            for (int i = 0; i < result.Events.Count; i++)
            {
                SocietyEvent actionEvent = result.Events[i];
                if (actionEvent.Kind != SocietyEventKind.PossessionTransferRequested)
                    continue;
                StealOpportunity opportunity = Find(
                    input.StealOpportunities,
                    actionEvent.OpportunityId,
                    value => value.OpportunityId);
                if (opportunity == null)
                    throw new InvalidOperationException(
                        $"Selected possession action {actionEvent.OpportunityId} has no projection.");

                MaterialWorldEvent materialEvent =
                    InstitutionalMaterialWorldService.TransferPossession(
                        world,
                        society,
                        new PossessionTransferRequest
                        {
                            EventId = $"material:{actionEvent.EventId}",
                            IssueId = opportunity.IssueId,
                            CauseDecisionId = actionEvent.CauseDecisionId,
                            Tick = actionEvent.Tick,
                            ActorAgentId = actionEvent.ActorId,
                            ResourceId = opportunity.ResourceId,
                            ExpectedPhysicalHolderId = opportunity.ExpectedPhysicalHolderId,
                            NewPhysicalHolderId = actionEvent.ActorId,
                            NewLocationContextId = opportunity.NewLocationContextId,
                            Visibility = ToMaterialVisibility(opportunity.Visibility),
                            Secrecy = opportunity.Secrecy,
                            DirectWitnessAgentIds = Clone(opportunity.DirectWitnessAgentIds),
                            PotentialRecordSourceIds = Clone(
                                opportunity.PotentialRecordSourceIds),
                            CauseEventIds = new List<string> { actionEvent.EventId },
                        });
                actionEvent.RelatedEventId = materialEvent.EventId;
                ApplyNeedRelief(
                    society.GetAgent(actionEvent.ActorId),
                    opportunity.ReliefNeed,
                    opportunity.ReliefAmount,
                    actionEvent.Deltas);
            }
        }

        private static void ApplyAuthorityExercises(
            SocietyState society,
            InstitutionalMaterialWorld world,
            SimulationInput input,
            SimulationStepResult result)
        {
            for (int i = 0; i < result.Events.Count; i++)
            {
                SocietyEvent actionEvent = result.Events[i];
                if (actionEvent.Kind != SocietyEventKind.RetaliatoryAuthorityExercised)
                    continue;
                RetaliationOpportunity opportunity = Find(
                    input.RetaliationOpportunities,
                    actionEvent.OpportunityId,
                    value => value.OpportunityId);
                if (opportunity == null)
                    throw new InvalidOperationException(
                        $"Selected retaliation {actionEvent.OpportunityId} has no projection.");

                MaterialWorldEvent materialEvent =
                    InstitutionalMaterialWorldService.ExerciseAuthority(
                        world,
                        society,
                        new AuthorityExerciseRequest
                        {
                            EventId = $"material:{actionEvent.EventId}",
                            CauseDecisionId = actionEvent.CauseDecisionId,
                            Tick = actionEvent.Tick,
                            ActorAgentId = actionEvent.ActorId,
                            TargetAgentId = opportunity.TargetAgentId,
                            AuthorityGrantId = opportunity.AuthorityGrantId,
                            AffectedAccessGrantId = opportunity.AffectedAccessGrantId,
                            RequiredAuthorityKind = MaterialAuthorityKind.RemoveAccess,
                            Visibility = ToMaterialVisibility(opportunity.Visibility),
                            Secrecy = opportunity.Secrecy,
                            DirectWitnessAgentIds = Clone(opportunity.DirectWitnessAgentIds),
                            PotentialRecordSourceIds = Clone(
                                opportunity.PotentialRecordSourceIds),
                            CauseEventIds = new List<string> { actionEvent.EventId },
                        });
                actionEvent.RelatedEventId = materialEvent.EventId;
            }
        }

        private static void FormCollectives(
            SocietyState society,
            InstitutionalMaterialWorld world,
            SimulationStepResult result)
        {
            var processed = new HashSet<string>(StringComparer.Ordinal);
            var organisationEvents = new List<SocietyEvent>();
            for (int i = 0; i < result.Events.Count; i++)
                if (result.Events[i].Kind == SocietyEventKind.OrganisationProposed)
                    organisationEvents.Add(result.Events[i]);
            organisationEvents.Sort((left, right) =>
                string.CompareOrdinal(left.EventId, right.EventId));

            for (int i = 0; i < organisationEvents.Count; i++)
            {
                SocietyEvent actionEvent = organisationEvents[i];
                if (!processed.Add(actionEvent.CollectiveCommitmentId)) continue;
                var witnesses = new List<string>();
                var recordSources = new List<string>();
                for (int j = 0; j < organisationEvents.Count; j++)
                {
                    SocietyEvent compatible = organisationEvents[j];
                    if (!string.Equals(
                            compatible.CollectiveCommitmentId,
                            actionEvent.CollectiveCommitmentId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    AddUniqueRange(witnesses, compatible.DirectWitnessAgentIds);
                    AddUniqueRange(recordSources, compatible.PotentialRecordSourceIds);
                }
                witnesses.Sort(StringComparer.Ordinal);
                recordSources.Sort(StringComparer.Ordinal);

                MaterialWorldEvent materialEvent =
                    InstitutionalMaterialWorldService.FormCollectiveFromOrganisation(
                        world,
                        society,
                        new CollectiveOrganisationRequest
                        {
                            EventId =
                                $"material:collective:{actionEvent.Tick}:" +
                                actionEvent.CollectiveCommitmentId,
                            CauseDecisionId = actionEvent.CauseDecisionId,
                            Tick = actionEvent.Tick,
                            ActorAgentId = actionEvent.ActorId,
                            CollectiveCommitmentId = actionEvent.CollectiveCommitmentId,
                            IssueId = actionEvent.CollectiveIssueId,
                            IntentionId = actionEvent.CollectiveIntentionId,
                            CommunicationContextId = actionEvent.ActionContextId,
                            RequiredParticipantCount = actionEvent.RequiredParticipantCount,
                            Visibility = ToMaterialVisibility(actionEvent.Visibility),
                            Secrecy = actionEvent.ActionSecrecy,
                            DirectWitnessAgentIds = witnesses,
                            PotentialRecordSourceIds = recordSources,
                        });
                if (materialEvent == null) continue;
                for (int j = 0; j < organisationEvents.Count; j++)
                {
                    if (string.Equals(
                            organisationEvents[j].CollectiveCommitmentId,
                            actionEvent.CollectiveCommitmentId,
                            StringComparison.Ordinal))
                    {
                        organisationEvents[j].RelatedEventId = materialEvent.EventId;
                    }
                }
            }
        }

        private static MaterialEventVisibility ToMaterialVisibility(
            EvidenceVisibility visibility)
        {
            switch (visibility)
            {
                case EvidenceVisibility.Private:
                    return MaterialEventVisibility.Private;
                case EvidenceVisibility.Observable:
                    return MaterialEventVisibility.WitnessLimited;
                case EvidenceVisibility.OfficialRecord:
                    return MaterialEventVisibility.PublicContext;
                default:
                    throw new ArgumentOutOfRangeException(nameof(visibility));
            }
        }

        private static void ApplyNeedRelief(
            AgentState actor,
            NeedKind kind,
            int relief,
            List<StateDelta> deltas)
        {
            if (actor == null || relief <= 0) return;
            NeedState need = actor.GetNeed(kind);
            if (need == null) return;
            int before = need.Pressure;
            need.Pressure = InstitutionalMath.Clamp(before - relief, 0, 100);
            deltas.Add(new StateDelta
            {
                EntityId = actor.StableId,
                FieldId = $"need:{kind}:material-relief",
                Before = before,
                After = need.Pressure,
            });
        }

        private static T Find<T>(
            IReadOnlyList<T> values,
            string expected,
            Func<T, string> id)
            where T : class
        {
            if (values == null) return null;
            for (int i = 0; i < values.Count; i++)
            {
                T value = values[i];
                if (value != null && string.Equals(id(value), expected, StringComparison.Ordinal))
                    return value;
            }
            return null;
        }

        private static List<string> Clone(IReadOnlyList<string> source)
        {
            var result = new List<string>(source?.Count ?? 0);
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            return result;
        }

        private static void AddUniqueRange(List<string> target, IReadOnlyList<string> source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                bool exists = false;
                for (int j = 0; j < target.Count; j++)
                {
                    if (!string.Equals(target[j], source[i], StringComparison.Ordinal)) continue;
                    exists = true;
                    break;
                }
                if (!exists) target.Add(source[i]);
            }
        }
    }
}
