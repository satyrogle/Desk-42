using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal readonly struct EvidenceEvaluation
    {
        internal readonly int Score;
        internal readonly int MinimumScore;
        internal readonly int MaximumScore;
        internal readonly List<EvidenceArtifact> Evidence;

        internal EvidenceEvaluation(
            int score,
            int minimumScore,
            int maximumScore,
            List<EvidenceArtifact> evidence)
        {
            Score = score;
            MinimumScore = minimumScore;
            MaximumScore = maximumScore;
            Evidence = evidence;
        }
    }

    /// <summary>
    /// Owns evidence construction, provenance projection, frozen case envelopes,
    /// and evidence-score evaluation. It knows no scenario or participant IDs.
    /// </summary>
    internal static class InstitutionalEvidencePipeline
    {
        internal static EvidenceArtifact FromAction(
            SocietyEvent societyEvent,
            string caseId,
            string issueId,
            EvidenceArtifactKind kind,
            string propositionId,
            EvidenceEffect effect,
            int baseWeight,
            long initialRulingCycle,
            string evidenceClassId = null)
        {
            if (societyEvent == null) throw new ArgumentNullException(nameof(societyEvent));
            return new EvidenceArtifact
            {
                ArtifactId = $"artifact:{societyEvent.EventId}",
                CaseId = caseId,
                EnteredCycle = societyEvent.Tick,
                Kind = kind,
                EvidenceClassId = evidenceClassId,
                IssueId = issueId,
                PropositionId = propositionId,
                Effect = effect,
                BaseWeight = baseWeight,
                Reliability = societyEvent.EvidenceReliability > 0
                    ? societyEvent.EvidenceReliability
                    : 100,
                OfficiallySubmitted = true,
                SuppressedByAgentId = societyEvent.EvidenceSuppressedByAgentId,
                KnownByAgentIds = new List<string> { societyEvent.ActorId },
                EnteredAfterInitialRuling = societyEvent.Tick > initialRulingCycle,
                Provenance = new EvidenceProvenance
                {
                    ProvenanceId = $"provenance:{societyEvent.EventId}",
                    CreatedCycle = societyEvent.Tick,
                    SourceAgentId = societyEvent.ActorId,
                    SourceDecisionId = societyEvent.CauseDecisionId,
                    SourceSocietyEventId = societyEvent.EventId,
                    SourceRecordId = societyEvent.EvidenceSourceId ?? societyEvent.EvidenceId,
                    Visibility = societyEvent.Visibility,
                    CreatedByAgentAction = true,
                    ChainOfCustodyIds = new List<string>
                    {
                        societyEvent.CauseDecisionId,
                        societyEvent.EventId,
                    },
                },
            };
        }

        internal static bool Add(
            InstitutionalConsequenceRun run,
            EvidenceArtifact artifact)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (artifact == null) return false;
            for (int i = 0; i < run.Report.EvidenceArtifacts.Count; i++)
            {
                if (string.Equals(run.Report.EvidenceArtifacts[i].ArtifactId,
                    artifact.ArtifactId, StringComparison.Ordinal)) return false;
            }

            run.Report.EvidenceArtifacts.Add(artifact);
            InstitutionalTimeline.FindObservedAction(
                    run.Report,
                    artifact.Provenance.SourceSocietyEventId)?
                .ResultEvidenceArtifactIds.Add(artifact.ArtifactId);
            InstitutionalTimeline.Add(
                run.Report,
                artifact.EnteredCycle,
                InstitutionalTimelineKind.EvidenceEntered,
                artifact.Provenance.SourceSocietyEventId,
                artifact.Provenance.SourceAgentId,
                artifact.ArtifactId);
            return true;
        }

        internal static void LinkToAuthoritativeBelief(
            InstitutionalConsequenceRun run,
            SocietyEvent societyEvent,
            EvidenceArtifact artifact,
            string observationKindId)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (societyEvent == null || artifact == null) return;
            for (int i = 0; i < run.AuthoritativeBeliefLinks.Count; i++)
            {
                AuthoritativeBeliefLink link = run.AuthoritativeBeliefLinks[i];
                if (!string.Equals(link.AgentId, societyEvent.ActorId,
                        StringComparison.Ordinal) ||
                    !string.Equals(link.BeliefId, societyEvent.EvidenceBeliefId,
                        StringComparison.Ordinal)) continue;
                run.AuthoritativeEvidenceLinks.Add(new AuthoritativeEvidenceLink
                {
                    LivedEventId = link.LivedEventId,
                    EvidenceArtifactId = artifact.ArtifactId,
                    ObservationKindId = observationKindId,
                });
                return;
            }
        }

        internal static EvidenceEvaluation Evaluate(
            InstitutionalConsequenceReport report,
            string caseId,
            long maximumCycle,
            InstitutionalPolicyConfiguration policy)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            List<EvidenceArtifact> evidence = ForCase(report, caseId, maximumCycle);
            int score = 0;
            int minimum = 0;
            int maximum = 0;

            for (int i = 0; i < evidence.Count; i++)
            {
                EvidenceArtifact artifact = evidence[i];
                int fullWeight = artifact.BaseWeight * policy.WeightPercent(artifact) / 100;
                int reliableWeight = fullWeight * artifact.Reliability / 100;
                if (artifact.Effect == EvidenceEffect.SupportsFinding)
                {
                    score += reliableWeight;
                    minimum += reliableWeight;
                    maximum += fullWeight;
                }
                else if (artifact.Effect == EvidenceEffect.OpposesFinding)
                {
                    score -= reliableWeight;
                    minimum -= fullWeight;
                    maximum -= reliableWeight;
                }
            }

            return new EvidenceEvaluation(score, minimum, maximum, evidence);
        }

        internal static List<EvidenceArtifact> ForCase(
            InstitutionalConsequenceReport report,
            string caseId,
            long maximumCycle)
        {
            var result = new List<EvidenceArtifact>();
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                EvidenceArtifact artifact = report.EvidenceArtifacts[i];
                if (artifact.EnteredCycle <= maximumCycle &&
                    string.Equals(artifact.CaseId, caseId, StringComparison.Ordinal))
                {
                    result.Add(artifact);
                }
            }
            result.Sort((left, right) => string.CompareOrdinal(
                left.ArtifactId,
                right.ArtifactId));
            return result;
        }

        internal static OfficialFinding CreateFinding(
            string caseId,
            string issueId,
            long cycle,
            string phase,
            FindingDisposition disposition,
            int score,
            int threshold,
            List<EvidenceArtifact> evidence)
        {
            return new OfficialFinding
            {
                FindingId = $"finding:{caseId}:{phase}:{cycle}",
                CaseId = caseId,
                Cycle = cycle,
                IssueId = issueId,
                Disposition = disposition,
                WeightedEvidenceScore = score,
                RequiredScore = threshold,
                EvidenceArtifactIds = CopyIds(evidence),
            };
        }

        internal static List<string> CopyIds(List<EvidenceArtifact> evidence)
        {
            var ids = new List<string>(evidence.Count);
            for (int i = 0; i < evidence.Count; i++) ids.Add(evidence[i].ArtifactId);
            return ids;
        }

        internal static bool ContainsKind(
            List<EvidenceArtifact> evidence,
            EvidenceArtifactKind kind)
        {
            for (int i = 0; i < evidence.Count; i++)
                if (evidence[i].Kind == kind) return true;
            return false;
        }

        internal static EvidenceArtifact FindByResource(
            InstitutionalConsequenceReport report,
            string resourceId)
        {
            if (report == null || string.IsNullOrEmpty(resourceId)) return null;
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                if (string.Equals(report.EvidenceArtifacts[i].OfficialResourceId,
                    resourceId, StringComparison.Ordinal))
                {
                    return report.EvidenceArtifacts[i];
                }
            }
            return null;
        }
    }
}
