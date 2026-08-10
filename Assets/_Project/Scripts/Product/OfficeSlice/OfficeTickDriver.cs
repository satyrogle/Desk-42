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

        public void ReplaceState(OfficeSimulationState state)
        {
            _state = state;
            _clock = new OfficeSimulationClock();
            _clock.SetPaused(true);
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
        }
    }
}
