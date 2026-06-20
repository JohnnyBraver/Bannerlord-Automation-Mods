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

    public enum KeepDonationCategory
    {
        None,
        WeaponsOnly,
        ArmorOnly,
        WeaponsAndArmor
    }

    public class KeepDonationCategoryOption
    {
        private readonly string _name;
        public KeepDonationCategory Value { get; }
        public KeepDonationCategoryOption(string name, KeepDonationCategory value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum EquipmentSaleReportDetailMode
    {
        CategoryCounts,
        FullItemList
    }

    public class EquipmentSaleReportDetailModeOption
    {
        private readonly string _name;
        public EquipmentSaleReportDetailMode Value { get; }
        public EquipmentSaleReportDetailModeOption(string name, EquipmentSaleReportDetailMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum EquipmentReportSortMode
    {
        Amount,
        MarketValue,
        PaidPrice
    }

    public class EquipmentReportSortModeOption
    {
        private readonly string _name;
        public EquipmentReportSortMode Value { get; }
        public EquipmentReportSortModeOption(string name, EquipmentReportSortMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum StealthGearPurchasePolicy
    {
        BlackenedOnly,
        AnyStealthCompatible
    }

    public class StealthGearPurchasePolicyOption
    {
        private readonly string _name;
        public StealthGearPurchasePolicy Value { get; }
        public StealthGearPurchasePolicyOption(string name, StealthGearPurchasePolicy value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum ProjectileUpgradePreference
    {
        CountAndDamage,
        CountOnly,
        DamageOnly
    }

    public class ProjectileUpgradePreferenceOption
    {
        private readonly string _name;
        public ProjectileUpgradePreference Value { get; }
        public ProjectileUpgradePreferenceOption(string name, ProjectileUpgradePreference value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "EquipmentManager_v0_4";
        public override string DisplayName => "Equipment Manager";
        public override string FolderName => "EquipmentManager";
        public override string FormatType => "json";

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

        private static readonly IReadOnlyList<KeepDonationCategoryOption> KeepDonationCategoryOptions = new List<KeepDonationCategoryOption>
        {
            new KeepDonationCategoryOption("None", KeepDonationCategory.None),
            new KeepDonationCategoryOption("Weapons Only", KeepDonationCategory.WeaponsOnly),
            new KeepDonationCategoryOption("Armor Only", KeepDonationCategory.ArmorOnly),
            new KeepDonationCategoryOption("Weapons & Armor", KeepDonationCategory.WeaponsAndArmor)
        };

        private static readonly IReadOnlyList<EquipmentSaleReportDetailModeOption> EquipmentSaleReportDetailModeOptions = new List<EquipmentSaleReportDetailModeOption>
        {
            new EquipmentSaleReportDetailModeOption("Category Counts", EquipmentSaleReportDetailMode.CategoryCounts),
            new EquipmentSaleReportDetailModeOption("Full Item List", EquipmentSaleReportDetailMode.FullItemList)
        };

        private static readonly IReadOnlyList<EquipmentReportSortModeOption> EquipmentReportSortModeOptions = new List<EquipmentReportSortModeOption>
        {
            new EquipmentReportSortModeOption("Amount", EquipmentReportSortMode.Amount),
            new EquipmentReportSortModeOption("Market Value", EquipmentReportSortMode.MarketValue),
            new EquipmentReportSortModeOption("Paid Price", EquipmentReportSortMode.PaidPrice)
        };

        private static readonly IReadOnlyList<StealthGearPurchasePolicyOption> StealthGearPurchasePolicyOptions = new List<StealthGearPurchasePolicyOption>
        {
            new StealthGearPurchasePolicyOption("Blackened Gear Only", StealthGearPurchasePolicy.BlackenedOnly),
            new StealthGearPurchasePolicyOption("Any Stealth-Compatible Gear", StealthGearPurchasePolicy.AnyStealthCompatible)
        };

        private static readonly IReadOnlyList<ProjectileUpgradePreferenceOption> ProjectileUpgradePreferenceOptions = new List<ProjectileUpgradePreferenceOption>
        {
            new ProjectileUpgradePreferenceOption("Count & Damage", ProjectileUpgradePreference.CountAndDamage),
            new ProjectileUpgradePreferenceOption("Count Only", ProjectileUpgradePreference.CountOnly),
            new ProjectileUpgradePreferenceOption("Damage Only", ProjectileUpgradePreference.DamageOnly)
        };

        // --- Group 0: General ---
        [SettingPropertyBool("Enable Equipment Manager", RequireRestart = false,
            HintText = "Master automation switch. When disabled, Equipment Manager will not react to settlement entry or post-battle loot. The manual inventory button still works.", Order = 0)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool ModEnabled { get; set; } = true;

        [SettingPropertyBool("Auto-Equip Companions", RequireRestart = false,
            HintText = "Automatically equip companions when optimizing party equipment.", Order = 1)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoEquipCompanions { get; set; } = true;

        [SettingPropertyDropdown("Auto-Equip Loadout Category", RequireRestart = false,
            HintText = "Choose which categories of equipment to automatically manage.", Order = 2)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<AutoEquipCategoryOption> AutoEquipCategoryDropdown { get; set; } =
            new Dropdown<AutoEquipCategoryOption>(AutoEquipCategoryOptions, 3); // Default: Weapons & Armor

        [SettingPropertyDropdown("Ammo Upgrade Preference", RequireRestart = false,
            HintText = "Controls whether ammo upgrades prefer stack count, damage, or require both to stay equal or better.", Order = 3)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<ProjectileUpgradePreferenceOption> AmmoUpgradePreferenceDropdown { get; set; } =
            new Dropdown<ProjectileUpgradePreferenceOption>(ProjectileUpgradePreferenceOptions, 0);

        [SettingPropertyDropdown("Throwing Weapon Upgrade Preference", RequireRestart = false,
            HintText = "Controls whether throwing weapon upgrades prefer stack count, missile damage, or require both to stay equal or better.", Order = 4)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<ProjectileUpgradePreferenceOption> ThrowingWeaponUpgradePreferenceDropdown { get; set; } =
            new Dropdown<ProjectileUpgradePreferenceOption>(ProjectileUpgradePreferenceOptions, 0);

        [SettingPropertyBool("Ignore Throwing Weapon Melee Stats", RequireRestart = false,
            HintText = "If enabled, compare throwing weapons by their stack count and missile damage preference without treating melee stats as drawbacks.", Order = 5)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool IgnoreThrowingWeaponMeleeStats { get; set; } = true;

        [SettingPropertyBool("Auto-Equip Before Settlement Trade", RequireRestart = false,
            HintText = "Run headless auto-equip before settlement automation decides what spare equipment can be sold.", Order = 6)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoEquipBeforeSettlementTrade { get; set; } = true;

        [SettingPropertyBool("Auto-Equip After Settlement Purchases", RequireRestart = false,
            HintText = "Run headless auto-equip after settlement automation buys equipment upgrades.", Order = 7)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoEquipAfterSettlementPurchases { get; set; } = true;

        [SettingPropertyBool("Auto-Equip After Battle Loot", RequireRestart = false,
            HintText = "Run headless auto-equip after post-battle loot adds equipment to the party inventory.", Order = 8)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool AutoEquipAfterBattleLoot { get; set; } = true;

        [SettingPropertyDropdown("Loadout Priority Order", RequireRestart = false,
            HintText = "Set the priority order for distributing equipment from inventory to loadouts.", Order = 9)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public Dropdown<LoadoutPriorityOption> LoadoutPriorityDropdown { get; set; } =
            new Dropdown<LoadoutPriorityOption>(LoadoutPriorityOptions, 0); // Default: Sneaking > Civilian > Combat (index 0)


        // --- Group 1: Keep & Sale Protection ---
        [SettingPropertyBool("Keep Positive Modifiers", RequireRestart = false,
            HintText = "Protect items with positive price/stat modifiers from automatic sale, even when they are below the tier threshold.", Order = 1)]
        [SettingPropertyGroup("Keep & Sale Protection", GroupOrder = 1)]
        public bool KeepPositiveModifiers { get; set; } = false;

        [SettingPropertyDropdown("Keep Donation Items", RequireRestart = false,
            HintText = "Protect cheap perk-donation weapons or armor from automatic sale. This does not change item lock icons.", Order = 2)]
        [SettingPropertyGroup("Keep & Sale Protection", GroupOrder = 1)]
        public Dropdown<KeepDonationCategoryOption> KeepDonationCategoryDropdown { get; set; } =
            new Dropdown<KeepDonationCategoryOption>(KeepDonationCategoryOptions, 3); // Default: Weapons & Armor

        [SettingPropertyInteger("Additional Armor Sets to Keep", 0, 10, "0", RequireRestart = false,
            HintText = "Keep the best spare armor pieces per enabled outfit type for future companions or family members. 0 disables this reserve.", Order = 3)]
        [SettingPropertyGroup("Keep & Sale Protection", GroupOrder = 1)]
        public int AdditionalArmorSetsToKeep { get; set; } = 0;

        [SettingPropertyBool("Keep Spare Combat Armor Sets", RequireRestart = false,
            HintText = "Protect the best spare combat armor pieces from automatic sale.", Order = 4)]
        [SettingPropertyGroup("Keep & Sale Protection", GroupOrder = 1)]
        public bool KeepSpareCombatArmorSets { get; set; } = false;

        [SettingPropertyBool("Keep Spare Civilian Armor Sets", RequireRestart = false,
            HintText = "Protect the best spare civilian armor pieces from automatic sale.", Order = 5)]
        [SettingPropertyGroup("Keep & Sale Protection", GroupOrder = 1)]
        public bool KeepSpareCivilianArmorSets { get; set; } = false;

        [SettingPropertyBool("Keep Spare Sneaking Armor Sets", RequireRestart = false,
            HintText = "Protect the best spare stealth/sneaking armor pieces from automatic sale.", Order = 6)]
        [SettingPropertyGroup("Keep & Sale Protection", GroupOrder = 1)]
        public bool KeepSpareSneakingArmorSets { get; set; } = false;


        // --- Group 2: Economy & Auto-Sell ---
        [SettingPropertyBool("Sell Unlocked Equipment", RequireRestart = false,
            HintText = "Sell equipment that is not manually locked and is not protected by the keep rules.", Order = 1)]
        [SettingPropertyGroup("Economy & Auto-Sell", GroupOrder = 2)]
        public bool SellUnlockedEquipment { get; set; } = true;

        private float _maxCostPerXp = 1.0f;
        [SettingPropertyFloatingInteger("Max Cost per XP", 0.1f, 10.0f, "#0.0", RequireRestart = false,
            HintText = "Maximum denar value of gear to discard per XP point gained from donation perks. Default: 1.0.", Order = 2)]
        [SettingPropertyGroup("Economy & Auto-Sell", GroupOrder = 2)]
        public float MaxCostPerXp
        {
            get => _maxCostPerXp;
            set => _maxCostPerXp = (float)System.Math.Round(value / 0.1f) * 0.1f;
        }

        [SettingPropertyBool("Prevent Equipment Sale in Villages", RequireRestart = false,
            HintText = "If enabled, auto-trading/selling will never sell equipment when entering villages. Safe to keep enabled to save valuable gear for rich towns.", Order = 3)]
        [SettingPropertyGroup("Economy & Auto-Sell", GroupOrder = 2)]
        public bool PreventEquipmentSaleInVillages { get; set; } = true;

        [SettingPropertyBool("Prioritize Weight/Value Ratio", RequireRestart = false,
            HintText = "If enabled, sort equipment by weight/value ratio descending before selling so that heavy, low-value items are sold first when town gold is low.", Order = 4)]
        [SettingPropertyGroup("Economy & Auto-Sell", GroupOrder = 2)]
        public bool PrioritizeHeavyTrash { get; set; } = true;


        // --- Group 3: Auto-Buy Upgrades ---
        [SettingPropertyBool("Buy Armor Upgrades", RequireRestart = false,
            HintText = "Automatically buy armor upgrades from merchants if your gold reserve and tier settings allow it.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public bool BuyArmorUpgrades { get; set; } = false;

        [SettingPropertyBool("Buy Hand-Slot Weapon Upgrades", RequireRestart = false,
            HintText = "Automatically buy direct upgrades for equipped battle/civilian hand-slot weapons. Stealth slots are not included.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public bool BuyHandSlotWeapons { get; set; } = false;

        [SettingPropertyBool("Buy Stealth/Blackened Gear Upgrades", RequireRestart = false,
            HintText = "Automatically buy stealth or blackened armor from merchants if it upgrades your sneaking slots.", Order = 3)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public bool BuyStealthGear { get; set; } = false;

        [SettingPropertyDropdown("Buy Upgrades For", RequireRestart = false,
            HintText = "Player Only: buy direct upgrades only for Main Hero (companions can still get hand-me-downs). Player & Companions: buy direct upgrades for all.", Order = 4)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public Dropdown<BuyEquipmentTargetOption> BuyEquipmentTargetDropdown { get; set; } =
            new Dropdown<BuyEquipmentTargetOption>(BuyEquipmentTargetOptions, 0); // Default: Player Only (index 0)

        [SettingPropertyInteger("Armor Upgrade Gold Reserve (x10k Denars)", 1, 100, "0", RequireRestart = false,
            HintText = "Only buy armor upgrades if the purchase leaves at least this much gold (in ten-thousands). Default: 1M.", Order = 5)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public int ArmorUpgradeGoldReserveTenK { get; set; } = 100;

        public int ArmorUpgradeGoldReserve => ArmorUpgradeGoldReserveTenK * 10000;

        [SettingPropertyInteger("Min Tier to Buy Armor Upgrades", 1, 6, "0", RequireRestart = false,
            HintText = "Only buy armor upgrades if the item tier is at or above this level (e.g. Tier 5 or 6). Default: 5.", Order = 6)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public int MinTierToBuyArmorUpgrades { get; set; } = 5;

        [SettingPropertyInteger("Weapon Gold Reserve (x10k Denars)", 1, 100, "0", RequireRestart = false,
            HintText = "Never let your gold drop below this amount when buying hand-slot weapon upgrades (in ten-thousands). Default: 100k.", Order = 7)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public int BuyWeaponGoldReserveTenK { get; set; } = 10;

        public int BuyWeaponGoldReserve => BuyWeaponGoldReserveTenK * 10000;

        [SettingPropertyInteger("Max Armor Upgrades per Visit", 1, 10, RequireRestart = false,
            HintText = "Maximum armor upgrades EquipmentManager may buy during one settlement automation cycle. Default: 1.", Order = 8)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public int MaxArmorUpgradesPerVisit { get; set; } = 1;

        [SettingPropertyInteger("Max Hand-Slot Weapon Upgrades per Visit", 1, 10, RequireRestart = false,
            HintText = "Maximum hand-slot weapon upgrades EquipmentManager may buy during one settlement automation cycle. Default: 1.", Order = 9)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public int MaxHandSlotWeaponUpgradesPerVisit { get; set; } = 1;

        [SettingPropertyInteger("Stealth Gear Gold Reserve (x10k Denars)", 1, 100, "0", RequireRestart = false,
            HintText = "Never let your gold drop below this amount when buying stealth gear (in ten-thousands). Default: 10k.", Order = 10)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public int MinimumGoldReserveTenK { get; set; } = 1;

        public int MinimumGoldReserve => MinimumGoldReserveTenK * 10000;

        [SettingPropertyDropdown("Stealth Gear Purchase Policy", RequireRestart = false,
            HintText = "Controls which stealth-compatible armor can be auto-bought for sneaking slots. Default: Blackened Gear Only.", Order = 11)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public Dropdown<StealthGearPurchasePolicyOption> StealthGearPurchasePolicyDropdown { get; set; } =
            new Dropdown<StealthGearPurchasePolicyOption>(StealthGearPurchasePolicyOptions, 0);

        [SettingPropertyDropdown("Armor Upgrade Spend Mode", RequireRestart = false,
            HintText = "Controls when armor upgrade requests run compared to other item requests.", Order = 12)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public Dropdown<RequestProfileOption> ArmorUpgradeSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Luxury));

        [SettingPropertyDropdown("Hand-Slot Weapon Upgrade Spend Mode", RequireRestart = false,
            HintText = "Controls when hand-slot weapon upgrade requests run compared to other item requests.", Order = 13)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public Dropdown<RequestProfileOption> WeaponSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Luxury));

        [SettingPropertyDropdown("Stealth Gear Spend Mode", RequireRestart = false,
            HintText = "Controls when stealth gear upgrade requests run compared to other item requests.", Order = 14)]
        [SettingPropertyGroup("Auto-Buy Upgrades", GroupOrder = 3)]
        public Dropdown<RequestProfileOption> StealthGearSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Luxury));


        // --- Group 3: Auto-Buy Upgrades/Weapon Categories ---
        [SettingPropertyBool("Buy One-Handed Weapon Upgrades", RequireRestart = false,
            HintText = "Allow hand-slot auto-buy to request one-handed melee weapon upgrades.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy Upgrades/Weapon Categories", GroupOrder = 3)]
        public bool BuyOneHandedWeaponUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Two-Handed Weapon Upgrades", RequireRestart = false,
            HintText = "Allow hand-slot auto-buy to request two-handed melee weapon upgrades.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy Upgrades/Weapon Categories", GroupOrder = 3)]
        public bool BuyTwoHandedWeaponUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Polearm Upgrades", RequireRestart = false,
            HintText = "Allow hand-slot auto-buy to request polearm upgrades.", Order = 3)]
        [SettingPropertyGroup("Auto-Buy Upgrades/Weapon Categories", GroupOrder = 3)]
        public bool BuyPolearmUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Throwing Weapon Upgrades", RequireRestart = false,
            HintText = "Allow hand-slot auto-buy to request throwing weapon upgrades.", Order = 4)]
        [SettingPropertyGroup("Auto-Buy Upgrades/Weapon Categories", GroupOrder = 3)]
        public bool BuyThrowingWeaponUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Bow Upgrades", RequireRestart = false,
            HintText = "Allow hand-slot auto-buy to request bow upgrades. Arrows are still left to normal inventory management.", Order = 5)]
        [SettingPropertyGroup("Auto-Buy Upgrades/Weapon Categories", GroupOrder = 3)]
        public bool BuyBowUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Crossbow Upgrades", RequireRestart = false,
            HintText = "Allow hand-slot auto-buy to request crossbow upgrades. Bolts are still left to normal inventory management.", Order = 6)]
        [SettingPropertyGroup("Auto-Buy Upgrades/Weapon Categories", GroupOrder = 3)]
        public bool BuyCrossbowUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Shield Upgrades", RequireRestart = false,
            HintText = "Allow hand-slot auto-buy to request shield upgrades.", Order = 7)]
        [SettingPropertyGroup("Auto-Buy Upgrades/Weapon Categories", GroupOrder = 3)]
        public bool BuyShieldUpgrades { get; set; } = true;


        // --- Group 4: Equipment Reporting ---
        [SettingPropertyDropdown("Sale Report Detail", RequireRestart = false,
            HintText = "Category Counts reports spare gear sales as armor/weapon totals. Full Item List prints individual sold equipment.", Order = 1)]
        [SettingPropertyGroup("Equipment Reporting", GroupOrder = 4)]
        public Dropdown<EquipmentSaleReportDetailModeOption> EquipmentSaleReportDetailDropdown { get; set; } =
            new Dropdown<EquipmentSaleReportDetailModeOption>(EquipmentSaleReportDetailModeOptions, 0);

        [SettingPropertyInteger("Max Items to Print Details For", 1, 20, RequireRestart = false,
            HintText = "Maximum individual equipment item types shown when printing detailed report lines.", Order = 2)]
        [SettingPropertyGroup("Equipment Reporting", GroupOrder = 4)]
        public int MaxReportItemsToPrint { get; set; } = 4;

        [SettingPropertyDropdown("Sorting Mode", RequireRestart = false,
            HintText = "Choose how detailed equipment report lines select which items to print.", Order = 3)]
        [SettingPropertyGroup("Equipment Reporting", GroupOrder = 4)]
        public Dropdown<EquipmentReportSortModeOption> EquipmentReportSortDropdown { get; set; } =
            new Dropdown<EquipmentReportSortModeOption>(EquipmentReportSortModeOptions, 2);

        // Compatibility wrappers
        public AutoEquipCategory AutoEquipCategorySetting => AutoEquipCategoryDropdown.SelectedValue.Value;
        public ProjectileUpgradePreference AmmoUpgradePreferenceSetting => AmmoUpgradePreferenceDropdown.SelectedValue.Value;
        public ProjectileUpgradePreference ThrowingWeaponUpgradePreferenceSetting => ThrowingWeaponUpgradePreferenceDropdown.SelectedValue.Value;
        public KeepDonationCategory KeepDonationCategorySetting => KeepDonationCategoryDropdown.SelectedValue.Value;

        public LoadoutPriority LoadoutPrioritySetting => LoadoutPriorityDropdown.SelectedValue.Value;
        public BuyEquipmentTarget BuyEquipmentTargetSetting => BuyEquipmentTargetDropdown.SelectedValue.Value;
        public RequestProfile ArmorUpgradeRequestProfile => ArmorUpgradeSpendModeDropdown.SelectedValue.Value;
        public RequestProfile WeaponRequestProfile => WeaponSpendModeDropdown.SelectedValue.Value;
        public RequestProfile StealthGearRequestProfile => StealthGearSpendModeDropdown.SelectedValue.Value;
        public StealthGearPurchasePolicy StealthGearPurchasePolicySetting => StealthGearPurchasePolicyDropdown.SelectedValue.Value;
        public EquipmentSaleReportDetailMode EquipmentSaleReportDetail => EquipmentSaleReportDetailDropdown.SelectedValue.Value;
        public EquipmentReportSortMode EquipmentReportSort => EquipmentReportSortDropdown.SelectedValue.Value;
    }
}
