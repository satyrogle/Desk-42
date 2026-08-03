using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    public enum ScenarioOpportunityKind
    {
        Work,
        Aid,
        Appeal,
    }

    public enum ScenarioVisibilityMode
    {
        AllBoundRoles,
        ListedRoles,
        NoBoundRoles,
    }

    /// <summary>
    /// Names one of the bounded semantic participants in an irreversible reliance
    /// choice. The scenario still supplies role ids; this enum only selects which
    /// declared role receives an effect.
    /// </summary>
    public enum ScenarioRelianceEffectRecipient
    {
        RelyingRole,
        BeneficiaryRole,
        RelatedRole,
    }

    /// <summary>
    /// An all-of semantic query used to bind a scenario role. It deliberately has no
    /// direct agent-id predicate: authored operations address roles, never people.
    /// </summary>
    [Serializable]
    public sealed class ScenarioParticipantQuery
    {
        public string RequiredSpeciesId;
        public string RequiredEmployerId;
        public List<string> RequiredRecognisedStatusIds = new();
        public List<string> RequiredUnrecognisedStatusIds = new();
        public List<string> RequiredAnomalyTraitIds = new();
        public List<string> RequiredCommitmentKinds = new();
    }

    [Serializable]
    public sealed class ScenarioParticipantRoleDefinition
    {
        public string RoleId;
        public ScenarioParticipantQuery Query = new();
        public List<string> DistinctFromRoleIds = new();
    }

    /// <summary>
    /// Authoritative lived incident input. It is kept separate from observable
    /// evidence so scenario data cannot accidentally declare institutional truth.
    /// </summary>
    [Serializable]
    public sealed class ScenarioLivedIncidentSeedDefinition
    {
        public string IncidentSeedId;
        public string IncidentId;
        public long Cycle;
        public string SubjectRoleId;
        public string CauseEntityId;
        public string PropositionId;
        public NeedKind AffectedNeed;
        public int NeedPressureDelta;
    }

    [Serializable]
    public sealed class ScenarioInitialEconomicAccountDefinition
    {
        public string AccountId;
        public string OwnerRoleId;
        public int InitialCredits;
        public int CycleIncome;
    }

    [Serializable]
    public sealed class ScenarioAlternativeDefinition
    {
        public string AlternativeKey;
        public string OwnerRoleId;
        public bool InitiallyAvailable;
        public int ResourceValue;
    }

    /// <summary>
    /// A named opportunity declaration. Kind-specific fields are validated by the
    /// scenario-definition boundary; no runtime action is performed here.
    /// </summary>
    [Serializable]
    public sealed class ScenarioOpportunityDefinition
    {
        public string OpportunityId;
        public ScenarioOpportunityKind Kind;
        public string PurposeId;
        public string SourceCauseId;
        public long AvailabilityStartCycle;
        public long AvailabilityEndCycle;
        public int UtilityBonus;
        public string RequiredEmployerId;
        public string RequiredOfficialStatusId;
        public bool RequiredOfficialStatusRecognised = true;
        public string CaseId;
        public string ChallengedRulingId;
        public long HearingCycle = -1;
        public List<string> EligibleRoleIds = new();
    }

    [Serializable]
    public sealed class ScenarioCycleScheduleEntry
    {
        public string ScheduleEntryId;
        public string IncidentId;
        public long Cycle;
        public bool WorkAvailable;
        public bool AidAvailable;
        public bool DisclosureRequested;
        public bool AppealWindowOpen;
        public string OpenDocketId;
        public ScenarioVisibilityMode Visibility;
        public List<string> VisibleRoleIds = new();
        public List<string> ActiveOpportunityIds = new();
    }

    /// <summary>
    /// Declarative mapping from an observed event signature to an institutional
    /// evidence classification. EvidenceClassId is opaque and scenario-owned.
    /// </summary>
    [Serializable]
    public sealed class ScenarioEvidenceTemplateDefinition
    {
        public string EvidenceTemplateId;
        public SocietyEventKind SourceEventKind;
        public string SourceOpportunityId;
        public string RequiredPropositionId;
        public string CaseId;
        public string IssueId;
        public string EvidenceClassId;
        public EvidenceEffect Effect;
        public int Weight;
        public EvidenceVisibility Visibility;
    }

    [Serializable]
    public sealed class ScenarioCaseDefinition
    {
        public string CaseId;
        public string IssueId;
        public string ClaimantRoleId;
        public string RespondentRoleId;
        public CaseFactSet Facts = new();
        public long OpenCycle;
        public long InitialEvidenceCutoffCycle;
        public long InitialRulingCycle;
        public long AdjudicationEvidenceCutoffCycle;
        public long AdjudicationCycle;
        public string InitialPhaseId;
        public string AdjudicationPhaseId;
        public string InitialRulingId;
        public string AdjudicationRulingId;
        public int InitialScoreThreshold;
        public int ProvisionalScoreThreshold;
        public bool ProvisionalRecognitionPermitted;
        public int AdjudicationScoreThreshold;
    }

    /// <summary>
    /// Makes a case conditional on one exact projected evidence template. The
    /// template owns the event/action signature; the runtime opening must retain
    /// the resulting artifact and autonomous action provenance.
    /// </summary>
    [Serializable]
    public sealed class ScenarioEvidenceActivatedCaseDefinition
    {
        public string ActivationId;
        public string CaseId;
        public string EvidenceTemplateId;
        public long TriggerCycle;
    }

    /// <summary>
    /// A request for a generic official-status effect. Cause and target are expressed
    /// with case/ruling/role identifiers; the declaration does not mutate society.
    /// </summary>
    [Serializable]
    public sealed class ScenarioOfficialStatusEffectRequest
    {
        public string EffectRequestId;
        public long Cycle;
        public string CauseCaseId;
        public string CauseRulingId;
        public RulingDisposition RequiredRulingDisposition;
        public string TargetRoleId;
        public string StatusId;
        public bool RequestedRecognisedState;
        public int RequestedResourceDelta;
    }

    /// <summary>
    /// A keyed irreversible choice made in reliance on a declared official state.
    /// The expected state is data for a later authority service to inspect.
    /// </summary>
    [Serializable]
    public sealed class ScenarioIrreversibleRelianceDefinition
    {
        public string RelianceId;
        public long Cycle;
        public string RelyingRoleId;
        public string SourceOpportunityId;
        public SocietyActionKind SourceActionKind;
        public string EnablingEffectRequestId;
        public string EnablingRulingId;
        public string IrreversibleChoiceKey;
        public string AbandonedAlternativeKey;
        public string ExpectedStatusId;
        public bool ExpectedRecognisedState;
        public string BeneficiaryRoleId;
        public string RelatedRoleId;
        public string ResourceId;
        public List<ScenarioRelianceEffectDefinition> Effects = new();
    }

    /// <summary>
    /// One bounded physical consequence requested by scenario data. The generic
    /// reliance service owns validation, atomic application and public projection.
    /// </summary>
    [Serializable]
    public sealed class ScenarioRelianceEffectDefinition
    {
        public string EffectId;
        public ScenarioRelianceEffectRecipient Recipient;
        public int ResourceDelta;
        public MaterialConsequenceKind MaterialKind;
        public string MaterialKindId;
        public string ResourceId;
        public bool HasNeedEffect;
        public NeedKind Need;
        public int NeedPressureDelta;
    }

    /// <summary>
    /// Declares the recovery dispute created when an irreversible choice survives
    /// reversal of the ruling on which it relied.
    /// </summary>
    [Serializable]
    public sealed class ScenarioRelianceRecoveryDefinition
    {
        public string RecoveryDefinitionId;
        public string RelianceId;
        public long Cycle;
        public string TriggerReversalRulingId;
        public string CaseIdPrefix;
        public string ParentCaseId;
        public string ClaimantRoleId;
        public string RespondentRoleId;
        public string IssueId;
        public CaseFactSet Facts = new();
    }

    [Serializable]
    public sealed class ScenarioAppealDefinition
    {
        public string AppealId;
        public string CaseId;
        public string OpportunityId;
        public string AppellantRoleId;
        public long FilingCycle;
        public long HearingCycle;
        public string ChallengedRulingId;
        public string ResultingRulingId;
        public string ResultingHoldingId;
        public List<string> GroundsEvidenceTemplateIds = new();
    }

    [Serializable]
    public sealed class ScenarioHoldingDefinition
    {
        public string HoldingId;
        public string ScopeId;
        public string SourceAppealId;
        public string SourceRulingId;
        public string RuleId;
        public string IssueId;
        public long EstablishedCycle;
        public bool Retrospective;
        public CaseFactSet RequiredScopeFacts = new();
        public List<string> SupportingEvidenceTemplateIds = new();
    }

    /// <summary>
    /// Declares one optional holding citation for one exact ruling. A holding that
    /// never materialises or does not match at runtime leaves this branch absent.
    /// </summary>
    [Serializable]
    public sealed class ScenarioHoldingCitationDefinition
    {
        public string CitationId;
        public string HoldingId;
        public string TargetCaseId;
        public string TargetRulingId;
    }

    /// <summary>
    /// Declares a descendant case whose opening depends on a role's society action.
    /// The declaration contains the causal keys but does not open the case itself.
    /// </summary>
    [Serializable]
    public sealed class ScenarioActionCausedDescendantCaseDefinition
    {
        public string DescendantDefinitionId;
        public string CaseId;
        public string ParentCaseId;
        public long OpenCycle;
        public long TriggerCycle;
        public string TriggerRoleId;
        public SocietyActionKind TriggerActionKind;
        public string TriggerOpportunityId;
        public string TriggerPropositionId;
        public string OriginatingRulingId;
        public List<string> ConnectedRoleIds = new();
    }

    [Serializable]
    public sealed class ScenarioExclusiveEntitlementDefinition
    {
        public string EntitlementId;
        public string ResourceId;
        public string OfficialStatusId;
        public string InitialHolderRoleId;
        public int Units = 1;
    }

    [Serializable]
    public sealed class ScenarioExclusiveEntitlementTransferDefinition
    {
        public string TransferId;
        public long Cycle;
        public string EntitlementId;
        public string FromRoleId;
        public string ToRoleId;
        public string CauseCaseId;
        public string CauseRulingId;
        public string CauseHoldingId;
        public RulingDisposition RequiredRulingDisposition =
            (RulingDisposition)(-1);
        public MaterialConsequenceKind GainKind = MaterialConsequenceKind.ResourceGranted;
        public MaterialConsequenceKind LossKind = MaterialConsequenceKind.ResourceRevoked;
        public string GainKindId;
        public string LossKindId;
    }

    internal static class InstitutionalScenarioDerivedIds
    {
        internal const string ConnectedOutcomePairPrefix = "connected:";

        internal static string ConnectedOutcomePair(string transferId)
        {
            return $"{ConnectedOutcomePairPrefix}{transferId}";
        }
    }

    /// <summary>
    /// Complete scenario input. All fields are serializable data; execution belongs
    /// to a separate authority/runtime layer.
    /// </summary>
    [Serializable]
    public sealed class InstitutionalScenarioDefinition
    {
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion = CurrentSchemaVersion;
        public string ScenarioId;
        public string IncidentId;
        public string PrimaryCaseId;
        public long StartCycle;
        public long EndCycle;
        public SocietyState InitialSociety;
        public List<ScenarioParticipantRoleDefinition> ParticipantRoles = new();
        public List<ScenarioLivedIncidentSeedDefinition> LivedIncidentSeeds = new();
        public List<ScenarioInitialEconomicAccountDefinition> InitialEconomicAccounts = new();
        public List<ScenarioAlternativeDefinition> Alternatives = new();
        public List<ScenarioOpportunityDefinition> Opportunities = new();
        public List<ScenarioCycleScheduleEntry> CycleSchedule = new();
        public List<ScenarioEvidenceTemplateDefinition> EvidenceTemplates = new();
        public List<ScenarioCaseDefinition> Cases = new();
        public List<ScenarioEvidenceActivatedCaseDefinition> EvidenceActivatedCases = new();
        public List<ScenarioOfficialStatusEffectRequest> OfficialStatusEffectRequests = new();
        public List<ScenarioIrreversibleRelianceDefinition> RelianceDefinitions = new();
        public List<ScenarioRelianceRecoveryDefinition> RelianceRecoveries = new();
        public List<ScenarioAppealDefinition> Appeals = new();
        public List<ScenarioHoldingDefinition> Holdings = new();
        public List<ScenarioHoldingCitationDefinition> HoldingCitations = new();
        public List<ScenarioActionCausedDescendantCaseDefinition> DescendantCases = new();
        public List<ScenarioExclusiveEntitlementDefinition> ExclusiveEntitlements = new();
        public List<ScenarioExclusiveEntitlementTransferDefinition> EntitlementTransfers = new();
    }
}
