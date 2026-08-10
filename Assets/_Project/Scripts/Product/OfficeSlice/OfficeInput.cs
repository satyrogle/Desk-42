using System;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeInputDirection
    {
        None,
        Left,
        Right,
        Down,
        Up,
    }

    public static class OfficeInputCanonicalizer
    {
        public const float DefaultAnalogThreshold = 0.35f;

        public static OfficeInputDirection FromDigital(
            bool left,
            bool right,
            bool down,
            bool up)
        {
            int x = (right ? 1 : 0) - (left ? 1 : 0);
            int z = (up ? 1 : 0) - (down ? 1 : 0);
            return FromAxes(x, z, 0f);
        }

        public static OfficeInputDirection FromAnalog(
            float x,
            float z,
            float threshold = DefaultAnalogThreshold)
        {
            if (threshold < 0f) throw new ArgumentOutOfRangeException(nameof(threshold));
            return FromAxes(x, z, threshold);
        }

        public static void ToAxes(OfficeInputDirection direction, out int x, out int z)
        {
            x = 0;
            z = 0;
            switch (direction)
            {
                case OfficeInputDirection.Left:
                    x = -1;
                    break;
                case OfficeInputDirection.Right:
                    x = 1;
                    break;
                case OfficeInputDirection.Down:
                    z = -1;
                    break;
                case OfficeInputDirection.Up:
                    z = 1;
                    break;
            }
        }

        private static OfficeInputDirection FromAxes(float x, float z, float threshold)
        {
            float absoluteX = Math.Abs(x);
            float absoluteZ = Math.Abs(z);
            if (absoluteX <= threshold && absoluteZ <= threshold)
                return OfficeInputDirection.None;

            // Horizontal wins exact ties for both digital and analog devices.
            if (absoluteX >= absoluteZ)
                return x >= 0f ? OfficeInputDirection.Right : OfficeInputDirection.Left;
            return z >= 0f ? OfficeInputDirection.Up : OfficeInputDirection.Down;
        }
    }

    /// <summary>
    /// Canonical device intent sampled at render frequency. Held movement remains
    /// state, while an interaction press is a one-shot buffer measured only in
    /// simulation ticks.
    /// </summary>
    public sealed class OfficeInputIntent
    {
        public const int InteractionBufferTicks = 6;

        private bool _interactionBuffered;
        private long _interactionExpiresAfterTick;
        private bool _choiceBuffered;
        private int _bufferedChoice;
        private long _choiceExpiresAfterTick;
        private bool _dropBuffered;
        private long _dropExpiresAfterTick;

        public OfficeInputDirection Movement { get; private set; }
        public bool HasBufferedInteraction => _interactionBuffered;
        public long InteractionExpiresAfterTick => _interactionExpiresAfterTick;
        public bool HasBufferedChoice => _choiceBuffered;

        public void SetMovement(OfficeInputDirection movement)
        {
            Movement = movement;
        }

        public void BufferInteraction(long currentTick)
        {
            if (currentTick < 0L) throw new ArgumentOutOfRangeException(nameof(currentTick));
            if (_interactionBuffered) return;
            if (currentTick > long.MaxValue - InteractionBufferTicks)
                throw new ArgumentOutOfRangeException(nameof(currentTick));

            _interactionBuffered = true;
            _interactionExpiresAfterTick = currentTick + InteractionBufferTicks;
        }

        public bool TryGetMovement(out int x, out int z)
        {
            OfficeInputCanonicalizer.ToAxes(Movement, out x, out z);
            return Movement != OfficeInputDirection.None;
        }

        public bool TryConsumeInteraction(long commandTick)
        {
            if (commandTick < 0L) throw new ArgumentOutOfRangeException(nameof(commandTick));
            if (!_interactionBuffered) return false;
            if (commandTick > _interactionExpiresAfterTick)
            {
                ClearInteraction();
                return false;
            }

            ClearInteraction();
            return true;
        }

        public void BufferChoice(int oneBasedChoice, long currentTick)
        {
            if (oneBasedChoice < 1 || oneBasedChoice > 4)
                throw new ArgumentOutOfRangeException(nameof(oneBasedChoice));
            if (currentTick < 0L) throw new ArgumentOutOfRangeException(nameof(currentTick));
            if (_choiceBuffered) return;
            _choiceBuffered = true;
            _bufferedChoice = oneBasedChoice;
            _choiceExpiresAfterTick = currentTick + InteractionBufferTicks;
        }

        public bool TryConsumeChoice(long commandTick, out int oneBasedChoice)
        {
            oneBasedChoice = 0;
            if (!_choiceBuffered) return false;
            if (commandTick > _choiceExpiresAfterTick)
            {
                ClearChoice();
                return false;
            }
            oneBasedChoice = _bufferedChoice;
            ClearChoice();
            return true;
        }

        public void BufferDrop(long currentTick)
        {
            if (currentTick < 0L) throw new ArgumentOutOfRangeException(nameof(currentTick));
            if (_dropBuffered) return;
            _dropBuffered = true;
            _dropExpiresAfterTick = currentTick + InteractionBufferTicks;
        }

        public bool TryConsumeDrop(long commandTick)
        {
            if (!_dropBuffered) return false;
            if (commandTick > _dropExpiresAfterTick)
            {
                ClearDrop();
                return false;
            }
            ClearDrop();
            return true;
        }

        public void Clear()
        {
            Movement = OfficeInputDirection.None;
            ClearInteraction();
            ClearChoice();
            ClearDrop();
        }

        private void ClearInteraction()
        {
            _interactionBuffered = false;
            _interactionExpiresAfterTick = 0L;
        }

        private void ClearChoice()
        {
            _choiceBuffered = false;
            _bufferedChoice = 0;
            _choiceExpiresAfterTick = 0L;
        }

        private void ClearDrop()
        {
            _dropBuffered = false;
            _dropExpiresAfterTick = 0L;
        }
    }

    /// <summary>
    /// Converts canonical render-frequency intent into at most one Warden Move
    /// command and one buffered Interact command for each simulation tick.
    /// </summary>
    public sealed class OfficeInputCommandGenerator
    {
        private readonly OfficeSimulationState _state;
        private readonly OfficeInputIntent _intent;

        public OfficeInputCommandGenerator(
            OfficeSimulationState state,
            OfficeInputIntent intent)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _intent = intent ?? throw new ArgumentNullException(nameof(intent));
        }

        public void AdvanceOneTick()
        {
            if (_state.ReplayMode)
            {
                _intent.Clear();
                _state.AdvanceOneTick();
                return;
            }

            if (_intent.TryGetMovement(out int x, out int z))
                _state.TryQueueCommand(
                    _state.CreateMoveCommand(x, z), out OfficeCommandFailure ignored);

            long commandTick = _state.CurrentTick + 1L;
            if (_intent.TryConsumeInteraction(commandTick))
                _state.TryQueueCommand(
                    _state.CreatePrimaryActionCommand(), out OfficeCommandFailure ignored);
            if (_intent.TryConsumeChoice(commandTick, out int choice))
                _state.TryQueueCommand(
                    _state.CreateChoiceCommand(choice), out OfficeCommandFailure ignored);
            if (_intent.TryConsumeDrop(commandTick))
                _state.TryQueueCommand(
                    _state.CreateDropCommand(), out OfficeCommandFailure ignored);

            _state.AdvanceOneTick();
        }
    }
}
