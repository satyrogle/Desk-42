using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Desk42.Institutional;
using Desk42.Institutional.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalConsequenceLoopTests
    {
        private const int ProofSeed = 420042;

        [Test]
        public void Gate01_SameSeed_ABCProduceThreeMaterialHistories()
        {
            InstitutionalConsequenceRun records = RunAssessor(
                InstitutionalPolicyConfigurations.RecordsFirst());
            InstitutionalConsequenceRun provisional = RunAssessor(
                InstitutionalPolicyConfigurations.ProvisionalTrust());
            InstitutionalConsequenceRun precedent = RunAssessor(
                InstitutionalPolicyConfigurations.PrecedentMachine());

            Assert.AreEqual(RulingDisposition.Denied, InitialRuling(records.Report).Disposition);
            Assert.AreEqual(RulingDisposition.Affirmed, PrimaryAppealRuling(records.Report).Disposition);
            Assert.AreEqual(RulingDisposition.Affirmed, LaterRuling(records.Report).Disposition);
            Assert.IsEmpty(records.RelianceLedger);
            Assert.IsEmpty(records.Report.Holdings);

            Assert.AreEqual(RulingDisposition.ProvisionallyRecognised,
                InitialRuling(provisional.Report).Disposition);
            Assert.AreEqual(RulingDisposition.ReversedAndDenied,
                PrimaryAppealRuling(provisional.Report).Disposition);
            Assert.AreEqual(RulingDisposition.Affirmed, LaterRuling(provisional.Report).Disposition);
            Assert.AreEqual(1, provisional.RelianceLedger.Count);
            Assert.IsTrue(provisional.RelianceLedger.Single().SurvivedReversal);

            Assert.AreEqual(RulingDisposition.Denied, InitialRuling(precedent.Report).Disposition);
            Assert.AreEqual(RulingDisposition.ReversedAndRecognised,
                PrimaryAppealRuling(precedent.Report).Disposition);
            Assert.AreEqual(RulingDisposition.ReversedAndRecognised,
                LaterRuling(precedent.Report).Disposition);
            Assert.AreEqual(1, precedent.Report.Holdings.Count);
            Assert.AreEqual(1, precedent.Report.ConnectedOutcomes.Count);

            Assert.AreEqual(3, new[]
            {
                MaterialSignature(records),
                MaterialSignature(provisional),
                MaterialSignature(precedent),
            }.Distinct(StringComparer.Ordinal).Count());
        }

        [Test]
        public void Gate02_ConnectedWinnerAndLoserShareARealTransferredAllocation()
        {
            InstitutionalConsequenceRun run = RunAssessor(
                InstitutionalPolicyConfigurations.PrecedentMachine());
            InstitutionalConsequenceReport report = run.Report;
            ConnectedOutcomePair pair = report.ConnectedOutcomes.Single();
            WorkAllocationObservation allocation = report.WorkAllocations.Single(value =>
                value.AllocationId == pair.ConnectionId);

            Assert.AreEqual("Imri Pell", pair.WinnerDisplayName);
            Assert.AreEqual("Vey Ankar", pair.LoserDisplayName);
            Assert.AreEqual(pair.WinnerAgentId, allocation.OriginalWorkerId);
            Assert.AreEqual(pair.WinnerAgentId, allocation.PaidHolderAgentId);
            Assert.AreEqual(LaterRuling(report).RulingId, allocation.LastMutationCauseId);
            Assert.AreEqual(allocation.CommittedWage, pair.WinnerResourceDelta);
            Assert.AreEqual(-allocation.CommittedWage, pair.LoserResourceDelta);

            OfficialStatusMutation originalGrant = report.OfficialStatusMutations.Single(value =>
                value.CauseId == InstitutionalConsequenceLoop.BaselineAllocationRulingId &&
                value.StatusId == "paid-shift-allocation" && value.AfterRecognised);
            Assert.AreEqual(pair.LoserAgentId, originalGrant.AffectedAgentId);
            Assert.IsTrue(report.OfficialStatusMutations.Any(value =>
                value.CauseId == LaterRuling(report).RulingId &&
                value.AffectedAgentId == pair.WinnerAgentId &&
                value.StatusId == "paid-shift-allocation" && value.AfterRecognised));
            Assert.IsTrue(report.OfficialStatusMutations.Any(value =>
                value.CauseId == LaterRuling(report).RulingId &&
                value.AffectedAgentId == pair.LoserAgentId &&
                value.StatusId == "paid-shift-allocation" && !value.AfterRecognised));

            EconomicAccountState winner = run.EconomicAccounts.Single(value =>
                value.AgentId == pair.WinnerAgentId);
            EconomicAccountState loser = run.EconomicAccounts.Single(value =>
                value.AgentId == pair.LoserAgentId);
            Assert.AreEqual(allocation.CommittedWage, winner.CommittedIncome);
            Assert.AreEqual(0, loser.CommittedIncome);
            Assert.IsTrue(report.MaterialConsequences.Any(value =>
                value.AgentId == pair.WinnerAgentId &&
                value.Kind == MaterialConsequenceKind.BackpayAwarded &&
                value.ResourceDelta == allocation.CommittedWage));
            Assert.IsTrue(report.MaterialConsequences.Any(value =>
                value.AgentId == pair.LoserAgentId &&
                value.Kind == MaterialConsequenceKind.WagesLost &&
                value.ResourceDelta == -allocation.CommittedWage));
        }

        [Test]
        public void Gate03_GenericActionsHaveDifferentConsequentialPaths()
        {
            InstitutionalConsequenceRun run = RunAssessor(
                InstitutionalPolicyConfigurations.ProvisionalTrust());
            InstitutionalConsequenceReport report = run.Report;
            HashSet<SocietyActionKind> consequential = run.AssessorActionTraces
                .Where(trace => TraceHasInstitutionalEffect(run, trace))
                .Select(trace => trace.Action)
                .ToHashSet();

            Assert.Contains(SocietyActionKind.Disclose, consequential.ToList());
            Assert.Contains(SocietyActionKind.Work, consequential.ToList());
            Assert.Contains(SocietyActionKind.SeekAid, consequential.ToList());
            Assert.Contains(SocietyActionKind.Appeal, consequential.ToList());
            Assert.GreaterOrEqual(consequential.Count, 4);

            foreach (SocietyActionKind selected in new[]
                     {
                         SocietyActionKind.Disclose,
                         SocietyActionKind.SeekAid,
                         SocietyActionKind.Appeal,
                     })
            {
                Assert.IsTrue(run.AssessorActionTraces.Any(trace =>
                    trace.Action == selected &&
                    trace.CandidateEvaluations.Any(candidate =>
                        candidate.Action == SocietyActionKind.Work)),
                    $"{selected} must beat a live routine-work alternative.");
            }

            InstitutionalConsequenceRun records = RunAssessor(
                InstitutionalPolicyConfigurations.RecordsFirst());
            Assert.IsTrue(records.AssessorActionTraces.Any(trace =>
                trace.Action == SocietyActionKind.Work &&
                trace.CandidateEvaluations.Any(candidate =>
                    candidate.Action == SocietyActionKind.Disclose ||
                    candidate.Action == SocietyActionKind.Withhold)),
                "A policy-sensitive evidence opportunity must sometimes lose to work.");

            Assert.IsTrue(report.ObservedAgentActions.Any(value =>
                value.Activity == ObservedActivityKind.EvidenceSubmitted &&
                value.ResultEvidenceArtifactIds.Count > 0));
            Assert.IsTrue(report.ObservedAgentActions.Any(value =>
                value.Activity == ObservedActivityKind.AppealFiled &&
                value.ResultDescendantCaseIds.Count > 0));
        }

        [Test]
        public void Gate04_ActionsCreateEvidenceAndCasesWithChronologicalCausalPaths()
        {
            foreach (InstitutionalPolicyConfiguration policy in AllPolicies())
            {
                InstitutionalConsequenceReport report = Run(policy);
                Dictionary<string, long> actionCycles = report.ObservedAgentActions.ToDictionary(
                    value => value.ActionEventId,
                    value => value.Cycle,
                    StringComparer.Ordinal);

                List<EvidenceArtifact> actionEvidence = report.EvidenceArtifacts
                    .Where(value => value.Provenance.CreatedByAgentAction)
                    .ToList();
                Assert.GreaterOrEqual(actionEvidence.Count, 2, policy.PolicyConfigurationId);
                foreach (EvidenceArtifact evidence in actionEvidence)
                {
                    Assert.IsTrue(actionCycles.ContainsKey(evidence.Provenance.SourceSocietyEventId));
                    Assert.LessOrEqual(actionCycles[evidence.Provenance.SourceSocietyEventId],
                        evidence.EnteredCycle);
                }

                Assert.IsTrue(report.DescendantCases.Any(value =>
                    value.Kind == DescendantCaseKind.RelatedClaim));
                foreach (DescendantCase descendant in report.DescendantCases)
                {
                    Assert.IsNotEmpty(descendant.SourceActionEventIds, descendant.CaseId);
                    foreach (string source in descendant.SourceActionEventIds)
                    {
                        Assert.IsTrue(actionCycles.ContainsKey(source), descendant.CaseId);
                        Assert.LessOrEqual(actionCycles[source], descendant.OpenedCycle);
                    }
                }
            }
        }

        [Test]
        public void Gate05_ProvisionalRelianceMutatesStateAndSurvivesOnlyAfterReversal()
        {
            InstitutionalConsequenceRun run = RunAssessor(
                InstitutionalPolicyConfigurations.ProvisionalTrust());
            InstitutionalConsequenceReport report = run.Report;
            RelianceEvent reliance = run.RelianceLedger.Single();
            OfficialStatusMutation enabling = report.OfficialStatusMutations.Single(value =>
                value.MutationId == reliance.ReliedOnMutationId);
            AgentActionTrace action = run.AssessorActionTraces.Single(value =>
                value.ResultEventIds.Contains(reliance.SourceActionEventId));
            SocietyEvent actionEvent = run.FinalSocietyState.EventLedger.Single(value =>
                value.EventId == reliance.SourceActionEventId);

            Assert.AreEqual(InstitutionalConsequenceLoop.TreatmentEntitlementStatusId,
                enabling.StatusId);
            Assert.IsTrue(enabling.AfterRecognised);
            Assert.AreEqual(SocietyActionKind.SeekAid, action.Action);
            Assert.IsTrue(action.Reasons.Any(value =>
                value.ReasonId == "standing.required-status" &&
                value.SourceId == enabling.StatusId));
            Assert.IsTrue(actionEvent.Deltas.Any(value =>
                value.FieldId == "need:Health" && value.After < value.Before));

            EconomicAccountState account = run.EconomicAccounts.Single(value =>
                value.AgentId == reliance.AgentId);
            AlternativeOptionState alternative = run.AlternativeOptions.Single(value =>
                value.OptionId == reliance.AbandonedAlternativeId);
            Assert.AreEqual(reliance.CreditsBefore - reliance.ResourceSpent,
                reliance.CreditsAfter);
            Assert.AreEqual(reliance.CreditsAfter, account.AvailableCredits);
            Assert.Greater(reliance.AgentSubsistenceAfter,
                reliance.AgentSubsistenceBefore);
            AgentActionTrace nextCycle = run.AssessorActionTraces.Single(value =>
                value.Cycle == reliance.Cycle + 1 && value.ActorId == reliance.AgentId);
            Assert.AreEqual(reliance.AgentSubsistenceAfter,
                nextCycle.PerceptionSnapshot.GetNeed(NeedKind.Subsistence).Pressure,
                "The reliance cost must enter the next utility calculation.");
            Assert.IsFalse(alternative.Available);
            Assert.AreEqual(reliance.HouseholdAgentId, alternative.AgentId);
            Assert.AreNotEqual(reliance.AgentId, reliance.HouseholdAgentId);
            Assert.Greater(reliance.HouseholdSubsistenceAfter,
                reliance.HouseholdSubsistenceBefore);
            Assert.AreEqual(reliance.SourceActionEventId, alternative.ChangedByActionEventId);
            Assert.IsTrue(reliance.SurvivedReversal);
            RelianceObservation publicReliance = report.RelianceObservations.Single();
            Assert.AreEqual(reliance.ReliedOnRulingId, publicReliance.EnablingRulingId);
            Assert.AreEqual(reliance.ReliedOnMutationId, publicReliance.EnablingMutationId);
            Assert.AreEqual(reliance.SourceActionEventId, publicReliance.SourceActionEventId);
            Assert.AreEqual(-reliance.ResourceSpent, publicReliance.RecordedResourceDelta);
            Assert.IsTrue(report.OfficialStatusMutations.Any(value =>
                value.StatusId == enabling.StatusId && !value.AfterRecognised && value.Cycle == 11));

            DescendantCase recovery = report.DescendantCases.Single(value =>
                value.CaseId == "case.recovery-after-reversal");
            Assert.AreEqual(11, recovery.OpenedCycle);
            Assert.AreEqual(PrimaryAppealRuling(report).RulingId, recovery.ParentCauseId);
            Assert.Contains(reliance.SourceActionEventId, recovery.SourceActionEventIds);
            Assert.Contains(reliance.AgentId, recovery.ConnectedAgentIds);
            Assert.Contains(reliance.HouseholdAgentId, recovery.ConnectedAgentIds);
            Assert.IsFalse(report.DescendantCases.Any(value =>
                value.Kind == DescendantCaseKind.Reliance && value.OpenedCycle < 11));
        }

        [Test]
        public void Gate06_CitedScopedHoldingAddsInterpretiveWeightAndTransfersAllocation()
        {
            InstitutionalPolicyConfiguration binding =
                InstitutionalPolicyConfigurations.PrecedentMachine();
            InstitutionalConsequenceRun citedRun = RunAssessor(binding);
            InstitutionalConsequenceReport cited = citedRun.Report;

            InstitutionalPolicyConfiguration uncited = binding.CloneWithIdentity(
                "configuration.precedent-machine-uncited",
                "precedent-machine-uncited.v1");
            uncited.AutoCiteMatchingHoldings = false;
            InstitutionalConsequenceRun uncitedRun = RunAssessor(uncited);
            InstitutionalConsequenceReport notCited = uncitedRun.Report;

            Holding holding = cited.Holdings.Single();
            DescendantCase laterCase = LaterCase(cited);
            Ruling citedRuling = LaterRuling(cited);
            Ruling uncitedRuling = LaterRuling(notCited);
            OfficialFinding citedFinding = Finding(cited, citedRuling.FindingId);
            OfficialFinding uncitedFinding = Finding(notCited, uncitedRuling.FindingId);

            CollectionAssert.AreEqual(new[] { holding.HoldingId }, citedRuling.CitedHoldingIds);
            CollectionAssert.AreEqual(new[] { holding.Scope.ScopeId }, citedRuling.CitedScopeIds);
            Assert.Contains(holding.HoldingId, laterCase.CitedHoldingIds);
            Assert.Contains(laterCase.CaseId, holding.AppliedCaseIds);
            CollectionAssert.AreEquivalent(
                PrimaryAppealRuling(cited).EvidenceArtifactIds,
                holding.SupportingEvidenceArtifactIds);
            Assert.IsTrue(holding.Scope.AppliesTo(
                laterCase.ClaimantAgentId,
                laterCase.OfficialEmployerId,
                laterCase.OfficialIdentityConditionId));
            Assert.IsTrue(holding.Scope.Retrospective);

            Assert.AreEqual(binding.CitedHoldingWeight,
                citedFinding.WeightedEvidenceScore - uncitedFinding.WeightedEvidenceScore);
            Assert.AreEqual(binding.CitedHoldingWeight,
                citedFinding.PrecedentWeightApplied);
            Assert.AreEqual(0, uncitedFinding.PrecedentWeightApplied);
            Assert.AreEqual(RulingDisposition.ReversedAndRecognised, citedRuling.Disposition);
            Assert.AreEqual(RulingDisposition.Affirmed, uncitedRuling.Disposition);
            Assert.AreEqual(laterCase.ClaimantAgentId,
                cited.WorkAllocations.Single().PaidHolderAgentId);
            Assert.AreNotEqual(laterCase.ClaimantAgentId,
                notCited.WorkAllocations.Single().PaidHolderAgentId);
            Assert.IsEmpty(notCited.ConnectedOutcomes);

            AgentActionTrace loserAfterTransfer = citedRun.AssessorActionTraces.Single(value =>
                value.Cycle == 15 &&
                value.ActorId == cited.ConnectedOutcomes.Single().LoserAgentId);
            AgentActionTrace sameAgentWithoutTransfer =
                uncitedRun.AssessorActionTraces.Single(value =>
                    value.Cycle == 15 && value.ActorId == loserAfterTransfer.ActorId);
            int citedSubsistence = loserAfterTransfer.PerceptionSnapshot
                .GetNeed(NeedKind.Subsistence).Pressure;
            int uncitedSubsistence = sameAgentWithoutTransfer.PerceptionSnapshot
                .GetNeed(NeedKind.Subsistence).Pressure;
            Assert.Greater(citedSubsistence, uncitedSubsistence,
                "The wage loser must perceive the allocation loss on the next pulse.");
            int citedWorkScore = loserAfterTransfer.CandidateEvaluations.Single(value =>
                value.Action == SocietyActionKind.Work &&
                string.IsNullOrEmpty(value.OpportunityId)).Score;
            int uncitedWorkScore = sameAgentWithoutTransfer.CandidateEvaluations.Single(value =>
                value.Action == SocietyActionKind.Work &&
                string.IsNullOrEmpty(value.OpportunityId)).Score;
            Assert.Greater(citedWorkScore, uncitedWorkScore,
                "Economic transfer must alter a later candidate utility, not only the ledger.");
        }

        [Test]
        public void Gate07_EverySelectedActionHasAnAttributablePerceptionUtilityTrace()
        {
            foreach (InstitutionalPolicyConfiguration policy in AllPolicies())
            {
                InstitutionalConsequenceRun run = RunAssessor(policy);
                Assert.AreEqual(15 * PrototypePopulationFactory.PrototypePopulationSize,
                    run.AssessorActionTraces.Count);
                foreach (AgentActionTrace trace in run.AssessorActionTraces)
                {
                    Assert.NotNull(trace.PerceptionSnapshot, trace.DecisionId);
                    Assert.NotNull(trace.RegimeSnapshot, trace.DecisionId);
                    Assert.NotNull(trace.InputSnapshot, trace.DecisionId);
                    Assert.AreEqual(trace.ActorId, trace.PerceptionSnapshot.StableId);
                    Assert.AreEqual(trace.UtilityScore,
                        trace.Reasons.Sum(value => value.ScoreDelta),
                        trace.DecisionId);
                    Assert.IsNotEmpty(trace.CandidateEvaluations, trace.DecisionId);
                    foreach (CandidateEvaluation candidate in trace.CandidateEvaluations)
                    {
                        Assert.AreEqual(candidate.Score,
                            candidate.Reasons.Sum(value => value.ScoreDelta),
                            $"{trace.DecisionId}:{candidate.CandidateId}");
                        foreach (DecisionReason reason in candidate.Reasons)
                            AssertReasonSource(trace, reason);
                    }
                    CandidateEvaluation expectedWinner = trace.CandidateEvaluations
                        .OrderByDescending(value => value.Score)
                        .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
                        .First();
                    Assert.AreEqual(expectedWinner.CandidateId, trace.CandidateId,
                        trace.DecisionId);
                    Assert.AreEqual(expectedWinner.Score, trace.UtilityScore,
                        trace.DecisionId);
                    Assert.IsFalse(trace.Reasons.Any(value =>
                        (value.SourceId ?? string.Empty).Contains("lived:") ||
                        (value.ReasonId ?? string.Empty).Contains("authoritative")));
                    foreach (DecisionReason reason in trace.Reasons)
                        AssertReasonSource(trace, reason);

                    DecisionReason variation = trace.Reasons.FirstOrDefault(value =>
                        value.ReasonId == "variation.keyed");
                    if (variation != null)
                        Assert.AreEqual(0, variation.ScoreDelta);
                }
            }
        }

        [Test]
        public void Gate08_RulingsFreezeExactEvidencePolicyProcedureAndCitationEnvelopes()
        {
            foreach (InstitutionalPolicyConfiguration policy in AllPolicies())
            {
                InstitutionalConsequenceReport report = Run(policy);
                foreach (Ruling ruling in report.Rulings)
                {
                    List<string> expectedEvidence = report.EvidenceArtifacts
                        .Where(value => value.CaseId == ruling.CaseId &&
                                        value.EnteredCycle <= ruling.Cycle)
                        .Select(value => value.ArtifactId)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToList();
                    CollectionAssert.AreEqual(expectedEvidence,
                        ruling.EvidenceArtifactIds.OrderBy(value => value,
                            StringComparer.Ordinal).ToList(),
                        ruling.RulingId);
                    Assert.NotNull(ruling.AppliedPolicyIds);
                    Assert.NotNull(ruling.SkippedProcedureIds);
                    Assert.NotNull(ruling.CitedHoldingIds);
                    Assert.NotNull(ruling.CitedScopeIds);
                    Assert.AreEqual(ruling.CitedHoldingIds.Count, ruling.CitedScopeIds.Count);
                    if (ruling.RulingId != InstitutionalConsequenceLoop.BaselineAllocationRulingId)
                    {
                        Assert.AreEqual(policy.PolicyConfigurationId,
                            ruling.PolicyConfigurationId);
                        Assert.Contains(policy.PolicyVersion, ruling.AppliedPolicyIds);
                        Assert.AreEqual(policy.PolicyVersion, ruling.PolicyVersion);
                    }
                }
            }

            Assert.Contains("procedure.forensic-payroll-verification",
                InitialRuling(Run(InstitutionalPolicyConfigurations.ProvisionalTrust()))
                    .SkippedProcedureIds);
        }

        [Test]
        public void Gate09_AuthoritativeLivedStateIsSeparatedByAssemblyAndProjection()
        {
            InstitutionalConsequenceRun run = RunAssessor(
                InstitutionalPolicyConfigurations.RecordsFirst());
            Assembly domain = typeof(InstitutionalConsequenceReport).Assembly;
            Assembly authority = typeof(InstitutionalConsequenceLoop).Assembly;

            Assert.AreNotEqual(domain, authority);
            Assert.AreEqual("Desk42.Institutional.Authority", authority.GetName().Name);
            Assert.IsFalse(domain.GetReferencedAssemblies().Any(value =>
                value.Name == "Desk42.Institutional.Authority"));
            Assert.IsFalse(typeof(InstitutionalSocietyStore).Assembly
                .GetReferencedAssemblies().Any(value =>
                    value.Name == "Desk42.Institutional.Authority"));
            Assert.IsNull(domain.GetType("Desk42.Institutional.LivedEvent", false));
            Assert.AreEqual(authority, typeof(LivedEvent).Assembly);
            Assert.IsFalse(typeof(LivedEvent).IsPublic);
            Assert.IsFalse(typeof(AgentDecision).IsPublic);
            Assert.IsFalse(typeof(AgentPerception).IsPublic);
            Assert.IsFalse(typeof(DecisionReason).IsPublic);
            Assert.IsFalse(typeof(CandidateEvaluation).IsPublic);
            Assert.IsFalse(typeof(SimulationStepResult).GetField(
                "Decisions", BindingFlags.Public | BindingFlags.Instance) != null);
            Assert.IsFalse(PublicGraphContains(
                typeof(InstitutionalConsequenceReport),
                typeof(LivedEvent),
                new HashSet<Type>()));

            LivedEvent lived = run.AuthoritativeEvents.Single();
            AuthoritativeBeliefLink livedBelief = run.AuthoritativeBeliefLinks.Single();
            Assert.AreEqual(lived.LivedEventId, livedBelief.LivedEventId);
            Assert.AreEqual(lived.Cycle,
                run.FinalSocietyState.GetAgent(livedBelief.AgentId)
                    .GetBelief(livedBelief.BeliefId).AcquiredTick);
            Assert.IsTrue(run.AuthoritativeEvidenceLinks.Any(value =>
                value.LivedEventId == lived.LivedEventId));
            string playerJson = CanonicalJson(run.Report);
            StringAssert.DoesNotContain("lived:", playerJson);
            StringAssert.DoesNotContain("RelianceLedger", playerJson);
            StringAssert.DoesNotContain("SurvivedReversal", playerJson);
            StringAssert.DoesNotContain("AbandonedAlternative", playerJson);
            StringAssert.DoesNotContain("UtilityScore", playerJson);
            StringAssert.DoesNotContain("SubjectBeliefId", playerJson);

            MethodInfo publicRun = typeof(InstitutionalConsequenceLoop).GetMethod(
                "RunProof", BindingFlags.Public | BindingFlags.Static);
            Assert.AreEqual(typeof(InstitutionalConsequenceReport), publicRun.ReturnType);
            Assert.IsFalse(typeof(InstitutionalConsequenceLoop)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(method => method.ReturnType == typeof(InstitutionalConsequenceRun)));
        }

        [Test]
        public void Gate10_ReplayIsByteForByteDeterministic()
        {
            foreach (InstitutionalPolicyConfiguration policy in AllPolicies())
            {
                InstitutionalConsequenceRun first = RunAssessor(policy);
                InstitutionalConsequenceRun second = RunAssessor(policy);
                Assert.AreEqual(CanonicalJson(first.Report), CanonicalJson(second.Report),
                    policy.PolicyConfigurationId);
                Assert.AreEqual(AssessorReplaySignature(first),
                    AssessorReplaySignature(second),
                    policy.PolicyConfigurationId);
            }
        }

        [Test]
        public void Gate11_RemappedIdsNamesEmployersAndListOrderPreserveCausalPattern()
        {
            InstitutionalPolicyConfiguration policy =
                InstitutionalPolicyConfigurations.PrecedentMachine();
            InstitutionalConsequenceRun original = RunAssessor(policy);
            SocietyState transformed = PrototypePopulationFactory.Create(ProofSeed);
            RemapIdentityAndOrder(transformed);
            InstitutionalConsequenceRun replacement = InstitutionalConsequenceLoop.RunForAssessor(
                ProofSeed, policy, transformed);

            Assert.AreEqual(OrdinalBehaviourSignature(original),
                OrdinalBehaviourSignature(replacement));
            Assert.AreEqual(StructuralConsequenceSignature(original),
                StructuralConsequenceSignature(replacement));

            SocietyState permutedOrdinals = PrototypePopulationFactory.Create(ProofSeed);
            foreach (AgentState agent in permutedOrdinals.Agents)
                agent.SimulationOrdinal =
                    PrototypePopulationFactory.PrototypePopulationSize - 1 -
                    agent.SimulationOrdinal;
            InstitutionalConsequenceRun reorderedRoles =
                InstitutionalConsequenceLoop.RunForAssessor(
                    ProofSeed, policy, permutedOrdinals);
            Assert.AreEqual(MaterialSignature(original),
                MaterialSignature(reorderedRoles),
                "Scenario roles must come from state semantics, not ordinal slots.");
            Assert.AreEqual("Imri Pell",
                reorderedRoles.Report.ConnectedOutcomes.Single().WinnerDisplayName);
            Assert.AreEqual("Vey Ankar",
                reorderedRoles.Report.ConnectedOutcomes.Single().LoserDisplayName);
        }

        [Test]
        public void Gate12_NoDirector_CausalAblationsRemoveTheirOwnConsequences()
        {
            Assert.IsFalse(typeof(InstitutionalConsequenceLoop).Assembly.GetTypes().Any(value =>
                value.Name.IndexOf("Director", StringComparison.OrdinalIgnoreCase) >= 0));

            SocietyState noDisclosureState = PrototypePopulationFactory.Create(ProofSeed);
            foreach (AgentState agent in noDisclosureState.Agents)
                agent.Standing.CanGiveEvidence = false;
            InstitutionalConsequenceRun noDisclosure = InstitutionalConsequenceLoop.RunForAssessor(
                ProofSeed,
                InstitutionalPolicyConfigurations.PrecedentMachine(),
                noDisclosureState,
                validateProof: false);
            Assert.IsFalse(noDisclosure.AssessorActionTraces.Any(value =>
                value.Action == SocietyActionKind.Disclose));
            Assert.AreEqual(RulingDisposition.Denied,
                InitialRuling(noDisclosure.Report).Disposition);
            Assert.IsEmpty(noDisclosure.Report.Holdings,
                "Removing autonomous disclosures must remove the appellate holding.");
            Assert.IsEmpty(noDisclosure.Report.ConnectedOutcomes);

            SocietyState noAidState = PrototypePopulationFactory.Create(ProofSeed);
            noAidState.Agents.Single(value => value.SimulationOrdinal == 0)
                .Standing.CanSeekAid = false;
            InstitutionalConsequenceRun noAid = InstitutionalConsequenceLoop.RunForAssessor(
                ProofSeed,
                InstitutionalPolicyConfigurations.ProvisionalTrust(),
                noAidState,
                validateProof: false);
            Assert.IsFalse(noAid.AssessorActionTraces.Any(value =>
                value.ActorId == noAid.FinalSocietyState.Agents.Single(agent =>
                    agent.SimulationOrdinal == 0).StableId &&
                value.Action == SocietyActionKind.SeekAid));
            Assert.AreEqual(RulingDisposition.ProvisionallyRecognised,
                InitialRuling(noAid.Report).Disposition);
            Assert.IsEmpty(noAid.RelianceLedger,
                "Removing the claimant's aid action must remove reliance.");
            Assert.IsTrue(noAid.AlternativeOptions.Single().Available);
            Assert.IsFalse(noAid.Report.DescendantCases.Any(value =>
                value.Kind == DescendantCaseKind.Reliance));

            SocietyState noLaterWorkState = PrototypePopulationFactory.Create(ProofSeed);
            noLaterWorkState.Agents.Single(value => value.SimulationOrdinal == 6)
                .Standing.CanWork = false;
            InstitutionalConsequenceRun noLaterWork = InstitutionalConsequenceLoop.RunForAssessor(
                ProofSeed,
                InstitutionalPolicyConfigurations.PrecedentMachine(),
                noLaterWorkState,
                validateProof: false);
            Assert.IsFalse(noLaterWork.AssessorActionTraces.Any(value =>
                value.ActorId == noLaterWork.FinalSocietyState.Agents.Single(agent =>
                    agent.SimulationOrdinal == 6).StableId &&
                value.Action == SocietyActionKind.Work));
            Assert.AreEqual(RulingDisposition.ReversedAndRecognised,
                PrimaryAppealRuling(noLaterWork.Report).Disposition);
            Assert.AreEqual(1, noLaterWork.Report.Holdings.Count);
            Assert.IsFalse(noLaterWork.Report.DescendantCases.Any(value =>
                value.CaseId == InstitutionalConsequenceLoop.LaterCaseId));
            Assert.IsEmpty(noLaterWork.Report.ConnectedOutcomes,
                "The calendar cannot manufacture the later case without the worker's action.");

            SocietyState noAppealState = PrototypePopulationFactory.Create(ProofSeed);
            noAppealState.Agents.Single(value => value.SimulationOrdinal == 0)
                .Standing.CanAppeal = false;
            noAppealState.Agents.Single(value => value.SimulationOrdinal == 6)
                .Standing.CanAppeal = false;
            InstitutionalConsequenceRun noAppeal = InstitutionalConsequenceLoop.RunForAssessor(
                ProofSeed,
                InstitutionalPolicyConfigurations.PrecedentMachine(),
                noAppealState,
                validateProof: false);
            Assert.IsFalse(noAppeal.AssessorActionTraces.Any(value =>
                value.Action == SocietyActionKind.Appeal));
            Assert.AreEqual(RulingDisposition.Denied,
                InitialRuling(noAppeal.Report).Disposition);
            Assert.IsEmpty(noAppeal.Report.Appeals);
            Assert.IsEmpty(noAppeal.Report.Holdings);
            Assert.IsEmpty(noAppeal.Report.ConnectedOutcomes);
        }

        [Test]
        public void ConfigurationLabelsAndEnumKindDoNotDriveBehaviour()
        {
            InstitutionalPolicyConfiguration source =
                InstitutionalPolicyConfigurations.ProvisionalTrust();
            InstitutionalPolicyConfiguration renamed = source.CloneWithIdentity(
                "configuration.renamed", "renamed-policy.v99");
            renamed.Kind = InstitutionalPolicyKind.RecordsFirst;

            Assert.AreEqual(
                StructuralConsequenceSignature(RunAssessor(source)),
                StructuralConsequenceSignature(RunAssessor(renamed)));
        }

        [Test]
        public void TimelineNeverPlacesAnEffectBeforeItsCause()
        {
            foreach (InstitutionalPolicyConfiguration policy in AllPolicies())
            {
                InstitutionalConsequenceReport report = Run(policy);
                Assert.That(report.Timeline.Select(value => value.Cycle), Is.Ordered);
                Dictionary<string, long> rulingCycles = report.Rulings.ToDictionary(
                    value => value.RulingId, value => value.Cycle, StringComparer.Ordinal);
                foreach (OfficialStatusMutation mutation in report.OfficialStatusMutations)
                    Assert.AreEqual(rulingCycles[mutation.CauseId], mutation.Cycle);
                foreach (Appeal appeal in report.Appeals)
                {
                    Assert.Less(rulingCycles[appeal.ChallengedRulingId], appeal.FiledCycle);
                    Assert.LessOrEqual(appeal.FiledCycle, appeal.HearingCycle);
                    Assert.GreaterOrEqual(rulingCycles[appeal.ResultingRulingId], appeal.FiledCycle);
                }
            }
        }

        [Test]
        public void SharedOpportunity_IsReservedOnceAndLoserUsesFrozenFallback()
        {
            SocietyState state = PrototypePopulationFactory.Create(ProofSeed);
            AgentState first = state.Agents.Single(value => value.SimulationOrdinal == 0);
            AgentState second = state.Agents.Single(value => value.SimulationOrdinal == 1);
            const string opportunityId = "aid-opportunity:shared-test";
            var input = new SimulationInput
            {
                IncidentId = "incident.atomic-opportunity-test",
                WorkAvailable = false,
                AidAvailable = true,
                DisclosureRequested = false,
                AppealWindowOpen = false,
                RestrictAidToOpportunities = true,
                VisibleAgentIds = new List<string>(),
                AidOpportunities = new List<AidOpportunity>
                {
                    new AidOpportunity
                    {
                        OpportunityId = opportunityId,
                        PurposeId = "aid.shared-capacity",
                        UtilityBonus = 100,
                        EligibleAgentIds = new List<string>
                        {
                            first.StableId,
                            second.StableId,
                        },
                    },
                },
            };

            SimulationStepResult result = new SocietySimulation().Advance(state, input);
            List<SocietyEvent> claimed = result.Events.Where(value =>
                value.OpportunityId == opportunityId &&
                value.Kind == SocietyEventKind.AidRequested).ToList();
            List<SocietyEvent> rejectedAtCapacity = result.Events.Where(value =>
                value.OpportunityId == opportunityId &&
                value.Kind == SocietyEventKind.NoActionObserved).ToList();
            Assert.AreEqual(1, claimed.Count);
            Assert.AreEqual(first.StableId, claimed[0].ActorId,
                "Stable simulation ordinal owns a contested opportunity.");
            Assert.IsEmpty(rejectedAtCapacity,
                "Capacity rejection must not be emitted as the selected action.");

            AgentDecision fallback = result.Decisions.Single(value =>
                value.ActorId == second.StableId);
            Assert.Greater(fallback.SelectedCandidateRank, 0);
            Assert.IsNull(fallback.OpportunityId);
            Assert.AreEqual(SocietyActionKind.Idle, fallback.Action);
            CapacityReservationTrace rejected = fallback.CapacityReservations.Single(value =>
                value.OpportunityId == opportunityId);
            Assert.IsFalse(rejected.Awarded);
            Assert.AreEqual(first.StableId, rejected.HolderActorId);
            Assert.IsTrue(result.Events.Any(value =>
                value.ActorId == second.StableId &&
                value.Kind == SocietyEventKind.NoActionObserved &&
                value.OpportunityId == null));
        }

        [Test]
        public void ValidatorRejectsSplicedCausesPrecedentPairsAndOneSidedTransfers()
        {
            InstitutionalConsequenceReport spliced = Run(
                InstitutionalPolicyConfigurations.PrecedentMachine());
            DescendantCase primaryAppeal = spliced.DescendantCases.Single(value =>
                value.CaseId == "case.primary-appeal");
            primaryAppeal.OriginatingEventId = spliced.ObservedAgentActions
                .First(value => !primaryAppeal.SourceActionEventIds.Contains(value.ActionEventId))
                .ActionEventId;
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalConsequenceValidator.Validate(spliced));

            InstitutionalConsequenceReport wrongParentKind = Run(
                InstitutionalPolicyConfigurations.PrecedentMachine());
            DescendantCase related = LaterCase(wrongParentKind);
            related.ParentCauseId = related.OriginatingRulingId;
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalConsequenceValidator.Validate(wrongParentKind));

            InstitutionalConsequenceReport mismatchedPrecedent = Run(
                InstitutionalPolicyConfigurations.PrecedentMachine());
            LaterRuling(mismatchedPrecedent).CitedScopeIds[0] = "scope.unrelated";
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalConsequenceValidator.Validate(mismatchedPrecedent));

            InstitutionalConsequenceReport oneSidedTransfer = Run(
                InstitutionalPolicyConfigurations.PrecedentMachine());
            ConnectedOutcomePair pair = oneSidedTransfer.ConnectedOutcomes.Single();
            OfficialStatusMutation winnerGrant = oneSidedTransfer.OfficialStatusMutations
                .Single(value =>
                    value.CauseId == LaterRuling(oneSidedTransfer).RulingId &&
                    value.AffectedAgentId == pair.WinnerAgentId &&
                    value.StatusId == "paid-shift-allocation" && value.AfterRecognised);
            winnerGrant.AffectedAgentId = pair.LoserAgentId;
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalConsequenceValidator.Validate(oneSidedTransfer));
        }

        [Test]
        public void ValidatorAcceptsHoldingSupportingStrictSubsetOfSourceRulingEvidence()
        {
            InstitutionalConsequenceReport report = Run(
                InstitutionalPolicyConfigurations.PrecedentMachine());
            Holding holding = report.Holdings.Single();
            Ruling sourceRuling = report.Rulings.Single(value =>
                value.RulingId == holding.SourceRulingId);

            Assert.Greater(sourceRuling.EvidenceArtifactIds.Count, 1,
                "The proof fixture must expose a strict evidence subset.");
            holding.SupportingEvidenceArtifactIds = new List<string>
            {
                sourceRuling.EvidenceArtifactIds[0],
            };

            Assert.DoesNotThrow(() => InstitutionalConsequenceValidator.Validate(report));
        }

        private static InstitutionalConsequenceReport Run(InstitutionalPolicyConfiguration policy)
            => InstitutionalConsequenceLoop.RunProof(ProofSeed, policy);

        private static InstitutionalConsequenceRun RunAssessor(
            InstitutionalPolicyConfiguration policy)
            => InstitutionalConsequenceLoop.RunForAssessor(ProofSeed, policy);

        private static IEnumerable<InstitutionalPolicyConfiguration> AllPolicies()
        {
            yield return InstitutionalPolicyConfigurations.RecordsFirst();
            yield return InstitutionalPolicyConfigurations.ProvisionalTrust();
            yield return InstitutionalPolicyConfigurations.PrecedentMachine();
        }

        private static Ruling InitialRuling(InstitutionalConsequenceReport report)
            => report.Rulings.Single(value => value.RulingId == "ruling:primary:initial:5");

        private static Ruling PrimaryAppealRuling(InstitutionalConsequenceReport report)
            => report.Rulings.Single(value => value.RulingId == "ruling:primary:appeal:11");

        private static Ruling LaterRuling(InstitutionalConsequenceReport report)
            => report.Rulings.Single(value => value.RulingId == "ruling:later:appeal:14");

        private static DescendantCase LaterCase(InstitutionalConsequenceReport report)
            => report.DescendantCases.Single(value =>
                value.CaseId == InstitutionalConsequenceLoop.LaterCaseId);

        private static OfficialFinding Finding(InstitutionalConsequenceReport report, string id)
            => report.OfficialFindings.Single(value => value.FindingId == id);

        private static bool TraceHasInstitutionalEffect(
            InstitutionalConsequenceRun run,
            AgentActionTrace trace)
        {
            if (trace.ResultEventIds.Count == 0) return false;
            HashSet<string> resultIds = trace.ResultEventIds.ToHashSet(StringComparer.Ordinal);
            if (run.Report.EvidenceArtifacts.Any(value =>
                resultIds.Contains(value.Provenance.SourceSocietyEventId))) return true;
            if (run.Report.Appeals.Any(value =>
                resultIds.Contains(value.FilingActionEventId))) return true;
            if (run.RelianceLedger.Any(value =>
                resultIds.Contains(value.SourceActionEventId))) return true;
            if (run.Report.DescendantCases.Any(value =>
                value.SourceActionEventIds.Any(resultIds.Contains))) return true;
            return false;
        }

        private static void AssertReasonSource(AgentActionTrace trace, DecisionReason reason)
        {
            string reasonId = reason.ReasonId ?? string.Empty;
            string sourceId = reason.SourceId;
            AgentPerception perception = trace.PerceptionSnapshot;
            if (reasonId.StartsWith("need.", StringComparison.Ordinal))
            {
                Assert.IsTrue(Enum.TryParse(sourceId, out NeedKind kind) &&
                              perception.GetNeed(kind) != null,
                    $"{trace.DecisionId}:{reasonId}:{sourceId}");
            }
            else if (reasonId.StartsWith("belief.", StringComparison.Ordinal))
            {
                Assert.NotNull(perception.GetBelief(sourceId),
                    $"{trace.DecisionId}:{reasonId}:{sourceId}");
            }
            else if (reasonId.StartsWith("relationship.", StringComparison.Ordinal) ||
                     reasonId == "perception.target-need")
            {
                RelationshipState relationship = perception.GetRelationship(sourceId);
                if (relationship == null)
                    Assert.AreEqual(0, reason.ScoreDelta,
                        $"An absent relationship may contribute no utility: " +
                        $"{trace.DecisionId}:{reasonId}:{sourceId}");
            }
            else if (reasonId == "standing.required-status")
            {
                Assert.IsTrue(perception.Standing.IsRecognised(sourceId),
                    $"{trace.DecisionId}:{reasonId}:{sourceId}");
            }
            else if (reasonId.StartsWith("opportunity.", StringComparison.Ordinal))
            {
                bool exists = trace.InputSnapshot.WorkOpportunities.Any(value =>
                                  value.OpportunityId == sourceId) ||
                              trace.InputSnapshot.AidOpportunities.Any(value =>
                                  value.OpportunityId == sourceId) ||
                              trace.InputSnapshot.AppealOpportunities.Any(value =>
                                  value.OpportunityId == sourceId);
                Assert.IsTrue(exists, $"{trace.DecisionId}:{reasonId}:{sourceId}");
            }
            else if (reasonId.StartsWith("regime.", StringComparison.Ordinal))
            {
                Assert.NotNull(trace.RegimeSnapshot);
            }
        }

        private static string CanonicalJson(InstitutionalConsequenceReport report)
            => JsonConvert.SerializeObject(report, Formatting.None);

        private static string MaterialSignature(InstitutionalConsequenceRun run)
        {
            var builder = new StringBuilder();
            foreach (Ruling ruling in run.Report.Rulings)
                builder.Append(ruling.Cycle).Append(':').Append(ruling.Disposition).Append('|');
            foreach (MaterialConsequence material in run.Report.MaterialConsequences)
                builder.Append(material.Cycle).Append(':').Append(material.Kind).Append(':')
                    .Append(material.ResourceDelta).Append('|');
            builder.Append("reliance=").Append(run.RelianceLedger.Count).Append('|')
                .Append("holdings=").Append(run.Report.Holdings.Count).Append('|')
                .Append("connected=").Append(run.Report.ConnectedOutcomes.Count);
            return builder.ToString();
        }

        private static string AssessorReplaySignature(InstitutionalConsequenceRun run)
        {
            var builder = new StringBuilder();
            foreach (LivedEvent lived in run.AuthoritativeEvents)
                builder.Append("L:").Append(lived.LivedEventId).Append(':')
                    .Append(lived.Cycle).Append(':').Append(lived.SubjectAgentId).Append(':')
                    .Append(lived.AffectedNeed).Append(':').Append(lived.NeedPressureDelta).Append('|');
            foreach (AuthoritativeBeliefLink belief in run.AuthoritativeBeliefLinks)
                builder.Append("BL:").Append(belief.LivedEventId).Append(':')
                    .Append(belief.AgentId).Append(':').Append(belief.BeliefId).Append('|');
            foreach (AuthoritativeEvidenceLink evidence in run.AuthoritativeEvidenceLinks)
                builder.Append("EL:").Append(evidence.LivedEventId).Append(':')
                    .Append(evidence.EvidenceArtifactId).Append(':')
                    .Append(evidence.ObservationKindId).Append('|');
            foreach (AgentActionTrace trace in run.AssessorActionTraces)
            {
                builder.Append("T:").Append(trace.Cycle).Append(':').Append(trace.DecisionId)
                    .Append(':').Append(trace.Action).Append(':').Append(trace.UtilityScore).Append('[');
                foreach (DecisionReason reason in trace.Reasons)
                    builder.Append(reason.ReasonId).Append(':').Append(reason.SourceId)
                        .Append('=').Append(reason.ScoreDelta).Append(',');
                builder.Append("]{");
                foreach (CandidateEvaluation candidate in trace.CandidateEvaluations)
                    builder.Append(candidate.CandidateId).Append('=')
                        .Append(candidate.Score).Append(',');
                builder.Append("}|");
            }
            foreach (RelianceEvent reliance in run.RelianceLedger)
                builder.Append("RL:").Append(reliance.RelianceEventId).Append(':')
                    .Append(reliance.SourceActionEventId).Append(':')
                    .Append(reliance.CreditsBefore).Append('>').Append(reliance.CreditsAfter)
                    .Append(':').Append(reliance.SurvivedReversal).Append('|');
            foreach (EconomicAccountState account in run.EconomicAccounts
                .OrderBy(value => value.AgentId, StringComparer.Ordinal))
                builder.Append("EA:").Append(account.AgentId).Append(':')
                    .Append(account.AvailableCredits).Append(':')
                    .Append(account.CommittedIncome).Append('|');
            foreach (AlternativeOptionState option in run.AlternativeOptions)
                builder.Append("AO:").Append(option.OptionId).Append(':')
                    .Append(option.Available).Append(':')
                    .Append(option.ChangedByActionEventId).Append('|');
            foreach (WorkAllocationState allocation in run.WorkAllocations)
                builder.Append("WA:").Append(allocation.AllocationId).Append(':')
                    .Append(allocation.PaidHolderAgentId).Append(':')
                    .Append(allocation.CommittedWage).Append(':')
                    .Append(allocation.LastMutationCauseId).Append('|');
            builder.Append("STATE:")
                .Append(JsonConvert.SerializeObject(run.FinalSocietyState, Formatting.None));
            return builder.ToString();
        }

        private static string OrdinalBehaviourSignature(InstitutionalConsequenceRun run)
        {
            Dictionary<string, int> ordinals = run.FinalSocietyState.Agents.ToDictionary(
                value => value.StableId,
                value => value.SimulationOrdinal,
                StringComparer.Ordinal);
            var builder = new StringBuilder();
            foreach (AgentActionTrace trace in run.AssessorActionTraces
                .OrderBy(value => value.Cycle)
                .ThenBy(value => ordinals[value.ActorId]))
            {
                builder.Append(trace.Cycle).Append(':').Append(ordinals[trace.ActorId]).Append(':')
                    .Append(trace.Action).Append(':').Append(trace.UtilityScore).Append('[');
                foreach (DecisionReason reason in trace.Reasons)
                    builder.Append(reason.ReasonId).Append('=').Append(reason.ScoreDelta).Append(',');
                builder.Append("]|");
            }
            return builder.ToString();
        }

        private static string StructuralConsequenceSignature(InstitutionalConsequenceRun run)
        {
            InstitutionalConsequenceReport report = run.Report;
            Dictionary<string, int> ordinals = run.FinalSocietyState.Agents.ToDictionary(
                value => value.StableId,
                value => value.SimulationOrdinal,
                StringComparer.Ordinal);
            var builder = new StringBuilder();
            foreach (ObservedAgentAction action in report.ObservedAgentActions
                .OrderBy(value => value.Cycle)
                .ThenBy(value => ordinals[value.ActorId]))
                builder.Append("A:").Append(action.Cycle).Append(':')
                    .Append(ordinals[action.ActorId]).Append(':').Append(action.Activity).Append('|');
            foreach (EvidenceArtifact evidence in report.EvidenceArtifacts
                .OrderBy(value => value.EnteredCycle)
                .ThenBy(value => value.PropositionId, StringComparer.Ordinal))
                builder.Append("E:").Append(evidence.EnteredCycle).Append(':')
                    .Append(evidence.Kind).Append(':').Append(evidence.PropositionId).Append(':')
                    .Append(evidence.BaseWeight).Append('|');
            foreach (Ruling ruling in report.Rulings)
                builder.Append("R:").Append(ruling.Cycle).Append(':')
                    .Append(ruling.Disposition).Append(':')
                    .Append(ruling.CitedHoldingIds.Count).Append('|');
            foreach (DescendantCase descendant in report.DescendantCases)
                builder.Append("D:").Append(descendant.OpenedCycle).Append(':')
                    .Append(descendant.Kind).Append(':').Append(descendant.Status).Append(':')
                    .Append(ordinals[descendant.ClaimantAgentId]).Append('|');
            foreach (WorkAllocationObservation allocation in report.WorkAllocations)
                builder.Append("W:").Append(ordinals[allocation.OriginalWorkerId]).Append('>')
                    .Append(ordinals[allocation.PaidHolderAgentId]).Append(':')
                    .Append(allocation.CommittedWage).Append('|');
            builder.Append("RL=").Append(run.RelianceLedger.Count).Append('|')
                .Append("H=").Append(report.Holdings.Count).Append('|')
                .Append("CO=").Append(report.ConnectedOutcomes.Count);
            return builder.ToString();
        }

        private static void RemapIdentityAndOrder(SocietyState state)
        {
            Dictionary<string, string> agentMap = state.Agents.ToDictionary(
                value => value.StableId,
                value => $"remapped.person.{value.SimulationOrdinal}",
                StringComparer.Ordinal);
            Dictionary<string, string> employerMap = state.Agents
                .Where(value => !string.IsNullOrEmpty(value.EmployerId))
                .Select(value => value.EmployerId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select((value, index) => new { value, id = $"remapped.organisation.{index}" })
                .ToDictionary(value => value.value, value => value.id, StringComparer.Ordinal);

            foreach (AgentState agent in state.Agents)
            {
                string oldId = agent.StableId;
                agent.StableId = agentMap[oldId];
                agent.DisplayName = $"Scrambled Name {100 - agent.SimulationOrdinal}";
                if (!string.IsNullOrEmpty(agent.EmployerId))
                    agent.EmployerId = employerMap[agent.EmployerId];
                foreach (RelationshipState relationship in agent.Relationships)
                    relationship.TargetAgentId = agentMap[relationship.TargetAgentId];
                foreach (CommitmentState commitment in agent.Commitments)
                {
                    if (agentMap.TryGetValue(commitment.TargetId, out string person))
                        commitment.TargetId = person;
                    else if (employerMap.TryGetValue(commitment.TargetId, out string employer))
                        commitment.TargetId = employer;
                }
                foreach (BeliefState belief in agent.Beliefs)
                {
                    if (agentMap.TryGetValue(belief.SubjectId, out string subject))
                        belief.SubjectId = subject;
                    else if (employerMap.TryGetValue(belief.SubjectId, out string employerSubject))
                        belief.SubjectId = employerSubject;
                    if (agentMap.TryGetValue(belief.ObjectId, out string target))
                        belief.ObjectId = target;
                    else if (employerMap.TryGetValue(belief.ObjectId, out string employerObject))
                        belief.ObjectId = employerObject;
                }
            }
            state.Agents.Reverse();
        }

        private static bool PublicGraphContains(Type root, Type forbidden, HashSet<Type> visited)
        {
            if (root == forbidden) return true;
            if (root == null || !visited.Add(root)) return false;
            if (root.IsArray) return PublicGraphContains(root.GetElementType(), forbidden, visited);
            if (root.IsGenericType)
                foreach (Type argument in root.GetGenericArguments())
                    if (PublicGraphContains(argument, forbidden, visited)) return true;
            if (root.Namespace == null || !root.Namespace.StartsWith("Desk42", StringComparison.Ordinal))
                return false;
            foreach (FieldInfo field in root.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (PublicGraphContains(field.FieldType, forbidden, visited)) return true;
            foreach (PropertyInfo property in root.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (PublicGraphContains(property.PropertyType, forbidden, visited)) return true;
            return false;
        }
    }
}
