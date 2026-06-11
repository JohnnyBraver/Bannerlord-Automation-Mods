using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

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
        public override string Id => "SmithingOptimizer_v1";
        public override string DisplayName => "Smithing Optimizer";
        public override string FolderName => "SmithingOptimizer";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<GoalOption> GoalOptions = new List<GoalOption>
        {
            new GoalOption("Profit (XP + Sell Value)", OptimizationGoal.Profit),
            new GoalOption("Damage (Max Swing/Thrust)", OptimizationGoal.Damage)
        };

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

        // Compatibility wrapper — used throughout the codebase
        public OptimizationGoal Goal => GoalDropdown.SelectedValue.Value;
    }
}
