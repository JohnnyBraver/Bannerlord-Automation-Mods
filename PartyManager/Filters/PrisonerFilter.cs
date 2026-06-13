using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace PartyManager.Filters
{
    public static class PrisonerFilter
    {
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

            foreach (var evalTroop in evalTroops)
            {
                bool evalTroopIsNoble = evalTroop.Tier >= 6;
                bool needsRecruitFilterCheck = false;

                // Check keeping policies by category
                if (isBandit)
                {
                    var policy = settings.BanditPrisonerKeepPolicySetting;
                    if (policy == BanditPrisonerKeepPolicy.RansomAll) continue;
                    if (policy == BanditPrisonerKeepPolicy.KeepNobleOnly && !evalTroopIsNoble) continue;
                    if (policy == BanditPrisonerKeepPolicy.KeepAll)
                    {
                        // Proceed to tier checks
                    }
                    else if (policy == BanditPrisonerKeepPolicy.KeepSelected)
                    {
                        needsRecruitFilterCheck = true;
                    }
                }
                else
                {
                    if (evalTroopIsNoble)
                    {
                        var policy = settings.NoblePrisonerKeepPolicySetting;
                        if (policy == PrisonerKeepPolicy.RansomAll) continue;
                        if (policy == PrisonerKeepPolicy.KeepAll)
                        {
                            // Proceed to tier checks
                        }
                        else if (policy == PrisonerKeepPolicy.KeepSelected)
                        {
                            needsRecruitFilterCheck = true;
                        }
                    }
                    else
                    {
                        var policy = settings.RegularPrisonerKeepPolicySetting;
                        if (policy == PrisonerKeepPolicy.RansomAll) continue;
                        if (policy == PrisonerKeepPolicy.KeepAll)
                        {
                            // Proceed to tier checks
                        }
                        else if (policy == PrisonerKeepPolicy.KeepSelected)
                        {
                            needsRecruitFilterCheck = true;
                        }
                    }
                }

                // Check recruitment filter if policy is KeepSelected
                if (needsRecruitFilterCheck)
                {
                    if (!RecruitmentFilter.MatchTroopFilter(evalTroop, settings)) continue;
                }

                // Check tier limits
                if (!(evalTroopIsNoble && settings.BypassNoblePrisonerTierLimit))
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
    }
}
