using System.Collections.Generic;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalAppealPrecedentTests
    {
        [Test]
        public void FileAppeal_RequiresObservedAutonomousAction_AndFreezesGroundsAtFiling()
        {
            FilingFixture fixture = CreateFilingFixture();

            InstitutionalServiceResult<Appeal> result =
                InstitutionalAppealPrecedentService.FileAppeal(
                    fixture.Run,
                    fixture.Event,
                    fixture.Opportunities);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, result.Outcome);
            CollectionAssert.AreEqual(
                new[] { "evidence.before" },
                result.Value.GroundsEvidenceArtifactIds);
            Assert.AreEqual("agent.appellant", result.Value.AppellantAgentId);
            Assert.AreEqual(1, fixture.Run.Report.Appeals.Count);
            Assert.AreEqual(1, CountTimeline(
                fixture.Run.Report,
                InstitutionalTimelineKind.AppealFiled));

            fixture.Run.Report.EvidenceArtifacts.Add(new EvidenceArtifact
            {
                ArtifactId = "evidence.entered-after-filing-call",
                CaseId = "case.alpha",
                EnteredCycle = 4,
            });
            CollectionAssert.AreEqual(
                new[] { "evidence.before" },
                result.Value.GroundsEvidenceArtifactIds,
                "The filed grounds are an as-of snapshot, not a live case query.");
        }

        [Test]
        public void FileAppeal_RejectsScriptedEventAndInvalidChronologyWithoutMutation()
        {
            FilingFixture scripted = CreateFilingFixture();
            scripted.Run.AssessorActionTraces.Clear();

            InstitutionalServiceResult<Appeal> scriptedResult =
                InstitutionalAppealPrecedentService.FileAppeal(
                    scripted.Run,
                    scripted.Event,
                    scripted.Opportunities);

            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, scriptedResult.Outcome);
            Assert.AreEqual("appeal.filing-not-autonomous", scriptedResult.ReasonId);
            Assert.IsEmpty(scripted.Run.Report.Appeals);
            Assert.IsEmpty(scripted.Run.Report.Timeline);

            FilingFixture outOfOrder = CreateFilingFixture();
            outOfOrder.Run.Report.Rulings[0].Cycle = outOfOrder.Event.Tick;
            InstitutionalServiceResult<Appeal> chronologyResult =
                InstitutionalAppealPrecedentService.FileAppeal(
                    outOfOrder.Run,
                    outOfOrder.Event,
                    outOfOrder.Opportunities);

            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, chronologyResult.Outcome);
            Assert.AreEqual("appeal.invalid-filing-chronology", chronologyResult.ReasonId);
            Assert.IsEmpty(outOfOrder.Run.Report.Appeals);
        }

        [Test]
        public void FileAppeal_IsIdempotent_AndDoesNotAssumeFirstPartyIsAppellant()
        {
            FilingFixture fixture = CreateFilingFixture();
            Assert.AreEqual("agent.observer", fixture.Opportunities[0].PartyAgentIds[0]);
            Assert.AreEqual("agent.appellant", fixture.Opportunities[0].PartyAgentIds[1]);

            InstitutionalServiceResult<Appeal> first =
                InstitutionalAppealPrecedentService.FileAppeal(
                    fixture.Run,
                    fixture.Event,
                    fixture.Opportunities);
            InstitutionalServiceResult<Appeal> second =
                InstitutionalAppealPrecedentService.FileAppeal(
                    fixture.Run,
                    fixture.Event,
                    fixture.Opportunities);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, first.Outcome);
            Assert.AreEqual(InstitutionalServiceOutcome.NoChange, second.Outcome);
            Assert.AreSame(first.Value, second.Value);
            Assert.AreEqual(1, fixture.Run.Report.Appeals.Count);
            Assert.AreEqual(1, CountTimeline(
                fixture.Run.Report,
                InstitutionalTimelineKind.AppealFiled));
        }

        [Test]
        public void ResolveAppeal_UsesSuppliedAppellateRuling_AndIsIdempotent()
        {
            ResolvedFixture fixture = CreateResolvedFixture(
                RulingDisposition.ReversedAndRecognised,
                resolve: false);

            InstitutionalServiceResult<Appeal> first =
                InstitutionalAppealPrecedentService.ResolveAppeal(
                    fixture.Filing.Run.Report,
                    fixture.Appeal.AppealId,
                    fixture.ResultingRuling);
            InstitutionalServiceResult<Appeal> second =
                InstitutionalAppealPrecedentService.ResolveAppeal(
                    fixture.Filing.Run.Report,
                    fixture.Appeal.AppealId,
                    fixture.ResultingRuling);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, first.Outcome);
            Assert.AreEqual(AppealDisposition.Reversed, first.Value.Disposition);
            Assert.AreEqual(fixture.ResultingRuling.RulingId, first.Value.ResultingRulingId);
            Assert.AreEqual(InstitutionalServiceOutcome.NoChange, second.Outcome);
            Assert.AreEqual(1, CountTimeline(
                fixture.Filing.Run.Report,
                InstitutionalTimelineKind.AppealHeard));
        }

        [Test]
        public void ResolveAppeal_RejectsMissingPrematureAndNonAppellateRulings()
        {
            ResolvedFixture missing = CreateResolvedFixture(
                RulingDisposition.ReversedAndRecognised,
                resolve: false);
            missing.Filing.Run.Report.Rulings.Remove(missing.ResultingRuling);
            InstitutionalServiceResult<Appeal> missingResult =
                InstitutionalAppealPrecedentService.ResolveAppeal(
                    missing.Filing.Run.Report,
                    missing.Appeal.AppealId,
                    missing.ResultingRuling);
            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, missingResult.Outcome);
            Assert.AreEqual("appeal.resulting-ruling-not-found", missingResult.ReasonId);

            ResolvedFixture premature = CreateResolvedFixture(
                RulingDisposition.ReversedAndRecognised,
                resolve: false);
            premature.ResultingRuling.Cycle = 5;
            InstitutionalServiceResult<Appeal> prematureResult =
                InstitutionalAppealPrecedentService.ResolveAppeal(
                    premature.Filing.Run.Report,
                    premature.Appeal.AppealId,
                    premature.ResultingRuling);
            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, prematureResult.Outcome);
            Assert.AreEqual("appeal.invalid-resolution-chronology", prematureResult.ReasonId);

            ResolvedFixture nonAppellate = CreateResolvedFixture(
                RulingDisposition.Recognised,
                resolve: false);
            InstitutionalServiceResult<Appeal> nonAppellateResult =
                InstitutionalAppealPrecedentService.ResolveAppeal(
                    nonAppellate.Filing.Run.Report,
                    nonAppellate.Appeal.AppealId,
                    nonAppellate.ResultingRuling);
            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, nonAppellateResult.Outcome);
            Assert.AreEqual(
                "appeal.non-appellate-ruling-disposition",
                nonAppellateResult.ReasonId);
        }

        [Test]
        public void EstablishHolding_RequiresReversedRecognition_AndDetachesFactScope()
        {
            ResolvedFixture reversed = CreateResolvedFixture(
                RulingDisposition.ReversedAndRecognised,
                resolve: true);
            PrecedentScope proposed = Scope(
                "scope.output-control",
                new CaseFact("permit-class", "licensed"),
                new CaseFact("output-state", "undissipated"));

            InstitutionalServiceResult<Holding> established =
                InstitutionalAppealPrecedentService.EstablishHolding(
                    reversed.Filing.Run.Report,
                    reversed.Appeal.AppealId,
                    "holding.output-control",
                    "rule.control-until-dissipation",
                    "issue.output-control",
                    proposed);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, established.Outcome);
            Assert.AreNotSame(proposed, established.Value.Scope);
            Assert.AreNotSame(proposed.RequiredFacts, established.Value.Scope.RequiredFacts);
            proposed.ScopeId = "scope.mutated";
            proposed.RequiredFacts.Facts[0].Value = "mutated";
            Assert.AreEqual("scope.output-control", established.Value.Scope.ScopeId);
            Assert.IsTrue(established.Value.Scope.RequiredFacts.Contains(
                "permit-class",
                "licensed"));

            InstitutionalServiceResult<Holding> replay =
                InstitutionalAppealPrecedentService.EstablishHolding(
                    reversed.Filing.Run.Report,
                    reversed.Appeal.AppealId,
                    "holding.output-control",
                    "rule.control-until-dissipation",
                    "issue.output-control",
                    established.Value.Scope.CopyForTest());
            Assert.AreEqual(InstitutionalServiceOutcome.NoChange, replay.Outcome);

            ResolvedFixture affirmed = CreateResolvedFixture(
                RulingDisposition.Affirmed,
                resolve: true);
            InstitutionalServiceResult<Holding> affirmedResult =
                InstitutionalAppealPrecedentService.EstablishHolding(
                    affirmed.Filing.Run.Report,
                    affirmed.Appeal.AppealId,
                    "holding.invalid",
                    "rule.invalid",
                    "issue.output-control",
                    Scope("scope.invalid", new CaseFact("class", "one")));
            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, affirmedResult.Outcome);
            Assert.AreEqual("holding.appeal-not-reversed", affirmedResult.ReasonId);
        }

        [Test]
        public void EstablishHolding_StrictEvidenceSubset_IsIdempotentAndMatchable()
        {
            ResolvedFixture fixture = CreateResolvedFixture(
                RulingDisposition.ReversedAndRecognised,
                resolve: true);
            PrecedentScope scope = Scope(
                "scope.strict-subset",
                new CaseFact("permit-class", "licensed"));
            var declaredSupport = new[] { "evidence.late" };

            InstitutionalServiceResult<Holding> established =
                InstitutionalAppealPrecedentService.EstablishHolding(
                    fixture.Filing.Run.Report,
                    fixture.Appeal.AppealId,
                    "holding.strict-subset",
                    "rule.strict-subset",
                    "issue.output-control",
                    scope,
                    declaredSupport);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, established.Outcome);
            Assert.AreEqual(2, fixture.ResultingRuling.EvidenceArtifactIds.Count);
            CollectionAssert.AreEqual(
                declaredSupport,
                established.Value.SupportingEvidenceArtifactIds);

            InstitutionalServiceResult<Holding> replay =
                InstitutionalAppealPrecedentService.EstablishHolding(
                    fixture.Filing.Run.Report,
                    fixture.Appeal.AppealId,
                    "holding.strict-subset",
                    "rule.strict-subset",
                    "issue.output-control",
                    established.Value.Scope.CopyForTest(),
                    declaredSupport);

            Assert.AreEqual(InstitutionalServiceOutcome.NoChange, replay.Outcome);
            Assert.AreSame(established.Value, replay.Value);

            InstitutionalServiceResult<List<Holding>> matches =
                InstitutionalAppealPrecedentService.FindMatchingHoldings(
                    fixture.Filing.Run.Report,
                    "issue.output-control",
                    new CaseFactSet(new[]
                    {
                        new CaseFact("permit-class", "licensed"),
                    }));

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, matches.Outcome);
            CollectionAssert.AreEqual(
                new[] { established.Value.HoldingId },
                matches.Value.ConvertAll(value => value.HoldingId));
        }

        [Test]
        public void FindMatchingHoldings_UsesAllFactsAndDeterministicSpecificityOrdering()
        {
            var report = new InstitutionalConsequenceReport();
            AddValidHolding(
                report,
                "b",
                8,
                "issue.generic",
                Scope(
                    "scope.b",
                    new CaseFact("category", "licensed"),
                    new CaseFact("state", "active")));
            AddValidHolding(
                report,
                "a",
                7,
                "issue.generic",
                Scope(
                    "scope.a",
                    new CaseFact("category", "licensed"),
                    new CaseFact("state", "active")));
            AddValidHolding(
                report,
                "broad",
                6,
                "issue.generic",
                Scope("scope.broad", new CaseFact("category", "licensed")));
            AddValidHolding(
                report,
                "wrong-issue",
                5,
                "issue.other",
                Scope("scope.other", new CaseFact("category", "licensed")));

            var facts = new CaseFactSet(new[]
            {
                new CaseFact("state", "active"),
                new CaseFact("category", "licensed"),
                new CaseFact("extra", "ignored"),
            });
            InstitutionalServiceResult<List<Holding>> result =
                InstitutionalAppealPrecedentService.FindMatchingHoldings(
                    report,
                    "issue.generic",
                    facts);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, result.Outcome);
            CollectionAssert.AreEqual(
                new[] { "holding.a", "holding.b", "holding.broad" },
                result.Value.ConvertAll(holding => holding.HoldingId));

            InstitutionalServiceResult<List<Holding>> incomplete =
                InstitutionalAppealPrecedentService.FindMatchingHoldings(
                    report,
                    "issue.generic",
                    new CaseFactSet(new[] { new CaseFact("state", "active") }));
            Assert.AreEqual(InstitutionalServiceOutcome.NoChange, incomplete.Outcome);
            Assert.IsEmpty(incomplete.Value);
        }

        [Test]
        public void FindMatchingHoldings_HonoursExactIndividualAndEmployerReach()
        {
            var report = new InstitutionalConsequenceReport();
            CaseFact required = new CaseFact("category", "licensed");
            AddValidHolding(
                report,
                "individual-match",
                5,
                "issue.generic",
                new PrecedentScope
                {
                    ScopeId = "scope.individual-match",
                    Reach = PrecedentReach.Individual,
                    BoundAgentId = "agent.target",
                    RequiredFacts = new CaseFactSet(new[] { required.Copy() }),
                });
            AddValidHolding(
                report,
                "individual-other",
                6,
                "issue.generic",
                new PrecedentScope
                {
                    ScopeId = "scope.individual-other",
                    Reach = PrecedentReach.Individual,
                    BoundAgentId = "agent.other",
                    RequiredFacts = new CaseFactSet(new[] { required.Copy() }),
                });
            AddValidHolding(
                report,
                "employer-match",
                7,
                "issue.generic",
                new PrecedentScope
                {
                    ScopeId = "scope.employer-match",
                    Reach = PrecedentReach.Employer,
                    BoundEmployerId = "employer.target",
                    RequiredFacts = new CaseFactSet(new[] { required.Copy() }),
                });
            AddValidHolding(
                report,
                "employer-other",
                8,
                "issue.generic",
                new PrecedentScope
                {
                    ScopeId = "scope.employer-other",
                    Reach = PrecedentReach.Employer,
                    BoundEmployerId = "employer.other",
                    RequiredFacts = new CaseFactSet(new[] { required.Copy() }),
                });
            AddValidHolding(
                report,
                "jurisdiction",
                9,
                "issue.generic",
                Scope("scope.jurisdiction", required.Copy()));
            var facts = new CaseFactSet(new[] { required.Copy() });

            InstitutionalServiceResult<List<Holding>> exact =
                InstitutionalAppealPrecedentService.FindMatchingHoldings(
                    report,
                    "issue.generic",
                    "agent.target",
                    "employer.target",
                    null,
                    facts);
            InstitutionalServiceResult<List<Holding>> noTarget =
                InstitutionalAppealPrecedentService.FindMatchingHoldings(
                    report,
                    "issue.generic",
                    facts);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, exact.Outcome);
            CollectionAssert.AreEqual(
                new[]
                {
                    "holding.individual-match",
                    "holding.employer-match",
                    "holding.jurisdiction",
                },
                exact.Value.ConvertAll(holding => holding.HoldingId));
            CollectionAssert.AreEqual(
                new[] { "holding.jurisdiction" },
                noTarget.Value.ConvertAll(holding => holding.HoldingId));
        }

        [Test]
        public void ApplyHolding_RequiresExactTargetContextBeforeCitationMutation()
        {
            var report = new InstitutionalConsequenceReport();
            Holding holding = AddValidHolding(
                report,
                "individual-source",
                6,
                "issue.generic",
                new PrecedentScope
                {
                    ScopeId = "scope.individual-source",
                    Reach = PrecedentReach.Individual,
                    BoundAgentId = "agent.target",
                    RequiredFacts = new CaseFactSet(new[]
                    {
                        new CaseFact("category", "licensed"),
                    }),
                });
            Ruling target = AddTargetCase(report, "target-context", 9, "issue.generic");
            var facts = new CaseFactSet(new[]
            {
                new CaseFact("category", "licensed"),
            });

            InstitutionalServiceResult<Holding> wrong =
                InstitutionalAppealPrecedentService.ApplyHolding(
                    report,
                    holding.HoldingId,
                    target.RulingId,
                    target.CaseId,
                    "issue.generic",
                    "agent.other",
                    "employer.target",
                    null,
                    facts);
            InstitutionalServiceResult<Holding> exact =
                InstitutionalAppealPrecedentService.ApplyHolding(
                    report,
                    holding.HoldingId,
                    target.RulingId,
                    target.CaseId,
                    "issue.generic",
                    "agent.target",
                    "employer.target",
                    null,
                    facts);

            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, wrong.Outcome);
            Assert.AreEqual("precedent.holding-does-not-match", wrong.ReasonId);
            Assert.AreEqual(InstitutionalServiceOutcome.Applied, exact.Outcome);
            CollectionAssert.AreEqual(new[] { holding.HoldingId }, target.CitedHoldingIds);
        }

        [Test]
        public void ApplyHolding_RecordsCitationScopeAndCaseExactlyOnce()
        {
            var report = new InstitutionalConsequenceReport();
            Holding holding = AddValidHolding(
                report,
                "source",
                6,
                "issue.generic",
                Scope("scope.source", new CaseFact("category", "licensed")));
            Ruling target = AddTargetCase(report, "target", 9, "issue.generic");
            var facts = new CaseFactSet(new[] { new CaseFact("category", "licensed") });

            InstitutionalServiceResult<Holding> first =
                InstitutionalAppealPrecedentService.ApplyHolding(
                    report,
                    holding.HoldingId,
                    target.RulingId,
                    target.CaseId,
                    "issue.generic",
                    facts);
            InstitutionalServiceResult<Holding> second =
                InstitutionalAppealPrecedentService.ApplyHolding(
                    report,
                    holding.HoldingId,
                    target.RulingId,
                    target.CaseId,
                    "issue.generic",
                    facts);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, first.Outcome);
            Assert.AreEqual(InstitutionalServiceOutcome.NoChange, second.Outcome);
            CollectionAssert.AreEqual(new[] { holding.HoldingId }, target.CitedHoldingIds);
            CollectionAssert.AreEqual(new[] { holding.Scope.ScopeId }, target.CitedScopeIds);
            CollectionAssert.AreEqual(new[] { target.CaseId }, holding.AppliedCaseIds);
            CollectionAssert.AreEqual(
                new[] { holding.HoldingId },
                report.DescendantCases[0].CitedHoldingIds);
            Assert.AreEqual(1, CountTimeline(
                report,
                InstitutionalTimelineKind.PrecedentApplied));
        }

        [Test]
        public void ApplyHolding_RejectsPartialRecordsAndChronologyErrors()
        {
            var partial = new InstitutionalConsequenceReport();
            Holding partialHolding = AddValidHolding(
                partial,
                "partial-source",
                6,
                "issue.generic",
                Scope("scope.partial", new CaseFact("category", "licensed")));
            Ruling partialTarget = AddTargetCase(partial, "partial-target", 9, "issue.generic");
            partialTarget.CitedHoldingIds.Add(partialHolding.HoldingId);

            InstitutionalServiceResult<Holding> partialResult =
                InstitutionalAppealPrecedentService.ApplyHolding(
                    partial,
                    partialHolding.HoldingId,
                    partialTarget.RulingId,
                    partialTarget.CaseId,
                    "issue.generic",
                    new CaseFactSet(new[] { new CaseFact("category", "licensed") }));
            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, partialResult.Outcome);
            Assert.AreEqual("precedent.partial-application-record", partialResult.ReasonId);
            Assert.IsEmpty(partialTarget.CitedScopeIds);
            Assert.IsEmpty(partialHolding.AppliedCaseIds);

            var outOfOrder = new InstitutionalConsequenceReport();
            Holding laterHolding = AddValidHolding(
                outOfOrder,
                "late-source",
                10,
                "issue.generic",
                Scope("scope.late", new CaseFact("category", "licensed")));
            Ruling earlierTarget = AddTargetCase(
                outOfOrder,
                "early-target",
                9,
                "issue.generic");
            InstitutionalServiceResult<Holding> chronologyResult =
                InstitutionalAppealPrecedentService.ApplyHolding(
                    outOfOrder,
                    laterHolding.HoldingId,
                    earlierTarget.RulingId,
                    earlierTarget.CaseId,
                    "issue.generic",
                    new CaseFactSet(new[] { new CaseFact("category", "licensed") }));
            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, chronologyResult.Outcome);
            Assert.AreEqual("precedent.invalid-target-ruling", chronologyResult.ReasonId);
        }

        private static FilingFixture CreateFilingFixture()
        {
            var report = new InstitutionalConsequenceReport();
            report.Rulings.Add(new Ruling
            {
                RulingId = "ruling.initial",
                CaseId = "case.alpha",
                Cycle = 2,
                Disposition = RulingDisposition.Denied,
            });
            report.EvidenceArtifacts.Add(new EvidenceArtifact
            {
                ArtifactId = "evidence.before",
                CaseId = "case.alpha",
                EnteredCycle = 3,
            });
            report.EvidenceArtifacts.Add(new EvidenceArtifact
            {
                ArtifactId = "evidence.late",
                CaseId = "case.alpha",
                EnteredCycle = 5,
            });
            report.EvidenceArtifacts.Add(new EvidenceArtifact
            {
                ArtifactId = "evidence.other-case",
                CaseId = "case.other",
                EnteredCycle = 3,
            });

            var filingEvent = new SocietyEvent
            {
                EventId = "event.appeal-filed",
                CauseDecisionId = "decision.appeal",
                IncidentId = "incident.generic",
                Tick = 4,
                Kind = SocietyEventKind.AppealFiled,
                ActorId = "agent.appellant",
                OpportunityId = "opportunity.appeal",
                Visibility = EvidenceVisibility.OfficialRecord,
            };
            report.ObservedAgentActions.Add(new ObservedAgentAction
            {
                Cycle = filingEvent.Tick,
                ActionEventId = filingEvent.EventId,
                ActorId = filingEvent.ActorId,
                Activity = ObservedActivityKind.AppealFiled,
            });

            var run = new InstitutionalConsequenceRun { Report = report };
            run.AssessorActionTraces.Add(new AgentActionTrace
            {
                Cycle = filingEvent.Tick,
                DecisionId = filingEvent.CauseDecisionId,
                ActorId = filingEvent.ActorId,
                Action = SocietyActionKind.Appeal,
                OpportunityId = filingEvent.OpportunityId,
                ResultEventIds = new List<string> { filingEvent.EventId },
            });

            var opportunity = new AppealOpportunity
            {
                OpportunityId = filingEvent.OpportunityId,
                DocketId = "docket.generic",
                CaseId = "case.alpha",
                ChallengedRulingId = "ruling.initial",
                SourceCauseId = "cause.generic",
                HearingCycle = 6,
                PartyAgentIds = new List<string>
                {
                    "agent.observer",
                    filingEvent.ActorId,
                },
            };
            return new FilingFixture
            {
                Run = run,
                Event = filingEvent,
                Opportunities = new List<AppealOpportunity> { opportunity },
            };
        }

        private static ResolvedFixture CreateResolvedFixture(
            RulingDisposition disposition,
            bool resolve)
        {
            FilingFixture filing = CreateFilingFixture();
            InstitutionalServiceResult<Appeal> filed =
                InstitutionalAppealPrecedentService.FileAppeal(
                    filing.Run,
                    filing.Event,
                    filing.Opportunities);
            Assert.AreEqual(InstitutionalServiceOutcome.Applied, filed.Outcome);

            var finding = new OfficialFinding
            {
                FindingId = "finding.result",
                CaseId = "case.alpha",
                Cycle = 6,
                IssueId = "issue.output-control",
                Disposition = disposition == RulingDisposition.ReversedAndRecognised
                    ? FindingDisposition.Established
                    : FindingDisposition.NotEstablished,
                EvidenceArtifactIds = new List<string>
                {
                    "evidence.before",
                    "evidence.late",
                },
            };
            filing.Run.Report.OfficialFindings.Add(finding);
            var resulting = new Ruling
            {
                RulingId = "ruling.result",
                CaseId = "case.alpha",
                Cycle = 6,
                Disposition = disposition,
                FindingId = finding.FindingId,
                EvidenceArtifactIds = new List<string>
                {
                    "evidence.before",
                    "evidence.late",
                },
            };
            filing.Run.Report.Rulings.Add(resulting);

            if (resolve)
            {
                InstitutionalServiceResult<Appeal> resolved =
                    InstitutionalAppealPrecedentService.ResolveAppeal(
                        filing.Run.Report,
                        filed.Value.AppealId,
                        resulting);
                Assert.AreEqual(InstitutionalServiceOutcome.Applied, resolved.Outcome);
            }

            return new ResolvedFixture
            {
                Filing = filing,
                Appeal = filed.Value,
                ResultingRuling = resulting,
            };
        }

        private static Holding AddValidHolding(
            InstitutionalConsequenceReport report,
            string suffix,
            long establishedCycle,
            string issueId,
            PrecedentScope scope)
        {
            string caseId = $"case.source.{suffix}";
            string evidenceId = $"evidence.source.{suffix}";
            string appealId = $"appeal.source.{suffix}";
            string challengedRulingId = $"ruling.challenged.{suffix}";
            string rulingId = $"ruling.source.{suffix}";
            string findingId = $"finding.source.{suffix}";
            string filingEventId = $"event.appeal.source.{suffix}";
            report.EvidenceArtifacts.Add(new EvidenceArtifact
            {
                ArtifactId = evidenceId,
                CaseId = caseId,
                EnteredCycle = establishedCycle - 2,
            });
            report.Rulings.Add(new Ruling
            {
                RulingId = challengedRulingId,
                CaseId = caseId,
                Cycle = establishedCycle - 3,
                Disposition = RulingDisposition.Denied,
            });
            report.Appeals.Add(new Appeal
            {
                AppealId = appealId,
                CaseId = caseId,
                FiledCycle = establishedCycle - 1,
                HearingCycle = establishedCycle,
                AppellantAgentId = $"agent.source.{suffix}",
                FilingActionEventId = filingEventId,
                ChallengedRulingId = challengedRulingId,
                Disposition = AppealDisposition.Reversed,
                ResultingRulingId = rulingId,
                GroundsEvidenceArtifactIds = new List<string> { evidenceId },
            });
            report.OfficialFindings.Add(new OfficialFinding
            {
                FindingId = findingId,
                CaseId = caseId,
                Cycle = establishedCycle,
                IssueId = issueId,
                Disposition = FindingDisposition.Established,
                EvidenceArtifactIds = new List<string> { evidenceId },
            });
            report.Rulings.Add(new Ruling
            {
                RulingId = rulingId,
                CaseId = caseId,
                Cycle = establishedCycle,
                Disposition = RulingDisposition.ReversedAndRecognised,
                FindingId = findingId,
                EvidenceArtifactIds = new List<string> { evidenceId },
            });
            var holding = new Holding
            {
                HoldingId = $"holding.{suffix}",
                EstablishedCycle = establishedCycle,
                SourceAppealId = appealId,
                SourceRulingId = rulingId,
                RuleId = $"rule.{suffix}",
                IssueId = issueId,
                SupportingEvidenceArtifactIds = new List<string> { evidenceId },
                Scope = scope,
            };
            report.Holdings.Add(holding);
            InstitutionalTimeline.Add(
                report,
                establishedCycle - 1,
                InstitutionalTimelineKind.AppealFiled,
                filingEventId,
                $"agent.source.{suffix}",
                appealId);
            InstitutionalTimeline.Add(
                report,
                establishedCycle,
                InstitutionalTimelineKind.AppealHeard,
                appealId,
                caseId,
                rulingId);
            InstitutionalTimeline.Add(
                report,
                establishedCycle,
                InstitutionalTimelineKind.HoldingEstablished,
                rulingId,
                holding.HoldingId,
                holding.RuleId);
            return holding;
        }

        private static Ruling AddTargetCase(
            InstitutionalConsequenceReport report,
            string suffix,
            long cycle,
            string issueId)
        {
            string caseId = $"case.{suffix}";
            string findingId = $"finding.{suffix}";
            report.OfficialFindings.Add(new OfficialFinding
            {
                FindingId = findingId,
                CaseId = caseId,
                Cycle = cycle,
                IssueId = issueId,
                Disposition = FindingDisposition.Established,
            });
            var ruling = new Ruling
            {
                RulingId = $"ruling.{suffix}",
                CaseId = caseId,
                Cycle = cycle,
                FindingId = findingId,
                Disposition = RulingDisposition.ReversedAndRecognised,
            };
            report.Rulings.Add(ruling);
            report.DescendantCases.Add(new DescendantCase
            {
                CaseId = caseId,
                OpenedCycle = cycle - 1,
                Status = DescendantCaseStatus.Open,
                OfficialIssueId = issueId,
            });
            return ruling;
        }

        private static PrecedentScope Scope(string scopeId, params CaseFact[] facts)
        {
            return new PrecedentScope
            {
                ScopeId = scopeId,
                Reach = PrecedentReach.Jurisdiction,
                RequiredFacts = new CaseFactSet(facts),
            };
        }

        private static int CountTimeline(
            InstitutionalConsequenceReport report,
            InstitutionalTimelineKind kind)
        {
            int count = 0;
            for (int i = 0; i < report.Timeline.Count; i++)
                if (report.Timeline[i].Kind == kind) count++;
            return count;
        }

        private sealed class FilingFixture
        {
            internal InstitutionalConsequenceRun Run;
            internal SocietyEvent Event;
            internal List<AppealOpportunity> Opportunities;
        }

        private sealed class ResolvedFixture
        {
            internal FilingFixture Filing;
            internal Appeal Appeal;
            internal Ruling ResultingRuling;
        }
    }

    internal static class PrecedentScopeTestExtensions
    {
        internal static PrecedentScope CopyForTest(this PrecedentScope source)
        {
            return new PrecedentScope
            {
                ScopeId = source.ScopeId,
                Reach = source.Reach,
                BoundAgentId = source.BoundAgentId,
                BoundEmployerId = source.BoundEmployerId,
                IdentityConditionId = source.IdentityConditionId,
                RequiredFacts = source.RequiredFacts.Copy(),
                Retrospective = source.Retrospective,
            };
        }
    }
}
