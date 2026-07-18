using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using SettlementAutomationCore;

namespace PartyManager
{
    public enum EvalTime
    {
        FinalUpgradeTier,
        PurchaseTimeTier
    }

    public class EvalTimeOption
    {
        private readonly string _name;
        public EvalTime Value { get; }
        public EvalTimeOption(string name, EvalTime value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum SellRidingMountsMode
    {
        Never,
        ExcessOnly,
        All
    }

    public class SellRidingMountsOption
    {
        private readonly string _name;
        public SellRidingMountsMode Value { get; }
        public SellRidingMountsOption(string name, SellRidingMountsMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum UpgradeMountPurchaseMode
    {
        Never,
        CurrentMountedTroops,
        AnyTroopWithMountedUpgrade,
        CavalryToFinalTier,
        AnyTroopToFinalMountedTier
    }

    public class UpgradeMountPurchaseOption
    {
        private readonly string _name;
        public UpgradeMountPurchaseMode Value { get; }
        public UpgradeMountPurchaseOption(string name, UpgradeMountPurchaseMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum MountPriceReferenceMode
    {
        ExactHorseValue,
        MountCategoryAverage
    }

    public class MountPriceReferenceOption
    {
        private readonly string _name;
        public MountPriceReferenceMode Value { get; }
        public MountPriceReferenceOption(string name, MountPriceReferenceMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum PostBattleSlaughterMode
    {
        None,
        Livestock,
        LivestockAndPack,
        All
    }

    public class PostBattleSlaughterOption
    {
        private readonly string _name;
        public PostBattleSlaughterMode Value { get; }
        public PostBattleSlaughterOption(string name, PostBattleSlaughterMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum MercenaryRecruitPolicy
    {
        None,
        MatchFilters,
        Any,
        AnyIgnoreTier
    }

    public class MercenaryRecruitPolicyOption
    {
        private readonly string _name;
        public MercenaryRecruitPolicy Value { get; }
        public MercenaryRecruitPolicyOption(string name, MercenaryRecruitPolicy value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum RecruitHireOrder
    {
        BestCandidatesFirst,
        BestValue,
        CheapestFirst,
        VolunteersFirst,
        MercenariesFirst
    }

    public class RecruitHireOrderOption
    {
        private readonly string _name;
        public RecruitHireOrder Value { get; }
        public RecruitHireOrderOption(string name, RecruitHireOrder value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum NobleRecruitPolicy
    {
        None,
        MatchFilters,
        Any,
        AnyIgnoreTier
    }

    public class NobleRecruitPolicyOption
    {
        private readonly string _name;
        public NobleRecruitPolicy Value { get; }
        public NobleRecruitPolicyOption(string name, NobleRecruitPolicy value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum RegularRecruitPolicy
    {
        None,
        MatchFilters,
        Any,
        AnyIgnoreTier
    }

    public class RegularRecruitPolicyOption
    {
        private readonly string _name;
        public RegularRecruitPolicy Value { get; }
        public RegularRecruitPolicyOption(string name, RegularRecruitPolicy value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum PrisonerKeepPolicy
    {
        RansomAll,
        KeepAll,
        KeepSelected
    }

    public class PrisonerKeepPolicyOption
    {
        private readonly string _name;
        public PrisonerKeepPolicy Value { get; }
        public PrisonerKeepPolicyOption(string name, PrisonerKeepPolicy value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum BanditPrisonerKeepPolicy
    {
        RansomAll,
        KeepAll,
        KeepNobleOnly,
        KeepSelected
    }

    public class BanditPrisonerKeepPolicyOption
    {
        private readonly string _name;
        public BanditPrisonerKeepPolicy Value { get; }
        public BanditPrisonerKeepPolicyOption(string name, BanditPrisonerKeepPolicy value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum PrisonerReportDetailMode
    {
        CategoryCounts,
        FullItemList
    }

    public class PrisonerReportDetailModeOption
    {
        private readonly string _name;
        public PrisonerReportDetailMode Value { get; }
        public PrisonerReportDetailModeOption(string name, PrisonerReportDetailMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum PrisonerReportSortMode
    {
        Amount,
        Tier,
        RansomValue
    }

    public class PrisonerReportSortModeOption
    {
        private readonly string _name;
        public PrisonerReportSortMode Value { get; }
        public PrisonerReportSortModeOption(string name, PrisonerReportSortMode value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "PartyManager_v0_5";
        public override string DisplayName => "Party Manager";
        public override string FolderName => "PartyManager";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<PrisonerReportDetailModeOption> PrisonerReportDetailModeOptions = new List<PrisonerReportDetailModeOption>
        {
            new PrisonerReportDetailModeOption("Category Counts", PrisonerReportDetailMode.CategoryCounts),
            new PrisonerReportDetailModeOption("Full Troop List", PrisonerReportDetailMode.FullItemList)
        };

        private static readonly IReadOnlyList<PrisonerReportSortModeOption> PrisonerReportSortModeOptions = new List<PrisonerReportSortModeOption>
        {
            new PrisonerReportSortModeOption("Amount", PrisonerReportSortMode.Amount),
            new PrisonerReportSortModeOption("Tier", PrisonerReportSortMode.Tier),
            new PrisonerReportSortModeOption("Ransom Value", PrisonerReportSortMode.RansomValue)
        };

        private static readonly IReadOnlyList<EvalTimeOption> EvalTimeOptions = new List<EvalTimeOption>
        {
            new EvalTimeOption("Final Upgrade Tier", EvalTime.FinalUpgradeTier),
            new EvalTimeOption("Purchase Time Tier", EvalTime.PurchaseTimeTier)
        };

        private static readonly IReadOnlyList<SettlementAutomationCore.RecruitmentNotificationModeOption> RecruitmentNotificationModeOptions = new List<SettlementAutomationCore.RecruitmentNotificationModeOption>
        {
            new SettlementAutomationCore.RecruitmentNotificationModeOption("One-by-One", SettlementAutomationCore.RecruitmentNotificationMode.OneByOne),
            new SettlementAutomationCore.RecruitmentNotificationModeOption("Consolidated", SettlementAutomationCore.RecruitmentNotificationMode.Consolidated)
        };

        private static readonly IReadOnlyList<SellRidingMountsOption> SellRidingMountsOptions = new List<SellRidingMountsOption>
        {
            new SellRidingMountsOption("Never", SellRidingMountsMode.Never),
            new SellRidingMountsOption("Excess Speed Mounts Only", SellRidingMountsMode.ExcessOnly),
            new SellRidingMountsOption("All Riding Mounts", SellRidingMountsMode.All)
        };

        private static readonly IReadOnlyList<UpgradeMountPurchaseOption> UpgradeMountPurchaseOptions = new List<UpgradeMountPurchaseOption>
        {
            new UpgradeMountPurchaseOption("Never Buy Upgrade Mounts", UpgradeMountPurchaseMode.Never),
            new UpgradeMountPurchaseOption("Buy for Current Mounted Troops", UpgradeMountPurchaseMode.CurrentMountedTroops),
            new UpgradeMountPurchaseOption("Buy for Troops With Mounted Upgrades", UpgradeMountPurchaseMode.AnyTroopWithMountedUpgrade),
            new UpgradeMountPurchaseOption("Buy for Cavalry to Final Tier", UpgradeMountPurchaseMode.CavalryToFinalTier),
            new UpgradeMountPurchaseOption("Buy for Any Potential Final Mounted Unit", UpgradeMountPurchaseMode.AnyTroopToFinalMountedTier)
        };

        private static readonly IReadOnlyList<MountPriceReferenceOption> MountPriceReferenceOptions = new List<MountPriceReferenceOption>
        {
            new MountPriceReferenceOption("Mount Category Average", MountPriceReferenceMode.MountCategoryAverage),
            new MountPriceReferenceOption("Exact Horse Value", MountPriceReferenceMode.ExactHorseValue)
        };

        private static readonly IReadOnlyList<PostBattleSlaughterOption> PostBattleSlaughterOptions = new List<PostBattleSlaughterOption>
        {
            new PostBattleSlaughterOption("None (Disabled)", PostBattleSlaughterMode.None),
            new PostBattleSlaughterOption("Livestock Only", PostBattleSlaughterMode.Livestock),
            new PostBattleSlaughterOption("Livestock & Pack Animals", PostBattleSlaughterMode.LivestockAndPack),
            new PostBattleSlaughterOption("All Animals (including mounts)", PostBattleSlaughterMode.All)
        };

        private static readonly IReadOnlyList<MercenaryRecruitPolicyOption> MercenaryRecruitPolicyOptions = new List<MercenaryRecruitPolicyOption>
        {
            new MercenaryRecruitPolicyOption("Do Not Recruit Mercenaries", MercenaryRecruitPolicy.None),
            new MercenaryRecruitPolicyOption("Match Recruitment Filters", MercenaryRecruitPolicy.MatchFilters),
            new MercenaryRecruitPolicyOption("Any Mercenary (Respect Tier Limits)", MercenaryRecruitPolicy.Any),
            new MercenaryRecruitPolicyOption("Any Mercenary (Ignore Tier Limits)", MercenaryRecruitPolicy.AnyIgnoreTier)
        };

        private static readonly IReadOnlyList<RecruitHireOrderOption> RecruitHireOrderOptions = new List<RecruitHireOrderOption>
        {
            new RecruitHireOrderOption("Best Candidates First", RecruitHireOrder.BestCandidatesFirst),
            new RecruitHireOrderOption("Best Value", RecruitHireOrder.BestValue),
            new RecruitHireOrderOption("Cheapest First", RecruitHireOrder.CheapestFirst),
            new RecruitHireOrderOption("Volunteers First", RecruitHireOrder.VolunteersFirst),
            new RecruitHireOrderOption("Mercenaries First", RecruitHireOrder.MercenariesFirst)
        };

        private static readonly IReadOnlyList<NobleRecruitPolicyOption> NobleRecruitPolicyOptions = new List<NobleRecruitPolicyOption>
        {
            new NobleRecruitPolicyOption("Do Not Recruit Nobles", NobleRecruitPolicy.None),
            new NobleRecruitPolicyOption("Match Recruitment Filters", NobleRecruitPolicy.MatchFilters),
            new NobleRecruitPolicyOption("Any Noble (Respect Tier Limits)", NobleRecruitPolicy.Any),
            new NobleRecruitPolicyOption("Any Noble (Ignore Tier Limits)", NobleRecruitPolicy.AnyIgnoreTier)
        };

        private static readonly IReadOnlyList<RegularRecruitPolicyOption> RegularRecruitPolicyOptions = new List<RegularRecruitPolicyOption>
        {
            new RegularRecruitPolicyOption("Do Not Recruit Regulars", RegularRecruitPolicy.None),
            new RegularRecruitPolicyOption("Match Recruitment Filters", RegularRecruitPolicy.MatchFilters),
            new RegularRecruitPolicyOption("Any Regular (Respect Tier Limits)", RegularRecruitPolicy.Any),
            new RegularRecruitPolicyOption("Any Regular (Ignore Tier Limits)", RegularRecruitPolicy.AnyIgnoreTier)
        };

        private static readonly IReadOnlyList<PrisonerKeepPolicyOption> PrisonerKeepPolicyOptions = new List<PrisonerKeepPolicyOption>
        {
            new PrisonerKeepPolicyOption("Ransom All", PrisonerKeepPolicy.RansomAll),
            new PrisonerKeepPolicyOption("Keep All", PrisonerKeepPolicy.KeepAll),
            new PrisonerKeepPolicyOption("Keep Selected", PrisonerKeepPolicy.KeepSelected)
        };

        private static readonly IReadOnlyList<BanditPrisonerKeepPolicyOption> BanditPrisonerKeepPolicyOptions = new List<BanditPrisonerKeepPolicyOption>
        {
            new BanditPrisonerKeepPolicyOption("Ransom All", BanditPrisonerKeepPolicy.RansomAll),
            new BanditPrisonerKeepPolicyOption("Keep All", BanditPrisonerKeepPolicy.KeepAll),
            new BanditPrisonerKeepPolicyOption("Keep Noble Only", BanditPrisonerKeepPolicy.KeepNobleOnly),
            new BanditPrisonerKeepPolicyOption("Keep Selected", BanditPrisonerKeepPolicy.KeepSelected)
        };

        [SettingPropertyBool("Enable Party Manager", RequireRestart = false, HintText = "Master switch. When disabled, Party Manager will not recruit, handle prisoners, request supplies, clean up herding, reserve animals, run post-battle actions, or show alerts.", Order = 0)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool ModEnabled { get; set; } = true;

        // --- Recruitment settings ---
        [SettingPropertyBool("Auto-Recruit Volunteers", RequireRestart = false, HintText = "Enable auto-recruitment from town/village notables.", Order = 1)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public bool AutoRecruitVolunteers { get; set; } = true;

        [SettingPropertyInteger("Recruit Up To Party Size (%)", 1, 100, RequireRestart = false,
            HintText = "Stop normal auto-recruitment when the party reaches this percentage of its size limit. Garrison donation can still over-recruit into available garrison space.", Order = 2)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public int RecruitUpToPartySizePercent { get; set; } = 100;

        [SettingPropertyDropdown("Regular Recruitment Policy", RequireRestart = false, HintText = "Control how regular troops are recruited from notables.", Order = 3)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public Dropdown<RegularRecruitPolicyOption> RegularRecruitDropdown { get; set; } = new Dropdown<RegularRecruitPolicyOption>(RegularRecruitPolicyOptions, 1); // Default: Match Recruitment Filters

        [SettingPropertyDropdown("Noble Recruitment Policy", RequireRestart = false, HintText = "Control how noble troops are recruited from notables.", Order = 4)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public Dropdown<NobleRecruitPolicyOption> NobleRecruitDropdown { get; set; } = new Dropdown<NobleRecruitPolicyOption>(NobleRecruitPolicyOptions, 1); // Default: Match Recruitment Filters

        [SettingPropertyDropdown("Mercenary Recruitment Policy", RequireRestart = false, HintText = "Control how mercenary troops are recruited from taverns.", Order = 5)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public Dropdown<MercenaryRecruitPolicyOption> MercenaryRecruitDropdown { get; set; } = new Dropdown<MercenaryRecruitPolicyOption>(MercenaryRecruitPolicyOptions, 0); // Default: Do Not Recruit Mercenaries

        [SettingPropertyDropdown("Recruit Hire Order", RequireRestart = false, HintText = "Choose how allowed tavern mercenaries and notable volunteers are ordered before Core applies shared gold and party-space limits.", Order = 6)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public Dropdown<RecruitHireOrderOption> RecruitHireOrderDropdown { get; set; } = new Dropdown<RecruitHireOrderOption>(RecruitHireOrderOptions, 0);

        [SettingPropertyInteger("Min Tier to Recruit", 1, 6, RequireRestart = false, HintText = "Minimum troop tier to buy.", Order = 7)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public int MinRecruitTier { get; set; } = 1;

        [SettingPropertyInteger("Max Tier to Recruit", 1, 6, RequireRestart = false, HintText = "Maximum troop tier to buy.", Order = 8)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public int MaxRecruitTier { get; set; } = 6;

        [SettingPropertyDropdown("Evaluation Target Tier", RequireRestart = false, HintText = "Evaluate filters against the troop's current purchase tier or their final upgrade tier.", Order = 9)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public Dropdown<EvalTimeOption> EvalTimeDropdown { get; set; } = new Dropdown<EvalTimeOption>(EvalTimeOptions, 0);

        [SettingPropertyDropdown("Recruitment Notification Mode", RequireRestart = false, HintText = "Configure the recruitment notification alert verbosity.", Order = 10)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 1)]
        public Dropdown<SettlementAutomationCore.RecruitmentNotificationModeOption> RecruitmentNotificationModeDropdown { get; set; } =
            new Dropdown<SettlementAutomationCore.RecruitmentNotificationModeOption>(RecruitmentNotificationModeOptions, 0);

        public SettlementAutomationCore.RecruitmentNotificationMode RecruitmentNotificationModeSetting => RecruitmentNotificationModeDropdown.SelectedValue.Value;

        // --- Party Needs ---
        [SettingPropertyBool("Auto-Buy Food", RequireRestart = false, HintText = "Automatically buy food to maintain party supplies.", Order = 1)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public bool AutoBuyFood { get; set; } = true;
 
        [SettingPropertyInteger("Critical Food Days", 1, 30, RequireRestart = false,
            HintText = "Buy any available food before other item requests if supplies fall below this many days.", Order = 2)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public int CriticalFoodDays { get; set; } = 2;
 
        [SettingPropertyInteger("Party Food Days to Keep", 1, 100, RequireRestart = false,
            HintText = "Maintain this many total days of food after critical and variety food requests.", Order = 3)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public int PartyFoodDaysToKeep { get; set; } = 10;
 
        [SettingPropertyInteger("Min Party Size for Variety", 1, 100, RequireRestart = false,
            HintText = "Minimum party size before the mod requests specific food items for variety. Each food type targets a fair share of the total food buffer.", Order = 4)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public int MinPartySizeForVariety { get; set; } = 20;
 
        [SettingPropertyBool("Auto-Buy Riding Mounts", RequireRestart = false,
            HintText = "Automatically buy riding mounts for unmounted infantry.", Order = 5)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public bool AutoBuyMounts { get; set; } = true;
 
        [SettingPropertyDropdown("Critical Food Spend Mode", RequireRestart = false,
            HintText = "Controls when emergency food purchases run compared to other item requests.", Order = 6)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public Dropdown<RequestProfileOption> CriticalFoodSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Critical));
 
        [SettingPropertyDropdown("Food Variety Spend Mode", RequireRestart = false,
            HintText = "Controls when specific food variety requests run compared to other item requests.", Order = 7)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public Dropdown<RequestProfileOption> FoodVarietySpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Essential));
 
        [SettingPropertyDropdown("Food Buffer Spend Mode", RequireRestart = false,
            HintText = "Controls when the total food buffer request runs compared to other item requests.", Order = 8)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public Dropdown<RequestProfileOption> FoodBufferSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Routine));
 
        [SettingPropertyDropdown("Riding Mount Spend Mode", RequireRestart = false,
            HintText = "Controls when riding mount requests run compared to other item requests.", Order = 9)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public Dropdown<RequestProfileOption> RidingMountSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Routine));
 
        [SettingPropertyDropdown("Upgrade Mount Spend Mode", RequireRestart = false,
            HintText = "Controls when troop upgrade mount requests run compared to other item requests. Default: Opportunistic.", Order = 10)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public Dropdown<RequestProfileOption> UpgradeMountSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Opportunistic));
 
        [SettingPropertyDropdown("Upgrade Mount Buying", RequireRestart = false,
            HintText = "Control whether the party buys higher-tier mounts for troop upgrades. Ordinary riding mounts for infantry are still handled by Auto-Buy Riding Mounts.", Order = 11)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public Dropdown<UpgradeMountPurchaseOption> UpgradeMountBuyingDropdown { get; set; } =
            new Dropdown<UpgradeMountPurchaseOption>(UpgradeMountPurchaseOptions, 0);
 
        [SettingPropertyDropdown("Mount Price Baseline", RequireRestart = false,
            HintText = "Choose whether mount price limits compare against the exact horse value or the average value of matching mount categories.", Order = 12)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public Dropdown<MountPriceReferenceOption> MountPriceReferenceDropdown { get; set; } =
            new Dropdown<MountPriceReferenceOption>(MountPriceReferenceOptions, 0);
 
        [SettingPropertyBool("Auto-Buy Boats", RequireRestart = false, HintText = "Automatically buy boats/ships up to the party's ideal capacity when visiting towns.", Order = 13)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public bool AutoBuyBoats { get; set; } = true;
 
        private int _minGoldReserveForBoats = 50000;
 
        [SettingPropertyInteger("Min Gold Reserve for Boats", 5000, 500000, RequireRestart = false,
            HintText = "Do not auto-buy boats if the player's gold is at or below this amount. Snapping step: 1000.", Order = 14)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 4)]
        public int MinGoldReserveForBoats
        {
            get => _minGoldReserveForBoats;
            set => _minGoldReserveForBoats = ((value + 500) / 1000) * 1000;
        }

        // --- Recruitment Cultures ---
        [SettingPropertyBool("Recruit Empire", RequireRestart = false, HintText = "Recruit Empire culture troops.", Order = 1)]
        [SettingPropertyGroup("Recruitment/Filters - Cultures", GroupOrder = 2)]
        public bool RecruitEmpire { get; set; } = true;

        [SettingPropertyBool("Recruit Vlandia", RequireRestart = false, HintText = "Recruit Vlandia culture troops.", Order = 2)]
        [SettingPropertyGroup("Recruitment/Filters - Cultures", GroupOrder = 2)]
        public bool RecruitVlandia { get; set; } = true;

        [SettingPropertyBool("Recruit Battania", RequireRestart = false, HintText = "Recruit Battania culture troops.", Order = 3)]
        [SettingPropertyGroup("Recruitment/Filters - Cultures", GroupOrder = 2)]
        public bool RecruitBattania { get; set; } = true;

        [SettingPropertyBool("Recruit Sturgia", RequireRestart = false, HintText = "Recruit Sturgia culture troops.", Order = 4)]
        [SettingPropertyGroup("Recruitment/Filters - Cultures", GroupOrder = 2)]
        public bool RecruitSturgia { get; set; } = true;

        [SettingPropertyBool("Recruit Khuzait", RequireRestart = false, HintText = "Recruit Khuzait culture troops.", Order = 5)]
        [SettingPropertyGroup("Recruitment/Filters - Cultures", GroupOrder = 2)]
        public bool RecruitKhuzait { get; set; } = true;

        [SettingPropertyBool("Recruit Aserai", RequireRestart = false, HintText = "Recruit Aserai culture troops.", Order = 6)]
        [SettingPropertyGroup("Recruitment/Filters - Cultures", GroupOrder = 2)]
        public bool RecruitAserai { get; set; } = true;

        [SettingPropertyBool("Recruit Other Cultures", RequireRestart = false, HintText = "Recruit troops from other cultures (e.g. minor factions, modded cultures).", Order = 7)]
        [SettingPropertyGroup("Recruitment/Filters - Cultures", GroupOrder = 2)]
        public bool RecruitOtherCultures { get; set; } = true;

        // --- Recruitment Roles ---
        [SettingPropertyBool("Recruit Light Infantry", RequireRestart = false, HintText = "Allow recruiting low-tier melee foot soldiers with no shields or specialized roles.", Order = 1)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitLightInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Shield Infantry", RequireRestart = false, HintText = "Allow recruiting melee foot soldiers carrying shields for protection.", Order = 2)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitShieldInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Spear Infantry", RequireRestart = false, HintText = "Allow recruiting melee foot soldiers carrying both shields and anti-cavalry spears.", Order = 3)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitSpearInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Shock Infantry", RequireRestart = false, HintText = "Allow recruiting skilled two-handed weapon and swingable polearm foot soldiers with no shields.", Order = 4)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitShockInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Pike Infantry", RequireRestart = false, HintText = "Allow recruiting specialized foot soldiers whose primary role is anti-cavalry pikes.", Order = 5)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitPikeInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Skirmishers", RequireRestart = false, HintText = "Allow recruiting foot soldiers whose primary role is throwing weapons.", Order = 6)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitSkirmishers { get; set; } = true;

        [SettingPropertyBool("Recruit Foot Archers", RequireRestart = false, HintText = "Allow recruiting foot archers carrying bows.", Order = 7)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitFootArchers { get; set; } = true;

        [SettingPropertyBool("Recruit Crossbowmen", RequireRestart = false, HintText = "Allow recruiting foot soldiers carrying crossbows.", Order = 8)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitCrossbowmen { get; set; } = true;

        [SettingPropertyBool("Recruit Melee Cavalry", RequireRestart = false, HintText = "Allow recruiting mounted melee soldiers (lancers, light cavalry).", Order = 9)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitMeleeCavalry { get; set; } = true;

        [SettingPropertyBool("Recruit Mounted Skirmishers", RequireRestart = false, HintText = "Allow recruiting mounted throwing weapon soldiers.", Order = 10)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitMountedSkirmisher { get; set; } = true;

        [SettingPropertyBool("Recruit Horse Archers", RequireRestart = false, HintText = "Allow recruiting mounted ranged soldiers carrying bows or crossbows.", Order = 11)]
        [SettingPropertyGroup("Recruitment/Filters - Roles", GroupOrder = 3)]
        public bool RecruitHorseArchers { get; set; } = true;

        // --- Mount & Herding settings ---

        [SettingPropertyBool("Herding Penalty Protection", RequireRestart = false, HintText = "Automatically sell excess animals to avoid herding speed penalty.", Order = 7)]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 5)]
        public bool PreventHerdingPenalty { get; set; } = true;

        [SettingPropertyDropdown("Sell Riding Mounts", RequireRestart = false, HintText = "Control when to sell excess riding mounts to reduce herding or cargo weight.", Order = 8)]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 5)]
        public Dropdown<SellRidingMountsOption> SellRidingMountsDropdown { get; set; } = new Dropdown<SellRidingMountsOption>(SellRidingMountsOptions, 1);

        [SettingPropertyBool("Preserve Riding Mounts for Foot Reserve", RequireRestart = false,
            HintText = "Protect riding mounts up to current foot troops plus empty party slots before selling mounts.", Order = 9)]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 5)]
        public bool PreserveRidingMountsForFootReserve { get; set; } = true;

        [SettingPropertyDropdown("Post-Battle Auto-Slaughter", RequireRestart = false, HintText = "Automatically slaughter animals after a battle to instantly clear herding penalties.", Order = 10)]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 5)]
        public Dropdown<PostBattleSlaughterOption> PostBattleSlaughterDropdown { get; set; } = new Dropdown<PostBattleSlaughterOption>(PostBattleSlaughterOptions, 2);

        [SettingPropertyInteger("Herding Warning Threshold (%)", 0, 80, RequireRestart = false,
            HintText = "Warn after post-battle cleanup if herding still slows the party by at least this much. Default: 5%.", Order = 11)]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 5)]
        public int HerdingWarningThresholdPercent { get; set; } = 5;

        [SettingPropertyInteger("Cargo Warning Threshold (%)", 0, 100, RequireRestart = false,
            HintText = "Warn after post-battle cleanup if overburdened cargo slows the party by at least this much. Default: 5%.", Order = 12)]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 5)]
        public int CargoWarningThresholdPercent { get; set; } = 5;



        // --- Prisoner Keep Policies & Limits ---
        [SettingPropertyBool("Keep Hero Prisoners", RequireRestart = false, HintText = "Never automatically ransom or donate hero/lord prisoners.", Order = 1)]
        [SettingPropertyGroup("Prisoners", GroupOrder = 6)]
        public bool KeepHeroPrisoners { get; set; } = true;

        [SettingPropertyDropdown("Noble Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for noble/elite prisoners.", Order = 2)]
        [SettingPropertyGroup("Prisoners", GroupOrder = 6)]
        public Dropdown<PrisonerKeepPolicyOption> NoblePrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 2); // Keep Selected

        [SettingPropertyDropdown("Regular Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for standard regular prisoners.", Order = 3)]
        [SettingPropertyGroup("Prisoners", GroupOrder = 6)]
        public Dropdown<PrisonerKeepPolicyOption> RegularPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 2); // Keep Selected

        [SettingPropertyDropdown("Bandit Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for bandit prisoners.", Order = 4)]
        [SettingPropertyGroup("Prisoners", GroupOrder = 6)]
        public Dropdown<BanditPrisonerKeepPolicyOption> BanditPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<BanditPrisonerKeepPolicyOption>(BanditPrisonerKeepPolicyOptions, 0); // Ransom All

        [SettingPropertyDropdown("Other / Minor Faction Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for minor faction and other modded culture prisoners.", Order = 5)]
        [SettingPropertyGroup("Prisoners", GroupOrder = 6)]
        public Dropdown<PrisonerKeepPolicyOption> OtherPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 2); // Keep Selected

        [SettingPropertyBool("Bypass Noble Prisoner Tier Limit", RequireRestart = false, HintText = "If enabled, noble prisoners will bypass the min tier keep limit.", Order = 6)]
        [SettingPropertyGroup("Prisoners/Keep Policies", GroupOrder = 7)]
        public bool BypassNoblePrisonerTierLimit { get; set; } = true;

        [SettingPropertyBool("Bypass Minor Faction/Other Tier Limit", RequireRestart = false, HintText = "If enabled, minor faction/other culture prisoners will bypass the min tier keep limit.", Order = 7)]
        [SettingPropertyGroup("Prisoners/Keep Policies", GroupOrder = 7)]
        public bool BypassOtherPrisonerTierLimit { get; set; } = true;

        [SettingPropertyInteger("Min Prisoner Tier to Keep", 1, 6, RequireRestart = false, HintText = "Minimum tier of regular/bandit prisoner to keep for recruitment.", Order = 8)]
        [SettingPropertyGroup("Prisoners/Keep Policies", GroupOrder = 7)]
        public int MinPrisonerTierToKeep { get; set; } = 4;

        [SettingPropertyBool("Perk-Based Prisoner Keep", RequireRestart = false, HintText = "Automatically override keep tier filters based on active Level 50 Leadership perks.", Order = 9)]
        [SettingPropertyGroup("Prisoners/Keep Policies", GroupOrder = 7)]
        public bool UsePerkBasedPrisonerKeep { get; set; } = true;

        [SettingPropertyBool("Bypass Noble Recruit Policy when Keeping", RequireRestart = false, HintText = "If enabled, keep selected noble prisoners matching role/culture filters even if noble recruitment from notables is disabled.", Order = 10)]
        [SettingPropertyGroup("Prisoners/Keep Policies", GroupOrder = 7)]
        public bool BypassNobleRecruitPolicy { get; set; } = false;

        [SettingPropertyBool("Bypass Regular Recruit Policy when Keeping", RequireRestart = false, HintText = "If enabled, keep selected regular prisoners matching role/culture filters even if regular recruitment from notables is disabled.", Order = 11)]
        [SettingPropertyGroup("Prisoners/Keep Policies", GroupOrder = 7)]
        public bool BypassRegularRecruitPolicy { get; set; } = false;

        [SettingPropertyBool("Bypass Mercenary Recruit Policy when Keeping", RequireRestart = false, HintText = "If enabled, keep selected mercenary prisoners matching role/culture filters even if tavern recruitment is disabled.", Order = 12)]
        [SettingPropertyGroup("Prisoners/Keep Policies", GroupOrder = 7)]
        public bool BypassMercenaryRecruitPolicy { get; set; } = false;

        // --- Prisoner Ransom Settings ---
        [SettingPropertyBool("Auto-Ransom Prisoners", RequireRestart = false, HintText = "Automatically ransom standard prisoners for gold in taverns.", Order = 1)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public bool AutoRansomPrisoners { get; set; } = true;

        // --- Prisoner Capacity Alerts ---
        [SettingPropertyInteger("Prisoner Capacity Alert Threshold (%)", 0, 100, RequireRestart = false, HintText = "Trigger alert if total prisoners exceed this percentage of capacity after automation. Set to 0 to disable.", Order = 8)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public int PrisonerCapacityAlertPercent { get; set; } = 33;

        [SettingPropertyInteger("Prisoner Stack Alert Capacity Limit (%)", 0, 100, RequireRestart = false, HintText = "Trigger alert if any single prisoner stack exceeds this percentage of total prisoner capacity. Set to 0 to disable.", Order = 9)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public int PrisonerStackAlertPercentLimit { get; set; } = 10;

        [SettingPropertyDropdown("Prisoner Report Detail", RequireRestart = false, HintText = "Detailed log format: Category Counts (simple summary) or Full Troop List (detailed troop breakdown).", Order = 10)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public Dropdown<PrisonerReportDetailModeOption> PrisonerReportDetailDropdown { get; set; } = new Dropdown<PrisonerReportDetailModeOption>(PrisonerReportDetailModeOptions, 1);

        [SettingPropertyInteger("Max Prisoners To Print", 1, 20, RequireRestart = false, HintText = "Maximum number of distinct prisoner types to log in detailed report before truncating with '+X more'.", Order = 11)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public int MaxPrisonersToPrint { get; set; } = 4;

        [SettingPropertyDropdown("Prisoner Report Sort", RequireRestart = false, HintText = "Sort order for detailed prisoner logs.", Order = 12)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public Dropdown<PrisonerReportSortModeOption> PrisonerReportSortDropdown { get; set; } = new Dropdown<PrisonerReportSortModeOption>(PrisonerReportSortModeOptions, 2);

        // --- Prisoner Donation Settings ---
        [SettingPropertyBool("Auto-Donate Prisoners to Dungeon", RequireRestart = false, HintText = "Automatically donate eligible prisoners to friendly town/castle dungeons to farm influence/XP.", Order = 2)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public bool AutoDonatePrisoners { get; set; } = false;

        [SettingPropertyInteger("Min Tier to Donate", 1, 6, RequireRestart = false, HintText = "Minimum tier of prisoner to donate.", Order = 3)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public int MinDonateTier { get; set; } = 3;

        [SettingPropertyBool("Prioritize High Tier for Donation", RequireRestart = false, HintText = "Donate higher tier prisoners first to maximize influence return.", Order = 4)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public bool PrioritizeHighTierDonation { get; set; } = true;

        // --- Prisoner Discard Settings ---
        [SettingPropertyBool("Auto Discard Post-Battle Excess Prisoners", RequireRestart = false, HintText = "Automatically dump/discard low tier prisoners post-battle if player party is over prisoner capacity.", Order = 5)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public bool AutoDiscardPrisonersPostBattle { get; set; } = false;

        [SettingPropertyInteger("Discard Prisoners Up To Tier", 1, 6, RequireRestart = false, HintText = "Discard excess prisoners up to (and including) this tier.", Order = 6)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public int DiscardPrisonersUpToTier { get; set; } = 2;

        [SettingPropertyBool("Perk-Based Prisoner Discard", RequireRestart = false, HintText = "If enabled, automatically overrides discard filters based on active Level 50 Leadership perks (Stout Defender protects T4-6, Fervent Attacker protects T1-3).", Order = 7)]
        [SettingPropertyGroup("Prisoners/Actions & Alerts", GroupOrder = 8)]
        public bool UsePerkBasedPrisonerDiscard { get; set; } = false;




        // --- Group 8: Garrison Donation ---
        [SettingPropertyBool("Enable Garrison Donation", RequireRestart = false, HintText = "Over-recruit troops and donate them to garrison to farm influence.", Order = 1)]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 9)]
        public bool EnableGarrisonDonation { get; set; } = false;

        private int _maxGarrisonSize = 400;

        [SettingPropertyInteger("Max Garrison Roster Size", 50, 1000, RequireRestart = false, HintText = "Do not donate to garrison if it has reached or exceeded this capacity. Snapping step: 50.", Order = 2)]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 9)]
        public int MaxGarrisonSize
        {
            get => _maxGarrisonSize;
            set => _maxGarrisonSize = ((value + 25) / 50) * 50;
        }

        [SettingPropertyInteger("Min Garrison Donation Tier", 1, 6, RequireRestart = false, HintText = "Minimum tier of troop to donate to garrison.", Order = 3)]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 9)]
        public int MinDonationTier { get; set; } = 1;

        public EvalTime EvalTimeSetting => EvalTimeDropdown.SelectedValue.Value;
        public SellRidingMountsMode SellRidingMountsSetting => SellRidingMountsDropdown.SelectedValue.Value;
        public PostBattleSlaughterMode PostBattleSlaughterSetting => PostBattleSlaughterDropdown.SelectedValue.Value;
        public MercenaryRecruitPolicy MercenaryRecruitSetting => MercenaryRecruitDropdown.SelectedValue.Value;
        public RecruitHireOrder RecruitHireOrderSetting => RecruitHireOrderDropdown.SelectedValue.Value;
        public NobleRecruitPolicy NobleRecruitSetting => NobleRecruitDropdown.SelectedValue.Value;
        public RegularRecruitPolicy RegularRecruitSetting => RegularRecruitDropdown.SelectedValue.Value;
        public RequestProfile CriticalFoodRequestProfile => CriticalFoodSpendModeDropdown.SelectedValue.Value;
        public RequestProfile FoodVarietyRequestProfile => FoodVarietySpendModeDropdown.SelectedValue.Value;
        public RequestProfile FoodBufferRequestProfile => FoodBufferSpendModeDropdown.SelectedValue.Value;
        public RequestProfile RidingMountRequestProfile => RidingMountSpendModeDropdown.SelectedValue.Value;
        public RequestProfile UpgradeMountRequestProfile => UpgradeMountSpendModeDropdown.SelectedValue.Value;
        public UpgradeMountPurchaseMode UpgradeMountPurchaseSetting => UpgradeMountBuyingDropdown.SelectedValue.Value;
        public MountPriceReferenceMode MountPriceReferenceSetting => MountPriceReferenceDropdown.SelectedValue.Value;
        public PrisonerKeepPolicy NoblePrisonerKeepPolicySetting => NoblePrisonerKeepPolicyDropdown.SelectedValue.Value;
        public PrisonerKeepPolicy RegularPrisonerKeepPolicySetting => RegularPrisonerKeepPolicyDropdown.SelectedValue.Value;
        public BanditPrisonerKeepPolicy BanditPrisonerKeepPolicySetting => BanditPrisonerKeepPolicyDropdown.SelectedValue.Value;
        public PrisonerKeepPolicy OtherPrisonerKeepPolicySetting => OtherPrisonerKeepPolicyDropdown.SelectedValue.Value;
        public PrisonerReportDetailMode PrisonerReportDetail => PrisonerReportDetailDropdown.SelectedValue.Value;
        public PrisonerReportSortMode PrisonerReportSort => PrisonerReportSortDropdown.SelectedValue.Value;
    }
}
