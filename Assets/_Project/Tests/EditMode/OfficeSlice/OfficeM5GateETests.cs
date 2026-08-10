using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM5GateETests
    {
        [Test]
        public void FeedbackDisabledDoesNotChangeSimulation()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string checksum = campaign.Checksum;
            var settings = new OfficeAudioSettings();
            settings.SetFeedbackEnabled(false);
            GameObject owner = new("M5 Gate E Disabled Owner");
            try
            {
                var feedback = new OfficeFeedbackDirector(
                    owner.transform, null, null, settings);
                feedback.RouteCue("event.promotion-trigger", 1f);
                feedback.RouteCue("folder.take", 1f);
                feedback.Update(1f / 60f);

                Assert.That(feedback.FeedbackRequestCount, Is.Zero);
                Assert.That(feedback.CameraImpulse, Is.Zero);
                Assert.That(feedback.RumbleRequestCount, Is.Zero);
                Assert.That(campaign.Checksum, Is.EqualTo(checksum));
                feedback.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ControllerRumbleCanBeDisabled()
        {
            var settings = new OfficeAudioSettings();
            settings.SetRumble(false);
            GameObject owner = new("M5 Gate E Rumble Owner");
            try
            {
                var feedback = new OfficeFeedbackDirector(
                    owner.transform, null, null, settings);
                feedback.RouteCue("event.copy-echo-trigger", 1f);

                Assert.That(feedback.RumbleRequestCount, Is.Zero);
                Assert.That(feedback.RumbleActive, Is.False);
                Assert.That(feedback.CameraImpulse,
                    Is.EqualTo(OfficeFeedbackDirector.MaximumCameraImpulse));
                feedback.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BreakFeedbackDoesNotObscureRequiredInteractionTargets()
        {
            var settings = new OfficeAudioSettings();
            GameObject owner = new("M5 Gate E Break Owner");
            GameObject cameraObject = new("M5 Gate E Camera");
            cameraObject.transform.SetParent(owner.transform, false);
            try
            {
                var feedback = new OfficeFeedbackDirector(
                    owner.transform, cameraObject.transform, null, settings);
                feedback.RouteCue("event.promotion-trigger", 1f);
                feedback.Update(1f / 60f);

                Assert.That(feedback.CameraImpulse,
                    Is.LessThanOrEqualTo(OfficeFeedbackDirector.MaximumCameraImpulse));
                Assert.That(feedback.ObscuresInteractionTargets, Is.False);
                Assert.That(feedback.ObjectCount, Is.EqualTo(1));
                Assert.That(feedback.GrowthCount, Is.Zero);
                feedback.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void FeedbackRootsRemainBoundedAcrossRestart()
        {
            var settings = new OfficeAudioSettings();
            GameObject owner = new("M5 Gate E Root Owner");
            try
            {
                var first = new OfficeFeedbackDirector(
                    owner.transform, null, null, settings);
                first.RouteCue("folder.take", 1f);
                var second = new OfficeFeedbackDirector(
                    owner.transform, null, null, settings);

                Assert.That(OfficeFeedbackDirector.ActiveRootCount(), Is.EqualTo(1));
                Assert.That(first.Root.gameObject.activeSelf, Is.False);
                Assert.That(second.Root.gameObject.activeSelf, Is.True);
                Assert.That(second.ObjectCount, Is.EqualTo(1));
                Assert.That(second.GrowthCount, Is.Zero);
                second.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
