using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace EquipmentManager
{
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

        [SettingPropertyBool("Auto-Equip Companions", RequireRestart = false,
            HintText = "Automatically equip companions when optimizing party equipment.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoEquipCompanions { get; set; } = true;

        [SettingPropertyBool("Auto-Equip Armor", RequireRestart = false,
            HintText = "Enable auto-equipping armor items.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoEquipArmor { get; set; } = true;

        [SettingPropertyBool("Auto-Equip Weapons", RequireRestart = false,
            HintText = "Enable auto-equipping weapon items.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoEquipWeapons { get; set; } = true;

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

        [SettingPropertyBool("Lock Donation Weapons", RequireRestart = false,
            HintText = "Keep donation weapons locked in inventory instead of donating them for XP.")]
        [SettingPropertyGroup("Keep Rules", GroupOrder = 2)]
        public bool LockDonationWeapons { get; set; } = true;

        [SettingPropertyBool("Lock Donation Armor", RequireRestart = false,
            HintText = "Keep donation armor locked in inventory instead of donating them for XP.")]
        [SettingPropertyGroup("Keep Rules", GroupOrder = 2)]
        public bool LockDonationArmor { get; set; } = true;

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

        [SettingPropertyBool("Prioritize Heavy Trash", RequireRestart = false,
            HintText = "If enabled, sort equipment by weight (descending) before selling so that the heaviest junk gets unloaded first if the town runs out of gold.")]
        [SettingPropertyGroup("Economy", GroupOrder = 3)]
        public bool PrioritizeHeavyTrash { get; set; } = false;

        [SettingPropertyBool("Limit to Inventory Capacity", RequireRestart = false,
            HintText = "Stop actions if they would cause party to exceed carry capacity.")]
        [SettingPropertyGroup("Economy", GroupOrder = 3)]
        public bool LimitToInventoryCapacity { get; set; } = true;

        // Compatibility wrappers
        public string MinQualityToKeep => MinQualityDropdown.SelectedValue;
    }
}
