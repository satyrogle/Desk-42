using System.Collections.Generic;

namespace Desk42.Institutional.Scenarios.GlassCanal
{
    /// <summary>
    /// Data-only environmental-liability scenario used as the second independent
    /// authoring proof for the institutional engine.
    /// </summary>
    public static partial class GlassCanalScenario
    {
        public const string ScenarioId = "scenario.glass-canal-discharge";
        public const string IncidentId = "incident.glass-canal.bound-weather-discharge";
        public const string PrimaryCaseId = "case.glass-canal-discharge";
        public const string LaterCaseId = "case.downstream-purification-priority";
        public const string IssueId = "issue.output-control-after-boundary-crossing";

        public const string WatershedId = "watershed.glass-canal";
        public const string PermitClassId = "permit.bound-weather";
        public const string OutputConditionId = "output.undissipated";
        public const string BoundCloudId = "entity.glass-canal.bound-cloud-17";

        public const string PrimaryClaimantRoleId = "role.glass.01-primary-downstream";
        public const string OperatorRoleId = "role.glass.02-bound-weather-operator";
        public const string InspectorRoleId = "role.glass.03-primary-inspector";
        public const string CompetingSamplerRoleId = "role.glass.04-competing-sampler";
        public const string ControllerWitnessRoleId = "role.glass.05-controller-witness";
        public const string LaterClaimantRoleId = "role.glass.06-later-downstream";
        public const string CartridgeHolderRoleId = "role.glass.07-cartridge-holder";
        public const string WatershedRepresentativeRoleId =
            "role.glass.08-watershed-representative";

        public const string MaraAgentId = "agent.glass.mara-kest";
        public const string NaraAgentId = "agent.glass.nara-quill";
        public const string KhetAgentId = "agent.glass.khet-daro";
        public const string OrinAgentId = "agent.glass.orin-pell";
        public const string IlyaAgentId = "agent.glass.ilya-ro";
        public const string SeraAgentId = "agent.glass.sera-vale";
        public const string VeyAgentId = "agent.glass.vey-ankar";
        public const string TomaAgentId = "agent.glass.toma-rill";
        public const string BystanderAgentId = "agent.glass.una-bell";

        public const string BoundWeatherTraitId =
            "anomaly.glass.bound-weather-controller-resonance";
        public const string ControllerResonanceExposureTraitId =
            "anomaly.glass.controller-resonance-exposure";
        public const string ContinuingControlStatusId =
            "status.continuing-output-control";
        public const string UndissipatedOutputStatusId =
            "status.glass.output-undissipated";
        public const string SelfContaminationLiabilityStatusId =
            "status.self-contamination-liability";
        public const string MunicipalPotableReliefStatusId =
            "status.municipal-potable-relief";
        public const string FilterEntitlementStatusId =
            "status.municipal-filter-entitlement";
        public const string PrimaryDispositionRecordedStatusId =
            "status.glass.primary-disposition-recorded";

        public const string SampleEvidenceClassId = "evidence.water.sealed-sample";
        public const string DrainTelemetryEvidenceClassId =
            "evidence.utility.drain-telemetry";
        public const string ControllerLogEvidenceClassId =
            "evidence.anomaly.controller-log";
        public const string PermitMapEvidenceClassId =
            "evidence.permit.boundary-map";
        public const string ResonanceEvidenceClassId = "evidence.anomaly.resonance";
        public const string ValveResidueEvidenceClassId = "evidence.valve.residue";

        public const string SampleEvidenceTemplateId =
            "evidence-template.glass.01-sealed-water-sample";
        public const string DrainTelemetryEvidenceTemplateId =
            "evidence-template.glass.02-drain-telemetry";
        public const string PermitMapEvidenceTemplateId =
            "evidence-template.glass.03-permit-boundary-map";
        public const string ResonanceEvidenceTemplateId =
            "evidence-template.glass.04-anomalous-resonance";
        public const string ControllerLogEvidenceTemplateId =
            "evidence-template.glass.05-controller-log";
        public const string ValveResidueEvidenceTemplateId =
            "evidence-template.glass.06-valve-residue";
        public const string LaterSampleEvidenceTemplateId =
            "evidence-template.glass.07-later-plume-sample";

        public const string SampleOpportunityId = "opportunity.glass.01-sealed-sample";
        public const string ControllerDelayOpportunityId =
            "opportunity.glass.02-controller-maintenance";
        public const string ResonanceObservationOpportunityId =
            "opportunity.glass.02-resonance-observation";
        public const string ComplianceOpportunityId =
            "opportunity.glass.03-condenser-surrender";
        public const string ReliefOpportunityId =
            "opportunity.glass.04-condenser-decommission";
        public const string ValveInspectionOpportunityId =
            "opportunity.glass.05-valve-inspection";
        public const string PrimaryDocketNoticeOpportunityId =
            "opportunity.glass.06-primary-docket-notice";
        public const string OperatorAppealOpportunityId =
            "opportunity.glass.07-operator-appeal";
        public const string MaraAppealOpportunityId =
            "opportunity.glass.08-water-user-appeal";
        public const string LaterReportOpportunityId =
            "opportunity.glass.09-later-plume-report";
        public const string SeraAppealOpportunityId =
            "opportunity.glass.10-later-water-user-appeal";

