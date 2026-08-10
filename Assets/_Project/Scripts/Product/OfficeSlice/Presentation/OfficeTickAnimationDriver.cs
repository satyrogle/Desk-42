using System;

namespace Desk42.Product.OfficeSlice
{
    /// <summary>
    /// Pure presentation cadence derived from the 30 Hz simulation tick. It has no
    /// animation events and cannot queue or complete gameplay actions.
    /// </summary>
    public sealed class OfficeTickAnimationDriver
    {
        public const int SimulationHz = 30;
        public const int VisualFramesPerSecond = 10;

        public int FrameAt(long simulationTick, int frameCount)
        {
            if (simulationTick < 0) throw new ArgumentOutOfRangeException(nameof(simulationTick));
            if (frameCount < 1) throw new ArgumentOutOfRangeException(nameof(frameCount));
            return (int)((simulationTick * VisualFramesPerSecond / SimulationHz) % frameCount);
        }

        public static string WardenMovementAssetId(
            OfficeInputDirection direction,
            bool carrying)
        {
            return direction switch
            {
                OfficeInputDirection.Up => carrying
                    ? "character.warden.carry-walk-up" : "character.warden.walk-up",
                OfficeInputDirection.Down => carrying
                    ? "character.warden.carry-walk-down" : "character.warden.walk-down",
                OfficeInputDirection.Left => carrying
                    ? "character.warden.carry-walk-left" : "character.warden.walk-left",
                OfficeInputDirection.Right => carrying
                    ? "character.warden.carry-walk-right" : "character.warden.walk-right",
                _ => carrying ? "character.warden.carry-walk-down" : "character.warden.idle",
            };
        }
    }
}
