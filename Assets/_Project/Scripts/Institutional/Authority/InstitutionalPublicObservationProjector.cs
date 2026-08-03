using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Publishes assessor-owned consequence snapshots only when their declared
    /// observation cycle arrives. This phase runs last so future public information
    /// cannot influence decisions or institutional transitions in an earlier pulse.
    /// </summary>
    internal static class InstitutionalPublicObservationProjector
    {
        internal static void ProjectDueReliance(
            InstitutionalConsequenceRun run,
            long cycle)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.Report == null || run.RelianceLedger == null ||
                run.PendingReliancePublicProjections == null)
            {
                throw new InvalidOperationException(
                    "Public observation projection requires initialized run state.");
            }

            HashSet<string> reservedPublicIds =
                CaptureExistingPublicIds(run.Report);
            var due = new List<PendingReliancePublicProjection>();
            var remaining = new List<PendingReliancePublicProjection>();
            for (int i = 0; i < run.PendingReliancePublicProjections.Count; i++)
            {
                PendingReliancePublicProjection pending =
                    run.PendingReliancePublicProjections[i];
                if (pending?.Observation == null)
                {
                    throw new InvalidOperationException(
                        "A pending reliance projection has no public observation.");
                }
                ValidateAgainstAuthority(run, pending);
                if (string.IsNullOrWhiteSpace(pending.RelianceEventId) ||
                    string.IsNullOrWhiteSpace(pending.Observation.ObservationId) ||
                    !reservedPublicIds.Add(
                        pending.Observation.ObservationId) ||
                    pending.MaterialConsequences == null ||
                    pending.MaterialConsequences.Count == 0)
                {
                    throw new InvalidOperationException(
                        "A pending reliance projection is incomplete or duplicates " +
                        "another public node.");
                }
                for (int materialIndex = 0;
                     materialIndex < pending.MaterialConsequences.Count;
                     materialIndex++)
                {
                    MaterialConsequence material =
                        pending.MaterialConsequences[materialIndex];
                    if (material == null ||
                        material.Cycle != pending.Observation.Cycle ||
                        string.IsNullOrWhiteSpace(material.ConsequenceId) ||
                        !reservedPublicIds.Add(material.ConsequenceId))
                    {
                        throw new InvalidOperationException(
                            $"Reliance observation " +
                            $"'{pending.Observation.ObservationId}' has an invalid " +
                            "or duplicate material projection.");
                    }
                }
                if (pending.Observation.Cycle < cycle)
                {
                    throw new InvalidOperationException(
                        $"Reliance observation '{pending.Observation.ObservationId}' " +
                        "passed its public projection cycle.");
                }
                if (pending.Observation.Cycle == cycle) due.Add(pending);
                else remaining.Add(pending);
            }
            if (due.Count == 0) return;

            due.Sort((left, right) => string.CompareOrdinal(
                left.Observation.ObservationId,
                right.Observation.ObservationId));

            var timelineEntries = new List<InstitutionalTimelineEntry>(due.Count);
            for (int i = 0; i < due.Count; i++)
            {
                PendingReliancePublicProjection pending = due[i];
                RelianceObservation observation = pending.Observation;
                for (int j = 0; j < pending.MaterialConsequences.Count; j++)
                {
                    MaterialConsequence material = pending.MaterialConsequences[j];
                    if (material.Cycle != cycle)
                    {
                        throw new InvalidOperationException(
                            $"Reliance observation '{observation.ObservationId}' has an " +
                            "invalid or duplicate material projection.");
                    }
                }

                var timeline = new InstitutionalTimelineEntry
                {
                    EntryId =
                        InstitutionalScenarioDerivedIds.DeferredRelianceTimeline(
                            cycle,
                            pending.RelianceEventId),
                    Cycle = cycle,
                    Kind = InstitutionalTimelineKind.RelianceCreated,
                    CauseId = observation.SourceActionEventId,
                    SubjectId = observation.AgentId,
                    DetailId = observation.ObservationId,
                };
                if (!reservedPublicIds.Add(timeline.EntryId))
                {
                    throw new InvalidOperationException(
                        $"Institutional timeline id '{timeline.EntryId}' already exists.");
                }
                timelineEntries.Add(timeline);
            }

            // Every row and identifier is validated before the public surface changes.
            for (int i = 0; i < due.Count; i++)
            {
                run.Report.MaterialConsequences.AddRange(
                    due[i].MaterialConsequences);
                run.Report.RelianceObservations.Add(due[i].Observation);
                run.Report.Timeline.Add(timelineEntries[i]);
            }
            run.PendingReliancePublicProjections.Clear();
            run.PendingReliancePublicProjections.AddRange(remaining);
        }

        /// <summary>
        /// Confirms that a reliance event has crossed the public-projection boundary
        /// as one exact observation, its complete authority-owned material set, and
        /// one matching timeline row. Recovery creation uses this narrower contract
        /// instead of treating the presence of an observation id as publication.
        /// </summary>
        internal static bool HasExactPublishedRelianceProjection(
            InstitutionalConsequenceRun run,
            RelianceEvent reliance)
        {
            if (run?.Report == null || reliance == null ||
                run.RelianceLedger == null ||
                run.PendingReliancePublicProjections == null ||
                run.Report.RelianceObservations == null ||
                run.Report.MaterialConsequences == null ||
                run.Report.Timeline == null ||
                reliance.AppliedEffects == null ||
                reliance.AppliedEffects.Count == 0)
            {
                return false;
            }

            int authorityCount = 0;
            for (int i = 0; i < run.RelianceLedger.Count; i++)
            {
                RelianceEvent candidate = run.RelianceLedger[i];
                if (string.Equals(
                        candidate?.RelianceEventId,
                        reliance.RelianceEventId,
                        StringComparison.Ordinal))
                {
                    if (!ReferenceEquals(candidate, reliance)) return false;
                    authorityCount++;
                }
            }
            if (authorityCount != 1) return false;

            for (int i = 0; i < run.PendingReliancePublicProjections.Count; i++)
            {
                PendingReliancePublicProjection pending =
                    run.PendingReliancePublicProjections[i];
                if (string.Equals(
                        pending?.Observation?.ObservationId,
                        reliance.PublicObservationId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        pending?.RelianceEventId,
                        reliance.RelianceEventId,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            RelianceObservation observation = null;
            int observationCount = 0;
            for (int i = 0; i < run.Report.RelianceObservations.Count; i++)
            {
                RelianceObservation candidate = run.Report.RelianceObservations[i];
                if (!string.Equals(
                        candidate?.ObservationId,
                        reliance.PublicObservationId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                observation = candidate;
                observationCount++;
            }
            if (observationCount != 1 || observation == null ||
                observation.Cycle != reliance.PublicObservationCycle ||
                !string.Equals(observation.AgentId, reliance.AgentId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.EnablingRulingId,
                    reliance.ReliedOnRulingId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.EnablingMutationId,
                    reliance.ReliedOnMutationId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.SourceActionEventId,
                    reliance.SourceActionEventId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.RecordedChoiceId,
                    reliance.RecordedChoiceId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.AbandonedAlternativeId,
                    reliance.AbandonedAlternativeId,
                    StringComparison.Ordinal) ||
                !string.Equals(observation.ResourceId, reliance.ResourceId,
                    StringComparison.Ordinal) ||
                observation.RecordedResourceDelta != -reliance.ResourceSpent)
            {
                return false;
            }

            var effectsByMaterialId = new Dictionary<
                string,
                RelianceAppliedEffect>(StringComparer.Ordinal);
            for (int i = 0; i < reliance.AppliedEffects.Count; i++)
            {
                RelianceAppliedEffect effect = reliance.AppliedEffects[i];
                if (effect == null ||
                    string.IsNullOrWhiteSpace(effect.MaterialConsequenceId) ||
                    !effectsByMaterialId.TryAdd(
                        effect.MaterialConsequenceId,
                        effect))
                {
                    return false;
                }
            }

            var matchedMaterials = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < run.Report.MaterialConsequences.Count; i++)
            {
                MaterialConsequence material =
                    run.Report.MaterialConsequences[i];
                if (material == null) return false;
                bool ownedId = effectsByMaterialId.TryGetValue(
                    material.ConsequenceId,
                    out RelianceAppliedEffect effect);
                bool ownedCause = string.Equals(
                    material.CauseId,
                    reliance.SourceActionEventId,
                    StringComparison.Ordinal);
                if (!ownedId && !ownedCause) continue;
                if (!ownedId || !ownedCause ||
                    !matchedMaterials.Add(material.ConsequenceId) ||
                    material.Cycle != reliance.PublicObservationCycle ||
                    !string.Equals(material.AgentId, effect.AgentId,
                        StringComparison.Ordinal) ||
                    material.ResourceDelta !=
                        effect.ResourceAfter - effect.ResourceBefore ||
                    material.Kind != effect.MaterialKind ||
                    !string.Equals(material.KindId, effect.MaterialKindId,
                        StringComparison.Ordinal) ||
                    !string.Equals(material.ResourceId, effect.ResourceId,
                        StringComparison.Ordinal) ||
                    material.HasNeedEffect != effect.HasNeedEffect ||
                    (effect.HasNeedEffect &&
                     (material.Need != effect.Need ||
                      material.NeedPressureBefore != effect.NeedPressureBefore ||
                      material.NeedPressureAfter != effect.NeedPressureAfter)))
                {
                    return false;
                }
            }
            if (matchedMaterials.Count != effectsByMaterialId.Count) return false;

            int exactTimelineCount = 0;
            for (int i = 0; i < run.Report.Timeline.Count; i++)
            {
                InstitutionalTimelineEntry entry = run.Report.Timeline[i];
                if (entry == null) return false;
                bool mentionsProjection =
                    entry.Kind == InstitutionalTimelineKind.RelianceCreated &&
                    (string.Equals(
                         entry.DetailId,
                         reliance.PublicObservationId,
                         StringComparison.Ordinal) ||
                     string.Equals(
                         entry.CauseId,
                         reliance.SourceActionEventId,
                         StringComparison.Ordinal));
                if (!mentionsProjection) continue;
                if (entry.Cycle != reliance.PublicObservationCycle ||
                    !string.Equals(
                        entry.CauseId,
                        reliance.SourceActionEventId,
                        StringComparison.Ordinal) ||
                    !string.Equals(entry.SubjectId, reliance.AgentId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        entry.DetailId,
                        reliance.PublicObservationId,
                        StringComparison.Ordinal))
                {
                    return false;
                }
                exactTimelineCount++;
            }
            return exactTimelineCount == 1;
        }

        private static void ValidateAgainstAuthority(
            InstitutionalConsequenceRun run,
            PendingReliancePublicProjection pending)
        {
            RelianceEvent reliance = null;
            int relianceCount = 0;
            for (int i = 0; i < run.RelianceLedger.Count; i++)
            {
                RelianceEvent candidate = run.RelianceLedger[i];
                if (!string.Equals(
                        candidate?.RelianceEventId,
                        pending.RelianceEventId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                reliance = candidate;
                relianceCount++;
            }

            RelianceObservation observation = pending.Observation;
            if (relianceCount != 1 || reliance?.AppliedEffects == null ||
                !string.Equals(
                    observation.ObservationId,
                    reliance.PublicObservationId,
                    StringComparison.Ordinal) ||
                observation.Cycle != reliance.PublicObservationCycle ||
                !string.Equals(
                    observation.AgentId,
                    reliance.AgentId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.EnablingRulingId,
                    reliance.ReliedOnRulingId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.EnablingMutationId,
                    reliance.ReliedOnMutationId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.SourceActionEventId,
                    reliance.SourceActionEventId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.AbandonedAlternativeId,
                    reliance.AbandonedAlternativeId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.RecordedChoiceId,
                    reliance.RecordedChoiceId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    observation.ResourceId,
                    reliance.ResourceId,
                    StringComparison.Ordinal) ||
                observation.RecordedResourceDelta != -reliance.ResourceSpent ||
                pending.MaterialConsequences == null ||
                pending.MaterialConsequences.Count != reliance.AppliedEffects.Count)
            {
                throw new InvalidOperationException(
                    $"Pending reliance projection '{observation.ObservationId}' " +
                    "does not match its authoritative event.");
            }

            var effectsByMaterialId = new Dictionary<
                string,
                RelianceAppliedEffect>(StringComparer.Ordinal);
            for (int i = 0; i < reliance.AppliedEffects.Count; i++)
            {
                RelianceAppliedEffect effect = reliance.AppliedEffects[i];
                if (effect == null ||
                    string.IsNullOrWhiteSpace(effect.MaterialConsequenceId) ||
                    !effectsByMaterialId.TryAdd(
                        effect.MaterialConsequenceId,
                        effect))
                {
                    throw new InvalidOperationException(
                        $"Authoritative reliance '{reliance.RelianceEventId}' has " +
                        "invalid applied-effect links.");
                }
            }

            var projectedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < pending.MaterialConsequences.Count; i++)
            {
                MaterialConsequence material = pending.MaterialConsequences[i];
                if (material == null ||
                    string.IsNullOrWhiteSpace(material.ConsequenceId) ||
                    !projectedIds.Add(material.ConsequenceId) ||
                    !effectsByMaterialId.TryGetValue(
                        material.ConsequenceId,
                        out RelianceAppliedEffect effect) ||
                    !string.Equals(
                        material.CauseId,
                        reliance.SourceActionEventId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        material.AgentId,
                        effect.AgentId,
                        StringComparison.Ordinal) ||
                    material.ResourceDelta !=
                        effect.ResourceAfter - effect.ResourceBefore ||
                    material.Kind != effect.MaterialKind ||
                    !string.Equals(
                        material.KindId,
                        effect.MaterialKindId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        material.ResourceId,
                        effect.ResourceId,
                        StringComparison.Ordinal) ||
                    material.HasNeedEffect != effect.HasNeedEffect ||
                    (effect.HasNeedEffect &&
                     (material.Need != effect.Need ||
                      material.NeedPressureBefore != effect.NeedPressureBefore ||
                      material.NeedPressureAfter != effect.NeedPressureAfter)))
                {
                    throw new InvalidOperationException(
                        $"Pending reliance projection '{observation.ObservationId}' " +
                        "contains a material row not owned by its authority event.");
                }
            }
        }

        internal static HashSet<string> CaptureExistingPublicIds(
            InstitutionalConsequenceReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var result = new HashSet<string>(StringComparer.Ordinal);
            AddIds(result, report.ObservedAgentActions,
                value => value.ActionEventId, "observed action");
            AddIds(result, report.EvidenceArtifacts,
                value => value.ArtifactId, "evidence artifact");
            AddIds(result, report.OfficialFindings,
                value => value.FindingId, "official finding");
            AddIds(result, report.Rulings,
                value => value.RulingId, "ruling");
            AddIds(result, report.OfficialStatusMutations,
                value => value.MutationId, "official mutation");
            AddIds(result, report.CaseOpenings,
                value => value.ActivationId, "case opening");
            AddIds(result, report.DescendantCases,
                value => value.CaseId, "descendant case");
            AddIds(result, report.Appeals,
                value => value.AppealId, "appeal");
            AddIds(result, report.Holdings,
                value => value.HoldingId, "holding");
            AddIds(result, report.Holdings,
                value => value.Scope?.ScopeId, "holding scope");
            AddIds(result, report.RelianceObservations,
                value => value.ObservationId, "reliance observation");
            AddIds(result, report.MaterialConsequences,
                value => value.ConsequenceId, "material consequence");
            AddIds(result, report.ConnectedOutcomes,
                value => value.PairId, "connected outcome");
            AddIds(result, report.ExclusiveEntitlements,
                value => value.EntitlementId, "exclusive entitlement");
            AddIds(result, report.WorkAllocations,
                value => value.AllocationId, "work allocation");
            AddIds(result, report.Timeline,
                value => value.EntryId, "timeline entry");
            return result;
        }

        private static void AddIds<T>(
            ISet<string> destination,
            IReadOnlyList<T> rows,
            Func<T, string> id,
            string label)
            where T : class
        {
            if (rows == null)
            {
                throw new InvalidOperationException(
                    $"The public report has no {label} collection.");
            }
            for (int i = 0; i < rows.Count; i++)
            {
                string value = rows[i] == null ? null : id(rows[i]);
                if (string.IsNullOrWhiteSpace(value) ||
                    !destination.Add(value))
                {
                    throw new InvalidOperationException(
                        $"The public report contains an invalid or globally reused " +
                        $"{label} id.");
                }
            }
        }
    }
}
