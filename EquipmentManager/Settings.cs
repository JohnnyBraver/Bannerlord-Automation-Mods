using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

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

        private static readonly IReadOnlyList<LockDonationCategoryOption> LockDonationCategoryOptions = new List<LockDonationCategoryOption>
        {
            new LockDonationCategoryOption("None", LockDonationCategory.None),
            new LockDonationCategoryOption("Weapons Only", LockDonationCategory.WeaponsOnly),
            new LockDonationCategoryOption("Armor Only", LockDonationCategory.ArmorOnly),
            new LockDonationCategoryOption("Weapons & Armor", LockDonationCategory.WeaponsAndArmor)
        };

        [SettingPropertyBool("Auto-Equip Companions", RequireRestart = false,
            HintText = "Automatically equip companions when optimizing party equipment.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoEquipCompanions { get; set; } = true;

        [SettingPropertyDropdown("Auto-Equip Loadout Category", RequireRestart = false,
            HintText = "Choose which categories of equipment to automatically manage.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<AutoEquipCategoryOption> AutoEquipCategoryDropdown { get; set; } =
            new Dropdown<AutoEquipCategoryOption>(AutoEquipCategoryOptions, 3); // Default: Weapons & Armor

        [SettingPropertyBool("Optimize Civilian for Sneaking", RequireRestart = false,
            HintText = "When equipping for civilian loadouts, prioritize light/sneaky armor.")]
        [SettingPropertyGroup("Civilian & Sneaking", GroupOrder = 1)]
        public bool OptimizeCivilianForSneaking { get; set; } = false;

        [SettingPropertyFloatingInteger("Sneaking Weight Penalty Factor", 0.5f, 5.0f, "#0.0", RequireRestart = false,
            HintText = "Multiplier for armor weight penalty when optimizing for sneaky civilian gear.")]
        [SettingPropertyGroup("Civilian & Sneaking", GroupOrder = 1)]
        public float SneakingWeightPenaltyFactor { get; set; } = 2.0f;

        [SettingPropertyInteger("Min Tier to Keep", 0, 6, "0", RequireRestart = false,
            HintText = "Items at or above this tier will be locked/kept in your inventory and not donated/sold.")]
        [SettingPropertyGroup("Keep Rules", GroupOrder = 2)]
        public int MinTierToKeep { get; set; } = 5;

        [SettingPropertyDropdown("Min Quality to Keep", RequireRestart = false,
            HintText = "Items with modifiers at or above this quality level will be locked/kept.")]
        [SettingPropertyGroup("Keep Rules", GroupOrder = 2)]
        public Dropdown<string> MinQualityDropdown { get; set; } = new Dropdown<string>(QualityOptions, 3); // Default: Fine (index 3)

        [SettingPropertyBool("Keep Positive Modifiers", RequireRestart = false,
            HintText = "Keep any items that have positive stat modifiers regardless of quality level.")]
        [SettingPropertyGroup("Keep Rules", GroupOrder = 2)]
        public bool KeepPositiveModifiers { get; set; } = true;

        [SettingPropertyDropdown("Lock Donation Items", RequireRestart = false,
            HintText = "Keep donation weapons or armor locked in inventory instead of donating them for XP.")]
        [SettingPropertyGroup("Keep Rules", GroupOrder = 2)]
        public Dropdown<LockDonationCategoryOption> LockDonationCategoryDropdown { get; set; } =
            new Dropdown<LockDonationCategoryOption>(LockDonationCategoryOptions, 3); // Default: Weapons & Armor

        [SettingPropertyFloatingInteger("Max Cost per XP", 0.1f, 10.0f, "#0.0", RequireRestart = false,
            HintText = "Maximum denar value of gear to discard per XP point gained from donation perks.")]
        [SettingPropertyGroup("Economy", GroupOrder = 3)]
        public float MaxCostPerXp { get; set; } = 1.0f;

        [SettingPropertyBool("Sell Unlocked Equipment", RequireRestart = false,
            HintText = "Sell any non-locked equipment that isn't chosen for party use.")]
        [SettingPropertyGroup("Economy", GroupOrder = 3)]
        public bool SellUnlockedEquipment { get; set; } = true;

        [SettingPropertyBool("Prevent Equipment Sale in Villages", RequireRestart = false,
            HintText = "If enabled, auto-trading/selling will never sell equipment when entering villages. Safe to keep enabled to save valuable gear for rich towns.")]
        [SettingPropertyGroup("Economy", GroupOrder = 3)]
        public bool PreventEquipmentSaleInVillages { get; set; } = false;

        [SettingPropertyBool("Prioritize Weight/Value Ratio", RequireRestart = false,
            HintText = "If enabled, sort equipment by weight/value ratio descending before selling so that heavy, low-value items are sold first when town gold is low.")]
        [SettingPropertyGroup("Economy", GroupOrder = 3)]
        public bool PrioritizeHeavyTrash { get; set; } = false;

        [SettingPropertyBool("Limit to Carry Capacity", RequireRestart = false,
            HintText = "Stop actions if they would cause party to exceed carry capacity.")]
        [SettingPropertyGroup("Economy", GroupOrder = 3)]
        public bool LimitToInventoryCapacity { get; set; } = true;

        // Compatibility wrappers
        public string MinQualityToKeep => MinQualityDropdown.SelectedValue;
        public AutoEquipCategory AutoEquipCategorySetting => AutoEquipCategoryDropdown.SelectedValue.Value;
        public LockDonationCategory LockDonationCategorySetting => LockDonationCategoryDropdown.SelectedValue.Value;
    }
}
