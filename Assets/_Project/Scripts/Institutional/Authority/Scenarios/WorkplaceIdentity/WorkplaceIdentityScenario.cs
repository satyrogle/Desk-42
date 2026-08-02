using System;
using System.Collections.Generic;

namespace Desk42.Institutional.Scenarios.WorkplaceIdentity
{
    /// <summary>
    /// Data-only employment-continuity control scenario for the shared
    /// institutional simulation contracts.
    /// </summary>
    public static partial class WorkplaceIdentityScenario
    {
        public const string ScenarioId = "scenario.workplace.identity-continuity";
        public const string IncidentId = "incident.workplace.identity-record-fracture";
        public const string EmployerId = "employer.helix-foundry";
        public const string PrimaryCaseId = "case.workplace.primary-identity";
        public const string SuccessorCaseId = "case.workplace.successor-shift";
        public const string IssueId = "issue.workplace.identity-continuity";

        public const string ContingentHolderRoleId =
            "role.workplace.contingent-holder";
        public const string DependentRoleId = "role.workplace.dependent";
        public const string EmployerRoleId = "role.workplace.employer";
        public const string LaterClaimantRoleId =
            "role.workplace.later-claimant";
        public const string PrimaryClaimantRoleId =
            "role.workplace.primary-claimant";

        public const string PrimaryClaimantAgentId = "agent.workplace.elias-vale";
        public const string DependentAgentId = "agent.workplace.nia-vale";
        public const string EmployerAgentId = "agent.workplace.arden-pike";
        public const string ContingentHolderAgentId = "agent.workplace.mara-quill";
        public const string LaterClaimantAgentId = "agent.workplace.ivo-reed";

        public const string IdentityAnomalyTraitId =
            "anomaly.workplace.official-status-echo";
        public const string IdentityPropositionId =
            "proposition.workplace.employment-survived-identity-change";
        public const string EvidenceTemplateId =
            "evidence-template.workplace.identity-disclosure";
        public const string EvidenceClassId =
            "evidence-class.workplace.identity-testimony";

        public const string AppealOpportunityId =
            "opportunity.workplace.appeal-primary";
        public const string AidOpportunityId =
            "opportunity.workplace.private-care";
        public const string WorkOpportunityId =
            "opportunity.workplace.replacement-shift";

        public const string PrimaryInitialRulingId =
            "ruling:case.workplace.primary-identity:initial:3";
        public const string PrimaryAppealRulingId =
            "ruling:case.workplace.primary-identity:appeal:6";
        public const string SuccessorInitialRulingId =
            "ruling:case.workplace.successor-shift:initial:9";
        public const string AdverseEffectRequestId =
            "effect.workplace.adverse-decision";
        public const string RelianceId = "reliance.workplace.private-care";
        public const string AppealId = "appeal.workplace.primary";
        public const string HoldingId = "holding.workplace.identity-continuity";
        public const string HoldingRuleId =
            "rule.workplace.superseded-identity-retains-employment";

        public const string PaidShiftEntitlementId =
            "entitlement.workplace.paid-shift";
        public const string PaidShiftResourceId =
            "resource.workplace.paid-shift-42";
        public const string PaidShiftHolderStatusId =
            "status.workplace.holds-paid-shift-42";
        public const string EntitlementTransferId =
            "transfer.workplace.successor-shift";

        public const long StartCycle = 0;
        public const long EndCycle = 11;

        public static InstitutionalScenarioDefinition CreateDefinition()
        {
            return new InstitutionalScenarioDefinition
            {
                ScenarioId = ScenarioId,
                IncidentId = IncidentId,
                PrimaryCaseId = PrimaryCaseId,
                StartCycle = StartCycle,
                EndCycle = EndCycle,
                InitialSociety = CreateInitialSociety(),
                ParticipantRoles = CreateParticipantRoles(),
                LivedIncidentSeeds = CreateLivedIncidentSeeds(),
                InitialEconomicAccounts = CreateEconomicAccounts(),
                Alternatives = CreateAlternatives(),
                Opportunities = CreateOpportunities(),
                CycleSchedule = CreateSchedule(),
                EvidenceTemplates = CreateEvidenceTemplates(),
                Cases = CreateCases(),
                OfficialStatusEffectRequests = CreateStatusEffects(),
                RelianceDefinitions = CreateRelianceDefinitions(),
                RelianceRecoveries = CreateRelianceRecoveries(),
                Appeals = CreateAppealDefinitions(),
                Holdings = CreateHoldingDefinitions(),
                DescendantCases = CreateDescendantDefinitions(),
                ExclusiveEntitlements = CreateEntitlementDefinitions(),
                EntitlementTransfers = CreateEntitlementTransfers(),
            };
        }

