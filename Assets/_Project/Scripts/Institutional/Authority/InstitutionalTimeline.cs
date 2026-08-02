using System;

namespace Desk42.Institutional
{
    /// <summary>
    /// Owns public causal-timeline projection and public-record lookups. Scenario
    /// definitions supply opaque identifiers; they do not write report rows.
    /// </summary>
    internal static class InstitutionalTimeline
    {
        internal static void Add(
            InstitutionalConsequenceReport report,
            long cycle,
            InstitutionalTimelineKind kind,
            string causeId,
            string subjectId,
            string detailId)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            report.Timeline.Add(new InstitutionalTimelineEntry
            {
                EntryId = $"timeline:{cycle}:{report.Timeline.Count}:{kind}",
                Cycle = cycle,
                Kind = kind,
                CauseId = causeId,
                SubjectId = subjectId,
                DetailId = detailId,
            });
        }

        internal static ObservedAgentAction FindObservedAction(
            InstitutionalConsequenceReport report,
            string actionEventId)
        {
            if (report == null || string.IsNullOrEmpty(actionEventId)) return null;
            for (int i = 0; i < report.ObservedAgentActions.Count; i++)
            {
                ObservedAgentAction action = report.ObservedAgentActions[i];
                if (string.Equals(action.ActionEventId, actionEventId,
                    StringComparison.Ordinal)) return action;
            }
            return null;
        }

        internal static DescendantCase FindDescendantCase(
            InstitutionalConsequenceReport report,
            string caseId)
        {
            if (report == null || string.IsNullOrEmpty(caseId)) return null;
            for (int i = 0; i < report.DescendantCases.Count; i++)
            {
                DescendantCase descendant = report.DescendantCases[i];
                if (string.Equals(descendant.CaseId, caseId,
                    StringComparison.Ordinal)) return descendant;
            }
            return null;
        }
    }
}
