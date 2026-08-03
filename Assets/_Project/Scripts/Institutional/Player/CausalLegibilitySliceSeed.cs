using System;
using System.Collections.Generic;

namespace Desk42.Institutional.Player
{
    /// <summary>
    /// One deterministic eight-person world seed. It establishes pressures, access
    /// and records, never selected actions or a plot calendar.
    /// </summary>
    internal static class CausalLegibilitySliceSeed
    {
        internal const int MasterSeed = 420042;
        internal const string InitialResourceId = "resource.medicine-a";
        internal const string LaterResourceId = "resource.medicine-b";
        internal const string OriginAgentId = "agent.kira-dax";
        internal const string ConnectedAgentId = "agent.mara-venn";
        internal const string RecorderAgentId = "agent.c9-records";

        internal static EndogenousRunSnapshot CreatePreRulingSnapshot()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld world = CreateWorld();
            SimulationInput input = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(society, world, input);
            new EndogenousSocietyStepService().Advance(society, world, input);

            var docket = new EndogenousDocketState { DirectorEnabled = false };
            EndogenousDocketPulse pulse = EndogenousIncidentDocketPipeline.Process(
                world, society, docket);
            if (pulse.AdmittedCase == null || docket.OpenCases.Count != 1)
            {
                throw new InvalidOperationException(
                    "The causal-legibility seed did not produce exactly one visible case.");
            }
            if (docket.Observations.Count == 0)
                throw new InvalidOperationException(
                    "The causal-legibility case has no institutionally visible record.");

            return EndogenousRunSnapshotService.Capture(
                "causal-legibility.pre-ruling",
                EndogenousCommitPhase.CaseOpened,
                society,
                world,
                docket);
        }

        /// <summary>
        /// Creates the continuing society used by the automation product. The seed
        /// establishes a bounded stock of shared material pressures and observable
        /// access. It does not select actions or author cases: the generic decision,
        /// material-event and docket pipeline still produces every dossier.
        /// </summary>
        internal static EndogenousRunSnapshot CreatePersistentAutomationSnapshot()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld world = new InstitutionalMaterialWorld();

            for (int agentIndex = 0; agentIndex < society.Agents.Count; agentIndex++)
            {
                AgentState agent = society.Agents[agentIndex];
                agent.GetNeed(NeedKind.Health).Pressure = 100;
                agent.Disposition.RiskTolerance = 100;
                agent.AnomalyRules.Add(CreateOperationalPressureRule(
                    agent, "metabolic-cycle", "observable.metabolic-pressure"));
                agent.AnomalyRules.Add(CreateOperationalPressureRule(
                    agent, "phase-instability", "observable.phase-pressure"));
            }

            // The production run can adjudicate ninety-six dossiers. Keep a bounded
            // but comfortably larger stock so one-accessor material opportunities do
            // not need to recycle ownership merely to sustain eight shifts.
            const int resourceCount = 128;
            for (int resourceIndex = 0; resourceIndex < resourceCount; resourceIndex++)
            {
                string resourceId =
                    "resource.branch42-supply-" + (resourceIndex + 1).ToString("D3");
                string recordPrefix = resourceIndex % 3 == 0
                    ? "record.camera"
                    : resourceIndex % 3 == 1
                        ? "record.access-log"
                        : "record.damaged-sensor";
                AddSharedOperationalResource(
                    society,
                    world,
                    resourceId,
                    recordPrefix + "." + (resourceIndex + 1).ToString("D3"),
                    resourceIndex + 1,
                    ResourceKindForIndex(resourceIndex));
            }
            AddRetaliatoryAuthorityPressure(society, world);
            AddCollectiveGrievancePressure(society, world);

            var docket = new EndogenousDocketState { DirectorEnabled = false };
            AdvanceAutomationPulse(society, world, docket, "automation-origin");
            AdmitAllCases(society, docket);
            if (docket.OpenCases.Count == 0)
                throw new InvalidOperationException(
                    "The persistent automation seed produced no observable work.");

