using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Generic utility-based decision process shared by every agent. The engine reads
    /// only the actor's needs, commitments, relationships, beliefs, perceived agent ids,
    /// and the rules exposed by the institution. It never reads lived ground truth.
    /// </summary>
    internal sealed class AgentDecisionEngine
    {
        public AgentDecision Decide(AgentDecisionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Actor == null) throw new ArgumentException("Decision context requires an actor.", nameof(context));
            if (context.Regime == null) throw new ArgumentException("Decision context requires a regime.", nameof(context));
            if (context.Input == null) throw new ArgumentException("Decision context requires input.", nameof(context));

            var candidates = new List<ActionCandidate>(16);
            AddIdleCandidate(candidates);
            AddWorkCandidate(context, candidates);
            AddSeekAidCandidate(context, candidates);
            AddHelpCandidates(context, candidates);
            AddEvidenceCandidates(context, candidates);
            AddAppealCandidate(context, candidates);
            AddLieCandidates(context, candidates);
            AddStealCandidates(context, candidates);
            AddRetaliationCandidates(context, candidates);
            AddOrganiseCandidates(context, candidates);

            candidates.Sort(CompareCandidatesByRank);
            ActionCandidate winner = candidates[0];

            var evaluations = new List<CandidateEvaluation>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                ActionCandidate candidate = candidates[i];
                var evaluation = new CandidateEvaluation
                {
                    CandidateId = candidate.CandidateId,
                    Action = candidate.Action,
                    TargetId = candidate.TargetId,
                    OpportunityId = candidate.OpportunityId,
                    SubjectBeliefId = candidate.SubjectBeliefId,
                    IntendedNeed = candidate.IntendedNeed,
                    Score = candidate.Score,
                };
                evaluation.Reasons.AddRange(CloneReasons(candidate.Reasons));
                evaluations.Add(evaluation);
            }

            var decision = new AgentDecision
            {
                Tick = context.Tick,
                ApplicationOrdinal = context.Actor.SimulationOrdinal,
                DecisionId = $"decision:{context.Tick}:{context.Actor.StableId}",
                CandidateId = winner.CandidateId,
                ActorId = context.Actor.StableId,
                Action = winner.Action,
                TargetId = winner.TargetId,
                OpportunityId = winner.OpportunityId,
                SubjectBeliefId = winner.SubjectBeliefId,
                IntendedNeed = winner.IntendedNeed,
                Score = winner.Score,
                Reasons = CloneReasons(winner.Reasons),
                CandidateEvaluations = evaluations,
                SelectedCandidateRank = 0,
                PerceptionSnapshot = context.Actor,
                RegimeSnapshot = CaptureRegimeSnapshot(context.Regime),
                InputSnapshot = CaptureInputSnapshot(context.Input),
            };
            decision.RetainRankedCandidatePlan(evaluations);
            return decision;
        }

        private static int CompareCandidatesByRank(ActionCandidate left, ActionCandidate right)
        {
            int score = right.Score.CompareTo(left.Score);
            return score != 0
                ? score
                : string.CompareOrdinal(left.CandidateId, right.CandidateId);
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

        internal static InstitutionalRegimeState CaptureRegimeSnapshot(
            InstitutionalRegimeState source)
        {
            return new InstitutionalRegimeState
            {
                WorkReward = source.WorkReward,
                AidEffectiveness = source.AidEffectiveness,
                DisclosureProtection = source.DisclosureProtection,
                RetaliationRisk = source.RetaliationRisk,
                AppealAccessibility = source.AppealAccessibility,
                DecisionVariationAmplitude = source.DecisionVariationAmplitude,
            };
        }

        internal static SimulationInput CaptureInputSnapshot(SimulationInput source)
        {
            return new SimulationInput
            {
                IncidentId = source.IncidentId,
                WorkAvailable = source.WorkAvailable,
                AidAvailable = source.AidAvailable,
                DisclosureRequested = source.DisclosureRequested,
                AppealWindowOpen = source.AppealWindowOpen,
                OpenDocketId = source.OpenDocketId,
                AidRequiredOfficialStatusId = source.AidRequiredOfficialStatusId,
                AppealEligibleAgentIds = CloneStrings(source.AppealEligibleAgentIds),
                WorkOpportunities = CloneWorkOpportunities(source.WorkOpportunities),
                AidOpportunities = CloneAidOpportunities(source.AidOpportunities),
                AppealOpportunities = CloneAppealOpportunities(source.AppealOpportunities),
                LieOpportunities = CloneLieOpportunities(source.LieOpportunities),
                StealOpportunities = CloneStealOpportunities(source.StealOpportunities),
                RetaliationOpportunities = CloneRetaliationOpportunities(
                    source.RetaliationOpportunities),
                OrganiseOpportunities = CloneOrganiseOpportunities(source.OrganiseOpportunities),
                RestrictAidToOpportunities = source.RestrictAidToOpportunities,
                RestrictAppealToOpportunities = source.RestrictAppealToOpportunities,
                VisibleAgentIds = CloneStrings(source.VisibleAgentIds),
            };
        }

        private static List<string> CloneStrings(IReadOnlyList<string> source)
        {
            if (source == null) return null;
            var clone = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++) clone.Add(source[i]);
            return clone;
        }

        private static List<WorkOpportunity> CloneWorkOpportunities(
            IReadOnlyList<WorkOpportunity> source)
        {
            if (source == null) return null;
            var clone = new List<WorkOpportunity>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                WorkOpportunity opportunity = source[i];
                clone.Add(opportunity == null
                    ? null
                    : new WorkOpportunity
                    {
                        OpportunityId = opportunity.OpportunityId,
                        PurposeId = opportunity.PurposeId,
                        SourceCauseId = opportunity.SourceCauseId,
                        RequiredEmployerId = opportunity.RequiredEmployerId,
                        RequiredOfficialStatusId = opportunity.RequiredOfficialStatusId,
                        RequiredOfficialStatusRecognised =
                            opportunity.RequiredOfficialStatusRecognised,
                        EarliestCycle = opportunity.EarliestCycle,
                        UtilityBonus = opportunity.UtilityBonus,
                        ParticipantAgentIds = CloneStrings(opportunity.ParticipantAgentIds),
                    });
            }

            return clone;
        }

        private static List<AidOpportunity> CloneAidOpportunities(
            IReadOnlyList<AidOpportunity> source)
        {
            if (source == null) return null;
            var clone = new List<AidOpportunity>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                AidOpportunity opportunity = source[i];
                clone.Add(opportunity == null
                    ? null
                    : new AidOpportunity
                    {
                        OpportunityId = opportunity.OpportunityId,
                        PurposeId = opportunity.PurposeId,
                        SourceCauseId = opportunity.SourceCauseId,
                        RequiredOfficialStatusId = opportunity.RequiredOfficialStatusId,
                        RequiredOfficialStatusRecognised =
                            opportunity.RequiredOfficialStatusRecognised,
                        UtilityBonus = opportunity.UtilityBonus,
                        EligibleAgentIds = CloneStrings(opportunity.EligibleAgentIds),
                    });
            }

            return clone;
        }

        private static List<AppealOpportunity> CloneAppealOpportunities(
            IReadOnlyList<AppealOpportunity> source)
        {
            if (source == null) return null;
            var clone = new List<AppealOpportunity>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                AppealOpportunity opportunity = source[i];
                clone.Add(opportunity == null
                    ? null
                    : new AppealOpportunity
                    {
                        OpportunityId = opportunity.OpportunityId,
                        DocketId = opportunity.DocketId,
                        CaseId = opportunity.CaseId,
                        ChallengedRulingId = opportunity.ChallengedRulingId,
                        SourceCauseId = opportunity.SourceCauseId,
                        HearingCycle = opportunity.HearingCycle,
                        UtilityBonus = opportunity.UtilityBonus,
                        PartyAgentIds = CloneStrings(opportunity.PartyAgentIds),
                    });
            }

            return clone;
        }

        private static List<LieOpportunity> CloneLieOpportunities(
            IReadOnlyList<LieOpportunity> source)
        {
            if (source == null) return null;
            var clone = new List<LieOpportunity>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                LieOpportunity opportunity = source[i];
                clone.Add(opportunity == null ? null : new LieOpportunity
                {
                    OpportunityId = opportunity.OpportunityId,
                    BeliefId = opportunity.BeliefId,
                    AssertionPropositionId = opportunity.AssertionPropositionId,
                    AssertionSubjectId = opportunity.AssertionSubjectId,
                    AssertionObjectId = opportunity.AssertionObjectId,
                    ContextId = opportunity.ContextId,
                    UtilityBonus = opportunity.UtilityBonus,
                    Visibility = opportunity.Visibility,
                    PotentialRecordSourceId = opportunity.PotentialRecordSourceId,
                    EligibleActorIds = CloneStrings(opportunity.EligibleActorIds),
                    AudienceAgentIds = CloneStrings(opportunity.AudienceAgentIds),
                });
            }

            return clone;
        }

        private static List<StealOpportunity> CloneStealOpportunities(
            IReadOnlyList<StealOpportunity> source)
        {
            if (source == null) return null;
            var clone = new List<StealOpportunity>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                StealOpportunity opportunity = source[i];
                clone.Add(opportunity == null ? null : new StealOpportunity
                {
                    OpportunityId = opportunity.OpportunityId,
                    ResourceId = opportunity.ResourceId,
                    ExpectedPhysicalHolderId = opportunity.ExpectedPhysicalHolderId,
                    NewLocationContextId = opportunity.NewLocationContextId,
                    AccessGrantId = opportunity.AccessGrantId,
                    ReliefNeed = opportunity.ReliefNeed,
                    ReliefAmount = opportunity.ReliefAmount,
                    UtilityBonus = opportunity.UtilityBonus,
                    Visibility = opportunity.Visibility,
                    Secrecy = opportunity.Secrecy,
                    EligibleActorIds = CloneStrings(opportunity.EligibleActorIds),
                    DirectWitnessAgentIds = CloneStrings(opportunity.DirectWitnessAgentIds),
                    PotentialRecordSourceIds = CloneStrings(
                        opportunity.PotentialRecordSourceIds),
                });
            }

            return clone;
        }

        private static List<RetaliationOpportunity> CloneRetaliationOpportunities(
            IReadOnlyList<RetaliationOpportunity> source)
        {
            if (source == null) return null;
            var clone = new List<RetaliationOpportunity>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                RetaliationOpportunity opportunity = source[i];
                clone.Add(opportunity == null ? null : new RetaliationOpportunity
                {
                    OpportunityId = opportunity.OpportunityId,
                    TargetAgentId = opportunity.TargetAgentId,
                    PerceivedPriorActionBeliefId = opportunity.PerceivedPriorActionBeliefId,
                    AuthorityGrantId = opportunity.AuthorityGrantId,
                    AffectedAccessGrantId = opportunity.AffectedAccessGrantId,
                    AdverseActionKindId = opportunity.AdverseActionKindId,
                    PerceivedPower = opportunity.PerceivedPower,
                    UtilityBonus = opportunity.UtilityBonus,
                    Visibility = opportunity.Visibility,
                    Secrecy = opportunity.Secrecy,
                    EligibleActorIds = CloneStrings(opportunity.EligibleActorIds),
                    DirectWitnessAgentIds = CloneStrings(opportunity.DirectWitnessAgentIds),
                    PotentialRecordSourceIds = CloneStrings(
                        opportunity.PotentialRecordSourceIds),
                });
            }

            return clone;
        }

        private static List<OrganiseOpportunity> CloneOrganiseOpportunities(
            IReadOnlyList<OrganiseOpportunity> source)
        {
            if (source == null) return null;
            var clone = new List<OrganiseOpportunity>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                OrganiseOpportunity opportunity = source[i];
                clone.Add(opportunity == null ? null : new OrganiseOpportunity
                {
                    OpportunityId = opportunity.OpportunityId,
                    CollectiveCommitmentId = opportunity.CollectiveCommitmentId,
                    IssueId = opportunity.IssueId,
                    IntentionId = opportunity.IntentionId,
                    CommunicationContextId = opportunity.CommunicationContextId,
                    RequiredParticipantCount = opportunity.RequiredParticipantCount,
                    UtilityBonus = opportunity.UtilityBonus,
                    Visibility = opportunity.Visibility,
                    Secrecy = opportunity.Secrecy,
                    EligibleActorIds = CloneStrings(opportunity.EligibleActorIds),
                    PerceivedCauseEventIds = CloneStrings(
                        opportunity.PerceivedCauseEventIds),
                    DirectWitnessAgentIds = CloneStrings(opportunity.DirectWitnessAgentIds),
                    PotentialRecordSourceIds = CloneStrings(
                        opportunity.PotentialRecordSourceIds),
                });
            }

            return clone;
        }

        private static void AddIdleCandidate(List<ActionCandidate> candidates)
        {
            var candidate = new ActionCandidate("idle", SocietyActionKind.Idle, null, null);
            candidate.Add("baseline.idle", null, 0);
            candidates.Add(candidate);
        }

        private static void AddWorkCandidate(AgentDecisionContext context, List<ActionCandidate> candidates)
        {
            AgentPerception actor = context.Actor;
            if (!context.Input.WorkAvailable || !actor.Standing.CanWork) return;

            AddWorkCandidate(
                context, candidates, "work", actor.EmployerId, null, 0, null, true);
            if (context.Input.WorkOpportunities == null) return;
            for (int i = 0; i < context.Input.WorkOpportunities.Count; i++)
            {
                WorkOpportunity opportunity = context.Input.WorkOpportunities[i];
                if (!CanPerformWorkOpportunity(actor, opportunity)) continue;
                AddWorkCandidate(
                    context,
                    candidates,
                    $"work:{opportunity.OpportunityId}",
                    opportunity.OpportunityId,
                    opportunity.OpportunityId,
                    opportunity.UtilityBonus,
                    opportunity.RequiredOfficialStatusId,
                    opportunity.RequiredOfficialStatusRecognised);
            }
        }

        private static void AddWorkCandidate(
            AgentDecisionContext context,
            List<ActionCandidate> candidates,
            string candidateId,
            string targetId,
            string opportunityId,
            int opportunityBonus,
            string requiredStatusId,
            bool requiredStatusRecognised)
        {
            AgentPerception actor = context.Actor;
            var candidate = new ActionCandidate(
                candidateId,
                SocietyActionKind.Work,
                targetId,
                null,
                null,
                opportunityId);
            candidate.Add("need.subsistence", NeedKind.Subsistence.ToString(), Need(actor, NeedKind.Subsistence) / 2);
            candidate.Add("need.autonomy_cost", NeedKind.Autonomy.ToString(), -(Need(actor, NeedKind.Autonomy) / 5));
            candidate.Add("disposition.duty", null, actor.Disposition.Duty / 3);
            candidate.Add("regime.work_reward", null, context.Regime.WorkReward / 5);
            candidate.Add("commitment.employment", actor.EmployerId,
                CommitmentStrength(actor, "employment", actor.EmployerId) / 3);
            if (!string.IsNullOrEmpty(opportunityId))
                candidate.Add("opportunity.work", opportunityId, opportunityBonus);
            if (!string.IsNullOrWhiteSpace(requiredStatusId))
            {
                candidate.Add(
                    requiredStatusRecognised
                        ? "standing.required-status"
                        : "standing.required-status-absent",
                    requiredStatusId,
                    0);
            }
            AddVariation(context, candidate);
            candidates.Add(candidate);
        }

        private static bool CanPerformWorkOpportunity(
            AgentPerception actor,
            WorkOpportunity opportunity)
        {
            if (opportunity == null || string.IsNullOrWhiteSpace(opportunity.OpportunityId)) return false;
            if (!string.IsNullOrWhiteSpace(opportunity.RequiredOfficialStatusId) &&
                actor.Standing.IsRecognised(opportunity.RequiredOfficialStatusId) !=
                opportunity.RequiredOfficialStatusRecognised) return false;
            if (opportunity.ParticipantAgentIds != null && opportunity.ParticipantAgentIds.Count > 0)
                return ContainsOrdinal(opportunity.ParticipantAgentIds, actor.StableId);
            return string.IsNullOrWhiteSpace(opportunity.RequiredEmployerId) ||
                   string.Equals(actor.EmployerId, opportunity.RequiredEmployerId, StringComparison.Ordinal);
        }

        private static void AddSeekAidCandidate(AgentDecisionContext context, List<ActionCandidate> candidates)
        {
            AgentPerception actor = context.Actor;
            if (!context.Input.AidAvailable || !actor.Standing.CanSeekAid) return;
            if (context.Input.AidOpportunities != null &&
                (context.Input.RestrictAidToOpportunities || context.Input.AidOpportunities.Count > 0))
            {
                for (int i = 0; i < context.Input.AidOpportunities.Count; i++)
                {
                    AidOpportunity opportunity = context.Input.AidOpportunities[i];
                    if (opportunity == null || string.IsNullOrWhiteSpace(opportunity.OpportunityId)) continue;
                    if (opportunity.EligibleAgentIds != null && opportunity.EligibleAgentIds.Count > 0 &&
                        !ContainsOrdinal(opportunity.EligibleAgentIds, actor.StableId)) continue;
                    if (!string.IsNullOrWhiteSpace(opportunity.RequiredOfficialStatusId) &&
                        actor.Standing.IsRecognised(opportunity.RequiredOfficialStatusId) !=
                        opportunity.RequiredOfficialStatusRecognised) continue;
                    AddSeekAidCandidate(context, candidates, opportunity);
                }
                return;
            }
            if (!string.IsNullOrWhiteSpace(context.Input.AidRequiredOfficialStatusId) &&
                !actor.Standing.IsRecognised(context.Input.AidRequiredOfficialStatusId))
            {
                return;
            }

            var candidate = new ActionCandidate("seek-aid", SocietyActionKind.SeekAid, "branch-42", null);
            if (!string.IsNullOrWhiteSpace(context.Input.AidRequiredOfficialStatusId))
            {
                candidate.Add(
                    "standing.required-status",
                    context.Input.AidRequiredOfficialStatusId,
                    0);
            }
            candidate.Add("need.health", NeedKind.Health.ToString(), Need(actor, NeedKind.Health) / 2);
            candidate.Add("need.safety", NeedKind.Safety.ToString(), Need(actor, NeedKind.Safety) / 3);
            candidate.Add("attitude.institutional-trust", null, actor.InstitutionalTrust / 5);
            candidate.Add("disposition.institutional-reliance", null,
                actor.Disposition.InstitutionalReliance / 4);
            candidate.Add("regime.aid-effectiveness", null, context.Regime.AidEffectiveness / 5);
            AddVariation(context, candidate);
            candidates.Add(candidate);
        }

        private static void AddSeekAidCandidate(
            AgentDecisionContext context,
            List<ActionCandidate> candidates,
            AidOpportunity opportunity)
        {
            AgentPerception actor = context.Actor;
            var candidate = new ActionCandidate(
                $"seek-aid:{opportunity.OpportunityId}",
                SocietyActionKind.SeekAid,
                opportunity.OpportunityId,
                null,
                null,
                opportunity.OpportunityId);
            if (!string.IsNullOrWhiteSpace(opportunity.RequiredOfficialStatusId))
            {
                candidate.Add(
                    opportunity.RequiredOfficialStatusRecognised
                        ? "standing.required-status"
                        : "standing.required-status-absent",
                    opportunity.RequiredOfficialStatusId,
                    0);
            }
            candidate.Add("need.health", NeedKind.Health.ToString(), Need(actor, NeedKind.Health) / 2);
            candidate.Add("need.safety", NeedKind.Safety.ToString(), Need(actor, NeedKind.Safety) / 3);
            candidate.Add("attitude.institutional-trust", null, actor.InstitutionalTrust / 5);
            candidate.Add("disposition.institutional-reliance", null,
                actor.Disposition.InstitutionalReliance / 4);
            candidate.Add("regime.aid-effectiveness", null, context.Regime.AidEffectiveness / 5);
            candidate.Add("opportunity.aid", opportunity.OpportunityId, opportunity.UtilityBonus);
            AddVariation(context, candidate);
            candidates.Add(candidate);
        }

        private static void AddHelpCandidates(AgentDecisionContext context, List<ActionCandidate> candidates)
        {
            AgentPerception actor = context.Actor;
            for (int i = 0; i < actor.Relationships.Count; i++)
            {
                RelationshipState relationship = actor.Relationships[i];
                if (!IsPerceived(context.PerceivedAgentIds, relationship.TargetAgentId)) continue;

                string candidateId = $"help:{relationship.TargetAgentId}";
                var candidate = new ActionCandidate(
                    candidateId,
                    SocietyActionKind.Help,
                    relationship.TargetAgentId,
                    null,
                    relationship.PerceivedNeed);
                candidate.Add("disposition.solidarity", null, actor.Disposition.Solidarity / 3);
                candidate.Add("need.belonging", NeedKind.Belonging.ToString(), Need(actor, NeedKind.Belonging) / 6);
                candidate.Add("relationship.obligation", relationship.TargetAgentId, relationship.Obligation / 2);
                candidate.Add("relationship.attachment", relationship.TargetAgentId, relationship.Attachment / 3);
                candidate.Add("relationship.trust", relationship.TargetAgentId, relationship.Trust / 5);
                candidate.Add("relationship.fear", relationship.TargetAgentId, -(relationship.Fear / 4));
                candidate.Add("commitment.target", relationship.TargetAgentId,
                    CommitmentStrengthForTarget(actor, relationship.TargetAgentId) / 3);
                candidate.Add("perception.target-need", relationship.TargetAgentId,
                    relationship.PerceivedNeedPressure / 3);
                AddVariation(context, candidate);
                candidates.Add(candidate);
            }
        }

        private static void AddEvidenceCandidates(AgentDecisionContext context, List<ActionCandidate> candidates)
        {
            AgentPerception actor = context.Actor;
            if (!context.Input.DisclosureRequested || !actor.Standing.CanGiveEvidence) return;

            for (int i = 0; i < actor.Beliefs.Count; i++)
            {
                BeliefState belief = actor.Beliefs[i];
                if (belief.Disclosed || belief.EnteredOfficialRecord) continue;

                RelationshipState subjectRelationship = actor.GetRelationship(belief.SubjectId);
                int obligation = subjectRelationship?.Obligation ?? 0;
                int fear = subjectRelationship?.Fear ?? 0;
                int authority = subjectRelationship?.Authority ?? 0;

                string discloseId = $"disclose:{belief.BeliefId}";
                var disclose = new ActionCandidate(
                    discloseId,
                    SocietyActionKind.Disclose,
                    "branch-42",
                    belief.BeliefId);
                disclose.Add("belief.confidence", belief.BeliefId, belief.Confidence / 3);
                disclose.Add("belief.emotional-weight", belief.BeliefId, belief.EmotionalWeight / 5);
                disclose.Add("belief.secrecy", belief.BeliefId, -(belief.Secrecy / 2));
                disclose.Add("disposition.candour", null, actor.Disposition.Candour / 2);
                disclose.Add("relationship.obligation", belief.SubjectId, obligation / 3);
                disclose.Add("relationship.fear", belief.SubjectId, -(fear / 3));
                disclose.Add("relationship.authority", belief.SubjectId, -(authority / 4));
                disclose.Add("attitude.institutional-trust", null, actor.InstitutionalTrust / 5);
                disclose.Add("regime.disclosure-protection", null, context.Regime.DisclosureProtection / 4);
                disclose.Add("regime.retaliation-risk", null, -(context.Regime.RetaliationRisk / 4));
                AddVariation(context, disclose);
                candidates.Add(disclose);

                if (!string.Equals(
                    belief.LastWithheldIncidentId,
                    context.Input.IncidentId,
                    StringComparison.Ordinal))
                {
                    string withholdId = $"withhold:{belief.BeliefId}";
                    var withhold = new ActionCandidate(
                        withholdId,
                        SocietyActionKind.Withhold,
                        "branch-42",
                        belief.BeliefId);
                    withhold.Add("belief.secrecy", belief.BeliefId, belief.Secrecy / 2);
                    withhold.Add("relationship.fear", belief.SubjectId, fear / 2);
                    withhold.Add("relationship.authority", belief.SubjectId, authority / 3);
                    withhold.Add("disposition.risk-aversion", null, (100 - actor.Disposition.RiskTolerance) / 5);
                    withhold.Add("disposition.candour", null, -(actor.Disposition.Candour / 3));
                    withhold.Add("regime.retaliation-risk", null, context.Regime.RetaliationRisk / 3);
                    withhold.Add("regime.disclosure-protection", null, -(context.Regime.DisclosureProtection / 6));
                    AddVariation(context, withhold);
                    candidates.Add(withhold);
                }
            }
        }

        private static void AddAppealCandidate(AgentDecisionContext context, List<ActionCandidate> candidates)
        {
            AgentPerception actor = context.Actor;
            if (!context.Input.AppealWindowOpen || !actor.Standing.CanAppeal ||
                !actor.Standing.IsRecognised(InstitutionalStatusIds.AdverseDecision) ||
                actor.Standing.IsRecognised(InstitutionalStatusIds.AppealPending))
            {
                return;
            }
            if (context.Input.AppealOpportunities != null &&
                (context.Input.RestrictAppealToOpportunities ||
                 context.Input.AppealOpportunities.Count > 0))
            {
                for (int i = 0; i < context.Input.AppealOpportunities.Count; i++)
                {
                    AppealOpportunity opportunity = context.Input.AppealOpportunities[i];
                    if (opportunity == null || string.IsNullOrWhiteSpace(opportunity.OpportunityId)) continue;
                    if (opportunity.PartyAgentIds != null && opportunity.PartyAgentIds.Count > 0 &&
                        !ContainsOrdinal(opportunity.PartyAgentIds, actor.StableId)) continue;
                    AddAppealCandidate(context, candidates, opportunity);
                }
                return;
            }
            if (context.Input.AppealEligibleAgentIds != null &&
                !ContainsOrdinal(context.Input.AppealEligibleAgentIds, actor.StableId))
            {
                return;
            }

            var candidate = new ActionCandidate("appeal", SocietyActionKind.Appeal, "branch-42", null);
            if (context.Input.AppealEligibleAgentIds != null)
                candidate.Add("procedure.appeal-eligibility", context.Input.IncidentId, 0);
            candidate.Add("need.autonomy", NeedKind.Autonomy.ToString(), Need(actor, NeedKind.Autonomy) / 2);
            candidate.Add("need.safety", NeedKind.Safety.ToString(), Need(actor, NeedKind.Safety) / 4);
            candidate.Add("disposition.institutional-reliance", null,
                actor.Disposition.InstitutionalReliance / 4);
            candidate.Add("attitude.institutional-trust", null, actor.InstitutionalTrust / 6);
            candidate.Add("regime.appeal-accessibility", null, context.Regime.AppealAccessibility / 3);
            AddVariation(context, candidate);
            candidates.Add(candidate);
        }

        private static void AddAppealCandidate(
            AgentDecisionContext context,
            List<ActionCandidate> candidates,
            AppealOpportunity opportunity)
        {
            AgentPerception actor = context.Actor;
            var candidate = new ActionCandidate(
                $"appeal:{opportunity.OpportunityId}",
                SocietyActionKind.Appeal,
                opportunity.OpportunityId,
                null,
                null,
                opportunity.OpportunityId);
            candidate.Add("procedure.appeal-eligibility", opportunity.CaseId, 0);
            candidate.Add("need.autonomy", NeedKind.Autonomy.ToString(), Need(actor, NeedKind.Autonomy) / 2);
            candidate.Add("need.safety", NeedKind.Safety.ToString(), Need(actor, NeedKind.Safety) / 4);
            candidate.Add("disposition.institutional-reliance", null,
                actor.Disposition.InstitutionalReliance / 4);
            candidate.Add("attitude.institutional-trust", null, actor.InstitutionalTrust / 6);
            candidate.Add("regime.appeal-accessibility", null, context.Regime.AppealAccessibility / 3);
            candidate.Add("opportunity.appeal", opportunity.OpportunityId, opportunity.UtilityBonus);
            AddVariation(context, candidate);
            candidates.Add(candidate);
        }

        private static void AddLieCandidates(
            AgentDecisionContext context,
            List<ActionCandidate> candidates)
        {
            if (context.Input.LieOpportunities == null) return;
            AgentPerception actor = context.Actor;
            for (int i = 0; i < context.Input.LieOpportunities.Count; i++)
            {
                LieOpportunity opportunity = context.Input.LieOpportunities[i];
                if (opportunity == null ||
                    !Eligible(opportunity.EligibleActorIds, actor.StableId) ||
                    string.IsNullOrWhiteSpace(opportunity.OpportunityId) ||
                    string.IsNullOrWhiteSpace(opportunity.AssertionPropositionId))
                {
                    continue;
                }

                BeliefState belief = actor.GetBelief(opportunity.BeliefId);
                if (belief == null || belief.Confidence < 50 ||
                    string.Equals(
                        belief.PropositionId,
                        opportunity.AssertionPropositionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                RelationshipState relationship = actor.GetRelationship(belief.SubjectId);
                var candidate = new ActionCandidate(
                    $"lie:{opportunity.OpportunityId}",
                    SocietyActionKind.Lie,
                    opportunity.ContextId,
                    belief.BeliefId,
                    null,
                    opportunity.OpportunityId);
                candidate.Add("belief.confidence", belief.BeliefId, belief.Confidence / 5);
                candidate.Add("belief.secrecy", belief.BeliefId, belief.Secrecy / 3);
                candidate.Add("relationship.obligation", belief.SubjectId,
                    (relationship?.Obligation ?? 0) / 3);
                candidate.Add("relationship.fear", belief.SubjectId,
                    (relationship?.Fear ?? 0) / 4);
                candidate.Add("need.safety", NeedKind.Safety.ToString(),
                    Need(actor, NeedKind.Safety) / 4);
                candidate.Add("disposition.candour", null, -(actor.Disposition.Candour / 2));
                candidate.Add("attitude.institutional-trust", null,
                    -(Math.Max(0, actor.InstitutionalTrust) / 6));
                candidate.Add("opportunity.lie", opportunity.OpportunityId,
                    opportunity.UtilityBonus);
                AddVariation(context, candidate);
                candidates.Add(candidate);
            }
        }

        private static void AddStealCandidates(
            AgentDecisionContext context,
            List<ActionCandidate> candidates)
        {
            if (context.Input.StealOpportunities == null) return;
            AgentPerception actor = context.Actor;
            for (int i = 0; i < context.Input.StealOpportunities.Count; i++)
            {
                StealOpportunity opportunity = context.Input.StealOpportunities[i];
                if (opportunity == null ||
                    !Eligible(opportunity.EligibleActorIds, actor.StableId) ||
                    string.IsNullOrWhiteSpace(opportunity.OpportunityId) ||
                    string.IsNullOrWhiteSpace(opportunity.ResourceId) ||
                    string.IsNullOrWhiteSpace(opportunity.AccessGrantId))
                {
                    continue;
                }

                var candidate = new ActionCandidate(
                    $"steal:{opportunity.OpportunityId}",
                    SocietyActionKind.Steal,
                    opportunity.ResourceId,
                    null,
                    opportunity.ReliefNeed,
                    opportunity.OpportunityId);
                candidate.Add("need.relief", opportunity.ReliefNeed.ToString(),
                    Need(actor, opportunity.ReliefNeed) / 2);
                candidate.Add("need.autonomy", NeedKind.Autonomy.ToString(),
                    Need(actor, NeedKind.Autonomy) / 5);
                candidate.Add("disposition.risk-tolerance", null,
                    actor.Disposition.RiskTolerance / 4);
                candidate.Add("disposition.duty", null, -(actor.Disposition.Duty / 4));
                candidate.Add("attitude.institutional-trust", null,
                    -(Math.Max(0, actor.InstitutionalTrust) / 5));
                candidate.Add("opportunity.steal", opportunity.OpportunityId,
                    opportunity.UtilityBonus);
                AddVariation(context, candidate);
                candidates.Add(candidate);
            }
        }

        private static void AddRetaliationCandidates(
            AgentDecisionContext context,
            List<ActionCandidate> candidates)
        {
            if (context.Input.RetaliationOpportunities == null) return;
            AgentPerception actor = context.Actor;
            for (int i = 0; i < context.Input.RetaliationOpportunities.Count; i++)
            {
                RetaliationOpportunity opportunity = context.Input.RetaliationOpportunities[i];
                if (opportunity == null ||
                    !Eligible(opportunity.EligibleActorIds, actor.StableId) ||
                    !IsPerceived(context.PerceivedAgentIds, opportunity.TargetAgentId) ||
                    string.IsNullOrWhiteSpace(opportunity.OpportunityId) ||
                    string.IsNullOrWhiteSpace(opportunity.AuthorityGrantId) ||
                    string.IsNullOrWhiteSpace(opportunity.AffectedAccessGrantId) ||
                    opportunity.PerceivedPower <= 0)
                {
                    continue;
                }

                BeliefState prior = actor.GetBelief(opportunity.PerceivedPriorActionBeliefId);
                if (prior == null || prior.Confidence < 25 ||
                    !string.Equals(prior.SubjectId, opportunity.TargetAgentId, StringComparison.Ordinal))
                {
                    continue;
                }

                RelationshipState relationship = actor.GetRelationship(opportunity.TargetAgentId);
                var candidate = new ActionCandidate(
                    $"retaliate:{opportunity.OpportunityId}",
                    SocietyActionKind.Retaliate,
                    opportunity.TargetAgentId,
                    prior.BeliefId,
                    null,
                    opportunity.OpportunityId);
                candidate.Add("belief.prior-adverse-action", prior.BeliefId,
                    prior.Confidence / 3);
                candidate.Add("belief.emotional-weight", prior.BeliefId,
                    prior.EmotionalWeight / 3);
                candidate.Add("authority.perceived-power", opportunity.AuthorityGrantId,
                    opportunity.PerceivedPower / 3);
                candidate.Add("relationship.fear", opportunity.TargetAgentId,
                    (relationship?.Fear ?? 0) / 5);
                candidate.Add("disposition.duty", null, actor.Disposition.Duty / 5);
                candidate.Add("regime.retaliation-risk", null,
                    context.Regime.RetaliationRisk / 5);
                candidate.Add("opportunity.retaliate", opportunity.OpportunityId,
                    opportunity.UtilityBonus);
                AddVariation(context, candidate);
                candidates.Add(candidate);
            }
        }

        private static void AddOrganiseCandidates(
            AgentDecisionContext context,
            List<ActionCandidate> candidates)
        {
            if (context.Input.OrganiseOpportunities == null) return;
            AgentPerception actor = context.Actor;
            for (int i = 0; i < context.Input.OrganiseOpportunities.Count; i++)
            {
                OrganiseOpportunity opportunity = context.Input.OrganiseOpportunities[i];
                if (opportunity == null ||
                    !Eligible(opportunity.EligibleActorIds, actor.StableId) ||
                    string.IsNullOrWhiteSpace(opportunity.OpportunityId) ||
                    string.IsNullOrWhiteSpace(opportunity.CollectiveCommitmentId) ||
                    string.IsNullOrWhiteSpace(opportunity.IssueId) ||
                    opportunity.RequiredParticipantCount < 2)
                {
                    continue;
                }

                int grievance = CommitmentStrength(actor, "grievance", opportunity.IssueId);
                if (grievance <= 0) continue;
                var candidate = new ActionCandidate(
                    $"organise:{opportunity.OpportunityId}",
                    SocietyActionKind.Organise,
                    opportunity.IssueId,
                    null,
                    NeedKind.Belonging,
                    opportunity.OpportunityId);
                candidate.Add("commitment.grievance", opportunity.IssueId, grievance / 2);
                candidate.Add("disposition.solidarity", null, actor.Disposition.Solidarity / 2);
                candidate.Add("need.belonging", NeedKind.Belonging.ToString(),
                    Need(actor, NeedKind.Belonging) / 4);
                candidate.Add("need.autonomy", NeedKind.Autonomy.ToString(),
                    Need(actor, NeedKind.Autonomy) / 4);
                candidate.Add("disposition.risk-tolerance", null,
                    actor.Disposition.RiskTolerance / 5);
                candidate.Add("opportunity.organise", opportunity.OpportunityId,
                    opportunity.UtilityBonus);
                AddVariation(context, candidate);
                candidates.Add(candidate);
            }
        }

        private static bool Eligible(IReadOnlyList<string> eligibleActorIds, string actorId)
            => eligibleActorIds == null || eligibleActorIds.Count == 0 ||
               ContainsOrdinal(eligibleActorIds, actorId);

        private static void AddVariation(AgentDecisionContext context, ActionCandidate candidate)
        {
            int amplitude = context.Regime.DecisionVariationAmplitude;
            int variation = amplitude == 0
                ? 0
                : StableDecisionRoll.Range(
                    context.MasterSeed,
                    context.Tick,
                    context.Actor.SimulationOrdinal.ToString(),
                    candidate.CandidateId,
                    -amplitude,
                    amplitude + 1);
            candidate.Add("variation.keyed", candidate.CandidateId, variation);
        }

        private static int Need(AgentPerception actor, NeedKind kind)
            => actor.GetNeed(kind)?.Pressure ?? 0;

        private static int CommitmentStrength(AgentPerception actor, string kind, string targetId)
        {
            int total = 0;
            for (int i = 0; i < actor.Commitments.Count; i++)
            {
                CommitmentState commitment = actor.Commitments[i];
                if (!string.Equals(commitment.Kind, kind, StringComparison.Ordinal)) continue;
                if (!string.Equals(commitment.TargetId, targetId, StringComparison.Ordinal)) continue;
                total += commitment.Strength;
            }

            return InstitutionalMath.Clamp(total, 0, 100);
        }

        private static int CommitmentStrengthForTarget(AgentPerception actor, string targetId)
        {
            int total = 0;
            for (int i = 0; i < actor.Commitments.Count; i++)
            {
                CommitmentState commitment = actor.Commitments[i];
                if (!string.Equals(commitment.TargetId, targetId, StringComparison.Ordinal)) continue;
                total += commitment.Strength;
            }

            return InstitutionalMath.Clamp(total, 0, 100);
        }

        private static bool IsPerceived(IReadOnlyList<string> perceivedIds, string targetId)
        {
            if (perceivedIds == null) return false;
            for (int i = 0; i < perceivedIds.Count; i++)
                if (string.Equals(perceivedIds[i], targetId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool ContainsOrdinal(IReadOnlyList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private sealed class ActionCandidate
        {
            public readonly string CandidateId;
            public readonly SocietyActionKind Action;
            public readonly string TargetId;
            public readonly string OpportunityId;
            public readonly string SubjectBeliefId;
            public readonly NeedKind? IntendedNeed;
            public readonly List<DecisionReason> Reasons = new();
            public int Score { get; private set; }

            public ActionCandidate(
                string candidateId,
                SocietyActionKind action,
                string targetId,
                string subjectBeliefId,
                NeedKind? intendedNeed = null,
                string opportunityId = null)
            {
                CandidateId = candidateId;
                Action = action;
                TargetId = targetId;
                OpportunityId = opportunityId;
                SubjectBeliefId = subjectBeliefId;
                IntendedNeed = intendedNeed;
            }

            public void Add(string reasonId, string sourceId, int scoreDelta)
            {
                Reasons.Add(new DecisionReason
                {
                    ReasonId = reasonId,
                    SourceId = sourceId,
                    ScoreDelta = scoreDelta,
                });
                Score += scoreDelta;
            }
        }
    }
}