        /// <summary>
        /// Produces costly reliance and a successful reversal, while leaving the
        /// appellate outcome non-binding.
        /// </summary>
        public static InstitutionalPolicyConfiguration CreateReliancePolicy()
        {
            return CreatePolicy(
                "configuration.workplace.reliance",
                evidenceWeightPercent: 60,
                aidEffectiveness: 100,
                establishHolding: false,
                autoCiteHolding: false);
        }

        /// <summary>
        /// Makes the evidence insufficient on appeal and private care unattractive,
        /// leaving the initial denial intact without reliance.
        /// </summary>
        public static InstitutionalPolicyConfiguration CreateFinalDenialPolicy()
        {
            return CreatePolicy(
                "configuration.workplace.final-denial",
                evidenceWeightPercent: 40,
                aidEffectiveness: 0,
                establishHolding: false,
                autoCiteHolding: false);
        }

        /// <summary>
        /// Enables the complete reliance, reversal, scoped precedent, citation and
        /// exclusive-entitlement path.
        /// </summary>
        public static InstitutionalPolicyConfiguration CreatePrecedentPolicy()
        {
            return CreatePolicy(
                "configuration.workplace.precedent",
                evidenceWeightPercent: 60,
                aidEffectiveness: 100,
                establishHolding: true,
                autoCiteHolding: true);
        }

        private static InstitutionalPolicyConfiguration CreatePolicy(
            string configurationId,
            int evidenceWeightPercent,
            int aidEffectiveness,
            bool establishHolding,
            bool autoCiteHolding)
        {
            return new InstitutionalPolicyConfiguration
            {
                PolicyConfigurationId = configurationId,
                PolicyVersion = $"{configurationId}.v1",
                Kind = InstitutionalPolicyKind.PrecedentMachine,
                WorkReward = 55,
                AidEffectiveness = aidEffectiveness,
                DisclosureProtection = 100,
                RetaliationRisk = 0,
                AppealAccessibility = 100,
                DecisionVariationAmplitude = 0,
                ClaimantEvidenceWeightPercent = 0,
                ClinicalEvidenceWeightPercent = 0,
                PatternEvidenceWeightPercent = 0,
                WitnessEvidenceWeightPercent = 0,
                ManagementEvidenceWeightPercent = 0,
                ActionRecordWeightPercent = 0,
                EvidenceClassWeights = new List<EvidenceClassWeight>
                {
                    new EvidenceClassWeight
                    {
                        EvidenceClassId = EvidenceClassId,
                        WeightPercent = evidenceWeightPercent,
                    },
                },
                InitialRecognitionThreshold = 80,
                ProvisionalRecognitionThreshold = 40,
                AppealRecognitionThreshold = 50,
                LaterRecognitionThreshold = 60,
                CitedHoldingWeight = 80,
                PermitProvisionalRecognition = false,
                ProvisionalReliefAmount = 0,
                EstablishAppellateHolding = establishHolding,
                AutoCiteMatchingHoldings = autoCiteHolding,
                HoldingReach = PrecedentReach.Employer,
                HoldingIsRetrospective = true,
            };
        }

        private static List<ScenarioParticipantRoleDefinition>
            CreateParticipantRoles()
        {
            return new List<ScenarioParticipantRoleDefinition>
            {
                Role(
                    ContingentHolderRoleId,
                    "contingent-shift-holder",
                    recognisedStatusId: PaidShiftHolderStatusId),
                Role(
                    DependentRoleId,
                    "household-dependent",
                    requiredEmployerId: "employer.none"),
                Role(EmployerRoleId, "management-authority"),
                Role(
                    LaterClaimantRoleId,
                    "later-identity-claimant",
                    unrecognisedStatusId: PaidShiftHolderStatusId,
                    anomalyTraitId: IdentityAnomalyTraitId),
                Role(
                    PrimaryClaimantRoleId,
                    "identity-continuity-claimant",
                    unrecognisedStatusId: InstitutionalStatusIds.AdverseDecision,
                    anomalyTraitId: IdentityAnomalyTraitId),
            };
        }