        public const string PrimaryInitialRulingId =
            "ruling:case.glass-canal-discharge:initial:6";
        public const string PrimaryAppealRulingId =
            "ruling:case.glass-canal-discharge:appeal:12";
        public const string LaterInitialRulingId =
            "ruling:case.downstream-purification-priority:initial:15";
        public const string LaterAppealRulingId =
            "ruling:case.downstream-purification-priority:appeal:17";

        public const string OperatorAppealId = "appeal.glass.01-primary-operator";
        public const string MaraAppealId = "appeal.glass.02-primary-water-user";
        public const string SeraAppealId = "appeal.glass.03-later-water-user";
        public const string HoldingId = "holding.glass.licensed-output-accountability";
        public const string HoldingRuleId =
            "rule.glass.bound-weather-control-until-dissipation";
        public const string HoldingCitationId =
            "citation.glass.later-appellate-bound-weather";

        public const string FilterEntitlementId = "entitlement.glass.filter-cf-9";
        public const string FilterResourceId = "filter-cartridge.cf-9";
        public const string FilterTransferId = "transfer.glass.filter-cf-9-to-sera";

        public const string DenialRelianceId = "reliance.glass.denial-surrender";
        public const string GrantRelianceId = "reliance.glass.grant-decommission";
        public const string CondenserAlternativeId = "alternative.rooftop-condenser";

        public const long StartCycle = 0;
        public const long EndCycle = 18;

