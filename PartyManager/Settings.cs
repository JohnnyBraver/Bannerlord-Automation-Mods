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

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "PartyManager_v3";
        public override string DisplayName => "Party Manager";
        public override string FolderName => "PartyManager";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<EvalTimeOption> EvalTimeOptions = new List<EvalTimeOption>
        {
            new EvalTimeOption("Final Upgrade Tier", EvalTime.FinalUpgradeTier),
            new EvalTimeOption("Purchase Time Tier", EvalTime.PurchaseTimeTier)
        };

        private static readonly IReadOnlyList<SellRidingMountsOption> SellRidingMountsOptions = new List<SellRidingMountsOption>
        {
            new SellRidingMountsOption("Never", SellRidingMountsMode.Never),
            new SellRidingMountsOption("Excess Speed Mounts Only", SellRidingMountsMode.ExcessOnly),
            new SellRidingMountsOption("All Riding Mounts", SellRidingMountsMode.All)
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

        // --- Recruitment settings ---
        [SettingPropertyBool("Auto-Recruit Volunteers", RequireRestart = false, HintText = "Enable auto-recruitment from town/village notables.", Order = 1)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 5)]
        public bool AutoRecruitVolunteers { get; set; } = true;

        [SettingPropertyInteger("Recruit Up To Party Size (%)", 1, 100, RequireRestart = false,
            HintText = "Stop normal auto-recruitment when the party reaches this percentage of its size limit. Garrison donation can still over-recruit into available garrison space.", Order = 2)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 5)]
        public int RecruitUpToPartySizePercent { get; set; } = 100;

        [SettingPropertyDropdown("Regular Recruitment Policy", RequireRestart = false, HintText = "Control how regular troops are recruited from notables.", Order = 3)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 5)]
        public Dropdown<RegularRecruitPolicyOption> RegularRecruitDropdown { get; set; } = new Dropdown<RegularRecruitPolicyOption>(RegularRecruitPolicyOptions, 1); // Default: Match Recruitment Filters

        [SettingPropertyDropdown("Noble Recruitment Policy", RequireRestart = false, HintText = "Control how noble troops are recruited from notables.", Order = 4)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 5)]
        public Dropdown<NobleRecruitPolicyOption> NobleRecruitDropdown { get; set; } = new Dropdown<NobleRecruitPolicyOption>(NobleRecruitPolicyOptions, 1); // Default: Match Recruitment Filters

        [SettingPropertyDropdown("Mercenary Recruitment Policy", RequireRestart = false, HintText = "Control how mercenary troops are recruited from taverns.", Order = 5)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 5)]
        public Dropdown<MercenaryRecruitPolicyOption> MercenaryRecruitDropdown { get; set; } = new Dropdown<MercenaryRecruitPolicyOption>(MercenaryRecruitPolicyOptions, 0); // Default: Do Not Recruit Mercenaries

        [SettingPropertyInteger("Min Tier to Recruit", 1, 6, RequireRestart = false, HintText = "Minimum troop tier to buy.", Order = 6)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 5)]
        public int MinRecruitTier { get; set; } = 1;

        [SettingPropertyInteger("Max Tier to Recruit", 1, 6, RequireRestart = false, HintText = "Maximum troop tier to buy.", Order = 7)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 5)]
        public int MaxRecruitTier { get; set; } = 6;

        [SettingPropertyDropdown("Evaluation Target Tier", RequireRestart = false, HintText = "Evaluate filters against the troop's current purchase tier or their final upgrade tier.", Order = 8)]
        [SettingPropertyGroup("Recruitment", GroupOrder = 5)]
        public Dropdown<EvalTimeOption> EvalTimeDropdown { get; set; } = new Dropdown<EvalTimeOption>(EvalTimeOptions, 0);

        // --- Party Needs ---
        [SettingPropertyBool("Auto-Buy Food", RequireRestart = false, HintText = "Automatically buy food to maintain party supplies.", Order = 1)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 6)]
        public bool AutoBuyFood { get; set; } = true;

        [SettingPropertyInteger("Critical Food Days", 1, 30, RequireRestart = false,
            HintText = "Buy any available food before other item requests if supplies fall below this many days.", Order = 2)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 6)]
        public int CriticalFoodDays { get; set; } = 2;

        [SettingPropertyInteger("Party Food Days to Keep", 1, 100, RequireRestart = false,
            HintText = "Maintain this many total days of food after critical and variety food requests.", Order = 3)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 6)]
        public int PartyFoodDaysToKeep { get; set; } = 10;

        [SettingPropertyInteger("Min Party Size for Variety", 1, 100, RequireRestart = false,
            HintText = "Minimum party size before the mod requests specific food items for variety. Each food type targets a fair share of the total food buffer.", Order = 4)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 6)]
        public int MinPartySizeForVariety { get; set; } = 20;

        [SettingPropertyBool("Auto-Buy Riding Mounts", RequireRestart = false,
            HintText = "Automatically buy riding mounts for unmounted infantry.", Order = 5)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 6)]
        public bool AutoBuyMounts { get; set; } = true;

        [SettingPropertyDropdown("Critical Food Spend Mode", RequireRestart = false,
            HintText = "Controls when emergency food purchases run compared to other item requests.", Order = 6)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 6)]
        public Dropdown<RequestProfileOption> CriticalFoodSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Critical));

        [SettingPropertyDropdown("Food Variety Spend Mode", RequireRestart = false,
            HintText = "Controls when specific food variety requests run compared to other item requests.", Order = 7)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 6)]
        public Dropdown<RequestProfileOption> FoodVarietySpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Essential));

        [SettingPropertyDropdown("Food Buffer Spend Mode", RequireRestart = false,
            HintText = "Controls when the total food buffer request runs compared to other item requests.", Order = 8)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 6)]
        public Dropdown<RequestProfileOption> FoodBufferSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Routine));

        [SettingPropertyDropdown("Riding Mount Spend Mode", RequireRestart = false,
            HintText = "Controls when riding mount requests run compared to other item requests.", Order = 9)]
        [SettingPropertyGroup("Party Needs", GroupOrder = 6)]
        public Dropdown<RequestProfileOption> RidingMountSpendModeDropdown { get; set; } =
            new Dropdown<RequestProfileOption>(RequestProfileOptions.All, RequestProfileOptions.IndexOf(RequestProfile.Routine));

        // --- Recruitment Cultures ---
        [SettingPropertyBool("Recruit Empire", RequireRestart = false, HintText = "Recruit Empire culture troops.", Order = 1)]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 4)]
        public bool RecruitEmpire { get; set; } = true;

        [SettingPropertyBool("Recruit Vlandia", RequireRestart = false, HintText = "Recruit Vlandia culture troops.", Order = 2)]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 4)]
        public bool RecruitVlandia { get; set; } = true;

        [SettingPropertyBool("Recruit Battania", RequireRestart = false, HintText = "Recruit Battania culture troops.", Order = 3)]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 4)]
        public bool RecruitBattania { get; set; } = true;

        [SettingPropertyBool("Recruit Sturgia", RequireRestart = false, HintText = "Recruit Sturgia culture troops.", Order = 4)]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 4)]
        public bool RecruitSturgia { get; set; } = true;

        [SettingPropertyBool("Recruit Khuzait", RequireRestart = false, HintText = "Recruit Khuzait culture troops.", Order = 5)]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 4)]
        public bool RecruitKhuzait { get; set; } = true;

        [SettingPropertyBool("Recruit Aserai", RequireRestart = false, HintText = "Recruit Aserai culture troops.", Order = 6)]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 4)]
        public bool RecruitAserai { get; set; } = true;

        [SettingPropertyBool("Recruit Other Cultures", RequireRestart = false, HintText = "Recruit troops from other cultures (e.g. minor factions, modded cultures).", Order = 7)]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 4)]
        public bool RecruitOtherCultures { get; set; } = true;

        // --- Recruitment Roles ---
        [SettingPropertyBool("Recruit Frontline Infantry", RequireRestart = false, HintText = "Allow recruiting melee foot soldiers intended to hold the line. This includes shield infantry and low-tier recruits.", Order = 1)]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 3)]
        public bool RecruitShieldInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Shock Infantry", RequireRestart = false, HintText = "Allow recruiting skilled two-handed and offensive polearm foot soldiers with no shields.", Order = 2)]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 3)]
        public bool RecruitShockInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Pike Infantry", RequireRestart = false, HintText = "Allow recruiting foot soldiers whose primary role is anti-cavalry pikes.", Order = 3)]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 3)]
        public bool RecruitPikeInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Light Throwing Infantry", RequireRestart = false, HintText = "Allow recruiting foot soldiers whose primary role is throwing weapons rather than frontline or shock infantry.", Order = 4)]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 3)]
        public bool RecruitSkirmishers { get; set; } = true;

        [SettingPropertyBool("Recruit Foot Archers", RequireRestart = false, HintText = "Allow recruiting foot archers carrying bows.", Order = 5)]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 3)]
        public bool RecruitFootArchers { get; set; } = true;

        [SettingPropertyBool("Recruit Crossbowmen", RequireRestart = false, HintText = "Allow recruiting foot soldiers carrying crossbows.", Order = 6)]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 3)]
        public bool RecruitCrossbowmen { get; set; } = true;

        [SettingPropertyBool("Recruit Melee Cavalry", RequireRestart = false, HintText = "Allow recruiting mounted melee soldiers (lancers, light cavalry)..", Order = 7)]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 3)]
        public bool RecruitMeleeCavalry { get; set; } = true;

        [SettingPropertyBool("Recruit Horse Archers", RequireRestart = false, HintText = "Allow recruiting mounted ranged soldiers (bow/crossbow).", Order = 8)]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 3)]
        public bool RecruitHorseArchers { get; set; } = true;

        // --- Mount & Herding settings ---

        [SettingPropertyBool("Herding Penalty Protection", RequireRestart = false, HintText = "Automatically sell or slaughter excess animals to avoid herding speed penalty.", Order = 7)]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 2)]
        public bool PreventHerdingPenalty { get; set; } = true;

        [SettingPropertyBool("Slaughter Animals for Herding", RequireRestart = false, HintText = "Slaughter livestock and pack animals instead of selling them when herding limits are exceeded in settlements.", Order = 8)]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 2)]
        public bool SlaughterAnimalsForHerding { get; set; } = true;

        [SettingPropertyDropdown("Sell/Slaughter Riding Mounts", RequireRestart = false, HintText = "Control when the mod can sell or slaughter riding mounts.", Order = 9)]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 2)]
        public Dropdown<SellRidingMountsOption> SellRidingMountsDropdown { get; set; } = new Dropdown<SellRidingMountsOption>(SellRidingMountsOptions, 1);

        [SettingPropertyDropdown("Post-Battle Auto-Slaughter", RequireRestart = false, HintText = "Automatically slaughter animals after a battle to instantly clear herding penalties.", Order = 10)]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 2)]
        public Dropdown<PostBattleSlaughterOption> PostBattleSlaughterDropdown { get; set; } = new Dropdown<PostBattleSlaughterOption>(PostBattleSlaughterOptions, 2);

        // --- Garrison Donation Settings ---
        [SettingPropertyBool("Enable Garrison Donation", RequireRestart = false, HintText = "Over-recruit troops and donate them to garrison to farm influence.", Order = 1)]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 1)]
        public bool EnableGarrisonDonation { get; set; } = false;

        [SettingPropertyInteger("Max Garrison Roster Size", 50, 1000, RequireRestart = false, HintText = "Do not donate to garrison if it has reached or exceeded this capacity.", Order = 2)]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 1)]
        public int MaxGarrisonSize { get; set; } = 400;

        [SettingPropertyInteger("Min Garrison Donation Tier", 1, 6, RequireRestart = false, HintText = "Minimum tier of troop to donate to garrison.", Order = 3)]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 1)]
        public int MinDonationTier { get; set; } = 1;

        // --- Prisoner Keep Policies & Limits ---
        [SettingPropertyBool("Keep Hero Prisoners", RequireRestart = false, HintText = "Never automatically ransom or donate hero/lord prisoners.", Order = 1)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public bool KeepHeroPrisoners { get; set; } = true;

        [SettingPropertyDropdown("Noble Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for noble/elite prisoners.", Order = 2)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public Dropdown<PrisonerKeepPolicyOption> NoblePrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 2); // Keep Selected

        [SettingPropertyDropdown("Regular Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for standard regular prisoners.", Order = 3)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public Dropdown<PrisonerKeepPolicyOption> RegularPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 2); // Keep Selected

        [SettingPropertyDropdown("Bandit Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for bandit prisoners.", Order = 4)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public Dropdown<BanditPrisonerKeepPolicyOption> BanditPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<BanditPrisonerKeepPolicyOption>(BanditPrisonerKeepPolicyOptions, 0); // Ransom All

        [SettingPropertyDropdown("Other / Minor Faction Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for minor faction and other modded culture prisoners.", Order = 5)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public Dropdown<PrisonerKeepPolicyOption> OtherPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 2); // Keep Selected

        [SettingPropertyBool("Bypass Noble Prisoner Tier Limit", RequireRestart = false, HintText = "If enabled, noble prisoners will bypass the min tier keep limit.", Order = 6)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public bool BypassNoblePrisonerTierLimit { get; set; } = true;

        [SettingPropertyBool("Bypass Minor Faction/Other Tier Limit", RequireRestart = false, HintText = "If enabled, minor faction/other culture prisoners will bypass the min tier keep limit.", Order = 7)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public bool BypassOtherPrisonerTierLimit { get; set; } = true;

        [SettingPropertyInteger("Min Prisoner Tier to Keep", 1, 6, RequireRestart = false, HintText = "Minimum tier of regular/bandit prisoner to keep for recruitment.", Order = 8)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public int MinPrisonerTierToKeep { get; set; } = 4;

        [SettingPropertyBool("Perk-Based Prisoner Keep", RequireRestart = false, HintText = "Automatically override keep tier filters based on active Level 50 Leadership perks.", Order = 9)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public bool UsePerkBasedPrisonerKeep { get; set; } = true;

        [SettingPropertyBool("Bypass Noble Recruit Policy when Keeping", RequireRestart = false, HintText = "If enabled, keep selected noble prisoners matching role/culture filters even if noble recruitment from notables is disabled.", Order = 10)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public bool BypassNobleRecruitPolicy { get; set; } = false;

        [SettingPropertyBool("Bypass Regular Recruit Policy when Keeping", RequireRestart = false, HintText = "If enabled, keep selected regular prisoners matching role/culture filters even if regular recruitment from notables is disabled.", Order = 11)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public bool BypassRegularRecruitPolicy { get; set; } = false;

        [SettingPropertyBool("Bypass Mercenary Recruit Policy when Keeping", RequireRestart = false, HintText = "If enabled, keep selected mercenary prisoners matching role/culture filters even if tavern recruitment is disabled.", Order = 12)]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 0)]
        public bool BypassMercenaryRecruitPolicy { get; set; } = false;

        // --- Prisoner Ransom Settings ---
        [SettingPropertyBool("Auto-Ransom Prisoners", RequireRestart = false, HintText = "Automatically ransom standard prisoners for gold in taverns.", Order = 1)]
        [SettingPropertyGroup("Prisoners", GroupOrder = 0)]
        public bool AutoRansomPrisoners { get; set; } = true;

        // --- Prisoner Capacity Alerts ---
        [SettingPropertyInteger("Prisoner Capacity Alert Threshold (%)", 0, 100, RequireRestart = false, HintText = "Trigger alert if total prisoners exceed this percentage of capacity after automation. Set to 0 to disable.", Order = 1)]
        [SettingPropertyGroup("Prisoners/Capacity Alerts", GroupOrder = 0)]
        public int PrisonerCapacityAlertPercent { get; set; } = 33;

        [SettingPropertyInteger("Prisoner Stack Alert Capacity Limit (%)", 0, 100, RequireRestart = false, HintText = "Trigger alert if any single prisoner stack exceeds this percentage of total prisoner capacity. Set to 0 to disable.", Order = 2)]
        [SettingPropertyGroup("Prisoners/Capacity Alerts", GroupOrder = 0)]
        public int PrisonerStackAlertPercentLimit { get; set; } = 10;

        // --- Prisoner Donation Settings ---
        [SettingPropertyBool("Auto-Donate Prisoners to Dungeon", RequireRestart = false, HintText = "Automatically donate eligible prisoners to friendly town/castle dungeons to farm influence/XP.", Order = 1)]
        [SettingPropertyGroup("Prisoners/Donation", GroupOrder = 0)]
        public bool AutoDonatePrisoners { get; set; } = false;

        [SettingPropertyInteger("Min Tier to Donate", 1, 6, RequireRestart = false, HintText = "Minimum tier of prisoner to donate.", Order = 2)]
        [SettingPropertyGroup("Prisoners/Donation", GroupOrder = 0)]
        public int MinDonateTier { get; set; } = 3;

        [SettingPropertyBool("Prioritize High Tier for Donation", RequireRestart = false, HintText = "Donate higher tier prisoners first to maximize influence return.", Order = 3)]
        [SettingPropertyGroup("Prisoners/Donation", GroupOrder = 0)]
        public bool PrioritizeHighTierDonation { get; set; } = true;

        // --- Prisoner Discard Settings ---
        [SettingPropertyBool("Auto Discard Post-Battle Excess Prisoners", RequireRestart = false, HintText = "Automatically dump/discard low tier prisoners post-battle if player party is over prisoner capacity.", Order = 1)]
        [SettingPropertyGroup("Prisoners/Post-Battle Discard", GroupOrder = 0)]
        public bool AutoDiscardPrisonersPostBattle { get; set; } = false;

        [SettingPropertyInteger("Discard Prisoners Up To Tier", 1, 6, RequireRestart = false, HintText = "Discard excess prisoners up to (and including) this tier.", Order = 2)]
        [SettingPropertyGroup("Prisoners/Post-Battle Discard", GroupOrder = 0)]
        public int DiscardPrisonersUpToTier { get; set; } = 2;

        [SettingPropertyBool("Perk-Based Prisoner Discard", RequireRestart = false, HintText = "If enabled, automatically overrides discard filters based on active Level 50 Leadership perks (Stout Defender protects T4-6, Fervent Attacker protects T1-3).", Order = 3)]
        [SettingPropertyGroup("Prisoners/Post-Battle Discard", GroupOrder = 0)]
        public bool UsePerkBasedPrisonerDiscard { get; set; } = false;



        public EvalTime EvalTimeSetting => EvalTimeDropdown.SelectedValue.Value;
        public SellRidingMountsMode SellRidingMountsSetting => SellRidingMountsDropdown.SelectedValue.Value;
        public PostBattleSlaughterMode PostBattleSlaughterSetting => PostBattleSlaughterDropdown.SelectedValue.Value;
        public MercenaryRecruitPolicy MercenaryRecruitSetting => MercenaryRecruitDropdown.SelectedValue.Value;
        public NobleRecruitPolicy NobleRecruitSetting => NobleRecruitDropdown.SelectedValue.Value;
        public RegularRecruitPolicy RegularRecruitSetting => RegularRecruitDropdown.SelectedValue.Value;
        public RequestProfile CriticalFoodRequestProfile => CriticalFoodSpendModeDropdown.SelectedValue.Value;
        public RequestProfile FoodVarietyRequestProfile => FoodVarietySpendModeDropdown.SelectedValue.Value;
        public RequestProfile FoodBufferRequestProfile => FoodBufferSpendModeDropdown.SelectedValue.Value;
        public RequestProfile RidingMountRequestProfile => RidingMountSpendModeDropdown.SelectedValue.Value;
        public PrisonerKeepPolicy NoblePrisonerKeepPolicySetting => NoblePrisonerKeepPolicyDropdown.SelectedValue.Value;
        public PrisonerKeepPolicy RegularPrisonerKeepPolicySetting => RegularPrisonerKeepPolicyDropdown.SelectedValue.Value;
        public BanditPrisonerKeepPolicy BanditPrisonerKeepPolicySetting => BanditPrisonerKeepPolicyDropdown.SelectedValue.Value;
        public PrisonerKeepPolicy OtherPrisonerKeepPolicySetting => OtherPrisonerKeepPolicyDropdown.SelectedValue.Value;
    }
}
