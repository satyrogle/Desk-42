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
        public OfficeInputDirection VisualMovement =>
            _inputIntent?.Movement ?? OfficeInputDirection.None;

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
            bool newRunPressed = _bootstrap.EvaluationMode.Enabled &&
                _bootstrap.CampaignState.IsComplete &&
                ((keyboard != null && keyboard.enterKey.wasPressedThisFrame) ||
                 (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame));
            if (newRunPressed)
            {
                _bootstrap.NotifyControlScheme(
                    gamepad != null && gamepad.buttonSouth.wasPressedThisFrame
                        ? OfficeM6ControlScheme.Controller
                        : OfficeM6ControlScheme.Keyboard);
                _bootstrap.StartNewEvaluationRun();
                return;
            }
            if (HandlePauseMenuInput(keyboard, gamepad))
            {
                _bootstrap.RefreshPresentation();
                return;
            }
            if (_bootstrap.PauseController.Paused) return;
            SampleDeviceIntent(keyboard, gamepad);

            if (keyboard != null && _bootstrap.DeveloperShortcutsAllowed)
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

        private bool HandlePauseMenuInput(Keyboard keyboard, Gamepad gamepad)
        {
            bool keyboardToggle = keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame;
            bool controllerToggle = gamepad != null &&
                gamepad.startButton.wasPressedThisFrame;
            if (keyboardToggle || controllerToggle)
            {
                _bootstrap.NotifyControlScheme(controllerToggle
                    ? OfficeM6ControlScheme.Controller
                    : OfficeM6ControlScheme.Keyboard);
                _clock.SetPaused(_bootstrap.TogglePauseMenu());
                return true;
            }
            if (!_bootstrap.PauseController.Paused) return false;

            int vertical = 0;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame) vertical = -1;
                else if (keyboard.downArrowKey.wasPressedThisFrame) vertical = 1;
            }
            if (vertical == 0 && gamepad != null)
            {
                if (gamepad.dpad.up.wasPressedThisFrame) vertical = -1;
                else if (gamepad.dpad.down.wasPressedThisFrame) vertical = 1;
            }
            if (vertical != 0)
                _bootstrap.NavigatePauseMenu(vertical);

            int horizontal = 0;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) horizontal = -1;
                else if (keyboard.rightArrowKey.wasPressedThisFrame) horizontal = 1;
            }
            if (horizontal == 0 && gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame) horizontal = -1;
                else if (gamepad.dpad.right.wasPressedThisFrame) horizontal = 1;
            }
            if (horizontal != 0)
                _bootstrap.AdjustPauseSetting(horizontal);

            bool confirm = keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame);
            confirm |= gamepad != null &&
                gamepad.buttonSouth.wasPressedThisFrame;
            if (confirm)
                _clock.SetPaused(_bootstrap.ConfirmPauseMenu());

            if (gamepad != null && (vertical != 0 || horizontal != 0 || confirm))
                _bootstrap.NotifyControlScheme(OfficeM6ControlScheme.Controller);
            else if (vertical != 0 || horizontal != 0 || confirm)
                _bootstrap.NotifyControlScheme(OfficeM6ControlScheme.Keyboard);
            return true;
        }

        private void SampleDeviceIntent(Keyboard keyboard, Gamepad gamepad)
        {
            if (_state.ReplayMode)
            {
                _inputIntent.Clear();
                return;
            }

            OfficeInputDirection movement = OfficeInputDirection.None;
            bool keyboardUsed = false;
            bool controllerUsed = false;
            if (keyboard != null)
            {
                movement = OfficeInputCanonicalizer.FromDigital(
                    keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                    keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                    keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                    keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
                keyboardUsed = movement != OfficeInputDirection.None;
            }

            if (movement == OfficeInputDirection.None && gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                Vector2 dpad = gamepad.dpad.ReadValue();
                Vector2 direction = stick.sqrMagnitude >= dpad.sqrMagnitude ? stick : dpad;
                movement = OfficeInputCanonicalizer.FromAnalog(direction.x, direction.y);
                controllerUsed = movement != OfficeInputDirection.None;
            }
            _inputIntent.SetMovement(movement);

            bool keyboardInteractionPressed = keyboard != null &&
                (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
            bool controllerInteractionPressed =
                gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
            keyboardUsed |= keyboardInteractionPressed;
            controllerUsed |= controllerInteractionPressed;
            bool interactionPressed = keyboardInteractionPressed ||
                controllerInteractionPressed;
            if (interactionPressed)
            {
                _bootstrap.NotifyPresentationInteractionAttempt(
                    _state.PrimaryActionLabel != "MOVE TO A WORK POINT");
                _inputIntent.BufferInteraction(_state.CurrentTick);
            }

            int choice = 0;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) choice = 1;
                else if (keyboard.digit2Key.wasPressedThisFrame) choice = 2;
                else if (keyboard.digit3Key.wasPressedThisFrame) choice = 3;
                else if (keyboard.digit4Key.wasPressedThisFrame) choice = 4;
                keyboardUsed |= choice > 0;
            }
            if (choice == 0 && gamepad != null)
            {
                if (gamepad.buttonWest.wasPressedThisFrame) choice = 1;
                else if (gamepad.buttonNorth.wasPressedThisFrame) choice = 2;
                else if (gamepad.leftShoulder.wasPressedThisFrame) choice = 3;
                else if (gamepad.rightShoulder.wasPressedThisFrame) choice = 4;
                controllerUsed |= choice > 0;
            }
            if (choice > 0) _inputIntent.BufferChoice(choice, _state.CurrentTick);

            bool keyboardDropPressed =
                keyboard != null && keyboard.qKey.wasPressedThisFrame;
            bool controllerDropPressed =
                gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            keyboardUsed |= keyboardDropPressed;
            controllerUsed |= controllerDropPressed;
            bool dropPressed = keyboardDropPressed || controllerDropPressed;
            if (dropPressed) _inputIntent.BufferDrop(_state.CurrentTick);

            bool keyboardRulePressed =
                keyboard != null && keyboard.rKey.wasPressedThisFrame;
            bool controllerRulePressed =
                gamepad != null && gamepad.selectButton.wasPressedThisFrame;
            keyboardUsed |= keyboardRulePressed;
            controllerUsed |= controllerRulePressed;
            bool toggleRulePressed = keyboardRulePressed || controllerRulePressed;
            if (toggleRulePressed) _inputIntent.BufferToggleRule(_state.CurrentTick);

            bool keyboardRule2Pressed = keyboard != null &&
                keyboard.tKey.wasPressedThisFrame;
            bool controllerRule2Pressed = gamepad != null &&
                gamepad.rightStickButton.wasPressedThisFrame;
            keyboardUsed |= keyboardRule2Pressed;
            controllerUsed |= controllerRule2Pressed;
            bool toggleRule2Pressed = keyboardRule2Pressed ||
                controllerRule2Pressed;
            if (toggleRule2Pressed)
                _inputIntent.BufferToggleRule2(_state.CurrentTick);

            bool keyboardRestartPressed =
                keyboard != null && keyboard.enterKey.wasPressedThisFrame;
            bool controllerRestartPressed =
                gamepad != null && gamepad.startButton.wasPressedThisFrame;
            keyboardUsed |= keyboardRestartPressed;
            controllerUsed |= controllerRestartPressed;
            bool restartPressed = keyboardRestartPressed ||
                controllerRestartPressed;
            if (restartPressed) _inputIntent.BufferRestart(_state.CurrentTick);

            bool keyboardWhatHappenedPressed = keyboard != null &&
                keyboard.hKey.wasPressedThisFrame;
            bool controllerWhatHappenedPressed = gamepad != null &&
                gamepad.buttonNorth.wasPressedThisFrame &&
                !_state.ManualTasks.IsActive;
            keyboardUsed |= keyboardWhatHappenedPressed;
            controllerUsed |= controllerWhatHappenedPressed;
            bool whatHappenedPressed = keyboardWhatHappenedPressed ||
                controllerWhatHappenedPressed;
            if (whatHappenedPressed)
                _bootstrap.ToggleWhatHappened();

            if (keyboardUsed)
                _bootstrap.NotifyControlScheme(OfficeM6ControlScheme.Keyboard);
            else if (controllerUsed)
                _bootstrap.NotifyControlScheme(OfficeM6ControlScheme.Controller);
        }
    }
}
