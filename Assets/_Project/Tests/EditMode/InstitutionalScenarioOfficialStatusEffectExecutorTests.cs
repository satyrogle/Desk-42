using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalScenarioOfficialStatusEffectExecutorTests
    {
        [Test]
        public void Execute_MatchingExactCause_AppliesThroughStatusServiceAndReturnsRequestId()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ScenarioOfficialStatusEffectRequest request = Request();
            EconomicAccountState account = run.EconomicAccounts.Single();

            ScenarioOfficialStatusEffectExecutionResult result =
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, request, RoleMap());

            Assert.That(result.EffectRequestId, Is.EqualTo(request.EffectRequestId));
            Assert.That(result.RequiredDispositionMatched, Is.True);
            Assert.That(result.StatusMutationResult.Changed, Is.True);
            Assert.That(result.StatusMutationResult.CurrentRecognisedState, Is.True);
            Assert.That(result.StatusMutationResult.RecordedMutation, Is.Not.Null);
            Assert.That(result.StatusMutationResult.RecordedMutation.CauseId,
                Is.EqualTo(request.CauseRulingId));
            Assert.That(result.StatusMutationResult.RecordedMutation.AffectedAgentId,
                Is.EqualTo("agent.target"));
            Assert.That(run.Report.Rulings.Single().OfficialStatusMutationIds,
                Is.EquivalentTo(new[]
                {
                    result.StatusMutationResult.RecordedMutation.MutationId,
                }));
            Assert.That(run.FinalSocietyState.GetAgent("agent.target").Standing
                .IsRecognised(request.StatusId), Is.True);
            Assert.That(account.AvailableCredits, Is.EqualTo(105));
        }

        [Test]
        public void Execute_NonMatchingDisposition_IsExplicitNoChange()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ScenarioOfficialStatusEffectRequest request = Request();
            request.RequiredRulingDisposition = RulingDisposition.Denied;

            ScenarioOfficialStatusEffectExecutionResult result =
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, request, RoleMap());

            Assert.That(result.EffectRequestId, Is.EqualTo(request.EffectRequestId));
            Assert.That(result.RequiredDispositionMatched, Is.False);
            Assert.That(result.StatusMutationResult.Changed, Is.False);
            Assert.That(result.StatusMutationResult.CurrentRecognisedState, Is.False);
            Assert.That(result.StatusMutationResult.RecordedMutation, Is.Null);
            Assert.That(run.Report.OfficialStatusMutations, Is.Empty);
            Assert.That(run.Report.Rulings.Single().OfficialStatusMutationIds, Is.Empty);
            Assert.That(run.EconomicAccounts.Single().AvailableCredits, Is.EqualTo(100));
        }

        [Test]
        public void Execute_IdenticalReplay_ReturnsOriginalResultWithoutApplyingTwice()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ScenarioOfficialStatusEffectRequest request = Request();

            ScenarioOfficialStatusEffectExecutionResult first =
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, request, RoleMap());
            ScenarioOfficialStatusEffectExecutionResult replay =
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, request, RoleMap());

            Assert.That(replay.StatusMutationResult.Changed, Is.True);
            Assert.That(replay.StatusMutationResult.RecordedMutation,
                Is.SameAs(first.StatusMutationResult.RecordedMutation));
            Assert.That(run.Report.OfficialStatusMutations, Has.Count.EqualTo(1));
            Assert.That(run.Report.Rulings.Single().OfficialStatusMutationIds,
                Has.Count.EqualTo(1));
            Assert.That(run.EconomicAccounts.Single().AvailableCredits, Is.EqualTo(105));
        }

        [Test]
        public void Execute_SameRequestIdWithChangedDeclaration_RejectsConflictingReplay()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ScenarioOfficialStatusEffectRequest first = Request();
            InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                run, first, RoleMap());
            ScenarioOfficialStatusEffectRequest conflicting = Request();
            conflicting.RequestedResourceDelta = 9;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, conflicting, RoleMap()));

            Assert.That(exception.Message, Does.Contain("conflicting"));
            Assert.That(run.Report.OfficialStatusMutations, Has.Count.EqualTo(1));
            Assert.That(run.EconomicAccounts.Single().AvailableCredits, Is.EqualTo(105));
        }

        [Test]
        public void Execute_NoChangeReplayCannotBeReauthoredIntoMatchingEffect()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ScenarioOfficialStatusEffectRequest first = Request();
            first.RequiredRulingDisposition = RulingDisposition.Denied;
            InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                run, first, RoleMap());
            ScenarioOfficialStatusEffectRequest conflicting = Request();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, conflicting, RoleMap()));

            Assert.That(exception.Message, Does.Contain("conflicting"));
            Assert.That(run.Report.OfficialStatusMutations, Is.Empty);
            Assert.That(run.EconomicAccounts.Single().AvailableCredits, Is.EqualTo(100));
        }

        [Test]
        public void Execute_MissingOrAmbiguousCauseRuling_Rejects(
            [Values(false, true)] bool ambiguous)
        {
            InstitutionalConsequenceRun run = CreateRun();
            ScenarioOfficialStatusEffectRequest request = Request();
            if (ambiguous)
            {
                run.Report.Rulings.Add(Ruling(
                    request.CauseRulingId,
                    request.CauseCaseId,
                    request.Cycle,
                    RulingDisposition.ProvisionallyRecognised));
            }
            else
            {
                run.Report.Rulings.Clear();
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, request, RoleMap()));

            Assert.That(exception.Message,
                Does.Contain(ambiguous ? "ambiguous" : "missing"));
            Assert.That(run.Report.OfficialStatusMutations, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Execute_WrongDeclaredCaseOrCycle_Rejects(bool wrongCase)
        {
            InstitutionalConsequenceRun run = CreateRun();
            ScenarioOfficialStatusEffectRequest request = Request();
            if (wrongCase) request.CauseCaseId = "case.wrong";
            else request.Cycle++;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, request, RoleMap()));

            Assert.That(exception.Message,
                Does.Contain(wrongCase ? "belongs to case" : "issued at cycle"));
            Assert.That(run.Report.OfficialStatusMutations, Is.Empty);
        }

        [Test]
        public void Execute_TargetRoleWithoutMapping_Rejects()
        {
            InstitutionalConsequenceRun run = CreateRun();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, Request(), new Dictionary<string, string>()));

            Assert.That(exception.Message, Does.Contain("no agent mapping"));
        }

        [Test]
        public void Execute_TargetRoleMappedToMissingAgent_Rejects()
        {
            InstitutionalConsequenceRun run = CreateRun();
            var mapping = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role.target"] = "agent.missing",
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, Request(), mapping));

            Assert.That(exception.Message, Does.Contain("maps to missing agent"));
        }

        [Test]
        public void Execute_DirectAgentIdInTargetRole_Rejects()
        {
            InstitutionalConsequenceRun run = CreateRun();
            ScenarioOfficialStatusEffectRequest request = Request();
            request.TargetRoleId = "agent.target";
            var mapping = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["agent.target"] = "agent.target",
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, request, mapping));

            Assert.That(exception.Message, Does.Contain("forbidden direct agent id"));
        }

        [Test]
        public void Execute_ReplayWithChangedRoleBinding_Rejects()
        {
            InstitutionalConsequenceRun run = CreateRun(includeSecondAgent: true);
            InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                run, Request(), RoleMap());
            var changedMapping = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role.target"] = "agent.other",
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                InstitutionalScenarioOfficialStatusEffectExecutor.Execute(
                    run, Request(), changedMapping));

            Assert.That(exception.Message, Does.Contain("conflicting"));
            Assert.That(run.Report.OfficialStatusMutations, Has.Count.EqualTo(1));
        }

        private static InstitutionalConsequenceRun CreateRun(bool includeSecondAgent = false)
        {
            var target = new AgentState
            {
                StableId = "agent.target",
                SimulationOrdinal = 0,
                PresentationId = "presentation.target",
                DisplayName = "Target",
                SpeciesId = "species.test",
                HouseholdId = "household.target",
                EmployerId = "employer.test",
            };
            target.Standing.SetRecognised("status.access", false);
            var society = new SocietyState
            {
                MasterSeed = 42,
                CurrentTick = 8,
                Regime = new InstitutionalRegimeState(),
            };
            society.Agents.Add(target);
            var run = new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport(),
                FinalSocietyState = society,
            };
            run.Report.Rulings.Add(Ruling(
                "ruling.primary",
                "case.primary",
                5,
                RulingDisposition.ProvisionallyRecognised));
            run.EconomicAccounts.Add(new EconomicAccountState
            {
                AgentId = target.StableId,
                AvailableCredits = 100,
            });

            if (includeSecondAgent)
            {
                var other = new AgentState
                {
                    StableId = "agent.other",
                    SimulationOrdinal = 1,
                    PresentationId = "presentation.other",
                    DisplayName = "Other",
                    SpeciesId = "species.test",
                    HouseholdId = "household.other",
                    EmployerId = "employer.test",
                };
                other.Standing.SetRecognised("status.access", false);
                society.Agents.Add(other);
                run.EconomicAccounts.Add(new EconomicAccountState
                {
                    AgentId = other.StableId,
                    AvailableCredits = 100,
                });
            }
            return run;
        }

        private static ScenarioOfficialStatusEffectRequest Request()
        {
            return new ScenarioOfficialStatusEffectRequest
            {
                EffectRequestId = "effect.primary-access",
                Cycle = 5,
                CauseCaseId = "case.primary",
                CauseRulingId = "ruling.primary",
                RequiredRulingDisposition = RulingDisposition.ProvisionallyRecognised,
                TargetRoleId = "role.target",
                StatusId = "status.access",
                RequestedRecognisedState = true,
                RequestedResourceDelta = 5,
            };
        }

        private static Dictionary<string, string> RoleMap()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role.target"] = "agent.target",
            };
        }

        private static Ruling Ruling(
            string rulingId,
            string caseId,
            long cycle,
            RulingDisposition disposition)
        {
            return new Ruling
            {
                RulingId = rulingId,
                CaseId = caseId,
                Cycle = cycle,
                PolicyConfigurationId = "policy.test",
                PolicyVersion = "policy.test.v1",
                Disposition = disposition,
                FindingId = "finding.test",
            };
        }
    }
}
