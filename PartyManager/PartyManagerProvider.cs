using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using SettlementAutomationCore;
using PartyManager.Filters;
using PartyManager.Helpers;

namespace PartyManager
{
    public class PartyManagerProvider : IPreSellProvider, IRecruitOrderProvider, IGarrisonOrderProvider, IRansomOrderProvider, IDungeonOrderProvider, IAutomationRequestProvider
    {
        public string ProviderName => "PartyManager";

        // ----------------------------------------------------
        // IPreSellProvider (Pack Animal Pre-Sell for XP farm split transactions)
        // ----------------------------------------------------
        public List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement)
        {
            var settings = Settings.Instance;
            if (settings == null) return new List<TradeOrder>();
            return TradeHelper.GetPreSellOrders(party, settings);
        }

        // ----------------------------------------------------
        // IAutomationRequestProvider
        // ----------------------------------------------------
        public void SubmitAutomationRequests(MobileParty party, Settlement settlement)
        {
            var settings = Settings.Instance;
            if (settings != null)
            {
                TradeHelper.SubmitAutomationRequests(party, settings);
            }
        }



        // ----------------------------------------------------
        // IRecruitOrderProvider (Recruitment Filter System)
        // ----------------------------------------------------
        public List<RecruitOrder> GetRecruitOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<RecruitOrder>();
            var settings = Settings.Instance;
            if (settings == null || !settings.AutoRecruitVolunteers) return orders;

            // Stop recruiting if over capacity (unless garrison donation is active and garrison size limit is not hit)
            int currentSize = party.MemberRoster.TotalManCount;
            int limit = party.Party.PartySizeLimit;

            bool canOverRecruit = settings.EnableGarrisonDonation && settlement.Town != null &&
                                  settlement.Town.GarrisonParty != null &&
                                  settlement.Town.GarrisonParty.MemberRoster.TotalManCount < settings.MaxGarrisonSize;

            if (currentSize >= limit && !canOverRecruit) return orders;

