using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalConnectedOutcomeProjectorTests
    {
        [Test]
        public void Project_OwnsNamedPairedPublication_AndIsIdempotent()
        {
            InstitutionalConsequenceRun run = Run();

            InstitutionalServiceResult<ConnectedOutcomePair> first =
                InstitutionalConnectedOutcomeProjector.Project(
                    run,
                    "connected:transfer.alpha",
                    "rule.alpha",
                    "resource.alpha",
                    "agent:winner",
                    "agent:loser",
                    3);
            InstitutionalServiceResult<ConnectedOutcomePair> replay =
                InstitutionalConnectedOutcomeProjector.Project(
                    run,
                    "connected:transfer.alpha",
                    "rule.alpha",
                    "resource.alpha",
                    "agent:winner",
                    "agent:loser",
                    3);

            Assert.That(first.Outcome, Is.EqualTo(InstitutionalServiceOutcome.Applied));
            Assert.That(replay.Outcome, Is.EqualTo(InstitutionalServiceOutcome.NoChange));
            Assert.That(replay.Value, Is.SameAs(first.Value));
            Assert.That(run.Report.ConnectedOutcomes, Has.Count.EqualTo(1));
            ConnectedOutcomePair pair = run.Report.ConnectedOutcomes[0];
            Assert.That(pair.CauseRuleId, Is.EqualTo("rule.alpha"));
            Assert.That(pair.ConnectionId, Is.EqualTo("resource.alpha"));
            Assert.That(pair.WinnerAgentId, Is.EqualTo("agent:winner"));
            Assert.That(pair.WinnerDisplayName, Is.EqualTo("Winner"));
            Assert.That(pair.WinnerResourceDelta, Is.EqualTo(3));
            Assert.That(pair.LoserAgentId, Is.EqualTo("agent:loser"));
            Assert.That(pair.LoserDisplayName, Is.EqualTo("Loser"));
            Assert.That(pair.LoserResourceDelta, Is.EqualTo(-3));
        }

        [Test]
        public void Project_RejectsConflictingReplayWithoutMutatingReport()
        {
            InstitutionalConsequenceRun run = Run();
            InstitutionalConnectedOutcomeProjector.Project(
                run,
                "connected:transfer.alpha",
                "rule.alpha",
                "resource.alpha",
                "agent:winner",
                "agent:loser",
                3);

            InstitutionalServiceResult<ConnectedOutcomePair> conflict =
                InstitutionalConnectedOutcomeProjector.Project(
                    run,
                    "connected:transfer.alpha",
                    "rule.other",
                    "resource.alpha",
                    "agent:winner",
                    "agent:loser",
                    3);

            Assert.That(conflict.Outcome, Is.EqualTo(InstitutionalServiceOutcome.Rejected));
            Assert.That(conflict.ReasonId,
                Is.EqualTo("connected-outcome.conflicting-existing-pair"));
            Assert.That(run.Report.ConnectedOutcomes, Has.Count.EqualTo(1));
            Assert.That(run.Report.ConnectedOutcomes[0].CauseRuleId, Is.EqualTo("rule.alpha"));
        }

        [Test]
        public void Project_RejectsMissingOrSelfConnectedParticipants()
        {
            InstitutionalConsequenceRun run = Run();

            InstitutionalServiceResult<ConnectedOutcomePair> missing =
                InstitutionalConnectedOutcomeProjector.Project(
                    run,
                    "connected:missing",
                    "rule.alpha",
                    "resource.alpha",
                    "agent:unknown",
                    "agent:loser",
                    1);
            InstitutionalServiceResult<ConnectedOutcomePair> self =
                InstitutionalConnectedOutcomeProjector.Project(
                    run,
                    "connected:self",
                    "rule.alpha",
                    "resource.alpha",
                    "agent:winner",
                    "agent:winner",
                    1);

            Assert.That(missing.ReasonId, Is.EqualTo("connected-outcome.missing-participant"));
            Assert.That(self.ReasonId, Is.EqualTo("connected-outcome.invalid-request"));
            Assert.That(run.Report.ConnectedOutcomes, Is.Empty);
        }

        private static InstitutionalConsequenceRun Run()
        {
            var state = new SocietyState();
            state.Agents.Add(new AgentState
            {
                StableId = "agent:winner",
                DisplayName = "Winner",
            });
            state.Agents.Add(new AgentState
            {
                StableId = "agent:loser",
                DisplayName = "Loser",
            });
            return new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport(),
                FinalSocietyState = state,
            };
        }
    }
}
