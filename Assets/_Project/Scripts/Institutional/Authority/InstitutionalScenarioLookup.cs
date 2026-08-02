using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// Strict ordinal lookups shared by scenario execution phases. Scenario
    /// validation owns authoring integrity; these checks protect mutable runtime
    /// projections from duplicate or missing authoritative rows.
    /// </summary>
    internal static class InstitutionalScenarioLookup
    {
        internal static bool CaseHasOpened(
            InstitutionalScenarioExecutionContext context,
            ScenarioCaseDefinition caseDefinition)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return CaseIsActive(
                context.Definition,
                context.Run.Report,
                caseDefinition,
                long.MaxValue);
        }

        internal static bool CaseIsActive(
            InstitutionalScenarioDefinition definition,
            InstitutionalConsequenceReport report,
            ScenarioCaseDefinition caseDefinition,
            long cycle)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (caseDefinition == null)
                throw new ArgumentNullException(nameof(caseDefinition));
            if (cycle < caseDefinition.OpenCycle) return false;

            bool isActionCaused = false;
            for (int i = 0; i < definition.DescendantCases.Count; i++)
            {
                ScenarioActionCausedDescendantCaseDefinition descendant =
                    definition.DescendantCases[i];
                if (descendant != null && Equal(descendant.CaseId, caseDefinition.CaseId))
                {
                    isActionCaused = true;
                    break;
                }
            }
            if (!isActionCaused) return true;
            if (report == null) return false;
            if (report.DescendantCases == null)
            {
                throw new InvalidOperationException(
                    "Case activation requires a descendant-case collection.");
            }

            DescendantCase active = null;
            int matches = 0;
            for (int i = 0; i < report.DescendantCases.Count; i++)
            {
                DescendantCase candidate = report.DescendantCases[i];
                if (candidate == null || !Equal(candidate.CaseId, caseDefinition.CaseId))
                    continue;
                active = candidate;
                matches++;
            }
            if (matches > 1)
            {
                throw new InvalidOperationException(
                    $"Descendant case '{caseDefinition.CaseId}' is duplicated in the report.");
            }
            if (active == null) return false;
            if (active.OpenedCycle != caseDefinition.OpenCycle)
            {
                throw new InvalidOperationException(
                    $"Descendant case '{caseDefinition.CaseId}' opened outside its " +
                    "declared cycle.");
            }
            return active.OpenedCycle <= cycle;
        }

        internal static ScenarioCaseDefinition Case(
            InstitutionalScenarioDefinition definition,
            string caseId)
        {
            for (int i = 0; i < definition.Cases.Count; i++)
            {
                if (Equal(definition.Cases[i].CaseId, caseId))
                    return definition.Cases[i];
            }
            throw new InvalidOperationException($"Missing declared case '{caseId}'.");
        }

        internal static ScenarioAppealDefinition Appeal(
            InstitutionalScenarioDefinition definition,
            string appealId)
        {
            for (int i = 0; i < definition.Appeals.Count; i++)
            {
                if (Equal(definition.Appeals[i].AppealId, appealId))
                    return definition.Appeals[i];
            }
            throw new InvalidOperationException($"Missing declared appeal '{appealId}'.");
        }

        internal static ScenarioHoldingDefinition Holding(
            InstitutionalScenarioDefinition definition,
            string holdingId)
        {
            for (int i = 0; i < definition.Holdings.Count; i++)
            {
                if (Equal(definition.Holdings[i].HoldingId, holdingId))
                    return definition.Holdings[i];
            }
            throw new InvalidOperationException($"Missing declared holding '{holdingId}'.");
        }

        internal static ScenarioExclusiveEntitlementDefinition Entitlement(
            InstitutionalScenarioDefinition definition,
            string entitlementId)
        {
            for (int i = 0; i < definition.ExclusiveEntitlements.Count; i++)
            {
                if (Equal(
                        definition.ExclusiveEntitlements[i].EntitlementId,
                        entitlementId)) return definition.ExclusiveEntitlements[i];
            }
            throw new InvalidOperationException(
                $"Missing declared exclusive entitlement '{entitlementId}'.");
        }

        internal static Ruling Ruling(
            InstitutionalConsequenceReport report,
            string rulingId)
        {
            Ruling result = null;
            int count = 0;
            for (int i = 0; i < report.Rulings.Count; i++)
            {
                if (!Equal(report.Rulings[i].RulingId, rulingId)) continue;
                result = report.Rulings[i];
                count++;
            }
            if (count > 1)
                throw new InvalidOperationException($"Duplicate ruling id '{rulingId}'.");
            return result;
        }

        internal static bool TryResolveEvidenceArtifactIds(
            InstitutionalConsequenceReport report,
            IReadOnlyList<string> sourceTemplateIds,
            string caseId,
            long maximumCycle,
            string context,
            out List<string> artifactIds)
        {
            artifactIds = new List<string>();
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.EvidenceArtifacts == null)
                throw new InvalidOperationException($"{context} has no evidence collection.");
            if (sourceTemplateIds == null || sourceTemplateIds.Count == 0)
                throw new InvalidOperationException($"{context} has no template declarations.");
            if (string.IsNullOrWhiteSpace(caseId))
                throw new InvalidOperationException($"{context} has no case envelope.");

            var requestedTemplates = new HashSet<string>(StringComparer.Ordinal);
            var matchesByTemplate = new Dictionary<string, List<EvidenceArtifact>>(
                StringComparer.Ordinal);
            for (int templateIndex = 0;
                 templateIndex < sourceTemplateIds.Count;
                 templateIndex++)
            {
                string templateId = sourceTemplateIds[templateIndex];
                if (string.IsNullOrWhiteSpace(templateId) ||
                    !requestedTemplates.Add(templateId))
                {
                    throw new InvalidOperationException(
                        $"{context} template declarations must be non-blank and unique.");
                }
                matchesByTemplate.Add(templateId, new List<EvidenceArtifact>());
            }

            var seenArtifactIds = new HashSet<string>(StringComparer.Ordinal);
            for (int artifactIndex = 0;
                 artifactIndex < report.EvidenceArtifacts.Count;
                 artifactIndex++)
            {
                EvidenceArtifact candidate = report.EvidenceArtifacts[artifactIndex];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.ArtifactId) ||
                    !seenArtifactIds.Add(candidate.ArtifactId))
                {
                    throw new InvalidOperationException(
                        $"{context} encountered a null, blank, or duplicate evidence artifact.");
                }
                if (!matchesByTemplate.TryGetValue(
                        candidate.SourceTemplateId ?? string.Empty,
                        out List<EvidenceArtifact> templateMatches) ||
                    !Equal(candidate.CaseId, caseId) ||
                    candidate.EnteredCycle > maximumCycle)
                {
                    continue;
                }

                RequireExactScenarioEvidenceProvenance(candidate, context);
                templateMatches.Add(candidate);
            }

            for (int templateIndex = 0;
                 templateIndex < sourceTemplateIds.Count;
                 templateIndex++)
            {
                List<EvidenceArtifact> matches = matchesByTemplate[
                    sourceTemplateIds[templateIndex]];
                if (matches.Count == 0)
                {
                    artifactIds.Clear();
                    return false;
                }
                matches.Sort(CompareEvidenceArtifacts);
                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                    artifactIds.Add(matches[matchIndex].ArtifactId);
            }
            return true;
        }

        private static void RequireExactScenarioEvidenceProvenance(
            EvidenceArtifact artifact,
            string context)
        {
            EvidenceProvenance provenance = artifact.Provenance;
            string expectedArtifactId = provenance == null
                ? null
                : $"artifact:{provenance.SourceSocietyEventId}:{artifact.SourceTemplateId}";
            string expectedProvenanceId = provenance == null
                ? null
                : $"provenance:{provenance.SourceSocietyEventId}:{artifact.SourceTemplateId}";
            bool exact = provenance != null &&
                         provenance.CreatedByAgentAction &&
                         provenance.CreatedCycle == artifact.EnteredCycle &&
                         !string.IsNullOrWhiteSpace(provenance.SourceSocietyEventId) &&
                         !string.IsNullOrWhiteSpace(provenance.SourceDecisionId) &&
                         Equal(artifact.ArtifactId, expectedArtifactId) &&
                         Equal(provenance.ProvenanceId, expectedProvenanceId) &&
                         provenance.ChainOfCustodyIds != null &&
                         provenance.ChainOfCustodyIds.Count == 2 &&
                         Equal(
                             provenance.ChainOfCustodyIds[0],
                             provenance.SourceDecisionId) &&
                         Equal(
                             provenance.ChainOfCustodyIds[1],
                             provenance.SourceSocietyEventId);
            if (!exact)
            {
                throw new InvalidOperationException(
                    $"{context} artifact '{artifact.ArtifactId}' does not have exact " +
                    "event-template provenance.");
            }
        }

        private static int CompareEvidenceArtifacts(
            EvidenceArtifact left,
            EvidenceArtifact right)
        {
            int cycle = left.EnteredCycle.CompareTo(right.EnteredCycle);
            return cycle != 0
                ? cycle
                : StringComparer.Ordinal.Compare(left.ArtifactId, right.ArtifactId);
        }

        internal static void RequireDeclaredRulingId(string declaredId, Ruling ruling)
        {
            if (ruling == null || !Equal(declaredId, ruling.RulingId))
            {
                throw new InvalidOperationException(
                    $"Adjudication produced ruling '{ruling?.RulingId}' instead of " +
                    $"declared ruling '{declaredId}'.");
            }
        }

        internal static void RequireAccepted<T>(
            InstitutionalServiceResult<T> result,
            string operation) where T : class
        {
            if (result == null || result.Outcome == InstitutionalServiceOutcome.Rejected)
            {
                throw new InvalidOperationException(
                    $"Institutional {operation} was rejected: " +
                    $"{result?.ReasonId ?? "missing-result"}.");
            }
        }

        internal static bool Contains(
            IReadOnlyList<string> values,
            string expected)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++)
            {
                if (Equal(values[i], expected)) return true;
            }
            return false;
        }

        internal static bool Equal(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
