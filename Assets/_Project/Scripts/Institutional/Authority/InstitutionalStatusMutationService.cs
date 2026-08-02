using System;

namespace Desk42.Institutional
{
    /// <summary>
    /// Sole owner of official recognition mutations and their public causal rows.
    /// </summary>
    internal static class InstitutionalStatusMutationService
    {
        internal static StatusMutationResult Apply(
            InstitutionalConsequenceRun run,
            Ruling ruling,
            string agentId,
            string statusId,
            bool recognised,
            int resourceDelta)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (ruling == null) throw new ArgumentNullException(nameof(ruling));
            if (run.Report == null || run.FinalSocietyState == null)
                throw new InvalidOperationException(
                    "Status mutation requires a report and final society state.");
            if (string.IsNullOrWhiteSpace(statusId))
                throw new ArgumentException("Status mutation requires a stable status id.",
                    nameof(statusId));
            if (run.Report.Rulings == null ||
                run.Report.OfficialStatusMutations == null ||
                run.Report.Timeline == null ||
                ruling.OfficialStatusMutationIds == null)
            {
                throw new InvalidOperationException(
                    "Status mutation requires initialized report and ruling collections.");
            }
            int registeredRulingCount = 0;
            bool exactRulingRegistered = false;
            for (int i = 0; i < run.Report.Rulings.Count; i++)
            {
                Ruling registered = run.Report.Rulings[i];
                if (registered == null || !string.Equals(
                        registered.RulingId, ruling.RulingId, StringComparison.Ordinal))
                    continue;
                registeredRulingCount++;
                exactRulingRegistered |= ReferenceEquals(registered, ruling);
            }
            if (registeredRulingCount != 1 || !exactRulingRegistered)
                throw new InvalidOperationException(
                    "Status mutation cause must be the unique ruling registered in the report.");
            AgentState agent = run.FinalSocietyState.GetAgent(agentId);
            if (agent == null)
                throw new InvalidOperationException($"Unknown mutation target {agentId}.");
            if (agent.Standing == null)
                throw new InvalidOperationException(
                    $"Mutation target {agentId} has no institutional standing.");

            bool before = agent.Standing.IsRecognised(statusId);
            if (before == recognised && resourceDelta == 0)
            {
                return new StatusMutationResult
                {
                    Changed = false,
                    CurrentRecognisedState = before,
                    RecordedMutation = null,
                };
            }

            EconomicAccountState account = null;
            int creditsAfter = 0;
            if (resourceDelta != 0)
            {
                account = FindAccount(run, agentId);
                creditsAfter = checked(account.AvailableCredits + resourceDelta);
            }

            string mutationId = BuildMutationId(
                ruling,
                run.Report.OfficialStatusMutations.Count,
                agentId,
                statusId);
            EnsureMutationIdAvailable(run.Report, mutationId);

            var mutation = new OfficialStatusMutation
            {
                MutationId = mutationId,
                Cycle = ruling.Cycle,
                CauseId = ruling.RulingId,
                AffectedAgentId = agentId,
                StatusId = statusId,
                BeforeRecognised = before,
                AfterRecognised = recognised,
                ResourceDelta = resourceDelta,
            };
            agent.Standing.SetRecognised(statusId, recognised);
            if (resourceDelta != 0)
                account.AvailableCredits = creditsAfter;
            run.Report.OfficialStatusMutations.Add(mutation);
            ruling.OfficialStatusMutationIds.Add(mutation.MutationId);
            InstitutionalTimeline.Add(
                run.Report,
                ruling.Cycle,
                InstitutionalTimelineKind.StatusMutated,
                ruling.RulingId,
                agentId,
                statusId);
            return new StatusMutationResult
            {
                Changed = true,
                CurrentRecognisedState = recognised,
                RecordedMutation = mutation,
            };
        }

        internal static OfficialStatusMutation FindLatest(
            InstitutionalConsequenceReport report,
            string agentId,
            string statusId,
            bool recognised)
        {
            if (report == null) return null;
            for (int i = report.OfficialStatusMutations.Count - 1; i >= 0; i--)
            {
                OfficialStatusMutation mutation = report.OfficialStatusMutations[i];
                if (string.Equals(mutation.AffectedAgentId, agentId,
                        StringComparison.Ordinal) &&
                    string.Equals(mutation.StatusId, statusId,
                        StringComparison.Ordinal) &&
                    mutation.AfterRecognised == recognised)
                {
                    return mutation;
                }
            }
            return null;
        }

        internal static string BuildMutationId(
            Ruling ruling,
            int index,
            string agentId,
            string statusId)
        {
            if (ruling == null) throw new ArgumentNullException(nameof(ruling));
            return $"mutation:{ruling.Cycle}:{index}:{agentId}:{statusId}";
        }

        internal static void EnsureMutationIdAvailable(
            InstitutionalConsequenceReport report,
            string mutationId)
        {
            if (report?.OfficialStatusMutations == null)
                throw new InvalidOperationException(
                    "Mutation id validation requires an initialized mutation collection.");
            for (int i = 0; i < report.OfficialStatusMutations.Count; i++)
            {
                if (string.Equals(report.OfficialStatusMutations[i]?.MutationId,
                        mutationId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Official mutation id '{mutationId}' already exists.");
                }
            }
        }

        private static EconomicAccountState FindAccount(
            InstitutionalConsequenceRun run,
            string agentId)
        {
            for (int i = 0; i < run.EconomicAccounts.Count; i++)
            {
                if (string.Equals(run.EconomicAccounts[i].AgentId, agentId,
                    StringComparison.Ordinal)) return run.EconomicAccounts[i];
            }
            throw new InvalidOperationException($"Missing economic account for {agentId}.");
        }
    }
}
