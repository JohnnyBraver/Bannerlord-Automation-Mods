using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

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
        public override string Id => "PartyManager_v1";
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
        [SettingPropertyBool("Auto-Recruit Volunteers", RequireRestart = false, HintText = "Enable auto-recruitment from town/village notables.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public bool AutoRecruitVolunteers { get; set; } = true;

        [SettingPropertyBool("Recruit Regular Troops", RequireRestart = false, HintText = "Recruit regular (non-noble) volunteer troops.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public bool RecruitRegularTroops { get; set; } = true;

        [SettingPropertyDropdown("Noble Recruitment Policy", RequireRestart = false, HintText = "Control how noble troops are recruited from notables.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public Dropdown<NobleRecruitPolicyOption> NobleRecruitDropdown { get; set; } = new Dropdown<NobleRecruitPolicyOption>(NobleRecruitPolicyOptions, 1); // Default: Match Recruitment Filters

        [SettingPropertyDropdown("Mercenary Recruitment Policy", RequireRestart = false, HintText = "Control how mercenary troops are recruited from taverns.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public Dropdown<MercenaryRecruitPolicyOption> MercenaryRecruitDropdown { get; set; } = new Dropdown<MercenaryRecruitPolicyOption>(MercenaryRecruitPolicyOptions, 0); // Default: Do Not Recruit Mercenaries

        [SettingPropertyInteger("Min Tier to Recruit", 1, 6, RequireRestart = false, HintText = "Minimum troop tier to buy.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public int MinRecruitTier { get; set; } = 1;

        [SettingPropertyInteger("Max Tier to Recruit", 1, 6, RequireRestart = false, HintText = "Maximum troop tier to buy.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public int MaxRecruitTier { get; set; } = 6;

        [SettingPropertyDropdown("Evaluation Target Tier", RequireRestart = false, HintText = "Evaluate filters against the troop's current purchase tier or their final upgrade tier.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public Dropdown<EvalTimeOption> EvalTimeDropdown { get; set; } = new Dropdown<EvalTimeOption>(EvalTimeOptions, 0);

        // --- Recruitment Cultures ---
        [SettingPropertyBool("Recruit Empire", RequireRestart = false, HintText = "Recruit Empire culture troops.")]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 1)]
        public bool RecruitEmpire { get; set; } = true;

        [SettingPropertyBool("Recruit Vlandia", RequireRestart = false, HintText = "Recruit Vlandia culture troops.")]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 1)]
        public bool RecruitVlandia { get; set; } = true;

        [SettingPropertyBool("Recruit Battania", RequireRestart = false, HintText = "Recruit Battania culture troops.")]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 1)]
        public bool RecruitBattania { get; set; } = true;

        [SettingPropertyBool("Recruit Sturgia", RequireRestart = false, HintText = "Recruit Sturgia culture troops.")]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 1)]
        public bool RecruitSturgia { get; set; } = true;

        [SettingPropertyBool("Recruit Khuzait", RequireRestart = false, HintText = "Recruit Khuzait culture troops.")]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 1)]
        public bool RecruitKhuzait { get; set; } = true;

        [SettingPropertyBool("Recruit Aserai", RequireRestart = false, HintText = "Recruit Aserai culture troops.")]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 1)]
        public bool RecruitAserai { get; set; } = true;

        [SettingPropertyBool("Recruit Other Cultures", RequireRestart = false, HintText = "Recruit troops from other cultures (e.g. minor factions, modded cultures).")]
        [SettingPropertyGroup("Recruitment Filters - Cultures", GroupOrder = 1)]
        public bool RecruitOtherCultures { get; set; } = true;

        // --- Recruitment Roles ---
        [SettingPropertyBool("Recruit Shield Infantry", RequireRestart = false, HintText = "Allow recruiting frontline foot soldiers with shields.")]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 2)]
        public bool RecruitShieldInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Shock Infantry", RequireRestart = false, HintText = "Allow recruiting two-handed and polearm foot soldiers with no shields.")]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 2)]
        public bool RecruitShockInfantry { get; set; } = true;

        [SettingPropertyBool("Recruit Skirmishers", RequireRestart = false, HintText = "Allow recruiting foot soldiers carrying throwing weapons.")]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 2)]
        public bool RecruitSkirmishers { get; set; } = true;

        [SettingPropertyBool("Recruit Foot Archers", RequireRestart = false, HintText = "Allow recruiting foot archers carrying bows.")]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 2)]
        public bool RecruitFootArchers { get; set; } = true;

        [SettingPropertyBool("Recruit Crossbowmen", RequireRestart = false, HintText = "Allow recruiting foot soldiers carrying crossbows.")]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 2)]
        public bool RecruitCrossbowmen { get; set; } = true;

        [SettingPropertyBool("Recruit Melee Cavalry", RequireRestart = false, HintText = "Allow recruiting mounted melee soldiers (lancers, light cavalry)..")]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 2)]
        public bool RecruitMeleeCavalry { get; set; } = true;

        [SettingPropertyBool("Recruit Horse Archers", RequireRestart = false, HintText = "Allow recruiting mounted ranged soldiers (bow/crossbow).")]
        [SettingPropertyGroup("Recruitment Filters - Roles", GroupOrder = 2)]
        public bool RecruitHorseArchers { get; set; } = true;

        // --- Mount & Herding settings ---
        [SettingPropertyBool("Auto-Buy Food to Restock", RequireRestart = false, HintText = "Automatically buy food to maintain supply for soldiers.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public bool AutoBuyFood { get; set; } = true;

        [SettingPropertyInteger("Party Food Days to Keep", 1, 100, RequireRestart = false, HintText = "Maintain at least this many days of food supply for the party.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public int PartyFoodDaysToKeep { get; set; } = 10;

        [SettingPropertyInteger("Min Party Size for Variety", 1, 100, RequireRestart = false, HintText = "Minimum party size before the mod starts buying a variety of foods for Steward XP. Below this size, the mod only buys the cheapest food to prevent starvation.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public int MinPartySizeForVariety { get; set; } = 20;

        [SettingPropertyInteger("Min Gold for Mounts", 1000, 50000, RequireRestart = false, HintText = "Minimum gold reserve required to buy mounts. Below this, mount buying is skipped.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public int MinGoldForMounts { get; set; } = 10000;

        [SettingPropertyInteger("Min Gold for Variety", 500, 20000, RequireRestart = false, HintText = "Minimum gold reserve required to buy food varieties. Below this, only survival food is bought.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public int MinGoldForVariety { get; set; } = 3000;

        [SettingPropertyBool("Auto-Buy Riding Mounts for Speed", RequireRestart = false, HintText = "Automatically buy mounts for foot soldiers to upgrade party speed.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public bool AutoBuyMounts { get; set; } = true;

        [SettingPropertyBool("Herding Penalty Protection", RequireRestart = false, HintText = "Automatically sell or slaughter excess animals to avoid herding speed penalty.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public bool PreventHerdingPenalty { get; set; } = true;

        [SettingPropertyBool("Slaughter Animals for Herding", RequireRestart = false, HintText = "Slaughter livestock and pack animals instead of selling them when herding limits are exceeded in settlements.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public bool SlaughterAnimalsForHerding { get; set; } = true;

        [SettingPropertyDropdown("Sell/Slaughter Riding Mounts", RequireRestart = false, HintText = "Control when the mod can sell or slaughter riding mounts.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public Dropdown<SellRidingMountsOption> SellRidingMountsDropdown { get; set; } = new Dropdown<SellRidingMountsOption>(SellRidingMountsOptions, 1);

        [SettingPropertyDropdown("Post-Battle Auto-Slaughter", RequireRestart = false, HintText = "Automatically slaughter animals after a battle to instantly clear herding penalties.")]
        [SettingPropertyGroup("Mounts & Logistics", GroupOrder = 3)]
        public Dropdown<PostBattleSlaughterOption> PostBattleSlaughterDropdown { get; set; } = new Dropdown<PostBattleSlaughterOption>(PostBattleSlaughterOptions, 2);

        // --- Garrison Donation Settings ---
        [SettingPropertyBool("Enable Garrison Donation", RequireRestart = false, HintText = "Over-recruit troops and donate them to garrison to farm influence.")]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 4)]
        public bool EnableGarrisonDonation { get; set; } = false;

        [SettingPropertyInteger("Max Garrison Roster Size", 50, 1000, RequireRestart = false, HintText = "Do not donate to garrison if it has reached or exceeded this capacity.")]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 4)]
        public int MaxGarrisonSize { get; set; } = 400;

        [SettingPropertyInteger("Min Garrison Donation Tier", 1, 6, RequireRestart = false, HintText = "Minimum tier of troop to donate to garrison.")]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 4)]
        public int MinDonationTier { get; set; } = 1;

        // --- Prisoner Keep Policies & Limits ---
        [SettingPropertyBool("Keep Hero Prisoners", RequireRestart = false, HintText = "Never automatically ransom or donate hero/lord prisoners.")]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 5)]
        public bool KeepHeroPrisoners { get; set; } = true;

        [SettingPropertyDropdown("Noble Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for noble/elite prisoners.")]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 5)]
        public Dropdown<PrisonerKeepPolicyOption> NoblePrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 2); // Keep Selected

        [SettingPropertyDropdown("Regular Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for standard regular prisoners.")]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 5)]
        public Dropdown<PrisonerKeepPolicyOption> RegularPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 2); // Keep Selected

        [SettingPropertyDropdown("Bandit Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for bandit prisoners.")]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 5)]
        public Dropdown<BanditPrisonerKeepPolicyOption> BanditPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<BanditPrisonerKeepPolicyOption>(BanditPrisonerKeepPolicyOptions, 0); // Ransom All

        [SettingPropertyBool("Bypass Noble Prisoner Tier Limit", RequireRestart = false, HintText = "If enabled, noble prisoners (and noble-upgrading bandits if kept) will bypass the min tier keep limit.")]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 5)]
        public bool BypassNoblePrisonerTierLimit { get; set; } = true;

        [SettingPropertyInteger("Min Prisoner Tier to Keep", 1, 6, RequireRestart = false, HintText = "Minimum tier of regular/bandit prisoner to keep for recruitment.")]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 5)]
        public int MinPrisonerTierToKeep { get; set; } = 4;

        [SettingPropertyBool("Perk-Based Prisoner Keep", RequireRestart = false, HintText = "Automatically override keep tier filters based on active Level 50 Leadership perks (Stout Defender keeping T4-6, Fervent Attacker keeping T1-3).")]
        [SettingPropertyGroup("Prisoners/Keep Policies & Limits", GroupOrder = 5)]
        public bool UsePerkBasedPrisonerKeep { get; set; } = true;

        // --- Prisoner Ransom Settings ---
        [SettingPropertyBool("Auto-Ransom Prisoners", RequireRestart = false, HintText = "Automatically ransom standard prisoners for gold in taverns.")]
        [SettingPropertyGroup("Prisoners/Ransom", GroupOrder = 5)]
        public bool AutoRansomPrisoners { get; set; } = true;

        // --- Prisoner Capacity Alerts ---
        [SettingPropertyInteger("Prisoner Capacity Alert Threshold (%)", 0, 100, RequireRestart = false, HintText = "Trigger alert if total prisoners exceed this percentage of capacity after automation. Set to 0 to disable.")]
        [SettingPropertyGroup("Prisoners/Capacity Alerts", GroupOrder = 5)]
        public int PrisonerCapacityAlertPercent { get; set; } = 20;

        [SettingPropertyInteger("Prisoner Stack Alert Flat Limit", 0, 1000, RequireRestart = false, HintText = "Trigger alert if any single prisoner stack has at least this many prisoners. Set to 0 to disable.")]
        [SettingPropertyGroup("Prisoners/Capacity Alerts", GroupOrder = 5)]
        public int PrisonerStackAlertFlatLimit { get; set; } = 5;

        [SettingPropertyInteger("Prisoner Stack Alert Capacity Limit (%)", 0, 100, RequireRestart = false, HintText = "Trigger alert if any single prisoner stack exceeds this percentage of total prisoner capacity. Set to 0 to disable.")]
        [SettingPropertyGroup("Prisoners/Capacity Alerts", GroupOrder = 5)]
        public int PrisonerStackAlertPercentLimit { get; set; } = 10;

        // --- Prisoner Donation Settings ---
        [SettingPropertyBool("Auto-Donate Prisoners to Dungeon", RequireRestart = false, HintText = "Automatically donate prisoners to friendly town/castle dungeons to farm influence/XP.")]
        [SettingPropertyGroup("Prisoners/Donation", GroupOrder = 5)]
        public bool AutoDonatePrisoners { get; set; } = false;

        [SettingPropertyInteger("Min Tier to Donate", 1, 6, RequireRestart = false, HintText = "Minimum tier of prisoner to donate.")]
        [SettingPropertyGroup("Prisoners/Donation", GroupOrder = 5)]
        public int MinDonateTier { get; set; } = 3;

        [SettingPropertyBool("Prioritize High Tier for Donation", RequireRestart = false, HintText = "Donate higher tier prisoners first to maximize influence return.")]
        [SettingPropertyGroup("Prisoners/Donation", GroupOrder = 5)]
        public bool PrioritizeHighTierDonation { get; set; } = true;

        // --- Prisoner Discard Settings ---
        [SettingPropertyBool("Auto Discard Post-Battle Excess Prisoners", RequireRestart = false, HintText = "Automatically dump/discard low tier prisoners post-battle if player party is over prisoner capacity.")]
        [SettingPropertyGroup("Prisoners/Post-Battle Discard", GroupOrder = 5)]
        public bool AutoDiscardPrisonersPostBattle { get; set; } = false;

        [SettingPropertyInteger("Discard Prisoners Up To Tier", 1, 6, RequireRestart = false, HintText = "Discard excess prisoners up to (and including) this tier.")]
        [SettingPropertyGroup("Prisoners/Post-Battle Discard", GroupOrder = 5)]
        public int DiscardPrisonersUpToTier { get; set; } = 2;

        [SettingPropertyBool("Perk-Based Prisoner Discard", RequireRestart = false, HintText = "If enabled, automatically overrides discard filters based on active Level 50 Leadership perks (Stout Defender protects T4-6, Fervent Attacker protects T1-3).")]
        [SettingPropertyGroup("Prisoners/Post-Battle Discard", GroupOrder = 5)]
        public bool UsePerkBasedPrisonerDiscard { get; set; } = false;



        public EvalTime EvalTimeSetting => EvalTimeDropdown.SelectedValue.Value;
        public SellRidingMountsMode SellRidingMountsSetting => SellRidingMountsDropdown.SelectedValue.Value;
        public PostBattleSlaughterMode PostBattleSlaughterSetting => PostBattleSlaughterDropdown.SelectedValue.Value;
        public MercenaryRecruitPolicy MercenaryRecruitSetting => MercenaryRecruitDropdown.SelectedValue.Value;
        public NobleRecruitPolicy NobleRecruitSetting => NobleRecruitDropdown.SelectedValue.Value;
        public PrisonerKeepPolicy NoblePrisonerKeepPolicySetting => NoblePrisonerKeepPolicyDropdown.SelectedValue.Value;
        public PrisonerKeepPolicy RegularPrisonerKeepPolicySetting => RegularPrisonerKeepPolicyDropdown.SelectedValue.Value;
        public BanditPrisonerKeepPolicy BanditPrisonerKeepPolicySetting => BanditPrisonerKeepPolicyDropdown.SelectedValue.Value;
    }
}
