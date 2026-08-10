using System;
using System.IO;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM5GateATests
    {
        [Test]
        public void AudioDirectorDoesNotMutateSimulationChecksum()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeSimulationState state = campaign.CurrentSimulation;
            string before = state.Checksum;
            GameObject owner = new("M5 Audio Test Owner");
            try
            {
                var director = new OfficeAudioDirector(
                    owner.transform,
                    OfficeAudioCueCatalog.Load(),
                    new OfficeAudioSettings());

                director.Apply(state, campaign, 1f / 60f);

                Assert.That(state.Checksum, Is.EqualTo(before));
                director.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MutedAudioProducesIdenticalCampaignChecksum()
        {
            OfficeCampaignState enabledCampaign = OfficeCampaignState.Create();
            OfficeCampaignState mutedCampaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                enabledCampaign, 1, "05-shift-1-copy-echo-break");
            OfficeCampaignCaptureDriver.Prepare(
                mutedCampaign, 1, "05-shift-1-copy-echo-break");
            var muted = new OfficeAudioSettings();
            muted.SetAudioEnabled(false);
            GameObject owner = new("M5 Muted Audio Owner");
            try
            {
                var director = new OfficeAudioDirector(
                    owner.transform, OfficeAudioCueCatalog.Load(), muted);
                director.Apply(mutedCampaign.CurrentSimulation,
                    mutedCampaign, 1f / 60f);

                Assert.That(mutedCampaign.Checksum,
                    Is.EqualTo(enabledCampaign.Checksum));
                Assert.That(director.Settings.Muted, Is.True);
                Assert.That(director.VoicePool.ActiveOneShotCount, Is.Zero);
                director.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MissingOptionalCueDoesNotBreakGameplay()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string before = campaign.Checksum;
            GameObject owner = new("M5 Optional Cue Owner");
            try
            {
                var director = new OfficeAudioDirector(
                    owner.transform,
                    OfficeAudioCueCatalog.Load(),
                    new OfficeAudioSettings());

                Assert.DoesNotThrow(() => director.PlayCue("optional.not-present"));
                Assert.That(director.PlayCue("optional.not-present"), Is.False);
                Assert.That(campaign.Checksum, Is.EqualTo(before));
                director.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AudioVoiceCountIsBounded()
        {
            GameObject owner = new("M5 Voice Pool Owner");
            try
            {
                var director = new OfficeAudioDirector(
                    owner.transform,
                    OfficeAudioCueCatalog.Load(),
                    new OfficeAudioSettings());
                for (int i = 0; i < 100; i++)
                    director.PlayCue("music.work");

                Assert.That(director.VoicePool.ActiveOneShotCount,
                    Is.LessThanOrEqualTo(OfficeAudioVoicePool.OneShotCapacity));
                Assert.That(director.VoicePool.PeakOneShotVoices,
                    Is.LessThanOrEqualTo(OfficeAudioVoicePool.OneShotCapacity));
                Assert.That(director.VoicePool.TotalSourceCount,
                    Is.EqualTo(44));
                Assert.That(director.VoicePool.GrowthCount, Is.Zero);
                director.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AudioRootsDoNotDuplicateAfterRestart()
        {
            GameObject owner = new("M5 Root Owner");
            try
            {
                var first = new OfficeAudioVoicePool(owner.transform);
                var second = new OfficeAudioVoicePool(owner.transform);

                Assert.That(OfficeAudioVoicePool.ActiveRootCount(), Is.EqualTo(1));
                Assert.That(first.Root.gameObject.activeSelf, Is.False);
                Assert.That(second.Root.gameObject.activeSelf, Is.True);
                second.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RuntimeAudioManifestHasProvenance()
        {
            OfficeAudioCueCatalog catalog = OfficeAudioCueCatalog.Load();
            Assert.That(catalog.AssetCount, Is.EqualTo(8));
            Assert.That(catalog.CueCount, Is.EqualTo(8));
            Assert.That(catalog.MissingClipCount, Is.Zero);

            string root = Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException("Project root is unavailable.");
            string ledger = Path.Combine(root, "AudioLab", "OfficeSliceM5",
                "Provenance", "audio-ledger.csv");
            string[] lines = File.ReadAllLines(ledger);
            Assert.That(lines, Has.Length.EqualTo(catalog.AssetCount + 1));
            for (int i = 0; i < catalog.Manifest.assets.Length; i++)
            {
                OfficeAudioAssetRecord asset = catalog.Manifest.assets[i];
                Assert.That(asset.sample_rate, Is.EqualTo(48000));
                Assert.That(asset.bit_depth, Is.EqualTo(16));
                Assert.That(File.Exists(Path.Combine(root,
                    asset.runtime_filename.Replace('/', Path.DirectorySeparatorChar))),
                    Is.True, asset.asset_id);
                Assert.That(Array.Exists(lines, line =>
                    line.StartsWith(asset.asset_id + ",", StringComparison.Ordinal)),
                    Is.True, asset.asset_id);
            }
        }
    }
}
