using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace FiefManager
{
    public enum BuildingPriorityCategory
    {
        Balanced,
        MilitaryFirst,
        EconomicFirst
    }

    public class PriorityOption
    {
        private readonly string _name;
        public BuildingPriorityCategory Value { get; }
        public PriorityOption(string name, BuildingPriorityCategory value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "FiefManager_v1";
        public override string DisplayName => "Fief Manager";
        public override string FolderName => "FiefManager";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<PriorityOption> PriorityOptions = new List<PriorityOption>
        {
            new PriorityOption("Balanced (Default order)", BuildingPriorityCategory.Balanced),
            new PriorityOption("Military First (Walls, Garrison, Militia)", BuildingPriorityCategory.MilitaryFirst),
            new PriorityOption("Economic First (Granary, Taxes, Prosperity)", BuildingPriorityCategory.EconomicFirst)
        };

        [SettingPropertyBool("Auto Set Build Queue", RequireRestart = false,
            HintText = "Automatically select default construction projects when the build queue is empty.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoSetBuildQueue { get; set; } = true;

        [SettingPropertyDropdown("Building Category Priority", RequireRestart = false,
            HintText = "Prioritize either military or economic projects when selecting the next construction project.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<PriorityOption> PriorityDropdown { get; set; } =
            new Dropdown<PriorityOption>(PriorityOptions, 0);

        [SettingPropertyBool("Auto Deposit Project Boost Gold", RequireRestart = false,
            HintText = "Automatically deposit denars into town/castle reserves to speed up construction.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoDepositProjectBoost { get; set; } = true;

        [SettingPropertyInteger("Days of Funding", 1, 100, "0", RequireRestart = false,
            HintText = "Number of days of boost process to fund (Castle = 250/day, Town = 500/day).")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int DaysOfFunding { get; set; } = 10;

        [SettingPropertyInteger("Max Reserve Limit (Town)", 10000, 500000, "0", RequireRestart = false,
            HintText = "Maximum gold reserve a town fief is allowed to have. Deposits will top up to this limit.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int MaxReserveLimitTown { get; set; } = 100000;

        [SettingPropertyInteger("Max Reserve Limit (Castle)", 5000, 200000, "0", RequireRestart = false,
            HintText = "Maximum gold reserve a castle fief is allowed to have. Deposits will top up to this limit.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int MaxReserveLimitCastle { get; set; } = 50000;

        [SettingPropertyInteger("Min Player Gold Reserve", 1000, 500000, "0", RequireRestart = false,
            HintText = "Do not deposit gold to fiefs if player's gold is below this threshold.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int MinPlayerGoldReserve { get; set; } = 100000;

        public BuildingPriorityCategory Priority => PriorityDropdown.SelectedValue.Value;
    }
}
