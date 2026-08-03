using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Executes collective recognition against every member of the material
    /// commitment that produced the case. The trace makes replay exact-once and
    /// keeps the group effect attributable to the committed ruling.
    /// </summary>
    internal static class EndogenousCollectiveRemedyEffectService
    {
        internal const string CollectiveRecognitionStatusPrefix =
            "status.collective-recognised:";

        internal static EndogenousCollectiveRemedyApplicationTrace Execute(
            SocietyState society,
            InstitutionalMaterialWorld world,
            EndogenousDocketState docket,
            CommittedPlayerRuling ruling)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (docket == null) throw new ArgumentNullException(nameof(docket));
            if (ruling == null) throw new ArgumentNullException(nameof(ruling));

            EndogenousInstitutionalCase opened = docket.GetCase(ruling.CaseId) ??
                throw new InvalidOperationException(
                    "The collective remedy requires its committed case.");
            if (!string.Equals(opened.IssueId,
                    EndogenousIssueKindIds.CollectiveGrievance,
                    StringComparison.Ordinal) ||
                !EstablishesRecognition(ruling.Disposition) ||
                !Contains(ruling.RemedyDefinitionIds,
                    EndogenousPlayerRulingService.RecogniseCollectiveRemedy))
            {
                return null;
            }

            string traceId = "collective-remedy:" + ruling.RulingId;
            EndogenousCollectiveRemedyApplicationTrace replay = FindTrace(
                docket, traceId);
            if (replay != null) return replay;

            DocketCandidate candidate = docket.GetDocketCandidate(
                opened.DocketCandidateId) ?? throw new InvalidOperationException(
                    "The collective case has no docket source.");
            IncidentCandidate incident = docket.GetIncident(
                candidate.AuthorityIncidentCandidateId) ??
                throw new InvalidOperationException(
                    "The collective case has no material incident source.");
            CollectiveCommitmentState commitment = FindCommitment(
                world, incident.CauseEventIds) ??
                throw new InvalidOperationException(
                    "The collective case has no continuing group commitment.");

            string statusId = CollectiveRecognitionStatusPrefix +
                commitment.CommitmentId;
            var members = new List<string>(commitment.MemberAgentIds);
            members.Sort(StringComparer.Ordinal);
            var changed = new List<string>();
            for (int i = 0; i < members.Count; i++)
            {
                AgentState member = society.GetAgent(members[i]) ??
                    throw new InvalidOperationException(
                        "The collective remedy references an unknown member.");
                bool before = member.Standing.IsRecognised(statusId);
                member.Standing.SetRecognised(statusId, true);
                if (!before) changed.Add(member.StableId);
            }

            var trace = new EndogenousCollectiveRemedyApplicationTrace
            {
                TraceId = traceId,
                RulingId = ruling.RulingId,
                CaseId = ruling.CaseId,
                AppliedTick = society.CurrentTick,
                CollectiveCommitmentId = commitment.CommitmentId,
                RecognisedStatusId = statusId,
                MemberAgentIds = members,
                ChangedAgentIds = changed,
            };
            docket.CollectiveRemedyApplicationTraces.Add(trace);
            EndogenousDocketValidator.Validate(docket, society);
            return trace;
        }

        private static CollectiveCommitmentState FindCommitment(
            InstitutionalMaterialWorld world,
            IReadOnlyList<string> causeEventIds)
        {
            for (int i = 0; i < causeEventIds.Count; i++)
            {
                MaterialWorldEvent materialEvent = world.GetEvent(causeEventIds[i]);
                if (materialEvent?.Kind !=
                    MaterialWorldEventKind.CollectiveCommitmentChanged) continue;
                CollectiveCommitmentState commitment =
                    world.GetCollectiveCommitment(materialEvent.StateRecordId);
                if (commitment != null) return commitment;
            }
            return null;
        }

        private static EndogenousCollectiveRemedyApplicationTrace FindTrace(
            EndogenousDocketState docket,
            string traceId)
        {
            for (int i = 0;
                 i < docket.CollectiveRemedyApplicationTraces.Count;
                 i++)
                if (string.Equals(
                        docket.CollectiveRemedyApplicationTraces[i].TraceId,
                        traceId,
                        StringComparison.Ordinal))
                    return docket.CollectiveRemedyApplicationTraces[i];
            return null;
        }

        private static bool EstablishesRecognition(RulingDisposition disposition)
        {
            return disposition == RulingDisposition.Recognised ||
                   disposition == RulingDisposition.Affirmed ||
                   disposition == RulingDisposition.ReversedAndRecognised;
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string expected)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
