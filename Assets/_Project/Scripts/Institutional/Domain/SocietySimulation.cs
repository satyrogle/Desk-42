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

            // Capacity pass: every plan is already frozen. Stable actor order reserves
            // contested opportunities and rejected plans fall through to their next
            // ranked candidate without observing any applied state mutation.
            ResolveCapacityReservations(result.Decisions);

            // Application pass: phase then actor ordering is explicit and stable.
            result.Decisions.Sort(CompareForApplication);
            for (int i = 0; i < result.Decisions.Count; i++)
                ApplyDecision(state, input, result.Decisions[i], result.Events);

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

        private static void ResolveCapacityReservations(List<AgentDecision> decisions)
        {
            var reservationOrder = new List<AgentDecision>(decisions);
            reservationOrder.Sort(CompareForCapacityReservation);
            var opportunityHolders = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int decisionIndex = 0; decisionIndex < reservationOrder.Count; decisionIndex++)
            {
                AgentDecision decision = reservationOrder[decisionIndex];
                bool selected = false;
                for (int rank = 0; rank < decision.RankedCandidatePlan.Count; rank++)
                {
                    RankedCandidatePlanEntry candidate = decision.RankedCandidatePlan[rank];
                    if (!string.IsNullOrEmpty(candidate.OpportunityId))
                    {
                        if (opportunityHolders.TryGetValue(
                                candidate.OpportunityId,
                                out string holderActorId))
                        {
                            decision.CapacityReservations.Add(new CapacityReservationTrace
                            {
                                CandidateRank = rank,
                                CandidateId = candidate.CandidateId,
                                OpportunityId = candidate.OpportunityId,
                                Awarded = false,
                                HolderActorId = holderActorId,
                            });
                            continue;
                        }

                        opportunityHolders.Add(candidate.OpportunityId, decision.ActorId);
                        decision.CapacityReservations.Add(new CapacityReservationTrace
                        {
                            CandidateRank = rank,
                            CandidateId = candidate.CandidateId,
                            OpportunityId = candidate.OpportunityId,
                            Awarded = true,
                            HolderActorId = decision.ActorId,
                        });
                    }

                    SelectCandidate(
                        decision,
                        candidate,
                        decision.CandidateEvaluations[rank],
                        rank);
                    selected = true;
                    break;
                }

                if (!selected)
                {
                    throw new InvalidOperationException(
                        $"Decision {decision.DecisionId} has no capacity-valid candidate. " +
                        "Every ranked plan must retain its unconditional idle fallback.");
                }
            }
        }

        private static int CompareForCapacityReservation(AgentDecision left, AgentDecision right)
        {
            int ordinal = left.ApplicationOrdinal.CompareTo(right.ApplicationOrdinal);
            if (ordinal != 0) return ordinal;
            int actor = string.CompareOrdinal(left.ActorId, right.ActorId);
            return actor != 0
                ? actor
                : string.CompareOrdinal(left.DecisionId, right.DecisionId);
        }

        private static void SelectCandidate(
            AgentDecision decision,
            RankedCandidatePlanEntry candidate,
            CandidateEvaluation evaluation,
            int rank)
        {
            decision.SelectedCandidateRank = rank;
            decision.CandidateId = candidate.CandidateId;
            decision.Action = candidate.Action;
            decision.TargetId = candidate.TargetId;
            decision.OpportunityId = candidate.OpportunityId;
            decision.SubjectBeliefId = candidate.SubjectBeliefId;
            decision.IntendedNeed = candidate.IntendedNeed;
            decision.Score = candidate.Score;
            decision.Reasons = CloneReasons(evaluation.Reasons);
        }

        private static List<DecisionReason> CloneReasons(IReadOnlyList<DecisionReason> source)
        {
            var clone = new List<DecisionReason>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                DecisionReason reason = source[i];
                clone.Add(new DecisionReason
                {
                    ReasonId = reason.ReasonId,
                    SourceId = reason.SourceId,
                    ScoreDelta = reason.ScoreDelta,
                });
            }

            return clone;
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
                case SocietyActionKind.Lie:
                    return 0;
                case SocietyActionKind.Appeal:
                case SocietyActionKind.SeekAid:
                case SocietyActionKind.Retaliate:
                    return 1;
                case SocietyActionKind.Help:
                case SocietyActionKind.Organise:
                    return 2;
                case SocietyActionKind.Work:
                case SocietyActionKind.Steal:
                    return 3;
                default:
                    return 4;
            }
        }

        private static void ApplyDecision(
            SocietyState state,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            AgentState actor = state.GetAgent(decision.ActorId);
            if (actor == null)
                throw new InvalidOperationException($"Decision actor no longer exists: {decision.ActorId}");

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
                case SocietyActionKind.Lie:
                    ApplyLie(state, actor, input, decision, events);
                    break;
                case SocietyActionKind.Steal:
                    ApplySteal(actor, input, decision, events);
                    break;
                case SocietyActionKind.Retaliate:
                    ApplyRetaliate(state, actor, input, decision, events);
                    break;
                case SocietyActionKind.Organise:
                    ApplyOrganise(actor, input, decision, events);
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
            // This pulse records the autonomous filing attempt only. Whether it becomes
            // a pending institutional appeal is decided by the authority pipeline; the
            // generic agent layer must not mutate official status speculatively.
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
            events.Add(societyEvent);
        }

        private static void ApplyLie(
            SocietyState state,
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            LieOpportunity opportunity = FindByOpportunityId(
                input.LieOpportunities,
                decision.OpportunityId,
                value => value.OpportunityId);
            BeliefState concealedBelief = actor.GetBelief(decision.SubjectBeliefId);
            if (opportunity == null || concealedBelief == null) return;

            SocietyEvent societyEvent = NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.AssertionMade,
                actor.StableId,
                opportunity.ContextId,
                opportunity.Visibility == EvidenceVisibility.OfficialRecord
                    ? $"record:assertion:{decision.Tick}:{actor.StableId}:{opportunity.OpportunityId}"
                    : null,
                opportunity.Visibility,
                decision.DecisionId);
            societyEvent.OpportunityId = opportunity.OpportunityId;
            societyEvent.EvidencePropositionId = opportunity.AssertionPropositionId;
            societyEvent.EvidenceSubjectId = opportunity.AssertionSubjectId;
            societyEvent.EvidenceObjectId = opportunity.AssertionObjectId;
            societyEvent.EvidenceSourceId = actor.StableId;
            societyEvent.EvidenceBeliefId = concealedBelief.BeliefId;
            societyEvent.ActionContextId = opportunity.ContextId;
            societyEvent.PotentialRecordSourceIds = StableSingleton(
                opportunity.PotentialRecordSourceId);

            if (opportunity.AudienceAgentIds != null)
            {
                for (int i = 0; i < opportunity.AudienceAgentIds.Count; i++)
                {
                    AgentState listener = state.GetAgent(opportunity.AudienceAgentIds[i]);
                    if (listener == null || string.Equals(
                            listener.StableId,
                            actor.StableId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int evaluatedConfidence = EvaluateAssertionConfidence(
                        listener,
                        actor.StableId,
                        opportunity,
                        concealedBelief);
                    if (evaluatedConfidence <= 0) continue;

                    string beliefId =
                        $"belief:assertion:{decision.Tick}:{listener.StableId}:" +
                        $"{actor.StableId}:{opportunity.OpportunityId}";
                    BeliefState received = listener.GetBelief(beliefId);
                    int before = received?.Confidence ?? 0;
                    if (received == null)
                    {
                        received = new BeliefState
                        {
                            BeliefId = beliefId,
                            PropositionId = opportunity.AssertionPropositionId,
                            SubjectId = opportunity.AssertionSubjectId,
                            ObjectId = opportunity.AssertionObjectId,
                            SourceId = actor.StableId,
                            Confidence = evaluatedConfidence,
                            Secrecy = opportunity.Visibility == EvidenceVisibility.Private ? 70 : 20,
                            EmotionalWeight = concealedBelief.EmotionalWeight / 2,
                            AcquiredTick = decision.Tick,
                            EnteredOfficialRecord =
                                opportunity.Visibility == EvidenceVisibility.OfficialRecord,
                        };
                        listener.Beliefs.Add(received);
                    }
                    else
                    {
                        received.Confidence = Math.Max(received.Confidence, evaluatedConfidence);
                    }

                    societyEvent.DirectWitnessAgentIds.Add(listener.StableId);
                    societyEvent.Deltas.Add(new StateDelta
                    {
                        EntityId = listener.StableId,
                        FieldId = $"belief:{beliefId}:source-evaluated-confidence",
                        Before = before,
                        After = received.Confidence,
                    });
                }
            }

            societyEvent.EvidenceReliability = societyEvent.Deltas.Count == 0
                ? 0
                : societyEvent.Deltas[0].After;
            events.Add(societyEvent);
        }

        private static int EvaluateAssertionConfidence(
            AgentState listener,
            string sourceAgentId,
            LieOpportunity opportunity,
            BeliefState sourceBelief)
        {
            RelationshipState source = listener.GetRelationship(sourceAgentId);
            int trust = source?.Trust ?? 0;
            int authority = source?.Authority ?? 0;
            int corroboration = 0;
            int opposition = 0;
            for (int i = 0; i < listener.Beliefs.Count; i++)
            {
                BeliefState existing = listener.Beliefs[i];
                if (!string.Equals(
                        existing.SubjectId,
                        opportunity.AssertionSubjectId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        existing.ObjectId,
                        opportunity.AssertionObjectId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(
                        existing.PropositionId,
                        opportunity.AssertionPropositionId,
                        StringComparison.Ordinal))
                {
                    corroboration = Math.Max(corroboration, existing.Confidence);
                }
                else
                {
                    opposition = Math.Max(opposition, existing.Confidence);
                }
            }

            return InstitutionalMath.Clamp(
                10 + trust / 2 + authority / 4 + sourceBelief.Confidence / 8 +
                corroboration / 5 - opposition / 3,
                0,
                100);
        }

        private static void ApplySteal(
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            StealOpportunity opportunity = FindByOpportunityId(
                input.StealOpportunities,
                decision.OpportunityId,
                value => value.OpportunityId);
            if (opportunity == null) return;
            SocietyEvent societyEvent = NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.PossessionTransferRequested,
                actor.StableId,
                opportunity.ExpectedPhysicalHolderId,
                null,
                opportunity.Visibility,
                decision.DecisionId);
            societyEvent.OpportunityId = opportunity.OpportunityId;
            societyEvent.ActionResourceId = opportunity.ResourceId;
            societyEvent.ActionContextId = opportunity.NewLocationContextId;
            societyEvent.AffectedStateRecordId = opportunity.AccessGrantId;
            societyEvent.ActionSecrecy = opportunity.Secrecy;
            societyEvent.EnablingRulingId = opportunity.EnablingRulingId;
            societyEvent.ParentCaseId = opportunity.ParentCaseId;
            societyEvent.DirectWitnessAgentIds = CloneStrings(
                opportunity.DirectWitnessAgentIds);
            societyEvent.PotentialRecordSourceIds = CloneStrings(
                opportunity.PotentialRecordSourceIds);
            events.Add(societyEvent);
        }

        private static void ApplyRetaliate(
            SocietyState state,
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            RetaliationOpportunity opportunity = FindByOpportunityId(
                input.RetaliationOpportunities,
                decision.OpportunityId,
                value => value.OpportunityId);
            if (opportunity == null) return;
            SocietyEvent societyEvent = NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.RetaliatoryAuthorityExercised,
                actor.StableId,
                opportunity.TargetAgentId,
                opportunity.Visibility == EvidenceVisibility.OfficialRecord
                    ? $"record:authority:{decision.Tick}:{actor.StableId}:" +
                      opportunity.OpportunityId
                    : null,
                opportunity.Visibility,
                decision.DecisionId);
            societyEvent.OpportunityId = opportunity.OpportunityId;
            societyEvent.EvidenceBeliefId = opportunity.PerceivedPriorActionBeliefId;
            societyEvent.AuthorityGrantId = opportunity.AuthorityGrantId;
            societyEvent.AffectedStateRecordId = opportunity.AffectedAccessGrantId;
            societyEvent.ActionContextId = opportunity.AdverseActionKindId;
            societyEvent.ActionSecrecy = opportunity.Secrecy;
            societyEvent.DirectWitnessAgentIds = CloneStrings(
                opportunity.DirectWitnessAgentIds);
            societyEvent.PotentialRecordSourceIds = CloneStrings(
                opportunity.PotentialRecordSourceIds);

            AgentState target = state.GetAgent(opportunity.TargetAgentId);
            if (target != null && opportunity.Visibility != EvidenceVisibility.Private)
            {
                ChangeRelationshipField(
                    target, actor.StableId, "trust", -10, societyEvent.Deltas);
                ChangeRelationshipField(
                    target, actor.StableId, "fear", 12, societyEvent.Deltas);
            }
            events.Add(societyEvent);
        }

        private static void ApplyOrganise(
            AgentState actor,
            SimulationInput input,
            AgentDecision decision,
            List<SocietyEvent> events)
        {
            OrganiseOpportunity opportunity = FindByOpportunityId(
                input.OrganiseOpportunities,
                decision.OpportunityId,
                value => value.OpportunityId);
            if (opportunity == null) return;
            SocietyEvent societyEvent = NewEvent(
                decision.Tick,
                input.IncidentId,
                SocietyEventKind.OrganisationProposed,
                actor.StableId,
                opportunity.IssueId,
                null,
                opportunity.Visibility,
                decision.DecisionId);
            societyEvent.OpportunityId = opportunity.OpportunityId;
            societyEvent.ActionContextId = opportunity.CommunicationContextId;
            societyEvent.CollectiveCommitmentId = opportunity.CollectiveCommitmentId;
            societyEvent.CollectiveIssueId = opportunity.IssueId;
            societyEvent.CollectiveIntentionId = opportunity.IntentionId;
            societyEvent.RequiredParticipantCount = opportunity.RequiredParticipantCount;
            societyEvent.ActionSecrecy = opportunity.Secrecy;
            societyEvent.PerceivedCauseEventIds = CloneStrings(
                opportunity.PerceivedCauseEventIds);
            societyEvent.DirectWitnessAgentIds = CloneStrings(
                opportunity.DirectWitnessAgentIds);
            societyEvent.PotentialRecordSourceIds = CloneStrings(
                opportunity.PotentialRecordSourceIds);
            events.Add(societyEvent);
        }

        private static T FindByOpportunityId<T>(
            IReadOnlyList<T> values,
            string expectedId,
            Func<T, string> id)
            where T : class
        {
            if (values == null || string.IsNullOrEmpty(expectedId)) return null;
            for (int i = 0; i < values.Count; i++)
            {
                T value = values[i];
                if (value != null && string.Equals(
                        id(value), expectedId, StringComparison.Ordinal))
                {
                    return value;
                }
            }

            return null;
        }

        private static List<string> CloneStrings(IReadOnlyList<string> source)
        {
            var result = new List<string>(source?.Count ?? 0);
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            return result;
        }

        private static List<string> StableSingleton(string value)
            => string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : new List<string> { value };

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
            ChangeRelationshipField(actor, targetId, fieldId, delta, deltas);
        }

        private static void ChangeRelationshipField(
            AgentState actor,
            string targetId,
            string fieldId,
            int delta,
            List<StateDelta> deltas)
        {
            RelationshipState relationship = actor.GetRelationship(targetId);
            if (relationship == null) return;

            int before;
            int after;
            switch (fieldId)
            {
                case "trust":
                    before = relationship.Trust;
                    relationship.Trust = InstitutionalMath.Clamp(before + delta, 0, 100);
                    after = relationship.Trust;
                    break;
                case "fear":
                    before = relationship.Fear;
                    relationship.Fear = InstitutionalMath.Clamp(before + delta, 0, 100);
                    after = relationship.Fear;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported relationship mutation field {fieldId}.");
            }

            deltas.Add(new StateDelta
            {
                EntityId = actor.StableId,
                FieldId = $"relationship:{targetId}:{fieldId}",
                Before = before,
                After = after,
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
