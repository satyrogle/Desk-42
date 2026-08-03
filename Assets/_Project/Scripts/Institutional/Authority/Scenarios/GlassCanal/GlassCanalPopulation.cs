using System.Collections.Generic;

namespace Desk42.Institutional.Scenarios.GlassCanal
{
    public static partial class GlassCanalScenario
    {
        private const string CanalAuthorityEmployerId =
            "institution.glass-canal-authority";
        private const string WeatherOperatorEmployerId =
            "employer.bound-weather-works";

        private static SocietyState CreateInitialSociety()
        {
            return new SocietyState
            {
                MasterSeed = 420_902,
                CurrentTick = StartCycle,
                Regime = new InstitutionalRegimeState(),
                Agents = new List<AgentState>
                {
                    CreateMara(),
                    CreateNara(),
                    CreateKhet(),
                    CreateOrin(),
                    CreateIlya(),
                    CreateSera(),
                    CreateVey(),
                    CreateToma(),
                    CreateBystander(),
                },
            };
        }

        private static AgentState CreateMara()
        {
            AgentState agent = Agent(
                MaraAgentId,
                0,
                "Mara Kest",
                "employer.glass-canal-growers",
                "household.glass.kest",
                health: 60,
                subsistence: 50,
                safety: 70,
                belonging: 35,
                autonomy: 25,
                institutionalTrust: 20,
                riskTolerance: 60,
                candour: 90,
                solidarity: 50,
                duty: 50,
                institutionalReliance: 100,
                canWork: true,
                canSeekAid: true,
                canAppeal: true,
                canGiveEvidence: true);
            agent.Commitments.Add(Commitment(
                "commitment.glass.mara.downstream",
                "glass-primary-downstream",
                WatershedId,
                100));
            agent.Commitments.Add(Commitment(
                "commitment.glass.mara.employment",
                "employment",
                agent.EmployerId,
                100));
            agent.Standing.SetRecognised("status.glass.registered-water-user", true);
            agent.Standing.SetRecognised(UndissipatedOutputStatusId, true);
            agent.Standing.SetRecognised(InstitutionalStatusIds.AdverseDecision, false);
            agent.Beliefs.Add(Belief(
                "belief.glass.mara.resonance",
                ResonancePropositionId,
                BoundCloudId,
                WeatherOperatorEmployerId,
                confidence: 30,
                secrecy: 58,
                emotionalWeight: 20));
            agent.AnomalyRules.Add(new AnomalyStatusRule
            {
                TraitId = ControllerResonanceExposureTraitId,
                RequiredOfficialStatusId = UndissipatedOutputStatusId,
                AffectedNeed = NeedKind.Autonomy,
                RecognisedPressureDelta = 10,
                UnrecognisedPressureDelta = 0,
                MinimumTicksBetweenActivations = 1,
                LastAppliedTick = StartCycle,
                ObservableEffectId =
                    "effect.glass.undissipated-resonance-autonomy-pressure",
            });
            return agent;
        }

        private static AgentState CreateNara()
        {
            AgentState agent = Agent(
                NaraAgentId,
                1,
                "Nara Quill",
                WeatherOperatorEmployerId,
                "household.glass.quill",
                health: 20,
                subsistence: 30,
                safety: 35,
                belonging: 99,
                autonomy: 40,
                institutionalTrust: 60,
                riskTolerance: 20,
                candour: 15,
                solidarity: 20,
                duty: 80,
                institutionalReliance: 85,
                canWork: false,
                canSeekAid: false,
                canAppeal: true,
                canGiveEvidence: true);
            agent.Commitments.Add(Commitment(
                "commitment.glass.nara.operator",
                "glass-bound-weather-operator",
                WeatherOperatorEmployerId,
                100));
            agent.Standing.SetRecognised("status.glass.bound-weather-permit", true);
            agent.Standing.SetRecognised(ContinuingControlStatusId, false);
            agent.Standing.SetRecognised(UndissipatedOutputStatusId, true);
            agent.Standing.SetRecognised(InstitutionalStatusIds.AdverseDecision, false);
            agent.Beliefs.Add(Belief(
                "belief.glass.nara.permit-map",
                PermitBoundaryPropositionId,
                BoundCloudId,
                WeatherOperatorEmployerId,
                confidence: 60,
                secrecy: 100,
                emotionalWeight: 20));
            agent.AnomalyRules.Add(new AnomalyStatusRule
            {
                TraitId = BoundWeatherTraitId,
                RequiredOfficialStatusId = UndissipatedOutputStatusId,
                AffectedNeed = NeedKind.Belonging,
                RecognisedPressureDelta = 1,
                UnrecognisedPressureDelta = 0,
                MinimumTicksBetweenActivations = 3,
                LastAppliedTick = StartCycle,
                ObservableEffectId = ResonancePropositionId,
            });
            return agent;
        }