        private static ScenarioParticipantRoleDefinition Role(
            string roleId,
            string commitmentKind,
            string requiredEmployerId = EmployerId,
            string recognisedStatusId = null,
            string unrecognisedStatusId = null,
            string anomalyTraitId = null)
        {
            var query = new ScenarioParticipantQuery
            {
                RequiredSpeciesId = "species.registered-person",
                RequiredEmployerId = requiredEmployerId,
                RequiredCommitmentKinds = new List<string> { commitmentKind },
            };
            if (!string.IsNullOrEmpty(recognisedStatusId))
                query.RequiredRecognisedStatusIds.Add(recognisedStatusId);
            if (!string.IsNullOrEmpty(unrecognisedStatusId))
                query.RequiredUnrecognisedStatusIds.Add(unrecognisedStatusId);
            if (!string.IsNullOrEmpty(anomalyTraitId))
                query.RequiredAnomalyTraitIds.Add(anomalyTraitId);

            var distinctRoles = new List<string>
            {
                ContingentHolderRoleId,
                DependentRoleId,
                EmployerRoleId,
                LaterClaimantRoleId,
                PrimaryClaimantRoleId,
            };
            distinctRoles.Remove(roleId);
            return new ScenarioParticipantRoleDefinition
            {
                RoleId = roleId,
                Query = query,
                DistinctFromRoleIds = distinctRoles,
            };
        }

        private static List<ScenarioLivedIncidentSeedDefinition>
            CreateLivedIncidentSeeds()
        {
            return new List<ScenarioLivedIncidentSeedDefinition>
            {
                new ScenarioLivedIncidentSeedDefinition
                {
                    IncidentSeedId = "incident-seed.workplace.identity-injury",
                    IncidentId = IncidentId,
                    Cycle = 1,
                    SubjectRoleId = PrimaryClaimantRoleId,
                    CauseEntityId = EmployerId,
                    PropositionId = IdentityPropositionId,
                    AffectedNeed = NeedKind.Health,
                    NeedPressureDelta = 20,
                },
            };
        }

        private static List<ScenarioInitialEconomicAccountDefinition>
            CreateEconomicAccounts()
        {
            return new List<ScenarioInitialEconomicAccountDefinition>
            {
                new ScenarioInitialEconomicAccountDefinition
                {
                    AccountId = "account.workplace.dependent",
                    OwnerRoleId = DependentRoleId,
                    InitialCredits = 100,
                    CycleIncome = 0,
                },
                new ScenarioInitialEconomicAccountDefinition
                {
                    AccountId = "account.workplace.primary",
                    OwnerRoleId = PrimaryClaimantRoleId,
                    InitialCredits = 100,
                    CycleIncome = 0,
                },
            };
        }

        private static List<ScenarioAlternativeDefinition> CreateAlternatives()
        {
            return new List<ScenarioAlternativeDefinition>
            {
                new ScenarioAlternativeDefinition
                {
                    AlternativeKey = "alternative.workplace.employer-clinic",
                    OwnerRoleId = PrimaryClaimantRoleId,
                    InitiallyAvailable = true,
                    ResourceValue = 30,
                },
            };
        }

        private static List<ScenarioOpportunityDefinition> CreateOpportunities()
        {
            return new List<ScenarioOpportunityDefinition>
            {
                new ScenarioOpportunityDefinition
                {
                    OpportunityId = AppealOpportunityId,
                    Kind = ScenarioOpportunityKind.Appeal,
                    PurposeId = "purpose.workplace.challenge-denial",
                    SourceCauseId = "cause.workplace.appeal-window",
                    AvailabilityStartCycle = 5,
                    AvailabilityEndCycle = 5,
                    UtilityBonus = 20,
                    CaseId = PrimaryCaseId,
                    ChallengedRulingId = PrimaryInitialRulingId,
                    HearingCycle = 6,
                    EligibleRoleIds =
                        new List<string> { PrimaryClaimantRoleId },
                },
                new ScenarioOpportunityDefinition
                {
                    OpportunityId = AidOpportunityId,
                    Kind = ScenarioOpportunityKind.Aid,
                    PurposeId = "purpose.workplace.private-treatment",
                    SourceCauseId = "cause.workplace.denied-treatment",
                    AvailabilityStartCycle = 4,
                    AvailabilityEndCycle = 4,
                    UtilityBonus = -60,
                    RequiredOfficialStatusId =
                        InstitutionalStatusIds.AdverseDecision,
                    RequiredOfficialStatusRecognised = true,
                    HearingCycle = -1,
                    EligibleRoleIds =
                        new List<string> { PrimaryClaimantRoleId },
                },
                new ScenarioOpportunityDefinition
                {
                    OpportunityId = WorkOpportunityId,
                    Kind = ScenarioOpportunityKind.Work,
                    PurposeId = "purpose.workplace.cover-disputed-shift",
                    SourceCauseId = "cause.workplace.unfilled-shift",
                    AvailabilityStartCycle = 7,
                    AvailabilityEndCycle = 7,
                    UtilityBonus = 20,
                    RequiredEmployerId = EmployerId,
                    RequiredOfficialStatusId = PaidShiftHolderStatusId,
                    RequiredOfficialStatusRecognised = true,
                    HearingCycle = -1,
                    EligibleRoleIds =
                        new List<string> { ContingentHolderRoleId },
                },
            };
        }

