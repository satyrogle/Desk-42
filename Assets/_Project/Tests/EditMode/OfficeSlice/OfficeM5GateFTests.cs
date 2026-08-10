using System;
using System.IO;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM5GateFTests
    {
        private const string PreferencePrefix =
            "desk42.office-slice.presentation.";

        [Test]
        public void PresentationSettingsPersistOutsideSimulation()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string checksum = campaign.Checksum;
            string[] floatKeys = { "master", "music", "sfx", "ambience" };
            string[] intKeys = { "rumble", "reduced-flash" };
            bool[] floatExisted = new bool[floatKeys.Length];
            bool[] intExisted = new bool[intKeys.Length];
            float[] oldFloats = new float[floatKeys.Length];
            int[] oldInts = new int[intKeys.Length];
            for (int i = 0; i < floatKeys.Length; i++)
            {
                string key = PreferencePrefix + floatKeys[i];
                floatExisted[i] = PlayerPrefs.HasKey(key);
                oldFloats[i] = PlayerPrefs.GetFloat(key);
            }
            for (int i = 0; i < intKeys.Length; i++)
            {
                string key = PreferencePrefix + intKeys[i];
                intExisted[i] = PlayerPrefs.HasKey(key);
                oldInts[i] = PlayerPrefs.GetInt(key);
            }

            try
            {
                var settings = new OfficeAudioSettings();
                settings.SetVolumes(0.71f, 0.42f, 0.63f, 0.34f);
                settings.SetRumble(false);
                settings.SetReducedFlash(true);
                settings.Save();
                OfficeAudioSettings loaded = OfficeAudioSettings.Load();

                Assert.That(loaded.Master, Is.EqualTo(0.71f).Within(0.001f));
                Assert.That(loaded.Music, Is.EqualTo(0.42f).Within(0.001f));
                Assert.That(loaded.Sfx, Is.EqualTo(0.63f).Within(0.001f));
                Assert.That(loaded.Ambience, Is.EqualTo(0.34f).Within(0.001f));
                Assert.That(loaded.Rumble, Is.False);
                Assert.That(loaded.ReducedFlash, Is.True);
                Assert.That(campaign.Checksum, Is.EqualTo(checksum));
            }
            finally
            {
                for (int i = 0; i < floatKeys.Length; i++)
                {
                    string key = PreferencePrefix + floatKeys[i];
                    if (floatExisted[i]) PlayerPrefs.SetFloat(key, oldFloats[i]);
                    else PlayerPrefs.DeleteKey(key);
                }
                for (int i = 0; i < intKeys.Length; i++)
                {
                    string key = PreferencePrefix + intKeys[i];
                    if (intExisted[i]) PlayerPrefs.SetInt(key, oldInts[i]);
                    else PlayerPrefs.DeleteKey(key);
                }
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void MuteAndIndividualBusesApplyIndependently()
        {
            var settings = new OfficeAudioSettings();
            settings.SetVolumes(1f, 0.2f, 0.4f, 0.6f);
            Assert.That(settings.BusGain("Music"), Is.EqualTo(0.2f));
            Assert.That(settings.BusGain("SFX"), Is.EqualTo(0.4f));
            Assert.That(settings.BusGain("Ambience"), Is.EqualTo(0.6f));

            settings.SetAudioEnabled(false);
            Assert.That(settings.BusGain("Music"), Is.Zero);
            Assert.That(settings.BusGain("SFX"), Is.Zero);
            Assert.That(settings.BusGain("Ambience"), Is.Zero);
        }

        [Test]
        public void DefaultMixMeetsReadabilityAndHeadroomChecks()
        {
            OfficeAudioMixAuditReport report = OfficeAudioMixAudit.Evaluate(
                OfficeAudioCueCatalog.Load(), new OfficeAudioSettings());

            Assert.That(report.PrimaryActionsReadable, Is.True);
            Assert.That(report.AutomationReadableInRush, Is.True);
            Assert.That(report.RecoveryReadableInBreak, Is.True);
            Assert.That(report.CustomerWarningsProtected, Is.True);
            Assert.That(report.ComfortableDefaults, Is.True);
            Assert.That(report.NominalHeadroom, Is.True);
            Assert.That(report.Passed, Is.True);
        }

        [Test]
        public void ResultMixClearsOperationalBreakLayers()
        {
            GameObject owner = new("M5 Gate F Result Owner");
            try
            {
                var director = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), new OfficeAudioSettings());
                director.ApplyMixForPresentation(OfficeAudioMixState.Break, 1f);
                Assert.That(director.VoicePool.MusicTargetVolume(2),
                    Is.GreaterThan(0f));

                director.ApplyMixForPresentation(OfficeAudioMixState.Result, 1f);

                Assert.That(director.VoicePool.MusicTargetVolume(1), Is.Zero);
                Assert.That(director.VoicePool.MusicTargetVolume(2), Is.Zero);
                Assert.That(director.VoicePool.MusicTargetVolume(0),
                    Is.GreaterThan(0f));
                director.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RuntimePayloadAndProvenanceStayWithinBudget()
        {
            OfficeAudioCueCatalog catalog = OfficeAudioCueCatalog.Load();
            Assert.That(catalog.AssetCount, Is.EqualTo(65));
            Assert.That(catalog.MissingClipCount, Is.Zero);
            string root = Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException("Project root unavailable.");
            string audioRoot = Path.Combine(root, "Assets", "_Project", "Audio",
                "OfficeSliceM5");
            string[] runtimeFiles = Directory.GetFiles(
                audioRoot, "*.wav", SearchOption.AllDirectories);
            long bytes = 0L;
            for (int i = 0; i < runtimeFiles.Length; i++)
                bytes += new FileInfo(runtimeFiles[i]).Length;

            Assert.That(runtimeFiles, Has.Length.EqualTo(65));
            Assert.That(bytes, Is.LessThanOrEqualTo(40L * 1024L * 1024L));
            Assert.That(Directory.Exists(Path.Combine(audioRoot, "Candidates")),
                Is.False);
            Assert.That(catalog.PcmMemoryEstimateBytes, Is.GreaterThan(0L));
        }

        [Test]
        public void AudioTelemetryRemainsBounded()
        {
            GameObject owner = new("M5 Gate F Telemetry Owner");
            try
            {
                var director = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), new OfficeAudioSettings());
                for (int i = 0; i < 96; i++)
                    director.PlayCue("event.copy-spawn");

                Assert.That(director.VoicePool.TotalSourceCount, Is.EqualTo(44));
                Assert.That(director.VoicePool.ActiveOneShotCount,
                    Is.LessThanOrEqualTo(OfficeAudioVoicePool.OneShotCapacity));
                Assert.That(director.VoicePool.PeakOneShotVoices,
                    Is.LessThanOrEqualTo(OfficeAudioVoicePool.OneShotCapacity));
                Assert.That(director.VoicePool.GrowthCount, Is.Zero);
                Assert.That(OfficeAudioVoicePool.ActiveRootCount(), Is.EqualTo(1));
                director.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SteadyAudioPresentationAllocatesZeroBytes()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            GameObject owner = new("M5 Gate F Allocation Owner");
            try
            {
                var director = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), new OfficeAudioSettings());
                director.ResetForState(campaign.CurrentSimulation, campaign);
                director.Apply(campaign.CurrentSimulation, campaign, 1f / 60f);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++)
                    director.Apply(campaign.CurrentSimulation, campaign, 1f / 60f);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(allocated, Is.Zero);
                director.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AudioMuteAndFeedbackModesShareCampaignChecksum()
        {
            OfficeCampaignState enabled = CompleteCampaign();
            OfficeCampaignState muted = CompleteCampaign();
            OfficeCampaignState deviceUnavailable = CompleteCampaign();
            OfficeCampaignState feedbackDisabled = CompleteCampaign();
            GameObject owner = new("M5 Gate F Determinism Owner");
            try
            {
                var enabledSettings = new OfficeAudioSettings();
                var mutedSettings = new OfficeAudioSettings();
                mutedSettings.SetAudioEnabled(false);
                var feedbackSettings = new OfficeAudioSettings();
                feedbackSettings.SetFeedbackEnabled(false);
                var unavailableSettings = new OfficeAudioSettings();
                unavailableSettings.SetAudioDeviceAvailable(false);
                var enabledDirector = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), enabledSettings);
                enabledDirector.Apply(enabled.CurrentSimulation, enabled, 1f / 60f);
                var mutedDirector = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), mutedSettings);
                mutedDirector.Apply(muted.CurrentSimulation, muted, 1f / 60f);
                var unavailableDirector = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), unavailableSettings);
                unavailableDirector.Apply(deviceUnavailable.CurrentSimulation,
                    deviceUnavailable, 1f / 60f);
                var feedbackDirector = new OfficeFeedbackDirector(owner.transform,
                    null, null, feedbackSettings);
                feedbackDirector.RouteCue("event.final-result", 1f);

                Assert.That(muted.Checksum, Is.EqualTo(enabled.Checksum));
                Assert.That(deviceUnavailable.Checksum, Is.EqualTo(enabled.Checksum));
                Assert.That(feedbackDisabled.Checksum, Is.EqualTo(enabled.Checksum));
                enabledDirector.Dispose();
                mutedDirector.Dispose();
                unavailableDirector.Dispose();
                feedbackDirector.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static OfficeCampaignState CompleteCampaign()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 3, "15-final-campaign-result");
            return campaign;
        }
    }
}
