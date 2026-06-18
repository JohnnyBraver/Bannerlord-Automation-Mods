using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace PartyManager.Filters
{
    public static class RecruitmentFilter
    {
        [System.Flags]
        internal enum RecruitmentRole
        {
            None = 0,
            FrontlineInfantry = 1 << 0,
            ShockInfantry = 1 << 1,
            Skirmisher = 1 << 2,
            FootArcher = 1 << 3,
            Crossbowman = 1 << 4,
            MeleeCavalry = 1 << 5,
            HorseArcher = 1 << 6,
            PikeInfantry = 1 << 7
        }

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

        private static IEnumerable<Equipment> GetBattleEquipments(CharacterObject troop)
        {
            bool yieldedAny = false;
            if (troop.BattleEquipments != null)
            {
                foreach (var equipment in troop.BattleEquipments)
                {
                    if (equipment != null)
                    {
                        yieldedAny = true;
                        yield return equipment;
                    }
                }
            }

            if (!yieldedAny && troop.Equipment != null)
            {
                yield return troop.Equipment;
            }
        }

        private static bool AppearsInMostLoadouts(CharacterObject troop, System.Func<Equipment, bool> predicate)
        {
            int loadoutCount = 0;
            int matchedCount = 0;

            foreach (var equipment in GetBattleEquipments(troop))
            {
                loadoutCount++;
                if (predicate(equipment))
                {
                    matchedCount++;
                }
            }

            return loadoutCount > 0 && matchedCount > loadoutCount / 2;
        }

        private static bool HasWeaponOfClass(Equipment equipment, params WeaponClass[] classes)
        {
            for (int i = 0; i < 4; i++)
            {
                var element = equipment[i];
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

        private static bool HasWeaponOfClassInMostLoadouts(CharacterObject troop, params WeaponClass[] classes)
        {
            return AppearsInMostLoadouts(troop, equipment => HasWeaponOfClass(equipment, classes));
        }

        private static bool HasJavelin(Equipment equipment)
        {
            for (int i = 0; i < 4; i++)
            {
                var item = equipment[i].Item;
                if (item == null)
                {
                    continue;
                }

                var weapon = item.PrimaryWeapon;
                if (weapon == null)
                {
                    continue;
                }

                string itemId = item.StringId?.ToLowerInvariant() ?? string.Empty;
                if (weapon.WeaponClass == WeaponClass.Javelin ||
                    itemId.Contains("javelin"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasJavelinInMostLoadouts(CharacterObject troop)
        {
            return AppearsInMostLoadouts(troop, HasJavelin);
        }

        private static bool HasPike(CharacterObject troop)
        {
            return AppearsInMostLoadouts(troop, HasPike);
        }

        private static bool HasPike(Equipment equipment)
        {
            for (int i = 0; i < 4; i++)
            {
                var item = equipment[i].Item;
                if (item == null)
                {
                    continue;
                }

                string itemId = item.StringId?.ToLowerInvariant() ?? string.Empty;
                string itemName = item.Name?.ToString().ToLowerInvariant() ?? string.Empty;
                string weaponClass = item.PrimaryWeapon?.WeaponClass.ToString() ?? string.Empty;

                if (weaponClass == "Pike" ||
                    itemId.Contains("pike") ||
                    itemName.Contains("pike"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLargeSwingable(CharacterObject troop)
        {
            return AppearsInMostLoadouts(troop, HasLargeSwingable);
        }

        private static bool HasLargeSwingable(Equipment equipment)
        {
            for (int i = 0; i < 4; i++)
            {
                var item = equipment[i].Item;
                if (item == null)
                {
                    continue;
                }

                var weapon = item.PrimaryWeapon;
                if (weapon == null)
                {
                    continue;
                }

                switch (weapon.WeaponClass)
                {
                    case WeaponClass.TwoHandedSword:
                    case WeaponClass.TwoHandedAxe:
                    case WeaponClass.TwoHandedMace:
                        return true;
                    case WeaponClass.TwoHandedPolearm:
                    case WeaponClass.LowGripPolearm:
                        if (weapon.SwingDamage > 0)
                        {
                            return true;
                        }
                        break;
                }
            }

            return false;
        }

        private static bool HasShield(Equipment equipment)
        {
            for (int i = 0; i < 4; i++)
            {
                var element = equipment[i];
                if (element.Item != null && element.Item.PrimaryWeapon != null && element.Item.PrimaryWeapon.IsShield)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasShieldInMostLoadouts(CharacterObject troop)
        {
            return AppearsInMostLoadouts(troop, HasShield);
        }

        private static bool IsRoleEnabled(CharacterObject troop, Settings settings)
        {
            RecruitmentRole role = ClassifyRole(
                troop.IsMounted,
                HasWeaponOfClassInMostLoadouts(troop, WeaponClass.Bow),
                HasWeaponOfClassInMostLoadouts(troop, WeaponClass.Crossbow),
                HasJavelinInMostLoadouts(troop),
                HasShieldInMostLoadouts(troop),
                HasPike(troop),
                HasLargeSwingable(troop));

            return IsAnyRoleEnabled(role, settings);
        }

        internal static RecruitmentRole ClassifyRole(
            bool isMounted,
            bool hasBow,
            bool hasCrossbow,
            bool hasJavelin,
            bool hasShield,
            bool hasPike,
            bool hasLargeSwingable)
        {
            if (isMounted)
            {
                return hasBow || hasCrossbow
                    ? RecruitmentRole.HorseArcher
                    : RecruitmentRole.MeleeCavalry;
            }

            if (hasBow)
            {
                return RecruitmentRole.FootArcher;
            }

            if (hasCrossbow)
            {
                return RecruitmentRole.Crossbowman;
            }

            if (hasPike)
            {
                return RecruitmentRole.PikeInfantry;
            }

            if (hasShield)
            {
                return RecruitmentRole.FrontlineInfantry;
            }

            if (hasLargeSwingable)
            {
                return RecruitmentRole.ShockInfantry;
            }

            if (hasJavelin)
            {
                return RecruitmentRole.Skirmisher;
            }

            return RecruitmentRole.FrontlineInfantry;
        }

        internal static bool IsAnyRoleEnabled(RecruitmentRole roles, Settings settings)
        {
            return
                roles.HasFlag(RecruitmentRole.FrontlineInfantry) && settings.RecruitShieldInfantry ||
                roles.HasFlag(RecruitmentRole.ShockInfantry) && settings.RecruitShockInfantry ||
                roles.HasFlag(RecruitmentRole.Skirmisher) && settings.RecruitSkirmishers ||
                roles.HasFlag(RecruitmentRole.FootArcher) && settings.RecruitFootArchers ||
                roles.HasFlag(RecruitmentRole.Crossbowman) && settings.RecruitCrossbowmen ||
                roles.HasFlag(RecruitmentRole.MeleeCavalry) && settings.RecruitMeleeCavalry ||
                roles.HasFlag(RecruitmentRole.HorseArcher) && settings.RecruitHorseArchers ||
                roles.HasFlag(RecruitmentRole.PikeInfantry) && settings.RecruitPikeInfantry;
        }

        private static void WriteLog(string prefix, bool result, List<string> lines)
        {
            SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", $"{prefix} Result: {result}. Details: {string.Join(" | ", lines)}");
        }
    }
}
