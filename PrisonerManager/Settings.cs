using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace PrisonerManager
{
    public enum KeepPolicy
    {
        SellAll,
        KeepAll,
        KeepSelected
    }

    public class KeepPolicyOption
    {
        private readonly string _name;
        public KeepPolicy Value { get; }
        public KeepPolicyOption(string name, KeepPolicy value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum BanditKeepPolicy
    {
        SellAll,
        KeepAll,
        KeepNobleOnly,
        KeepSelected
    }

    public class BanditKeepPolicyOption
    {
        private readonly string _name;
        public BanditKeepPolicy Value { get; }
        public BanditKeepPolicyOption(string name, BanditKeepPolicy value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public enum KeepEvalTime
    {
        FinalUpgradeTier,
        CurrentTier
    }

    public class KeepEvalTimeOption
    {
        private readonly string _name;
        public KeepEvalTime Value { get; }
        public KeepEvalTimeOption(string name, KeepEvalTime value) { _name = name; Value = value; }
        public override string ToString() => _name;
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "PrisonerManager_v1";
        public override string DisplayName => "Prisoner Manager";
        public override string FolderName => "PrisonerManager";
        public override string FormatType => "json";

        private static readonly IReadOnlyList<KeepPolicyOption> KeepPolicyOptions = new List<KeepPolicyOption>
        {
            new KeepPolicyOption("Ransom All", KeepPolicy.SellAll),
            new KeepPolicyOption("Keep All", KeepPolicy.KeepAll),
            new KeepPolicyOption("Keep Selected", KeepPolicy.KeepSelected)
        };

        private static readonly IReadOnlyList<BanditKeepPolicyOption> BanditKeepPolicyOptions = new List<BanditKeepPolicyOption>
        {
            new BanditKeepPolicyOption("Ransom All", BanditKeepPolicy.SellAll),
            new BanditKeepPolicyOption("Keep All", BanditKeepPolicy.KeepAll),
            new BanditKeepPolicyOption("Keep Noble Only", BanditKeepPolicy.KeepNobleOnly),
            new BanditKeepPolicyOption("Keep Selected", BanditKeepPolicy.KeepSelected)
        };

        private static readonly IReadOnlyList<KeepEvalTimeOption> KeepEvalTimeOptions = new List<KeepEvalTimeOption>
        {
            new KeepEvalTimeOption("Final Upgrade Leaves", KeepEvalTime.FinalUpgradeTier),
            new KeepEvalTimeOption("Current Troop Type", KeepEvalTime.CurrentTier)
        };

        // --- Ransom Settings ---
        [SettingPropertyBool("Auto-Ransom Prisoners", RequireRestart = false, HintText = "Automatically ransom standard prisoners for gold in taverns.")]
        [SettingPropertyGroup("1. Ransom Settings", GroupOrder = 0)]
        public bool AutoRansom { get; set; } = true;

        [SettingPropertyInteger("Min Tier to Ransom", 1, 6, RequireRestart = false, HintText = "Minimum tier of prisoner to automatically ransom.")]
        [SettingPropertyGroup("1. Ransom Settings", GroupOrder = 0)]
        public int MinRansomTier { get; set; } = 1;

        // --- Keep / Recruit Filter Settings ---
        [SettingPropertyBool("Keep Hero Prisoners", RequireRestart = false, HintText = "Never automatically ransom hero/lord prisoners.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public bool KeepHeroes { get; set; } = true;

        [SettingPropertyDropdown("Noble Keep Policy", RequireRestart = false, HintText = "Keep policy for noble/elite prisoners.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public Dropdown<KeepPolicyOption> NobleKeepPolicyDropdown { get; set; } = new Dropdown<KeepPolicyOption>(KeepPolicyOptions, 1);

        [SettingPropertyDropdown("Regular Keep Policy", RequireRestart = false, HintText = "Keep policy for standard regular prisoners.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public Dropdown<KeepPolicyOption> RegularKeepPolicyDropdown { get; set; } = new Dropdown<KeepPolicyOption>(KeepPolicyOptions, 0);

        [SettingPropertyDropdown("Bandit Keep Policy", RequireRestart = false, HintText = "Keep policy for bandit prisoners.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public Dropdown<BanditKeepPolicyOption> BanditKeepPolicyDropdown { get; set; } = new Dropdown<BanditKeepPolicyOption>(BanditKeepPolicyOptions, 0);

        [SettingPropertyDropdown("Evaluation Target Type", RequireRestart = false, HintText = "Evaluate keep filters against the prisoner's current troop type or their final upgrade leaves.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public Dropdown<KeepEvalTimeOption> KeepEvalTimeDropdown { get; set; } = new Dropdown<KeepEvalTimeOption>(KeepEvalTimeOptions, 0);

        [SettingPropertyBool("Bypass Noble Tier Limit", RequireRestart = false, HintText = "If enabled, noble prisoners (and noble-upgrading bandits if kept) will bypass the min tier keep limit.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public bool BypassNobleTierLimit { get; set; } = true;

        [SettingPropertyInteger("Min Tier to Keep", 1, 6, RequireRestart = false, HintText = "Minimum tier of regular/bandit prisoner to keep for recruitment.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public int MinTierToKeep { get; set; } = 3;

        // --- Keep Selected Archetypes Subgroup ---
        [SettingPropertyBool("Keep Mounted Archetype", RequireRestart = false, HintText = "Keep cavalry units when policy is Keep Selected.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters/Selected Archetypes", GroupOrder = 2)]
        public bool KeepMounted { get; set; } = true;

        [SettingPropertyBool("Keep Foot Archetype", RequireRestart = false, HintText = "Keep infantry units when policy is Keep Selected.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters/Selected Archetypes", GroupOrder = 2)]
        public bool KeepFoot { get; set; } = true;

        [SettingPropertyBool("Keep Melee Archetype", RequireRestart = false, HintText = "Keep melee units when policy is Keep Selected.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters/Selected Archetypes", GroupOrder = 2)]
        public bool KeepMelee { get; set; } = true;

        [SettingPropertyBool("Keep Ranged Archetype", RequireRestart = false, HintText = "Keep ranged units when policy is Keep Selected.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters/Selected Archetypes", GroupOrder = 2)]
        public bool KeepRanged { get; set; } = true;

        // --- Donation Settings ---
        [SettingPropertyBool("Auto-Donate Prisoners to Dungeon", RequireRestart = false, HintText = "Automatically donate prisoners to friendly town/castle dungeons to farm influence/XP.")]
        [SettingPropertyGroup("3. Dungeon Donation", GroupOrder = 2)]
        public bool AutoDonate { get; set; } = false;

        [SettingPropertyInteger("Min Tier to Donate", 1, 6, RequireRestart = false, HintText = "Minimum tier of prisoner to donate.")]
        [SettingPropertyGroup("3. Dungeon Donation", GroupOrder = 2)]
        public int MinDonateTier { get; set; } = 3;

        [SettingPropertyBool("Prioritize High Tier for Donation", RequireRestart = false, HintText = "Donate higher tier prisoners first to maximize influence return.")]
        [SettingPropertyGroup("3. Dungeon Donation", GroupOrder = 2)]
        public bool PrioritizeHighTier { get; set; } = true;

        // --- Discard Settings ---
        [SettingPropertyBool("Auto Discard Post-Battle Excess", RequireRestart = false, HintText = "Automatically dump/discard low tier prisoners post-battle if player party is over prisoner capacity.")]
        [SettingPropertyGroup("4. Post-Battle Discard", GroupOrder = 3)]
        public bool AutoDiscardPostBattle { get; set; } = false;

        [SettingPropertyInteger("Discard Up To Tier", 1, 6, RequireRestart = false, HintText = "Discard excess prisoners up to (and including) this tier.")]
        [SettingPropertyGroup("4. Post-Battle Discard", GroupOrder = 3)]
        public int DiscardUpToTier { get; set; } = 2;

        public KeepPolicy NobleKeepPolicySetting => NobleKeepPolicyDropdown.SelectedValue.Value;
        public KeepPolicy RegularKeepPolicySetting => RegularKeepPolicyDropdown.SelectedValue.Value;
        public BanditKeepPolicy BanditKeepPolicySetting => BanditKeepPolicyDropdown.SelectedValue.Value;
        public KeepEvalTime KeepEvalTimeSetting => KeepEvalTimeDropdown.SelectedValue.Value;
    }
}
