using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional
{
    /// <summary>
    /// Detached trace of one scenario-template match. It describes the projection
    /// boundary without exposing mutable evidence or scenario objects.
    /// </summary>
    internal sealed class ScenarioEvidenceProjectionRecord
    {
        internal ScenarioEvidenceProjectionRecord(
            string sourceEventId,
            string evidenceTemplateId,
            string artifactId,
            bool added)
        {
            SourceEventId = sourceEventId;
            EvidenceTemplateId = evidenceTemplateId;
            ArtifactId = artifactId;
            Added = added;
        }

        internal string SourceEventId { get; }
        internal string EvidenceTemplateId { get; }
        internal string ArtifactId { get; }
        internal bool Added { get; }
    }

    /// <summary>
    /// Deterministically ordered event-to-artifact projection result.
    /// </summary>
    internal sealed class ScenarioEvidenceProjectionResult
    {
        internal ScenarioEvidenceProjectionResult(
            IReadOnlyList<ScenarioEvidenceProjectionRecord> records)
        {
            Records = new ReadOnlyCollection<ScenarioEvidenceProjectionRecord>(
                new List<ScenarioEvidenceProjectionRecord>(records));
        }

        internal IReadOnlyList<ScenarioEvidenceProjectionRecord> Records { get; }
    }

    /// <summary>
    /// Projects frozen society events through declarative evidence templates. The
    /// caller supplies a definition that has passed scenario validation. This phase
    /// owns evidence entry only: it never scores, adjudicates, or mutates society.
    /// </summary>
    internal static class InstitutionalScenarioEvidenceProjector
    {
        private sealed class PendingProjection
        {
            internal SocietyEvent SocietyEvent;
            internal ScenarioEvidenceTemplateDefinition Template;
            internal ScenarioCaseDefinition Case;
            internal string ArtifactId;
        }

        internal static ScenarioEvidenceProjectionResult Project(
            InstitutionalConsequenceRun run,
            InstitutionalScenarioDefinition definition,
            SimulationStepResult step)
        {
            return Project(run, definition, step, roleAgentIds: null);
        }

        internal static ScenarioEvidenceProjectionResult Project(
            InstitutionalConsequenceRun run,
            InstitutionalScenarioDefinition definition,
            SimulationStepResult step,
            IReadOnlyDictionary<string, string> roleAgentIds)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.Report == null)
                throw new InvalidOperationException("Evidence projection requires a report.");
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (step.Events == null)
                throw new InvalidOperationException("A simulation step requires an event collection.");

            List<PendingProjection> pending = BuildPendingProjections(
                run,
                definition,
                step,
                roleAgentIds);
            var records = new List<ScenarioEvidenceProjectionRecord>(pending.Count);
            for (int i = 0; i < pending.Count; i++)
            {
                PendingProjection item = pending[i];
                EvidenceArtifact existing = FindArtifact(run.Report, item.ArtifactId);
                if (existing != null)
                {
                    RequireEquivalent(existing, item);
                    records.Add(new ScenarioEvidenceProjectionRecord(
                        item.SocietyEvent.EventId,
                        item.Template.EvidenceTemplateId,
                        item.ArtifactId,
                        false));
                    continue;
                }

                EvidenceArtifact artifact = CreateArtifact(item);
                bool added = InstitutionalEvidencePipeline.Add(run, artifact);
                if (!added)
                {
                    throw new InvalidOperationException(
                        $"Evidence artifact '{artifact.ArtifactId}' collided during projection.");
                }

                if (item.SocietyEvent.Kind == SocietyEventKind.EvidenceDisclosed)
                {
                    InstitutionalEvidencePipeline.LinkToAuthoritativeBelief(
                        run,
                        item.SocietyEvent,
                        artifact,
                        "observation.agent-representation");
                }

                records.Add(new ScenarioEvidenceProjectionRecord(
                    item.SocietyEvent.EventId,
                    item.Template.EvidenceTemplateId,
                    item.ArtifactId,
                    true));
            }

            return new ScenarioEvidenceProjectionResult(records);
        }

        private static List<PendingProjection> BuildPendingProjections(
            InstitutionalConsequenceRun run,
            InstitutionalScenarioDefinition definition,
            SimulationStepResult step,
            IReadOnlyDictionary<string, string> roleAgentIds)
        {
            var casesById = new Dictionary<string, ScenarioCaseDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Cases.Count; i++)
            {
                ScenarioCaseDefinition caseDefinition = definition.Cases[i] ??
                    throw new InvalidOperationException("Scenario cases cannot contain null entries.");
                if (!casesById.TryAdd(caseDefinition.CaseId, caseDefinition))
                {
                    throw new InvalidOperationException(
                        $"Duplicate scenario case id '{caseDefinition.CaseId}'.");
                }
            }

            var events = new List<SocietyEvent>(step.Events.Count);
            for (int i = 0; i < step.Events.Count; i++)
            {
                SocietyEvent societyEvent = step.Events[i] ??
                    throw new InvalidOperationException("Simulation events cannot contain null entries.");
                if (string.IsNullOrWhiteSpace(societyEvent.EventId))
                    throw new InvalidOperationException("Every projected event requires a stable id.");
                events.Add(societyEvent);
            }
            events.Sort(CompareEvents);

            var templates = new List<ScenarioEvidenceTemplateDefinition>(
                definition.EvidenceTemplates.Count);
            for (int i = 0; i < definition.EvidenceTemplates.Count; i++)
            {
                ScenarioEvidenceTemplateDefinition template = definition.EvidenceTemplates[i] ??
                    throw new InvalidOperationException("Evidence templates cannot contain null entries.");
                if (string.IsNullOrWhiteSpace(template.EvidenceTemplateId))
                    throw new InvalidOperationException("Every evidence template requires a stable id.");
                templates.Add(template);
            }
            templates.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.EvidenceTemplateId,
                right.EvidenceTemplateId));

            var result = new List<PendingProjection>();
            var artifactIds = new HashSet<string>(StringComparer.Ordinal);
            for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                SocietyEvent societyEvent = events[eventIndex];
                for (int templateIndex = 0; templateIndex < templates.Count; templateIndex++)
                {
                    ScenarioEvidenceTemplateDefinition template = templates[templateIndex];
                    if (!Matches(template, societyEvent)) continue;
                    if (!casesById.TryGetValue(template.CaseId, out ScenarioCaseDefinition caseDefinition))
                    {
                        throw new InvalidOperationException(
                            $"Evidence template '{template.EvidenceTemplateId}' references " +
                            $"unknown case '{template.CaseId}'.");
                    }
                    if (!InstitutionalScenarioLookup.CaseIsActive(
                            definition,
                            run.Report,
                            caseDefinition,
                            societyEvent.Tick) &&
                        !IsExactTriggerEvidence(
                            run,
                            definition,
                            roleAgentIds,
                            caseDefinition.CaseId,
                            template,
                            societyEvent))
                    {
                        continue;
                    }

                    string artifactId = ArtifactId(societyEvent.EventId, template.EvidenceTemplateId);
                    if (!artifactIds.Add(artifactId))
                    {
                        throw new InvalidOperationException(
                            $"Ambiguous evidence-template matches would create duplicate artifact " +
                            $"'{artifactId}'.");
                    }
                    result.Add(new PendingProjection
                    {
                        SocietyEvent = societyEvent,
                        Template = template,
                        Case = caseDefinition,
                        ArtifactId = artifactId,
                    });
                }
            }
            return result;
        }

        private static bool IsExactTriggerEvidence(
            InstitutionalConsequenceRun run,
            InstitutionalScenarioDefinition definition,
            IReadOnlyDictionary<string, string> roleAgentIds,
            string caseId,
            ScenarioEvidenceTemplateDefinition template,
            SocietyEvent societyEvent)
        {
            ScenarioActionCausedDescendantCaseDefinition descendant = null;
            ScenarioEvidenceActivatedCaseDefinition activation = null;
            int descendantMatches = 0;
            int activationMatches = 0;
            for (int i = 0; i < definition.DescendantCases.Count; i++)
            {
                ScenarioActionCausedDescendantCaseDefinition candidate =
                    definition.DescendantCases[i];
                if (candidate == null || !string.Equals(
                        candidate.CaseId,
                        caseId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                descendant = candidate;
                descendantMatches++;
            }
            for (int i = 0; i < definition.EvidenceActivatedCases.Count; i++)
            {
                ScenarioEvidenceActivatedCaseDefinition candidate =
                    definition.EvidenceActivatedCases[i];
                if (candidate == null || !string.Equals(
                        candidate.CaseId,
                        caseId,
                        StringComparison.Ordinal)) continue;
                activation = candidate;
                activationMatches++;
            }
            if (descendantMatches > 1 || activationMatches > 1 ||
                (descendantMatches == 1 && activationMatches == 1))
            {
                throw new InvalidOperationException(
                    $"Case '{caseId}' has ambiguous trigger declarations.");
            }
            if (activation != null)
            {
                return InstitutionalEvidenceActivatedCaseService
                    .IsExactDeclaredTriggerEvent(
                        run,
                        activation,
                        template,
                        societyEvent);
            }
            return descendant != null &&
                   InstitutionalActionCausedDescendantCaseService
                       .IsExactDeclaredTriggerEvent(
                           run,
                           descendant,
                           roleAgentIds,
                           societyEvent);
        }

        private static bool Matches(
            ScenarioEvidenceTemplateDefinition template,
            SocietyEvent societyEvent)
        {
            return template.SourceEventKind == societyEvent.Kind &&
                   MatchesOptional(template.SourceOpportunityId, societyEvent.OpportunityId) &&
                   MatchesOptional(
                       template.RequiredPropositionId,
                       societyEvent.EvidencePropositionId);
        }

        private static bool MatchesOptional(string requiredValue, string actualValue)
        {
            return string.IsNullOrWhiteSpace(requiredValue) ||
                   string.Equals(requiredValue, actualValue, StringComparison.Ordinal);
        }

        private static EvidenceArtifact CreateArtifact(PendingProjection item)
        {
            string propositionId = ResolvePropositionId(
                item.SocietyEvent,
                item.Template);
            EvidenceArtifact artifact = InstitutionalEvidencePipeline.FromAction(
                item.SocietyEvent,
                item.Template.CaseId,
                item.Template.IssueId,
                EvidenceArtifactKind.ActionRecord,
                propositionId,
                item.Template.Effect,
                item.Template.Weight,
                item.Case.InitialRulingCycle,
                item.Template.EvidenceClassId);
            artifact.ArtifactId = item.ArtifactId;
            artifact.SourceTemplateId = item.Template.EvidenceTemplateId;
            artifact.Provenance.ProvenanceId = ProvenanceId(
                item.SocietyEvent.EventId,
                item.Template.EvidenceTemplateId);
            artifact.Provenance.Visibility = item.Template.Visibility;
            return artifact;
        }

        private static EvidenceArtifact FindArtifact(
            InstitutionalConsequenceReport report,
            string artifactId)
        {
            EvidenceArtifact matched = null;
            int matches = 0;
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                EvidenceArtifact artifact = report.EvidenceArtifacts[i];
                if (artifact != null && string.Equals(
                        artifact.ArtifactId,
                        artifactId,
                        StringComparison.Ordinal))
                {
                    matched = artifact;
                    matches++;
                }
            }
            if (matches > 1)
            {
                throw new InvalidOperationException(
                    $"Evidence artifact id '{artifactId}' is duplicated in the report.");
            }
            return matched;
        }

        private static void RequireEquivalent(
            EvidenceArtifact existing,
            PendingProjection pending)
        {
            string expectedProposition = ResolvePropositionId(
                pending.SocietyEvent,
                pending.Template);
            bool equivalent =
                string.Equals(existing.CaseId, pending.Template.CaseId, StringComparison.Ordinal) &&
                string.Equals(existing.IssueId, pending.Template.IssueId, StringComparison.Ordinal) &&
                string.Equals(existing.EvidenceClassId, pending.Template.EvidenceClassId,
                    StringComparison.Ordinal) &&
                string.Equals(existing.SourceTemplateId,
                    pending.Template.EvidenceTemplateId,
                    StringComparison.Ordinal) &&
                string.Equals(existing.PropositionId, expectedProposition, StringComparison.Ordinal) &&
                existing.Kind == EvidenceArtifactKind.ActionRecord &&
                existing.Effect == pending.Template.Effect &&
                existing.BaseWeight == pending.Template.Weight &&
                existing.Reliability == (pending.SocietyEvent.EvidenceReliability > 0
                    ? pending.SocietyEvent.EvidenceReliability
                    : 100) &&
                existing.OfficiallySubmitted &&
                string.Equals(existing.SuppressedByAgentId,
                    pending.SocietyEvent.EvidenceSuppressedByAgentId,
                    StringComparison.Ordinal) &&
                ContainsOnly(existing.KnownByAgentIds, pending.SocietyEvent.ActorId) &&
                existing.EnteredCycle == pending.SocietyEvent.Tick &&
                existing.EnteredAfterInitialRuling ==
                    (pending.SocietyEvent.Tick > pending.Case.InitialRulingCycle) &&
                existing.Provenance != null &&
                existing.Provenance.CreatedCycle == pending.SocietyEvent.Tick &&
                string.Equals(existing.Provenance.SourceAgentId,
                    pending.SocietyEvent.ActorId, StringComparison.Ordinal) &&
                string.Equals(existing.Provenance.SourceDecisionId,
                    pending.SocietyEvent.CauseDecisionId, StringComparison.Ordinal) &&
                string.Equals(
                    existing.Provenance.SourceSocietyEventId,
                    pending.SocietyEvent.EventId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    existing.Provenance.ProvenanceId,
                    ProvenanceId(
                        pending.SocietyEvent.EventId,
                    pending.Template.EvidenceTemplateId),
                    StringComparison.Ordinal) &&
                string.Equals(existing.Provenance.SourceRecordId,
                    pending.SocietyEvent.EvidenceSourceId ?? pending.SocietyEvent.EvidenceId,
                    StringComparison.Ordinal) &&
                existing.Provenance.Visibility == pending.Template.Visibility &&
                existing.Provenance.CreatedByAgentAction &&
                SequenceEquals(
                    existing.Provenance.ChainOfCustodyIds,
                    pending.SocietyEvent.CauseDecisionId,
                    pending.SocietyEvent.EventId);
            if (!equivalent)
            {
                throw new InvalidOperationException(
                    $"Evidence artifact id '{pending.ArtifactId}' is already owned by a " +
                    "different event-template projection.");
            }
        }

        private static bool ContainsOnly(List<string> values, string expected)
        {
            return values != null && values.Count == 1 &&
                   string.Equals(values[0], expected, StringComparison.Ordinal);
        }

        private static string ResolvePropositionId(
            SocietyEvent societyEvent,
            ScenarioEvidenceTemplateDefinition template)
        {
            if (!string.IsNullOrWhiteSpace(societyEvent.EvidencePropositionId))
                return societyEvent.EvidencePropositionId;
            if (!string.IsNullOrWhiteSpace(template.RequiredPropositionId))
                return template.RequiredPropositionId;
            if (!string.IsNullOrWhiteSpace(societyEvent.EvidenceId))
                return societyEvent.EvidenceId;
            if (!string.IsNullOrWhiteSpace(societyEvent.EvidenceSourceId))
                return societyEvent.EvidenceSourceId;
            return $"proposition:{template.EvidenceTemplateId}";
        }

        private static bool SequenceEquals(
            List<string> values,
            string first,
            string second)
        {
            return values != null && values.Count == 2 &&
                   string.Equals(values[0], first, StringComparison.Ordinal) &&
                   string.Equals(values[1], second, StringComparison.Ordinal);
        }

        private static int CompareEvents(SocietyEvent left, SocietyEvent right)
        {
            int tick = left.Tick.CompareTo(right.Tick);
            return tick != 0
                ? tick
                : StringComparer.Ordinal.Compare(left.EventId, right.EventId);
        }

        private static string ArtifactId(string eventId, string templateId)
        {
            return $"artifact:{eventId}:{templateId}";
        }

        private static string ProvenanceId(string eventId, string templateId)
        {
            return $"provenance:{eventId}:{templateId}";
        }
    }
}
