using System;
using System.Collections.Generic;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM5GateDTests
    {
        [Test]
        public void CopyEchoAudioSequenceFollowsObservableEventOrder()
        {
            OfficeCampaignState breakCampaign = OfficeCampaignState.Create();
            var projector = new OfficeAudioStateProjector();
            OfficeAudioStateSnapshot opening = projector.Project(
                breakCampaign.CurrentSimulation, breakCampaign);
            OfficeCampaignCaptureDriver.Prepare(
                breakCampaign, 1, "05-shift-1-copy-echo-break");
            List<string> cues = Route(opening, breakCampaign);

            OfficeCampaignState recoveredCampaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                recoveredCampaign, 1, "06-shift-1-upgrade-choice");
            OfficeAudioStateSnapshot broken = projector.Project(
                breakCampaign.CurrentSimulation, breakCampaign);
            cues.AddRange(Route(broken, recoveredCampaign));

            AssertSubsequence(cues,
                "event.copy-echo-trigger",
                "event.copy-spawn",
                "event.copier-stop",
                "event.copy-clear",
                "event.original-recovered",
                "event.recovery-complete");
        }

        [Test]
        public void PromotionCascadeAudioSequenceFollowsObservableEventOrder()
        {
            var projector = new OfficeAudioStateProjector();
            OfficeCampaignState opening = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                opening, 3, "11-shift-3-opening-both-rules");
            OfficeCampaignState active = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                active, 3, "13-shift-3-promotion-cascade");
            List<string> cues = Route(projector.Project(
                opening.CurrentSimulation, opening), active);

            OfficeCampaignState recovered = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                recovered, 3, "14-shift-3-recovery");
            cues.AddRange(Route(projector.Project(
                active.CurrentSimulation, active), recovered));

            AssertSubsequence(cues,
                "automation.copied-accepted",
                "event.promotion-trigger",
                "event.copier-promoted",
                "event.supervisor-authority",
                "event.runner-to-copier",
                "event.supervisor-removed",
                "event.copier-stop",
                "event.runner-to-warden",
                "event.original-recovered",
                "event.recovery-complete");
        }

        [Test]
        public void RecoverySilencesBreakLayersOnlyAfterRecoveryState()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string checksum = campaign.Checksum;
            GameObject owner = new("M5 Gate D Recovery Owner");
            try
            {
                var director = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), new OfficeAudioSettings());
                director.ApplyMixForPresentation(OfficeAudioMixState.Break, 1f);
                float breakTarget = director.VoicePool.MusicTargetVolume(2);
                director.ApplyMixForPresentation(OfficeAudioMixState.Break, 1f);
                Assert.That(director.VoicePool.MusicTargetVolume(2),
                    Is.EqualTo(breakTarget).And.GreaterThan(0f));

                director.ApplyMixForPresentation(OfficeAudioMixState.Recovery, 1f);
                Assert.That(director.VoicePool.MusicTargetVolume(2), Is.Zero);
                Assert.That(director.VoicePool.MusicTargetVolume(0),
                    Is.GreaterThan(0f));
                Assert.That(campaign.Checksum, Is.EqualTo(checksum));
                director.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ShiftRestartClearsAllTransientAudio()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            GameObject owner = new("M5 Gate D Restart Owner");
            try
            {
                var director = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), new OfficeAudioSettings());
                director.PlayCue("event.copy-spawn");
                director.PlayCue("event.promotion-trigger");
                Assert.That(director.VoicePool.ActiveOneShotCount,
                    Is.GreaterThan(0));

                director.ResetForState(campaign.CurrentSimulation, campaign);

                Assert.That(director.VoicePool.ActiveOneShotCount, Is.Zero);
                director.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CampaignReplayDoesNotDuplicateOneShotEvents()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            var projector = new OfficeAudioStateProjector();
            OfficeAudioStateSnapshot opening = projector.Project(
                campaign.CurrentSimulation, campaign);
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 1, "05-shift-1-copy-echo-break");
            OfficeAudioStateSnapshot broken = projector.Project(
                campaign.CurrentSimulation, campaign);
            var router = new OfficeAudioEventRouter();
            router.Reset(opening);
            var first = new List<string>();
            router.Route(broken, campaign.CurrentSimulation,
                (cue, _) => first.Add(cue));
            var replay = new List<string>();
            router.Route(broken, campaign.CurrentSimulation,
                (cue, _) => replay.Add(cue));

            Assert.That(first, Does.Contain("event.copy-echo-trigger"));
            Assert.That(replay, Is.Empty);
        }

        private static List<string> Route(
            OfficeAudioStateSnapshot previous,
            OfficeCampaignState currentCampaign)
        {
            var router = new OfficeAudioEventRouter();
            router.Reset(previous);
            var cues = new List<string>();
            OfficeSimulationState state = currentCampaign.CurrentSimulation;
            var projector = new OfficeAudioStateProjector();
            router.Route(projector.Project(state, currentCampaign), state,
                (cue, _) => cues.Add(cue));
            return cues;
        }

        private static void AssertSubsequence(
            IReadOnlyList<string> actual,
            params string[] expected)
        {
            int cursor = 0;
            for (int i = 0; i < actual.Count && cursor < expected.Length; i++)
                if (string.Equals(actual[i], expected[cursor],
                        StringComparison.Ordinal)) cursor++;
            Assert.That(cursor, Is.EqualTo(expected.Length),
                "Expected ordered cues: " + string.Join(" -> ", expected) +
                "\nObserved: " + string.Join(" | ", actual));
        }
    }
}
