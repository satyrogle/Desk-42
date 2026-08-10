using UnityEngine;
using UnityEngine.InputSystem;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeTickDriver : MonoBehaviour
    {
        private OfficeSliceBootstrap _bootstrap;
        private OfficeSimulationState _state;
        private OfficeSimulationClock _clock;
        private bool _initialized;

        public OfficeSimulationClock Clock => _clock;

        public void Initialize(OfficeSliceBootstrap bootstrap, OfficeSimulationState state)
        {
            _bootstrap = bootstrap;
            _state = state;
            _clock = new OfficeSimulationClock();
            _initialized = true;
        }

        public void ReplaceState(OfficeSimulationState state)
        {
            _state = state;
            _clock = new OfficeSimulationClock();
            _clock.SetPaused(true);
        }

        private void Update()
        {
            if (!_initialized || _state == null) return;
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            if (keyboard != null)
            {
                if (keyboard.pKey.wasPressedThisFrame)
                    _clock.SetPaused(!_clock.Paused);
                if (keyboard.nKey.wasPressedThisFrame)
                {
                    _clock.SetPaused(true);
                    _clock.Step(_state.AdvanceOneTick);
                    _bootstrap.RefreshPresentation();
                    return;
                }
                if (keyboard.f5Key.wasPressedThisFrame) _bootstrap.SaveCommandLog();
                if (keyboard.f6Key.wasPressedThisFrame)
                    _bootstrap.ForceAllFoldersThroughM1Route();
                if (keyboard.f7Key.wasPressedThisFrame)
                    _bootstrap.ReplayRecordedCommands();

                if (!TryReadKeyboardDirection(keyboard, out int x, out int z))
                {
                    x = 0;
                    z = 0;
                }
                if (!_state.ReplayMode && (x != 0 || z != 0))
                    _state.TryQueueCommand(
                        _state.CreateMoveCommand(x, z), out OfficeCommandFailure ignored);
                if (!_state.ReplayMode &&
                    (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                    _state.TryQueueCommand(
                        _state.CreateInteractCommand(), out OfficeCommandFailure ignored);
            }

            if (gamepad != null && !_state.ReplayMode)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                Vector2 dpad = gamepad.dpad.ReadValue();
                Vector2 direction = stick.sqrMagnitude >= dpad.sqrMagnitude ? stick : dpad;
                if (Mathf.Abs(direction.x) > 0.35f || Mathf.Abs(direction.y) > 0.35f)
                {
                    int x = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                        ? (direction.x >= 0f ? 1 : -1)
                        : 0;
                    int z = x == 0 ? (direction.y >= 0f ? 1 : -1) : 0;
                    _state.TryQueueCommand(
                        _state.CreateMoveCommand(x, z), out OfficeCommandFailure ignored);
                }
                if (gamepad.buttonSouth.wasPressedThisFrame)
                    _state.TryQueueCommand(
                        _state.CreateInteractCommand(), out OfficeCommandFailure ignored);
            }

            _clock.Advance(Time.unscaledDeltaTime, _state.AdvanceOneTick);
            _bootstrap.RefreshPresentation();
        }

        private static bool TryReadKeyboardDirection(Keyboard keyboard, out int x, out int z)
        {
            x = 0;
            z = 0;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x--;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x++;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) z--;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) z++;
            if (x != 0 && z != 0) z = 0;
            return x != 0 || z != 0;
        }
    }
}
