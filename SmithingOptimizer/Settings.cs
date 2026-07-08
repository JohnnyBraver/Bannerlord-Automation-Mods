using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using SettlementAutomationCore;

namespace SmithingOptimizer
{
    public enum OptimizationGoal
    {
        XpEfficiency,
        SellValue,
        Damage,
        Profit = SellValue
    }

    public class GoalOption
    {
        private readonly string _name;
        public OptimizationGoal Value { get; }
        public GoalOption(string name, OptimizationGoal value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "SmithingOptimizer_v0_4";
        public override string DisplayName => "Smithing Optimizer";
        public override string FolderName => "SmithingOptimizer";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<GoalOption> GoalOptions = new List<GoalOption>
        {
            new GoalOption("XP Efficiency (Raw XP/Stamina)", OptimizationGoal.XpEfficiency),
            new GoalOption("Sell Value", OptimizationGoal.SellValue),
            new GoalOption("Damage (Max Swing/Thrust)", OptimizationGoal.Damage)
        };

        [SettingPropertyBool("Auto-optimize in Forge", RequireRestart = false,
            HintText = "Automatically re-optimize when opening the forge, switching designs, unlocking a new piece, or crafting.", Order = 1)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoSwitchEnabled { get; set; } = true;

        [SettingPropertyDropdown("Optimization Goal", RequireRestart = false,
            HintText = "Choose raw Smithing XP per stamina, sell value, or maximum weapon damage.", Order = 2)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<GoalOption> GoalDropdown { get; set; } =
            new Dropdown<GoalOption>(GoalOptions, 0);

        [SettingPropertyBool("Limit to Owned Materials", RequireRestart = false,
            HintText = "Only suggest designs that can be crafted with your current material stock.", Order = 3)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool LimitToInventory { get; set; } = true;

        [SettingPropertyBool("Auto-Buy Smithing Supplies", RequireRestart = false,
            HintText = "Ask Settlement Automation Core to keep basic smithing supplies stocked when entering trade settlements.", Order = 1)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public bool AutoBuySmithingSupplies { get; set; } = true;

        private int _desiredHardwood = 40;
        private int _desiredCharcoal = 20;

        [SettingPropertyInteger("Hardwood to Keep", 0, 500, RequireRestart = false,
            HintText = "Desired hardwood count after automation purchases. Set to 0 to disable hardwood requests. Snapping step: 10.", Order = 2)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public int DesiredHardwood
        {
            get => _desiredHardwood;
            set => _desiredHardwood = ((value + 5) / 10) * 10;
        }

        [SettingPropertyInteger("Charcoal to Keep", 0, 500, RequireRestart = false,
            HintText = "Desired charcoal count after automation purchases. Set to 0 to disable charcoal requests. Snapping step: 10.", Order = 3)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public int DesiredCharcoal
        {
            get => _desiredCharcoal;
            set => _desiredCharcoal = ((value + 5) / 10) * 10;
        }

        [SettingPropertyDropdown("Supply Spend Mode", RequireRestart = false,
            HintText = "Controls when smithing supply requests run compared to other automated purchases.", Order = 4)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public Dropdown<RequestProfileOption> SupplySpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Routine));

        [SettingPropertyInteger("Supply Request Priority", 1, 9, RequireRestart = false,
            HintText = "Priority within the selected spend mode. Higher numbers run first.", Order = 5)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public int SupplyRequestPriority { get; set; } = 5;

        private int _supplyGoldReserve = 1000;

        [SettingPropertyInteger("Supply Gold Reserve", 0, 50000, RequireRestart = false,
            HintText = "Do not buy smithing supplies if the purchase would leave less than this much gold. Default: 1000 denars. Snapping step: 1,000.", Order = 6)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public int SupplyGoldReserve
        {
            get => _supplyGoldReserve;
            set => _supplyGoldReserve = ((value + 500) / 1000) * 1000;
        }

        public OptimizationGoal Goal => GoalDropdown.SelectedValue.Value;
        public RequestProfile SupplyRequestProfile => SupplySpendModeDropdown.SelectedValue.Value;
    }
}
