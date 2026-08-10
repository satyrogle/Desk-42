using System;
using System.Collections.Generic;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM4GateETests
    {
        [Test]
        public void BreakRecoveryTargetsRemainVisibleAt1280x720()
        {
            var presenter = new OfficeM4HudPresenter();
            Assert.That(presenter.BreakTargetsRemainVisible(1280, 720), Is.True);
        }

        [Test]
        public void HudFitsAt1280x720WithoutClipping()
        {
            Assert.That(new OfficeM4HudPresenter().Fits(1280, 720), Is.True);
        }

        [Test]
        public void HudFitsAt1600x900WithoutClipping()
        {
            Assert.That(new OfficeM4HudPresenter().Fits(1600, 900), Is.True);
        }

        [Test]
        public void DevelopmentHudIsHiddenInProductionCapture()
        {
            var presenter = new OfficeM4HudPresenter();
            Assert.That(presenter.DevelopmentHudVisible, Is.False);
        }

        [Test]
        public void ReducedFlashSeamChangesPresentationOnly()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            string checksum = state.Checksum;
            var presenter = new OfficeM4HudPresenter();
            presenter.SetReducedFlash(true);
            Assert.That(presenter.ReducedFlash, Is.True);
            Assert.That(state.Checksum, Is.EqualTo(checksum));
        }

        [TestCase(1, "01-shift-1-opening")]
        [TestCase(1, "02-shift-1-paper-check")]
        [TestCase(1, "03-shift-1-money-trace")]
        [TestCase(1, "04-shift-1-copy-echo-warning")]
        [TestCase(1, "05-shift-1-copy-echo-break")]
        [TestCase(1, "06-shift-1-upgrade-choice")]
        [TestCase(2, "07-shift-2-opening-upgrade-visible")]
        [TestCase(2, "08-shift-2-ghost-clock")]
        [TestCase(2, "09-shift-2-missing-room-access")]
        [TestCase(2, "10-shift-2-second-upgrade-choice")]
        [TestCase(3, "11-shift-3-opening-both-rules")]
        [TestCase(3, "12-shift-3-promotion-warning")]
        [TestCase(3, "13-shift-3-promotion-cascade")]
        [TestCase(3, "14-shift-3-recovery")]
        [TestCase(3, "15-final-campaign-result")]
        [TestCase(3, "16-next-day-tease")]
        public void EveryRequiredCaptureStateUsesCanonicalCampaignFlow(
            int shift,
            string stateName)
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();

            OfficeCampaignCaptureDriver.Prepare(campaign, shift, stateName);

            OfficeSimulationState state = campaign.CurrentSimulation;
            Assert.That(campaign.CurrentShiftOrdinal, Is.EqualTo(shift), stateName);
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(),
                Is.True, stateName);
            switch (stateName)
            {
                case "01-shift-1-opening":
                    Assert.That(state.CurrentTick, Is.Zero); break;
                case "02-shift-1-paper-check":
                    Assert.That(state.ManualTasks.ActiveKind,
                        Is.EqualTo(OfficeManualTaskKind.Compare)); break;
                case "03-shift-1-money-trace":
                    Assert.That(state.ManualTasks.ActiveKind,
                        Is.EqualTo(OfficeManualTaskKind.Trace)); break;
                case "04-shift-1-copy-echo-warning":
                    Assert.That(state.AutomationRule.Enabled, Is.True);
                    Assert.That(state.BreakState.Active, Is.False); break;
                case "05-shift-1-copy-echo-break":
                    Assert.That(state.BreakState.Active, Is.True); break;
                case "06-shift-1-upgrade-choice":
                case "10-shift-2-second-upgrade-choice":
                    Assert.That(campaign.Phase,
                        Is.EqualTo(OfficeCampaignPhase.ChooseUpgrade)); break;
                case "07-shift-2-opening-upgrade-visible":
                    Assert.That(campaign.Upgrades.FastTraysTier, Is.EqualTo(1)); break;
                case "08-shift-2-ghost-clock":
                    Assert.That(state.GhostClock.Active, Is.True); break;
                case "09-shift-2-missing-room-access":
                    Assert.That(state.MissingRoomAccess.Active, Is.True); break;
                case "11-shift-3-opening-both-rules":
                    Assert.That(state.AutomationRule.Enabled, Is.True);
                    Assert.That(state.PayrollRule.Enabled, Is.True); break;
                case "12-shift-3-promotion-warning":
                    Assert.That(state.PromotionCascade.PromotionFormIds.Count,
                        Is.GreaterThan(0));
                    Assert.That(state.PromotionCascade.HasTriggered, Is.False); break;
                case "13-shift-3-promotion-cascade":
                    Assert.That(state.PromotionCascade.Active, Is.True); break;
                case "14-shift-3-recovery":
                    Assert.That(state.PromotionCascade.Recovered, Is.True); break;
                case "15-final-campaign-result":
                case "16-next-day-tease":
                    Assert.That(campaign.IsComplete, Is.True); break;
            }
        }

        [Test]
        public void SteadyStateVisualUpdateAllocatesZeroManagedBytesAfterWarmup()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            OfficeVisualSnapshot snapshot =
                new OfficeVisualStateProjector().Project(state, null);
            var root = new GameObject("M4 Allocation Test");
            try
            {
                var director = new OfficeVisualDirector(
                    root.transform, OfficeSpriteCatalog.LoadRequired());
                director.BuildEnvironment();
                director.Apply(snapshot);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++) director.Apply(snapshot);
                long after = GC.GetAllocatedBytesForCurrentThread();
                Assert.That(after - before, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoRuntimeMaterialCountGrowthAcrossThreeShiftCampaign()
        {
            var root = new GameObject("M4 Material Growth Test");
            try
            {
                var director = new OfficeVisualDirector(
                    root.transform, OfficeSpriteCatalog.LoadRequired());
                director.BuildEnvironment();
                int baseline = DistinctMaterialCount(root);
                var projector = new OfficeVisualStateProjector();
                for (int shift = 1; shift <= 3; shift++)
                {
                    OfficeCampaignState campaign = OfficeCampaignState.Create();
                    string capture = shift == 1 ? "05-shift-1-copy-echo-break" :
                        shift == 2 ? "08-shift-2-ghost-clock" :
                        "13-shift-3-promotion-cascade";
                    OfficeCampaignCaptureDriver.Prepare(campaign, shift, capture);
                    director.Apply(projector.Project(
                        campaign.CurrentSimulation, campaign));
                    Assert.That(DistinctMaterialCount(root), Is.EqualTo(baseline));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoUnboundedTemporaryGameObjectGrowthAcrossRestart()
        {
            int baselineRoots = OfficeVisualDirector.ActiveRootCount();
            for (int restart = 0; restart < 20; restart++)
            {
                var root = new GameObject("M4 Temporary Root " + restart);
                var director = new OfficeVisualDirector(
                    root.transform, OfficeSpriteCatalog.LoadRequired());
                director.BuildEnvironment();
                int children = root.transform.childCount;
                for (int effect = 0; effect < 16; effect++)
                    director.RequestVfx("vfx.copy-spawn", Vector3.zero);
                director.VfxPool.ReleaseAll();
                Assert.That(root.transform.childCount, Is.EqualTo(children));
                UnityEngine.Object.DestroyImmediate(root);
                Assert.That(OfficeVisualDirector.ActiveRootCount(),
                    Is.EqualTo(baselineRoots));
            }
        }

        private static int DistinctMaterialCount(GameObject root)
        {
            var materials = new HashSet<int>();
            SpriteRenderer[] renderers =
                root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i].sharedMaterial != null)
                    materials.Add(renderers[i].sharedMaterial.GetInstanceID());
            return materials.Count;
        }
    }
}
