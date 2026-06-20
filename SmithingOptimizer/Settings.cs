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
        Profit,
        Damage
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
            new GoalOption("Profit (XP + Sell Value)", OptimizationGoal.Profit),
            new GoalOption("Damage (Max Swing/Thrust)", OptimizationGoal.Damage)
        };

        [SettingPropertyBool("Enable Smithing Optimizer", RequireRestart = false,
            HintText = "Master automation switch. When disabled, Smithing Optimizer will not react to unlocks, request supplies, or report purchases. The manual crafting button still works.", Order = 0)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool ModEnabled { get; set; } = true;

        [SettingPropertyBool("Auto-switch on New Piece Unlock", RequireRestart = false,
            HintText = "Automatically re-optimize the design when a new crafting piece is unlocked.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoSwitchEnabled { get; set; } = true;

        [SettingPropertyDropdown("Optimization Goal", RequireRestart = false,
            HintText = "Whether to optimize for maximum sell value/XP, or maximum weapon damage.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<GoalOption> GoalDropdown { get; set; } =
            new Dropdown<GoalOption>(GoalOptions, 0);

        [SettingPropertyBool("Limit to Owned Materials", RequireRestart = false,
            HintText = "Only suggest designs that can be crafted with your current material stock.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool LimitToInventory { get; set; } = true;

        [SettingPropertyBool("Auto-Buy Smithing Supplies", RequireRestart = false,
            HintText = "Ask Settlement Automation Core to keep basic smithing supplies stocked when entering trade settlements.", Order = 1)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public bool AutoBuySmithingSupplies { get; set; } = true;

        [SettingPropertyInteger("Hardwood to Keep", 0, 500, RequireRestart = false,
            HintText = "Desired hardwood count after automation purchases. Set to 0 to disable hardwood requests.", Order = 2)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public int DesiredHardwood { get; set; } = 40;

        [SettingPropertyInteger("Charcoal to Keep", 0, 500, RequireRestart = false,
            HintText = "Desired charcoal count after automation purchases. Set to 0 to disable charcoal requests.", Order = 3)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public int DesiredCharcoal { get; set; } = 20;

        [SettingPropertyDropdown("Supply Spend Mode", RequireRestart = false,
            HintText = "Controls when smithing supply requests run compared to other automated purchases.", Order = 4)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public Dropdown<RequestProfileOption> SupplySpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Routine));

        [SettingPropertyInteger("Supply Request Priority", 1, 9, RequireRestart = false,
            HintText = "Priority within the selected spend mode. Higher numbers run first.", Order = 5)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public int SupplyRequestPriority { get; set; } = 5;

        [SettingPropertyInteger("Supply Gold Reserve", 0, 50000, RequireRestart = false,
            HintText = "Do not buy smithing supplies if the purchase would leave less than this much gold.", Order = 6)]
        [SettingPropertyGroup("Automation", GroupOrder = 1)]
        public int SupplyGoldReserve { get; set; } = 1000;

        public OptimizationGoal Goal => GoalDropdown.SelectedValue.Value;
        public RequestProfile SupplyRequestProfile => SupplySpendModeDropdown.SelectedValue.Value;
    }
}