        private static List<ScenarioCycleScheduleEntry> CreateSchedule()
        {
            var schedule = new List<ScenarioCycleScheduleEntry>();
            for (long cycle = StartCycle + 1; cycle <= EndCycle; cycle++)
            {
                var row = new ScenarioCycleScheduleEntry
                {
                    ScheduleEntryId = $"schedule.workplace.{cycle:000}",
                    IncidentId = IncidentId,
                    Cycle = cycle,
                    Visibility = ScenarioVisibilityMode.NoBoundRoles,
                };
                if (cycle == 1)
                {
                    row.DisclosureRequested = true;
                }
                else if (cycle == 4)
                {
                    row.AidAvailable = true;
                    row.ActiveOpportunityIds.Add(AidOpportunityId);
                }
                else if (cycle == 5)
                {
                    row.AppealWindowOpen = true;
                    row.OpenDocketId = "docket.workplace.primary-identity";
                    row.ActiveOpportunityIds.Add(AppealOpportunityId);
                }
                else if (cycle == 7)
                {
                    row.WorkAvailable = true;
                    row.ActiveOpportunityIds.Add(WorkOpportunityId);
                }
                schedule.Add(row);
            }
            return schedule;
        }

        private static List<ScenarioEvidenceTemplateDefinition>
            CreateEvidenceTemplates()
        {
            return new List<ScenarioEvidenceTemplateDefinition>
            {
                new ScenarioEvidenceTemplateDefinition
                {
                    EvidenceTemplateId = EvidenceTemplateId,
                    SourceEventKind = SocietyEventKind.EvidenceDisclosed,
                    RequiredPropositionId = IdentityPropositionId,
                    CaseId = PrimaryCaseId,
                    IssueId = IssueId,
                    EvidenceClassId = EvidenceClassId,
                    Effect = EvidenceEffect.SupportsFinding,
                    Weight = 150,
                    Visibility = EvidenceVisibility.OfficialRecord,
                },
            };
        }

        private static List<ScenarioCaseDefinition> CreateCases()
        {
            return new List<ScenarioCaseDefinition>
            {
                new ScenarioCaseDefinition
                {
                    CaseId = PrimaryCaseId,
                    IssueId = IssueId,
                    ClaimantRoleId = PrimaryClaimantRoleId,
                    RespondentRoleId = EmployerRoleId,
                    Facts = IdentityScopeFacts(),
                    OpenCycle = 0,
                    InitialEvidenceCutoffCycle = 1,
                    InitialRulingCycle = 3,
                    AdjudicationEvidenceCutoffCycle = 5,
                    AdjudicationCycle = 6,
                    InitialPhaseId = "initial",
                    AdjudicationPhaseId = "appeal",
                    InitialRulingId = PrimaryInitialRulingId,
                    AdjudicationRulingId = PrimaryAppealRulingId,
                    InitialScoreThreshold = 80,
                    ProvisionalScoreThreshold = 40,
                    ProvisionalRecognitionPermitted = false,
                    AdjudicationScoreThreshold = 50,
                },
                new ScenarioCaseDefinition
                {
                    CaseId = SuccessorCaseId,
                    IssueId = IssueId,
                    ClaimantRoleId = LaterClaimantRoleId,
                    RespondentRoleId = EmployerRoleId,
                    Facts = SuccessorFacts(),
                    OpenCycle = 8,
                    InitialEvidenceCutoffCycle = 8,
                    InitialRulingCycle = 9,
                    AdjudicationEvidenceCutoffCycle = 10,
                    AdjudicationCycle = 10,
                    InitialPhaseId = "initial",
                    AdjudicationPhaseId = "appeal",
                    InitialRulingId = SuccessorInitialRulingId,
                    AdjudicationRulingId =
                        "ruling:case.workplace.successor-shift:appeal:10",
                    InitialScoreThreshold = 60,
                    ProvisionalScoreThreshold = 30,
                    ProvisionalRecognitionPermitted = false,
                    AdjudicationScoreThreshold = 60,
                    CitedHoldingIds = new List<string> { HoldingId },
                },
            };
        }

