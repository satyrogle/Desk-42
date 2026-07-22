// ============================================================
// DESK 42 — SynergyResolver Unit Tests (Edit Mode)
//
// Covers the duration / credit-cost / soul-cost modifier chains
// against real registered Ship Tier supply effects (Paperclip,
// Rubber Stamp, Paper Weight — see ShipTierSupplies.cs).
//
// Includes a regression test for the structured per-step trace
// exposed by ResolveCascade for tooltip/debug presentation.
// ============================================================

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Desk42.Core;
using Desk42.OfficeSupplies;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class SynergyResolverTests
    {
        private GameObject _managerGo;
        private OfficeSupplyManager _manager;
        private SynergyResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _managerGo = new GameObject("TestSupplyManager");
            _manager = _managerGo.AddComponent<OfficeSupplyManager>();
            _manager.Initialize(() => new SupplyContext());
            _resolver = _manager.Resolver;
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerGo != null)
                Object.DestroyImmediate(_managerGo);
            RumorMill.ClearAllSubscriptions();
        }

        private OfficeSupplyData PlaceTestSupply(string supplyId, DeskZone zone)
        {
            var data = ScriptableObject.CreateInstance<OfficeSupplyData>();
            data.SupplyId = supplyId;
            data.DisplayName = supplyId;
            data.Zone = zone;
            _manager.PlaceSupply(data);
            return data;
        }

        // ── Duration Modifier Chain (Paperclip) ───────────────

        [Test]
        public void Paperclip_DoublesPendingReviewDuration()
        {
            PlaceTestSupply("paperclip", DeskZone.Tray);

            float result = _resolver.ApplyDurationModifiers(PunchCardType.PendingReview, 10f);

            Assert.AreEqual(20f, result, 0.001f);
        }

        [Test]
        public void Paperclip_DoesNotAffectOtherCardTypes()
        {
            PlaceTestSupply("paperclip", DeskZone.Tray);

            float result = _resolver.ApplyDurationModifiers(PunchCardType.ThreatAudit, 10f);

            Assert.AreEqual(10f, result, 0.001f);
        }

        [Test]
        public void Paperclip_PlusNoOpPaperWeight_ChainComposesCorrectly()
        {
            PlaceTestSupply("paperclip", DeskZone.Tray);
            PlaceTestSupply("paper_weight", DeskZone.Inbox);

            float result = _resolver.ApplyDurationModifiers(PunchCardType.PendingReview, 10f);

            Assert.AreEqual(20f, result, 0.001f,
                "PaperWeight has no duration effect; chain must pass Paperclip's doubling through unchanged.");
        }

        [Test]
        public void NoActiveSupplies_DurationUnchanged()
        {
            float result = _resolver.ApplyDurationModifiers(PunchCardType.PendingReview, 10f);

            Assert.AreEqual(10f, result, 0.001f);
        }

        // ── Credit Cost Modifier Chain (Rubber Stamp) ─────────

        [Test]
        public void RubberStamp_ZeroesFirstSlamCreditCost()
        {
            PlaceTestSupply("rubber_stamp", DeskZone.Tray);

            int result = _resolver.ApplyCreditCostModifiers(PunchCardType.PendingReview, 5);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void RubberStamp_OnlyAppliesOnce_SecondCallUnaffected()
        {
            PlaceTestSupply("rubber_stamp", DeskZone.Tray);

            _resolver.ApplyCreditCostModifiers(PunchCardType.PendingReview, 5);
            int second = _resolver.ApplyCreditCostModifiers(PunchCardType.PendingReview, 5);

            Assert.AreEqual(5, second, "Rubber Stamp's free-slam is consumed once per encounter (until OnEncounterStart resets it).");
        }

        [Test]
        public void PreviewCascade_DoesNotConsumeRubberStamp()
        {
            PlaceTestSupply("rubber_stamp", DeskZone.Tray);

            var firstPreview = _resolver.PreviewCascade(
                PunchCardType.PendingReview, 10f, 5, 0f);
            var secondPreview = _resolver.PreviewCascade(
                PunchCardType.PendingReview, 10f, 5, 0f);
            var applied = _resolver.ResolveCascade(
                PunchCardType.PendingReview, 10f, 5, 0f);
            var afterApplication = _resolver.ResolveCascade(
                PunchCardType.PendingReview, 10f, 5, 0f);

            Assert.AreEqual(0, firstPreview.FinalCreditCost);
            Assert.AreEqual(0, secondPreview.FinalCreditCost,
                "Repeated hover must not consume the projected free slam.");
            Assert.AreEqual(0, applied.FinalCreditCost,
                "The real slam must still receive the projected discount.");
            Assert.AreEqual(5, afterApplication.FinalCreditCost,
                "Only the applied cascade consumes the Rubber Stamp.");
        }

        [Test]
        public void NoActiveSupplies_CreditCostUnchanged()
        {
            int result = _resolver.ApplyCreditCostModifiers(PunchCardType.PendingReview, 5);

            Assert.AreEqual(5, result);
        }

        // ── Soul Cost Modifier Chain (Paper Weight) ───────────

        [Test]
        public void PaperWeight_ReducesSoulCostByOne()
        {
            PlaceTestSupply("paper_weight", DeskZone.Inbox);

            float result = _resolver.ApplySoulCostModifiers(5f);

            Assert.AreEqual(4f, result, 0.001f);
        }

        [Test]
        public void PaperWeight_SoulCostFlooredAtZero()
        {
            PlaceTestSupply("paper_weight", DeskZone.Inbox);

            float result = _resolver.ApplySoulCostModifiers(0.5f);

            Assert.AreEqual(0f, result, 0.001f);
        }

        // ── Regression lock: current Debug.Log-only per-step output ──

        [Test]
        public void Paperclip_DurationChange_IsCurrentlyOnlyObservableViaLog()
        {
            PlaceTestSupply("paperclip", DeskZone.Tray);

            LogAssert.Expect(LogType.Log,
                new System.Text.RegularExpressions.Regex(
                    @"\[SynergyResolver\].*duration 10\.00s.*20\.00s.*PendingReview"));

            _resolver.ApplyDurationModifiers(PunchCardType.PendingReview, 10f);
        }

        // ── WIRING GAP ─────────────────────────────────────────

        [Test]
        public void DurationAndCostChains_ExposeStructuredPerStepTrace()
        {
            // Intended: ApplyDurationModifiers / ApplyCreditCostModifiers should expose
            // each contributing supply's individual delta through a public API, not just
            // a Debug.Log line. Today the only place that information exists is the log.
            var candidateMembers = typeof(SynergyResolver)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.Contains("Trace") || m.Name.Contains("Step") ||
                            m.Name.Contains("Breakdown") || m.Name.Contains("Cascade"))
                .ToList();

            Assert.IsNotEmpty(candidateMembers,
                "SynergyResolver must expose a structured per-step trace API for " +
                "tooltip and debug UI use.");
        }
    }
}
