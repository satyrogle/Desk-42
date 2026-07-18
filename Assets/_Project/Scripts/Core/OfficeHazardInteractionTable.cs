using System.Collections.Generic;

namespace Desk42.Core
{
    public static class OfficeHazardInteractionTable
    {
        private static readonly Dictionary<(OfficeHazardType, OfficeHazardType), OfficeHazardType> _chains = new()
        {
            [(OfficeHazardType.PrinterJam,        OfficeHazardType.SystemCrash)]       = OfficeHazardType.UnscheduledAudit,
            [(OfficeHazardType.SystemCrash,        OfficeHazardType.PrinterJam)]        = OfficeHazardType.UnscheduledAudit,
            [(OfficeHazardType.MandatoryMeeting,   OfficeHazardType.FireDrill)]         = OfficeHazardType.CoffeeMachineDown,
            [(OfficeHazardType.PrinterJam,         OfficeHazardType.MandatoryMeeting)]  = OfficeHazardType.SystemCrash,
            [(OfficeHazardType.CoffeeMachineDown,  OfficeHazardType.MandatoryMeeting)]  = OfficeHazardType.FireDrill,
        };

        public static OfficeHazardType? CheckChain(OfficeHazardType previous, OfficeHazardType current)
        {
            if (_chains.TryGetValue((previous, current), out var result))
                return result;
            return null;
        }
    }
}