        private static List<ScenarioOfficialStatusEffectRequest>
            CreateStatusEffects()
        {
            return new List<ScenarioOfficialStatusEffectRequest>
            {
                new ScenarioOfficialStatusEffectRequest
                {
                    EffectRequestId = AdverseEffectRequestId,
                    Cycle = 3,
                    CauseCaseId = PrimaryCaseId,
                    CauseRulingId = PrimaryInitialRulingId,
                    RequiredRulingDisposition = RulingDisposition.Denied,
                    TargetRoleId = PrimaryClaimantRoleId,
                    StatusId = InstitutionalStatusIds.AdverseDecision,
                    RequestedRecognisedState = true,
                    RequestedResourceDelta = -5,
                },
            };
        }

        private static List<ScenarioIrreversibleRelianceDefinition>
            CreateRelianceDefinitions()
        {
            return new List<ScenarioIrreversibleRelianceDefinition>
            {
                new ScenarioIrreversibleRelianceDefinition
                {
                    RelianceId = RelianceId,
                    Cycle = 4,
                    RelyingRoleId = PrimaryClaimantRoleId,
                    SourceOpportunityId = AidOpportunityId,
                    SourceActionKind = SocietyActionKind.SeekAid,
                    EnablingEffectRequestId = AdverseEffectRequestId,
                    EnablingRulingId = PrimaryInitialRulingId,
                    IrreversibleChoiceKey =
                        "choice.workplace.private-treatment",
                    AbandonedAlternativeKey =
                        "alternative.workplace.employer-clinic",
                    ExpectedStatusId = InstitutionalStatusIds.AdverseDecision,
                    ExpectedRecognisedState = true,
                    BeneficiaryRoleId = DependentRoleId,
                    ResourceId = "resource.workplace.credits",
                    Effects = new List<ScenarioRelianceEffectDefinition>
                    {
                        new ScenarioRelianceEffectDefinition
                        {
                            EffectId = "reliance-effect.workplace.beneficiary",
                            Recipient =
                                ScenarioRelianceEffectRecipient.BeneficiaryRole,
                            ResourceDelta = 10,
                            MaterialKind = MaterialConsequenceKind.ReliefPaid,
                            MaterialKindId =
                                "material-kind.workplace.dependent-care",
                            ResourceId = "resource.workplace.credits",
                            HasNeedEffect = true,
                            Need = NeedKind.Health,
                            NeedPressureDelta = -15,
                        },
                        new ScenarioRelianceEffectDefinition
                        {
                            EffectId = "reliance-effect.workplace.claimant",
                            Recipient =
                                ScenarioRelianceEffectRecipient.RelyingRole,
                            ResourceDelta = -30,
                            MaterialKind = MaterialConsequenceKind.RelianceSpent,
                            MaterialKindId =
                                "material-kind.workplace.private-treatment",
                            ResourceId = "resource.workplace.credits",
                            HasNeedEffect = true,
                            Need = NeedKind.Subsistence,
                            NeedPressureDelta = 10,
                        },
                    },
                },
            };
        }

        private static List<ScenarioRelianceRecoveryDefinition>
            CreateRelianceRecoveries()
        {
            return new List<ScenarioRelianceRecoveryDefinition>
            {
                new ScenarioRelianceRecoveryDefinition
                {
                    RecoveryDefinitionId = "recovery.workplace.private-care",
                    RelianceId = RelianceId,
                    Cycle = 6,
                    TriggerReversalRulingId = PrimaryAppealRulingId,
                    CaseIdPrefix = "case.workplace.reliance-recovery",
                    ParentCaseId = PrimaryCaseId,
                    ClaimantRoleId = PrimaryClaimantRoleId,
                    RespondentRoleId = EmployerRoleId,
                    IssueId = "issue.workplace.reliance-loss",
                    Facts = new CaseFactSet(new[]
                    {
                        new CaseFact(
                            "choice",
                            "choice.workplace.private-treatment"),
                        new CaseFact("reliance-kind", "care-cost"),
                    }),
                },
            };
        }