            return EndogenousRunSnapshotService.Capture(
                "persistent-automation.origin",
                EndogenousCommitPhase.CaseOpened,
                society,
                world,
                docket);
        }

        internal static void AdvanceAutomationPulse(
            SocietyState society,
            InstitutionalMaterialWorld world,
            EndogenousDocketState docket,
            string pulseId)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (docket == null) throw new ArgumentNullException(nameof(docket));
            SimulationInput input = QuietInput();
            input.IncidentId = string.IsNullOrWhiteSpace(pulseId)
                ? "persistent-automation-pulse"
                : pulseId;
            EndogenousActionOpportunityBuilder.Populate(society, world, input);
            new EndogenousSocietyStepService().Advance(society, world, input);
            EndogenousIncidentDocketPipeline.ProcessWithinValidatedTransaction(
                world, society, docket, admitOneCase: false);
        }

        internal static void AdmitAllCases(
            SocietyState society,
            EndogenousDocketState docket)
        {
            EndogenousDocketService.AdmitAllWithinValidatedTransaction(
                society, docket);
        }

        internal static SimulationInput QuietInput()
        {
            return new SimulationInput
            {
                IncidentId = "causal-legibility-pulse",
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
            };
        }

        private static SocietyState CreateSociety()
        {
            AgentState origin = Agent(
                OriginAgentId, 0, "Kira Dax", "species.shedborn", "household.dax");
            origin.GetNeed(NeedKind.Health).Pressure = 100;
            origin.Disposition.RiskTolerance = 100;

            AgentState connected = Agent(
                ConnectedAgentId, 1, "Mara Venn", "species.baseline", "household.venn");
            connected.GetNeed(NeedKind.Health).Pressure = 70;
            connected.Disposition.Duty = 100;
            connected.InstitutionalTrust = 100;

            AgentState recorder = Agent(
                RecorderAgentId, 2, "C-9 Records", "species.clerical-synthetic",
                "household.registered-unit");
            AgentState ollo = Agent(
                "agent.ollo-seven", 3, "Ollo Seven", "species.echo-bodied",
                "household.seven");
            AgentState sera = Agent(
                "agent.sera-vale", 4, "Sera Vale", "species.baseline",
                "household.vale");
            AgentState nara = Agent(
                "agent.nara-quill", 5, "Nara Quill", "species.glasskin",
                "household.quill");
            AgentState imri = Agent(
                "agent.imri-pell", 6, "Imri Pell", "species.baseline",
                "household.pell");
            AgentState vey = Agent(
                "agent.vey-sable", 7, "Vey Sable", "species.phase-adjacent",
                "household.sable");

            return new SocietyState
            {
                MasterSeed = MasterSeed,
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 0,
                    AidEffectiveness = 0,
                    DisclosureProtection = 0,
                    RetaliationRisk = 0,
                    AppealAccessibility = 0,
                    DecisionVariationAmplitude = 0,
                },
                Agents = new List<AgentState>
                {
                    origin, connected, recorder, ollo, sera, nara, imri, vey,
                },
            };
        }

        private static AgentState Agent(
            string id,
            int ordinal,
            string displayName,
            string speciesId,
            string householdId)
        {
            var agent = new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = $"silhouette.{ordinal + 1}",
                DisplayName = displayName,
                SpeciesId = speciesId,
                HouseholdId = householdId,
                EmployerId = "employer.meridian-clinic",
                InstitutionalTrust = 50,
                Disposition = new AgentDispositionState(),
                Standing = new InstitutionalStandingState(),
            };
            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
                agent.Needs.Add(new NeedState { Kind = kind, Pressure = 0 });
            agent.Standing.SetRecognised("status.identity-recognised", true);
            agent.Standing.SetRecognised("status.employment-recognised", true);
            return agent;
        }

        private static InstitutionalMaterialWorld CreateWorld()
        {
            var world = new InstitutionalMaterialWorld();
            AddMedicine(
                world,
                InitialResourceId,
                OriginAgentId,
                "record.camera.medicine-a");
            AddMedicine(
                world,
                LaterResourceId,
                ConnectedAgentId,
                "record.camera.medicine-b");
            return world;
        }

        private static void AddMedicine(
            InstitutionalMaterialWorld world,
            string resourceId,
            string accessAgentId,
            string cameraRecordId)
        {
            world.Resources.Add(new MaterialResourceState
            {
                ResourceId = resourceId,
                ResourceKindId = "medicine",
                Quantity = 1,
                PhysicalHolderId = "clinic",
                LocationContextId = "clinic.secure-store",
            });
            world.OfficialOwnerships.Add(new OfficialOwnershipState
            {
                OwnershipRecordId = $"ownership:{resourceId}",
                ResourceId = resourceId,
                RegisteredOwnerId = "clinic",
                OwnershipSourceId = "record.clinic-inventory",
                RecognitionTick = 0,
            });
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = $"access:{accessAgentId}:{resourceId}",
                AgentId = accessAgentId,
                AccessKindId =
                    EndogenousActionOpportunityBuilder.MaterialPossessionAccessKind,
                TargetId = resourceId,
                SourceRecordId = "record.clinic-shift",
                ValidFromTick = 0,
            });
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = $"recording:{resourceId}",
                AgentId = RecorderAgentId,
                AccessKindId = EndogenousActionOpportunityBuilder.RecordingAccessKind,
                TargetId = resourceId,
                SourceRecordId = cameraRecordId,
                ValidFromTick = 0,
            });
        }

        private static AnomalyStatusRule CreateOperationalPressureRule(
            AgentState agent,
            string suffix,
            string observableEffectId)
        {
            return new AnomalyStatusRule
            {
                TraitId = "trait." + suffix + "." + agent.StableId,
                RequiredOfficialStatusId = "status.identity-recognised",
                AffectedNeed = NeedKind.Health,
                RecognisedPressureDelta = 10,
                UnrecognisedPressureDelta = 10,
                MinimumTicksBetweenActivations = 1,
                LastAppliedTick = -1,
                ObservableEffectId = observableEffectId + "." + agent.StableId,
            };
        }

        private static void AddSharedOperationalResource(
            SocietyState society,
            InstitutionalMaterialWorld world,
            string resourceId,
            string recordSourceId,
            int resourceOrdinal,
            string resourceKindId = "regulated-medical-supply")
        {
            world.Resources.Add(new MaterialResourceState
            {
                ResourceId = resourceId,
                ResourceKindId = resourceKindId,
                Quantity = 1,
                PhysicalHolderId = "clinic",
                LocationContextId = "clinic.secure-store",
            });
            world.OfficialOwnerships.Add(new OfficialOwnershipState
            {
                OwnershipRecordId = "ownership:" + resourceId,
                ResourceId = resourceId,
                RegisteredOwnerId = "clinic",
                OwnershipSourceId = "record.clinic-inventory",
                RecognitionTick = 0,
            });
            AgentState assigned = society.Agents[
                (resourceOrdinal - 1) % society.Agents.Count];
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = "access:" + assigned.StableId + ":" + resourceId,
                AgentId = assigned.StableId,
                AccessKindId =
                    EndogenousActionOpportunityBuilder.MaterialPossessionAccessKind,
                TargetId = resourceId,
                SourceRecordId = "record.branch42-shift-roster",
                ValidFromTick = 0,
            });
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = "recording:" + resourceId,
                AgentId = RecorderAgentId,
                AccessKindId = EndogenousActionOpportunityBuilder.RecordingAccessKind,
                TargetId = resourceId,
                SourceRecordId = recordSourceId,
                ValidFromTick = 0,
            });
        }

        private static string ResourceKindForIndex(int resourceIndex)
        {
            int cycleIndex = resourceIndex % 24;
            if (cycleIndex >= 16 && cycleIndex < 20)
                return "identity-continuity-record";
            if (cycleIndex >= 20)
                return "dependency-emergency-support";
            return "regulated-medical-supply";
        }

        private static void AddRetaliatoryAuthorityPressure(
            SocietyState society,
            InstitutionalMaterialWorld world)
        {
            AgentState supervisor = society.GetAgent(RecorderAgentId);
            AgentState target = society.GetAgent("agent.ollo-seven");
            if (supervisor == null || target == null)
                throw new InvalidOperationException(
                    "The persistent authority pressure requires its generic profiles.");
            supervisor.Disposition.Duty = 100;
            supervisor.Relationships.Add(new RelationshipState
            {
                TargetAgentId = target.StableId,
                Fear = 60,
            });
            target.Relationships.Add(new RelationshipState
            {
                TargetAgentId = supervisor.StableId,
                Trust = 65,
            });
            supervisor.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.branch42-adverse-action",
                PropositionId =
                    EndogenousActionOpportunityBuilder.PerceivedAdverseActionProposition,
                SubjectId = target.StableId,
                ObjectId = "resource.branch42-roster",
                SourceId = "observation.supervisory-report",
                Confidence = 100,
                Secrecy = 30,
                EmotionalWeight = 100,
            });
            world.AuthorityGrants.Add(new MaterialAuthorityGrantState
            {
                GrantId = "authority.branch42.remove-ollo-access",
                AgentId = supervisor.StableId,
                Kind = MaterialAuthorityKind.RemoveAccess,
                TargetId = target.StableId,
                SourceRecordId = "record.authority.branch42-supervision",
                ValidFromTick = 0,
            });
        }

        private static void AddCollectiveGrievancePressure(
            SocietyState society,
            InstitutionalMaterialWorld world)
        {
            const string issueId = "workplace.shared-access-withdrawal";
            string[] memberIds = { "agent.ollo-seven", "agent.sera-vale" };
            for (int i = 0; i < memberIds.Length; i++)
            {
                AgentState member = society.GetAgent(memberIds[i]) ??
                    throw new InvalidOperationException(
                        "The collective product seed requires both generic members.");
                member.Disposition.Solidarity = 100;
                member.Disposition.RiskTolerance = 100;
                member.GetNeed(NeedKind.Belonging).Pressure = 100;
                member.GetNeed(NeedKind.Autonomy).Pressure = 100;
                member.Commitments.Add(new CommitmentState
                {
                    CommitmentId = "commitment.collective:" + member.StableId,
                    Kind = "grievance",
                    TargetId = issueId,
                    Strength = 100,
                });
                world.AccessGrants.Add(new MaterialAccessGrantState
                {
                    GrantId = "communication.collective:" + member.StableId,
                    AgentId = member.StableId,
                    AccessKindId =
                        EndogenousActionOpportunityBuilder.CommunicationAccessKind,
                    TargetId = "branch42.shared-channel",
                    SourceRecordId = "record.access-log.collective-channel",
                    ValidFromTick = 0,
                });
            }
        }
    }
}
