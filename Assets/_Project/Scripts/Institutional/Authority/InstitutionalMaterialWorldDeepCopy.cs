using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal static class InstitutionalMaterialWorldDeepCopy
    {
        internal static InstitutionalMaterialWorld Copy(InstitutionalMaterialWorld source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new InstitutionalMaterialWorld
            {
                SchemaVersion = source.SchemaVersion,
                RulesetVersion = source.RulesetVersion,
                Resources = new List<MaterialResourceState>(source.Resources.Count),
                OfficialOwnerships = new List<OfficialOwnershipState>(
                    source.OfficialOwnerships.Count),
                AccessGrants = new List<MaterialAccessGrantState>(source.AccessGrants.Count),
                AuthorityGrants = new List<MaterialAuthorityGrantState>(
                    source.AuthorityGrants.Count),
                CollectiveCommitments = new List<CollectiveCommitmentState>(
                    source.CollectiveCommitments.Count),
                EventLedger = new List<MaterialWorldEvent>(source.EventLedger.Count),
            };

            for (int i = 0; i < source.Resources.Count; i++)
            {
                MaterialResourceState value = source.Resources[i];
                copy.Resources.Add(new MaterialResourceState
                {
                    ResourceId = value.ResourceId,
                    ResourceKindId = value.ResourceKindId,
                    Quantity = value.Quantity,
                    PhysicalHolderId = value.PhysicalHolderId,
                    LocationContextId = value.LocationContextId,
                });
            }
            for (int i = 0; i < source.OfficialOwnerships.Count; i++)
            {
                OfficialOwnershipState value = source.OfficialOwnerships[i];
                copy.OfficialOwnerships.Add(new OfficialOwnershipState
                {
                    OwnershipRecordId = value.OwnershipRecordId,
                    ResourceId = value.ResourceId,
                    RegisteredOwnerId = value.RegisteredOwnerId,
                    OwnershipSourceId = value.OwnershipSourceId,
                    RecognitionTick = value.RecognitionTick,
                    Disputed = value.Disputed,
                });
            }
            for (int i = 0; i < source.AccessGrants.Count; i++)
            {
                MaterialAccessGrantState value = source.AccessGrants[i];
                copy.AccessGrants.Add(new MaterialAccessGrantState
                {
                    GrantId = value.GrantId,
                    AgentId = value.AgentId,
                    AccessKindId = value.AccessKindId,
                    TargetId = value.TargetId,
                    SourceRecordId = value.SourceRecordId,
                    ValidFromTick = value.ValidFromTick,
                    ValidUntilTick = value.ValidUntilTick,
                    Active = value.Active,
                });
            }
            for (int i = 0; i < source.AuthorityGrants.Count; i++)
            {
                MaterialAuthorityGrantState value = source.AuthorityGrants[i];
                copy.AuthorityGrants.Add(new MaterialAuthorityGrantState
                {
                    GrantId = value.GrantId,
                    AgentId = value.AgentId,
                    Kind = value.Kind,
                    TargetId = value.TargetId,
                    SourceRecordId = value.SourceRecordId,
                    ValidFromTick = value.ValidFromTick,
                    ValidUntilTick = value.ValidUntilTick,
                    Active = value.Active,
                });
            }
            for (int i = 0; i < source.CollectiveCommitments.Count; i++)
            {
                CollectiveCommitmentState value = source.CollectiveCommitments[i];
                copy.CollectiveCommitments.Add(new CollectiveCommitmentState
                {
                    CommitmentId = value.CommitmentId,
                    IssueId = value.IssueId,
                    CurrentIntentionId = value.CurrentIntentionId,
                    Strength = value.Strength,
                    FormedTick = value.FormedTick,
                    MemberAgentIds = Clone(value.MemberAgentIds),
                    FormationCauseEventIds = Clone(value.FormationCauseEventIds),
                });
            }
            for (int i = 0; i < source.EventLedger.Count; i++)
            {
                MaterialWorldEvent value = source.EventLedger[i];
                copy.EventLedger.Add(new MaterialWorldEvent
                {
                    EventId = value.EventId,
                    IssueId = value.IssueId,
                    CauseDecisionId = value.CauseDecisionId,
                    Tick = value.Tick,
                    Kind = value.Kind,
                    ActorAgentId = value.ActorAgentId,
                    TargetAgentId = value.TargetAgentId,
                    ResourceId = value.ResourceId,
                    Quantity = value.Quantity,
                    PreviousPhysicalHolderId = value.PreviousPhysicalHolderId,
                    NewPhysicalHolderId = value.NewPhysicalHolderId,
                    ContextId = value.ContextId,
                    StateRecordId = value.StateRecordId,
                    StateBefore = value.StateBefore,
                    StateAfter = value.StateAfter,
                    Visibility = value.Visibility,
                    Secrecy = value.Secrecy,
                    DirectWitnessAgentIds = Clone(value.DirectWitnessAgentIds),
                    PotentialRecordSourceIds = Clone(value.PotentialRecordSourceIds),
                    CauseEventIds = Clone(value.CauseEventIds),
                });
            }

            return copy;
        }

        private static List<string> Clone(IReadOnlyList<string> source)
        {
            var result = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            return result;
        }
    }
}
