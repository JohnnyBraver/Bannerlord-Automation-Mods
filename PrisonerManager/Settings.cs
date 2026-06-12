using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace PrisonerManager
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "PrisonerManager_v1";
        public override string DisplayName => "Prisoner Manager";
        public override string FolderName => "PrisonerManager";
        public override string FormatType => "json";

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

        [SettingPropertyBool("Keep Noble Prisoners", RequireRestart = false, HintText = "Do not auto-ransom noble/elite prisoners (useful for recruiting later).")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public bool KeepNobles { get; set; } = true;

        [SettingPropertyBool("Keep Regular Troops", RequireRestart = false, HintText = "Keep regular troops (if matching filters below for recruiting).")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public bool KeepRegulars { get; set; } = false;

        [SettingPropertyBool("Keep Melee Archetype", RequireRestart = false, HintText = "Keep melee units (one-handed/two-handed/polearm) for recruiting.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public bool KeepMelee { get; set; } = true;

        [SettingPropertyBool("Keep Ranged Archetype", RequireRestart = false, HintText = "Keep ranged units (bow/crossbow/throwing) for recruiting.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public bool KeepRanged { get; set; } = true;

        [SettingPropertyBool("Keep Mounted Troops", RequireRestart = false, HintText = "Keep cavalry units for recruiting.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public bool KeepMounted { get; set; } = true;

        [SettingPropertyBool("Keep Foot Troops", RequireRestart = false, HintText = "Keep infantry units for recruiting.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public bool KeepFoot { get; set; } = true;

        [SettingPropertyInteger("Keep Min Tier", 1, 6, RequireRestart = false, HintText = "Minimum tier of troop to keep for recruitment.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public int KeepMinTier { get; set; } = 3;

        [SettingPropertyInteger("Keep Max Tier", 1, 6, RequireRestart = false, HintText = "Maximum tier of troop to keep for recruitment.")]
        [SettingPropertyGroup("2. Keep / Recruit Filters", GroupOrder = 1)]
        public int KeepMaxTier { get; set; } = 6;

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
    }
}
