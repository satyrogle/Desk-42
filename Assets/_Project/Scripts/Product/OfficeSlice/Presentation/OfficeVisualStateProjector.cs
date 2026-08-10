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
        public bool AutomationRuleActive { get; }
        public bool PayrollRuleActive { get; }
        public bool FastTraysVisible { get; }
        public bool CalmChairsVisible { get; }
        public bool RedLabelsVisible { get; }
        public int FastTraysTier { get; }
        public int CalmChairsTier { get; }
        public int RedLabelsTier { get; }

        public OfficeVisualSnapshot(
            long tick,
            int shiftOrdinal,
            string simulationChecksum,
            OfficeVisualPressureState pressure,
            bool copyEchoActive,
            bool ghostClockActive,
            bool missingRoomActive,
            bool promotionCascadeActive,
            bool automationRuleActive,
            bool payrollRuleActive,
            bool fastTraysVisible,
            bool calmChairsVisible,
            bool redLabelsVisible,
            int fastTraysTier,
            int calmChairsTier,
            int redLabelsTier)
        {
            Tick = tick;
            ShiftOrdinal = shiftOrdinal;
            SimulationChecksum = simulationChecksum ?? string.Empty;
            Pressure = pressure;
            CopyEchoActive = copyEchoActive;
            GhostClockActive = ghostClockActive;
            MissingRoomActive = missingRoomActive;
            PromotionCascadeActive = promotionCascadeActive;
            AutomationRuleActive = automationRuleActive;
            PayrollRuleActive = payrollRuleActive;
            FastTraysVisible = fastTraysVisible;
            CalmChairsVisible = calmChairsVisible;
            RedLabelsVisible = redLabelsVisible;
            FastTraysTier = fastTraysTier;
            CalmChairsTier = calmChairsTier;
            RedLabelsTier = redLabelsTier;
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
                state.AutomationRule.Enabled,
                state.PayrollRule.Enabled,
                campaign?.Upgrades.FastTraysTier > 0,
                campaign?.Upgrades.CalmChairsTier > 0,
                campaign?.Upgrades.RedLabelsTier > 0,
                campaign?.Upgrades.FastTraysTier ?? 0,
                campaign?.Upgrades.CalmChairsTier ?? 0,
                campaign?.Upgrades.RedLabelsTier ?? 0);
        }

        private static OfficeVisualPressureState ProjectPressure(
            OfficeSimulationState state,
            OfficeCampaignState campaign)
        {
            if (campaign?.Phase == OfficeCampaignPhase.CampaignResult ||
                state.Shift.Phase == OfficeShiftPhase.Result)
                return OfficeVisualPressureState.Result;
            if (state.BreakState.Active || state.GhostClock.Active ||
                state.MissingRoomAccess.Active || state.PromotionCascade.Active)
                return OfficeVisualPressureState.Break;
            if (state.BreakState.Recovered || state.GhostClock.Recovered ||
                state.MissingRoomAccess.Recovered || state.PromotionCascade.Recovered)
                return OfficeVisualPressureState.Recovery;
            if (state.Customers.ActiveDeskCustomer?.VisibleMoodState >=
                OfficeVisibleMoodState.Worried)
                return OfficeVisualPressureState.Rush;
            return OfficeVisualPressureState.Calm;
        }
    }
}
