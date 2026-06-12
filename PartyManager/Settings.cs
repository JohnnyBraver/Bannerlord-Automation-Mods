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

    public enum VolunteerRecruitPolicy
    {
        None,
        RegularOnly,
        NobleOnly,
        RegularAndNoble
    }

    public class VolunteerRecruitPolicyOption
    {
        private readonly string _name;
        public VolunteerRecruitPolicy Value { get; }
        public VolunteerRecruitPolicyOption(string name, VolunteerRecruitPolicy value) { _name = name; Value = value; }
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

        private static readonly IReadOnlyList<VolunteerRecruitPolicyOption> VolunteerRecruitPolicyOptions = new List<VolunteerRecruitPolicyOption>
        {
            new VolunteerRecruitPolicyOption("None", VolunteerRecruitPolicy.None),
            new VolunteerRecruitPolicyOption("Regular Only", VolunteerRecruitPolicy.RegularOnly),
            new VolunteerRecruitPolicyOption("Noble Only", VolunteerRecruitPolicy.NobleOnly),
            new VolunteerRecruitPolicyOption("Regular & Noble", VolunteerRecruitPolicy.RegularAndNoble)
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

        [SettingPropertyDropdown("Volunteer Recruitment Policy", RequireRestart = false, HintText = "Choose which classes of volunteer troops to recruit.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public Dropdown<VolunteerRecruitPolicyOption> VolunteerRecruitDropdown { get; set; } = new Dropdown<VolunteerRecruitPolicyOption>(VolunteerRecruitPolicyOptions, 3); // Default: Regular & Noble

        [SettingPropertyBool("Recruit Mercenary Troops", RequireRestart = false, HintText = "Recruit mercenary troops from taverns.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public bool RecruitMercenary { get; set; } = true;

        [SettingPropertyInteger("Min Tier to Recruit", 1, 6, RequireRestart = false, HintText = "Minimum troop tier to buy.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public int MinRecruitTier { get; set; } = 1;

        [SettingPropertyInteger("Max Tier to Recruit", 1, 6, RequireRestart = false, HintText = "Maximum troop tier to buy.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public int MaxRecruitTier { get; set; } = 6;

        [SettingPropertyDropdown("Evaluation Target Tier", RequireRestart = false, HintText = "Evaluate filters against the troop's current purchase tier or their final upgrade tier.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public Dropdown<EvalTimeOption> EvalTimeDropdown { get; set; } = new Dropdown<EvalTimeOption>(EvalTimeOptions, 0);

        [SettingPropertyBool("Recruit Melee Archetype", RequireRestart = false, HintText = "Allow recruiting troops focused on one-handed/two-handed/polearm melee.")]
        [SettingPropertyGroup("Recruitment/Archetypes", GroupOrder = 1)]
        public bool RecruitMelee { get; set; } = true;

        [SettingPropertyBool("Recruit Shield Archetype", RequireRestart = false, HintText = "Prioritize shield-carrying melee troops.")]
        [SettingPropertyGroup("Recruitment/Archetypes", GroupOrder = 1)]
        public bool RecruitShield { get; set; } = true;

        [SettingPropertyBool("Recruit Bow/Ranged Archetype", RequireRestart = false, HintText = "Allow recruiting archers (bow).")]
        [SettingPropertyGroup("Recruitment/Archetypes", GroupOrder = 1)]
        public bool RecruitBow { get; set; } = true;

        [SettingPropertyBool("Recruit Crossbow Archetype", RequireRestart = false, HintText = "Allow recruiting crossbowmen.")]
        [SettingPropertyGroup("Recruitment/Archetypes", GroupOrder = 1)]
        public bool RecruitCrossbow { get; set; } = true;

        [SettingPropertyBool("Recruit Throwing Archetype", RequireRestart = false, HintText = "Allow recruiting skirmishers focusing on throwing.")]
        [SettingPropertyGroup("Recruitment/Archetypes", GroupOrder = 1)]
        public bool RecruitThrowing { get; set; } = true;

        [SettingPropertyBool("Recruit Mounted Troops", RequireRestart = false, HintText = "Allow recruiting cavalry.")]
        [SettingPropertyGroup("Recruitment/Archetypes", GroupOrder = 1)]
        public bool RecruitMounted { get; set; } = true;

        [SettingPropertyBool("Recruit Foot Troops", RequireRestart = false, HintText = "Allow recruiting infantry.")]
        [SettingPropertyGroup("Recruitment/Archetypes", GroupOrder = 1)]
        public bool RecruitFoot { get; set; } = true;

        // --- Mount & Herding settings ---
        [SettingPropertyBool("Auto-Buy Food to Restock", RequireRestart = false, HintText = "Automatically buy food to maintain supply for soldiers.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public bool AutoBuyFood { get; set; } = true;

        [SettingPropertyInteger("Party Food Days to Keep", 1, 100, RequireRestart = false, HintText = "Maintain at least this many days of food supply for the party.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public int PartyFoodDaysToKeep { get; set; } = 10;

        [SettingPropertyInteger("Min Party Size for Variety", 1, 100, RequireRestart = false, HintText = "Minimum party size before the mod starts buying a variety of foods for Steward XP. Below this size, the mod only buys the cheapest food to prevent starvation.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public int MinPartySizeForVariety { get; set; } = 20;

        [SettingPropertyInteger("Min Gold for Mounts", 1000, 50000, RequireRestart = false, HintText = "Minimum gold reserve required to buy mounts. Below this, mount buying is skipped.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public int MinGoldForMounts { get; set; } = 10000;

        [SettingPropertyInteger("Min Gold for Variety", 500, 20000, RequireRestart = false, HintText = "Minimum gold reserve required to buy food varieties. Below this, only survival food is bought.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public int MinGoldForVariety { get; set; } = 3000;

        [SettingPropertyBool("Auto-Buy Riding Mounts for Speed", RequireRestart = false, HintText = "Automatically buy mounts for foot soldiers to upgrade party speed.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public bool AutoBuyMounts { get; set; } = true;


        [SettingPropertyBool("Herding Penalty Protection", RequireRestart = false, HintText = "Automatically sell or slaughter excess animals to avoid herding speed penalty.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public bool PreventHerdingPenalty { get; set; } = true;

        [SettingPropertyBool("Slaughter Animals for Herding", RequireRestart = false, HintText = "Slaughter livestock and pack animals instead of selling them when herding limits are exceeded in settlements.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public bool SlaughterAnimalsForHerding { get; set; } = true;

        [SettingPropertyDropdown("Sell/Slaughter Riding Mounts", RequireRestart = false, HintText = "Control when the mod can sell or slaughter riding mounts.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public Dropdown<SellRidingMountsOption> SellRidingMountsDropdown { get; set; } = new Dropdown<SellRidingMountsOption>(SellRidingMountsOptions, 1);

        [SettingPropertyDropdown("Post-Battle Auto-Slaughter", RequireRestart = false, HintText = "Automatically slaughter animals after a battle to instantly clear herding penalties.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public Dropdown<PostBattleSlaughterOption> PostBattleSlaughterDropdown { get; set; } = new Dropdown<PostBattleSlaughterOption>(PostBattleSlaughterOptions, 2);

        // --- Garrison Donation Settings ---
        [SettingPropertyBool("Enable Garrison Donation", RequireRestart = false, HintText = "Over-recruit troops and donate them to garrison to farm influence.")]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 3)]
        public bool EnableGarrisonDonation { get; set; } = false;

        [SettingPropertyInteger("Max Garrison Roster Size", 50, 1000, RequireRestart = false, HintText = "Do not donate to garrison if it has reached or exceeded this capacity.")]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 3)]
        public int MaxGarrisonSize { get; set; } = 400;

        [SettingPropertyInteger("Min Garrison Donation Tier", 1, 6, RequireRestart = false, HintText = "Minimum tier of troop to donate to garrison.")]
        [SettingPropertyGroup("Garrison Donation", GroupOrder = 3)]
        public int MinDonationTier { get; set; } = 1;

        // --- Prisoner Ransom Settings ---
        [SettingPropertyBool("Auto-Ransom Prisoners", RequireRestart = false, HintText = "Automatically ransom standard prisoners for gold in taverns.")]
        [SettingPropertyGroup("Prisoners", GroupOrder = 4)]
        public bool AutoRansomPrisoners { get; set; } = true;

        [SettingPropertyInteger("Min Tier to Ransom", 1, 6, RequireRestart = false, HintText = "Minimum tier of prisoner to automatically ransom.")]
        [SettingPropertyGroup("Prisoners", GroupOrder = 4)]
        public int MinRansomTier { get; set; } = 1;

        [SettingPropertyBool("Keep Hero Prisoners", RequireRestart = false, HintText = "Never automatically ransom or donate hero/lord prisoners.")]
        [SettingPropertyGroup("Prisoners", GroupOrder = 4)]
        public bool KeepHeroPrisoners { get; set; } = true;

        [SettingPropertyDropdown("Noble Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for noble/elite prisoners.")]
        [SettingPropertyGroup("Prisoners/Keep Filters", GroupOrder = 5)]
        public Dropdown<PrisonerKeepPolicyOption> NoblePrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 1); // Keep All

        [SettingPropertyDropdown("Regular Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for standard regular prisoners.")]
        [SettingPropertyGroup("Prisoners/Keep Filters", GroupOrder = 5)]
        public Dropdown<PrisonerKeepPolicyOption> RegularPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<PrisonerKeepPolicyOption>(PrisonerKeepPolicyOptions, 0); // Ransom All

        [SettingPropertyDropdown("Bandit Prisoner Keep Policy", RequireRestart = false, HintText = "Keep policy for bandit prisoners.")]
        [SettingPropertyGroup("Prisoners/Keep Filters", GroupOrder = 5)]
        public Dropdown<BanditPrisonerKeepPolicyOption> BanditPrisonerKeepPolicyDropdown { get; set; } = new Dropdown<BanditPrisonerKeepPolicyOption>(BanditPrisonerKeepPolicyOptions, 0); // Ransom All

        [SettingPropertyBool("Bypass Noble Prisoner Tier Limit", RequireRestart = false, HintText = "If enabled, noble prisoners (and noble-upgrading bandits if kept) will bypass the min tier keep limit.")]
        [SettingPropertyGroup("Prisoners/Keep Filters", GroupOrder = 5)]
        public bool BypassNoblePrisonerTierLimit { get; set; } = true;

        [SettingPropertyInteger("Min Prisoner Tier to Keep", 1, 6, RequireRestart = false, HintText = "Minimum tier of regular/bandit prisoner to keep for recruitment.")]
        [SettingPropertyGroup("Prisoners/Keep Filters", GroupOrder = 5)]
        public int MinPrisonerTierToKeep { get; set; } = 3;

        // --- Prisoner Donation Settings ---
        [SettingPropertyBool("Auto-Donate Prisoners to Dungeon", RequireRestart = false, HintText = "Automatically donate prisoners to friendly town/castle dungeons to farm influence/XP.")]
        [SettingPropertyGroup("Prisoners/Dungeon Donation", GroupOrder = 6)]
        public bool AutoDonatePrisoners { get; set; } = false;

        [SettingPropertyInteger("Min Tier to Donate", 1, 6, RequireRestart = false, HintText = "Minimum tier of prisoner to donate.")]
        [SettingPropertyGroup("Prisoners/Dungeon Donation", GroupOrder = 6)]
        public int MinDonateTier { get; set; } = 3;

        [SettingPropertyBool("Prioritize High Tier for Donation", RequireRestart = false, HintText = "Donate higher tier prisoners first to maximize influence return.")]
        [SettingPropertyGroup("Prisoners/Dungeon Donation", GroupOrder = 6)]
        public bool PrioritizeHighTierDonation { get; set; } = true;

        // --- Prisoner Discard Settings ---
        [SettingPropertyBool("Auto Discard Post-Battle Excess Prisoners", RequireRestart = false, HintText = "Automatically dump/discard low tier prisoners post-battle if player party is over prisoner capacity.")]
        [SettingPropertyGroup("Prisoners/Post-Battle Discard", GroupOrder = 7)]
        public bool AutoDiscardPrisonersPostBattle { get; set; } = false;

        [SettingPropertyInteger("Discard Prisoners Up To Tier", 1, 6, RequireRestart = false, HintText = "Discard excess prisoners up to (and including) this tier.")]
        [SettingPropertyGroup("Prisoners/Post-Battle Discard", GroupOrder = 7)]
        public int DiscardPrisonersUpToTier { get; set; } = 2;

        public EvalTime EvalTimeSetting => EvalTimeDropdown.SelectedValue.Value;
        public SellRidingMountsMode SellRidingMountsSetting => SellRidingMountsDropdown.SelectedValue.Value;
        public PostBattleSlaughterMode PostBattleSlaughterSetting => PostBattleSlaughterDropdown.SelectedValue.Value;
        public VolunteerRecruitPolicy VolunteerRecruitSetting => VolunteerRecruitDropdown.SelectedValue.Value;
        public bool RecruitRegularSetting => VolunteerRecruitSetting == VolunteerRecruitPolicy.RegularOnly || VolunteerRecruitSetting == VolunteerRecruitPolicy.RegularAndNoble;
        public bool RecruitNobleSetting => VolunteerRecruitSetting == VolunteerRecruitPolicy.NobleOnly || VolunteerRecruitSetting == VolunteerRecruitPolicy.RegularAndNoble;
        public PrisonerKeepPolicy NoblePrisonerKeepPolicySetting => NoblePrisonerKeepPolicyDropdown.SelectedValue.Value;
        public PrisonerKeepPolicy RegularPrisonerKeepPolicySetting => RegularPrisonerKeepPolicyDropdown.SelectedValue.Value;
        public BanditPrisonerKeepPolicy BanditPrisonerKeepPolicySetting => BanditPrisonerKeepPolicyDropdown.SelectedValue.Value;
    }
}