        private static List<ScenarioAppealDefinition> CreateAppealDefinitions()
        {
            return new List<ScenarioAppealDefinition>
            {
                new ScenarioAppealDefinition
                {
                    AppealId = AppealId,
                    CaseId = PrimaryCaseId,
                    OpportunityId = AppealOpportunityId,
                    AppellantRoleId = PrimaryClaimantRoleId,
                    FilingCycle = 5,
                    HearingCycle = 6,
                    ChallengedRulingId = PrimaryInitialRulingId,
                    ResultingRulingId = PrimaryAppealRulingId,
                    ResultingHoldingId = HoldingId,
                    GroundsEvidenceTemplateIds =
                        new List<string> { EvidenceTemplateId },
                },
            };
        }

        private static List<ScenarioHoldingDefinition> CreateHoldingDefinitions()
        {
            return new List<ScenarioHoldingDefinition>
            {
                new ScenarioHoldingDefinition
                {
                    HoldingId = HoldingId,
                    ScopeId =
                        "scope.workplace.employer-and-superseded-identity",
                    SourceAppealId = AppealId,
                    SourceRulingId = PrimaryAppealRulingId,
                    RuleId = HoldingRuleId,
                    IssueId = IssueId,
                    EstablishedCycle = 6,
                    Retrospective = true,
                    RequiredScopeFacts = IdentityScopeFacts(),
                    SupportingEvidenceTemplateIds =
                        new List<string> { EvidenceTemplateId },
                },
            };
        }

        private static List<ScenarioActionCausedDescendantCaseDefinition>
            CreateDescendantDefinitions()
        {
            return new List<ScenarioActionCausedDescendantCaseDefinition>
            {
                new ScenarioActionCausedDescendantCaseDefinition
                {
                    DescendantDefinitionId =
                        "descendant.workplace.replacement-shift",
                    CaseId = SuccessorCaseId,
                    ParentCaseId = PrimaryCaseId,
                    OpenCycle = 8,
                    TriggerCycle = 7,
                    TriggerRoleId = ContingentHolderRoleId,
                    TriggerActionKind = SocietyActionKind.Work,
                    TriggerOpportunityId = WorkOpportunityId,
                    OriginatingRulingId = PrimaryInitialRulingId,
                    ConnectedRoleIds = new List<string>
                    {
                        ContingentHolderRoleId,
                        LaterClaimantRoleId,
                    },
                },
            };
        }

        private static List<ScenarioExclusiveEntitlementDefinition>
            CreateEntitlementDefinitions()
        {
            return new List<ScenarioExclusiveEntitlementDefinition>
            {
                new ScenarioExclusiveEntitlementDefinition
                {
                    EntitlementId = PaidShiftEntitlementId,
                    ResourceId = PaidShiftResourceId,
                    OfficialStatusId = PaidShiftHolderStatusId,
                    InitialHolderRoleId = ContingentHolderRoleId,
                    Units = 1,
                },
            };
        }

        private static List<ScenarioExclusiveEntitlementTransferDefinition>
            CreateEntitlementTransfers()
        {
            return new List<ScenarioExclusiveEntitlementTransferDefinition>
            {
                new ScenarioExclusiveEntitlementTransferDefinition
                {
                    TransferId = EntitlementTransferId,
                    Cycle = 10,
                    EntitlementId = PaidShiftEntitlementId,
                    FromRoleId = ContingentHolderRoleId,
                    ToRoleId = LaterClaimantRoleId,
                    CauseCaseId = SuccessorCaseId,
                    CauseRulingId = SuccessorInitialRulingId,
                    CauseHoldingId = HoldingId,
                    GainKind = MaterialConsequenceKind.BackpayAwarded,
                    LossKind = MaterialConsequenceKind.WagesLost,
                    GainKindId = "material-kind.workplace.shift-awarded",
                    LossKindId = "material-kind.workplace.shift-displaced",
                },
            };
        }

        private static CaseFactSet IdentityScopeFacts()
        {
            return new CaseFactSet(new[]
            {
                new CaseFact("employer", EmployerId),
                new CaseFact(
                    "identity-condition",
                    "identity.superseded-continuity"),
            });
        }

        private static CaseFactSet SuccessorFacts()
        {
            return new CaseFactSet(new[]
            {
                new CaseFact("employer", EmployerId),
                new CaseFact(
                    "identity-condition",
                    "identity.superseded-continuity"),
                new CaseFact("shift-class", "shift.paid-night-42"),
            });
        }
    }
}
