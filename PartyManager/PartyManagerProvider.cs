using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using SettlementAutomationCore;

namespace PartyManager
{
    public class PartyManagerProvider : ITradeOrderProvider, IRecruitOrderProvider, IGarrisonOrderProvider, IRansomOrderProvider
    {
        public string ProviderName => "PartyManager";

        // ----------------------------------------------------
        // ITradeOrderProvider (Mount & Herding Management)
        // ----------------------------------------------------
        public List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<TradeOrder>();
            var settings = Settings.Instance;
            if (settings == null || !settings.PreventHerdingPenalty) return orders;

            CalculatePartyAnimals(party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
                out var ridingItems, out var packItems, out var livestockItems);

            int totalAnimals = riding + pack + livestock;
            int maxAllowed = (infantry * 2) + (cavalry * 1);

            if (totalAnimals > maxAllowed)
            {
                int excess = totalAnimals - maxAllowed;

                // 1. Sell Livestock first
                foreach (var el in livestockItems)
                {
                    if (excess <= 0) break;
                    int toSell = Math.Min(excess, el.Amount);
                    orders.Add(new TradeOrder(el.EquipmentElement, toSell, false));
                    excess -= toSell;
                }

                // 2. Sell excess riding mounts (more riding mounts than foot troops)
                int excessRiding = riding - infantry;
                if (excess > 0 && excessRiding > 0)
                {
                    foreach (var el in ridingItems)
                    {
                        if (excess <= 0 || excessRiding <= 0) break;
                        int available = el.Amount;
                        int toSell = Math.Min(Math.Min(excess, excessRiding), available);
                        orders.Add(new TradeOrder(el.EquipmentElement, toSell, false));
                        excess -= toSell;
                        excessRiding -= toSell;
                    }
                }

                // 3. Sell pack animals if still over herding limit
                if (excess > 0)
                {
                    foreach (var el in packItems)
                    {
                        if (excess <= 0) break;
                        int toSell = Math.Min(excess, el.Amount);
                        orders.Add(new TradeOrder(el.EquipmentElement, toSell, false));
                        excess -= toSell;
                    }
                }
            }

            return orders;
        }

        public List<TradeOrder> GetMainOrders(MobileParty party, Settlement settlement, InventoryLogic currentLogic)
        {
            var orders = new List<TradeOrder>();
            var settings = Settings.Instance;
            if (settings == null) return orders;

            // Herding Protection (Phase 3)
            if (settings.PreventHerdingPenalty)
            {
                orders.AddRange(GetPreSellOrders(party, settlement));
            }

            // Auto-Buy Mounts (Phase 3)
            if (settings.AutoBuyMounts)
            {
                CalculatePartyAnimals(party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
                    out _, out _, out _);

                int mountsNeeded = infantry - riding;
                if (mountsNeeded > 0)
                {
                    // Find cheap mounts to buy
                    var otherElements = currentLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory);
                    var buyableMounts = new List<RosterElementInfo>();

                    for (int i = 0; i < otherElements.Count; i++)
                    {
                        var el = otherElements[i];
                        if (el.IsEmpty || el.Amount <= 0) continue;
                        var item = el.EquipmentElement.Item;
                        if (item != null && item.IsMountable && item.HorseComponent != null && !item.HorseComponent.IsPackAnimal)
                        {
                            int price = currentLogic.GetItemPrice(el.EquipmentElement, true);
                            buyableMounts.Add(new RosterElementInfo(el.EquipmentElement, el.Amount, price));
                        }
                    }

                    // Sort mounts by price ascending
                    buyableMounts = buyableMounts.OrderBy(m => m.Price).ToList();

                    int budget = Hero.MainHero.Gold - 1000; // Keep at least 1000 denars reserve
                    foreach (var m in buyableMounts)
                    {
                        if (mountsNeeded <= 0 || budget <= 0) break;
                        int toBuy = Math.Min(mountsNeeded, m.Amount);
                        int cost = toBuy * m.Price;
                        if (cost > budget)
                        {
                            toBuy = budget / m.Price;
                        }
                        if (toBuy > 0)
                        {
                            orders.Add(new TradeOrder(m.EqElement, toBuy, true));
                            mountsNeeded -= toBuy;
                            budget -= toBuy * m.Price;
                        }
                    }
                }
            }

