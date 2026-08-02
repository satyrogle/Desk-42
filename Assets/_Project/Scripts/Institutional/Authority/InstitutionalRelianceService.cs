using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal enum RelianceFailureReason
    {
        None,
        InvalidRequest,
        DuplicateReliance,
        DuplicateObservation,
        SourceActionNotObserved,
        SourceActionActorMismatch,
        AutonomousTraceNotFound,
        SourceActionKindMismatch,
        SourceOpportunityMismatch,
        ActionDidNotReadRequiredStatus,
        EnablingMutationNotFound,
        EnablingMutationMismatch,
        EnablingRulingNotFound,
        InvalidChronology,
        AlternativeNotFound,
        AlternativeOwnerMismatch,
        AlternativeUnavailable,
        AgentNotFound,
        EconomicAccountNotFound,
        NeedNotFound,
        InsufficientResources,
    }

    internal enum RelianceEffectRecipient
    {
        Actor,
        Beneficiary,
        RelatedAgent,
    }

    /// <summary>
    /// One bounded, declarative material effect of a reliance choice. A request may
    /// contain at most one effect per recipient role. Need pressure is clamped to the
    /// same 0..100 interval as the society simulation.
    /// </summary>
    internal sealed class RelianceEffectDelta
    {
        internal string EffectId;
        internal RelianceEffectRecipient Recipient;
        internal int ResourceDelta;
        internal MaterialConsequenceKind MaterialKind;
        internal string MaterialKindId;
        internal string ResourceId;
        internal NeedKind? Need;
        internal int NeedPressureDelta;
    }

    internal sealed class RelianceCreationRequest
    {
        internal string RelianceEventId;
        internal string ObservationId;
        internal string SourceActionEventId;
        internal string ActorAgentId;
        internal SocietyActionKind ExpectedActionKind;
        internal string ExpectedOpportunityId;
        internal string BeneficiaryAgentId;
        internal string RelatedAgentId;
        internal string EnablingRulingId;
        internal string EnablingMutationId;
        internal string RequiredStatusId;
        internal bool ExpectedRecognisedState = true;
        internal string ChoiceId;
        internal string RecordedChoiceId;
        internal string AbandonedAlternativeId;
        internal string ResourceId;
        internal List<RelianceEffectDelta> Effects = new();
    }

    internal sealed class RelianceCreationResult
    {
        internal bool Created;
        internal RelianceFailureReason FailureReason;
        internal RelianceEvent Reliance;
        internal RelianceObservation Observation;
        internal List<MaterialConsequence> MaterialConsequences = new();
    }

    internal enum RelianceRecoveryFailureReason
    {
        None,
        InvalidRequest,
        RelianceNotFound,
        ReversalRulingNotFound,
        RulingIsNotAReversal,
        InvalidChronology,
        SourceActionNotObserved,
        DuplicateRecoveryCase,
    }

    internal sealed class RelianceRecoveryRequest
    {
        internal string RelianceEventId;
        internal string CaseIdPrefix;
        internal string ParentCaseId;
        internal string RespondentId;
        internal string OfficialIssueId;
        internal CaseFactSet Facts = new();
        // Legacy compatibility projections for the preserved workplace proof.
        // New scenarios express scope through Facts.
        internal string OfficialIdentityConditionId;
        internal string OfficialEmployerId;
    }

    internal sealed class RelianceRecoveryResult
    {
        internal bool Created;
        internal RelianceRecoveryFailureReason FailureReason;
        internal DescendantCase RecoveryCase;
    }

    /// <summary>
    /// Scenario-neutral reliance and recovery projection. Reliance is accepted only
    /// when an observed autonomous decision trace explicitly read a recognised status
    /// created by the exact earlier ruling mutation supplied by the caller.
    /// </summary>
    internal static class InstitutionalRelianceService
    {
        internal const int MaximumEffects = 3;
        internal const int MaximumIdentifierLength = 160;
        internal const int MaximumResourceDeltaMagnitude = 1_000_000;
        internal const int MaximumNeedDeltaMagnitude = 100;

        internal static RelianceCreationResult TryCreate(
            InstitutionalConsequenceRun run,
            RelianceCreationRequest request)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.Report == null || run.FinalSocietyState == null)
                throw new InvalidOperationException(
                    "Reliance requires a public report and final society state.");

            RelianceFailureReason requestFailure = ValidateRequest(request);
            if (requestFailure != RelianceFailureReason.None)
                return Failed(requestFailure);

            ValidateCreationCollections(run);

            if (FindReliance(run, request.RelianceEventId) != null)
                return Failed(RelianceFailureReason.DuplicateReliance);
            if (FindObservation(run.Report, request.ObservationId) != null)
                return Failed(RelianceFailureReason.DuplicateObservation);

            ObservedAgentAction action = InstitutionalTimeline.FindObservedAction(
                run.Report,
                request.SourceActionEventId);
            if (action == null)
                return Failed(RelianceFailureReason.SourceActionNotObserved);
            if (!string.Equals(action.ActorId, request.ActorAgentId,
                    StringComparison.Ordinal))
                return Failed(RelianceFailureReason.SourceActionActorMismatch);

            AgentActionTrace trace = FindTrace(run, request.SourceActionEventId);
            if (trace == null ||
                !string.Equals(trace.ActorId, request.ActorAgentId,
                    StringComparison.Ordinal))
            {
                return Failed(RelianceFailureReason.AutonomousTraceNotFound);
            }
            if (trace.Action != request.ExpectedActionKind)
                return Failed(RelianceFailureReason.SourceActionKindMismatch);
            if (!string.Equals(trace.OpportunityId, request.ExpectedOpportunityId,
                    StringComparison.Ordinal))
            {
                return Failed(RelianceFailureReason.SourceOpportunityMismatch);
            }
            if (!TraceReadsStatus(
                    trace,
                    request.RequiredStatusId,
                    request.ExpectedRecognisedState))
                return Failed(RelianceFailureReason.ActionDidNotReadRequiredStatus);

            OfficialStatusMutation mutation = FindMutation(
                run.Report,
                request.EnablingMutationId);
            if (mutation == null)
                return Failed(RelianceFailureReason.EnablingMutationNotFound);
            if (!string.Equals(mutation.CauseId, request.EnablingRulingId,
                    StringComparison.Ordinal) ||
                !string.Equals(mutation.AffectedAgentId, request.ActorAgentId,
                    StringComparison.Ordinal) ||
                !string.Equals(mutation.StatusId, request.RequiredStatusId,
                    StringComparison.Ordinal) ||
                mutation.AfterRecognised != request.ExpectedRecognisedState)
            {
                return Failed(RelianceFailureReason.EnablingMutationMismatch);
            }

            Ruling ruling = FindRuling(run.Report, request.EnablingRulingId);
            if (ruling == null || ruling.OfficialStatusMutationIds == null ||
                !ruling.OfficialStatusMutationIds.Contains(mutation.MutationId))
            {
                return Failed(RelianceFailureReason.EnablingRulingNotFound);
            }
            if (mutation.Cycle != ruling.Cycle || mutation.Cycle >= action.Cycle)
                return Failed(RelianceFailureReason.InvalidChronology);

            AlternativeOptionState alternative = FindAlternative(
                run,
                request.AbandonedAlternativeId);
            if (alternative == null)
                return Failed(RelianceFailureReason.AlternativeNotFound);
            if (!string.Equals(alternative.AgentId, request.ActorAgentId,
                    StringComparison.Ordinal))
            {
                return Failed(RelianceFailureReason.AlternativeOwnerMismatch);
            }
            if (!alternative.Available)
                return Failed(RelianceFailureReason.AlternativeUnavailable);

            AgentState actor = run.FinalSocietyState.GetAgent(request.ActorAgentId);
            AgentState beneficiary = run.FinalSocietyState.GetAgent(
                request.BeneficiaryAgentId);
            AgentState related = string.IsNullOrEmpty(request.RelatedAgentId)
                ? null
                : run.FinalSocietyState.GetAgent(request.RelatedAgentId);
            if (actor == null || beneficiary == null ||
                (!string.IsNullOrEmpty(request.RelatedAgentId) && related == null))
            {
                return Failed(RelianceFailureReason.AgentNotFound);
            }

            var applications = new List<EffectApplication>(request.Effects.Count);
            var stagedAccountCredits = new Dictionary<EconomicAccountState, int>();
            var stagedNeedPressures = new Dictionary<NeedState, int>();
            int actorResourceDelta = 0;
            var occupiedRecipients = new HashSet<RelianceEffectRecipient>();
            for (int i = 0; i < request.Effects.Count; i++)
            {
                RelianceEffectDelta effect = request.Effects[i];
                if (!occupiedRecipients.Add(effect.Recipient))
                    return Failed(RelianceFailureReason.InvalidRequest);

                AgentState target = ResolveTarget(effect.Recipient, actor, beneficiary, related);
                if (target == null)
                    return Failed(RelianceFailureReason.AgentNotFound);

                EconomicAccountState account = null;
                int creditsBefore = 0;
                int creditsAfter = 0;
                if (effect.ResourceDelta != 0)
                {
                    account = FindAccount(run, target.StableId);
                    if (account == null)
                        return Failed(RelianceFailureReason.EconomicAccountNotFound);
                    creditsBefore = stagedAccountCredits.TryGetValue(account, out int stagedCredits)
                        ? stagedCredits
                        : account.AvailableCredits;
                    creditsAfter = checked(creditsBefore + effect.ResourceDelta);
                    if (creditsAfter < 0)
                        return Failed(RelianceFailureReason.InsufficientResources);
                    stagedAccountCredits[account] = creditsAfter;
                }

                NeedState need = null;
                int needBefore = 0;
                int needAfter = 0;
                if (effect.Need.HasValue)
                {
                    need = target.GetNeed(effect.Need.Value);
                    if (need == null)
                        return Failed(RelianceFailureReason.NeedNotFound);
                    needBefore = stagedNeedPressures.TryGetValue(need, out int stagedPressure)
                        ? stagedPressure
                        : need.Pressure;
                    needAfter = InstitutionalMath.Clamp(
                        checked(needBefore + effect.NeedPressureDelta),
                        0,
                        100);
                    stagedNeedPressures[need] = needAfter;
                }

                if (effect.Recipient == RelianceEffectRecipient.Actor)
                    actorResourceDelta += effect.ResourceDelta;
                applications.Add(new EffectApplication
                {
                    Effect = effect,
                    Agent = target,
                    Account = account,
                    CreditsBefore = creditsBefore,
                    CreditsAfter = creditsAfter,
                    Need = need,
                    NeedBefore = needBefore,
                    NeedAfter = needAfter,
                });
            }

            // The existing public validation contract defines reliance as an
            // irreversible cost recorded against the relying actor.
            if (actorResourceDelta >= 0)
                return Failed(RelianceFailureReason.InvalidRequest);

            EconomicAccountState actorAccount = FindAccount(run, actor.StableId);
            if (actorAccount == null)
                return Failed(RelianceFailureReason.EconomicAccountNotFound);
            int actorCreditsBefore = actorAccount.AvailableCredits;
            int actorSubsistenceBefore = Pressure(actor, NeedKind.Subsistence);
            int relatedSubsistenceBefore = Pressure(related, NeedKind.Subsistence);
            bool alternativeBefore = alternative.Available;

            var materials = new List<MaterialConsequence>(applications.Count);
            var stagedMaterialIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < applications.Count; i++)
            {
                EffectApplication application = applications[i];
                application.Material = CreateMaterial(
                    run.Report.MaterialConsequences.Count + i,
                    action.Cycle,
                    action.ActionEventId,
                    application.Agent.StableId,
                    application.Effect.MaterialKind,
                    application.Effect.MaterialKindId,
                    application.Effect.ResourceId ?? request.ResourceId,
                    application.Effect.ResourceDelta,
                    request.RelianceEventId,
                    application.Effect.Need,
                    application.NeedBefore,
                    application.NeedAfter);
                EnsureMaterialIdAvailable(
                    run.Report,
                    application.Material.ConsequenceId,
                    stagedMaterialIds);
                materials.Add(application.Material);
            }

            var reliance = new RelianceEvent
            {
                RelianceEventId = request.RelianceEventId,
                Cycle = action.Cycle,
                AgentId = actor.StableId,
                BeneficiaryAgentId = beneficiary.StableId,
                ReliedOnRulingId = ruling.RulingId,
                ReliedOnMutationId = mutation.MutationId,
                SourceActionEventId = action.ActionEventId,
                ChoiceId = request.ChoiceId,
                AbandonedAlternativeId = alternative.OptionId,
                ResourceSpent = -actorResourceDelta,
                HealthPressureAfterAction = ProjectedPressure(
                    beneficiary,
                    NeedKind.Health,
                    stagedNeedPressures),
                AlternativeAvailableBefore = alternativeBefore,
                AlternativeAvailableAfter = false,
                CreditsBefore = actorCreditsBefore,
                CreditsAfter = ProjectedCredits(actorAccount, stagedAccountCredits),
                AgentSubsistenceBefore = actorSubsistenceBefore,
                AgentSubsistenceAfter = ProjectedPressure(
                    actor,
                    NeedKind.Subsistence,
                    stagedNeedPressures),
                HouseholdAgentId = related?.StableId,
                HouseholdSubsistenceBefore = relatedSubsistenceBefore,
                HouseholdSubsistenceAfter = ProjectedPressure(
                    related,
                    NeedKind.Subsistence,
                    stagedNeedPressures),
            };
            for (int i = 0; i < applications.Count; i++)
            {
                EffectApplication application = applications[i];
                reliance.AppliedEffects.Add(new RelianceAppliedEffect
                {
                    EffectId = application.Effect.EffectId,
                    AgentId = application.Agent.StableId,
                    ResourceBefore = application.CreditsBefore,
                    ResourceAfter = application.CreditsAfter,
                    HasNeedEffect = application.Effect.Need.HasValue,
                    Need = application.Effect.Need ?? default,
                    NeedPressureBefore = application.NeedBefore,
                    NeedPressureAfter = application.NeedAfter,
                    MaterialConsequenceId = application.Material.ConsequenceId,
                });
            }

            var observation = new RelianceObservation
            {
                ObservationId = request.ObservationId,
                Cycle = action.Cycle,
                AgentId = actor.StableId,
                EnablingRulingId = ruling.RulingId,
                EnablingMutationId = mutation.MutationId,
                SourceActionEventId = action.ActionEventId,
                RecordedChoiceId = request.RecordedChoiceId,
                AbandonedAlternativeId = alternative.OptionId,
                ResourceId = request.ResourceId,
                RecordedResourceDelta = actorResourceDelta,
            };

            InstitutionalTimelineEntry timelineEntry = CreateTimelineEntry(
                run.Report,
                action.Cycle,
                InstitutionalTimelineKind.RelianceCreated,
                action.ActionEventId,
                actor.StableId,
                observation.ObservationId);
            EnsureTimelineIdAvailable(run.Report, timelineEntry.EntryId);

            // Commit only after every authority/public row, collection, account,
            // projected value, and generated identifier has been validated.
            foreach (KeyValuePair<EconomicAccountState, int> staged in stagedAccountCredits)
                staged.Key.AvailableCredits = staged.Value;
            foreach (KeyValuePair<NeedState, int> staged in stagedNeedPressures)
                staged.Key.Pressure = staged.Value;
            alternative.Available = false;
            alternative.ChangedByActionEventId = action.ActionEventId;
            run.Report.MaterialConsequences.AddRange(materials);
            run.RelianceLedger.Add(reliance);
            run.Report.RelianceObservations.Add(observation);
            run.Report.Timeline.Add(timelineEntry);

            return new RelianceCreationResult
            {
                Created = true,
                FailureReason = RelianceFailureReason.None,
                Reliance = reliance,
                Observation = observation,
                MaterialConsequences = materials,
            };
        }

        internal static RelianceRecoveryResult TryCreateRecoveryAfterReversal(
            InstitutionalConsequenceRun run,
            Ruling reversal,
            RelianceRecoveryRequest request)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.Report == null)
                throw new InvalidOperationException("Recovery requires a public report.");
            if (!ValidIdentifier(request?.RelianceEventId) ||
                !ValidIdentifier(request.CaseIdPrefix) ||
                !ValidIdentifier(request.ParentCaseId) ||
                !ValidIdentifier(request.RespondentId) ||
                !ValidIdentifier(request.OfficialIssueId) ||
                request.Facts == null)
            {
                return RecoveryFailed(RelianceRecoveryFailureReason.InvalidRequest);
            }

            CaseFactSet detachedFacts;
            try
            {
                request.Facts.Validate();
                detachedFacts = request.Facts.Copy();
            }
            catch (InvalidOperationException)
            {
                return RecoveryFailed(RelianceRecoveryFailureReason.InvalidRequest);
            }

            RelianceEvent reliance = FindReliance(run, request.RelianceEventId);
            if (reliance == null)
                return RecoveryFailed(RelianceRecoveryFailureReason.RelianceNotFound);
            if (reversal == null || FindRuling(run.Report, reversal.RulingId) != reversal ||
                !string.Equals(reversal.CaseId, request.ParentCaseId,
                    StringComparison.Ordinal))
            {
                return RecoveryFailed(
                    RelianceRecoveryFailureReason.ReversalRulingNotFound);
            }
            if (reversal.Disposition != RulingDisposition.ReversedAndDenied &&
                reversal.Disposition != RulingDisposition.ReversedAndRecognised)
            {
                return RecoveryFailed(
                    RelianceRecoveryFailureReason.RulingIsNotAReversal);
            }
            if (reversal.Cycle < reliance.Cycle)
                return RecoveryFailed(RelianceRecoveryFailureReason.InvalidChronology);

            ObservedAgentAction action = InstitutionalTimeline.FindObservedAction(
                run.Report,
                reliance.SourceActionEventId);
            if (action == null ||
                !string.Equals(action.ActorId, reliance.AgentId,
                    StringComparison.Ordinal))
            {
                return RecoveryFailed(
                    RelianceRecoveryFailureReason.SourceActionNotObserved);
            }

            string caseId = $"{request.CaseIdPrefix}:{reliance.RelianceEventId}";
            if (caseId.Length > MaximumIdentifierLength * 2)
                return RecoveryFailed(RelianceRecoveryFailureReason.InvalidRequest);
            if (InstitutionalTimeline.FindDescendantCase(run.Report, caseId) != null)
                return RecoveryFailed(
                    RelianceRecoveryFailureReason.DuplicateRecoveryCase);

            var connectedAgents = new List<string>();
            AddUnique(connectedAgents, reliance.AgentId);
            AddUnique(connectedAgents, reliance.BeneficiaryAgentId);
            AddUnique(connectedAgents, reliance.HouseholdAgentId);
            var recovery = new DescendantCase
            {
                CaseId = caseId,
                ParentCaseId = request.ParentCaseId,
                OpenedCycle = reversal.Cycle,
                Kind = DescendantCaseKind.Reliance,
                Status = DescendantCaseStatus.Open,
                ParentCauseId = reversal.RulingId,
                OriginatingEventId = reliance.SourceActionEventId,
                OriginatingRulingId = reversal.RulingId,
                CausalAgentActionId = reliance.SourceActionEventId,
                ClaimantAgentId = reliance.AgentId,
                RespondentId = request.RespondentId,
                OfficialIssueId = request.OfficialIssueId,
                OfficialIdentityConditionId = request.OfficialIdentityConditionId,
                OfficialEmployerId = request.OfficialEmployerId,
                Facts = detachedFacts,
                ConnectedAgentIds = connectedAgents,
                SourceActionEventIds = new List<string>
                {
                    reliance.SourceActionEventId,
                },
            };
            run.Report.DescendantCases.Add(recovery);
            action.ResultDescendantCaseIds.Add(recovery.CaseId);
            reliance.SurvivedReversal = true;
            InstitutionalTimeline.Add(
                run.Report,
                reversal.Cycle,
                InstitutionalTimelineKind.DescendantCaseOpened,
                reversal.RulingId,
                recovery.CaseId,
                recovery.Kind.ToString());
            return new RelianceRecoveryResult
            {
                Created = true,
                FailureReason = RelianceRecoveryFailureReason.None,
                RecoveryCase = recovery,
            };
        }

        private static RelianceFailureReason ValidateRequest(
            RelianceCreationRequest request)
        {
            if (request == null ||
                !ValidIdentifier(request.RelianceEventId) ||
                !ValidIdentifier(request.ObservationId) ||
                !ValidIdentifier(request.SourceActionEventId) ||
                !ValidIdentifier(request.ActorAgentId) ||
                !Enum.IsDefined(typeof(SocietyActionKind), request.ExpectedActionKind) ||
                (request.ExpectedActionKind != SocietyActionKind.Work &&
                 request.ExpectedActionKind != SocietyActionKind.SeekAid &&
                 request.ExpectedActionKind != SocietyActionKind.Appeal) ||
                !ValidIdentifier(request.ExpectedOpportunityId) ||
                !ValidIdentifier(request.BeneficiaryAgentId) ||
                !ValidIdentifier(request.EnablingRulingId) ||
                !ValidIdentifier(request.EnablingMutationId) ||
                !ValidIdentifier(request.RequiredStatusId) ||
                !ValidIdentifier(request.ChoiceId) ||
                !ValidIdentifier(request.RecordedChoiceId) ||
                !ValidIdentifier(request.AbandonedAlternativeId) ||
                !ValidIdentifier(request.ResourceId) ||
                request.Effects == null || request.Effects.Count == 0 ||
                request.Effects.Count > MaximumEffects)
            {
                return RelianceFailureReason.InvalidRequest;
            }

            bool hasActorEffect = false;
            var effectIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < request.Effects.Count; i++)
            {
                RelianceEffectDelta effect = request.Effects[i];
                if (effect == null ||
                    !ValidIdentifier(effect.EffectId) ||
                    !effectIds.Add(effect.EffectId) ||
                    !Enum.IsDefined(typeof(RelianceEffectRecipient), effect.Recipient) ||
                    !Enum.IsDefined(typeof(MaterialConsequenceKind), effect.MaterialKind) ||
                    Math.Abs((long)effect.ResourceDelta) >
                        MaximumResourceDeltaMagnitude ||
                    Math.Abs((long)effect.NeedPressureDelta) >
                        MaximumNeedDeltaMagnitude ||
                    (!effect.Need.HasValue && effect.NeedPressureDelta != 0) ||
                    (effect.Need.HasValue &&
                     !Enum.IsDefined(typeof(NeedKind), effect.Need.Value)) ||
                    (effect.ResourceDelta != 0 &&
                     !ValidIdentifier(effect.ResourceId ?? request.ResourceId)) ||
                    (effect.ResourceDelta == 0 && effect.NeedPressureDelta == 0))
                {
                    return RelianceFailureReason.InvalidRequest;
                }
                if (effect.Recipient == RelianceEffectRecipient.Actor)
                    hasActorEffect = true;
                if (effect.Recipient == RelianceEffectRecipient.RelatedAgent &&
                    !ValidIdentifier(request.RelatedAgentId))
                {
                    return RelianceFailureReason.InvalidRequest;
                }
            }
            return hasActorEffect
                ? RelianceFailureReason.None
                : RelianceFailureReason.InvalidRequest;
        }

        private static RelianceCreationResult Failed(RelianceFailureReason reason)
        {
            return new RelianceCreationResult
            {
                Created = false,
                FailureReason = reason,
            };
        }

        private static RelianceRecoveryResult RecoveryFailed(
            RelianceRecoveryFailureReason reason)
        {
            return new RelianceRecoveryResult
            {
                Created = false,
                FailureReason = reason,
            };
        }

        private static AgentActionTrace FindTrace(
            InstitutionalConsequenceRun run,
            string actionEventId)
        {
            for (int i = 0; i < run.AssessorActionTraces.Count; i++)
            {
                AgentActionTrace trace = run.AssessorActionTraces[i];
                if (trace.ResultEventIds != null &&
                    trace.ResultEventIds.Contains(actionEventId)) return trace;
            }
            return null;
        }

        private static bool TraceReadsStatus(
            AgentActionTrace trace,
            string statusId,
            bool recognisedState)
        {
            if (trace.PerceptionSnapshot?.Standing == null ||
                trace.PerceptionSnapshot.Standing.IsRecognised(statusId) != recognisedState ||
                trace.InputSnapshot == null)
            {
                return false;
            }

            if (trace.Action == SocietyActionKind.Work)
            {
                return HasRequiredStatus(
                    trace.InputSnapshot.WorkOpportunities,
                    trace.OpportunityId,
                    statusId,
                    recognisedState);
            }
            if (trace.Action == SocietyActionKind.SeekAid)
            {
                return HasRequiredStatus(
                    trace.InputSnapshot.AidOpportunities,
                    trace.OpportunityId,
                    statusId,
                    recognisedState);
            }
            return false;
        }

        private static bool HasRequiredStatus(
            IReadOnlyList<WorkOpportunity> opportunities,
            string opportunityId,
            string statusId,
            bool recognisedState)
        {
            if (opportunities == null) return false;
            int matches = 0;
            bool requirementMatches = false;
            for (int i = 0; i < opportunities.Count; i++)
            {
                WorkOpportunity opportunity = opportunities[i];
                if (opportunity == null || !string.Equals(
                        opportunity.OpportunityId, opportunityId, StringComparison.Ordinal))
                    continue;
                matches++;
                requirementMatches = string.Equals(
                        opportunity.RequiredOfficialStatusId, statusId,
                        StringComparison.Ordinal) &&
                    opportunity.RequiredOfficialStatusRecognised == recognisedState;
            }
            return matches == 1 && requirementMatches;
        }

        private static bool HasRequiredStatus(
            IReadOnlyList<AidOpportunity> opportunities,
            string opportunityId,
            string statusId,
            bool recognisedState)
        {
            if (opportunities == null) return false;
            int matches = 0;
            bool requirementMatches = false;
            for (int i = 0; i < opportunities.Count; i++)
            {
                AidOpportunity opportunity = opportunities[i];
                if (opportunity == null || !string.Equals(
                        opportunity.OpportunityId, opportunityId, StringComparison.Ordinal))
                    continue;
                matches++;
                requirementMatches = string.Equals(
                        opportunity.RequiredOfficialStatusId, statusId,
                        StringComparison.Ordinal) &&
                    opportunity.RequiredOfficialStatusRecognised == recognisedState;
            }
            return matches == 1 && requirementMatches;
        }

        private static RelianceEvent FindReliance(
            InstitutionalConsequenceRun run,
            string relianceEventId)
        {
            for (int i = 0; i < run.RelianceLedger.Count; i++)
            {
                if (string.Equals(run.RelianceLedger[i].RelianceEventId,
                    relianceEventId, StringComparison.Ordinal))
                    return run.RelianceLedger[i];
            }
            return null;
        }

        private static RelianceObservation FindObservation(
            InstitutionalConsequenceReport report,
            string observationId)
        {
            for (int i = 0; i < report.RelianceObservations.Count; i++)
            {
                if (string.Equals(report.RelianceObservations[i].ObservationId,
                    observationId, StringComparison.Ordinal))
                    return report.RelianceObservations[i];
            }
            return null;
        }

        private static OfficialStatusMutation FindMutation(
            InstitutionalConsequenceReport report,
            string mutationId)
        {
            for (int i = 0; i < report.OfficialStatusMutations.Count; i++)
            {
                if (string.Equals(report.OfficialStatusMutations[i].MutationId,
                    mutationId, StringComparison.Ordinal))
                    return report.OfficialStatusMutations[i];
            }
            return null;
        }

        private static Ruling FindRuling(
            InstitutionalConsequenceReport report,
            string rulingId)
        {
            if (report == null || string.IsNullOrEmpty(rulingId)) return null;
            for (int i = 0; i < report.Rulings.Count; i++)
            {
                if (string.Equals(report.Rulings[i].RulingId,
                    rulingId, StringComparison.Ordinal)) return report.Rulings[i];
            }
            return null;
        }

        private static AlternativeOptionState FindAlternative(
            InstitutionalConsequenceRun run,
            string optionId)
        {
            for (int i = 0; i < run.AlternativeOptions.Count; i++)
            {
                if (string.Equals(run.AlternativeOptions[i].OptionId,
                    optionId, StringComparison.Ordinal)) return run.AlternativeOptions[i];
            }
            return null;
        }

        private static EconomicAccountState FindAccount(
            InstitutionalConsequenceRun run,
            string agentId)
        {
            EconomicAccountState matched = null;
            int matches = 0;
            for (int i = 0; i < run.EconomicAccounts.Count; i++)
            {
                EconomicAccountState account = run.EconomicAccounts[i];
                if (account == null || !string.Equals(account.AgentId,
                        agentId, StringComparison.Ordinal)) continue;
                matched = account;
                matches++;
            }
            if (matches > 1)
                throw new InvalidOperationException(
                    $"Expected one economic account for {agentId}, found {matches}.");
            return matched;
        }

        private static AgentState ResolveTarget(
            RelianceEffectRecipient recipient,
            AgentState actor,
            AgentState beneficiary,
            AgentState related)
        {
            return recipient switch
            {
                RelianceEffectRecipient.Actor => actor,
                RelianceEffectRecipient.Beneficiary => beneficiary,
                RelianceEffectRecipient.RelatedAgent => related,
                _ => null,
            };
        }

        private static MaterialConsequence CreateMaterial(
            int index,
            long cycle,
            string causeId,
            string agentId,
            MaterialConsequenceKind kind,
            string kindId,
            string resourceId,
            int resourceDelta,
            string relianceEventId,
            NeedKind? need,
            int needBefore,
            int needAfter)
        {
            var material = new MaterialConsequence
            {
                ConsequenceId =
                    $"material:{cycle}:{index}:" +
                    $"{agentId}:{kind}:{relianceEventId}",
                Cycle = cycle,
                CauseId = causeId,
                AgentId = agentId,
                Kind = kind,
                KindId = string.IsNullOrWhiteSpace(kindId) ? kind.ToString() : kindId,
                ResourceId = resourceId,
                ResourceDelta = resourceDelta,
                HasNeedEffect = need.HasValue,
                Need = need ?? default,
                NeedPressureBefore = needBefore,
                NeedPressureAfter = needAfter,
            };
            return material;
        }

        private static void ValidateCreationCollections(
            InstitutionalConsequenceRun run)
        {
            if (run.AssessorActionTraces == null ||
                run.RelianceLedger == null ||
                run.EconomicAccounts == null ||
                run.AlternativeOptions == null ||
                run.FinalSocietyState.Agents == null ||
                run.Report.ObservedAgentActions == null ||
                run.Report.Rulings == null ||
                run.Report.OfficialStatusMutations == null ||
                run.Report.RelianceObservations == null ||
                run.Report.MaterialConsequences == null ||
                run.Report.Timeline == null)
            {
                throw new InvalidOperationException(
                    "Reliance creation requires initialized authority and public collections.");
            }
        }

        private static int ProjectedCredits(
            EconomicAccountState account,
            IReadOnlyDictionary<EconomicAccountState, int> stagedCredits)
        {
            return stagedCredits.TryGetValue(account, out int value)
                ? value
                : account.AvailableCredits;
        }

        private static int ProjectedPressure(
            AgentState agent,
            NeedKind kind,
            IReadOnlyDictionary<NeedState, int> stagedPressures)
        {
            NeedState need = agent?.GetNeed(kind);
            if (need == null) return 0;
            return stagedPressures.TryGetValue(need, out int value)
                ? value
                : need.Pressure;
        }

        private static void EnsureMaterialIdAvailable(
            InstitutionalConsequenceReport report,
            string consequenceId,
            ISet<string> stagedIds)
        {
            if (!stagedIds.Add(consequenceId))
                throw new InvalidOperationException(
                    $"Material consequence id '{consequenceId}' is duplicated in the staged reliance.");
            for (int i = 0; i < report.MaterialConsequences.Count; i++)
            {
                if (string.Equals(report.MaterialConsequences[i]?.ConsequenceId,
                        consequenceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Material consequence id '{consequenceId}' already exists.");
                }
            }
        }

        private static InstitutionalTimelineEntry CreateTimelineEntry(
            InstitutionalConsequenceReport report,
            long cycle,
            InstitutionalTimelineKind kind,
            string causeId,
            string subjectId,
            string detailId)
        {
            return new InstitutionalTimelineEntry
            {
                EntryId = $"timeline:{cycle}:{report.Timeline.Count}:{kind}",
                Cycle = cycle,
                Kind = kind,
                CauseId = causeId,
                SubjectId = subjectId,
                DetailId = detailId,
            };
        }

        private static void EnsureTimelineIdAvailable(
            InstitutionalConsequenceReport report,
            string entryId)
        {
            for (int i = 0; i < report.Timeline.Count; i++)
            {
                if (string.Equals(report.Timeline[i]?.EntryId, entryId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Institutional timeline id '{entryId}' already exists.");
                }
            }
        }

        private static int Pressure(AgentState agent, NeedKind kind)
        {
            return agent?.GetNeed(kind)?.Pressure ?? 0;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
                values.Add(value);
        }

        private static bool ValidIdentifier(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Length <= MaximumIdentifierLength;
        }

        private sealed class EffectApplication
        {
            internal RelianceEffectDelta Effect;
            internal AgentState Agent;
            internal EconomicAccountState Account;
            internal int CreditsBefore;
            internal int CreditsAfter;
            internal NeedState Need;
            internal int NeedBefore;
            internal int NeedAfter;
            internal MaterialConsequence Material;
        }
    }
}
