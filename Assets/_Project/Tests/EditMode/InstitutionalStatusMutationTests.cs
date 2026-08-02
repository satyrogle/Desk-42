using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalStatusMutationTests
    {
        [Test]
        public void NoOp_ReturnsExplicitStateWithoutInventingMutation()
        {
            InstitutionalConsequenceRun run = InstitutionalConsequenceLoop.RunForAssessor(
                424242,
                InstitutionalPolicyConfigurations.RecordsFirst());
            AgentState agent = run.FinalSocietyState.Agents[0];
            const string statusId = "status.test.already-recognised";
            agent.Standing.SetRecognised(statusId, true);
            Ruling ruling = CreateTestRuling(run, "ruling.test.no-op");
            int mutationCount = run.Report.OfficialStatusMutations.Count;
            int timelineCount = run.Report.Timeline.Count;

            StatusMutationResult result = InstitutionalStatusMutationService.Apply(
                run,
                ruling,
                agent.StableId,
                statusId,
                true,
                0);

            Assert.IsFalse(result.Changed);
            Assert.IsTrue(result.CurrentRecognisedState);
            Assert.IsNull(result.RecordedMutation);
            Assert.AreEqual(mutationCount, run.Report.OfficialStatusMutations.Count);
            Assert.AreEqual(timelineCount, run.Report.Timeline.Count);
            Assert.IsEmpty(ruling.OfficialStatusMutationIds);
        }

        [Test]
        public void ChangedRequest_RecordsOneMutationAndItsMaterialDelta()
        {
            InstitutionalConsequenceRun run = InstitutionalConsequenceLoop.RunForAssessor(
                424242,
                InstitutionalPolicyConfigurations.RecordsFirst());
            AgentState agent = run.FinalSocietyState.Agents[0];
            const string statusId = "status.test.new-recognition";
            agent.Standing.SetRecognised(statusId, false);
            Ruling ruling = CreateTestRuling(run, "ruling.test.changed");
            EconomicAccountState account = run.EconomicAccounts.Single(value =>
                value.AgentId == agent.StableId);
            int creditsBefore = account.AvailableCredits;

            StatusMutationResult result = InstitutionalStatusMutationService.Apply(
                run,
                ruling,
                agent.StableId,
                statusId,
                true,
                7);

            Assert.IsTrue(result.Changed);
            Assert.IsTrue(result.CurrentRecognisedState);
            Assert.NotNull(result.RecordedMutation);
            Assert.AreEqual(result.RecordedMutation.MutationId,
                ruling.OfficialStatusMutationIds.Single());
            Assert.AreEqual(ruling.RulingId, result.RecordedMutation.CauseId);
            Assert.AreEqual(7, result.RecordedMutation.ResourceDelta);
            Assert.AreEqual(creditsBefore + 7, account.AvailableCredits);
            Assert.IsTrue(agent.Standing.IsRecognised(statusId));
            Assert.That(run.Report.Timeline.Any(entry =>
                entry.Kind == InstitutionalTimelineKind.StatusMutated &&
                entry.CauseId == ruling.RulingId &&
                entry.SubjectId == agent.StableId));
        }

        [Test]
        public void MissingResourceAccountRejectsBeforeAnyStatusOrReportMutation()
        {
            var agent = new AgentState
            {
                StableId = "agent.test.no-account",
                SimulationOrdinal = 0,
            };
            agent.Standing.SetRecognised("status.test.atomic", false);
            var run = new InstitutionalConsequenceRun
            {
                FinalSocietyState = new SocietyState
                {
                    Agents = new System.Collections.Generic.List<AgentState> { agent },
                },
                Report = new InstitutionalConsequenceReport(),
            };
            Ruling ruling = CreateTestRuling(run, "ruling.test.atomic");

            Assert.Throws<System.InvalidOperationException>(() =>
                InstitutionalStatusMutationService.Apply(
                    run,
                    ruling,
                    agent.StableId,
                    "status.test.atomic",
                    true,
                    5));

            Assert.IsFalse(agent.Standing.IsRecognised("status.test.atomic"));
            Assert.That(run.Report.OfficialStatusMutations, Is.Empty);
            Assert.That(ruling.OfficialStatusMutationIds, Is.Empty);
            Assert.That(run.Report.Timeline, Is.Empty);
        }

        private static Ruling CreateTestRuling(
            InstitutionalConsequenceRun run,
            string rulingId)
        {
            var ruling = new Ruling
            {
                RulingId = rulingId,
                CaseId = "case.test.status-mutation",
                Cycle = run.FinalSocietyState.CurrentTick + 1,
                PolicyConfigurationId = "configuration.test",
                PolicyVersion = "configuration.test.v1",
                Disposition = RulingDisposition.Recognised,
                FindingId = "finding.test.status-mutation",
            };
            run.Report.Rulings.Add(ruling);
            return ruling;
        }
    }
}
