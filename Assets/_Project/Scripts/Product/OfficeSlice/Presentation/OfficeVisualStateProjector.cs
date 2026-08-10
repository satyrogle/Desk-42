using System;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeVisualPressureState
    {
        Calm,
        Rush,
        Break,
        Recovery,
        Result,
    }

    public sealed class OfficeVisualSnapshot
    {
        public long Tick { get; }
        public int ShiftOrdinal { get; }
        public string SimulationChecksum { get; }
        public OfficeVisualPressureState Pressure { get; }
        public bool CopyEchoActive { get; }
        public bool GhostClockActive { get; }
        public bool MissingRoomActive { get; }
        public bool PromotionCascadeActive { get; }
        public bool FastTraysVisible { get; }
        public bool CalmChairsVisible { get; }
        public bool RedLabelsVisible { get; }

        public OfficeVisualSnapshot(
            long tick,
            int shiftOrdinal,
            string simulationChecksum,
            OfficeVisualPressureState pressure,
            bool copyEchoActive,
            bool ghostClockActive,
            bool missingRoomActive,
            bool promotionCascadeActive,
            bool fastTraysVisible,
            bool calmChairsVisible,
            bool redLabelsVisible)
        {
            Tick = tick;
            ShiftOrdinal = shiftOrdinal;
            SimulationChecksum = simulationChecksum ?? string.Empty;
            Pressure = pressure;
            CopyEchoActive = copyEchoActive;
            GhostClockActive = ghostClockActive;
            MissingRoomActive = missingRoomActive;
            PromotionCascadeActive = promotionCascadeActive;
            FastTraysVisible = fastTraysVisible;
            CalmChairsVisible = calmChairsVisible;
            RedLabelsVisible = redLabelsVisible;
        }
    }

    /// <summary>Reads public product state and never owns or mutates simulation state.</summary>
    public sealed class OfficeVisualStateProjector
    {
        public OfficeVisualSnapshot Project(
            OfficeSimulationState state,
            OfficeCampaignState campaign)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            OfficeVisualPressureState pressure = ProjectPressure(state, campaign);
            return new OfficeVisualSnapshot(
                state.CurrentTick,
                campaign?.CurrentShiftOrdinal ?? state.Shift.ShiftOrdinal,
                state.Checksum,
                pressure,
                state.BreakState.Active && !state.BreakState.Recovered,
                state.GhostClock.Active,
                state.MissingRoomAccess.Active,
                state.PromotionCascade.Active && !state.PromotionCascade.Recovered,
                campaign?.Upgrades.FastTraysTier > 0,
                campaign?.Upgrades.CalmChairsTier > 0,
                campaign?.Upgrades.RedLabelsTier > 0);
        }

        private static OfficeVisualPressureState ProjectPressure(
            OfficeSimulationState state,
            OfficeCampaignState campaign)
        {
            if (campaign?.Phase == OfficeCampaignPhase.CampaignResult ||
                state.Shift.Phase == OfficeShiftPhase.Result)
                return OfficeVisualPressureState.Result;
            if (state.BreakState.Recovered || state.GhostClock.Recovered ||
                state.MissingRoomAccess.Recovered || state.PromotionCascade.Recovered)
                return OfficeVisualPressureState.Recovery;
            if (state.BreakState.Active || state.GhostClock.Active ||
                state.MissingRoomAccess.Active || state.PromotionCascade.Active)
                return OfficeVisualPressureState.Break;
            if (state.Customers.ActiveDeskCustomer?.VisibleMoodState >=
                OfficeVisibleMoodState.Worried)
                return OfficeVisualPressureState.Rush;
            return OfficeVisualPressureState.Calm;
        }
    }
}