            return orders;
        }

        private class RosterElementInfo
        {
            public EquipmentElement EqElement { get; }
            public int Amount { get; }
            public int Price { get; }
            public RosterElementInfo(EquipmentElement eqElement, int amount, int price)
            {
                EqElement = eqElement;
                Amount = amount;
                Price = price;
            }
        }

        private void CalculatePartyAnimals(MobileParty party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
            out List<ItemRosterElementInfo> ridingItems, out List<ItemRosterElementInfo> packItems, out List<ItemRosterElementInfo> livestockItems)
        {
            infantry = 0;
            cavalry = 0;
            riding = 0;
            pack = 0;
            livestock = 0;

            ridingItems = new List<ItemRosterElementInfo>();
            packItems = new List<ItemRosterElementInfo>();
            livestockItems = new List<ItemRosterElementInfo>();

            // Count Troops
            var memberRoster = party.MemberRoster;
            for (int i = 0; i < memberRoster.Count; i++)
            {
                var element = memberRoster.GetElementCopyAtIndex(i);
                if (element.Character != null)
                {
                    if (element.Character.IsMounted)
                    {
                        cavalry += element.Number;
                    }
                    else
                    {
                        infantry += element.Number;
                    }
                }
            }

            // Count Animals in inventory
            var itemRoster = party.ItemRoster;
            for (int i = 0; i < itemRoster.Count; i++)
            {
                var el = itemRoster.GetElementCopyAtIndex(i);
                var item = el.EquipmentElement.Item;
                if (item != null)
                {
                    if (item.IsAnimal)
                    {
                        livestock += el.Amount;
                        livestockItems.Add(new ItemRosterElementInfo(el.EquipmentElement, el.Amount));
                    }
                    else if (item.IsMountable && item.HorseComponent != null)
                    {
                        if (item.HorseComponent.IsPackAnimal)
                        {
                            pack += el.Amount;
                            packItems.Add(new ItemRosterElementInfo(el.EquipmentElement, el.Amount));
                        }
                        else
                        {
                            riding += el.Amount;
                            ridingItems.Add(new ItemRosterElementInfo(el.EquipmentElement, el.Amount));
                        }
                    }
                }
            }
        }

        private class ItemRosterElementInfo
        {
            public EquipmentElement EquipmentElement { get; }
            public int Amount { get; }
            public ItemRosterElementInfo(EquipmentElement eq, int amount)
            {
                EquipmentElement = eq;
                Amount = amount;
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
                int slotsCount = Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(Hero.MainHero, notable, -101);
                for (int slot = 0; slot < slotsCount; slot++)
                {
                    if (slot >= notable.VolunteerTypes.Length) break;
                    var troop = notable.VolunteerTypes[slot];
                    if (troop == null) continue;

                    if (MatchTroopFilter(troop, settings))
                    {
                        orders.Add(new RecruitOrder(notable, slot));
                    }
                }
            }

            return orders;
        }

        private bool MatchTroopFilter(CharacterObject troop, Settings settings)
        {
            // Noble / Regular Troop Class check
            var leafTroops = new List<CharacterObject>();
            GetLeafTroops(troop, leafTroops);
            int maxLeafTier = leafTroops.Count > 0 ? leafTroops.Max(l => l.Tier) : troop.Tier;

            bool isNoble = maxLeafTier >= 6;
            if (isNoble && !settings.RecruitNoble) return false;
            if (!isNoble && !settings.RecruitRegular) return false;

            // Combat Mounted / Foot check
            bool isMounted = troop.IsMounted;
            if (isMounted && !settings.RecruitMounted) return false;
            if (!isMounted && !settings.RecruitFoot) return false;

            // Tier evaluation (final leaf evaluation or purchase evaluation)
            if (settings.EvalTimeSetting == EvalTime.FinalUpgradeTier)
            {
                if (leafTroops.Count == 0) return false;

                // Require at least one final upgrade leaf to match tier and combat archetype filters
                bool leafMatched = false;
                foreach (var leaf in leafTroops)
                {
                    if (leaf.Tier >= settings.MinRecruitTier && leaf.Tier <= settings.MaxRecruitTier)
                    {
                        if (MatchArchetype(leaf, settings))
                        {
                            leafMatched = true;
                            break;
                        }
                    }
                }
                if (!leafMatched) return false;
            }
            else
            {
                // Purchase time evaluation
                if (troop.Tier < settings.MinRecruitTier || troop.Tier > settings.MaxRecruitTier) return false;
                if (!MatchArchetype(troop, settings)) return false;
            }

            return true;
        }

        private bool MatchArchetype(CharacterObject troop, Settings settings)
        {
            // Crossbowmen
            if (troop.GetSkillValue(DefaultSkills.Crossbow) > 30)
            {
                return settings.RecruitCrossbow;
            }
            // Archers
            if (troop.GetSkillValue(DefaultSkills.Bow) > 30)
            {
                return settings.RecruitBow;
            }
            // Skirmishers (throwing)
            if (troop.GetSkillValue(DefaultSkills.Throwing) > 30)
            {
                return settings.RecruitThrowing;
            }
            // Melee shield/combat
            bool hasShield = false;
            if (troop.Equipment != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var weapon = troop.Equipment[i];
                    if (weapon.Item != null && weapon.Item.PrimaryWeapon != null && weapon.Item.PrimaryWeapon.IsShield)
                    {
                        hasShield = true;
                        break;
                    }
                }
            }
            if (hasShield)
            {
                return settings.RecruitShield || settings.RecruitMelee;
            }

            return settings.RecruitMelee;
        }

        private static void GetLeafTroops(CharacterObject troop, List<CharacterObject> leafTroops)
        {
            if (troop.UpgradeTargets == null || troop.UpgradeTargets.Length == 0)
            {
                leafTroops.Add(troop);
                return;
            }
            foreach (var target in troop.UpgradeTargets)
            {
                if (target != null)
                {
                    GetLeafTroops(target, leafTroops);
                }
            }
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
        // IRansomOrderProvider (Tavern Mercenaries)
        // ----------------------------------------------------
        public List<RansomOrder> GetRansomOrders(MobileParty party, Settlement settlement)
        {
            return new List<RansomOrder>(); // Prisoner ransoms handled by PrisonerManager
        }

        public List<MercenaryRecruitOrder> GetMercenaryRecruitOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<MercenaryRecruitOrder>();
            var settings = Settings.Instance;
            if (settings == null || !settings.RecruitMercenary || settlement.Town == null) return orders;

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
                    if (MatchTroopFilter(troop, settings))
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
