using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Generic utility-based decision process shared by every agent. The engine reads
    /// only the actor's needs, commitments, relationships, beliefs, perceived agent ids,
    /// and the rules exposed by the institution. It never reads lived ground truth.
    /// </summary>
    public sealed class AgentDecisionEngine
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

            ActionCandidate winner = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                ActionCandidate candidate = candidates[i];
                if (candidate.Score > winner.Score ||
                    (candidate.Score == winner.Score &&
                     string.CompareOrdinal(candidate.CandidateId, winner.CandidateId) < 0))
                {
                    winner = candidate;
                }
            }

            return new AgentDecision
            {
                Tick = context.Tick,
                ApplicationOrdinal = context.Actor.SimulationOrdinal,
                DecisionId = $"decision:{context.Tick}:{context.Actor.StableId}",
                CandidateId = winner.CandidateId,
                ActorId = context.Actor.StableId,
                Action = winner.Action,
                TargetId = winner.TargetId,
                SubjectBeliefId = winner.SubjectBeliefId,
                IntendedNeed = winner.IntendedNeed,
                Score = winner.Score,
                Reasons = winner.Reasons,
            };
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

            var candidate = new ActionCandidate("work", SocietyActionKind.Work, actor.EmployerId, null);
            candidate.Add("need.subsistence", NeedKind.Subsistence.ToString(), Need(actor, NeedKind.Subsistence) / 2);
            candidate.Add("need.autonomy_cost", NeedKind.Autonomy.ToString(), -(Need(actor, NeedKind.Autonomy) / 5));
            candidate.Add("disposition.duty", null, actor.Disposition.Duty / 3);
            candidate.Add("regime.work_reward", null, context.Regime.WorkReward / 5);
            candidate.Add("commitment.employment", actor.EmployerId,
                CommitmentStrength(actor, "employment", actor.EmployerId) / 3);
            AddVariation(context, candidate);
            candidates.Add(candidate);
        }

        private static void AddSeekAidCandidate(AgentDecisionContext context, List<ActionCandidate> candidates)
        {
            AgentPerception actor = context.Actor;
            if (!context.Input.AidAvailable || !actor.Standing.CanSeekAid) return;

            var candidate = new ActionCandidate("seek-aid", SocietyActionKind.SeekAid, "branch-42", null);
            candidate.Add("need.health", NeedKind.Health.ToString(), Need(actor, NeedKind.Health) / 2);
            candidate.Add("need.safety", NeedKind.Safety.ToString(), Need(actor, NeedKind.Safety) / 3);
            candidate.Add("attitude.institutional-trust", null, actor.InstitutionalTrust / 5);
            candidate.Add("disposition.institutional-reliance", null,
                actor.Disposition.InstitutionalReliance / 4);
            candidate.Add("regime.aid-effectiveness", null, context.Regime.AidEffectiveness / 5);
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
                !actor.Standing.IsRecognised("adverse-decision") ||
                actor.Standing.IsRecognised("appeal-pending"))
            {
                return;
            }

            var candidate = new ActionCandidate("appeal", SocietyActionKind.Appeal, "branch-42", null);
            candidate.Add("need.autonomy", NeedKind.Autonomy.ToString(), Need(actor, NeedKind.Autonomy) / 2);
            candidate.Add("need.safety", NeedKind.Safety.ToString(), Need(actor, NeedKind.Safety) / 4);
            candidate.Add("disposition.institutional-reliance", null,
                actor.Disposition.InstitutionalReliance / 4);
            candidate.Add("attitude.institutional-trust", null, actor.InstitutionalTrust / 6);
            candidate.Add("regime.appeal-accessibility", null, context.Regime.AppealAccessibility / 3);
            AddVariation(context, candidate);
            candidates.Add(candidate);
        }

        private static void AddVariation(AgentDecisionContext context, ActionCandidate candidate)
        {
            int variation = StableDecisionRoll.Range(
                context.MasterSeed,
                context.Tick,
                context.Actor.StableId,
                candidate.CandidateId,
                -2,
                3);
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

        private sealed class ActionCandidate
        {
            public readonly string CandidateId;
            public readonly SocietyActionKind Action;
            public readonly string TargetId;
            public readonly string SubjectBeliefId;
            public readonly NeedKind? IntendedNeed;
            public readonly List<DecisionReason> Reasons = new();
            public int Score { get; private set; }

            public ActionCandidate(
                string candidateId,
                SocietyActionKind action,
                string targetId,
                string subjectBeliefId,
                NeedKind? intendedNeed = null)
            {
                CandidateId = candidateId;
                Action = action;
                TargetId = targetId;
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
