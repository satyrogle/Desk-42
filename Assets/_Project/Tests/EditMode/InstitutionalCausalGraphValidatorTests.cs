using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalCausalGraphValidatorTests
    {
        [Test]
        public void Validate_AcceptsScenarioNeutralCompletedCausalGraph()
        {
            Fixture fixture = BuildValidFixture();

            Assert.DoesNotThrow(() => InstitutionalCausalGraphValidator.Validate(
                fixture.Run,
                fixture.Registry));
        }

        [Test]
        public void ValidateReport_AcceptsPublicProjectionWithoutAuthorityTypes()
        {
            Fixture fixture = BuildValidFixture();

            Assert.DoesNotThrow(() => InstitutionalCausalGraphValidator.Validate(
                fixture.Run.Report));
        }

        [Test]
        public void Validate_RejectsDuplicateAndNullReportRows()
        {
            Fixture duplicate = BuildValidFixture();
            duplicate.Run.Report.ObservedAgentActions.Add(
                duplicate.Run.Report.ObservedAgentActions[0]);
            InvalidOperationException duplicateError = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(duplicate.Run));
            StringAssert.Contains("Duplicate observed action id", duplicateError.Message);

            Fixture nullRow = BuildValidFixture();
            nullRow.Run.Report.Rulings.Add(null);
            InvalidOperationException nullError = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(nullRow.Run));
            StringAssert.Contains("contains a null row", nullError.Message);
        }

        [Test]
        public void Validate_RejectsEvidenceWithoutBidirectionalActionSource()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.Report.EvidenceArtifacts[0].Provenance.SourceSocietyEventId =
                "action:missing";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("does not point back", error.Message);
        }

        [Test]
        public void Validate_RejectsRulingAndFindingEvidenceDisagreement()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.Report.Rulings[0].EvidenceArtifactIds.Clear();

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("different evidence envelopes", error.Message);
        }

        [Test]
        public void Validate_RejectsDescendantWithUnresolvedParentCause()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.Report.DescendantCases[0].ParentCauseId = "cause:missing";
            ReplaceTimelineCause(
                fixture.Run.Report,
                InstitutionalTimelineKind.DescendantCaseOpened,
                "cause:missing");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("unresolved or future parent cause", error.Message);
        }

        [Test]
        public void Validate_RejectsAppealThatPredatesChallengedRuling()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.Report.Appeals[0].FiledCycle = 2;

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("invalid filing action", error.Message);
        }

        [Test]
        public void Validate_RejectsHoldingWithoutSupportingEvidence()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.Report.Holdings[0].SupportingEvidenceArtifactIds.Clear();

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("has no supporting evidence", error.Message);
        }

        [Test]
        public void Validate_RejectsRelianceWithoutExactEnablingMutation()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.Report.RelianceObservations[0].EnablingMutationId =
                "mutation:missing";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("invalid enabling mutation", error.Message);
        }

        [Test]
        public void Validate_RejectsMaterialConsequenceWithoutPublicCause()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.Report.MaterialConsequences[0].CauseId = "cause:missing";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("unresolved or future cause", error.Message);
        }

        [Test]
        public void Validate_RejectsUnconservedExclusiveEntitlementTransfer()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.Report.MaterialConsequences[2].ResourceDelta = -9;

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(
                    fixture.Run,
                    fixture.Registry));

            StringAssert.Contains("transfer is not conserved", error.Message);
        }

        [Test]
        public void Validate_AcceptsConnectedOutcomeUsingEntitlementResourceId()
        {
            Fixture fixture = BuildValidFixture();
            Holding holding = fixture.Run.Report.Holdings.Single();
            ExclusiveEntitlementObservation entitlement =
                fixture.Run.Report.ExclusiveEntitlements.Single();
            fixture.Run.Report.ConnectedOutcomes.Add(new ConnectedOutcomePair
            {
                PairId = "connected:entitlement-resource",
                CauseRuleId = holding.RuleId,
                ConnectionId = entitlement.ResourceId,
                WinnerAgentId = "agent:b",
                WinnerDisplayName = "Agent B",
                WinnerResourceDelta = 10,
                LoserAgentId = "agent:a",
                LoserDisplayName = "Agent A",
                LoserResourceDelta = -10,
            });

            Assert.DoesNotThrow(() => InstitutionalCausalGraphValidator.Validate(
                fixture.Run,
                fixture.Registry));
        }

        [Test]
        public void Validate_RejectsFinalExclusiveEntitlementHolderMismatch()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.FinalSocietyState.GetAgent("agent:b")
                .Standing.SetRecognised("status:holder", false);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(
                    fixture.Run,
                    fixture.Registry));

            StringAssert.Contains("final holder invariant", error.Message);
        }

        [Test]
        public void Validate_RejectsTimelineRegression()
        {
            Fixture fixture = BuildValidFixture();
            InstitutionalTimelineEntry cycleOne = fixture.Run.Report.Timeline[0];
            fixture.Run.Report.Timeline.RemoveAt(0);
            fixture.Run.Report.Timeline.Insert(1, cycleOne);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("Timeline regressed", error.Message);
        }

        [Test]
        public void Validate_RejectsAuthorityOnlyLivedEventIdentifierInPublicReport()
        {
            Fixture fixture = BuildValidFixture();
            const string livedEventId = "lived:hidden-state";
            fixture.Run.AuthoritativeEvents.Add(new LivedEvent
            {
                LivedEventId = livedEventId,
                Cycle = 0,
                EventKindId = "incident:hidden-state",
                SubjectAgentId = "agent:a",
                CauseEntityId = "entity:opaque",
                AffectedNeed = NeedKind.Health,
                NeedPressureDelta = 1,
            });
            fixture.Run.Report.EvidenceArtifacts[0].Provenance.SourceRecordId = livedEventId;

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("leaked into the public report", error.Message);
        }

        [Test]
        public void Validate_RejectsBrokenAuthorityEvidenceLink()
        {
            Fixture fixture = BuildValidFixture();
            fixture.Run.AuthoritativeEvidenceLinks.Add(new AuthoritativeEvidenceLink
            {
                LivedEventId = "lived:missing",
                EvidenceArtifactId = "evidence:source",
                ObservationKindId = "observation:test",
            });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => InstitutionalCausalGraphValidator.Validate(fixture.Run));

            StringAssert.Contains("unknown lived event", error.Message);
        }

        [Test]
        public void Validate_AllowsOnlyOpeningTriggerEvidenceToPredateDescendant()
        {
            Fixture triggerFixture = BuildValidFixture();
            AddPreOpeningDescendantEvidence(triggerFixture, "action:reliance");

            Assert.DoesNotThrow(() =>
                InstitutionalCausalGraphValidator.Validate(triggerFixture.Run));

            Fixture foreignFixture = BuildValidFixture();
            AddPreOpeningDescendantEvidence(foreignFixture, "action:disclosure");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalCausalGraphValidator.Validate(foreignFixture.Run));
            StringAssert.Contains("predates its case", error.Message);
        }

        private static void AddPreOpeningDescendantEvidence(
            Fixture fixture,
            string sourceActionId)
        {
            ObservedAgentAction source = fixture.Run.Report.ObservedAgentActions.Single(
                value => value.ActionEventId == sourceActionId);
            AgentActionTrace trace = fixture.Run.AssessorActionTraces.Single(value =>
                value.ResultEventIds.Contains(sourceActionId));
            string artifactId = "evidence:pre-open:" + sourceActionId;
            var artifact = new EvidenceArtifact
            {
                ArtifactId = artifactId,
                CaseId = "case:descendant",
                EnteredCycle = source.Cycle,
                Kind = EvidenceArtifactKind.ActionRecord,
                EvidenceClassId = "evidence-class:trigger",
                IssueId = "issue:recognition",
                PropositionId = "proposition:trigger",
                Effect = EvidenceEffect.SupportsFinding,
                BaseWeight = 10,
                Reliability = 100,
                OfficiallySubmitted = true,
                KnownByAgentIds = new List<string> { source.ActorId },
                Provenance = new EvidenceProvenance
                {
                    ProvenanceId = "provenance:pre-open:" + sourceActionId,
                    CreatedCycle = source.Cycle,
                    SourceAgentId = source.ActorId,
                    SourceDecisionId = trace.DecisionId,
                    SourceSocietyEventId = source.ActionEventId,
                    SourceRecordId = "record:pre-open:" + sourceActionId,
                    Visibility = EvidenceVisibility.OfficialRecord,
                    CreatedByAgentAction = true,
                    ChainOfCustodyIds = new List<string>
                    {
                        trace.DecisionId,
                        source.ActionEventId,
                    },
                },
            };
            fixture.Run.Report.EvidenceArtifacts.Add(artifact);
            source.ResultEvidenceArtifactIds.Add(artifactId);
            InstitutionalTimeline.Add(
                fixture.Run.Report,
                source.Cycle,
                InstitutionalTimelineKind.EvidenceEntered,
                source.ActionEventId,
                source.ActorId,
                artifactId);
            fixture.Run.Report.Timeline.Sort((left, right) =>
                left.Cycle.CompareTo(right.Cycle));
        }

        private static Fixture BuildValidFixture()
        {
            SocietyState society = CreateSociety();
            var report = new InstitutionalConsequenceReport
            {
                MasterSeed = society.MasterSeed,
                PolicyConfigurationId = "policy:configuration",
                PrimaryCaseId = "case:primary",
                FinalCycle = society.CurrentTick,
            };
            var run = new InstitutionalConsequenceRun
            {
                Report = report,
                FinalSocietyState = society,
            };

            ObservedAgentAction disclosure = AddAction(
                run,
                "action:disclosure",
                "decision:disclosure",
                "agent:a",
                1,
                SocietyActionKind.Disclose,
                SocietyEventKind.EvidenceDisclosed,
                ObservedActivityKind.EvidenceSubmitted);
            disclosure.ResultEvidenceArtifactIds.Add("evidence:source");

            var evidence = new EvidenceArtifact
            {
                ArtifactId = "evidence:source",
                CaseId = report.PrimaryCaseId,
                EnteredCycle = 1,
                Kind = EvidenceArtifactKind.ActionRecord,
                EvidenceClassId = "evidence-class:statement",
                IssueId = "issue:recognition",
                PropositionId = "proposition:recognition",
                Effect = EvidenceEffect.SupportsFinding,
                BaseWeight = 40,
                Reliability = 100,
                OfficiallySubmitted = true,
                KnownByAgentIds = new List<string> { "agent:a" },
                Provenance = new EvidenceProvenance
                {
                    ProvenanceId = "provenance:source",
                    CreatedCycle = 1,
                    SourceAgentId = "agent:a",
                    SourceDecisionId = "decision:disclosure",
                    SourceSocietyEventId = disclosure.ActionEventId,
                    SourceRecordId = "record:source",
                    Visibility = EvidenceVisibility.OfficialRecord,
                    CreatedByAgentAction = true,
                    ChainOfCustodyIds = new List<string>
                    {
                        "decision:disclosure",
                        disclosure.ActionEventId,
                    },
                },
            };
            report.EvidenceArtifacts.Add(evidence);
            InstitutionalTimeline.Add(
                report,
                1,
                InstitutionalTimelineKind.EvidenceEntered,
                disclosure.ActionEventId,
                "agent:a",
                evidence.ArtifactId);

            Ruling initial = AddRuling(
                report,
                "ruling:initial",
                "finding:initial",
                report.PrimaryCaseId,
                "issue:recognition",
                2,
                RulingDisposition.Denied,
                FindingDisposition.NotEstablished,
                new List<string> { evidence.ArtifactId });

            ObservedAgentAction appealAction = AddAction(
                run,
                "action:appeal",
                "decision:appeal",
                "agent:b",
                3,
                SocietyActionKind.Appeal,
                SocietyEventKind.AppealFiled,
                ObservedActivityKind.AppealFiled);
            var appeal = new Appeal
            {
                AppealId = "appeal:primary",
                CaseId = report.PrimaryCaseId,
                FiledCycle = 3,
                HearingCycle = 4,
                AppellantAgentId = "agent:b",
                FilingActionEventId = appealAction.ActionEventId,
                ChallengedRulingId = initial.RulingId,
                Disposition = AppealDisposition.Reversed,
                ResultingRulingId = "ruling:appeal-result",
                GroundsEvidenceArtifactIds = new List<string> { evidence.ArtifactId },
            };
            report.Appeals.Add(appeal);
            InstitutionalTimeline.Add(
                report,
                3,
                InstitutionalTimelineKind.AppealFiled,
                appealAction.ActionEventId,
                "agent:b",
                appeal.AppealId);

            Ruling appealResult = AddRuling(
                report,
                appeal.ResultingRulingId,
                "finding:appeal-result",
                report.PrimaryCaseId,
                "issue:recognition",
                4,
                RulingDisposition.ReversedAndRecognised,
                FindingDisposition.Established,
                new List<string> { evidence.ArtifactId });
            InstitutionalTimeline.Add(
                report,
                4,
                InstitutionalTimelineKind.AppealHeard,
                appeal.AppealId,
                appeal.CaseId,
                appealResult.RulingId);

            var enablingMutation = new OfficialStatusMutation
            {
                MutationId = "mutation:permit",
                Cycle = 4,
                CauseId = appealResult.RulingId,
                AffectedAgentId = "agent:a",
                StatusId = "status:permit",
                BeforeRecognised = false,
                AfterRecognised = true,
            };
            report.OfficialStatusMutations.Add(enablingMutation);
            appealResult.OfficialStatusMutationIds.Add(enablingMutation.MutationId);
            InstitutionalTimeline.Add(
                report,
                4,
                InstitutionalTimelineKind.StatusMutated,
                appealResult.RulingId,
                "agent:a",
                "status:permit");

            CaseFactSet scopeFacts = Facts();
            var holding = new Holding
            {
                HoldingId = "holding:recognition",
                EstablishedCycle = 4,
                SourceAppealId = appeal.AppealId,
                SourceRulingId = appealResult.RulingId,
                RuleId = "rule:recognition",
                IssueId = "issue:recognition",
                SupportingEvidenceArtifactIds = new List<string> { evidence.ArtifactId },
                Scope = new PrecedentScope
                {
                    ScopeId = "scope:recognition",
                    Reach = PrecedentReach.Jurisdiction,
                    RequiredFacts = scopeFacts.Copy(),
                },
                AppliedCaseIds = new List<string> { "case:descendant" },
            };
            report.Holdings.Add(holding);
            InstitutionalTimeline.Add(
                report,
                4,
                InstitutionalTimelineKind.HoldingEstablished,
                appealResult.RulingId,
                holding.HoldingId,
                holding.RuleId);

            ObservedAgentAction relianceAction = AddAction(
                run,
                "action:reliance",
                "decision:reliance",
                "agent:a",
                5,
                SocietyActionKind.Work,
                SocietyEventKind.WorkPerformed,
                ObservedActivityKind.WorkPerformed);
            var relianceObservation = new RelianceObservation
            {
                ObservationId = "reliance-observation:choice",
                Cycle = 5,
                AgentId = "agent:a",
                EnablingRulingId = appealResult.RulingId,
                EnablingMutationId = enablingMutation.MutationId,
                SourceActionEventId = relianceAction.ActionEventId,
                RecordedChoiceId = "choice:commit",
                AbandonedAlternativeId = "alternative:safe",
                ResourceId = "resource:credits",
                RecordedResourceDelta = -3,
            };
            report.RelianceObservations.Add(relianceObservation);
            report.MaterialConsequences.Add(new MaterialConsequence
            {
                ConsequenceId = "material:reliance",
                Cycle = 5,
                CauseId = relianceAction.ActionEventId,
                AgentId = "agent:a",
                Kind = MaterialConsequenceKind.RelianceSpent,
                KindId = "material-kind:reliance",
                ResourceId = "resource:credits",
                ResourceDelta = -3,
            });
            run.RelianceLedger.Add(new RelianceEvent
            {
                RelianceEventId = "reliance:choice",
                Cycle = 5,
                AgentId = "agent:a",
                BeneficiaryAgentId = "agent:a",
                ReliedOnRulingId = appealResult.RulingId,
                ReliedOnMutationId = enablingMutation.MutationId,
                SourceActionEventId = relianceAction.ActionEventId,
                ChoiceId = "choice:actual",
                AbandonedAlternativeId = "alternative:safe",
                ResourceSpent = 3,
                AlternativeAvailableBefore = true,
                AlternativeAvailableAfter = false,
            });
            run.EconomicAccounts.Add(new EconomicAccountState
            {
                AgentId = "agent:a",
                AvailableCredits = 7,
            });
            run.EconomicAccounts.Add(new EconomicAccountState
            {
                AgentId = "agent:b",
                AvailableCredits = 10,
            });
            run.AlternativeOptions.Add(new AlternativeOptionState
            {
                OptionId = "alternative:safe",
                AgentId = "agent:a",
                Available = false,
                ChangedByActionEventId = relianceAction.ActionEventId,
            });
            InstitutionalTimeline.Add(
                report,
                5,
                InstitutionalTimelineKind.RelianceCreated,
                relianceAction.ActionEventId,
                "agent:a",
                relianceObservation.ObservationId);

            var descendant = new DescendantCase
            {
                CaseId = "case:descendant",
                ParentCaseId = report.PrimaryCaseId,
                OpenedCycle = 6,
                Kind = DescendantCaseKind.RelatedClaim,
                Status = DescendantCaseStatus.Recognised,
                ParentCauseId = relianceAction.ActionEventId,
                OriginatingEventId = relianceAction.ActionEventId,
                OriginatingRulingId = appealResult.RulingId,
                CausalAgentActionId = relianceAction.ActionEventId,
                ClaimantAgentId = "agent:a",
                RespondentId = "institution:respondent",
                OfficialIssueId = "issue:recognition",
                Facts = Facts(),
                ConnectedAgentIds = new List<string> { "agent:a", "agent:b" },
                SourceActionEventIds = new List<string> { relianceAction.ActionEventId },
                CitedHoldingIds = new List<string> { holding.HoldingId },
            };
            report.DescendantCases.Add(descendant);
            relianceAction.ResultDescendantCaseIds.Add(descendant.CaseId);
            InstitutionalTimeline.Add(
                report,
                6,
                InstitutionalTimelineKind.DescendantCaseOpened,
                relianceAction.ActionEventId,
                descendant.CaseId,
                descendant.Kind.ToString());

            Ruling descendantRuling = AddRuling(
                report,
                "ruling:descendant",
                "finding:descendant",
                descendant.CaseId,
                descendant.OfficialIssueId,
                7,
                RulingDisposition.Recognised,
                FindingDisposition.Established,
                new List<string>());
            descendantRuling.CitedHoldingIds.Add(holding.HoldingId);
            descendantRuling.CitedScopeIds.Add(holding.Scope.ScopeId);
            descendantRuling.AppliedPolicyIds.Add(holding.RuleId);
            InstitutionalTimeline.Add(
                report,
                7,
                InstitutionalTimelineKind.PrecedentApplied,
                holding.HoldingId,
                descendant.CaseId,
                descendantRuling.RulingId);

            AddEntitlementTransfer(
                report,
                descendantRuling,
                "agent:a",
                "agent:b");

            var registry = new ExclusiveEntitlementRegistry();
            registry.Add(new ExclusiveEntitlementState(
                "entitlement:unit",
                "resource:unit",
                "status:holder",
                10,
                "agent:b",
                descendantRuling.RulingId));

            return new Fixture(run, registry);
        }

        private static Ruling AddRuling(
            InstitutionalConsequenceReport report,
            string rulingId,
            string findingId,
            string caseId,
            string issueId,
            long cycle,
            RulingDisposition rulingDisposition,
            FindingDisposition findingDisposition,
            List<string> evidenceIds)
        {
            var finding = new OfficialFinding
            {
                FindingId = findingId,
                CaseId = caseId,
                Cycle = cycle,
                IssueId = issueId,
                Disposition = findingDisposition,
                WeightedEvidenceScore = evidenceIds.Count == 0 ? 10 : 40,
                RequiredScore = 30,
                EvidenceArtifactIds = new List<string>(evidenceIds),
            };
            var ruling = new Ruling
            {
                RulingId = rulingId,
                CaseId = caseId,
                Cycle = cycle,
                PolicyConfigurationId = report.PolicyConfigurationId,
                PolicyVersion = "policy-version:v1",
                Disposition = rulingDisposition,
                FindingId = findingId,
                ConfidenceMinimum = finding.WeightedEvidenceScore,
                ConfidenceMaximum = finding.WeightedEvidenceScore,
                EvidenceArtifactIds = new List<string>(evidenceIds),
                AppliedPolicyIds = new List<string> { "policy-version:v1" },
            };
            report.OfficialFindings.Add(finding);
            report.Rulings.Add(ruling);
            InstitutionalTimeline.Add(
                report,
                cycle,
                InstitutionalTimelineKind.RulingIssued,
                rulingId,
                caseId,
                rulingDisposition.ToString());
            return ruling;
        }

        private static ObservedAgentAction AddAction(
            InstitutionalConsequenceRun run,
            string actionId,
            string decisionId,
            string actorId,
            long cycle,
            SocietyActionKind actionKind,
            SocietyEventKind eventKind,
            ObservedActivityKind activity)
        {
            var observed = new ObservedAgentAction
            {
                Cycle = cycle,
                ActionEventId = actionId,
                ActorId = actorId,
                Activity = activity,
            };
            run.Report.ObservedAgentActions.Add(observed);
            run.FinalSocietyState.EventLedger.Add(new SocietyEvent
            {
                EventId = actionId,
                CauseDecisionId = decisionId,
                IncidentId = "incident:test",
                Tick = cycle,
                Kind = eventKind,
                ActorId = actorId,
                Visibility = EvidenceVisibility.OfficialRecord,
            });
            run.AssessorActionTraces.Add(new AgentActionTrace
            {
                Cycle = cycle,
                DecisionId = decisionId,
                CandidateId = $"candidate:{decisionId}",
                ActorId = actorId,
                Action = actionKind,
                UtilityScore = 10,
                SelectedCandidateRank = 1,
                ResultEventIds = new List<string> { actionId },
                PerceptionSnapshot = AgentPerception.Capture(
                    run.FinalSocietyState.GetAgent(actorId)),
                RegimeSnapshot = new InstitutionalRegimeState(),
                InputSnapshot = new SimulationInput(),
            });
            return observed;
        }

        private static void AddEntitlementTransfer(
            InstitutionalConsequenceReport report,
            Ruling ruling,
            string previousHolderId,
            string newHolderId)
        {
            var lossMutation = new OfficialStatusMutation
            {
                MutationId = "mutation:holder-loss",
                Cycle = ruling.Cycle,
                CauseId = ruling.RulingId,
                AffectedAgentId = previousHolderId,
                StatusId = "status:holder",
                BeforeRecognised = true,
                AfterRecognised = false,
            };
            var gainMutation = new OfficialStatusMutation
            {
                MutationId = "mutation:holder-gain",
                Cycle = ruling.Cycle,
                CauseId = ruling.RulingId,
                AffectedAgentId = newHolderId,
                StatusId = "status:holder",
                BeforeRecognised = false,
                AfterRecognised = true,
            };
            report.OfficialStatusMutations.Add(lossMutation);
            report.OfficialStatusMutations.Add(gainMutation);
            ruling.OfficialStatusMutationIds.Add(lossMutation.MutationId);
            ruling.OfficialStatusMutationIds.Add(gainMutation.MutationId);
            InstitutionalTimeline.Add(
                report,
                ruling.Cycle,
                InstitutionalTimelineKind.StatusMutated,
                ruling.RulingId,
                previousHolderId,
                "status:holder");
            InstitutionalTimeline.Add(
                report,
                ruling.Cycle,
                InstitutionalTimelineKind.StatusMutated,
                ruling.RulingId,
                newHolderId,
                "status:holder");

            report.MaterialConsequences.Add(new MaterialConsequence
            {
                ConsequenceId = "material:holder-gain",
                Cycle = ruling.Cycle,
                CauseId = ruling.RulingId,
                AgentId = newHolderId,
                Kind = MaterialConsequenceKind.ReliefPaid,
                KindId = "material-kind:entitlement-gain",
                ResourceId = "resource:unit",
                ResourceDelta = 10,
            });
            report.MaterialConsequences.Add(new MaterialConsequence
            {
                ConsequenceId = "material:holder-loss",
                Cycle = ruling.Cycle,
                CauseId = ruling.RulingId,
                AgentId = previousHolderId,
                Kind = MaterialConsequenceKind.WagesLost,
                KindId = "material-kind:entitlement-loss",
                ResourceId = "resource:unit",
                ResourceDelta = -10,
            });
            report.ExclusiveEntitlements.Add(new ExclusiveEntitlementObservation
            {
                EntitlementId = "entitlement:unit",
                ResourceId = "resource:unit",
                HolderStatusId = "status:holder",
                ConservedAmount = 10,
                CurrentHolderAgentId = newHolderId,
                LastMutationCauseId = ruling.RulingId,
            });
        }

        private static SocietyState CreateSociety()
        {
            var society = new SocietyState
            {
                MasterSeed = 42,
                CurrentTick = 7,
            };
            AgentState first = CreateAgent("agent:a", 0);
            first.Standing.SetRecognised("status:permit", true);
            first.Standing.SetRecognised("status:holder", false);
            AgentState second = CreateAgent("agent:b", 1);
            second.Standing.SetRecognised("status:holder", true);
            society.Agents.Add(first);
            society.Agents.Add(second);
            society.Agents.Add(CreateAgent("agent:c", 2));
            return society;
        }

        private static AgentState CreateAgent(string id, int ordinal)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                InstitutionalTrust = 50,
            };
            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = kind, Pressure = 20 });
            return agent;
        }

        private static CaseFactSet Facts()
        {
            return new CaseFactSet(new[]
            {
                new CaseFact("medium", "vapour"),
            });
        }

        private static void ReplaceTimelineCause(
            InstitutionalConsequenceReport report,
            InstitutionalTimelineKind kind,
            string causeId)
        {
            for (int i = 0; i < report.Timeline.Count; i++)
            {
                if (report.Timeline[i].Kind == kind)
                {
                    report.Timeline[i].CauseId = causeId;
                    return;
                }
            }
        }

        private sealed class Fixture
        {
            internal Fixture(
                InstitutionalConsequenceRun run,
                ExclusiveEntitlementRegistry registry)
            {
                Run = run;
                Registry = registry;
            }

            internal InstitutionalConsequenceRun Run { get; }
            internal ExclusiveEntitlementRegistry Registry { get; }
        }
    }
}
