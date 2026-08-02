using System;
using System.Collections.Generic;
using System.Reflection;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalScenarioDefinitionTests
    {
        [Test]
        public void CompletePlainDataDefinition_ValidatesAndSupportsDeterministicLookup()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();

            Assert.DoesNotThrow(() => InstitutionalScenarioDefinitionValidator.Validate(definition));

            var index = new InstitutionalScenarioDefinitionIndex(definition);
            Assert.That(index.GetRole("role.alpha"), Is.SameAs(definition.ParticipantRoles[0]));
            Assert.That(index.GetLivedIncidentSeed("incident-seed.001"),
                Is.SameAs(definition.LivedIncidentSeeds[0]));
            Assert.That(index.GetInitialEconomicAccount("account.001"),
                Is.SameAs(definition.InitialEconomicAccounts[0]));
            Assert.That(index.GetAlternative("alternative.001"),
                Is.SameAs(definition.Alternatives[0]));
            Assert.That(index.GetOpportunity("op.002-appeal"), Is.SameAs(definition.Opportunities[1]));
            Assert.That(index.GetScheduleEntry("schedule.003"),
                Is.SameAs(definition.CycleSchedule[2]));
            Assert.That(index.GetEvidenceTemplate("evidence.003"),
                Is.SameAs(definition.EvidenceTemplates[2]));
            Assert.That(index.GetCase("case.002"), Is.SameAs(definition.Cases[1]));
            Assert.That(index.GetStatusEffect("effect.001"),
                Is.SameAs(definition.OfficialStatusEffectRequests[0]));
            Assert.That(index.GetReliance("reliance.001"),
                Is.SameAs(definition.RelianceDefinitions[0]));
            Assert.That(index.GetRelianceRecovery("recovery.001"),
                Is.SameAs(definition.RelianceRecoveries[0]));
            Assert.That(index.GetAppeal("appeal.001"), Is.SameAs(definition.Appeals[0]));
            Assert.That(index.GetHolding("holding.001"), Is.SameAs(definition.Holdings[0]));
            Assert.That(index.GetDescendant("descendant.001"),
                Is.SameAs(definition.DescendantCases[0]));
            Assert.That(index.GetEntitlement("entitlement.001"),
                Is.SameAs(definition.ExclusiveEntitlements[0]));
            Assert.That(index.GetTransfer("transfer.001"),
                Is.SameAs(definition.EntitlementTransfers[0]));
        }

        [Test]
        public void Validator_RejectsDuplicateStableIds()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.ParticipantRoles.Insert(1, definition.ParticipantRoles[0]);

            Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
        }

        [Test]
        public void Validator_RejectsMissingReferences()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.EvidenceTemplates[0].CaseId = "case.missing";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("missing case"));
        }

        [Test]
        public void Validator_RejectsInvalidCycles()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.Cases[0].AdjudicationCycle = definition.EndCycle + 1;

            Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
        }

        [Test]
        public void Validator_RejectsDirectAgentIdsInOperations()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.OfficialStatusEffectRequests[0].TargetRoleId = "agent.001";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("forbidden direct agent id"));
        }

        [Test]
        public void Validator_RejectsSparseExecutableSchedule()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.CycleSchedule.RemoveAll(value => value.Cycle == 10);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("every cycle"));
        }

        [Test]
        public void Validator_RejectsImpossibleNonDisclosurePropositionFilter()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.EvidenceTemplates[0].RequiredPropositionId =
                "proposition.never-emitted";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("Only disclosure"));
        }

        [Test]
        public void Validator_RejectsDescendantOpportunityOutsideExactTriggerCycle()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.DescendantCases[0].TriggerCycle = 3;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("exact trigger cycle"));
        }

        [Test]
        public void Validator_RejectsEntitlementWithoutOneMatchingRecognisedHolder()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.ExclusiveEntitlements[0].OfficialStatusId =
                "status.no-initial-holder";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("exactly one recognised holder"));
        }

        [Test]
        public void Validator_RejectsTransferWhoseExactCauseRulingBelongsToAnotherCase()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.EntitlementTransfers[0].CauseRulingId =
                "ruling:case.001:adjudication:8";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));

            Assert.That(exception.Message, Does.Contain("exact cause case"));
        }

        [Test]
        public void Validator_RejectsTransferWithoutDeclaredExactCauseRuling()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.EntitlementTransfers[0].CauseRulingId = null;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));

            Assert.That(exception.Message, Does.Contain("cause ruling id"));
        }

        [Test]
        public void Validator_RejectsDirectAgentIdsHiddenInOpaqueCauseFields()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.Opportunities[0].SourceCauseId = "agent.001";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("forbidden direct agent id"));
        }

        [Test]
        public void DefinitionRecords_ContainNoDelegateFieldsOrDirectAgentOperationFields()
        {
            Type[] definitionTypes =
            {
                typeof(ScenarioParticipantQuery),
                typeof(ScenarioParticipantRoleDefinition),
                typeof(ScenarioLivedIncidentSeedDefinition),
                typeof(ScenarioInitialEconomicAccountDefinition),
                typeof(ScenarioAlternativeDefinition),
                typeof(ScenarioOpportunityDefinition),
                typeof(ScenarioCycleScheduleEntry),
                typeof(ScenarioEvidenceTemplateDefinition),
                typeof(ScenarioCaseDefinition),
                typeof(ScenarioOfficialStatusEffectRequest),
                typeof(ScenarioIrreversibleRelianceDefinition),
                typeof(ScenarioRelianceEffectDefinition),
                typeof(ScenarioRelianceRecoveryDefinition),
                typeof(ScenarioAppealDefinition),
                typeof(ScenarioHoldingDefinition),
                typeof(ScenarioActionCausedDescendantCaseDefinition),
                typeof(ScenarioExclusiveEntitlementDefinition),
                typeof(ScenarioExclusiveEntitlementTransferDefinition),
                typeof(InstitutionalScenarioDefinition),
            };

            for (int i = 0; i < definitionTypes.Length; i++)
            {
                FieldInfo[] fields = definitionTypes[i].GetFields(
                    BindingFlags.Public | BindingFlags.Instance);
                for (int j = 0; j < fields.Length; j++)
                {
                    Assert.That(typeof(Delegate).IsAssignableFrom(fields[j].FieldType), Is.False,
                        $"{definitionTypes[i].Name}.{fields[j].Name} stores executable behaviour.");
                }
            }

            Type[] operationTypes =
            {
                typeof(ScenarioOpportunityDefinition),
                typeof(ScenarioCycleScheduleEntry),
                typeof(ScenarioOfficialStatusEffectRequest),
                typeof(ScenarioIrreversibleRelianceDefinition),
                typeof(ScenarioRelianceRecoveryDefinition),
                typeof(ScenarioAppealDefinition),
                typeof(ScenarioActionCausedDescendantCaseDefinition),
                typeof(ScenarioExclusiveEntitlementTransferDefinition),
            };
            for (int i = 0; i < operationTypes.Length; i++)
            {
                FieldInfo[] fields = operationTypes[i].GetFields(
                    BindingFlags.Public | BindingFlags.Instance);
                for (int j = 0; j < fields.Length; j++)
                {
                    Assert.That(fields[j].Name.EndsWith("AgentId", StringComparison.Ordinal), Is.False,
                        $"{operationTypes[i].Name}.{fields[j].Name} bypasses role binding.");
                }
            }
        }

        private static InstitutionalScenarioDefinition ValidDefinition()
        {
            var definition = new InstitutionalScenarioDefinition
            {
                ScenarioId = "scenario.001",
                IncidentId = "incident.001",
                PrimaryCaseId = "case.001",
                StartCycle = 0,
                EndCycle = 10,
                InitialSociety = InitialSociety(),
            };

            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.alpha",
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.alpha",
                    RequiredEmployerId = "organisation.alpha",
                    RequiredRecognisedStatusIds = new List<string> { "status.alpha" },
                    RequiredAnomalyTraitIds = new List<string> { "trait.alpha" },
                    RequiredCommitmentKinds = new List<string> { "commitment.alpha" },
                },
                DistinctFromRoleIds = new List<string> { "role.beta" },
            });
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.beta",
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.beta",
                    RequiredEmployerId = "organisation.beta",
                    RequiredUnrecognisedStatusIds = new List<string> { "status.beta" },
                    RequiredCommitmentKinds = new List<string> { "commitment.beta" },
                },
                DistinctFromRoleIds = new List<string> { "role.alpha" },
            });

            definition.LivedIncidentSeeds.Add(new ScenarioLivedIncidentSeedDefinition
            {
                IncidentSeedId = "incident-seed.001",
                IncidentId = "incident.001",
                Cycle = 1,
                SubjectRoleId = "role.alpha",
                CauseEntityId = "entity.001",
                PropositionId = "proposition.incident.001",
                AffectedNeed = NeedKind.Safety,
                NeedPressureDelta = 8,
            });
            definition.InitialEconomicAccounts.Add(new ScenarioInitialEconomicAccountDefinition
            {
                AccountId = "account.001",
                OwnerRoleId = "role.alpha",
                InitialCredits = 100,
                CycleIncome = 5,
            });
            definition.InitialEconomicAccounts.Add(new ScenarioInitialEconomicAccountDefinition
            {
                AccountId = "account.002",
                OwnerRoleId = "role.beta",
                InitialCredits = 80,
                CycleIncome = 7,
            });
            definition.Alternatives.Add(new ScenarioAlternativeDefinition
            {
                AlternativeKey = "alternative.001",
                OwnerRoleId = "role.alpha",
                InitiallyAvailable = true,
                ResourceValue = 20,
            });

            definition.Cases.Add(Case(
                "case.001", "issue.001", "role.alpha", "role.beta", "fact.jurisdiction", "alpha",
                0, 4, 8, "ruling:case.001:initial:4",
                "ruling:case.001:adjudication:8"));
            definition.Cases.Add(Case(
                "case.002", "issue.001", "role.beta", "role.alpha", "fact.jurisdiction", "alpha",
                7, 8, 9, "ruling:case.002:initial:8",
                "ruling:case.002:adjudication:9"));
            definition.Cases[1].Facts.Add("fact.subject", "beta");
            definition.Cases[1].CitedHoldingIds.Add("holding.001");

            definition.Opportunities.Add(Opportunity(
                "op.001-aid", ScenarioOpportunityKind.Aid, 5, 5, "role.alpha"));
            definition.Opportunities[0].RequiredOfficialStatusId = "status.relief";
            definition.Opportunities[0].RequiredOfficialStatusRecognised = true;
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = "op.002-appeal",
                Kind = ScenarioOpportunityKind.Appeal,
                PurposeId = "purpose.002",
                SourceCauseId = "cause.002",
                AvailabilityStartCycle = 6,
                AvailabilityEndCycle = 7,
                UtilityBonus = 10,
                CaseId = "case.001",
                ChallengedRulingId = "ruling:case.001:initial:4",
                HearingCycle = 8,
                EligibleRoleIds = new List<string> { "role.alpha" },
            });
            definition.Opportunities.Add(Opportunity(
                "op.003-work", ScenarioOpportunityKind.Work, 2, 2, "role.beta"));

            definition.CycleSchedule.Add(Schedule(
                "schedule.001", 1, false, false, false, false, null));
            definition.CycleSchedule.Add(Schedule(
                "schedule.002", 2, true, false, false, false, null, "op.003-work"));
            definition.CycleSchedule.Add(Schedule(
                "schedule.003", 3, false, false, false, false, null));
            definition.CycleSchedule.Add(Schedule(
                "schedule.004", 4, false, false, false, false, null));
            definition.CycleSchedule.Add(Schedule(
                "schedule.005", 5, false, true, false, false, null, "op.001-aid"));
            definition.CycleSchedule.Add(Schedule(
                "schedule.006", 6, false, false, true, true, "docket.001", "op.002-appeal"));
            definition.CycleSchedule.Add(Schedule(
                "schedule.007", 7, false, false, false, false, null));
            definition.CycleSchedule.Add(Schedule(
                "schedule.008", 8, false, false, false, false, null));
            definition.CycleSchedule.Add(Schedule(
                "schedule.009", 9, false, false, false, false, null));
            definition.CycleSchedule.Add(Schedule(
                "schedule.010", 10, false, false, false, false, null));

            definition.EvidenceTemplates.Add(Evidence(
                "evidence.001", SocietyEventKind.AidRequested, "op.001-aid",
                null, "case.001", "issue.001", EvidenceEffect.SupportsFinding));
            definition.EvidenceTemplates.Add(Evidence(
                "evidence.002", SocietyEventKind.AppealFiled, "op.002-appeal",
                null, "case.001", "issue.001", EvidenceEffect.OpposesFinding));
            definition.EvidenceTemplates.Add(Evidence(
                "evidence.003", SocietyEventKind.WorkPerformed, "op.003-work",
                null, "case.002", "issue.001", EvidenceEffect.SupportsFinding));

            definition.OfficialStatusEffectRequests.Add(new ScenarioOfficialStatusEffectRequest
            {
                EffectRequestId = "effect.001",
                Cycle = 4,
                CauseCaseId = "case.001",
                CauseRulingId = "ruling:case.001:initial:4",
                RequiredRulingDisposition = RulingDisposition.ProvisionallyRecognised,
                TargetRoleId = "role.alpha",
                StatusId = "status.relief",
                RequestedRecognisedState = true,
                RequestedResourceDelta = 25,
            });
            definition.OfficialStatusEffectRequests.Add(new ScenarioOfficialStatusEffectRequest
            {
                EffectRequestId = "effect.002",
                Cycle = 4,
                CauseCaseId = "case.001",
                CauseRulingId = "ruling:case.001:initial:4",
                RequiredRulingDisposition = RulingDisposition.ProvisionallyRecognised,
                TargetRoleId = "role.alpha",
                StatusId = InstitutionalStatusIds.AdverseDecision,
                RequestedRecognisedState = true,
            });

            definition.RelianceDefinitions.Add(new ScenarioIrreversibleRelianceDefinition
            {
                RelianceId = "reliance.001",
                Cycle = 5,
                RelyingRoleId = "role.alpha",
                SourceOpportunityId = "op.001-aid",
                SourceActionKind = SocietyActionKind.SeekAid,
                EnablingEffectRequestId = "effect.001",
                EnablingRulingId = "ruling:case.001:initial:4",
                IrreversibleChoiceKey = "choice.001",
                AbandonedAlternativeKey = "alternative.001",
                ExpectedStatusId = "status.relief",
                ExpectedRecognisedState = true,
                BeneficiaryRoleId = "role.alpha",
                ResourceId = "resource.reliance.001",
                Effects = new List<ScenarioRelianceEffectDefinition>
                {
                    new()
                    {
                        EffectId = "reliance-effect.001",
                        Recipient = ScenarioRelianceEffectRecipient.RelyingRole,
                        ResourceDelta = -20,
                        MaterialKind = MaterialConsequenceKind.RelianceSpent,
                        MaterialKindId = "material-kind.reliance-spent",
                        ResourceId = "resource.reliance.001",
                    },
                },
            });

            definition.Appeals.Add(new ScenarioAppealDefinition
            {
                AppealId = "appeal.001",
                CaseId = "case.001",
                OpportunityId = "op.002-appeal",
                AppellantRoleId = "role.alpha",
                FilingCycle = 6,
                HearingCycle = 8,
                ChallengedRulingId = "ruling:case.001:initial:4",
                ResultingRulingId = "ruling:case.001:adjudication:8",
                ResultingHoldingId = "holding.001",
                GroundsEvidenceTemplateIds = new List<string> { "evidence.002" },
            });
            definition.RelianceRecoveries.Add(new ScenarioRelianceRecoveryDefinition
            {
                RecoveryDefinitionId = "recovery.001",
                RelianceId = "reliance.001",
                Cycle = 8,
                TriggerReversalRulingId = "ruling:case.001:adjudication:8",
                CaseIdPrefix = "case.recovery",
                ParentCaseId = "case.001",
                ClaimantRoleId = "role.alpha",
                RespondentRoleId = "role.beta",
                IssueId = "issue.reliance-recovery",
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("fact.reliance-kind", "irreversible-choice"),
                }),
            });
            definition.Holdings.Add(new ScenarioHoldingDefinition
            {
                HoldingId = "holding.001",
                ScopeId = "scope.001",
                SourceAppealId = "appeal.001",
                SourceRulingId = "ruling:case.001:adjudication:8",
                RuleId = "rule.001",
                IssueId = "issue.001",
                EstablishedCycle = 8,
                Retrospective = false,
                RequiredScopeFacts = new CaseFactSet(new[] { new CaseFact("fact.jurisdiction", "alpha") }),
                SupportingEvidenceTemplateIds = new List<string> { "evidence.002" },
            });

            definition.DescendantCases.Add(new ScenarioActionCausedDescendantCaseDefinition
            {
                DescendantDefinitionId = "descendant.001",
                CaseId = "case.002",
                ParentCaseId = "case.001",
                OpenCycle = 7,
                TriggerCycle = 2,
                TriggerRoleId = "role.beta",
                TriggerActionKind = SocietyActionKind.Work,
                TriggerOpportunityId = "op.003-work",
                TriggerPropositionId = null,
                OriginatingRulingId = "ruling:case.001:initial:4",
                ConnectedRoleIds = new List<string> { "role.alpha", "role.beta" },
            });

            definition.ExclusiveEntitlements.Add(new ScenarioExclusiveEntitlementDefinition
            {
                EntitlementId = "entitlement.001",
                ResourceId = "resource.001",
                OfficialStatusId = "status.alpha",
                InitialHolderRoleId = "role.alpha",
                Units = 1,
            });
            definition.EntitlementTransfers.Add(new ScenarioExclusiveEntitlementTransferDefinition
            {
                TransferId = "transfer.001",
                Cycle = 9,
                EntitlementId = "entitlement.001",
                FromRoleId = "role.alpha",
                ToRoleId = "role.beta",
                CauseCaseId = "case.002",
                CauseRulingId = "ruling:case.002:adjudication:9",
                CauseHoldingId = "holding.001",
            });

            return definition;
        }

        private static SocietyState InitialSociety()
        {
            var society = new SocietyState
            {
                MasterSeed = 42,
                CurrentTick = 0,
                Regime = new InstitutionalRegimeState(),
            };
            society.Agents.Add(Agent(
                "agent.001", 0, "species.alpha", "organisation.alpha",
                "status.alpha", true, "commitment.alpha", "trait.alpha"));
            society.Agents.Add(Agent(
                "agent.002", 1, "species.beta", "organisation.beta",
                "status.beta", false, "commitment.beta", null));
            return society;
        }

        private static AgentState Agent(
            string id,
            int ordinal,
            string species,
            string employer,
            string statusId,
            bool recognised,
            string commitmentKind,
            string anomalyTrait)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = "presentation." + ordinal,
                DisplayName = "Participant " + ordinal,
                SpeciesId = species,
                HouseholdId = "household." + ordinal,
                EmployerId = employer,
                InstitutionalTrust = 50,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = 50,
                    Candour = 50,
                    Solidarity = 50,
                    Duty = 50,
                    InstitutionalReliance = 50,
                },
            };
            foreach (NeedKind need in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = need, Pressure = 20 });
            agent.Standing.SetRecognised(statusId, recognised);
            agent.Commitments.Add(new CommitmentState
            {
                CommitmentId = "commitment." + ordinal,
                Kind = commitmentKind,
                TargetId = employer,
                Strength = 70,
            });
            if (!string.IsNullOrEmpty(anomalyTrait))
            {
                agent.AnomalyRules.Add(new AnomalyStatusRule
                {
                    TraitId = anomalyTrait,
                    RequiredOfficialStatusId = statusId,
                    AffectedNeed = NeedKind.Safety,
                    RecognisedPressureDelta = -1,
                    UnrecognisedPressureDelta = 2,
                    MinimumTicksBetweenActivations = 3,
                    LastAppliedTick = -1,
                    ObservableEffectId = "effect.observable." + ordinal,
                });
            }
            return agent;
        }

        private static ScenarioCaseDefinition Case(
            string caseId,
            string issueId,
            string claimantRoleId,
            string respondentRoleId,
            string factKey,
            string factValue,
            long openCycle,
            long initialCycle,
            long adjudicationCycle,
            string initialRulingId,
            string adjudicationRulingId)
        {
            return new ScenarioCaseDefinition
            {
                CaseId = caseId,
                IssueId = issueId,
                ClaimantRoleId = claimantRoleId,
                RespondentRoleId = respondentRoleId,
                Facts = new CaseFactSet(new[] { new CaseFact(factKey, factValue) }),
                OpenCycle = openCycle,
                InitialEvidenceCutoffCycle = initialCycle,
                InitialRulingCycle = initialCycle,
                AdjudicationEvidenceCutoffCycle = adjudicationCycle,
                AdjudicationCycle = adjudicationCycle,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "adjudication",
                InitialRulingId = initialRulingId,
                AdjudicationRulingId = adjudicationRulingId,
                InitialScoreThreshold = 40,
                ProvisionalScoreThreshold = 20,
                ProvisionalRecognitionPermitted = true,
                AdjudicationScoreThreshold = 50,
            };
        }

        private static ScenarioOpportunityDefinition Opportunity(
            string id,
            ScenarioOpportunityKind kind,
            long start,
            long end,
            string roleId)
        {
            return new ScenarioOpportunityDefinition
            {
                OpportunityId = id,
                Kind = kind,
                PurposeId = "purpose." + id,
                SourceCauseId = "cause." + id,
                AvailabilityStartCycle = start,
                AvailabilityEndCycle = end,
                UtilityBonus = 10,
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { roleId },
            };
        }

        private static ScenarioCycleScheduleEntry Schedule(
            string id,
            long cycle,
            bool work,
            bool aid,
            bool disclosure,
            bool appeal,
            string docketId,
            params string[] opportunityIds)
        {
            return new ScenarioCycleScheduleEntry
            {
                ScheduleEntryId = id,
                IncidentId = "incident.001",
                Cycle = cycle,
                WorkAvailable = work,
                AidAvailable = aid,
                DisclosureRequested = disclosure,
                AppealWindowOpen = appeal,
                OpenDocketId = docketId,
                Visibility = ScenarioVisibilityMode.ListedRoles,
                VisibleRoleIds = new List<string> { "role.alpha", "role.beta" },
                ActiveOpportunityIds = opportunityIds == null
                    ? new List<string>()
                    : new List<string>(opportunityIds),
            };
        }

        private static ScenarioEvidenceTemplateDefinition Evidence(
            string id,
            SocietyEventKind eventKind,
            string opportunityId,
            string propositionId,
            string caseId,
            string issueId,
            EvidenceEffect effect)
        {
            return new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = id,
                SourceEventKind = eventKind,
                SourceOpportunityId = opportunityId,
                RequiredPropositionId = propositionId,
                CaseId = caseId,
                IssueId = issueId,
                EvidenceClassId = "evidence-class." + id,
                Effect = effect,
                Weight = 25,
                Visibility = EvidenceVisibility.OfficialRecord,
            };
        }
    }
}
