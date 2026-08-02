using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalScenarioEngineTests
    {
        [Test]
        public void Run_IssuesDeclaredInitialRulingAndMutatesOnlyAfterFrozenDecision()
        {
            InstitutionalScenarioDefinition definition = Definition("alpha");
            string claimantRoleId = "role.alpha.claimant";
            string claimantAgentId = "agent.alpha.claimant";
            string statusId = "status.alpha.after-ruling";

            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                definition,
                Policy("alpha"));

            Assert.That(result.Report.Rulings, Has.Count.EqualTo(1));
            Assert.That(result.Report.Rulings[0].RulingId,
                Is.EqualTo("ruling:case.alpha:initial:1"));
            Assert.That(result.Report.Rulings[0].Disposition,
                Is.EqualTo(RulingDisposition.Denied));
            Assert.That(result.Report.FinalCycle, Is.EqualTo(2));
            Assert.That(result.AgentIdByRole[claimantRoleId], Is.EqualTo(claimantAgentId));
            Assert.That(result.AssessorRun, Is.Not.Null);
            Assert.That(result.EntitlementRegistry.Count, Is.Zero);

            AgentActionTrace firstClaimantDecision = result.AssessorRun.AssessorActionTraces
                .Single(trace => trace.Cycle == 1 && trace.ActorId == claimantAgentId);
            Assert.That(
                firstClaimantDecision.PerceptionSnapshot.Standing.IsRecognised(statusId),
                Is.False,
                "The cycle-one decision must not observe its later deadline mutation.");
            Assert.That(
                result.AssessorRun.FinalSocietyState.GetAgent(claimantAgentId)
                    .Standing.IsRecognised(statusId),
                Is.True);
            Assert.That(result.Report.OfficialStatusMutations, Has.Count.EqualTo(1));

            Assert.That(definition.InitialSociety.CurrentTick, Is.Zero);
            Assert.That(
                definition.InitialSociety.GetAgent(claimantAgentId)
                    .Standing.IsRecognised(statusId),
                Is.False,
                "Execution must not mutate authored scenario state.");
        }

        [Test]
        public void Run_RenamedEquivalentScenarioPreservesShapeWithoutIdentifierLeakage()
        {
            InstitutionalScenarioRunResult first = InstitutionalScenarioEngine.Run(
                Definition("first"),
                Policy("first"));
            InstitutionalScenarioRunResult second = InstitutionalScenarioEngine.Run(
                Definition("second"),
                Policy("second"));

            Assert.That(first.Report.Rulings[0].RulingId,
                Is.EqualTo("ruling:case.first:initial:1"));
            Assert.That(second.Report.Rulings[0].RulingId,
                Is.EqualTo("ruling:case.second:initial:1"));
            Assert.That(second.Report.PrimaryCaseId, Is.EqualTo("case.second"));
            Assert.That(second.Report.Rulings[0].CaseId, Is.EqualTo("case.second"));
            Assert.That(second.Report.Rulings[0].RulingId, Does.Not.Contain("first"));

            CollectionAssert.AreEqual(
                first.Report.Rulings.Select(ruling => ruling.Disposition),
                second.Report.Rulings.Select(ruling => ruling.Disposition));
            CollectionAssert.AreEqual(
                first.Report.Timeline.Select(entry => entry.Kind),
                second.Report.Timeline.Select(entry => entry.Kind));
            Assert.That(first.Report.ObservedAgentActions.Count,
                Is.EqualTo(second.Report.ObservedAgentActions.Count));
        }

        [Test]
        public void Run_ExactTriggerEvidenceMayPrecedeOnlyItsMaterialisedDescendant()
        {
            InstitutionalScenarioRunResult result = InstitutionalScenarioEngine.Run(
                DescendantTriggerDefinition(),
                Policy("trigger"));

            DescendantCase descendant = result.Report.DescendantCases.Single();
            Assert.That(descendant.CaseId, Is.EqualTo("case.trigger-descendant"));
            Assert.That(descendant.OpenedCycle, Is.EqualTo(3));
            EvidenceArtifact triggerEvidence = result.Report.EvidenceArtifacts.Single(value =>
                value.CaseId == descendant.CaseId);
            Assert.That(triggerEvidence.EnteredCycle, Is.EqualTo(2));
            Assert.That(triggerEvidence.EnteredCycle, Is.LessThan(descendant.OpenedCycle));
            Assert.That(triggerEvidence.Provenance.SourceSocietyEventId,
                Is.EqualTo(descendant.CausalAgentActionId));
            Assert.That(result.Report.Rulings.Any(value =>
                value.CaseId == descendant.CaseId), Is.True);
        }

        private static InstitutionalScenarioDefinition DescendantTriggerDefinition()
        {
            const string claimantRoleId = "role.trigger.claimant";
            const string respondentRoleId = "role.trigger.respondent";
            const string claimantAgentId = "agent.trigger.claimant";
            const string respondentAgentId = "agent.trigger.respondent";
            const string primaryCaseId = "case.trigger";
            const string descendantCaseId = "case.trigger-descendant";
            const string issueId = "issue.trigger";
            const string opportunityId = "opportunity.trigger.aid";
            const string propositionId = "proposition.trigger.disclosure";

            AgentState claimant = Agent(
                claimantAgentId,
                0,
                "species.trigger.claimant",
                null);
            claimant.Standing.CanWork = true;
            claimant.Standing.CanGiveEvidence = true;
            claimant.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.trigger",
                PropositionId = propositionId,
                SubjectId = claimantAgentId,
                ObjectId = "object.trigger",
                SourceId = "record.trigger",
                Confidence = 100,
                Secrecy = 0,
                EmotionalWeight = 100,
                AcquiredTick = 0,
            });
            AgentState respondent = Agent(
                respondentAgentId,
                1,
                "species.trigger.respondent",
                null);
            var definition = new InstitutionalScenarioDefinition
            {
                ScenarioId = "scenario.trigger",
                IncidentId = "incident.trigger",
                PrimaryCaseId = primaryCaseId,
                StartCycle = 0,
                EndCycle = 4,
                InitialSociety = new SocietyState
                {
                    MasterSeed = 2718,
                    CurrentTick = 0,
                    Regime = new InstitutionalRegimeState(),
                    Agents = new List<AgentState> { claimant, respondent },
                },
            };
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = claimantRoleId,
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.trigger.claimant",
                },
                DistinctFromRoleIds = new List<string> { respondentRoleId },
            });
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = respondentRoleId,
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = "species.trigger.respondent",
                },
                DistinctFromRoleIds = new List<string> { claimantRoleId },
            });
            definition.Cases.Add(new ScenarioCaseDefinition
            {
                CaseId = primaryCaseId,
                IssueId = issueId,
                ClaimantRoleId = claimantRoleId,
                RespondentRoleId = respondentRoleId,
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("fact.trigger.jurisdiction", "fixture"),
                }),
                OpenCycle = 0,
                InitialEvidenceCutoffCycle = 1,
                InitialRulingCycle = 1,
                AdjudicationEvidenceCutoffCycle = 4,
                AdjudicationCycle = 4,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "adjudication",
                InitialRulingId = "ruling:case.trigger:initial:1",
                AdjudicationRulingId = "ruling:case.trigger:adjudication:4",
                InitialScoreThreshold = 40,
                ProvisionalScoreThreshold = 20,
                AdjudicationScoreThreshold = 40,
            });
            definition.Cases.Add(new ScenarioCaseDefinition
            {
                CaseId = descendantCaseId,
                IssueId = issueId,
                ClaimantRoleId = claimantRoleId,
                RespondentRoleId = respondentRoleId,
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact("fact.trigger.jurisdiction", "fixture"),
                }),
                OpenCycle = 3,
                InitialEvidenceCutoffCycle = 3,
                InitialRulingCycle = 3,
                AdjudicationEvidenceCutoffCycle = 4,
                AdjudicationCycle = 4,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "adjudication",
                InitialRulingId = "ruling:case.trigger-descendant:initial:3",
                AdjudicationRulingId = "ruling:case.trigger-descendant:adjudication:4",
                InitialScoreThreshold = 40,
                ProvisionalScoreThreshold = 20,
                AdjudicationScoreThreshold = 40,
            });
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = opportunityId,
                Kind = ScenarioOpportunityKind.Aid,
                PurposeId = "purpose.trigger.aid",
                SourceCauseId = "cause.trigger.aid",
                AvailabilityStartCycle = 4,
                AvailabilityEndCycle = 4,
                UtilityBonus = 10,
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { respondentRoleId },
            });
            for (long cycle = 1; cycle <= 4; cycle++)
            {
                var schedule = new ScenarioCycleScheduleEntry
                {
                    ScheduleEntryId = $"schedule.trigger.{cycle:000}",
                    IncidentId = "incident.trigger",
                    Cycle = cycle,
                    Visibility = ScenarioVisibilityMode.NoBoundRoles,
                };
                if (cycle == 2)
                {
                    schedule.DisclosureRequested = true;
                }
                if (cycle == 4)
                {
                    schedule.AidAvailable = true;
                    schedule.ActiveOpportunityIds.Add(opportunityId);
                }
                definition.CycleSchedule.Add(schedule);
            }
            definition.EvidenceTemplates.Add(new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = "evidence.trigger.primary",
                SourceEventKind = SocietyEventKind.EvidenceDisclosed,
                SourceOpportunityId = null,
                RequiredPropositionId = propositionId,
                CaseId = primaryCaseId,
                IssueId = issueId,
                EvidenceClassId = "evidence-class.trigger.work",
                Effect = EvidenceEffect.SupportsFinding,
                Weight = 50,
                Visibility = EvidenceVisibility.OfficialRecord,
            });
            definition.EvidenceTemplates.Add(new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = "evidence.trigger.zeta",
                SourceEventKind = SocietyEventKind.EvidenceDisclosed,
                SourceOpportunityId = null,
                RequiredPropositionId = propositionId,
                CaseId = descendantCaseId,
                IssueId = issueId,
                EvidenceClassId = "evidence-class.trigger.work",
                Effect = EvidenceEffect.SupportsFinding,
                Weight = 50,
                Visibility = EvidenceVisibility.OfficialRecord,
            });
            definition.DescendantCases.Add(
                new ScenarioActionCausedDescendantCaseDefinition
                {
                    DescendantDefinitionId = "descendant.trigger",
                    CaseId = descendantCaseId,
                    ParentCaseId = primaryCaseId,
                    OpenCycle = 3,
                    TriggerCycle = 2,
                    TriggerRoleId = claimantRoleId,
                    TriggerActionKind = SocietyActionKind.Disclose,
                    TriggerOpportunityId = null,
                    TriggerPropositionId = propositionId,
                    OriginatingRulingId = "ruling:case.trigger:initial:1",
                    ConnectedRoleIds = new List<string>
                    {
                        claimantRoleId,
                        respondentRoleId,
                    },
                });
            return definition;
        }

        private static InstitutionalScenarioDefinition Definition(string key)
        {
            string claimantRoleId = $"role.{key}.claimant";
            string respondentRoleId = $"role.{key}.respondent";
            string claimantAgentId = $"agent.{key}.claimant";
            string respondentAgentId = $"agent.{key}.respondent";
            string caseId = $"case.{key}";
            string issueId = $"issue.{key}";
            string incidentId = $"incident.{key}";
            string opportunityId = $"opportunity.{key}.work";
            string statusId = $"status.{key}.after-ruling";

            var definition = new InstitutionalScenarioDefinition
            {
                ScenarioId = $"scenario.{key}",
                IncidentId = incidentId,
                PrimaryCaseId = caseId,
                StartCycle = 0,
                EndCycle = 2,
                InitialSociety = new SocietyState
                {
                    MasterSeed = 31415,
                    CurrentTick = 0,
                    Regime = new InstitutionalRegimeState(),
                    Agents = new List<AgentState>
                    {
                        Agent(
                            claimantAgentId,
                            0,
                            $"species.{key}.claimant",
                            statusId),
                        Agent(
                            respondentAgentId,
                            1,
                            $"species.{key}.respondent",
                            null),
                    },
                },
            };
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = claimantRoleId,
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = $"species.{key}.claimant",
                },
                DistinctFromRoleIds = new List<string> { respondentRoleId },
            });
            definition.ParticipantRoles.Add(new ScenarioParticipantRoleDefinition
            {
                RoleId = respondentRoleId,
                Query = new ScenarioParticipantQuery
                {
                    RequiredSpeciesId = $"species.{key}.respondent",
                },
                DistinctFromRoleIds = new List<string> { claimantRoleId },
            });
            definition.Opportunities.Add(new ScenarioOpportunityDefinition
            {
                OpportunityId = opportunityId,
                Kind = ScenarioOpportunityKind.Work,
                PurposeId = $"purpose.{key}.work",
                SourceCauseId = $"cause.{key}.work",
                AvailabilityStartCycle = 1,
                AvailabilityEndCycle = 1,
                UtilityBonus = 100,
                HearingCycle = -1,
                EligibleRoleIds = new List<string> { claimantRoleId },
            });
            definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
            {
                ScheduleEntryId = $"schedule.{key}.001",
                IncidentId = incidentId,
                Cycle = 1,
                WorkAvailable = true,
                Visibility = ScenarioVisibilityMode.NoBoundRoles,
                ActiveOpportunityIds = new List<string> { opportunityId },
            });
            definition.CycleSchedule.Add(new ScenarioCycleScheduleEntry
            {
                ScheduleEntryId = $"schedule.{key}.002",
                IncidentId = incidentId,
                Cycle = 2,
                Visibility = ScenarioVisibilityMode.NoBoundRoles,
            });
            definition.EvidenceTemplates.Add(new ScenarioEvidenceTemplateDefinition
            {
                EvidenceTemplateId = $"evidence.{key}.work",
                SourceEventKind = SocietyEventKind.WorkPerformed,
                SourceOpportunityId = opportunityId,
                CaseId = caseId,
                IssueId = issueId,
                EvidenceClassId = $"evidence-class.{key}.work",
                Effect = EvidenceEffect.SupportsFinding,
                Weight = 50,
                Visibility = EvidenceVisibility.OfficialRecord,
            });
            definition.Cases.Add(new ScenarioCaseDefinition
            {
                CaseId = caseId,
                IssueId = issueId,
                ClaimantRoleId = claimantRoleId,
                RespondentRoleId = respondentRoleId,
                Facts = new CaseFactSet(new[]
                {
                    new CaseFact($"fact.{key}.jurisdiction", "fixture"),
                }),
                OpenCycle = 0,
                InitialEvidenceCutoffCycle = 1,
                InitialRulingCycle = 1,
                AdjudicationEvidenceCutoffCycle = 2,
                AdjudicationCycle = 2,
                InitialPhaseId = "initial",
                AdjudicationPhaseId = "appeal",
                InitialRulingId = $"ruling:{caseId}:initial:1",
                AdjudicationRulingId = $"ruling:{caseId}:appeal:2",
                InitialScoreThreshold = 40,
                ProvisionalScoreThreshold = 20,
                ProvisionalRecognitionPermitted = false,
                AdjudicationScoreThreshold = 40,
            });
            definition.OfficialStatusEffectRequests.Add(
                new ScenarioOfficialStatusEffectRequest
                {
                    EffectRequestId = $"effect.{key}.after-ruling",
                    Cycle = 1,
                    CauseCaseId = caseId,
                    CauseRulingId = $"ruling:{caseId}:initial:1",
                    RequiredRulingDisposition = RulingDisposition.Denied,
                    TargetRoleId = claimantRoleId,
                    StatusId = statusId,
                    RequestedRecognisedState = true,
                });
            return definition;
        }

        private static AgentState Agent(
            string id,
            int ordinal,
            string speciesId,
            string unrecognisedStatusId)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = $"presentation.{id}",
                DisplayName = $"Participant {ordinal}",
                SpeciesId = speciesId,
                HouseholdId = $"household.{id}",
                EmployerId = $"organisation.{id}",
                InstitutionalTrust = 50,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = 50,
                    Candour = 50,
                    Solidarity = 50,
                    Duty = 50,
                    InstitutionalReliance = 50,
                },
                Standing = new InstitutionalStandingState
                {
                    CanWork = false,
                    CanSeekAid = false,
                    CanAppeal = false,
                    CanGiveEvidence = false,
                },
            };
            foreach (NeedKind need in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = need, Pressure = 20 });
            if (!string.IsNullOrEmpty(unrecognisedStatusId))
                agent.Standing.SetRecognised(unrecognisedStatusId, false);
            return agent;
        }

        private static InstitutionalPolicyConfiguration Policy(string key)
        {
            return new InstitutionalPolicyConfiguration
            {
                PolicyConfigurationId = $"configuration.{key}",
                PolicyVersion = $"configuration.{key}.v1",
                WorkReward = 50,
                AidEffectiveness = 50,
                DisclosureProtection = 50,
                RetaliationRisk = 50,
                AppealAccessibility = 50,
                DecisionVariationAmplitude = 0,
                InitialRecognitionThreshold = 40,
                ProvisionalRecognitionThreshold = 20,
                AppealRecognitionThreshold = 40,
                LaterRecognitionThreshold = 40,
                CitedHoldingWeight = 0,
                PermitProvisionalRecognition = false,
                ProvisionalReliefAmount = 0,
                EstablishAppellateHolding = false,
                AutoCiteMatchingHoldings = false,
                HoldingReach = PrecedentReach.Individual,
                HoldingIsRetrospective = false,
                EvidenceClassWeights = new List<EvidenceClassWeight>
                {
                    new EvidenceClassWeight
                    {
                        EvidenceClassId = $"evidence-class.{key}.work",
                        WeightPercent = 100,
                    },
                },
            };
        }
    }
}
