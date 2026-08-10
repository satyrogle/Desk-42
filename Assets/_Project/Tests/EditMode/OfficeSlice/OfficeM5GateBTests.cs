using System.Collections.Generic;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM5GateBTests
    {
        [Test]
        public void EveryPrimaryActionHasMappedCue()
        {
            OfficeAudioCueCatalog catalog = OfficeAudioCueCatalog.Load();
            var commands = new[]
            {
                OfficeCommand.Move(6, 1, 1, 0),
                OfficeCommand.Interact(7, 2),
                OfficeCommand.Carry(8, 3, "folder"),
                OfficeCommand.Drop(9, 4),
                OfficeCommand.Send(10, 5, "folder"),
                OfficeCommand.StartWork(11, 6, "case", OfficeManualTaskKind.Compare),
                OfficeCommand.StartWork(12, 7, "case", OfficeManualTaskKind.Trace),
                OfficeCommand.SubmitWorkChoice(13, 8, 1),
                OfficeCommand.Help(14, 9, "job"),
                OfficeCommand.Calm(15, 10, "customer"),
                OfficeCommand.Fix(16, 11),
                OfficeCommand.Decide(17, 12, "case"),
                OfficeCommand.ContinueToNextShift(18, 13),
            };

            foreach (OfficeCommand command in commands)
            {
                string cue = OfficeAudioEventRouter.CueForCommand(command);
                Assert.That(cue, Is.Not.Empty, command.Kind.ToString());
                Assert.That(catalog.ContainsCue(cue), Is.True, cue);
            }

            Assert.That(catalog.ContainsCue("action.invalid"), Is.True);
        }

        [Test]
        public void PaperResultsAreAudiblyDistinct()
        {
            string correct = OfficeAudioEventRouter.CueForManualResult(
                OfficeManualTaskKind.Compare, true);
            string incorrect = OfficeAudioEventRouter.CueForManualResult(
                OfficeManualTaskKind.Compare, false);

            Assert.That(correct, Is.Not.EqualTo(incorrect));
            AssertResolvableAndDistinct(correct, incorrect);
        }

        [Test]
        public void MoneyResultsAreAudiblyDistinct()
        {
            string correct = OfficeAudioEventRouter.CueForManualResult(
                OfficeManualTaskKind.Trace, true);
            string incorrect = OfficeAudioEventRouter.CueForManualResult(
                OfficeManualTaskKind.Trace, false);

            Assert.That(correct, Is.Not.EqualTo(incorrect));
            AssertResolvableAndDistinct(correct, incorrect);
        }

        [Test]
        public void InvalidActionHasNonSuccessCue()
        {
            OfficeAudioCueCatalog catalog = OfficeAudioCueCatalog.Load();
            Assert.That(catalog.TryResolve("action.invalid", out OfficeAudioCueRecord invalid,
                out AudioClip invalidClip), Is.True);
            Assert.That(catalog.TryResolve("choice.confirm", out OfficeAudioCueRecord success,
                out AudioClip successClip), Is.True);
            Assert.That(invalid.asset_id, Is.Not.EqualTo(success.asset_id));
            Assert.That(invalidClip, Is.Not.SameAs(successClip));
        }

        [Test]
        public void PrimaryCueRoutingDoesNotChangeCommandLog()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeSimulationState state = campaign.CurrentSimulation;
            int commandCount = state.CommandLog.Commands.Count;
            string checksum = campaign.Checksum;
            GameObject owner = new("M5 Gate B Audio Owner");
            try
            {
                var director = new OfficeAudioDirector(owner.transform,
                    OfficeAudioCueCatalog.Load(), new OfficeAudioSettings());
                var commands = new List<OfficeCommand>
                {
                    OfficeCommand.Move(6, 1, 1, 0),
                    OfficeCommand.Carry(7, 2, "folder"),
                    OfficeCommand.Drop(8, 3),
                    OfficeCommand.Send(9, 4, "folder"),
                    OfficeCommand.Help(10, 5, "job"),
                    OfficeCommand.Calm(11, 6, "customer"),
                    OfficeCommand.Fix(12, 7),
                    OfficeCommand.Decide(13, 8, "case"),
                };

                foreach (OfficeCommand command in commands)
                    director.PlayCue(OfficeAudioEventRouter.CueForCommand(command));

                director.NotifyInteractionAttempt(false);
                Assert.That(state.CommandLog.Commands.Count, Is.EqualTo(commandCount));
                Assert.That(campaign.Checksum, Is.EqualTo(checksum));
                director.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void AssertResolvableAndDistinct(string first, string second)
        {
            OfficeAudioCueCatalog catalog = OfficeAudioCueCatalog.Load();
            Assert.That(catalog.TryResolve(first, out OfficeAudioCueRecord firstCue,
                out AudioClip firstClip), Is.True, first);
            Assert.That(catalog.TryResolve(second, out OfficeAudioCueRecord secondCue,
                out AudioClip secondClip), Is.True, second);
            Assert.That(firstCue.asset_id, Is.Not.EqualTo(secondCue.asset_id));
            Assert.That(firstClip, Is.Not.SameAs(secondClip));
        }
    }
}
