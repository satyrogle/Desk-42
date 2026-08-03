using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Desk42.Institutional;
using Desk42.Institutional.Runtime;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalEndogenousPersistenceTests
    {
        [Test]
        public void SaveLoadAtEveryCommittedBoundary_ReproducesUninterruptedSnapshotByteForByte()
        {
            string directory = TemporaryDirectory();
            try
            {
                EndogenousRunSnapshot uninterrupted = RunFullChain(
                    reloadAtEveryBoundary: false,
                    Path.Combine(directory, "unused.json"));
                EndogenousRunSnapshot resumed = RunFullChain(
                    reloadAtEveryBoundary: true,
                    Path.Combine(directory, "active-chain.json"));

                Assert.AreEqual(
                    EndogenousRunSnapshotStore.SerializePayload(uninterrupted),
                    EndogenousRunSnapshotStore.SerializePayload(resumed));
                Assert.AreEqual(2, resumed.Docket.OpenCases.Count);
                Assert.AreEqual(1, resumed.Docket.Rulings.Count);
                Assert.AreEqual(1, resumed.Docket.ScopeApplicationTraces.Count,
                    "Reapplying the persisted scope phase must not duplicate precedent effects.");
                Assert.AreEqual("agent.connected",
                    resumed.MaterialWorld.GetResource("resource.later").PhysicalHolderId);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void Snapshot_CarriesCursorsExactOnceIdsAndExplicitInactiveQueues()
        {
            string directory = TemporaryDirectory();
            try
            {
                EndogenousRunSnapshot snapshot = RunFullChain(
                    reloadAtEveryBoundary: true,
                    Path.Combine(directory, "active-chain.json"));

                Assert.AreEqual(snapshot.Society.CurrentTick, snapshot.CurrentTick);
                Assert.AreEqual(snapshot.Society.EventLedger.Count,
                    snapshot.SocietyEventLedgerCursor);
                Assert.AreEqual(snapshot.MaterialWorld.EventLedger.Count,
                    snapshot.MaterialEventLedgerCursor);
                CollectionAssert.AreEqual(
                    snapshot.AppliedCommandIds.OrderBy(value => value, StringComparer.Ordinal),
                    snapshot.AppliedCommandIds);
                CollectionAssert.AreEqual(
                    snapshot.AppliedTransitionIds.OrderBy(value => value, StringComparer.Ordinal),
                    snapshot.AppliedTransitionIds);
                Assert.AreEqual(1, snapshot.AppliedCommandIds.Count);
                Assert.IsTrue(snapshot.AppliedTransitionIds.Any(value =>
                    value.StartsWith("scope:", StringComparison.Ordinal)));
                Assert.IsNotNull(snapshot.PendingAppealIds);
                Assert.IsNotNull(snapshot.RelianceEventIds);
                Assert.IsNotNull(snapshot.PendingPublicObservationIds);
                Assert.IsNotNull(snapshot.ExclusiveEntitlementIds);
                Assert.IsEmpty(snapshot.PendingAppealIds);
                Assert.IsEmpty(snapshot.RelianceEventIds);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void Store_UsesChecksumAndFallsBackToLastValidBackup()
        {
            string directory = TemporaryDirectory();
            string path = Path.Combine(directory, "snapshot.json");
            try
            {
                RunState state = BuildInitialState();
                EndogenousRunSnapshot first = EndogenousRunSnapshotService.Capture(
                    "snapshot.first",
                    EndogenousCommitPhase.TickCommitted,
                    state.Society,
                    state.World,
                    state.Docket);
                EndogenousRunSnapshotStore.Save(path, first);
                EndogenousRunSnapshot second = EndogenousRunSnapshotService.Capture(
                    "snapshot.second",
                    EndogenousCommitPhase.TickCommitted,
                    state.Society,
                    state.World,
                    state.Docket);
                EndogenousRunSnapshotStore.Save(path, second);
                File.WriteAllText(path, "{ corrupt primary }");

                EndogenousRunSnapshot recovered = EndogenousRunSnapshotStore.Load(path);

                Assert.AreEqual("snapshot.first", recovered.SnapshotId);
                Assert.IsTrue(File.Exists(path + ".bak"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void RestoredTransactions_RejectConflictsAndReplayWithoutDuplicates()
        {
            string directory = TemporaryDirectory();
            try
            {
                EndogenousRunSnapshot snapshot = RunFullChain(
                    reloadAtEveryBoundary: true,
                    Path.Combine(directory, "active-chain.json"));
                int materialCount = snapshot.MaterialWorld.EventLedger.Count;
                int incidentCount = snapshot.Docket.IncidentCandidates.Count;
                int observationCount = snapshot.Docket.Observations.Count;
                int docketCount = snapshot.Docket.DocketCandidates.Count;
                int caseCount = snapshot.Docket.OpenCases.Count;
                int rulingCount = snapshot.Docket.Rulings.Count;
                CommittedPlayerRuling ruling = snapshot.Docket.Rulings.Single();
                PlayerRulingCommand replayCommand = CommandFrom(ruling);

                CommittedPlayerRuling replay = EndogenousPlayerRulingService.Commit(
                    snapshot.Society, snapshot.Docket, replayCommand);
                EndogenousDocketPulse docketReplay =
                    EndogenousIncidentDocketPipeline.Process(
                        snapshot.MaterialWorld,
                        snapshot.Society,
                        snapshot.Docket);
                MaterialWorldEvent firstTransfer = snapshot.MaterialWorld.EventLedger[0];
                MaterialWorldEvent materialReplay =
                    InstitutionalMaterialWorldService.TransferPossession(
                        snapshot.MaterialWorld,
                        snapshot.Society,
                        new PossessionTransferRequest
                        {
                            EventId = firstTransfer.EventId,
                            CauseDecisionId = firstTransfer.CauseDecisionId,
                            Tick = firstTransfer.Tick,
                            ActorAgentId = firstTransfer.ActorAgentId,
                            ResourceId = firstTransfer.ResourceId,
                            ExpectedPhysicalHolderId = firstTransfer.PreviousPhysicalHolderId,
                            NewPhysicalHolderId = firstTransfer.NewPhysicalHolderId,
                            NewLocationContextId = firstTransfer.ContextId,
                            Visibility = firstTransfer.Visibility,
                            Secrecy = firstTransfer.Secrecy,
                            DirectWitnessAgentIds = new List<string>(
                                firstTransfer.DirectWitnessAgentIds),
                            PotentialRecordSourceIds = new List<string>(
                                firstTransfer.PotentialRecordSourceIds),
                            CauseEventIds = new List<string>(firstTransfer.CauseEventIds),
                        });

                Assert.AreSame(ruling, replay);
                Assert.AreSame(firstTransfer, materialReplay);
                Assert.IsEmpty(docketReplay.DetectedIncidents);
                Assert.IsEmpty(docketReplay.ProjectedObservations);
                Assert.IsEmpty(docketReplay.ComposedDocketCandidates);
                Assert.IsNull(docketReplay.AdmittedCase);
                Assert.AreEqual(materialCount, snapshot.MaterialWorld.EventLedger.Count);
                Assert.AreEqual(incidentCount, snapshot.Docket.IncidentCandidates.Count);
                Assert.AreEqual(observationCount, snapshot.Docket.Observations.Count);
                Assert.AreEqual(docketCount, snapshot.Docket.DocketCandidates.Count);
                Assert.AreEqual(caseCount, snapshot.Docket.OpenCases.Count);
                Assert.AreEqual(rulingCount, snapshot.Docket.Rulings.Count);

                PlayerRulingCommand conflict = CommandFrom(ruling);
                conflict.Scope.Value = "different-issue";
                Assert.Throws<InvalidOperationException>(() =>
                    EndogenousPlayerRulingService.Commit(
                        snapshot.Society, snapshot.Docket, conflict));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        private static EndogenousRunSnapshot RunFullChain(
            bool reloadAtEveryBoundary,
            string snapshotPath)
        {
            RunState state = BuildInitialState();
            int checkpoint = 0;
            SimulationInput initialInput = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(
                state.Society, state.World, initialInput);
            new EndogenousSocietyStepService().Advance(
                state.Society, state.World, initialInput);
            Checkpoint(state, EndogenousCommitPhase.TickCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);

            EndogenousIncidentDetector.Detect(state.World, state.Society, state.Docket);
            Checkpoint(state, EndogenousCommitPhase.IncidentCandidatesCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);
            EndogenousObservationProjector.Project(state.World, state.Society, state.Docket);
            Checkpoint(state, EndogenousCommitPhase.PublicObservationsCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);
            EndogenousDocketService.Compose(state.Society, state.Docket);
            Checkpoint(state, EndogenousCommitPhase.DocketCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);
            EndogenousInstitutionalCase initialCase = EndogenousDocketService.AdmitNext(
                state.Society, state.Docket);
            Assert.IsNotNull(initialCase);
            string initialCaseId = initialCase.CaseId;
            Checkpoint(state, EndogenousCommitPhase.CaseOpened,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);

            EndogenousPlayerRulingService.Commit(
                state.Society,
                state.Docket,
                BroadRulingCommand(state.Docket.GetCase(initialCaseId)));
            Checkpoint(state, EndogenousCommitPhase.RulingCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);

            SimulationInput laterInput = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(
                state.Society, state.World, laterInput);
            EndogenousScopeEffectService.Apply(
                state.Society, state.Docket, laterInput);
            Checkpoint(state, EndogenousCommitPhase.ScopeEffectsCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);

            // Pending inputs are rebuilt from committed state after restore. Replaying
            // scope effects must rebind lineage without duplicating the transition.
            laterInput = QuietInput();
            EndogenousActionOpportunityBuilder.Populate(
                state.Society, state.World, laterInput);
            EndogenousScopeEffectService.Apply(
                state.Society, state.Docket, laterInput);
            new EndogenousSocietyStepService().Advance(
                state.Society, state.World, laterInput);
            Checkpoint(state, EndogenousCommitPhase.TickCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);

            EndogenousIncidentDetector.Detect(state.World, state.Society, state.Docket);
            Checkpoint(state, EndogenousCommitPhase.IncidentCandidatesCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);
            EndogenousObservationProjector.Project(state.World, state.Society, state.Docket);
            Checkpoint(state, EndogenousCommitPhase.PublicObservationsCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);
            EndogenousDocketService.Compose(state.Society, state.Docket);
            Checkpoint(state, EndogenousCommitPhase.DocketCommitted,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);
            EndogenousInstitutionalCase descendant = EndogenousDocketService.AdmitNext(
                state.Society, state.Docket);
            Assert.IsNotNull(descendant);
            Assert.AreEqual(initialCaseId, descendant.ParentCaseId);
            Checkpoint(state, EndogenousCommitPhase.CaseOpened,
                reloadAtEveryBoundary, snapshotPath, ref checkpoint);

            return EndogenousRunSnapshotService.Capture(
                "snapshot.final",
                EndogenousCommitPhase.CaseOpened,
                state.Society,
                state.World,
                state.Docket);
        }

        private static void Checkpoint(
            RunState state,
            EndogenousCommitPhase phase,
            bool reload,
            string path,
            ref int ordinal)
        {
            EndogenousRunSnapshot snapshot = EndogenousRunSnapshotService.Capture(
                $"snapshot.boundary.{ordinal++}",
                phase,
                state.Society,
                state.World,
                state.Docket);
            if (!reload) return;
            EndogenousRunSnapshotStore.Save(path, snapshot);
            EndogenousRunSnapshot loaded = EndogenousRunSnapshotStore.Load(path);
            state.Society = loaded.Society;
            state.World = loaded.MaterialWorld;
            state.Docket = loaded.Docket;
        }

        private static RunState BuildInitialState()
        {
            AgentState origin = Agent("agent.origin", 0);
            origin.GetNeed(NeedKind.Health).Pressure = 100;
            origin.Disposition.RiskTolerance = 100;
            AgentState connected = Agent("agent.connected", 1);
            connected.GetNeed(NeedKind.Health).Pressure = 70;
            connected.Disposition.Duty = 100;
            connected.InstitutionalTrust = 100;
            AgentState recorder = Agent("agent.recorder", 2);
            SocietyState society = Society(origin, connected, recorder);
            var world = new InstitutionalMaterialWorld();
            AddResource(world, "resource.initial", "agent.origin", "record.camera.initial");
            AddResource(world, "resource.later", "agent.connected", "record.camera.later");
            return new RunState
            {
                Society = society,
                World = world,
                Docket = new EndogenousDocketState(),
            };
        }

        private static PlayerRulingCommand BroadRulingCommand(
            EndogenousInstitutionalCase opened)
        {
            return new PlayerRulingCommand
            {
                CommandId = "command.scope.broad",
                CaseId = opened.CaseId,
                ExpectedCaseVersion = opened.CaseVersion,
                EvidenceEnvelopeHash = opened.EvidenceEnvelopeHash,
                RecognisedFactIds = new List<string> { opened.AvailableFactIds[0] },
                CitedEvidenceArtifactIds = new List<string> { opened.ObservationIds[0] },
                Disposition = RulingDisposition.Recognised,
                HoldingRuleId = EndogenousPlayerRulingService.PossessionHoldingRule,
                Scope = new ScopeExpression
                {
                    Kind = ScopeExpressionKind.Predicate,
                    PredicateKind = ScopePredicateKind.IssueEquals,
                    Value = EndogenousIssueKindIds.PossessionDispute,
                },
                TemporalReach = TemporalReach.Prospective,
                RemedyDefinitionIds = new List<string>
                {
                    EndogenousPlayerRulingService.RestorePossessionRemedy,
                },
            };
        }

        private static PlayerRulingCommand CommandFrom(CommittedPlayerRuling ruling)
        {
            return new PlayerRulingCommand
            {
                CommandId = ruling.PlayerCommandId,
                CaseId = ruling.CaseId,
                ExpectedCaseVersion = ruling.CaseVersion,
                EvidenceEnvelopeHash = ruling.EvidenceEnvelopeHash,
                RecognisedFactIds = new List<string>(ruling.RecognisedFactIds),
                CitedEvidenceArtifactIds = new List<string>(
                    ruling.CitedEvidenceArtifactIds),
                Disposition = ruling.Disposition,
                HoldingRuleId = ruling.HoldingRuleId,
                Scope = ScopeExpressionEvaluator.Copy(ruling.Scope),
                TemporalReach = ruling.TemporalReach,
                RemedyDefinitionIds = new List<string>(ruling.RemedyDefinitionIds),
            };
        }

        private static void AddResource(
            InstitutionalMaterialWorld world,
            string resourceId,
            string actorId,
            string recordSourceId)
        {
            world.Resources.Add(new MaterialResourceState
            {
                ResourceId = resourceId,
                ResourceKindId = "medicine",
                Quantity = 1,
                PhysicalHolderId = "clinic",
                LocationContextId = "clinic.store",
            });
            world.OfficialOwnerships.Add(new OfficialOwnershipState
            {
                OwnershipRecordId = $"ownership:{resourceId}",
                ResourceId = resourceId,
                RegisteredOwnerId = "clinic",
                OwnershipSourceId = "record.inventory",
                RecognitionTick = 0,
            });
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = $"access:{actorId}:{resourceId}",
                AgentId = actorId,
                AccessKindId = EndogenousActionOpportunityBuilder.MaterialPossessionAccessKind,
                TargetId = resourceId,
                SourceRecordId = "record.shift",
                ValidFromTick = 0,
            });
            world.AccessGrants.Add(new MaterialAccessGrantState
            {
                GrantId = $"recording:{resourceId}",
                AgentId = "agent.recorder",
                AccessKindId = EndogenousActionOpportunityBuilder.RecordingAccessKind,
                TargetId = resourceId,
                SourceRecordId = recordSourceId,
                ValidFromTick = 0,
            });
        }

        private static SocietyState Society(params AgentState[] agents)
        {
            return new SocietyState
            {
                MasterSeed = 424242,
                Regime = new InstitutionalRegimeState
                {
                    WorkReward = 0,
                    AidEffectiveness = 0,
                    DisclosureProtection = 0,
                    RetaliationRisk = 0,
                    AppealAccessibility = 0,
                    DecisionVariationAmplitude = 0,
                },
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

        private static SimulationInput QuietInput()
        {
            return new SimulationInput
            {
                IncidentId = "endogenous-persistence-pulse",
                WorkAvailable = false,
                AidAvailable = false,
                DisclosureRequested = false,
                AppealWindowOpen = false,
            };
        }

        private static string TemporaryDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "desk42-endogenous-persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class RunState
        {
            internal SocietyState Society;
            internal InstitutionalMaterialWorld World;
            internal EndogenousDocketState Docket;
        }
    }
}
