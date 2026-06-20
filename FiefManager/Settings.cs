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

    public enum BuildingUpgradeApproach
    {
        DefaultOrder,
        LowestLevelFirst,
        CheapestFirst,
        MostExpensiveFirst
    }

    public class PriorityOption
    {
        private readonly string _name;
        public BuildingPriorityCategory Value { get; }
        public PriorityOption(string name, BuildingPriorityCategory value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class UpgradeApproachOption
    {
        private readonly string _name;
        public BuildingUpgradeApproach Value { get; }
        public UpgradeApproachOption(string name, BuildingUpgradeApproach value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "FiefManager_v0_4";
        public override string DisplayName => "Fief Manager";
        public override string FolderName => "FiefManager";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<PriorityOption> PriorityOptions = new List<PriorityOption>
        {
            new PriorityOption("Balanced (Default order)", BuildingPriorityCategory.Balanced),
            new PriorityOption("Military First (Walls, Garrison, Militia)", BuildingPriorityCategory.MilitaryFirst),
            new PriorityOption("Economic First (Granary, Taxes, Prosperity)", BuildingPriorityCategory.EconomicFirst)
        };

        private static readonly IReadOnlyList<UpgradeApproachOption> UpgradeApproachOptions = new List<UpgradeApproachOption>
        {
            new UpgradeApproachOption("Default Order", BuildingUpgradeApproach.DefaultOrder),
            new UpgradeApproachOption("Lowest Level First", BuildingUpgradeApproach.LowestLevelFirst),
            new UpgradeApproachOption("Cheapest First", BuildingUpgradeApproach.CheapestFirst),
            new UpgradeApproachOption("Most Expensive First", BuildingUpgradeApproach.MostExpensiveFirst)
        };

        [SettingPropertyBool("Enable Fief Manager", RequireRestart = false,
            HintText = "Master switch. When disabled, Fief Manager will not queue projects or deposit project boost gold.", Order = 0)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool ModEnabled { get; set; } = true;

        [SettingPropertyBool("Auto Set Build Queue", RequireRestart = false,
            HintText = "Automatically select default construction projects when the build queue is empty.", Order = 1)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoSetBuildQueue { get; set; } = true;

        [SettingPropertyDropdown("Building Category Priority", RequireRestart = false,
            HintText = "Prioritize either military or economic projects when selecting the next construction project.", Order = 2)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<PriorityOption> PriorityDropdown { get; set; } =
            new Dropdown<PriorityOption>(PriorityOptions, 0);

        [SettingPropertyDropdown("Upgrade Approach", RequireRestart = false,
            HintText = "Choose how eligible construction projects are ordered within the selected category priority.", Order = 3)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<UpgradeApproachOption> UpgradeApproachDropdown { get; set; } =
            new Dropdown<UpgradeApproachOption>(UpgradeApproachOptions, 1);

        [SettingPropertyInteger("Max Queued Build Projects", 1, 12, "0", RequireRestart = false,
            HintText = "Maximum number of construction projects to keep queued ahead. Existing queued projects count toward this limit.", Order = 4)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int MaxQueuedBuildProjects { get; set; } = 3;

        [SettingPropertyBool("Auto Deposit Project Boost Gold", RequireRestart = false,
            HintText = "Automatically deposit denars into town/castle reserves to speed up construction.", Order = 1)]
        [SettingPropertyGroup("Reserves & Deposit Rules", GroupOrder = 1)]
        public bool AutoDepositProjectBoost { get; set; } = true;

        [SettingPropertyBool("Only Deposit When Upgrade Projects Remain", RequireRestart = false,
            HintText = "Skip project boost deposits when every non-default building project is already max level.", Order = 2)]
        [SettingPropertyGroup("Reserves & Deposit Rules", GroupOrder = 1)]
        public bool OnlyDepositWithUpgradeableProjects { get; set; } = true;

        [SettingPropertyInteger("Days of Funding", 1, 100, "0", RequireRestart = false,
            HintText = "Number of days of boost process to fund (Castle = 250/day, Town = 500/day).", Order = 3)]
        [SettingPropertyGroup("Reserves & Deposit Rules", GroupOrder = 1)]
        public int DaysOfFunding { get; set; } = 10;

        [SettingPropertyInteger("Max Reserve Limit (Town) (k Denars)", 10, 100, "0", RequireRestart = false,
            HintText = "Maximum gold reserve a town fief is allowed to have (in thousands). Deposits will top up to this limit. Default: 100k.", Order = 4)]
        [SettingPropertyGroup("Reserves & Deposit Rules", GroupOrder = 1)]
        public int MaxReserveLimitTownK { get; set; } = 100;

        public int MaxReserveLimitTown => MaxReserveLimitTownK * 1000;

        [SettingPropertyInteger("Max Reserve Limit (Castle) (k Denars)", 5, 50, "0", RequireRestart = false,
            HintText = "Maximum gold reserve a castle fief is allowed to have (in thousands). Deposits will top up to this limit. Default: 50k.", Order = 5)]
        [SettingPropertyGroup("Reserves & Deposit Rules", GroupOrder = 1)]
        public int MaxReserveLimitCastleK { get; set; } = 50;

        public int MaxReserveLimitCastle => MaxReserveLimitCastleK * 1000;

        [SettingPropertyInteger("Min Player Gold Reserve (x10k Denars)", 0, 50, "0", RequireRestart = false,
            HintText = "Do not deposit gold to fiefs if player's gold is below this threshold (in ten-thousands). Default: 100k.", Order = 6)]
        [SettingPropertyGroup("Reserves & Deposit Rules", GroupOrder = 1)]
        public int MinPlayerGoldReserveTenK { get; set; } = 10;

        public int MinPlayerGoldReserve => MinPlayerGoldReserveTenK * 10000;


        public BuildingPriorityCategory Priority => PriorityDropdown.SelectedValue.Value;
        public BuildingUpgradeApproach UpgradeApproach => UpgradeApproachDropdown.SelectedValue.Value;
    }
}
