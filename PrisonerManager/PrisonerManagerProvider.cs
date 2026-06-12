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
            // Noble / Regular Troop Class check
            var leafTroops = new List<CharacterObject>();
            SettlementAutomationCore.Helpers.TroopHelper.GetLeafTroops(prisoner, leafTroops);
            int maxLeafTier = leafTroops.Count > 0 ? leafTroops.Max(l => l.Tier) : prisoner.Tier;

            bool isNoble = maxLeafTier >= 6;
            if (isNoble && settings.KeepNobles) return true;
            if (!isNoble && !settings.KeepRegulars) return false;

            // Combat Mounted / Foot check
            bool isMounted = prisoner.IsMounted;
            if (isMounted && !settings.KeepMounted) return false;
            if (!isMounted && !settings.KeepFoot) return false;

            // Archetype check
            bool isCrossbow = prisoner.GetSkillValue(DefaultSkills.Crossbow) > 30;
            bool isBow = prisoner.GetSkillValue(DefaultSkills.Bow) > 30;
            bool isThrowing = prisoner.GetSkillValue(DefaultSkills.Throwing) > 30;
            bool isRanged = isCrossbow || isBow || isThrowing;

            if (isRanged && !settings.KeepRanged) return false;
            if (!isRanged && !settings.KeepMelee) return false;

            // Tier evaluation (recruit filters usually match range)
            if (prisoner.Tier < settings.KeepMinTier || prisoner.Tier > settings.KeepMaxTier) return false;

            return true;
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
