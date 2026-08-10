using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM6GateDTests
    {
        [Test]
        public void CurrentCustomerHasSinglePresentationFocus()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();

            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(campaign.CurrentSimulation.Customers.ActiveDeskCustomer,
                Is.Not.Null);
            Assert.That(model.CurrentCustomerPresentationFocusCount,
                Is.EqualTo(1));
            Assert.That(model.CustomerCardVisible, Is.True);
            Assert.That(model.CustomerName, Is.Not.Empty);
        }

        [Test]
        public void OriginalAndCopyRemainDistinguishableWithoutColour()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            Assert.That(catalog.TryResolve("folder.original", out Sprite original),
                Is.True);
            Assert.That(catalog.TryResolve("folder.copy.tier-0", out Sprite copy),
                Is.True);
            Assert.That(AssetDatabase.GetAssetDependencyHash(
                    AssetDatabase.GetAssetPath(original)),
                Is.Not.EqualTo(AssetDatabase.GetAssetDependencyHash(
                    AssetDatabase.GetAssetPath(copy))));

            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 1, "05-shift-1-copy-echo-break");
            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);
            Assert.That(model.OriginalCopyLegend, Does.Contain("ORIGINAL"));
            Assert.That(model.OriginalCopyLegend, Does.Contain("COPY"));
            Assert.That(model.OriginalCopyLegend, Does.Contain("STRIPED"));
        }

        [Test]
        public void ActiveRuleHasVisibleState()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 3, "11-shift-3-opening-both-rules");

            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(model.RuleCardVisible, Is.True);
            Assert.That(model.RuleOneText, Does.Contain("AUTO SORTER: ON"));
            Assert.That(model.RuleOneText,
                Does.Contain(OfficeM6PlayerCopyCatalog.RuleOne));
            Assert.That(model.RuleTwoText, Does.Contain("PAY MACHINE: ON"));
            Assert.That(model.RuleTwoText,
                Does.Contain(OfficeM6PlayerCopyCatalog.RuleTwo));
        }

        [Test]
        public void BreakRecoveryTargetsRemainVisible()
        {
            OfficeCampaignState broken = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                broken, 3, "13-shift-3-promotion-cascade");
            var presenter = new OfficeM6HudPresenter();
            OfficeM6HudModel active = presenter.Project(
                broken.CurrentSimulation,
                broken,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(active.BreakCardVisible, Is.True);
            Assert.That(active.RecoveryItems.Count, Is.EqualTo(6));
            Assert.That(active.ActionableProblemRoom, Does.Contain("WEIRD ROOM"));
            Assert.That(presenter.CriticalTargetsRemainVisible(active, 1280, 720),
                Is.True);

            OfficeCampaignState recovered = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                recovered, 3, "14-shift-3-recovery");
            OfficeM6HudModel recovery = presenter.Project(
                recovered.CurrentSimulation,
                recovered,
                OfficeM6ControlScheme.Keyboard);
            Assert.That(recovery.DangerState,
                Is.EqualTo(OfficeM6DangerState.Recovery));
            Assert.That(recovery.WhatHappenedAvailable, Is.True);
        }

        [Test]
        public void ReducedFlashRetainsCriticalStateReadability()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 1, "05-shift-1-copy-echo-break");
            var settings = new OfficeAudioSettings();
            settings.SetReducedFlash(true);

            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(settings.ReducedFlash, Is.True);
            Assert.That(model.DangerText, Is.EqualTo("BREAK"));
            Assert.That(model.BreakCardVisible, Is.True);
            Assert.That(model.BreakCause, Is.Not.Empty);
            Assert.That(model.RecoveryItems, Is.Not.Empty);
        }

        [Test]
        public void AudioMutedRetainsCriticalVisualReadability()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 2, "08-shift-2-ghost-clock");
            var settings = new OfficeAudioSettings();
            settings.SetAudioEnabled(false);

            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(settings.Muted, Is.True);
            Assert.That(model.DangerText, Is.EqualTo("BREAK"));
            Assert.That(model.BreakCause,
                Is.EqualTo(OfficeM6PlayerCopyCatalog.GhostClockCause));
            Assert.That(model.ActionableProblemRoom,
                Does.Contain("PAPER ROOM"));
        }
    }
}
