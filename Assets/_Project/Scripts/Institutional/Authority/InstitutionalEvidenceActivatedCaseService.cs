using System;

namespace Desk42.Institutional
{
    /// <summary>
    /// Opens a conditionally scheduled case only after one exact scenario evidence
    /// projection can be traced back to the autonomous action that produced it.
    /// The public opening record carries no authoritative lived-event state.
    /// </summary>
    internal static class InstitutionalEvidenceActivatedCaseService
    {
        internal static void OpenDueCases(
            InstitutionalScenarioExecutionContext context,
            long cycle)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            for (int i = 0; i < context.Definition.EvidenceActivatedCases.Count; i++)
            {
                ScenarioEvidenceActivatedCaseDefinition activation =
                    context.Definition.EvidenceActivatedCases[i];
                ScenarioCaseDefinition target = InstitutionalScenarioLookup.Case(
                    context.Definition,
                    activation.CaseId);
                if (target.OpenCycle != cycle) continue;

                ScenarioEvidenceTemplateDefinition template = FindTemplate(
                    context.Definition,
                    activation.EvidenceTemplateId);
                EvidenceArtifact trigger = FindUniqueTriggerArtifact(
                    context.Run.Report,
                    activation,
                    out int triggerCount);
                if (triggerCount == 0) continue;
                if (triggerCount != 1 || !IsExactDeclaredTriggerEvidence(
                        context.Run,
                        activation,
                        template,
                        trigger))
                {
                    throw new InvalidOperationException(
                        $"Case activation '{activation.ActivationId}' has ambiguous or " +
                        "invalid evidence provenance.");
                }

                int existingCount = 0;
                InstitutionalCaseOpening existing = null;
                for (int openingIndex = 0;
                     openingIndex < context.Run.Report.CaseOpenings.Count;
                     openingIndex++)
                {
                    InstitutionalCaseOpening candidate =
                        context.Run.Report.CaseOpenings[openingIndex];
                    if (candidate == null ||
                        (!Equal(candidate.ActivationId, activation.ActivationId) &&
                         !Equal(candidate.CaseId, activation.CaseId))) continue;
                    existing = candidate;
                    existingCount++;
                }
                if (existingCount != 0)
                {
                    if (existingCount == 1 && Equivalent(
                            existing,
                            activation,
                            target,
                            trigger))
                    {
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"Case activation '{activation.ActivationId}' conflicts with an " +
                        "existing opening projection.");
                }

                var opening = new InstitutionalCaseOpening
                {
                    ActivationId = activation.ActivationId,
                    CaseId = activation.CaseId,
                    OpenedCycle = target.OpenCycle,
                    TriggerEvidenceArtifactId = trigger.ArtifactId,
                    CausalAgentActionId = trigger.Provenance.SourceSocietyEventId,
                };
                context.Run.Report.CaseOpenings.Add(opening);
                InstitutionalTimeline.Add(
                    context.Run.Report,
                    opening.OpenedCycle,
                    InstitutionalTimelineKind.CaseOpened,
                    opening.TriggerEvidenceArtifactId,
                    opening.CaseId,
                    opening.ActivationId);
            }
        }

        internal static bool IsExactDeclaredTriggerEvent(
            InstitutionalConsequenceRun run,
            ScenarioEvidenceActivatedCaseDefinition activation,
            ScenarioEvidenceTemplateDefinition template,
            SocietyEvent societyEvent)
        {
            if (run == null || run.Report == null || activation == null ||
                template == null || societyEvent == null ||
                !Equal(template.EvidenceTemplateId, activation.EvidenceTemplateId) ||
                !Equal(template.CaseId, activation.CaseId) ||
                societyEvent.Tick != activation.TriggerCycle ||
                societyEvent.Kind != template.SourceEventKind ||
                !MatchesOptional(template.SourceOpportunityId, societyEvent.OpportunityId) ||
                !MatchesOptional(
                    template.RequiredPropositionId,
                    societyEvent.EvidencePropositionId))
            {
                return false;
            }

            return HasExactActionAndTrace(
                run,
                societyEvent.EventId,
                societyEvent.CauseDecisionId,
                societyEvent.ActorId,
                societyEvent.Tick,
                template,
                evidenceArtifactId: null);
        }