        private static AgentState CreateKhet()
        {
            AgentState agent = Agent(
                KhetAgentId,
                2,
                "Khet Daro",
                CanalAuthorityEmployerId,
                "household.glass.daro",
                health: 20,
                subsistence: 20,
                safety: 30,
                belonging: 30,
                autonomy: 80,
                institutionalTrust: 70,
                riskTolerance: 45,
                candour: 60,
                solidarity: 45,
                duty: 0,
                institutionalReliance: 60,
                canWork: true,
                canSeekAid: false,
                canAppeal: false,
                canGiveEvidence: false);
            agent.Commitments.Add(Commitment(
                "commitment.glass.khet.inspector",
                "glass-primary-inspector",
                CanalAuthorityEmployerId,
                100));
            agent.Commitments.Add(Commitment(
                "commitment.glass.khet.employment",
                "employment",
                CanalAuthorityEmployerId,
                100));
            agent.Standing.SetRecognised("status.glass.primary-sampling-authority", true);
            agent.Standing.SetRecognised("status.glass.canal-access", true);
            return agent;
        }

        private static AgentState CreateOrin()
        {
            AgentState agent = Agent(
                OrinAgentId,
                3,
                "Orin Pell",
                CanalAuthorityEmployerId,
                "household.glass.pell",
                health: 20,
                subsistence: 20,
                safety: 30,
                belonging: 30,
                autonomy: 80,
                institutionalTrust: 65,
                riskTolerance: 45,
                candour: 55,
                solidarity: 45,
                duty: 0,
                institutionalReliance: 55,
                canWork: true,
                canSeekAid: false,
                canAppeal: false,
                canGiveEvidence: false);
            agent.Commitments.Add(Commitment(
                "commitment.glass.orin.sampler",
                "glass-competing-sampler",
                CanalAuthorityEmployerId,
                100));
            agent.Commitments.Add(Commitment(
                "commitment.glass.orin.employment",
                "employment",
                CanalAuthorityEmployerId,
                100));
            agent.Standing.SetRecognised("status.glass.canal-access", true);
            agent.Standing.SetRecognised("status.glass.primary-sampling-authority", false);
            return agent;
        }

        private static AgentState CreateIlya()
        {
            AgentState agent = Agent(
                IlyaAgentId,
                4,
                "Ilya Ro",
                WeatherOperatorEmployerId,
                "household.glass.ro",
                health: 25,
                subsistence: 70,
                safety: 30,
                belonging: 30,
                autonomy: 30,
                institutionalTrust: 50,
                riskTolerance: 50,
                candour: 95,
                solidarity: 45,
                duty: 90,
                institutionalReliance: 65,
                canWork: true,
                canSeekAid: false,
                canAppeal: false,
                canGiveEvidence: true);
            agent.Commitments.Add(Commitment(
                "commitment.glass.ilya.controller-witness",
                "glass-controller-witness",
                WeatherOperatorEmployerId,
                100));
            agent.Commitments.Add(Commitment(
                "commitment.glass.ilya.employment",
                "employment",
                WeatherOperatorEmployerId,
                100));
            agent.Standing.SetRecognised("status.glass.controller-access", true);
            agent.Beliefs.Add(Belief(
                "belief.glass.ilya.controller-log",
                ControllerLogPropositionId,
                BoundCloudId,
                WeatherOperatorEmployerId,
                confidence: 100,
                secrecy: 10,
                emotionalWeight: 50));
            return agent;
        }

