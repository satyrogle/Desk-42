using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Advances the society in deterministic, inspectable steps. Status-bound anomaly
    /// effects are resolved first, every agent then decides against the same frozen
    /// post-status snapshot, and chosen actions are applied in a stable phase order.
    /// </summary>
    public sealed class SocietySimulation
    {
        private readonly AgentDecisionEngine _decisionEngine;

        public SocietySimulation()
            : this(new AgentDecisionEngine())
        {
        }

        internal SocietySimulation(AgentDecisionEngine decisionEngine)
        {
            _decisionEngine = decisionEngine ?? throw new ArgumentNullException(nameof(decisionEngine));
        }

        public SimulationStepResult Advance(SocietyState state, SimulationInput input)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            input ??= new SimulationInput();
            if (string.IsNullOrWhiteSpace(input.IncidentId))
                throw new ArgumentException("Simulation input requires a stable incident id.", nameof(input));
            SocietyStateValidator.Validate(state);

            long tick = state.CurrentTick + 1;
            var result = new SimulationStepResult { Tick = tick };
            var orderedAgents = new List<AgentState>(state.Agents);
            orderedAgents.Sort(CompareAgentsForSimulation);

            ApplyAnomalyStatusRules(orderedAgents, input, tick, result.Events);

            // Decision pass: no state is mutated until every agent has selected an action.
            for (int i = 0; i < orderedAgents.Count; i++)
            {
                AgentState actor = orderedAgents[i];
                AgentDecision decision = _decisionEngine.Decide(new AgentDecisionContext
                {
                    MasterSeed = state.MasterSeed,
                    Tick = tick,
                    Actor = AgentPerception.Capture(actor),
                    PerceivedAgentIds = BuildPerceivedAgentIds(actor, input),
                    Regime = state.Regime,
                    Input = input,
                });
                result.Decisions.Add(decision);
            }

            // Application pass: phase then actor ordering is explicit and stable.
            result.Decisions.Sort(CompareForApplication);
            var claimedOpportunityIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < result.Decisions.Count; i++)
                ApplyDecision(state, input, result.Decisions[i], result.Events,
                    claimedOpportunityIds);

            // Expose traces in actor order, independent from internal application phases.
            result.Decisions.Sort((left, right) => string.CompareOrdinal(left.ActorId, right.ActorId));

            state.CurrentTick = tick;
            AppendEvents(state, result.Events);
            return result;
        }

        private static void ApplyAnomalyStatusRules(
            List<AgentState> orderedAgents,
            SimulationInput input,
            long tick,
            List<SocietyEvent> events)
        {
            for (int agentIndex = 0; agentIndex < orderedAgents.Count; agentIndex++)
            {
                AgentState agent = orderedAgents[agentIndex];
                agent.AnomalyRules.Sort((left, right) => string.CompareOrdinal(left.TraitId, right.TraitId));

                for (int ruleIndex = 0; ruleIndex < agent.AnomalyRules.Count; ruleIndex++)
                {
                    AnomalyStatusRule rule = agent.AnomalyRules[ruleIndex];
                    if (rule.LastAppliedTick >= 0 &&
                        tick - rule.LastAppliedTick < rule.MinimumTicksBetweenActivations)
                    {
                        continue;
                    }

                    bool recognised = agent.Standing.IsRecognised(rule.RequiredOfficialStatusId);
                    int pressureDelta = recognised
                        ? rule.RecognisedPressureDelta
                        : rule.UnrecognisedPressureDelta;

                    var anomalyEvent = NewEvent(
                        tick,
                        input.IncidentId,
                        SocietyEventKind.AnomalyStatusResponse,
                        agent.StableId,
                        agent.StableId,
                        null,
                        EvidenceVisibility.Observable,
                        $"anomaly:{tick}:{agent.StableId}:{rule.TraitId}");
                    anomalyEvent.EventId = $"event:{tick}:{agent.StableId}:anomaly:{rule.TraitId}";

                    bool changed = ChangeNeed(agent, rule.AffectedNeed, pressureDelta, anomalyEvent.Deltas);
                    if (!changed) continue;

                    rule.LastAppliedTick = tick;
                    anomalyEvent.EvidenceId = rule.ObservableEffectId;
                    events.Add(anomalyEvent);
                }
            }
        }

        private static int CompareForApplication(AgentDecision left, AgentDecision right)
        {
            int phase = ActionPhase(left.Action).CompareTo(ActionPhase(right.Action));
            if (phase != 0) return phase;
            int actor = left.ApplicationOrdinal.CompareTo(right.ApplicationOrdinal);
            return actor != 0 ? actor : string.CompareOrdinal(left.CandidateId, right.CandidateId);
        }

        private static int CompareAgentsForSimulation(AgentState left, AgentState right)
        {
            int ordinal = left.SimulationOrdinal.CompareTo(right.SimulationOrdinal);
            return ordinal != 0 ? ordinal : string.CompareOrdinal(left.StableId, right.StableId);
        }

        private static int ActionPhase(SocietyActionKind action)
        {
            switch (action)
            {
                case SocietyActionKind.Disclose:
                case SocietyActionKind.Withhold:
                    return 0;
                case SocietyActionKind.Appeal:
                case SocietyActionKind.SeekAid:
                    return 1;
                case SocietyActionKind.Help:
                    return 2;
                case SocietyActionKind.Work:
                    return 3;
                default:
                    return 4;
            }
        }

        private static void ApplyDecision(
            SocietyState state,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events,
            HashSet<string> claimedOpportunityIds)
        {
            AgentState actor = state.GetAgent(decision.ActorId);
            if (actor == null)
                throw new InvalidOperationException($"Decision actor no longer exists: {decision.ActorId}");

            if (!string.IsNullOrEmpty(decision.OpportunityId) &&
                !claimedOpportunityIds.Add(decision.OpportunityId))
            {
                SocietyEvent unavailable = NewEvent(
                    decision.Tick,
                    input.IncidentId,
                    SocietyEventKind.NoActionObserved,
                    actor.StableId,
                    decision.OpportunityId,
                    null,
                    EvidenceVisibility.Observable,
                    decision.DecisionId);
                unavailable.OpportunityId = decision.OpportunityId;
                events.Add(unavailable);
                return;
            }

            switch (decision.Action)
            {
                case SocietyActionKind.Work:
                    ApplyWork(actor, input, decision, events);
                    break;
                case SocietyActionKind.SeekAid:
                    ApplySeekAid(state, actor, input, decision, events);
                    break;
                case SocietyActionKind.Help:
                    ApplyHelp(state, actor, input, decision, events);
                    break;
                case SocietyActionKind.Disclose:
                    ApplyDisclose(actor, input, decision, events);
                    break;
                case SocietyActionKind.Withhold:
                    ApplyWithhold(actor, input, decision, events);
                    break;
                case SocietyActionKind.Appeal:
                    ApplyAppeal(actor, input, decision, events);
                    break;
                default:
                    events.Add(NewEvent(
                        decision.Tick,
                        input.IncidentId,
                        SocietyEventKind.NoActionObserved,
                        actor.StableId,
                        null,
                        null,
                        EvidenceVisibility.Observable,
                        decision.DecisionId));
                    break;
            }
        }

        private static void ApplyWork(
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            SocietyEvent societyEvent = NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.WorkPerformed,
                actor.StableId,
                decision.OpportunityId ?? actor.EmployerId,
                $"record:work:{decision.Tick}:{actor.StableId}",
                EvidenceVisibility.Observable,
                decision.DecisionId);
            societyEvent.OpportunityId = decision.OpportunityId;
            ChangeNeed(actor, NeedKind.Subsistence, -8, societyEvent.Deltas);
            ChangeNeed(actor, NeedKind.Autonomy, 2, societyEvent.Deltas);
            events.Add(societyEvent);
        }

        private static void ApplySeekAid(
            SocietyState state,
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            SocietyEvent societyEvent = NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.AidRequested,
                actor.StableId,
                decision.OpportunityId ?? "branch-42",
                $"record:aid-request:{decision.Tick}:{actor.StableId}",
                EvidenceVisibility.OfficialRecord,
                decision.DecisionId);
            societyEvent.OpportunityId = decision.OpportunityId;
            int relief = Math.Max(2, state.Regime.AidEffectiveness / 10);
            ChangeNeed(actor, NeedKind.Health, -relief, societyEvent.Deltas);
            ChangeNeed(actor, NeedKind.Safety, -(relief / 2), societyEvent.Deltas);
            ChangeInstitutionalTrust(actor, 2, societyEvent.Deltas);
            events.Add(societyEvent);
        }

        private static void ApplyHelp(
            SocietyState state,
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            AgentState target = state.GetAgent(decision.TargetId);
            if (target == null)
            {
                events.Add(NewEvent(
                    decision.Tick,
                    input.IncidentId,
                    SocietyEventKind.NoActionObserved,
                    actor.StableId,
                    decision.TargetId,
                    null,
                    EvidenceVisibility.Observable,
                    decision.DecisionId));
                return;
            }

            SocietyEvent societyEvent = NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.AssistanceGiven,
                actor.StableId,
                target.StableId,
                $"record:assistance:{decision.Tick}:{actor.StableId}:{target.StableId}",
                EvidenceVisibility.Observable,
                decision.DecisionId);
            NeedKind intendedNeed = decision.IntendedNeed ?? NeedKind.Safety;
            ChangeNeed(target, intendedNeed, -6, societyEvent.Deltas);
            ChangeNeed(actor, NeedKind.Belonging, -3, societyEvent.Deltas);
            ChangeRelationship(actor, target.StableId, "trust", 2, societyEvent.Deltas);
            events.Add(societyEvent);
        }

        private static void ApplyDisclose(
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            BeliefState belief = actor.GetBelief(decision.SubjectBeliefId);
            if (belief == null) return;

            belief.Disclosed = true;
            belief.EnteredOfficialRecord = true;
            SocietyEvent societyEvent = NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.EvidenceDisclosed,
                actor.StableId,
                "branch-42",
                $"evidence:{decision.Tick}:{actor.StableId}:{belief.BeliefId}",
                EvidenceVisibility.OfficialRecord,
                decision.DecisionId);
            societyEvent.Deltas.Add(new StateDelta
            {
                EntityId = actor.StableId,
                FieldId = $"belief:{belief.BeliefId}:official-record",
                Before = 0,
                After = 1,
            });
            societyEvent.EvidencePropositionId = belief.PropositionId;
            societyEvent.EvidenceSubjectId = belief.SubjectId;
            societyEvent.EvidenceObjectId = belief.ObjectId;
            societyEvent.EvidenceSourceId = belief.SourceId;
            societyEvent.EvidenceBeliefId = belief.BeliefId;
            societyEvent.EvidenceReliability = belief.Confidence;
            societyEvent.EvidenceSuppressedByAgentId = belief.LastWithheldTick > 0
                ? actor.StableId
                : null;
            events.Add(societyEvent);
        }

        private static void ApplyWithhold(
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            BeliefState belief = actor.GetBelief(decision.SubjectBeliefId);
            if (belief != null)
            {
                belief.LastWithheldTick = decision.Tick;
                belief.LastWithheldIncidentId = input.IncidentId;
            }

            // The observable record is the non-response, never the undisclosed belief.
            events.Add(NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.ResponseWithheld,
                actor.StableId,
                "branch-42",
                null,
                EvidenceVisibility.Observable,
                decision.DecisionId));
        }

        private static void ApplyAppeal(
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            bool wasPending = actor.Standing.IsRecognised("appeal-pending");
            actor.Standing.SetRecognised("appeal-pending", true);
            SocietyEvent societyEvent = NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.AppealFiled,
                actor.StableId,
                decision.OpportunityId ?? "branch-42",
                $"record:appeal:{decision.Tick}:{actor.StableId}",
                EvidenceVisibility.OfficialRecord,
                decision.DecisionId);
            societyEvent.OpportunityId = decision.OpportunityId;
            societyEvent.Deltas.Add(new StateDelta
            {
                EntityId = actor.StableId,
                FieldId = "official-status:appeal-pending",
                Before = wasPending ? 1 : 0,
                After = 1,
            });
            events.Add(societyEvent);
        }

        private static SocietyEvent NewEvent(
            long tick,
            string incidentId,
            SocietyEventKind kind,
            string actorId,
            string targetId,
            string evidenceId,
            EvidenceVisibility visibility,
            string causeDecisionId)
        {
            return new SocietyEvent
            {
                EventId = $"event:{tick}:{actorId}:{kind}",
                CauseDecisionId = causeDecisionId,
                IncidentId = incidentId,
                Tick = tick,
                Kind = kind,
                ActorId = actorId,
                TargetId = targetId,
                EvidenceId = evidenceId,
                Visibility = visibility,
            };
        }

        private static List<string> BuildPerceivedAgentIds(AgentState actor, SimulationInput input)
        {
            var perceived = new List<string>(actor.Relationships.Count);
            for (int i = 0; i < actor.Relationships.Count; i++)
            {
                string targetId = actor.Relationships[i].TargetAgentId;
                if (input.VisibleAgentIds != null &&
                    !ContainsOrdinal(input.VisibleAgentIds, targetId))
                {
                    continue;
                }
                if (!perceived.Contains(targetId)) perceived.Add(targetId);
            }
            perceived.Sort(StringComparer.Ordinal);
            return perceived;
        }

        private static bool ContainsOrdinal(List<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool ChangeNeed(
            AgentState agent,
            NeedKind kind,
            int delta,
            List<StateDelta> deltas)
        {
            NeedState need = agent.GetNeed(kind);
            if (need == null)
            {
                need = new NeedState { Kind = kind, Pressure = 0 };
                agent.Needs.Add(need);
            }

            int before = need.Pressure;
            need.Pressure = InstitutionalMath.Clamp(before + delta, 0, 100);
            deltas.Add(new StateDelta
            {
                EntityId = agent.StableId,
                FieldId = $"need:{kind}",
                Before = before,
                After = need.Pressure,
            });
            return before != need.Pressure;
        }

        private static void ChangeInstitutionalTrust(
            AgentState agent,
            int delta,
            List<StateDelta> deltas)
        {
            int before = agent.InstitutionalTrust;
            agent.InstitutionalTrust = InstitutionalMath.Clamp(before + delta, -100, 100);
            deltas.Add(new StateDelta
            {
                EntityId = agent.StableId,
                FieldId = "attitude:institutional-trust",
                Before = before,
                After = agent.InstitutionalTrust,
            });
        }

        private static void ChangeRelationship(
            AgentState actor,
            string targetId,
            string fieldId,
            int delta,
            List<StateDelta> deltas)
        {
            RelationshipState relationship = actor.GetRelationship(targetId);
            if (relationship == null) return;

            int before = relationship.Trust;
            relationship.Trust = InstitutionalMath.Clamp(before + delta, 0, 100);
            deltas.Add(new StateDelta
            {
                EntityId = actor.StableId,
                FieldId = $"relationship:{targetId}:{fieldId}",
                Before = before,
                After = relationship.Trust,
            });
        }

        private static void AppendEvents(SocietyState state, List<SocietyEvent> events)
        {
            state.EventLedger ??= new List<SocietyEvent>();
            state.EventLedger.AddRange(events);
            int overflow = state.EventLedger.Count - SocietyState.MaximumEventHistory;
            if (overflow > 0) state.EventLedger.RemoveRange(0, overflow);
        }
    }
}
