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
            LightInfantry = 1 << 0,
            ShieldInfantry = 1 << 1,
            ShockInfantry = 1 << 2,
            Skirmisher = 1 << 3,
            FootArcher = 1 << 4,
            Crossbowman = 1 << 5,
            MeleeCavalry = 1 << 6,
            HorseArcher = 1 << 7,
            PikeInfantry = 1 << 8,
            SpearInfantry = 1 << 9,
            MountedSkirmisher = 1 << 10
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

        private static bool IsRoleEnabled(CharacterObject troop, Settings settings)
        {
            var troopRole = TroopClassifier.TroopRoleClassifier.Classify(troop);
            RecruitmentRole role = MapTroopRole(troopRole);
            return IsAnyRoleEnabled(role, settings);
        }

        internal static RecruitmentRole MapTroopRole(TroopClassifier.TroopRole troopRole)
        {
            switch (troopRole)
            {
                case TroopClassifier.TroopRole.LightInfantry: return RecruitmentRole.LightInfantry;
                case TroopClassifier.TroopRole.ShieldInfantry: return RecruitmentRole.ShieldInfantry;
                case TroopClassifier.TroopRole.ShockInfantry: return RecruitmentRole.ShockInfantry;
                case TroopClassifier.TroopRole.Skirmisher: return RecruitmentRole.Skirmisher;
                case TroopClassifier.TroopRole.FootArcher: return RecruitmentRole.FootArcher;
                case TroopClassifier.TroopRole.Crossbowman: return RecruitmentRole.Crossbowman;
                case TroopClassifier.TroopRole.MeleeCavalry: return RecruitmentRole.MeleeCavalry;
                case TroopClassifier.TroopRole.HorseArcher: return RecruitmentRole.HorseArcher;
                case TroopClassifier.TroopRole.PikeInfantry: return RecruitmentRole.PikeInfantry;
                case TroopClassifier.TroopRole.SpearInfantry: return RecruitmentRole.SpearInfantry;
                case TroopClassifier.TroopRole.MountedSkirmisher: return RecruitmentRole.MountedSkirmisher;
                default: return RecruitmentRole.None;
            }
        }

        internal static bool IsAnyRoleEnabled(RecruitmentRole roles, Settings settings)
        {
            return
                roles.HasFlag(RecruitmentRole.LightInfantry) && settings.RecruitLightInfantry ||
                roles.HasFlag(RecruitmentRole.ShieldInfantry) && settings.RecruitShieldInfantry ||
                roles.HasFlag(RecruitmentRole.ShockInfantry) && settings.RecruitShockInfantry ||
                roles.HasFlag(RecruitmentRole.Skirmisher) && settings.RecruitSkirmishers ||
                roles.HasFlag(RecruitmentRole.FootArcher) && settings.RecruitFootArchers ||
                roles.HasFlag(RecruitmentRole.Crossbowman) && settings.RecruitCrossbowmen ||
                roles.HasFlag(RecruitmentRole.MeleeCavalry) && settings.RecruitMeleeCavalry ||
                roles.HasFlag(RecruitmentRole.HorseArcher) && settings.RecruitHorseArchers ||
                roles.HasFlag(RecruitmentRole.PikeInfantry) && settings.RecruitPikeInfantry ||
                roles.HasFlag(RecruitmentRole.SpearInfantry) && settings.RecruitSpearInfantry ||
                roles.HasFlag(RecruitmentRole.MountedSkirmisher) && settings.RecruitMountedSkirmisher;
        }

        private static void WriteLog(string prefix, bool result, List<string> lines)
        {
            SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", $"{prefix} Result: {result}. Details: {string.Join(" | ", lines)}");
        }
    }
}