        private const string DrainTelemetryPropositionId =
            "proposition.glass.drain-path-reaches-cistern";
        private const string PermitBoundaryPropositionId =
            "proposition.glass.registered-parcel-boundary-crossed";
        public const string ResonancePropositionId =
            "proposition.glass.controller-resonance-persists";
        private const string ControllerLogPropositionId =
            "proposition.glass.post-boundary-correction-pulse";

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
                LivedIncidentSeeds = CreateIncidentSeeds(),
                InitialEconomicAccounts = CreateEconomicAccounts(),
                Alternatives = CreateAlternatives(),
                Opportunities = CreateOpportunities(),
                CycleSchedule = CreateSchedule(),
                EvidenceTemplates = CreateEvidenceTemplates(),
                Cases = CreateCases(),
                EvidenceActivatedCases = CreateEvidenceActivations(),
                OfficialStatusEffectRequests = CreateStatusEffects(),
                RelianceDefinitions = CreateRelianceDefinitions(),
                RelianceRecoveries = CreateRelianceRecoveries(),
                Appeals = CreateAppeals(),
                Holdings = CreateHoldings(),
                HoldingCitations = CreateHoldingCitations(),
                DescendantCases = CreateDescendants(),
                ExclusiveEntitlements = CreateEntitlements(),
                EntitlementTransfers = CreateTransfers(),
            };
        }

        public static InstitutionalPolicyConfiguration CreateBoundaryLiteralismPolicy()
        {
            return CreatePolicy(
                "configuration.glass.boundary-literalism",
                workReward: 100,
                aidEffectiveness: 0,
                appealAccessibility: 0,
                permitProvisional: false,
                establishHolding: false,
                autoCiteHolding: false,
                EvidenceRule(ControllerLogEvidenceClassId, 10, 20),
                EvidenceRule(ResonanceEvidenceClassId, 10, 30),
                EvidenceRule(PermitMapEvidenceClassId, 100, 100),
                EvidenceRule(DrainTelemetryEvidenceClassId, 40, 50),
                EvidenceRule(ValveResidueEvidenceClassId, 10, 20),
                EvidenceRule(SampleEvidenceClassId, 80, 50));
        }

        public static InstitutionalPolicyConfiguration CreatePrecautionaryAccessPolicy()
        {
            return CreatePolicy(
                "configuration.glass.precautionary-access",
                workReward: 20,
                aidEffectiveness: 100,
                appealAccessibility: 100,
                permitProvisional: true,
                establishHolding: false,
                autoCiteHolding: false,
                EvidenceRule(ControllerLogEvidenceClassId, 20, 50),
                EvidenceRule(ResonanceEvidenceClassId, 100, 100),
                EvidenceRule(PermitMapEvidenceClassId, 100, 50),
                EvidenceRule(DrainTelemetryEvidenceClassId, 100, 100),
                EvidenceRule(ValveResidueEvidenceClassId, 20, 50),
                EvidenceRule(SampleEvidenceClassId, 100, 100));
        }

        public static InstitutionalPolicyConfiguration
            CreateLicensedOutputAccountabilityPolicy()
        {
            return CreatePolicy(
                "configuration.glass.licensed-output-accountability",
                workReward: 20,
                aidEffectiveness: 100,
                appealAccessibility: 100,
                permitProvisional: false,
                establishHolding: true,
                autoCiteHolding: true,
                EvidenceRule(ControllerLogEvidenceClassId, 45, 100),
                EvidenceRule(ResonanceEvidenceClassId, 100, 100),
                EvidenceRule(PermitMapEvidenceClassId, 100, 50),
                EvidenceRule(DrainTelemetryEvidenceClassId, 100, 100),
                EvidenceRule(ValveResidueEvidenceClassId, 45, 100),
                EvidenceRule(SampleEvidenceClassId, 100, 100));
        }

        private static InstitutionalPolicyConfiguration CreatePolicy(
            string configurationId,
            int workReward,
            int aidEffectiveness,
            int appealAccessibility,
            bool permitProvisional,
            bool establishHolding,
            bool autoCiteHolding,
            params EvidenceClassWeight[] evidenceRules)
        {
            return new InstitutionalPolicyConfiguration
            {
                PolicyConfigurationId = configurationId,
                PolicyVersion = $"{configurationId}.v1",
                Kind = InstitutionalPolicyKind.PrecedentMachine,
                WorkReward = workReward,
                AidEffectiveness = aidEffectiveness,
                DisclosureProtection = 100,
                RetaliationRisk = 0,
                AppealAccessibility = appealAccessibility,
                DecisionVariationAmplitude = 0,
                ClaimantEvidenceWeightPercent = 0,
                ClinicalEvidenceWeightPercent = 0,
                PatternEvidenceWeightPercent = 0,
                WitnessEvidenceWeightPercent = 0,
                ManagementEvidenceWeightPercent = 0,
                ActionRecordWeightPercent = 0,
                EvidenceClassWeights = new List<EvidenceClassWeight>(evidenceRules),
                InitialRecognitionThreshold = 80,
                ProvisionalRecognitionThreshold = 40,
                AppealRecognitionThreshold = 100,
                LaterRecognitionThreshold = 60,
                CitedHoldingWeight = 90,
                PermitProvisionalRecognition = permitProvisional,
                ProvisionalReliefAmount = 0,
                EstablishAppellateHolding = establishHolding,
                AutoCiteMatchingHoldings = autoCiteHolding,
                HoldingReach = PrecedentReach.Jurisdiction,
                HoldingIsRetrospective = false,
            };
        }

        private static EvidenceClassWeight EvidenceRule(
            string classId,
            int weightPercent,
            int policyReliabilityPercent)
        {
            return new EvidenceClassWeight
            {
                EvidenceClassId = classId,
                WeightPercent = weightPercent,
                PolicyReliabilityPercent = policyReliabilityPercent,
            };
        }

        private static List<ScenarioParticipantRoleDefinition> CreateParticipantRoles()
        {
            return new List<ScenarioParticipantRoleDefinition>
            {
                Role(PrimaryClaimantRoleId, "glass-primary-downstream",
                    recognisedStatusId: "status.glass.registered-water-user",
                    anomalyTraitId: ControllerResonanceExposureTraitId),
                Role(OperatorRoleId, "glass-bound-weather-operator",
                    recognisedStatusId: "status.glass.bound-weather-permit",
                    anomalyTraitId: BoundWeatherTraitId),
                Role(InspectorRoleId, "glass-primary-inspector",
                    recognisedStatusId: "status.glass.primary-sampling-authority"),
                Role(CompetingSamplerRoleId, "glass-competing-sampler",
                    recognisedStatusId: "status.glass.canal-access",
                    unrecognisedStatusId: "status.glass.primary-sampling-authority"),
                Role(ControllerWitnessRoleId, "glass-controller-witness",
                    recognisedStatusId: "status.glass.controller-access"),
                Role(LaterClaimantRoleId, "glass-later-downstream",
                    recognisedStatusId: "status.glass.registered-water-user"),
                Role(CartridgeHolderRoleId, "glass-cartridge-holder",
                    recognisedStatusId: FilterEntitlementStatusId),
                Role(WatershedRepresentativeRoleId, "glass-watershed-representative",
                    recognisedStatusId: "status.glass.watershed-representation"),
            };
        }

        private static ScenarioParticipantRoleDefinition Role(
            string roleId,
            string commitmentKind,
            string recognisedStatusId = null,
            string unrecognisedStatusId = null,
            string anomalyTraitId = null)
        {
            var query = new ScenarioParticipantQuery
            {
                RequiredSpeciesId = "species.registered-person",
                RequiredCommitmentKinds = new List<string> { commitmentKind },
            };
            if (!string.IsNullOrEmpty(recognisedStatusId))
                query.RequiredRecognisedStatusIds.Add(recognisedStatusId);
            if (!string.IsNullOrEmpty(unrecognisedStatusId))
                query.RequiredUnrecognisedStatusIds.Add(unrecognisedStatusId);
            if (!string.IsNullOrEmpty(anomalyTraitId))
                query.RequiredAnomalyTraitIds.Add(anomalyTraitId);

            var distinct = new List<string>
            {
                PrimaryClaimantRoleId,
                OperatorRoleId,
                InspectorRoleId,
                CompetingSamplerRoleId,
                ControllerWitnessRoleId,
                LaterClaimantRoleId,
                CartridgeHolderRoleId,
                WatershedRepresentativeRoleId,
            };
            distinct.Remove(roleId);
            distinct.Sort(System.StringComparer.Ordinal);
            return new ScenarioParticipantRoleDefinition
            {
                RoleId = roleId,
                Query = query,
                DistinctFromRoleIds = distinct,
            };
        }

        private static List<ScenarioLivedIncidentSeedDefinition> CreateIncidentSeeds()
        {
            return new List<ScenarioLivedIncidentSeedDefinition>
            {
                new ScenarioLivedIncidentSeedDefinition
                {
                    IncidentSeedId = "incident-seed.glass.01-primary-discharge",
                    IncidentId = IncidentId,
                    Cycle = 1,
                    SubjectRoleId = PrimaryClaimantRoleId,
                    CauseEntityId = BoundCloudId,
                    PropositionId = ResonancePropositionId,
                    AffectedNeed = NeedKind.Health,
                    NeedPressureDelta = 20,
                },
                new ScenarioLivedIncidentSeedDefinition
                {
                    IncidentSeedId = "incident-seed.glass.02-second-plume",
                    IncidentId = IncidentId,
                    Cycle = 13,
                    SubjectRoleId = LaterClaimantRoleId,
                    CauseEntityId = BoundCloudId,
                    PropositionId = "proposition.glass.second-undissipated-plume",
                    AffectedNeed = NeedKind.Safety,
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
                    AccountId = "account.glass.mara-condenser",
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
                    AlternativeKey = CondenserAlternativeId,
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
                WorkOpportunity(SampleOpportunityId, 2, 1,
                    InspectorRoleId, CompetingSamplerRoleId),
                WorkOpportunity(ControllerDelayOpportunityId, 3, 80,
                    ControllerWitnessRoleId, availabilityEndCycle: 4),
                new ScenarioOpportunityDefinition
                {
                    OpportunityId = ResonanceObservationOpportunityId,
                    Kind = ScenarioOpportunityKind.Work,
                    PurposeId = "purpose.glass.observe-undissipated-resonance",
                    SourceCauseId = BoundWeatherTraitId,
                    AvailabilityStartCycle = 4,
                    AvailabilityEndCycle = 4,
                    UtilityBonus = 200,
                    RequiredOfficialStatusId = UndissipatedOutputStatusId,
                    RequiredOfficialStatusRecognised = true,
                    HearingCycle = -1,
                    EligibleRoleIds = new List<string> { PrimaryClaimantRoleId },
                },
                AidOpportunity(ComplianceOpportunityId, 7, -40,
                    SelfContaminationLiabilityStatusId, PrimaryClaimantRoleId),
                AidOpportunity(ReliefOpportunityId, 7, -40,
                    MunicipalPotableReliefStatusId, PrimaryClaimantRoleId),
                WorkOpportunity(ValveInspectionOpportunityId, 9, 100,
                    InspectorRoleId),
                AppealOpportunity(PrimaryDocketNoticeOpportunityId, 10, -70,
                    PrimaryCaseId, PrimaryInitialRulingId, 12,
                    WatershedRepresentativeRoleId),
                AppealOpportunity(OperatorAppealOpportunityId, 11, -65,
                    PrimaryCaseId, PrimaryInitialRulingId, 12, OperatorRoleId),
                AppealOpportunity(MaraAppealOpportunityId, 11, -100,
                    PrimaryCaseId, PrimaryInitialRulingId, 12,
                    PrimaryClaimantRoleId),
                new ScenarioOpportunityDefinition
                {
                    OpportunityId = LaterReportOpportunityId,
                    Kind = ScenarioOpportunityKind.Work,
                    PurposeId = "purpose.glass.record-second-plume",
                    SourceCauseId = "cause.glass.second-plume",
                    AvailabilityStartCycle = 14,
                    AvailabilityEndCycle = 14,
                    UtilityBonus = 100,
                    RequiredOfficialStatusId = PrimaryDispositionRecordedStatusId,
                    RequiredOfficialStatusRecognised = true,
                    HearingCycle = -1,
                    EligibleRoleIds = new List<string> { LaterClaimantRoleId },
                },
                AppealOpportunity(SeraAppealOpportunityId, 16, -80,
                    LaterCaseId, LaterInitialRulingId, 17,
                    LaterClaimantRoleId),
            };
        }

        private static ScenarioOpportunityDefinition WorkOpportunity(
            string opportunityId,
            long cycle,
            int bonus,
            string firstRoleId,
            string secondRoleId = null,
            long? availabilityEndCycle = null)
        {
            var roles = new List<string> { firstRoleId };
            if (!string.IsNullOrEmpty(secondRoleId)) roles.Add(secondRoleId);
            roles.Sort(System.StringComparer.Ordinal);
            return new ScenarioOpportunityDefinition
            {
                OpportunityId = opportunityId,
                Kind = ScenarioOpportunityKind.Work,
                PurposeId = $"purpose.{opportunityId}",
                SourceCauseId = $"cause.{opportunityId}",
                AvailabilityStartCycle = cycle,
                AvailabilityEndCycle = availabilityEndCycle ?? cycle,
                UtilityBonus = bonus,
                HearingCycle = -1,
                EligibleRoleIds = roles,
            };
        }

        private static ScenarioOpportunityDefinition AidOpportunity(
            string opportunityId,
            long cycle,
            int bonus,
            string requiredStatusId,
            string roleId)
        {
            return new ScenarioOpportunityDefinition
            {
                OpportunityId = opportunityId,
                Kind = ScenarioOpportunityKind.Aid,
                PurposeId = $"purpose.{opportunityId}",
                SourceCauseId = $"cause.{opportunityId}",
                AvailabilityStartCycle = cycle,
                AvailabilityEndCycle = cycle,
                UtilityBonus = bonus,
                RequiredOfficialStatusId = requiredStatusId,
                RequiredOfficialStatusRecognised = true,
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { roleId },
            };
        }

        private static ScenarioOpportunityDefinition AppealOpportunity(
            string opportunityId,
            long cycle,
            int bonus,
            string caseId,
            string challengedRulingId,
            long hearingCycle,
            string roleId)
        {
            return new ScenarioOpportunityDefinition
            {
                OpportunityId = opportunityId,
                Kind = ScenarioOpportunityKind.Appeal,
                PurposeId = $"purpose.{opportunityId}",
                SourceCauseId = $"cause.{opportunityId}",
                AvailabilityStartCycle = cycle,
                AvailabilityEndCycle = cycle,
                UtilityBonus = bonus,
                CaseId = caseId,
                ChallengedRulingId = challengedRulingId,
                HearingCycle = hearingCycle,
                EligibleRoleIds = new List<string> { roleId },
            };
        }

        private static List<ScenarioCycleScheduleEntry> CreateSchedule()
        {
            var schedule = new List<ScenarioCycleScheduleEntry>();
            for (long cycle = StartCycle + 1; cycle <= EndCycle; cycle++)
            {
                var row = new ScenarioCycleScheduleEntry
                {
                    ScheduleEntryId = $"schedule.glass.{cycle:000}",
                    IncidentId = IncidentId,
                    Cycle = cycle,
                    Visibility = ScenarioVisibilityMode.NoBoundRoles,
                };
                switch (cycle)
                {
                    case 2:
                        row.WorkAvailable = true;
                        row.ActiveOpportunityIds.Add(SampleOpportunityId);
                        break;
                    case 3:
                    case 4:
                        row.WorkAvailable = true;
                        row.DisclosureRequested = true;
                        row.ActiveOpportunityIds.Add(ControllerDelayOpportunityId);
                        if (cycle == 4)
                            row.ActiveOpportunityIds.Add(ResonanceObservationOpportunityId);
                        break;
                    case 7:
                        row.WorkAvailable = true;
                        row.AidAvailable = true;
                        row.ActiveOpportunityIds.Add(ComplianceOpportunityId);
                        row.ActiveOpportunityIds.Add(ReliefOpportunityId);
                        break;
                    case 8:
                        row.Visibility = ScenarioVisibilityMode.AllBoundRoles;
                        break;
                    case 9:
                        row.WorkAvailable = true;
                        row.DisclosureRequested = true;
                        row.ActiveOpportunityIds.Add(ValveInspectionOpportunityId);
                        break;
                    case 10:
                        row.AppealWindowOpen = true;
                        row.OpenDocketId = "docket.glass.primary-notice";
                        row.ActiveOpportunityIds.Add(PrimaryDocketNoticeOpportunityId);
                        break;
                    case 11:
                        row.AppealWindowOpen = true;
                        row.OpenDocketId = "docket.glass.primary";
                        row.ActiveOpportunityIds.Add(OperatorAppealOpportunityId);
                        row.ActiveOpportunityIds.Add(MaraAppealOpportunityId);
                        break;
                    case 14:
                        row.WorkAvailable = true;
                        row.ActiveOpportunityIds.Add(LaterReportOpportunityId);
                        break;
                    case 16:
                        row.AppealWindowOpen = true;
                        row.OpenDocketId = "docket.glass.later";
                        row.ActiveOpportunityIds.Add(SeraAppealOpportunityId);
                        break;
                }
                schedule.Add(row);
            }
            return schedule;
        }

        private static List<ScenarioEvidenceTemplateDefinition> CreateEvidenceTemplates()
        {
            return new List<ScenarioEvidenceTemplateDefinition>
            {
                WorkEvidence(SampleEvidenceTemplateId, SampleOpportunityId,
                    PrimaryCaseId, SampleEvidenceClassId, EvidenceEffect.SupportsFinding, 50),
                DisclosureEvidence(DrainTelemetryEvidenceTemplateId,
                    DrainTelemetryPropositionId, PrimaryCaseId,
                    DrainTelemetryEvidenceClassId, EvidenceEffect.SupportsFinding, 30),
                DisclosureEvidence(PermitMapEvidenceTemplateId,
                    PermitBoundaryPropositionId, PrimaryCaseId,
                    PermitMapEvidenceClassId, EvidenceEffect.OpposesFinding, 60),
                WorkEvidence(ResonanceEvidenceTemplateId,
                    ResonanceObservationOpportunityId, PrimaryCaseId,
                    ResonanceEvidenceClassId, EvidenceEffect.SupportsFinding, 20),
                DisclosureEvidence(ControllerLogEvidenceTemplateId,
                    ControllerLogPropositionId, PrimaryCaseId,
                    ControllerLogEvidenceClassId, EvidenceEffect.SupportsFinding, 40),
                WorkEvidence(ValveResidueEvidenceTemplateId,
                    ValveInspectionOpportunityId, PrimaryCaseId,
                    ValveResidueEvidenceClassId, EvidenceEffect.SupportsFinding, 40),
                WorkEvidence(LaterSampleEvidenceTemplateId,
                    LaterReportOpportunityId, LaterCaseId,
                    SampleEvidenceClassId, EvidenceEffect.SupportsFinding, 20),
            };
        }

        private static ScenarioEvidenceTemplateDefinition WorkEvidence(
            string templateId,
            string opportunityId,
            string caseId,
            string classId,
            EvidenceEffect effect,
            int weight)
        {
            return new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = templateId,
                SourceEventKind = SocietyEventKind.WorkPerformed,
                SourceOpportunityId = opportunityId,
                CaseId = caseId,
                IssueId = IssueId,
                EvidenceClassId = classId,
                Effect = effect,
                Weight = weight,
                Visibility = EvidenceVisibility.OfficialRecord,
            };
        }

        private static ScenarioEvidenceTemplateDefinition DisclosureEvidence(
            string templateId,
            string propositionId,
            string caseId,
            string classId,
            EvidenceEffect effect,
            int weight)
        {
            return new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = templateId,
                SourceEventKind = SocietyEventKind.EvidenceDisclosed,
                RequiredPropositionId = propositionId,
                CaseId = caseId,
                IssueId = IssueId,
                EvidenceClassId = classId,
                Effect = effect,
                Weight = weight,
                Visibility = EvidenceVisibility.OfficialRecord,
            };
        }

        private static List<ScenarioCaseDefinition> CreateCases()
        {
            return new List<ScenarioCaseDefinition>
            {
                new ScenarioCaseDefinition
                {
                    CaseId = LaterCaseId,
                    IssueId = IssueId,
                    ClaimantRoleId = LaterClaimantRoleId,
                    RespondentRoleId = OperatorRoleId,
                    Facts = LaterFacts(),
                    OpenCycle = 15,
                    InitialEvidenceCutoffCycle = 15,
                    InitialRulingCycle = 15,
                    AdjudicationEvidenceCutoffCycle = 16,
                    AdjudicationCycle = 17,
                    InitialPhaseId = "initial",
                    AdjudicationPhaseId = "appeal",
                    InitialRulingId = LaterInitialRulingId,
                    AdjudicationRulingId = LaterAppealRulingId,
                    InitialScoreThreshold = 60,
                    ProvisionalScoreThreshold = 30,
                    ProvisionalRecognitionPermitted = false,
                    AdjudicationScoreThreshold = 100,
                },
                new ScenarioCaseDefinition
                {
                    CaseId = PrimaryCaseId,
                    IssueId = IssueId,
                    ClaimantRoleId = PrimaryClaimantRoleId,
                    RespondentRoleId = OperatorRoleId,
                    Facts = ScopeFacts("plume.primary"),
                    OpenCycle = 2,
                    InitialEvidenceCutoffCycle = 5,
                    InitialRulingCycle = 6,
                    AdjudicationEvidenceCutoffCycle = 9,
                    AdjudicationCycle = 12,
                    InitialPhaseId = "initial",
                    AdjudicationPhaseId = "appeal",
                    InitialRulingId = PrimaryInitialRulingId,
                    AdjudicationRulingId = PrimaryAppealRulingId,
                    InitialScoreThreshold = 80,
                    ProvisionalScoreThreshold = 40,
                    ProvisionalRecognitionPermitted = true,
                    AdjudicationScoreThreshold = 100,
                },
            };
        }

        private static List<ScenarioEvidenceActivatedCaseDefinition>
            CreateEvidenceActivations()
        {
            return new List<ScenarioEvidenceActivatedCaseDefinition>
            {
                new ScenarioEvidenceActivatedCaseDefinition
                {
                    ActivationId = "activation.glass.primary-sealed-sample",
                    CaseId = PrimaryCaseId,
                    EvidenceTemplateId = SampleEvidenceTemplateId,
                    TriggerCycle = 2,
                },
            };
        }

        private static List<ScenarioOfficialStatusEffectRequest> CreateStatusEffects()
        {
            return new List<ScenarioOfficialStatusEffectRequest>
            {
                StatusEffect("effect.glass.01-denial-adverse-mara", 6,
                    PrimaryCaseId, PrimaryInitialRulingId, RulingDisposition.Denied,
                    PrimaryClaimantRoleId, InstitutionalStatusIds.AdverseDecision, true),
                StatusEffect("effect.glass.02-denial-liability-mara", 6,
                    PrimaryCaseId, PrimaryInitialRulingId, RulingDisposition.Denied,
                    PrimaryClaimantRoleId, SelfContaminationLiabilityStatusId, true, -5),
                StatusEffect("effect.glass.03-provisional-adverse-operator", 6,
                    PrimaryCaseId, PrimaryInitialRulingId,
                    RulingDisposition.ProvisionallyRecognised,
                    OperatorRoleId, InstitutionalStatusIds.AdverseDecision, true),
                StatusEffect("effect.glass.04-provisional-relief-mara", 6,
                    PrimaryCaseId, PrimaryInitialRulingId,
                    RulingDisposition.ProvisionallyRecognised,
                    PrimaryClaimantRoleId, MunicipalPotableReliefStatusId, true, 5),
                StatusEffect("effect.glass.05-docket-standing-denial", 6,
                    PrimaryCaseId, PrimaryInitialRulingId, RulingDisposition.Denied,
                    WatershedRepresentativeRoleId,
                    InstitutionalStatusIds.AdverseDecision, true),
                StatusEffect("effect.glass.06-docket-standing-provisional", 6,
                    PrimaryCaseId, PrimaryInitialRulingId,
                    RulingDisposition.ProvisionallyRecognised,
                    WatershedRepresentativeRoleId,
                    InstitutionalStatusIds.AdverseDecision, true),
                StatusEffect("effect.glass.07-later-report-after-denial", 6,
                    PrimaryCaseId, PrimaryInitialRulingId, RulingDisposition.Denied,
                    LaterClaimantRoleId, PrimaryDispositionRecordedStatusId, true),
                StatusEffect("effect.glass.08-later-report-after-provisional", 6,
                    PrimaryCaseId, PrimaryInitialRulingId,
                    RulingDisposition.ProvisionallyRecognised,
                    LaterClaimantRoleId, PrimaryDispositionRecordedStatusId, true),
                StatusEffect("effect.glass.09-appellate-continuing-control", 12,
                    PrimaryCaseId, PrimaryAppealRulingId,
                    RulingDisposition.ReversedAndRecognised,
                    OperatorRoleId, ContinuingControlStatusId, true),
                StatusEffect("effect.glass.10-later-adverse-sera", 15,
                    LaterCaseId, LaterInitialRulingId, RulingDisposition.Denied,
                    LaterClaimantRoleId, InstitutionalStatusIds.AdverseDecision, true),
            };
        }

        private static ScenarioOfficialStatusEffectRequest StatusEffect(
            string effectId,
            long cycle,
            string caseId,
            string rulingId,
            RulingDisposition disposition,
            string roleId,
            string statusId,
            bool recognised,
            int resourceDelta = 0)
        {
            return new ScenarioOfficialStatusEffectRequest
            {
                EffectRequestId = effectId,
                Cycle = cycle,
                CauseCaseId = caseId,
                CauseRulingId = rulingId,
                RequiredRulingDisposition = disposition,
                TargetRoleId = roleId,
                StatusId = statusId,
                RequestedRecognisedState = recognised,
                RequestedResourceDelta = resourceDelta,
            };
        }

        private static List<ScenarioIrreversibleRelianceDefinition>
            CreateRelianceDefinitions()
        {
            return new List<ScenarioIrreversibleRelianceDefinition>
            {
                Reliance(
                    DenialRelianceId,
                    ComplianceOpportunityId,
                    "effect.glass.02-denial-liability-mara",
                    SelfContaminationLiabilityStatusId,
                    "choice.glass.surrender-rooftop-condenser",
                    -30,
                    10),
                Reliance(
                    GrantRelianceId,
                    ReliefOpportunityId,
                    "effect.glass.04-provisional-relief-mara",
                    MunicipalPotableReliefStatusId,
                    "choice.glass.decommission-rooftop-condenser",
                    -25,
                    15),
            };
        }

        private static ScenarioIrreversibleRelianceDefinition Reliance(
            string relianceId,
            string opportunityId,
            string enablingEffectId,
            string expectedStatusId,
            string choiceId,
            int resourceDelta,
            int safetyDelta)
        {
            return new ScenarioIrreversibleRelianceDefinition
            {
                RelianceId = relianceId,
                Cycle = 7,
                PublicObservationCycle = 8,
                RelyingRoleId = PrimaryClaimantRoleId,
                SourceOpportunityId = opportunityId,
                SourceActionKind = SocietyActionKind.SeekAid,
                EnablingEffectRequestId = enablingEffectId,
                EnablingRulingId = PrimaryInitialRulingId,
                IrreversibleChoiceKey = choiceId,
                AbandonedAlternativeKey = CondenserAlternativeId,
                ExpectedStatusId = expectedStatusId,
                ExpectedRecognisedState = true,
                BeneficiaryRoleId = WatershedRepresentativeRoleId,
                ResourceId = "resource.glass.private-condenser",
                Effects = new List<ScenarioRelianceEffectDefinition>
                {
                    new ScenarioRelianceEffectDefinition
                    {
                        EffectId = $"reliance-effect.{relianceId}",
                        Recipient = ScenarioRelianceEffectRecipient.RelyingRole,
                        ResourceDelta = resourceDelta,
                        MaterialKind = MaterialConsequenceKind.RelianceSpent,
                        MaterialKindId = "material-kind.glass.condenser-loss",
                        ResourceId = "resource.glass.private-condenser",
                        HasNeedEffect = true,
                        Need = NeedKind.Safety,
                        NeedPressureDelta = safetyDelta,
                    },
                },
            };
        }

        private static List<ScenarioRelianceRecoveryDefinition>
            CreateRelianceRecoveries()
        {
            return new List<ScenarioRelianceRecoveryDefinition>
            {
                Recovery("recovery.glass.denial-surrender", DenialRelianceId,
                    "case.glass.recovery-denial", "reliance.denial-surrender"),
                Recovery("recovery.glass.grant-decommission", GrantRelianceId,
                    "case.glass.recovery-grant", "reliance.grant-decommission"),
            };
        }

        private static ScenarioRelianceRecoveryDefinition Recovery(
            string definitionId,
            string relianceId,
            string casePrefix,
            string relianceKind)
        {
            return new ScenarioRelianceRecoveryDefinition
            {
                RecoveryDefinitionId = definitionId,
                RelianceId = relianceId,
                Cycle = 12,
                TriggerReversalRulingId = PrimaryAppealRulingId,
                CaseIdPrefix = casePrefix,
                ParentCaseId = PrimaryCaseId,
                ClaimantRoleId = PrimaryClaimantRoleId,
                RespondentRoleId = OperatorRoleId,
                IssueId = "issue.glass.stranded-condenser-recovery",
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("asset", "rooftop-condenser"),
                    new CaseFact("reliance-kind", relianceKind),
                }),
            };
        }

        private static List<ScenarioAppealDefinition> CreateAppeals()
        {
            return new List<ScenarioAppealDefinition>
            {
                new ScenarioAppealDefinition
                {
                    AppealId = OperatorAppealId,
                    CaseId = PrimaryCaseId,
                    OpportunityId = OperatorAppealOpportunityId,
                    AppellantRoleId = OperatorRoleId,
                    FilingCycle = 11,
                    HearingCycle = 12,
                    ChallengedRulingId = PrimaryInitialRulingId,
                    ResultingRulingId = PrimaryAppealRulingId,
                    GroundsEvidenceTemplateIds = new List<string>
                    {
                        SampleEvidenceTemplateId,
                        PermitMapEvidenceTemplateId,
                    },
                },
                new ScenarioAppealDefinition
                {
                    AppealId = MaraAppealId,
                    CaseId = PrimaryCaseId,
                    OpportunityId = MaraAppealOpportunityId,
                    AppellantRoleId = PrimaryClaimantRoleId,
                    FilingCycle = 11,
                    HearingCycle = 12,
                    ChallengedRulingId = PrimaryInitialRulingId,
                    ResultingRulingId = PrimaryAppealRulingId,
                    ResultingHoldingId = HoldingId,
                    GroundsEvidenceTemplateIds = new List<string>
                    {
                        ResonanceEvidenceTemplateId,
                        ControllerLogEvidenceTemplateId,
                        ValveResidueEvidenceTemplateId,
                    },
                },
                new ScenarioAppealDefinition
                {
                    AppealId = SeraAppealId,
                    CaseId = LaterCaseId,
                    OpportunityId = SeraAppealOpportunityId,
                    AppellantRoleId = LaterClaimantRoleId,
                    FilingCycle = 16,
                    HearingCycle = 17,
                    ChallengedRulingId = LaterInitialRulingId,
                    ResultingRulingId = LaterAppealRulingId,
                    GroundsEvidenceTemplateIds = new List<string>
                    {
                        LaterSampleEvidenceTemplateId,
                    },
                },
            };
        }

        private static List<ScenarioHoldingDefinition> CreateHoldings()
        {
            return new List<ScenarioHoldingDefinition>
            {
                new ScenarioHoldingDefinition
                {
                    HoldingId = HoldingId,
                    ScopeId = "scope.glass.watershed-bound-weather-undissipated",
                    SourceAppealId = MaraAppealId,
                    SourceRulingId = PrimaryAppealRulingId,
                    RuleId = HoldingRuleId,
                    IssueId = IssueId,
                    EstablishedCycle = 12,
                    Retrospective = false,
                    RequiredScopeFacts = ScopeFacts(),
                    SupportingEvidenceTemplateIds = new List<string>
                    {
                        ResonanceEvidenceTemplateId,
                        ControllerLogEvidenceTemplateId,
                        ValveResidueEvidenceTemplateId,
                    },
                },
            };
        }

        private static List<ScenarioHoldingCitationDefinition> CreateHoldingCitations()
        {
            return new List<ScenarioHoldingCitationDefinition>
            {
                new ScenarioHoldingCitationDefinition
                {
                    CitationId = HoldingCitationId,
                    HoldingId = HoldingId,
                    TargetCaseId = LaterCaseId,
                    TargetRulingId = LaterAppealRulingId,
                },
            };
        }

        private static List<ScenarioActionCausedDescendantCaseDefinition>
            CreateDescendants()
        {
            return new List<ScenarioActionCausedDescendantCaseDefinition>
            {
                new ScenarioActionCausedDescendantCaseDefinition
                {
                    DescendantDefinitionId = "descendant.glass.second-plume-report",
                    CaseId = LaterCaseId,
                    ParentCaseId = PrimaryCaseId,
                    OpenCycle = 15,
                    TriggerCycle = 14,
                    TriggerRoleId = LaterClaimantRoleId,
                    TriggerActionKind = SocietyActionKind.Work,
                    TriggerOpportunityId = LaterReportOpportunityId,
                    OriginatingRulingId = PrimaryInitialRulingId,
                    ConnectedRoleIds = new List<string>
                    {
                        LaterClaimantRoleId,
                        CartridgeHolderRoleId,
                    },
                },
            };
        }

        private static List<ScenarioExclusiveEntitlementDefinition> CreateEntitlements()
        {
            return new List<ScenarioExclusiveEntitlementDefinition>
            {
                new ScenarioExclusiveEntitlementDefinition
                {
                    EntitlementId = FilterEntitlementId,
                    ResourceId = FilterResourceId,
                    OfficialStatusId = FilterEntitlementStatusId,
                    InitialHolderRoleId = CartridgeHolderRoleId,
                    Units = 1,
                },
            };
        }

        private static List<ScenarioExclusiveEntitlementTransferDefinition>
            CreateTransfers()
        {
            return new List<ScenarioExclusiveEntitlementTransferDefinition>
            {
                new ScenarioExclusiveEntitlementTransferDefinition
                {
                    TransferId = FilterTransferId,
                    Cycle = 17,
                    EntitlementId = FilterEntitlementId,
                    FromRoleId = CartridgeHolderRoleId,
                    ToRoleId = LaterClaimantRoleId,
                    CauseCaseId = LaterCaseId,
                    CauseRulingId = LaterAppealRulingId,
                    CauseHoldingId = HoldingId,
                    RequiredRulingDisposition =
                        RulingDisposition.ReversedAndRecognised,
                    GainKind = MaterialConsequenceKind.ResourceGranted,
                    LossKind = MaterialConsequenceKind.ResourceRevoked,
                    GainKindId = "material-kind.glass.filter-awarded",
                    LossKindId = "material-kind.glass.filter-displaced",
                },
            };
        }

        private static CaseFactSet ScopeFacts(string plumeId = null)
        {
            var facts = new List<CaseFact>
            {
                new CaseFact("permit-class", "bound-weather"),
                new CaseFact("output-state", "undissipated"),
                new CaseFact("watershed", "glass-canal"),
            };
            if (!string.IsNullOrEmpty(plumeId))
                facts.Add(new CaseFact("plume", plumeId));
            return new CaseFactSet(facts);
        }

        private static CaseFactSet LaterFacts()
        {
            return ScopeFacts("plume.secondary");
        }
    }
}
