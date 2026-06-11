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

        // --- Recruitment settings ---
        [SettingPropertyBool("Auto-Recruit Volunteers", RequireRestart = false, HintText = "Enable auto-recruitment from town/village notables.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public bool AutoRecruitVolunteers { get; set; } = true;

        [SettingPropertyBool("Recruit Regular Troops", RequireRestart = false, HintText = "Recruit standard volunteer tree troops.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public bool RecruitRegular { get; set; } = true;

        [SettingPropertyBool("Recruit Noble Troops", RequireRestart = false, HintText = "Recruit elite/noble volunteer tree troops.")]
        [SettingPropertyGroup("Recruitment", GroupOrder = 0)]
        public bool RecruitNoble { get; set; } = true;

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
        [SettingPropertyBool("Auto-Buy Riding Mounts for Speed", RequireRestart = false, HintText = "Automatically buy mounts for foot soldiers to upgrade party speed.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public bool AutoBuyMounts { get; set; } = true;

        [SettingPropertyBool("Herding Penalty Protection", RequireRestart = false, HintText = "Automatically sell excess animals (livestock, pack, riding) to avoid herding speed penalty.")]
        [SettingPropertyGroup("Mounts & Herding", GroupOrder = 2)]
        public bool PreventHerdingPenalty { get; set; } = true;

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

        public EvalTime EvalTimeSetting => EvalTimeDropdown.SelectedValue.Value;
    }
}
