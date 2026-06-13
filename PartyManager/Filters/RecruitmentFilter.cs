using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace PartyManager.Filters
{
    public static class RecruitmentFilter
    {
        private enum UnifiedPolicy
        {
            None,
            MatchFilters,
            Any,
            AnyIgnoreTier
        }

        public static bool MatchTroopFilter(CharacterObject troop, Settings settings, bool ignoreRecruitPolicy = false)
        {
            var leafTroops = new List<CharacterObject>();
            SettlementAutomationCore.Helpers.TroopHelper.GetLeafTroops(troop, leafTroops);

            string logPrefix = $"[Recruit Filter: {troop.Name} (Tier {troop.Tier})]";
            var logLines = new List<string>();

            // 1. Mercenary check at purchase time (early exit if disabled and not bypassed)
            if (troop.Occupation == Occupation.Mercenary && !ignoreRecruitPolicy)
            {
                var policy = settings.MercenaryRecruitSetting;
                if (policy == MercenaryRecruitPolicy.None)
                {
                    logLines.Add("Rejected: Mercenary recruitment is disabled.");
                    WriteLog(logPrefix, false, logLines);
                    return false;
                }
            }

            bool isMatch = false;

            if (settings.EvalTimeSetting == EvalTime.FinalUpgradeTier)
            {
                if (leafTroops.Count == 0)
                {
                    logLines.Add("Failed: No upgrade leaf troops found.");
                    isMatch = false;
                }
                else
                {
                    bool leafMatched = false;
                    foreach (var leaf in leafTroops)
                    {
                        string leafInfo = $"Leaf {leaf.Name} (Tier {leaf.Tier})";
                        bool leafIsNoble = leaf.Tier >= 6;
                        if (EvaluateTroop(leaf, settings, logLines, leafInfo, ignoreRecruitPolicy, leafIsNoble))
                        {
                            leafMatched = true;
                            break;
                        }
                    }
                    isMatch = leafMatched;
                }
            }
            else
            {
                // Purchase time evaluation
                int maxLeafTier = leafTroops.Count > 0 ? leafTroops.Max(l => l.Tier) : troop.Tier;
                bool isNoble = maxLeafTier >= 6;
                isMatch = EvaluateTroop(troop, settings, logLines, "Purchase-time Troop", ignoreRecruitPolicy, isNoble);
            }

            WriteLog(logPrefix, isMatch, logLines);
            return isMatch;
        }

        private static bool EvaluateTroop(CharacterObject troop, Settings settings, List<string> logLines, string label, bool ignoreRecruitPolicy, bool isNoble)
        {
            UnifiedPolicy policy;
            string typeLabel;

            if (troop.Occupation == Occupation.Mercenary)
            {
                typeLabel = "Mercenary";
                if (ignoreRecruitPolicy && settings.BypassMercenaryRecruitPolicy && settings.MercenaryRecruitSetting == MercenaryRecruitPolicy.None)
                {
                    policy = UnifiedPolicy.MatchFilters;
                }
                else
                {
                    policy = (UnifiedPolicy)settings.MercenaryRecruitSetting;
                }
            }
            else if (isNoble)
            {
                typeLabel = "Noble";
                if (ignoreRecruitPolicy && settings.BypassNobleRecruitPolicy && settings.NobleRecruitSetting == NobleRecruitPolicy.None)
                {
                    policy = UnifiedPolicy.MatchFilters;
                }
                else
                {
                    policy = (UnifiedPolicy)settings.NobleRecruitSetting;
                }
            }
            else
            {
                typeLabel = "Regular";
                if (ignoreRecruitPolicy && settings.BypassRegularRecruitPolicy && settings.RegularRecruitSetting == RegularRecruitPolicy.None)
                {
                    policy = UnifiedPolicy.MatchFilters;
                }
                else
                {
                    policy = (UnifiedPolicy)settings.RegularRecruitSetting;
                }
            }

            string fullLabel = $"{label} ({typeLabel})";

            if (policy == UnifiedPolicy.None)
            {
                logLines.Add($"{fullLabel} failed: recruitment disabled.");
                return false;
            }

            if (policy == UnifiedPolicy.AnyIgnoreTier)
            {
                bool cultureOk = IsCultureEnabled(troop, settings);
                logLines.Add(cultureOk ? $"{fullLabel} matched: culture enabled." : $"{fullLabel} failed: culture disabled.");
                return cultureOk;
            }

            bool cultureAndTierOk = IsCultureEnabled(troop, settings) &&
                                    troop.Tier >= settings.MinRecruitTier &&
                                    troop.Tier <= settings.MaxRecruitTier;

            if (policy == UnifiedPolicy.Any)
            {
                logLines.Add(cultureAndTierOk ? $"{fullLabel} matched: culture and tier limits met." : $"{fullLabel} failed: tier/culture mismatch.");
                return cultureAndTierOk;
            }

            // UnifiedPolicy.MatchFilters
            bool allOk = cultureAndTierOk && IsRoleEnabled(troop, settings);
            logLines.Add(allOk ? $"{fullLabel} matched: all filters met." : $"{fullLabel} failed: role/tier/culture mismatch.");
            return allOk;
        }

        private static bool IsCultureEnabled(CharacterObject troop, Settings settings)
        {
            if (troop.Culture == null) return settings.RecruitOtherCultures;

            string cultureId = troop.Culture.StringId?.ToLowerInvariant() ?? "";
            switch (cultureId)
            {
                case "empire":
                    return settings.RecruitEmpire;
                case "vlandia":
                    return settings.RecruitVlandia;
                case "battania":
                    return settings.RecruitBattania;
                case "sturgia":
                    return settings.RecruitSturgia;
                case "khuzait":
                    return settings.RecruitKhuzait;
                case "aserai":
                    return settings.RecruitAserai;
                default:
                    return settings.RecruitOtherCultures;
            }
        }

        private static bool HasWeaponOfClass(CharacterObject troop, params WeaponClass[] classes)
        {
            if (troop.Equipment == null) return false;
            for (int i = 0; i < 4; i++)
            {
                var element = troop.Equipment[i];
                if (element.Item != null && element.Item.PrimaryWeapon != null)
                {
                    if (classes.Contains(element.Item.PrimaryWeapon.WeaponClass))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool HasShield(CharacterObject troop)
        {
            if (troop.Equipment == null) return false;
            for (int i = 0; i < 4; i++)
            {
                var element = troop.Equipment[i];
                if (element.Item != null && element.Item.PrimaryWeapon != null && element.Item.PrimaryWeapon.IsShield)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsRoleEnabled(CharacterObject troop, Settings settings)
        {
            bool isMounted = troop.IsMounted;

            if (isMounted)
            {
                // Horse Archer check: has bow or crossbow or throwing
                bool hasMountedRanged = HasWeaponOfClass(troop, WeaponClass.Bow, WeaponClass.Crossbow, WeaponClass.ThrowingAxe, WeaponClass.ThrowingKnife, WeaponClass.Javelin);
                if (hasMountedRanged)
                {
                    return settings.RecruitHorseArchers;
                }
                return settings.RecruitMeleeCavalry;
            }
            else
            {
                // Foot soldier
                // Archers
                if (HasWeaponOfClass(troop, WeaponClass.Bow))
                {
                    return settings.RecruitFootArchers;
                }
                // Crossbowmen
                if (HasWeaponOfClass(troop, WeaponClass.Crossbow))
                {
                    return settings.RecruitCrossbowmen;
                }
                // Skirmishers (throwing)
                if (HasWeaponOfClass(troop, WeaponClass.ThrowingAxe, WeaponClass.ThrowingKnife, WeaponClass.Javelin))
                {
                    return settings.RecruitSkirmishers;
                }

                // Melee Infantry
                bool hasShield = HasShield(troop);
                bool hasTwoHandedOrPolearm = HasWeaponOfClass(troop,
                    WeaponClass.TwoHandedSword,
                    WeaponClass.TwoHandedAxe,
                    WeaponClass.TwoHandedMace,
                    WeaponClass.OneHandedPolearm,
                    WeaponClass.TwoHandedPolearm,
                    WeaponClass.LowGripPolearm);

                if (!hasShield && hasTwoHandedOrPolearm)
                {
                    return settings.RecruitShockInfantry;
                }

                return settings.RecruitShieldInfantry;
            }
        }

        private static void WriteLog(string prefix, bool result, List<string> lines)
        {
            SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", $"{prefix} Result: {result}. Details: {string.Join(" | ", lines)}");
        }
    }
}
