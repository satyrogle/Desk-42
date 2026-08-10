using System;
using System.IO;
using Desk42.Product.OfficeSlice;
using Desk42.Tests.EditMode.OfficeM3;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM6GateGTests
    {
        [Test]
        public void EvaluationModeStartsAtShiftOne()
        {
            var mode = EvaluationMode();
            OfficeCampaignState campaign = OfficeCampaignState.Create();

            Assert.That(mode.Enabled, Is.True);
            Assert.That(mode.StartingShiftOrdinal, Is.EqualTo(1));
            Assert.That(campaign.CurrentShiftOrdinal,
                Is.EqualTo(mode.StartingShiftOrdinal));
            Assert.That(campaign.CurrentSimulation.CurrentTick, Is.Zero);
        }

        [Test]
        public void EvaluationModeHasNoDeveloperShortcuts()
        {
            var mode = EvaluationMode();
            var presenter = new OfficeM6HudPresenter();

            Assert.That(mode.DeveloperShortcutsAllowed, Is.False);
            Assert.That(presenter.DevelopmentHudVisible, Is.False);
            Assert.That(mode.ForceFreshOnboarding, Is.True);
        }

        [Test]
        public void EvaluationModeUsesPlayerHud()
        {
            var mode = EvaluationMode();
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(mode.UsesPlayerHud, Is.True);
            Assert.That(mode.UsesM4VisualTarget, Is.True);
            Assert.That(mode.UsesM5AudioFeedback, Is.True);
            Assert.That(model.ActionPrompt, Does.StartWith("E - "));
            Assert.That(model.DevelopmentHudVisible, Is.False);
        }

        [Test]
        public void EvaluationModeWritesSessionTelemetry()
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "Desk42-M6-Evaluation-Mode-Tests",
                Guid.NewGuid().ToString("N"));
            try
            {
                var mode = EvaluationMode();
                using var recorder = new OfficeM6TelemetryRecorder(
                    mode.TelemetryEnabled,
                    directory,
                    OfficeM6EvaluationMode.BuildIdentifier);
                recorder.CloseNormal(30L, 1);

                Assert.That(recorder.Enabled, Is.True);
                Assert.That(File.Exists(recorder.FilePath), Is.True);
                string json = File.ReadAllText(recorder.FilePath);
                Assert.That(json, Does.Contain("session_start"));
                Assert.That(json, Does.Contain("session_end"));
                Assert.That(json,
                    Does.Contain(OfficeM6EvaluationMode.BuildIdentifier));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void EvaluationModeCompletesThreeShiftCampaign()
        {
            OfficeM6EvaluationMode mode = EvaluationMode();
            OfficeCampaignState campaign = OfficeCampaignState.Create();

            OfficeM3TestDriver.DriveCampaignToResult(campaign);

            Assert.That(mode.Enabled, Is.True);
            Assert.That(campaign.IsComplete, Is.True);
            Assert.That(campaign.CompletedShiftSummaries.Count, Is.EqualTo(3));
            Assert.That(campaign.Checksum, Is.EqualTo("B42CFA89D6277EA2"));
        }

        private static OfficeM6EvaluationMode EvaluationMode()
        {
            return new OfficeM6EvaluationMode(new[]
            {
                OfficeM6EvaluationMode.LaunchArgument,
            });
        }
    }
}
