using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using SettlementAutomationCore;

namespace EquipmentManager
{
    public enum AutoEquipCategory
    {
        None,
        ArmorOnly,
        WeaponsOnly,
        WeaponsAndArmor
    }

    public class AutoEquipCategoryOption
    {
        private readonly string _name;
        public AutoEquipCategory Value { get; }
        public AutoEquipCategoryOption(string name, AutoEquipCategory value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }



    public enum LoadoutPriority
    {
        Sneaking_Civilian_Combat,
        Sneaking_Combat_Civilian,
        Combat_Sneaking_Civilian,
        Combat_Civilian_Sneaking,
        Civilian_Sneaking_Combat,
        Civilian_Combat_Sneaking
    }

    public class LoadoutPriorityOption
    {
        private readonly string _name;
        public LoadoutPriority Value { get; }
        public LoadoutPriorityOption(string name, LoadoutPriority value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum BuyEquipmentTarget
    {
        PlayerOnly,
        PlayerAndCompanions
    }

    public class BuyEquipmentTargetOption
    {
        private readonly string _name;
        public BuyEquipmentTarget Value { get; }
        public BuyEquipmentTargetOption(string name, BuyEquipmentTarget value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum LockDonationCategory
    {
        None,
        WeaponsOnly,
        ArmorOnly,
        WeaponsAndArmor
    }

    public class LockDonationCategoryOption
    {
        private readonly string _name;
        public LockDonationCategory Value { get; }
        public LockDonationCategoryOption(string name, LockDonationCategory value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "EquipmentManager_v1";
        public override string DisplayName => "Equipment Manager";
        public override string FolderName => "EquipmentManager";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<string> QualityOptions = new List<string>
        {
            "Poor",
            "Inferior",
            "Common",
            "Fine",
            "Masterwork",
            "Legendary"
        };

        private static readonly IReadOnlyList<AutoEquipCategoryOption> AutoEquipCategoryOptions = new List<AutoEquipCategoryOption>
        {
            new AutoEquipCategoryOption("None", AutoEquipCategory.None),
            new AutoEquipCategoryOption("Armor Only", AutoEquipCategory.ArmorOnly),
            new AutoEquipCategoryOption("Weapons Only", AutoEquipCategory.WeaponsOnly),
            new AutoEquipCategoryOption("Weapons & Armor", AutoEquipCategory.WeaponsAndArmor)
        };



        private static readonly IReadOnlyList<LoadoutPriorityOption> LoadoutPriorityOptions = new List<LoadoutPriorityOption>
        {
            new LoadoutPriorityOption("Sneaking > Civilian > Combat (Default)", LoadoutPriority.Sneaking_Civilian_Combat),
            new LoadoutPriorityOption("Sneaking > Combat > Civilian", LoadoutPriority.Sneaking_Combat_Civilian),
            new LoadoutPriorityOption("Combat > Sneaking > Civilian", LoadoutPriority.Combat_Sneaking_Civilian),
            new LoadoutPriorityOption("Combat > Civilian > Sneaking", LoadoutPriority.Combat_Civilian_Sneaking),
            new LoadoutPriorityOption("Civilian > Sneaking > Combat", LoadoutPriority.Civilian_Sneaking_Combat),
            new LoadoutPriorityOption("Civilian > Combat > Sneaking", LoadoutPriority.Civilian_Combat_Sneaking)
        };

        private static readonly IReadOnlyList<BuyEquipmentTargetOption> BuyEquipmentTargetOptions = new List<BuyEquipmentTargetOption>
        {
            new BuyEquipmentTargetOption("Player Only (Hand-Me-Downs)", BuyEquipmentTarget.PlayerOnly),
            new BuyEquipmentTargetOption("Player & Companions (Buy Direct)", BuyEquipmentTarget.PlayerAndCompanions)
        };

        private static readonly IReadOnlyList<LockDonationCategoryOption> LockDonationCategoryOptions = new List<LockDonationCategoryOption>
        {
            new LockDonationCategoryOption("None", LockDonationCategory.None),
            new LockDonationCategoryOption("Weapons Only", LockDonationCategory.WeaponsOnly),
            new LockDonationCategoryOption("Armor Only", LockDonationCategory.ArmorOnly),
            new LockDonationCategoryOption("Weapons & Armor", LockDonationCategory.WeaponsAndArmor)
        };

        [SettingPropertyBool("Auto-Equip Companions", RequireRestart = false,
            HintText = "Automatically equip companions when optimizing party equipment.", Order = 1)]
        [SettingPropertyGroup("General", GroupOrder = 4)]
        public bool AutoEquipCompanions { get; set; } = true;

        [SettingPropertyDropdown("Auto-Equip Loadout Category", RequireRestart = false,
            HintText = "Choose which categories of equipment to automatically manage.", Order = 2)]
        [SettingPropertyGroup("General", GroupOrder = 4)]
        public Dropdown<AutoEquipCategoryOption> AutoEquipCategoryDropdown { get; set; } =
            new Dropdown<AutoEquipCategoryOption>(AutoEquipCategoryOptions, 3); // Default: Weapons & Armor



        [SettingPropertyDropdown("Loadout Priority Order", RequireRestart = false,
            HintText = "Set the priority order for distributing equipment from inventory to loadouts.", Order = 1)]
        [SettingPropertyGroup("Civilian & Sneaking", GroupOrder = 3)]
        public Dropdown<LoadoutPriorityOption> LoadoutPriorityDropdown { get; set; } =
            new Dropdown<LoadoutPriorityOption>(LoadoutPriorityOptions, 0); // Default: Sneaking > Civilian > Combat (index 0)

        [SettingPropertyInteger("Min Tier to Keep", 0, 6, "0", RequireRestart = false,
            HintText = "Items at or above this tier will be locked/kept in your inventory and not donated/sold.", Order = 2)]
        [SettingPropertyGroup("Keep & Lock Rules", GroupOrder = 1)]
        public int MinTierToKeep { get; set; } = 4;

        [SettingPropertyDropdown("Min Quality to Keep", RequireRestart = false,
            HintText = "Items with modifiers at or above this quality level will be locked/kept.", Order = 3)]
        [SettingPropertyGroup("Keep & Lock Rules", GroupOrder = 1)]
        public Dropdown<string> MinQualityDropdown { get; set; } = new Dropdown<string>(QualityOptions, 3); // Default: Fine (index 3)

        [SettingPropertyBool("Keep Positive Modifiers", RequireRestart = false,
            HintText = "Keep any items that have positive stat modifiers regardless of quality level.", Order = 1)]
        [SettingPropertyGroup("Keep & Lock Rules", GroupOrder = 1)]
        public bool KeepPositiveModifiers { get; set; } = true;

        [SettingPropertyDropdown("Lock Donation Items", RequireRestart = false,
            HintText = "Keep donation weapons or armor locked in inventory instead of donating them for XP.", Order = 4)]
        [SettingPropertyGroup("Keep & Lock Rules", GroupOrder = 1)]
        public Dropdown<LockDonationCategoryOption> LockDonationCategoryDropdown { get; set; } =
            new Dropdown<LockDonationCategoryOption>(LockDonationCategoryOptions, 3); // Default: Weapons & Armor

        [SettingPropertyFloatingInteger("Max Cost per XP", 0.1f, 10.0f, "#0.0", RequireRestart = false,
            HintText = "Maximum denar value of gear to discard per XP point gained from donation perks.", Order = 2)]
        [SettingPropertyGroup("Economy & Auto-Sell", GroupOrder = 0)]
        public float MaxCostPerXp { get; set; } = 1.0f;

        [SettingPropertyBool("Sell Unlocked Equipment", RequireRestart = false,
            HintText = "Sell any non-locked equipment that isn't chosen for party use.", Order = 1)]
        [SettingPropertyGroup("Economy & Auto-Sell", GroupOrder = 0)]
        public bool SellUnlockedEquipment { get; set; } = true;

        [SettingPropertyBool("Prevent Equipment Sale in Villages", RequireRestart = false,
            HintText = "If enabled, auto-trading/selling will never sell equipment when entering villages. Safe to keep enabled to save valuable gear for rich towns.", Order = 3)]
        [SettingPropertyGroup("Economy & Auto-Sell", GroupOrder = 0)]
        public bool PreventEquipmentSaleInVillages { get; set; } = true;

        [SettingPropertyBool("Prioritize Weight/Value Ratio", RequireRestart = false,
            HintText = "If enabled, sort equipment by weight/value ratio descending before selling so that heavy, low-value items are sold first when town gold is low.", Order = 4)]
        [SettingPropertyGroup("Economy & Auto-Sell", GroupOrder = 0)]
        public bool PrioritizeHeavyTrash { get; set; } = true;


        [SettingPropertyBool("Limit to Carry Capacity", RequireRestart = false,
            HintText = "Stop actions if they would cause party to exceed carry capacity.", Order = 5)]
        [SettingPropertyGroup("Economy & Auto-Sell", GroupOrder = 0)]
        public bool LimitToInventoryCapacity { get; set; } = true;

        [SettingPropertyBool("Buy Stealth Gear Upgrades", RequireRestart = false,
            HintText = "Automatically buy 'Blackened' stealth items from merchants if they upgrade your sneaking slots.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 2)]
        public bool BuyStealthGear { get; set; } = false;

        [SettingPropertyBool("Buy Top Armor Upgrades", RequireRestart = false,
            HintText = "Automatically buy premium armor upgrades from merchants if you are very wealthy.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 2)]
        public bool BuyTopArmor { get; set; } = false;

        [SettingPropertyInteger("Min Gold to Buy Top Armor", 100000, 5000000, "0", RequireRestart = false,
            HintText = "Only buy premium armor upgrades if your current gold balance is at or above this amount. Default: 1M.", Order = 4)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 2)]
        public int BuyTopArmorGoldThreshold { get; set; } = 1000000;

        [SettingPropertyInteger("Min Tier to Buy Top Armor", 1, 6, "0", RequireRestart = false,
            HintText = "Only buy premium armor upgrades if the item tier is at or above this level (e.g. Tier 5 or 6). Default: 5.", Order = 5)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 2)]
        public int MinTierToBuyTopArmor { get; set; } = 5;

        [SettingPropertyDropdown("Buy Upgrades For", RequireRestart = false,
            HintText = "Player Only: buy upgrades only for Main Hero (companions get hand-me-downs). Player & Companions: buy direct upgrades for all.", Order = 3)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 2)]
        public Dropdown<BuyEquipmentTargetOption> BuyEquipmentTargetDropdown { get; set; } =
            new Dropdown<BuyEquipmentTargetOption>(BuyEquipmentTargetOptions, 0); // Default: Player Only (index 0)

        [SettingPropertyInteger("Minimum Gold Reserve", 1000, 100000, "0", RequireRestart = false,
            HintText = "Never let your gold drop below this amount when buying stealth gear. Default: 10,000 denars.", Order = 6)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 2)]
        public int MinimumGoldReserve { get; set; } = 10000;

        [SettingPropertyDropdown("Top Armor Spend Mode", RequireRestart = false,
            HintText = "Controls when premium armor upgrade requests run compared to other item requests.", Order = 7)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 2)]
        public Dropdown<RequestProfileOption> TopArmorSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Luxury));

        [SettingPropertyDropdown("Stealth Gear Spend Mode", RequireRestart = false,
            HintText = "Controls when stealth gear upgrade requests run compared to other item requests.", Order = 8)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 2)]
        public Dropdown<RequestProfileOption> StealthGearSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Luxury));


        // Compatibility wrappers
        public string MinQualityToKeep => MinQualityDropdown.SelectedValue;
        public AutoEquipCategory AutoEquipCategorySetting => AutoEquipCategoryDropdown.SelectedValue.Value;
        public LockDonationCategory LockDonationCategorySetting => LockDonationCategoryDropdown.SelectedValue.Value;

        public LoadoutPriority LoadoutPrioritySetting => LoadoutPriorityDropdown.SelectedValue.Value;
        public BuyEquipmentTarget BuyEquipmentTargetSetting => BuyEquipmentTargetDropdown.SelectedValue.Value;
        public RequestProfile TopArmorRequestProfile => TopArmorSpendModeDropdown.SelectedValue.Value;
        public RequestProfile StealthGearRequestProfile => StealthGearSpendModeDropdown.SelectedValue.Value;
    }
}
