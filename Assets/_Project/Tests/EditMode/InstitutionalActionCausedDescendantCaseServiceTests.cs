using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalActionCausedDescendantCaseServiceTests
    {
        [Test]
        public void Open_EarlierAutonomousCause_AppliesDetachedEnvelopeExactlyOnce()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddDisclosureCause(run, "action.source", 4);
            ScenarioActionCausedDescendantCaseDefinition definition = Definition();
            ScenarioCaseDefinition caseDefinition = CaseDefinition();

            InstitutionalServiceResult<DescendantCase> applied =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    definition,
                    caseDefinition,
                    Bindings(),
                    7);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, applied.Outcome);
            DescendantCase opened = applied.Value;
            Assert.NotNull(opened);
            Assert.AreEqual("case.child", opened.CaseId);
            Assert.AreEqual("case.parent", opened.ParentCaseId);
            Assert.AreEqual(7, opened.OpenedCycle);
            Assert.AreEqual(DescendantCaseKind.RelatedClaim, opened.Kind);
            Assert.AreEqual("action.source", opened.ParentCauseId);
            Assert.AreEqual("action.source", opened.OriginatingEventId);
            Assert.AreEqual("action.source", opened.CausalAgentActionId);
            Assert.AreEqual("ruling.parent", opened.OriginatingRulingId);
            Assert.AreEqual("agent.claimant", opened.ClaimantAgentId);
            Assert.AreEqual("agent.respondent", opened.RespondentId);
            Assert.AreEqual("issue.child", opened.OfficialIssueId);
            CollectionAssert.AreEqual(
                new[] { "agent.claimant", "agent.trigger" },
                opened.ConnectedAgentIds);
            CollectionAssert.AreEqual(
                new[] { "action.source" },
                opened.SourceActionEventIds);
            Assert.IsTrue(opened.Facts.Contains("region", "north"));

            caseDefinition.Facts.Facts.Single().Value = "mutated-after-open";
            Assert.IsTrue(opened.Facts.Contains("region", "north"));
            Assert.AreEqual(1, ResultLinkCount(run, "action.source", "case.child"));
            Assert.AreEqual(1, OpeningTimelineCount(run, "action.source", "case.child"));

            int caseCount = run.Report.DescendantCases.Count;
            int timelineCount = run.Report.Timeline.Count;
            InstitutionalServiceResult<DescendantCase> repeated =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    definition,
                    CaseDefinition(),
                    Bindings(),
                    9);

            Assert.AreEqual(InstitutionalServiceOutcome.NoChange, repeated.Outcome);
            Assert.AreSame(opened, repeated.Value);
            Assert.AreEqual(caseCount, run.Report.DescendantCases.Count);
            Assert.AreEqual(timelineCount, run.Report.Timeline.Count);
            Assert.AreEqual(1, ResultLinkCount(run, "action.source", "case.child"));
        }

        [Test]
        public void Open_NoMatchingCauseAtDeclaredCycle_IsNoChangeAndAtomic()
        {
            InstitutionalConsequenceRun run = CreateRun();
            int timelineCount = run.Report.Timeline.Count;

            InstitutionalServiceResult<DescendantCase> result =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    Definition(),
                    CaseDefinition(),
                    Bindings(),
                    7);

            Assert.AreEqual(InstitutionalServiceOutcome.NoChange, result.Outcome);
            Assert.AreEqual("descendant.trigger-not-observed", result.ReasonId);
            Assert.IsNull(result.Value);
            Assert.IsEmpty(run.Report.DescendantCases);
            Assert.AreEqual(timelineCount, run.Report.Timeline.Count);
        }

        [Test]
        public void Open_NoTriggerAndNoOptionalOrigin_IsCleanNonMaterialization()
        {
            InstitutionalConsequenceRun run = CreateRun();
            run.Report.Rulings.Clear();

            InstitutionalServiceResult<DescendantCase> result =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    Definition(),
                    CaseDefinition(),
                    Bindings(),
                    7);

            Assert.AreEqual(InstitutionalServiceOutcome.NoChange, result.Outcome);
            Assert.AreEqual("descendant.trigger-not-observed", result.ReasonId);
            Assert.IsEmpty(run.Report.DescendantCases);
            Assert.IsEmpty(run.Report.Timeline);
        }

        [Test]
        public void Open_PresentTriggerWithoutDeclaredOrigin_IsRejectedAtomically()
        {
            InstitutionalConsequenceRun run = CreateRun();
            run.Report.Rulings.Clear();
            AddDisclosureCause(run, "action.source", 4);

            InstitutionalServiceResult<DescendantCase> result =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    Definition(),
                    CaseDefinition(),
                    Bindings(),
                    7);

            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, result.Outcome);
            Assert.AreEqual("descendant.missing-originating-ruling", result.ReasonId);
            Assert.IsEmpty(run.Report.DescendantCases);
            Assert.IsEmpty(run.Report.Timeline);
            Assert.That(run.Report.ObservedAgentActions.Single()
                .ResultDescendantCaseIds, Is.Empty);
        }

        [Test]
        public void Open_MultipleMatchingCausesOnExactTriggerCycle_IsRejectedWithoutProjection()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddDisclosureCause(run, "action.first", 4);
            AddDisclosureCause(run, "action.second", 4);

            InstitutionalServiceResult<DescendantCase> result =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    Definition(),
                    CaseDefinition(),
                    Bindings(),
                    7);

            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, result.Outcome);
            Assert.AreEqual("descendant.ambiguous-trigger", result.ReasonId);
            Assert.IsEmpty(run.Report.DescendantCases);
            Assert.IsEmpty(run.Report.Timeline);
            Assert.That(run.Report.ObservedAgentActions.All(action =>
                action.ResultDescendantCaseIds.Count == 0));
        }

        [Test]
        public void Open_ActionsOutsideExactTriggerCycle_DoNotCompeteWithDeclaredCause()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddDisclosureCause(run, "action.early", 3);
            AddDisclosureCause(run, "action.declared", 4);
            AddDisclosureCause(run, "action.late", 5);

            InstitutionalServiceResult<DescendantCase> result =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    Definition(),
                    CaseDefinition(),
                    Bindings(),
                    7);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, result.Outcome);
            Assert.AreEqual("action.declared", result.Value.CausalAgentActionId);
            Assert.AreEqual(0, ResultLinkCount(run, "action.early", "case.child"));
            Assert.AreEqual(1, ResultLinkCount(run, "action.declared", "case.child"));
            Assert.AreEqual(0, ResultLinkCount(run, "action.late", "case.child"));
        }

        [Test]
        public void Open_MatchingTraceWithoutObservedAction_IsRejected()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddDisclosureCause(run, "action.unprojected", 4, addObservation: false);

            InstitutionalServiceResult<DescendantCase> result =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    Definition(),
                    CaseDefinition(),
                    Bindings(),
                    7);

            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, result.Outcome);
            Assert.AreEqual("descendant.invalid-trigger-projection", result.ReasonId);
            Assert.IsEmpty(run.Report.DescendantCases);
            Assert.IsEmpty(run.Report.Timeline);
        }

        [Test]
        public void Open_NonBeliefAction_BlankPropositionIsWildcard()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddWorkCause(run, "action.work", 6);
            ScenarioActionCausedDescendantCaseDefinition definition = Definition();
            definition.TriggerActionKind = SocietyActionKind.Work;
            definition.TriggerCycle = 6;
            definition.TriggerOpportunityId = "opportunity.sample";
            definition.TriggerPropositionId = null;

            InstitutionalServiceResult<DescendantCase> result =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    definition,
                    CaseDefinition(),
                    Bindings(),
                    7);

            Assert.AreEqual(InstitutionalServiceOutcome.Applied, result.Outcome);
            Assert.AreEqual("action.work", result.Value.CausalAgentActionId);
        }

        [Test]
        public void Open_AfterDeclaredCycleWithoutExistingCase_RejectsBackfill()
        {
            InstitutionalConsequenceRun run = CreateRun();
            AddDisclosureCause(run, "action.source", 4);

            InstitutionalServiceResult<DescendantCase> result =
                InstitutionalActionCausedDescendantCaseService.Open(
                    run,
                    Definition(),
                    CaseDefinition(),
                    Bindings(),
                    8);

            Assert.AreEqual(InstitutionalServiceOutcome.Rejected, result.Outcome);
            Assert.AreEqual("descendant.declared-cycle-missed", result.ReasonId);
            Assert.IsEmpty(run.Report.DescendantCases);
            Assert.That(run.Report.ObservedAgentActions.Single()
                .ResultDescendantCaseIds, Is.Empty);
        }

        private static InstitutionalConsequenceRun CreateRun()
        {
            var run = new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport(),
            };
            run.Report.Rulings.Add(new Ruling
            {
                RulingId = "ruling.parent",
                CaseId = "case.parent",
                Cycle = 2,
                Disposition = RulingDisposition.Denied,
            });
            return run;
        }

        private static ScenarioActionCausedDescendantCaseDefinition Definition()
        {
            return new ScenarioActionCausedDescendantCaseDefinition
            {
                DescendantDefinitionId = "descendant.child",
                CaseId = "case.child",
                ParentCaseId = "case.parent",
                OpenCycle = 7,
                TriggerCycle = 4,
                TriggerRoleId = "role.trigger",
                TriggerActionKind = SocietyActionKind.Disclose,
                TriggerPropositionId = "proposition.trigger",
                OriginatingRulingId = "ruling.parent",
                ConnectedRoleIds = new List<string>
                {
                    "role.claimant",
                    "role.trigger",
                },
            };
        }

        private static ScenarioCaseDefinition CaseDefinition()
        {
            return new ScenarioCaseDefinition
            {
                CaseId = "case.child",
                IssueId = "issue.child",
                ClaimantRoleId = "role.claimant",
                RespondentRoleId = "role.respondent",
                Facts = new CaseFactSet(new[] { new CaseFact("region", "north") }),
                OpenCycle = 7,
                InitialEvidenceCutoffCycle = 9,
                InitialRulingCycle = 9,
                AdjudicationEvidenceCutoffCycle = 11,
                AdjudicationCycle = 11,
            };
        }

        private static Dictionary<string, string> Bindings()
        {
            return new Dictionary<string, string>
            {
                ["role.trigger"] = "agent.trigger",
                ["role.claimant"] = "agent.claimant",
                ["role.respondent"] = "agent.respondent",
            };
        }

        private static void AddDisclosureCause(
            InstitutionalConsequenceRun run,
            string actionEventId,
            long cycle,
            bool addObservation = true)
        {
            string beliefId = $"belief.{actionEventId}";
            run.AssessorActionTraces.Add(new AgentActionTrace
            {
                Cycle = cycle,
                DecisionId = $"decision.{actionEventId}",
                ActorId = "agent.trigger",
                Action = SocietyActionKind.Disclose,
                SubjectBeliefId = beliefId,
                PerceptionSnapshot = new AgentPerception
                {
                    Beliefs = new List<BeliefState>
                    {
                        new()
                        {
                            BeliefId = beliefId,
                            PropositionId = "proposition.trigger",
                        },
                    },
                },
                ResultEventIds = new List<string> { actionEventId },
            });
            if (!addObservation) return;
            run.Report.ObservedAgentActions.Add(new ObservedAgentAction
            {
                Cycle = cycle,
                ActionEventId = actionEventId,
                ActorId = "agent.trigger",
                Activity = ObservedActivityKind.EvidenceSubmitted,
            });
        }

        private static void AddWorkCause(
            InstitutionalConsequenceRun run,
            string actionEventId,
            long cycle)
        {
            run.AssessorActionTraces.Add(new AgentActionTrace
            {
                Cycle = cycle,
                DecisionId = $"decision.{actionEventId}",
                ActorId = "agent.trigger",
                Action = SocietyActionKind.Work,
                OpportunityId = "opportunity.sample",
                ResultEventIds = new List<string> { actionEventId },
            });
            run.Report.ObservedAgentActions.Add(new ObservedAgentAction
            {
                Cycle = cycle,
                ActionEventId = actionEventId,
                ActorId = "agent.trigger",
                Activity = ObservedActivityKind.WorkPerformed,
            });
        }

        private static int ResultLinkCount(
            InstitutionalConsequenceRun run,
            string actionEventId,
            string caseId)
        {
            return run.Report.ObservedAgentActions
                .Single(action => action.ActionEventId == actionEventId)
                .ResultDescendantCaseIds.Count(id => id == caseId);
        }

        private static int OpeningTimelineCount(
            InstitutionalConsequenceRun run,
            string actionEventId,
            string caseId)
        {
            return run.Report.Timeline.Count(entry =>
                entry.Kind == InstitutionalTimelineKind.DescendantCaseOpened &&
                entry.CauseId == actionEventId &&
                entry.SubjectId == caseId);
        }
    }
}
