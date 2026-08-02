using System;

namespace Desk42.Institutional
{
    /// <summary>
    /// The first eight-person micro-society. These records contain authored starting
    /// conditions only; all subsequent choices use the shared decision pipeline.
    /// </summary>
    public static class PrototypePopulationFactory
    {
        public const int PrototypePopulationSize = 8;

        public static SocietyState Create(int masterSeed)
        {
            var state = new SocietyState
            {
                MasterSeed = masterSeed,
                CurrentTick = 0,
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 54,
                    AidEffectiveness = 48,
                    DisclosureProtection = 36,
                    RetaliationRisk = 62,
                    AppealAccessibility = 44,
                },
            };

            AgentState elias = Agent(
                "agent.elias-venn", "portrait.elias-venn", "Elias Venn", "species.shedborn",
                "household.venn-ollo", "employer.nadir-reclamation", 18,
                38, 72, 66, 42, 78,
                45, 52, 64, 48, 61);
            elias.Commitments.Add(Commitment("commitment.elias-ollo", "dependant", "agent.ollo-seven", 88));
            elias.Commitments.Add(Commitment("commitment.elias-work", "employment", elias.EmployerId, 76));
            elias.Relationships.Add(Relationship("agent.mara-kest", 64, 12, 73, 8, 68));
            elias.Relationships.Add(Relationship("agent.ollo-seven", 82, 8, 91, 4, 94));
            elias.Relationships.Add(Relationship("agent.sera-vale", 46, 16, 33, 12, 29));
            elias.Beliefs.Add(Belief(
                "belief.elias.badge-replaced", "identity.badge-was-replaced", elias.StableId,
                "employer.nadir-reclamation", "memory.personal", 82, 42, 78));
            elias.Standing.SetRecognised("identity-continuity", false);
            elias.Standing.SetRecognised("adverse-decision", true);
            elias.AnomalyRules.Add(Anomaly(
                "anomaly.superseded-body", "identity-continuity", NeedKind.Health, -1, 4,
                "observable.elias-phase-instability"));

            AgentState mara = Agent(
                "agent.mara-kest", "portrait.mara-kest", "Mara Kest", "species.baseline",
                "household.kest", "employer.nadir-reclamation", 31,
                24, 55, 71, 57, 49,
                34, 63, 55, 69, 42);
            mara.Commitments.Add(Commitment("commitment.mara-work", "employment", mara.EmployerId, 84));
            mara.Commitments.Add(Commitment("commitment.mara-elias", "promise", "agent.elias-venn", 71));
            mara.Relationships.Add(Relationship("agent.elias-venn", 72, 18, 78, 6, 61));
            mara.Relationships.Add(Relationship("agent.nara-quill", 28, 73, 18, 82, 12));
            mara.Relationships.Add(Relationship("agent.sera-vale", 58, 22, 39, 10, 34));
            mara.Beliefs.Add(Belief(
                "belief.mara.dual-roster", "records.two-active-rosters", "agent.elias-venn",
                mara.EmployerId, "record.shift-roster-copy", 91, 79, 84));
            mara.Beliefs.Add(Belief(
                "belief.mara.supervisor-warning", "supervisor.warned-against-disclosure", "agent.nara-quill",
                mara.StableId, "memory.conversation", 76, 88, 72));
            mara.Standing.SetRecognised("records-access", true);

            AgentState nara = Agent(
                "agent.nara-quill", "portrait.nara-quill", "Nara Quill", "species.vesper",
                "household.quill", "employer.nadir-reclamation", 43,
                31, 44, 63, 35, 39,
                58, 36, 27, 86, 54);
            nara.Commitments.Add(Commitment("commitment.nara-work", "employment", nara.EmployerId, 92));
            nara.Commitments.Add(Commitment("commitment.nara-safety", "duty", "employer.nadir-reclamation", 67));
            nara.Relationships.Add(Relationship("agent.mara-kest", 36, 21, 14, 76, 18));
            nara.Relationships.Add(Relationship("agent.elias-venn", 19, 46, 8, 69, 6));
            nara.Relationships.Add(Relationship("agent.imri-pell", 41, 18, 22, 71, 11));
            nara.Beliefs.Add(Belief(
                "belief.nara.roster-authorised", "records.replacement-was-authorised", "agent.elias-venn",
                nara.EmployerId, "record.management-circular", 73, 61, 55));
            nara.Standing.SetRecognised("management-authority", true);

