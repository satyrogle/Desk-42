using System;
using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalMaterialWorldTests
    {
        [Test]
        public void ValidWorld_SeparatesPhysicalPossessionFromOfficialOwnership()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld world = CreateWorld();

            InstitutionalMaterialWorldValidator.Validate(world, society);

            MaterialResourceState resource = world.GetResource("resource.medicine.001");
            OfficialOwnershipState ownership =
                world.GetOfficialOwnership("resource.medicine.001");
            Assert.That(resource.PhysicalHolderId, Is.EqualTo("organisation.clinic"));
            Assert.That(ownership.RegisteredOwnerId, Is.EqualTo("organisation.clinic"));
            Assert.That(resource.LocationContextId, Is.EqualTo("context.clinic-store"));
            Assert.That(ownership.OwnershipSourceId, Is.EqualTo("record.clinic-inventory"));
        }

        [Test]
        public void TransferPossession_ChangesPhysicalHolderOnlyAndCapturesObservability()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld world = CreateWorld();
            OfficialOwnershipState ownership =
                world.GetOfficialOwnership("resource.medicine.001");

            MaterialWorldEvent materialEvent =
                InstitutionalMaterialWorldService.TransferPossession(
                    world,
                    society,
                    TransferRequest());

            MaterialResourceState resource = world.GetResource("resource.medicine.001");
            Assert.That(resource.PhysicalHolderId, Is.EqualTo("agent.patient"));
            Assert.That(resource.LocationContextId, Is.EqualTo("context.clinic-corridor"));
            Assert.That(ownership.RegisteredOwnerId, Is.EqualTo("organisation.clinic"));
            Assert.That(ownership.Disputed, Is.False);
            Assert.That(materialEvent.Kind, Is.EqualTo(MaterialWorldEventKind.PossessionTransferred));
            Assert.That(materialEvent.PreviousPhysicalHolderId,
                Is.EqualTo("organisation.clinic"));
            Assert.That(materialEvent.NewPhysicalHolderId, Is.EqualTo("agent.patient"));
            Assert.That(materialEvent.Quantity, Is.EqualTo(1));
            Assert.That(materialEvent.DirectWitnessAgentIds,
                Is.EqualTo(new[] { "agent.witness" }));
            Assert.That(materialEvent.PotentialRecordSourceIds,
                Is.EqualTo(new[] { "record-source.clinic-camera" }));
            Assert.That(materialEvent.CauseEventIds,
                Is.EqualTo(new[] { "society.event.access-opened" }));
            Assert.That(world.EventLedger, Has.Count.EqualTo(1));
            InstitutionalMaterialWorldValidator.Validate(world, society);
        }

        [Test]
        public void TransferPossession_ReplayingSameEventIsIdempotent()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld world = CreateWorld();
            PossessionTransferRequest request = TransferRequest();

            MaterialWorldEvent first = InstitutionalMaterialWorldService.TransferPossession(
                world,
                society,
                request);
            MaterialWorldEvent replay = InstitutionalMaterialWorldService.TransferPossession(
                world,
                society,
                request);

            Assert.That(replay, Is.SameAs(first));
            Assert.That(world.EventLedger, Has.Count.EqualTo(1));
            Assert.That(world.GetResource(request.ResourceId).PhysicalHolderId,
                Is.EqualTo(request.NewPhysicalHolderId));
        }

        [Test]
        public void TransferPossession_ReusedEventIdWithDifferentPayloadIsRejected()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld world = CreateWorld();
            PossessionTransferRequest first = TransferRequest();
            InstitutionalMaterialWorldService.TransferPossession(world, society, first);

            PossessionTransferRequest conflict = TransferRequest();
            conflict.NewPhysicalHolderId = "agent.witness";

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalMaterialWorldService.TransferPossession(
                    world,
                    society,
                    conflict));
            Assert.That(world.EventLedger, Has.Count.EqualTo(1));
            Assert.That(world.GetResource(first.ResourceId).PhysicalHolderId,
                Is.EqualTo("agent.patient"));
        }

        [Test]
        public void Validator_AllowsPrunedSameTickSocietyCauseButRejectsFutureCause()
        {
            SocietyState society = CreateSociety();
            society.CurrentTick = 2;
            society.EventLedger.Clear();
            society.EventLedger.Add(new SocietyEvent
            {
                EventId = "event:2:agent.witness:WorkPerformed",
                Tick = 2,
                Kind = SocietyEventKind.WorkPerformed,
                ActorId = "agent.witness",
            });
            PossessionTransferRequest historical = TransferRequest();
            historical.CauseEventIds = new List<string>
            {
                "event:2:agent.patient:PossessionTransferRequested",
            };

            Assert.DoesNotThrow(() =>
                InstitutionalMaterialWorldService.TransferPossession(
                    CreateWorld(), society, historical));

            PossessionTransferRequest future = TransferRequest();
            future.CauseEventIds = new List<string>
            {
                "event:3:agent.patient:PossessionTransferRequested",
            };
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalMaterialWorldService.TransferPossession(
                    CreateWorld(), society, future));
        }

        [Test]
        public void PrivateTransfer_CanExistWithoutWitnessOrRecordSource()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld world = CreateWorld();
            PossessionTransferRequest request = TransferRequest();
            request.Visibility = MaterialEventVisibility.Private;
            request.Secrecy = 100;
            request.DirectWitnessAgentIds.Clear();
            request.PotentialRecordSourceIds.Clear();

            MaterialWorldEvent materialEvent =
                InstitutionalMaterialWorldService.TransferPossession(
                    world,
                    society,
                    request);

            Assert.That(materialEvent.Visibility, Is.EqualTo(MaterialEventVisibility.Private));
            Assert.That(materialEvent.DirectWitnessAgentIds, Is.Empty);
            Assert.That(materialEvent.PotentialRecordSourceIds, Is.Empty);
            Assert.That(world.GetOfficialOwnership(request.ResourceId).RegisteredOwnerId,
                Is.EqualTo("organisation.clinic"));
        }

        [Test]
        public void TransferPossession_UnknownWitnessIsRejectedWithoutMutation()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld world = CreateWorld();
            PossessionTransferRequest request = TransferRequest();
            request.DirectWitnessAgentIds[0] = "agent.unknown";

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalMaterialWorldService.TransferPossession(
                    world,
                    society,
                    request));
            Assert.That(world.GetResource(request.ResourceId).PhysicalHolderId,
                Is.EqualTo("organisation.clinic"));
            Assert.That(world.EventLedger, Is.Empty);
        }

        [Test]
        public void AccessAndAuthority_AreIndependentAndTimeBound()
        {
            InstitutionalMaterialWorld world = CreateWorld();

            Assert.That(InstitutionalMaterialWorldService.HasActiveAccess(
                world,
                "agent.patient",
                "physical-access",
                "resource.medicine.001",
                2), Is.True);
            Assert.That(InstitutionalMaterialWorldService.HasActiveAccess(
                world,
                "agent.supervisor",
                "physical-access",
                "resource.medicine.001",
                2), Is.False);
            Assert.That(InstitutionalMaterialWorldService.HasActiveAuthority(
                world,
                "agent.supervisor",
                MaterialAuthorityKind.RemoveAccess,
                "organisation.clinic",
                2), Is.True);
            Assert.That(InstitutionalMaterialWorldService.HasActiveAuthority(
                world,
                "agent.patient",
                MaterialAuthorityKind.RemoveAccess,
                "organisation.clinic",
                2), Is.False);

            world.AccessGrants[0].ValidUntilTick = 4;
            Assert.That(InstitutionalMaterialWorldService.HasActiveAccess(
                world,
                "agent.patient",
                "physical-access",
                "resource.medicine.001",
                5), Is.False);
        }

        [Test]
        public void CollectiveCommitment_RequiresMultipleMembersAndCausalActions()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld valid = CreateWorld(includeCollective: true);
            Assert.DoesNotThrow(() =>
                InstitutionalMaterialWorldValidator.Validate(valid, society));

            InstitutionalMaterialWorld oneMember =
                InstitutionalMaterialWorldDeepCopy.Copy(valid);
            oneMember.CollectiveCommitments[0].MemberAgentIds.RemoveAt(1);
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalMaterialWorldValidator.Validate(oneMember, society));

            InstitutionalMaterialWorld oneCause =
                InstitutionalMaterialWorldDeepCopy.Copy(valid);
            oneCause.CollectiveCommitments[0].FormationCauseEventIds.RemoveAt(1);
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalMaterialWorldValidator.Validate(oneCause, society));

            InstitutionalMaterialWorld unknownCause =
                InstitutionalMaterialWorldDeepCopy.Copy(valid);
            unknownCause.CollectiveCommitments[0].FormationCauseEventIds[1] =
                "material.event.not-recorded";
            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalMaterialWorldValidator.Validate(unknownCause, society));
        }

        [Test]
        public void Validator_RejectsOwnershipForUnknownResource()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld world = CreateWorld();
            world.OfficialOwnerships[0].ResourceId = "resource.missing";

            Assert.Throws<InvalidOperationException>(() =>
                InstitutionalMaterialWorldValidator.Validate(world, society));
        }

        [Test]
        public void DeepCopy_DetachesAllMaterialStateAndNestedObservability()
        {
            SocietyState society = CreateSociety();
            InstitutionalMaterialWorld source = CreateWorld(includeCollective: true);
            InstitutionalMaterialWorldService.TransferPossession(
                source,
                society,
                TransferRequest());

            InstitutionalMaterialWorld copy = InstitutionalMaterialWorldDeepCopy.Copy(source);
            source.Resources[0].PhysicalHolderId = "mutated.holder";
            source.OfficialOwnerships[0].RegisteredOwnerId = "mutated.owner";
            source.AccessGrants[0].Active = false;
            source.AuthorityGrants[0].Active = false;
            source.CollectiveCommitments[0].MemberAgentIds.Clear();
            source.CollectiveCommitments[0].FormationCauseEventIds.Clear();
            source.EventLedger[0].DirectWitnessAgentIds.Clear();
            source.EventLedger[0].PotentialRecordSourceIds.Clear();
            source.EventLedger[0].CauseEventIds.Clear();
            source.Resources.Clear();
            source.EventLedger.Clear();

            Assert.That(copy.Resources, Has.Count.EqualTo(1));
            Assert.That(copy.Resources[0].PhysicalHolderId, Is.EqualTo("agent.patient"));
            Assert.That(copy.OfficialOwnerships[0].RegisteredOwnerId,
                Is.EqualTo("organisation.clinic"));
            Assert.That(copy.AccessGrants[0].Active, Is.True);
            Assert.That(copy.AuthorityGrants[0].Active, Is.True);
            Assert.That(copy.CollectiveCommitments[0].MemberAgentIds,
                Is.EqualTo(new[] { "agent.patient", "agent.witness" }));
            Assert.That(copy.EventLedger, Has.Count.EqualTo(1));
            Assert.That(copy.EventLedger[0].DirectWitnessAgentIds,
                Is.EqualTo(new[] { "agent.witness" }));
            Assert.That(copy.EventLedger[0].PotentialRecordSourceIds,
                Is.EqualTo(new[] { "record-source.clinic-camera" }));
            Assert.That(copy.EventLedger[0].CauseEventIds,
                Is.EqualTo(new[] { "society.event.access-opened" }));
        }

        [Test]
        public void MaterialTruthTypes_AreAuthorityOnlyAndAbsentFromPublicReportGraph()
        {
            Type[] truthTypes =
            {
                typeof(InstitutionalMaterialWorld),
                typeof(MaterialResourceState),
                typeof(OfficialOwnershipState),
                typeof(MaterialWorldEvent),
            };
            Assert.That(truthTypes.All(type => type.IsNotPublic), Is.True);

            Type reportType = typeof(InstitutionalConsequenceReport);
            Type[] exposed = reportType
                .GetFields()
                .Select(field => field.FieldType)
                .Concat(reportType.GetProperties().Select(property => property.PropertyType))
                .ToArray();
            Assert.That(exposed.Any(type => truthTypes.Contains(type)), Is.False);
        }

        private static SocietyState CreateSociety()
        {
            var society = new SocietyState
            {
                MasterSeed = 42042,
                CurrentTick = 1,
                Agents = new List<AgentState>
                {
                    Agent("agent.patient", 0),
                    Agent("agent.witness", 1),
                    Agent("agent.supervisor", 2),
                },
                EventLedger = new List<SocietyEvent>
                {
                    new()
                    {
                        EventId = "society.event.access-opened",
                        Tick = 0,
                        Kind = SocietyEventKind.WorkPerformed,
                        ActorId = "agent.supervisor",
                    },
                    new()
                    {
                        EventId = "society.event.shared-grievance",
                        Tick = 1,
                        Kind = SocietyEventKind.AssistanceGiven,
                        ActorId = "agent.witness",
                    },
                },
            };
            SocietyStateValidator.Validate(society);
            return society;
        }

        private static AgentState Agent(string id, int ordinal)
        {
            var needs = new List<NeedState>();
            foreach (NeedKind kind in Enum.GetValues(typeof(NeedKind)))
                needs.Add(new NeedState { Kind = kind, Pressure = 40 });
            return new AgentState
            {
                StableId = id,
                SimulationOrdinal = ordinal,
                PresentationId = $"presentation.{ordinal}",
                DisplayName = $"Agent {ordinal}",
                SpeciesId = "species.test",
                HouseholdId = $"household.{ordinal}",
                EmployerId = "organisation.clinic",
                InstitutionalTrust = 0,
                Needs = needs,
            };
        }

        private static InstitutionalMaterialWorld CreateWorld(bool includeCollective = false)
        {
            var world = new InstitutionalMaterialWorld
            {
                Resources = new List<MaterialResourceState>
                {
                    new()
                    {
                        ResourceId = "resource.medicine.001",
                        ResourceKindId = "medicine-unit",
                        Quantity = 1,
                        PhysicalHolderId = "organisation.clinic",
                        LocationContextId = "context.clinic-store",
                    },
                },
                OfficialOwnerships = new List<OfficialOwnershipState>
                {
                    new()
                    {
                        OwnershipRecordId = "ownership.clinic-medicine.001",
                        ResourceId = "resource.medicine.001",
                        RegisteredOwnerId = "organisation.clinic",
                        OwnershipSourceId = "record.clinic-inventory",
                        RecognitionTick = 0,
                        Disputed = false,
                    },
                },
                AccessGrants = new List<MaterialAccessGrantState>
                {
                    new()
                    {
                        GrantId = "access.patient-medicine",
                        AgentId = "agent.patient",
                        AccessKindId = "physical-access",
                        TargetId = "resource.medicine.001",
                        SourceRecordId = "record.clinic-access-roster",
                        ValidFromTick = 0,
                        ValidUntilTick = -1,
                        Active = true,
                    },
                },
                AuthorityGrants = new List<MaterialAuthorityGrantState>
                {
                    new()
                    {
                        GrantId = "authority.supervisor-access",
                        AgentId = "agent.supervisor",
                        Kind = MaterialAuthorityKind.RemoveAccess,
                        TargetId = "organisation.clinic",
                        SourceRecordId = "record.clinic-management-authority",
                        ValidFromTick = 0,
                        ValidUntilTick = -1,
                        Active = true,
                    },
                },
            };
            if (includeCollective)
            {
                world.CollectiveCommitments.Add(new CollectiveCommitmentState
                {
                    CommitmentId = "collective.medicine-access",
                    IssueId = "issue.medicine-access",
                    CurrentIntentionId = "intention.protect-shared-access",
                    Strength = 55,
                    FormedTick = 1,
                    MemberAgentIds = new List<string>
                    {
                        "agent.patient",
                        "agent.witness",
                    },
                    FormationCauseEventIds = new List<string>
                    {
                        "society.event.access-opened",
                        "society.event.shared-grievance",
                    },
                });
            }

            return world;
        }

        private static PossessionTransferRequest TransferRequest()
        {
            return new PossessionTransferRequest
            {
                EventId = "material.event.medicine-transfer",
                CauseDecisionId = "decision.patient-takes-medicine",
                Tick = 2,
                ActorAgentId = "agent.patient",
                ResourceId = "resource.medicine.001",
                ExpectedPhysicalHolderId = "organisation.clinic",
                NewPhysicalHolderId = "agent.patient",
                NewLocationContextId = "context.clinic-corridor",
                Visibility = MaterialEventVisibility.WitnessLimited,
                Secrecy = 70,
                DirectWitnessAgentIds = new List<string> { "agent.witness" },
                PotentialRecordSourceIds = new List<string>
                {
                    "record-source.clinic-camera",
                },
                CauseEventIds = new List<string> { "society.event.access-opened" },
            };
        }
    }
}
