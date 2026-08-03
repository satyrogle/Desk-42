using System.Collections.Generic;
using System.Linq;
using Desk42.Institutional;
using Desk42.Institutional.Player;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalEndogenousRemedyTests
    {
        [Test]
        public void RestorePossession_ExecutesExactlyOnceToRegisteredOwner()
        {
            EndogenousRunSnapshot snapshot =
                CausalLegibilitySliceSeed.CreatePreRulingSnapshot();
            EndogenousInstitutionalCase opened = snapshot.Docket.OpenCases.Single();
            CommittedPlayerRuling ruling = EndogenousPlayerRulingService.Commit(
                snapshot.Society,
                snapshot.Docket,
                Command(opened, RulingDisposition.Recognised));
            MaterialResourceState resource = snapshot.MaterialWorld.GetResource(
                CausalLegibilitySliceSeed.InitialResourceId);
            Assert.AreEqual(CausalLegibilitySliceSeed.OriginAgentId,
                resource.PhysicalHolderId);
            int eventsBefore = snapshot.MaterialWorld.EventLedger.Count;

            EndogenousRemedyApplicationTrace first =
                EndogenousRemedyEffectService.Execute(
                    snapshot.Society,
                    snapshot.MaterialWorld,
                    snapshot.Docket,
                    ruling);
            EndogenousRemedyApplicationTrace replay =
                EndogenousRemedyEffectService.Execute(
                    snapshot.Society,
                    snapshot.MaterialWorld,
                    snapshot.Docket,
                    ruling);

            Assert.AreSame(first, replay);
            Assert.AreEqual("clinic", resource.PhysicalHolderId);
            Assert.AreEqual(eventsBefore + 1, snapshot.MaterialWorld.EventLedger.Count);
            Assert.AreEqual(1, snapshot.Docket.RemedyApplicationTraces.Count);
            Assert.AreEqual(EndogenousRemedyEffectService.RegisteredOwnerDestinationRule,
                first.DestinationRuleId);
            MaterialWorldEvent materialEvent = snapshot.MaterialWorld.GetEvent(
                first.MaterialEventId);
            Assert.NotNull(materialEvent);
            Assert.AreEqual(ruling.RulingId, materialEvent.CauseDecisionId);
            Assert.AreEqual(first.PreviousPhysicalHolderId,
                materialEvent.PreviousPhysicalHolderId);
            Assert.AreEqual(first.NewPhysicalHolderId,
                materialEvent.NewPhysicalHolderId);
            Assert.DoesNotThrow(() => EndogenousRunSnapshotService.Capture(
                "remedy-exact-once",
                EndogenousCommitPhase.RulingCommitted,
                snapshot.Society,
                snapshot.MaterialWorld,
                snapshot.Docket));
        }

        [Test]
        public void Denial_RecordsNoExecutableRemedyOrMaterialChange()
        {
            EndogenousRunSnapshot snapshot =
                CausalLegibilitySliceSeed.CreatePreRulingSnapshot();
            EndogenousInstitutionalCase opened = snapshot.Docket.OpenCases.Single();
            CommittedPlayerRuling ruling = EndogenousPlayerRulingService.Commit(
                snapshot.Society,
                snapshot.Docket,
                Command(opened, RulingDisposition.Denied));
            MaterialResourceState resource = snapshot.MaterialWorld.GetResource(
                CausalLegibilitySliceSeed.InitialResourceId);
            string holderBefore = resource.PhysicalHolderId;
            int eventsBefore = snapshot.MaterialWorld.EventLedger.Count;

            EndogenousRemedyApplicationTrace trace =
                EndogenousRemedyEffectService.Execute(
                    snapshot.Society,
                    snapshot.MaterialWorld,
                    snapshot.Docket,
                    ruling);

            Assert.IsNull(trace);
            Assert.AreEqual(holderBefore, resource.PhysicalHolderId);
            Assert.AreEqual(eventsBefore, snapshot.MaterialWorld.EventLedger.Count);
            Assert.IsEmpty(snapshot.Docket.RemedyApplicationTraces);
        }

        [Test]
        public void ScopeTimeline_PreservesActualApplicationTickAfterLaterPulses()
        {
            EndogenousRunSnapshot snapshot =
                CausalLegibilitySliceSeed.CreatePreRulingSnapshot();
            EndogenousInstitutionalCase opened = snapshot.Docket.OpenCases.Single();
            CommittedPlayerRuling ruling = EndogenousPlayerRulingService.Commit(
                snapshot.Society,
                snapshot.Docket,
                Command(opened, RulingDisposition.Recognised));
            SimulationInput later = CausalLegibilitySliceSeed.QuietInput();
            EndogenousActionOpportunityBuilder.Populate(
                snapshot.Society, snapshot.MaterialWorld, later);
            EndogenousRemedyEffectService.Execute(
                snapshot.Society, snapshot.MaterialWorld, snapshot.Docket, ruling);
            List<EndogenousScopeApplicationTrace> traces =
                EndogenousScopeEffectService.Apply(
                    snapshot.Society, snapshot.Docket, later);
            EndogenousScopeApplicationTrace applied = traces.Single(value =>
                value.ActorId == CausalLegibilitySliceSeed.ConnectedAgentId);
            long appliedTick = applied.AppliedTick;

            var step = new EndogenousSocietyStepService();
            for (int i = 0; i < 3; i++)
                step.Advance(
                    snapshot.Society,
                    snapshot.MaterialWorld,
                    CausalLegibilitySliceSeed.QuietInput());
            PlayerInstitutionView view = PlayerInstitutionProjector.Project(
                snapshot.Society, snapshot.MaterialWorld, snapshot.Docket);
            PublicTimelineEntry row = view.Timeline.Single(value =>
                value.EntryId == "timeline:" + applied.TraceId);

            Assert.Greater(snapshot.Society.CurrentTick, appliedTick);
            Assert.AreEqual(appliedTick, row.Cycle);
        }

        private static PlayerRulingCommand Command(
            EndogenousInstitutionalCase opened,
            RulingDisposition disposition)
        {
            return new PlayerRulingCommand
            {
                CommandId = $"remedy-test:{disposition}",
                CaseId = opened.CaseId,
                ExpectedCaseVersion = opened.CaseVersion,
                EvidenceEnvelopeHash = opened.EvidenceEnvelopeHash,
                RecognisedFactIds = new List<string>(opened.AvailableFactIds),
                CitedEvidenceArtifactIds = new List<string>(opened.ObservationIds),
                Disposition = disposition,
                HoldingRuleId = EndogenousPlayerRulingService.PossessionHoldingRule,
                Scope = new ScopeExpression
                {
                    Kind = ScopeExpressionKind.Predicate,
                    PredicateKind = ScopePredicateKind.IssueEquals,
                    Value = opened.IssueId,
                },
                TemporalReach = TemporalReach.Prospective,
                RemedyDefinitionIds = new List<string>
                {
                    disposition == RulingDisposition.Denied
                        ? EndogenousPlayerRulingService.NoChangeRemedy
                        : EndogenousPlayerRulingService.RestorePossessionRemedy,
                },
            };
        }
    }
}
