using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace SettlementAutomationCore
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "SettlementAutomationCore_v1";
        public override string DisplayName => "Settlement Automation Core";
        public override string FolderName => "SettlementAutomationCore";
        public override string FormatType => "json";

        [SettingPropertyInteger("Minimum Gold Reserve", 0, 50000, RequireRestart = false,
            HintText = "Never let your gold balance drop below this amount when buying. Default: 1000 denars.", Order = 1)]
        [SettingPropertyGroup("Budget Protection", GroupOrder = 0)]
        public int MinimumGoldReserve { get; set; } = 1000;

        [SettingPropertyInteger("Min Days of Expenses to Keep", 0, 100, RequireRestart = false,
            HintText = "Ensure you keep enough gold to cover this many days of party wages/expenses (excluding daily income).", Order = 2)]
        [SettingPropertyGroup("Budget Protection", GroupOrder = 0)]
        public int MinDaysExpensesToKeep { get; set; } = 10;

        [SettingPropertyBool("Limit to Carry Capacity", RequireRestart = false,
            HintText = "Stop buying when your party's carry weight would be exceeded.", Order = 1)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public bool LimitToInventoryCapacity { get; set; } = true;

        [SettingPropertyFloatingInteger("Routine Request Price Limit", 0.5f, 5.0f, "#0.00", RequireRestart = false,
            HintText = "Maximum price/value ratio for routine requests. Critical and essential requests ignore this.", Order = 2)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public float RoutinePriceLimitMultiplier { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Opportunistic Request Price Limit", 0.5f, 5.0f, "#0.00", RequireRestart = false,
            HintText = "Maximum price/value ratio for opportunistic requests.", Order = 3)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public float OpportunisticPriceLimitMultiplier { get; set; } = 1.1f;
    }
}
