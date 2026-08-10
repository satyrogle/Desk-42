using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM5GateCTests
    {
        private static readonly string[] MachineIds =
        {
            "front-desk-counter",
            "paper-check",
            "money-trace",
            "auto-sorter",
            "copy-echo",
            "ghost-clock",
            "supervisor-stamp",
        };

        private static readonly string[] MachineStates =
        {
            "idle", "active", "warning", "break", "recovered",
        };

        [Test]
        public void CustomerMoodTransitionsMapToOrderedCues()
        {
            OfficeAudioCueCatalog catalog = OfficeAudioCueCatalog.Load();
            OfficeAudioCueRecord worried = Resolve(catalog,
                OfficeAudioEventRouter.CueForMood(OfficeVisibleMoodState.Worried));
            OfficeAudioCueRecord strange = Resolve(catalog,
                OfficeAudioEventRouter.CueForMood(OfficeVisibleMoodState.Strange));
            OfficeAudioCueRecord upset = Resolve(catalog,
                OfficeAudioEventRouter.CueForMood(OfficeVisibleMoodState.Upset));

            Assert.That(worried.asset_id, Is.Not.EqualTo(strange.asset_id));
            Assert.That(strange.asset_id, Is.Not.EqualTo(upset.asset_id));
            Assert.That(worried.base_volume, Is.LessThan(strange.base_volume));
            Assert.That(strange.base_volume, Is.LessThan(upset.base_volume));
        }

        [Test]
        public void AllSevenMachinesResolveRequiredAudioStates()
        {
            OfficeAudioCueCatalog catalog = OfficeAudioCueCatalog.Load();
            foreach (string machineId in MachineIds)
            foreach (string machineState in MachineStates)
            {
                string cue = OfficeAudioEventRouter.CueForMachine(
                    machineId, machineState);
                Assert.That(catalog.TryResolve(cue, out _, out _), Is.True, cue);
            }
        }

        [Test]
        public void AutomationEnableMatchAndRejectAreDistinct()
        {
            OfficeAudioCueCatalog catalog = OfficeAudioCueCatalog.Load();
            string enabled = Resolve(catalog, "automation.enabled").asset_id;
            string match = Resolve(catalog, "automation.match").asset_id;
            string reject = Resolve(catalog, "automation.reject").asset_id;
            string accepted = Resolve(catalog, "automation.copied-accepted").asset_id;

            Assert.That(new[] { enabled, match, reject, accepted }, Is.Unique);
        }

        [Test]
        public void RushStateRaisesPressureWithoutChangingSimulation()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string checksum = campaign.Checksum;
            GameObject owner = new("M5 Gate C Rush Owner");
            try
            {
                var director = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), new OfficeAudioSettings());
                director.ApplyMixForPresentation(OfficeAudioMixState.Calm, 1f);
                float calmPressure = director.VoicePool.MusicTargetVolume(1);
                director.ApplyMixForPresentation(OfficeAudioMixState.Rush, 1f);

                Assert.That(director.VoicePool.MusicTargetVolume(1),
                    Is.GreaterThan(calmPressure));
                Assert.That(director.MixState, Is.EqualTo(OfficeAudioMixState.Rush));
                Assert.That(campaign.Checksum, Is.EqualTo(checksum));
                Assert.That(director.VoicePool.ActiveSourceCount,
                    Is.LessThanOrEqualTo(OfficeAudioVoicePool.ContinuousCapacity +
                        OfficeAudioVoicePool.MusicCapacity));
                director.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PositionalCueFallbackRemainsAudible()
        {
            GameObject owner = new("M5 Gate C Position Owner");
            try
            {
                var director = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), new OfficeAudioSettings());

                Assert.That(director.PlayPositionalCue(
                    "machine.copy-echo.warning", null), Is.True);
                Assert.That(director.VoicePool.ActiveOneShotCount,
                    Is.GreaterThan(0));
                director.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static OfficeAudioCueRecord Resolve(
            OfficeAudioCueCatalog catalog,
            string cueId)
        {
            Assert.That(catalog.TryResolve(cueId, out OfficeAudioCueRecord cue,
                out AudioClip clip), Is.True, cueId);
            Assert.That(clip, Is.Not.Null, cueId);
            return cue;
        }
    }
}
