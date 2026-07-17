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
        // Keep these values stable: MCM persists the selected dropdown index.
        SellValue = 0,
        Damage = 1,
        SmithingXp = 2
    }

    public enum OptimizationEfficiency
    {
        Raw = 0,
        PerStamina = 1,
        PerMaterialValue = 2
    }

    public enum MinimumCraftQuality
    {
        Normal = 2,
        Fine = 3,
        Masterwork = 4,
        Legendary = 5
    }

    public class GoalOption
    {
        private readonly string _name;
        public OptimizationGoal Value { get; }
        public GoalOption(string name, OptimizationGoal value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class EfficiencyOption
    {
        private readonly string _name;
        public OptimizationEfficiency Value { get; }
        public EfficiencyOption(string name, OptimizationEfficiency value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class QualityOption
    {
        private readonly string _name;
        public MinimumCraftQuality Value { get; }
        public QualityOption(string name, MinimumCraftQuality value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        // A new profile prevents earlier releases from silently inheriting new order defaults.
        public override string Id => "SmithingOptimizer_v0_6_0";
        public override string DisplayName => "Smithing Optimizer";
        public override string FolderName => "SmithingOptimizer";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<GoalOption> GoalOptions = new List<GoalOption>
        {
            new GoalOption("Sell Value", OptimizationGoal.SellValue),
            new GoalOption("Damage (Max Swing/Thrust)", OptimizationGoal.Damage),
            new GoalOption("Smithing XP", OptimizationGoal.SmithingXp)
        };

        private static readonly IReadOnlyList<EfficiencyOption> EfficiencyOptions = new List<EfficiencyOption>
        {
            new EfficiencyOption("Raw Result", OptimizationEfficiency.Raw),
            new EfficiencyOption("Per Stamina", OptimizationEfficiency.PerStamina),
            new EfficiencyOption("Per Material Value", OptimizationEfficiency.PerMaterialValue)
        };

        private static readonly IReadOnlyList<QualityOption> QualityOptions = new List<QualityOption>
        {
            new QualityOption("Normal", MinimumCraftQuality.Normal),
            new QualityOption("Fine", MinimumCraftQuality.Fine),
            new QualityOption("Masterwork", MinimumCraftQuality.Masterwork),
            new QualityOption("Legendary", MinimumCraftQuality.Legendary)
        };

        [SettingPropertyBool("Auto-switch on New Piece Unlock", RequireRestart = false,
            HintText = "Automatically re-optimize the design when a new crafting piece is unlocked.", Order = 1)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoSwitchEnabled { get; set; } = true;

        [SettingPropertyBool("Optimize Selected Crafting Order", RequireRestart = false,
            HintText = "When selecting an order, choose affordable unlocked parts that give the selected smith the best chance to complete it.", Order = 1)]
        [SettingPropertyGroup("Crafting Orders", GroupOrder = 1)]
        public bool AutoOptimizeCraftingOrders { get; set; } = true;

        [SettingPropertyInteger("Order Completion Chance", 0, 100, RequireRestart = false,
            HintText = "Require at least this chance for the selected smith to complete an order. 0% disables the gate. Default: 100%.", Order = 2)]
        [SettingPropertyGroup("Crafting Orders", GroupOrder = 1)]
        public int OrderMinimumCompletionChance { get; set; } = 100;

        [SettingPropertyDropdown("Optimization Goal", RequireRestart = false,
            HintText = "Whether to optimize for sell value, Smithing XP, or maximum weapon damage.", Order = 4)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<GoalOption> GoalDropdown { get; set; } =
            new Dropdown<GoalOption>(GoalOptions, 0);

        [SettingPropertyDropdown("Efficiency Basis", RequireRestart = false,
            HintText = "Optimize the selected target directly, per stamina spent, or per base-value of materials consumed.", Order = 5)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<EfficiencyOption> EfficiencyDropdown { get; set; } =
            new Dropdown<EfficiencyOption>(EfficiencyOptions, 0);

        [SettingPropertyDropdown("Damage Minimum Quality", RequireRestart = false,
            HintText = "For Damage, the quality tier used by the reliability gate. The chance slider at 0% disables the gate.", Order = 6)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<QualityOption> DamageMinimumQualityDropdown { get; set; } =
            new Dropdown<QualityOption>(QualityOptions, 0);

        [SettingPropertyInteger("Damage Quality Chance", 0, 100, RequireRestart = false,
            HintText = "For Damage, require at least this chance to craft the selected quality or better. Set 0% to disable the gate.", Order = 7)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int DamageMinimumQualityChance { get; set; } = 25;

        [SettingPropertyBool("Limit to Owned Materials", RequireRestart = false,
            HintText = "Only suggest designs that can be crafted with your current material stock.", Order = 8)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool LimitToInventory { get; set; } = true;

        [SettingPropertyBool("Auto-Buy Smithing Supplies", RequireRestart = false,
            HintText = "Ask Settlement Automation Core to keep basic smithing supplies stocked when entering trade settlements.", Order = 1)]
        [SettingPropertyGroup("Automation", GroupOrder = 2)]
        public bool AutoBuySmithingSupplies { get; set; } = true;

        [SettingPropertyInteger("Hardwood to Keep", 0, 500, RequireRestart = false,
            HintText = "Buy hardwood until this amount is reached. Set to 0 to disable hardwood purchases.", Order = 2)]
        [SettingPropertyGroup("Automation", GroupOrder = 2)]
        public int DesiredHardwood { get; set; } = 40;

        [SettingPropertyDropdown("Supply Spend Mode", RequireRestart = false,
            HintText = "Controls when smithing supply requests run compared to other automated purchases.", Order = 3)]
        [SettingPropertyGroup("Automation", GroupOrder = 2)]
        public Dropdown<RequestProfileOption> SupplySpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Routine));

        private int _supplyGoldReserve = 10000;

        [SettingPropertyInteger("Supply Gold Reserve", 0, 50000, RequireRestart = false,
            HintText = "Do not buy hardwood if the party has this much gold or less. Default: 10,000 denars. Snapping step: 1,000.", Order = 4)]
        [SettingPropertyGroup("Automation", GroupOrder = 2)]
        public int SupplyGoldReserve
        {
            get => _supplyGoldReserve;
            set => _supplyGoldReserve = ((value + 500) / 1000) * 1000;
        }

        public OptimizationGoal Goal => GoalDropdown.SelectedValue.Value;
        public OptimizationEfficiency Efficiency => EfficiencyDropdown.SelectedValue.Value;
        public MinimumCraftQuality DamageMinimumQuality => DamageMinimumQualityDropdown.SelectedValue.Value;
        public RequestProfile SupplyRequestProfile => SupplySpendModeDropdown.SelectedValue.Value;
    }
}
