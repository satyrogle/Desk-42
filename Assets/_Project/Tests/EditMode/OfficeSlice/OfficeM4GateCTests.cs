using System;
using System.Collections.Generic;
using Desk42.Product.OfficeSlice;
using Desk42.Tests.EditMode.OfficeM3;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM4GateCTests
    {
        private static readonly string[] Customers =
        {
            "nia-bell", "owen-pike", "mara-vale",
            "iris-cole", "tomas-reed", "june-hart",
        };

        [Test]
        public void AllCharacterStatesShareStableAnchors()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            int count = 0;
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                OfficeSpriteCatalog.Entry entry = catalog.Entries[i];
                if (!entry.Id.StartsWith("character.", StringComparison.Ordinal)) continue;
                Assert.That(entry.Anchor, Is.EqualTo(new Vector2(0.5f, 0f)), entry.Id);
                Assert.That(entry.Sprite.pivot.x / entry.Sprite.rect.width,
                    Is.EqualTo(0.5f).Within(0.01f), entry.Id);
                count++;
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(55));
        }

        [Test]
        public void EveryCustomerHasDistinctSilhouetteSignature()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            var hashes = new HashSet<Hash128>();
            for (int i = 0; i < Customers.Length; i++)
            {
                string id = "character." + Customers[i] + ".calm";
                Assert.That(catalog.TryResolve(id, out Sprite sprite), Is.True, id);
                Assert.That(hashes.Add(AssetDatabase.GetAssetDependencyHash(
                    AssetDatabase.GetAssetPath(sprite))), Is.True, id);
            }
        }

        [Test]
        public void RequiredCastStatesAndPortraitsResolve()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            string[] roleStates =
            {
                "character.warden.walk-up", "character.warden.walk-down",
                "character.warden.walk-left", "character.warden.walk-right",
                "character.warden.carry-walk-up", "character.warden.carry-walk-down",
                "character.warden.carry-walk-left", "character.warden.carry-walk-right",
                "character.warden.interact", "character.warden.calm",
                "character.warden.fix", "character.warden.help", "character.warden.stunned",
                "character.runner.carry", "character.runner.work", "character.runner.blocked",
                "character.runner.obey-copier", "character.runner.return-to-warden",
                "character.talker.calm-customer", "character.talker.blocked",
                "character.talker.work",
            };
            for (int i = 0; i < roleStates.Length; i++)
                Assert.That(catalog.TryResolve(roleStates[i], out Sprite sprite) && sprite != null,
                    Is.True, roleStates[i]);
            for (int customer = 0; customer < Customers.Length; customer++)
                foreach (string mood in new[] { "calm", "worried", "upset", "strange" })
                {
                    string id = "portrait." + Customers[customer] + "." + mood;
                    Assert.That(catalog.TryResolve(id, out Sprite sprite) && sprite != null,
                        Is.True, id);
                }
            Assert.That(catalog.TryResolve("portrait.mara-vale.promotion-cascade", out _), Is.True);
            Assert.That(catalog.TryResolve("portrait.tomas-reed.ghost-clock", out _), Is.True);
        }

        [Test]
        public void AnimationEventsDoNotOwnGameplayActions()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            string checksum = state.Checksum;
            int commands = state.CommandLog.Commands.Count;
            var animation = new OfficeTickAnimationDriver();

            for (int tick = 0; tick < 300; tick++)
            {
                Assert.That(animation.FrameAt(tick, 4), Is.InRange(0, 3));
                OfficeTickAnimationDriver.WardenMovementAssetId(
                    (OfficeInputDirection)(tick % 5), tick % 2 == 0);
            }

            Assert.That(state.Checksum, Is.EqualTo(checksum));
            Assert.That(state.CommandLog.Commands.Count, Is.EqualTo(commands));
            Assert.That(state.CurrentTick, Is.Zero);
        }

        [Test]
        public void VisualFrameRateDoesNotChangeCampaignChecksum()
        {
            string expected = null;
            var projector = new OfficeVisualStateProjector();
            foreach (int renderFps in new[] { 30, 60, 144 })
            {
                OfficeCampaignState campaign = OfficeCampaignState.Create();
                OfficeCampaignCaptureDriver.Prepare(campaign, 3, "promotion-cascade");
                string before = campaign.Checksum;
                int projections = renderFps * 3;
                for (int frame = 0; frame < projections; frame++)
                    projector.Project(campaign.CurrentSimulation, campaign);
                Assert.That(campaign.Checksum, Is.EqualTo(before));
                expected ??= before;
                Assert.That(campaign.Checksum, Is.EqualTo(expected));
            }
        }

        [Test]
        public void ReplayProducesSameSimulationChecksumWithVisualsEnabledAndDisabled()
        {
            OfficeCampaignState live = OfficeCampaignState.Create();
            OfficeM3TestDriver.DriveCampaignToResult(live);
            OfficeCampaignState replay = OfficeCampaignReplayRunner.ReplayToResult(
                live.CreateReplayTape());
            var projector = new OfficeVisualStateProjector();
            projector.Project(replay.CurrentSimulation, replay);

            Assert.That(replay.Checksum, Is.EqualTo(live.Checksum));
            Assert.That(replay.CurrentSimulation.Checksum,
                Is.EqualTo(live.CurrentSimulation.Checksum));
        }
    }
}
