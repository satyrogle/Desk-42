using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>Central invariant boundary used before ticks and at both save boundaries.</summary>
    public static class SocietyStateValidator
    {
        public static void Validate(SocietyState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.SchemaVersion != SocietyState.CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported society schema version {state.SchemaVersion}.");
            if (!string.Equals(
                state.RulesetVersion,
                SocietyState.CurrentRulesetVersion,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported society ruleset {state.RulesetVersion}.");
            }
            if (state.Regime == null)
                throw new InvalidOperationException("Society state requires an institutional regime.");
            if (state.Agents == null)
                throw new InvalidOperationException("Society state requires an agent collection.");

            ValidateRange(state.Regime.WorkReward, 0, 100, "regime.work-reward");
            ValidateRange(state.Regime.AidEffectiveness, 0, 100, "regime.aid-effectiveness");
            ValidateRange(state.Regime.DisclosureProtection, 0, 100, "regime.disclosure-protection");
            ValidateRange(state.Regime.RetaliationRisk, 0, 100, "regime.retaliation-risk");
            ValidateRange(state.Regime.AppealAccessibility, 0, 100, "regime.appeal-accessibility");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var ordinals = new HashSet<int>();
            for (int i = 0; i < state.Agents.Count; i++)
            {
                AgentState agent = state.Agents[i];
                if (agent == null || string.IsNullOrWhiteSpace(agent.StableId))
                    throw new InvalidOperationException("Every agent requires a stable id.");
                if (!ids.Add(agent.StableId))
                    throw new InvalidOperationException($"Duplicate agent id: {agent.StableId}");
                if (agent.SimulationOrdinal < 0 || !ordinals.Add(agent.SimulationOrdinal))
                    throw new InvalidOperationException($"Agent {agent.StableId} has an invalid simulation ordinal.");
            }

            for (int i = 0; i < state.Agents.Count; i++)
                ValidateAgent(state.Agents[i], ids);

            state.EventLedger ??= new List<SocietyEvent>();
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.EventLedger.Count; i++)
            {
                SocietyEvent societyEvent = state.EventLedger[i];
                if (societyEvent == null || string.IsNullOrWhiteSpace(societyEvent.EventId))
                    throw new InvalidOperationException("Every persisted society event requires a stable id.");
                if (!eventIds.Add(societyEvent.EventId))
                    throw new InvalidOperationException($"Duplicate society event id: {societyEvent.EventId}");
            }
        }

        private static void ValidateAgent(AgentState agent, HashSet<string> populationIds)
        {
            if (agent.Disposition == null || agent.Standing == null ||
                agent.Standing.OfficialStatuses == null || agent.Needs == null ||
                agent.Commitments == null || agent.Relationships == null || agent.Beliefs == null ||
                agent.AnomalyRules == null)
            {
                throw new InvalidOperationException($"Agent {agent.StableId} has incomplete simulation state.");
            }

            ValidateRange(agent.InstitutionalTrust, -100, 100, $"{agent.StableId}.institutional-trust");
            ValidateRange(agent.Disposition.RiskTolerance, 0, 100, $"{agent.StableId}.risk-tolerance");
            ValidateRange(agent.Disposition.Candour, 0, 100, $"{agent.StableId}.candour");
            ValidateRange(agent.Disposition.Solidarity, 0, 100, $"{agent.StableId}.solidarity");
            ValidateRange(agent.Disposition.Duty, 0, 100, $"{agent.StableId}.duty");
            ValidateRange(agent.Disposition.InstitutionalReliance, 0, 100,
                $"{agent.StableId}.institutional-reliance");

            var needKinds = new HashSet<NeedKind>();
            for (int i = 0; i < agent.Needs.Count; i++)
            {
                NeedState need = agent.Needs[i] ??
                    throw new InvalidOperationException($"Agent {agent.StableId} has a null need.");
                if (!needKinds.Add(need.Kind))
                    throw new InvalidOperationException($"Agent {agent.StableId} has duplicate need {need.Kind}.");
                ValidateRange(need.Pressure, 0, 100, $"{agent.StableId}.need.{need.Kind}");
            }
            if (needKinds.Count != Enum.GetValues(typeof(NeedKind)).Length)
                throw new InvalidOperationException($"Agent {agent.StableId} requires exactly one state for every need.");

            var commitmentIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < agent.Commitments.Count; i++)
            {
                CommitmentState commitment = agent.Commitments[i];
                if (commitment == null || string.IsNullOrWhiteSpace(commitment.CommitmentId) ||
                    !commitmentIds.Add(commitment.CommitmentId))
                {
                    throw new InvalidOperationException($"Agent {agent.StableId} has a missing or duplicate commitment id.");
                }
                ValidateRange(commitment.Strength, 0, 100,
                    $"{agent.StableId}.commitment.{commitment.CommitmentId}");
            }

            var relationshipTargets = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < agent.Relationships.Count; i++)
            {
                RelationshipState relationship = agent.Relationships[i];
                if (relationship == null || string.IsNullOrWhiteSpace(relationship.TargetAgentId) ||
                    string.Equals(relationship.TargetAgentId, agent.StableId, StringComparison.Ordinal) ||
                    !populationIds.Contains(relationship.TargetAgentId) ||
                    !relationshipTargets.Add(relationship.TargetAgentId))
                {
                    throw new InvalidOperationException($"Agent {agent.StableId} has an invalid relationship target.");
                }
                ValidateRange(relationship.Trust, 0, 100, $"{agent.StableId}.relationship.trust");
                ValidateRange(relationship.Fear, 0, 100, $"{agent.StableId}.relationship.fear");
                ValidateRange(relationship.Obligation, 0, 100, $"{agent.StableId}.relationship.obligation");
                ValidateRange(relationship.Authority, 0, 100, $"{agent.StableId}.relationship.authority");
                ValidateRange(relationship.Attachment, 0, 100, $"{agent.StableId}.relationship.attachment");
                ValidateRange(relationship.PerceivedNeedPressure, 0, 100,
                    $"{agent.StableId}.relationship.perceived-need");
            }

            var beliefIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < agent.Beliefs.Count; i++)
            {
                BeliefState belief = agent.Beliefs[i];
                if (belief == null || string.IsNullOrWhiteSpace(belief.BeliefId) || !beliefIds.Add(belief.BeliefId))
                    throw new InvalidOperationException($"Agent {agent.StableId} has a missing or duplicate belief id.");
                ValidateRange(belief.Confidence, 0, 100, $"{agent.StableId}.belief.confidence");
                ValidateRange(belief.Secrecy, 0, 100, $"{agent.StableId}.belief.secrecy");
                ValidateRange(belief.EmotionalWeight, 0, 100, $"{agent.StableId}.belief.emotional-weight");
            }

            var statusIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < agent.Standing.OfficialStatuses.Count; i++)
            {
                OfficialStatusState status = agent.Standing.OfficialStatuses[i];
                if (status == null || string.IsNullOrWhiteSpace(status.StatusId) || !statusIds.Add(status.StatusId))
                    throw new InvalidOperationException($"Agent {agent.StableId} has a missing or duplicate status id.");
            }

            var traitIds = new HashSet<string>(StringComparer.Ordinal);
            if (agent.AnomalyRules.Count > SocietyState.MaximumAnomalyRulesPerAgent)
            {
                throw new InvalidOperationException(
                    $"Agent {agent.StableId} exceeds the bounded anomaly rule limit of " +
                    $"{SocietyState.MaximumAnomalyRulesPerAgent}.");
            }
            for (int i = 0; i < agent.AnomalyRules.Count; i++)
            {
                AnomalyStatusRule rule = agent.AnomalyRules[i];
                if (rule == null || string.IsNullOrWhiteSpace(rule.TraitId) || !traitIds.Add(rule.TraitId))
                    throw new InvalidOperationException($"Agent {agent.StableId} has a missing or duplicate anomaly trait id.");
                if (string.IsNullOrWhiteSpace(rule.RequiredOfficialStatusId) ||
                    string.IsNullOrWhiteSpace(rule.ObservableEffectId))
                {
                    throw new InvalidOperationException($"Anomaly trait {rule.TraitId} is not bound to inspectable state.");
                }
                ValidateRange(
                    rule.RecognisedPressureDelta,
                    -SocietyState.MaximumAnomalyPressurePerActivation,
                    SocietyState.MaximumAnomalyPressurePerActivation,
                    $"{rule.TraitId}.recognised-delta");
                ValidateRange(
                    rule.UnrecognisedPressureDelta,
                    -SocietyState.MaximumAnomalyPressurePerActivation,
                    SocietyState.MaximumAnomalyPressurePerActivation,
                    $"{rule.TraitId}.unrecognised-delta");
                ValidateRange(rule.MinimumTicksBetweenActivations, 1, 100,
                    $"{rule.TraitId}.minimum-activation-interval");
            }
        }

        private static void ValidateRange(int value, int minimum, int maximum, string fieldId)
        {
            if (value < minimum || value > maximum)
                throw new InvalidOperationException($"{fieldId} must be in [{minimum}, {maximum}], got {value}.");
        }
    }
}
