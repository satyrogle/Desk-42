using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Desk42.Institutional
{
    /// <summary>
    /// Outcome of one declarative status-effect request. A non-matching ruling is a
    /// completed execution with an explicit no-change mutation result.
    /// </summary>
    internal sealed class ScenarioOfficialStatusEffectExecutionResult
    {
        internal ScenarioOfficialStatusEffectExecutionResult(
            string effectRequestId,
            bool requiredDispositionMatched,
            StatusMutationResult statusMutationResult)
        {
            EffectRequestId = effectRequestId;
            RequiredDispositionMatched = requiredDispositionMatched;
            StatusMutationResult = statusMutationResult ??
                throw new ArgumentNullException(nameof(statusMutationResult));
        }

        internal string EffectRequestId { get; }
        internal bool RequiredDispositionMatched { get; }
        internal StatusMutationResult StatusMutationResult { get; }
    }

    /// <summary>
    /// Executes a scenario declaration only after resolving its exact already-issued
    /// ruling. It does not score evidence, issue rulings, or contain scenario branches.
    /// Request ids are exact-once keys within an InstitutionalConsequenceRun.
    /// </summary>
    internal static class InstitutionalScenarioOfficialStatusEffectExecutor
    {
        private sealed class ExecutionLedger
        {
            internal readonly object Sync = new();
            internal readonly Dictionary<string, ExecutionRecord> Records =
                new(StringComparer.Ordinal);
        }

        private sealed class ExecutionRecord
        {
            internal RequestFingerprint Fingerprint;
            internal bool RequiredDispositionMatched;
            internal bool Changed;
            internal bool CurrentRecognisedState;
            internal OfficialStatusMutation RecordedMutation;
        }

        private sealed class RequestFingerprint
        {
            internal long Cycle;
            internal string CauseCaseId;
            internal string CauseRulingId;
            internal RulingDisposition RequiredRulingDisposition;
            internal RulingDisposition ObservedRulingDisposition;
            internal string TargetRoleId;
            internal string TargetAgentId;
            internal string StatusId;
            internal bool RequestedRecognisedState;
            internal int RequestedResourceDelta;

            internal bool Matches(RequestFingerprint other)
            {
                return other != null &&
                       Cycle == other.Cycle &&
                       string.Equals(CauseCaseId, other.CauseCaseId, StringComparison.Ordinal) &&
                       string.Equals(CauseRulingId, other.CauseRulingId, StringComparison.Ordinal) &&
                       RequiredRulingDisposition == other.RequiredRulingDisposition &&
                       ObservedRulingDisposition == other.ObservedRulingDisposition &&
                       string.Equals(TargetRoleId, other.TargetRoleId, StringComparison.Ordinal) &&
                       string.Equals(TargetAgentId, other.TargetAgentId, StringComparison.Ordinal) &&
                       string.Equals(StatusId, other.StatusId, StringComparison.Ordinal) &&
                       RequestedRecognisedState == other.RequestedRecognisedState &&
                       RequestedResourceDelta == other.RequestedResourceDelta;
            }
        }

        private static readonly ConditionalWeakTable<InstitutionalConsequenceRun, ExecutionLedger>
            Ledgers = new();

        internal static ScenarioOfficialStatusEffectExecutionResult Execute(
            InstitutionalConsequenceRun run,
            ScenarioOfficialStatusEffectRequest request,
            IReadOnlyDictionary<string, string> agentIdByRole)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (agentIdByRole == null) throw new ArgumentNullException(nameof(agentIdByRole));
            ValidateRun(run);
            ValidateRequest(request);

            Ruling causeRuling = ResolveExactCauseRuling(run.Report, request);
            string targetAgentId = ResolveTargetAgentId(run, request, agentIdByRole);
            AgentState targetAgent = run.FinalSocietyState.GetAgent(targetAgentId);
            var fingerprint = new RequestFingerprint
            {
                Cycle = request.Cycle,
                CauseCaseId = request.CauseCaseId,
                CauseRulingId = request.CauseRulingId,
                RequiredRulingDisposition = request.RequiredRulingDisposition,
                ObservedRulingDisposition = causeRuling.Disposition,
                TargetRoleId = request.TargetRoleId,
                TargetAgentId = targetAgentId,
                StatusId = request.StatusId,
                RequestedRecognisedState = request.RequestedRecognisedState,
                RequestedResourceDelta = request.RequestedResourceDelta,
            };

            ExecutionLedger ledger = Ledgers.GetValue(run, _ => new ExecutionLedger());
            lock (ledger.Sync)
            {
                if (ledger.Records.TryGetValue(request.EffectRequestId,
                        out ExecutionRecord existing))
                {
                    if (!existing.Fingerprint.Matches(fingerprint))
                    {
                        throw new InvalidOperationException(
                            $"Status-effect request '{request.EffectRequestId}' was replayed " +
                            "with conflicting cause, target, condition, or mutation data.");
                    }
                    return ToResult(request.EffectRequestId, existing);
                }

                bool dispositionMatched =
                    causeRuling.Disposition == request.RequiredRulingDisposition;
                StatusMutationResult mutationResult;
                if (dispositionMatched)
                {
                    mutationResult = InstitutionalStatusMutationService.Apply(
                        run,
                        causeRuling,
                        targetAgentId,
                        request.StatusId,
                        request.RequestedRecognisedState,
                        request.RequestedResourceDelta);
                }
                else
                {
                    mutationResult = new StatusMutationResult
                    {
                        Changed = false,
                        CurrentRecognisedState = targetAgent.Standing.IsRecognised(
                            request.StatusId),
                        RecordedMutation = null,
                    };
                }

                var record = new ExecutionRecord
                {
                    Fingerprint = fingerprint,
                    RequiredDispositionMatched = dispositionMatched,
                    Changed = mutationResult.Changed,
                    CurrentRecognisedState = mutationResult.CurrentRecognisedState,
                    RecordedMutation = mutationResult.RecordedMutation,
                };
                ledger.Records.Add(request.EffectRequestId, record);
                return ToResult(request.EffectRequestId, record);
            }
        }

        private static ScenarioOfficialStatusEffectExecutionResult ToResult(
            string requestId,
            ExecutionRecord record)
        {
            return new ScenarioOfficialStatusEffectExecutionResult(
                requestId,
                record.RequiredDispositionMatched,
                new StatusMutationResult
                {
                    Changed = record.Changed,
                    CurrentRecognisedState = record.CurrentRecognisedState,
                    RecordedMutation = record.RecordedMutation,
                });
        }

        private static Ruling ResolveExactCauseRuling(
            InstitutionalConsequenceReport report,
            ScenarioOfficialStatusEffectRequest request)
        {
            Ruling matched = null;
            int idMatches = 0;
            for (int i = 0; i < report.Rulings.Count; i++)
            {
                Ruling candidate = report.Rulings[i];
                if (candidate == null ||
                    !string.Equals(candidate.RulingId, request.CauseRulingId,
                        StringComparison.Ordinal)) continue;
                matched = candidate;
                idMatches++;
            }

            if (idMatches == 0)
            {
                throw new InvalidOperationException(
                    $"Status-effect request '{request.EffectRequestId}' names missing cause " +
                    $"ruling '{request.CauseRulingId}'.");
            }
            if (idMatches != 1)
            {
                throw new InvalidOperationException(
                    $"Status-effect request '{request.EffectRequestId}' has an ambiguous cause " +
                    $"ruling '{request.CauseRulingId}'.");
            }
            if (!string.Equals(matched.CaseId, request.CauseCaseId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Status-effect request '{request.EffectRequestId}' expects cause ruling " +
                    $"'{request.CauseRulingId}' in case '{request.CauseCaseId}', but the ruling " +
                    $"belongs to case '{matched.CaseId}'.");
            }
            if (matched.Cycle != request.Cycle)
            {
                throw new InvalidOperationException(
                    $"Status-effect request '{request.EffectRequestId}' expects cause ruling " +
                    $"'{request.CauseRulingId}' at cycle {request.Cycle}, but it was issued at " +
                    $"cycle {matched.Cycle}.");
            }
            return matched;
        }

        private static string ResolveTargetAgentId(
            InstitutionalConsequenceRun run,
            ScenarioOfficialStatusEffectRequest request,
            IReadOnlyDictionary<string, string> agentIdByRole)
        {
            if (run.FinalSocietyState.GetAgent(request.TargetRoleId) != null)
            {
                throw new InvalidOperationException(
                    $"Status-effect request '{request.EffectRequestId}' uses forbidden direct " +
                    $"agent id '{request.TargetRoleId}' instead of a semantic role.");
            }
            if (!agentIdByRole.TryGetValue(request.TargetRoleId, out string targetAgentId) ||
                string.IsNullOrWhiteSpace(targetAgentId))
            {
                throw new InvalidOperationException(
                    $"Status-effect request '{request.EffectRequestId}' has no agent mapping " +
                    $"for target role '{request.TargetRoleId}'.");
            }
            if (run.FinalSocietyState.GetAgent(targetAgentId) == null)
            {
                throw new InvalidOperationException(
                    $"Target role '{request.TargetRoleId}' maps to missing agent " +
                    $"'{targetAgentId}'.");
            }
            return targetAgentId;
        }

        private static void ValidateRun(InstitutionalConsequenceRun run)
        {
            if (run.Report == null)
                throw new InvalidOperationException("A consequence report is required.");
            if (run.Report.Rulings == null)
                throw new InvalidOperationException("The consequence report has no ruling collection.");
            if (run.FinalSocietyState == null)
                throw new InvalidOperationException("A final society state is required.");
        }

        private static void ValidateRequest(ScenarioOfficialStatusEffectRequest request)
        {
            RequireId(request.EffectRequestId, "effect request");
            RequireId(request.CauseCaseId, "cause case");
            RequireId(request.CauseRulingId, "cause ruling");
            RequireId(request.TargetRoleId, "target role");
            RequireId(request.StatusId, "status");
            if (!Enum.IsDefined(typeof(RulingDisposition), request.RequiredRulingDisposition))
            {
                throw new InvalidOperationException(
                    $"Status-effect request '{request.EffectRequestId}' has an invalid required " +
                    "ruling disposition.");
            }
        }

        private static void RequireId(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"A {label} id is required.");
        }
    }
}