            AgentState ollo = Agent(
                "agent.ollo-seven", "portrait.ollo-seven", "Ollo Seven", "species.echo-colony",
                "household.venn-ollo", null, 11,
                69, 81, 77, 86, 58,
                22, 38, 71, 32, 29);
            ollo.Standing.CanWork = false;
            ollo.Commitments.Add(Commitment("commitment.ollo-elias", "household", "agent.elias-venn", 95));
            ollo.Relationships.Add(Relationship("agent.elias-venn", 86, 12, 84, 33, 97));
            ollo.Relationships.Add(Relationship("agent.khet-daro", 61, 27, 43, 48, 35));
            ollo.Beliefs.Add(Belief(
                "belief.ollo.shared-name", "household.shared-name-is-continuous", ollo.StableId,
                "agent.elias-venn", "memory.echo", 88, 36, 93));
            ollo.Standing.SetRecognised("household-member", false);
            ollo.AnomalyRules.Add(Anomaly(
                "anomaly.echo-household", "household-member", NeedKind.Belonging, -1, 4,
                "observable.ollo-voice-fragmentation"));

            AgentState sera = Agent(
                "agent.sera-vale", "portrait.sera-vale", "Sera Vale", "species.baseline",
                "household.vale", "organisation.dockworkers-circle", 54,
                29, 48, 51, 63, 72,
                68, 79, 88, 74, 57);
            sera.Commitments.Add(Commitment("commitment.sera-members", "duty", "organisation.dockworkers-circle", 94));
            sera.Commitments.Add(Commitment("commitment.sera-work", "employment", sera.EmployerId, 48));
            sera.Relationships.Add(Relationship("agent.elias-venn", 57, 9, 64, 7, 41));
            sera.Relationships.Add(Relationship("agent.mara-kest", 66, 14, 56, 5, 39));
            sera.Relationships.Add(Relationship("agent.imri-pell", 74, 8, 72, 6, 53));
            sera.Beliefs.Add(Belief(
                "belief.sera.pattern", "employer.uses-identity-replacement-to-avoid-obligations",
                "employer.nadir-reclamation", "organisation.dockworkers-circle",
                "testimony.multiple-workers", 67, 31, 81));
            sera.Standing.SetRecognised("worker-representative", true);

            AgentState khet = Agent(
                "agent.khet-daro", "portrait.khet-daro", "Khet Daro", "species.chitinous",
                "household.daro", "clinic.meridian", 62,
                33, 39, 46, 37, 41,
                51, 71, 62, 81, 73);
            khet.Commitments.Add(Commitment("commitment.khet-clinic", "employment", khet.EmployerId, 66));
            khet.Commitments.Add(Commitment("commitment.khet-patients", "duty", "clinic.meridian", 91));
            khet.Relationships.Add(Relationship("agent.ollo-seven", 63, 11, 72, 38, 32));
            khet.Relationships.Add(Relationship("agent.elias-venn", 48, 9, 61, 42, 25));
            khet.Beliefs.Add(Belief(
                "belief.khet.discontinuity-harm", "identity-discontinuity-correlates-with-physical-harm",
                "agent.elias-venn", "clinic.meridian", "record.clinical-observation", 79, 52, 66));
            khet.Standing.SetRecognised("licensed-assessor", true);

            AgentState imri = Agent(
                "agent.imri-pell", "portrait.imri-pell", "Imri Pell", "species.glass-blooded",
                "household.pell", "employer.nadir-reclamation", 7,
                52, 84, 69, 73, 88,
                39, 44, 58, 35, 24);
            imri.Commitments.Add(Commitment("commitment.imri-work", "employment", imri.EmployerId, 89));
            imri.Relationships.Add(Relationship("agent.sera-vale", 71, 15, 68, 9, 48));
            imri.Relationships.Add(Relationship("agent.nara-quill", 17, 81, 9, 88, 7));
            imri.Relationships.Add(Relationship("agent.mara-kest", 49, 24, 32, 16, 27));
            imri.Beliefs.Add(Belief(
                "belief.imri.precedent-threat", "appeal.may-endanger-current-employment", imri.StableId,
                "agent.nara-quill", "testimony.supervisor", 64, 83, 76));
            imri.Standing.SetRecognised("adverse-decision", true);

