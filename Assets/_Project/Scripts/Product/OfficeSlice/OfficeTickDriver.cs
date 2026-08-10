using UnityEngine;
using UnityEngine.InputSystem;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeTickDriver : MonoBehaviour
    {
        private OfficeSliceBootstrap _bootstrap;
        private OfficeSimulationState _state;
        private OfficeSimulationClock _clock;
        private OfficeInputIntent _inputIntent;
        private OfficeInputCommandGenerator _inputCommandGenerator;
        private bool _initialized;

        public OfficeSimulationClock Clock => _clock;

        public void Initialize(OfficeSliceBootstrap bootstrap, OfficeSimulationState state)
        {
            _bootstrap = bootstrap;
            _state = state;
            _clock = new OfficeSimulationClock();
            _inputIntent = new OfficeInputIntent();
            _inputCommandGenerator = new OfficeInputCommandGenerator(_state, _inputIntent);
            _initialized = true;
        }

        public void ReplaceState(OfficeSimulationState state, bool paused = true)
        {
            _state = state;
            _clock = new OfficeSimulationClock();
            _clock.SetPaused(paused);
            _inputIntent = new OfficeInputIntent();
            _inputCommandGenerator = new OfficeInputCommandGenerator(_state, _inputIntent);
        }

        private void Update()
        {
            if (!_initialized || _state == null) return;
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            SampleDeviceIntent(keyboard, gamepad);

            if (keyboard != null)
            {
                if (keyboard.pKey.wasPressedThisFrame)
                    _clock.SetPaused(!_clock.Paused);
                if (keyboard.nKey.wasPressedThisFrame)
                {
                    _clock.SetPaused(true);
                    _clock.Step(_inputCommandGenerator.AdvanceOneTick);
                    _bootstrap.RefreshPresentation();
                    return;
                }
                if (keyboard.f5Key.wasPressedThisFrame) _bootstrap.SaveCommandLog();
                if (keyboard.f6Key.wasPressedThisFrame)
                    _bootstrap.ForceAllFoldersThroughM1Route();
                if (keyboard.f7Key.wasPressedThisFrame)
                    _bootstrap.ReplayRecordedCommands();
            }

            _clock.Advance(Time.unscaledDeltaTime, _inputCommandGenerator.AdvanceOneTick);
            _bootstrap.SynchronizeCampaignState();
            if (_state.M2Enabled && _state.Shift.RestartRequested)
            {
                _bootstrap.RestartShift();
                return;
            }
            _bootstrap.RefreshPresentation();
        }

        private void SampleDeviceIntent(Keyboard keyboard, Gamepad gamepad)
        {
            if (_state.ReplayMode)
            {
                _inputIntent.Clear();
                return;
            }

            OfficeInputDirection movement = OfficeInputDirection.None;
            if (keyboard != null)
            {
                movement = OfficeInputCanonicalizer.FromDigital(
                    keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                    keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                    keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                    keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
            }

            if (movement == OfficeInputDirection.None && gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                Vector2 dpad = gamepad.dpad.ReadValue();
                Vector2 direction = stick.sqrMagnitude >= dpad.sqrMagnitude ? stick : dpad;
                movement = OfficeInputCanonicalizer.FromAnalog(direction.x, direction.y);
            }
            _inputIntent.SetMovement(movement);

            bool interactionPressed =
                keyboard != null &&
                (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
            interactionPressed |= gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
            if (interactionPressed) _inputIntent.BufferInteraction(_state.CurrentTick);

            int choice = 0;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) choice = 1;
                else if (keyboard.digit2Key.wasPressedThisFrame) choice = 2;
                else if (keyboard.digit3Key.wasPressedThisFrame) choice = 3;
                else if (keyboard.digit4Key.wasPressedThisFrame) choice = 4;
            }
            if (choice == 0 && gamepad != null)
            {
                if (gamepad.buttonWest.wasPressedThisFrame) choice = 1;
                else if (gamepad.buttonNorth.wasPressedThisFrame) choice = 2;
                else if (gamepad.leftShoulder.wasPressedThisFrame) choice = 3;
                else if (gamepad.rightShoulder.wasPressedThisFrame) choice = 4;
            }
            if (choice > 0) _inputIntent.BufferChoice(choice, _state.CurrentTick);

            bool dropPressed = keyboard != null && keyboard.qKey.wasPressedThisFrame;
            dropPressed |= gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            if (dropPressed) _inputIntent.BufferDrop(_state.CurrentTick);

            bool toggleRulePressed = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            toggleRulePressed |= gamepad != null && gamepad.selectButton.wasPressedThisFrame;
            if (toggleRulePressed) _inputIntent.BufferToggleRule(_state.CurrentTick);

            bool restartPressed = keyboard != null && keyboard.enterKey.wasPressedThisFrame;
            restartPressed |= gamepad != null && gamepad.startButton.wasPressedThisFrame;
            if (restartPressed) _inputIntent.BufferRestart(_state.CurrentTick);
        }
    }
}
