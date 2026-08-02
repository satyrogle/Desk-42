using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalDecisionResolutionTests
    {
        [Test]
        public void ContestedCapacity_StableOrdinalWinsBeforeActionPhase_AndLoserFallsBack()
        {
            const string opportunityId = "opportunity.shared-capacity";
            AgentState workWinner = CreateWorker("agent.work-winner", 0, canSeekAid: false);
            AgentState aidLoser = CreateWorker("agent.aid-loser", 1, canSeekAid: true);
            var state = new SocietyState
            {
                MasterSeed = 420042,
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 100,
                    AidEffectiveness = 100,
                    DisclosureProtection = 50,
                    RetaliationRisk = 50,
                    AppealAccessibility = 50,
                    DecisionVariationAmplitude = 0,
                },
            };

            // Deliberately reverse storage order. SimulationOrdinal, not collection
            // position or action phase, owns the deterministic reservation tie-break.
            state.Agents.Add(aidLoser);
            state.Agents.Add(workWinner);

            var input = new SimulationInput
            {
                IncidentId = "incident.capacity-arbitration",
                WorkAvailable = true,
                AidAvailable = true,
                DisclosureRequested = false,
                AppealWindowOpen = false,
                RestrictAidToOpportunities = true,
                WorkOpportunities = new List<WorkOpportunity>
                {
                    new()
                    {
                        OpportunityId = opportunityId,
                        PurposeId = "purpose.sample",
                        UtilityBonus = 300,
                        ParticipantAgentIds = new List<string> { workWinner.StableId },
                    },
                },
                AidOpportunities = new List<AidOpportunity>
                {
                    new()
                    {
                        OpportunityId = opportunityId,
                        PurposeId = "purpose.sample",
                        UtilityBonus = 300,
                        EligibleAgentIds = new List<string> { aidLoser.StableId },
                    },
                },
            };

            SimulationStepResult result = new SocietySimulation().Advance(state, input);

            AgentDecision winner = result.Decisions.Single(value =>
                value.ActorId == workWinner.StableId);
            AgentDecision loser = result.Decisions.Single(value =>
                value.ActorId == aidLoser.StableId);

            Assert.AreEqual($"work:{opportunityId}", winner.CandidateId);
            Assert.AreEqual(SocietyActionKind.Work, winner.Action);
            Assert.AreEqual(opportunityId, winner.OpportunityId);
            Assert.AreEqual(0, winner.SelectedCandidateRank);
            AssertReservation(
                winner,
                opportunityId,
                awarded: true,
                holderActorId: workWinner.StableId);

            Assert.AreEqual($"seek-aid:{opportunityId}",
                loser.CandidateEvaluations[0].CandidateId,
                "The rejected first choice must remain in the frozen ranked plan.");
            Assert.AreEqual("work", loser.CandidateId);
            Assert.AreEqual(SocietyActionKind.Work, loser.Action);
            Assert.IsNull(loser.OpportunityId);
            Assert.Greater(loser.SelectedCandidateRank, 0);
            AssertReservation(
                loser,
                opportunityId,
                awarded: false,
                holderActorId: workWinner.StableId);

            Assert.IsTrue(result.Events.Any(value =>
                value.ActorId == workWinner.StableId &&
                value.Kind == SocietyEventKind.WorkPerformed &&
                value.OpportunityId == opportunityId));
            Assert.IsTrue(result.Events.Any(value =>
                value.ActorId == aidLoser.StableId &&
                value.Kind == SocietyEventKind.WorkPerformed &&
                value.OpportunityId == null));
            Assert.IsFalse(result.Events.Any(value =>
                value.ActorId == aidLoser.StableId &&
                value.Kind == SocietyEventKind.NoActionObserved),
                "Capacity rejection must execute the next ranked candidate, not emit forced idle.");
        }

        [Test]
        public void DecisionSnapshots_DeepDetachRegimeInputAndNestedOpportunities()
        {
            AgentState actor = CreateWorker("agent.snapshot", 0, canSeekAid: true);
            actor.Standing.CanAppeal = true;
            actor.Standing.SetRecognised("adverse-decision", true);

            var regime = new InstitutionalRegimeState
            {
                WorkReward = 11,
                AidEffectiveness = 22,
                DisclosureProtection = 33,
                RetaliationRisk = 44,
                AppealAccessibility = 55,
                DecisionVariationAmplitude = 0,
            };
            var work = new WorkOpportunity
            {
                OpportunityId = "work.original",
                PurposeId = "purpose.work",
                SourceCauseId = "cause.work",
                RequiredEmployerId = "employer.fixture",
                RequiredOfficialStatusId = "status.worker",
                EarliestCycle = 7,
                UtilityBonus = 8,
                ParticipantAgentIds = new List<string> { actor.StableId },
            };
            var aid = new AidOpportunity
            {
                OpportunityId = "aid.original",
                PurposeId = "purpose.aid",
                SourceCauseId = "cause.aid",
                RequiredOfficialStatusId = "status.aid",
                UtilityBonus = 9,
                EligibleAgentIds = new List<string> { actor.StableId },
            };
            var appeal = new AppealOpportunity
            {
                OpportunityId = "appeal.original",
                DocketId = "docket.original",
                CaseId = "case.original",
                ChallengedRulingId = "ruling.original",
                SourceCauseId = "cause.appeal",
                HearingCycle = 10,
                UtilityBonus = 12,
                PartyAgentIds = new List<string> { actor.StableId },
            };
            var input = new SimulationInput
            {
                IncidentId = "incident.original",
                WorkAvailable = true,
                AidAvailable = true,
                DisclosureRequested = false,
                AppealWindowOpen = true,
                OpenDocketId = "docket.original",
                AidRequiredOfficialStatusId = "status.aid",
                AppealEligibleAgentIds = new List<string> { actor.StableId },
                WorkOpportunities = new List<WorkOpportunity> { work },
                AidOpportunities = new List<AidOpportunity> { aid },
                AppealOpportunities = new List<AppealOpportunity> { appeal },
                RestrictAidToOpportunities = true,
                RestrictAppealToOpportunities = true,
                VisibleAgentIds = new List<string> { "agent.visible" },
            };
            var context = new AgentDecisionContext
            {
                MasterSeed = 1234,
                Tick = 5,
                Actor = AgentPerception.Capture(actor),
                PerceivedAgentIds = Array.Empty<string>(),
                Regime = regime,
                Input = input,
            };

            AgentDecision decision = new AgentDecisionEngine().Decide(context);
            string rankedPlan = RankedPlanSignature(decision);

            Assert.AreNotSame(regime, decision.RegimeSnapshot);
            Assert.AreNotSame(input, decision.InputSnapshot);
            Assert.AreNotSame(work, decision.InputSnapshot.WorkOpportunities[0]);
            Assert.AreNotSame(aid, decision.InputSnapshot.AidOpportunities[0]);
            Assert.AreNotSame(appeal, decision.InputSnapshot.AppealOpportunities[0]);
            Assert.AreNotSame(work.ParticipantAgentIds,
                decision.InputSnapshot.WorkOpportunities[0].ParticipantAgentIds);
            Assert.AreNotSame(aid.EligibleAgentIds,
                decision.InputSnapshot.AidOpportunities[0].EligibleAgentIds);
            Assert.AreNotSame(appeal.PartyAgentIds,
                decision.InputSnapshot.AppealOpportunities[0].PartyAgentIds);

            regime.WorkReward = 99;
            regime.AidEffectiveness = 98;
            input.IncidentId = "incident.mutated";
            input.WorkAvailable = false;
            input.AppealEligibleAgentIds[0] = "agent.mutated";
            input.VisibleAgentIds[0] = "agent.hidden";
            work.OpportunityId = "work.mutated";
            work.UtilityBonus = 1000;
            work.ParticipantAgentIds[0] = "agent.mutated";
            aid.OpportunityId = "aid.mutated";
            aid.EligibleAgentIds[0] = "agent.mutated";
            appeal.OpportunityId = "appeal.mutated";
            appeal.DocketId = "docket.mutated";
            appeal.PartyAgentIds[0] = "agent.mutated";
            input.WorkOpportunities.Clear();
            input.AidOpportunities.Clear();
            input.AppealOpportunities.Clear();

            Assert.AreEqual(11, decision.RegimeSnapshot.WorkReward);
            Assert.AreEqual(22, decision.RegimeSnapshot.AidEffectiveness);
            Assert.AreEqual("incident.original", decision.InputSnapshot.IncidentId);
            Assert.IsTrue(decision.InputSnapshot.WorkAvailable);
            Assert.AreEqual(actor.StableId, decision.InputSnapshot.AppealEligibleAgentIds.Single());
            Assert.AreEqual("agent.visible", decision.InputSnapshot.VisibleAgentIds.Single());

            Assert.AreEqual("work.original",
                decision.InputSnapshot.WorkOpportunities.Single().OpportunityId);
            Assert.AreEqual(8,
                decision.InputSnapshot.WorkOpportunities.Single().UtilityBonus);
            Assert.AreEqual(actor.StableId,
                decision.InputSnapshot.WorkOpportunities.Single().ParticipantAgentIds.Single());
            Assert.AreEqual("aid.original",
                decision.InputSnapshot.AidOpportunities.Single().OpportunityId);
            Assert.AreEqual(actor.StableId,
                decision.InputSnapshot.AidOpportunities.Single().EligibleAgentIds.Single());
            Assert.AreEqual("appeal.original",
                decision.InputSnapshot.AppealOpportunities.Single().OpportunityId);
            Assert.AreEqual("docket.original",
                decision.InputSnapshot.AppealOpportunities.Single().DocketId);
            Assert.AreEqual(actor.StableId,
                decision.InputSnapshot.AppealOpportunities.Single().PartyAgentIds.Single());

            Assert.AreEqual(rankedPlan, RankedPlanSignature(decision),
                "Later source mutations must not rewrite the frozen ranked plan.");
            Assert.Throws<NotSupportedException>(() =>
                ((IList<RankedCandidatePlanEntry>)decision.RankedCandidatePlan).Add(null),
                "Resolution consumers must not be able to append or reorder retained candidates.");
            CollectionAssert.AreEqual(
                decision.RankedCandidatePlan
                    .OrderByDescending(value => value.Score)
                    .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
                    .Select(value => value.CandidateId),
                decision.RankedCandidatePlan.Select(value => value.CandidateId),
                "The retained plan must already be in deterministic rank order.");
        }

        [Test]
        public void ActionProjection_DeepDetachesAssessorSnapshotsFromRetainedStep()
        {
            AgentState actor = CreateWorker("agent.projected-snapshot", 0, false);
            var state = new SocietyState
            {
                MasterSeed = 77,
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 41,
                    DecisionVariationAmplitude = 0,
                },
                Agents = new List<AgentState> { actor },
            };
            var input = new SimulationInput
            {
                IncidentId = "incident.projected-snapshot",
                WorkAvailable = true,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
                WorkOpportunities = new List<WorkOpportunity>
                {
                    new()
                    {
                        OpportunityId = "opportunity.original",
                        PurposeId = "purpose.original",
                        ParticipantAgentIds = new List<string> { actor.StableId },
                    },
                },
            };
            SimulationStepResult step = new SocietySimulation().Advance(state, input);
            var run = new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport(),
                FinalSocietyState = state,
            };

            InstitutionalActionProjector.Capture(run, step);

            AgentActionTrace trace = run.AssessorActionTraces.Single(value =>
                value.ActorId == actor.StableId);
            AgentDecision retained = step.Decisions.Single(value =>
                value.ActorId == actor.StableId);
            Assert.AreNotSame(retained.PerceptionSnapshot, trace.PerceptionSnapshot);
            Assert.AreNotSame(retained.RegimeSnapshot, trace.RegimeSnapshot);
            Assert.AreNotSame(retained.InputSnapshot, trace.InputSnapshot);
            Assert.AreNotSame(
                retained.InputSnapshot.WorkOpportunities[0],
                trace.InputSnapshot.WorkOpportunities[0]);

            retained.PerceptionSnapshot.Needs[0].Pressure = 99;
            retained.RegimeSnapshot.WorkReward = 999;
            retained.InputSnapshot.IncidentId = "incident.mutated";
            retained.InputSnapshot.WorkOpportunities[0].OpportunityId =
                "opportunity.mutated";
            retained.InputSnapshot.WorkOpportunities[0].ParticipantAgentIds[0] =
                "agent.mutated";

            Assert.AreNotEqual(99, trace.PerceptionSnapshot.Needs[0].Pressure);
            Assert.AreEqual(41, trace.RegimeSnapshot.WorkReward);
            Assert.AreEqual("incident.projected-snapshot", trace.InputSnapshot.IncidentId);
            Assert.AreEqual(
                "opportunity.original",
                trace.InputSnapshot.WorkOpportunities[0].OpportunityId);
            Assert.AreEqual(
                actor.StableId,
                trace.InputSnapshot.WorkOpportunities[0].ParticipantAgentIds[0]);
        }

        [Test]
        public void ActionProjection_ReplayingStepFailsWithoutPartialMutation()
        {
            AgentState actor = CreateWorker("agent.projected-once", 0, false);
            var state = new SocietyState
            {
                MasterSeed = 78,
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 41,
                    DecisionVariationAmplitude = 0,
                },
                Agents = new List<AgentState> { actor },
            };
            var input = new SimulationInput
            {
                IncidentId = "incident.projected-once",
                WorkAvailable = true,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
            };
            SimulationStepResult step = new SocietySimulation().Advance(state, input);
            var run = new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport(),
                FinalSocietyState = state,
            };

            InstitutionalActionProjector.Capture(run, step);
            int traceCount = run.AssessorActionTraces.Count;
            int observedCount = run.Report.ObservedAgentActions.Count;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalActionProjector.Capture(run, step));

            StringAssert.Contains("already projected", exception.Message);
            Assert.AreEqual(traceCount, run.AssessorActionTraces.Count,
                "A rejected replay must not append assessor traces.");
            Assert.AreEqual(observedCount, run.Report.ObservedAgentActions.Count,
                "A rejected replay must not append public observations.");
        }

        [Test]
        public void ActionProjection_InvalidLaterDecisionDoesNotCommitEarlierProjection()
        {
            AgentState first = CreateWorker("agent.projection-first", 0, false);
            AgentState second = CreateWorker("agent.projection-second", 1, false);
            var state = new SocietyState
            {
                MasterSeed = 79,
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 41,
                    DecisionVariationAmplitude = 0,
                },
                Agents = new List<AgentState> { first, second },
            };
            var input = new SimulationInput
            {
                IncidentId = "incident.projection-atomicity",
                WorkAvailable = true,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
            };
            SimulationStepResult step = new SocietySimulation().Advance(state, input);
            step.Decisions.Single(value => value.ActorId == second.StableId)
                .CapacityReservations = null;
            var run = new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport(),
                FinalSocietyState = state,
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstitutionalActionProjector.Capture(run, step));

            StringAssert.Contains("complete frozen decision payloads", exception.Message);
            Assert.IsEmpty(run.AssessorActionTraces,
                "Projection must commit only after every incoming decision is staged.");
            Assert.IsEmpty(run.Report.ObservedAgentActions,
                "Public projection must remain empty when any decision is invalid.");
        }

        private static AgentState CreateWorker(string id, int ordinal, bool canSeekAid)
        {
            var actor = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = "portrait.fixture",
                DisplayName = "Fixture Worker",
                SpeciesId = "species.fixture",
                EmployerId = "employer.fixture",
                InstitutionalTrust = 0,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = 50,
                    Candour = 50,
                    Solidarity = 0,
                    Duty = 100,
                    InstitutionalReliance = 0,
                },
                Standing = new InstitutionalStandingState
                {
                    CanWork = true,
                    CanSeekAid = canSeekAid,
                    CanAppeal = false,
                    CanGiveEvidence = false,
                },
            };

            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
            {
                actor.Needs.Add(new NeedState
                {
                    Kind = kind,
                    Pressure = kind == NeedKind.Subsistence ? 60 : 0,
                });
            }

            actor.Commitments.Add(new CommitmentState
            {
                CommitmentId = $"commitment:{id}:employment",
                Kind = "employment",
                TargetId = actor.EmployerId,
                Strength = 100,
            });
            return actor;
        }

        private static void AssertReservation(
            AgentDecision decision,
            string opportunityId,
            bool awarded,
            string holderActorId)
        {
            CapacityReservationTrace reservation = decision.CapacityReservations.Single(value =>
                value.OpportunityId == opportunityId);
            Assert.AreEqual(awarded, reservation.Awarded);
            Assert.AreEqual(holderActorId, reservation.HolderActorId);
        }

        private static string RankedPlanSignature(AgentDecision decision)
        {
            return string.Join("|", decision.RankedCandidatePlan.Select(value =>
                $"{value.CandidateId}:{value.Action}:{value.TargetId}:" +
                $"{value.OpportunityId}:{value.SubjectBeliefId}:{value.IntendedNeed}:{value.Score}"));
        }
    }
}
