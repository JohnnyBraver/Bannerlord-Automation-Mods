using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using SettlementAutomationCore;

namespace PrisonerManager
{
    public class PrisonerManagerProvider : IRansomOrderProvider, IDungeonOrderProvider
    {
        public string ProviderName => "PrisonerManager";

        // ----------------------------------------------------
        // IRansomOrderProvider
        // ----------------------------------------------------
        public List<RansomOrder> GetRansomOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<RansomOrder>();
            if (!settlement.IsTown) return orders;

            var settings = Settings.Instance;
            if (settings == null || !settings.AutoRansom) return orders;

            var prisonRoster = party.PrisonRoster;
            for (int i = 0; i < prisonRoster.Count; i++)
            {
                var el = prisonRoster.GetElementCopyAtIndex(i);
                var prisoner = el.Character;
                if (prisoner == null || el.Number <= 0) continue;

                // 1. Keep Heroes check
                if (prisoner.IsHero && settings.KeepHeroes) continue;

                // 2. Keep Recruit Candidate check
                if (MatchKeepFilter(prisoner, settings)) continue;

                // 3. Min Tier to ransom check
                if (prisoner.Tier < settings.MinRansomTier) continue;

                // Ransom all available amount of this prisoner type
                orders.Add(new RansomOrder(prisoner, el.Number));
            }

            if (orders.Count > 0)
            {
                var summary = string.Join(", ", orders.Select(o => $"{o.Amount}x {o.Prisoner.Name}"));
                SettlementAutomationCore.Helpers.Logger.WriteLog("PrisonerManager", $"Ransom Orders compiled for {settlement.Name}: {summary}");
            }

            return orders;
        }

        private bool MatchKeepFilter(CharacterObject prisoner, Settings settings)
        {
            var evalTroops = new List<CharacterObject>();
            if (settings.KeepEvalTimeSetting == KeepEvalTime.FinalUpgradeTier)
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
                bool needsArchetypeCheck = false;

                // Step 1: Check keeping policies by category
                if (isBandit)
                {
                    var policy = settings.BanditKeepPolicySetting;
                    if (policy == BanditKeepPolicy.SellAll) continue;
                    if (policy == BanditKeepPolicy.KeepNobleOnly && !evalTroopIsNoble) continue;
                    if (policy == BanditKeepPolicy.KeepSelected)
                    {
                        var noblePolicy = settings.NobleKeepPolicySetting;
                        var regularPolicy = settings.RegularKeepPolicySetting;
                        if (evalTroopIsNoble && noblePolicy == KeepPolicy.SellAll) continue;
                        if (!evalTroopIsNoble && regularPolicy == KeepPolicy.SellAll) continue;
                        
                        needsArchetypeCheck = true;
                    }
                }
                else
                {
                    if (evalTroopIsNoble)
                    {
                        var policy = settings.NobleKeepPolicySetting;
                        if (policy == KeepPolicy.SellAll) continue;
                        if (policy == KeepPolicy.KeepSelected) needsArchetypeCheck = true;
                    }
                    else
                    {
                        var policy = settings.RegularKeepPolicySetting;
                        if (policy == KeepPolicy.SellAll) continue;
                        if (policy == KeepPolicy.KeepSelected) needsArchetypeCheck = true;
                    }
                }

                // Step 2: Check archetype filters (if required by KeepSelected)
                if (needsArchetypeCheck)
                {
                    bool isMounted = evalTroop.IsMounted;
                    if (isMounted && !settings.KeepMounted) continue;
                    if (!isMounted && !settings.KeepFoot) continue;

                    bool isCrossbow = evalTroop.GetSkillValue(DefaultSkills.Crossbow) > 30;
                    bool isBow = evalTroop.GetSkillValue(DefaultSkills.Bow) > 30;
                    bool isThrowing = evalTroop.GetSkillValue(DefaultSkills.Throwing) > 30;
                    bool isRanged = isCrossbow || isBow || isThrowing;

                    if (isRanged && !settings.KeepRanged) continue;
                    if (!isRanged && !settings.KeepMelee) continue;
                }

                // Step 3: Check tier limits
                if (!(evalTroopIsNoble && settings.BypassNobleTierLimit))
                {
                    if (prisoner.Tier < settings.MinTierToKeep) continue;
                }

                // If any evaluated path satisfies the filters, keep this prisoner
                return true;
            }

            return false;
        }

        public List<MercenaryRecruitOrder> GetMercenaryRecruitOrders(MobileParty party, Settlement settlement)
        {
            // Mercenary recruitment is handled by PartyManager
            return new List<MercenaryRecruitOrder>();
        }

        // ----------------------------------------------------
        // IDungeonOrderProvider
        // ----------------------------------------------------
        public List<DungeonOrder> GetDungeonOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<DungeonOrder>();
            if (!settlement.IsTown && !settlement.IsCastle) return orders;

            var settings = Settings.Instance;
            if (settings == null || !settings.AutoDonate) return orders;

            // Only donate to friendly settlement dungeons
            if (settlement.OwnerClan == null || settlement.MapFaction != party.MapFaction)
            {
                return orders;
            }

            var prisonRoster = party.PrisonRoster;
            var candidates = new List<PrisonerInfo>();

            for (int i = 0; i < prisonRoster.Count; i++)
            {
                var el = prisonRoster.GetElementCopyAtIndex(i);
                var prisoner = el.Character;
                if (prisoner == null || el.Number <= 0 || prisoner.IsHero) continue; // Never auto-donate heroes

                if (prisoner.Tier >= settings.MinDonateTier)
                {
                    candidates.Add(new PrisonerInfo(prisoner, el.Number));
                }
            }

            // Sort candidates: high-tier first or standard
            if (settings.PrioritizeHighTier)
            {
                candidates = candidates.OrderByDescending(c => c.Prisoner.Tier).ToList();
            }
            else
            {
                candidates = candidates.OrderBy(c => c.Prisoner.Tier).ToList();
            }

            foreach (var candidate in candidates)
            {
                orders.Add(new DungeonOrder(candidate.Prisoner, candidate.Amount));
            }

            if (orders.Count > 0)
            {
                var summary = string.Join(", ", orders.Select(o => $"{o.Amount}x {o.Prisoner.Name}"));
                SettlementAutomationCore.Helpers.Logger.WriteLog("PrisonerManager", $"Dungeon Donation Orders compiled for {settlement.Name}: {summary}");
            }

            return orders;
        }


        private class PrisonerInfo
        {
            public CharacterObject Prisoner { get; }
            public int Amount { get; }
            public PrisonerInfo(CharacterObject prisoner, int amount)
            {
                Prisoner = prisoner;
                Amount = amount;
            }
        }
    }
}
