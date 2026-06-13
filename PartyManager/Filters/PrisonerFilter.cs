using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace PartyManager.Filters
{
    public static class PrisonerFilter
    {
        private enum UnifiedKeepPolicy
        {
            RansomAll,
            KeepAll,
            KeepNobleOnly,
            KeepSelected
        }

        public static bool MatchKeepFilter(CharacterObject prisoner, Settings settings)
        {
            var evalTroops = new List<CharacterObject>();
            if (settings.EvalTimeSetting == EvalTime.FinalUpgradeTier)
            {
                SettlementAutomationCore.Helpers.TroopHelper.GetLeafTroops(prisoner, evalTroops);
            }
            else
            {
                evalTroops.Add(prisoner);
            }

            if (evalTroops.Count == 0)
            {
                evalTroops.Add(prisoner);
            }

            bool isBandit = prisoner.Culture != null && prisoner.Culture.IsBandit;
            bool isMinorFaction = !isBandit && prisoner.Culture != null && !IsMajorCulture(prisoner.Culture);

            foreach (var evalTroop in evalTroops)
            {
                bool evalTroopIsNoble = evalTroop.Tier >= 6;
                UnifiedKeepPolicy policy;

                // Determine policy based on troop classification
                if (isBandit)
                {
                    policy = (UnifiedKeepPolicy)settings.BanditPrisonerKeepPolicySetting;
                }
                else if (evalTroopIsNoble)
                {
                    policy = (UnifiedKeepPolicy)settings.NoblePrisonerKeepPolicySetting;
                }
                else if (isMinorFaction)
                {
                    policy = (UnifiedKeepPolicy)settings.OtherPrisonerKeepPolicySetting;
                }
                else
                {
                    policy = (UnifiedKeepPolicy)settings.RegularPrisonerKeepPolicySetting;
                }

                if (policy == UnifiedKeepPolicy.RansomAll) continue;
                if (policy == UnifiedKeepPolicy.KeepNobleOnly && !evalTroopIsNoble) continue;

                // Check recruitment filter if policy is KeepSelected
                if (policy == UnifiedKeepPolicy.KeepSelected)
                {
                    // Pass ignoreRecruitPolicy = true if the respective bypass check is enabled in settings
                    bool ignoreRecruitPolicy = false;
                    if (evalTroop.Occupation == Occupation.Mercenary)
                    {
                        ignoreRecruitPolicy = settings.BypassMercenaryRecruitPolicy;
                    }
                    else if (evalTroopIsNoble)
                    {
                        ignoreRecruitPolicy = settings.BypassNobleRecruitPolicy;
                    }
                    else
                    {
                        ignoreRecruitPolicy = settings.BypassRegularRecruitPolicy;
                    }

                    if (!RecruitmentFilter.MatchTroopFilter(evalTroop, settings, ignoreRecruitPolicy))
                    {
                        continue;
                    }
                }

                // Check tier limits
                bool bypassTierLimit = false;
                if (evalTroopIsNoble && settings.BypassNoblePrisonerTierLimit)
                {
                    bypassTierLimit = true;
                }
                else if (isMinorFaction && settings.BypassOtherPrisonerTierLimit)
                {
                    bypassTierLimit = true;
                }

                if (!bypassTierLimit)
                {
                    if (settings.UsePerkBasedPrisonerKeep && Hero.MainHero != null)
                    {
                        bool stoutDefender = Hero.MainHero.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Leadership.StoutDefender);
                        bool ferventAttacker = Hero.MainHero.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Leadership.FerventAttacker);

                        if (stoutDefender && ferventAttacker)
                        {
                            // Keep all tiers if both are active (e.g. cheats/mods)
                        }
                        else if (stoutDefender)
                        {
                            if (prisoner.Tier < 4) continue;
                        }
                        else if (ferventAttacker)
                        {
                            if (prisoner.Tier > 3) continue;
                        }
                        else
                        {
                            if (prisoner.Tier < settings.MinPrisonerTierToKeep) continue;
                        }
                    }
                    else
                    {
                        if (prisoner.Tier < settings.MinPrisonerTierToKeep) continue;
                    }
                }

                // If any path matches the filters, keep this prisoner type
                return true;
            }

            return false;
        }

        private static bool IsMajorCulture(CultureObject culture)
        {
            if (culture == null) return false;
            string cid = culture.StringId?.ToLowerInvariant() ?? "";
            return cid == "empire" || cid == "vlandia" || cid == "battania" || cid == "sturgia" || cid == "khuzait" || cid == "aserai";
        }
    }
}
