using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Assessor-only material state. It records what physically exists and happened;
    /// it is deliberately separate from official ownership, evidence and public reports.
    /// </summary>
    [Serializable]
    internal sealed class InstitutionalMaterialWorld
    {
        internal const int CurrentSchemaVersion = 1;
        internal const string CurrentRulesetVersion = "institutional-material-world-v1";

        internal int SchemaVersion = CurrentSchemaVersion;
        internal string RulesetVersion = CurrentRulesetVersion;
        internal List<MaterialResourceState> Resources = new();
        internal List<OfficialOwnershipState> OfficialOwnerships = new();
        internal List<MaterialAccessGrantState> AccessGrants = new();
        internal List<MaterialAuthorityGrantState> AuthorityGrants = new();
        internal List<CollectiveCommitmentState> CollectiveCommitments = new();
        internal List<MaterialWorldEvent> EventLedger = new();

        internal MaterialResourceState GetResource(string resourceId)
            => Find(Resources, value => value.ResourceId, resourceId);

        internal OfficialOwnershipState GetOfficialOwnership(string resourceId)
        {
            for (int i = 0; i < OfficialOwnerships.Count; i++)
            {
                OfficialOwnershipState ownership = OfficialOwnerships[i];
                if (string.Equals(ownership.ResourceId, resourceId, StringComparison.Ordinal))
                    return ownership;
            }

            return null;
        }

        internal MaterialAccessGrantState GetAccessGrant(string grantId)
            => Find(AccessGrants, value => value.GrantId, grantId);

        internal MaterialAuthorityGrantState GetAuthorityGrant(string grantId)
            => Find(AuthorityGrants, value => value.GrantId, grantId);

        internal CollectiveCommitmentState GetCollectiveCommitment(string commitmentId)
            => Find(CollectiveCommitments, value => value.CommitmentId, commitmentId);

        internal MaterialWorldEvent GetEvent(string eventId)
            => Find(EventLedger, value => value.EventId, eventId);

        private static T Find<T>(IReadOnlyList<T> values, Func<T, string> id, string expected)
            where T : class
        {
            if (string.IsNullOrEmpty(expected)) return null;
            for (int i = 0; i < values.Count; i++)
            {
                T value = values[i];
                if (value != null && string.Equals(id(value), expected, StringComparison.Ordinal))
                    return value;
            }

            return null;
        }
    }

    [Serializable]
    internal sealed class MaterialResourceState
    {
        internal string ResourceId;
        internal string ResourceKindId;
        internal int Quantity;
        internal string PhysicalHolderId;
        internal string LocationContextId;
    }

    /// <summary>
    /// Official ownership is institutional state, not a mirror of physical possession.
    /// A resource may therefore have a physical holder different from its registered owner.
    /// </summary>
    [Serializable]
    internal sealed class OfficialOwnershipState
    {
        internal string OwnershipRecordId;
        internal string ResourceId;
        internal string RegisteredOwnerId;
        internal string OwnershipSourceId;
        internal long RecognitionTick;
        internal bool Disputed;
    }

    [Serializable]
    internal sealed class MaterialAccessGrantState
    {
        internal string GrantId;
        internal string AgentId;
        internal string AccessKindId;
        internal string TargetId;
        internal string SourceRecordId;
        internal long ValidFromTick;
        internal long ValidUntilTick = -1;
        internal bool Active = true;
    }

    internal enum MaterialAuthorityKind
    {
        RemoveAccess,
        SuspendEmployment,
        IssueRecord,
        AlterSchedule,
    }

    [Serializable]
    internal sealed class MaterialAuthorityGrantState
    {
        internal string GrantId;
        internal string AgentId;
        internal MaterialAuthorityKind Kind;
        internal string TargetId;
        internal string SourceRecordId;
        internal long ValidFromTick;
        internal long ValidUntilTick = -1;
        internal bool Active = true;
    }

    /// <summary>
    /// A formed collective commitment. Individual organising attempts remain events;
    /// this state exists only after multiple compatible actions satisfy formation rules.
    /// </summary>
    [Serializable]
    internal sealed class CollectiveCommitmentState
    {
        internal string CommitmentId;
        internal string IssueId;
        internal string CurrentIntentionId;
        internal int Strength;
        internal long FormedTick;
        internal List<string> MemberAgentIds = new();
        internal List<string> FormationCauseEventIds = new();
    }

    internal enum MaterialWorldEventKind
    {
        PossessionTransferred,
        AccessChanged,
        AuthorityChanged,
        CollectiveCommitmentChanged,
    }

    /// <summary>
    /// Lived-event visibility. None of these values means that the institution has
    /// admitted the event as evidence or knows its authoritative interpretation.
    /// </summary>
    internal enum MaterialEventVisibility
    {
        Private,
        WitnessLimited,
        PublicContext,
    }

    [Serializable]
    internal sealed class MaterialWorldEvent
    {
        internal string EventId;
        internal string CauseDecisionId;
        internal long Tick;
        internal MaterialWorldEventKind Kind;
        internal string ActorAgentId;
        internal string TargetAgentId;
        internal string ResourceId;
        internal int Quantity;
        internal string PreviousPhysicalHolderId;
        internal string NewPhysicalHolderId;
        internal string ContextId;
        internal string StateRecordId;
        internal bool StateBefore;
        internal bool StateAfter;
        internal MaterialEventVisibility Visibility;
        internal int Secrecy;
        internal List<string> DirectWitnessAgentIds = new();
        internal List<string> PotentialRecordSourceIds = new();
        internal List<string> CauseEventIds = new();
    }

    internal sealed class PossessionTransferRequest
    {
        internal string EventId;
        internal string CauseDecisionId;
        internal long Tick;
        internal string ActorAgentId;
        internal string ResourceId;
        internal string ExpectedPhysicalHolderId;
        internal string NewPhysicalHolderId;
        internal string NewLocationContextId;
        internal MaterialEventVisibility Visibility;
        internal int Secrecy;
        internal List<string> DirectWitnessAgentIds = new();
        internal List<string> PotentialRecordSourceIds = new();
        internal List<string> CauseEventIds = new();
    }
}
