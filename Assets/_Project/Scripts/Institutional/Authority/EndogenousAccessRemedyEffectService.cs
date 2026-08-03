using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Exact-once restoration for an adjudicated access-withdrawal case. The grant is
    /// resolved from authority-owned causal provenance, never from player allegations.
    /// </summary>
    internal static class EndogenousAccessRemedyEffectService
    {
        internal static EndogenousAccessRemedyApplicationTrace Execute(
            SocietyState society,
            InstitutionalMaterialWorld world,
            EndogenousDocketState state,
            CommittedPlayerRuling ruling)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (ruling == null) throw new ArgumentNullException(nameof(ruling));
            InstitutionalMaterialWorldValidator.Validate(world, society);
            EndogenousDocketValidator.Validate(state, society);
            if (!Contains(
                    ruling.RemedyDefinitionIds,
                    EndogenousPlayerRulingService.RestoreAccessRemedy)) return null;

            EndogenousInstitutionalCase opened = state.GetCase(ruling.CaseId);
            if (opened == null || !string.Equals(
                    opened.IssueId,
                    EndogenousIssueKindIds.AccessWithdrawal,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Restore-access requires a committed access-withdrawal case.");
            }
            MaterialWorldEvent withdrawal = ResolveWithdrawal(world, state, opened);
            MaterialAccessGrantState grant = world.GetAccessGrant(
                withdrawal.StateRecordId) ??
                throw new InvalidOperationException(
                    "Restore-access could not resolve the withdrawn grant.");
            string traceId = "access-remedy-effect:" + ruling.RulingId + ":" +
                             grant.GrantId;
            EndogenousAccessRemedyApplicationTrace replay = FindTrace(state, traceId);
            if (replay != null) return replay;

            bool before = grant.Active;
            bool changed = !before;
            string eventId = changed ? "material:" + traceId : string.Empty;
            var trace = new EndogenousAccessRemedyApplicationTrace
            {
                TraceId = traceId,
                RulingId = ruling.RulingId,
                CaseId = ruling.CaseId,
                AppliedTick = society.CurrentTick,
                AccessGrantId = grant.GrantId,
                BeneficiaryAgentId = grant.AgentId,
                StateBefore = before,
                StateAfter = true,
                MaterialEventId = eventId,
                MaterialStateChanged = changed,
            };
            state.AccessRemedyApplicationTraces.Add(trace);
            if (changed)
            {
                grant.Active = true;
                world.EventLedger.Add(new MaterialWorldEvent
                {
                    EventId = eventId,
                    CauseDecisionId = ruling.RulingId,
                    Tick = society.CurrentTick,
                    Kind = MaterialWorldEventKind.AccessChanged,
                    ActorAgentId = grant.AgentId,
                    TargetAgentId = grant.AgentId,
                    ContextId = "restore-access",
                    StateRecordId = grant.GrantId,
                    StateBefore = false,
                    StateAfter = true,
                    Visibility = MaterialEventVisibility.PublicContext,
                    Secrecy = 0,
                    PotentialRecordSourceIds = new List<string>
                    {
                        "record:remedy-execution:" + ruling.RulingId,
                    },
                });
            }

            try
            {
                InstitutionalMaterialWorldValidator.Validate(world, society);
                EndogenousDocketValidator.Validate(state, society);
            }
            catch
            {
                state.AccessRemedyApplicationTraces.RemoveAt(
                    state.AccessRemedyApplicationTraces.Count - 1);
                if (changed)
                {
                    world.EventLedger.RemoveAt(world.EventLedger.Count - 1);
                    grant.Active = false;
                }
                throw;
            }
            return trace;
        }

        private static MaterialWorldEvent ResolveWithdrawal(
            InstitutionalMaterialWorld world,
            EndogenousDocketState state,
            EndogenousInstitutionalCase opened)
        {
            DocketCandidate docket = state.GetDocketCandidate(
                opened.DocketCandidateId) ??
                throw new InvalidOperationException("The access case lost its docket source.");
            IncidentCandidate incident = state.GetIncident(
                docket.AuthorityIncidentCandidateId) ??
                throw new InvalidOperationException("The access case lost its causal incident.");
            MaterialWorldEvent result = null;
            for (int i = 0; i < incident.CauseEventIds.Count; i++)
            {
                MaterialWorldEvent candidate = world.GetEvent(incident.CauseEventIds[i]);
                if (candidate == null ||
                    candidate.Kind != MaterialWorldEventKind.AccessChanged ||
                    candidate.StateAfter) continue;
                if (result != null)
                    throw new InvalidOperationException(
                        "Restore-access cannot choose between multiple withdrawals.");
                result = candidate;
            }
            return result ?? throw new InvalidOperationException(
                "Restore-access requires one authority-owned withdrawal event.");
        }

        private static EndogenousAccessRemedyApplicationTrace FindTrace(
            EndogenousDocketState state,
            string traceId)
        {
            for (int i = 0; i < state.AccessRemedyApplicationTraces.Count; i++)
                if (string.Equals(
                        state.AccessRemedyApplicationTraces[i].TraceId,
                        traceId,
                        StringComparison.Ordinal))
                    return state.AccessRemedyApplicationTraces[i];
            return null;
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
