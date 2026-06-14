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
        public void SubmitAutomationRequests(AutomationRequestContext context)
        {
            var settings = Settings.Instance;
            if (settings != null)
            {
                TradeHelper.SubmitAutomationRequests(context.Party, settings);
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

            // Stop recruiting at the configured party-size target, plus any garrison donation space.
            int currentSize = party.MemberRoster.TotalManCount;
            int maxRecruitableSize = GetMaxRecruitablePartySize(party, settlement, settings);
            int remainingRecruitSlots = Math.Max(0, maxRecruitableSize - currentSize);
            if (remainingRecruitSlots <= 0) return orders;

            // Cycle through settlement notables
            foreach (var notable in settlement.Notables)
            {
                if (remainingRecruitSlots <= 0) break;
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
                        remainingRecruitSlots--;
                        if (remainingRecruitSlots <= 0) break;
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
            var mainHero = Hero.MainHero;
            if (mainHero == null) return orders;
            var campaign = Campaign.Current;
            if (campaign == null) return orders;

            int currentSize = party.MemberRoster.TotalManCount;
            int maxRecruitableSize = GetMaxRecruitablePartySize(party, settlement, settings);
            int capacity = Math.Max(0, maxRecruitableSize - currentSize);
            if (capacity <= 0) return orders;

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
                        int cost = (int)campaign.Models.PartyWageModel.GetTroopRecruitmentCost(troop, mainHero, false).ResultNumber;
                        int budget = mainHero.Gold - 1000;
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

        private static int GetMaxRecruitablePartySize(MobileParty party, Settlement settlement, Settings settings)
        {
            int partyLimit = party.Party.PartySizeLimit;
            int normalLimit = (int)Math.Ceiling(partyLimit * Math.Max(1, Math.Min(100, settings.RecruitUpToPartySizePercent)) / 100.0);
            normalLimit = Math.Min(partyLimit, normalLimit);
            if (normalLimit < partyLimit)
            {
                return normalLimit;
            }

            return partyLimit + GetAvailableGarrisonDonationSpace(settlement, settings);
        }

        private static int GetAvailableGarrisonDonationSpace(Settlement settlement, Settings settings)
        {
            if (!settings.EnableGarrisonDonation || settlement.Town?.GarrisonParty == null)
            {
                return 0;
            }

            int garrisonSize = settlement.Town.GarrisonParty.MemberRoster.TotalManCount;
            return Math.Max(0, settings.MaxGarrisonSize - garrisonSize);
        }
    }
}
