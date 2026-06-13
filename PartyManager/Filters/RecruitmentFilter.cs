using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace PartyManager.Filters
{
    public static class RecruitmentFilter
    {
        public static bool MatchTroopFilter(CharacterObject troop, Settings settings)
        {
            var leafTroops = new List<CharacterObject>();
            SettlementAutomationCore.Helpers.TroopHelper.GetLeafTroops(troop, leafTroops);

            string logPrefix = $"[Recruit Filter: {troop.Name} (Tier {troop.Tier})]";
            var logLines = new List<string>();

            // 1. Mercenary check at purchase time
            if (troop.Occupation == Occupation.Mercenary)
            {
                var policy = settings.MercenaryRecruitSetting;
                if (policy == MercenaryRecruitPolicy.None)
                {
                    logLines.Add("Rejected: Mercenary recruitment is disabled.");
                    WriteLog(logPrefix, false, logLines);
                    return false;
                }
                if (policy == MercenaryRecruitPolicy.AnyIgnoreTier)
                {
                    logLines.Add("Approved: Mercenary (AnyIgnoreTier).");
                    WriteLog(logPrefix, true, logLines);
                    return true;
                }
                if (policy == MercenaryRecruitPolicy.Any)
                {
                    bool tierOk = troop.Tier >= settings.MinRecruitTier && troop.Tier <= settings.MaxRecruitTier;
                    logLines.Add(tierOk ? "Approved: Mercenary matching tier limits." : $"Rejected: Mercenary tier {troop.Tier} not in range [{settings.MinRecruitTier}-{settings.MaxRecruitTier}].");
                    WriteLog(logPrefix, tierOk, logLines);
                    return tierOk;
                }
                // MatchFilters continues down to standard checks
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

                        // Check leaf mercenary
                        if (leaf.Occupation == Occupation.Mercenary)
                        {
                            var policy = settings.MercenaryRecruitSetting;
                            if (policy == MercenaryRecruitPolicy.None)
                            {
                                logLines.Add($"{leafInfo} failed: Mercenary recruitment disabled.");
                                continue;
                            }
                            if (policy == MercenaryRecruitPolicy.AnyIgnoreTier)
                            {
                                logLines.Add($"{leafInfo} matched: Mercenary (AnyIgnoreTier).");
                                leafMatched = true;
                                break;
                            }
                            if (policy == MercenaryRecruitPolicy.Any)
                            {
                                if (leaf.Tier >= settings.MinRecruitTier && leaf.Tier <= settings.MaxRecruitTier)
                                {
                                    logLines.Add($"{leafInfo} matched: Mercenary matching tier limits.");
                                    leafMatched = true;
                                    break;
                                }
                                logLines.Add($"{leafInfo} failed: Mercenary tier {leaf.Tier} not in range.");
                                continue;
                            }
                        }

                        // Check leaf noble
                        bool leafIsNoble = leaf.Tier >= 6;
                        if (leafIsNoble)
                        {
                            var policy = settings.NobleRecruitSetting;
                            if (policy == NobleRecruitPolicy.None)
                            {
                                logLines.Add($"{leafInfo} failed: Noble recruitment disabled.");
                                continue;
                            }
                            if (policy == NobleRecruitPolicy.AnyIgnoreTier)
                            {
                                if (IsCultureEnabled(leaf, settings))
                                {
                                    logLines.Add($"{leafInfo} matched: Noble (AnyIgnoreTier) with culture enabled.");
                                    leafMatched = true;
                                    break;
                                }
                                logLines.Add($"{leafInfo} failed: Noble (AnyIgnoreTier) but culture disabled.");
                                continue;
                            }
                            if (policy == NobleRecruitPolicy.Any)
                            {
                                if (IsCultureEnabled(leaf, settings) && leaf.Tier >= settings.MinRecruitTier && leaf.Tier <= settings.MaxRecruitTier)
                                {
                                    logLines.Add($"{leafInfo} matched: Noble (Any) with culture and tier enabled.");
                                    leafMatched = true;
                                    break;
                                }
                                logLines.Add($"{leafInfo} failed: Noble (Any) but culture or tier disabled.");
                                continue;
                            }

                            // MatchFilters
                            if (IsCultureEnabled(leaf, settings) &&
                                leaf.Tier >= settings.MinRecruitTier && leaf.Tier <= settings.MaxRecruitTier &&
                                IsRoleEnabled(leaf, settings))
                            {
                                logLines.Add($"{leafInfo} matched: Noble (MatchFilters) matches culture, tier, and role.");
                                leafMatched = true;
                                break;
                            }
                            logLines.Add($"{leafInfo} failed: Noble (MatchFilters) role/culture/tier mismatch.");
                        }
                        else
                        {
                            // Regular troop
                            if (!settings.RecruitRegularTroops)
                            {
                                logLines.Add($"{leafInfo} failed: Regular recruitment disabled.");
                                continue;
                            }
                            if (IsCultureEnabled(leaf, settings) &&
                                leaf.Tier >= settings.MinRecruitTier && leaf.Tier <= settings.MaxRecruitTier &&
                                IsRoleEnabled(leaf, settings))
                            {
                                logLines.Add($"{leafInfo} matched: Regular matches culture, tier, and role.");
                                leafMatched = true;
                                break;
                            }
                            logLines.Add($"{leafInfo} failed: Regular role/culture/tier mismatch.");
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

                if (isNoble)
                {
                    var policy = settings.NobleRecruitSetting;
                    if (policy == NobleRecruitPolicy.None)
                    {
                        logLines.Add("Failed: Noble recruitment disabled.");
                        isMatch = false;
                    }
                    else if (policy == NobleRecruitPolicy.AnyIgnoreTier)
                    {
                        isMatch = IsCultureEnabled(troop, settings);
                        logLines.Add(isMatch ? "Approved: Noble (AnyIgnoreTier) with culture enabled." : "Failed: Noble (AnyIgnoreTier) but culture disabled.");
                    }
                    else if (policy == NobleRecruitPolicy.Any)
                    {
                        isMatch = IsCultureEnabled(troop, settings) && troop.Tier >= settings.MinRecruitTier && troop.Tier <= settings.MaxRecruitTier;
                        logLines.Add(isMatch ? "Approved: Noble (Any) matching culture and tier." : "Failed: Noble (Any) culture or tier mismatch.");
                    }
                    else // MatchFilters
                    {
                        isMatch = IsCultureEnabled(troop, settings) &&
                                  troop.Tier >= settings.MinRecruitTier && troop.Tier <= settings.MaxRecruitTier &&
                                  IsRoleEnabled(troop, settings);
                        logLines.Add(isMatch ? "Approved: Noble (MatchFilters) matches all." : "Failed: Noble (MatchFilters) role/culture/tier mismatch.");
                    }
                }
                else
                {
                    // Regular troop
                    if (!settings.RecruitRegularTroops)
                    {
                        logLines.Add("Failed: Regular recruitment disabled.");
                        isMatch = false;
                    }
                    else
                    {
                        isMatch = IsCultureEnabled(troop, settings) &&
                                  troop.Tier >= settings.MinRecruitTier && troop.Tier <= settings.MaxRecruitTier &&
                                  IsRoleEnabled(troop, settings);
                        logLines.Add(isMatch ? "Approved: Regular matches all." : "Failed: Regular role/culture/tier mismatch.");
                    }
                }
            }

            WriteLog(logPrefix, isMatch, logLines);
            return isMatch;
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
