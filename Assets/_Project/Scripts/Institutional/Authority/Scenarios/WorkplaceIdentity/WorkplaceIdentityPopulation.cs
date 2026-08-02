using System;
using System.Collections.Generic;

namespace Desk42.Institutional.Scenarios.WorkplaceIdentity
{
    public static partial class WorkplaceIdentityScenario
    {
        private static SocietyState CreateInitialSociety()
        {
            return new SocietyState
            {
                MasterSeed = 420042,
                CurrentTick = StartCycle,
                Regime = new InstitutionalRegimeState(),
                Agents = new List<AgentState>
                {
                    CreatePrimaryClaimant(),
                    CreateDependent(),
                    CreateEmployerRepresentative(),
                    CreateContingentHolder(),
                    CreateLaterClaimant(),
                },
            };
        }

        private static AgentState CreatePrimaryClaimant()
        {
            AgentState agent = Agent(
                PrimaryClaimantAgentId,
                0,
                "Elias Vale",
                EmployerId,
                "household.workplace.vale",
                health: 45,
                subsistence: 40,
                safety: 36,
                belonging: 30,
                autonomy: 50,
                institutionalTrust: 0,
                riskTolerance: 50,
                candour: 40,
                solidarity: 40,
                duty: 45,
                institutionalReliance: 20,
                canWork: false,
                canSeekAid: true,
                canAppeal: true,
                canGiveEvidence: true);
            agent.Commitments.Add(Commitment(
                "commitment.workplace.primary-continuity",
                "identity-continuity-claimant",
                EmployerId,
                90));
            agent.Standing.SetRecognised(
                InstitutionalStatusIds.AdverseDecision,
                false);
            agent.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.workplace.identity-continuity",
                PropositionId = IdentityPropositionId,
                SubjectId = PrimaryClaimantAgentId,
                ObjectId = EmployerId,
                SourceId = "source.workplace.old-roster-copy",
                Confidence = 60,
                Secrecy = 80,
                EmotionalWeight = 40,
                AcquiredTick = 0,
            });
            agent.AnomalyRules.Add(new AnomalyStatusRule
            {
                TraitId = IdentityAnomalyTraitId,
                RequiredOfficialStatusId = InstitutionalStatusIds.AdverseDecision,
                AffectedNeed = NeedKind.Autonomy,
                RecognisedPressureDelta = 2,
                UnrecognisedPressureDelta = 0,
                MinimumTicksBetweenActivations = 2,
                ObservableEffectId = "effect.workplace.denial-echo",
            });
            return agent;
        }

        private static AgentState CreateDependent()
        {
            AgentState agent = Agent(
                DependentAgentId,
                1,
                "Nia Vale",
                "employer.none",
                "household.workplace.vale",
                health: 60,
                subsistence: 45,
                safety: 40,
                belonging: 35,
                autonomy: 30,
                institutionalTrust: 10,
                riskTolerance: 30,
                candour: 45,
                solidarity: 55,
                duty: 40,
                institutionalReliance: 25,
                canWork: false,
                canSeekAid: false,
                canAppeal: false,
                canGiveEvidence: false);
            agent.Commitments.Add(Commitment(
                "commitment.workplace.dependent",
                "household-dependent",
                PrimaryClaimantAgentId,
                90));
            return agent;
        }

        private static AgentState CreateEmployerRepresentative()
        {
            AgentState agent = Agent(
                EmployerAgentId,
                2,
                "Arden Pike",
                EmployerId,
                "household.workplace.pike",
                health: 20,
                subsistence: 20,
                safety: 25,
                belonging: 25,
                autonomy: 20,
                institutionalTrust: 50,
                riskTolerance: 65,
                candour: 35,
                solidarity: 20,
                duty: 80,
                institutionalReliance: 70,
                canWork: false,
                canSeekAid: false,
                canAppeal: false,
                canGiveEvidence: false);
            agent.Commitments.Add(Commitment(
                "commitment.workplace.management",
                "management-authority",
                EmployerId,
                100));
            return agent;
        }

        private static AgentState CreateContingentHolder()
        {
            AgentState agent = Agent(
                ContingentHolderAgentId,
                3,
                "Mara Quill",
                EmployerId,
                "household.workplace.quill",
                health: 25,
                subsistence: 70,
                safety: 35,
                belonging: 30,
                autonomy: 30,
                institutionalTrust: 20,
                riskTolerance: 55,
                candour: 45,
                solidarity: 35,
                duty: 60,
                institutionalReliance: 40,
                canWork: true,
                canSeekAid: false,
                canAppeal: false,
                canGiveEvidence: false);
            agent.Commitments.Add(Commitment(
                "commitment.workplace.contingent-holder",
                "contingent-shift-holder",
                EmployerId,
                90));
            agent.Commitments.Add(Commitment(
                "commitment.workplace.contingent-employment",
                "employment",
                EmployerId,
                80));
            agent.Standing.SetRecognised(PaidShiftHolderStatusId, true);
            return agent;
        }

        private static AgentState CreateLaterClaimant()
        {
            AgentState agent = Agent(
                LaterClaimantAgentId,
                4,
                "Ivo Reed",
                EmployerId,
                "household.workplace.reed",
                health: 30,
                subsistence: 55,
                safety: 35,
                belonging: 35,
                autonomy: 55,
                institutionalTrust: 5,
                riskTolerance: 45,
                candour: 55,
                solidarity: 45,
                duty: 55,
                institutionalReliance: 30,
                canWork: false,
                canSeekAid: false,
                canAppeal: false,
                canGiveEvidence: false);
            agent.Commitments.Add(Commitment(
                "commitment.workplace.later-continuity",
                "later-identity-claimant",
                EmployerId,
                95));
            agent.Standing.SetRecognised(PaidShiftHolderStatusId, false);
            agent.AnomalyRules.Add(new AnomalyStatusRule
            {
                TraitId = IdentityAnomalyTraitId,
                RequiredOfficialStatusId = PaidShiftHolderStatusId,
                AffectedNeed = NeedKind.Autonomy,
                RecognisedPressureDelta = -2,
                UnrecognisedPressureDelta = 1,
                MinimumTicksBetweenActivations = 2,
                ObservableEffectId = "effect.workplace.shift-status-echo",
            });
            return agent;
        }

        private static AgentState Agent(
            string stableId,
            int ordinal,
            string displayName,
            string employerId,
            string householdId,
            int health,
            int subsistence,
            int safety,
            int belonging,
            int autonomy,
            int institutionalTrust,
            int riskTolerance,
            int candour,
            int solidarity,
            int duty,
            int institutionalReliance,
            bool canWork,
            bool canSeekAid,
            bool canAppeal,
            bool canGiveEvidence)
        {
            return new AgentState
            {
                StableId = stableId,
                SimulationOrdinal = ordinal,
                PresentationId = $"presentation.{stableId}",
                DisplayName = displayName,
                SpeciesId = "species.registered-person",
                HouseholdId = householdId,
                EmployerId = employerId,
                InstitutionalTrust = institutionalTrust,
                Disposition = new AgentDispositionState
                {
                    RiskTolerance = riskTolerance,
                    Candour = candour,
                    Solidarity = solidarity,
                    Duty = duty,
                    InstitutionalReliance = institutionalReliance,
                },
                Standing = new InstitutionalStandingState
                {
                    CanWork = canWork,
                    CanSeekAid = canSeekAid,
                    CanAppeal = canAppeal,
                    CanGiveEvidence = canGiveEvidence,
                },
                Needs = new List<NeedState>
                {
                    new NeedState { Kind = NeedKind.Health, Pressure = health },
                    new NeedState
                    {
                        Kind = NeedKind.Subsistence,
                        Pressure = subsistence,
                    },
                    new NeedState { Kind = NeedKind.Safety, Pressure = safety },
                    new NeedState
                    {
                        Kind = NeedKind.Belonging,
                        Pressure = belonging,
                    },
                    new NeedState { Kind = NeedKind.Autonomy, Pressure = autonomy },
                },
            };
        }

        private static CommitmentState Commitment(
            string commitmentId,
            string kind,
            string targetId,
            int strength)
        {
            return new CommitmentState
            {
                CommitmentId = commitmentId,
                Kind = kind,
                TargetId = targetId,
                Strength = strength,
            };
        }
    }
}