            AgentState vey = Agent(
                "agent.vey-ankar", "portrait.vey-ankar", "Vey Ankar", "species.contract-shadow",
                "household.ankar", "agency.temporary-forms", 38,
                46, 67, 74, 54, 62,
                57, 49, 43, 63, 46);
            vey.Commitments.Add(Commitment("commitment.vey-work", "employment", vey.EmployerId, 82));
            vey.Commitments.Add(Commitment("commitment.vey-mara", "promise", "agent.mara-kest", 41));
            vey.Relationships.Add(Relationship("agent.mara-kest", 55, 20, 49, 12, 31));
            vey.Relationships.Add(Relationship("agent.imri-pell", 52, 18, 37, 8, 34));
            vey.Beliefs.Add(Belief(
                "belief.vey.badge-shadow", "employment-badge-controls-shadow-cohesion", vey.StableId,
                vey.EmployerId, "memory.embodied", 93, 47, 88));
            vey.Standing.SetRecognised("employment-authorisation", true);
            vey.AnomalyRules.Add(Anomaly(
                "anomaly.contract-shadow", "employment-authorisation", NeedKind.Safety, -1, 4,
                "observable.vey-shadow-cohesion"));

            state.Agents.Add(elias);
            state.Agents.Add(mara);
            state.Agents.Add(nara);
            state.Agents.Add(ollo);
            state.Agents.Add(sera);
            state.Agents.Add(khet);
            state.Agents.Add(imri);
            state.Agents.Add(vey);
            for (int i = 0; i < state.Agents.Count; i++)
                state.Agents[i].SimulationOrdinal = i;
            return state;
        }

        private static AgentState Agent(
            string id,
            string presentationId,
            string displayName,
            string speciesId,
            string householdId,
            string employerId,
            int institutionalTrust,
            int health,
            int subsistence,
            int safety,
            int belonging,
            int autonomy,
            int riskTolerance,
            int candour,
            int solidarity,
            int duty,
            int institutionalReliance)
        {
            var agent = new AgentState
            {
                StableId = id,
                PresentationId = presentationId,
                DisplayName = displayName,
                SpeciesId = speciesId,
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
            };
            agent.Needs.Add(new NeedState { Kind = NeedKind.Health, Pressure = health });
            agent.Needs.Add(new NeedState { Kind = NeedKind.Subsistence, Pressure = subsistence });
            agent.Needs.Add(new NeedState { Kind = NeedKind.Safety, Pressure = safety });
            agent.Needs.Add(new NeedState { Kind = NeedKind.Belonging, Pressure = belonging });
            agent.Needs.Add(new NeedState { Kind = NeedKind.Autonomy, Pressure = autonomy });
            return agent;
        }

        private static CommitmentState Commitment(string id, string kind, string targetId, int strength)
        {
            return new CommitmentState
            {
                CommitmentId = id,
                Kind = kind,
                TargetId = targetId,
                Strength = strength,
            };
        }

        private static RelationshipState Relationship(
            string targetId,
            int trust,
            int fear,
            int obligation,
            int authority,
            int attachment,
            NeedKind perceivedNeed = NeedKind.Safety,
            int perceivedNeedPressure = 50)
        {
            return new RelationshipState
            {
                TargetAgentId = targetId,
                Trust = trust,
                Fear = fear,
                Obligation = obligation,
                Authority = authority,
                Attachment = attachment,
                PerceivedNeed = perceivedNeed,
                PerceivedNeedPressure = perceivedNeedPressure,
                PerceivedNeedObservedTick = 0,
            };
        }

        private static BeliefState Belief(
            string id,
            string propositionId,
            string subjectId,
            string objectId,
            string sourceId,
            int confidence,
            int secrecy,
            int emotionalWeight)
        {
            return new BeliefState
            {
                BeliefId = id,
                PropositionId = propositionId,
                SubjectId = subjectId,
                ObjectId = objectId,
                SourceId = sourceId,
                Confidence = confidence,
                Secrecy = secrecy,
                EmotionalWeight = emotionalWeight,
                AcquiredTick = 0,
                EnteredOfficialRecord = false,
                Disclosed = false,
            };
        }

        private static AnomalyStatusRule Anomaly(
            string traitId,
            string requiredStatusId,
            NeedKind affectedNeed,
            int recognisedDelta,
            int unrecognisedDelta,
            string observableEffectId)
        {
            if (string.IsNullOrWhiteSpace(requiredStatusId))
                throw new ArgumentException("An anomaly rule requires an official status id.", nameof(requiredStatusId));

            return new AnomalyStatusRule
            {
                TraitId = traitId,
                RequiredOfficialStatusId = requiredStatusId,
                AffectedNeed = affectedNeed,
                RecognisedPressureDelta = recognisedDelta,
                UnrecognisedPressureDelta = unrecognisedDelta,
                MinimumTicksBetweenActivations = 3,
                LastAppliedTick = -1,
                ObservableEffectId = observableEffectId,
            };
        }
    }
}
