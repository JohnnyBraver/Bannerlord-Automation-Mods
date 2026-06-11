using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

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
        [SettingPropertyGroup("Ransom", GroupOrder = 0)]
        public bool AutoRansom { get; set; } = true;

        [SettingPropertyInteger("Min Tier to Ransom", 1, 6, RequireRestart = false, HintText = "Minimum tier of prisoner to automatically ransom.")]
        [SettingPropertyGroup("Ransom", GroupOrder = 0)]
        public int MinRansomTier { get; set; } = 1;

        [SettingPropertyBool("Keep Noble Prisoners", RequireRestart = false, HintText = "Do not auto-ransom noble/elite prisoners (useful for recruiting later).")]
        [SettingPropertyGroup("Ransom", GroupOrder = 0)]
        public bool KeepNobles { get; set; } = true;

        [SettingPropertyBool("Keep Hero Prisoners", RequireRestart = false, HintText = "Never automatically ransom hero/lord prisoners.")]
        [SettingPropertyGroup("Ransom", GroupOrder = 0)]
        public bool KeepHeroes { get; set; } = true;

        // --- Donation Settings ---
        [SettingPropertyBool("Auto-Donate Prisoners to Dungeon", RequireRestart = false, HintText = "Automatically donate prisoners to friendly town/castle dungeons to farm influence/XP.")]
        [SettingPropertyGroup("Donation", GroupOrder = 1)]
        public bool AutoDonate { get; set; } = false;

        [SettingPropertyInteger("Min Tier to Donate", 1, 6, RequireRestart = false, HintText = "Minimum tier of prisoner to donate.")]
        [SettingPropertyGroup("Donation", GroupOrder = 1)]
        public int MinDonateTier { get; set; } = 3;

        [SettingPropertyBool("Prioritize High Tier for Donation", RequireRestart = false, HintText = "Donate higher tier prisoners first to maximize influence return.")]
        [SettingPropertyGroup("Donation", GroupOrder = 1)]
        public bool PrioritizeHighTier { get; set; } = true;
    }
}