        internal static bool IsExactDeclaredTriggerEvidence(
            InstitutionalConsequenceRun run,
            ScenarioEvidenceActivatedCaseDefinition activation,
            ScenarioEvidenceTemplateDefinition template,
            EvidenceArtifact artifact)
        {
            if (run == null || run.Report == null || activation == null ||
                template == null || artifact == null || artifact.Provenance == null ||
                !Equal(template.EvidenceTemplateId, activation.EvidenceTemplateId) ||
                !Equal(template.CaseId, activation.CaseId) ||
                !Equal(artifact.SourceTemplateId, activation.EvidenceTemplateId) ||
                !Equal(artifact.CaseId, activation.CaseId) ||
                !Equal(artifact.IssueId, template.IssueId) ||
                !Equal(artifact.EvidenceClassId, template.EvidenceClassId) ||
                artifact.Effect != template.Effect ||
                artifact.BaseWeight != template.Weight ||
                artifact.Kind != EvidenceArtifactKind.ActionRecord ||
                artifact.Provenance.Visibility != template.Visibility ||
                !artifact.OfficiallySubmitted ||
                artifact.EnteredCycle != activation.TriggerCycle ||
                !artifact.Provenance.CreatedByAgentAction ||
                artifact.Provenance.CreatedCycle != artifact.EnteredCycle ||
                !Equal(
                    artifact.ArtifactId,
                    $"artifact:{artifact.Provenance.SourceSocietyEventId}:" +
                    activation.EvidenceTemplateId) ||
                !Equal(
                    artifact.Provenance.ProvenanceId,
                    $"provenance:{artifact.Provenance.SourceSocietyEventId}:" +
                    activation.EvidenceTemplateId) ||
                !MatchesOptional(
                    template.RequiredPropositionId,
                    artifact.PropositionId))
            {
                return false;
            }

            return HasExactActionAndTrace(
                run,
                artifact.Provenance.SourceSocietyEventId,
                artifact.Provenance.SourceDecisionId,
                artifact.Provenance.SourceAgentId,
                artifact.EnteredCycle,
                template,
                artifact.ArtifactId);
        }

        private static bool HasExactActionAndTrace(
            InstitutionalConsequenceRun run,
            string actionEventId,
            string decisionId,
            string actorId,
            long cycle,
            ScenarioEvidenceTemplateDefinition template,
            string evidenceArtifactId)
        {
            if (string.IsNullOrWhiteSpace(actionEventId) ||
                string.IsNullOrWhiteSpace(decisionId) ||
                string.IsNullOrWhiteSpace(actorId) ||
                run.AssessorActionTraces == null ||
                run.Report.ObservedAgentActions == null)
            {
                return false;
            }

            ObservedAgentAction action = null;
            int actionCount = 0;
            for (int i = 0; i < run.Report.ObservedAgentActions.Count; i++)
            {
                ObservedAgentAction candidate = run.Report.ObservedAgentActions[i];
                if (candidate == null || !Equal(candidate.ActionEventId, actionEventId))
                    continue;
                action = candidate;
                actionCount++;
            }

            AgentActionTrace trace = null;
            int traceCount = 0;
            for (int i = 0; i < run.AssessorActionTraces.Count; i++)
            {
                AgentActionTrace candidate = run.AssessorActionTraces[i];
                if (candidate == null || !Equal(candidate.DecisionId, decisionId)) continue;
                trace = candidate;
                traceCount++;
            }

            return actionCount == 1 && traceCount == 1 &&
                   action.Cycle == cycle &&
                   Equal(action.ActorId, actorId) &&
                   action.Activity == InstitutionalActionProjector.ActivityFor(
                       template.SourceEventKind) &&
                   trace.Cycle == cycle &&
                   Equal(trace.ActorId, actorId) &&
                   Count(trace.ResultEventIds, actionEventId) == 1 &&
                   MatchesTrace(template, trace) &&
                   (evidenceArtifactId == null ||
                    Count(action.ResultEvidenceArtifactIds, evidenceArtifactId) == 1);
        }

