using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM6GateETests
    {
        [Test]
        public void SettingsPersistOutsideCampaignState()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string checksum = campaign.Checksum;
            var store = new OfficeM6MemorySettingsStore();
            var savedAudio = new OfficeAudioSettings();
            var saved = new OfficeM6PresentationSettings(savedAudio, store);
            savedAudio.SetVolumes(0.65f, 0.45f, 0.75f, 0.35f);
            savedAudio.SetRumble(false);
            savedAudio.SetReducedFlash(true);
            saved.SetTutorialHints(false);
            saved.SetTextScale(OfficeM6TextScale.Maximum);
            saved.SetFullscreen(false);
            saved.SetResolutionIndex(2);
            saved.Save();

            var loadedAudio = new OfficeAudioSettings();
            var loaded = new OfficeM6PresentationSettings(loadedAudio, store);
            loaded.Load();

            Assert.That(loadedAudio.Master, Is.EqualTo(0.65f).Within(0.001f));
            Assert.That(loadedAudio.Music, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(loadedAudio.Sfx, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(loadedAudio.Ambience, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(loadedAudio.Rumble, Is.False);
            Assert.That(loadedAudio.ReducedFlash, Is.True);
            Assert.That(loaded.TutorialHints, Is.False);
            Assert.That(loaded.TextScale, Is.EqualTo(OfficeM6TextScale.Maximum));
            Assert.That(loaded.Fullscreen, Is.False);
            Assert.That(loaded.Resolution.ToString(), Is.EqualTo("1920x1080"));
            Assert.That(campaign.Checksum, Is.EqualTo(checksum));
        }

        [Test]
        public void TextScaleDoesNotClipCriticalHudAt1280x720()
        {
            var settings = new OfficeM6PresentationSettings(
                new OfficeAudioSettings(), new OfficeM6MemorySettingsStore());
            settings.SetTextScale(OfficeM6TextScale.Maximum);

            Assert.That(new OfficeM6HudPresenter().FitsAtTextScale(
                1280, 720, settings.TextScaleMultiplier), Is.True);
        }

        [Test]
        public void ReducedFlashDoesNotChangeSimulation()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string checksum = campaign.Checksum;
            var settings = new OfficeM6PresentationSettings(
                new OfficeAudioSettings(), new OfficeM6MemorySettingsStore());

            settings.Audio.SetReducedFlash(true);

            Assert.That(settings.Audio.ReducedFlash, Is.True);
            Assert.That(campaign.Checksum, Is.EqualTo(checksum));
        }

        [Test]
        public void RumbleOffDoesNotChangeSimulation()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string checksum = campaign.Checksum;
            var settings = new OfficeM6PresentationSettings(
                new OfficeAudioSettings(), new OfficeM6MemorySettingsStore());

            settings.Audio.SetRumble(false);

            Assert.That(settings.Audio.Rumble, Is.False);
            Assert.That(campaign.Checksum, Is.EqualTo(checksum));
        }

        [Test]
        public void PauseDoesNotAdvanceSimulation()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            var clock = new OfficeSimulationClock();
            clock.SetPaused(true);

            int executed = clock.Advance(10d, state.AdvanceOneTick);

            Assert.That(executed, Is.Zero);
            Assert.That(state.CurrentTick, Is.Zero);
            Assert.That(state.CommandLog.Commands, Is.Empty);
        }

        [Test]
        public void ResumePreservesCommandOrder()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            var clock = new OfficeSimulationClock();
            intent.SetMovement(OfficeInputDirection.Right);
            intent.BufferInteraction(state.CurrentTick);
            clock.SetPaused(true);
            clock.Advance(1d, input.AdvanceOneTick);

            clock.SetPaused(false);
            clock.Advance(OfficeSimulationClock.TickDurationSeconds,
                input.AdvanceOneTick);

            Assert.That(state.CommandLog.Commands.Count, Is.EqualTo(2));
            Assert.That(state.CommandLog.Commands[0].Kind,
                Is.EqualTo(OfficeCommandKind.Move));
            Assert.That(state.CommandLog.Commands[1].Kind,
                Is.EqualTo(OfficeCommandKind.Interact));
            Assert.That(state.CommandLog.Commands[0].Sequence,
                Is.LessThan(state.CommandLog.Commands[1].Sequence));
        }

        [Test]
        public void ControllerNavigatesPauseAndSettings()
        {
            var pause = new OfficeM6PauseController();
            pause.Toggle();
            pause.MoveSelection(1);
            OfficeM6MenuAction openSettings = pause.Confirm();
            pause.Apply(openSettings);

            Assert.That(pause.Paused, Is.True);
            Assert.That(pause.Page, Is.EqualTo(OfficeM6MenuPage.Settings));
            Assert.That(pause.Selection, Is.Zero);

            pause.MoveSelection(-1);
            Assert.That(pause.Selection, Is.EqualTo(10));
            OfficeM6MenuAction back = pause.Confirm();
            pause.Apply(back);
            Assert.That(pause.Page, Is.EqualTo(OfficeM6MenuPage.Pause));
        }
    }
}
