using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Converts one frozen simulation pulse into assessor action traces and the
    /// deliberately narrower public activity surface. It owns no scenario routing.
    /// </summary>
    internal static class InstitutionalActionProjector
    {
        internal static void Capture(
            InstitutionalConsequenceRun run,
            SimulationStepResult step)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.Report == null)
                throw new InvalidOperationException("Action projection requires a report.");
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (step.Decisions == null || step.Events == null ||
                run.AssessorActionTraces == null ||
                run.Report.ObservedAgentActions == null)
            {
                throw new InvalidOperationException(
                    "Action projection requires initialized decision, event and output collections.");
            }
            PreflightExactOnce(run, step);

            var stagedTraces = new List<AgentActionTrace>(step.Decisions.Count);
            var stagedObservedActions = new List<ObservedAgentAction>(
                step.Decisions.Count);

            for (int decisionIndex = 0; decisionIndex < step.Decisions.Count; decisionIndex++)
            {
                AgentDecision decision = step.Decisions[decisionIndex];
                var trace = new AgentActionTrace
                {
                    Cycle = step.Tick,
                    DecisionId = decision.DecisionId,
                    CandidateId = decision.CandidateId,
                    ActorId = decision.ActorId,
                    Action = decision.Action,
                    TargetId = decision.TargetId,
                    OpportunityId = decision.OpportunityId,
                    SubjectBeliefId = decision.SubjectBeliefId,
                    UtilityScore = decision.Score,
                    SelectedCandidateRank = decision.SelectedCandidateRank,
                    PerceptionSnapshot = AgentPerception.Copy(decision.PerceptionSnapshot),
                    RegimeSnapshot = AgentDecisionEngine.CaptureRegimeSnapshot(
                        decision.RegimeSnapshot),
                    InputSnapshot = AgentDecisionEngine.CaptureInputSnapshot(
                        decision.InputSnapshot),
                };
                CopyReasons(decision.Reasons, trace.Reasons);
                for (int reservationIndex = 0;
                     reservationIndex < decision.CapacityReservations.Count;
                     reservationIndex++)
                {
                    CapacityReservationTrace source =
                        decision.CapacityReservations[reservationIndex];
                    trace.CapacityReservations.Add(new CapacityReservationTrace
                    {
                        CandidateRank = source.CandidateRank,
                        CandidateId = source.CandidateId,
                        OpportunityId = source.OpportunityId,
                        Awarded = source.Awarded,
                        HolderActorId = source.HolderActorId,
                    });
                }
                for (int candidateIndex = 0;
                     candidateIndex < decision.CandidateEvaluations.Count;
                     candidateIndex++)
                {
                    CandidateEvaluation source = decision.CandidateEvaluations[candidateIndex];
                    var evaluation = new CandidateEvaluation
                    {
                        CandidateId = source.CandidateId,
                        Action = source.Action,
                        TargetId = source.TargetId,
                        OpportunityId = source.OpportunityId,
                        SubjectBeliefId = source.SubjectBeliefId,
                        IntendedNeed = source.IntendedNeed,
                        Score = source.Score,
                    };
                    CopyReasons(source.Reasons, evaluation.Reasons);
                    trace.CandidateEvaluations.Add(evaluation);
                }

                SocietyEvent firstCausedEvent = null;
                for (int eventIndex = 0; eventIndex < step.Events.Count; eventIndex++)
                {
                    SocietyEvent candidate = step.Events[eventIndex];
                    if (!string.Equals(candidate.CauseDecisionId, decision.DecisionId,
                        StringComparison.Ordinal)) continue;
                    trace.ResultEventIds.Add(candidate.EventId);
                    if (firstCausedEvent == null) firstCausedEvent = candidate;
                }

                stagedTraces.Add(trace);
                if (firstCausedEvent == null) continue;
                stagedObservedActions.Add(new ObservedAgentAction
                {
                    Cycle = step.Tick,
                    ActionEventId = firstCausedEvent.EventId,
                    ActorId = firstCausedEvent.ActorId,
                    Activity = ActivityFor(firstCausedEvent.Kind),
                    TargetId = firstCausedEvent.Kind == SocietyEventKind.ResponseWithheld
                        ? null
                        : firstCausedEvent.TargetId,
                });
            }

            run.AssessorActionTraces.AddRange(stagedTraces);
            run.Report.ObservedAgentActions.AddRange(stagedObservedActions);
        }

        internal static ObservedActivityKind ActivityFor(SocietyEventKind kind)
        {
            switch (kind)
            {
                case SocietyEventKind.WorkPerformed:
                    return ObservedActivityKind.WorkPerformed;
                case SocietyEventKind.AidRequested:
                    return ObservedActivityKind.AidRequested;
                case SocietyEventKind.AssistanceGiven:
                    return ObservedActivityKind.AssistanceGiven;
                case SocietyEventKind.EvidenceDisclosed:
                    return ObservedActivityKind.EvidenceSubmitted;
                case SocietyEventKind.AppealFiled:
                    return ObservedActivityKind.AppealFiled;
                default:
                    return ObservedActivityKind.NoVisibleAction;
            }
        }

        private static void PreflightExactOnce(
            InstitutionalConsequenceRun run,
            SimulationStepResult step)
        {
            var incomingDecisionIds = new HashSet<string>(StringComparer.Ordinal);
            var incomingObservedEventIds = new HashSet<string>(StringComparer.Ordinal);
            for (int eventIndex = 0; eventIndex < step.Events.Count; eventIndex++)
            {
                if (step.Events[eventIndex] == null)
                    throw new InvalidOperationException(
                        "Action projection cannot capture a null society event.");
            }
            for (int decisionIndex = 0;
                 decisionIndex < step.Decisions.Count;
                 decisionIndex++)
            {
                AgentDecision decision = step.Decisions[decisionIndex] ??
                    throw new InvalidOperationException(
                        "Action projection cannot capture a null decision.");
                if (decision.Reasons == null ||
                    decision.CandidateEvaluations == null ||
                    decision.CapacityReservations == null ||
                    decision.PerceptionSnapshot == null ||
                    decision.RegimeSnapshot == null ||
                    decision.InputSnapshot == null)
                {
                    throw new InvalidOperationException(
                        "Action projection requires complete frozen decision payloads.");
                }
                if (string.IsNullOrWhiteSpace(decision.DecisionId) ||
                    !incomingDecisionIds.Add(decision.DecisionId))
                {
                    throw new InvalidOperationException(
                        "Action projection decision ids must be non-blank and unique.");
                }
                for (int existingIndex = 0;
                     existingIndex < run.AssessorActionTraces.Count;
                     existingIndex++)
                {
                    if (string.Equals(
                        run.AssessorActionTraces[existingIndex]?.DecisionId,
                        decision.DecisionId,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Decision '{decision.DecisionId}' was already projected.");
                    }
                }

                SocietyEvent first = null;
                for (int eventIndex = 0; eventIndex < step.Events.Count; eventIndex++)
                {
                    SocietyEvent candidate = step.Events[eventIndex];
                    if (first == null && string.Equals(
                        candidate.CauseDecisionId,
                        decision.DecisionId,
                        StringComparison.Ordinal))
                    {
                        first = candidate;
                    }
                }
                if (first == null) continue;
                if (string.IsNullOrWhiteSpace(first.EventId) ||
                    !incomingObservedEventIds.Add(first.EventId))
                {
                    throw new InvalidOperationException(
                        "Projected public action event ids must be non-blank and unique.");
                }
                for (int existingIndex = 0;
                     existingIndex < run.Report.ObservedAgentActions.Count;
                     existingIndex++)
                {
                    if (string.Equals(
                        run.Report.ObservedAgentActions[existingIndex]?.ActionEventId,
                        first.EventId,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Action event '{first.EventId}' was already projected.");
                    }
                }
            }
        }

        private static void CopyReasons(
            System.Collections.Generic.IReadOnlyList<DecisionReason> source,
            System.Collections.Generic.List<DecisionReason> target)
        {
            for (int i = 0; i < source.Count; i++)
            {
                DecisionReason reason = source[i];
                target.Add(new DecisionReason
                {
                    ReasonId = reason.ReasonId,
                    SourceId = reason.SourceId,
                    ScoreDelta = reason.ScoreDelta,
                });
            }
        }
    }
}