            // Cycle through settlement notables
            foreach (var notable in settlement.Notables)
            {
                if (notable == null || notable.VolunteerTypes == null) continue;

                // Check relation level and recruit slots unlocked
                int maxIndex = Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(Hero.MainHero, notable, -101);
                
                for (int slot = 0; slot < notable.VolunteerTypes.Length; slot++)
                {
                    var troop = notable.VolunteerTypes[slot];
                    if (troop == null) continue;

                    if (slot > maxIndex)
                    {
                        SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", 
                            $"[Recruit Slot Locked] Slot {slot} for notable {notable.Name} is locked. Relation allows recruiting up to index {maxIndex} (unlocked slots: {maxIndex + 1}). Troop: {troop.Name} (Tier {troop.Tier})");
                        continue;
                    }

                    if (RecruitmentFilter.MatchTroopFilter(troop, settings))
                    {
                        orders.Add(new RecruitOrder(notable, slot));
                    }
                }
            }

            return orders;
        }




        // ----------------------------------------------------
        // IGarrisonOrderProvider (Garrison Influence Donation)
        // ----------------------------------------------------
        public List<GarrisonOrder> GetGarrisonOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<GarrisonOrder>();
            var settings = Settings.Instance;
            if (settings == null || !settings.EnableGarrisonDonation || settlement.Town == null || settlement.Town.GarrisonParty == null)
            {
                return orders;
            }

            // Verify garrison size limit has not been hit
            int garrisonSize = settlement.Town.GarrisonParty.MemberRoster.TotalManCount;
            if (garrisonSize >= settings.MaxGarrisonSize) return orders;

            int partySize = party.MemberRoster.TotalManCount;
            int limit = party.Party.PartySizeLimit;

            // If we are over size limit, donate troops to friendly keeps/garrison
            if (partySize > limit)
            {
                int excessCount = partySize - limit;

                // Find non-hero recruits to donate. Prioritize lowest tiers, or matching Garrison minimum tier.
                var candidates = new List<TroopRosterElementInfo>();
                var memberRoster = party.MemberRoster;
                for (int i = 0; i < memberRoster.Count; i++)
                {
                    var el = memberRoster.GetElementCopyAtIndex(i);
                    if (el.Character != null && !el.Character.IsHero && el.Character.Tier >= settings.MinDonationTier)
                    {
                        candidates.Add(new TroopRosterElementInfo(el.Character, el.Number));
                    }
                }

                // Sort by tier ascending (donate lowest tier recruits first)
                candidates = candidates.OrderBy(c => c.Character.Tier).ToList();

                foreach (var candidate in candidates)
                {
                    if (excessCount <= 0 || garrisonSize >= settings.MaxGarrisonSize) break;
                    int toDonate = Math.Min(Math.Min(excessCount, candidate.Amount), settings.MaxGarrisonSize - garrisonSize);
                    if (toDonate > 0)
                    {
                        orders.Add(new GarrisonOrder(candidate.Character, toDonate));
                        excessCount -= toDonate;
                        garrisonSize += toDonate;
                    }
                }
            }

            return orders;
        }

        private class TroopRosterElementInfo
        {
            public CharacterObject Character { get; }
            public int Amount { get; }
            public TroopRosterElementInfo(CharacterObject ch, int amount)
            {
                Character = ch;
                Amount = amount;
            }
        }

        // ----------------------------------------------------
        // IRansomOrderProvider (Prisoner Ransoms & Tavern Mercenaries)
        // ----------------------------------------------------
        public List<RansomOrder> GetRansomOrders(MobileParty party, Settlement settlement)
        {
            var settings = Settings.Instance;
            if (settings == null) return new List<RansomOrder>();
            return PrisonerHelper.GetRansomOrders(party, settlement, settings);
        }



        // ----------------------------------------------------
        // IDungeonOrderProvider
        // ----------------------------------------------------
        public List<DungeonOrder> GetDungeonOrders(MobileParty party, Settlement settlement)
        {
            var settings = Settings.Instance;
            if (settings == null) return new List<DungeonOrder>();
            return PrisonerHelper.GetDungeonOrders(party, settlement, settings);
        }

        public List<MercenaryRecruitOrder> GetMercenaryRecruitOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<MercenaryRecruitOrder>();
            var settings = Settings.Instance;
            if (settings == null || settings.MercenaryRecruitSetting == MercenaryRecruitPolicy.None || settlement.Town == null) return orders;

            int currentSize = party.MemberRoster.TotalManCount;
            int limit = party.Party.PartySizeLimit;

            bool canOverRecruit = settings.EnableGarrisonDonation &&
                                   settlement.Town.GarrisonParty != null &&
                                   settlement.Town.GarrisonParty.MemberRoster.TotalManCount < settings.MaxGarrisonSize;

            if (currentSize >= limit && !canOverRecruit) return orders;

            var recruitmentBehavior = Campaign.Current?.GetCampaignBehavior<TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior>();
            if (recruitmentBehavior != null)
            {
                var data = recruitmentBehavior.GetMercenaryData(settlement.Town);
                if (data != null && data.Number > 0 && data.TroopType != null)
                {
                    var troop = data.TroopType;
                    int count = data.Number;
                    if (RecruitmentFilter.MatchTroopFilter(troop, settings))
                    {
                        int cost = (int)Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, Hero.MainHero, false).ResultNumber;
                        int budget = Hero.MainHero.Gold - 1000;
                        int capacity = limit - currentSize;
                        
                        // If garrison donation is active, we can overrecruit up to the garrison space limit
                        if (canOverRecruit && capacity <= 0)
                        {
                            capacity = settings.MaxGarrisonSize - settlement.Town.GarrisonParty.MemberRoster.TotalManCount;
                        }
                        else if (canOverRecruit)
                        {
                            capacity += (settings.MaxGarrisonSize - settlement.Town.GarrisonParty.MemberRoster.TotalManCount);
                        }

                        int toRecruit = Math.Min(count, capacity);
                        if (cost > 0)
                        {
                            toRecruit = Math.Min(toRecruit, budget / cost);
                        }
                        if (toRecruit > 0)
                        {
                            orders.Add(new MercenaryRecruitOrder(troop, toRecruit));
                        }
                    }
                }
            }

            return orders;
        }
    }
}
