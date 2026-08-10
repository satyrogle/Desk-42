using System;
using System.IO;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM6GateFTests
    {
        [Test]
        public void TelemetryDisabledProducesIdenticalChecksum()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string before = campaign.Checksum;
            using var recorder = new OfficeM6TelemetryRecorder(
                false, string.Empty, "test-build");
            var observer = new OfficeM6TelemetryObserver(recorder);

            observer.Observe(campaign.CurrentSimulation, campaign,
                new OfficeM6Onboarding());

            Assert.That(recorder.Events, Is.Empty);
            Assert.That(campaign.Checksum, Is.EqualTo(before));
        }

        [Test]
        public void TelemetryEnabledProducesIdenticalChecksum()
        {
            string directory = NewDirectory();
            try
            {
                OfficeCampaignState observed = OfficeCampaignState.Create();
                OfficeCampaignState control = OfficeCampaignState.Create();
                OfficeCampaignCaptureDriver.Prepare(
                    observed, 1, "05-shift-1-copy-echo-break");
                OfficeCampaignCaptureDriver.Prepare(
                    control, 1, "05-shift-1-copy-echo-break");
                using var recorder = new OfficeM6TelemetryRecorder(
                    true, directory, "test-build");
                var observer = new OfficeM6TelemetryObserver(recorder);

                observer.Observe(observed.CurrentSimulation, observed,
                    new OfficeM6Onboarding());

                Assert.That(observed.Checksum, Is.EqualTo(control.Checksum));
                Assert.That(recorder.Events.Count, Is.GreaterThan(1));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void TelemetryContainsNoPersonalIdentifiers()
        {
            string directory = NewDirectory();
            try
            {
                string path;
                using (var recorder = new OfficeM6TelemetryRecorder(
                    true, directory, "test-build"))
                {
                    recorder.Record("first_meaningful_input", 1L, 1, "Move");
                    path = recorder.FilePath;
                }
                string json = File.ReadAllText(path);
                string[] forbidden =
                {
                    "player_name", "email", "ip_address", "microphone",
                    "machine_username", "machine_name", "free_form",
                };
                for (int i = 0; i < forbidden.Length; i++)
                    Assert.That(json, Does.Not.Contain(forbidden[i]).IgnoreCase);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void EvaluationSchemaIsVersioned()
        {
            string directory = NewDirectory();
            try
            {
                using var recorder = new OfficeM6TelemetryRecorder(
                    true, directory, "test-build");

                Assert.That(OfficeM6TelemetryRecorder.CurrentSchemaVersion,
                    Is.EqualTo(1));
                Assert.That(recorder.Events[0].SchemaVersion, Is.EqualTo(1));
                recorder.CloseNormal(0L, 1);
                Assert.That(File.ReadAllText(recorder.FilePath),
                    Does.Contain("\"schema_version\":1"));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void SessionFileClosesOnNormalExit()
        {
            string directory = NewDirectory();
            try
            {
                var recorder = new OfficeM6TelemetryRecorder(
                    true, directory, "test-build");
                string path = recorder.FilePath;
                recorder.CloseNormal(30L, 1);

                Assert.That(recorder.Closed, Is.True);
                Assert.DoesNotThrow(() =>
                {
                    using FileStream stream = File.Open(
                        path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                });
                Assert.That(File.ReadAllText(path),
                    Does.Contain("\"event\":\"session_end\""));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void RestartDoesNotOverwritePreviousSession()
        {
            string directory = NewDirectory();
            try
            {
                string first;
                using (var recorder = new OfficeM6TelemetryRecorder(
                    true, directory, "test-build"))
                    first = recorder.FilePath;
                string second;
                using (var recorder = new OfficeM6TelemetryRecorder(
                    true, directory, "test-build"))
                    second = recorder.FilePath;

                Assert.That(second, Is.Not.EqualTo(first));
                Assert.That(File.Exists(first), Is.True);
                Assert.That(File.Exists(second), Is.True);
                Assert.That(Directory.GetFiles(directory, "*.jsonl").Length,
                    Is.EqualTo(2));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static string NewDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Desk42-M6-Telemetry-Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
