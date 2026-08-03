using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Projects authority-owned world state into bounded opportunities. The projection
    /// creates choices only; it never selects an actor's action.
    /// </summary>
    internal static class EndogenousActionOpportunityBuilder
    {
        internal const string MaterialPossessionAccessKind = "material-possession";
        internal const string ObservationAccessKind = "observe";
        internal const string RecordingAccessKind = "record-source";
        internal const string CommunicationAccessKind = "communication";
        internal const string PerceivedAdverseActionProposition =
            "perceived-adverse-action";

        internal static void Populate(
            SocietyState society,
            InstitutionalMaterialWorld world,
            SimulationInput input)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (input == null) throw new ArgumentNullException(nameof(input));
            SocietyStateValidator.Validate(society);
            InstitutionalMaterialWorldValidator.Validate(world, society);

            input.LieOpportunities ??= new List<LieOpportunity>();
            input.StealOpportunities ??= new List<StealOpportunity>();
            input.RetaliationOpportunities ??= new List<RetaliationOpportunity>();
            input.OrganiseOpportunities ??= new List<OrganiseOpportunity>();
            input.LieOpportunities.Clear();
            input.StealOpportunities.Clear();
            input.RetaliationOpportunities.Clear();
            input.OrganiseOpportunities.Clear();

            long tick = society.CurrentTick + 1;
            var agents = new List<AgentState>(society.Agents);
            agents.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
            PopulateLieOpportunities(agents, input);
            PopulateStealOpportunities(agents, world, tick, input);
            PopulateRetaliationOpportunities(agents, world, tick, input);
            PopulateOrganiseOpportunities(agents, world, tick, input);
        }

        private static void PopulateLieOpportunities(
            IReadOnlyList<AgentState> agents,
            SimulationInput input)
        {
            for (int agentIndex = 0; agentIndex < agents.Count; agentIndex++)
            {
                AgentState actor = agents[agentIndex];
                for (int beliefIndex = 0; beliefIndex < actor.Beliefs.Count; beliefIndex++)
                {
                    BeliefState belief = actor.Beliefs[beliefIndex];
                    if (belief.Confidence < 50 || string.IsNullOrWhiteSpace(belief.PropositionId))
                        continue;

                    var opportunity = new LieOpportunity
                    {
                        OpportunityId = $"assertion:{actor.StableId}:{belief.BeliefId}",
                        BeliefId = belief.BeliefId,
                        AssertionPropositionId = $"denial:{belief.PropositionId}",
                        AssertionSubjectId = belief.SubjectId,
                        AssertionObjectId = belief.ObjectId,
                        ContextId = "shared-interaction",
                        UtilityBonus = 0,
                        Visibility = EvidenceVisibility.Observable,
                    };
                    opportunity.EligibleActorIds.Add(actor.StableId);
                    for (int relationshipIndex = 0;
                         relationshipIndex < actor.Relationships.Count;
                         relationshipIndex++)
                    {
                        AddUnique(
                            opportunity.AudienceAgentIds,
                            actor.Relationships[relationshipIndex].TargetAgentId);
                    }
                    opportunity.AudienceAgentIds.Sort(StringComparer.Ordinal);
                    input.LieOpportunities.Add(opportunity);
                }
            }
        }

        private static void PopulateStealOpportunities(
            IReadOnlyList<AgentState> agents,
            InstitutionalMaterialWorld world,
            long tick,
            SimulationInput input)
        {
            var resources = new List<MaterialResourceState>(world.Resources);
            resources.Sort((left, right) => string.CompareOrdinal(left.ResourceId, right.ResourceId));
            for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
            {
                MaterialResourceState resource = resources[resourceIndex];
                var eligible = new List<string>();
                for (int agentIndex = 0; agentIndex < agents.Count; agentIndex++)
                {
                    AgentState actor = agents[agentIndex];
                    if (!string.Equals(
                            actor.StableId,
                            resource.PhysicalHolderId,
                            StringComparison.Ordinal) &&
                        InstitutionalMaterialWorldService.HasActiveAccess(
                            world,
                            actor.StableId,
                            MaterialPossessionAccessKind,
                            resource.ResourceId,
                            tick))
                    {
                        eligible.Add(actor.StableId);
                    }
                }
                if (eligible.Count == 0) continue;

                var opportunity = new StealOpportunity
                {
                    OpportunityId = $"possession:{resource.ResourceId}:{tick}",
                    ResourceId = resource.ResourceId,
                    ExpectedPhysicalHolderId = resource.PhysicalHolderId,
                    NewLocationContextId = "actor-controlled",
                    AccessGrantId = $"projected-access:{resource.ResourceId}:{tick}",
                    ProtectionStatusId =
                        EndogenousScopeEffectService.ProtectedPossessionStatusId,
                    RecognisedProtectionUtilityBonus = 80,
                    UnrecognisedExposureUtilityPenalty = 20,
                    ReliefNeed = NeedKind.Health,
                    ReliefAmount = 20,
                    UtilityBonus = 0,
                    Visibility = EvidenceVisibility.Private,
                    Secrecy = 70,
                    EligibleActorIds = eligible,
                };
                AddObservers(world, agents, resource.ResourceId, tick,
                    opportunity.DirectWitnessAgentIds);
                AddRecordSources(world, resource.ResourceId, tick,
                    opportunity.PotentialRecordSourceIds);
                if (opportunity.DirectWitnessAgentIds.Count > 0 ||
                    opportunity.PotentialRecordSourceIds.Count > 0)
                    opportunity.Visibility = EvidenceVisibility.Observable;
                input.StealOpportunities.Add(opportunity);
            }
        }

        private static void PopulateRetaliationOpportunities(
            IReadOnlyList<AgentState> agents,
            InstitutionalMaterialWorld world,
            long tick,
            SimulationInput input)
        {
            for (int agentIndex = 0; agentIndex < agents.Count; agentIndex++)
            {
                AgentState actor = agents[agentIndex];
                for (int beliefIndex = 0; beliefIndex < actor.Beliefs.Count; beliefIndex++)
                {
                    BeliefState belief = actor.Beliefs[beliefIndex];
                    if (belief.Confidence < 25 || !string.Equals(
                            belief.PropositionId,
                            PerceivedAdverseActionProposition,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    MaterialAuthorityGrantState authority = FindAuthority(
                        world, actor.StableId, belief.SubjectId, tick);
                    MaterialAccessGrantState access = FindActiveAccess(
                        world, belief.SubjectId, tick);
                    if (authority == null || access == null) continue;

                    var opportunity = new RetaliationOpportunity
                    {
                        OpportunityId =
                            $"authority:{actor.StableId}:{belief.SubjectId}:{access.GrantId}:{tick}",
                        TargetAgentId = belief.SubjectId,
                        PerceivedPriorActionBeliefId = belief.BeliefId,
                        AuthorityGrantId = authority.GrantId,
                        AffectedAccessGrantId = access.GrantId,
                        AdverseActionKindId = "remove-access",
                        PerceivedPower = authority.Active ? 100 : 0,
                        Visibility = EvidenceVisibility.Observable,
                        Secrecy = 20,
                    };
                    opportunity.EligibleActorIds.Add(actor.StableId);
                    opportunity.DirectWitnessAgentIds.Add(belief.SubjectId);
                    input.RetaliationOpportunities.Add(opportunity);
                }
            }
        }

        private static void PopulateOrganiseOpportunities(
            IReadOnlyList<AgentState> agents,
            InstitutionalMaterialWorld world,
            long tick,
            SimulationInput input)
        {
            for (int agentIndex = 0; agentIndex < agents.Count; agentIndex++)
            {
                AgentState actor = agents[agentIndex];
                for (int commitmentIndex = 0;
                     commitmentIndex < actor.Commitments.Count;
                     commitmentIndex++)
                {
                    CommitmentState commitment = actor.Commitments[commitmentIndex];
                    if (!string.Equals(commitment.Kind, "grievance", StringComparison.Ordinal) ||
                        commitment.Strength <= 0)
                    {
                        continue;
                    }

                    MaterialAccessGrantState communication = FindCommunicationAccess(
                        world, actor.StableId, tick);
                    if (communication == null) continue;
                    var opportunity = new OrganiseOpportunity
                    {
                        OpportunityId =
                            $"organise:{commitment.TargetId}:{actor.StableId}:{tick}",
                        CollectiveCommitmentId = $"collective:{commitment.TargetId}",
                        IssueId = commitment.TargetId,
                        IntentionId = $"seek-remedy:{commitment.TargetId}",
                        CommunicationContextId = communication.TargetId,
                        RequiredParticipantCount = 2,
                        UtilityBonus = 0,
                        Visibility = EvidenceVisibility.Observable,
                        Secrecy = 30,
                    };
                    opportunity.EligibleActorIds.Add(actor.StableId);
                    input.OrganiseOpportunities.Add(opportunity);
                }
            }
        }

        private static MaterialAuthorityGrantState FindAuthority(
            InstitutionalMaterialWorld world,
            string actorId,
            string targetId,
            long tick)
        {
            for (int i = 0; i < world.AuthorityGrants.Count; i++)
            {
                MaterialAuthorityGrantState grant = world.AuthorityGrants[i];
                if (grant.Active && grant.Kind == MaterialAuthorityKind.RemoveAccess &&
                    ActiveAt(grant.ValidFromTick, grant.ValidUntilTick, tick) &&
                    string.Equals(grant.AgentId, actorId, StringComparison.Ordinal) &&
                    string.Equals(grant.TargetId, targetId, StringComparison.Ordinal))
                {
                    return grant;
                }
            }
            return null;
        }

        private static MaterialAccessGrantState FindActiveAccess(
            InstitutionalMaterialWorld world,
            string agentId,
            long tick)
        {
            for (int i = 0; i < world.AccessGrants.Count; i++)
            {
                MaterialAccessGrantState grant = world.AccessGrants[i];
                if (grant.Active && ActiveAt(grant.ValidFromTick, grant.ValidUntilTick, tick) &&
                    string.Equals(grant.AgentId, agentId, StringComparison.Ordinal))
                {
                    return grant;
                }
            }
            return null;
        }

        private static MaterialAccessGrantState FindCommunicationAccess(
            InstitutionalMaterialWorld world,
            string agentId,
            long tick)
        {
            for (int i = 0; i < world.AccessGrants.Count; i++)
            {
                MaterialAccessGrantState grant = world.AccessGrants[i];
                if (grant.Active && ActiveAt(grant.ValidFromTick, grant.ValidUntilTick, tick) &&
                    string.Equals(grant.AgentId, agentId, StringComparison.Ordinal) &&
                    string.Equals(
                        grant.AccessKindId,
                        CommunicationAccessKind,
                        StringComparison.Ordinal))
                {
                    return grant;
                }
            }
            return null;
        }

        private static void AddObservers(
            InstitutionalMaterialWorld world,
            IReadOnlyList<AgentState> agents,
            string targetId,
            long tick,
            List<string> witnesses)
        {
            for (int i = 0; i < agents.Count; i++)
            {
                if (InstitutionalMaterialWorldService.HasActiveAccess(
                        world,
                        agents[i].StableId,
                        ObservationAccessKind,
                        targetId,
                        tick))
                {
                    witnesses.Add(agents[i].StableId);
                }
            }
        }

        private static void AddRecordSources(
            InstitutionalMaterialWorld world,
            string targetId,
            long tick,
            List<string> recordSources)
        {
            for (int i = 0; i < world.AccessGrants.Count; i++)
            {
                MaterialAccessGrantState grant = world.AccessGrants[i];
                if (grant.Active && ActiveAt(grant.ValidFromTick, grant.ValidUntilTick, tick) &&
                    string.Equals(grant.AccessKindId, RecordingAccessKind, StringComparison.Ordinal) &&
                    string.Equals(grant.TargetId, targetId, StringComparison.Ordinal))
                {
                    AddUnique(recordSources, grant.SourceRecordId);
                }
            }
            recordSources.Sort(StringComparer.Ordinal);
        }

        private static bool ActiveAt(long from, long until, long tick)
            => tick >= from && (until == -1 || tick <= until);

        private static void AddUnique(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return;
            values.Add(value);
        }
    }
}
