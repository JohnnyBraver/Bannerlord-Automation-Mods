using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace EquipmentManager
{
    public enum AutoEquipCategory
    {
        Disabled,
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

    public enum CombatUpgradeCheckOrder
    {
        BattleArmorFirst,
        WeaponsFirst
    }

    public class CombatUpgradeCheckOrderOption
    {
        private readonly string _name;
        public CombatUpgradeCheckOrder Value { get; }
        public CombatUpgradeCheckOrderOption(string name, CombatUpgradeCheckOrder value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum SpareArmorLoadouts
    {
        Disabled,
        Battle,
        Civilian,
        BattleAndCivilian
    }

    public class SpareArmorLoadoutsOption
    {
        private readonly string _name;
        public SpareArmorLoadouts Value { get; }
        public SpareArmorLoadoutsOption(string name, SpareArmorLoadouts value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum AutoSellCategory
    {
        Disabled,
        ArmorOnly,
        WeaponsOnly,
        WeaponsAndArmor
    }

    public class AutoSellCategoryOption
    {
        private readonly string _name;
        public AutoSellCategory Value { get; }
        public AutoSellCategoryOption(string name, AutoSellCategory value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum PositiveModifierProtectionCategory
    {
        Disabled,
        ArmorOnly,
        WeaponsOnly,
        WeaponsAndArmor
    }

    public class PositiveModifierProtectionCategoryOption
    {
        private readonly string _name;
        public PositiveModifierProtectionCategory Value { get; }
        public PositiveModifierProtectionCategoryOption(string name, PositiveModifierProtectionCategory value) { _name = name; Value = value; }
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

    public enum MeleeWeaponUpgradePreference
    {
        Balanced,
        DamageAndReach,
        SpeedAndHandling
    }

    public class MeleeWeaponUpgradePreferenceOption
    {
        private readonly string _name;
        public MeleeWeaponUpgradePreference Value { get; }
        public MeleeWeaponUpgradePreferenceOption(string name, MeleeWeaponUpgradePreference value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum RangedWeaponUpgradePreference
    {
        Balanced,
        Damage,
        Accuracy
    }

    public class RangedWeaponUpgradePreferenceOption
    {
        private readonly string _name;
        public RangedWeaponUpgradePreference Value { get; }
        public RangedWeaponUpgradePreferenceOption(string name, RangedWeaponUpgradePreference value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum ShieldUpgradePreference
    {
        Balanced,
        MaxHitPoints,
        MaxSize
    }

    public class ShieldUpgradePreferenceOption
    {
        private readonly string _name;
        public ShieldUpgradePreference Value { get; }
        public ShieldUpgradePreferenceOption(string name, ShieldUpgradePreference value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum WeaponPropertyMatching
    {
        PreserveAll,
        IgnoreMinor,
        StatsOnly
    }

    public class WeaponPropertyMatchingOption
    {
        private readonly string _name;
        public WeaponPropertyMatching Value { get; }
        public WeaponPropertyMatchingOption(string name, WeaponPropertyMatching value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "EquipmentManager_v0_5_1";
        public override string DisplayName => "Equipment Manager";
        public override string FolderName => "EquipmentManager";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<AutoEquipCategoryOption> AutoEquipCategoryOptions = new List<AutoEquipCategoryOption>
        {
            new AutoEquipCategoryOption("Disabled", AutoEquipCategory.Disabled),
            new AutoEquipCategoryOption("Armor Only", AutoEquipCategory.ArmorOnly),
            new AutoEquipCategoryOption("Weapons Only", AutoEquipCategory.WeaponsOnly),
            new AutoEquipCategoryOption("Weapons & Armor", AutoEquipCategory.WeaponsAndArmor)
        };



        private static readonly IReadOnlyList<LoadoutPriorityOption> LoadoutPriorityOptions = new List<LoadoutPriorityOption>
        {
            new LoadoutPriorityOption("Sneaking > Civilian > Combat", LoadoutPriority.Sneaking_Civilian_Combat),
            new LoadoutPriorityOption("Sneaking > Combat > Civilian", LoadoutPriority.Sneaking_Combat_Civilian),
            new LoadoutPriorityOption("Combat > Sneaking > Civilian", LoadoutPriority.Combat_Sneaking_Civilian),
            new LoadoutPriorityOption("Combat > Civilian > Sneaking (Default)", LoadoutPriority.Combat_Civilian_Sneaking),
            new LoadoutPriorityOption("Civilian > Sneaking > Combat", LoadoutPriority.Civilian_Sneaking_Combat),
            new LoadoutPriorityOption("Civilian > Combat > Sneaking", LoadoutPriority.Civilian_Combat_Sneaking)
        };

        private static readonly IReadOnlyList<BuyEquipmentTargetOption> BuyEquipmentTargetOptions = new List<BuyEquipmentTargetOption>
        {
            new BuyEquipmentTargetOption("Player Only (Hand-Me-Downs)", BuyEquipmentTarget.PlayerOnly),
            new BuyEquipmentTargetOption("Player & Companions (Buy Direct)", BuyEquipmentTarget.PlayerAndCompanions)
        };

        private static readonly IReadOnlyList<CombatUpgradeCheckOrderOption> CombatUpgradeCheckOrderOptions = new List<CombatUpgradeCheckOrderOption>
        {
            new CombatUpgradeCheckOrderOption("Battle Armor First", CombatUpgradeCheckOrder.BattleArmorFirst),
            new CombatUpgradeCheckOrderOption("Weapons First", CombatUpgradeCheckOrder.WeaponsFirst)
        };

        private static readonly IReadOnlyList<SpareArmorLoadoutsOption> SpareArmorLoadoutsOptions = new List<SpareArmorLoadoutsOption>
        {
            new SpareArmorLoadoutsOption("Disabled", SpareArmorLoadouts.Disabled),
            new SpareArmorLoadoutsOption("Battle Loadout", SpareArmorLoadouts.Battle),
            new SpareArmorLoadoutsOption("Civilian Loadout", SpareArmorLoadouts.Civilian),
            new SpareArmorLoadoutsOption("Battle & Civilian Loadouts", SpareArmorLoadouts.BattleAndCivilian)
        };

        private static readonly IReadOnlyList<KeepDonationCategoryOption> KeepDonationCategoryOptions = new List<KeepDonationCategoryOption>
        {
            new KeepDonationCategoryOption("None", KeepDonationCategory.None),
            new KeepDonationCategoryOption("Weapons Only", KeepDonationCategory.WeaponsOnly),
            new KeepDonationCategoryOption("Armor Only", KeepDonationCategory.ArmorOnly),
            new KeepDonationCategoryOption("Weapons & Armor", KeepDonationCategory.WeaponsAndArmor)
        };

        private static readonly IReadOnlyList<AutoSellCategoryOption> AutoSellCategoryOptions = new List<AutoSellCategoryOption>
        {
            new AutoSellCategoryOption("Disabled", AutoSellCategory.Disabled),
            new AutoSellCategoryOption("Armor Only", AutoSellCategory.ArmorOnly),
            new AutoSellCategoryOption("Weapons Only", AutoSellCategory.WeaponsOnly),
            new AutoSellCategoryOption("Weapons & Armor", AutoSellCategory.WeaponsAndArmor)
        };

        private static readonly IReadOnlyList<PositiveModifierProtectionCategoryOption> PositiveModifierProtectionCategoryOptions = new List<PositiveModifierProtectionCategoryOption>
        {
            new PositiveModifierProtectionCategoryOption("Disabled", PositiveModifierProtectionCategory.Disabled),
            new PositiveModifierProtectionCategoryOption("Armor Only", PositiveModifierProtectionCategory.ArmorOnly),
            new PositiveModifierProtectionCategoryOption("Weapons Only", PositiveModifierProtectionCategory.WeaponsOnly),
            new PositiveModifierProtectionCategoryOption("Weapons & Armor", PositiveModifierProtectionCategory.WeaponsAndArmor)
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

        private static readonly IReadOnlyList<MeleeWeaponUpgradePreferenceOption> MeleeWeaponUpgradePreferenceOptions = new List<MeleeWeaponUpgradePreferenceOption>
        {
            new MeleeWeaponUpgradePreferenceOption("Balanced", MeleeWeaponUpgradePreference.Balanced),
            new MeleeWeaponUpgradePreferenceOption("Damage & Reach", MeleeWeaponUpgradePreference.DamageAndReach),
            new MeleeWeaponUpgradePreferenceOption("Speed & Handling", MeleeWeaponUpgradePreference.SpeedAndHandling)
        };

        private static readonly IReadOnlyList<RangedWeaponUpgradePreferenceOption> RangedWeaponUpgradePreferenceOptions = new List<RangedWeaponUpgradePreferenceOption>
        {
            new RangedWeaponUpgradePreferenceOption("Balanced", RangedWeaponUpgradePreference.Balanced),
            new RangedWeaponUpgradePreferenceOption("Damage", RangedWeaponUpgradePreference.Damage),
            new RangedWeaponUpgradePreferenceOption("Accuracy", RangedWeaponUpgradePreference.Accuracy)
        };

        private static readonly IReadOnlyList<ShieldUpgradePreferenceOption> ShieldUpgradePreferenceOptions = new List<ShieldUpgradePreferenceOption>
        {
            new ShieldUpgradePreferenceOption("Balanced", ShieldUpgradePreference.Balanced),
            new ShieldUpgradePreferenceOption("Max HP", ShieldUpgradePreference.MaxHitPoints),
            new ShieldUpgradePreferenceOption("Max Size", ShieldUpgradePreference.MaxSize)
        };

        private static readonly IReadOnlyList<WeaponPropertyMatchingOption> WeaponPropertyMatchingOptions = new List<WeaponPropertyMatchingOption>
        {
            new WeaponPropertyMatchingOption("Preserve All", WeaponPropertyMatching.PreserveAll),
            new WeaponPropertyMatchingOption("Ignore Minor", WeaponPropertyMatching.IgnoreMinor),
            new WeaponPropertyMatchingOption("Stats Only", WeaponPropertyMatching.StatsOnly)
        };

        // --- Group 0: General ---
        [SettingPropertyBool("Enable Equipment Manager", RequireRestart = false,
            HintText = "Master automation switch. When disabled, Equipment Manager will not react to settlement entry or post-battle loot. The manual inventory button still works.", Order = 0)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool ModEnabled { get; set; } = true;

        // --- Group 1: Auto-Equip ---
        [SettingPropertyBool("Auto-Equip Companions", RequireRestart = false,
            HintText = "Automatically equip companions when optimizing party equipment.", Order = 1)]
        [SettingPropertyGroup("Auto-Equip", GroupOrder = 1)]
        public bool AutoEquipCompanions { get; set; } = true;

        [SettingPropertyDropdown("Auto-Equip Equipment Types", RequireRestart = false,
            HintText = "Choose whether auto-equip manages armor, weapons, or both. This affects equipping only; it never changes auto-sell.", Order = 2)]
        [SettingPropertyGroup("Auto-Equip", GroupOrder = 1)]
        public Dropdown<AutoEquipCategoryOption> AutoEquipCategoryDropdown { get; set; } =
            new Dropdown<AutoEquipCategoryOption>(AutoEquipCategoryOptions, 3); // Default: Weapons & Armor

        [SettingPropertyDropdown("Loadout Priority Order", RequireRestart = false,
            HintText = "Set the order in which available equipment is distributed between combat, civilian, and sneaking loadouts.", Order = 3)]
        [SettingPropertyGroup("Auto-Equip", GroupOrder = 1)]
        public Dropdown<LoadoutPriorityOption> LoadoutPriorityDropdown { get; set; } =
            new Dropdown<LoadoutPriorityOption>(LoadoutPriorityOptions, 3); // Default: Combat > Civilian > Sneaking (index 3)

        // --- Group 2: Auto-Sell ---
        [SettingPropertyDropdown("Auto-Sell Equipment", RequireRestart = false,
            HintText = "Choose which equipment types may be sold automatically. Manually locked items and all keep/protection rules remain protected.", Order = 1)]
        [SettingPropertyGroup("Auto-Sell", GroupOrder = 2)]
        public Dropdown<AutoSellCategoryOption> AutoSellCategoryDropdown { get; set; } =
            new Dropdown<AutoSellCategoryOption>(AutoSellCategoryOptions, 3);

        [SettingPropertyBool("Prevent Equipment Sale in Villages", RequireRestart = false,
            HintText = "Block automatic equipment sales in villages. Manual sales are unaffected, so gear can be saved for richer towns.", Order = 3)]
        [SettingPropertyGroup("Auto-Sell", GroupOrder = 2)]
        public bool PreventEquipmentSaleInVillages { get; set; } = true;

        [SettingPropertyBool("Prioritize Weight/Value Ratio", RequireRestart = false,
            HintText = "If enabled, sort equipment by weight/value ratio descending before selling so that heavy, low-value items are sold first when town gold is low.", Order = 4)]
        [SettingPropertyGroup("Auto-Sell", GroupOrder = 2)]
        public bool PrioritizeHeavyTrash { get; set; } = true;

        // --- Group 3: Auto-Sell / Protection ---
        [SettingPropertyDropdown("Keep Positive Modifiers", RequireRestart = false,
            HintText = "Choose whether positive modifiers protect armor, weapons, or both from automatic sale. Banners are not included.", Order = 1)]
        [SettingPropertyGroup("Auto-Sell/Protection", GroupOrder = 3)]
        public Dropdown<PositiveModifierProtectionCategoryOption> PositiveModifierProtectionCategoryDropdown { get; set; } =
            new Dropdown<PositiveModifierProtectionCategoryOption>(PositiveModifierProtectionCategoryOptions, 0);

        [SettingPropertyDropdown("Keep Donation Items", RequireRestart = false,
            HintText = "With the applicable donation perk, protect cheap weapons and/or armor from automatic sale. This does not change item lock icons.", Order = 2)]
        [SettingPropertyGroup("Auto-Sell/Donation Protection", GroupOrder = 4)]
        public Dropdown<KeepDonationCategoryOption> KeepDonationCategoryDropdown { get; set; } =
            new Dropdown<KeepDonationCategoryOption>(KeepDonationCategoryOptions, 3); // Default: Weapons & Armor

        [SettingPropertyDropdown("Reserve Spare Armor For", RequireRestart = false,
            HintText = "Protect the best spare armor for the selected loadouts. Stealth gear is never reserved.", Order = 3)]
        [SettingPropertyGroup("Auto-Sell/Protection", GroupOrder = 3)]
        public Dropdown<SpareArmorLoadoutsOption> SpareArmorLoadoutsDropdown { get; set; } =
            new Dropdown<SpareArmorLoadoutsOption>(SpareArmorLoadoutsOptions, 0);

        [SettingPropertyInteger("Additional Armor Sets to Keep", 0, 5, "0", RequireRestart = false,
            HintText = "For each selected loadout, protect this many of the best spare pieces for every armor slot. 0 disables the reserve.", Order = 4)]
        [SettingPropertyGroup("Auto-Sell/Protection", GroupOrder = 3)]
        public int AdditionalArmorSetsToKeep { get; set; } = 0;

        private float _maxCostPerXp = 1.0f;
        [SettingPropertyFloatingInteger("Donation Gear Max Value per XP", 0.1f, 10.0f, "#0.0", RequireRestart = false,
            HintText = "When donation protection is enabled, only protect gear whose sale value is at or below this many denars per donation XP. Default: 1.0.", Order = 5)]
        [SettingPropertyGroup("Auto-Sell/Donation Protection", GroupOrder = 4)]
        public float MaxCostPerXp
        {
            get => _maxCostPerXp;
            set => _maxCostPerXp = (float)System.Math.Round(value / 0.1f) * 0.1f;
        }
        // --- Group 4: Auto-Buy ---
        [SettingPropertyDropdown("Buy Upgrades For", RequireRestart = false,
            HintText = "Choose who receives direct merchant purchases. Player-only purchases can still become companion hand-me-downs through auto-equip.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy", GroupOrder = 5)]
        public Dropdown<BuyEquipmentTargetOption> BuyEquipmentTargetDropdown { get; set; } =
            new Dropdown<BuyEquipmentTargetOption>(BuyEquipmentTargetOptions, 0);

        [SettingPropertyDropdown("Combat Upgrade Check Order", RequireRestart = false,
            HintText = "When battle armor and weapons can both buy upgrades, evaluate the selected combat track first. Civilian and stealth upgrades always follow combat gear.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy", GroupOrder = 5)]
        public Dropdown<CombatUpgradeCheckOrderOption> CombatUpgradeCheckOrderDropdown { get; set; } =
            new Dropdown<CombatUpgradeCheckOrderOption>(CombatUpgradeCheckOrderOptions, 0);

        // --- Group 5: Auto-Buy / Armor / Battle Armor ---
        [SettingPropertyBool("Buy Battle Armor Upgrades", RequireRestart = false,
            HintText = "Buy armor upgrades for battle loadouts only. Civilian and stealth gear have their own controls and budgets.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy/Armor/Battle Armor", GroupOrder = 6)]
        public bool BuyBattleArmorUpgrades { get; set; } = true;

        private int _battleArmorGoldReserve = 1000000;
        [SettingPropertyInteger("Battle Armor Gold Reserve", 10000, 1000000, "0", RequireRestart = false,
            HintText = "Do not buy battle armor if the purchase would leave less gold than this reserve. Default: 1M. Snapping step: 10,000.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy/Armor/Battle Armor", GroupOrder = 6)]
        public int BattleArmorGoldReserve
        {
            get => _battleArmorGoldReserve;
            set => _battleArmorGoldReserve = ((value + 5000) / 10000) * 10000;
        }

        [SettingPropertyInteger("Minimum Battle Armor Tier", 1, 6, "0", RequireRestart = false,
            HintText = "Only buy battle armor at or above this tier. Default: 5.", Order = 3)]
        [SettingPropertyGroup("Auto-Buy/Armor/Battle Armor", GroupOrder = 6)]
        public int MinimumBattleArmorTier { get; set; } = 5;

        [SettingPropertyInteger("Max Battle Armor Upgrades per Visit", 1, 10, RequireRestart = false,
            HintText = "Maximum battle-armor purchases in one settlement visit. Default: 1.", Order = 4)]
        [SettingPropertyGroup("Auto-Buy/Armor/Battle Armor", GroupOrder = 6)]
        public int MaxBattleArmorUpgradesPerVisit { get; set; } = 1;

        // --- Group 6: Auto-Buy / Weapons ---
        [SettingPropertyBool("Buy Hand-Slot Weapon Upgrades", RequireRestart = false,
            HintText = "Buy direct upgrades for battle and civilian hand-slot weapons. Stealth slots are not included.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy/Weapons", GroupOrder = 9)]
        public bool BuyHandSlotWeapons { get; set; } = true;

        private int _weaponGoldReserve = 100000;
        [SettingPropertyInteger("Weapon Gold Reserve", 10000, 1000000, "0", RequireRestart = false,
            HintText = "Do not buy weapons if the purchase would leave less gold than this reserve. Default: 100k. Snapping step: 10,000.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy/Weapons", GroupOrder = 9)]
        public int WeaponGoldReserve
        {
            get => _weaponGoldReserve;
            set => _weaponGoldReserve = ((value + 5000) / 10000) * 10000;
        }

        [SettingPropertyInteger("Max Weapon Upgrades per Visit", 1, 10, RequireRestart = false,
            HintText = "Maximum hand-slot weapon purchases in one settlement visit. Default: 1.", Order = 3)]
        [SettingPropertyGroup("Auto-Buy/Weapons", GroupOrder = 9)]
        public int MaxWeaponUpgradesPerVisit { get; set; } = 1;

        // --- Group 9: Auto-Buy / Armor / Civilian Armor ---
        [SettingPropertyBool("Buy Civilian Armor Upgrades", RequireRestart = false,
            HintText = "Buy armor upgrades for civilian loadouts only. Disable this to keep automatic purchases focused on combat gear.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy/Armor/Civilian Armor", GroupOrder = 7)]
        public bool BuyCivilianArmorUpgrades { get; set; } = false;

        private int _civilianArmorGoldReserve = 100000;
        [SettingPropertyInteger("Civilian Armor Gold Reserve", 10000, 1000000, "0", RequireRestart = false,
            HintText = "Do not buy civilian armor if the purchase would leave less gold than this reserve. Default: 100k. Snapping step: 10,000.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy/Armor/Civilian Armor", GroupOrder = 7)]
        public int CivilianArmorGoldReserve
        {
            get => _civilianArmorGoldReserve;
            set => _civilianArmorGoldReserve = ((value + 5000) / 10000) * 10000;
        }

        [SettingPropertyInteger("Minimum Civilian Armor Tier", 1, 6, "0", RequireRestart = false,
            HintText = "Only buy civilian armor at or above this tier. Default: 2.", Order = 3)]
        [SettingPropertyGroup("Auto-Buy/Armor/Civilian Armor", GroupOrder = 7)]
        public int MinimumCivilianArmorTier { get; set; } = 2;

        [SettingPropertyInteger("Max Civilian Armor Upgrades per Visit", 1, 10, RequireRestart = false,
            HintText = "Maximum civilian-armor purchases in one settlement visit. Default: 1.", Order = 4)]
        [SettingPropertyGroup("Auto-Buy/Armor/Civilian Armor", GroupOrder = 7)]
        public int MaxCivilianArmorUpgradesPerVisit { get; set; } = 1;

        // --- Group 10: Auto-Buy / Armor / Stealth Gear ---
        [SettingPropertyBool("Buy Stealth Gear Upgrades", RequireRestart = false,
            HintText = "Buy armor upgrades for stealth loadouts only. Stealth gear has its own purchase policy, reserve, and visit cap.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy/Armor/Stealth Gear", GroupOrder = 8)]
        public bool BuyStealthGear { get; set; } = false;

        [SettingPropertyDropdown("Stealth Gear Purchase Policy", RequireRestart = false,
            HintText = "Choose which stealth-compatible armor can be auto-bought. Default: Blackened Gear Only.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy/Armor/Stealth Gear", GroupOrder = 8)]
        public Dropdown<StealthGearPurchasePolicyOption> StealthGearPurchasePolicyDropdown { get; set; } =
            new Dropdown<StealthGearPurchasePolicyOption>(StealthGearPurchasePolicyOptions, 0);

        private int _stealthGearGoldReserve = 10000;
        [SettingPropertyInteger("Stealth Gear Gold Reserve", 10000, 1000000, "0", RequireRestart = false,
            HintText = "Do not buy stealth gear if the purchase would leave less gold than this reserve. Default: 10k. Snapping step: 10,000.", Order = 3)]
        [SettingPropertyGroup("Auto-Buy/Armor/Stealth Gear", GroupOrder = 8)]
        public int StealthGearGoldReserve
        {
            get => _stealthGearGoldReserve;
            set => _stealthGearGoldReserve = ((value + 5000) / 10000) * 10000;
        }

        [SettingPropertyInteger("Max Stealth Gear Upgrades per Visit", 1, 10, RequireRestart = false,
            HintText = "Maximum stealth-gear purchases in one settlement visit. Default: 1.", Order = 4)]
        [SettingPropertyGroup("Auto-Buy/Armor/Stealth Gear", GroupOrder = 8)]
        public int MaxStealthGearUpgradesPerVisit { get; set; } = 1;

        // --- Group 7: Auto-Buy / Weapons / Allowed Weapon Types ---
        [SettingPropertyBool("Buy One-Handed Weapon Upgrades", RequireRestart = false,
            HintText = "Allow one-handed melee purchases when weapon auto-buy is enabled.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Allowed Weapon Types", GroupOrder = 10)]
        public bool BuyOneHandedWeaponUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Two-Handed Weapon Upgrades", RequireRestart = false,
            HintText = "Allow two-handed melee purchases when weapon auto-buy is enabled.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Allowed Weapon Types", GroupOrder = 10)]
        public bool BuyTwoHandedWeaponUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Polearm Upgrades", RequireRestart = false,
            HintText = "Allow polearm purchases when weapon auto-buy is enabled.", Order = 3)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Allowed Weapon Types", GroupOrder = 10)]
        public bool BuyPolearmUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Throwing Weapon Upgrades", RequireRestart = false,
            HintText = "Allow throwing-weapon purchases when weapon auto-buy is enabled.", Order = 4)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Allowed Weapon Types", GroupOrder = 10)]
        public bool BuyThrowingWeaponUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Ranged Weapon Upgrades", RequireRestart = false,
            HintText = "Allow bow and crossbow purchases when weapon auto-buy is enabled. Ammunition remains under normal inventory management.", Order = 5)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Allowed Weapon Types", GroupOrder = 10)]
        public bool BuyRangedWeaponUpgrades { get; set; } = true;

        [SettingPropertyBool("Buy Shield Upgrades", RequireRestart = false,
            HintText = "Allow shield purchases when weapon auto-buy is enabled.", Order = 6)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Allowed Weapon Types", GroupOrder = 10)]
        public bool BuyShieldUpgrades { get; set; } = true;

        // --- Group 11: Auto-Buy / Weapons / Weapon Evaluation ---
        [SettingPropertyDropdown("One-Handed Swords", RequireRestart = false,
            HintText = "Controls how one-handed sword upgrades balance damage, reach, speed, and handling.", Order = 1)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<MeleeWeaponUpgradePreferenceOption> OneHandedSwordPreferenceDropdown { get; set; } = new Dropdown<MeleeWeaponUpgradePreferenceOption>(MeleeWeaponUpgradePreferenceOptions, 0);

        [SettingPropertyDropdown("One-Handed Axes & Maces", RequireRestart = false,
            HintText = "Controls how one-handed axe and mace upgrades balance damage, reach, speed, and handling.", Order = 2)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<MeleeWeaponUpgradePreferenceOption> OneHandedAxeMacePreferenceDropdown { get; set; } = new Dropdown<MeleeWeaponUpgradePreferenceOption>(MeleeWeaponUpgradePreferenceOptions, 0);

        [SettingPropertyDropdown("Two-Handed Weapons", RequireRestart = false,
            HintText = "Controls how two-handed weapon upgrades balance damage, reach, speed, and handling.", Order = 3)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<MeleeWeaponUpgradePreferenceOption> TwoHandedPreferenceDropdown { get; set; } = new Dropdown<MeleeWeaponUpgradePreferenceOption>(MeleeWeaponUpgradePreferenceOptions, 0);

        [SettingPropertyDropdown("Thrust Polearms", RequireRestart = false,
            HintText = "Controls how spear, lance, and pike upgrades balance thrust damage, reach, speed, and handling.", Order = 4)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<MeleeWeaponUpgradePreferenceOption> ThrustPolearmPreferenceDropdown { get; set; } = new Dropdown<MeleeWeaponUpgradePreferenceOption>(MeleeWeaponUpgradePreferenceOptions, 0);

        [SettingPropertyDropdown("Swing Polearms", RequireRestart = false,
            HintText = "Controls how glaive, menavlion, and other swing-polearm upgrades balance swing damage, reach, speed, and handling.", Order = 5)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<MeleeWeaponUpgradePreferenceOption> SwingPolearmPreferenceDropdown { get; set; } = new Dropdown<MeleeWeaponUpgradePreferenceOption>(MeleeWeaponUpgradePreferenceOptions, 0);

        [SettingPropertyDropdown("Ranged Weapons", RequireRestart = false,
            HintText = "Controls whether bow and crossbow upgrades prioritize damage, accuracy, or a balanced mix.", Order = 6)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<RangedWeaponUpgradePreferenceOption> RangedWeaponPreferenceDropdown { get; set; } = new Dropdown<RangedWeaponUpgradePreferenceOption>(RangedWeaponUpgradePreferenceOptions, 0);

        [SettingPropertyDropdown("Shields", RequireRestart = false,
            HintText = "Controls whether shield upgrades prioritize hit points, size, or a balanced mix.", Order = 7)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<ShieldUpgradePreferenceOption> ShieldPreferenceDropdown { get; set; } = new Dropdown<ShieldUpgradePreferenceOption>(ShieldUpgradePreferenceOptions, 0);

        [SettingPropertyDropdown("Weapon Property Matching", RequireRestart = false,
            HintText = "Controls how strictly weapon properties must be preserved when a candidate has better preferred stats.", Order = 8)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<WeaponPropertyMatchingOption> WeaponPropertyMatchingDropdown { get; set; } = new Dropdown<WeaponPropertyMatchingOption>(WeaponPropertyMatchingOptions, 1);

        [SettingPropertyDropdown("Ammo Upgrade Preference", RequireRestart = false,
            HintText = "Choose whether ammo upgrades prioritize stack count, damage, or both remaining at least as good.", Order = 9)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<ProjectileUpgradePreferenceOption> AmmoUpgradePreferenceDropdown { get; set; } = new Dropdown<ProjectileUpgradePreferenceOption>(ProjectileUpgradePreferenceOptions, 0);

        [SettingPropertyDropdown("Throwing Weapon Upgrade Preference", RequireRestart = false,
            HintText = "Choose whether throwing-weapon upgrades prioritize stack count, missile damage, or both remaining at least as good.", Order = 10)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public Dropdown<ProjectileUpgradePreferenceOption> ThrowingWeaponUpgradePreferenceDropdown { get; set; } = new Dropdown<ProjectileUpgradePreferenceOption>(ProjectileUpgradePreferenceOptions, 0);

        [SettingPropertyBool("Ignore Throwing Weapon Melee Stats", RequireRestart = false,
            HintText = "Compare throwing weapons only by the selected stack-count and missile-damage preference; melee-stat trade-offs are ignored.", Order = 11)]
        [SettingPropertyGroup("Auto-Buy/Weapons/Weapon Evaluation", GroupOrder = 11)]
        public bool IgnoreThrowingWeaponMeleeStats { get; set; } = true;


        // --- Group 12: Reports ---
        [SettingPropertyDropdown("Sale Report Detail", RequireRestart = false,
            HintText = "Category Counts reports spare gear sales as armor/weapon totals. Full Item List prints individual sold equipment.", Order = 1)]
        [SettingPropertyGroup("Reports", GroupOrder = 12)]
        public Dropdown<EquipmentSaleReportDetailModeOption> EquipmentSaleReportDetailDropdown { get; set; } =
            new Dropdown<EquipmentSaleReportDetailModeOption>(EquipmentSaleReportDetailModeOptions, 0);

        [SettingPropertyInteger("Max Items to Print Details For", 1, 20, RequireRestart = false,
            HintText = "Maximum individual equipment item types shown when printing detailed report lines.", Order = 2)]
        [SettingPropertyGroup("Reports", GroupOrder = 12)]
        public int MaxReportItemsToPrint { get; set; } = 4;

        [SettingPropertyDropdown("Sorting Mode", RequireRestart = false,
            HintText = "Choose how detailed equipment report lines select which items to print.", Order = 3)]
        [SettingPropertyGroup("Reports", GroupOrder = 12)]
        public Dropdown<EquipmentReportSortModeOption> EquipmentReportSortDropdown { get; set; } =
            new Dropdown<EquipmentReportSortModeOption>(EquipmentReportSortModeOptions, 2);

        // Read-only setting values used by the automation code.
        public AutoEquipCategory AutoEquipCategorySetting => AutoEquipCategoryDropdown.SelectedValue.Value;
        public ProjectileUpgradePreference AmmoUpgradePreferenceSetting => AmmoUpgradePreferenceDropdown.SelectedValue.Value;
        public ProjectileUpgradePreference ThrowingWeaponUpgradePreferenceSetting => ThrowingWeaponUpgradePreferenceDropdown.SelectedValue.Value;
        public MeleeWeaponUpgradePreference OneHandedSwordPreferenceSetting => OneHandedSwordPreferenceDropdown.SelectedValue.Value;
        public MeleeWeaponUpgradePreference OneHandedAxeMacePreferenceSetting => OneHandedAxeMacePreferenceDropdown.SelectedValue.Value;
        public MeleeWeaponUpgradePreference TwoHandedPreferenceSetting => TwoHandedPreferenceDropdown.SelectedValue.Value;
        public MeleeWeaponUpgradePreference ThrustPolearmPreferenceSetting => ThrustPolearmPreferenceDropdown.SelectedValue.Value;
        public MeleeWeaponUpgradePreference SwingPolearmPreferenceSetting => SwingPolearmPreferenceDropdown.SelectedValue.Value;
        public RangedWeaponUpgradePreference RangedWeaponPreferenceSetting => RangedWeaponPreferenceDropdown.SelectedValue.Value;
        public ShieldUpgradePreference ShieldPreferenceSetting => ShieldPreferenceDropdown.SelectedValue.Value;
        public WeaponPropertyMatching WeaponPropertyMatchingSetting => WeaponPropertyMatchingDropdown.SelectedValue.Value;
        public KeepDonationCategory KeepDonationCategorySetting => KeepDonationCategoryDropdown.SelectedValue.Value;
        public AutoSellCategory AutoSellCategorySetting => AutoSellCategoryDropdown.SelectedValue.Value;
        public PositiveModifierProtectionCategory PositiveModifierProtectionCategorySetting => PositiveModifierProtectionCategoryDropdown.SelectedValue.Value;
        public SpareArmorLoadouts SpareArmorLoadoutsSetting => SpareArmorLoadoutsDropdown.SelectedValue.Value;

        public LoadoutPriority LoadoutPrioritySetting => LoadoutPriorityDropdown.SelectedValue.Value;
        public BuyEquipmentTarget BuyEquipmentTargetSetting => BuyEquipmentTargetDropdown.SelectedValue.Value;
        public CombatUpgradeCheckOrder CombatUpgradeCheckOrderSetting => CombatUpgradeCheckOrderDropdown.SelectedValue.Value;
        public StealthGearPurchasePolicy StealthGearPurchasePolicySetting => StealthGearPurchasePolicyDropdown.SelectedValue.Value;
        public EquipmentSaleReportDetailMode EquipmentSaleReportDetail => EquipmentSaleReportDetailDropdown.SelectedValue.Value;
        public EquipmentReportSortMode EquipmentReportSort => EquipmentReportSortDropdown.SelectedValue.Value;
    }
}