        private static bool MatchesTrace(
            ScenarioEvidenceTemplateDefinition template,
            AgentActionTrace trace)
        {
            SocietyActionKind expected = template.SourceEventKind switch
            {
                SocietyEventKind.NoActionObserved => SocietyActionKind.Idle,
                SocietyEventKind.WorkPerformed => SocietyActionKind.Work,
                SocietyEventKind.AidRequested => SocietyActionKind.SeekAid,
                SocietyEventKind.AssistanceGiven => SocietyActionKind.Help,
                SocietyEventKind.EvidenceDisclosed => SocietyActionKind.Disclose,
                SocietyEventKind.ResponseWithheld => SocietyActionKind.Withhold,
                SocietyEventKind.AppealFiled => SocietyActionKind.Appeal,
                _ => (SocietyActionKind)(-1),
            };
            return trace.Action == expected &&
                   MatchesOptional(template.SourceOpportunityId, trace.OpportunityId);
        }

        private static EvidenceArtifact FindUniqueTriggerArtifact(
            InstitutionalConsequenceReport report,
            ScenarioEvidenceActivatedCaseDefinition activation,
            out int count)
        {
            EvidenceArtifact result = null;
            count = 0;
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                EvidenceArtifact candidate = report.EvidenceArtifacts[i];
                if (candidate == null ||
                    !Equal(candidate.CaseId, activation.CaseId) ||
                    !Equal(candidate.SourceTemplateId, activation.EvidenceTemplateId) ||
                    candidate.EnteredCycle != activation.TriggerCycle)
                {
                    continue;
                }
                result = candidate;
                count++;
            }
            return result;
        }

        private static ScenarioEvidenceTemplateDefinition FindTemplate(
            InstitutionalScenarioDefinition definition,
            string templateId)
        {
            ScenarioEvidenceTemplateDefinition result = null;
            int count = 0;
            for (int i = 0; i < definition.EvidenceTemplates.Count; i++)
            {
                ScenarioEvidenceTemplateDefinition candidate =
                    definition.EvidenceTemplates[i];
                if (candidate == null || !Equal(
                        candidate.EvidenceTemplateId,
                        templateId)) continue;
                result = candidate;
                count++;
            }
            if (count != 1)
            {
                throw new InvalidOperationException(
                    $"Evidence template '{templateId}' has {count} declarations.");
            }
            return result;
        }

        private static bool Equivalent(
            InstitutionalCaseOpening opening,
            ScenarioEvidenceActivatedCaseDefinition activation,
            ScenarioCaseDefinition target,
            EvidenceArtifact trigger)
        {
            return opening != null &&
                   Equal(opening.ActivationId, activation.ActivationId) &&
                   Equal(opening.CaseId, activation.CaseId) &&
                   opening.OpenedCycle == target.OpenCycle &&
                   Equal(opening.TriggerEvidenceArtifactId, trigger.ArtifactId) &&
                   Equal(
                       opening.CausalAgentActionId,
                       trigger.Provenance.SourceSocietyEventId);
        }

        private static int Count(System.Collections.Generic.IReadOnlyList<string> values, string id)
        {
            if (values == null) return 0;
            int count = 0;
            for (int i = 0; i < values.Count; i++)
                if (Equal(values[i], id)) count++;
            return count;
        }

        private static bool MatchesOptional(string expected, string actual)
        {
            return string.IsNullOrWhiteSpace(expected) || Equal(expected, actual);
        }

        private static bool Equal(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
