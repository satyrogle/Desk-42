using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalPlayerRulingTests
    {
        [Test]
        public void Commit_FreezesValidatedOfficialRecordAndExecutableScope()
        {
            (SocietyState society, EndogenousDocketState state,
                EndogenousInstitutionalCase opened) = OpenPossessionCase();
            PlayerRulingCommand command = RecogniseCommand(opened);

            CommittedPlayerRuling committed = EndogenousPlayerRulingService.Commit(
                society, state, command);

            Assert.AreEqual("ruling:command.test", committed.RulingId);
            Assert.AreEqual(opened.EvidenceEnvelopeHash, committed.EvidenceEnvelopeHash);
            Assert.AreEqual(RulingDisposition.Recognised, committed.Disposition);
            Assert.AreEqual(
                EndogenousPlayerRulingService.PossessionHoldingRule,
                committed.HoldingRuleId);
            Assert.AreEqual(
                EndogenousPlayerRulingService.RestorePossessionRemedy,
                committed.RemedyDefinitionIds.Single());
            Assert.IsTrue(ScopeExpressionEvaluator.Matches(
                committed.Scope,
                new ScopeMatchContext
                {
                    IssueId = EndogenousIssueKindIds.PossessionDispute,
                    JurisdictionId = "branch-42",
                }));

            command.RecognisedFactIds[0] = "mutated";
            command.CitedEvidenceArtifactIds[0] = "mutated";
            command.RemedyDefinitionIds[0] = "mutated";
            command.Scope.Value = "mutated";
            Assert.AreNotEqual("mutated", committed.RecognisedFactIds[0]);
            Assert.AreNotEqual("mutated", committed.CitedEvidenceArtifactIds[0]);
            Assert.AreNotEqual("mutated", committed.RemedyDefinitionIds[0]);
            Assert.AreEqual(
                EndogenousIssueKindIds.PossessionDispute,
                committed.Scope.Value);
        }

        [Test]
        public void Commit_ReplayIsIdempotent_ConflictingPayloadIsRejected()
        {
            (SocietyState society, EndogenousDocketState state,
                EndogenousInstitutionalCase opened) = OpenPossessionCase();
            PlayerRulingCommand command = RecogniseCommand(opened);
            CommittedPlayerRuling first = EndogenousPlayerRulingService.Commit(
                society, state, command);

            CommittedPlayerRuling replay = EndogenousPlayerRulingService.Commit(
                society, state, RecogniseCommand(opened));

            Assert.AreSame(first, replay);
            Assert.AreEqual(1, state.Rulings.Count);

            PlayerRulingCommand conflict = RecogniseCommand(opened);
            conflict.Disposition = RulingDisposition.Denied;
            conflict.RemedyDefinitionIds = new List<string>
            {
                EndogenousPlayerRulingService.NoChangeRemedy,
            };
            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, conflict));
            Assert.AreEqual(1, state.Rulings.Count);
        }

        [Test]
        public void Commit_RejectsStaleCaseVersionOrEvidenceEnvelope()
        {
            (SocietyState society, EndogenousDocketState state,
                EndogenousInstitutionalCase opened) = OpenPossessionCase();
            PlayerRulingCommand staleVersion = RecogniseCommand(opened);
            staleVersion.ExpectedCaseVersion++;
            PlayerRulingCommand staleEvidence = RecogniseCommand(opened);
            staleEvidence.EvidenceEnvelopeHash = "stale";

            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, staleVersion));
            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, staleEvidence));
            Assert.IsEmpty(state.Rulings);
        }

        [Test]
        public void Commit_RejectsFactsOrEvidenceAbsentFromOfficialCaseRecord()
        {
            (SocietyState society, EndogenousDocketState state,
                EndogenousInstitutionalCase opened) = OpenPossessionCase();
            PlayerRulingCommand hiddenFact = RecogniseCommand(opened);
            hiddenFact.RecognisedFactIds[0] = "fact:lived.authoritative-theft";
            PlayerRulingCommand hiddenEvidence = RecogniseCommand(opened);
            hiddenEvidence.CitedEvidenceArtifactIds[0] =
                "material:authoritative-transfer-event";

            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, hiddenFact));
            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, hiddenEvidence));
            Assert.IsEmpty(state.Rulings);
        }

        [Test]
        public void Commit_RejectsHoldingOrRemedyIncompatibleWithIssueAndDisposition()
        {
            (SocietyState society, EndogenousDocketState state,
                EndogenousInstitutionalCase opened) = OpenPossessionCase();
            PlayerRulingCommand wrongHolding = RecogniseCommand(opened);
            wrongHolding.HoldingRuleId = EndogenousPlayerRulingService.AccessHoldingRule;
            PlayerRulingCommand wrongRemedy = RecogniseCommand(opened);
            wrongRemedy.RemedyDefinitionIds[0] =
                EndogenousPlayerRulingService.NoChangeRemedy;

            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, wrongHolding));
            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, wrongRemedy));
            Assert.IsEmpty(state.Rulings);
        }

        [Test]
        public void Commit_RejectsHiddenScopeVocabularyAndUnimplementedRetrospection()
        {
            (SocietyState society, EndogenousDocketState state,
                EndogenousInstitutionalCase opened) = OpenPossessionCase();
            PlayerRulingCommand hiddenScope = RecogniseCommand(opened);
            hiddenScope.Scope = Predicate(
                ScopePredicateKind.FactEquals,
                "lived.true-holder",
                "agent.respondent");
            PlayerRulingCommand retrospective = RecogniseCommand(opened);
            retrospective.TemporalReach = TemporalReach.Retrospective;

            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, hiddenScope));
            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, retrospective));
            Assert.IsEmpty(state.Rulings);
        }

        [Test]
        public void Denial_CanBeProcedurallyValidWhileContradictingLivedIncidentTruth()
        {
            (SocietyState society, EndogenousDocketState state,
                EndogenousInstitutionalCase opened) = OpenPossessionCase();
            Assert.AreEqual(1, state.IncidentCandidates.Count,
                "The assessor substrate records the lived possession conflict.");
            PlayerRulingCommand command = RecogniseCommand(opened);
            command.Disposition = RulingDisposition.Denied;
            command.RemedyDefinitionIds = new List<string>
            {
                EndogenousPlayerRulingService.NoChangeRemedy,
            };

            CommittedPlayerRuling committed = EndogenousPlayerRulingService.Commit(
                society, state, command);

            Assert.AreEqual(RulingDisposition.Denied, committed.Disposition);
            Assert.AreEqual(1, state.Rulings.Count);
            Assert.AreEqual(1, state.IncidentCandidates.Count,
                "Committing official reality must not rewrite lived truth.");
        }

        [Test]
        public void ScopeGrammar_AllAnyNotAndPredicates_AreExecutableAndBounded()
        {
            ScopeExpression expression = new ScopeExpression
            {
                Kind = ScopeExpressionKind.All,
                Children = new List<ScopeExpression>
                {
                    Predicate(
                        ScopePredicateKind.IssueEquals,
                        null,
                        EndogenousIssueKindIds.PossessionDispute),
                    new ScopeExpression
                    {
                        Kind = ScopeExpressionKind.Any,
                        Children = new List<ScopeExpression>
                        {
                            Predicate(
                                ScopePredicateKind.JurisdictionEquals,
                                null,
                                "branch-42"),
                            Predicate(
                                ScopePredicateKind.OrganisationEquals,
                                null,
                                "organisation.other"),
                        },
                    },
                    new ScopeExpression
                    {
                        Kind = ScopeExpressionKind.Not,
                        Children = new List<ScopeExpression>
                        {
                            Predicate(
                                ScopePredicateKind.OfficialStatusEquals,
                                null,
                                "status.exempt"),
                        },
                    },
                },
            };

            Assert.IsTrue(ScopeExpressionEvaluator.Matches(
                expression,
                new ScopeMatchContext
                {
                    IssueId = EndogenousIssueKindIds.PossessionDispute,
                    JurisdictionId = "branch-42",
                }));

            ScopeExpression tooDeep = Predicate(
                ScopePredicateKind.IssueEquals, null, "issue");
            for (int i = 0; i < ScopeExpressionEvaluator.MaximumDepth + 1; i++)
            {
                tooDeep = new ScopeExpression
                {
                    Kind = ScopeExpressionKind.Not,
                    Children = new List<ScopeExpression> { tooDeep },
                };
            }
            Assert.Throws<InvalidOperationException>(() =>
                ScopeExpressionEvaluator.Validate(tooDeep));
        }

        [Test]
        public void ScopeMustApplyToCurrentOfficialCase()
        {
            (SocietyState society, EndogenousDocketState state,
                EndogenousInstitutionalCase opened) = OpenPossessionCase();
            PlayerRulingCommand command = RecogniseCommand(opened);
            command.Scope = Predicate(
                ScopePredicateKind.AgentEquals,
                null,
                "agent.not-a-party");

            Assert.Throws<InvalidOperationException>(() =>
                EndogenousPlayerRulingService.Commit(society, state, command));
        }

        private static (SocietyState, EndogenousDocketState, EndogenousInstitutionalCase)
            OpenPossessionCase()
        {
            SocietyState society = Society(Agent("agent.respondent", 0));
            society.CurrentTick = 5;
            var state = new EndogenousDocketState();
            const string incidentId = "incident.possession";
            const string observationId = "observation.camera";
            const string docketId = "docket.possession";
            state.IncidentCandidates.Add(new IncidentCandidate
            {
                CandidateId = incidentId,
                CauseEventIds = new List<string> { "material.transfer" },
                AffectedAgentIds = new List<string> { "agent.respondent" },
                ConflictKindId = EndogenousIssueKindIds.PossessionDispute,
                DetectedTick = 4,
                UnresolvedMaterialHarm = 60,
                SubjectResourceId = "resource.medicine",
                DedupeKey = "possession:material.transfer",
            });
            state.Observations.Add(new DocketObservation
            {
                ObservationId = observationId,
                RecordedTick = 4,
                ObservationKindId = "recorded-possession-change",
                IssueId = EndogenousIssueKindIds.PossessionDispute,
                PropositionId = "registered-asset-possession-changed",
                AllegedSubjectAgentId = "agent.respondent",
                OfficialResourceId = "resource.medicine",
                SourceRecordId = "record.camera",
                Reliability = 90,
                ObservedMaterialHarm = 60,
                OfficiallySubmitted = true,
                AuthorityIncidentCandidateId = incidentId,
            });
            state.DocketCandidates.Add(new DocketCandidate
            {
                DocketCandidateId = docketId,
                EligibilityRuleId = "observable-possession-conflict-v1",
                IssueId = EndogenousIssueKindIds.PossessionDispute,
                EligibleTick = 4,
                UnresolvedMaterialHarm = 60,
                ObservableEvidenceIds = new List<string> { observationId },
                PotentialPartyIds = new List<string> { "agent.respondent" },
                AuthorityIncidentCandidateId = incidentId,
            });
            EndogenousInstitutionalCase opened = EndogenousDocketService.AdmitNext(
                society, state);
            return (society, state, opened);
        }

        private static PlayerRulingCommand RecogniseCommand(
            EndogenousInstitutionalCase opened)
        {
            return new PlayerRulingCommand
            {
                CommandId = "command.test",
                CaseId = opened.CaseId,
                ExpectedCaseVersion = opened.CaseVersion,
                EvidenceEnvelopeHash = opened.EvidenceEnvelopeHash,
                RecognisedFactIds = new List<string>
                {
                    opened.AvailableFactIds[0],
                },
                CitedEvidenceArtifactIds = new List<string>
                {
                    opened.ObservationIds[0],
                },
                Disposition = RulingDisposition.Recognised,
                HoldingRuleId = EndogenousPlayerRulingService.PossessionHoldingRule,
                Scope = Predicate(
                    ScopePredicateKind.IssueEquals,
                    null,
                    EndogenousIssueKindIds.PossessionDispute),
                TemporalReach = TemporalReach.Prospective,
                RemedyDefinitionIds = new List<string>
                {
                    EndogenousPlayerRulingService.RestorePossessionRemedy,
                },
            };
        }

        private static ScopeExpression Predicate(
            ScopePredicateKind kind,
            string key,
            string value)
        {
            return new ScopeExpression
            {
                Kind = ScopeExpressionKind.Predicate,
                PredicateKind = kind,
                Key = key,
                Value = value,
            };
        }

        private static SocietyState Society(params AgentState[] agents)
        {
            return new SocietyState
            {
                MasterSeed = 42,
                Agents = agents.ToList(),
            };
        }

        private static AgentState Agent(string id, int ordinal)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = "portrait.generic",
                DisplayName = id,
                SpeciesId = "species.generic",
                HouseholdId = $"household.{id}",
                EmployerId = "employer.generic",
                Disposition = new AgentDispositionState(),
                Standing = new InstitutionalStandingState(),
            };
            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = kind, Pressure = 0 });
            return agent;
        }
    }
}
