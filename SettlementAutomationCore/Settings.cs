using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace SettlementAutomationCore
{
    public enum CoreReportingMode
    {
        Off,
        SilentMods,
        Full
    }

    public enum RecruitmentNotificationMode
    {
        OneByOne,
        Consolidated
    }

    public class RecruitmentNotificationModeOption
    {
        private readonly string _name;
        public RecruitmentNotificationMode Value { get; }

        public RecruitmentNotificationModeOption(string name, RecruitmentNotificationMode value)
        {
            _name = name;
            Value = value;
        }

        public override string ToString() => _name;
    }

    public class CoreReportingModeOption
    {
        private readonly string _name;
        public CoreReportingMode Value { get; }

        public CoreReportingModeOption(string name, CoreReportingMode value)
        {
            _name = name;
            Value = value;
        }

        public override string ToString() => _name;
    }

    public enum IgnoreWeightLimitTier
    {
        None,
        CriticalOnly,
        EssentialOrHigher,
        RoutineOrHigher,
        AllRequests
    }

    public class IgnoreWeightLimitTierOption
    {
        private readonly string _name;
        public IgnoreWeightLimitTier Value { get; }

        public IgnoreWeightLimitTierOption(string name, IgnoreWeightLimitTier value)
        {
            _name = name;
            Value = value;
        }

        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "SettlementAutomationCore_v0_5";
        public override string DisplayName => "Settlement Automation Core";
        public override string FolderName => "SettlementAutomationCore";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<CoreReportingModeOption> CoreReportingModeOptions = new List<CoreReportingModeOption>
        {
            new CoreReportingModeOption("Off", CoreReportingMode.Off),
            new CoreReportingModeOption("Silent Mods", CoreReportingMode.SilentMods),
            new CoreReportingModeOption("Full", CoreReportingMode.Full)
        };

        private static readonly IReadOnlyList<IgnoreWeightLimitTierOption> IgnoreWeightLimitTierOptions = new List<IgnoreWeightLimitTierOption>
        {
            new IgnoreWeightLimitTierOption("None (Respect weight)", IgnoreWeightLimitTier.None),
            new IgnoreWeightLimitTierOption("Critical only", IgnoreWeightLimitTier.CriticalOnly),
            new IgnoreWeightLimitTierOption("Essential or higher", IgnoreWeightLimitTier.EssentialOrHigher),
            new IgnoreWeightLimitTierOption("Routine or higher", IgnoreWeightLimitTier.RoutineOrHigher),
            new IgnoreWeightLimitTierOption("All requests", IgnoreWeightLimitTier.AllRequests)
        };


        [SettingPropertyBool("Disable Settlement Automation", RequireRestart = false,
            HintText = "Stops Core-driven automatic settlement-entry actions. Manual buttons can still be used.", Order = 1)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool DisableSettlementAutomation { get; set; } = false;

        private int _minimumGoldReserve = 1000;

        [SettingPropertyInteger("Minimum Gold Reserve", 0, 50000, RequireRestart = false,
            HintText = "Never let your gold balance drop below this amount when buying. Default: 1000 denars. Snapping step: 1,000.", Order = 2)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int MinimumGoldReserve
        {
            get => _minimumGoldReserve;
            set => _minimumGoldReserve = ((value + 500) / 1000) * 1000;
        }

        [SettingPropertyInteger("Min Days of Expenses to Keep", 0, 100, RequireRestart = false,
            HintText = "Ensure you keep enough gold to cover this many days of party wages/expenses (excluding daily income).", Order = 3)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int MinDaysExpensesToKeep { get; set; } = 10;

        [SettingPropertyBool("Respect Carry Capacity", RequireRestart = false,
            HintText = "If enabled, Core stops item purchases before they would exceed carry capacity. If disabled, automation may buy cargo even when it causes the overburdened speed penalty.", Order = 1)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public bool LimitToInventoryCapacity { get; set; } = true;

        [SettingPropertyInteger("Reserve Carry Capacity (%)", 0, 50, RequireRestart = false,
            HintText = "Keep this percentage of carry capacity free for loot and manual inventory use when automation buys cargo. Default: 10%.", Order = 2)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public int ReserveCarryCapacityPercent { get; set; } = 10;

        private float _essentialPriceLimitMultiplier = 1.50f;
        [SettingPropertyFloatingInteger("Essential Request Price Limit", 0.5f, 4.0f, "#0.00", RequireRestart = false,
            HintText = "Maximum price/value ratio for essential requests. Critical and luxury requests ignore this. Default: 1.50.", Order = 3)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public float EssentialPriceLimitMultiplier
        {
            get => _essentialPriceLimitMultiplier;
            set => _essentialPriceLimitMultiplier = (float)System.Math.Round(value / 0.05f) * 0.05f;
        }

        private float _routinePriceLimitMultiplier = 1.25f;
        [SettingPropertyFloatingInteger("Routine Request Price Limit", 0.5f, 3.0f, "#0.00", RequireRestart = false,
            HintText = "Maximum price/value ratio for routine requests. Critical and luxury requests ignore this. Default: 1.25.", Order = 4)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public float RoutinePriceLimitMultiplier
        {
            get => _routinePriceLimitMultiplier;
            set => _routinePriceLimitMultiplier = (float)System.Math.Round(value / 0.05f) * 0.05f;
        }

        private float _opportunisticPriceLimitMultiplier = 0.75f;
        [SettingPropertyFloatingInteger("Opportunistic Request Price Limit", 0.25f, 1.5f, "#0.00", RequireRestart = false,
            HintText = "Maximum price/value ratio for opportunistic requests. Default: 0.75.", Order = 5)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public float OpportunisticPriceLimitMultiplier
        {
            get => _opportunisticPriceLimitMultiplier;
            set => _opportunisticPriceLimitMultiplier = (float)System.Math.Round(value / 0.05f) * 0.05f;
        }

        [SettingPropertyDropdown("Ignore Weight Limit for", RequireRestart = false,
            HintText = "Allow buying vital items even when party is overburdened. None respects cargo capacity limits. All Requests ignores weight for all prioritized orders. Default: All Requests.", Order = 6)]
        [SettingPropertyGroup("Trading Policies", GroupOrder = 1)]
        public Dropdown<IgnoreWeightLimitTierOption> IgnoreWeightLimitDropdown { get; set; } =
            new Dropdown<IgnoreWeightLimitTierOption>(IgnoreWeightLimitTierOptions, 4); // Default: All Requests

        public IgnoreWeightLimitTier IgnoreWeightLimitSetting => IgnoreWeightLimitDropdown.SelectedValue.Value;

        [SettingPropertyDropdown("Market Report Detail", RequireRestart = false,
            HintText = "Choose Core's in-game market reporting behavior. Off writes no in-game automation report lines. Silent Mods writes module reports plus Core fallback for modules that do not report. Full also writes Core's complete transaction summary.", Order = 1)]
        [SettingPropertyGroup("Reporting", GroupOrder = 2)]
        public Dropdown<CoreReportingModeOption> CoreReportingModeDropdown { get; set; } =
            new Dropdown<CoreReportingModeOption>(CoreReportingModeOptions, 2); // Default: Full

        public CoreReportingMode CoreReportingModeSetting => CoreReportingModeDropdown.SelectedValue.Value;

        [SettingPropertyBool("Log Rejected Order Details", RequireRestart = false,
            HintText = "If enabled, Core writes one detailed file-log entry for each item request it could not fulfill. Off by default to keep logs focused on completed work.", Order = 2)]
        [SettingPropertyGroup("Reporting", GroupOrder = 2)]
        public bool LogRejectedOrderDetails { get; set; } = false;
    }
}
