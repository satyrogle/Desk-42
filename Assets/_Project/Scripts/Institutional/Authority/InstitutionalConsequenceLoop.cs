using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// A deterministic 15-cycle institutional proof. The calendar opens work,
    /// evidence, aid, and appeal windows. Stateful opportunities determine who is
    /// eligible; the existing utility engine decides whether each agent acts.
    /// </summary>
    public static class InstitutionalConsequenceLoop
    {
        public const int FinalCycle = 15;
        public const string PrimaryCaseId = "case.workplace-identity-injury";
        public const string BaselineAllocationCaseId = "case.baseline-shift-allocation";
        public const string LaterCaseId = "case.related-identity-continuity";
        public const string BaselineAllocationRulingId = "ruling:allocation:baseline:0";
        public const string ContinuityIssueId = "issue.employment-identity-continuity";
        public const string IdentityConditionId = "identity.superseded-while-work-continued";
        public const string TreatmentEntitlementStatusId = "treatment-entitlement";

        private const string AllocationId = "work-allocation.shift-17";
        private const string EmployerResponsePurposeId = "work.employer-response";
        private const string EmergencyAidPurposeId = "aid.emergency-first-aid";
        private const string TreatmentAidPurposeId = "aid.continuity-treatment";
        private const int AllocationWage = 45;
        private const string PrimaryAppealDocketId = "docket.primary-appeal";
        private const string RelatedClaimDocketId = "docket.related-claim";

        public static InstitutionalConsequenceReport RunProof(
            int masterSeed,
            InstitutionalPolicyConfiguration policy)
        {
            return RunForAssessor(masterSeed, policy).Report;
        }

        internal static InstitutionalConsequenceRun RunForAssessor(
            int masterSeed,
            InstitutionalPolicyConfiguration policy,
            SocietyState initialState = null,
            InstitutionalIncidentRoles suppliedRoles = null,
            bool validateProof = true)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            policy.Validate();

            SocietyState society = initialState ?? PrototypePopulationFactory.Create(masterSeed);
            if (society.CurrentTick != 0)
                throw new InvalidOperationException("The proof must begin from cycle zero.");
            if (society.Agents.Count != PrototypePopulationFactory.PrototypePopulationSize)
                throw new InvalidOperationException("The proof requires the existing eight-person population.");

            society.MasterSeed = masterSeed;
            society.Regime = policy.CreateRegime();
            InstitutionalIncidentRoles roles = suppliedRoles ?? BuildRoles(society);
            ValidateRoles(society, roles);
            PrepareOfficialProjection(society, roles);

            var run = new InstitutionalConsequenceRun
            {
                Report = new InstitutionalConsequenceReport
                {
                    MasterSeed = masterSeed,
                    PolicyConfigurationId = policy.PolicyConfigurationId,
                    PrimaryCaseId = PrimaryCaseId,
                },
                FinalSocietyState = society,
            };
            var context = new LoopContext(run, policy, roles);
            InitialiseEconomicState(context);
            InitialiseBaselineAllocation(context);
            BeginIncident(context);

            var simulation = new SocietySimulation();
            for (int cycle = 1; cycle <= FinalCycle; cycle++)
            {
                SimulationInput input = FixedInputForCycle(cycle);
                AttachAvailableOpportunities(context, cycle, input);
                SimulationStepResult step = simulation.Advance(society, input);
                CaptureAgentActions(context, step);
                ProjectAgentActions(context, step);
                ResolveDeadline(context, cycle);
            }

            SnapshotState(context);
            run.Report.FinalCycle = society.CurrentTick;
            if (validateProof)
                InstitutionalConsequenceValidator.Validate(run.Report);
            return run;
        }

        private static InstitutionalIncidentRoles BuildRoles(SocietyState state)
        {
            AgentState claimant = FindUniqueRoleAgent(state,
                agent => HasAnomalyBoundTo(agent, "identity-continuity"),
                "identity-affected claimant");
            AgentState witness = FindUniqueRoleAgent(state,
                agent => agent.Standing.IsRecognised("records-access"),
                "records witness");
            AgentState employerRepresentative = FindUniqueRoleAgent(state,
                agent => agent.Standing.IsRecognised("management-authority"),
                "employer representative");
            AgentState householdMember = FindUniqueRoleAgent(state,
                agent => !string.Equals(agent.StableId, claimant.StableId,
                             StringComparison.Ordinal) &&
                         string.Equals(agent.HouseholdId, claimant.HouseholdId,
                             StringComparison.Ordinal),
                "connected household member");
            AgentState workerRepresentative = FindUniqueRoleAgent(state,
                agent => agent.Standing.IsRecognised("worker-representative"),
                "worker representative");
            AgentState clinicalAssessor = FindUniqueRoleAgent(state,
                agent => agent.Standing.IsRecognised("licensed-assessor"),
                "clinical assessor");
            AgentState laterClaimant = FindUniqueRoleAgent(state,
                agent => !string.Equals(agent.StableId, claimant.StableId,
                             StringComparison.Ordinal) &&
                         string.Equals(agent.EmployerId, claimant.EmployerId,
                             StringComparison.Ordinal) &&
                         agent.Standing.IsRecognised("adverse-decision"),
                "later adversely affected worker");
            AgentState contingentWorker = FindUniqueRoleAgent(state,
                agent => agent.Standing.IsRecognised("employment-authorisation"),
                "contingent allocation holder");
            return new InstitutionalIncidentRoles
            {
                PrimaryClaimantId = claimant.StableId,
                PrimaryWitnessId = witness.StableId,
                EmployerRepresentativeId = employerRepresentative.StableId,
                HouseholdMemberId = householdMember.StableId,
                WorkerRepresentativeId = workerRepresentative.StableId,
                ClinicalAssessorId = clinicalAssessor.StableId,
                LaterClaimantId = laterClaimant.StableId,
                ContingentWorkerId = contingentWorker.StableId,
                EmployerId = claimant.EmployerId,
            };
        }

        private static AgentState FindUniqueRoleAgent(
            SocietyState state,
            Predicate<AgentState> predicate,
            string roleLabel)
        {
            AgentState result = null;
            for (int i = 0; i < state.Agents.Count; i++)
            {
                AgentState candidate = state.Agents[i];
                if (!predicate(candidate)) continue;
                if (result != null)
                    throw new InvalidOperationException($"Multiple agents satisfy role {roleLabel}.");
                result = candidate;
            }
            return result ?? throw new InvalidOperationException(
                $"No agent satisfies role {roleLabel}.");
        }

        private static bool HasAnomalyBoundTo(AgentState agent, string statusId)
        {
            for (int i = 0; i < agent.AnomalyRules.Count; i++)
                if (string.Equals(agent.AnomalyRules[i].RequiredOfficialStatusId,
                    statusId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static void ValidateRoles(SocietyState state, InstitutionalIncidentRoles roles)
        {
            if (roles == null) throw new ArgumentNullException(nameof(roles));
            var ids = new HashSet<string>(StringComparer.Ordinal)
            {
                roles.PrimaryClaimantId,
                roles.PrimaryWitnessId,
                roles.EmployerRepresentativeId,
                roles.HouseholdMemberId,
                roles.WorkerRepresentativeId,
                roles.ClinicalAssessorId,
                roles.LaterClaimantId,
                roles.ContingentWorkerId,
            };
            if (ids.Count != PrototypePopulationFactory.PrototypePopulationSize || ids.Contains(null))
                throw new InvalidOperationException("Every proof role must reference a different agent.");
            foreach (string id in ids)
                if (state.GetAgent(id) == null)
                    throw new InvalidOperationException($"Proof role references unknown agent {id}.");
            if (string.IsNullOrWhiteSpace(roles.EmployerId))
                throw new InvalidOperationException("The incident requires an employer.");
        }

        private static void PrepareOfficialProjection(SocietyState society, InstitutionalIncidentRoles roles)
        {
            AgentState claimant = society.GetAgent(roles.PrimaryClaimantId);
            AgentState employerRepresentative = society.GetAgent(roles.EmployerRepresentativeId);
            AgentState laterClaimant = society.GetAgent(roles.LaterClaimantId);
            AgentState contingentWorker = society.GetAgent(roles.ContingentWorkerId);

            claimant.Standing.SetRecognised("identity-continuity", false);
            claimant.Standing.SetRecognised("adverse-decision", false);
            claimant.Standing.SetRecognised("appeal-pending", false);
            claimant.Standing.SetRecognised(TreatmentEntitlementStatusId, false);
            claimant.Standing.SetRecognised("provisional-relief-paid", false);
            employerRepresentative.Standing.SetRecognised("adverse-decision", false);
            employerRepresentative.Standing.SetRecognised("appeal-pending", false);
            laterClaimant.Standing.SetRecognised("adverse-decision", false);
            laterClaimant.Standing.SetRecognised("appeal-pending", false);
            laterClaimant.Standing.SetRecognised("employment-continuity", false);
            contingentWorker.Standing.SetRecognised("paid-shift-allocation", false);
        }

        private static void InitialiseEconomicState(LoopContext context)
        {
            for (int i = 0; i < context.Society.Agents.Count; i++)
            {
                context.Run.EconomicAccounts.Add(new EconomicAccountState
                {
                    AgentId = context.Society.Agents[i].StableId,
                    AvailableCredits = 100,
                    CommittedIncome = 0,
                });
            }
            context.Run.AlternativeOptions.Add(new AlternativeOptionState
            {
                OptionId = "alternative.household-emergency-fund",
                AgentId = context.Roles.HouseholdMemberId,
                Available = true,
            });
        }

        private static void InitialiseBaselineAllocation(LoopContext context)
        {
            var finding = new OfficialFinding
            {
                FindingId = "finding:allocation:baseline:0",
                CaseId = BaselineAllocationCaseId,
                Cycle = 0,
                IssueId = ContinuityIssueId,
                Disposition = FindingDisposition.NotEstablished,
                WeightedEvidenceScore = 0,
                RequiredScore = context.Policy.LaterRecognitionThreshold,
            };
            context.Report.OfficialFindings.Add(finding);
            var ruling = new Ruling
            {
                RulingId = BaselineAllocationRulingId,
                CaseId = BaselineAllocationCaseId,
                Cycle = 0,
                PolicyConfigurationId = "baseline.identifier-mismatch",
                PolicyVersion = "baseline.identifier-mismatch.v1",
                Disposition = RulingDisposition.Denied,
                FindingId = finding.FindingId,
                ConfidenceMinimum = 0,
                ConfidenceMaximum = 0,
                AppliedPolicyIds = new List<string> { "baseline.identifier-mismatch.v1" },
                SkippedProcedureIds = new List<string> { "procedure.worker-hearing" },
            };
            context.Report.Rulings.Add(ruling);
            AddTimeline(context.Report, 0, InstitutionalTimelineKind.RulingIssued,
                ruling.RulingId, BaselineAllocationCaseId, ruling.Disposition.ToString());
            ApplyStatusMutation(context, ruling, context.Roles.LaterClaimantId,
                "adverse-decision", true, 0);
            ApplyStatusMutation(context, ruling, context.Roles.ContingentWorkerId,
                "paid-shift-allocation", true, 0);

            var allocation = new WorkAllocationState
            {
                AllocationId = AllocationId,
                EmployerId = context.Roles.EmployerId,
                OriginalWorkerId = context.Roles.LaterClaimantId,
                PaidHolderAgentId = context.Roles.ContingentWorkerId,
                IdentityConditionId = IdentityConditionId,
                CommittedWage = AllocationWage,
                SourceRulingId = ruling.RulingId,
                LastMutationCauseId = ruling.RulingId,
            };
            context.Run.WorkAllocations.Add(allocation);
            FindAccount(context, allocation.PaidHolderAgentId).CommittedIncome += allocation.CommittedWage;
            context.BaselineAllocationRuling = ruling;
        }

        private static void BeginIncident(LoopContext context)
        {
            var lived = new LivedEvent
            {
                LivedEventId = "lived:workplace-injury:1",
                Cycle = 1,
                EventKindId = "workplace.press-injury",
                SubjectAgentId = context.Roles.PrimaryClaimantId,
                CauseEntityId = "equipment.reclamation-press-17",
                AffectedNeed = NeedKind.Health,
                NeedPressureDelta = 35,
            };
            context.Run.AuthoritativeEvents.Add(lived);
            AgentState claimant = context.Society.GetAgent(lived.SubjectAgentId);
            NeedState need = claimant.GetNeed(lived.AffectedNeed);
            need.Pressure = Clamp(
                need.Pressure + lived.NeedPressureDelta,
                0,
                100);

            string injuryBeliefId = $"belief.observed-injury:{claimant.SimulationOrdinal}:1";
            claimant.Beliefs.Add(new BeliefState
            {
                BeliefId = injuryBeliefId,
                PropositionId = "injury.press-caused-harm",
                SubjectId = claimant.StableId,
                ObjectId = lived.CauseEntityId,
                SourceId = "observation.embodied-injury",
                Confidence = 95,
                Secrecy = 20,
                EmotionalWeight = 90,
                AcquiredTick = lived.Cycle,
            });
            context.Run.AuthoritativeBeliefLinks.Add(new AuthoritativeBeliefLink
            {
                LivedEventId = lived.LivedEventId,
                AgentId = claimant.StableId,
                BeliefId = injuryBeliefId,
            });

            context.PendingAid.Add(new AidOpportunity
            {
                OpportunityId = "aid-opportunity:emergency:1",
                PurposeId = EmergencyAidPurposeId,
                SourceCauseId = lived.LivedEventId,
                UtilityBonus = 60,
                EligibleAgentIds = new List<string> { lived.SubjectAgentId },
            });
            AddTimeline(context.Report, 1, InstitutionalTimelineKind.Incident,
                "calendar.workplace-injury", lived.SubjectAgentId,
                "observable.workplace-injury-reported");
        }

        private static SimulationInput FixedInputForCycle(int cycle)
        {
            var input = new SimulationInput
            {
                IncidentId = PrimaryCaseId,
                WorkAvailable = true,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
                VisibleAgentIds = new List<string>(),
                RestrictAidToOpportunities = true,
                RestrictAppealToOpportunities = true,
            };
            switch (cycle)
            {
                case 1:
                    input.IncidentId = "incident.workplace-injury";
                    input.AidAvailable = true;
                    break;
                case 4:
                case 8:
                case 12:
                case 15:
                    input.VisibleAgentIds = null;
                    break;
                case 3:
                case 9:
                    input.DisclosureRequested = true;
                    break;
                case 6:
                    input.AidAvailable = true;
                    break;
                case 10:
                    input.OpenDocketId = PrimaryAppealDocketId;
                    input.AppealWindowOpen = true;
                    break;
                case 13:
                    input.OpenDocketId = RelatedClaimDocketId;
                    input.AppealWindowOpen = true;
                    break;
            }
            return input;
        }

        private static void AttachAvailableOpportunities(
            LoopContext context,
            int cycle,
            SimulationInput input)
        {
            for (int i = 0; i < context.PendingWork.Count; i++)
                if (context.PendingWork[i].EarliestCycle <= cycle)
                    input.WorkOpportunities.Add(context.PendingWork[i]);
            for (int i = 0; i < context.PendingAid.Count; i++)
                input.AidOpportunities.Add(context.PendingAid[i]);
            for (int i = 0; i < context.PendingCases.Count; i++)
            {
                CaseOpportunityState pending = context.PendingCases[i];
                if (!pending.Filed && pending.EarliestFilingCycle <= cycle &&
                    string.Equals(pending.Opportunity.DocketId, input.OpenDocketId,
                        StringComparison.Ordinal))
                    input.AppealOpportunities.Add(pending.Opportunity);
            }
        }

        private static void CaptureAgentActions(LoopContext context, SimulationStepResult step)
        {
            for (int decisionIndex = 0; decisionIndex < step.Decisions.Count; decisionIndex++)
            {
                AgentDecision decision = step.Decisions[decisionIndex];
                var trace = new AgentActionTrace
                {
                    Cycle = step.Tick,
                    DecisionId = decision.DecisionId,
                    CandidateId = decision.CandidateId,
                    ActorId = decision.ActorId,
                    Action = decision.Action,
                    TargetId = decision.TargetId,
                    OpportunityId = decision.OpportunityId,
                    SubjectBeliefId = decision.SubjectBeliefId,
                    UtilityScore = decision.Score,
                    PerceptionSnapshot = decision.PerceptionSnapshot,
                    RegimeSnapshot = decision.RegimeSnapshot,
                    InputSnapshot = decision.InputSnapshot,
                };
                for (int reasonIndex = 0; reasonIndex < decision.Reasons.Count; reasonIndex++)
                {
                    DecisionReason reason = decision.Reasons[reasonIndex];
                    trace.Reasons.Add(new DecisionReason
                    {
                        ReasonId = reason.ReasonId,
                        SourceId = reason.SourceId,
                        ScoreDelta = reason.ScoreDelta,
                    });
                }
                for (int candidateIndex = 0;
                     candidateIndex < decision.CandidateEvaluations.Count;
                     candidateIndex++)
                {
                    CandidateEvaluation source = decision.CandidateEvaluations[candidateIndex];
                    var evaluation = new CandidateEvaluation
                    {
                        CandidateId = source.CandidateId,
                        Action = source.Action,
                        TargetId = source.TargetId,
                        OpportunityId = source.OpportunityId,
                        SubjectBeliefId = source.SubjectBeliefId,
                        Score = source.Score,
                    };
                    for (int reasonIndex = 0; reasonIndex < source.Reasons.Count; reasonIndex++)
                    {
                        DecisionReason reason = source.Reasons[reasonIndex];
                        evaluation.Reasons.Add(new DecisionReason
                        {
                            ReasonId = reason.ReasonId,
                            SourceId = reason.SourceId,
                            ScoreDelta = reason.ScoreDelta,
                        });
                    }
                    trace.CandidateEvaluations.Add(evaluation);
                }

                SocietyEvent causedEvent = null;
                for (int eventIndex = 0; eventIndex < step.Events.Count; eventIndex++)
                {
                    SocietyEvent candidate = step.Events[eventIndex];
                    if (!string.Equals(candidate.CauseDecisionId, decision.DecisionId,
                        StringComparison.Ordinal)) continue;
                    trace.ResultEventIds.Add(candidate.EventId);
                    if (causedEvent == null) causedEvent = candidate;
                }
                context.Run.AssessorActionTraces.Add(trace);
                if (causedEvent == null) continue;
                context.Report.ObservedAgentActions.Add(new ObservedAgentAction
                {
                    Cycle = step.Tick,
                    ActionEventId = causedEvent.EventId,
                    ActorId = causedEvent.ActorId,
                    Activity = PlayerActivity(causedEvent.Kind),
                    TargetId = PlayerTarget(causedEvent),
                });
            }
        }

        private static ObservedActivityKind PlayerActivity(SocietyEventKind kind)
        {
            switch (kind)
            {
                case SocietyEventKind.WorkPerformed:
                    return ObservedActivityKind.WorkPerformed;
                case SocietyEventKind.AidRequested:
                    return ObservedActivityKind.AidRequested;
                case SocietyEventKind.AssistanceGiven:
                    return ObservedActivityKind.AssistanceGiven;
                case SocietyEventKind.EvidenceDisclosed:
                    return ObservedActivityKind.EvidenceSubmitted;
                case SocietyEventKind.AppealFiled:
                    return ObservedActivityKind.AppealFiled;
                default:
                    return ObservedActivityKind.NoVisibleAction;
            }
        }

        private static string PlayerTarget(SocietyEvent societyEvent)
        {
            return societyEvent.Kind == SocietyEventKind.ResponseWithheld
                ? null
                : societyEvent.TargetId;
        }

        private static void ProjectAgentActions(LoopContext context, SimulationStepResult step)
        {
            for (int i = 0; i < step.Events.Count; i++)
            {
                SocietyEvent societyEvent = step.Events[i];
                switch (societyEvent.Kind)
                {
                    case SocietyEventKind.EvidenceDisclosed:
                        EvidenceArtifact disclosure = EvidenceFromDisclosure(societyEvent);
                        AddEvidence(context, disclosure);
                        LinkEvidenceToAuthoritativeBelief(context, societyEvent, disclosure);
                        break;
                    case SocietyEventKind.WorkPerformed:
                        ProjectWork(context, societyEvent);
                        break;
                    case SocietyEventKind.AidRequested:
                        ProjectAid(context, societyEvent);
                        break;
                    case SocietyEventKind.AppealFiled:
                        FileAppeal(context, societyEvent);
                        break;
                }
            }
        }

        private static void ProjectWork(LoopContext context, SocietyEvent societyEvent)
        {
            WorkOpportunity task = FindWorkOpportunity(context, societyEvent.OpportunityId);
            if (task != null)
            {
                context.PendingWork.Remove(task);
                if (string.Equals(task.PurposeId, EmployerResponsePurposeId, StringComparison.Ordinal))
                {
                    EvidenceArtifact response = EvidenceFromAction(
                        societyEvent,
                        PrimaryCaseId,
                        EvidenceArtifactKind.ManagementRecord,
                        "record.employer-maintained-superseded-roster",
                        EvidenceEffect.OpposesFinding,
                        60);
                    response.OfficialEmployerId = context.Roles.EmployerId;
                    response.OfficialIdentityConditionId = IdentityConditionId;
                    AddEvidence(context, response);
                    AddTimeline(context.Report, societyEvent.Tick,
                        InstitutionalTimelineKind.EmployerResponded,
                        societyEvent.EventId,
                        task.SourceCauseId,
                        task.OpportunityId);
                }
                return;
            }

            AgentState actor = context.Society.GetAgent(societyEvent.ActorId);
            for (int i = 0; i < context.Run.WorkAllocations.Count; i++)
            {
                WorkAllocationState allocation = context.Run.WorkAllocations[i];
                if (!string.Equals(allocation.OriginalWorkerId, actor.StableId, StringComparison.Ordinal) ||
                    string.Equals(allocation.PaidHolderAgentId, actor.StableId, StringComparison.Ordinal) ||
                    !string.Equals(allocation.EmployerId, actor.EmployerId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (FindEvidenceByResource(context.Report, allocation.AllocationId) != null) continue;

                EvidenceArtifact artifact = EvidenceFromAction(
                    societyEvent,
                    LaterCaseId,
                    EvidenceArtifactKind.ActionRecord,
                    "record.work-performed-after-identifier-supersession",
                    EvidenceEffect.SupportsFinding,
                    40);
                artifact.OfficialEmployerId = allocation.EmployerId;
                artifact.OfficialIdentityConditionId = allocation.IdentityConditionId;
                artifact.OfficialResourceId = allocation.AllocationId;
                AddEvidence(context, artifact);
                context.PendingCases.Add(new CaseOpportunityState
                {
                    Opportunity = new AppealOpportunity
                    {
                        OpportunityId = $"appeal-opportunity:{allocation.AllocationId}",
                        DocketId = RelatedClaimDocketId,
                        CaseId = LaterCaseId,
                        ChallengedRulingId = allocation.SourceRulingId,
                        SourceCauseId = societyEvent.EventId,
                        HearingCycle = 14,
                        UtilityBonus = 30,
                        PartyAgentIds = new List<string> { actor.StableId },
                    },
                    DescendantKind = DescendantCaseKind.RelatedClaim,
                    ParentCaseId = BaselineAllocationCaseId,
                    IssueId = ContinuityIssueId,
                    EmployerId = artifact.OfficialEmployerId,
                    IdentityConditionId = artifact.OfficialIdentityConditionId,
                    ResourceId = artifact.OfficialResourceId,
                    OriginatingActionEventId = societyEvent.EventId,
                    EarliestFilingCycle = societyEvent.Tick + 1,
                });
            }
        }

        private static void ProjectAid(LoopContext context, SocietyEvent societyEvent)
        {
            AidOpportunity opportunity = FindAidOpportunity(context, societyEvent.OpportunityId);
            if (opportunity == null) return;
            context.PendingAid.Remove(opportunity);

            if (string.Equals(opportunity.PurposeId, EmergencyAidPurposeId, StringComparison.Ordinal))
            {
                EvidenceArtifact artifact = EvidenceFromAction(
                    societyEvent,
                    PrimaryCaseId,
                    EvidenceArtifactKind.ActionRecord,
                    "record.emergency-treatment-after-workplace-injury",
                    EvidenceEffect.SupportsFinding,
                    10);
                artifact.Reliability = 100;
                AddEvidence(context, artifact);
                context.Run.AuthoritativeEvidenceLinks.Add(new AuthoritativeEvidenceLink
                {
                    LivedEventId = opportunity.SourceCauseId,
                    EvidenceArtifactId = artifact.ArtifactId,
                    ObservationKindId = "observation.emergency-treatment-record",
                });
                return;
            }

            if (!string.Equals(opportunity.PurposeId, TreatmentAidPurposeId, StringComparison.Ordinal))
                return;
            EvidenceArtifact receipt = EvidenceFromAction(
                societyEvent,
                PrimaryCaseId,
                EvidenceArtifactKind.ActionRecord,
                "record.continuity-treatment-purchased",
                EvidenceEffect.SupportsFinding,
                15);
            AddEvidence(context, receipt);
            CreateReliance(context, societyEvent, opportunity);
        }

        private static void CreateReliance(
            LoopContext context,
            SocietyEvent aidEvent,
            AidOpportunity opportunity)
        {
            AgentActionTrace trace = FindAssessorTrace(context.Run, aidEvent.CauseDecisionId);
            if (trace == null || !TraceReadsStatus(trace, opportunity.RequiredOfficialStatusId)) return;
            OfficialStatusMutation dependency = FindLatestMutation(
                context.Report,
                aidEvent.ActorId,
                opportunity.RequiredOfficialStatusId,
                true);
            if (dependency == null) return;

            EconomicAccountState account = FindAccount(context, aidEvent.ActorId);
            AlternativeOptionState alternative = context.Run.AlternativeOptions[0];
            int creditsBefore = account.AvailableCredits;
            bool alternativeBefore = alternative.Available;
            account.AvailableCredits -= 45;
            alternative.Available = false;
            alternative.ChangedByActionEventId = aidEvent.EventId;
            AgentState household = context.Society.GetAgent(alternative.AgentId);
            int householdSubsistenceBefore = household.GetNeed(NeedKind.Subsistence).Pressure;
            ChangeNeedPressure(household, NeedKind.Subsistence, 15);
            int householdSubsistenceAfter = household.GetNeed(NeedKind.Subsistence).Pressure;
            AgentState relyingAgent = context.Society.GetAgent(aidEvent.ActorId);
            int agentSubsistenceBefore = relyingAgent.GetNeed(NeedKind.Subsistence).Pressure;
            ChangeNeedPressure(relyingAgent, NeedKind.Subsistence, 10);
            int agentSubsistenceAfter = relyingAgent.GetNeed(NeedKind.Subsistence).Pressure;

            int healthAfter = context.Society.GetAgent(aidEvent.ActorId)
                .GetNeed(NeedKind.Health).Pressure;
            var reliance = new RelianceEvent
            {
                RelianceEventId = "reliance:primary-treatment",
                Cycle = aidEvent.Tick,
                AgentId = aidEvent.ActorId,
                BeneficiaryAgentId = aidEvent.ActorId,
                ReliedOnRulingId = dependency.CauseId,
                ReliedOnMutationId = dependency.MutationId,
                SourceActionEventId = aidEvent.EventId,
                ChoiceId = "choice.purchase-continuity-treatment",
                AbandonedAlternativeId = alternative.OptionId,
                ResourceSpent = 45,
                HealthPressureAfterAction = healthAfter,
                AlternativeAvailableBefore = alternativeBefore,
                AlternativeAvailableAfter = alternative.Available,
                CreditsBefore = creditsBefore,
                CreditsAfter = account.AvailableCredits,
                AgentSubsistenceBefore = agentSubsistenceBefore,
                AgentSubsistenceAfter = agentSubsistenceAfter,
                HouseholdAgentId = household.StableId,
                HouseholdSubsistenceBefore = householdSubsistenceBefore,
                HouseholdSubsistenceAfter = householdSubsistenceAfter,
            };
            context.Run.RelianceLedger.Add(reliance);
            var observation = new RelianceObservation
            {
                ObservationId = "reliance-observation:primary-treatment",
                Cycle = aidEvent.Tick,
                AgentId = aidEvent.ActorId,
                EnablingRulingId = dependency.CauseId,
                EnablingMutationId = dependency.MutationId,
                SourceActionEventId = aidEvent.EventId,
                RecordedChoiceId = "recorded-choice.continuity-treatment",
                RecordedResourceDelta = -45,
            };
            context.Report.RelianceObservations.Add(observation);
            AddTimeline(context.Report, aidEvent.Tick,
                InstitutionalTimelineKind.RelianceCreated,
                aidEvent.EventId,
                aidEvent.ActorId,
                observation.ObservationId);
            AddMaterial(context, aidEvent.Tick, aidEvent.EventId, aidEvent.ActorId,
                MaterialConsequenceKind.RelianceSpent, -45);
        }

        private static void FileAppeal(LoopContext context, SocietyEvent filingEvent)
        {
            CaseOpportunityState pending = FindCaseOpportunity(
                context,
                filingEvent.OpportunityId);
            if (pending == null || pending.Filed) return;
            pending.Filed = true;

            var appeal = new Appeal
            {
                AppealId = $"appeal:{pending.Opportunity.CaseId}:{filingEvent.Tick}",
                CaseId = pending.Opportunity.CaseId,
                FiledCycle = filingEvent.Tick,
                HearingCycle = pending.Opportunity.HearingCycle,
                AppellantAgentId = filingEvent.ActorId,
                FilingActionEventId = filingEvent.EventId,
                ChallengedRulingId = pending.Opportunity.ChallengedRulingId,
                Disposition = AppealDisposition.Pending,
            };
            List<EvidenceArtifact> grounds = EvidenceForCase(
                context.Report,
                pending.Opportunity.CaseId,
                filingEvent.Tick);
            for (int i = 0; i < grounds.Count; i++)
                appeal.GroundsEvidenceArtifactIds.Add(grounds[i].ArtifactId);
            context.Report.Appeals.Add(appeal);
            pending.FiledAppealId = appeal.AppealId;
            AddTimeline(context.Report, filingEvent.Tick, InstitutionalTimelineKind.AppealFiled,
                filingEvent.EventId, filingEvent.ActorId, appeal.AppealId);

            EvidenceArtifact filingArtifact = EvidenceFromAction(
                filingEvent,
                pending.Opportunity.CaseId,
                EvidenceArtifactKind.ActionRecord,
                "record.appeal-filed",
                EvidenceEffect.Neutral,
                0);
            filingArtifact.OfficialEmployerId = pending.EmployerId;
            filingArtifact.OfficialIdentityConditionId = pending.IdentityConditionId;
            filingArtifact.OfficialResourceId = pending.ResourceId;
            AddEvidence(context, filingArtifact);

            DescendantCaseKind kind = pending.DescendantKind;
            var descendant = new DescendantCase
            {
                CaseId = kind == DescendantCaseKind.Appeal
                    ? "case.primary-appeal"
                    : LaterCaseId,
                OpenedCycle = filingEvent.Tick,
                Kind = kind,
                Status = DescendantCaseStatus.Open,
                ParentCaseId = pending.ParentCaseId,
                ParentCauseId = kind == DescendantCaseKind.Appeal
                    ? pending.Opportunity.ChallengedRulingId
                    : pending.OriginatingActionEventId,
                OriginatingEventId = pending.OriginatingActionEventId ?? filingEvent.EventId,
                OriginatingRulingId = pending.Opportunity.ChallengedRulingId,
                CausalAgentActionId = pending.OriginatingActionEventId ?? filingEvent.EventId,
                ClaimantAgentId = filingEvent.ActorId,
                RespondentId = pending.EmployerId ?? "branch-42",
                OfficialIssueId = pending.IssueId,
                OfficialIdentityConditionId = pending.IdentityConditionId,
                OfficialEmployerId = pending.EmployerId,
                ConnectedAgentIds = new List<string> { filingEvent.ActorId },
                SourceActionEventIds = new List<string> { filingEvent.EventId },
            };
            if (!string.IsNullOrEmpty(pending.OriginatingActionEventId) &&
                !descendant.SourceActionEventIds.Contains(pending.OriginatingActionEventId))
            {
                descendant.SourceActionEventIds.Insert(0, pending.OriginatingActionEventId);
            }
            context.Report.DescendantCases.Add(descendant);
            FindObservedAction(context.Report, filingEvent.EventId)?.ResultDescendantCaseIds
                .Add(descendant.CaseId);
            AddTimeline(context.Report, filingEvent.Tick, InstitutionalTimelineKind.DescendantCaseOpened,
                filingEvent.EventId, descendant.CaseId, descendant.Kind.ToString());
        }

        private static void ResolveDeadline(LoopContext context, int cycle)
        {
            switch (cycle)
            {
                case 5:
                    IssueInitialRuling(context);
                    break;
                case 11:
                    HearPrimaryAppeal(context);
                    break;
                case 14:
                    HearLaterAppeal(context);
                    break;
                case 15:
                    AddTimeline(context.Report, 15, InstitutionalTimelineKind.ComparisonClosed,
                        context.Policy.PolicyVersion, PrimaryCaseId, "comparison.abc-ready");
                    break;
            }
        }

        private static void IssueInitialRuling(LoopContext context)
        {
            List<EvidenceArtifact> evidence = EvidenceForCase(context.Report, PrimaryCaseId, 5);
            int score = ScoreEvidence(evidence, context.Policy);
            ScoreEvidenceBounds(evidence, context.Policy,
                out int confidenceMinimum, out int confidenceMaximum);
            FindingDisposition findingDisposition;
            RulingDisposition rulingDisposition;
            if (score >= context.Policy.InitialRecognitionThreshold)
            {
                findingDisposition = FindingDisposition.Established;
                rulingDisposition = RulingDisposition.Recognised;
            }
            else if (context.Policy.PermitProvisionalRecognition &&
                     score >= context.Policy.ProvisionalRecognitionThreshold)
            {
                findingDisposition = FindingDisposition.ProvisionallyEstablished;
                rulingDisposition = RulingDisposition.ProvisionallyRecognised;
            }
            else
            {
                findingDisposition = FindingDisposition.NotEstablished;
                rulingDisposition = RulingDisposition.Denied;
            }

            OfficialFinding finding = CreateFinding(
                PrimaryCaseId, 5, "initial", findingDisposition, score,
                context.Policy.InitialRecognitionThreshold, evidence);
            context.Report.OfficialFindings.Add(finding);
            var ruling = new Ruling
            {
                RulingId = "ruling:primary:initial:5",
                CaseId = PrimaryCaseId,
                Cycle = 5,
                PolicyConfigurationId = context.Policy.PolicyConfigurationId,
                PolicyVersion = context.Policy.PolicyVersion,
                Disposition = rulingDisposition,
                FindingId = finding.FindingId,
                ConfidenceMinimum = confidenceMinimum,
                ConfidenceMaximum = confidenceMaximum,
                EvidenceArtifactIds = CopyEvidenceIds(evidence),
                AppliedPolicyIds = new List<string>
                {
                    context.Policy.PolicyVersion,
                    "rule.initial-burden",
                    "rule.identity-continuity",
                },
            };
            if (!ContainsEvidenceKind(evidence, EvidenceArtifactKind.WitnessRecord))
                ruling.SkippedProcedureIds.Add("procedure.authenticated-roster");
            if (rulingDisposition == RulingDisposition.ProvisionallyRecognised)
                ruling.SkippedProcedureIds.Add("procedure.forensic-payroll-verification");
            context.Report.Rulings.Add(ruling);
            context.InitialRuling = ruling;
            AddTimeline(context.Report, 5, InstitutionalTimelineKind.RulingIssued,
                ruling.RulingId, PrimaryCaseId, ruling.Disposition.ToString());

            bool recognised = rulingDisposition == RulingDisposition.Recognised ||
                              rulingDisposition == RulingDisposition.ProvisionallyRecognised;
            if (recognised)
            {
                ApplyStatusMutation(context, ruling, context.Roles.PrimaryClaimantId,
                    "identity-continuity", true, 0);
                OfficialStatusMutation entitlement = ApplyStatusMutation(
                    context, ruling, context.Roles.PrimaryClaimantId,
                    TreatmentEntitlementStatusId, true, 0);
                ApplyStatusMutation(context, ruling, context.Roles.PrimaryClaimantId,
                    "adverse-decision", false, 0);
                if (rulingDisposition == RulingDisposition.ProvisionallyRecognised)
                {
                    ApplyStatusMutation(context, ruling, context.Roles.PrimaryClaimantId,
                        "provisional-relief-paid", true, context.Policy.ProvisionalReliefAmount);
                    AddMaterial(context, 5, ruling.RulingId, context.Roles.PrimaryClaimantId,
                        MaterialConsequenceKind.ReliefPaid, context.Policy.ProvisionalReliefAmount);
                    ApplyStatusMutation(context, ruling, context.Roles.EmployerRepresentativeId,
                        "adverse-decision", true, 0);
                    context.PendingAid.Add(new AidOpportunity
                    {
                        OpportunityId = "aid-opportunity:continuity-treatment",
                        PurposeId = TreatmentAidPurposeId,
                        SourceCauseId = entitlement.MutationId,
                        RequiredOfficialStatusId = TreatmentEntitlementStatusId,
                        UtilityBonus = 40,
                        EligibleAgentIds = new List<string> { context.Roles.PrimaryClaimantId },
                    });
                }
            }
            else
            {
                ApplyStatusMutation(context, ruling, context.Roles.PrimaryClaimantId,
                    "adverse-decision", true, 0);
            }

            string adverselyAffected = recognised
                ? context.Roles.EmployerRepresentativeId
                : context.Roles.PrimaryClaimantId;
            context.PendingCases.Add(new CaseOpportunityState
            {
                Opportunity = new AppealOpportunity
                {
                    OpportunityId = "appeal-opportunity:primary",
                    DocketId = PrimaryAppealDocketId,
                    CaseId = PrimaryCaseId,
                    ChallengedRulingId = ruling.RulingId,
                    SourceCauseId = ruling.RulingId,
                    HearingCycle = 11,
                    UtilityBonus = 25,
                    PartyAgentIds = new List<string> { adverselyAffected },
                },
                DescendantKind = DescendantCaseKind.Appeal,
                ParentCaseId = PrimaryCaseId,
                IssueId = ContinuityIssueId,
                EmployerId = context.Roles.EmployerId,
                IdentityConditionId = IdentityConditionId,
                OriginatingActionEventId = null,
                EarliestFilingCycle = 10,
            });
            context.PendingWork.Add(new WorkOpportunity
            {
                OpportunityId = "work-opportunity:employer-response",
                PurposeId = EmployerResponsePurposeId,
                SourceCauseId = ruling.RulingId,
                RequiredEmployerId = context.Roles.EmployerId,
                RequiredOfficialStatusId = "management-authority",
                EarliestCycle = 7,
                UtilityBonus = 40,
            });
        }

        private static void HearPrimaryAppeal(LoopContext context)
        {
            Appeal appeal = FindPendingAppeal(context.Report, PrimaryCaseId, 11);
            if (appeal == null) return;
            AddTimeline(context.Report, 11, InstitutionalTimelineKind.AppealHeard,
                appeal.AppealId, PrimaryCaseId, "hearing.primary");

            List<EvidenceArtifact> evidence = EvidenceForCase(context.Report, PrimaryCaseId, 11);
            int score = ScoreEvidence(evidence, context.Policy);
            ScoreEvidenceBounds(evidence, context.Policy,
                out int confidenceMinimum, out int confidenceMaximum);
            bool finalRecognised = score >= context.Policy.AppealRecognitionThreshold;
            bool initialRecognised = context.InitialRuling.Disposition == RulingDisposition.Recognised ||
                                     context.InitialRuling.Disposition == RulingDisposition.ProvisionallyRecognised;
            RulingDisposition disposition = finalRecognised
                ? (initialRecognised ? RulingDisposition.Affirmed : RulingDisposition.ReversedAndRecognised)
                : (initialRecognised ? RulingDisposition.ReversedAndDenied : RulingDisposition.Affirmed);
            OfficialFinding finding = CreateFinding(
                PrimaryCaseId, 11, "appeal",
                finalRecognised ? FindingDisposition.Established : FindingDisposition.NotEstablished,
                score, context.Policy.AppealRecognitionThreshold, evidence);
            context.Report.OfficialFindings.Add(finding);
            var ruling = new Ruling
            {
                RulingId = "ruling:primary:appeal:11",
                CaseId = PrimaryCaseId,
                Cycle = 11,
                PolicyConfigurationId = context.Policy.PolicyConfigurationId,
                PolicyVersion = context.Policy.PolicyVersion,
                Disposition = disposition,
                FindingId = finding.FindingId,
                ConfidenceMinimum = confidenceMinimum,
                ConfidenceMaximum = confidenceMaximum,
                EvidenceArtifactIds = CopyEvidenceIds(evidence),
                AppliedPolicyIds = new List<string>
                {
                    context.Policy.PolicyVersion,
                    "rule.appellate-review",
                    "rule.identity-continuity",
                },
            };
            context.Report.Rulings.Add(ruling);
            context.PrimaryAppealRuling = ruling;
            AddTimeline(context.Report, 11, InstitutionalTimelineKind.RulingIssued,
                ruling.RulingId, PrimaryCaseId, ruling.Disposition.ToString());

            ApplyStatusMutation(context, ruling, context.Roles.PrimaryClaimantId,
                "identity-continuity", finalRecognised, 0);
            ApplyStatusMutation(context, ruling, context.Roles.PrimaryClaimantId,
                TreatmentEntitlementStatusId, finalRecognised, 0);
            ApplyStatusMutation(context, ruling, context.Roles.PrimaryClaimantId,
                "adverse-decision", !finalRecognised, 0);
            ApplyStatusMutation(context, ruling, appeal.AppellantAgentId,
                "appeal-pending", false, 0);
            if (string.Equals(appeal.AppellantAgentId,
                context.Roles.EmployerRepresentativeId, StringComparison.Ordinal))
            {
                ApplyStatusMutation(context, ruling, context.Roles.EmployerRepresentativeId,
                    "adverse-decision", false, 0);
            }

            appeal.Disposition = disposition == RulingDisposition.Affirmed
                ? AppealDisposition.Affirmed
                : AppealDisposition.Reversed;
            appeal.ResultingRulingId = ruling.RulingId;
            DescendantCase primaryAppealCase = FindDescendantCase(context.Report, "case.primary-appeal");
            if (primaryAppealCase != null)
                primaryAppealCase.Status = appeal.Disposition == AppealDisposition.Reversed
                    ? DescendantCaseStatus.Recognised
                    : DescendantCaseStatus.Denied;

            if (!finalRecognised && initialRecognised && context.Run.RelianceLedger.Count > 0)
                CreateRecoveryCaseFromSurvivingReliance(context, ruling);
            if (finalRecognised && context.Policy.EstablishAppellateHolding)
                EstablishHolding(context, appeal, ruling);
        }

        private static void CreateRecoveryCaseFromSurvivingReliance(
            LoopContext context,
            Ruling reversal)
        {
            for (int i = 0; i < context.Run.RelianceLedger.Count; i++)
            {
                RelianceEvent reliance = context.Run.RelianceLedger[i];
                reliance.SurvivedReversal = true;
                var recovery = new DescendantCase
                {
                    CaseId = "case.recovery-after-reversal",
                    ParentCaseId = PrimaryCaseId,
                    OpenedCycle = reversal.Cycle,
                    Kind = DescendantCaseKind.Reliance,
                    Status = DescendantCaseStatus.Open,
                    ParentCauseId = reversal.RulingId,
                    OriginatingEventId = reliance.SourceActionEventId,
                    OriginatingRulingId = reversal.RulingId,
                    CausalAgentActionId = reliance.SourceActionEventId,
                    ClaimantAgentId = reliance.AgentId,
                    RespondentId = "branch-42",
                    ConnectedAgentIds = new List<string> { reliance.AgentId },
                    SourceActionEventIds = new List<string> { reliance.SourceActionEventId },
                };
                if (!recovery.ConnectedAgentIds.Contains(reliance.HouseholdAgentId))
                    recovery.ConnectedAgentIds.Add(reliance.HouseholdAgentId);
                context.Report.DescendantCases.Add(recovery);
                FindObservedAction(context.Report, reliance.SourceActionEventId)?.ResultDescendantCaseIds
                    .Add(recovery.CaseId);
                AddTimeline(context.Report, reversal.Cycle,
                    InstitutionalTimelineKind.DescendantCaseOpened,
                    reversal.RulingId, recovery.CaseId, recovery.Kind.ToString());
            }
        }

        private static void EstablishHolding(LoopContext context, Appeal appeal, Ruling ruling)
        {
            var holding = new Holding
            {
                HoldingId = "holding:continuity-after-supersession",
                EstablishedCycle = ruling.Cycle,
                SourceAppealId = appeal.AppealId,
                SourceRulingId = ruling.RulingId,
                RuleId = "rule.superseded-identity-preserves-employment-continuity",
                IssueId = ContinuityIssueId,
                SupportingEvidenceArtifactIds = new List<string>(ruling.EvidenceArtifactIds),
                Scope = new PrecedentScope
                {
                    ScopeId = "scope:workplace-identity-continuity",
                    Reach = context.Policy.HoldingReach,
                    BoundAgentId = context.Roles.PrimaryClaimantId,
                    BoundEmployerId = context.Roles.EmployerId,
                    IdentityConditionId = IdentityConditionId,
                    Retrospective = context.Policy.HoldingIsRetrospective,
                },
            };
            context.Report.Holdings.Add(holding);
            AddTimeline(context.Report, ruling.Cycle, InstitutionalTimelineKind.HoldingEstablished,
                ruling.RulingId, holding.HoldingId, holding.RuleId);
        }

        private static void HearLaterAppeal(LoopContext context)
        {
            Appeal appeal = FindPendingAppeal(context.Report, LaterCaseId, 14);
            if (appeal == null) return;
            CaseOpportunityState pending = FindCaseOpportunityByAppeal(context, appeal.AppealId);
            if (pending == null) return;
            AddTimeline(context.Report, 14, InstitutionalTimelineKind.AppealHeard,
                appeal.AppealId, LaterCaseId, "hearing.related-claim");

            List<EvidenceArtifact> evidence = EvidenceForCase(context.Report, LaterCaseId, 14);
            int score = ScoreEvidence(evidence, context.Policy);
            ScoreEvidenceBounds(evidence, context.Policy,
                out int confidenceMinimum, out int confidenceMaximum);
            Holding cited = FindMatchingHolding(context, pending);
            if (cited != null)
            {
                score += context.Policy.CitedHoldingWeight;
                confidenceMinimum += context.Policy.CitedHoldingWeight;
                confidenceMaximum += context.Policy.CitedHoldingWeight;
            }
            bool recognised = score >= context.Policy.LaterRecognitionThreshold;
            OfficialFinding finding = CreateFinding(
                LaterCaseId, 14, "appeal",
                recognised ? FindingDisposition.Established : FindingDisposition.NotEstablished,
                score, context.Policy.LaterRecognitionThreshold, evidence);
            finding.PrecedentWeightApplied = cited == null
                ? 0
                : context.Policy.CitedHoldingWeight;
            context.Report.OfficialFindings.Add(finding);
            var ruling = new Ruling
            {
                RulingId = "ruling:later:appeal:14",
                CaseId = LaterCaseId,
                Cycle = 14,
                PolicyConfigurationId = context.Policy.PolicyConfigurationId,
                PolicyVersion = context.Policy.PolicyVersion,
                Disposition = recognised
                    ? RulingDisposition.ReversedAndRecognised
                    : RulingDisposition.Affirmed,
                FindingId = finding.FindingId,
                ConfidenceMinimum = confidenceMinimum,
                ConfidenceMaximum = confidenceMaximum,
                EvidenceArtifactIds = CopyEvidenceIds(evidence),
                AppliedPolicyIds = new List<string>
                {
                    context.Policy.PolicyVersion,
                    "rule.later-appellate-review",
                },
            };
            if (cited != null)
            {
                ruling.CitedHoldingIds.Add(cited.HoldingId);
                ruling.CitedScopeIds.Add(cited.Scope.ScopeId);
                ruling.AppliedPolicyIds.Add(cited.RuleId);
            }
            context.Report.Rulings.Add(ruling);
            AddTimeline(context.Report, 14, InstitutionalTimelineKind.RulingIssued,
                ruling.RulingId, LaterCaseId, ruling.Disposition.ToString());

            ApplyStatusMutation(context, ruling, appeal.AppellantAgentId,
                "appeal-pending", false, 0);
            DescendantCase laterCase = FindDescendantCase(context.Report, LaterCaseId);
            if (recognised)
            {
                ApplyStatusMutation(context, ruling, appeal.AppellantAgentId,
                    "employment-continuity", true, 0);
                ApplyStatusMutation(context, ruling, appeal.AppellantAgentId,
                    "adverse-decision", false, 0);
                if (cited != null && cited.Scope.Retrospective)
                    TransferWorkAllocation(context, ruling, cited, pending, appeal.AppellantAgentId);
                if (cited != null)
                {
                    cited.AppliedCaseIds.Add(LaterCaseId);
                    laterCase.CitedHoldingIds.Add(cited.HoldingId);
                }
                laterCase.Status = DescendantCaseStatus.Recognised;
                appeal.Disposition = AppealDisposition.Reversed;
            }
            else
            {
                laterCase.Status = DescendantCaseStatus.Denied;
                appeal.Disposition = AppealDisposition.Affirmed;
            }
            appeal.ResultingRulingId = ruling.RulingId;
        }

        private static Holding FindMatchingHolding(
            LoopContext context,
            CaseOpportunityState pending)
        {
            if (!context.Policy.AutoCiteMatchingHoldings) return null;
            for (int i = 0; i < context.Report.Holdings.Count; i++)
            {
                Holding holding = context.Report.Holdings[i];
                if (string.Equals(holding.IssueId, pending.IssueId, StringComparison.Ordinal) &&
                    holding.Scope.AppliesTo(
                    pending.Opportunity.PartyAgentIds[0],
                    pending.EmployerId,
                    pending.IdentityConditionId)) return holding;
            }
            return null;
        }

        private static void TransferWorkAllocation(
            LoopContext context,
            Ruling ruling,
            Holding holding,
            CaseOpportunityState pending,
            string newHolderId)
        {
            WorkAllocationState allocation = FindAllocation(context, pending.ResourceId);
            if (allocation == null || string.Equals(
                allocation.PaidHolderAgentId, newHolderId, StringComparison.Ordinal)) return;
            string previousHolderId = allocation.PaidHolderAgentId;
            int wage = allocation.CommittedWage;

            EconomicAccountState previous = FindAccount(context, previousHolderId);
            EconomicAccountState next = FindAccount(context, newHolderId);
            previous.CommittedIncome -= wage;
            next.CommittedIncome += wage;
            ChangeNeedPressure(context.Society.GetAgent(previousHolderId), NeedKind.Subsistence, 10);
            ChangeNeedPressure(context.Society.GetAgent(newHolderId), NeedKind.Subsistence, -10);
            allocation.PaidHolderAgentId = newHolderId;
            allocation.LastMutationCauseId = ruling.RulingId;

            ApplyStatusMutation(context, ruling, previousHolderId,
                "paid-shift-allocation", false, 0);
            ApplyStatusMutation(context, ruling, newHolderId,
                "paid-shift-allocation", true, 0);
            AddMaterial(context, ruling.Cycle, ruling.RulingId, newHolderId,
                MaterialConsequenceKind.BackpayAwarded, wage);
            AddMaterial(context, ruling.Cycle, ruling.RulingId, previousHolderId,
                MaterialConsequenceKind.WagesLost, -wage);

            AgentState winner = context.Society.GetAgent(newHolderId);
            AgentState loser = context.Society.GetAgent(previousHolderId);
            context.Report.ConnectedOutcomes.Add(new ConnectedOutcomePair
            {
                PairId = $"outcome:{allocation.AllocationId}",
                CauseRuleId = holding.RuleId,
                ConnectionId = allocation.AllocationId,
                WinnerAgentId = winner.StableId,
                WinnerDisplayName = winner.DisplayName,
                WinnerResourceDelta = wage,
                LoserAgentId = loser.StableId,
                LoserDisplayName = loser.DisplayName,
                LoserResourceDelta = -wage,
            });
            AddTimeline(context.Report, ruling.Cycle, InstitutionalTimelineKind.PrecedentApplied,
                holding.HoldingId, LaterCaseId, allocation.AllocationId);
        }

        private static EvidenceArtifact EvidenceFromDisclosure(SocietyEvent societyEvent)
        {
            EvidenceArtifactKind kind = EvidenceArtifactKind.ActionRecord;
            EvidenceEffect effect = EvidenceEffect.Neutral;
            int weight = 0;
            switch (societyEvent.EvidencePropositionId)
            {
                case "identity.badge-was-replaced":
                    kind = EvidenceArtifactKind.ClaimantStatement;
                    effect = EvidenceEffect.SupportsFinding;
                    weight = 20;
                    break;
                case "identity-discontinuity-correlates-with-physical-harm":
                    kind = EvidenceArtifactKind.ClinicalRecord;
                    effect = EvidenceEffect.SupportsFinding;
                    weight = 25;
                    break;
                case "employer.uses-identity-replacement-to-avoid-obligations":
                    kind = EvidenceArtifactKind.PatternTestimony;
                    effect = EvidenceEffect.SupportsFinding;
                    weight = 15;
                    break;
                case "records.two-active-rosters":
                    kind = EvidenceArtifactKind.WitnessRecord;
                    effect = EvidenceEffect.SupportsFinding;
                    weight = 40;
                    break;
                case "records.replacement-was-authorised":
                    kind = EvidenceArtifactKind.ManagementRecord;
                    effect = EvidenceEffect.OpposesFinding;
                    weight = 60;
                    break;
                case "injury.press-caused-harm":
                    kind = EvidenceArtifactKind.ClaimantStatement;
                    effect = EvidenceEffect.SupportsFinding;
                    weight = 10;
                    break;
            }
            return EvidenceFromAction(
                societyEvent,
                PrimaryCaseId,
                kind,
                societyEvent.EvidencePropositionId,
                effect,
                weight);
        }

        private static EvidenceArtifact EvidenceFromAction(
            SocietyEvent societyEvent,
            string caseId,
            EvidenceArtifactKind kind,
            string propositionId,
            EvidenceEffect effect,
            int weight)
        {
            return new EvidenceArtifact
            {
                ArtifactId = $"artifact:{societyEvent.EventId}",
                CaseId = caseId,
                EnteredCycle = societyEvent.Tick,
                Kind = kind,
                IssueId = ContinuityIssueId,
                PropositionId = propositionId,
                Effect = effect,
                BaseWeight = weight,
                Reliability = societyEvent.EvidenceReliability > 0
                    ? societyEvent.EvidenceReliability
                    : 100,
                OfficiallySubmitted = true,
                SuppressedByAgentId = societyEvent.EvidenceSuppressedByAgentId,
                KnownByAgentIds = new List<string> { societyEvent.ActorId },
                EnteredAfterInitialRuling = societyEvent.Tick > 5,
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

        private static void AddEvidence(LoopContext context, EvidenceArtifact artifact)
        {
            if (artifact == null) return;
            for (int i = 0; i < context.Report.EvidenceArtifacts.Count; i++)
                if (string.Equals(context.Report.EvidenceArtifacts[i].ArtifactId,
                    artifact.ArtifactId, StringComparison.Ordinal)) return;
            context.Report.EvidenceArtifacts.Add(artifact);
            FindObservedAction(context.Report, artifact.Provenance.SourceSocietyEventId)?.ResultEvidenceArtifactIds
                .Add(artifact.ArtifactId);
            AddTimeline(context.Report, artifact.EnteredCycle,
                InstitutionalTimelineKind.EvidenceEntered,
                artifact.Provenance.SourceSocietyEventId,
                artifact.Provenance.SourceAgentId,
                artifact.ArtifactId);
        }

        private static void LinkEvidenceToAuthoritativeBelief(
            LoopContext context,
            SocietyEvent societyEvent,
            EvidenceArtifact artifact)
        {
            for (int i = 0; i < context.Run.AuthoritativeBeliefLinks.Count; i++)
            {
                AuthoritativeBeliefLink link = context.Run.AuthoritativeBeliefLinks[i];
                if (!string.Equals(link.AgentId, societyEvent.ActorId, StringComparison.Ordinal) ||
                    !string.Equals(link.BeliefId, societyEvent.EvidenceBeliefId,
                        StringComparison.Ordinal)) continue;
                context.Run.AuthoritativeEvidenceLinks.Add(new AuthoritativeEvidenceLink
                {
                    LivedEventId = link.LivedEventId,
                    EvidenceArtifactId = artifact.ArtifactId,
                    ObservationKindId = "observation.agent-representation",
                });
                return;
            }
        }

        private static int ScoreEvidence(
            List<EvidenceArtifact> evidence,
            InstitutionalPolicyConfiguration policy)
        {
            int total = 0;
            for (int i = 0; i < evidence.Count; i++)
            {
                EvidenceArtifact artifact = evidence[i];
                int weighted = artifact.BaseWeight * policy.WeightPercent(artifact.Kind) / 100;
                weighted = weighted * artifact.Reliability / 100;
                if (artifact.Effect == EvidenceEffect.SupportsFinding) total += weighted;
                if (artifact.Effect == EvidenceEffect.OpposesFinding) total -= weighted;
            }
            return total;
        }

        private static void ScoreEvidenceBounds(
            List<EvidenceArtifact> evidence,
            InstitutionalPolicyConfiguration policy,
            out int minimum,
            out int maximum)
        {
            minimum = 0;
            maximum = 0;
            for (int i = 0; i < evidence.Count; i++)
            {
                EvidenceArtifact artifact = evidence[i];
                int fullWeight = artifact.BaseWeight * policy.WeightPercent(artifact.Kind) / 100;
                int reliableWeight = fullWeight * artifact.Reliability / 100;
                if (artifact.Effect == EvidenceEffect.SupportsFinding)
                {
                    minimum += reliableWeight;
                    maximum += fullWeight;
                }
                else if (artifact.Effect == EvidenceEffect.OpposesFinding)
                {
                    minimum -= fullWeight;
                    maximum -= reliableWeight;
                }
            }
        }

        private static OfficialFinding CreateFinding(
            string caseId,
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
                IssueId = ContinuityIssueId,
                Disposition = disposition,
                WeightedEvidenceScore = score,
                RequiredScore = threshold,
                EvidenceArtifactIds = CopyEvidenceIds(evidence),
            };
        }

        private static OfficialStatusMutation ApplyStatusMutation(
            LoopContext context,
            Ruling ruling,
            string agentId,
            string statusId,
            bool recognised,
            int resourceDelta)
        {
            AgentState agent = context.Society.GetAgent(agentId);
            if (agent == null) throw new InvalidOperationException($"Unknown mutation target {agentId}.");
            bool before = agent.Standing.IsRecognised(statusId);
            if (before == recognised && resourceDelta == 0)
                return FindLatestMutation(context.Report, agentId, statusId, recognised);

            var mutation = new OfficialStatusMutation
            {
                MutationId = $"mutation:{ruling.Cycle}:{context.Report.OfficialStatusMutations.Count}:{agentId}:{statusId}",
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
                FindAccount(context, agentId).AvailableCredits += resourceDelta;
            context.Report.OfficialStatusMutations.Add(mutation);
            ruling.OfficialStatusMutationIds.Add(mutation.MutationId);
            AddTimeline(context.Report, ruling.Cycle, InstitutionalTimelineKind.StatusMutated,
                ruling.RulingId, agentId, statusId);
            return mutation;
        }

        private static void AddMaterial(
            LoopContext context,
            long cycle,
            string causeId,
            string agentId,
            MaterialConsequenceKind kind,
            int delta)
        {
            context.Report.MaterialConsequences.Add(new MaterialConsequence
            {
                ConsequenceId = $"material:{cycle}:{context.Report.MaterialConsequences.Count}:{agentId}:{kind}",
                Cycle = cycle,
                CauseId = causeId,
                AgentId = agentId,
                Kind = kind,
                ResourceDelta = delta,
            });
        }

        private static void SnapshotState(LoopContext context)
        {
            context.Report.WorkAllocations.Clear();
            for (int i = 0; i < context.Run.WorkAllocations.Count; i++)
            {
                WorkAllocationState allocation = context.Run.WorkAllocations[i];
                context.Report.WorkAllocations.Add(new WorkAllocationObservation
                {
                    AllocationId = allocation.AllocationId,
                    EmployerId = allocation.EmployerId,
                    OriginalWorkerId = allocation.OriginalWorkerId,
                    PaidHolderAgentId = allocation.PaidHolderAgentId,
                    IdentityConditionId = allocation.IdentityConditionId,
                    CommittedWage = allocation.CommittedWage,
                    LastMutationCauseId = allocation.LastMutationCauseId,
                });
            }
        }

        private static List<EvidenceArtifact> EvidenceForCase(
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
                    result.Add(artifact);
            }
            result.Sort((left, right) => string.CompareOrdinal(left.ArtifactId, right.ArtifactId));
            return result;
        }

        private static List<string> CopyEvidenceIds(List<EvidenceArtifact> evidence)
        {
            var ids = new List<string>(evidence.Count);
            for (int i = 0; i < evidence.Count; i++) ids.Add(evidence[i].ArtifactId);
            return ids;
        }

        private static bool ContainsEvidenceKind(List<EvidenceArtifact> evidence, EvidenceArtifactKind kind)
        {
            for (int i = 0; i < evidence.Count; i++)
                if (evidence[i].Kind == kind) return true;
            return false;
        }

        private static WorkOpportunity FindWorkOpportunity(LoopContext context, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < context.PendingWork.Count; i++)
                if (string.Equals(context.PendingWork[i].OpportunityId, id, StringComparison.Ordinal))
                    return context.PendingWork[i];
            return null;
        }

        private static AidOpportunity FindAidOpportunity(LoopContext context, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < context.PendingAid.Count; i++)
                if (string.Equals(context.PendingAid[i].OpportunityId, id, StringComparison.Ordinal))
                    return context.PendingAid[i];
            return null;
        }

        private static CaseOpportunityState FindCaseOpportunity(LoopContext context, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < context.PendingCases.Count; i++)
                if (string.Equals(context.PendingCases[i].Opportunity.OpportunityId, id,
                    StringComparison.Ordinal)) return context.PendingCases[i];
            return null;
        }

        private static CaseOpportunityState FindCaseOpportunityByAppeal(
            LoopContext context,
            string appealId)
        {
            for (int i = 0; i < context.PendingCases.Count; i++)
                if (string.Equals(context.PendingCases[i].FiledAppealId, appealId,
                    StringComparison.Ordinal)) return context.PendingCases[i];
            return null;
        }

        private static Appeal FindPendingAppeal(
            InstitutionalConsequenceReport report,
            string caseId,
            long hearingCycle)
        {
            for (int i = 0; i < report.Appeals.Count; i++)
            {
                Appeal appeal = report.Appeals[i];
                if (appeal.Disposition == AppealDisposition.Pending &&
                    appeal.HearingCycle == hearingCycle &&
                    string.Equals(appeal.CaseId, caseId, StringComparison.Ordinal)) return appeal;
            }
            return null;
        }

        private static EvidenceArtifact FindEvidenceByResource(
            InstitutionalConsequenceReport report,
            string resourceId)
        {
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
                if (string.Equals(report.EvidenceArtifacts[i].OfficialResourceId, resourceId,
                    StringComparison.Ordinal)) return report.EvidenceArtifacts[i];
            return null;
        }

        private static WorkAllocationState FindAllocation(LoopContext context, string id)
        {
            for (int i = 0; i < context.Run.WorkAllocations.Count; i++)
                if (string.Equals(context.Run.WorkAllocations[i].AllocationId, id,
                    StringComparison.Ordinal)) return context.Run.WorkAllocations[i];
            return null;
        }

        private static EconomicAccountState FindAccount(LoopContext context, string agentId)
        {
            for (int i = 0; i < context.Run.EconomicAccounts.Count; i++)
                if (string.Equals(context.Run.EconomicAccounts[i].AgentId, agentId,
                    StringComparison.Ordinal)) return context.Run.EconomicAccounts[i];
            throw new InvalidOperationException($"Missing economic account for {agentId}.");
        }

        private static AgentActionTrace FindAssessorTrace(
            InstitutionalConsequenceRun run,
            string decisionId)
        {
            for (int i = 0; i < run.AssessorActionTraces.Count; i++)
                if (string.Equals(run.AssessorActionTraces[i].DecisionId, decisionId,
                    StringComparison.Ordinal)) return run.AssessorActionTraces[i];
            return null;
        }

        private static bool TraceReadsStatus(AgentActionTrace trace, string statusId)
        {
            for (int i = 0; i < trace.Reasons.Count; i++)
                if (string.Equals(trace.Reasons[i].ReasonId, "standing.required-status",
                        StringComparison.Ordinal) &&
                    string.Equals(trace.Reasons[i].SourceId, statusId,
                        StringComparison.Ordinal)) return true;
            return false;
        }

        private static OfficialStatusMutation FindLatestMutation(
            InstitutionalConsequenceReport report,
            string agentId,
            string statusId,
            bool recognised)
        {
            for (int i = report.OfficialStatusMutations.Count - 1; i >= 0; i--)
            {
                OfficialStatusMutation mutation = report.OfficialStatusMutations[i];
                if (string.Equals(mutation.AffectedAgentId, agentId, StringComparison.Ordinal) &&
                    string.Equals(mutation.StatusId, statusId, StringComparison.Ordinal) &&
                    mutation.AfterRecognised == recognised) return mutation;
            }
            return null;
        }

        private static ObservedAgentAction FindObservedAction(
            InstitutionalConsequenceReport report,
            string actionEventId)
        {
            for (int i = 0; i < report.ObservedAgentActions.Count; i++)
                if (string.Equals(report.ObservedAgentActions[i].ActionEventId, actionEventId,
                    StringComparison.Ordinal)) return report.ObservedAgentActions[i];
            return null;
        }

        private static DescendantCase FindDescendantCase(
            InstitutionalConsequenceReport report,
            string caseId)
        {
            for (int i = 0; i < report.DescendantCases.Count; i++)
                if (string.Equals(report.DescendantCases[i].CaseId, caseId,
                    StringComparison.Ordinal)) return report.DescendantCases[i];
            return null;
        }

        private static void ChangeNeedPressure(AgentState agent, NeedKind kind, int delta)
        {
            NeedState need = agent.GetNeed(kind);
            need.Pressure = Clamp(need.Pressure + delta, 0, 100);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }

        private static void AddTimeline(
            InstitutionalConsequenceReport report,
            long cycle,
            InstitutionalTimelineKind kind,
            string causeId,
            string subjectId,
            string detailId)
        {
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

        private sealed class LoopContext
        {
            internal readonly InstitutionalConsequenceRun Run;
            internal readonly InstitutionalPolicyConfiguration Policy;
            internal readonly InstitutionalIncidentRoles Roles;
            internal readonly List<WorkOpportunity> PendingWork = new();
            internal readonly List<AidOpportunity> PendingAid = new();
            internal readonly List<CaseOpportunityState> PendingCases = new();
            internal InstitutionalConsequenceReport Report => Run.Report;
            internal SocietyState Society => Run.FinalSocietyState;
            internal Ruling BaselineAllocationRuling;
            internal Ruling InitialRuling;
            internal Ruling PrimaryAppealRuling;

            internal LoopContext(
                InstitutionalConsequenceRun run,
                InstitutionalPolicyConfiguration policy,
                InstitutionalIncidentRoles roles)
            {
                Run = run;
                Policy = policy;
                Roles = roles;
            }
        }
    }
}