        private static AgentState CreateSera()
        {
            AgentState agent = Agent(
                SeraAgentId,
                5,
                "Sera Vale",
                "employer.glass-canal-fishery",
                "household.glass.vale",
                health: 30,
                subsistence: 55,
                safety: 50,
                belonging: 35,
                autonomy: 60,
                institutionalTrust: 20,
                riskTolerance: 50,
                candour: 65,
                solidarity: 55,
                duty: 60,
                institutionalReliance: 80,
                canWork: true,
                canSeekAid: false,
                canAppeal: true,
                canGiveEvidence: false);
            agent.Commitments.Add(Commitment(
                "commitment.glass.sera.downstream",
                "glass-later-downstream",
                WatershedId,
                100));
            agent.Commitments.Add(Commitment(
                "commitment.glass.sera.employment",
                "employment",
                agent.EmployerId,
                100));
            agent.Standing.SetRecognised("status.glass.registered-water-user", true);
            agent.Standing.SetRecognised(PrimaryDispositionRecordedStatusId, false);
            agent.Standing.SetRecognised(InstitutionalStatusIds.AdverseDecision, false);
            agent.Beliefs.Add(Belief(
                "belief.glass.sera.second-plume",
                "proposition.glass.second-undissipated-plume",
                BoundCloudId,
                WeatherOperatorEmployerId,
                confidence: 90,
                secrecy: 0,
                emotionalWeight: 80));
            return agent;
        }

        private static AgentState CreateVey()
        {
            AgentState agent = Agent(
                VeyAgentId,
                6,
                "Vey Ankar",
                "employer.glass-municipal-treatment",
                "household.glass.ankar",
                health: 25,
                subsistence: 35,
                safety: 40,
                belonging: 30,
                autonomy: 30,
                institutionalTrust: 55,
                riskTolerance: 35,
                candour: 50,
                solidarity: 40,
                duty: 60,
                institutionalReliance: 70,
                canWork: false,
                canSeekAid: false,
                canAppeal: false,
                canGiveEvidence: false);
            agent.Commitments.Add(Commitment(
                "commitment.glass.vey.cartridge-holder",
                "glass-cartridge-holder",
                FilterResourceId,
                100));
            agent.Standing.SetRecognised(FilterEntitlementStatusId, true);
            return agent;
        }

        private static AgentState CreateToma()
        {
            AgentState agent = Agent(
                TomaAgentId,
                7,
                "Toma Rill",
                CanalAuthorityEmployerId,
                "household.glass.rill",
                health: 25,
                subsistence: 35,
                safety: 45,
                belonging: 40,
                autonomy: 45,
                institutionalTrust: 60,
                riskTolerance: 45,
                candour: 90,
                solidarity: 80,
                duty: 75,
                institutionalReliance: 70,
                canWork: false,
                canSeekAid: false,
                canAppeal: true,
                canGiveEvidence: true);
            agent.Commitments.Add(Commitment(
                "commitment.glass.toma.watershed",
                "glass-watershed-representative",
                WatershedId,
                100));
            agent.Standing.SetRecognised("status.glass.watershed-representation", true);
            agent.Standing.SetRecognised(InstitutionalStatusIds.AdverseDecision, false);
            agent.Beliefs.Add(Belief(
                "belief.glass.toma.drain-telemetry",
                DrainTelemetryPropositionId,
                BoundCloudId,
                CanalAuthorityEmployerId,
                confidence: 90,
                secrecy: 0,
                emotionalWeight: 60));
            return agent;
        }

        private static AgentState CreateBystander()
        {
            return Agent(
                BystanderAgentId,
                8,
                "Una Bell",
                "employer.glass-market",
                "household.glass.bell",
                health: 25,
                subsistence: 35,
                safety: 30,
                belonging: 35,
                autonomy: 30,
                institutionalTrust: 25,
                riskTolerance: 40,
                candour: 45,
                solidarity: 40,
                duty: 45,
                institutionalReliance: 35,
                canWork: false,
                canSeekAid: false,
                canAppeal: false,
                canGiveEvidence: false);
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

        private static BeliefState Belief(
            string beliefId,
            string propositionId,
            string subjectId,
            string objectId,
            int confidence,
            int secrecy,
            int emotionalWeight)
        {
            return new BeliefState
            {
                BeliefId = beliefId,
                PropositionId = propositionId,
                SubjectId = subjectId,
                ObjectId = objectId,
                SourceId = $"source.{beliefId}",
                Confidence = confidence,
                Secrecy = secrecy,
                EmotionalWeight = emotionalWeight,
                AcquiredTick = StartCycle,
            };
        }
    }
}
