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
        public override string Id => "SmithingOptimizer_v0_4";
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

        [SettingPropertyDropdown("Optimization Goal", RequireRestart = false,
            HintText = "Whether to optimize for sell value, Smithing XP, or maximum weapon damage.", Order = 2)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<GoalOption> GoalDropdown { get; set; } =
            new Dropdown<GoalOption>(GoalOptions, 0);

        [SettingPropertyDropdown("Efficiency Basis", RequireRestart = false,
            HintText = "Optimize the selected target directly, per stamina spent, or per base-value of materials consumed.", Order = 3)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<EfficiencyOption> EfficiencyDropdown { get; set; } =
            new Dropdown<EfficiencyOption>(EfficiencyOptions, 0);

        [SettingPropertyDropdown("Damage Minimum Quality", RequireRestart = false,
            HintText = "For Damage, the quality tier used by the reliability gate. The chance slider at 0% disables the gate.", Order = 4)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<QualityOption> DamageMinimumQualityDropdown { get; set; } =
            new Dropdown<QualityOption>(QualityOptions, 1);

        [SettingPropertyInteger("Damage Quality Chance", 0, 100, RequireRestart = false,
            HintText = "For Damage, require at least this chance to craft the selected quality or better. Set 0% to disable the gate.", Order = 5)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int DamageMinimumQualityChance { get; set; } = 0;

        [SettingPropertyBool("Limit to Owned Materials", RequireRestart = false,
            HintText = "Only suggest designs that can be crafted with your current material stock.", Order = 6)]
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
        public OptimizationEfficiency Efficiency => EfficiencyDropdown.SelectedValue.Value;
        public MinimumCraftQuality DamageMinimumQuality => DamageMinimumQualityDropdown.SelectedValue.Value;
        public RequestProfile SupplyRequestProfile => SupplySpendModeDropdown.SelectedValue.Value;
    }
}
