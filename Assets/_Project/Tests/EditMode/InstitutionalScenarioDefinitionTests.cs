using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Desk42.Institutional;
using Desk42.Institutional.Scenarios.WorkplaceIdentity;
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
            Assert.That(index.GetHoldingCitation("citation.001"),
                Is.SameAs(definition.HoldingCitations[0]));
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
        public void Validator_BoundsReliancePublicObservationAfterItsAction()
        {
            InstitutionalScenarioDefinition valid = ValidDefinition();
            valid.RelianceDefinitions[0].PublicObservationCycle =
                valid.RelianceDefinitions[0].Cycle + 1;
            Assert.DoesNotThrow(
                () => InstitutionalScenarioDefinitionValidator.Validate(valid));

            InstitutionalScenarioDefinition early = ValidDefinition();
            early.RelianceDefinitions[0].PublicObservationCycle =
                early.RelianceDefinitions[0].Cycle - 1;
            Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(early));

            InstitutionalScenarioDefinition late = ValidDefinition();
            late.RelianceDefinitions[0].PublicObservationCycle =
                late.EndCycle + 1;
            Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(late));

            InstitutionalScenarioDefinition sameCycleRecovery = ValidDefinition();
            sameCycleRecovery.RelianceDefinitions[0].PublicObservationCycle =
                sameCycleRecovery.RelianceRecoveries[0].Cycle;
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioDefinitionValidator.Validate(sameCycleRecovery));
        }

        [Test]
        public void Validator_RejectsMultipleReliancesBoundToOneObservedAction()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.Alternatives.Add(new ScenarioAlternativeDefinition
            {
                AlternativeKey = "alternative.002",
                OwnerRoleId = "role.alpha",
                InitiallyAvailable = true,
                ResourceValue = 10,
            });
            definition.RelianceDefinitions.Add(new ScenarioIrreversibleRelianceDefinition
            {
                RelianceId = "reliance.002",
                Cycle = 5,
                RelyingRoleId = "role.alpha",
                SourceOpportunityId = "op.001-aid",
                SourceActionKind = SocietyActionKind.SeekAid,
                EnablingEffectRequestId = "effect.001",
                EnablingRulingId = "ruling:case.001:initial:4",
                IrreversibleChoiceKey = "choice.002",
                AbandonedAlternativeKey = "alternative.002",
                ExpectedStatusId = "status.relief",
                ExpectedRecognisedState = true,
                BeneficiaryRoleId = "role.alpha",
                ResourceId = "resource.reliance.002",
                Effects = new List<ScenarioRelianceEffectDefinition>
                {
                    new()
                    {
                        EffectId = "reliance-effect.002",
                        Recipient = ScenarioRelianceEffectRecipient.RelyingRole,
                        ResourceDelta = -10,
                        MaterialKind = MaterialConsequenceKind.RelianceSpent,
                        MaterialKindId = "material-kind.reliance-spent",
                        ResourceId = "resource.reliance.002",
                    },
                },
            });

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message,
                Does.Contain("same role, cycle and source opportunity"));
        }

        [Test]
        public void Validator_RejectsRelianceOnAnOpportunityWithoutFrozenStatusInput()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.RelianceDefinitions[0].SourceActionKind =
                SocietyActionKind.Appeal;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message,
                Does.Contain("status-bearing work or aid action"));
        }

        [Test]
        public void Validator_RequiresDistinctRolesForReliancesSharingAnOpportunityCycle()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            ScenarioParticipantQuery overlappingQuery =
                definition.ParticipantRoles[0].Query;
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = "role.gamma",
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = overlappingQuery.RequiredSpeciesId,
                    RequiredEmployerId = overlappingQuery.RequiredEmployerId,
                    RequiredRecognisedStatusIds = new List<string>(
                        overlappingQuery.RequiredRecognisedStatusIds),
                    RequiredUnrecognisedStatusIds = new List<string>(
                        overlappingQuery.RequiredUnrecognisedStatusIds),
                    RequiredAnomalyTraitIds = new List<string>(
                        overlappingQuery.RequiredAnomalyTraitIds),
                    RequiredCommitmentKinds = new List<string>(
                        overlappingQuery.RequiredCommitmentKinds),
                },
            });
            definition.Opportunities.Single(value =>
                value.OpportunityId == "op.001-aid").EligibleRoleIds.Add("role.gamma");
            definition.OfficialStatusEffectRequests.Insert(
                1,
                new ScenarioOfficialStatusEffectRequest
                {
                    EffectRequestId = "effect.001-beta",
                    Cycle = 4,
                    CauseCaseId = "case.001",
                    CauseRulingId = "ruling:case.001:initial:4",
                    RequiredRulingDisposition =
                        RulingDisposition.ProvisionallyRecognised,
                    TargetRoleId = "role.gamma",
                    StatusId = "status.relief",
                    RequestedRecognisedState = true,
                    RequestedResourceDelta = 25,
                });
            definition.Alternatives.Add(new ScenarioAlternativeDefinition
            {
                AlternativeKey = "alternative.002",
                OwnerRoleId = "role.gamma",
                InitiallyAvailable = true,
                ResourceValue = 10,
            });
            definition.RelianceDefinitions.Add(new ScenarioIrreversibleRelianceDefinition
            {
                RelianceId = "reliance.002",
                Cycle = 5,
                RelyingRoleId = "role.gamma",
                SourceOpportunityId = "op.001-aid",
                SourceActionKind = SocietyActionKind.SeekAid,
                EnablingEffectRequestId = "effect.001-beta",
                EnablingRulingId = "ruling:case.001:initial:4",
                IrreversibleChoiceKey = "choice.002",
                AbandonedAlternativeKey = "alternative.002",
                ExpectedStatusId = "status.relief",
                ExpectedRecognisedState = true,
                BeneficiaryRoleId = "role.gamma",
                ResourceId = "resource.reliance.002",
                Effects = new List<ScenarioRelianceEffectDefinition>
                {
                    new()
                    {
                        EffectId = "reliance-effect.002",
                        Recipient = ScenarioRelianceEffectRecipient.RelyingRole,
                        ResourceDelta = -10,
                        MaterialKind = MaterialConsequenceKind.RelianceSpent,
                        MaterialKindId = "material-kind.reliance-spent",
                        ResourceId = "resource.reliance.002",
                    },
                },
            });
            definition.InitialEconomicAccounts.Add(
                new ScenarioInitialEconomicAccountDefinition
                {
                    AccountId = "account.003",
                    OwnerRoleId = "role.gamma",
                    InitialCredits = 60,
                    CycleIncome = 3,
                });

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message,
                Does.Contain("reliance source roles sharing an opportunity cycle"));
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
        public void Validator_RejectsTransferWithoutExplicitValidDisposition()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.EntitlementTransfers[0].RequiredRulingDisposition =
                (RulingDisposition)999;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));

            Assert.That(exception.Message, Does.Contain("valid ruling disposition"));
        }

        [Test]
        public void Validator_RejectsTransferDispositionImpossibleForCauseRulingPhase()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.EntitlementTransfers[0].RequiredRulingDisposition =
                RulingDisposition.Recognised;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));

            Assert.That(exception.Message, Does.Contain("cannot materialise"));
        }

        [Test]
        public void Validator_RejectsTransferWhoseDerivedConnectedPairIdIsTooLong()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.EntitlementTransfers[0].TransferId = new string('t', 119);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));

            Assert.That(exception.Message,
                Does.Contain("connected-outcome pair id"));
        }

        [Test]
        public void Validator_RejectsTransferAfterExactCauseRulingCycle()
        {
            InstitutionalScenarioDefinition definition =
                WorkplaceIdentityScenario.CreateDefinition();
            definition.EntitlementTransfers[0].Cycle++;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));

            Assert.That(exception.Message,
                Does.Contain("exact cause holding and ruling citation"));
        }

        [Test]
        public void Validator_RejectsTransferWhoseHoldingIsDeclaredForAnotherRuling()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.EntitlementTransfers[0].CauseRulingId =
                "ruling:case.002:initial:8";
            definition.EntitlementTransfers[0].Cycle = 8;
            definition.EntitlementTransfers[0].RequiredRulingDisposition =
                RulingDisposition.Denied;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));

            Assert.That(exception.Message, Does.Contain("exact cause holding and ruling"));
        }

        [Test]
        public void Validator_RejectsInitialCitationEstablishedLaterInSameCycle()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.HoldingCitations[0].TargetRulingId =
                "ruling:case.002:initial:8";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));

            Assert.That(exception.Message, Does.Contain("execution order"));
        }

        [Test]
        public void Validator_AcceptsSameCycleAdjudicationCitation()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            ScenarioCaseDefinition target = definition.Cases[1];
            target.OpenCycle = 6;
            target.InitialEvidenceCutoffCycle = 6;
            target.InitialRulingCycle = 6;
            target.InitialRulingId = "ruling:case.002:initial:6";
            target.AdjudicationEvidenceCutoffCycle = 8;
            target.AdjudicationCycle = 8;
            target.AdjudicationRulingId =
                "ruling:case.002:adjudication:8";
            definition.DescendantCases[0].OpenCycle = 6;

            ScenarioOpportunityDefinition opportunity = definition.Opportunities.Single(
                item => item.OpportunityId == "op.003-appeal-second");
            opportunity.AvailabilityStartCycle = 7;
            opportunity.AvailabilityEndCycle = 7;
            opportunity.ChallengedRulingId = target.InitialRulingId;
            opportunity.HearingCycle = 8;
            ScenarioCycleScheduleEntry filing = definition.CycleSchedule.Single(
                item => item.Cycle == 7);
            filing.AppealWindowOpen = true;
            filing.OpenDocketId = "docket.002.same-cycle";
            filing.ActiveOpportunityIds.Add(opportunity.OpportunityId);
            ScenarioCycleScheduleEntry oldFiling = definition.CycleSchedule.Single(
                item => item.Cycle == 9);
            oldFiling.AppealWindowOpen = false;
            oldFiling.OpenDocketId = null;
            oldFiling.ActiveOpportunityIds.Clear();

            ScenarioAppealDefinition appeal = definition.Appeals.Single(
                item => item.AppealId == "appeal.002");
            appeal.FilingCycle = 7;
            appeal.HearingCycle = 8;
            appeal.ChallengedRulingId = target.InitialRulingId;
            appeal.ResultingRulingId = target.AdjudicationRulingId;
            ScenarioOfficialStatusEffectRequest adverse =
                definition.OfficialStatusEffectRequests.Single(
                    item => item.EffectRequestId == "effect.003");
            adverse.Cycle = 6;
            adverse.CauseRulingId = target.InitialRulingId;
            definition.HoldingCitations[0].TargetRulingId =
                target.AdjudicationRulingId;
            definition.EntitlementTransfers[0].Cycle = 8;
            definition.EntitlementTransfers[0].CauseRulingId =
                target.AdjudicationRulingId;

            Assert.DoesNotThrow(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
        }

        [Test]
        public void Validator_RejectsAdjudicationCitationWithoutExactAppealRoute()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.Appeals.RemoveAll(item => item.AppealId == "appeal.002");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));

            Assert.That(exception.Message, Does.Contain("exact declared appeal route"));
        }

        [Test]
        public void Validator_RejectsDuplicateExactHoldingRulingCitation()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.HoldingCitations.Add(new ScenarioHoldingCitationDefinition
            {
                CitationId = "citation.002",
                HoldingId = "holding.001",
                TargetCaseId = "case.002",
                TargetRulingId = "ruling:case.002:adjudication:10",
            });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("exact target ruling"));
        }

        [Test]
        public void Validator_RejectsCitationWhoseRulingBelongsToAnotherCase()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.HoldingCitations[0].TargetCaseId = "case.001";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("exact target case"));
        }

        [Test]
        public void Validator_RejectsHoldingSourceRulingSelfCitation()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.HoldingCitations[0].TargetCaseId = "case.001";
            definition.HoldingCitations[0].TargetRulingId =
                "ruling:case.001:adjudication:8";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("cannot cite itself"));
        }

        [Test]
        public void Validator_AcceptsSameCycleEvidenceActivation()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.DescendantCases.Clear();
            definition.Cases[1].OpenCycle = 2;
            definition.EvidenceActivatedCases.Add(new ScenarioEvidenceActivatedCaseDefinition
            {
                ActivationId = "activation.001",
                CaseId = "case.002",
                EvidenceTemplateId = "evidence.003",
                TriggerCycle = 2,
            });

            Assert.DoesNotThrow(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
        }

        [Test]
        public void Validator_RejectsEvidenceActivationAfterOpenCycle()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.DescendantCases.Clear();
            definition.EvidenceActivatedCases.Add(new ScenarioEvidenceActivatedCaseDefinition
            {
                ActivationId = "activation.001",
                CaseId = "case.002",
                EvidenceTemplateId = "evidence.003",
                TriggerCycle = 8,
            });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("cannot follow"));
        }

        [Test]
        public void Validator_RejectsNonOpportunityEvidenceActivation()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.DescendantCases.Clear();
            definition.Cases[1].OpenCycle = 2;
            ScenarioEvidenceTemplateDefinition template =
                definition.EvidenceTemplates.Single(value =>
                    value.EvidenceTemplateId == "evidence.003");
            template.SourceEventKind = SocietyEventKind.NoActionObserved;
            template.SourceOpportunityId = null;
            definition.EvidenceActivatedCases.Add(
                new ScenarioEvidenceActivatedCaseDefinition
                {
                    ActivationId = "activation.001",
                    CaseId = "case.002",
                    EvidenceTemplateId = "evidence.003",
                    TriggerCycle = 2,
                });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("capacity-reserved"));
        }

        [Test]
        public void Validator_RejectsActivationOnInitializationCycle()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.DescendantCases.Clear();
            definition.Cases[1].OpenCycle = 2;
            definition.EvidenceActivatedCases.Add(
                new ScenarioEvidenceActivatedCaseDefinition
                {
                    ActivationId = "activation.001",
                    CaseId = "case.002",
                    EvidenceTemplateId = "evidence.003",
                    TriggerCycle = definition.StartCycle,
                });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("initialization-only"));
        }

        [Test]
        public void Validator_RejectsActivationWhenOpportunityIsInactiveThatCycle()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.DescendantCases.Clear();
            definition.Cases[1].OpenCycle = 3;
            definition.EvidenceActivatedCases.Add(
                new ScenarioEvidenceActivatedCaseDefinition
                {
                    ActivationId = "activation.001",
                    CaseId = "case.002",
                    EvidenceTemplateId = "evidence.003",
                    TriggerCycle = 3,
                });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("not active"));
        }

        [Test]
        public void Validator_RejectsCaseWithTwoActivationContracts()
        {
            InstitutionalScenarioDefinition definition = ValidDefinition();
            definition.EvidenceActivatedCases.Add(new ScenarioEvidenceActivatedCaseDefinition
            {
                ActivationId = "activation.001",
                CaseId = "case.002",
                EvidenceTemplateId = "evidence.003",
                TriggerCycle = 2,
            });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalScenarioDefinitionValidator.Validate(definition));
            Assert.That(exception.Message, Does.Contain("both evidence-activated and action-caused"));
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
                typeof(ScenarioEvidenceActivatedCaseDefinition),
                typeof(ScenarioOfficialStatusEffectRequest),
                typeof(ScenarioIrreversibleRelianceDefinition),
                typeof(ScenarioRelianceEffectDefinition),
                typeof(ScenarioRelianceRecoveryDefinition),
                typeof(ScenarioAppealDefinition),
                typeof(ScenarioHoldingDefinition),
                typeof(ScenarioHoldingCitationDefinition),
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
                typeof(ScenarioEvidenceActivatedCaseDefinition),
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
                7, 8, 10, "ruling:case.002:initial:8",
                "ruling:case.002:adjudication:10"));
            definition.Cases[1].Facts.Add("fact.subject", "beta");

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
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = "op.003-appeal-second",
                Kind = ScenarioOpportunityKind.Appeal,
                PurposeId = "purpose.003",
                SourceCauseId = "cause.003",
                AvailabilityStartCycle = 9,
                AvailabilityEndCycle = 9,
                UtilityBonus = 10,
                CaseId = "case.002",
                ChallengedRulingId = "ruling:case.002:initial:8",
                HearingCycle = 10,
                EligibleRoleIds = new List<string> { "role.beta" },
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
                "schedule.009", 9, false, false, false, true, "docket.002",
                "op.003-appeal-second"));
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
            definition.OfficialStatusEffectRequests.Add(new ScenarioOfficialStatusEffectRequest
            {
                EffectRequestId = "effect.003",
                Cycle = 8,
                CauseCaseId = "case.002",
                CauseRulingId = "ruling:case.002:initial:8",
                RequiredRulingDisposition = RulingDisposition.Denied,
                TargetRoleId = "role.beta",
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
            definition.Appeals.Add(new ScenarioAppealDefinition
            {
                AppealId = "appeal.002",
                CaseId = "case.002",
                OpportunityId = "op.003-appeal-second",
                AppellantRoleId = "role.beta",
                FilingCycle = 9,
                HearingCycle = 10,
                ChallengedRulingId = "ruling:case.002:initial:8",
                ResultingRulingId = "ruling:case.002:adjudication:10",
                GroundsEvidenceTemplateIds = new List<string> { "evidence.003" },
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
            definition.HoldingCitations.Add(new ScenarioHoldingCitationDefinition
            {
                CitationId = "citation.001",
                HoldingId = "holding.001",
                TargetCaseId = "case.002",
                TargetRulingId = "ruling:case.002:adjudication:10",
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
                Cycle = 10,
                EntitlementId = "entitlement.001",
                FromRoleId = "role.alpha",
                ToRoleId = "role.beta",
                CauseCaseId = "case.002",
                CauseRulingId = "ruling:case.002:adjudication:10",
                CauseHoldingId = "holding.001",
                RequiredRulingDisposition = RulingDisposition.ReversedAndRecognised,
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
