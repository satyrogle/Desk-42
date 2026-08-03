using System;

namespace Desk42.Institutional
{
    /// <summary>
    /// Registers authored conserved resources and applies only transfers caused by a
    /// ruling that actually cited the declared scoped holding and reached the
    /// disposition required by the transfer declaration.
    /// </summary>
    internal static class InstitutionalScenarioEntitlementPhase
    {
        internal static void RegisterInitial(InstitutionalScenarioExecutionContext context)
        {
            for (int i = 0; i < context.Definition.ExclusiveEntitlements.Count; i++)
            {
                ScenarioExclusiveEntitlementDefinition declaration =
                    context.Definition.ExclusiveEntitlements[i];
                ExclusiveEntitlementService.RegisterInitialState(
                    context.EntitlementRegistry,
                    context.Run,
                    declaration.EntitlementId,
                    declaration.ResourceId,
                    declaration.OfficialStatusId,
                    declaration.Units,
                    context.AgentIdByRole[declaration.InitialHolderRoleId]);
            }
        }

        internal static void TransferDue(
            InstitutionalScenarioExecutionContext context,
            long cycle)
        {
            for (int i = 0; i < context.Definition.EntitlementTransfers.Count; i++)
            {
                ScenarioExclusiveEntitlementTransferDefinition declaration =
                    context.Definition.EntitlementTransfers[i];
                if (declaration.Cycle != cycle) continue;

                Ruling causeRuling = FindDeclaredCauseRuling(
                    context,
                    declaration);
                if (causeRuling == null)
                {
                    continue;
                }

                ScenarioExclusiveEntitlementDefinition entitlement =
                    InstitutionalScenarioLookup.Entitlement(
                        context.Definition,
                        declaration.EntitlementId);
                ExclusiveEntitlementTransferResult result =
                    ExclusiveEntitlementService.ChangeHolder(
                        context.EntitlementRegistry,
                        context.Run,
                        causeRuling,
                        entitlement.EntitlementId,
                        entitlement.ResourceId,
                        context.AgentIdByRole[declaration.FromRoleId],
                        context.AgentIdByRole[declaration.ToRoleId],
                        declaration.GainKind,
                        declaration.LossKind,
                        declaration.GainKindId,
                        declaration.LossKindId);
                if (result.Changed)
                    PublishConnectedOutcome(context, declaration, result);
            }
        }

        private static Ruling FindDeclaredCauseRuling(
            InstitutionalScenarioExecutionContext context,
            ScenarioExclusiveEntitlementTransferDefinition declaration)
        {
            Ruling cause = InstitutionalScenarioLookup.Ruling(
                context.Run.Report,
                declaration.CauseRulingId);
            if (cause == null) return null;
            if (!InstitutionalScenarioLookup.Equal(
                    cause.CaseId,
                    declaration.CauseCaseId) ||
                cause.Cycle != declaration.Cycle)
            {
                throw new InvalidOperationException(
                    $"Transfer '{declaration.TransferId}' exact cause ruling is inconsistent.");
            }

            // A declared transfer is conditional on the exact ruling actually citing
            // the declared holding. The ruling itself may legitimately materialise
            // without that optional citation under another policy configuration; in
            // that case the transfer branch simply does not materialise.
            if (!InstitutionalScenarioLookup.Contains(
                    cause.CitedHoldingIds,
                    declaration.CauseHoldingId) ||
                cause.Disposition != declaration.RequiredRulingDisposition)
            {
                return null;
            }
            return cause;
        }

        internal static string BuildConnectedOutcomePairId(string transferId)
        {
            return InstitutionalScenarioDerivedIds.ConnectedOutcomePair(transferId);
        }

        private static void PublishConnectedOutcome(
            InstitutionalScenarioExecutionContext context,
            ScenarioExclusiveEntitlementTransferDefinition declaration,
            ExclusiveEntitlementTransferResult transfer)
        {
            ScenarioHoldingDefinition holding = InstitutionalScenarioLookup.Holding(
                context.Definition,
                declaration.CauseHoldingId);
            InstitutionalServiceResult<ConnectedOutcomePair> projection =
                InstitutionalConnectedOutcomeProjector.Project(
                    context.Run,
                    BuildConnectedOutcomePairId(declaration.TransferId),
                    holding.RuleId,
                    transfer.ResourceId,
                    transfer.CurrentHolderAgentId,
                    transfer.PreviousHolderAgentId,
                    transfer.ConservedAmount);
            InstitutionalScenarioLookup.RequireAccepted(
                projection,
                "connected-outcome projection");
        }
    }
}
