using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM6GateHTests
    {
        private static readonly (int Shift, string State)[] CaptureMatrix =
        {
            (1, "01-first-launch-first-customer"),
            (1, "02-first-paper-check"),
            (1, "03-first-money-trace"),
            (1, "04-first-decision"),
            (1, "05-auto-sorter-active"),
            (1, "06-copy-echo-warning"),
            (1, "07-copy-echo-break"),
            (1, "08-copy-echo-recovery"),
            (1, "09-shift-1-result-upgrade"),
            (2, "10-shift-2-opening"),
            (2, "11-ghost-clock"),
            (2, "12-rule-2-active"),
            (2, "13-shift-2-result"),
            (3, "14-shift-3-opening"),
            (3, "15-promotion-cascade"),
            (3, "16-promotion-recovery"),
            (3, "17-final-campaign-result"),
            (3, "18-next-day-tease"),
            (1, "19-pause-settings"),
            (1, "20-what-happened"),
        };

        [Test]
        public void EvaluationCaptureMatrixUsesCanonicalCampaignFlow()
        {
            for (int i = 0; i < CaptureMatrix.Length; i++)
            {
                OfficeCampaignState campaign = OfficeCampaignState.Create();
                OfficeCampaignCaptureDriver.Prepare(
                    campaign, CaptureMatrix[i].Shift, CaptureMatrix[i].State);

                Assert.That(campaign.CurrentShiftOrdinal,
                    Is.EqualTo(CaptureMatrix[i].Shift), CaptureMatrix[i].State);
                Assert.That(campaign.CurrentSimulation.Queues
                    .HasSingleLogicalOwnerForEveryFolder(),
                    Is.True, CaptureMatrix[i].State);
            }
        }

        [Test]
        public void PromotionRecoveryChecklistFitsAtMaximumTextScale()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 3, "15-promotion-cascade");
            var presenter = new OfficeM6HudPresenter();
            OfficeM6HudModel model = presenter.Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(model.RecoveryItems.Count, Is.EqualTo(6));
            Assert.That(presenter.RecoveryRowCount(model), Is.EqualTo(3));
            Assert.That(presenter.BreakContentFits(model, 1.3f), Is.True);
        }
    }
}
